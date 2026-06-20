using System;
using System.Collections;
using System.Collections.Generic;
using Firebase.Extensions;
using Firebase.Firestore;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;
using UnityEngine;
using XFGameFrameWork;

public static class RelationshipDivinationStatus
{
    public const string Invited = "invited";
    public const string InitiatorRevealed = "initiator_revealed";
    public const string ReceiverJoined = "receiver_joined";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

[Serializable]
public class RelationshipDivinationCard
{
    public string positionKey;
    public string position;
    public string cardId;
    public string cardName;
    public string orientation;
    public string visibleTo;

    public bool IsUpright => orientation == "upright";
    public string OrientationText => IsUpright ? "正位" : "逆位";
    public string DisplayName => $"{cardName}（{OrientationText}）";
}

[Serializable]
public class RelationshipDivinationRecord
{
    public string readingId;
    public string initiatorUid;
    public string receiverUid;
    public string initiatorName;
    public string receiverName;
    public string question;
    public string status;
    public bool initiatorRevealed;
    public bool receiverJoined;
    public bool receiverRevealed;
    public List<RelationshipDivinationCard> cards = new List<RelationshipDivinationCard>();
    public string createdAt;
    public string updatedAt;
    public string completedAt;
    public string oracleId;
    public bool isLocalOnly;

    public bool IsCompleted => status == RelationshipDivinationStatus.Completed;
    public bool IsCancelled => status == RelationshipDivinationStatus.Cancelled;

    public RelationshipDivinationCard InitiatorCard => FindCard("initiator_private");
    public RelationshipDivinationCard SharedCard => FindCard("shared");
    public RelationshipDivinationCard ReceiverCard => FindCard("receiver_private");

    public bool IsCurrentUserInitiator(string uid) => !string.IsNullOrEmpty(uid) && uid == initiatorUid;
    public bool IsCurrentUserReceiver(string uid) => !string.IsNullOrEmpty(uid) && uid == receiverUid;
    public bool CanCurrentUserReveal(string uid)
    {
        if (IsCancelled || IsCompleted) return false;
        if (IsCurrentUserInitiator(uid)) return !initiatorRevealed;
        if (IsCurrentUserReceiver(uid)) return !receiverRevealed;
        return isLocalOnly && !initiatorRevealed;
    }

    public string GetStatusText(string currentUid)
    {
        if (isLocalOnly) return "创建好友档案占卜，结果仅保存在本机和你的占卜上下文中";
        if (IsCancelled) return "邀请已取消";
        if (IsCompleted) return "双方已完成翻牌，可以一起查看共同指引";
        if (IsCurrentUserInitiator(currentUid))
            return initiatorRevealed ? $"已翻开你的牌，正在等待 {receiverName} 加入" : "邀请已创建，你可以先翻开自己的牌";
        if (IsCurrentUserReceiver(currentUid))
            return receiverRevealed ? "你已加入，等待对方完成翻牌" : $"{initiatorName} 邀请你进行双人关系占卜";
        return "双人关系占卜进行中";
    }

    private RelationshipDivinationCard FindCard(string positionKey)
    {
        if (cards == null) return null;
        return cards.Find(card => card != null && card.positionKey == positionKey);
    }
}

public class RelationshipDivinationFirestore : MonoSingleton<RelationshipDivinationFirestore>
{
    private const string CollectionName = "relationship_divinations";
    private const int IncomingLimit = 20;
    private const string DebugFriendUidPrefix = "test_real_friend_";
    private const string DebugReadingIdPrefix = "rel_debug_";

    private FirebaseFirestore db;
    private bool initialized;
    private readonly Dictionary<string, RelationshipDivinationRecord> debugRecords = new Dictionary<string, RelationshipDivinationRecord>();
    private readonly HashSet<string> debugAutoRevealStarted = new HashSet<string>();

    public bool IsReady => initialized && db != null;

    protected override void Awake()
    {
        base.Awake();
        TryInit();
    }

    private void TryInit()
    {
        if (FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.IsFirebaseInitialized)
        {
            db = FirebaseFirestore.DefaultInstance;
            initialized = true;
            Debug.Log("[RelationshipDivinationFirestore] 初始化完成");
            return;
        }

        if (FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.OnFirebaseInitialized += () =>
            {
                db = FirebaseFirestore.DefaultInstance;
                initialized = true;
                Debug.Log("[RelationshipDivinationFirestore] Firebase 初始化完成后就绪");
            };
        }
    }

    public void CreateInvite(FriendDataManager.FriendData friend, string question, Action<RelationshipDivinationRecord> onComplete)
    {
        if (friend == null)
        {
            onComplete?.Invoke(null);
            return;
        }

        if (friend.isVirtual)
        {
            RelationshipDivinationRecord localRecord = CreateLocalReading(friend, question);
            ToastManager.ShowToast($"已为 {localRecord.receiverName} 生成本地关系占卜");
            onComplete?.Invoke(localRecord);
            return;
        }

        if (IsDebugTestFriendUid(friend.firebaseUid))
        {
            RelationshipDivinationRecord debugRecord = CreateDebugInvite(friend, question);
            ToastManager.ShowToast($"已创建 {debugRecord.receiverName} 的测试双人占卜房间");
            onComplete?.Invoke(debugRecord);
            return;
        }

        string currentUid = GetCurrentUid();
        if (string.IsNullOrEmpty(currentUid) || string.IsNullOrEmpty(friend.firebaseUid))
        {
            ToastManager.ShowToast("好友关系未同步，暂时不能发起双人占卜");
            onComplete?.Invoke(null);
            return;
        }

        if (!IsReady)
        {
            ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
            onComplete?.Invoke(null);
            return;
        }

        RelationshipDivinationRecord record = BuildRecord(friend, question, false);
        DocumentReference docRef = db.Collection(CollectionName).Document(record.readingId);
        Dictionary<string, object> data = SerializeRecord(record);
        data["createdAt"] = FieldValue.ServerTimestamp;
        data["updatedAt"] = FieldValue.ServerTimestamp;

        docRef.SetAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("[RelationshipDivinationFirestore] 创建关系占卜邀请失败: " + task.Exception?.InnerException?.Message);
                ToastManager.ShowToast("关系占卜邀请创建失败");
                onComplete?.Invoke(null);
                return;
            }

            ToastManager.ShowToast($"已向 {record.receiverName} 发起关系占卜邀请");
            onComplete?.Invoke(record);
        });
    }

    private RelationshipDivinationRecord CreateLocalReading(FriendDataManager.FriendData friend, string question)
    {
        RelationshipDivinationRecord record = BuildRecord(friend, question, true);
        record.status = RelationshipDivinationStatus.Completed;
        record.initiatorRevealed = true;
        record.receiverJoined = true;
        record.receiverRevealed = true;
        record.completedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        SavePersonalHistory(record);
        return record;
    }

    public void RevealMyCard(RelationshipDivinationRecord record, Action<RelationshipDivinationRecord> onComplete)
    {
        if (record == null)
        {
            onComplete?.Invoke(null);
            return;
        }

        string currentUid = GetCurrentUid();
        if (record.isLocalOnly)
        {
            record.initiatorRevealed = true;
            record.receiverJoined = true;
            record.receiverRevealed = true;
            record.status = RelationshipDivinationStatus.Completed;
            record.completedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SavePersonalHistory(record);
            onComplete?.Invoke(record);
            return;
        }

        if (!record.CanCurrentUserReveal(currentUid))
        {
            ToastManager.ShowToast("当前没有可翻开的牌");
            onComplete?.Invoke(record);
            return;
        }

        bool isInitiator = record.IsCurrentUserInitiator(currentUid);
        if (isInitiator)
        {
            record.initiatorRevealed = true;
            record.status = record.receiverRevealed
                ? RelationshipDivinationStatus.Completed
                : RelationshipDivinationStatus.InitiatorRevealed;
        }
        else
        {
            record.receiverJoined = true;
            record.receiverRevealed = true;
            record.status = record.initiatorRevealed
                ? RelationshipDivinationStatus.Completed
                : RelationshipDivinationStatus.ReceiverJoined;
        }

        if (record.status == RelationshipDivinationStatus.Completed)
            record.completedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        record.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        if (IsDebugReading(record))
        {
            StoreDebugRecord(record);
            if (record.status == RelationshipDivinationStatus.Completed)
            {
                SavePersonalHistory(record);
            }
            else if (record.initiatorRevealed)
            {
                StartDebugAutoReveal(record.readingId);
            }

            ToastManager.ShowToast(record.status == RelationshipDivinationStatus.Completed ? "双方关系占卜已完成" : "已翻开你的牌");
            onComplete?.Invoke(record);
            return;
        }

        if (!IsReady)
        {
            onComplete?.Invoke(record);
            return;
        }

        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "initiatorRevealed", record.initiatorRevealed },
            { "receiverJoined", record.receiverJoined },
            { "receiverRevealed", record.receiverRevealed },
            { "status", record.status },
            { "updatedAt", FieldValue.ServerTimestamp }
        };

        if (record.status == RelationshipDivinationStatus.Completed)
            updates["completedAt"] = FieldValue.ServerTimestamp;

        db.Collection(CollectionName)
            .Document(record.readingId)
            .SetAsync(updates, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("[RelationshipDivinationFirestore] 翻牌状态同步失败: " + task.Exception?.InnerException?.Message);
                    ToastManager.ShowToast("翻牌同步失败，请稍后再试");
                    onComplete?.Invoke(record);
                    return;
                }

                if (record.status == RelationshipDivinationStatus.Completed)
                    SavePersonalHistory(record);

                ToastManager.ShowToast(record.status == RelationshipDivinationStatus.Completed ? "双方关系占卜已完成" : "已翻开你的牌");
                onComplete?.Invoke(record);
            });
    }

    public void CancelInvite(RelationshipDivinationRecord record, Action<bool> onComplete = null)
    {
        if (record == null || record.isLocalOnly)
        {
            onComplete?.Invoke(false);
            return;
        }

        string currentUid = GetCurrentUid();
        if (!record.IsCurrentUserInitiator(currentUid) && !record.IsCurrentUserReceiver(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        if (IsDebugReading(record))
        {
            record.status = RelationshipDivinationStatus.Cancelled;
            record.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            StoreDebugRecord(record);
            onComplete?.Invoke(true);
            return;
        }

        db.Collection(CollectionName)
            .Document(record.readingId)
            .SetAsync(new Dictionary<string, object>
            {
                { "status", RelationshipDivinationStatus.Cancelled },
                { "updatedAt", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                bool success = !(task.IsFaulted || task.IsCanceled);
                if (!success)
                    Debug.LogError("[RelationshipDivinationFirestore] 取消邀请失败: " + task.Exception?.InnerException?.Message);
                onComplete?.Invoke(success);
            });
    }

    public void LoadIncomingInvites(Action<List<RelationshipDivinationRecord>> onComplete)
    {
        if (!IsReady)
        {
            onComplete?.Invoke(new List<RelationshipDivinationRecord>());
            return;
        }

        string currentUid = GetCurrentUid();
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(new List<RelationshipDivinationRecord>());
            return;
        }

        db.Collection(CollectionName)
            .WhereEqualTo("receiverUid", currentUid)
            .Limit(IncomingLimit)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                List<RelationshipDivinationRecord> records = new List<RelationshipDivinationRecord>();
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("[RelationshipDivinationFirestore] 读取关系占卜邀请失败: " + task.Exception?.InnerException?.Message);
                    onComplete?.Invoke(records);
                    return;
                }

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    RelationshipDivinationRecord record = DeserializeRecord(doc);
                    if (record == null || record.IsCancelled || record.IsCompleted || IsInviteExpired(record)) continue;
                    records.Add(record);
                }

                records.Sort((a, b) => ParseTime(b.createdAt).CompareTo(ParseTime(a.createdAt)));
                onComplete?.Invoke(records);
            });
    }

    public void LoadActiveWithFriend(string friendUid, Action<RelationshipDivinationRecord> onComplete)
    {
        if (IsDebugTestFriendUid(friendUid))
        {
            onComplete?.Invoke(FindLatestDebugActive(friendUid));
            return;
        }

        if (!IsReady || string.IsNullOrEmpty(friendUid))
        {
            onComplete?.Invoke(null);
            return;
        }

        string currentUid = GetCurrentUid();
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(null);
            return;
        }

        db.Collection(CollectionName)
            .WhereEqualTo("initiatorUid", currentUid)
            .WhereEqualTo("receiverUid", friendUid)
            .Limit(10)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                RelationshipDivinationRecord latest = null;
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("[RelationshipDivinationFirestore] 读取我发起的关系占卜失败: " + task.Exception?.InnerException?.Message);
                }
                else
                {
                    latest = FindLatestActive(task.Result);
                }

                db.Collection(CollectionName)
                    .WhereEqualTo("initiatorUid", friendUid)
                    .WhereEqualTo("receiverUid", currentUid)
                    .Limit(10)
                    .GetSnapshotAsync()
                    .ContinueWithOnMainThread(incomingTask =>
                    {
                        if (incomingTask.IsFaulted || incomingTask.IsCanceled)
                        {
                            Debug.LogWarning("[RelationshipDivinationFirestore] 读取好友发起的关系占卜失败: " + incomingTask.Exception?.InnerException?.Message);
                            onComplete?.Invoke(latest);
                            return;
                        }

                        RelationshipDivinationRecord incoming = FindLatestActive(incomingTask.Result);
                        if (incoming != null && (latest == null || ParseTime(incoming.createdAt) > ParseTime(latest.createdAt)))
                            latest = incoming;
                        onComplete?.Invoke(latest);
                    });
            });
    }

    public void LoadReading(string readingId, Action<RelationshipDivinationRecord> onComplete)
    {
        if (!string.IsNullOrWhiteSpace(readingId) && debugRecords.TryGetValue(readingId, out RelationshipDivinationRecord debugRecord))
        {
            onComplete?.Invoke(debugRecord);
            return;
        }

        if (!IsReady || string.IsNullOrWhiteSpace(readingId))
        {
            onComplete?.Invoke(null);
            return;
        }

        db.Collection(CollectionName)
            .Document(readingId)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("[RelationshipDivinationFirestore] 读取关系占卜记录失败: " + task.Exception?.InnerException?.Message);
                    onComplete?.Invoke(null);
                    return;
                }

                onComplete?.Invoke(DeserializeRecord(task.Result));
            });
    }

    private RelationshipDivinationRecord FindLatestActive(QuerySnapshot snapshot)
    {
        RelationshipDivinationRecord latest = null;
        if (snapshot == null) return latest;

        foreach (DocumentSnapshot doc in snapshot.Documents)
        {
            RelationshipDivinationRecord record = DeserializeRecord(doc);
            if (record == null || record.IsCancelled || record.IsCompleted || IsInviteExpired(record)) continue;
            if (latest == null || ParseTime(record.createdAt) > ParseTime(latest.createdAt))
                latest = record;
        }

        return latest;
    }

    private RelationshipDivinationRecord CreateDebugInvite(FriendDataManager.FriendData friend, string question)
    {
        RelationshipDivinationRecord record = BuildRecord(friend, question, false);
        record.readingId = $"{DebugReadingIdPrefix}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        if (string.IsNullOrWhiteSpace(record.initiatorUid))
            record.initiatorUid = "debug_current_user";
        record.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        record.updatedAt = record.createdAt;
        StoreDebugRecord(record);
        return record;
    }

    private void StoreDebugRecord(RelationshipDivinationRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.readingId)) return;
        debugRecords[record.readingId] = record;
    }

    private RelationshipDivinationRecord FindLatestDebugActive(string friendUid)
    {
        string currentUid = GetCurrentUid();
        RelationshipDivinationRecord latest = null;

        foreach (RelationshipDivinationRecord record in debugRecords.Values)
        {
            if (record == null || record.IsCancelled || record.IsCompleted || IsInviteExpired(record)) continue;
            bool samePair = record.initiatorUid == currentUid && record.receiverUid == friendUid
                || record.initiatorUid == friendUid && record.receiverUid == currentUid;
            if (!samePair) continue;

            if (latest == null || ParseTime(record.createdAt) > ParseTime(latest.createdAt))
                latest = record;
        }

        return latest;
    }

    private void StartDebugAutoReveal(string readingId)
    {
        if (string.IsNullOrWhiteSpace(readingId) || debugAutoRevealStarted.Contains(readingId)) return;
        debugAutoRevealStarted.Add(readingId);
        StartCoroutine(DebugAutoRevealRoutine(readingId));
    }

    private IEnumerator DebugAutoRevealRoutine(string readingId)
    {
        yield return new WaitForSeconds(4f);

        if (!debugRecords.TryGetValue(readingId, out RelationshipDivinationRecord record)) yield break;
        if (record == null || record.IsCancelled || record.IsCompleted || !record.initiatorRevealed) yield break;

        record.receiverJoined = true;
        record.receiverRevealed = true;
        record.status = RelationshipDivinationStatus.Completed;
        record.completedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        record.updatedAt = record.completedAt;
        StoreDebugRecord(record);
        SavePersonalHistory(record);
        ToastManager.ShowToast("测试好友已完成翻牌");
    }

    private static bool IsDebugTestFriendUid(string uid)
    {
        return !string.IsNullOrWhiteSpace(uid) && uid.StartsWith(DebugFriendUidPrefix, StringComparison.Ordinal);
    }

    private static bool IsDebugReading(RelationshipDivinationRecord record)
    {
        if (record == null) return false;
        return (!string.IsNullOrWhiteSpace(record.readingId) && record.readingId.StartsWith(DebugReadingIdPrefix, StringComparison.Ordinal))
            || IsDebugTestFriendUid(record.initiatorUid)
            || IsDebugTestFriendUid(record.receiverUid);
    }

    private RelationshipDivinationRecord BuildRecord(FriendDataManager.FriendData friend, string question, bool localOnly)
    {
        string currentUid = GetCurrentUid();
        string currentName = UserDataManager.Instance != null && !string.IsNullOrWhiteSpace(UserDataManager.Instance.UserName)
            ? UserDataManager.Instance.UserName
            : "我";
        string friendName = string.IsNullOrWhiteSpace(friend?.name) ? "好友" : friend.name.Trim();
        string readingId = $"rel_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

        return new RelationshipDivinationRecord
        {
            readingId = readingId,
            initiatorUid = currentUid,
            receiverUid = localOnly ? $"virtual:{friend?.virtualFriendId ?? friend?.id.ToString()}" : friend.firebaseUid,
            initiatorName = currentName,
            receiverName = friendName,
            question = string.IsNullOrWhiteSpace(question) ? $"我和 {friendName} 的关系接下来会如何发展？" : question.Trim(),
            status = RelationshipDivinationStatus.Invited,
            initiatorRevealed = false,
            receiverJoined = false,
            receiverRevealed = false,
            cards = DrawRelationshipCards(),
            createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            oracleId = GetCurrentOracleId(),
            isLocalOnly = localOnly
        };
    }

    private static List<RelationshipDivinationCard> DrawRelationshipCards()
    {
        List<(TarotCard card, bool upright)> draws = TarotDeck.DrawMultiple(3);
        string[] keys = { "initiator_private", "shared", "receiver_private" };
        string[] labels = { "你的内心与看法", "共同揭示", "对方的内心与想法" };
        string[] visibility = { "initiator", "both", "receiver" };

        List<RelationshipDivinationCard> cards = new List<RelationshipDivinationCard>();
        for (int i = 0; i < draws.Count && i < 3; i++)
        {
            TarotCard card = draws[i].card;
            bool upright = draws[i].upright;
            cards.Add(new RelationshipDivinationCard
            {
                positionKey = keys[i],
                position = labels[i],
                cardId = card.cardId,
                cardName = card.nameZh,
                orientation = upright ? "upright" : "reversed",
                visibleTo = visibility[i]
            });
        }
        return cards;
    }

    private Dictionary<string, object> SerializeRecord(RelationshipDivinationRecord record)
    {
        return new Dictionary<string, object>
        {
            { "readingId", record.readingId ?? "" },
            { "initiatorUid", record.initiatorUid ?? "" },
            { "receiverUid", record.receiverUid ?? "" },
            { "initiatorName", record.initiatorName ?? "" },
            { "receiverName", record.receiverName ?? "" },
            { "question", record.question ?? "" },
            { "status", record.status ?? RelationshipDivinationStatus.Invited },
            { "initiatorRevealed", record.initiatorRevealed },
            { "receiverJoined", record.receiverJoined },
            { "receiverRevealed", record.receiverRevealed },
            { "cards", SerializeCards(record.cards) },
            { "oracleId", record.oracleId ?? "tarot" }
        };
    }

    private List<Dictionary<string, object>> SerializeCards(List<RelationshipDivinationCard> cards)
    {
        List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
        if (cards == null) return result;

        foreach (RelationshipDivinationCard card in cards)
        {
            if (card == null) continue;
            result.Add(new Dictionary<string, object>
            {
                { "positionKey", card.positionKey ?? "" },
                { "position", card.position ?? "" },
                { "cardId", card.cardId ?? "" },
                { "cardName", card.cardName ?? "" },
                { "orientation", card.orientation ?? "" },
                { "visibleTo", card.visibleTo ?? "" }
            });
        }

        return result;
    }

    private RelationshipDivinationRecord DeserializeRecord(DocumentSnapshot doc)
    {
        if (doc == null || !doc.Exists) return null;

        Dictionary<string, object> data = doc.ToDictionary();
        RelationshipDivinationRecord record = new RelationshipDivinationRecord
        {
            readingId = GetStr(data, "readingId", doc.Id),
            initiatorUid = GetStr(data, "initiatorUid"),
            receiverUid = GetStr(data, "receiverUid"),
            initiatorName = GetStr(data, "initiatorName", "好友"),
            receiverName = GetStr(data, "receiverName", "我"),
            question = GetStr(data, "question"),
            status = GetStr(data, "status", RelationshipDivinationStatus.Invited),
            initiatorRevealed = GetBool(data, "initiatorRevealed"),
            receiverJoined = GetBool(data, "receiverJoined"),
            receiverRevealed = GetBool(data, "receiverRevealed"),
            cards = DeserializeCards(data),
            createdAt = GetTimeString(doc, data, "createdAt"),
            updatedAt = GetTimeString(doc, data, "updatedAt"),
            completedAt = GetTimeString(doc, data, "completedAt"),
            oracleId = GetStr(data, "oracleId", "tarot"),
            isLocalOnly = false
        };
        return record;
    }

    private List<RelationshipDivinationCard> DeserializeCards(Dictionary<string, object> data)
    {
        List<RelationshipDivinationCard> cards = new List<RelationshipDivinationCard>();
        if (!data.TryGetValue("cards", out object obj) || obj == null) return cards;

        if (obj is IEnumerable<object> list)
        {
            foreach (object item in list)
            {
                if (item is Dictionary<string, object> cardMap)
                {
                    cards.Add(new RelationshipDivinationCard
                    {
                        positionKey = GetStr(cardMap, "positionKey"),
                        position = GetStr(cardMap, "position"),
                        cardId = GetStr(cardMap, "cardId"),
                        cardName = GetStr(cardMap, "cardName"),
                        orientation = GetStr(cardMap, "orientation"),
                        visibleTo = GetStr(cardMap, "visibleTo")
                    });
                }
            }
        }

        return cards;
    }

    private void SavePersonalHistory(RelationshipDivinationRecord record)
    {
        DivinationRecordFirestore history = DivinationRecordFirestore.Instance;
        if (history == null) return;

        history.SaveRecord(new DivinationRecordData
        {
            readingId = record.readingId,
            question = record.question,
            scene = "friend_relationship_divination",
            spreadKind = "relationship_tension",
            lockedCards = ToLockedCards(record.cards),
            shortVerdict = BuildShortVerdict(record),
            oracleId = record.oracleId,
            createdAt = string.IsNullOrEmpty(record.createdAt) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : record.createdAt
        });
    }

    private List<LockedCard> ToLockedCards(List<RelationshipDivinationCard> cards)
    {
        List<LockedCard> result = new List<LockedCard>();
        if (cards == null) return result;
        foreach (RelationshipDivinationCard card in cards)
        {
            if (card == null) continue;
            result.Add(new LockedCard
            {
                positionKey = card.positionKey,
                position = card.position,
                cardId = card.cardId,
                cardName = card.cardName,
                orientation = card.orientation
            });
        }
        return result;
    }

    private string BuildShortVerdict(RelationshipDivinationRecord record)
    {
        RelationshipDivinationCard shared = record.SharedCard;
        string sharedText = shared != null ? shared.DisplayName : "共同牌";
        return $"双人关系占卜：{record.initiatorName} 与 {record.receiverName}，共同揭示为 {sharedText}。";
    }

    private string GetCurrentUid()
    {
        if (UserDataManager.Instance != null && !string.IsNullOrWhiteSpace(UserDataManager.Instance.FirebaseUid))
            return UserDataManager.Instance.FirebaseUid;
        return Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? "";
    }

    private string GetCurrentOracleId()
    {
        if (RoleManager.Instance == null) return "tarot";
        return RoleManager.Instance.characterType switch
        {
            CharacterType.Astrologer => "astrology",
            CharacterType.Meditator => "sage",
            _ => "tarot",
        };
    }

    private static string GetStr(Dictionary<string, object> data, string key, string fallback = "")
    {
        return data != null && data.TryGetValue(key, out object value) && value != null ? value.ToString() : fallback;
    }

    private static bool GetBool(Dictionary<string, object> data, string key)
    {
        return data != null && data.TryGetValue(key, out object value) && value is bool result && result;
    }

    private static string GetTimeString(DocumentSnapshot doc, Dictionary<string, object> data, string key)
    {
        if (doc != null && doc.TryGetValue(key, out Timestamp timestamp))
            return timestamp.ToDateTime().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        return GetStr(data, key);
    }

    private static DateTime ParseTime(string value)
    {
        return DateTime.TryParse(value, out DateTime parsed) ? parsed : DateTime.MinValue;
    }

    private static bool IsInviteExpired(RelationshipDivinationRecord record)
    {
        if (record == null || record.isLocalOnly || record.IsCompleted || record.IsCancelled) return false;
        DateTime createdAt = ParseTime(record.createdAt);
        if (createdAt == DateTime.MinValue) return false;
        return createdAt.AddHours(24) <= DateTime.Now;
    }
}
