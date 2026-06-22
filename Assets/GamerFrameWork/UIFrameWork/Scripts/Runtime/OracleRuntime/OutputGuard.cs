using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GamerFrameWork.OracleRuntime
{
    /// <summary>
    /// 输出守卫 —— 检查 AI 回复是否符合质量+安全要求
    /// 对应 runtime-reference outputGuard.mjs
    /// </summary>
    public static class OutputGuard
    {
        public static readonly string[] OUTPUT_GUARD_ISSUES =
        {
            "too_many_sentences", "too_many_words", "wrong_language",
            "unlocked_card", "non_current_card", "absolute_prediction",
            "third_party_mind_claim", "medical_claim", "legal_claim",
            "therapy_claim", "clinical_diagnosis", "manipulative_fomo",
            "fear_based_retention", "shaming_language", "listen_final_verdict"
        };

        private static readonly Regex AbsolutePredictionPattern = new Regex(
            @"(一定|肯定会|绝对不会|必然|注定|命运已定|definitely|absolutely.*will|destiny.*determined)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ThirdPartyMindPattern = new Regex(
            @"((他|她|ta|对方).{0,8}(心里|内心|暗地里|其实|偷偷|真的|肯定|一定|放不下).{0,12}(喜欢|爱|想|讨厌|在乎|放不下|忘不了|不爱)|\b(he|she|they|morgan|ava|alex)\b[^.!?。！？]{0,80}\b(secretly feels|secretly loves|secretly hates|knows deep down|is hiding that|does not care about you)\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex MedicalClaimPattern = new Regex(
            @"(诊断|症状|病因|治疗|开药|处方|你.*患有|你得了)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LegalClaimPattern = new Regex(
            @"(起诉|打官司|法院会|法官会|这违法|法律建议|you should sue|file a lawsuit|legal advice|court will)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TherapyClaimPattern = new Regex(
            @"(治疗师|心理咨询师|临床|counselor|therapist|clinician)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ClinicalDiagnosisPattern = new Regex(
            @"(你有抑郁症|你是焦虑症|人格障碍|创伤后应激|抑郁症患者|clinical diagnosis|you have depression|you have anxiety)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex FomoPattern = new Regex(
            @"(如果不.*占卜|不抽牌.*会|错过|再不.*就晚了|only.*chance)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ShamingPattern = new Regex(
            @"(你太敏感|你就是想太多|你活该|你怎么这么|you are too needy|your fault)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ListenVerdictPattern = new Regex(
            @"(结论是|最终答案是|牌的意思就是|所以你应该分手|所以你应该发|verdict.*is|final.*answer)");

        /// <summary>
        /// 检查输出质量
        /// </summary>
        public static OutputGuardResult Check(string text, OutputGuardOptions options = null)
        {
            options = options ?? new OutputGuardOptions();
            var issues = new List<string>();
            var contract = options.responseContract ?? ResponseContracts.GetFor(options.stage ?? "listen");

            var metrics = new OutputMetrics
            {
                sentenceCount = CountSentences(text),
                wordCount = CountWords(text),
                cjkCount = CountCjk(text)
            };

            // 长度检查
            if (contract.maxSentences > 0 && metrics.sentenceCount > contract.maxSentences)
                issues.Add("too_many_sentences");
            if (contract.maxWords > 0 && metrics.wordCount > contract.maxWords)
                issues.Add("too_many_words");

            // 语言检查（中文模式不检查语言）
            // 安全内容检查
            if (AbsolutePredictionPattern.IsMatch(text))
                issues.Add("absolute_prediction");
            if (ThirdPartyMindPattern.IsMatch(text))
                issues.Add("third_party_mind_claim");
            if (MedicalClaimPattern.IsMatch(text))
                issues.Add("medical_claim");
            if (LegalClaimPattern.IsMatch(text))
                issues.Add("legal_claim");
            if (TherapyClaimPattern.IsMatch(text))
                issues.Add("therapy_claim");
            if (ClinicalDiagnosisPattern.IsMatch(text))
                issues.Add("clinical_diagnosis");
            if (FomoPattern.IsMatch(text))
                issues.Add("manipulative_fomo");
            if (ShamingPattern.IsMatch(text))
                issues.Add("shaming_language");

            // 阶段特定检查
            if (options.stage == "listen" && ListenVerdictPattern.IsMatch(text))
                issues.Add("listen_final_verdict");

            // 阅读锁检查（如果处于占卜中）
            if (options.readingLock != null && options.readingLock.locked && options.deckCardNames != null)
            {
                if (ReadingLockBuilder.MentionsUnlockedCard(text, options.readingLock, options.deckCardNames))
                    issues.Add("unlocked_card");
                if (!string.IsNullOrEmpty(options.currentCardId) &&
                    MentionsLockedCardOtherThan(text, options.readingLock, options.currentCardId))
                    issues.Add("non_current_card");
            }

            return new OutputGuardResult
            {
                ok = issues.Count == 0,
                issues = issues,
                metrics = metrics
            };
        }

        public static int CountSentences(string text)
        {
            var value = (text ?? "").Trim();
            if (string.IsNullOrEmpty(value)) return 0;
            var matches = Regex.Matches(value, @"[.!?。！？；;]+");
            var tailWithoutPunctuation = Regex.IsMatch(value, @"[A-Za-z0-9\u4e00-\u9fff]$") ? 1 : 0;
            return matches.Count + tailWithoutPunctuation;
        }

        public static int CountWords(string text)
        {
            var value = (text ?? "").Trim();
            if (string.IsNullOrEmpty(value)) return 0;
            var latinMatches = Regex.Matches(value, @"[A-Za-z0-9']+");
            var cjkMatches = Regex.Matches(value, @"[\u4e00-\u9fff]");
            return latinMatches.Count + cjkMatches.Count;
        }

        private static int CountCjk(string text)
        {
            return Regex.Matches(text ?? "", @"[\u4e00-\u9fff]").Count;
        }

        private static bool MentionsLockedCardOtherThan(string text, ReadingLock readingLock, string currentCardId)
        {
            if (string.IsNullOrEmpty(text) || readingLock?.allowedCards == null) return false;
            string value = text.ToLowerInvariant();
            string current = (currentCardId ?? "").ToLowerInvariant();

            foreach (LockedCard card in readingLock.allowedCards)
            {
                if (card == null) continue;
                string id = (card.cardId ?? "").ToLowerInvariant();
                if (string.IsNullOrEmpty(id) || id == current) continue;

                var aliases = new List<string> { card.cardId, card.cardName }
                    .Where(alias => !string.IsNullOrWhiteSpace(alias) && alias.Length > 1)
                    .Select(alias => alias.ToLowerInvariant());

                if (aliases.Any(alias => value.Contains(alias)))
                    return true;
            }

            return false;
        }
    }

    public class OutputGuardOptions
    {
        public string stage;
        public string responseMode;
        public string locale = "zh-CN";
        public ResponseContract responseContract;
        public ReadingLock readingLock;
        public List<string> deckCardNames;
        public string currentCardId;
    }

    public class OutputGuardResult
    {
        public bool ok;
        public List<string> issues;
        public OutputMetrics metrics;
    }

    public class OutputMetrics
    {
        public int sentenceCount;
        public int wordCount;
        public int cjkCount;
    }

    /// <summary>
    /// 响应契约 —— 不同阶段/模式的回复约束
    /// 对应 runtime-reference persona/responseContracts.mjs
    /// </summary>
    public static class ResponseContracts
    {
        private static readonly Dictionary<string, ResponseContract> Contracts;

        static ResponseContracts()
        {
            Contracts = new Dictionary<string, ResponseContract>
            {
                ["listen"] = new ResponseContract
                {
                    stage = "listen", responseMode = "micro_chat",
                    maxSentences = 2, maxWords = 80,
                    mustDo = new List<string> { "回声用户一个具体细节", "命名情绪张力但不诊断", "给一句人话判断", "关系等待/联系问题给具体边界" },
                    mustNot = new List<string> { "不要给最终判决", "不要诊断用户", "不要声称知道第三方秘密心理", "不要说回到自己/倾听内心等空话" },
                    nextActionPolicy = "chips"
                },
                ["clarify"] = new ResponseContract
                {
                    stage = "clarify", responseMode = "micro_chat",
                    maxSentences = 2, maxWords = 80,
                    mustDo = new List<string> { "问一个开放但有用的问题，或给三个清楚选项", "收窄占卜问题" },
                    mustNot = new List<string> { "不要审问用户", "不要强迫抽牌", "不要提前给结论" },
                    nextActionPolicy = "chips"
                },
                ["before_draw"] = new ResponseContract
                {
                    stage = "before_draw", responseMode = "ritual_prompt",
                    maxSentences = 3, maxWords = 110,
                    mustDo = new List<string> { "分开事实和故事", "解释为什么这个牌阵适合当前问题", "邀请而非强迫" },
                    mustNot = new List<string> { "不要说结果是命运", "不要承诺确定性", "不要透露或编造牌名" },
                    nextActionPolicy = "one"
                },
                ["card_reveal"] = new ResponseContract
                {
                    stage = "card_reveal", responseMode = "card_reveal",
                    maxSentences = 3, maxWords = 120,
                    mustDo = new List<string> { "只解释当前翻开的牌和位置", "连接到用户主体性", "避免提未翻开的牌" },
                    mustNot = new List<string> { "不要提前总结全牌阵", "不要提未锁定牌" },
                    nextActionPolicy = "one"
                },
                ["verdict"] = new ResponseContract
                {
                    stage = "verdict", responseMode = "oracle_verdict",
                    maxSentences = 4, maxWords = 160,
                    mustDo = new List<string> { "给一句简短温柔但直接的真相", "像朋友说话而不是牌义手册", "给一个24小时行动", "给追问/明日回看/深入出口" },
                    mustNot = new List<string> { "不要绝对预测", "不要恐吓留存", "不要羞辱用户" },
                    nextActionPolicy = "chips"
                },
                ["micro_action"] = new ResponseContract
                {
                    stage = "micro_action", responseMode = "follow_up",
                    maxSentences = 2, maxWords = 80,
                    mustDo = new List<string> { "给一个24小时内能做的具体做/不做动作" },
                    mustNot = new List<string> { "不要说靠近/稳定/倾听内心等抽象动作", "不要暗示不回来就失败" },
                    nextActionPolicy = "chips"
                },
                ["follow_up"] = new ResponseContract
                {
                    stage = "follow_up", responseMode = "follow_up",
                    maxSentences = 2, maxWords = 90,
                    mustDo = new List<string> { "先直接回答追问", "承接当前 readingId 或关系上下文", "除非用户要求不要重新抽牌" },
                    mustNot = new List<string> { "不要重启整次占卜", "不要编造新牌" },
                    nextActionPolicy = "chips"
                },
                ["recall"] = new ResponseContract
                {
                    stage = "recall", responseMode = "follow_up",
                    maxSentences = 2, maxWords = 90,
                    mustDo = new List<string> { "承接昨天的开放循环", "让回归感觉低压力且可选" },
                    mustNot = new List<string> { "使用 FOMO 语言", "暗示用户错过了唯一的机会" },
                    nextActionPolicy = "chips"
                },
                ["dive_deeper"] = new ResponseContract
                {
                    stage = "dive_deeper", responseMode = "dive_deeper",
                    maxSentences = 10, maxWords = 360,
                    mustDo = new List<string> { "只在明确深入动作后展开", "保持锁牌边界", "更深入但不要重复旧结论" },
                    mustNot = new List<string> { "不要挡住基础短解读", "不要引入锁牌外的新牌" },
                    nextActionPolicy = "chips"
                },
            };
        }

        public static ResponseContract GetFor(string stage)
        {
            OracleTypes.AssertOneOf(stage, OracleTypes.ORACLE_STAGES, "stage");
            return Contracts.TryGetValue(stage, out var contract)
                ? contract
                : Contracts["listen"];
        }
    }
}
