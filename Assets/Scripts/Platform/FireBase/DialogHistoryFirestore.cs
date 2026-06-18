using System;
using System.Collections.Generic;
using System.IO;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using XFGameFrameWork;

public class DialogHistoryFirestore : MonoSingleton<DialogHistoryFirestore>
{
    private const string COLLECTION_NAME = "dialog_sessions";
    private const string DEFAULT_SESSION_ID = "default";

    private FirebaseFirestore _db;
    private bool _isInitialized;

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
            Debug.Log("[DialogHistoryFirestore] 初始化完成");
        }
        else if (FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.OnFirebaseInitialized += () =>
            {
                _db = FirebaseFirestore.DefaultInstance;
                _isInitialized = true;
                Debug.Log("[DialogHistoryFirestore] Firebase 初始化完成后就绪");
            };
        }
    }

    public void SaveDefault(DialogHistorySnapshot snapshot, Action<bool> onComplete = null)
    {
        bool localSaved = SaveLocalDefault(snapshot);
        if (!IsReady)
        {
            onComplete?.Invoke(localSaved);
            return;
        }

        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning("[DialogHistoryFirestore] 用户未登录，对话已保存到本地缓存");
            onComplete?.Invoke(localSaved);
            return;
        }

        var data = SerializeSnapshot(snapshot);
        data["updatedAt"] = FieldValue.ServerTimestamp;

        _db.Collection("users")
            .Document(uid)
            .Collection(COLLECTION_NAME)
            .Document(DEFAULT_SESSION_ID)
            .SetAsync(data, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[DialogHistoryFirestore] 保存对话失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(localSaved);
                    return;
                }

                Debug.Log($"[DialogHistoryFirestore] 对话已保存: messages={snapshot?.messages?.Count ?? 0}");
                onComplete?.Invoke(true);
            });
    }

    public void LoadDefault(Action<DialogHistorySnapshot> onComplete)
    {
        string uid = GetCurrentUid();
        if (!IsReady || string.IsNullOrEmpty(uid))
        {
            onComplete?.Invoke(LoadLocalDefault());
            return;
        }

        _db.Collection("users")
            .Document(uid)
            .Collection(COLLECTION_NAME)
            .Document(DEFAULT_SESSION_ID)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                {
                    onComplete?.Invoke(LoadLocalDefault());
                    return;
                }

                var snapshot = DeserializeSnapshot(task.Result.ToDictionary());
                SaveLocalDefault(snapshot);
                onComplete?.Invoke(snapshot);
            });
    }

    private bool SaveLocalDefault(DialogHistorySnapshot snapshot)
    {
        try
        {
            if (snapshot == null)
                snapshot = new DialogHistorySnapshot();

            string json = JsonUtility.ToJson(snapshot);
            string path = GetLocalCachePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DialogHistoryFirestore] 本地对话缓存保存失败: {ex.Message}");
            return false;
        }
    }

    private DialogHistorySnapshot LoadLocalDefault()
    {
        try
        {
            string path = GetLocalCachePath();
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var snapshot = JsonUtility.FromJson<DialogHistorySnapshot>(json);
            if (snapshot == null) return null;
            if (snapshot.messages == null)
                snapshot.messages = new List<ChatMessageData>();
            if (snapshot.apiMessages == null)
                snapshot.apiMessages = new List<DeepSeekAPI.Message>();
            return snapshot;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DialogHistoryFirestore] 本地对话缓存读取失败: {ex.Message}");
            return null;
        }
    }

    private string GetLocalCachePath()
    {
        string dir = Path.Combine(Application.persistentDataPath, "dialog_cache");
        return Path.Combine(dir, "dialog_default.json");
    }

    private Dictionary<string, object> SerializeSnapshot(DialogHistorySnapshot snapshot)
    {
        if (snapshot == null)
            snapshot = new DialogHistorySnapshot();

        return new Dictionary<string, object>
        {
            { "schemaVersion", 1 },
            { "messages", SerializeMessages(snapshot.messages) },
            { "apiMessages", SerializeApiMessages(snapshot.apiMessages) },
            { "activeReadingId", snapshot.activeReadingId ?? "" },
            { "activeReadingState", snapshot.activeReadingState ?? "" },
            { "activeActionKind", snapshot.activeActionKind ?? "" },
            { "activeRelationshipId", snapshot.activeRelationshipId ?? "" },
            { "activeFriendContext", snapshot.activeFriendContext ?? "" },
            { "savedAtLocal", DateTime.Now.ToString("o") }
        };
    }

    private List<object> SerializeMessages(List<ChatMessageData> messages)
    {
        var list = new List<object>();
        if (messages == null) return list;

        foreach (var msg in messages)
        {
            if (msg == null) continue;
            list.Add(new Dictionary<string, object>
            {
                { "id", msg.id },
                { "roleType", msg.roleType.ToString() },
                { "messageType", msg.messageType.ToString() },
                { "content", msg.content ?? "" },
                { "options", SerializeStringList(msg.options) },
                { "divinerType", msg.divinerType.ToString() },
                { "spreadKind", msg.spreadKind ?? "" },
                { "spreadCardsDrawn", msg.spreadCardsDrawn },
                { "spreadDrawnCards", SerializeDraws(msg.spreadDrawnCards) },
                { "spreadDrawResultAddedToHistory", msg.spreadDrawResultAddedToHistory },
                { "readingId", msg.readingId ?? "" },
                { "divinationQuestion", msg.divinationQuestion ?? "" },
                { "divinationScene", msg.divinationScene ?? "" },
                { "divinationCreatedAt", msg.divinationCreatedAt ?? "" },
                { "shortVerdict", msg.shortVerdict ?? "" },
                { "judgeContent", msg.judgeContent ?? "" },
                { "adviceContent", msg.adviceContent ?? "" },
                { "followupTopics", SerializeStringList(msg.followupTopics) },
                { "friendName", msg.friendName ?? "" },
                { "friendContext", msg.friendContext ?? "" }
            });
        }

        return list;
    }

    private List<object> SerializeApiMessages(List<DeepSeekAPI.Message> messages)
    {
        var list = new List<object>();
        if (messages == null) return list;

        foreach (var msg in messages)
        {
            if (msg == null) continue;
            list.Add(new Dictionary<string, object>
            {
                { "role", msg.role ?? "" },
                { "content", msg.content ?? "" }
            });
        }

        return list;
    }

    private List<object> SerializeDraws(List<TarotDrawData> draws)
    {
        var list = new List<object>();
        if (draws == null) return list;

        foreach (var draw in draws)
        {
            if (draw == null) continue;
            list.Add(new Dictionary<string, object>
            {
                { "cardId", draw.cardId ?? "" },
                { "upright", draw.upright }
            });
        }

        return list;
    }

    private List<object> SerializeStringList(List<string> values)
    {
        var list = new List<object>();
        if (values == null) return list;

        foreach (var value in values)
        {
            if (!string.IsNullOrEmpty(value))
                list.Add(value);
        }

        return list;
    }

    private DialogHistorySnapshot DeserializeSnapshot(Dictionary<string, object> data)
    {
        var snapshot = new DialogHistorySnapshot
        {
            messages = DeserializeMessages(GetList(data, "messages")),
            apiMessages = DeserializeApiMessages(GetList(data, "apiMessages")),
            activeReadingId = GetString(data, "activeReadingId"),
            activeReadingState = GetString(data, "activeReadingState"),
            activeActionKind = GetString(data, "activeActionKind"),
            activeRelationshipId = GetString(data, "activeRelationshipId"),
            activeFriendContext = GetString(data, "activeFriendContext")
        };

        return snapshot;
    }

    private List<ChatMessageData> DeserializeMessages(List<object> values)
    {
        var messages = new List<ChatMessageData>();
        if (values == null) return messages;

        foreach (var value in values)
        {
            var map = value as Dictionary<string, object>;
            if (map == null) continue;

            var msg = new ChatMessageData
            {
                id = GetInt(map, "id"),
                roleType = ParseEnum(GetString(map, "roleType"), DialogRoleType.AI),
                messageType = ParseEnum(GetString(map, "messageType"), MsgType.Str),
                content = GetString(map, "content"),
                options = DeserializeStringList(GetList(map, "options")),
                divinerType = ParseEnum(GetString(map, "divinerType"), DivinerType.Tarot),
                spreadKind = GetString(map, "spreadKind"),
                spreadCardsDrawn = GetBool(map, "spreadCardsDrawn"),
                spreadDrawnCards = DeserializeDraws(GetList(map, "spreadDrawnCards")),
                spreadDrawResultAddedToHistory = GetBool(map, "spreadDrawResultAddedToHistory"),
                readingId = GetString(map, "readingId"),
                divinationQuestion = GetString(map, "divinationQuestion"),
                divinationScene = GetString(map, "divinationScene"),
                divinationCreatedAt = GetString(map, "divinationCreatedAt"),
                shortVerdict = GetString(map, "shortVerdict"),
                judgeContent = GetString(map, "judgeContent"),
                adviceContent = GetString(map, "adviceContent"),
                followupTopics = DeserializeStringList(GetList(map, "followupTopics")),
                friendName = GetString(map, "friendName"),
                friendContext = GetString(map, "friendContext")
            };
            messages.Add(msg);
        }

        return messages;
    }

    private List<DeepSeekAPI.Message> DeserializeApiMessages(List<object> values)
    {
        var messages = new List<DeepSeekAPI.Message>();
        if (values == null) return messages;

        foreach (var value in values)
        {
            var map = value as Dictionary<string, object>;
            if (map == null) continue;
            string role = GetString(map, "role");
            string content = GetString(map, "content");
            if (!string.IsNullOrEmpty(role) || !string.IsNullOrEmpty(content))
                messages.Add(new DeepSeekAPI.Message(role, content));
        }

        return messages;
    }

    private List<TarotDrawData> DeserializeDraws(List<object> values)
    {
        var draws = new List<TarotDrawData>();
        if (values == null) return draws;

        foreach (var value in values)
        {
            var map = value as Dictionary<string, object>;
            if (map == null) continue;
            draws.Add(new TarotDrawData
            {
                cardId = GetString(map, "cardId"),
                upright = GetBool(map, "upright")
            });
        }

        return draws;
    }

    private List<string> DeserializeStringList(List<object> values)
    {
        var list = new List<string>();
        if (values == null) return list;

        foreach (var value in values)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
                list.Add(text);
        }

        return list;
    }

    private List<object> GetList(Dictionary<string, object> map, string key)
    {
        if (map != null && map.TryGetValue(key, out var value) && value is List<object> list)
            return list;
        return null;
    }

    private string GetString(Dictionary<string, object> map, string key)
    {
        if (map != null && map.TryGetValue(key, out var value))
            return value?.ToString() ?? "";
        return "";
    }

    private int GetInt(Dictionary<string, object> map, string key)
    {
        if (map != null && map.TryGetValue(key, out var value))
        {
            if (value is int intValue) return intValue;
            if (value is long longValue) return (int)longValue;
            if (int.TryParse(value.ToString(), out int parsed)) return parsed;
        }
        return 0;
    }

    private bool GetBool(Dictionary<string, object> map, string key)
    {
        if (map != null && map.TryGetValue(key, out var value))
        {
            if (value is bool boolValue) return boolValue;
            if (bool.TryParse(value.ToString(), out bool parsed)) return parsed;
        }
        return false;
    }

    private T ParseEnum<T>(string value, T fallback) where T : struct
    {
        if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, out T parsed))
            return parsed;
        return fallback;
    }

    private bool CheckReady(Action<bool> onComplete = null)
    {
        if (!_isInitialized || _db == null)
        {
            Debug.LogWarning("[DialogHistoryFirestore] Firestore 尚未初始化，跳过对话历史操作");
            onComplete?.Invoke(false);
            return false;
        }
        return true;
    }

    private string GetCurrentUid()
    {
        return UserDataManager.Instance != null ? UserDataManager.Instance.FirebaseUid : "";
    }
}

[Serializable]
public class DialogHistorySnapshot
{
    public List<ChatMessageData> messages = new List<ChatMessageData>();
    public List<DeepSeekAPI.Message> apiMessages = new List<DeepSeekAPI.Message>();
    public string activeReadingId;
    public string activeReadingState;
    public string activeActionKind;
    public string activeRelationshipId;
    public string activeFriendContext;
}
