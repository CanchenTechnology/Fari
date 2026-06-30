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
    private const string LOCAL_CACHE_PREFIX = "DailyOracleCache_";
    private const string PENDING_SAVE_KEY_PREFIX = "DailyOraclePendingSaves_";
    private FirebaseFirestore _db;
    private bool _isInitialized = false;
    private bool _hasSubscribedFirebaseInit = false;
    private bool _hasSubscribedAuthEvents = false;
    private bool _isSyncingPendingSaves = false;

    [Serializable]
    private class PendingDateList
    {
        public List<string> dates = new List<string>();
    }

    public bool IsReady => _isInitialized && _db != null;

    protected override void Awake()
    {
        base.Awake();
        TryInit();
    }

    private void TryInit()
    {
        if (IsReady) return;

        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        if (authManager == null) return;

        SubscribeAuthEvents(authManager);

        if (authManager.IsFirebaseInitialized)
        {
            OnFirebaseReady();
        }
        else if (!_hasSubscribedFirebaseInit)
        {
            _hasSubscribedFirebaseInit = true;
            authManager.OnFirebaseInitialized += OnFirebaseReady;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeAuthEvents();

        FirebaseAuthManager authManager = FindObjectOfType<FirebaseAuthManager>();
        if (authManager != null && _hasSubscribedFirebaseInit)
            authManager.OnFirebaseInitialized -= OnFirebaseReady;
        _hasSubscribedFirebaseInit = false;
    }

    private void SubscribeAuthEvents(FirebaseAuthManager authManager)
    {
        if (authManager == null || _hasSubscribedAuthEvents)
            return;

        authManager.OnLoginSuccess += OnAuthLoginSuccess;
        authManager.OnLogout += OnAuthLogout;
        _hasSubscribedAuthEvents = true;
    }

    private void UnsubscribeAuthEvents()
    {
        if (!_hasSubscribedAuthEvents)
            return;

        FirebaseAuthManager authManager = FindObjectOfType<FirebaseAuthManager>();
        if (authManager != null)
        {
            authManager.OnLoginSuccess -= OnAuthLoginSuccess;
            authManager.OnLogout -= OnAuthLogout;
        }
        _hasSubscribedAuthEvents = false;
    }

    private void OnFirebaseReady()
    {
        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        if (authManager != null && _hasSubscribedFirebaseInit)
            authManager.OnFirebaseInitialized -= OnFirebaseReady;
        _hasSubscribedFirebaseInit = false;

        _db = FirebaseFirestore.DefaultInstance;
        _isInitialized = true;
        Debug.Log("[DailyOracleFirestore] Firebase 初始化完成后就绪");
        SyncPendingDailyOracleSaves();
    }

    private void OnAuthLoginSuccess(AuthProvider provider, Firebase.Auth.FirebaseUser user)
    {
        if (!IsReady)
            TryInit();

        SyncPendingDailyOracleSaves();
    }

    private void OnAuthLogout()
    {
        _isSyncingPendingSaves = false;
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
        if (!IsReady)
        {
            onComplete?.Invoke(LoadByDateLocal(date));
            return;
        }

        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            onComplete?.Invoke(LoadByDateLocal(date));
            return;
        }

        GetDailyDoc(uid, date)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                {
                    onComplete?.Invoke(LoadByDateLocal(date));
                    return;
                }

                DailyOracleCloudRecord record = DailyOracleCloudRecord.FromSnapshot(task.Result);
                if (record != null && record.HasPayload)
                    SaveRecordLocal(record);
                onComplete?.Invoke(record);
            });
    }

    public void SaveToday(TarotCard card, bool upright, TodayOraclePayload payload, string locale, Action<bool> onComplete = null)
    {
        SaveByDate(DateTime.Now.ToString("yyyy-MM-dd"), card, upright, payload, locale, onComplete);
    }

    public void SaveByDate(string date, TarotCard card, bool upright, TodayOraclePayload payload, string locale, Action<bool> onComplete = null)
    {
        if (card == null || payload == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        SaveByDateLocal(date, card, upright, payload, locale);
        QueuePendingDailyOracleSave(date);

        if (!CheckReady(onComplete)) return;

        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            onComplete?.Invoke(false);
            return;
        }

        DailyOracleCloudRecord record = LoadByDateLocal(date);
        SaveRecordToCloud(record, uid, onComplete);
    }

    public static DailyOracleCloudRecord LoadTodayLocal()
    {
        return LoadByDateLocal(DateTime.Now.ToString("yyyy-MM-dd"));
    }

    public static DailyOracleCloudRecord LoadByDateLocal(string date)
    {
        if (string.IsNullOrWhiteSpace(date))
            date = DateTime.Now.ToString("yyyy-MM-dd");

        foreach (string key in GetLocalCacheKeys(date))
        {
            string json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrWhiteSpace(json)) continue;

            try
            {
                DailyOracleCloudRecord record = JsonUtility.FromJson<DailyOracleCloudRecord>(json);
                if (record != null && record.date == date && record.HasPayload)
                    return record;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DailyOracleFirestore] 本地今日神谕缓存解析失败: {e.Message}");
            }
        }

        return null;
    }

    public static void SaveTodayLocal(TarotCard card, bool upright, TodayOraclePayload payload, string locale)
    {
        SaveByDateLocal(DateTime.Now.ToString("yyyy-MM-dd"), card, upright, payload, locale);
    }

    public static void SaveTodayLocalPending(TarotCard card, bool upright, TodayOraclePayload payload, string locale)
    {
        SaveByDateLocalPending(DateTime.Now.ToString("yyyy-MM-dd"), card, upright, payload, locale);
    }

    public static void SaveByDateLocalPending(string date, TarotCard card, bool upright, TodayOraclePayload payload, string locale)
    {
        SaveByDateLocal(date, card, upright, payload, locale);
        QueuePendingDailyOracleSave(string.IsNullOrWhiteSpace(date) ? DateTime.Now.ToString("yyyy-MM-dd") : date);
    }

    public static void ClearLocalCacheForCurrentUser(int daysBack = 366)
    {
        int safeDaysBack = Mathf.Clamp(daysBack, 1, 3660);
        for (int i = 0; i < safeDaysBack; i++)
        {
            string date = DateTime.Now.Date.AddDays(-i).ToString("yyyy-MM-dd");
            foreach (string key in GetLocalCacheKeys(date))
                PlayerPrefs.DeleteKey(key);
        }

        foreach (string key in GetPendingSaveKeysForSync())
            PlayerPrefs.DeleteKey(key);

        PlayerPrefs.Save();
        Debug.Log("[DailyOracleFirestore] 已清除当前用户本地每日神谕缓存");
    }

    public static void ClearTodayLocalCacheForCurrentUser()
    {
        ClearLocalCacheForDate(DateTime.Now.ToString("yyyy-MM-dd"));
    }

    public static void ClearLocalCacheForDate(string date)
    {
        if (string.IsNullOrWhiteSpace(date))
            date = DateTime.Now.ToString("yyyy-MM-dd");

        foreach (string key in GetLocalCacheKeys(date))
            PlayerPrefs.DeleteKey(key);

        foreach (string key in GetPendingSaveKeysForSync())
        {
            List<string> pending = LoadPendingSaves(key);
            if (pending.RemoveAll(item => item == date) > 0)
                SavePendingSaves(key, pending);
        }

        PlayerPrefs.Save();
        Debug.Log($"[DailyOracleFirestore] 已清除当前用户 {date} 本地每日神谕缓存");
    }

    public static void SaveByDateLocal(string date, TarotCard card, bool upright, TodayOraclePayload payload, string locale)
    {
        if (card == null || payload == null) return;
        if (string.IsNullOrWhiteSpace(date))
            date = DateTime.Now.ToString("yyyy-MM-dd");

        SaveRecordLocal(new DailyOracleCloudRecord
        {
            date = date,
            cardId = card.cardId ?? "",
            cardName = card.nameZh ?? "",
            orientation = upright ? "upright" : "reversed",
            title = payload.title ?? "",
            oracle = payload.oracle ?? "",
            detail = payload.detail ?? "",
            dos = payload.dos ?? new List<string>(),
            donts = payload.donts ?? new List<string>(),
            microAction = payload.microAction ?? "",
            locale = string.IsNullOrEmpty(locale) ? "zh-CN" : locale,
            oracleId = GetCurrentOracleId(),
            createdAtLocal = DateTime.Now.ToString("o"),
            syncEnabled = DailyDivinationSyncSettingsManager.Instance.Enabled,
            visibility = DailyDivinationSyncSettingsManager.Instance.GetSettings().VisibilityKey,
            summaryOnly = false,
        });
    }

    private static void SaveRecordLocal(DailyOracleCloudRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.date) || !record.HasPayload)
            return;

        string json = JsonUtility.ToJson(record);
        foreach (string key in GetWritableLocalCacheKeys(record.date))
            PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    private void SaveRecordToCloud(DailyOracleCloudRecord record, string uid, Action<bool> onComplete = null)
    {
        if (record == null || !record.HasPayload || string.IsNullOrWhiteSpace(record.date) || string.IsNullOrWhiteSpace(uid))
        {
            onComplete?.Invoke(false);
            return;
        }

        GetDailyDoc(uid, record.date)
            .SetAsync(BuildDailyOracleData(record), SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[DailyOracleFirestore] 保存今日神谕失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                Debug.Log($"[DailyOracleFirestore] 今日神谕已保存: {record.date}");
                RemovePendingDailyOracleSave(record.date);
                SaveRecordLocal(record);
                SaveSummaryFromRecord(record, DailyDivinationSyncSettingsManager.Instance.GetSettings());
                onComplete?.Invoke(true);
            });
    }

    private Dictionary<string, object> BuildDailyOracleData(DailyOracleCloudRecord record)
    {
        DailyDivinationSyncSettings settings = DailyDivinationSyncSettingsManager.Instance.GetSettings();
        return new Dictionary<string, object>
        {
            { "date", record.date ?? "" },
            { "cardId", record.cardId ?? "" },
            { "cardName", record.cardName ?? "" },
            { "orientation", record.orientation ?? "" },
            { "title", record.title ?? "" },
            { "oracle", record.oracle ?? "" },
            { "detail", record.detail ?? "" },
            { "dos", record.dos ?? new List<string>() },
            { "donts", record.donts ?? new List<string>() },
            { "microAction", record.microAction ?? "" },
            { "locale", string.IsNullOrEmpty(record.locale) ? "zh-CN" : record.locale },
            { "oracleId", string.IsNullOrEmpty(record.oracleId) ? GetCurrentOracleId() : record.oracleId },
            { "syncEnabled", settings.ShouldPublishToFeed },
            { "visibility", settings.VisibilityKey },
            { "summaryOnly", false },
            { "createdAtLocal", string.IsNullOrEmpty(record.createdAtLocal) ? DateTime.Now.ToString("o") : record.createdAtLocal },
            { "updatedAt", FieldValue.ServerTimestamp },
        };
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

    public void ApplySyncSettingsToPublishedSummaries(DailyDivinationSyncSettings settings, int daysBack = 30, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;

        settings ??= DailyDivinationSyncSettingsManager.Instance.GetSettings();
        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            onComplete?.Invoke(false);
            return;
        }

        int safeDaysBack = Mathf.Clamp(daysBack, 1, 90);
        int pending = safeDaysBack;
        bool hasFailure = false;
        string today = DateTime.Now.ToString("yyyy-MM-dd");

        void CompleteOne(bool success)
        {
            if (!success) hasFailure = true;
            pending--;
            if (pending <= 0)
                onComplete?.Invoke(!hasFailure);
        }

        for (int i = 0; i < safeDaysBack; i++)
        {
            string date = DateTime.Now.AddDays(-i).ToString("yyyy-MM-dd");
            if (settings.ShouldPublishToFeed && date == today)
                PublishSummaryByDate(uid, date, settings, CompleteOne);
            else
                UpdateExistingSummarySettingsByDate(uid, date, settings, CompleteOne);
        }
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
                if (summary != null && summary.IsVisibleInFriendFeed)
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

    private void PublishSummaryByDate(string uid, string date, DailyDivinationSyncSettings settings, Action<bool> onComplete)
    {
        GetDailyDoc(uid, date)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[DailyOracleFirestore] 读取每日牌用于同步摘要失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                if (!task.Result.Exists)
                {
                    UpdateExistingSummarySettingsByDate(uid, date, settings, onComplete);
                    return;
                }

                DailyOracleCloudRecord record = DailyOracleCloudRecord.FromSnapshot(task.Result);
                if (record == null || !record.HasPayload)
                {
                    UpdateExistingSummarySettingsByDate(uid, date, settings, onComplete);
                    return;
                }

                SaveSummaryFromRecord(record, settings, onComplete);
            });
    }

    private void UpdateExistingSummarySettingsByDate(string uid, string date, DailyDivinationSyncSettings settings, Action<bool> onComplete)
    {
        GetSummaryDoc(uid, date)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[DailyOracleFirestore] 读取每日占卜摘要权限失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                if (!task.Result.Exists)
                {
                    onComplete?.Invoke(true);
                    return;
                }

                var data = new Dictionary<string, object>
                {
                    { "ownerUid", uid },
                    { "date", date },
                    { "syncEnabled", settings.ShouldPublishToFeed },
                    { "visibility", settings.VisibilityKey },
                    { "summaryOnly", true },
                    { "updatedAt", FieldValue.ServerTimestamp },
                };

                SetSummaryData(uid, date, data, onComplete);
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

    private void SyncPendingDailyOracleSaves()
    {
        if (_isSyncingPendingSaves || !IsReady)
            return;

        string uid = GetCurrentUid();
        if (string.IsNullOrWhiteSpace(uid))
            return;

        HashSet<string> pendingDates = LoadPendingDailyOracleSaveDates();
        if (pendingDates.Count == 0)
            return;

        int pendingOperations = 0;
        _isSyncingPendingSaves = true;

        void FinishOne()
        {
            pendingOperations--;
            if (pendingOperations <= 0)
                _isSyncingPendingSaves = false;
        }

        foreach (string date in pendingDates)
        {
            DailyOracleCloudRecord record = LoadByDateLocal(date);
            if (record == null || !record.HasPayload)
            {
                RemovePendingDailyOracleSave(date);
                continue;
            }

            pendingOperations++;
            SaveRecordToCloud(record, uid, success =>
            {
                if (success)
                    Debug.Log($"[DailyOracleFirestore] 已同步待上传每日神谕: {date}");
                FinishOne();
            });
        }

        if (pendingOperations == 0)
            _isSyncingPendingSaves = false;
    }

    private static void QueuePendingDailyOracleSave(string date)
    {
        if (string.IsNullOrWhiteSpace(date)) return;

        string key = GetPendingSaveKey();
        List<string> pending = LoadPendingSaves(key);
        if (!pending.Exists(item => item == date))
            pending.Add(date);

        SavePendingSaves(key, pending);
    }

    private static void RemovePendingDailyOracleSave(string date)
    {
        if (string.IsNullOrWhiteSpace(date)) return;

        foreach (string key in GetPendingSaveKeysForSync())
        {
            List<string> pending = LoadPendingSaves(key);
            if (pending.RemoveAll(item => item == date) > 0)
                SavePendingSaves(key, pending);
        }
    }

    private static HashSet<string> LoadPendingDailyOracleSaveDates()
    {
        HashSet<string> dates = new HashSet<string>();
        foreach (string key in GetPendingSaveKeysForSync())
        {
            foreach (string date in LoadPendingSaves(key))
            {
                if (!string.IsNullOrWhiteSpace(date))
                    dates.Add(date);
            }
        }

        return dates;
    }

    private static string GetPendingSaveKey()
    {
        string owner = FirstNonEmpty(
            UserDataManager.Instance?.FirebaseUid,
            Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser?.UserId,
            UserDataManager.Instance?.UserId,
            "local");
        return BuildPendingSaveKey(owner);
    }

    private static IEnumerable<string> GetPendingSaveKeysForSync()
    {
        HashSet<string> keys = new HashSet<string>();

        foreach (string ownerKey in GetLocalOwnerKeys())
        {
            string key = BuildPendingSaveKey(ownerKey);
            if (keys.Add(key))
                yield return key;
        }

        string localKey = BuildPendingSaveKey("local");
        if (keys.Add(localKey))
            yield return localKey;
    }

    private static List<string> LoadPendingSaves(string key)
    {
        if (string.IsNullOrEmpty(key)) return new List<string>();

        string json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();

        try
        {
            PendingDateList data = JsonUtility.FromJson<PendingDateList>(json);
            return data?.dates ?? new List<string>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DailyOracleFirestore] 待上传每日神谕队列读取失败，已重置: {e.Message}");
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return new List<string>();
        }
    }

    private static void SavePendingSaves(string key, List<string> pending)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (pending == null || pending.Count == 0)
            PlayerPrefs.DeleteKey(key);
        else
            PlayerPrefs.SetString(key, JsonUtility.ToJson(new PendingDateList { dates = pending }));

        PlayerPrefs.Save();
    }

    private static IEnumerable<string> GetLocalCacheKeys(string date)
    {
        HashSet<string> keys = new HashSet<string>();

        foreach (string ownerKey in GetLocalOwnerKeys())
        {
            string key = BuildLocalCacheKey(ownerKey, date);
            if (keys.Add(key))
                yield return key;
        }

        string localKey = BuildLocalCacheKey("local", date);
        if (keys.Add(localKey))
            yield return localKey;
    }

    private static IEnumerable<string> GetWritableLocalCacheKeys(string date)
    {
        string primaryOwner = FirstNonEmpty(
            UserDataManager.Instance?.FirebaseUid,
            Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser?.UserId,
            UserDataManager.Instance?.UserId,
            "local");

        yield return BuildLocalCacheKey(primaryOwner, date);

        if (primaryOwner != "local")
            yield return BuildLocalCacheKey("local", date);
    }

    private static IEnumerable<string> GetLocalOwnerKeys()
    {
        yield return FirstNonEmpty(UserDataManager.Instance?.FirebaseUid, "");
        yield return FirstNonEmpty(Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser?.UserId, "");
        yield return FirstNonEmpty(UserDataManager.Instance?.UserId, "");
        yield return "local";
    }

    private static string BuildLocalCacheKey(string ownerKey, string date)
    {
        return LOCAL_CACHE_PREFIX + SanitizeKey(string.IsNullOrWhiteSpace(ownerKey) ? "local" : ownerKey) + "_" + date;
    }

    private static string BuildPendingSaveKey(string ownerKey)
    {
        return PENDING_SAVE_KEY_PREFIX + SanitizeKey(string.IsNullOrWhiteSpace(ownerKey) ? "local" : ownerKey);
    }

    private static string SanitizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "local";
        return value.Replace(" ", "_").Replace("/", "_").Replace("\\", "_").Replace(":", "_");
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null) return "";
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return "";
    }

    private static string GetCurrentUid()
    {
        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        string authUid = auth?.CurrentUser?.UserId ?? "";
        if (string.IsNullOrWhiteSpace(authUid))
        {
            Debug.LogWarning("[DailyOracleFirestore] FirebaseAuth 当前用户为空，跳过云端每日神谕操作");
            return "";
        }

        string cachedUid = UserDataManager.Instance?.FirebaseUid ?? "";
        if (!string.IsNullOrWhiteSpace(cachedUid) && cachedUid != authUid)
        {
            Debug.LogWarning($"[DailyOracleFirestore] 本地 FirebaseUid({cachedUid}) 与 AuthUid({authUid}) 不一致，已使用 AuthUid");
        }

        return authUid;
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
