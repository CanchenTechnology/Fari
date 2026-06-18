using System;
using System.Collections.Generic;
using GamerFrameWork.OracleRuntime;
using UnityEngine;

public static class DivinationRecordBuilder
{
    public static DivinationRecordData FromChatMessage(ChatMessageData message, SpreadDefinition spreadDef = null)
    {
        if (message == null) return null;

        string spreadKind = FirstNonEmpty(message.spreadKind, spreadDef?.kind, "self_repair");
        if (spreadDef == null && DivinationEngine.Instance != null)
            spreadDef = DivinationEngine.Instance.GetSpreadDefinition(spreadKind);

        var lockedCards = BuildLockedCards(message.spreadDrawnCards, spreadDef);

        return new DivinationRecordData
        {
            readingId = FirstNonEmpty(message.readingId, $"chat_{message.id}"),
            question = FirstNonEmpty(message.divinationQuestion, message.content),
            scene = FirstNonEmpty(message.divinationScene, "relationship_anxiety"),
            spreadKind = spreadKind,
            lockedCards = lockedCards,
            shortVerdict = message.shortVerdict ?? "",
            judgeContent = message.judgeContent ?? "",
            adviceContent = message.adviceContent ?? "",
            topics = message.followupTopics != null
                ? new List<string>(message.followupTopics)
                : new List<string>(),
            oracleId = RoleManager.Instance != null
                ? RoleManager.Instance.characterType.ToString()
                : "tarot",
            createdAt = FirstNonEmpty(message.divinationCreatedAt, DateTime.Now.ToString("o"))
        };
    }

    public static DivinationRecordData FromSession()
    {
        var session = DivinationEngine.Instance?.CurrentSession;
        if (session == null) return null;

        return new DivinationRecordData
        {
            readingId = session.readingId,
            question = session.question,
            scene = session.scene,
            spreadKind = session.spreadKind,
            lockedCards = session.lockedCards ?? new List<LockedCard>(),
            shortVerdict = session.shortVerdict ?? "",
            judgeContent = session.judgeContent ?? "",
            adviceContent = session.adviceContent ?? "",
            topics = session.topics ?? new List<string>(),
            oracleId = RoleManager.Instance != null
                ? RoleManager.Instance.characterType.ToString()
                : "tarot",
            createdAt = string.IsNullOrEmpty(session.createdAt)
                ? DateTime.Now.ToString("o")
                : session.createdAt
        };
    }

    private static List<LockedCard> BuildLockedCards(List<TarotDrawData> draws, SpreadDefinition spreadDef)
    {
        var lockedCards = new List<LockedCard>();
        if (draws == null) return lockedCards;

        for (int i = 0; i < draws.Count; i++)
        {
            var draw = draws[i];
            var card = TarotDeck.GetById(draw.cardId);
            if (card == null)
            {
                Debug.LogWarning($"[DivinationRecordBuilder] 未找到塔罗牌数据: {draw.cardId}");
                continue;
            }

            string positionKey = spreadDef?.positions != null && i < spreadDef.positions.Count
                ? spreadDef.positions[i].key
                : $"pos_{i + 1}";
            string positionLabel = spreadDef?.positions != null && i < spreadDef.positions.Count
                ? spreadDef.positions[i].label
                : $"第{i + 1}张";

            lockedCards.Add(new LockedCard
            {
                positionKey = positionKey,
                position = positionLabel,
                cardId = card.cardId,
                cardName = card.nameZh,
                orientation = draw.upright ? "upright" : "reversed"
            });
        }

        return lockedCards;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null) return "";
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return "";
    }
}
