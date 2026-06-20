using System;
using System.Collections.Generic;
using GamerFrameWork.OracleRuntime;
using GamerFrameWork.UIFrameWork;
using UnityEngine;
using UnityEngine.UI;

public static class RelationshipDivinationFlow
{
    private const int InviteValidHours = 24;
    private const string DailyPairKeyPrefix = "RelationshipDivinationDailyPair_";
    private const string DebugFriendUidPrefix = "test_real_friend_";
    private const string DebugReadingIdPrefix = "rel_debug_";

    public static FriendDataManager.FriendData CurrentFriend { get; private set; }
    public static RelationshipDivinationRecord CurrentRecord { get; private set; }

    public static void ShowInviteConfirm(FriendDataManager.FriendData friend)
    {
        if (friend == null)
        {
            ToastManager.ShowToast("好友资料不完整");
            return;
        }

        if (!CanUseTwoPersonDivination(friend, true))
            return;

        CurrentFriend = friend;
        CurrentRecord = null;
        HideFlowWindows();
        UIModule.Instance.PopUpWindow<TwoPersonDivinationInviteConfirmFlowUI>();
    }

    public static void ShowRecord(RelationshipDivinationRecord record, FriendDataManager.FriendData friend = null)
    {
        if (record == null)
        {
            ToastManager.ShowToast("双人占卜记录不存在");
            return;
        }

        CurrentRecord = record;
        CurrentFriend = friend ?? FindFriendForRecord(record) ?? BuildFallbackFriend(record);

        if (record.IsCancelled)
        {
            ToastManager.ShowToast("这次双人占卜邀请已取消");
            HideFlowWindows();
            return;
        }

        if (record.IsCompleted || record.isLocalOnly)
        {
            ShowRevealReady(record, CurrentFriend);
            return;
        }

        string uid = GetCurrentUid();
        if (IsInviteExpired(record))
        {
            ToastManager.ShowToast("这次双人占卜邀请已过期");
            HideFlowWindows();
            return;
        }

        if (CurrentFriend != null && !IsDebugTestFriend(CurrentFriend) && IsDailyLimitReached(CurrentFriend))
        {
            ToastManager.ShowToast("你们今天已经完成过一次双人占卜，明天再开启新的指引");
            HideFlowWindows();
            return;
        }

        if (record.CanCurrentUserReveal(uid))
        {
            ShowInviteSent(record, CurrentFriend);
        }
        else
        {
            ShowWaitingFriend(record, CurrentFriend);
        }
    }

    public static void ShowInviteSent(RelationshipDivinationRecord record, FriendDataManager.FriendData friend = null)
    {
        CurrentRecord = record;
        CurrentFriend = friend ?? CurrentFriend ?? FindFriendForRecord(record) ?? BuildFallbackFriend(record);
        HideFlowWindows();
        UIModule.Instance.PopUpWindow<TwoPersonDivinationInviteSentFlowUI>();
    }

    public static void ShowWaitingFriend(RelationshipDivinationRecord record, FriendDataManager.FriendData friend = null)
    {
        CurrentRecord = record;
        CurrentFriend = friend ?? CurrentFriend ?? FindFriendForRecord(record) ?? BuildFallbackFriend(record);
        HideFlowWindows();
        UIModule.Instance.PopUpWindow<TwoPersonDivinationWaitingFriendFlowUI>();
    }

    public static void ShowRevealReady(RelationshipDivinationRecord record, FriendDataManager.FriendData friend = null)
    {
        CurrentRecord = record;
        CurrentFriend = friend ?? CurrentFriend ?? FindFriendForRecord(record) ?? BuildFallbackFriend(record);
        MarkDailyCompleted(record);
        SaveResultToHistory(record);
        HideFlowWindows();
        UIModule.Instance.PopUpWindow<TwoPersonDivinationRevealReadyFlowUI>();
    }

    public static void HideFlowWindows()
    {
        UIModule.Instance.HideWindow<TwoPersonDivinationInviteConfirmFlowUI>();
        UIModule.Instance.HideWindow<TwoPersonDivinationInviteSentFlowUI>();
        UIModule.Instance.HideWindow<TwoPersonDivinationWaitingFriendFlowUI>();
        UIModule.Instance.HideWindow<TwoPersonDivinationRevealReadyFlowUI>();
        UIModule.Instance.HideWindow<TwoPersonDivinationResultFlowUI>();
    }

    public static void RefreshCurrentRecord(Action<RelationshipDivinationRecord> onComplete)
    {
        RelationshipDivinationRecord record = CurrentRecord;
        RelationshipDivinationFirestore service = RelationshipDivinationFirestore.Instance;
        if (record == null || record.isLocalOnly || service == null)
        {
            onComplete?.Invoke(record);
            return;
        }

        service.LoadReading(record.readingId, updated =>
        {
            if (updated != null)
            {
                CurrentRecord = updated;
                if (CurrentFriend == null)
                    CurrentFriend = FindFriendForRecord(updated) ?? BuildFallbackFriend(updated);
            }

            onComplete?.Invoke(CurrentRecord);
        });
    }

    public static void TryOpenActiveOrCreate(FriendDataManager.FriendData friend, Action onCanCreate)
    {
        if (friend == null)
        {
            ToastManager.ShowToast("好友资料不完整");
            return;
        }

        if (!CanUseTwoPersonDivination(friend, true))
            return;

        RelationshipDivinationFirestore service = RelationshipDivinationFirestore.Instance;
        if (service == null)
        {
            ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
            return;
        }

        if (IsDebugTestFriend(friend))
        {
            onCanCreate?.Invoke();
            return;
        }

        if (string.IsNullOrWhiteSpace(friend.firebaseUid) || !service.IsReady)
        {
            if (CanCreateNewReading(friend, true))
                onCanCreate?.Invoke();
            return;
        }

        service.LoadActiveWithFriend(friend.firebaseUid, active =>
        {
            if (active != null)
            {
                ShowRecord(active, friend);
                return;
            }

            if (CanCreateNewReading(friend, true))
                onCanCreate?.Invoke();
        });
    }

    public static bool CanCreateNewReading(FriendDataManager.FriendData friend, bool showToast)
    {
        if (friend == null) return false;
        if (!CanUseTwoPersonDivination(friend, showToast)) return false;
        if (IsDebugTestFriend(friend)) return true;
        if (!IsDailyLimitReached(friend)) return true;

        if (showToast)
            ToastManager.ShowToast("你们今天已经完成过一次双人占卜，明天再开启新的指引");
        return false;
    }

    public static bool CanUseTwoPersonDivination(FriendDataManager.FriendData friend, bool showToast)
    {
        if (friend == null) return false;
        if (friend.isVirtual) return true;
        if (!friend.isVirtual && !string.IsNullOrWhiteSpace(friend.firebaseUid)) return true;

        if (showToast)
            ToastManager.ShowToast("真实好友关系未同步，暂时不能发起双人占卜");
        return false;
    }

    public static string GetDailyLimitText(FriendDataManager.FriendData friend)
    {
        if (IsDebugTestFriend(friend))
            return "测试好友 · 不受每日次数限制";

        return IsDailyLimitReached(friend)
            ? "今日同好友占卜 · 剩余 0 次"
            : "今日同好友占卜 · 剩余 1 次";
    }

    public static string BuildQuestion(FriendDataManager.FriendData friend)
    {
        string friendName = GetFriendName(friend);
        return $"我和 {friendName} 的关系接下来会如何发展？";
    }

    public static void OpenResult(RelationshipDivinationRecord record)
    {
        if (record == null)
        {
            ToastManager.ShowToast("占卜结果不存在");
            return;
        }

        if (!record.IsCompleted && !record.isLocalOnly)
        {
            ToastManager.ShowToast("等待双方都完成翻牌后，才会开启共同结果");
            return;
        }

        MarkDailyCompleted(record);
        DivinationRecordData recordData = BuildDivinationRecord(record);
        SaveResultToHistory(record);
        DialogSystem.Instance?.ActivateReadingFromRecord(recordData, DivinationPhase.Completed);
        CurrentRecord = record;
        CurrentFriend = CurrentFriend ?? FindFriendForRecord(record) ?? BuildFallbackFriend(record);
        HideFlowWindows();
        UIModule.Instance.PopUpWindow<TwoPersonDivinationResultFlowUI>();
    }

    public static string BuildShareText(RelationshipDivinationRecord record)
    {
        if (record == null) return string.Empty;

        string currentUid = GetCurrentUid();
        string otherName = GetOtherName(record, currentUid);
        string link = $"moonly://relationship-divination/{record.readingId}";
        return $"我邀请你进行双人关系占卜：{record.question}\n与 {otherName} 一起翻开三张关系牌。\n{link}";
    }

    public static void CopyInviteText(RelationshipDivinationRecord record, string successText = "邀请链接已复制")
    {
        string text = BuildShareText(record);
        if (string.IsNullOrWhiteSpace(text))
        {
            ToastManager.ShowToast("暂无可复制的邀请信息");
            return;
        }

        GUIUtility.systemCopyBuffer = text;
        ToastManager.ShowToast(successText);
    }

    public static TimeSpan GetInviteRemaining(RelationshipDivinationRecord record)
    {
        if (record == null) return TimeSpan.Zero;

        DateTime createdAt = ParseTime(record.createdAt);
        if (createdAt == DateTime.MinValue)
            createdAt = DateTime.Now;

        DateTime expiresAt = createdAt.AddHours(InviteValidHours);
        TimeSpan remaining = expiresAt - DateTime.Now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public static bool IsInviteExpired(RelationshipDivinationRecord record)
    {
        if (record == null || record.isLocalOnly || record.IsCompleted || record.IsCancelled) return false;
        DateTime createdAt = ParseTime(record.createdAt);
        if (createdAt == DateTime.MinValue) return false;
        return createdAt.AddHours(InviteValidHours) <= DateTime.Now;
    }

    public static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero) return "已过期";
        int hours = Mathf.FloorToInt((float)remaining.TotalHours);
        return $"{hours:00}:{remaining.Minutes:00}";
    }

    public static RelationshipDivinationCard GetMyPrivateCard(RelationshipDivinationRecord record)
    {
        if (record == null) return null;
        string uid = GetCurrentUid();
        if (record.isLocalOnly || record.IsCurrentUserInitiator(uid))
            return record.InitiatorCard;
        if (record.IsCurrentUserReceiver(uid))
            return record.ReceiverCard;
        return record.InitiatorCard;
    }

    public static RelationshipDivinationCard GetFriendPrivateCard(RelationshipDivinationRecord record)
    {
        if (record == null) return null;
        string uid = GetCurrentUid();
        if (record.IsCurrentUserInitiator(uid))
            return record.ReceiverCard;
        if (record.IsCurrentUserReceiver(uid))
            return record.InitiatorCard;
        return record.ReceiverCard;
    }

    public static bool IsMyCardRevealed(RelationshipDivinationRecord record)
    {
        if (record == null) return false;
        string uid = GetCurrentUid();
        if (record.isLocalOnly || record.IsCurrentUserInitiator(uid))
            return record.initiatorRevealed;
        if (record.IsCurrentUserReceiver(uid))
            return record.receiverRevealed;
        return record.initiatorRevealed;
    }

    public static bool IsFriendCardRevealed(RelationshipDivinationRecord record)
    {
        if (record == null) return false;
        string uid = GetCurrentUid();
        if (record.IsCurrentUserInitiator(uid))
            return record.receiverRevealed;
        if (record.IsCurrentUserReceiver(uid))
            return record.initiatorRevealed;
        return false;
    }

    public static Sprite LoadCardSprite(RelationshipDivinationCard card)
    {
        return card == null ? null : TarotSpriteLoader.Load(card.cardId);
    }

    public static void SetButtonText(Button button, string text)
    {
        if (button == null) return;
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null) label.text = text ?? string.Empty;
    }

    public static string GetFriendName(FriendDataManager.FriendData friend)
    {
        return string.IsNullOrWhiteSpace(friend?.name) ? "好友" : friend.name.Trim();
    }

    public static string GetOtherName(RelationshipDivinationRecord record, string currentUid)
    {
        if (record == null) return "好友";
        if (record.isLocalOnly)
            return string.IsNullOrWhiteSpace(record.receiverName) ? "创建好友" : record.receiverName;

        return record.IsCurrentUserInitiator(currentUid)
            ? FirstNonEmpty(record.receiverName, "好友")
            : FirstNonEmpty(record.initiatorName, "好友");
    }

    public static string GetOtherUid(RelationshipDivinationRecord record, string currentUid)
    {
        if (record == null) return "";
        if (record.IsCurrentUserInitiator(currentUid)) return record.receiverUid;
        if (record.IsCurrentUserReceiver(currentUid)) return record.initiatorUid;
        return FirstNonEmpty(record.receiverUid, record.initiatorUid);
    }

    public static string GetCurrentUid()
    {
        if (UserDataManager.Instance != null && !string.IsNullOrWhiteSpace(UserDataManager.Instance.FirebaseUid))
            return UserDataManager.Instance.FirebaseUid;
        string firebaseUid = Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? "";
        if (!string.IsNullOrWhiteSpace(firebaseUid)) return firebaseUid;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return "debug_current_user";
#else
        return "";
#endif
    }

    public static FriendDataManager.FriendData FindFriendForRecord(RelationshipDivinationRecord record)
    {
        if (record == null || FriendDataManager.Instance == null) return null;
        return FriendDataManager.Instance.FindRealFriendByFirebaseUid(GetOtherUid(record, GetCurrentUid()));
    }

    public static DivinationRecordData BuildDivinationRecord(RelationshipDivinationRecord record)
    {
        return new DivinationRecordData
        {
            readingId = record.readingId,
            question = record.question,
            scene = "friend_relationship_divination",
            spreadKind = "relationship_tension",
            lockedCards = ToLockedCards(record.cards),
            shortVerdict = BuildShortVerdict(record),
            judgeContent = BuildJudgeContent(record),
            adviceContent = BuildAdviceContent(record),
            topics = BuildTopics(record),
            oracleId = string.IsNullOrWhiteSpace(record.oracleId) ? "tarot" : record.oracleId,
            createdAt = string.IsNullOrWhiteSpace(record.createdAt) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : record.createdAt
        };
    }

    public static void SaveResultToHistory(RelationshipDivinationRecord record)
    {
        if (record == null || (!record.IsCompleted && !record.isLocalOnly)) return;

        DivinationRecordData recordData = BuildDivinationRecord(record);
        DivinationRecordFirestore.SaveRecordLocal(recordData);
        DivinationRecordFirestore.Instance?.SaveRecord(recordData);
    }

    public static void MarkDailyCompleted(RelationshipDivinationRecord record)
    {
        string key = BuildDailyKey(record);
        if (string.IsNullOrEmpty(key)) return;

        PlayerPrefs.SetString(key, DateTime.Now.ToString("yyyyMMdd"));
        PlayerPrefs.Save();
    }

    private static bool IsDailyLimitReached(FriendDataManager.FriendData friend)
    {
        if (IsDebugTestFriend(friend)) return false;
        string key = BuildDailyKey(friend);
        if (string.IsNullOrEmpty(key)) return false;

        return PlayerPrefs.GetString(key, "") == DateTime.Now.ToString("yyyyMMdd");
    }

    private static string BuildDailyKey(FriendDataManager.FriendData friend)
    {
        if (friend == null) return "";
        string self = FirstNonEmpty(GetCurrentUid(), UserDataManager.Instance?.UserId, "local_user");
        string other = GetFriendStableId(friend);
        if (string.IsNullOrWhiteSpace(other)) return "";
        return DailyPairKeyPrefix + SanitizeKey(self) + "_" + SanitizeKey(other);
    }

    private static string BuildDailyKey(RelationshipDivinationRecord record)
    {
        if (record == null) return "";
        if (IsDebugReading(record)) return "";
        string self = FirstNonEmpty(GetCurrentUid(), UserDataManager.Instance?.UserId, "local_user");
        string other = GetOtherUid(record, self);
        if (string.IsNullOrWhiteSpace(other)) return "";
        return DailyPairKeyPrefix + SanitizeKey(self) + "_" + SanitizeKey(other);
    }

    private static string GetFriendStableId(FriendDataManager.FriendData friend)
    {
        if (friend == null) return "";
        if (!string.IsNullOrWhiteSpace(friend.firebaseUid)) return friend.firebaseUid.Trim();
        if (!string.IsNullOrWhiteSpace(friend.virtualFriendId)) return $"virtual:{friend.virtualFriendId.Trim()}";
        return $"local:{friend.id}";
    }

    private static string SanitizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "empty";
        return value.Trim().Replace(":", "_").Replace("/", "_").Replace("\\", "_").Replace(" ", "_");
    }

    private static FriendDataManager.FriendData BuildFallbackFriend(RelationshipDivinationRecord record)
    {
        string uid = GetCurrentUid();
        return new FriendDataManager.FriendData
        {
            firebaseUid = GetOtherUid(record, uid),
            name = GetOtherName(record, uid),
            relationship = "好友",
            info = "双人关系占卜对象",
            isVirtual = record != null && record.isLocalOnly
        };
    }

    private static bool IsDebugTestFriend(FriendDataManager.FriendData friend)
    {
        return friend != null
            && !string.IsNullOrWhiteSpace(friend.firebaseUid)
            && friend.firebaseUid.StartsWith(DebugFriendUidPrefix, StringComparison.Ordinal);
    }

    private static bool IsDebugReading(RelationshipDivinationRecord record)
    {
        if (record == null) return false;
        return (!string.IsNullOrWhiteSpace(record.readingId) && record.readingId.StartsWith(DebugReadingIdPrefix, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(record.initiatorUid) && record.initiatorUid.StartsWith(DebugFriendUidPrefix, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(record.receiverUid) && record.receiverUid.StartsWith(DebugFriendUidPrefix, StringComparison.Ordinal));
    }

    private static List<LockedCard> ToLockedCards(List<RelationshipDivinationCard> cards)
    {
        List<LockedCard> result = new List<LockedCard>();
        if (cards == null) return result;

        foreach (RelationshipDivinationCard card in cards)
        {
            if (card == null) continue;
            result.Add(new LockedCard
            {
                position = card.position,
                positionKey = card.positionKey,
                cardId = card.cardId,
                cardName = card.cardName,
                orientation = card.orientation
            });
        }

        return result;
    }

    private static string BuildShortVerdict(RelationshipDivinationRecord record)
    {
        RelationshipDivinationCard shared = record.SharedCard;
        string sharedText = shared != null ? shared.DisplayName : "共同牌";
        return $"双人关系占卜已完成：{record.initiatorName} 与 {record.receiverName} 的共同揭示为 {sharedText}。";
    }

    private static string BuildJudgeContent(RelationshipDivinationRecord record)
    {
        List<string> lines = new List<string>
        {
            "你们一起完成了这组三张关系牌。它更适合当作一次真诚沟通的入口，而不是替彼此下定论。",
            BuildCardLine(record.InitiatorCard),
            BuildCardLine(record.SharedCard),
            BuildCardLine(record.ReceiverCard)
        };
        return string.Join("\n", lines);
    }

    private static string BuildAdviceContent(RelationshipDivinationRecord record)
    {
        RelationshipDivinationCard shared = record.SharedCard;
        string sharedText = shared != null ? shared.DisplayName : "共同牌";
        return $"建议先围绕“{sharedText}”讨论你们现在共同感受到的关系状态，再分别表达自己的期待与边界。把这次占卜当作温柔的开场，而不是要求对方立刻给出答案。";
    }

    private static List<string> BuildTopics(RelationshipDivinationRecord record)
    {
        return new List<string>
        {
            "我们现在最需要坦诚沟通的部分是什么？",
            "我可以怎样更温柔地表达自己的期待？",
            "这段关系下一步最适合的小行动是什么？"
        };
    }

    private static string BuildCardLine(RelationshipDivinationCard card)
    {
        if (card == null) return "";
        return $"{card.position}：{card.DisplayName}";
    }

    private static DateTime ParseTime(string value)
    {
        return DateTime.TryParse(value, out DateTime parsed) ? parsed : DateTime.MinValue;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null) return "";
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "";
    }
}
