using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Extensions;
using XFGameFrameWork;
using GamerFrameWork.OracleRuntime;

/// <summary>
/// 占卜记录 Firestore 管理器
/// 负责占卜记录的云端增删改查
///
/// Firestore 数据结构：
///   users/{uid}/divination_records/{readingId}
///     - readingId          : string   (占卜会话ID)
///     - question           : string   (用户问题)
///     - scene              : string   (场景)
///     - spreadKind         : string   (牌阵类型)
///     - lockedCards        : array    (抽到的牌 [{positionKey, position, cardId, cardName, orientation}])
///     - shortVerdict       : string   (AI 解读摘要)
///     - oracleId           : string   (神谕师类型: tarot/astrology/sage)
///     - createdAt          : Timestamp (创建时间)
///     - updatedAt          : Timestamp (更新时间)
/// </summary>
public class DivinationRecordFirestore : MonoSingleton<DivinationRecordFirestore>
{
    private const string CACHE_KEY_PREFIX = "DivinationRecordCache_";
    private const string PENDING_DELETE_KEY_PREFIX = "DivinationRecordPendingDeletes_";
    private const int MAX_CACHED_RECORDS = 100;
    private FirebaseFirestore _db;
    private bool _isInitialized = false;
    private bool _hasSubscribedFirebaseInit = false;
    private bool _initRetryScheduled = false;

    [Serializable]
    private class PendingRecordDeleteList
    {
        public List<string> readingIds = new List<string>();
    }

    /// <summary>Firestore 是否已初始化</summary>
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
        if (authManager == null)
        {
            ScheduleInitRetry();
            return;
        }

        if (authManager.IsFirebaseInitialized)
        {
            OnFirebaseReady();
            return;
        }

        if (!_hasSubscribedFirebaseInit)
        {
            _hasSubscribedFirebaseInit = true;
            authManager.OnFirebaseInitialized += OnFirebaseReady;
        }
    }

    private void ScheduleInitRetry()
    {
        if (_initRetryScheduled) return;

        _initRetryScheduled = true;
        Invoke(nameof(RetryInit), 0.5f);
    }

    private void RetryInit()
    {
        _initRetryScheduled = false;
        TryInit();
    }

    private void OnFirebaseReady()
    {
        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        if (authManager != null && _hasSubscribedFirebaseInit)
            authManager.OnFirebaseInitialized -= OnFirebaseReady;
        _hasSubscribedFirebaseInit = false;

        try
        {
            _db = FirebaseFirestore.DefaultInstance;
            _isInitialized = true;
            Debug.Log("[DivinationRecordFirestore] Firebase 初始化完成后就绪");
            ClearLocalCacheForCurrentUser();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DivinationRecordFirestore] 初始化失败: {e.Message}");
            ScheduleInitRetry();
        }
    }

    #region 写入

    /// <summary>
    /// 保存一条占卜记录到 Firestore
    /// </summary>
    public void SaveRecord(DivinationRecordData record, Action<bool> onComplete = null)
    {
        SaveRecord(record, true, onComplete);
    }

    public void SaveRecord(DivinationRecordData record, bool restoreDeleted, Action<bool> onComplete = null)
    {
        if (record == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (string.IsNullOrEmpty(record.readingId))
            record.readingId = "record_" + Guid.NewGuid().ToString("N");
        if (string.IsNullOrEmpty(record.createdAt))
            record.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        if (!CheckReady(onComplete)) return;

        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning("[DivinationRecordFirestore] 用户未登录，无法保存记录");
            onComplete?.Invoke(false);
            return;
        }

        DocumentReference docRef = _db.Collection("users")
            .Document(uid)
            .Collection("divination_records")
            .Document(record.readingId);

        var data = SerializeRecord(record);
        data["updatedAt"] = FieldValue.ServerTimestamp;

        // 使用 MergeAll 以便部分更新
        docRef.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[DivinationRecordFirestore] 保存记录失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }
            Debug.Log($"[DivinationRecordFirestore] 记录已保存: {record.readingId}");
            onComplete?.Invoke(true);
        });
    }

    public static bool SaveRecordLocal(DivinationRecordData record)
    {
        return SaveRecordLocal(record, true);
    }

    public static bool SaveRecordLocal(DivinationRecordData record, bool restoreDeleted)
    {
        Debug.LogWarning("[DivinationRecordFirestore] 占卜历史不再保存到本地，请使用 SaveRecord 写入 Firebase。");
        return false;
    }

    public static void ClearLocalCacheForCurrentUser()
    {
        foreach (string key in GetCacheKeysForCurrentUserLocal())
            PlayerPrefs.DeleteKey(key);

        foreach (string key in GetPendingRecordDeleteKeysForCurrentUserLocal())
            PlayerPrefs.DeleteKey(key);

        PlayerPrefs.Save();
        Debug.Log("[DivinationRecordFirestore] 已清除当前用户本地占卜历史缓存");
    }

    /// <summary>
    /// 从 DivinationSession 自动构建并保存记录
    /// </summary>
    public void SaveFromSession(DivinationSession session, string shortVerdict, Action<bool> onComplete = null)
    {
        if (session == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        var record = new DivinationRecordData
        {
            readingId = session.readingId,
            question = session.question,
            scene = session.scene,
            spreadKind = session.spreadKind,
            lockedCards = session.lockedCards ?? new List<LockedCard>(),
            shortVerdict = shortVerdict ?? session.shortVerdict ?? "",
            judgeContent = session.judgeContent ?? "",
            adviceContent = session.adviceContent ?? "",
            topics = session.topics ?? new List<string>(),
            oracleId = GetCurrentOracleId(),
            createdAt = session.createdAt,
        };

        SaveRecord(record, onComplete);
    }

    #endregion

    #region 读取

    /// <summary>
    /// 加载用户的所有占卜记录（按时间倒序）
    /// </summary>
    public void LoadAllRecords(Action<List<DivinationRecordData>> onComplete)
    {
        if (!IsReady)
        {
            onComplete?.Invoke(new List<DivinationRecordData>());
            return;
        }

        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning("[DivinationRecordFirestore] 用户未登录");
            onComplete?.Invoke(new List<DivinationRecordData>());
            return;
        }

        var records = new List<DivinationRecordData>();

        _db.Collection("users")
            .Document(uid)
            .Collection("divination_records")
            .OrderByDescending("createdAt")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[DivinationRecordFirestore] 加载记录失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(new List<DivinationRecordData>());
                    return;
                }

                foreach (var doc in task.Result.Documents)
                {
                    var record = DeserializeRecord(doc);
                    AddOrReplaceRecord(records, record);
                }

                Debug.Log($"[DivinationRecordFirestore] 加载了 {records.Count} 条记录");
                onComplete?.Invoke(records);
            });
    }

    /// <summary>
    /// 加载单条占卜记录
    /// </summary>
    public void LoadRecord(string readingId, Action<DivinationRecordData> onComplete)
    {
        if (!IsReady)
        {
            onComplete?.Invoke(null);
            return;
        }

        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            onComplete?.Invoke(null);
            return;
        }

        _db.Collection("users")
            .Document(uid)
            .Collection("divination_records")
            .Document(readingId)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                {
                    onComplete?.Invoke(null);
                    return;
                }
                var record = DeserializeRecord(task.Result);
                onComplete?.Invoke(record);
            });
    }

    #endregion

    #region 删除

    /// <summary>
    /// 删除一条占卜记录
    /// </summary>
    public void DeleteRecord(string readingId, Action<bool> onComplete = null)
    {
        if (string.IsNullOrEmpty(readingId))
        {
            onComplete?.Invoke(false);
            return;
        }

        if (!IsReady)
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

        _db.Collection("users")
            .Document(uid)
            .Collection("divination_records")
            .Document(readingId)
            .DeleteAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[DivinationRecordFirestore] 删除失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }
                Debug.Log($"[DivinationRecordFirestore] 已删除记录: {readingId}");
                onComplete?.Invoke(true);
            });
    }

    #endregion

    #region 序列化

    private Dictionary<string, object> SerializeRecord(DivinationRecordData record)
    {
        var data = new Dictionary<string, object>
        {
            { "readingId",  record.readingId ?? "" },
            { "question",   record.question ?? "" },
            { "scene",      record.scene ?? "" },
            { "spreadKind", record.spreadKind ?? "" },
            { "shortVerdict", record.shortVerdict ?? "" },
            { "judgeContent", record.judgeContent ?? "" },
            { "adviceContent", record.adviceContent ?? "" },
            { "oracleId",   record.oracleId ?? "tarot" },
        };

        // 序列化追问话题
        if (record.topics != null && record.topics.Count > 0)
        {
            data["topics"] = record.topics;
        }

        // 序列化抽到的牌
        var cardsList = new List<Dictionary<string, object>>();
        if (record.lockedCards != null)
        {
            foreach (var card in record.lockedCards)
            {
                cardsList.Add(new Dictionary<string, object>
                {
                    { "positionKey",  card.positionKey ?? "" },
                    { "position",     card.position ?? "" },
                    { "cardId",       card.cardId ?? "" },
                    { "cardName",     card.cardName ?? "" },
                    { "orientation",  card.orientation ?? "" },
                });
            }
        }
        data["lockedCards"] = cardsList;

        // createdAt: 如果已有字符串时间，尝试解析为 Timestamp
        if (!string.IsNullOrEmpty(record.createdAt))
        {
            if (DateTime.TryParse(record.createdAt, out var dt))
            {
                data["createdAt"] = Timestamp.FromDateTime(dt.ToUniversalTime());
            }
            else
            {
                data["createdAt"] = FieldValue.ServerTimestamp;
            }
        }
        else
        {
            data["createdAt"] = FieldValue.ServerTimestamp;
        }

        return data;
    }

    private DivinationRecordData DeserializeRecord(DocumentSnapshot doc)
    {
        if (doc == null || !doc.Exists) return null;

        var record = new DivinationRecordData();
        var d = doc.ToDictionary();

        record.readingId     = GetStr(d, "readingId");
        record.question      = GetStr(d, "question");
        record.scene         = GetStr(d, "scene");
        record.spreadKind    = GetStr(d, "spreadKind");
        record.shortVerdict  = GetStr(d, "shortVerdict");
        record.judgeContent  = GetStr(d, "judgeContent");
        record.adviceContent = GetStr(d, "adviceContent");
        record.oracleId      = GetStr(d, "oracleId");

        // 解析追问话题
        record.topics = new List<string>();
        if (d.TryGetValue("topics", out object topicsObj) && topicsObj is List<object> topicList)
        {
            foreach (var t in topicList)
                record.topics.Add(t?.ToString() ?? "");
        }

        // 解析时间戳
        if (doc.TryGetValue("createdAt", out Timestamp ts))
        {
            record.createdAt = ts.ToDateTime().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }
        else if (d.TryGetValue("createdAt", out object createdAtObj))
        {
            record.createdAt = createdAtObj?.ToString() ?? "";
        }

        // 解析 lockedCards
        record.lockedCards = new List<LockedCard>();
        if (d.TryGetValue("lockedCards", out object cardsObj))
        {
            if (cardsObj is List<object> cardList)
            {
                foreach (var c in cardList)
                {
                    if (c is Dictionary<string, object> cardDict)
                    {
                        record.lockedCards.Add(new LockedCard
                        {
                            positionKey = GetStr(cardDict, "positionKey"),
                            position    = GetStr(cardDict, "position"),
                            cardId      = GetStr(cardDict, "cardId"),
                            cardName    = GetStr(cardDict, "cardName"),
                            orientation = GetStr(cardDict, "orientation"),
                        });
                    }
                }
            }
        }

        return record;
    }

    #endregion

    #region 工具方法

    private bool CheckReady(Action<bool> onFail)
    {
        if (!_isInitialized || _db == null)
        {
            Debug.LogWarning("[DivinationRecordFirestore] Firestore 尚未就绪");
            onFail?.Invoke(false);
            return false;
        }
        return true;
    }

    private string GetCurrentUid()
    {
        if (UserDataManager.Instance != null && !string.IsNullOrWhiteSpace(UserDataManager.Instance.FirebaseUid))
            return UserDataManager.Instance.FirebaseUid;

        var auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        return auth?.CurrentUser?.UserId ?? "";
    }

    private string GetCurrentOracleId()
    {
        if (RoleManager.Instance == null) return "tarot";
        return RoleManager.Instance.characterType switch
        {
            CharacterType.Astrologer => "astrology",
            CharacterType.Meditator  => "sage",
            _                        => "tarot",
        };
    }

    private static string GetStr(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out object val))
            return val?.ToString() ?? "";
        return "";
    }

    public List<DivinationRecordData> LoadCachedRecords()
    {
        return new List<DivinationRecordData>();
    }

    private DivinationRecordData FindCachedRecord(string readingId)
    {
        if (string.IsNullOrEmpty(readingId)) return null;

        var records = LoadCachedRecords();
        foreach (var record in records)
        {
            if (record != null && record.readingId == readingId)
                return record;
        }
        return null;
    }

    private void CacheRecord(DivinationRecordData record)
    {
        if (record == null || string.IsNullOrEmpty(record.readingId)) return;

        var records = LoadCachedRecords();
        records.RemoveAll(item => item == null || item.readingId == record.readingId);
        records.Insert(0, record);
        SaveRecordsToCache(records);
    }

    private bool RemoveCachedRecord(string readingId)
    {
        if (string.IsNullOrEmpty(readingId)) return false;

        bool removedAny = false;
        foreach (string key in GetCacheKeysForCurrentUserLocal())
        {
            var records = LoadCachedRecordsForKey(key);
            int removed = records.RemoveAll(item => item == null || item.readingId == readingId);
            if (removed > 0)
            {
                removedAny = true;
                SaveRecordsToCacheLocal(records, key);
            }
        }

        return removedAny;
    }

    private void SaveRecordsToCache(List<DivinationRecordData> records)
    {
        SaveRecordsToCacheLocal(records);
    }

    private static List<DivinationRecordData> LoadCachedRecordsLocal()
    {
        var merged = new List<DivinationRecordData>();
        foreach (string key in GetCacheKeysForCurrentUserLocal())
            MergeRecords(merged, LoadCachedRecordsForKey(key));

        SortRecordsDescendingLocal(merged);
        return merged;
    }

    private static List<DivinationRecordData> LoadCachedRecordsForKey(string key)
    {
        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(json)) return new List<DivinationRecordData>();

        try
        {
            var wrapper = JsonUtility.FromJson<DivinationRecordCacheWrapper>(json);
            var records = wrapper?.records ?? new List<DivinationRecordData>();
            SortRecordsDescendingLocal(records);
            return records;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DivinationRecordFirestore] 旧版本历史缓存读取失败: " + e.Message);
            return new List<DivinationRecordData>();
        }
    }

    private static void SaveRecordsToCacheLocal(List<DivinationRecordData> records)
    {
        SaveRecordsToCacheLocal(records, GetCacheKeyLocal());
    }

    private static void SaveRecordsToCacheLocal(List<DivinationRecordData> records, string key)
    {
        if (records == null) records = new List<DivinationRecordData>();
        if (string.IsNullOrEmpty(key)) return;

        records.RemoveAll(record => record == null || string.IsNullOrEmpty(record.readingId));
        SortRecordsDescendingLocal(records);
        if (records.Count > MAX_CACHED_RECORDS)
            records.RemoveRange(MAX_CACHED_RECORDS, records.Count - MAX_CACHED_RECORDS);

        string json = JsonUtility.ToJson(new DivinationRecordCacheWrapper { records = records });
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    private static void MergeRecords(List<DivinationRecordData> target, List<DivinationRecordData> source)
    {
        if (target == null || source == null) return;

        foreach (var record in source)
            AddOrReplaceRecord(target, record);

        SortRecordsDescendingLocal(target);
        if (target.Count > MAX_CACHED_RECORDS)
            target.RemoveRange(MAX_CACHED_RECORDS, target.Count - MAX_CACHED_RECORDS);
    }

    private static void RemovePendingDeletedRecords(List<DivinationRecordData> records, HashSet<string> pendingDeletes)
    {
        if (records == null || pendingDeletes == null || pendingDeletes.Count == 0) return;
        records.RemoveAll(record => record == null || pendingDeletes.Contains(record.readingId));
    }

    private static void AddOrReplaceRecord(List<DivinationRecordData> records, DivinationRecordData record)
    {
        if (records == null || record == null || string.IsNullOrEmpty(record.readingId)) return;

        records.RemoveAll(item => item == null || item.readingId == record.readingId);
        records.Add(record);
    }

    private static void SortRecordsDescendingLocal(List<DivinationRecordData> records)
    {
        records.Sort((a, b) =>
        {
            DateTime aTime = ParseRecordTimeLocal(a);
            DateTime bTime = ParseRecordTimeLocal(b);
            return bTime.CompareTo(aTime);
        });
    }

    private static DateTime ParseRecordTimeLocal(DivinationRecordData record)
    {
        if (record != null && DateTime.TryParse(record.createdAt, out var parsed))
            return parsed;
        return DateTime.MinValue;
    }

    private static string GetCacheKeyLocal()
    {
        foreach (string key in GetCacheKeysForCurrentUserLocal())
            return key;

        return BuildCacheKey("local");
    }

    private void SortRecordsDescending(List<DivinationRecordData> records)
    {
        records.Sort((a, b) =>
        {
            DateTime aTime = ParseRecordTime(a);
            DateTime bTime = ParseRecordTime(b);
            return bTime.CompareTo(aTime);
        });
    }

    private DateTime ParseRecordTime(DivinationRecordData record)
    {
        if (record != null && DateTime.TryParse(record.createdAt, out var parsed))
            return parsed;
        return DateTime.MinValue;
    }

    private static List<string> GetCacheKeysForCurrentUserLocal()
    {
        var keys = new List<string>();

        void AddKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string key = BuildCacheKey(value);
            if (!keys.Contains(key))
                keys.Add(key);
        }

        AddKey(UserDataManager.Instance?.FirebaseUid);
        AddKey(Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser?.UserId);
        AddKey(UserDataManager.Instance?.UserId);
        AddKey("local");
        return keys;
    }

    private static bool IsPendingRecordDelete(string readingId)
    {
        if (string.IsNullOrWhiteSpace(readingId)) return false;
        return LoadPendingRecordDeletesForCurrentUserLocal().Contains(readingId);
    }

    private static void QueuePendingRecordDelete(string readingId)
    {
        if (string.IsNullOrWhiteSpace(readingId)) return;

        string key = GetPendingRecordDeleteKeyLocal();
        List<string> pending = LoadPendingRecordDeletes(key);
        if (!pending.Contains(readingId))
            pending.Add(readingId);

        SavePendingRecordDeletes(key, pending);
    }

    private static void RemovePendingRecordDelete(string readingId)
    {
        if (string.IsNullOrWhiteSpace(readingId)) return;

        foreach (string key in GetPendingRecordDeleteKeysForCurrentUserLocal())
        {
            List<string> pending = LoadPendingRecordDeletes(key);
            if (pending.RemoveAll(item => item == readingId) > 0)
                SavePendingRecordDeletes(key, pending);
        }
    }

    private static HashSet<string> LoadPendingRecordDeletesForCurrentUserLocal()
    {
        var result = new HashSet<string>();
        foreach (string key in GetPendingRecordDeleteKeysForCurrentUserLocal())
        {
            foreach (string readingId in LoadPendingRecordDeletes(key))
            {
                if (!string.IsNullOrWhiteSpace(readingId))
                    result.Add(readingId);
            }
        }

        return result;
    }

    private static List<string> LoadPendingRecordDeletes(string key)
    {
        if (string.IsNullOrEmpty(key)) return new List<string>();

        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrEmpty(json)) return new List<string>();

        try
        {
            PendingRecordDeleteList data = JsonUtility.FromJson<PendingRecordDeleteList>(json);
            return data?.readingIds ?? new List<string>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DivinationRecordFirestore] 待同步历史删除队列读取失败，已重置。{e.Message}");
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return new List<string>();
        }
    }

    private static void SavePendingRecordDeletes(string key, List<string> pending)
    {
        if (string.IsNullOrEmpty(key)) return;

        pending ??= new List<string>();
        pending.RemoveAll(string.IsNullOrWhiteSpace);
        if (pending.Count == 0)
        {
            PlayerPrefs.DeleteKey(key);
        }
        else
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(new PendingRecordDeleteList { readingIds = pending }));
        }

        PlayerPrefs.Save();
    }

    private static string GetPendingRecordDeleteKeyLocal()
    {
        foreach (string key in GetPendingRecordDeleteKeysForCurrentUserLocal())
            return key;

        return BuildPendingRecordDeleteKey("local");
    }

    private static List<string> GetPendingRecordDeleteKeysForCurrentUserLocal()
    {
        var keys = new List<string>();

        void AddKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string key = BuildPendingRecordDeleteKey(value);
            if (!keys.Contains(key))
                keys.Add(key);
        }

        AddKey(UserDataManager.Instance?.FirebaseUid);
        AddKey(Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser?.UserId);
        AddKey(UserDataManager.Instance?.UserId);
        AddKey("local");
        return keys;
    }

    private static string BuildPendingRecordDeleteKey(string userKey)
    {
        return PENDING_DELETE_KEY_PREFIX + (string.IsNullOrWhiteSpace(userKey) ? "local" : userKey);
    }

    private static string BuildCacheKey(string userKey)
    {
        return CACHE_KEY_PREFIX + (string.IsNullOrWhiteSpace(userKey) ? "local" : userKey.Trim());
    }

    #endregion
}

[Serializable]
public class DivinationRecordCacheWrapper
{
    public List<DivinationRecordData> records = new List<DivinationRecordData>();
}

/// <summary>
/// 占卜记录数据模型 —— 用于 Firestore 序列化 和 UI 展示
/// </summary>
[Serializable]
public class DivinationRecordData
{
    public string readingId;
    public string question;
    public string scene;
    public string spreadKind;
    public List<LockedCard> lockedCards;
    public string shortVerdict;
    /// <summary>AI 生成的详细评判内容</summary>
    public string judgeContent;
    /// <summary>AI 生成的建议内容</summary>
    public string adviceContent;
    /// <summary>AI 建议的追问话题列表</summary>
    public List<string> topics;
    public string oracleId;
    public string createdAt;

    /// <summary>格式化时间（友好显示）</summary>
    public string DisplayTime
    {
        get
        {
            if (DateTime.TryParse(createdAt, out var dt))
                return dt.ToString("MM/dd HH:mm");
            return createdAt ?? "";
        }
    }

    /// <summary>问题摘要（截断）</summary>
    public string QuestionPreview
    {
        get
        {
            if (string.IsNullOrEmpty(question)) return "";
            return question.Length > 20 ? question.Substring(0, 20) + "..." : question;
        }
    }

    /// <summary>卡牌名称摘要</summary>
    public string CardsSummary
    {
        get
        {
            if (lockedCards == null || lockedCards.Count == 0) return "";
            var names = new List<string>();
            foreach (var c in lockedCards)
            {
                string orientation = c.orientation == "upright" ? "正" : "逆";
                names.Add($"{c.cardName}({orientation})");
            }
            return string.Join(" · ", names);
        }
    }

    /// <summary>牌阵类型中文名</summary>
    public string SpreadLabel
    {
        get
        {
            string builtInLabel = GetBuiltInSpreadLabel(spreadKind);
            if (!string.IsNullOrEmpty(builtInLabel))
                return builtInLabel;

            if (DivinationEngine.Instance == null) return spreadKind ?? "";
            var def = DivinationEngine.Instance.GetSpreadDefinition(spreadKind);
            return def?.label ?? spreadKind ?? "";
        }
    }

    private static string GetBuiltInSpreadLabel(string kind)
    {
        return kind switch
        {
            "friend_relationship_divination" => "双人关系占卜",
            "relationship_tension" => "双人关系占卜",
            _ => ""
        };
    }
}
