using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;
using UnityEngine;
using UnityEngine.Networking;
using XFGameFrameWork;

public class RelationshipDivinationFirestore : MonoSingleton<RelationshipDivinationFirestore>
{
    private const string CollectionName = "relationship_divinations";
    private const string ActionFunctionUrl = "https://us-central1-fari-app-b2fd2.cloudfunctions.net/relationshipDivinationAction";
    private const int IncomingLimit = 20;
    private const string DebugFriendUidPrefix = "test_real_friend_";
    private const string DebugReadingIdPrefix = "rel_debug_";

    private FirebaseFirestore db;
    private bool initialized;
    private bool hasSubscribedFirebaseInit;
    private bool initRetryScheduled;
    private readonly Dictionary<string, RelationshipDivinationRecord> debugRecords = new Dictionary<string, RelationshipDivinationRecord>();
    private readonly Dictionary<string, RelationshipDivinationRecord> runtimeRecordStates = new Dictionary<string, RelationshipDivinationRecord>();
    private readonly HashSet<string> debugAutoRevealStarted = new HashSet<string>();

    [Serializable]
    private class RelationshipActionRequest
    {
        public string readingId;
        public string action;
    }

    [Serializable]
    private class RelationshipActionResponse
    {
        public bool ok = false;
        public RelationshipDivinationRecord record = null;
        public string error = "";
    }

    public bool IsReady => initialized && db != null;

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

        if (!hasSubscribedFirebaseInit)
        {
            hasSubscribedFirebaseInit = true;
            authManager.OnFirebaseInitialized += OnFirebaseReady;
        }
    }

    private void ScheduleInitRetry()
    {
        if (initRetryScheduled) return;

        initRetryScheduled = true;
        Invoke(nameof(RetryInit), 0.5f);
    }

    private void RetryInit()
    {
        initRetryScheduled = false;
        TryInit();
    }

    private void OnFirebaseReady()
    {
        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        if (authManager != null && hasSubscribedFirebaseInit)
            authManager.OnFirebaseInitialized -= OnFirebaseReady;
        hasSubscribedFirebaseInit = false;

        try
        {
            db = FirebaseFirestore.DefaultInstance;
            initialized = true;
            Debug.Log("[RelationshipDivinationFirestore] Firebase 初始化完成后就绪");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelationshipDivinationFirestore] 初始化失败: {e.Message}");
            ScheduleInitRetry();
        }
    }

    public void CreateInvite(FriendDataManager.FriendData friend, string question, Action<RelationshipDivinationRecord> onComplete, bool showSuccessToast = true)
    {
        if (friend == null)
        {
            onComplete?.Invoke(null);
            return;
        }

        if (friend.isVirtual)
        {
            RelationshipDivinationRecord localRecord = CreatedFriendRelationshipDivinationLocalFlow.CreateRecord(friend, question);
            if (showSuccessToast)
                ToastManager.ShowToast($"已为 {localRecord.receiverName} 生成本地关系占卜");
            onComplete?.Invoke(localRecord);
            return;
        }

        if (IsDebugTestFriendUid(friend.firebaseUid))
        {
            RelationshipDivinationRecord debugRecord = CreateDebugInvite(friend, question);
            if (showSuccessToast)
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

            if (showSuccessToast)
                ToastManager.ShowToast($"已向 {record.receiverName} 发起关系占卜邀请");
            RememberRecordState(record);
            onComplete?.Invoke(record);
        });
    }

    public void RevealMyCard(RelationshipDivinationRecord record, Action<RelationshipDivinationRecord> onComplete, bool showSuccessToast = true)
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
            record.updatedAt = record.completedAt;
            SavePersonalHistory(record);
            RememberRecordState(record);
            onComplete?.Invoke(record);
            return;
        }

        if (!record.CanCurrentUserReveal(currentUid))
        {
            ToastManager.ShowToast("当前没有可翻开的牌");
            onComplete?.Invoke(record);
            return;
        }

        if (!IsDebugReading(record) && !IsReady)
        {
            ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
            onComplete?.Invoke(record);
            return;
        }

        if (!IsDebugReading(record))
        {
            RunBackendAction(record, "reveal", (updated, error) =>
            {
                if (updated == null)
                {
                    ToastManager.ShowToast(string.IsNullOrWhiteSpace(error) ? "翻牌同步失败，请稍后再试" : error);
                    onComplete?.Invoke(record);
                    return;
                }

                if (showSuccessToast)
                    ToastManager.ShowToast(updated.IsCompleted ? "双方关系占卜已完成" : "已翻开你的牌");
                onComplete?.Invoke(updated);
            });
            return;
        }

        bool originalInitiatorRevealed = record.initiatorRevealed;
        bool originalReceiverJoined = record.receiverJoined;
        bool originalReceiverRevealed = record.receiverRevealed;
        string originalStatus = record.status;
        string originalCompletedAt = record.completedAt;
        string originalUpdatedAt = record.updatedAt;

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
            // 当前产品流是“发起邀请时已带着发起者翻开的牌”。
            // 兼容旧数据：旧邀请如果发起方状态还没标记，接收方翻牌时一并补齐。
            if (!record.initiatorRevealed)
                record.initiatorRevealed = true;
            record.status = record.initiatorRevealed
                ? RelationshipDivinationStatus.Completed
                : RelationshipDivinationStatus.ReceiverJoined;
        }

        if (record.status == RelationshipDivinationStatus.Completed)
            record.completedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        record.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        RememberRecordState(record);

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

            if (showSuccessToast)
                ToastManager.ShowToast(record.status == RelationshipDivinationStatus.Completed ? "双方关系占卜已完成" : "已翻开你的牌");
            onComplete?.Invoke(record);
            return;
        }

        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "updatedAt", FieldValue.ServerTimestamp }
        };

        updates["initiatorRevealed"] = record.initiatorRevealed;
        updates["receiverJoined"] = record.receiverJoined;
        updates["receiverRevealed"] = record.receiverRevealed;

        if (record.status == RelationshipDivinationStatus.Completed)
        {
            updates["status"] = RelationshipDivinationStatus.Completed;
            updates["completedAt"] = FieldValue.ServerTimestamp;
        }
        else
        {
            updates["status"] = record.status;
        }

        DocumentReference docRef = db.Collection(CollectionName).Document(record.readingId);
        docRef.SetAsync(updates, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("[RelationshipDivinationFirestore] 翻牌状态同步失败: " + task.Exception?.InnerException?.Message);
                    record.initiatorRevealed = originalInitiatorRevealed;
                    record.receiverJoined = originalReceiverJoined;
                    record.receiverRevealed = originalReceiverRevealed;
                    record.status = originalStatus;
                    record.completedAt = originalCompletedAt;
                    record.updatedAt = originalUpdatedAt;
                    RememberRecordState(record);
                    ToastManager.ShowToast("翻牌同步失败，请稍后再试");
                    onComplete?.Invoke(record);
                    return;
                }

                RefreshRevealStateAndFinish(docRef, record, onComplete, showSuccessToast);
            });
    }

    public void JoinInvite(RelationshipDivinationRecord record, Action<RelationshipDivinationRecord> onComplete, bool showSuccessToast = true)
    {
        if (record == null)
        {
            onComplete?.Invoke(null);
            return;
        }

        string currentUid = GetCurrentUid();
        if (record.isLocalOnly || record.IsCompleted || record.IsCancelled || record.receiverJoined)
        {
            onComplete?.Invoke(record);
            return;
        }

        if (!record.IsCurrentUserReceiver(currentUid))
        {
            ToastManager.ShowToast("没有权限接受这次邀请");
            onComplete?.Invoke(record);
            return;
        }

        if (!IsDebugReading(record) && !IsReady)
        {
            ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
            onComplete?.Invoke(record);
            return;
        }

        if (!IsDebugReading(record))
        {
            RunBackendAction(record, "accept", (updated, error) =>
            {
                if (updated == null)
                {
                    ToastManager.ShowToast(string.IsNullOrWhiteSpace(error) ? "接受邀请失败，请稍后再试" : error);
                    onComplete?.Invoke(record);
                    return;
                }

                if (showSuccessToast)
                    ToastManager.ShowToast("已接受双人占卜邀请");
                onComplete?.Invoke(updated);
            });
            return;
        }

        bool originalInitiatorRevealed = record.initiatorRevealed;
        bool originalReceiverJoined = record.receiverJoined;
        string originalStatus = record.status;
        string originalUpdatedAt = record.updatedAt;

        if (!record.initiatorRevealed)
            record.initiatorRevealed = true;
        record.receiverJoined = true;
        if (string.IsNullOrWhiteSpace(record.status)
            || record.status == RelationshipDivinationStatus.Invited
            || record.status == RelationshipDivinationStatus.InitiatorRevealed)
        {
            record.status = RelationshipDivinationStatus.ReceiverJoined;
        }
        record.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        RememberRecordState(record);

        if (IsDebugReading(record))
        {
            StoreDebugRecord(record);
            if (showSuccessToast)
                ToastManager.ShowToast("已接受双人占卜邀请");
            onComplete?.Invoke(record);
            return;
        }

        DocumentReference docRef = db.Collection(CollectionName).Document(record.readingId);
        docRef.SetAsync(new Dictionary<string, object>
        {
            { "initiatorRevealed", record.initiatorRevealed },
            { "receiverJoined", true },
            { "status", record.status },
            { "updatedAt", FieldValue.ServerTimestamp }
        }, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("[RelationshipDivinationFirestore] 接受关系占卜邀请失败: " + task.Exception?.InnerException?.Message);
                ToastManager.ShowToast("接受邀请失败，请稍后再试");
                record.initiatorRevealed = originalInitiatorRevealed;
                record.receiverJoined = originalReceiverJoined;
                record.status = originalStatus;
                record.updatedAt = originalUpdatedAt;
                RememberRecordState(record);
                onComplete?.Invoke(record);
                return;
            }

            docRef.GetSnapshotAsync().ContinueWithOnMainThread(snapshotTask =>
            {
                if (!snapshotTask.IsFaulted && !snapshotTask.IsCanceled)
                {
                    RelationshipDivinationRecord latest = DeserializeRecord(snapshotTask.Result);
                    if (latest != null)
                    {
                        MergeRuntimeRecordState(latest);
                        CopyRecordState(record, latest);
                    }
                }

                if (showSuccessToast)
                    ToastManager.ShowToast("已接受双人占卜邀请");
                RememberRecordState(record);
                onComplete?.Invoke(record);
            });
        });
    }

	    private void RefreshRevealStateAndFinish(DocumentReference docRef, RelationshipDivinationRecord record, Action<RelationshipDivinationRecord> onComplete, bool showSuccessToast = true)
	    {
	        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
	        {
	            if (!task.IsFaulted && !task.IsCanceled)
            {
                RelationshipDivinationRecord latest = DeserializeRecord(task.Result);
	                if (latest != null)
	                {
	                    MergeRuntimeRecordState(latest);
	                    CopyRecordState(record, latest);
	                }
	            }

	            PromoteLegacyReceiverCompleted(record);

	            if (record.initiatorRevealed && record.receiverRevealed && !record.IsCompleted)
	            {
                CompleteRevealState(docRef, record, onComplete, showSuccessToast);
                return;
            }

            FinishReveal(record, onComplete, showSuccessToast);
        });
    }

    private void CompleteRevealState(DocumentReference docRef, RelationshipDivinationRecord record, Action<RelationshipDivinationRecord> onComplete, bool showSuccessToast = true)
    {
        string previousStatus = record.status;
        string previousCompletedAt = record.completedAt;
        string previousUpdatedAt = record.updatedAt;

        record.status = RelationshipDivinationStatus.Completed;
        record.completedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        record.updatedAt = record.completedAt;
        RememberRecordState(record);

	        docRef.SetAsync(new Dictionary<string, object>
	        {
	            { "initiatorRevealed", true },
	            { "receiverJoined", true },
	            { "receiverRevealed", true },
	            { "status", RelationshipDivinationStatus.Completed },
	            { "completedAt", FieldValue.ServerTimestamp },
	            { "updatedAt", FieldValue.ServerTimestamp }
        }, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("[RelationshipDivinationFirestore] 完成状态同步失败: " + task.Exception?.InnerException?.Message);
                record.status = previousStatus;
                record.completedAt = previousCompletedAt;
                record.updatedAt = previousUpdatedAt;
                RememberRecordState(record);
                ToastManager.ShowToast("已翻开你的牌，等待结果同步");
                onComplete?.Invoke(record);
                return;
            }

            FinishReveal(record, onComplete, showSuccessToast);
        });
    }

    private void FinishReveal(RelationshipDivinationRecord record, Action<RelationshipDivinationRecord> onComplete, bool showSuccessToast = true)
    {
        if (record.status == RelationshipDivinationStatus.Completed)
            SavePersonalHistory(record);

        RememberRecordState(record);
        if (showSuccessToast)
            ToastManager.ShowToast(record.status == RelationshipDivinationStatus.Completed ? "双方关系占卜已完成" : "已翻开你的牌");
        onComplete?.Invoke(record);
    }

    private static void CopyRecordState(RelationshipDivinationRecord target, RelationshipDivinationRecord source)
    {
        if (target == null || source == null) return;

        target.readingId = source.readingId;
        target.initiatorUid = source.initiatorUid;
        target.receiverUid = source.receiverUid;
        target.initiatorName = source.initiatorName;
        target.receiverName = source.receiverName;
        target.question = source.question;
        target.status = source.status;
        target.initiatorRevealed = source.initiatorRevealed;
        target.receiverJoined = source.receiverJoined;
        target.receiverRevealed = source.receiverRevealed;
        target.cards = source.cards;
        target.createdAt = source.createdAt;
        target.updatedAt = source.updatedAt;
        target.completedAt = source.completedAt;
        target.receiverSeenAt = source.receiverSeenAt;
        target.oracleId = source.oracleId;
        target.isLocalOnly = source.isLocalOnly;
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

        if (!IsReady)
        {
            ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
            onComplete?.Invoke(false);
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
                else
                {
                    record.status = RelationshipDivinationStatus.Cancelled;
                    record.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }
                onComplete?.Invoke(success);
            });
    }

    public void LoadIncomingInvites(Action<List<RelationshipDivinationRecord>> onComplete)
    {
        LoadIncomingInvites((records, _) => onComplete?.Invoke(records));
    }

    public void LoadIncomingInvites(Action<List<RelationshipDivinationRecord>, bool> onComplete)
    {
        if (!IsReady)
        {
            onComplete?.Invoke(new List<RelationshipDivinationRecord>(), false);
            return;
        }

        string currentUid = GetCurrentUid();
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(new List<RelationshipDivinationRecord>(), false);
            return;
        }

        db.Collection(CollectionName)
            .WhereEqualTo("receiverUid", currentUid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                List<RelationshipDivinationRecord> records = new List<RelationshipDivinationRecord>();
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("[RelationshipDivinationFirestore] 读取关系占卜邀请失败: " + task.Exception?.InnerException?.Message);
                    onComplete?.Invoke(records, false);
                    return;
                }

	                foreach (DocumentSnapshot doc in task.Result.Documents)
	                {
	                    RelationshipDivinationRecord record = MergeRuntimeRecordState(DeserializeRecord(doc));
	                    PromoteLegacyReceiverCompleted(record);
	                    if (ShouldAutoComplete(record))
	                    {
                        PromoteCompletedIfNeeded(record);
                        continue;
                    }

                    if (record == null || record.IsCancelled || record.IsCompleted || record.receiverRevealed || IsInviteExpired(record)) continue;
                    records.Add(record);
                }

                records.Sort((a, b) => ParseTime(b.createdAt).CompareTo(ParseTime(a.createdAt)));
                if (records.Count > IncomingLimit)
                    records.RemoveRange(IncomingLimit, records.Count - IncomingLimit);
                onComplete?.Invoke(records, true);
            });
    }

    public void MarkIncomingInvitesSeen(IReadOnlyList<RelationshipDivinationRecord> records, Action<bool> onComplete = null)
    {
        if (records == null || records.Count == 0)
        {
            onComplete?.Invoke(true);
            return;
        }

        string currentUid = GetCurrentUid();
        string seenTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        List<RelationshipDivinationRecord> writableRecords = new List<RelationshipDivinationRecord>();

        for (int i = 0; i < records.Count; i++)
        {
            RelationshipDivinationRecord record = records[i];
            if (record == null || string.IsNullOrWhiteSpace(record.readingId))
                continue;

            record.receiverSeenAt = seenTime;
            RememberRecordState(record);

            if (record.isLocalOnly || IsDebugReading(record))
            {
                StoreDebugRecord(record);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(currentUid)
                && !string.IsNullOrWhiteSpace(record.receiverUid)
                && record.receiverUid != currentUid)
            {
                continue;
            }

            writableRecords.Add(record);
        }

        if (writableRecords.Count == 0)
        {
            onComplete?.Invoke(true);
            return;
        }

        if (!IsReady || string.IsNullOrWhiteSpace(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        WriteBatch batch = db.StartBatch();
        for (int i = 0; i < writableRecords.Count; i++)
        {
            RelationshipDivinationRecord record = writableRecords[i];
            DocumentReference docRef = db.Collection(CollectionName).Document(record.readingId);
            batch.Set(docRef, new Dictionary<string, object>
            {
                { "receiverSeenAt", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll);
        }

        batch.CommitAsync().ContinueWithOnMainThread(task =>
        {
            bool succeeded = !(task.IsFaulted || task.IsCanceled);
            if (!succeeded)
                Debug.LogWarning("[RelationshipDivinationFirestore] 同步关系占卜邀请已读状态失败: " + task.Exception?.InnerException?.Message);
            onComplete?.Invoke(succeeded);
        });
    }

    public void LoadOutgoingInvitationList(Action<List<RelationshipDivinationRecord>, bool> onComplete)
    {
        LoadInvitationList("initiatorUid", onComplete);
    }

    public void LoadReceivedInvitationList(Action<List<RelationshipDivinationRecord>, bool> onComplete)
    {
        LoadInvitationList("receiverUid", onComplete);
    }

    private void LoadInvitationList(string userField, Action<List<RelationshipDivinationRecord>, bool> onComplete)
    {
        if (!IsReady)
        {
            onComplete?.Invoke(new List<RelationshipDivinationRecord>(), false);
            return;
        }

        string currentUid = GetCurrentUid();
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(new List<RelationshipDivinationRecord>(), false);
            return;
        }

        db.Collection(CollectionName)
            .WhereEqualTo(userField, currentUid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                List<RelationshipDivinationRecord> records = new List<RelationshipDivinationRecord>();
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("[RelationshipDivinationFirestore] 读取关系占卜邀请列表失败: " + task.Exception?.InnerException?.Message);
                    onComplete?.Invoke(records, false);
                    return;
                }

	                foreach (DocumentSnapshot doc in task.Result.Documents)
	                {
	                    RelationshipDivinationRecord record = MergeRuntimeRecordState(DeserializeRecord(doc));
	                    if (record == null) continue;

	                    PromoteLegacyReceiverCompleted(record);
	                    if (ShouldAutoComplete(record))
	                        PromoteCompletedIfNeeded(record);

                    if (record.IsCancelled) continue;
                    if (!record.IsCompleted && IsInviteExpired(record)) continue;
                    records.Add(record);
                }

                records.Sort((a, b) => GetRecordRelevantTime(b).CompareTo(GetRecordRelevantTime(a)));
                if (records.Count > IncomingLimit)
                    records.RemoveRange(IncomingLimit, records.Count - IncomingLimit);
                onComplete?.Invoke(records, true);
            });
    }

    public void LoadActiveWithFriend(string friendUid, Action<RelationshipDivinationRecord> onComplete)
    {
        LoadActiveWithFriend(friendUid, (record, _) => onComplete?.Invoke(record));
    }

    public void LoadActiveWithFriend(string friendUid, Action<RelationshipDivinationRecord, bool> onComplete)
    {
        if (IsDebugTestFriendUid(friendUid))
        {
            onComplete?.Invoke(FindLatestDebugActive(friendUid), true);
            return;
        }

        if (!IsReady || string.IsNullOrEmpty(friendUid))
        {
            onComplete?.Invoke(null, false);
            return;
        }

        string currentUid = GetCurrentUid();
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(null, false);
            return;
        }

        LoadActiveForDirection(currentUid, friendUid, (outgoing, outgoingSucceeded) =>
        {
            if (!outgoingSucceeded)
            {
                onComplete?.Invoke(null, false);
                return;
            }

            LoadActiveForDirection(friendUid, currentUid, (incoming, incomingSucceeded) =>
            {
                if (!incomingSucceeded)
                {
                    onComplete?.Invoke(null, false);
                    return;
                }

                onComplete?.Invoke(PickLatest(outgoing, incoming), true);
            });
        });
    }

    private void LoadActiveForDirection(string initiatorUid, string receiverUid, Action<RelationshipDivinationRecord, bool> onComplete)
    {
        db.Collection(CollectionName)
            .WhereEqualTo("initiatorUid", initiatorUid)
            .WhereEqualTo("receiverUid", receiverUid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                RelationshipDivinationRecord latest = null;
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("[RelationshipDivinationFirestore] 读取活跃关系占卜失败: " + task.Exception?.InnerException?.Message);
                    onComplete?.Invoke(null, false);
                    return;
                }

                latest = FindLatestActive(task.Result);
                onComplete?.Invoke(latest, true);
            });
    }

    public void LoadCompletedTodayWithFriend(string friendUid, Action<RelationshipDivinationRecord, bool> onComplete)
    {
        if (IsDebugTestFriendUid(friendUid))
        {
            onComplete?.Invoke(FindLatestDebugCompletedToday(friendUid), true);
            return;
        }

        if (!IsReady || string.IsNullOrEmpty(friendUid))
        {
            onComplete?.Invoke(null, false);
            return;
        }

        string currentUid = GetCurrentUid();
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(null, false);
            return;
        }

        LoadCompletedTodayForDirection(currentUid, friendUid, (outgoing, outgoingSucceeded) =>
        {
            if (!outgoingSucceeded)
            {
                onComplete?.Invoke(null, false);
                return;
            }

            LoadCompletedTodayForDirection(friendUid, currentUid, (incoming, incomingSucceeded) =>
            {
                if (!incomingSucceeded)
                {
                    onComplete?.Invoke(null, false);
                    return;
                }

                RelationshipDivinationRecord latest = PickLatest(outgoing, incoming);
                onComplete?.Invoke(latest, true);
            });
        });
    }

	    private void LoadCompletedTodayForDirection(string initiatorUid, string receiverUid, Action<RelationshipDivinationRecord, bool> onComplete)
	    {
	        db.Collection(CollectionName)
	            .WhereEqualTo("initiatorUid", initiatorUid)
	            .WhereEqualTo("receiverUid", receiverUid)
	            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                RelationshipDivinationRecord latest = null;
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("[RelationshipDivinationFirestore] 读取今日已完成关系占卜失败: " + task.Exception?.InnerException?.Message);
                    onComplete?.Invoke(null, false);
                    return;
                }

	                foreach (DocumentSnapshot doc in task.Result.Documents)
	                {
	                    RelationshipDivinationRecord record = MergeRuntimeRecordState(DeserializeRecord(doc));
	                    PromoteLegacyReceiverCompleted(record);
	                    if (ShouldAutoComplete(record))
	                        PromoteCompletedIfNeeded(record);
	                    if (record == null || !IsCompletedToday(record)) continue;
	                    latest = PickLatest(latest, record);
                }

                onComplete?.Invoke(latest, true);
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

	                RelationshipDivinationRecord record = MergeRuntimeRecordState(DeserializeRecord(task.Result));
	                PromoteLegacyReceiverCompleted(record);
	                PromoteCompletedIfNeeded(record);
	                onComplete?.Invoke(record);
            });
    }

    private RelationshipDivinationRecord FindLatestActive(QuerySnapshot snapshot)
    {
        RelationshipDivinationRecord latest = null;
        if (snapshot == null) return latest;

	        foreach (DocumentSnapshot doc in snapshot.Documents)
	        {
	            RelationshipDivinationRecord record = MergeRuntimeRecordState(DeserializeRecord(doc));
	            if (record == null || record.IsCancelled || IsInviteExpired(record)) continue;
	            PromoteLegacyReceiverCompleted(record);
	            if (ShouldAutoComplete(record))
	            {
                PromoteCompletedIfNeeded(record);
                latest = PickLatest(latest, record);
                continue;
            }

            if (record.IsCompleted) continue;
            if (latest == null || ParseTime(record.createdAt) > ParseTime(latest.createdAt))
                latest = record;
        }

        return latest;
    }

    private static RelationshipDivinationRecord PickLatest(RelationshipDivinationRecord first, RelationshipDivinationRecord second)
    {
        if (first == null) return second;
        if (second == null) return first;

        DateTime firstTime = GetRecordRelevantTime(first);
        DateTime secondTime = GetRecordRelevantTime(second);
        return secondTime > firstTime ? second : first;
    }

	    private bool PromoteCompletedIfNeeded(RelationshipDivinationRecord record)
	    {
	        if (!ShouldAutoComplete(record)) return false;

        record.status = RelationshipDivinationStatus.Completed;
        if (string.IsNullOrWhiteSpace(record.completedAt))
            record.completedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        record.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        RememberRecordState(record);
        SyncCompletedStatus(record);
	        SavePersonalHistory(record);
	        return true;
	    }

	    private bool PromoteLegacyReceiverCompleted(RelationshipDivinationRecord record)
	    {
	        if (record == null || record.IsCancelled)
	            return false;

	        bool changed = false;
	        if (record.receiverRevealed && !record.initiatorRevealed)
	        {
	            record.initiatorRevealed = true;
	            changed = true;
	        }

	        if (record.receiverRevealed && !record.receiverJoined)
	        {
	            record.receiverJoined = true;
	            changed = true;
	        }

	        if (record.IsCompleted && !record.initiatorRevealed)
	        {
	            record.initiatorRevealed = true;
	            changed = true;
	        }

	        if (changed)
	            RememberRecordState(record);

	        return changed;
	    }

    private static bool ShouldAutoComplete(RelationshipDivinationRecord record)
    {
        return record != null
            && !record.IsCancelled
            && !record.IsCompleted
            && record.initiatorRevealed
            && record.receiverRevealed;
    }

    private void SyncCompletedStatus(RelationshipDivinationRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.readingId)) return;

        if (IsDebugReading(record))
        {
            StoreDebugRecord(record);
            return;
        }

        if (!IsReady) return;
        RememberRecordState(record);

	        db.Collection(CollectionName)
	            .Document(record.readingId)
	            .SetAsync(new Dictionary<string, object>
	            {
	                { "initiatorRevealed", true },
	                { "receiverJoined", true },
	                { "receiverRevealed", true },
	                { "status", RelationshipDivinationStatus.Completed },
	                { "completedAt", FieldValue.ServerTimestamp },
	                { "updatedAt", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                    Debug.LogWarning("[RelationshipDivinationFirestore] 自动补全完成状态失败: " + task.Exception?.InnerException?.Message);
            });
    }

	    private static bool IsCompletedToday(RelationshipDivinationRecord record)
	    {
	        if (record == null || !record.IsCompleted)
	            return false;

	        DateTime completedTime = GetRecordRelevantTime(record);
	        return completedTime != DateTime.MinValue && completedTime.Date == DateTime.Now.Date;
	    }

    private static DateTime GetRecordRelevantTime(RelationshipDivinationRecord record)
    {
        if (record == null) return DateTime.MinValue;

        DateTime completed = ParseTime(record.completedAt);
        if (completed != DateTime.MinValue) return completed;

        DateTime updated = ParseTime(record.updatedAt);
        if (updated != DateTime.MinValue) return updated;

        return ParseTime(record.createdAt);
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
        RememberRecordState(record);
    }

    private void RememberRecordState(RelationshipDivinationRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.readingId)) return;
        runtimeRecordStates[record.readingId] = CloneRecordState(record);
    }

    private RelationshipDivinationRecord MergeRuntimeRecordState(RelationshipDivinationRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.readingId))
            return record;

        if (runtimeRecordStates.TryGetValue(record.readingId, out RelationshipDivinationRecord cached)
            && IsRecordProgressAhead(cached, record))
        {
            CopyRecordState(record, cached);
        }

        RememberRecordState(record);
        return record;
    }

    private static bool IsRecordProgressAhead(RelationshipDivinationRecord candidate, RelationshipDivinationRecord current)
    {
        int candidateRank = GetRecordProgressRank(candidate);
        int currentRank = GetRecordProgressRank(current);
        if (candidateRank != currentRank)
            return candidateRank > currentRank;

        DateTime candidateTime = GetRecordRelevantTime(candidate);
        DateTime currentTime = GetRecordRelevantTime(current);
        return candidateTime != DateTime.MinValue && candidateTime > currentTime;
    }

    private static int GetRecordProgressRank(RelationshipDivinationRecord record)
    {
        if (record == null) return 0;
        if (record.IsCancelled) return 100;
        if (record.IsCompleted) return 90;
        if (record.initiatorRevealed && record.receiverRevealed) return 80;
        if (record.receiverRevealed) return 70;
        if (record.receiverJoined) return 60;
        if (record.initiatorRevealed) return 50;
        return 10;
    }

    private static RelationshipDivinationRecord CloneRecordState(RelationshipDivinationRecord source)
    {
        if (source == null) return null;

        RelationshipDivinationRecord clone = new RelationshipDivinationRecord();
        CopyRecordState(clone, source);
        if (source.cards != null)
        {
            clone.cards = new List<RelationshipDivinationCard>();
            foreach (RelationshipDivinationCard card in source.cards)
            {
                if (card == null) continue;
                clone.cards.Add(new RelationshipDivinationCard
                {
                    positionKey = card.positionKey,
                    position = card.position,
                    cardId = card.cardId,
                    cardName = card.cardName,
                    orientation = card.orientation,
                    visibleTo = card.visibleTo
                });
            }
        }
        return clone;
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

    private RelationshipDivinationRecord FindLatestDebugCompletedToday(string friendUid)
    {
        string currentUid = GetCurrentUid();
        RelationshipDivinationRecord latest = null;

        foreach (RelationshipDivinationRecord record in debugRecords.Values)
        {
            if (record == null || !record.IsCompleted || !IsCompletedToday(record)) continue;
            bool samePair = record.initiatorUid == currentUid && record.receiverUid == friendUid
                || record.initiatorUid == friendUid && record.receiverUid == currentUid;
            if (!samePair) continue;

            latest = PickLatest(latest, record);
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
	            status = RelationshipDivinationStatus.InitiatorRevealed,
	            initiatorRevealed = true,
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
            receiverSeenAt = GetTimeString(doc, data, "receiverSeenAt"),
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

    private void RunBackendAction(RelationshipDivinationRecord record, string action, Action<RelationshipDivinationRecord, string> onComplete)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.readingId))
        {
            onComplete?.Invoke(null, "占卜记录不存在");
            return;
        }

        StartCoroutine(RunBackendActionCoroutine(record, action, onComplete));
    }

    private IEnumerator RunBackendActionCoroutine(RelationshipDivinationRecord record, string action, Action<RelationshipDivinationRecord, string> onComplete)
    {
        string idToken = null;
        string tokenError = null;
        yield return GetFirebaseIdToken(
            token => idToken = token,
            error => tokenError = error);

        if (string.IsNullOrEmpty(idToken))
        {
            onComplete?.Invoke(null, string.IsNullOrWhiteSpace(tokenError) ? "用户未登录，无法同步双人占卜" : tokenError);
            yield break;
        }

        RelationshipActionRequest payload = new RelationshipActionRequest
        {
            readingId = record.readingId,
            action = action
        };
        string json = JsonUtility.ToJson(payload);

        using (UnityWebRequest request = new UnityWebRequest(ActionFunctionUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + idToken);

            yield return request.SendWebRequest();

            if (IsRequestFailed(request))
            {
                onComplete?.Invoke(null, ExtractBackendError(request, "双人占卜同步失败"));
                yield break;
            }

            RelationshipActionResponse response = null;
            try
            {
                response = JsonUtility.FromJson<RelationshipActionResponse>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                onComplete?.Invoke(null, "双人占卜同步结果解析失败: " + e.Message);
                yield break;
            }

            if (response == null || !response.ok || response.record == null)
            {
                string error = response != null && !string.IsNullOrWhiteSpace(response.error)
                    ? response.error
                    : "双人占卜同步失败";
                onComplete?.Invoke(null, error);
                yield break;
            }

            RelationshipDivinationRecord updated = ApplyBackendRecord(record, response.record);
            onComplete?.Invoke(updated, null);
        }
    }

    private RelationshipDivinationRecord ApplyBackendRecord(RelationshipDivinationRecord target, RelationshipDivinationRecord backendRecord)
    {
        if (backendRecord == null) return null;

        if ((backendRecord.cards == null || backendRecord.cards.Count == 0)
            && target != null
            && target.cards != null
            && target.cards.Count > 0)
        {
            backendRecord.cards = target.cards;
        }

        RelationshipDivinationRecord result = target ?? backendRecord;
        CopyRecordState(result, backendRecord);
        PromoteLegacyReceiverCompleted(result);
        RememberRecordState(result);

        if (result.IsCompleted)
            SavePersonalHistory(result);

        return result;
    }

    private IEnumerator GetFirebaseIdToken(Action<string> onToken, Action<string> onError)
    {
#if UNITY_EDITOR
        if (FirebaseAuthManager.Instance != null
            && FirebaseAuthManager.Instance.TryGetEditorRestIdToken(out string editorRestToken))
        {
            onToken?.Invoke(editorRestToken);
            yield break;
        }
#endif

        var user = FirebaseAuth.DefaultInstance?.CurrentUser;
        if (user == null)
        {
            onError?.Invoke("用户未登录，无法同步双人占卜");
            yield break;
        }

        var task = user.TokenAsync(false);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled)
        {
            onError?.Invoke(task.Exception?.InnerException?.Message ?? "获取 Firebase Token 失败");
            yield break;
        }

        onToken?.Invoke(task.Result);
    }

    private static bool IsRequestFailed(UnityWebRequest request)
    {
#if UNITY_2020_1_OR_NEWER
        return request.result == UnityWebRequest.Result.ConnectionError
            || request.result == UnityWebRequest.Result.ProtocolError;
#else
        return request.isNetworkError || request.isHttpError;
#endif
    }

    private static string ExtractBackendError(UnityWebRequest request, string fallback)
    {
        string body = request.downloadHandler != null ? request.downloadHandler.text : "";
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                RelationshipActionResponse response = JsonUtility.FromJson<RelationshipActionResponse>(body);
                if (response != null && !string.IsNullOrWhiteSpace(response.error))
                    return response.error;
            }
            catch
            {
                // Fall through to request.error/fallback.
            }
        }

        if (!string.IsNullOrWhiteSpace(request.error))
            return request.error;
        return fallback;
    }

    private void SavePersonalHistory(RelationshipDivinationRecord record)
    {
        if (record == null) return;

        DivinationRecordData recordData = RelationshipDivinationFlow.BuildDivinationRecord(record);
        RelationshipDivinationFlow.GetOrCreateHistoryService()?.SaveRecord(recordData, false);
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

        string firebaseUid = Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? "";
        if (!string.IsNullOrWhiteSpace(firebaseUid))
            return firebaseUid;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return "debug_current_user";
#else
        return "";
#endif
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
