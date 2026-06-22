using System;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using UnityEngine;

public static class CreatedFriendRelationshipDivinationLocalFlow
{
    private const string ReadingIdPrefix = "rel_local_";

    public static bool CanHandle(FriendDataManager.FriendData friend)
    {
        return friend != null && friend.isVirtual;
    }

    public static bool TryStart(FriendDataManager.FriendData friend)
    {
        if (!CanHandle(friend)) return false;

        RelationshipDivinationRecord record = CreateRecord(friend, RelationshipDivinationFlow.BuildQuestion(friend));
        GamerFrameWork.UIFrameWork.ToastManager.ShowToast($"已为 {record.receiverName} 生成本地关系占卜");
        RelationshipDivinationFlow.ShowRecord(record, friend);
        return true;
    }

    public static RelationshipDivinationRecord CreateRecord(FriendDataManager.FriendData friend, string question)
    {
        string friendName = string.IsNullOrWhiteSpace(friend?.name) ? "好友" : friend.name.Trim();
        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        return new RelationshipDivinationRecord
        {
            readingId = $"{ReadingIdPrefix}{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
            initiatorUid = RelationshipDivinationFlow.GetCurrentUid(),
            receiverUid = BuildVirtualReceiverUid(friend),
            initiatorName = GetCurrentUserName(),
            receiverName = friendName,
            question = string.IsNullOrWhiteSpace(question) ? $"我和 {friendName} 的关系接下来会如何发展？" : question.Trim(),
            status = RelationshipDivinationStatus.Completed,
            initiatorRevealed = true,
            receiverJoined = true,
            receiverRevealed = true,
            cards = DrawCards(),
            createdAt = now,
            updatedAt = now,
            completedAt = now,
            oracleId = GetCurrentOracleId(),
            isLocalOnly = true
        };
    }

    private static string BuildVirtualReceiverUid(FriendDataManager.FriendData friend)
    {
        if (!string.IsNullOrWhiteSpace(friend?.virtualFriendId))
            return $"virtual:{friend.virtualFriendId.Trim()}";

        return $"virtual:{friend?.id.ToString() ?? "created_friend"}";
    }

    private static string GetCurrentUserName()
    {
        return UserDataManager.Instance != null && !string.IsNullOrWhiteSpace(UserDataManager.Instance.UserName)
            ? UserDataManager.Instance.UserName
            : "我";
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

    private static List<RelationshipDivinationCard> DrawCards()
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
}
