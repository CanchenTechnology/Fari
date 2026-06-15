using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GamerFrameWork.OracleRuntime
{
    /// <summary>
    /// 风险分类器 —— 检测用户消息中的安全风险标记
    /// 对应 runtime-reference planner/riskClassifier.mjs
    /// </summary>
    public static class RiskClassifier
    {
        private static readonly Regex SelfHarmPattern = new Regex(
            @"自杀|自残|自伤|不想活|结束生命|kill myself|suicide|self[- ]?harm",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex MedicalPattern = new Regex(
            @"诊断|开药|剂量|药物|症状是不是|是不是抑郁症|是不是焦虑症|diagnose|prescription|symptom|disorder|illness",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LegalPattern = new Regex(
            @"起诉|打官司|律师|判刑|合法吗|违法|sue|legal|illegal|lawyer|court|sentence",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex MinorSafetyPattern = new Regex(
            @"未成年|小孩|孩子.*安全|child|minor|underage",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PrivacySensitivePattern = new Regex(
            @"身份证|银行卡|密码|泄露|privacy|password|leak|exposed",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static List<string> ClassifyRisk(string text, string activeRelationshipConsent = null)
        {
            var flags = new List<string>();
            if (string.IsNullOrEmpty(text)) return flags;

            if (SelfHarmPattern.IsMatch(text)) flags.Add("self_harm");
            if (MedicalPattern.IsMatch(text)) flags.Add("medical");
            if (LegalPattern.IsMatch(text)) flags.Add("legal");
            if (MinorSafetyPattern.IsMatch(text)) flags.Add("minor_safety");
            if (PrivacySensitivePattern.IsMatch(text)) flags.Add("privacy_sensitive");

            // 检测第三方断言 —— 声称知道某个不在对话中的人的想法
            if (HasThirdPartyClaim(text))
                flags.Add("third_party_claim");

            return flags;
        }

        private static bool HasThirdPartyClaim(string text)
        {
            // 检测 "他/她觉得""他/她在想""我朋友说" 等第三方内心状态断言
            var thirdPartyPattern = new Regex(
                @"(他|她|ta|他/她)(一定|肯定|绝对|就是|在|会|觉得|认为|想|喜欢|讨厌|恨).{0,20}(我|的)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return thirdPartyPattern.IsMatch(text);
        }

        public static string RiskLevel(List<string> riskFlags)
        {
            if (riskFlags == null || riskFlags.Count == 0) return "none";
            if (riskFlags.Contains("self_harm") || riskFlags.Contains("minor_safety")) return "high";
            if (riskFlags.Contains("medical") || riskFlags.Contains("legal") || riskFlags.Contains("privacy_sensitive")) return "medium";
            return "low";
        }

        public static string SceneForRisk(List<string> riskFlags)
        {
            if (riskFlags == null || riskFlags.Count == 0) return null;
            if (riskFlags.Contains("self_harm") || riskFlags.Contains("minor_safety")) return "crisis";
            return null; // 其他风险不强制替换场景，但会调整阶段
        }

        public static string SafetyInstructionsForRisk(List<string> riskFlags)
        {
            var lines = new List<string>
            {
                "Do not diagnose, treat, or offer medical/legal advice.",
                "Do not make absolute predictions.",
                "Do not claim to know third-party inner thoughts.",
                "Do not use fear, shame, or FOMO."
            };

            if (riskFlags != null && riskFlags.Contains("self_harm"))
            {
                lines.Add("CRITICAL: User expressed distress. Prioritize safety. Encourage contacting trusted person or local emergency services. Do NOT continue with entertainment-style divination.");
            }
            if (riskFlags != null && riskFlags.Contains("medical"))
            {
                lines.Add("Medical content detected. Do NOT diagnose or suggest treatment. Recommend consulting a professional.");
            }
            if (riskFlags != null && riskFlags.Contains("third_party_claim"))
            {
                lines.Add("Do NOT claim to know what a third party secretly thinks or feels. Only speak to the user's experience and observed patterns.");
            }

            return string.Join("\n", lines);
        }
    }
}
