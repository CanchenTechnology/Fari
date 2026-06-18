using UnityEngine;
using XFGameFrameWork;

/// <summary>
/// 通知偏好设置管理器
/// 负责通知相关的偏好设置内存管理和本地持久化
/// </summary>
public class NotificationSettingsManager : MonoSingleton<NotificationSettingsManager>
{
    #region 数据字段

    /// <summary>每日神谕提醒开关</summary>
    public bool DailyOracleEnabled { get; private set; } = true;

    /// <summary>对话回复提醒开关</summary>
    public bool DialogueReplyEnabled { get; private set; } = true;

    /// <summary>占卜回访提醒开关</summary>
    public bool DivinationReturnEnabled { get; private set; } = true;

    /// <summary>朋友互动提醒开关</summary>
    public bool FriendInteractionEnabled { get; private set; } = false;

    /// <summary>活动与系统通知开关</summary>
    public bool ActivitySystemEnabled { get; private set; } = true;

    /// <summary>每日提醒时间（格式：HH:mm）</summary>
    public string ReminderTime { get; private set; } = "08:30";

    #endregion

    #region 常量

    private const string KEY_DAILY_ORACLE = "Notif_DailyOracle";
    private const string KEY_DIALOGUE_REPLY = "Notif_DialogueReply";
    private const string KEY_DIVINATION_RETURN = "Notif_DivinationReturn";
    private const string KEY_FRIEND_INTERACTION = "Notif_FriendInteraction";
    private const string KEY_ACTIVITY_SYSTEM = "Notif_ActivitySystem";
    private const string KEY_REMINDER_TIME = "Notif_ReminderTime";

    private const string TIME_MORNING = "08:30";
    private const string TIME_EVENING = "21:30";

    #endregion

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();
        LoadSettings();
    }

    #endregion

    #region 设置操作

    /// <summary>
    /// 设置每日神谕提醒
    /// </summary>
    public void SetDailyOracle(bool enabled)
    {
        DailyOracleEnabled = enabled;
        SaveSettings();
    }

    /// <summary>
    /// 设置对话回复提醒
    /// </summary>
    public void SetDialogueReply(bool enabled)
    {
        DialogueReplyEnabled = enabled;
        SaveSettings();
    }

    /// <summary>
    /// 设置占卜回访提醒
    /// </summary>
    public void SetDivinationReturn(bool enabled)
    {
        DivinationReturnEnabled = enabled;
        SaveSettings();
    }

    /// <summary>
    /// 设置朋友互动提醒
    /// </summary>
    public void SetFriendInteraction(bool enabled)
    {
        FriendInteractionEnabled = enabled;
        SaveSettings();
    }

    /// <summary>
    /// 设置活动与系统通知
    /// </summary>
    public void SetActivitySystem(bool enabled)
    {
        ActivitySystemEnabled = enabled;
        SaveSettings();
    }

    /// <summary>
    /// 切换每日提醒时间（08:30 <=> 21:00）
    /// </summary>
    /// <returns>切换后的时间字符串</returns>
    public string ToggleReminderTime()
    {
        ReminderTime = ReminderTime == TIME_MORNING ? TIME_EVENING : TIME_MORNING;
        SaveSettings();
        return ReminderTime;
    }

    /// <summary>
    /// 设置指定的提醒时间
    /// </summary>
    public void SetReminderTime(string time)
    {
        ReminderTime = NormalizeReminderTime(time);
        SaveSettings();
    }

    /// <summary>
    /// 从云端或外部存储应用完整通知设置。
    /// </summary>
    public void ApplySettings(
        bool dailyOracleEnabled,
        bool dialogueReplyEnabled,
        bool divinationReturnEnabled,
        bool friendInteractionEnabled,
        bool activitySystemEnabled,
        string reminderTime,
        bool save = true)
    {
        DailyOracleEnabled = dailyOracleEnabled;
        DialogueReplyEnabled = dialogueReplyEnabled;
        DivinationReturnEnabled = divinationReturnEnabled;
        FriendInteractionEnabled = friendInteractionEnabled;
        ActivitySystemEnabled = activitySystemEnabled;
        ReminderTime = NormalizeReminderTime(reminderTime);

        if (save)
            SaveSettings();
    }

    #endregion

    #region 持久化

    /// <summary>
    /// 保存所有设置到本地
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.SetInt(KEY_DAILY_ORACLE, DailyOracleEnabled ? 1 : 0);
        PlayerPrefs.SetInt(KEY_DIALOGUE_REPLY, DialogueReplyEnabled ? 1 : 0);
        PlayerPrefs.SetInt(KEY_DIVINATION_RETURN, DivinationReturnEnabled ? 1 : 0);
        PlayerPrefs.SetInt(KEY_FRIEND_INTERACTION, FriendInteractionEnabled ? 1 : 0);
        PlayerPrefs.SetInt(KEY_ACTIVITY_SYSTEM, ActivitySystemEnabled ? 1 : 0);
        PlayerPrefs.SetString(KEY_REMINDER_TIME, ReminderTime);
        PlayerPrefs.Save();

        Debug.Log($"[NotificationSettingsManager] 通知设置已保存，提醒时间：{ReminderTime}");
    }

    /// <summary>
    /// 从本地加载设置
    /// </summary>
    public void LoadSettings()
    {
        DailyOracleEnabled = PlayerPrefs.GetInt(KEY_DAILY_ORACLE, 1) == 1;
        DialogueReplyEnabled = PlayerPrefs.GetInt(KEY_DIALOGUE_REPLY, 1) == 1;
        DivinationReturnEnabled = PlayerPrefs.GetInt(KEY_DIVINATION_RETURN, 1) == 1;
        FriendInteractionEnabled = PlayerPrefs.GetInt(KEY_FRIEND_INTERACTION, 0) == 1;
        ActivitySystemEnabled = PlayerPrefs.GetInt(KEY_ACTIVITY_SYSTEM, 1) == 1;
        ReminderTime = NormalizeReminderTime(PlayerPrefs.GetString(KEY_REMINDER_TIME, TIME_MORNING));

        Debug.Log($"[NotificationSettingsManager] 通知设置已加载，提醒时间：{ReminderTime}");
    }

    /// <summary>
    /// 恢复默认设置
    /// </summary>
    public void ResetToDefault()
    {
        DailyOracleEnabled = true;
        DialogueReplyEnabled = true;
        DivinationReturnEnabled = true;
        FriendInteractionEnabled = false;
        ActivitySystemEnabled = true;
        ReminderTime = TIME_MORNING;

        SaveSettings();
        Debug.Log("[NotificationSettingsManager] 通知设置已恢复默认");
    }

    private string NormalizeReminderTime(string time)
    {
        if (string.IsNullOrWhiteSpace(time)) return TIME_MORNING;
        return time;
    }

    #endregion
}
