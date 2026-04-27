using Android.App;
using Android.Runtime;

[assembly: UsesPermission(Android.Manifest.Permission.Internet)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessNetworkState)]
[assembly: UsesPermission(Android.Manifest.Permission.BindNotificationListenerService)]
[assembly: UsesPermission(Android.Manifest.Permission.RequestIgnoreBatteryOptimizations)]
[assembly: UsesPermission(Android.Manifest.Permission.WakeLock)]

namespace Dogebot.MobileClient.Platforms.Android;

[Application(
    Theme = "@style/Maui.SplashTheme",
    Icon = "@mipmap/appicon",
    RoundIcon = "@mipmap/appicon_round",
    SupportsRtl = true,
    AllowBackup = false)]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

