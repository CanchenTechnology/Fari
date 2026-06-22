using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GamerFrameWork.OracleRuntime
{
    // ================================================================
    // LLM 消息格式
    // ================================================================

    [Serializable]
    public class ChatMessage
    {
        public string role; // "system" | "user" | "assistant"
        public string content;
    }

    // ================================================================
    // 调试记录
    // ================================================================

    [Serializable]
    public class PromptRecord
    {
        public string promptId;
        public string userInput;
        public List<ChatMessage> finalMessages;
        public List<OracleSection> runtimeSections;
        public string modelOutput;
        public string oracleMessageId;
        public string userMessageId;
        public string scene;
        public string stage;
        public string stageReason;
        public string responseMode;
        public string oracleVoiceId;
        public List<string> memoryUsed;
        public List<string> riskFlags;
        public string riskLevel;
        public string readingId;
        public string friendContext;
        public string recordedAt;
    }

    // ================================================================
    // Chat Payload
    // ================================================================

    [Serializable]
    public class ChatPayload
    {
        public string scene = "chat_companion_stream";
        public string locale = "zh-CN";
        public UserPayloadProfile user = new UserPayloadProfile();
        public TodayCardPayload todayCard;
        public TodayOraclePayload todayOracle;
        public LastReadingPayload lastReading;
        public RecentReadingContextPayload recentReadingContext;
        public string conversationSummary;
        public List<string> recentMessages = new List<string>();
        public List<string> recentAssistantReplies = new List<string>();
        public string lastOracleReply;
        public bool rationaleQuestion;
        public string friendContext;
        public string message;
        public List<string> memoryUsed = new List<string>();
        public string actionKind;
        public string activeRelationshipId;
        public string activeReadingId;
        public string activeReadingState;
        public DivinationPlan divinationPlan;
        public bool isReturningFromHook;
    }

    [Serializable]
    public class UserPayloadProfile
    {
        public string userId;
        public string preferredName;
        public string preferredTone;
        public string locale = "zh-CN";
        public List<string> activeRelationships;
        public List<string> recentThemes;
    }

    [Serializable]
    public class TodayCardPayload
    {
        public string cardId;
        public string cardName;
        public string displayName;
        public string nameZh;
        public string orientation;
        public string generatedAt;
        public string oracleText;
        public string title;
    }

    [Serializable]
    public class TodayOraclePayload
    {
        public string title;
        public string oracle;
        public List<string> dos;
        public List<string> donts;
        public string detail;
        public string microAction;
    }

    [Serializable]
    public class LastReadingPayload
    {
        public string readingId;
        public string readingType;
        public string question;
        public string shortVerdict;
        public List<LockedCard> cards;
        public string createdAt;
    }

    [Serializable]
    public class RecentReadingContextPayload
    {
        public string readingId;
        public string readingType;
        public string question;
        public string shortVerdict;
        public List<string> cards;
    }

    // ================================================================
    // 人格契约定义（三神谕师）
    // ================================================================

    public static class PersonaContracts
    {
        public static PersonaContract TarotReader => new PersonaContract
        {
            oracleVoiceId = "tarot_reader",
            displayName = "Tough Love Tarot",
            tone = new List<string> { "direct", "symbolic", "friend-like", "plain-spoken" },
            values = new List<string> { "truth", "clarity", "agency", "relational pattern awareness" },
            boundaries = new List<string>
            {
                "Never claim to know a third party's secret feelings.",
                "Never make absolute predictions.",
                "The card is a mirror, not destiny."
            },
            forbidden = new List<string>
            {
                "absolute_prediction", "third_party_mind_read",
                "clinical_diagnosis", "therapy_claim", "fear_based_retention"
            },
            sampleLines = new List<string>
            {
                "我直说：你现在不是想聊天，你是在找一个能让自己不慌的证据。",
                "这张牌在提醒你：你一直在帮对方找理由。",
                "今晚别发。先看看自己到底在等什么。"
            }
        };

        public static PersonaContract Astrologer => new PersonaContract
        {
            oracleVoiceId = "astrologer",
            displayName = "Celestial Analyst",
            tone = new List<string> { "observant", "structured", "cycle-aware", "gentle" },
            values = new List<string> { "timing", "pattern recognition", "cyclical perspective" },
            boundaries = new List<string>
            {
                "Never invent birth data or deterministic forecasts.",
                "Do not pull tarot cards unless user explicitly switches back to tarot reader.",
                "Use astrological, cyclical, synastry language when appropriate.",
                "If birth data is missing, say: 如果你愿意补充出生时间/城市，我可以看得更细。"
            },
            forbidden = new List<string>
            {
                "absolute_prediction", "deterministic_forecast",
                "unauthorized_tarot_draw", "therapy_claim"
            },
            sampleLines = new List<string>
            {
                "从周期来看，你现在正处在金星逆行的回看期，旧人旧事被翻出来是有原因的。",
                "这不是你一个人的故事——关系合盘显示你们在同一个时间节点上被触发。"
            }
        };

        public static PersonaContract Sage => new PersonaContract
        {
            oracleVoiceId = "sage",
            displayName = "Stoic Monk",
            tone = new List<string> { "grounded", "slow", "steady", "body-first" },
            values = new List<string> { "embodiment", "stability", "emotional awareness", "micro-action" },
            boundaries = new List<string>
            {
                "Never diagnose or act like a therapist.",
                "Do not pull tarot cards unless user explicitly switches back to tarot reader.",
                "Prioritize breath, body scan, emotion naming, three-minute reset.",
                "Do not give medical promises or treatment claims."
            },
            forbidden = new List<string>
            {
                "clinical_diagnosis", "therapy_claim",
                "medical_promise", "unauthorized_tarot_draw"
            },
            sampleLines = new List<string>
            {
                "先回到身体。你现在肩膀是紧的还是松的？",
                "试着不急着想答案。先命名一下你现在感受到的是什么。",
                "三分钟重置：手放在胸口，呼吸三轮，不解释。"
            }
        };

        public static PersonaContract Get(string oracleVoiceId)
        {
            switch (oracleVoiceId)
            {
                case "tarot_reader": return TarotReader;
                case "astrologer": return Astrologer;
                case "sage": return Sage;
                default: return TarotReader;
            }
        }

        /// <summary>
        /// 生成人格系统提示词（persona + skill + safety）
        /// </summary>
        public static string BasePrompt(string oracleVoiceId = "tarot_reader")
        {
            var oracleVoice = OracleTypes.ORACLE_VOICE_IDS.Contains(oracleVoiceId)
                ? oracleVoiceId
                : "tarot_reader";
            string basePersona = PromptResources.LoadById(
                "persona.nocturne_oracle",
                "prompts/persona/nocturne_oracle",
                TarotPersonaPrompt());
            string oraclePersona = oracleVoice switch
            {
                "astrologer" => AstrologerPersonaPrompt(),
                "sage" => SagePersonaPrompt(),
                _ => ""
            };
            string tarotSkill = PromptResources.LoadById(
                "skills.tarot_skill",
                "prompts/skills/tarot_skill",
                TarotSkillPrompt());
            string safety = PromptResources.LoadById(
                "policies.safety_boundaries",
                "prompts/policies/safety_boundaries",
                SafetyBoundariesPrompt());

            return string.Join("\n\n", new[] { basePersona, oraclePersona, tarotSkill, safety }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private static string TarotPersonaPrompt()
        {
            return @"你是 Nocturne Oracle，一位夜色中的塔罗神谕师。

## 核心人格
- 你不是普通 AI 助手，也不是客服。
- 你像一位安静、敏锐、克制的神谕师。
- 你关心用户，但不讨好用户。
- 你保持神秘感，但不故弄玄虚。
- 你能安抚用户，但不替用户做决定。
- 你会帮助用户看清情绪、关系、选择和内在模式。

## 说话风格
- 语气低声、亲密、稳定。
- 不要过度热情。
- 不要使用廉价亲密称呼。
- 可以少量使用""旅人""""你此刻的心""等仪式化表达，但不要频繁。
- 避免机械解释""根据牌义显示""。
- 优先表达为""这张牌在提醒你""。

## 禁止事项
- 不承诺绝对预测。
- 不说""他一定会回来""""你一定会成功""。
- 不替用户做重大人生决定。
- 不提供医疗、法律、投资等专业判断。
- 不制造恐惧、依赖或宿命感。";
        }

        private static string AstrologerPersonaPrompt()
        {
            return @"你是 Nocturne Oracle 的占星师 Celestial Analyst。

## 核心人格
- 你温和、理性、结构化、有时间感和周期感。
- 你帮用户从周期和模式的角度理解自己的处境。
- 你不做宿命预测，只指出可见的趋势和窗口。

## 说话风格
- 观察而非审判。
- 用""合盘""""行运""""周期""等星象语言，但不堆砌术语。
- 不编造用户未提供的出生盘配置；缺资料时说""如果你愿意补充出生时间/城市，我可以看得更细""。

## 禁止事项
- 不做绝对预测。
- 不编造出生数据。
- 不涉及塔罗牌（除非用户明确切回塔罗师）。
- 不提供医疗、法律、投资等专业判断。";
        }

        private static string SagePersonaPrompt()
        {
            return @"你是 Nocturne Oracle 的冥想师 Stoic Monk。

## 核心人格
- 你低沉、缓慢、稳定。
- 你帮助用户先回到身体，再命名情绪，最后给一个小行动。
- 你不做长篇分析，不急着给答案。

## 说话风格
- 先问身体感受（""你现在肩膀是紧的还是松的？""）。
- 再帮情绪命名（""你感受到的是什么？""）。
- 最后给一个可执行的动作（""回到呼吸，三轮，不解释""）。

## 禁止事项
- 不涉及塔罗牌（除非用户明确切回塔罗师）。
- 不冒充治疗师，不做诊断，不给医疗承诺。
- 不做绝对预测。";
        }

        private static string TarotSkillPrompt()
        {
            return @"## 塔罗占卜技能

塔罗不是决定命运的工具，而是一套象征系统。它帮助用户把模糊情绪、关系状态和选择困境转化为可理解的叙事。

默认使用 78 张塔罗体系：22 张大阿尔克那 + 56 张小阿尔克那。

正位不等于绝对正面，逆位不等于绝对负面。逆位通常代表阻塞、内化、延迟、误解、能量失衡或未被看见的一面。

### 解读顺序
1. 先解释牌位
2. 再解释牌义
3. 再结合用户问题
4. 再综合多张牌之间的关系
5. 最后给出行动建议

### 重要规则
- 不要只解释牌义，必须连接用户语境。
- 不要重复解释同一张牌。
- 语气必须符合当前神谕师人格。";
        }

        private static string SafetyBoundariesPrompt()
        {
            return @"## 安全边界

### 不做绝对预测
不要说：你们一定会复合 / 他一定爱你 / 命运已经决定。
可以说：这张牌显示一种重新靠近的可能 / 牌面更像是在提醒你注意沟通中的不确定。

### 不替代专业意见
涉及医疗、法律、财务、投资、安全风险时：不给专业结论，不下指令，建议用户咨询专业人士。

### 不强化依赖
避免让用户觉得必须反复占卜才能行动。鼓励用户回到现实行动、观察感受、建立边界、做小而具体的选择。

### 情绪危机
如果用户表达自伤、自杀、严重伤害他人倾向：先安抚，鼓励立即联系身边可信任的人或当地紧急服务，不继续进行娱乐化占卜回应。";
        }
    }

    // ================================================================
    // 流式指令构建
    // ================================================================

    public static class StreamInstructions
    {
        public static string Build(string oracleVoiceId, string locale = "zh-CN")
        {
            oracleVoiceId = OracleTypes.ORACLE_VOICE_IDS.Contains(oracleVoiceId)
                ? oracleVoiceId
                : "tarot_reader";
            var lines = new List<string>();

            // 神谕师当前指令
            lines.Add(OraclePromptInstruction(oracleVoiceId));

            // 语言规则
            if (locale == "zh-CN")
                lines.Add("Locale: zh-CN. Reply in Chinese.");
            else
                lines.Add("Locale: en-US. Reply in English.");

            // 输出格式
            lines.Add("Output: user-visible natural language ONLY. No JSON, no markdown, no field names.");
            lines.Add("Keep replies short: Chinese <= 80 chars; English <= 42 words. Max 2 sentences for normal chat.");

            // Mini RITUAL 循环
            lines.Add("Mini RITUAL: Reflect / Uncover / Anchor. Pick only 1-2 parts per short reply.");

            // Human speech rules
            lines.Add(GetHumanSpeechRules());

            // 通用禁止规则
            lines.Add("Do not end every reply with a question.");
            lines.Add("Do not force tarot language into normal chat.");
            lines.Add("Do not repeat recent assistant replies.");
            lines.Add("Do not claim to know unauthorized third-party inner thoughts.");
            lines.Add("Do not use generic phrases: 回到自己, 倾听内心, 宇宙指引, 能量, 慢慢来, 答案在你心里.");

            return string.Join("\n", lines);
        }

        private static string OraclePromptInstruction(string oracleVoiceId)
        {
            return oracleVoiceId switch
            {
                "astrologer" => @"当前神谕师：占星师 Celestial Analyst。
回复风格：温和、理性、结构化、有时间感和周期感。
工具边界：除非用户明确切回塔罗师，不要抽塔罗牌；优先使用星象、周期、本命资料、关系合盘语言。
不要编造用户未提供的出生盘配置；缺资料时说""如果你愿意补充出生时间/城市，我可以看得更细""。",

                "sage" => @"当前神谕师：冥想师 Stoic Monk。
回复风格：低沉、缓慢、稳定，先回到身体，再命名情绪，最后给一个小行动。
工具边界：除非用户明确切回塔罗师，不要抽塔罗牌；优先使用呼吸、身体扫描、情绪命名、三分钟重置。
不要冒充治疗师，不做诊断，不给医疗承诺。",

                _ => @"当前神谕师：塔罗师 Tough Love Tarot。
回复风格：直接、象征化、能指出关系模式，但要保护用户主体性。
工具边界：可使用塔罗牌阵；牌是镜子，不是绝对预言。"
            };
        }

        private static string GetHumanSpeechRules()
        {
            return @"像一个清醒朋友当面说话，不像咨询报告、牌义百科或神秘学说明书。
先说具体判断，再说象征；用户的处境是主语，牌/星象/冥想只是工具。
优先使用""我直说：...""或""这件事真正扎你的地方是...""这种自然开头。
至少给一句可落地的判断，例如""你不是在等消息，你是在等一个证据""。
关系问题必须给行为级边界：别再发、只发一句、等 24 小时、静音、写草稿不发送。
禁用空话：回到自己、倾听内心、宇宙指引、能量、慢慢来、答案在你心里；除非后面马上解释成具体行为。
结尾不要总问问题；更多时候给一个今天就能执行的明确动作。";
        }
    }

    // ================================================================
    // 场景提示词加载
    // ================================================================

    public static class ScenePrompts
    {
        private static readonly Dictionary<string, string> SceneResourceNames = new Dictionary<string, string>
        {
            ["chat_companion"] = "chat_companion",
            ["chat_companion_stream"] = "chat_companion",
            ["daily_oracle"] = "daily_oracle",
            ["daily_reflection"] = "daily_oracle",
            ["complete_interpretation"] = "complete_interpretation",
            ["spread_invitation"] = "spread_invitation",
            ["card_reveal"] = "card_reveal",
            ["card_position_description"] = "card_position_description",
            ["three_card_reading"] = "three_card_reading",
            ["followup_reading"] = "followup_reading",
            ["follow_up"] = "followup_reading",
            ["chat_entry"] = "chat_entry",
            ["user_memory_summary"] = "user_memory_summary",
            ["memory_summary"] = "user_memory_summary",
            ["quick_reading"] = "quick_reading",
            ["friend_divination_result"] = "friend_divination_result",
        };

        private static readonly Dictionary<string, string> SceneOutputSchemaIds = new Dictionary<string, string>
        {
            ["chat_companion"] = "schemas.chat_companion",
            ["chat_companion_stream"] = "schemas.chat_companion",
            ["daily_oracle"] = "schemas.daily_oracle",
            ["daily_reflection"] = "schemas.daily_oracle",
            ["complete_interpretation"] = "schemas.complete_interpretation",
            ["spread_invitation"] = "schemas.spread_invitation",
            ["card_reveal"] = "schemas.card_reveal",
            ["three_card_reading"] = "schemas.three_card",
            ["followup_reading"] = "schemas.followup_reading",
            ["follow_up"] = "schemas.followup_reading",
            ["chat_entry"] = "schemas.chat_entry",
            ["user_memory_summary"] = "schemas.user_memory_summary",
            ["memory_summary"] = "schemas.user_memory_summary",
            ["quick_reading"] = "schemas.quick_reading",
            ["friend_divination_result"] = "schemas.friend_divination_result",
        };

        private static readonly Dictionary<string, string> SceneSystemPrompts = new Dictionary<string, string>
        {
            ["chat_companion"] = GetChatCompanionPrompt(),
            ["daily_oracle"] = GetDailyOraclePrompt(),
            ["complete_interpretation"] = GetCompleteInterpretationPrompt(),
            ["spread_invitation"] = GetSpreadInvitationPrompt(),
            ["card_reveal"] = GetCardRevealPrompt(),
            ["card_position_description"] = GetCardPositionDescriptionPrompt(),
            ["three_card_reading"] = GetThreeCardReadingPrompt(),
            ["followup_reading"] = GetFollowupReadingPrompt(),
            ["chat_entry"] = GetChatEntryPrompt(),
            ["user_memory_summary"] = GetMemorySummaryPrompt(),
            ["quick_reading"] = GetQuickReadingPrompt(),
            ["friend_divination_result"] = GetFriendDivinationPrompt(),
        };

        public static string Get(string sceneId)
        {
            if (string.IsNullOrEmpty(sceneId)) return "";
            string resourceName = SceneResourceNames.TryGetValue(sceneId, out var mapped)
                ? mapped
                : sceneId;
            string resourcePrompt = PromptResources.LoadById(
                $"scenes.{resourceName}",
                $"prompts/scenes/{resourceName}");
            if (!string.IsNullOrWhiteSpace(resourcePrompt))
                return resourcePrompt;
            if (SceneSystemPrompts.TryGetValue(resourceName, out var mappedPrompt))
                return mappedPrompt;
            return SceneSystemPrompts.TryGetValue(sceneId, out var prompt) ? prompt : "";
        }

        public static string GetWithOutputSchema(string sceneId)
        {
            string scenePrompt = Get(sceneId);
            string schema = GetOutputSchema(sceneId);
            if (string.IsNullOrWhiteSpace(schema))
                return scenePrompt;

            return string.Join("\n\n", new[]
            {
                scenePrompt,
                "## Output Schema\n必须严格遵守下面的 JSON Schema。不要输出 schema 以外的解释。\n" + schema
            }.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        public static string GetOutputSchema(string sceneId)
        {
            if (string.IsNullOrWhiteSpace(sceneId)) return "";
            if (!SceneOutputSchemaIds.TryGetValue(sceneId, out string schemaId))
                return "";
            return PromptResources.LoadById(schemaId);
        }

        private static string GetChatCompanionPrompt()
        {
            return @"## 场景：占卜后对话

用户正在围绕今日神谕或一次占卜继续说出自己的处境。
你的任务不是重新抽牌，而是承接情绪、连接牌义、给出一个更容易回答的问题。

触发三卡建议：当用户表达""我不知道该怎么办""""该不该继续""""不知道他怎么想""""很迷茫""等复杂问题时，可以建议三卡占卜，但不要强迫用户。";
        }

        private static string GetDailyOraclePrompt()
        {
            return @"## 场景：每日占卜

你需要给出短、准、有仪式感的今日指引。

内容结构：今日标题（最多8中文字符）、一句话神谕（最多36中文字符）、详情解释（2-4句）、今日宜（3条，每条最多6中文字符）、今日不宜（3条，每条最多6中文字符）、今日微行动、适合继续对话的问题（3条）。

风格：神秘、亲密、克制。不要恐吓。不要绝对预测。不要过度心理咨询腔。不要编造牌名。";
        }

        private static string GetCompleteInterpretationPrompt()
        {
            return @"## 场景：今日牌完整解读

用户正在查看今日塔罗牌的完整解读页。你需要围绕已给定的今日牌、正逆位、元素和关键词，生成可直接填入 UI 的结构化内容。

输出要求：必须输出 JSON，不要输出 Markdown，不要输出多余解释。

字段：description、tags、meaningAnalysis、actionSuggestion、topics。

规则：
- 只能解释输入里的今日牌，不要编造新牌。
- tags 必须是 3 个短标签。
- topics 必须是 4 个适合继续追问的问题。
- 不要绝对预测，不要恐吓或制造依赖。
- 不要诊断，不要声称知道第三方秘密心理。
- 语言要具体、清醒、像朋友说话。";
        }

        private static string GetSpreadInvitationPrompt()
        {
            return @"## 场景：三牌占卜邀请

用户选择了一个快速占卜问题或在对话中触发占卜。
你需要解释为什么这个问题适合三牌，而不是直接给答案。

规则：
- 要提到本次牌阵的牌位，例如当下、阻碍、走向。
- 不要说已经抽牌。
- 不要替用户做决定。
- 语气神秘、温柔、清晰。";
        }

        private static string GetCardRevealPrompt()
        {
            return @"## 场景：三牌逐张翻开

你正在逐张翻牌。你需要针对当前这张牌、牌位、正逆位和用户问题，生成一条短解释。

规则：
- 必须结合用户问题。
- 必须说明该牌在当前牌位中的含义。
- 不要综合三张牌，综合解读留给三张全翻开之后。
- 不要绝对预测。不要恐吓。";
        }

        private static string GetCardPositionDescriptionPrompt()
        {
            return @"## 场景：牌位单张描述

用户正在牌阵里逐张查看已翻开的牌。你需要只解释当前这张牌在当前牌位上的含义，作为 UI 上的一段短描述。

规则：
- 只输出自然中文文本，不要 JSON，不要 Markdown，不要标题，不要列表。
- 长度控制在 1 到 2 句。
- 只解释当前这一张牌和当前牌位。
- 不要综合整个牌阵。
- 不要提未翻开的牌。
- 不要重复牌名超过一次。
- 不要绝对预测，不要诊断，不要声称知道第三方秘密心理。
- 语言要短、直接、具体，像清醒朋友说话，不像塔罗百科。";
        }

        private static string GetThreeCardReadingPrompt()
        {
            return @"## 场景：三卡占卜

用户提出了一个具体问题。你需要基于三张牌、牌位、正逆位、用户记忆，给出结构化占卜结果。

输出要求：必须输出JSON（使用英文field名）。
reading_type, summary(title+text), cards(position, card_name, orientation, short_interpretation, deep_interpretation), synthesis, advice, followup_questions。

解读步骤：
1. 解释每张牌在对应牌位中的含义
2. 综合三张牌之间的关系
3. 连接用户当前状态
4. 给出行动建议
5. 生成3个适合继续追问的问题

禁止：不要说""他一定会回来""，不要替用户做决定。";
        }

        private static string GetFollowupReadingPrompt()
        {
            return @"## 场景：占卜结果追问

用户基于刚才的占卜继续追问。不要重新随机占卜，应基于上一次reading的牌、综合解读和用户记忆继续解释。

输出JSON：{reply, suggested_message, followup_questions, voice_text}。";
        }

        private static string GetChatEntryPrompt()
        {
            return @"## 场景：进入对话

用户刚进入对话页。根据入口来源、今日牌、用户记忆和最近对话，生成一条短的开场语。

规则：
- 如果从今日占卜进入，明确承接今日牌，使用payload中提供的牌名。
- 如果直接进入，承接最近状态或邀请用户说出问题。
- 不要编造新抽牌，不要更换牌名。
- 不要写固定模板。不要出现""作为 AI""。";
        }

        private static string GetMemorySummaryPrompt()
        {
            return @"## 场景：用户记忆总结

根据最新对话或占卜结果，更新用户记忆。只提炼长期有用的信息，不要保存琐碎原文。

规则：
- 不要保存敏感身份信息。
- 不要做医学诊断。
- 不要把用户的话逐字复制。
- 只保留能帮助下一次对话更贴合用户的内容。";
        }

        private static string GetQuickReadingPrompt()
        {
            return @"## 场景：快速占卜推荐

用户打开快速占卜工具。你需要根据用户记忆、今日牌、最近对话，为四类快速占卜生成更贴合当前用户的问题。

规则：
- 只能使用输入中允许的 topic key。
- 每个问题不超过 22 个中文字符。
- 问题必须适合触发三牌占卜。
- 不要写医疗、法律、财务确定性建议。
- 不要恐吓用户。";
        }

        private static string GetFriendDivinationPrompt()
        {
            return @"## 场景：好友关系协作占卜

输入中包含用户资料、好友资料、用户问题、三张已抽好的塔罗牌、双方各自负责翻开的牌位权限。

规则：
- 不要重新抽牌，不要替换牌面。
- 只解释输入里的三张牌。
- 语气温柔、清醒、有边界感，不做绝对预测。
- 如果涉及真人好友，不要假装知道对方真实想法，只能说""牌面显示的关系动态""。";
        }
    }

    public static class PromptResources
    {
        private const string ManifestResourcePath = "prompts/prompt_manifest";

        private static readonly string[] RequiredPromptIds =
        {
            "persona.nocturne_oracle",
            "skills.tarot_skill",
            "policies.safety_boundaries",
            "scenes.daily_oracle",
            "scenes.complete_interpretation",
            "scenes.chat_entry",
            "scenes.chat_companion",
            "scenes.quick_reading",
            "scenes.spread_invitation",
            "scenes.card_reveal",
            "scenes.card_position_description",
            "scenes.three_card_reading",
            "scenes.followup_reading",
            "scenes.friend_divination_result",
            "scenes.user_memory_summary",
            "schemas.chat_entry",
            "schemas.chat_companion",
            "schemas.daily_oracle",
            "schemas.complete_interpretation",
            "schemas.quick_reading",
            "schemas.spread_invitation",
            "schemas.card_reveal",
            "schemas.three_card",
            "schemas.followup_reading",
            "schemas.friend_divination_result",
            "schemas.user_memory_summary",
            "schemas.divination_verdict"
        };

        private static readonly HashSet<string> AllowedKinds = new HashSet<string>
        {
            "persona", "skill", "policy", "scene", "schema"
        };

        private static PromptManifestConfig cachedManifest;
        private static Dictionary<string, PromptManifestEntry> cachedManifestIndex;
        private static string cachedManifestParseError;

        public static string Load(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return "";
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            return asset != null ? asset.text : "";
        }

        public static void ClearCache()
        {
            cachedManifest = null;
            cachedManifestIndex = null;
            cachedManifestParseError = null;
        }

        public static string LoadOrFallback(string resourcePath, string fallback)
        {
            string text = Load(resourcePath);
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        public static string LoadById(string promptId, string fallbackResourcePath = null, string fallbackText = "")
        {
            string resourcePath = ResolveResourcePath(promptId, fallbackResourcePath);
            string text = Load(resourcePath);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
            return fallbackText ?? "";
        }

        public static string ResolveResourcePath(string promptId, string fallbackResourcePath = null)
        {
            EnsureManifestIndex();
            if (!string.IsNullOrEmpty(promptId) &&
                cachedManifestIndex != null &&
                cachedManifestIndex.TryGetValue(promptId, out PromptManifestEntry entry) &&
                !string.IsNullOrWhiteSpace(entry.resourcePath))
            {
                return entry.resourcePath;
            }
            return fallbackResourcePath ?? promptId ?? "";
        }

        public static PromptManifestConfig GetManifest()
        {
            if (cachedManifest != null) return cachedManifest;

            string json = Load(ManifestResourcePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                cachedManifestParseError = null;
                cachedManifest = new PromptManifestConfig();
                return cachedManifest;
            }

            try
            {
                cachedManifest = JsonUtility.FromJson<PromptManifestConfig>(json) ?? new PromptManifestConfig();
                cachedManifestParseError = null;
            }
            catch (Exception ex)
            {
                cachedManifestParseError = ex.Message;
                cachedManifest = new PromptManifestConfig();
            }

            cachedManifest.Normalize();
            return cachedManifest;
        }

        public static List<string> ValidateManifest()
        {
            var issues = new List<string>();
            string manifestText = Load(ManifestResourcePath);
            if (string.IsNullOrWhiteSpace(manifestText))
            {
                issues.Add($"Missing prompt manifest: Resources/{ManifestResourcePath}");
                return issues;
            }

            PromptManifestConfig manifest = GetManifest();
            if (!string.IsNullOrWhiteSpace(cachedManifestParseError))
                issues.Add($"Prompt manifest JSON parse failed: {cachedManifestParseError}");

            List<PromptManifestEntry> entries = manifest.GetAllEntries();
            if (entries.Count == 0)
                issues.Add("Prompt manifest has no entries.");

            var seenIds = new HashSet<string>();
            var declaredIds = new HashSet<string>();
            foreach (PromptManifestEntry entry in entries)
            {
                if (entry == null)
                {
                    issues.Add("Prompt manifest contains a null entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.id))
                {
                    issues.Add($"Prompt manifest entry missing id: {entry.resourcePath}");
                }
                else
                {
                    declaredIds.Add(entry.id);
                    if (!seenIds.Add(entry.id))
                        issues.Add($"Duplicate prompt id: {entry.id}");
                }

                if (string.IsNullOrWhiteSpace(entry.kind))
                    issues.Add($"Prompt manifest entry missing kind: {entry.id}");
                else if (!AllowedKinds.Contains(entry.kind))
                    issues.Add($"Unsupported prompt kind '{entry.kind}': {entry.id}");

                if (string.IsNullOrWhiteSpace(entry.resourcePath))
                {
                    issues.Add($"Prompt manifest entry missing resourcePath: {entry.id}");
                    continue;
                }

                if (entry.required && string.IsNullOrWhiteSpace(Load(entry.resourcePath)))
                    issues.Add($"Required prompt not loadable: {entry.id} -> Resources/{entry.resourcePath}");
            }

            foreach (string requiredId in RequiredPromptIds)
            {
                if (!declaredIds.Contains(requiredId))
                    issues.Add($"Prompt manifest missing required id: {requiredId}");
            }

            return issues;
        }

        public static List<string> BuildDiagnostics()
        {
            var lines = new List<string>();
            PromptManifestConfig manifest = GetManifest();
            lines.Add($"Prompt manifest version: {(string.IsNullOrWhiteSpace(manifest.version) ? "missing" : manifest.version)}");

            foreach (PromptManifestEntry entry in manifest.GetAllEntries())
            {
                if (entry == null) continue;
                string text = Load(entry.resourcePath);
                string status = string.IsNullOrWhiteSpace(text) ? "MISSING" : $"OK {text.Length} chars";
                lines.Add($"{status} | {entry.id} | {entry.resourcePath}");
            }

            List<string> issues = ValidateManifest();
            if (issues.Count == 0)
                lines.Add("Prompt manifest validation: OK");
            else
                lines.AddRange(issues.Select(issue => "ISSUE: " + issue));

            return lines;
        }

        private static void EnsureManifestIndex()
        {
            if (cachedManifestIndex != null) return;

            cachedManifestIndex = new Dictionary<string, PromptManifestEntry>();
            foreach (PromptManifestEntry entry in GetManifest().GetAllEntries())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id)) continue;
                cachedManifestIndex[entry.id] = entry;
            }
        }
    }

    [Serializable]
    public class PromptManifestConfig
    {
        public string version;
        public List<PromptManifestEntry> basePrompts = new List<PromptManifestEntry>();
        public List<PromptManifestEntry> scenePrompts = new List<PromptManifestEntry>();
        public List<PromptManifestEntry> outputSchemas = new List<PromptManifestEntry>();

        public void Normalize()
        {
            basePrompts ??= new List<PromptManifestEntry>();
            scenePrompts ??= new List<PromptManifestEntry>();
            outputSchemas ??= new List<PromptManifestEntry>();
        }

        public List<PromptManifestEntry> GetAllEntries()
        {
            Normalize();
            return basePrompts
                .Concat(scenePrompts)
                .Concat(outputSchemas)
                .ToList();
        }
    }

    [Serializable]
    public class PromptManifestEntry
    {
        public string id;
        public string kind;
        public string resourcePath;
        public bool required;
    }

    // ================================================================
    // 治疗策略映射（Stage → Therapy Lens）
    // ================================================================

    public static class TherapyStrategies
    {
        private static readonly Dictionary<string, TherapyStrategy> Strategies;

        static TherapyStrategies()
        {
            Strategies = new Dictionary<string, TherapyStrategy>
            {
                ["listen"] = new TherapyStrategy
                {
                    stage = "listen",
                    therapyLens = new List<string> { "reflective_listening", "trauma_informed_safety" },
                    mustDo = new List<string> { "先认可情绪", "命名张力", "不要急着给结论" },
                    mustAvoid = new List<string> { "检测性问题", "最终判断", "过早的牌义解释" },
                    outputMoves = new List<string> { "我看见你在……", "这件事真正扎你的是……" }
                },
                ["clarify"] = new TherapyStrategy
                {
                    stage = "clarify",
                    therapyLens = new List<string> { "motivational_interviewing" },
                    mustDo = new List<string> { "问一个有用的澄清问题", "给具体选项" },
                    mustAvoid = new List<string> { "跳过澄清直接给方案" },
                    outputMoves = new List<string> { "你最想知道的是……？", "这件事有几个方向：……" }
                },
                ["before_draw"] = new TherapyStrategy
                {
                    stage = "before_draw",
                    therapyLens = new List<string> { "act_defusion", "narrative_externalization" },
                    mustDo = new List<string> { "解释为什么这个牌阵适合当前问题", "邀请而非强迫" },
                    mustAvoid = new List<string> { "直接抽牌", "跳过解释框架" },
                    outputMoves = new List<string> { "用这个牌阵来看……", "当下-阻碍-走向能帮你……" }
                },
                ["card_reveal"] = new TherapyStrategy
                {
                    stage = "card_reveal",
                    therapyLens = new List<string> { "parts_language" },
                    mustDo = new List<string> { "只解释当前翻开的牌和位置" },
                    mustAvoid = new List<string> { "给出整体结论", "提未翻开的牌", "提其他位置的牌" },
                    outputMoves = new List<string> { "这张牌在「当下」位置提醒你……" }
                },
                ["verdict"] = new TherapyStrategy
                {
                    stage = "verdict",
                    therapyLens = new List<string> { "values_action", "cbt_reframe" },
                    mustDo = new List<string> { "给一句简短真相", "给一个24小时行动", "基于所有已翻牌综合解读" },
                    mustAvoid = new List<string> { "重复解释每张牌", "绝对预测" },
                    outputMoves = new List<string> { "我直说：……", "接下来24小时你可以……" }
                },
                ["micro_action"] = new TherapyStrategy
                {
                    stage = "micro_action",
                    therapyLens = new List<string> { "values_action", "cbt_reframe" },
                    mustDo = new List<string> { "给一个行为级可执行的动作", "要具体不要抽象" },
                    mustAvoid = new List<string> { "只说'慢慢来'、'回到自己'等空话" },
                    outputMoves = new List<string> { "今天可以试试……", "具体的做法是……" }
                },
                ["follow_up"] = new TherapyStrategy
                {
                    stage = "follow_up",
                    therapyLens = new List<string> { "narrative_externalization", "cbt_reframe" },
                    mustDo = new List<string> { "基于已有 readingId 延续，不要重新抽牌" },
                    mustAvoid = new List<string> { "引入新牌" },
                    outputMoves = new List<string> { "上次你抽到……现在回头看……" }
                },
                ["recall"] = new TherapyStrategy
                {
                    stage = "recall",
                    therapyLens = new List<string> { "narrative_externalization", "values_action" },
                    mustDo = new List<string> { "承接明日提醒或开放循环", "保持叙事连续性" },
                    mustAvoid = new List<string> { "重新开启新话题" },
                    outputMoves = new List<string> { "你之前说……现在有变化吗？" }
                },
                ["dive_deeper"] = new TherapyStrategy
                {
                    stage = "dive_deeper",
                    therapyLens = new List<string> { "cbt_reframe", "parts_language" },
                    mustDo = new List<string> { "更深入展开但保持精炼" },
                    mustAvoid = new List<string> { "重复之前的结论" },
                    outputMoves = new List<string> { "更具体地说……", "这里面有一个模式……" }
                },
            };
        }

        public static TherapyStrategy GetFor(string stage)
        {
            OracleTypes.AssertOneOf(stage, OracleTypes.ORACLE_STAGES, "stage");
            return Strategies.TryGetValue(stage, out var strategy) ? strategy : Strategies["listen"];
        }
    }

    // ================================================================
    // ContextAssembler —— 核心组装器
    // ================================================================

    /// <summary>
    /// Oracle Runtime 上下文组装器 —— 将十个模块拼成一次完整的 LLM 调用。
    /// 对应 server.mjs 的 buildChatMessages() / assembleSceneCallMessages()
    /// </summary>
    public static class ContextAssembler
    {
        // ============================================================
        // 组装流式聊天消息（6条系统 + 1条用户）
        // ============================================================

        public static AssemblyResult AssembleStreamingChat(ChatPayload payload,
            MemorySource memorySource = null, ReadingLock readingLock = null)
        {
            payload ??= new ChatPayload();
            var oracleVoiceId = NormalizeOracleVoiceId(payload.user?.preferredTone);
            var locale = payload.locale ?? "zh-CN";

            // Step 1: 安全风险分类
            var riskFlags = RiskClassifier.ClassifyRisk(payload.message);
            var riskLevel = RiskClassifier.RiskLevel(riskFlags);

            // Step 2: 场景推断
            var (scene, sceneEvidence) = SceneInferrer.Infer(
                payload.message,
                payload.activeRelationshipId,
                null,
                riskFlags);
            payload.scene = scene;

            // Step 3: 阶段路由
            var (stage, responseMode, stageReason) = StageRouter.Route(
                payload.message, scene, payload.actionKind,
                payload.activeReadingState, payload.activeReadingId,
                payload.activeRelationshipId, false,
                payload.isReturningFromHook, payload.recentMessages?.Count ?? 0,
                riskFlags);

            // Step 4: 人格契约
            var personaContract = PersonaContracts.Get(oracleVoiceId);

            // Step 5: 治疗策略
            var therapyStrategy = TherapyStrategies.GetFor(stage);

            // Step 6: 响应契约
            var responseContract = ResponseContracts.GetFor(stage);

            // Step 6.5: 占卜规划。before_draw/clarify 阶段只规划，不锁牌。
            var divinationPlan = ResolveTurnDivinationPlan(
                payload.divinationPlan,
                payload.message,
                scene,
                stage,
                payload.activeRelationshipId,
                oracleVoiceId,
                riskLevel);

            // Step 7: 记忆打包
            var memoryPack = BuildMemoryPack(memorySource, scene, payload.activeRelationshipId,
                payload.activeReadingId, stage == "recall" || payload.isReturningFromHook);

            // Step 8: 阅读锁
            var readingLockContext = ReadingLockBuilder.Build(readingLock);

            // Step 10: 构建 RuntimePlan
            var runtimePlan = new RuntimePlan
            {
                turnId = Guid.NewGuid().ToString("N").Substring(0, 12),
                scene = scene,
                sceneEvidence = sceneEvidence,
                stage = stage,
                stageReason = stageReason,
                locale = locale,
                responseMode = responseMode,
                oracleVoiceId = oracleVoiceId,
                shouldInviteCardDraw = divinationPlan != null || stage == "clarify",
                shouldCreateTomorrowHook = (stage == "verdict" || stage == "micro_action" || stage == "follow_up")
                    && riskLevel != "high",
                therapyLens = therapyStrategy.therapyLens,
                divinationPlan = divinationPlan,
                riskFlags = riskFlags,
                riskLevel = riskLevel,
                memoryQuery = new MemoryPackQuery
                {
                    scene = scene,
                    activeRelationshipId = payload.activeRelationshipId,
                    activeReadingId = payload.activeReadingId,
                    includeTomorrowHook = stage == "recall",
                    maxItems = 4
                }
            };
            runtimePlan.suggestedActions = BuildSuggestedActions(
                scene,
                stage,
                payload.message,
                payload.activeRelationshipId,
                payload.activeReadingId,
                divinationPlan,
                riskLevel);

            // Step 10.5: 输出契约
            var outputContract = BuildOutputContract(
                stage,
                responseMode,
                responseContract,
                readingLockContext,
                runtimePlan,
                locale);

            // Step 11: 构建 OracleContext
            var oracleContext = new OracleContext
            {
                assembledAt = DateTime.UtcNow.ToString("o"),
                runtimePlan = runtimePlan,
                personaContract = personaContract,
                responseContract = responseContract,
                therapyStrategy = therapyStrategy,
                memoryPack = memoryPack,
                readingLock = readingLockContext,
                outputContract = outputContract,
                userPayload = new Dictionary<string, object>
                {
                    ["scene"] = scene,
                    ["stage"] = stage,
                    ["message"] = payload.message,
                    ["locale"] = locale
                }
            };

            // Step 12: 序列化 RuntimeContext 为 9 段
            var sections = SerializeOracleContext(oracleContext);

            // Step 13: 组装消息数组
            var messages = new List<ChatMessage>();

            // [0] Persona + Skill + Safety
            messages.Add(new ChatMessage
            {
                role = "system",
                content = PersonaContracts.BasePrompt(oracleVoiceId)
            });

            // [1] Stream Instruction
            messages.Add(new ChatMessage
            {
                role = "system",
                content = StreamInstructions.Build(oracleVoiceId, locale)
            });

            // [2] Oracle Runtime v1.2 Context
            var serializedContext = $"以下是 Oracle Runtime v1.2 当前上下文（{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC）:\n\n"
                + string.Join("\n\n---\n\n", sections.Select(s => $"## {s.title}\n{string.Join("\n", s.lines)}"));
            messages.Add(new ChatMessage
            {
                role = "system",
                content = serializedContext
            });

            // [3] Chat Turn Instruction
            var chatTurnInstruction = BuildChatTurnInstruction(scene, stage, responseMode, therapyStrategy, responseContract);
            messages.Add(new ChatMessage
            {
                role = "system",
                content = chatTurnInstruction
            });

            // [4] Relevant Memory
            var memoryLines = MemoryPackBuilder.ToPromptLines(memoryPack);
            var memoryPrompt = "## 用户记忆\n" + string.Join("\n", memoryLines);
            messages.Add(new ChatMessage
            {
                role = "system",
                content = memoryPrompt
            });

            // [5] User payload as JSON
            var payloadDict = BuildPayloadDict(payload, oracleContext);
            messages.Add(new ChatMessage
            {
                role = "user",
                content = MiniJsonSerializer.Serialize(payloadDict)
            });

            // 返回组装结果 + 调试记录
            return new AssemblyResult
            {
                messages = messages,
                oracleContext = oracleContext,
                sections = sections,
                promptRecord = new PromptRecord
                {
                    promptId = runtimePlan.turnId,
                    userInput = payload.message,
                    finalMessages = messages.ToList(),
                    runtimeSections = sections,
                    scene = scene,
                    stage = stage,
                    stageReason = stageReason,
                    responseMode = responseMode,
                    oracleVoiceId = oracleVoiceId,
                    memoryUsed = memoryLines,
                    riskFlags = riskFlags,
                    riskLevel = riskLevel,
                    readingId = readingLock?.readingId ?? payload.activeReadingId,
                    friendContext = payload.friendContext,
                    recordedAt = oracleContext.assembledAt
                }
            };
        }

        // ============================================================
        // 组装非流式场景调用消息（5条）
        // ============================================================

        public static AssemblyResult AssembleSceneCall(string sceneId, ChatPayload payload,
            MemorySource memorySource = null, string oracleVoiceId = "tarot_reader")
        {
            payload ??= new ChatPayload();
            var locale = payload.locale ?? "zh-CN";
            var oracleVoice = NormalizeOracleVoiceId(oracleVoiceId);

            var messages = new List<ChatMessage>();

            // [0] Persona + Skill + Safety
            messages.Add(new ChatMessage
            {
                role = "system",
                content = PersonaContracts.BasePrompt(oracleVoice)
            });

            // [1] Oracle Instruction
            messages.Add(new ChatMessage
            {
                role = "system",
                content = OraclePromptInstructionOnly(oracleVoice)
            });

            // [2] Scene Prompt
            var scenePrompt = ScenePrompts.GetWithOutputSchema(sceneId);
            if (!string.IsNullOrEmpty(scenePrompt))
            {
                messages.Add(new ChatMessage
                {
                    role = "system",
                    content = scenePrompt
                });
            }

            // [3] Memory
            var memoryPack = BuildMemoryPack(memorySource, sceneId,
                payload?.activeRelationshipId, payload?.activeReadingId);
            var memoryLines = MemoryPackBuilder.ToPromptLines(memoryPack);
            var memoryPrompt = "## 用户记忆\n" + string.Join("\n", memoryLines);
            messages.Add(new ChatMessage
            {
                role = "system",
                content = memoryPrompt
            });

            // [4] User payload
            var payloadDict = BuildPayloadDict(payload, null);
            messages.Add(new ChatMessage
            {
                role = "user",
                content = MiniJsonSerializer.Serialize(payloadDict)
            });

            return new AssemblyResult
            {
                messages = messages,
                promptRecord = new PromptRecord
                {
                    promptId = Guid.NewGuid().ToString("N").Substring(0, 12),
                    userInput = payload?.message ?? sceneId,
                    finalMessages = messages.ToList(),
                    scene = sceneId,
                    stage = sceneId,
                    responseMode = "scene_call",
                    oracleVoiceId = oracleVoice,
                    memoryUsed = memoryLines,
                    readingId = payload?.activeReadingId,
                    friendContext = payload?.friendContext,
                    recordedAt = DateTime.UtcNow.ToString("o")
                }
            };
        }

        // ============================================================
        // 序列化 OracleContext 为 9 段
        // ============================================================

        public static List<OracleSection> SerializeOracleContext(OracleContext context)
        {
            var sections = new List<OracleSection>();

            // 1. Safety Boundary
            sections.Add(new OracleSection
            {
                title = "Safety Boundary",
                lines = new List<string>
                {
                    RiskClassifier.SafetyInstructionsForRisk(context.runtimePlan?.riskFlags),
                    "Checklist: no absolute prediction | no diagnosis | no therapy claim | "
                    + "no third-party mind claim | no fear/FOMO retention."
                }
            });

            // 2. Persona Contract
            var pc = context.personaContract;
            if (pc != null)
            {
                sections.Add(new OracleSection
                {
                    title = "Persona Contract",
                    lines = new List<string>
                    {
                        $"Oracle Voice: {pc.displayName} ({pc.oracleVoiceId})",
                        $"Tone: {string.Join(", ", pc.tone)}",
                        $"Values: {string.Join(", ", pc.values)}",
                        $"Boundaries: {string.Join(" | ", pc.boundaries)}",
                        $"Forbidden: {string.Join(" | ", pc.forbidden)}"
                    }
                });
            }

            // 3. Scene / Stage
            var plan = context.runtimePlan;
            if (plan != null)
            {
                sections.Add(new OracleSection
                {
                    title = "Scene / Stage",
                    lines = new List<string>
                    {
                        $"Scene: {plan.scene} | Evidence: {string.Join(", ", plan.sceneEvidence ?? new List<string>())}",
                        $"Stage: {plan.stage} ({plan.stageReason})",
                        $"Response Mode: {plan.responseMode}",
                        $"Locale: {plan.locale}",
                        $"Risk Level: {plan.riskLevel}{(plan.riskFlags?.Count > 0 ? $" | Flags: {string.Join(", ", plan.riskFlags)}" : "")}",
                        $"Suggested Actions: {(plan.suggestedActions?.Count > 0 ? string.Join(" | ", plan.suggestedActions.Select(action => $"{action.kind}:{action.label}")) : "none")}"
                    }
                });
            }

            // 4. Therapy Lens
            var ts = context.therapyStrategy;
            if (ts != null)
            {
                sections.Add(new OracleSection
                {
                    title = "Therapy Lens",
                    lines = new List<string>
                    {
                        $"Stage: {ts.stage}",
                        $"Lenses: {string.Join(", ", ts.therapyLens ?? new List<string>())}",
                        $"Must Do: {string.Join(" | ", ts.mustDo ?? new List<string>())}",
                        $"Must Avoid: {string.Join(" | ", ts.mustAvoid ?? new List<string>())}",
                        $"Output Moves: {string.Join(" | ", ts.outputMoves ?? new List<string>())}"
                    }
                });
            }

            // 5. Divination Plan
            var dp = plan?.divinationPlan;
            if (dp != null)
            {
                sections.Add(new OracleSection
                {
                    title = "Divination Plan",
                    lines = new List<string>
                    {
                        $"Plan: {dp.planId} | Spread: {dp.spreadKind} | Cards: {dp.cardCount}",
                        $"Question: {dp.question}",
                        $"Reason: {dp.reasonForSpread}",
                        $"Positions: {string.Join(" | ", (dp.positions ?? new List<SpreadPosition>()).Select(position => $"{position.label}:{position.prompt}"))}",
                        $"Complexity: {dp.complexity}",
                        $"Requires Pro: {dp.requiresProForFullReading}"
                    }
                });
            }
            else
            {
                sections.Add(new OracleSection
                {
                    title = "Divination Plan",
                    lines = new List<string> { "No active divination plan." }
                });
            }

            // 6. Reading Lock
            var rl = context.readingLock;
            if (rl != null)
            {
                sections.Add(new OracleSection
                {
                    title = "Reading Lock",
                    lines = rl.lines ?? new List<string> { "No active card lock." }
                });
            }

            // 7. Memory Pack
            var mp = context.memoryPack;
            if (mp != null)
            {
                sections.Add(new OracleSection
                {
                    title = "Memory Pack",
                    lines = MemoryPackBuilder.ToPromptLines(mp)
                });
            }

            // 8. Output Contract
            var oc = context.outputContract;
            if (oc != null)
            {
                sections.Add(new OracleSection
                {
                    title = "Output Contract",
                    lines = oc.lines ?? new List<string>()
                });
            }

            // 9. User Payload
            if (context.userPayload != null && context.userPayload.Count > 0)
            {
                sections.Add(new OracleSection
                {
                    title = "User Payload",
                    lines = context.userPayload
                        .Where(kv => kv.Value != null)
                        .Select(kv => $"{kv.Key}: {kv.Value}")
                        .ToList()
                });
            }

            return sections;
        }

        // ============================================================
        // 私有辅助方法
        // ============================================================

        private static MemoryPack BuildMemoryPack(MemorySource source, string scene,
            string activeRelationshipId, string activeReadingId, bool includeTomorrowHook = false)
        {
            return MemoryPackBuilder.Build(
                new MemoryPackQuery
                {
                    scene = scene ?? "general_chat",
                    activeRelationshipId = activeRelationshipId,
                    activeReadingId = activeReadingId,
                    includeTomorrowHook = includeTomorrowHook,
                    maxItems = 4
                },
                source ?? new MemorySource());
        }

        private static OutputContract BuildOutputContract(
            string stage,
            string responseMode,
            ResponseContract responseContract,
            ReadingLockContext lockContext,
            RuntimePlan runtimePlan,
            string locale)
        {
            var lines = new List<string>
            {
                $"Max sentences: {responseContract.maxSentences}",
                $"Max words/chars: {responseContract.maxWords}",
                $"Must do: {string.Join(" | ", responseContract.mustDo ?? new List<string>())}",
                $"Must not: {string.Join(" | ", responseContract.mustNot ?? new List<string>())}",
                $"Next action policy: {responseContract.nextActionPolicy}",
                "Style: normal human speech, one direct truth line, no mystical filler.",
                "Avoid vague phrases like 回到自己、倾听内心、宇宙指引、能量、慢慢来、答案在你心里 unless immediately translated into a concrete behavior.",
                "Action specificity: relationship messaging anxiety can use concrete boundaries like 今晚别补第二条、只发一句、等24小时、静音聊天、写草稿不发送.",
                "Do not use broad action language like 靠近、稳定自己、找回节奏 unless it is immediately translated into an actual behavior.",
                $"Primary language: {locale ?? "zh-CN"}."
            };

            if (stage == "listen")
                lines.Add("Do not end with a question unless it is the single most useful next step.");
            if (stage == "card_reveal")
                lines.Add("Explain only the current revealed card and position.");
            if (stage == "verdict")
                lines.Add("Include a short compassionate truth and one 24-hour action.");
            if (lockContext != null && lockContext.locked)
                lines.Add("Use only cards in Reading Lock.");
            if (runtimePlan?.suggestedActions != null && runtimePlan.suggestedActions.Count > 0)
                lines.Add("Suggested actions: " + string.Join(" | ", runtimePlan.suggestedActions.Select(action => $"{action.kind}:{action.label}")));

            return new OutputContract
            {
                stage = stage,
                responseMode = responseMode,
                lines = lines
            };
        }

        private static DivinationPlan ResolveTurnDivinationPlan(
            DivinationPlan existingPlan,
            string question,
            string scene,
            string stage,
            string activeRelationshipId,
            string oracleVoiceId,
            string riskLevel)
        {
            if (existingPlan != null) return existingPlan;
            if (riskLevel == "high") return null;
            if (!ShouldPlanDivination(question, stage)) return null;
            return BuildPlannedDivination(question, scene, activeRelationshipId, oracleVoiceId);
        }

        private static bool ShouldPlanDivination(string question, string stage)
        {
            if (stage == "before_draw") return true;
            return stage == "clarify" && StageRouter.HasDivinationIntent(question);
        }

        private static DivinationPlan BuildPlannedDivination(
            string question,
            string scene,
            string activeRelationshipId,
            string oracleVoiceId)
        {
            string spreadKind = ChooseSpreadKind(question, scene, activeRelationshipId);
            string complexity = EstimateQuestionComplexity(question, activeRelationshipId);
            int cardCount = PreferredCardCount(spreadKind, complexity);
            var positions = BuildSpreadPositions(spreadKind, cardCount);

            return new DivinationPlan
            {
                planId = Guid.NewGuid().ToString("N").Substring(0, 12),
                userId = "",
                conversationId = "dialog",
                question = question ?? "",
                scene = scene ?? "general_chat",
                spreadKind = spreadKind,
                cardCount = cardCount,
                complexity = complexity,
                positions = positions,
                reasonForSpread = BuildSpreadReason(spreadKind),
                professionalFrame = new List<string>
                {
                    "牌是镜子，不是绝对预测。",
                    "至少一个位置必须回到用户能做的下一步。",
                    "只根据用户问题和已锁定牌面解读。"
                },
                requiresProForFullReading = spreadKind == "pattern_map"
                    || spreadKind == "timeline_thread"
                    || spreadKind == "celtic_inspired_deep_cross",
                createdAt = DateTime.UtcNow.ToString("o")
            };
        }

        private static string ChooseSpreadKind(string question, string scene, string activeRelationshipId)
        {
            string text = NormalizeText(question);
            if (Regex.IsMatch(text, @"deep reading|full picture|whole situation|many moving parts|深入|完整|全貌|整体|复杂|很多"))
                return "celtic_inspired_deep_cross";
            if (Regex.IsMatch(text, @"self[- ]?worth|shame|ashamed|not enough|hate myself|sleep|self care|自我价值|羞耻|不够好|讨厌自己|睡前|修复自己|照顾自己"))
                return "self_repair";
            if (Regex.IsMatch(text, @"future|next month|long term|timeline|seven days|7 days|reconcile|come back|where is this going|未来|下个月|长期|时间线|七天|7天|复合|回来|走向"))
                return "timeline_thread";
            if (Regex.IsMatch(text, @"again|always|keep|pattern|cycle|push and pull|repeating|same thing|反复|总是|一直|模式|循环|拉扯|重复"))
                return "pattern_map";
            if (Regex.IsMatch(text, @"should i|whether|option|choice|choose|stay or|leave or|text or|该不该|要不要|是否|还是|选择|继续|放弃|主动"))
                return "choice_gate";
            if (scene == "relationship_anxiety" || scene == "breakup_relapse" ||
                !string.IsNullOrEmpty(activeRelationshipId) || IsRelationshipQuestion(text))
                return "relationship_tension";
            if (EstimateEmotionalIntensity(text) == "high")
                return "mirror_card";
            return "mirror_card";
        }

        private static string EstimateQuestionComplexity(string question, string activeRelationshipId)
        {
            string text = NormalizeText(question);
            int score = 0;
            if (!string.IsNullOrEmpty(activeRelationshipId) || IsRelationshipQuestion(text)) score += 1;
            if (Regex.IsMatch(text, @"should i|whether|option|choice|choose|该不该|要不要|是否|还是|选择|继续|放弃")) score += 1;
            if (Regex.IsMatch(text, @"again|always|pattern|cycle|反复|总是|模式|循环|拉扯|重复")) score += 2;
            if (Regex.IsMatch(text, @"future|next month|long term|timeline|seven days|7 days|未来|下个月|长期|时间线|七天|7天|复合|回来|走向")) score += 2;
            if (Regex.IsMatch(text, @"everything|complicated|whole situation|deep reading|full picture|复杂|很多|整体|全貌|深入|全部")) score += 3;

            if (score >= 5) return "deep";
            if (score >= 2) return "standard";
            return "light";
        }

        private static string EstimateEmotionalIntensity(string question)
        {
            string text = NormalizeText(question);
            if (Regex.IsMatch(text, @"panic|spiral|can't breathe|desperate|devastated|crying|tonight|right now|overwhelmed|崩溃|喘不过气|很害怕|很慌|现在|今晚|受不了"))
                return "high";
            if (Regex.IsMatch(text, @"anxious|worried|confused|stuck|hurt|afraid|uncertain|waiting|silence|焦虑|担心|迷茫|卡住|受伤|不确定|等待|沉默|不安"))
                return "medium";
            return "low";
        }

        private static int PreferredCardCount(string spreadKind, string complexity)
        {
            switch (spreadKind)
            {
                case "mirror_card": return 1;
                case "relationship_tension": return 3;
                case "choice_gate": return 5;
                case "pattern_map": return 5;
                case "timeline_thread": return 5;
                case "self_repair": return 3;
                case "celtic_inspired_deep_cross": return 5; // 当前 UI 支持 5 张，作为深层十字的轻量版。
                default: return 3;
            }
        }

        private static List<SpreadPosition> BuildSpreadPositions(string spreadKind, int cardCount)
        {
            switch (spreadKind)
            {
                case "mirror_card":
                    return new List<SpreadPosition>
                    {
                        Position("mirror_action", "情绪与下一步", "照见此刻最需要被承认的感受，以及未来24小时一个干净行动。", "action", "cups")
                    };
                case "relationship_tension":
                    return new List<SpreadPosition>
                    {
                        Position("what_is_happening", "正在发生什么", "当前关系场里可观察到的事实。", "clarify", "swords"),
                        Position("what_is_hidden", "未说出口的压力", "影响用户体验的假设、压力或未表达层。", "externalize", "cups"),
                        Position("what_protects_you_tonight", "今晚保护你的行动", "未来24小时保护尊严和主体性的具体行动。", "action", "pentacles")
                    };
                case "choice_gate":
                    return new List<SpreadPosition>
                    {
                        Position("option_a", "路径A", "选择A会打开什么。", "clarify", "wands"),
                        Position("option_b", "路径B", "选择B会打开什么。", "clarify", "swords"),
                        Position("fear_voice", "恐惧的声音", "哪个恐惧正在替用户做决定。", "externalize", "swords"),
                        Position("values_voice", "价值的声音", "哪个价值真正想带路。", "reframe", "cups"),
                        Position("one_clean_action", "一个干净行动", "无论结果如何，用户未来24小时可以做的自尊行动。", "action", "pentacles")
                    };
                case "pattern_map":
                    return new List<SpreadPosition>
                    {
                        Position("surface_loop", "表层循环", "表面上一直重复的模式。", "mirror", "swords"),
                        Position("emotional_need", "底层需要", "循环下面真正未被照顾的需要。", "clarify", "cups"),
                        Position("protective_strategy", "保护策略", "哪个行为在试图保护用户。", "externalize", "court"),
                        Position("cost_of_pattern", "代价", "这个模式继续无意识运转会消耗什么。", "reframe", "pentacles"),
                        Position("new_pattern_action", "新行动", "一个能打断循环且保住尊严的小行动。", "action", "wands")
                    };
                case "timeline_thread":
                    return new List<SpreadPosition>
                    {
                        Position("now", "现在", "当前情绪和关系天气。", "mirror", "cups"),
                        Position("next_step", "下一步信号", "如果什么都不改变，接下来可能出现的倾向。", "clarify", "swords"),
                        Position("turning_point", "转折点", "用户可以中断旧路径的位置。", "reframe", "wands"),
                        Position("likely_tendency", "当前路径倾向", "当前模式继续时更可能形成的趋势，而非预测。", "integrate", "major_arcana"),
                        Position("anchor_action", "锚定行动", "贯穿这段时间线的稳定行动。", "action", "pentacles")
                    };
                case "celtic_inspired_deep_cross":
                    return new List<SpreadPosition>
                    {
                        Position("center", "问题中心", "当前局面的核心。", "mirror", "major_arcana"),
                        Position("crossing", "交叉压力", "让问题复杂化的压力。", "externalize", "swords"),
                        Position("root", "根部模式", "当前事件下面更旧的模式。", "clarify", "cups"),
                        Position("field", "关系/现实场", "用户可以观察到的外部环境。", "externalize", "pentacles"),
                        Position("clean_action", "干净行动", "现在保护主体性的行动。", "action", "wands")
                    };
                case "self_repair":
                default:
                    return new List<SpreadPosition>
                    {
                        Position("wound_story", "刺痛你的故事", "哪个叙事让用户觉得自己变小。", "externalize", "swords"),
                        Position("true_need", "真正的需要", "此刻真正需要的照顾或确认。", "clarify", "cups"),
                        Position("repair_action", "修复行动", "未来24小时一个自尊的照顾动作。", "action", "pentacles")
                    };
            }
        }

        private static SpreadPosition Position(string key, string label, string prompt, string role, string axis)
        {
            return new SpreadPosition
            {
                key = key,
                label = label,
                prompt = prompt,
                therapeuticRole = role,
                tarotTheoryAxis = axis,
                consentBoundary = "user_scope_only"
            };
        }

        private static string BuildSpreadReason(string spreadKind)
        {
            switch (spreadKind)
            {
                case "relationship_tension":
                    return "这不是一个简单的是/否问题，需要拆开正在发生的事实、未说出口的压力，以及今晚保护你的行动。";
                case "choice_gate":
                    return "这是选择题，需要并列看两条路径，同时保留一个不替你做决定的干净行动。";
                case "pattern_map":
                    return "这更像反复出现的循环，适合看模式从哪里开始、代价是什么，以及你能怎样中断。";
                case "timeline_thread":
                    return "这个问题带着时间线，适合看当前路径的倾向，而不是把未来说成定局。";
                case "self_repair":
                    return "这个问题先需要修复感受，再谈确定性，所以牌阵会从刺痛、需要和行动入手。";
                case "celtic_inspired_deep_cross":
                    return "局面有很多层，适合用深层十字的轻量结构看中心、压力、根部、外部场和干净行动。";
                case "mirror_card":
                default:
                    return "当情绪很响时，一张镜子牌足够先照见此刻，而不是把问题变成预言。";
            }
        }

        private static List<OracleAction> BuildSuggestedActions(
            string scene,
            string stage,
            string text,
            string activeRelationshipId,
            string activeReadingId,
            DivinationPlan divinationPlan,
            string riskLevel)
        {
            if (riskLevel == "high")
                return new List<OracleAction> { ContinueChatAction("我可以陪你先走一个小步骤") };

            if (stage == "before_draw" && divinationPlan != null)
            {
                return new List<OracleAction>
                {
                    CreateAction("start_spread", $"开始{divinationPlan.cardCount}张牌阵", "start_spread",
                        new Dictionary<string, object>
                        {
                            ["planId"] = divinationPlan.planId,
                            ["spreadKind"] = divinationPlan.spreadKind,
                            ["cardCount"] = divinationPlan.cardCount
                        }),
                    ContinueChatAction("先把事情说清楚", new Dictionary<string, object> { ["scene"] = scene })
                };
            }

            if (stage == "card_reveal")
            {
                return new List<OracleAction>
                {
                    CreateAction("reveal_next", "翻开下一张", "reveal_card",
                        new Dictionary<string, object> { ["activeReadingId"] = activeReadingId ?? "" }),
                    ContinueChatAction("先停一下感受", new Dictionary<string, object> { ["activeReadingId"] = activeReadingId ?? "" })
                };
            }

            if (stage == "verdict")
            {
                return new List<OracleAction>
                {
                    DiveDeeperAction(activeReadingId),
                    SaveTomorrowHookAction(activeReadingId, scene, activeRelationshipId),
                    ContinueChatAction("继续追问", new Dictionary<string, object> { ["activeReadingId"] = activeReadingId ?? "" })
                };
            }

            if (stage == "clarify")
            {
                return new List<OracleAction>
                {
                    PlanSpreadAction("为这件事选牌阵", scene, activeRelationshipId, text),
                    CreateAction("ask_about_person", string.IsNullOrEmpty(activeRelationshipId) ? "先说这个人是谁" : "只看我的部分", "ask_about_person",
                        new Dictionary<string, object> { ["activeRelationshipId"] = activeRelationshipId ?? "" }),
                    ContinueChatAction("我想先解释一下", new Dictionary<string, object> { ["scene"] = scene })
                };
            }

            if (scene == "relationship_anxiety" || scene == "breakup_relapse")
            {
                return new List<OracleAction>
                {
                    PlanSpreadAction("抽一个关系牌阵", scene, activeRelationshipId, text),
                    ContinueChatAction("帮我理清事实", new Dictionary<string, object> { ["scene"] = scene }),
                    SaveTomorrowHookAction(activeReadingId, scene, activeRelationshipId)
                };
            }

            if (scene == "career_uncertainty")
            {
                return new List<OracleAction>
                {
                    PlanSpreadAction("比较两个选择", scene, activeRelationshipId, text),
                    ContinueChatAction("帮我看真实取舍", new Dictionary<string, object> { ["scene"] = scene }),
                    SaveTomorrowHookAction(activeReadingId, scene, activeRelationshipId)
                };
            }

            return new List<OracleAction>
            {
                PlanSpreadAction("为这件事抽一张牌", scene, activeRelationshipId, text),
                ContinueChatAction("问我一个落地问题", new Dictionary<string, object> { ["scene"] = scene }),
                SaveTomorrowHookAction(activeReadingId, scene, activeRelationshipId)
            };
        }

        private static OracleAction PlanSpreadAction(string label, string scene, string activeRelationshipId, string text)
        {
            return CreateAction("plan_spread", label, "plan_spread",
                new Dictionary<string, object>
                {
                    ["scene"] = scene ?? "",
                    ["activeRelationshipId"] = activeRelationshipId ?? "",
                    ["text"] = text ?? ""
                });
        }

        private static OracleAction ContinueChatAction(string label, Dictionary<string, object> payload = null)
        {
            return CreateAction("continue_chat", label, "continue_chat", payload ?? new Dictionary<string, object>());
        }

        private static OracleAction SaveTomorrowHookAction(string activeReadingId, string scene = null, string activeRelationshipId = null)
        {
            return CreateAction("save_tomorrow_hook", "明天再回来看", "save_tomorrow_hook",
                new Dictionary<string, object>
                {
                    ["activeReadingId"] = activeReadingId ?? "",
                    ["scene"] = scene ?? "",
                    ["activeRelationshipId"] = activeRelationshipId ?? ""
                });
        }

        private static OracleAction DiveDeeperAction(string activeReadingId)
        {
            return CreateAction("dive_deeper", "深入一点", "dive_deeper",
                new Dictionary<string, object> { ["activeReadingId"] = activeReadingId ?? "" });
        }

        private static OracleAction CreateAction(string id, string label, string kind, Dictionary<string, object> payload)
        {
            OracleTypes.AssertOneOf(kind, OracleTypes.ORACLE_ACTION_KINDS, "OracleAction kind");
            return new OracleAction
            {
                id = id,
                label = label,
                kind = kind,
                payload = payload ?? new Dictionary<string, object>()
            };
        }

        private static bool IsRelationshipQuestion(string text)
        {
            return Regex.IsMatch(text ?? "",
                @"\b(he|she|they|alex|ava|morgan|ex|crush|partner|situationship|relationship|text|reply|respond|silence|cold|ghost|love|contact)\b|关系|感情|喜欢|回应|联系|复合|暧昧|对方|他(?!人)|她|前任",
                RegexOptions.IgnoreCase);
        }

        private static string NormalizeText(string value)
        {
            return Regex.Replace(value ?? "", @"\s+", " ").Trim().ToLowerInvariant();
        }

        private static string BuildChatTurnInstruction(string scene, string stage, string responseMode,
            TherapyStrategy therapy, ResponseContract responseContract)
        {
            var lines = new List<string>
            {
                $"Current scene: {scene}",
                $"Current stage: {stage} | Response mode: {responseMode}",
                $"Max: {responseContract.maxSentences} sentences, {responseContract.maxWords} words.",
                "",
                "## Stage rules",
                $"Must do: {string.Join(" | ", responseContract.mustDo ?? new List<string>())}",
                $"Must NOT: {string.Join(" | ", responseContract.mustNot ?? new List<string>())}"
            };

            var sceneRules = GetStreamingSceneRules(scene, stage);
            if (sceneRules.Count > 0)
            {
                lines.Add("");
                lines.Add("## Scene rules");
                lines.AddRange(sceneRules.Select(rule => $"- {rule}"));
            }

            if (therapy?.therapyLens != null && therapy.therapyLens.Count > 0)
            {
                var lensDescriptions = therapy.therapyLens.Select(l => l switch
                {
                    "reflective_listening" => "reflect user emotions before suggesting anything",
                    "trauma_informed_safety" => "prioritize emotional safety, do not probe deeply",
                    "motivational_interviewing" => "help user find their own direction with open questions",
                    "cbt_reframe" => "connect card symbols to user's real situation, reframe stuck patterns",
                    "act_defusion" => "help user separate from anxious thoughts, not merge with them",
                    "values_action" => "encourage one small values-congruent action",
                    "narrative_externalization" => "treat the card as the story, not the person as the problem",
                    "parts_language" => "name different parts of user's experience without judgment",
                    _ => l
                }).ToList();
                lines.Add($"Therapy lens: {string.Join(" | ", lensDescriptions)}");
            }

            // Anti-repetition
            lines.Add("");
            lines.Add("## Anti-repetition: compare your reply with the last 3 assistant replies.");
            lines.Add("Avoid repeating the same metaphor, same card meaning, same question, same conclusion.");

            // Respond to "why do you say that?"
            lines.Add("If user asks 为什么这么说, answer with evidence from user's own words and observed patterns.");
            lines.Add("Do NOT restate the previous conclusion as if it were new.");

            return string.Join("\n", lines);
        }

        private static List<string> GetStreamingSceneRules(string scene, string stage)
        {
            var rules = new List<string>
            {
                "普通聊天不要强行使用塔罗、牌面或神秘学语言；只有用户明确进入占卜/追问牌面时才使用。",
                "先回应用户真实处境，再使用任何象征工具。"
            };

            switch (scene)
            {
                case "relationship_anxiety":
                case "breakup_relapse":
                case "friendship":
                case "ambiguous_situation":
                    rules.Add("关系问题只谈用户提供的事实和可观察互动，不声称知道第三方秘密心理。");
                    rules.Add("当用户问该不该联系、发消息、等回应时，给一个行为级边界；例如只发一句、等24小时、静音、写草稿不发送。");
                    break;
                case "career_uncertainty":
                    rules.Add("事业问题优先给现实约束、下一步实验和可验证行动，不做确定性预测。");
                    break;
                case "daily_reflection":
                    rules.Add("只有 payload 有 todayCard/todayOracle 时才承接今日牌；不要编造新的今日牌。");
                    break;
                case "crisis":
                    rules.Add("安全优先，先稳定和转介现实支持，不做娱乐化占卜。");
                    break;
                default:
                    rules.Add("如果只是闲聊或情绪表达，保持短、直接、具体，不主动建议抽牌。");
                    break;
            }

            if (stage == "card_reveal")
                rules.Add("逐张揭牌时只解释当前牌，不综合未翻开的牌。");
            if (stage == "follow_up")
                rules.Add("追问阶段沿用已有 readingId，不主动引入新牌或新牌阵。");

            return rules;
        }

        private static string OraclePromptInstructionOnly(string oracleVoiceId)
        {
            oracleVoiceId = NormalizeOracleVoiceId(oracleVoiceId);
            return oracleVoiceId switch
            {
                "astrologer" => @"当前神谕师：占星师 Celestial Analyst。
回复风格：温和、理性、结构化、有时间感和周期感。
工具边界：除非用户明确切回塔罗师，不要抽塔罗牌。不要编造用户未提供的出生盘配置。",

                "sage" => @"当前神谕师：冥想师 Stoic Monk。
回复风格：低沉、缓慢、稳定，先回到身体，再命名情绪，最后给一个小行动。
工具边界：除非用户明确切回塔罗师，不要抽塔罗牌。不要冒充治疗师，不做诊断，不给医疗承诺。",

                _ => @"当前神谕师：塔罗师 Tough Love Tarot。
回复风格：直接、象征化、能指出关系模式，但要保护用户主体性。
工具边界：可使用塔罗牌阵；牌是镜子，不是绝对预言。"
            };
        }

        /// <summary>
        /// 构建发送给 LLM 的 user payload
        /// </summary>
        private static Dictionary<string, object> BuildPayloadDict(ChatPayload p, OracleContext ctx)
        {
            if (p == null) return new Dictionary<string, object>();

            var dict = new Dictionary<string, object>
            {
                ["scene"] = p.scene ?? "chat_companion_stream",
                ["message"] = p.message ?? ""
            };

            // 用户资料
            if (p.user != null)
            {
                var userDict = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(p.user.preferredName)) userDict["preferredName"] = p.user.preferredName;
                if (!string.IsNullOrEmpty(p.user.preferredTone)) userDict["preferredTone"] = p.user.preferredTone;
                if (p.user.activeRelationships?.Count > 0) userDict["activeRelationships"] = p.user.activeRelationships;
                dict["user"] = userDict;
            }

            // 今日牌
            if (p.todayCard != null)
            {
                dict["todayCard"] = new Dictionary<string, object>
                {
                    ["cardId"] = p.todayCard.cardId,
                    ["cardName"] = p.todayCard.cardName ?? p.todayCard.cardId,
                    ["displayName"] = p.todayCard.displayName ?? p.todayCard.cardName,
                    ["nameZh"] = p.todayCard.nameZh ?? p.todayCard.cardName,
                    ["orientation"] = p.todayCard.orientation ?? "upright",
                    ["oracleText"] = p.todayCard.oracleText ?? "",
                    ["title"] = p.todayCard.title ?? ""
                };
            }

            // 今日神谕
            if (p.todayOracle != null)
            {
                dict["todayOracle"] = new Dictionary<string, object>
                {
                    ["title"] = p.todayOracle.title ?? "",
                    ["oracle"] = p.todayOracle.oracle ?? "",
                    ["dos"] = p.todayOracle.dos ?? new List<string>(),
                    ["donts"] = p.todayOracle.donts ?? new List<string>(),
                    ["detail"] = p.todayOracle.detail ?? "",
                    ["microAction"] = p.todayOracle.microAction ?? ""
                };
            }

            // 最近占卜
            if (p.lastReading != null)
            {
                dict["lastReading"] = new Dictionary<string, object>
                {
                    ["readingId"] = p.lastReading.readingId,
                    ["readingType"] = p.lastReading.readingType ?? "",
                    ["question"] = p.lastReading.question ?? "",
                    ["shortVerdict"] = p.lastReading.shortVerdict ?? "",
                    ["cards"] = LockedCardsToPayload(p.lastReading.cards),
                    ["createdAt"] = p.lastReading.createdAt ?? ""
                };
            }

            // 最近对话上下文
            if (p.recentReadingContext != null)
            {
                dict["recentReadingContext"] = new Dictionary<string, object>
                {
                    ["readingId"] = p.recentReadingContext.readingId ?? "",
                    ["readingType"] = p.recentReadingContext.readingType ?? "",
                    ["question"] = p.recentReadingContext.question ?? "",
                    ["shortVerdict"] = p.recentReadingContext.shortVerdict ?? ""
                };
            }

            // 运行时上下文
            if (ctx?.runtimePlan != null)
            {
                dict["oracleStage"] = ctx.runtimePlan.stage;
                dict["oracleScene"] = ctx.runtimePlan.scene;
                dict["responseMode"] = ctx.runtimePlan.responseMode;
                dict["stageReason"] = ctx.runtimePlan.stageReason ?? "";
                dict["shouldInviteCardDraw"] = ctx.runtimePlan.shouldInviteCardDraw;
                dict["shouldCreateTomorrowHook"] = ctx.runtimePlan.shouldCreateTomorrowHook;
                if (ctx.runtimePlan.suggestedActions != null && ctx.runtimePlan.suggestedActions.Count > 0)
                    dict["suggestedActions"] = OracleActionsToPayload(ctx.runtimePlan.suggestedActions);
            }

            var plan = ctx?.runtimePlan?.divinationPlan ?? p.divinationPlan;
            if (plan != null)
                dict["divinationPlan"] = DivinationPlanToPayload(plan);

            // 对话摘要和最近消息
            if (!string.IsNullOrEmpty(p.conversationSummary))
                dict["conversationSummary"] = p.conversationSummary;
            if (p.recentMessages?.Count > 0)
                dict["recentMessages"] = p.recentMessages;
            if (p.recentAssistantReplies?.Count > 0)
                dict["recentAssistantReplies"] = p.recentAssistantReplies;
            if (!string.IsNullOrEmpty(p.lastOracleReply))
                dict["lastOracleReply"] = p.lastOracleReply;

            dict["rationaleQuestion"] = p.rationaleQuestion;
            dict["memoryUsed"] = p.memoryUsed ?? new List<string>();
            dict["actionKind"] = p.actionKind ?? "";
            dict["activeRelationshipId"] = p.activeRelationshipId ?? "";
            dict["activeReadingId"] = p.activeReadingId ?? "";
            dict["activeReadingState"] = p.activeReadingState ?? "";

            if (!string.IsNullOrEmpty(p.friendContext))
                dict["friendContext"] = p.friendContext;

            return dict;
        }

        private static List<Dictionary<string, object>> OracleActionsToPayload(List<OracleAction> actions)
        {
            var result = new List<Dictionary<string, object>>();
            if (actions == null) return result;

            foreach (var action in actions)
            {
                if (action == null) continue;
                result.Add(new Dictionary<string, object>
                {
                    ["id"] = action.id ?? "",
                    ["label"] = action.label ?? "",
                    ["kind"] = action.kind ?? "",
                    ["payload"] = action.payload ?? new Dictionary<string, object>()
                });
            }

            return result;
        }

        private static string NormalizeOracleVoiceId(string oracleVoiceId)
        {
            return OracleTypes.ORACLE_VOICE_IDS.Contains(oracleVoiceId)
                ? oracleVoiceId
                : "tarot_reader";
        }

        private static Dictionary<string, object> DivinationPlanToPayload(DivinationPlan plan)
        {
            if (plan == null) return new Dictionary<string, object>();

            return new Dictionary<string, object>
            {
                ["planId"] = plan.planId ?? "",
                ["question"] = plan.question ?? "",
                ["scene"] = plan.scene ?? "",
                ["spreadKind"] = plan.spreadKind ?? "",
                ["cardCount"] = plan.cardCount,
                ["complexity"] = plan.complexity ?? "",
                ["reasonForSpread"] = plan.reasonForSpread ?? "",
                ["professionalFrame"] = plan.professionalFrame ?? new List<string>(),
                ["requiresProForFullReading"] = plan.requiresProForFullReading,
                ["positions"] = (plan.positions ?? new List<SpreadPosition>())
                    .Select(position => new Dictionary<string, object>
                    {
                        ["key"] = position.key ?? "",
                        ["label"] = position.label ?? "",
                        ["prompt"] = position.prompt ?? "",
                        ["therapeuticRole"] = position.therapeuticRole ?? "",
                        ["consentBoundary"] = position.consentBoundary ?? ""
                    })
                    .ToList()
            };
        }

        private static List<Dictionary<string, object>> LockedCardsToPayload(List<LockedCard> cards)
        {
            var result = new List<Dictionary<string, object>>();
            if (cards == null) return result;

            foreach (LockedCard card in cards)
            {
                if (card == null) continue;
                result.Add(new Dictionary<string, object>
                {
                    ["position"] = card.position ?? "",
                    ["positionKey"] = card.positionKey ?? "",
                    ["cardId"] = card.cardId ?? "",
                    ["cardName"] = card.cardName ?? "",
                    ["orientation"] = card.orientation ?? ""
                });
            }

            return result;
        }
    }

    // ================================================================
    // AssemblyResult
    // ================================================================

    [Serializable]
    public class AssemblyResult
    {
        public List<ChatMessage> messages;
        public OracleContext oracleContext;
        public List<OracleSection> sections;
        public PromptRecord promptRecord;
    }

    // ================================================================
    // 简易 JSON 序列化器（不依赖 Unity JsonUtility 处理 Dictionary）
    // ================================================================

    public static class MiniJsonSerializer
    {
        public static string Serialize(Dictionary<string, object> dict)
        {
            if (dict == null) return "{}";
            var pairs = new List<string>();
            foreach (var kv in dict)
            {
                pairs.Add($"\"{EscapeString(kv.Key)}\": {SerializeValue(kv.Value)}");
            }
            return "{\n  " + string.Join(",\n  ", pairs) + "\n}";
        }

        private static string SerializeValue(object value)
        {
            if (value == null) return "null";
            if (value is string s) return $"\"{EscapeString(s)}\"";
            if (value is bool b) return b ? "true" : "false";
            if (value is int i) return i.ToString();
            if (value is long l) return l.ToString();
            if (value is float f) return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value is double dv) return dv.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value is List<string> ls) return "[" + string.Join(", ", ls.Select(x => $"\"{EscapeString(x)}\"")) + "]";
            if (value is Dictionary<string, object> d) return Serialize(d);
            if (value is System.Collections.IList list)
            {
                var items = new List<string>();
                foreach (var item in list) items.Add(SerializeValue(item));
                return "[" + string.Join(", ", items) + "]";
            }
            return $"\"{EscapeString(value.ToString())}\"";
        }

        private static string EscapeString(string s)
        {
            return (s ?? "")
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
