using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Android.Util;
using Android.Views;
using Dogebot.Commons;

namespace Dogebot.MobileClient.Platforms.Android;

public class KakaoBotService : IKakaoBotService
{
    private PowerManager.WakeLock? _wakeLock;

    public bool IsNotificationServiceEnabled()
    {
        var cn = new ComponentName(global::Android.App.Application.Context, Java.Lang.Class.FromType(typeof(KakaoNotificationListener)).Name);
        string? flat = Settings.Secure.GetString(global::Android.App.Application.Context.ContentResolver, "enabled_notification_listeners");

        return flat != null && flat.Contains(cn.FlattenToString());
    }

    public void OpenNotificationSettings()
    {
        var intent = new Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS");
        intent.SetFlags(ActivityFlags.NewTask);
        Platform.CurrentActivity?.StartActivity(intent);
    }

    public bool IsIgnoringBatteryOptimizations()
    {
        var pm = (PowerManager?)global::Android.App.Application.Context.GetSystemService(Context.PowerService);
        return pm?.IsIgnoringBatteryOptimizations(global::Android.App.Application.Context.PackageName) ?? false;
    }

    public void RequestIgnoreBatteryOptimizations()
    {
        var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations,
            global::Android.Net.Uri.Parse($"package:{(global::Android.App.Application.Context.PackageName)}"));
        intent.SetFlags(ActivityFlags.NewTask);
        Platform.CurrentActivity?.StartActivity(intent);
    }

    public void AcquirePartialWakeLock()
    {
        var pm = (PowerManager?)global::Android.App.Application.Context.GetSystemService(Context.PowerService);
        _wakeLock ??= pm?.NewWakeLock(WakeLockFlags.Partial, "Dogebot.WakelockTag");

        if (_wakeLock != null && !_wakeLock.IsHeld)
        {
            _wakeLock.Acquire();
            Log.Info("KakaoBot", "Partial WakeLock Acquired.");
        }
    }

    public void ReleasePartialWakeLock()
    {
        if (_wakeLock?.IsHeld == true)
        {
            _wakeLock.Release();
            Log.Info("KakaoBot", "Partial WakeLock Released.");
        }
    }

    public Task<bool> SendReplyAsync(string roomId, string message) => Task.FromResult(KakaoNotificationListener.SendReply(roomId, message));

    public Task<bool> MarkAsReadAsync(string roomId) => Task.FromResult(KakaoNotificationListener.MarkAsRead(roomId));
}
