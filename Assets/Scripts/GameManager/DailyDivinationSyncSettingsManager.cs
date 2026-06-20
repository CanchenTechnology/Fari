using UnityEngine;
using XFGameFrameWork;

public enum DailyDivinationSyncVisibility
{
    AllFriends,
    RealFriends,
    OnlyMe
}

public class DailyDivinationSyncSettings
{
    public bool enabled = true;
    public DailyDivinationSyncVisibility visibility = DailyDivinationSyncVisibility.OnlyMe;

    public bool ShouldPublishToFeed =>
        enabled && visibility != DailyDivinationSyncVisibility.OnlyMe;

    public string VisibilityKey => DailyDivinationSyncSettingsManager.ToVisibilityKey(visibility);
}

public class DailyDivinationSyncSettingsManager : MonoSingleton<DailyDivinationSyncSettingsManager>
{
    private const string KEY_ENABLED = "DailyDivinationSync_Enabled";
    private const string KEY_VISIBILITY = "DailyDivinationSync_Visibility";

    public bool Enabled { get; private set; } = true;
    public DailyDivinationSyncVisibility Visibility { get; private set; } = DailyDivinationSyncVisibility.OnlyMe;

    protected override void Awake()
    {
        base.Awake();
        LoadLocal();
    }

    public DailyDivinationSyncSettings GetSettings()
    {
        return new DailyDivinationSyncSettings
        {
            enabled = Enabled,
            visibility = Visibility
        };
    }

    public void ApplySettings(bool enabled, string visibilityKey, bool save = true)
    {
        Enabled = enabled;
        Visibility = FromVisibilityKey(visibilityKey);
        if (save) SaveLocal();
    }

    public void SetEnabled(bool enabled, bool save = true)
    {
        Enabled = enabled;
        if (save) SaveLocal();
    }

    public void SetVisibility(DailyDivinationSyncVisibility visibility, bool save = true)
    {
        Visibility = visibility;
        if (save) SaveLocal();
    }

    public void SaveLocal()
    {
        PlayerPrefs.SetInt(KEY_ENABLED, Enabled ? 1 : 0);
        PlayerPrefs.SetString(KEY_VISIBILITY, ToVisibilityKey(Visibility));
        PlayerPrefs.Save();
    }

    private void LoadLocal()
    {
        Enabled = PlayerPrefs.GetInt(KEY_ENABLED, 1) == 1;
        Visibility = FromVisibilityKey(PlayerPrefs.GetString(KEY_VISIBILITY, ToVisibilityKey(DailyDivinationSyncVisibility.OnlyMe)));
    }

    public static string ToVisibilityKey(DailyDivinationSyncVisibility visibility)
    {
        return visibility switch
        {
            DailyDivinationSyncVisibility.AllFriends => "all_friends",
            DailyDivinationSyncVisibility.RealFriends => "real_friends",
            _ => "only_me",
        };
    }

    public static DailyDivinationSyncVisibility FromVisibilityKey(string value)
    {
        return value switch
        {
            "all_friends" => DailyDivinationSyncVisibility.AllFriends,
            "real_friends" => DailyDivinationSyncVisibility.RealFriends,
            _ => DailyDivinationSyncVisibility.OnlyMe,
        };
    }
}
