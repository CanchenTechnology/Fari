using System;
using XFGameFrameWork;

/// <summary>
/// 本地每日使用统计。用于“我的”首页即时展示，后续可替换为后端额度接口。
/// </summary>
public class UsageStatsManager : MonoSingleton<UsageStatsManager>
{
    private const string KEY_DATE = "UsageStats_Date";
    private const string KEY_DAILY_ORACLE = "UsageStats_DailyOracle";
    private const string KEY_DIALOG_MESSAGES = "UsageStats_DialogMessages";
    private const string KEY_SPREAD_READINGS = "UsageStats_SpreadReadings";

    public const int FreeDailyOracleLimit = 1;
    public const int FreeDialogLimit = 100;
    public const int FreeReadingLimit = 15;

    public int DailyOracleCount { get; private set; }
    public int DialogMessageCount { get; private set; }
    public int SpreadReadingCount { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        LoadToday();
    }

    public void TrackDailyOracle()
    {
        LoadToday();
        DailyOracleCount = Math.Max(DailyOracleCount, 1);
        Save();
    }

    public void TrackDialogMessage()
    {
        LoadToday();
        DialogMessageCount++;
        Save();
    }

    public void TrackSpreadReading()
    {
        LoadToday();
        SpreadReadingCount++;
        Save();
    }

    public string GetDailyOracleDisplay(bool isPro)
    {
        return isPro ? $"{DailyOracleCount}/∞" : $"{DailyOracleCount}/{FreeDailyOracleLimit}";
    }

    public string GetDialogDisplay(bool isPro)
    {
        return isPro ? $"{DialogMessageCount}/∞" : $"{DialogMessageCount}/{FreeDialogLimit}";
    }

    public string GetReadingDisplay(bool isPro)
    {
        return isPro ? $"{SpreadReadingCount}/∞" : $"{SpreadReadingCount}/{FreeReadingLimit}";
    }

    private void LoadToday()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        string storedDate = UnityEngine.PlayerPrefs.GetString(KEY_DATE, "");
        if (storedDate != today)
        {
            DailyOracleCount = 0;
            DialogMessageCount = 0;
            SpreadReadingCount = 0;
            UnityEngine.PlayerPrefs.SetString(KEY_DATE, today);
            Save();
            return;
        }

        DailyOracleCount = UnityEngine.PlayerPrefs.GetInt(KEY_DAILY_ORACLE, 0);
        DialogMessageCount = UnityEngine.PlayerPrefs.GetInt(KEY_DIALOG_MESSAGES, 0);
        SpreadReadingCount = UnityEngine.PlayerPrefs.GetInt(KEY_SPREAD_READINGS, 0);
    }

    private void Save()
    {
        UnityEngine.PlayerPrefs.SetInt(KEY_DAILY_ORACLE, DailyOracleCount);
        UnityEngine.PlayerPrefs.SetInt(KEY_DIALOG_MESSAGES, DialogMessageCount);
        UnityEngine.PlayerPrefs.SetInt(KEY_SPREAD_READINGS, SpreadReadingCount);
        UnityEngine.PlayerPrefs.Save();
    }
}
