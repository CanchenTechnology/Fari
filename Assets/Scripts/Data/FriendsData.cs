using System;
using System.Collections.Generic;
using GamerFrameWork.OracleRuntime;
using UnityEngine;

/// <summary>
/// 好友资料数据。
/// 真实好友、创建的好友档案都使用同一套字段，业务层再通过 isVirtual 区分来源。
/// </summary>
[System.Serializable]
public class FriendProfileData
{
    public int id;
    public string firebaseUid;
    public string virtualFriendId;
    public string name;
    public string handle;
    public string info;
    public string relationship;
    public string birthday;
    public string birthTime;
    public string city;
    public string notes;
    public string source;
    public string photoUrl;
    public string avatarImagePath;
    public string avatarStoragePath;
    public Sprite headSprite;
    public bool isOnline;
    public long lastLoginUnixMs;
    public long virtualFriendLastOperatedUnixMs;
    public bool isVirtual;

    public string StableId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(firebaseUid)) return firebaseUid.Trim();
            if (!string.IsNullOrWhiteSpace(virtualFriendId)) return virtualFriendId.Trim();
            return id > 0 ? id.ToString() : string.Empty;
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(name) ? "未命名好友" : name.Trim();

    public string BuildOracleContext()
    {
        var parts = new List<string>();
        parts.Add($"姓名：{DisplayName}");
        if (!string.IsNullOrWhiteSpace(firebaseUid)) parts.Add($"Firebase UID：{firebaseUid}");
        if (!string.IsNullOrWhiteSpace(virtualFriendId)) parts.Add($"虚拟好友ID：{virtualFriendId}");
        if (!string.IsNullOrWhiteSpace(handle)) parts.Add($"用户名：{handle}");
        if (!string.IsNullOrWhiteSpace(relationship)) parts.Add($"关系：{relationship}");
        if (!string.IsNullOrWhiteSpace(birthday)) parts.Add($"生日：{birthday}");
        if (!string.IsNullOrWhiteSpace(birthTime)) parts.Add($"出生时间：{birthTime}");
        if (!string.IsNullOrWhiteSpace(city)) parts.Add($"城市：{city}");
        if (!string.IsNullOrWhiteSpace(notes)) parts.Add($"背景：{notes}");
        parts.Add(isVirtual ? "类型：创建的好友档案" : "类型：真实好友");
        return string.Join("\n", parts);
    }
}

/// <summary>
/// 好友邀请/请求数据。
/// </summary>
[System.Serializable]
public class FriendInviteData
{
    public int id;
    public string firebaseUid;
    public string email;
    public string photoUrl;
    public string status;
    public string name;
    public string info;
    public string seenAt;
    public Sprite headSprite;

    public string DisplayName => string.IsNullOrWhiteSpace(name) ? "未命名用户" : name.Trim();
}

/// <summary>
/// 好友分组数据（如：已有好友、创建的好友）
/// </summary>
[System.Serializable]
public class FriendGroupData
{
    public string groupName;
    public bool isExpanded = true;
    public List<FriendProfileData> friends = new List<FriendProfileData>();
}

/// <summary>
/// 今日神谕完整阅读数据。
/// 一次翻牌后，卡牌、今日神谕正文、完整解读、缓存日期和同步状态都汇聚到这里。
/// </summary>
[Serializable]
public class TodayOracleReadingData
{
    public string date;
    public TarotCard card;
    public bool upright;

    public string cardId;
    public string cardDisplayName;
    public string cardDescription;
    public string cardMeaning;
    public Sprite cardIcon;

    public TodayCardPayload cardPayload;
    public TodayOraclePayload oraclePayload;
    public CompleteInterpretationPayload interpretationPayload;

    public string locale;
    public string oracleId;
    public string preparedAt;
    public string createdAtLocal;
    public bool syncEnabled;
    public string visibility;
    public bool summaryOnly;
    public bool isCloudBacked;
    public bool isPendingSync;

    public bool IsFor(TarotCard targetCard, bool targetUpright)
    {
        return targetCard != null
            && cardId == targetCard.cardId
            && upright == targetUpright;
    }

    public TodayCardPayload BuildCardPayload()
    {
        TarotCard sourceCard = card ?? TarotDeck.GetById(cardId);
        string resolvedCardId = FirstNonEmpty(cardId, sourceCard?.cardId);
        string displayName = FirstNonEmpty(cardDisplayName, sourceCard?.DisplayName(upright), cardDisplayName);

        return new TodayCardPayload
        {
            cardId = resolvedCardId,
            cardName = sourceCard?.nameEn ?? "",
            displayName = displayName,
            nameZh = sourceCard?.nameZh ?? cardDisplayName ?? "",
            orientation = upright ? "upright" : "reversed",
            generatedAt = FirstNonEmpty(preparedAt, createdAtLocal, DateTime.Now.ToString("o")),
            oracleText = oraclePayload?.oracle,
            title = FirstNonEmpty(oraclePayload?.title, "今日塔罗")
        };
    }

    public static TodayOracleReadingData FromPreparedReading(
        TodayOraclePreparedReading preparedReading,
        string date = "",
        string locale = "zh-CN",
        bool isCloudBacked = false,
        bool isPendingSync = false)
    {
        if (preparedReading == null) return null;

        return new TodayOracleReadingData
        {
            date = string.IsNullOrWhiteSpace(date) ? DateTime.Now.ToString("yyyy-MM-dd") : date,
            card = preparedReading.card,
            upright = preparedReading.upright,
            cardId = preparedReading.cardId,
            cardDisplayName = preparedReading.cardDisplayName,
            cardDescription = preparedReading.cardDescription,
            cardMeaning = preparedReading.cardMeaning,
            cardIcon = preparedReading.cardIcon,
            cardPayload = preparedReading.cardPayload,
            oraclePayload = preparedReading.oraclePayload,
            interpretationPayload = preparedReading.interpretationPayload,
            locale = string.IsNullOrWhiteSpace(locale) ? "zh-CN" : locale,
            oracleId = "",
            preparedAt = preparedReading.preparedAt,
            createdAtLocal = preparedReading.preparedAt,
            syncEnabled = false,
            visibility = "",
            summaryOnly = false,
            isCloudBacked = isCloudBacked,
            isPendingSync = isPendingSync
        };
    }

    public static TodayOracleReadingData FromCloudRecord(DailyOracleCloudRecord record)
    {
        if (record == null || !record.HasPayload) return null;

        TarotCard card = TarotDeck.GetById(record.cardId);
        bool upright = record.IsUpright;
        TodayOraclePayload payload = record.ToPayload();

        return new TodayOracleReadingData
        {
            date = record.date,
            card = card,
            upright = upright,
            cardId = record.cardId,
            cardDisplayName = card != null ? card.DisplayName(upright) : record.cardName,
            cardDescription = payload.detail,
            cardMeaning = payload.oracle,
            cardIcon = card != null ? TarotSpriteLoader.Load(card.cardId) : null,
            cardPayload = new TodayCardPayload
            {
                cardId = record.cardId,
                cardName = card?.nameEn ?? "",
                displayName = card != null ? card.DisplayName(upright) : record.cardName,
                nameZh = card?.nameZh ?? record.cardName,
                orientation = record.orientation,
                generatedAt = record.createdAtLocal,
                oracleText = record.oracle,
                title = record.title
            },
            oraclePayload = payload,
            interpretationPayload = null,
            locale = record.locale,
            oracleId = record.oracleId,
            preparedAt = record.createdAtLocal,
            createdAtLocal = record.createdAtLocal,
            syncEnabled = record.syncEnabled,
            visibility = record.visibility,
            summaryOnly = record.summaryOnly,
            isCloudBacked = true,
            isPendingSync = false
        };
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
}

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
    public string DisplayName => TarotDeck.FormatDisplayName(cardName, IsUpright);
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
    public string receiverSeenAt;
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
        {
            if (receiverRevealed) return $"已收到 {receiverName} 的牌，正在同步共同结果";
            if (receiverJoined) return $"{receiverName} 已接受邀请，等待对方抽牌";
            return initiatorRevealed ? $"已翻开你的牌，正在等待 {receiverName} 加入" : "邀请已创建，你可以先翻开自己的牌";
        }
        if (IsCurrentUserReceiver(currentUid))
        {
            if (receiverRevealed) return "你已抽牌，等待共同结果同步";
            if (receiverJoined) return "你已接受邀请，请抽取你的牌";
            return $"{initiatorName} 邀请你进行双人关系占卜";
        }
        return "双人关系占卜进行中";
    }

    private RelationshipDivinationCard FindCard(string positionKey)
    {
        if (cards == null) return null;
        return cards.Find(card => card != null && card.positionKey == positionKey);
    }
}

[Serializable]
public class DivinationRecordCacheWrapper
{
    public List<DivinationRecordData> records = new List<DivinationRecordData>();
}

/// <summary>
/// 通用占卜历史记录。
/// 单牌、三牌/多牌牌阵、今日神谕和双人关系占卜都落到这个模型，方便历史页统一展示。
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
    public string judgeContent;
    public string adviceContent;
    public List<string> topics;
    public string oracleId;
    public string createdAt;
    public string relationshipFriendUid;
    public string relationshipFriendName;
    public string relationshipFriendAvatarUrl;

    public string DisplayTime
    {
        get
        {
            if (DateTime.TryParse(createdAt, out var dt))
                return dt.ToString("MM/dd HH:mm");
            return createdAt ?? "";
        }
    }

    public string QuestionPreview
    {
        get
        {
            if (string.IsNullOrEmpty(question)) return "";
            return question.Length > 20 ? question.Substring(0, 20) + "..." : question;
        }
    }

    public string CardsSummary
    {
        get
        {
            if (lockedCards == null || lockedCards.Count == 0) return "";
            var names = new List<string>();
            foreach (var card in lockedCards)
            {
                string orientation = card.orientation == "upright" ? "正" : "逆";
                names.Add(TarotDeck.FormatDisplayName(string.IsNullOrWhiteSpace(card.cardName) ? card.cardId : card.cardName, orientation));
            }
            return string.Join(" · ", names);
        }
    }

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
        switch (kind)
        {
            case "daily_oracle":
                return "今日神谕";
            case "friend_relationship_divination":
            case "relationship_tension":
                return "双人关系占卜";
            case "three_card":
                return "三牌占卜";
            default:
                return "";
        }
    }
}

/// <summary>
/// 用户个人资料数据快照。
/// UserDataManager 负责持久化，FirestoreManager 负责云端同步，这个类只描述资料本身。
/// </summary>
[Serializable]
public class UserProfileData
{
    public string userName;
    public string birthday;
    public string birthTime;
    public string city;
    public string profileBio;
    public AvatarType avatarType;

    public string email;
    public string userId;
    public string regTime;
    public LoginType loginType;
    public AccountStatus status;

    public string photoUrl;
    public string avatarStoragePath;
    public string firebaseUid;
    public string authProviderId;
    public string facebookProviderUserId;
    public bool isFirebaseAuthenticated;
    public bool isEmailVerified;
    public long creationTimestamp;
    public long lastSignInTimestamp;

    public static UserProfileData FromManager(UserDataManager manager)
    {
        if (manager == null) return new UserProfileData();

        return new UserProfileData
        {
            userName = manager.UserName,
            birthday = manager.Birthday,
            birthTime = manager.BirthTime,
            city = manager.City,
            profileBio = manager.ProfileBio,
            avatarType = manager.CurrentAvatar,
            email = manager.Email,
            userId = manager.UserId,
            regTime = manager.RegTime,
            loginType = manager.CurrentLoginType,
            status = manager.Status,
            photoUrl = manager.PhotoUrl,
            avatarStoragePath = manager.AvatarStoragePath,
            firebaseUid = manager.FirebaseUid,
            authProviderId = manager.AuthProviderId,
            facebookProviderUserId = manager.FacebookProviderUserId,
            isFirebaseAuthenticated = manager.IsFirebaseAuthenticated,
            isEmailVerified = manager.IsEmailVerified,
            creationTimestamp = manager.CreationTimestamp,
            lastSignInTimestamp = manager.LastSignInTimestamp
        };
    }
}

[Serializable]
public class UserSearchProfileData
{
    public string uid;
    public string displayName;
    public string email;
    public string photoUrl;
    public string birthday;
    public string birthTime;
    public string city;
    public bool basicInfoVisible;
    public bool hasBasicInfoVisibility;
    public string profileVisibility;
    public bool isSelf;

    public string Handle => string.IsNullOrEmpty(email) ? $"@{uid}" : $"@{email.Split('@')[0]}";
    public string Info => string.IsNullOrEmpty(email) ? "Firebase 注册用户" : email;
}
