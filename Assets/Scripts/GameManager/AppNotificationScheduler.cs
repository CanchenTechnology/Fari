using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;
using Newtonsoft.Json.Linq;
using UnityEngine;
using XFGameFrameWork;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

/// <summary>
/// 通知调度入口。Editor 中会降级为本地记录和调试提示；
/// Android/iOS 真机上会通过 Unity Mobile Notifications 调度本地系统通知。
/// </summary>
public class AppNotificationScheduler : MonoSingleton<AppNotificationScheduler>
{
    private const string ChannelId = "moonly_reminders";
    private const string ScheduledPrefix = "Notif_Scheduled_";
    private const string SeenPrefix = "Notif_Seen_";
    private const string LastSyncSummaryKey = "Notif_LastSyncSummary";

    private const int DailyOracleNotificationId = 910001;
    private const int DivinationReturnNotificationId = 910002;
    private const int ActivitySystemNotificationId = 910003;
    private const int DialogueReplyNotificationId = 910004;
    private const int FriendRequestNotificationId = 910101;
    private const int RelationshipInviteNotificationId = 910102;
    private const int FriendDailyOracleNotificationId = 910103;
    private const int DiagnosticNotificationId = 910999;
    private const int TomorrowHookNotificationBaseId = 920000;
    private const string ScheduledIdListKey = "Notif_ScheduledIds";

    private static readonly int[] KnownNotificationIds =
    {
        DailyOracleNotificationId,
        DivinationReturnNotificationId,
        ActivitySystemNotificationId,
        DialogueReplyNotificationId,
        FriendRequestNotificationId,
        RelationshipInviteNotificationId,
        FriendDailyOracleNotificationId,
        DiagnosticNotificationId,
    };

    private bool notificationApiInitialized;
    private bool permissionRequested;
    private bool notificationOpenHandled;
    private string lastHandledNotificationPayload;

    public string LastSyncSummary => PlayerPrefs.GetString(LastSyncSummaryKey, string.Empty);

    protected override void Awake()
    {
        base.Awake();
        InitializeUnityNotificationsApi();
        StartCoroutine(HandleLaunchNotificationNextFrame());
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            SyncFromCurrentSettings();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            StartCoroutine(HandleLaunchNotificationNextFrame());
        }
    }

    private void OnDestroy()
    {
        UnsubscribeNotificationCallbacks();
    }

    public bool ScheduleDiagnosticNotification(int delaySeconds = 10)
    {
        delaySeconds = Mathf.Clamp(delaySeconds, 5, 300);
        RequestPermissionIfNeeded();
        return ScheduleNotification(
            DiagnosticNotificationId,
            "FariApp 通知测试",
            "如果你看到这条通知，系统通知链路已经可以工作。",
            DateTime.Now.AddSeconds(delaySeconds),
            false,
            "diagnostic_test");
    }

    public string BuildScheduledDebugSummary()
    {
        List<int> ids = ReadScheduledIds();
        if (ids.Count == 0)
            return "no scheduled notifications recorded";

        List<string> lines = new List<string>();
        foreach (int id in ids)
        {
            string value = PlayerPrefs.GetString(ScheduledPrefix + id, string.Empty);
            if (string.IsNullOrWhiteSpace(value)) continue;

            string[] parts = value.Split('|');
            string title = parts.Length > 0 ? parts[0] : "untitled";
            string fireTimeText = parts.Length > 2 && DateTime.TryParse(parts[2], null, DateTimeStyles.RoundtripKind, out DateTime fireTime)
                ? fireTime.ToString("yyyy-MM-dd HH:mm:ss")
                : "unknown time";
            string repeat = parts.Length > 3 ? parts[3] : "unknown";
            string mode = parts.Length > 4 ? parts[4] : "unknown";
            lines.Add($"#{id} {title} @ {fireTimeText} ({repeat}, {mode})");
        }

        return lines.Count == 0
            ? "no scheduled notifications recorded"
            : string.Join("; ", lines);
    }

    public void SyncFromCurrentSettings()
    {
        SyncFromSettings(NotificationSettingsManager.Instance);
    }

    public void SyncFromSettings(NotificationSettingsManager settings)
    {
        if (settings == null) return;

        CancelKnownScheduledNotifications();

        List<string> scheduled = new List<string>();
        bool hasAnyEnabled = settings.DailyOracleEnabled
            || settings.DialogueReplyEnabled
            || settings.DivinationReturnEnabled
            || settings.FriendInteractionEnabled
            || settings.ActivitySystemEnabled;

        if (hasAnyEnabled)
            RequestPermissionIfNeeded();

        if (settings.DailyOracleEnabled)
        {
            DateTime fireTime = NextTimeTodayOrTomorrow(settings.ReminderTime);
            ScheduleNotification(
                DailyOracleNotificationId,
                "今日神谕已准备好",
                "抽一张每日牌，给今天留一个清晰的开场。",
                fireTime,
                true,
                "daily_oracle");
            scheduled.Add($"每日神谕 {fireTime:HH:mm}");
        }

        if (settings.DivinationReturnEnabled)
        {
            string returnTime = GetReturnReminderTime(settings.ReminderTime);
            DateTime fireTime = NextTimeTodayOrTomorrow(returnTime);
            ScheduleNotification(
                DivinationReturnNotificationId,
                "回看一次占卜指引",
                "昨天的问题也许有了新的答案，回来看看牌面里的线索。",
                fireTime,
                true,
                "divination_return");
            scheduled.Add($"占卜回访 {fireTime:HH:mm}");
        }

        if (settings.ActivitySystemEnabled)
        {
            DateTime fireTime = NextWeeklyActivityTime();
            ScheduleNotification(
                ActivitySystemNotificationId,
                "FariApp 有新的能量更新",
                "查看最新动态、好友同步和系统消息。",
                fireTime,
                true,
                "activity_system");
            scheduled.Add($"活动系统 {fireTime:MM-dd HH:mm}");
        }

        string summary = scheduled.Count == 0
            ? "所有通知提醒已关闭"
            : string.Join("；", scheduled);
        PlayerPrefs.SetString(LastSyncSummaryKey, summary);
        PlayerPrefs.Save();
        Debug.Log($"[AppNotificationScheduler] {summary}");
    }

    public void NotifyDialogueReplyReady(string preview)
    {
        if (!IsDialogueReplyEnabled()) return;

        if (Application.isFocused)
            return;

        string text = string.IsNullOrWhiteSpace(preview)
            ? "神谕师有新的回复，回来继续这段对话。"
            : TrimForNotification(preview, 80);
        string key = BuildSeenKey("dialogue_reply", string.IsNullOrEmpty(text) ? 0 : text.GetHashCode());
        if (HasSeenToday(key)) return;

        MarkSeen(key);
        ScheduleImmediate(DialogueReplyNotificationId, "神谕师回复了你", text, "dialogue_reply");
    }

    public void NotifyFriendRequestCount(int count)
    {
        if (count <= 0 || !IsFriendInteractionEnabled()) return;

        string key = BuildSeenKey("friend_request", count);
        if (HasSeenToday(key)) return;

        MarkSeen(key);
        string text = count == 1
            ? "你有 1 条新的好友请求待确认。"
            : $"你有 {count} 条新的好友请求待确认。";
        ScheduleImmediate(FriendRequestNotificationId, "新的好友请求", text, "friend_request");
    }

    public void NotifyRelationshipInviteCount(int count)
    {
        if (count <= 0 || !IsFriendInteractionEnabled()) return;

        string key = BuildSeenKey("relationship_invite", count);
        if (HasSeenToday(key)) return;

        MarkSeen(key);
        string text = count == 1
            ? "有好友邀请你进行双人关系占卜。"
            : $"你有 {count} 个双人关系占卜邀请待加入。";
        ScheduleImmediate(RelationshipInviteNotificationId, "双人关系占卜邀请", text, "relationship_invite");
    }

    public void NotifyFriendDailyOracleCount(int count)
    {
        if (count <= 0 || !IsFriendInteractionEnabled()) return;

        string key = BuildSeenKey("friend_daily_oracle", count);
        if (HasSeenToday(key)) return;

        MarkSeen(key);
        string text = count == 1
            ? "有好友同步了今天的每日牌摘要。"
            : $"{count} 位好友同步了今天的每日牌摘要。";
        ScheduleImmediate(FriendDailyOracleNotificationId, "好友每日牌动态", text, "friend_daily_oracle");
    }

    public void NotifyTomorrowHookCount(int count)
    {
        if (count <= 0) return;

        string key = BuildSeenKey("tomorrow_hook_due", count);
        if (HasSeenToday(key)) return;

        MarkSeen(key);
        string text = count == 1
            ? "昨天保存的线索已经可以回看。"
            : $"有 {count} 条明日线索可以回看。";
        ScheduleImmediate(TomorrowHookNotificationBaseId, "明日线索已到期", text, "tomorrow_hook_due");
    }

    public void HandleRemotePushData(IDictionary<string, string> data)
    {
        if (data == null || data.Count == 0)
            return;

        NotificationRoute route = NotificationRoute.FromDictionary(data);
        HandleNotificationRoute(route);
    }

    public void ScheduleTomorrowHookReminder(TomorrowHook hook)
    {
        if (hook == null || string.IsNullOrWhiteSpace(hook.hookId)) return;

        string date = string.IsNullOrWhiteSpace(hook.scheduledForLocalDate)
            ? DateTime.Now.AddDays(1).ToString("yyyy-MM-dd")
            : hook.scheduledForLocalDate;
        string time = NotificationSettingsManager.Instance != null
            ? NotificationSettingsManager.Instance.ReminderTime
            : "08:30";

        DateTime fireTime = BuildLocalDateTime(date, time);
        if (fireTime <= DateTime.Now.AddSeconds(10))
            fireTime = DateTime.Now.AddMinutes(1);

        int id = TomorrowHookNotificationBaseId + ((hook.hookId.GetHashCode() & 0x7fffffff) % 9999);
        string text = string.IsNullOrWhiteSpace(hook.triggerText)
            ? "回来看看昨天留下的占卜线索。"
            : hook.triggerText;
        ScheduleNotification(id, "明日线索已到期", TrimForNotification(text, 80), fireTime, false, $"tomorrow_hook:{hook.hookId}");
    }

    private void ScheduleImmediate(int id, string title, string text, string payload)
    {
        NotificationUnreadState.MarkUnread(payload);
        ScheduleNotification(id, title, text, DateTime.Now.AddSeconds(5), false, payload);
        TryEditorToast(text);
    }

    private bool IsFriendInteractionEnabled()
    {
        NotificationSettingsManager settings = NotificationSettingsManager.Instance;
        return settings != null && settings.FriendInteractionEnabled;
    }

    private bool IsDialogueReplyEnabled()
    {
        NotificationSettingsManager settings = NotificationSettingsManager.Instance;
        return settings != null && settings.DialogueReplyEnabled;
    }

    private bool ScheduleNotification(int id, string title, string text, DateTime fireTime, bool repeatDaily, string payload)
    {
        bool scheduledNative = TryScheduleWithUnityNotifications(id, title, text, fireTime, repeatDaily, payload);
        RecordScheduledNotification(id, title, text, fireTime, repeatDaily, scheduledNative);
        return scheduledNative;
    }

    private bool TryScheduleWithUnityNotifications(int id, string title, string text, DateTime fireTime, bool repeatDaily, string payload)
    {
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return TryScheduleAndroidNotification(id, title, text, fireTime, repeatDaily, payload);
#elif UNITY_IOS && !UNITY_EDITOR
            return TryScheduleIOSNotification(id, title, text, fireTime, repeatDaily, payload);
#else
            return false;
#endif
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AppNotificationScheduler] Unity Mobile Notifications 调度失败: {ex.Message}");
            return false;
        }
    }

    private bool InitializeUnityNotificationsApi()
    {
        if (notificationApiInitialized) return true;

        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!AndroidNotificationCenter.Initialize())
                return false;

            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(
                ChannelId,
                "FariApp Reminders",
                "每日神谕、好友互动和系统提醒",
                Importance.Default));
            AndroidNotificationCenter.OnNotificationReceived += OnAndroidNotificationReceived;
            notificationApiInitialized = true;
            return true;
#elif UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.OnNotificationReceived += OnIOSNotificationReceived;
            notificationApiInitialized = true;
            return true;
#else
            return false;
#endif
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AppNotificationScheduler] 初始化通知 API 失败: {ex.Message}");
            UnsubscribeNotificationCallbacks();
            notificationApiInitialized = false;
            return false;
        }
    }

    private void RequestPermissionIfNeeded()
    {
        if (permissionRequested) return;
        permissionRequested = true;

        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            StartCoroutine(RequestAndroidPermission());
#elif UNITY_IOS && !UNITY_EDITOR
            StartCoroutine(RequestIOSPermission());
#endif
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AppNotificationScheduler] 请求通知权限失败: {ex.Message}");
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private bool TryScheduleAndroidNotification(int id, string title, string text, DateTime fireTime, bool repeatDaily, string payload)
    {
        if (!InitializeUnityNotificationsApi())
            return false;

        DateTime normalizedFireTime = NormalizeFireTime(fireTime);
        AndroidNotification notification = new AndroidNotification(title, text, normalizedFireTime)
        {
            IntentData = payload ?? string.Empty,
            ShouldAutoCancel = true,
            ShowInForeground = false,
            ShowTimestamp = true,
        };

        if (repeatDaily)
            notification.RepeatInterval = TimeSpan.FromDays(1);

        AndroidNotificationCenter.SendNotificationWithExplicitID(notification, ChannelId, id);
        return true;
    }

    private IEnumerator RequestAndroidPermission()
    {
        if (!InitializeUnityNotificationsApi())
            yield break;

        PermissionStatus current = AndroidNotificationCenter.UserPermissionToPost;
        if (current == PermissionStatus.Allowed)
            yield break;

        PermissionRequest request = new PermissionRequest();
        while (request.Status == PermissionStatus.RequestPending)
            yield return null;

        Debug.Log($"[AppNotificationScheduler] Android 通知权限：{request.Status}");
    }

    private void OnAndroidNotificationReceived(AndroidNotificationIntentData data)
    {
        string payload = data?.Notification.IntentData;
        HandleNotificationPayload(payload);
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    private bool TryScheduleIOSNotification(int id, string title, string text, DateTime fireTime, bool repeatDaily, string payload)
    {
        InitializeUnityNotificationsApi();

        DateTime normalizedFireTime = NormalizeFireTime(fireTime);
        iOSNotification notification = new iOSNotification
        {
            Identifier = id.ToString(CultureInfo.InvariantCulture),
            Title = title,
            Body = text,
            Data = payload ?? string.Empty,
            ShowInForeground = false,
            ForegroundPresentationOption = PresentationOption.Alert | PresentationOption.Sound,
            SoundType = NotificationSoundType.Default,
            ThreadIdentifier = "moonly_reminders",
            Trigger = repeatDaily
                ? BuildDailyIOSTrigger(normalizedFireTime)
                : BuildOneShotIOSTrigger(normalizedFireTime),
        };

        iOSNotificationCenter.ScheduleNotification(notification);
        return true;
    }

    private IEnumerator RequestIOSPermission()
    {
        using (AuthorizationRequest request = new AuthorizationRequest(
                   AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound,
                   false))
        {
            while (!request.IsFinished)
                yield return null;

            Debug.Log($"[AppNotificationScheduler] iOS 通知权限：granted={request.Granted}, error={request.Error}");
        }
    }

    private iOSNotificationCalendarTrigger BuildDailyIOSTrigger(DateTime fireTime)
    {
        return new iOSNotificationCalendarTrigger
        {
            Hour = fireTime.Hour,
            Minute = fireTime.Minute,
            Second = fireTime.Second,
            Repeats = true,
            UtcTime = false,
        };
    }

    private iOSNotificationCalendarTrigger BuildOneShotIOSTrigger(DateTime fireTime)
    {
        return new iOSNotificationCalendarTrigger
        {
            Year = fireTime.Year,
            Month = fireTime.Month,
            Day = fireTime.Day,
            Hour = fireTime.Hour,
            Minute = fireTime.Minute,
            Second = fireTime.Second,
            Repeats = false,
            UtcTime = false,
        };
    }

    private void OnIOSNotificationReceived(iOSNotification notification)
    {
        HandleNotificationPayload(notification?.Data);
    }
#endif

    private IEnumerator HandleLaunchNotificationNextFrame()
    {
        yield return null;

        string payload = GetLastRespondedNotificationPayload();
        if (string.IsNullOrWhiteSpace(payload))
            yield break;

        if (notificationOpenHandled && payload == lastHandledNotificationPayload)
            yield break;

        notificationOpenHandled = true;
        lastHandledNotificationPayload = payload;
        HandleNotificationPayload(payload);
    }

    private string GetLastRespondedNotificationPayload()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!InitializeUnityNotificationsApi())
            return null;

        AndroidNotificationIntentData intentData = AndroidNotificationCenter.GetLastNotificationIntent();
        return intentData?.Notification.IntentData;
#elif UNITY_IOS && !UNITY_EDITOR
        iOSNotification notification = iOSNotificationCenter.GetLastRespondedNotification();
        return notification?.Data;
#else
        return null;
#endif
    }

    private void HandleNotificationPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return;

        HandleNotificationRoute(NotificationRoute.FromPayload(payload));
        Debug.Log($"[AppNotificationScheduler] 通过通知进入/收到通知：{payload}");
    }

    private void HandleNotificationRoute(NotificationRoute route)
    {
        if (route == null || string.IsNullOrWhiteSpace(route.RawPayload))
            return;

        NotificationUnreadState.MarkUnread(route.RawPayload);
        StartCoroutine(RouteNotificationNextFrame(route));
    }

    private IEnumerator RouteNotificationNextFrame(NotificationRoute route)
    {
        yield return null;

        string action = route.Action;
        if (string.IsNullOrWhiteSpace(action))
            yield break;

        NavigationUI navigation = UIModule.Instance?.GetWindow<NavigationUI>();
        if (navigation == null)
            navigation = UIModule.Instance?.PopUpWindow<NavigationUI>();

        switch (action)
        {
            case "daily_oracle":
                navigation?.OpenTodayOracleUI();
                break;
            case "dialogue_reply":
            case "chat_reply":
            case "ai_chat":
            case "tomorrow_hook":
            case "tomorrow_hook_due":
            case "divination_return":
                navigation?.OpenDialogUI();
                break;
            case "friend_request":
                navigation?.OpenFriendUI();
                OpenFriendRequestWindow(route.RequesterUid);
                break;
            case "relationship_invite":
                navigation?.OpenFriendUI();
                OpenRelationshipInvite(route.ReadingId);
                break;
            case "friend_daily_oracle":
                navigation?.OpenFriendUI();
                break;
            case "diagnostic":
            case "diagnostic_test":
            case "activity_system":
            case "remote_notification":
                UIModule.Instance?.PopUpWindow<NotionUI>();
                break;
        }

        if (!string.IsNullOrWhiteSpace(route.Toast))
            ToastManager.ShowToast(route.Toast);
    }

    private void OpenFriendRequestWindow(string requesterUid)
    {
        FriendRequestUI window = UIModule.Instance?.PopUpWindow<FriendRequestUI>();
        if (window != null)
            window.FocusRequester(requesterUid);
    }

    private void OpenRelationshipInvite(string readingId)
    {
        if (string.IsNullOrWhiteSpace(readingId))
            return;

        StartCoroutine(OpenRelationshipInviteWhenReady(readingId));
    }

    private IEnumerator OpenRelationshipInviteWhenReady(string readingId)
    {
        RelationshipDivinationFirestore service = RelationshipDivinationFlow.GetOrCreateService();
        const int maxAttempts = 20;
        for (int i = 0; i < maxAttempts; i++)
        {
            if (service != null && service.IsReady)
                break;

            yield return new WaitForSeconds(0.5f);
            service = RelationshipDivinationFlow.GetOrCreateService();
        }

        if (service == null || !service.IsReady)
        {
            ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
            yield break;
        }

        service.LoadReading(readingId, record =>
        {
            if (record == null)
            {
                ToastManager.ShowToast("双人占卜邀请不存在或已失效");
                return;
            }

            RelationshipDivinationFlow.ShowRecord(record);
        });
    }

    private DateTime NormalizeFireTime(DateTime fireTime)
    {
        DateTime minimum = DateTime.Now.AddSeconds(5);
        return fireTime <= minimum ? minimum : fireTime;
    }

    private void UnsubscribeNotificationCallbacks()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidNotificationCenter.OnNotificationReceived -= OnAndroidNotificationReceived;
#elif UNITY_IOS && !UNITY_EDITOR
        iOSNotificationCenter.OnNotificationReceived -= OnIOSNotificationReceived;
#endif
    }

    private void CancelKnownScheduledNotifications()
    {
        TryCancelNativeNotifications(KnownNotificationIds);
        foreach (int id in KnownNotificationIds)
            PlayerPrefs.DeleteKey(ScheduledPrefix + id);
        WriteScheduledIds(ReadScheduledIds().FindAll(id => Array.IndexOf(KnownNotificationIds, id) < 0));
        PlayerPrefs.Save();
    }

    private void TryCancelNativeNotifications(IEnumerable<int> ids)
    {
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!InitializeUnityNotificationsApi())
                return;

            foreach (int id in ids)
                AndroidNotificationCenter.CancelScheduledNotification(id);
#elif UNITY_IOS && !UNITY_EDITOR
            InitializeUnityNotificationsApi();
            foreach (int id in ids)
                iOSNotificationCenter.RemoveScheduledNotification(id.ToString(CultureInfo.InvariantCulture));
#endif
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AppNotificationScheduler] 清理已排程通知失败: {ex.Message}");
        }
    }

    private void RecordScheduledNotification(int id, string title, string text, DateTime fireTime, bool repeatDaily, bool nativeScheduled)
    {
        string value = string.Join("|", new[]
        {
            title ?? "",
            text ?? "",
            fireTime.ToString("o"),
            repeatDaily ? "repeat" : "once",
            nativeScheduled ? "native" : "fallback"
        });

        PlayerPrefs.SetString(ScheduledPrefix + id, value);
        AddScheduledId(id);
        PlayerPrefs.Save();

        string state = nativeScheduled ? "native" : "fallback";
        Debug.Log($"[AppNotificationScheduler] 已排程通知({state}): {title} @ {fireTime:yyyy-MM-dd HH:mm:ss}");
    }

    private void AddScheduledId(int id)
    {
        List<int> ids = ReadScheduledIds();
        if (!ids.Contains(id))
            ids.Add(id);
        WriteScheduledIds(ids);
    }

    private List<int> ReadScheduledIds()
    {
        List<int> ids = new List<int>();
        string raw = PlayerPrefs.GetString(ScheduledIdListKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return ids;

        string[] parts = raw.Split(',');
        foreach (string part in parts)
        {
            if (int.TryParse(part, out int id) && !ids.Contains(id))
                ids.Add(id);
        }

        return ids;
    }

    private void WriteScheduledIds(List<int> ids)
    {
        ids.RemoveAll(id => string.IsNullOrWhiteSpace(PlayerPrefs.GetString(ScheduledPrefix + id, string.Empty)));
        PlayerPrefs.SetString(ScheduledIdListKey, string.Join(",", ids));
    }

    private DateTime NextTimeTodayOrTomorrow(string hhmm)
    {
        if (!TryParseReminderTime(hhmm, out int hour, out int minute))
        {
            hour = 8;
            minute = 30;
        }

        DateTime now = DateTime.Now;
        DateTime target = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
        if (target <= now.AddSeconds(10))
            target = target.AddDays(1);
        return target;
    }

    private string GetReturnReminderTime(string reminderTime)
    {
        if (!TryParseReminderTime(reminderTime, out int hour, out _))
            return "21:30";

        return hour < 15 ? "21:30" : "08:30";
    }

    private bool TryParseReminderTime(string hhmm, out int hour, out int minute)
    {
        hour = 8;
        minute = 30;

        if (string.IsNullOrWhiteSpace(hhmm)) return false;
        string[] parts = hhmm.Trim().Split(':');
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0], out hour)) return false;
        if (!int.TryParse(parts[1], out minute)) return false;

        hour = Mathf.Clamp(hour, 0, 23);
        minute = Mathf.Clamp(minute, 0, 59);
        return true;
    }

    private DateTime NextWeeklyActivityTime()
    {
        DateTime now = DateTime.Now;
        DateTime target = new DateTime(now.Year, now.Month, now.Day, 20, 30, 0);
        int daysToFriday = ((int)DayOfWeek.Friday - (int)now.DayOfWeek + 7) % 7;
        target = target.AddDays(daysToFriday);
        if (target <= now.AddSeconds(10))
            target = target.AddDays(7);
        return target;
    }

    private DateTime BuildLocalDateTime(string date, string hhmm)
    {
        DateTime day = DateTime.TryParse(date, out DateTime parsedDate)
            ? parsedDate.Date
            : DateTime.Now.Date.AddDays(1);
        if (!TryParseReminderTime(hhmm, out int hour, out int minute))
        {
            hour = 8;
            minute = 30;
        }

        return day.AddHours(hour).AddMinutes(minute);
    }

    private string TrimForNotification(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        text = text.Trim();
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength).TrimEnd() + "...";
    }

    private string BuildSeenKey(string type, int count)
    {
        return $"{type}_{DateTime.Now:yyyyMMdd}_{count}";
    }

    private bool HasSeenToday(string key)
    {
        return PlayerPrefs.GetInt(SeenPrefix + key, 0) == 1;
    }

    private void MarkSeen(string key)
    {
        PlayerPrefs.SetInt(SeenPrefix + key, 1);
        PlayerPrefs.Save();
    }

    private void TryEditorToast(string text)
    {
#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(text))
            ToastManager.ShowToast(text);
#endif
    }

    private class NotificationRoute
    {
        public string RawPayload { get; private set; }
        public string Action { get; private set; }
        public string Toast { get; private set; }
        public string ReadingId { get; private set; }
        public string RequesterUid { get; private set; }
        public string NotificationId { get; private set; }

        public static NotificationRoute FromPayload(string payload)
        {
            string normalizedAction = NormalizeAction(payload);
            NotificationRoute route = new NotificationRoute
            {
                RawPayload = payload ?? string.Empty,
                Action = normalizedAction,
                Toast = BuildToast(normalizedAction),
                ReadingId = normalizedAction == "relationship_invite" ? ExtractActionSuffix(payload) : string.Empty,
                RequesterUid = normalizedAction == "friend_request" ? ExtractActionSuffix(payload) : string.Empty
            };

            if (!string.IsNullOrWhiteSpace(payload) && payload.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    JObject obj = JObject.Parse(payload);
                    string action = FirstNonEmpty(
                        GetValue(obj, "clickAction"),
                        GetValue(obj, "type"),
                        GetValue(obj, "source"));
                    route.Action = NormalizeAction(action);
                    route.Toast = BuildToast(route.Action);
                    route.ReadingId = FirstNonEmpty(
                        GetValue(obj, "readingId"),
                        GetValue(obj, "relationshipReadingId"),
                        GetValue(obj, "relationshipId"),
                        route.Action == "relationship_invite" ? ExtractActionSuffix(action) : route.ReadingId);
                    route.RequesterUid = FirstNonEmpty(
                        GetValue(obj, "requesterUid"),
                        GetValue(obj, "senderUid"),
                        GetValue(obj, "friendUid"),
                        route.Action == "friend_request" ? ExtractActionSuffix(action) : route.RequesterUid);
                    route.NotificationId = FirstNonEmpty(
                        GetValue(obj, "notificationId"),
                        GetValue(obj, "id"));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AppNotificationScheduler] 通知 payload JSON 解析失败: " + ex.Message);
                }
            }

            return route;
        }

        public static NotificationRoute FromDictionary(IDictionary<string, string> data)
        {
            string action = GetValue(data, "clickAction");
            if (string.IsNullOrWhiteSpace(action)) action = GetValue(data, "type");
            if (string.IsNullOrWhiteSpace(action)) action = GetValue(data, "source");

            JObject obj = new JObject();
            foreach (KeyValuePair<string, string> entry in data)
                obj[entry.Key] = entry.Value ?? string.Empty;

            string normalized = NormalizeAction(action);
            return new NotificationRoute
            {
                RawPayload = obj.ToString(Newtonsoft.Json.Formatting.None),
                Action = normalized,
                Toast = BuildToast(normalized),
                ReadingId = FirstNonEmpty(
                    GetValue(data, "readingId"),
                    GetValue(data, "relationshipReadingId"),
                    GetValue(data, "relationshipId"),
                    normalized == "relationship_invite" ? ExtractActionSuffix(action) : string.Empty),
                RequesterUid = FirstNonEmpty(
                    GetValue(data, "requesterUid"),
                    GetValue(data, "senderUid"),
                    GetValue(data, "friendUid"),
                    normalized == "friend_request" ? ExtractActionSuffix(action) : string.Empty),
                NotificationId = FirstNonEmpty(
                    GetValue(data, "notificationId"),
                    GetValue(data, "id"))
            };
        }

        private static string NormalizeAction(string action)
        {
            if (string.IsNullOrWhiteSpace(action)) return string.Empty;

            string normalized = action.Trim();
            int separatorIndex = normalized.IndexOf(':');
            if (separatorIndex > 0)
                normalized = normalized.Substring(0, separatorIndex);

            return normalized.ToLowerInvariant();
        }

        private static string ExtractActionSuffix(string action)
        {
            if (string.IsNullOrWhiteSpace(action)) return string.Empty;

            string trimmed = action.Trim();
            int separatorIndex = trimmed.IndexOf(':');
            if (separatorIndex < 0 || separatorIndex >= trimmed.Length - 1)
                return string.Empty;

            return trimmed.Substring(separatorIndex + 1).Trim();
        }

        private static string BuildToast(string action)
        {
            return action switch
            {
                "friend_request" => "已打开好友请求",
                "relationship_invite" => "已打开双人占卜邀请",
                "dialogue_reply" => "已打开对话",
                "chat_reply" => "已打开对话",
                "tomorrow_hook_due" => "已打开明日线索",
                "friend_daily_oracle" => "已打开好友每日牌动态",
                _ => string.Empty
            };
        }

        private static string GetValue(IDictionary<string, string> data, string key)
        {
            if (data == null || string.IsNullOrWhiteSpace(key)) return string.Empty;
            foreach (KeyValuePair<string, string> entry in data)
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                    return entry.Value;
            }
            return string.Empty;
        }

        private static string GetValue(JObject obj, string key)
        {
            if (obj == null || string.IsNullOrWhiteSpace(key)) return string.Empty;
            foreach (JProperty property in obj.Properties())
            {
                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                    return property.Value?.ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null) return string.Empty;
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return string.Empty;
        }
    }
}
