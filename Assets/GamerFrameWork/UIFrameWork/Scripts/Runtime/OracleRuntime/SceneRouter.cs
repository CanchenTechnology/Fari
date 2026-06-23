using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GamerFrameWork.OracleRuntime
{
    /// <summary>
    /// 场景推断器 —— 从用户文本推断当前对话场景
    /// 对应 runtime-reference planner/scenePlanner.mjs 的 inferOracleScene()
    /// </summary>
    public static class SceneInferrer
    {
        public static (string scene, List<string> evidence) Infer(string text,
            string activeRelationshipId = null, string entrySource = null,
            List<string> riskFlags = null)
        {
            // 风险强制覆盖
            var forcedByRisk = RiskClassifier.SceneForRisk(riskFlags);
            if (!string.IsNullOrEmpty(forcedByRisk))
                return (forcedByRisk, new List<string> { "risk" });

            var value = (text ?? "").ToLowerInvariant();
            var evidence = new List<string>();

            // 每日神谕入口
            if (entrySource == "daily" ||
                Regex.IsMatch(value, @"(今日牌|每日占卜|今日神谕|daily.*oracle|today.*card)"))
            {
                evidence.Add("daily");
                return ("daily_reflection", evidence);
            }

            // 关系相关（最高频场景）
            if (!string.IsNullOrEmpty(activeRelationshipId) ||
                Regex.IsMatch(value, @"关系|感情|喜欢|回应|联系|复合|暧昧|对方|前任|回应|消息|发信息|联系"))
            {
                evidence.Add("relationship");
                if (Regex.IsMatch(value, @"前任|分手|复合|回头|ex|breakup|broke.*up|reconcile|relapse"))
                    return ("breakup_relapse", evidence);
                return ("relationship_anxiety", evidence);
            }

            // 友情
            if (Regex.IsMatch(value, @"朋友|友情|室友|群聊|人际|friend|friendship|roommate|bestie"))
            {
                evidence.Add("friendship");
                return ("friendship", evidence);
            }

            // 事业
            if (Regex.IsMatch(value, @"工作|事业|老板|同事|项目|升职|职业|跳槽|面试|job|career|offer|boss|work|promotion"))
            {
                evidence.Add("career");
                return ("career_uncertainty", evidence);
            }

            // 自我价值
            if (Regex.IsMatch(value, @"不够好|自我价值|讨厌自己|自信|羞愧|不配|self.*worth|not.*enough|shame|unlovable"))
            {
                evidence.Add("self_worth");
                return ("self_worth", evidence);
            }

            // 暧昧不明
            if (Regex.IsMatch(value, @"迷茫|不清楚|暧昧|复杂|混乱|confused|ambiguous|mixed.*signal|complicated"))
            {
                evidence.Add("ambiguous");
                return ("ambiguous_situation", evidence);
            }

            return ("general_chat", new List<string> { "default" });
        }
    }

    /// <summary>
    /// 阶段路由器 —— 决定当前对话处于哪个阶段
    /// 对应 runtime-reference planner/stageRouter.mjs 的 routeStage()
    /// </summary>
    public static class StageRouter
    {
        private static readonly Dictionary<string, string> StageResponseModeMap = new Dictionary<string, string>
        {
            { "listen", "micro_chat" },
            { "clarify", "micro_chat" },
            { "before_draw", "ritual_prompt" },
            { "card_reveal", "card_reveal" },
            { "verdict", "oracle_verdict" },
            { "micro_action", "follow_up" },
            { "follow_up", "follow_up" },
            { "recall", "follow_up" },
            { "dive_deeper", "dive_deeper" },
        };

        public static (string stage, string responseMode, string reason) Route(
            string text, string scene, string actionKind = null,
            string activeReadingState = null, string activeReadingId = null,
            string activeRelationshipId = null, bool hasTomorrowHook = false,
            bool isReturningFromHook = false, int messageCount = 0,
            List<string> riskFlags = null)
        {
            // 高风险 → 强制聆听阶段
            if (riskFlags != null && (riskFlags.Contains("self_harm") || riskFlags.Contains("minor_safety")))
                return MakeResult("listen", "high_risk_safety");

            // 明日提醒回调
            if (isReturningFromHook || hasTomorrowHook || actionKind == "open_tomorrow_hook")
                return MakeResult("recall", "tomorrow_hook");

            // 明确的动作类型路由
            if (actionKind == "dive_deeper")
                return MakeResult("dive_deeper", "explicit_dive_deeper");

            if (actionKind == "reveal_card")
                return MakeResult("card_reveal", "explicit_reveal");

            if (actionKind == "complete_verdict")
                return MakeResult("verdict", "explicit_verdict");

            if (actionKind == "start_spread" || actionKind == "plan_spread" ||
                actionKind == "draw_one_card" || actionKind == "start_three_card")
                return MakeResult("before_draw", "explicit_spread_action");

            // 占卜进行中
            if (activeReadingState == "cards_locked" || activeReadingState == "revealing")
                return MakeResult("card_reveal", "reading_in_progress");

            if (activeReadingState == "generating_verdict" || activeReadingState == "fallback_verdict")
                return MakeResult("verdict", "reading_ready_for_verdict");

            if (activeReadingState == "completed" || !string.IsNullOrEmpty(activeReadingId))
                return MakeResult("follow_up", "reading_continuity");

            // 普通决策焦虑不等于占卜意图：先给人话判断/澄清，不自动推牌。
            if (!HasDivinationIntent(text) && IsDecisionOrActionQuestion(text))
            {
                if (scene == "relationship_anxiety" || scene == "breakup_relapse" ||
                    scene == "ambiguous_situation" || scene == "career_uncertainty")
                {
                    return messageCount > 0
                        ? MakeResult("micro_action", "non_tarot_decision_action")
                        : MakeResult("clarify", "non_tarot_decision_clarify");
                }
            }

            // 用户明确要求占卜/抽牌/牌阵时才进入占卜流程
            if (HasDivinationIntent(text))
            {
                if (!string.IsNullOrEmpty(activeRelationshipId) && IsRelationshipDivinationRequest(text))
                    return MakeResult("before_draw", "relationship_divination_request");

                if (IsImmediateDrawRequest(text))
                    return MakeResult("before_draw", "direct_draw_request");

                if (!string.IsNullOrEmpty(activeRelationshipId) ||
                    scene == "relationship_anxiety" || scene == "career_uncertainty")
                    return MakeResult("clarify", "needs_question_narrowing");

                return MakeResult("before_draw", "general_divination_request");
            }

            // 用户请求具体行动建议
            if (messageCount > 0 && Regex.IsMatch(text ?? "",
                @"(我该怎么办|怎么办|该如何|该怎么|what.*should.*i.*do|next.*step|how.*respond)",
                RegexOptions.IgnoreCase))
                return MakeResult("micro_action", "action_request");

            return MakeResult("listen", "default_listen");
        }

        public static bool HasDivinationIntent(string text)
        {
            return Regex.IsMatch(text ?? "",
                @"占卜|塔罗|抽牌|牌阵|神谕|建议.*牌|用牌|看牌|card|tarot|spread|reading|oracle|pull.*card|draw.*card",
                RegexOptions.IgnoreCase);
        }

        private static bool IsDecisionOrActionQuestion(string text)
        {
            return Regex.IsMatch(text ?? "",
                @"(该不该|要不要|会不会|怎么办|该如何|该怎么|能不能|要怎么|走向|还有没有可能|what.*should.*i.*do|should.*i|will.*they|next.*step|how.*respond)",
                RegexOptions.IgnoreCase);
        }

        private static bool IsImmediateDrawRequest(string text)
        {
            return Regex.IsMatch(text ?? "",
                @"开始.*(占卜|牌阵)|抽.*牌|翻.*牌|pull|draw|start.*(card|spread|reading)",
                RegexOptions.IgnoreCase);
        }

        private static bool IsRelationshipDivinationRequest(string text)
        {
            return Regex.IsMatch(text ?? "",
                @"((双人|关系|感情|喜欢|暧昧|复合|联系|好友|朋友|对方|他|她|ta|TA|我们|跟|和).*(占卜|塔罗|抽牌|牌阵|看牌|神谕|reading|tarot|spread|oracle))|((占卜|塔罗|抽牌|牌阵|看牌|神谕|reading|tarot|spread|oracle).*(双人|关系|感情|喜欢|暧昧|复合|联系|好友|朋友|对方|他|她|ta|TA|我们|跟|和))",
                RegexOptions.IgnoreCase);
        }

        private static (string stage, string responseMode, string reason) MakeResult(string stage, string reason)
        {
            OracleTypes.AssertOneOf(stage, OracleTypes.ORACLE_STAGES, "stage");
            var responseMode = StageResponseModeMap.TryGetValue(stage, out var mode) ? mode : "micro_chat";
            return (stage, responseMode, reason);
        }
    }
}
