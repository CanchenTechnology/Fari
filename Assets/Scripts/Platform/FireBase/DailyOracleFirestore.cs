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
                onComplete?.Invoke(true);
            });
    }

    private DocumentReference GetDailyDoc(string uid, string date)
    {
        return _db.Collection("users")
            .Document(uid)
            .Collection("daily_oracles")
            .Document(date);
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
}
