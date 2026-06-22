using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;
using UnityEngine;
using XFGameFrameWork;

/// <summary>
/// 通知调度入口。未安装 Unity Mobile Notifications 时会安全降级为本地记录和调试提示；
/// 包解析完成后会通过反射调用统一通知 API，避免当前工程在包未解析时编译失败。
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

    public string LastSyncSummary => PlayerPrefs.GetString(LastSyncSummaryKey, string.Empty);

    public bool ScheduleDiagnosticNotification(int delaySeconds = 10)
    {
        delaySeconds = Mathf.Clamp(delaySeconds, 5, 300);
        RequestPermissionIfNeeded();
        return ScheduleNotification(
            DiagnosticNotificationId,
            "Moonly 通知测试",
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
                "Moonly 有新的能量更新",
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
        if (!InitializeUnityNotificationsApi())
            return false;

        Type notificationType = ResolveUnityNotificationType("Unity.Notifications.Notification");
        Type dateScheduleType = ResolveUnityNotificationType("Unity.Notifications.NotificationDateTimeSchedule");
        Type intervalScheduleType = ResolveUnityNotificationType("Unity.Notifications.NotificationIntervalSchedule");
        Type centerType = ResolveUnityNotificationType("Unity.Notifications.NotificationCenter");
        if (notificationType == null || centerType == null)
            return false;

        try
        {
            object notification = Activator.CreateInstance(notificationType);
            SetMember(notification, "Identifier", id);
            SetMember(notification, "Title", title);
            SetMember(notification, "Text", text);
            SetMember(notification, "Body", text);
            SetMember(notification, "Data", payload ?? string.Empty);

            object schedule = CreateDateSchedule(dateScheduleType, fireTime, repeatDaily);
            if (schedule == null && intervalScheduleType != null)
                schedule = CreateIntervalSchedule(intervalScheduleType, fireTime, repeatDaily);

            if (schedule == null)
                return false;

            MethodInfo scheduleMethod = FindScheduleMethod(centerType, schedule.GetType());
            if (scheduleMethod == null)
                return false;

            scheduleMethod.Invoke(null, new[] { notification, schedule });
            return true;
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

        Type centerType = ResolveUnityNotificationType("Unity.Notifications.NotificationCenter");
        if (centerType == null)
            return false;

        try
        {
            Type argsType = ResolveUnityNotificationType("Unity.Notifications.NotificationCenterArgs");
            if (argsType != null)
            {
                object args = Activator.CreateInstance(argsType);
                SetMember(args, "AndroidChannelId", ChannelId);
                SetMember(args, "AndroidChannelName", "Moonly Reminders");
                SetMember(args, "AndroidChannelDescription", "每日神谕、好友互动和系统提醒");
                SetMember(args, "IOSNotificationCategories", null);
                SetFlagsMember(args, "PresentationOptions", "Alert", "Sound", "Badge", "Vibrate");

                MethodInfo initialize = centerType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                initialize?.Invoke(null, new[] { args });
            }

            notificationApiInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AppNotificationScheduler] 初始化通知 API 失败: {ex.Message}");
            notificationApiInitialized = false;
            return false;
        }
    }

    private void RequestPermissionIfNeeded()
    {
        if (permissionRequested) return;
        permissionRequested = true;

        Type centerType = ResolveUnityNotificationType("Unity.Notifications.NotificationCenter");
        if (centerType == null) return;

        try
        {
            MethodInfo method = centerType.GetMethod("RequestPermission", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            object request = method?.Invoke(null, null);
            if (request is IEnumerator routine)
                StartCoroutine(routine);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AppNotificationScheduler] 请求通知权限失败: {ex.Message}");
        }
    }

    private MethodInfo FindScheduleMethod(Type centerType, Type scheduleType)
    {
        foreach (MethodInfo method in centerType.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "ScheduleNotification") continue;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 2) continue;

            if (method.IsGenericMethodDefinition)
                return method.MakeGenericMethod(scheduleType);

            if (parameters[1].ParameterType.IsAssignableFrom(scheduleType))
                return method;
        }

        return null;
    }

    private object CreateIntervalSchedule(Type scheduleType, DateTime firstFireTime, bool repeatDaily)
    {
        if (scheduleType == null) return null;

        TimeSpan delay = firstFireTime - DateTime.Now;
        if (delay.TotalSeconds < 5)
            delay = delay.Add(TimeSpan.FromDays(1));

        object schedule = TryCreate(scheduleType, delay, repeatDaily);
        if (schedule == null)
            schedule = TryCreate(scheduleType, delay);
        if (schedule == null)
            schedule = Activator.CreateInstance(scheduleType);

        SetMember(schedule, "Interval", delay);
        SetMember(schedule, "TimeInterval", delay);
        SetMember(schedule, "Repeats", repeatDaily);
        return schedule;
    }

    private object CreateDateSchedule(Type scheduleType, DateTime fireTime, bool repeatDaily)
    {
        if (scheduleType == null) return null;

        object schedule = TryCreate(scheduleType, fireTime, repeatDaily ? EnumValue(scheduleType, "RepeatInterval", "Daily") : null);
        if (schedule == null)
            schedule = TryCreate(scheduleType, fireTime);
        if (schedule == null)
            schedule = Activator.CreateInstance(scheduleType);

        SetMember(schedule, "DateTime", fireTime);
        SetMember(schedule, "FireTime", fireTime);
        SetMember(schedule, "DeliveryTime", fireTime);
        if (repeatDaily)
            SetMember(schedule, "RepeatInterval", "Daily");
        return schedule;
    }

    private object EnumValue(Type hostType, string memberName, string valueName)
    {
        Type memberType = GetMemberType(hostType, memberName);
        if (memberType == null) return null;

        Type enumType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        if (!enumType.IsEnum) return null;

        try
        {
            return Enum.Parse(enumType, valueName, true);
        }
        catch
        {
            return null;
        }
    }

    private object TryCreate(Type type, params object[] args)
    {
        try
        {
            return Activator.CreateInstance(type, args);
        }
        catch
        {
            return null;
        }
    }

    private void CancelKnownScheduledNotifications()
    {
        TryCancelNativeNotifications();
        foreach (int id in KnownNotificationIds)
            PlayerPrefs.DeleteKey(ScheduledPrefix + id);
        WriteScheduledIds(ReadScheduledIds().FindAll(id => Array.IndexOf(KnownNotificationIds, id) < 0));
        PlayerPrefs.Save();
    }

    private void TryCancelNativeNotifications()
    {
        Type centerType = ResolveUnityNotificationType("Unity.Notifications.NotificationCenter");
        if (centerType == null) return;

        try
        {
            InvokeStaticIfExists(centerType, "CancelAllScheduledNotifications");
            InvokeStaticIfExists(centerType, "DismissAllDisplayedNotifications");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AppNotificationScheduler] 清理已排程通知失败: {ex.Message}");
        }
    }

    private void InvokeStaticIfExists(Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
        method?.Invoke(null, null);
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

    private Type ResolveUnityNotificationType(string fullName)
    {
        string[] assemblyNames =
        {
            "Unity.Notifications",
            "Unity.Notifications.Unified",
            "Unity.Notifications.Android",
            "Unity.Notifications.iOS"
        };

        foreach (string assemblyName in assemblyNames)
        {
            Type type = Type.GetType($"{fullName}, {assemblyName}");
            if (type != null) return type;
        }

        return Type.GetType(fullName);
    }

    private void SetMember(object target, string name, object value)
    {
        if (target == null || string.IsNullOrEmpty(name)) return;

        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, ConvertValue(value, property.PropertyType));
            return;
        }

        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
            field.SetValue(target, ConvertValue(value, field.FieldType));
    }

    private void SetFlagsMember(object target, string name, params string[] flags)
    {
        if (target == null) return;

        Type memberType = GetMemberType(target.GetType(), name);
        if (memberType == null) return;

        Type enumType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        if (!enumType.IsEnum) return;

        int combined = 0;
        foreach (string flag in flags)
        {
            try
            {
                object parsed = Enum.Parse(enumType, flag, true);
                combined |= Convert.ToInt32(parsed, CultureInfo.InvariantCulture);
            }
            catch
            {
                // 某些包版本没有 Badge 等选项，跳过即可。
            }
        }

        if (combined == 0) return;
        SetMember(target, name, Enum.ToObject(enumType, combined));
    }

    private Type GetMemberType(Type type, string name)
    {
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property != null) return property.PropertyType;
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        return field?.FieldType;
    }

    private object ConvertValue(object value, Type targetType)
    {
        if (targetType == null) return value;
        if (value == null) return null;

        Type nullableType = Nullable.GetUnderlyingType(targetType);
        Type realType = nullableType ?? targetType;

        if (realType.IsInstanceOfType(value)) return value;
        if (realType.IsEnum)
        {
            if (value is string textValue)
                return Enum.Parse(realType, textValue, true);
            return Enum.ToObject(realType, value);
        }

        try
        {
            return Convert.ChangeType(value, realType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return value;
        }
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
}
