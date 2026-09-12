using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Net;
using System.Text.Json;
using System.Text;
using Dogebot.Commons;
using Dogebot.MobileClient.Platforms.Android;

namespace Dogebot.MobileClient.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string ServerAddressPreferenceKey = "ServerAddress";
    private const string ApiKeyPreferenceKey = "ApiKey";
    private const string BotRunningPreferenceKey = "IsBotRunning";
    private readonly IKakaoBotService _kakaoBotService;
    private readonly HttpClient _httpClient;

    [ObservableProperty]
    private string serverAddress = string.Empty;

    [ObservableProperty]
    private string apiKey = string.Empty;

    [ObservableProperty]
    private string logText = "Waiting for bot to start...";

    [ObservableProperty]
    private string notificationStatus = "🚫 Notification Listener Disabled";

    [ObservableProperty]
    private string batteryOptStatus = "⚡ Battery Optimization Enabled";

    [ObservableProperty]
    private bool isBotRunning = false;

    // Background polling cancellation token source
    private CancellationTokenSource? _pollingCts;

    // Suppress saving while the initial API key load from SecureStorage is in progress
    private bool _isApiKeyLoaded;

    public MainViewModel(IKakaoBotService kakaoBotService)
    {
        _kakaoBotService = kakaoBotService;
        _httpClient = new HttpClient();

        // Load saved server address or use default
        ServerAddress = Preferences.Get(ServerAddressPreferenceKey, Constants.ServerEndpointUrl);

        // Load the API key from SecureStorage and resume the bot afterwards
        _ = InitializeAsync();

        KakaoNotificationListener.NotificationReceived += OnKakaoNotificationReceived;

        UpdateStatuses();
    }

    private async Task InitializeAsync()
    {
        try { ApiKey = await SecureStorage.Default.GetAsync(ApiKeyPreferenceKey) ?? string.Empty; }
        catch (Exception exception) { LogText = $"❌ Failed to load the API key: {exception.Message}"; }
        finally { _isApiKeyLoaded = true; }

        // Resume bot automatically if it was running when the app was force-closed
        if (Preferences.Get(BotRunningPreferenceKey, false)) StartBot();
    }

    partial void OnApiKeyChanged(string value)
    {
        if (!_isApiKeyLoaded) return;
        _ = SaveApiKeyAsync(value);
    }

    private async Task SaveApiKeyAsync(string value)
    {
        try { await SecureStorage.Default.SetAsync(ApiKeyPreferenceKey, value); }
        catch (Exception exception) { LogText = $"❌ Failed to save the API key: {exception.Message}"; }
    }

    // Update notification and battery optimization statuses
    [RelayCommand]
    private void UpdateStatuses()
    {
        NotificationStatus = _kakaoBotService.IsNotificationServiceEnabled() ? "✅ Notification Listener Enabled" : "🚫 Notification Listener Disabled";

        BatteryOptStatus = _kakaoBotService.IsIgnoringBatteryOptimizations() ? "👍 Battery Optimization Exempted" : "⚡ Battery Optimization Enabled";

        if (IsBotRunning) _kakaoBotService.AcquirePartialWakeLock();
        else _kakaoBotService.ReleasePartialWakeLock();
    }

    [RelayCommand]
    private void OpenNotificationSettings()
    {
        _kakaoBotService.OpenNotificationSettings();
        LogText = "Navigating to notification access settings. Please press 'Update Status' after configuring.";
    }
    
    [RelayCommand]
    private void RequestBatteryOptimizationExemption()
    {
        _kakaoBotService.RequestIgnoreBatteryOptimizations();
        LogText = "Navigating to battery optimization exemption screen. Please press 'Update Status' after configuring.";
    }

    [RelayCommand]
    private void ToggleBotRunning()
    {
        if (IsBotRunning) StopBot();
        else StartBot();
    }

    private void StartBot()
    {
        UpdateStatuses(); // Check statuses before starting

        if (!_kakaoBotService.IsNotificationServiceEnabled() || !_kakaoBotService.IsIgnoringBatteryOptimizations())
        {
            LogText = "❌ Bot start failed: Both notification permission and battery optimization exemption are required.";
            return;
        }

        // Save server address when bot starts
        Preferences.Set(ServerAddressPreferenceKey, ServerAddress);

        IsBotRunning = true;
        Preferences.Set(BotRunningPreferenceKey, true);
        LogText = $"✅ Bot started. Wakelock acquired. Server: {ServerAddress}";
        _kakaoBotService.AcquirePartialWakeLock();

        // Run polling loop detached so the toggle command completes and the button stays enabled
        _pollingCts = new CancellationTokenSource();
        _ = Task.Run(() => StartPollingServer(_pollingCts.Token));
    }

    private void StopBot()
    {
        _pollingCts?.Cancel();
        _pollingCts = null;
        IsBotRunning = false;
        Preferences.Set(BotRunningPreferenceKey, false);
        LogText = "Bot stopped. Wakelock released.";
        _kakaoBotService.ReleasePartialWakeLock();
    }

    // Process incoming KakaoTalk notifications
    private async void OnKakaoNotificationReceived(object? sender, KakaoMessageData data)
    {
        if (!IsBotRunning) return;

        LogText = $"[IN] Room: {data.RoomName} / Sender: {data.SenderName} / Content: {data.Content}";

        var notification = new ServerNotification { Data = data };
        var json = JsonSerializer.Serialize(notification);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{ServerAddress}/notify") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            ApplyApiKeyHeader(request);

            using var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var serverResponse = JsonSerializer.Deserialize<ServerResponse>(responseJson);

                if (serverResponse is not null) await ExecuteServerResponseAsync(serverResponse, "[OUT]", data.RoomName);
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                LogText = "❌ Authentication failed: Check the API key.";
            }
            else
            {
                LogText = $"❌ Server response error: Status code {response.StatusCode}";
            }
        }
        catch (Exception exception)
        {
            LogText = $"❌ Server communication error: {exception.Message}";
        }
    }

    private async Task StartPollingServer(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var availableRooms = KakaoNotificationListener.GetAvailableRoomIds();
                var roomsParam = availableRooms.Count > 0 ? $"?availableRooms={Uri.EscapeDataString(string.Join(",", availableRooms))}" : "";

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ServerAddress}/command{roomsParam}");
                ApplyApiKeyHeader(request);

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    var serverResponse = JsonSerializer.Deserialize<ServerResponse>(responseJson);

                    if (serverResponse != null) await ExecuteServerResponseAsync(serverResponse, "[CMD]", serverResponse.RoomId);
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    LogText = "❌ Authentication failed: Check the API key.";
                }
                else
                {
                    LogText = $"❌ Polling response error: Status code {response.StatusCode}";
                }
            }
            catch (TaskCanceledException)
            {
                // Bot stopped, exit polling loop
                break;
            }
            catch (Exception exception)
            {
                LogText = $"❌ Error during polling: {exception.Message}";
            }

            // Poll every 5 seconds
            await Task.Delay(5000, cancellationToken);
        }
    }

    private void ApplyApiKeyHeader(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            request.Headers.TryAddWithoutValidation(ApiKeyAuthenticationDefaults.HeaderName, ApiKey);
        }
    }

    private async Task ExecuteServerResponseAsync(ServerResponse serverResponse, string logPrefix, string defaultTargetName)
    {
        var responseItems = serverResponse.Items.Count > 0 ? serverResponse.Items : CreateSingleResponseItemList(serverResponse);

        foreach (var responseItem in responseItems)
        {
            var targetName = responseItems.Count == 1 ? defaultTargetName : responseItem.RoomId;

            if (responseItem.Action == "send_text" && !string.IsNullOrEmpty(responseItem.Message))
            {
                LogText = $"{logPrefix} Attempting to reply to {targetName}: {responseItem.Message}";
                var success = await _kakaoBotService.SendReplyAsync(responseItem.RoomId, responseItem.Message);
                LogText += success ? " (Success)" : " (Failed: Notification action may have disappeared)";
            }
            else if (responseItem.Action == "read")
            {
                LogText = $"{logPrefix} Attempting to mark {targetName} as read";
                var success = await _kakaoBotService.MarkAsReadAsync(responseItem.RoomId);
                LogText += success ? " (Success)" : " (Failed: Notification action may have disappeared)";
            }
        }
    }

    private static List<ServerResponseItem> CreateSingleResponseItemList(ServerResponse serverResponse) =>
    [
        new ServerResponseItem
        {
            Action = serverResponse.Action,
            RoomId = serverResponse.RoomId,
            Message = serverResponse.Message
        }
    ];
}
