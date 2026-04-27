using System;
using System.Collections.Generic;
using System.Text;

namespace Dogebot.MobileClient;

public interface IKakaoBotService
{
    bool IsNotificationServiceEnabled();
    void OpenNotificationSettings();

    bool IsIgnoringBatteryOptimizations();
    void RequestIgnoreBatteryOptimizations();

    void AcquirePartialWakeLock();
    void ReleasePartialWakeLock();
    
    Task<bool> SendReplyAsync(string roomId, string message);
    Task<bool> MarkAsReadAsync(string roomId);
}
