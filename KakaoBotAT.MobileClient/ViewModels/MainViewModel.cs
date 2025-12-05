using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;
using System.Text;
using KakaoBotAT.Commons;
using KakaoBotAT.MobileClient.Platforms.Android;

namespace KakaoBotAT.MobileClient.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string ServerAddressPreferenceKey = "ServerAddress";
    private readonly IKakaoBotService _kakaoBotService;
    private readonly HttpClient _httpClient;

    [ObservableProperty]
    private string serverAddress = string.Empty;

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

    public MainViewModel(IKakaoBotService kakaoBotService)
    {
        _kakaoBotService = kakaoBotService;
        _httpClient = new HttpClient();

        // Load saved server address or use default
        ServerAddress = Preferences.Get(ServerAddressPreferenceKey, Constants.ServerEndpointUrl);

        KakaoNotificationListener.NotificationReceived += OnKakaoNotificationReceived;

        UpdateStatuses();
    }

    // Update notification and battery optimization statuses
    [RelayCommand]
    private void UpdateStatuses()
    {
        NotificationStatus = _kakaoBotService.IsNotificationServiceEnabled()
            ? "✅ Notification Listener Enabled"
            : "🚫 Notification Listener Disabled";

        BatteryOptStatus = _kakaoBotService.IsIgnoringBatteryOptimizations()
            ? "👍 Battery Optimization Exempted"
            : "⚡ Battery Optimization Enabled";

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
    private async Task ToggleBotRunning()
    {
        if (IsBotRunning)
        {
            // Stop
            _pollingCts?.Cancel();
            _pollingCts = null;
            IsBotRunning = false;
            LogText = "Bot stopped. Wakelock released.";
            _kakaoBotService.ReleasePartialWakeLock();
        }
        else
        {
            // Start
            UpdateStatuses(); // Check statuses before starting

            if (!_kakaoBotService.IsNotificationServiceEnabled() || !_kakaoBotService.IsIgnoringBatteryOptimizations())
            {
                LogText = "❌ Bot start failed: Both notification permission and battery optimization exemption are required.";
                return;
            }

            // Save server address when bot starts
            Preferences.Set(ServerAddressPreferenceKey, ServerAddress);

            IsBotRunning = true;
            LogText = $"✅ Bot started. Wakelock acquired. Server: {ServerAddress}";
            _kakaoBotService.AcquirePartialWakeLock();

            _pollingCts = new CancellationTokenSource();
            await Task.Run(() => StartPollingServer(_pollingCts.Token));
        }
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
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{ServerAddress}/notify", content);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var serverResponse = JsonSerializer.Deserialize<ServerResponse>(responseJson);

                if (serverResponse?.Action == "send_text" && !string.IsNullOrEmpty(serverResponse.Message))
                {
                    LogText = $"[OUT] Attempting to reply to {data.RoomName}: {serverResponse.Message}";
                    bool success = await _kakaoBotService.SendReplyAsync(data.RoomId, serverResponse.Message);
                    LogText += success ? " (Success)" : " (Failed: Notification action may have disappeared)";
                }
                else if (serverResponse?.Action == "read")
                {
                    LogText = $"[OUT] Attempting to mark {data.RoomName} as read";
                    bool success = await _kakaoBotService.MarkAsReadAsync(data.RoomId);
                    LogText += success ? " (Success)" : " (Failed: Notification action may have disappeared)";
                }
            }
            else
            {
                LogText = $"❌ Server response error: Status code {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            LogText = $"❌ Server communication error: {ex.Message}";
        }
    }

    private async Task StartPollingServer(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ServerAddress}/command", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    var serverResponse = JsonSerializer.Deserialize<ServerResponse>(responseJson);

                    if (serverResponse != null)
                    {
                        if (serverResponse.Action == "send_text" && !string.IsNullOrEmpty(serverResponse.Message))
                        {
                            LogText = $"[CMD] Attempting to reply to {serverResponse.RoomId}: {serverResponse.Message}";
                            await _kakaoBotService.SendReplyAsync(serverResponse.RoomId, serverResponse.Message);
                        }
                        else if (serverResponse.Action == "read")
                        {
                            LogText = $"[CMD] Attempting to mark {serverResponse.RoomId} as read";
                            await _kakaoBotService.MarkAsReadAsync(serverResponse.RoomId);
                        }
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // No command (usually)
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
            catch (Exception ex)
            {
                LogText = $"❌ Error during polling: {ex.Message}";
            }

            // Poll every 5 seconds
            await Task.Delay(5000, cancellationToken);
        }
    }
}