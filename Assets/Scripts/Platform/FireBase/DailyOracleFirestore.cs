using System;
using System.Collections.Generic;
using Firebase.Firestore;
using Firebase.Extensions;
using GamerFrameWork.OracleRuntime;
using UnityEngine;
using XFGameFrameWork;

/// <summary>
/// 每日神谕 Firestore 管理器
///
/// 数据结构：
///   users/{uid}/daily_oracles/{yyyy-MM-dd}
///     - date, cardId, orientation
///     - title, oracle, detail, dos, donts, microAction
///     - locale, oracleId, createdAt, updatedAt
///
/// 朋友动态只读取摘要：
///   daily_oracle_summaries/{uid}_{yyyy-MM-dd}
///     - ownerUid, date, cardId, cardName, orientation, title, oracle
///     - syncEnabled, visibility, summaryOnly
/// </summary>
public class DailyOracleFirestore : MonoSingleton<DailyOracleFirestore>
{
    private FirebaseFirestore _db;
    private bool _isInitialized = false;

    public bool IsReady => _isInitialized && _db != null;

    protected override void Awake()
    {
        base.Awake();
        TryInit();
    }

    private void TryInit()
    {
        if (FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.IsFirebaseInitialized)
        {
            _db = FirebaseFirestore.DefaultInstance;
            _isInitialized = true;
            Debug.Log("[DailyOracleFirestore] 初始化完成");
        }
        else if (FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.OnFirebaseInitialized += () =>
            {
                _db = FirebaseFirestore.DefaultInstance;
                _isInitialized = true;
                Debug.Log("[DailyOracleFirestore] Firebase 初始化完成后就绪");
            };
        }
    }

    public void LoadToday(Action<DailyOracleCloudRecord> onComplete)
    {
        LoadByDate(DateTime.Now.ToString("yyyy-MM-dd"), onComplete);
    }

    public void LoadRecent(int limit, Action<List<DailyOracleCloudRecord>> onComplete)
    {
        if (!CheckReady(_ => onComplete?.Invoke(new List<DailyOracleCloudRecord>()))) return;

        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            onComplete?.Invoke(new List<DailyOracleCloudRecord>());
            return;
        }

        int safeLimit = Mathf.Clamp(limit, 1, 30);
        _db.Collection("users")
            .Document(uid)
            .Collection("daily_oracles")
            .OrderByDescending("date")
            .Limit(safeLimit)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[DailyOracleFirestore] 读取近期每日牌失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(new List<DailyOracleCloudRecord>());
                    return;
                }

                var records = new List<DailyOracleCloudRecord>();
                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    if (!doc.Exists) continue;
                    DailyOracleCloudRecord record = DailyOracleCloudRecord.FromSnapshot(doc);
                    if (record != null && record.HasPayload)
                        records.Add(record);
                }

                onComplete?.Invoke(records);
            });
    }

    public void LoadByDate(string date, Action<DailyOracleCloudRecord> onComplete)
    {
        if (!CheckReady(_ => onComplete?.Invoke(null))) return;

        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            onComplete?.Invoke(null);
            return;
        }

        GetDailyDoc(uid, date)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                {
                    onComplete?.Invoke(null);
                    return;
                }

                onComplete?.Invoke(DailyOracleCloudRecord.FromSnapshot(task.Result));
            });
    }

    public void SaveToday(TarotCard card, bool upright, TodayOraclePayload payload, string locale, Action<bool> onComplete = null)
    {
        SaveByDate(DateTime.Now.ToString("yyyy-MM-dd"), card, upright, payload, locale, onComplete);
    }

    public void SaveByDate(string date, TarotCard card, bool upright, TodayOraclePayload payload, string locale, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        if (card == null || payload == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            onComplete?.Invoke(false);
            return;
        }

        var data = new Dictionary<string, object>
        {
            { "date", date },
            { "cardId", card.cardId ?? "" },
            { "cardName", card.nameZh ?? "" },
            { "orientation", upright ? "upright" : "reversed" },
            { "title", payload.title ?? "" },
            { "oracle", payload.oracle ?? "" },
            { "detail", payload.detail ?? "" },
            { "dos", payload.dos ?? new List<string>() },
            { "donts", payload.donts ?? new List<string>() },
            { "microAction", payload.microAction ?? "" },
            { "locale", string.IsNullOrEmpty(locale) ? "zh-CN" : locale },
            { "oracleId", GetCurrentOracleId() },
            { "syncEnabled", DailyDivinationSyncSettingsManager.Instance.Enabled },
            { "visibility", DailyDivinationSyncSettingsManager.Instance.GetSettings().VisibilityKey },
            { "summaryOnly", false },
            { "createdAtLocal", DateTime.Now.ToString("o") },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        GetDailyDoc(uid, date)
            .SetAsync(data, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[DailyOracleFirestore] 保存今日神谕失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                Debug.Log($"[DailyOracleFirestore] 今日神谕已保存: {date}");
                SaveSummaryByDate(uid, date, card, upright, payload, locale, DailyDivinationSyncSettingsManager.Instance.GetSettings());
                onComplete?.Invoke(true);
            });
    }

    public void PublishTodaySummary(Action<bool> onComplete = null)
    {
        LoadToday(record =>
        {
            if (record == null || !record.HasPayload)
            {
                onComplete?.Invoke(false);
                return;
            }

            SaveSummaryFromRecord(record, DailyDivinationSyncSettingsManager.Instance.GetSettings(), onComplete);
        });
    }

    public void DisableTodaySummary(Action<bool> onComplete = null)
    {
        DisableSummaryByDate(DateTime.Now.ToString("yyyy-MM-dd"), onComplete);
    }

    public void LoadFriendSummary(string ownerUid, string date, Action<DailyOracleSummaryRecord> onComplete)
    {
        if (!CheckReady(_ => onComplete?.Invoke(null))) return;
        if (string.IsNullOrEmpty(ownerUid) || string.IsNullOrEmpty(date))
        {
            onComplete?.Invoke(null);
            return;
        }

        GetSummaryDoc(ownerUid, date)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                {
                    onComplete?.Invoke(null);
                    return;
                }

                onComplete?.Invoke(DailyOracleSummaryRecord.FromSnapshot(task.Result));
            });
    }

    public void LoadTodayFriendSummaries(Action<List<DailyOracleSummaryRecord>> onComplete)
    {
        LoadFriendSummaries(DateTime.Now.ToString("yyyy-MM-dd"), onComplete);
    }

    public void LoadRecentFriendSummaries(string ownerUid, int daysBack, int limit, Action<List<DailyOracleSummaryRecord>> onComplete)
    {
        if (!CheckReady(_ => onComplete?.Invoke(new List<DailyOracleSummaryRecord>()))) return;
        if (string.IsNullOrWhiteSpace(ownerUid))
        {
            onComplete?.Invoke(new List<DailyOracleSummaryRecord>());
            return;
        }

        int safeDays = Mathf.Clamp(daysBack, 1, 90);
        int safeLimit = Mathf.Clamp(limit, 1, 30);
        List<DailyOracleSummaryRecord> results = new List<DailyOracleSummaryRecord>();
        int pending = safeDays;

        for (int i = 0; i < safeDays; i++)
        {
            string date = DateTime.Now.AddDays(-i).ToString("yyyy-MM-dd");
            LoadFriendSummary(ownerUid, date, summary =>
            {
                if (summary != null && summary.IsVisibleInFriendFeed && results.Count < safeLimit)
                    results.Add(summary);

                pending--;
                if (pending == 0)
                {
                    results.Sort((a, b) => string.CompareOrdinal(b.date, a.date));
                    if (results.Count > safeLimit)
                        results.RemoveRange(safeLimit, results.Count - safeLimit);
                    onComplete?.Invoke(results);
                }
            });
        }
    }

    public void LoadFriendSummaries(string date, Action<List<DailyOracleSummaryRecord>> onComplete)
    {
        if (!CheckReady(_ => onComplete?.Invoke(new List<DailyOracleSummaryRecord>()))) return;
        if (FriendDataManager.Instance == null)
        {
            onComplete?.Invoke(new List<DailyOracleSummaryRecord>());
            return;
        }

        List<string> friendUids = new List<string>();
        foreach (var friend in FriendDataManager.Instance.RealFriendList)
        {
            if (friend == null || string.IsNullOrWhiteSpace(friend.firebaseUid)) continue;
            if (!friendUids.Contains(friend.firebaseUid)) friendUids.Add(friend.firebaseUid);
        }

        if (friendUids.Count == 0)
        {
            onComplete?.Invoke(new List<DailyOracleSummaryRecord>());
            return;
        }

        var results = new List<DailyOracleSummaryRecord>();
        int pending = friendUids.Count;

        foreach (string friendUid in friendUids)
        {
            LoadFriendSummary(friendUid, date, summary =>
            {
                if (summary != null && summary.IsVisibleInFriendFeed)
                    results.Add(summary);

                pending--;
                if (pending == 0)
                    onComplete?.Invoke(results);
            });
        }
    }

    private void SaveSummaryFromRecord(DailyOracleCloudRecord record, DailyDivinationSyncSettings settings, Action<bool> onComplete = null)
    {
        if (record == null || !record.HasPayload)
        {
            onComplete?.Invoke(false);
            return;
        }

        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            onComplete?.Invoke(false);
            return;
        }

        var data = BuildSummaryData(
            uid,
            record.date,
            record.cardId,
            record.cardName,
            record.orientation,
            record.title,
            record.oracle,
            record.microAction,
            record.locale,
            record.oracleId,
            settings);

        SetSummaryData(uid, record.date, data, onComplete);
    }

    private void SaveSummaryByDate(
        string uid,
        string date,
        TarotCard card,
        bool upright,
        TodayOraclePayload payload,
        string locale,
        DailyDivinationSyncSettings settings)
    {
        var data = BuildSummaryData(
            uid,
            date,
            card.cardId ?? "",
            card.nameZh ?? "",
            upright ? "upright" : "reversed",
            payload.title ?? "",
            payload.oracle ?? "",
            payload.microAction ?? "",
            string.IsNullOrEmpty(locale) ? "zh-CN" : locale,
            GetCurrentOracleId(),
            settings);

        SetSummaryData(uid, date, data);
    }

    private Dictionary<string, object> BuildSummaryData(
        string uid,
        string date,
        string cardId,
        string cardName,
        string orientation,
        string title,
        string oracle,
        string microAction,
        string locale,
        string oracleId,
        DailyDivinationSyncSettings settings)
    {
        settings ??= DailyDivinationSyncSettingsManager.Instance.GetSettings();

        return new Dictionary<string, object>
        {
            { "ownerUid", uid },
            { "date", date ?? "" },
            { "cardId", cardId ?? "" },
            { "cardName", cardName ?? "" },
            { "orientation", orientation ?? "" },
            { "title", title ?? "" },
            { "oracle", oracle ?? "" },
            { "microAction", microAction ?? "" },
            { "locale", string.IsNullOrEmpty(locale) ? "zh-CN" : locale },
            { "oracleId", string.IsNullOrEmpty(oracleId) ? GetCurrentOracleId() : oracleId },
            { "syncEnabled", settings.ShouldPublishToFeed },
            { "visibility", settings.VisibilityKey },
            { "summaryOnly", true },
            { "createdAtLocal", DateTime.Now.ToString("o") },
            { "updatedAt", FieldValue.ServerTimestamp },
        };
    }

    private void SetSummaryData(string uid, string date, Dictionary<string, object> data, Action<bool> onComplete = null)
    {
        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(date) || data == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        GetSummaryDoc(uid, date)
            .SetAsync(data, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[DailyOracleFirestore] 保存每日占卜摘要失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                Debug.Log($"[DailyOracleFirestore] 每日占卜摘要已更新: {date}");
                onComplete?.Invoke(true);
            });
    }

    private void DisableSummaryByDate(string date, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;

        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(date))
        {
            onComplete?.Invoke(false);
            return;
        }

        var data = new Dictionary<string, object>
        {
            { "ownerUid", uid },
            { "date", date },
            { "syncEnabled", false },
            { "visibility", "only_me" },
            { "summaryOnly", true },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        SetSummaryData(uid, date, data, onComplete);
    }

    private DocumentReference GetDailyDoc(string uid, string date)
    {
        return _db.Collection("users")
            .Document(uid)
            .Collection("daily_oracles")
            .Document(date);
    }

    private DocumentReference GetSummaryDoc(string uid, string date)
    {
        return _db.Collection("daily_oracle_summaries")
            .Document($"{uid}_{date}");
    }

    private bool CheckReady(Action<bool> onFail)
    {
        if (!_isInitialized || _db == null)
        {
            onFail?.Invoke(false);
            return false;
        }
        return true;
    }

    private static string GetCurrentUid()
    {
        if (UserDataManager.Instance != null && !string.IsNullOrEmpty(UserDataManager.Instance.FirebaseUid))
            return UserDataManager.Instance.FirebaseUid;

        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        return auth?.CurrentUser?.UserId ?? "";
    }

    private static string GetCurrentOracleId()
    {
        if (RoleManager.Instance == null) return "tarot";
        return RoleManager.Instance.characterType switch
        {
            CharacterType.Astrologer => "astrology",
            CharacterType.Meditator => "sage",
            _ => "tarot",
        };
    }
}

[Serializable]
public class DailyOracleCloudRecord
{
    public string date;
    public string cardId;
    public string cardName;
    public string orientation;
    public string title;
    public string oracle;
    public string detail;
    public List<string> dos = new List<string>();
    public List<string> donts = new List<string>();
    public string microAction;
    public string locale;
    public string oracleId;
    public string createdAtLocal;
    public bool syncEnabled;
    public string visibility;
    public bool summaryOnly;

    public bool IsUpright => orientation != "reversed";

    public bool HasPayload =>
        !string.IsNullOrEmpty(cardId)
        && (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(oracle) || !string.IsNullOrEmpty(detail));

    public TodayOraclePayload ToPayload()
    {
        return new TodayOraclePayload
        {
            title = title ?? "",
            oracle = oracle ?? "",
            detail = detail ?? "",
            dos = dos ?? new List<string>(),
            donts = donts ?? new List<string>(),
            microAction = microAction ?? "",
        };
    }

    public static DailyOracleCloudRecord FromSnapshot(DocumentSnapshot doc)
    {
        if (doc == null || !doc.Exists) return null;

        var data = doc.ToDictionary();
        return new DailyOracleCloudRecord
        {
            date = GetStr(data, "date"),
            cardId = GetStr(data, "cardId"),
            cardName = GetStr(data, "cardName"),
            orientation = GetStr(data, "orientation"),
            title = GetStr(data, "title"),
            oracle = GetStr(data, "oracle"),
            detail = GetStr(data, "detail"),
            dos = GetStringList(data, "dos"),
            donts = GetStringList(data, "donts"),
            microAction = GetStr(data, "microAction"),
            locale = GetStr(data, "locale"),
            oracleId = GetStr(data, "oracleId"),
            createdAtLocal = GetStr(data, "createdAtLocal"),
            syncEnabled = GetBool(data, "syncEnabled", false),
            visibility = GetStr(data, "visibility"),
            summaryOnly = GetBool(data, "summaryOnly", false),
        };
    }

    private static string GetStr(Dictionary<string, object> dict, string key)
    {
        if (dict != null && dict.TryGetValue(key, out object value))
            return value?.ToString() ?? "";
        return "";
    }

    private static List<string> GetStringList(Dictionary<string, object> dict, string key)
    {
        var result = new List<string>();
        if (dict == null || !dict.TryGetValue(key, out object value) || value == null)
            return result;

        if (value is IEnumerable<object> objects)
        {
            foreach (var item in objects)
                result.Add(item?.ToString() ?? "");
        }
        else if (value is IEnumerable<string> strings)
        {
            result.AddRange(strings);
        }

        return result;
    }

    private static bool GetBool(Dictionary<string, object> dict, string key, bool fallback)
    {
        if (dict == null || !dict.TryGetValue(key, out object value) || value == null)
            return fallback;
        if (value is bool boolValue) return boolValue;
        return bool.TryParse(value.ToString(), out bool parsed) ? parsed : fallback;
    }
}

[Serializable]
public class DailyOracleSummaryRecord
{
    public string ownerUid;
    public string date;
    public string cardId;
    public string cardName;
    public string orientation;
    public string title;
    public string oracle;
    public string microAction;
    public string locale;
    public string oracleId;
    public bool syncEnabled;
    public string visibility;
    public bool summaryOnly;

    public bool IsUpright => orientation != "reversed";
    public bool IsVisibleInFriendFeed =>
        syncEnabled
        && summaryOnly
        && (visibility == "all_friends" || visibility == "real_friends");

    public static DailyOracleSummaryRecord FromSnapshot(DocumentSnapshot doc)
    {
        if (doc == null || !doc.Exists) return null;
        var data = doc.ToDictionary();
        return new DailyOracleSummaryRecord
        {
            ownerUid = GetStr(data, "ownerUid"),
            date = GetStr(data, "date"),
            cardId = GetStr(data, "cardId"),
            cardName = GetStr(data, "cardName"),
            orientation = GetStr(data, "orientation"),
            title = GetStr(data, "title"),
            oracle = GetStr(data, "oracle"),
            microAction = GetStr(data, "microAction"),
            locale = GetStr(data, "locale"),
            oracleId = GetStr(data, "oracleId"),
            syncEnabled = GetBool(data, "syncEnabled", false),
            visibility = GetStr(data, "visibility"),
            summaryOnly = GetBool(data, "summaryOnly", true),
        };
    }

    private static string GetStr(Dictionary<string, object> dict, string key)
    {
        if (dict != null && dict.TryGetValue(key, out object value))
            return value?.ToString() ?? "";
        return "";
    }

    private static bool GetBool(Dictionary<string, object> dict, string key, bool fallback)
    {
        if (dict == null || !dict.TryGetValue(key, out object value) || value == null)
            return fallback;
        if (value is bool boolValue) return boolValue;
        return bool.TryParse(value.ToString(), out bool parsed) ? parsed : fallback;
    }
}
