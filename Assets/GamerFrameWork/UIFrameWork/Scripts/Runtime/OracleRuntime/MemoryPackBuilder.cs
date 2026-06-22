using System.Collections.Generic;
using System.Linq;

namespace GamerFrameWork.OracleRuntime
{
    /// <summary>
    /// 记忆打包器 —— 从用户记忆源中提取与当前场景最相关的记忆
    /// 对应 runtime-reference context/memoryPackBuilder.mjs
    /// </summary>
    public static class MemoryPackBuilder
    {
        /// <summary>
        /// 构建记忆包 —— 只注入3-5条最相关的精炼记忆，不做全量注入
        /// </summary>
        public static MemoryPack Build(MemoryPackQuery query, MemorySource source)
        {
            var q = NormalizeQuery(query);
            var src = NormalizeSource(source);
            var maxItems = q.maxItems;

            return new MemoryPack
            {
                stableProfile = PickStableProfile(src.stableProfile, maxItems),
                relationshipScope = PickRelationshipScope(src.relationships, q.activeRelationshipId, maxItems),
                readingContinuity = PickReadingContinuity(src.readingContinuity, q, maxItems),
                candidates = PickCandidates(src.candidates, maxItems),
                tomorrowHooks = q.includeTomorrowHook
                    ? PickTomorrowHooks(src.tomorrowHooks, q, maxItems)
                    : new List<string>()
            };
        }

        public static List<string> ToPromptLines(MemoryPack pack)
        {
            var items = Flatten(pack, 5);
            if (items.Count == 0)
                return new List<string> { "No relevant memory selected." };
            return items.Select(item => $"- {item}").ToList();
        }

        private static List<string> Flatten(MemoryPack pack, int maxItems)
        {
            pack ??= new MemoryPack();
            var items = new List<string>();

            AddItems(items, pack.relationshipScope, maxItems);
            AddItems(items, pack.readingContinuity, maxItems);
            AddItems(items, pack.tomorrowHooks, maxItems);
            AddItems(items, pack.stableProfile, maxItems);
            AddItems(items, pack.candidates, maxItems);

            return items
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct()
                .Take(maxItems)
                .ToList();
        }

        private static void AddItems(List<string> target, List<string> source, int maxItems)
        {
            if (target.Count >= maxItems || source == null) return;
            foreach (string item in source)
            {
                if (target.Count >= maxItems) break;
                if (!string.IsNullOrWhiteSpace(item))
                    target.Add(item);
            }
        }

        private static MemoryPackQuery NormalizeQuery(MemoryPackQuery query)
        {
            return new MemoryPackQuery
            {
                userId = query.userId ?? "demo_user",
                scene = query.scene ?? "general_chat",
                activeRelationshipId = query.activeRelationshipId,
                activeReadingId = query.activeReadingId,
                includeTomorrowHook = query.includeTomorrowHook,
                maxItems = System.Math.Clamp(query.maxItems, 1, 8)
            };
        }

        private static MemorySource NormalizeSource(MemorySource source)
        {
            return source ?? new MemorySource();
        }

        private static List<string> PickStableProfile(StableProfile profile, int maxItems)
        {
            var lines = new List<string>();
            if (profile == null) return lines;

            if (!string.IsNullOrEmpty(profile.preferredName))
                lines.Add($"Preferred name: {profile.preferredName}");
            if (!string.IsNullOrEmpty(profile.preferredTone))
                lines.Add($"Preferred tone: {profile.preferredTone}");
            if (profile.recurringThemes != null)
                foreach (var t in profile.recurringThemes)
                    lines.Add($"Recurring theme: {t}");
            if (profile.doNotSay != null)
                foreach (var d in profile.doNotSay)
                    lines.Add($"Do not say: {d}");
            if (profile.safetyNotes != null)
                foreach (var s in profile.safetyNotes)
                    lines.Add($"Safety note: {s}");

            return lines.Take(maxItems).ToList();
        }

        private static List<string> PickRelationshipScope(List<RelationshipMemory> relationships,
            string activeRelationshipId, int maxItems)
        {
            if (string.IsNullOrEmpty(activeRelationshipId)) return new List<string>();
            var rel = relationships?.FirstOrDefault(r => r.relationshipId == activeRelationshipId);
            if (rel == null) return new List<string>();

            var lines = new List<string>
            {
                $"Relationship object: {rel.displayName}",
                $"Consent mode: {rel.consentMode}",
                $"Entity type: {rel.entityType}"
            };

            if (rel.knownFacts != null)
                foreach (var f in rel.knownFacts)
                    lines.Add($"Known user-scoped fact: {f}");
            if (rel.openLoops != null)
                foreach (var l in rel.openLoops)
                    lines.Add($"Open loop: {l}");
            if (!string.IsNullOrEmpty(rel.lastActionAdvice))
                lines.Add($"Last action advice: {rel.lastActionAdvice}");

            return lines.Take(maxItems + 3).ToList();
        }

        private static List<string> PickReadingContinuity(List<ReadingContinuityEntry> readings,
            MemoryPackQuery query, int maxItems)
        {
            var filtered = readings?
                .Where(r =>
                {
                    if (!string.IsNullOrEmpty(query.activeReadingId) && r.readingId != query.activeReadingId)
                        return false;
                    if (!string.IsNullOrEmpty(query.activeRelationshipId) &&
                        !string.IsNullOrEmpty(r.relationshipId) &&
                        r.relationshipId != query.activeRelationshipId)
                        return false;
                    return true;
                })
                .OrderByDescending(r => r.createdAt)
                .Take(maxItems)
                .ToList() ?? new List<ReadingContinuityEntry>();

            return filtered.Select(r =>
            {
                var cards = r.cards != null
                    ? string.Join(", ", r.cards.Select(c =>
                        $"{c.position ?? c.positionName}:{c.cardName ?? c.cardId}{(string.IsNullOrEmpty(c.orientation) ? "" : $"({c.orientation})")}"))
                    : "";
                return $"Reading {r.readingId}: {r.shortVerdict ?? r.question ?? "continuity"}{(string.IsNullOrEmpty(cards) ? "" : $" [{cards}]")}";
            }).ToList();
        }

        private static List<string> PickCandidates(List<MemoryCandidate> candidates, int maxItems)
        {
            return candidates?
                .Where(c => c != null && IsCandidatePromoted(c.status))
                .OrderByDescending(c => c.confidence)
                .Take(maxItems)
                .Select(c => $"Candidate {c.type}: {c.text}")
                .ToList() ?? new List<string>();
        }

        private static bool IsCandidatePromoted(string status)
        {
            var value = (status ?? "").ToLowerInvariant();
            return string.IsNullOrEmpty(value) || value == "promoted" || value == "accepted";
        }

        private static List<string> PickTomorrowHooks(List<TomorrowHook> hooks,
            MemoryPackQuery query, int maxItems)
        {
            return hooks?
                .Where(h => h.status == "pending")
                .Where(h => string.IsNullOrEmpty(query.activeRelationshipId) ||
                            string.IsNullOrEmpty(h.relationshipId) ||
                            h.relationshipId == query.activeRelationshipId)
                .Take(maxItems)
                .Select(h => $"Tomorrow hook {h.hookType}: {h.triggerText}")
                .ToList() ?? new List<string>();
        }
    }

    // ============ 辅助数据类型 ============

    public class MemorySource
    {
        public StableProfile stableProfile = new StableProfile();
        public List<RelationshipMemory> relationships = new List<RelationshipMemory>();
        public List<ReadingContinuityEntry> readingContinuity = new List<ReadingContinuityEntry>();
        public List<MemoryCandidate> candidates = new List<MemoryCandidate>();
        public List<TomorrowHook> tomorrowHooks = new List<TomorrowHook>();
    }

    public class StableProfile
    {
        public string preferredName;
        public string preferredTone;
        public List<string> recurringThemes;
        public List<string> doNotSay;
        public List<string> safetyNotes;
    }

    public class ReadingContinuityEntry
    {
        public string readingId;
        public string question;
        public string shortVerdict;
        public string relationshipId;
        public List<ReadingCardEntry> cards;
        public string createdAt;
    }

    public class ReadingCardEntry
    {
        public string position;
        public string positionName;
        public string cardId;
        public string cardName;
        public string orientation;
    }

}
