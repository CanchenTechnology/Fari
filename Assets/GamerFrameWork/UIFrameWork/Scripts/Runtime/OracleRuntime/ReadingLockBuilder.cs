using System.Collections.Generic;
using System.Linq;

namespace GamerFrameWork.OracleRuntime
{
    /// <summary>
    /// 阅读锁定构建器 —— 确保占卜期间只能引用已锁定的牌
    /// 对应 runtime-reference context/readingLockBuilder.mjs
    /// </summary>
    public static class ReadingLockBuilder
    {
        /// <summary>
        /// 构建阅读锁上下文
        /// </summary>
        public static ReadingLockContext Build(ReadingLock readingLock)
        {
            if (readingLock == null || !readingLock.locked)
            {
                return new ReadingLockContext
                {
                    locked = false,
                    allowedCards = new List<LockedCard>(),
                    lines = new List<string> { "No active card lock. Do not invent card names." }
                };
            }

            var allowedCards = (readingLock.allowedCards ?? new List<LockedCard>())
                .Select((card, index) => new LockedCard
                {
                    position = card.position,
                    positionKey = card.positionKey,
                    cardId = card.cardId,
                    cardName = card.cardName ?? card.cardId,
                    orientation = card.orientation
                }).ToList();

            var lines = new List<string>
            {
                $"Reading lock: {readingLock.readingId} ({readingLock.readingType})",
                $"Allowed cards only: {string.Join(" | ", allowedCards.Select(c => $"{c.position}:{c.cardName} {c.orientation}"))}",
                "Never mention, reveal, or interpret cards outside this allowed list."
            };

            return new ReadingLockContext
            {
                readingId = readingLock.readingId,
                readingType = readingLock.readingType,
                locked = true,
                allowedCards = allowedCards,
                lines = lines
            };
        }

        /// <summary>
        /// 检查是否在非锁定状态下试图提及牌名
        /// </summary>
        public static bool MentionsUnlockedCard(string text, ReadingLock readingLock, List<string> deckCardNames)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (readingLock == null || !readingLock.locked) return false;

            var value = text.ToLowerInvariant();
            var allowedNames = new HashSet<string>();
            foreach (var card in readingLock.allowedCards ?? new List<LockedCard>())
            {
                if (!string.IsNullOrEmpty(card.cardName)) allowedNames.Add(card.cardName.ToLowerInvariant());
                if (!string.IsNullOrEmpty(card.cardId)) allowedNames.Add(card.cardId.ToLowerInvariant());
            }

            return (deckCardNames ?? new List<string>())
                .Any(name => !string.IsNullOrEmpty(name) && name.Length > 2 &&
                             value.Contains(name.ToLowerInvariant()) &&
                             !allowedNames.Contains(name.ToLowerInvariant()));
        }
    }
}
