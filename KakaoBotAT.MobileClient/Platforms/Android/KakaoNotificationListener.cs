using Android.App;
using Android.Content;
using Android.Service.Notification;
using Android.Util;
using Android.OS;
using KakaoBotAT.Commons;

namespace KakaoBotAT.MobileClient.Platforms.Android;

[Service(
    Label = "KakaoNotificationListener",
    Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE",
    Exported = true)]
[IntentFilter(["android.service.notification.NotificationListenerService"])]
public class KakaoNotificationListener : NotificationListenerService
{
    public static event EventHandler<KakaoMessageData>? NotificationReceived;

    private static readonly Dictionary<string, Notification.Action?[]> ReplyActions = [];

    public override void OnNotificationPosted(StatusBarNotification? sbn)
    {
        // KakaoTalk
        if (sbn == null || sbn?.PackageName != "com.kakao.talk")
            return;

        var notification = sbn.Notification;
        if (notification == null) return;

        var extras = notification.Extras;
        if (extras == null) return;

        // MessagingStyle only
        var style = extras.GetString(Notification.ExtraTemplate);
        if (style != "android.app.Notification$MessagingStyle") 
            return;

        var senderName = extras.GetString(Notification.ExtraTitle) ?? "Unknown";
        var roomName = extras.GetString(Notification.ExtraSubText) ?? extras.GetString(Notification.ExtraSummaryText) ?? senderName;
        var messageText = extras.GetCharSequence(Notification.ExtraText)?.ToString();
        var isGroupChat = extras.GetBoolean(Notification.ExtraIsGroupConversation);
        var roomId = sbn.Tag;
        var logId = extras?.GetLong("chatLogId").ToString();

        Bundle? messageBundle;
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) messageBundle = extras?.GetParcelableArray(Notification.ExtraMessages, Java.Lang.Class.FromType(typeof(Bundle)))?.Cast<Bundle>().FirstOrDefault();
        else messageBundle = extras?.GetParcelableArray(Notification.ExtraMessages)?.Cast<Bundle>().FirstOrDefault();

        Person? senderPerson;
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var senderPersonObj = messageBundle?.GetParcelable("sender_person", Java.Lang.Class.FromType(typeof(Person)));
            senderPerson = senderPersonObj as Person;
        }
        else senderPerson = (Person?)messageBundle?.GetParcelable("sender_person");

        var senderHash = senderPerson?.Key;

        if (roomId == null || senderHash == null || messageText == null) return;

        Notification.Action? replyAction = null;
        Notification.Action? readAction = null;

        var actions = notification.Actions ?? [];

        foreach (var action in actions)
        {
            var title = action.Title?.ToString().ToLowerInvariant();

            var remoteInputs = action.GetRemoteInputs();

            // Reply (With RemoteInputs & title with 'reply' or 'Reply')
            if (remoteInputs != null && remoteInputs.Length > 0 &&
                (title == "reply" || title == "답장"))
            {
                replyAction = action;
            }

            // Read (Without RemoteInputs & title with 'read' or 'Read')
            else if (remoteInputs == null || remoteInputs.Length == 0)
            {
                if (title == "read" || title == "읽음")
                {
                    readAction = action;
                }
            }
        }

        if (replyAction != null)
        {
            ReplyActions[roomId] = [readAction, replyAction];

            var data = new KakaoMessageData
            {
                RoomName = roomName,
                RoomId = roomId,
                SenderName = senderName,
                SenderHash = senderHash,
                Content = messageText,
                LogId = logId ?? string.Empty,
                IsGroupChat = isGroupChat,
                Time = sbn.PostTime
            };

            MainThread.BeginInvokeOnMainThread(() => NotificationReceived?.Invoke(this, data));
        }
    }

    public override void OnNotificationRemoved(StatusBarNotification? sbn)
    {
        if (sbn?.PackageName == "com.kakao.talk" && sbn?.Tag != null)
        {
            ReplyActions.Remove(sbn.Tag);
        }
    }

    public static bool SendReply(string roomId, string message)
    {
        if (!ReplyActions.TryGetValue(roomId, out var actions)) return false;

        var replyAction = actions[1];
        if (replyAction == null) return false;

        var intent = new Intent();
        var bundle = new Bundle();

        var remoteInputs = replyAction.GetRemoteInputs();
        if (remoteInputs == null || remoteInputs.Length == 0) return false;

        foreach (var input in remoteInputs) bundle.PutCharSequence(input.ResultKey, message);
        RemoteInput.AddResultsToIntent(remoteInputs, intent, bundle);

        try
        {
            replyAction.ActionIntent?.Send(Platform.CurrentActivity, Result.Canceled, intent);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("KakaoBot", $"SendReply failed: {ex.Message}");
            return false;
        }
    }

    public static bool MarkAsRead(string roomId)
    {
        if (!ReplyActions.TryGetValue(roomId, out var actions)) return false;

        var readAction = actions[0];
        if (readAction == null) return false;

        try
        {
            readAction.ActionIntent?.Send(Platform.CurrentActivity, Result.FirstUser, new Intent());
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("KakaoBot", $"MarkAsRead failed: {ex.Message}");
            return false;
        }
    }
}