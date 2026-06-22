using System;
using System.Collections.Generic;
using System.Linq;

namespace GamerFrameWork.OracleRuntime
{
    /// <summary>
    /// Oracle Runtime 类型系统 —— 对应 runtime-reference types.mjs
    /// </summary>
    public static class OracleTypes
    {
        public static readonly string[] ORACLE_SCENES =
        {
            "relationship_anxiety", "daily_reflection", "self_worth",
            "breakup_relapse", "friendship", "ambiguous_situation",
            "career_uncertainty", "crisis", "general_chat"
        };

        public static readonly string[] ORACLE_STAGES =
        {
            "listen", "clarify", "before_draw", "card_reveal",
            "verdict", "micro_action", "follow_up", "recall", "dive_deeper"
        };

        public static readonly string[] RESPONSE_MODES =
        {
            "micro_chat", "ritual_prompt", "card_reveal",
            "oracle_verdict", "dive_deeper", "follow_up"
        };

        public static readonly string[] ORACLE_VOICE_IDS = { "tarot_reader", "sage", "astrologer" };

        public static readonly string[] THERAPY_LENSES =
        {
            "reflective_listening", "motivational_interviewing", "cbt_reframe",
            "act_defusion", "values_action", "trauma_informed_safety",
            "narrative_externalization", "parts_language"
        };

        public static readonly string[] SPREAD_KINDS =
        {
            "mirror_card", "relationship_tension", "choice_gate",
            "pattern_map", "timeline_thread", "self_repair",
            "celtic_inspired_deep_cross"
        };

        public static readonly string[] SPREAD_COMPLEXITIES = { "light", "standard", "deep" };

        public static readonly string[] RISK_FLAGS =
        {
            "self_harm", "medical", "legal",
            "minor_safety", "privacy_sensitive", "third_party_claim"
        };

        public static readonly string[] ORACLE_ACTION_KINDS =
        {
            "draw_one_card", "start_three_card", "ask_about_person",
            "save_tomorrow_hook", "dive_deeper", "play_voice_summary",
            "share_result_card", "continue_chat", "plan_spread",
            "start_spread", "reveal_card"
        };

        public static readonly string[] READING_STATES =
        {
            "planned", "starting", "cards_locked", "revealing",
            "generating_verdict", "fallback_verdict", "completed", "error"
        };

        public static readonly string[] MEMORY_CANDIDATE_TYPES =
        {
            "preference", "relationship_fact", "recurring_theme", "boundary", "open_loop"
        };

        public static void AssertOneOf(string value, string[] allowed, string label)
        {
            if (!allowed.Contains(value))
                throw new ArgumentException($"{label} must be one of: {string.Join(", ", allowed)}");
        }
    }

    // ============ 数据模型 ============

    [Serializable]
    public class RuntimePlan
    {
        public string turnId;
        public string scene;
        public List<string> sceneEvidence;
        public string stage;
        public string stageReason;
        public string locale = "zh-CN";
        public string responseMode;
        public string oracleVoiceId = "tarot_reader";
        public bool shouldInviteCardDraw;
        public bool shouldCreateTomorrowHook;
        public List<OracleAction> suggestedActions;
        public MemoryPackQuery memoryQuery;
        public List<string> therapyLens;
        public DivinationPlan divinationPlan;
        public List<string> riskFlags;
        public string riskLevel;
    }

    [Serializable]
    public class PersonaContract
    {
        public string oracleVoiceId;
        public string displayName;
        public List<string> tone;
        public List<string> values;
        public List<string> boundaries;
        public List<string> forbidden;
        public List<string> sampleLines;
    }

    [Serializable]
    public class ResponseContract
    {
        public string stage;
        public string responseMode;
        public int maxSentences;
        public int maxWords;
        public List<string> mustDo;
        public List<string> mustNot;
        public string nextActionPolicy; // "none" | "one" | "chips"
    }

    [Serializable]
    public class DivinationPlan
    {
        public string planId;
        public string userId;
        public string conversationId;
        public string question;
        public string scene;
        public string spreadKind;
        public int cardCount;
        public string complexity;
        public List<SpreadPosition> positions;
        public string reasonForSpread;
        public List<string> professionalFrame;
        public bool requiresProForFullReading;
        public string createdAt;
    }

    [Serializable]
    public class SpreadPosition
    {
        public string key;
        public string label;
        public string prompt;
        public string therapeuticRole;
        public string tarotTheoryAxis;
        public string consentBoundary;
    }

    [Serializable]
    public class LockedCard
    {
        public string position;
        public string positionKey;
        public string cardId;
        public string cardName;
        public string orientation; // "upright" | "reversed"
    }

    [Serializable]
    public class ReadingLock
    {
        public string readingId;
        public string readingType;
        public List<LockedCard> allowedCards;
        public bool locked;
    }

    [Serializable]
    public class ReadingLockContext
    {
        public string readingId;
        public string readingType;
        public bool locked;
        public List<LockedCard> allowedCards;
        public List<string> lines;
    }

    [Serializable]
    public class MemoryPackQuery
    {
        public string userId;
        public string scene;
        public string activeRelationshipId;
        public string activeReadingId;
        public bool includeTomorrowHook;
        public int maxItems = 4;
    }

    [Serializable]
    public class MemoryPack
    {
        public List<string> stableProfile = new List<string>();
        public List<string> relationshipScope = new List<string>();
        public List<string> readingContinuity = new List<string>();
        public List<string> candidates = new List<string>();
        public List<string> tomorrowHooks = new List<string>();
    }

    [Serializable]
    public class MemoryCandidate
    {
        public string id;
        public string userId;
        public string type;
        public string text;
        public string status; // "pending" | "promoted" | "dismissed"
        public float confidence;
        public string relationshipId;
        public string sourceConversationId;
        public string sourceMessageId;
        public string createdAt;
        public bool important;
    }

    [Serializable]
    public class RelationshipMemory
    {
        public string relationshipId;
        public string displayName;
        public string entityType;
        public string consentMode;
        public List<string> knownFacts;
        public List<string> openLoops;
        public string lastActionAdvice;
        public List<string> lastReadingIds;
        public int mentionCount30d;
        public string lastTouchedAt;
    }

    [Serializable]
    public class TomorrowHook
    {
        public string hookId;
        public string userId;
        public string relationshipId;
        public string sourceReadingId;
        public string sourceConversationId;
        public string hookType;
        public string triggerText;
        public string scheduledForLocalDate;
        public string status; // "pending" | "sent" | "opened" | "dismissed"
    }

    [Serializable]
    public class OracleAction
    {
        public string id;
        public string label;
        public string kind;
        public Dictionary<string, object> payload;
    }

    [Serializable]
    public class TherapyStrategy
    {
        public string stage;
        public List<string> therapyLens;
        public List<string> mustDo;
        public List<string> mustAvoid;
        public List<string> outputMoves;
    }

    [Serializable]
    public class OracleContext
    {
        public string assembledAt;
        public RuntimePlan runtimePlan;
        public List<OracleSection> sections;
        public PersonaContract personaContract;
        public ResponseContract responseContract;
        public TherapyStrategy therapyStrategy;
        public MemoryPack memoryPack;
        public ReadingLockContext readingLock;
        public OutputContract outputContract;
        public Dictionary<string, object> userPayload;
    }

    [Serializable]
    public class OracleSection
    {
        public string title;
        public List<string> lines;
    }

    [Serializable]
    public class OutputContract
    {
        public string stage;
        public string responseMode;
        public List<string> lines;
    }

    /// <summary>
    /// 完整解读 Payload —— 包含 AI 生成的描述、标签、牌义、行动建议和话题列表
    /// 用于 CompleteInterpretationUI 展示
    /// </summary>
    [Serializable]
    public class CompleteInterpretationPayload
    {
        /// <summary>卡片描述 (AI 生成，叙事化)</summary>
        public string description;

        /// <summary>3 个情感/能量标签</summary>
        public List<string> tags;

        /// <summary>牌义解析描述</summary>
        public string meaningAnalysis;

        /// <summary>今日可以做的行为建议</summary>
        public string actionSuggestion;

        /// <summary>4 个适合继续聊的话题</summary>
        public List<string> topics;
    }
}
