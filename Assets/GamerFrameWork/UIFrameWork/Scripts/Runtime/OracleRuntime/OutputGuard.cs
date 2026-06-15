using System.Collections.Generic;
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
            @"(他|她|ta)(心里|内心|暗地里|其实|偷偷|真的)(喜欢|爱|想|讨厌)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex MedicalClaimPattern = new Regex(
            @"(诊断|症状|病因|治疗|开药|处方|你.*患有|你得了)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TherapyClaimPattern = new Regex(
            @"(治疗师|心理咨询师|临床|counselor|therapist|clinician)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex FomoPattern = new Regex(
            @"(如果不.*占卜|不抽牌.*会|错过|再不.*就晚了|only.*chance)",
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
            if (TherapyClaimPattern.IsMatch(text))
                issues.Add("therapy_claim");
            if (FomoPattern.IsMatch(text))
                issues.Add("manipulative_fomo");

            // 阶段特定检查
            if (options.stage == "listen" && ListenVerdictPattern.IsMatch(text))
                issues.Add("listen_final_verdict");

            // 阅读锁检查（如果处于占卜中）
            if (options.readingLock != null && options.readingLock.locked && options.deckCardNames != null)
            {
                if (ReadingLockBuilder.MentionsUnlockedCard(text, options.readingLock, options.deckCardNames))
                    issues.Add("unlocked_card");
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
                    maxSentences = 3, maxWords = 120,
                    mustDo = new List<string> { "先认可情绪", "不要急着给结论" },
                    mustNot = new List<string> { "不要问检测性问题", "不要给最终判断" },
                    nextActionPolicy = "one"
                },
                ["clarify"] = new ResponseContract
                {
                    stage = "clarify", responseMode = "micro_chat",
                    maxSentences = 3, maxWords = 120,
                    mustDo = new List<string> { "问一个有用的澄清问题", "给具体选项" },
                    mustNot = new List<string> { "不要跳过澄清直接给方案" },
                    nextActionPolicy = "chips"
                },
                ["before_draw"] = new ResponseContract
                {
                    stage = "before_draw", responseMode = "ritual_prompt",
                    maxSentences = 4, maxWords = 150,
                    mustDo = new List<string> { "解释为什么这个牌阵适合当前问题", "邀请而非强迫" },
                    mustNot = new List<string> { "不要直接抽牌", "不要跳过解释框架" },
                    nextActionPolicy = "chips"
                },
                ["card_reveal"] = new ResponseContract
                {
                    stage = "card_reveal", responseMode = "card_reveal",
                    maxSentences = 3, maxWords = 150,
                    mustDo = new List<string> { "只解释当前翻开的牌和位置", "不要提未翻开的牌" },
                    mustNot = new List<string> { "不要给出整体结论", "不要提其他位置的牌" },
                    nextActionPolicy = "chips"
                },
                ["verdict"] = new ResponseContract
                {
                    stage = "verdict", responseMode = "oracle_verdict",
                    maxSentences = 5, maxWords = 200,
                    mustDo = new List<string> { "给一句简短真相", "给一个24小时行动", "基于所有已翻牌综合解读" },
                    mustNot = new List<string> { "不要重复解释每张牌", "不要做绝对预测" },
                    nextActionPolicy = "chips"
                },
                ["micro_action"] = new ResponseContract
                {
                    stage = "micro_action", responseMode = "follow_up",
                    maxSentences = 3, maxWords = 120,
                    mustDo = new List<string> { "给一个行为级可执行的动作", "要具体不要抽象" },
                    mustNot = new List<string> { "不要只说'慢慢来'、'回到自己'等空话" },
                    nextActionPolicy = "one"
                },
                ["follow_up"] = new ResponseContract
                {
                    stage = "follow_up", responseMode = "follow_up",
                    maxSentences = 3, maxWords = 150,
                    mustDo = new List<string> { "基于已有 readingId 延续，不要重新抽牌" },
                    mustNot = new List<string> { "不要引入新牌" },
                    nextActionPolicy = "chips"
                },
                ["dive_deeper"] = new ResponseContract
                {
                    stage = "dive_deeper", responseMode = "dive_deeper",
                    maxSentences = 6, maxWords = 300,
                    mustDo = new List<string> { "更深入展开但保持精炼" },
                    mustNot = new List<string> { "不要重复之前的结论" },
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
