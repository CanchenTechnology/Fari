using System;
using System.Collections.Generic;
using UnityEngine;
using GamerFrameWork.OracleRuntime;

/// <summary>
/// 占卜会话状态
/// </summary>
public enum DivinationPhase
{
    Idle,               // 无活跃占卜
    QuestionReceived,   // 用户问了问题，AI 正在引导
    ChoosingSpread,     // AI 给出了牌阵选项，等待用户选择
    CardsLocked,        // 已抽牌，等待 AI 揭晓
    Revealing,          // AI 正在逐一揭晓牌面
    GeneratingVerdict,  // AI 正在生成解读
    Completed,          // 占卜完成
    FollowUp            // 用户继续追问
}

/// <summary>
/// 占卜引擎 —— 管理塔罗占卜的完整生命周期
/// 对接 OracleRuntime 的 ReadingLock / DivinationPlan / SceneRouter
/// </summary>
public class DivinationEngine : MonoBehaviour
{
    public static DivinationEngine Instance { get; private set; }

    [Header("牌阵定义")]
    public SpreadDefinition[] SpreadDefinitions;

    /// <summary>今日每日牌（跨会话保持）</summary>
    public (TarotCard card, bool upright)? TodayCard { get; private set; }

    /// <summary>当前占卜会话</summary>
    public DivinationSession CurrentSession { get; private set; }

    /// <summary>当前占卜阶段</summary>
    public DivinationPhase CurrentPhase => CurrentSession?.phase ?? DivinationPhase.Idle;

    /// <summary>当前 ReadingState（供 OracleRuntime 使用）</summary>
    public string ActiveReadingState
    {
        get
        {
            if (CurrentSession == null) return "";
            return CurrentSession.phase switch
            {
                DivinationPhase.QuestionReceived => "starting",
                DivinationPhase.ChoosingSpread => "planned",
                DivinationPhase.CardsLocked => "cards_locked",
                DivinationPhase.Revealing => "revealing",
                DivinationPhase.GeneratingVerdict => "generating_verdict",
                DivinationPhase.Completed => "completed",
                DivinationPhase.FollowUp => "completed",
                _ => ""
            };
        }
    }

    /// <summary>当前 ActionKind（供 DialogSystem 使用）</summary>
    public string ActiveActionKind
    {
        get
        {
            if (CurrentSession == null) return "";
            return CurrentSession.phase switch
            {
                DivinationPhase.ChoosingSpread => "plan_spread",
                DivinationPhase.CardsLocked => "reveal_card",
                DivinationPhase.GeneratingVerdict => "complete_verdict",
                DivinationPhase.FollowUp => "dive_deeper",
                _ => ""
            };
        }
    }

    /// <summary>当前 ReadingLock（供 DialogSystem 使用）</summary>
    public ReadingLock ActiveReadingLock => CurrentSession?.readingLock;

    /// <summary>当前 ActiveReadingId（供 DialogSystem 使用）</summary>
    public string ActiveReadingId => CurrentSession?.readingId;

    private DialogSystem _dialogSystem;
    private string _todayCardDate; // 防止同一天多次生成

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _dialogSystem = DialogSystem.Instance;
        EnsureSpreadDefs();
    }

    /// <summary>
    /// 绘制每日塔罗牌（一天只生成一次）
    /// </summary>
    public (TarotCard card, bool upright) DrawDailyCard()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (TodayCard.HasValue && _todayCardDate == today)
            return TodayCard.Value;

        var result = TarotDeck.DrawOne();
        TodayCard = result;
        _todayCardDate = today;
        UsageStatsManager.Instance?.TrackDailyOracle();
        Debug.Log($"[DivinationEngine] 今日牌: {result.card.DisplayName(result.upright)}");
        return result;
    }

    /// <summary>
    /// 用云端保存的今日牌覆盖本地今日牌，保证跨设备同一天抽到同一张牌。
    /// </summary>
    public void SetTodayCardFromCloud(TarotCard card, bool upright, string localDate = null)
    {
        if (card == null) return;

        TodayCard = (card, upright);
        _todayCardDate = string.IsNullOrEmpty(localDate)
            ? DateTime.Now.ToString("yyyy-MM-dd")
            : localDate;
        UsageStatsManager.Instance?.TrackDailyOracle();
        Debug.Log($"[DivinationEngine] 使用云端今日牌: {card.DisplayName(upright)}");
    }

    public void ClearTodayCard()
    {
        TodayCard = null;
        _todayCardDate = null;
        DialogSystem.Instance?.SetTodayCardPayload(null);
        Debug.Log("[DivinationEngine] 今日牌缓存已清除");
    }

    /// <summary>
    /// 获取今日牌的 TodayCardPayload
    /// </summary>
    public TodayCardPayload GetTodayCardPayload()
    {
        if (!TodayCard.HasValue) return null;
        var (card, upright) = TodayCard.Value;
        return new TodayCardPayload
        {
            cardId = card.cardId,
            cardName = card.nameEn,
            displayName = card.DisplayName(upright),
            nameZh = card.nameZh,
            orientation = upright ? "upright" : "reversed",
            generatedAt = DateTime.Now.ToString("o"),
            title = "今日塔罗"
        };
    }

    /// <summary>
    /// 开始一次新的占卜
    /// </summary>
    public DivinationSession StartDivination(string question, string scene = null)
    {
        var session = new DivinationSession
        {
            readingId = Guid.NewGuid().ToString("N").Substring(0, 12),
            question = question,
            scene = scene ?? "relationship_anxiety",
            phase = DivinationPhase.QuestionReceived,
            createdAt = DateTime.Now.ToString("o")
        };

        CurrentSession = session;
        Debug.Log($"[DivinationEngine] 开始占卜 [{session.readingId}] 问题: {question}");

        // 同步到 DialogSystem
        if (_dialogSystem != null)
            SyncToDialogSystem();

        return session;
    }

    /// <summary>
    /// 快速占卜——直接进入选牌阵阶段（跳过问题澄清）
    /// </summary>
    public DivinationSession StartQuickDivination(string question)
    {
        var session = StartDivination(question);
        // 快速占卜直接进入选牌阵阶段
        session.phase = DivinationPhase.ChoosingSpread;
        session.spreadKind = PlanSpreadKind(question, session.scene);
        Debug.Log($"[DivinationEngine] 快速占卜——跳过澄清，直接选牌阵");
        SyncToDialogSystem();
        return session;
    }

    /// <summary>
    /// 将 OracleRuntime 自动规划出的 DivinationPlan 同步为当前可交互牌阵会话。
    /// 只规划，不抽牌；真正抽牌仍由 SelectSpread / 洗牌 UI 完成并生成 ReadingLock。
    /// </summary>
    public DivinationSession ApplyRuntimeDivinationPlan(DivinationPlan plan)
    {
        if (plan == null) return CurrentSession;

        if (CurrentSession != null &&
            CurrentSession.readingLock != null &&
            (CurrentSession.phase == DivinationPhase.CardsLocked ||
             CurrentSession.phase == DivinationPhase.Revealing ||
             CurrentSession.phase == DivinationPhase.GeneratingVerdict ||
             CurrentSession.phase == DivinationPhase.Completed))
        {
            return CurrentSession;
        }

        var session = CurrentSession ?? new DivinationSession
        {
            readingId = Guid.NewGuid().ToString("N").Substring(0, 12),
            createdAt = DateTime.Now.ToString("o")
        };

        session.question = string.IsNullOrWhiteSpace(plan.question) ? session.question : plan.question;
        session.scene = string.IsNullOrWhiteSpace(plan.scene) ? session.scene : plan.scene;
        session.phase = DivinationPhase.ChoosingSpread;
        session.spreadKind = plan.spreadKind;
        session.divinationPlan = plan;

        CurrentSession = session;
        Debug.Log($"[DivinationEngine] 应用 Runtime DivinationPlan [{plan.planId}], spreadKind={plan.spreadKind}, cardCount={plan.cardCount}");
        SyncToDialogSystem();
        return session;
    }

    /// <summary>
    /// 根据问题和场景选择合适牌阵。只做规划，不抽牌；抽牌必须走 SelectSpread 并生成 ReadingLock。
    /// </summary>
    public string PlanSpreadKind(string question, string scene = null)
    {
        EnsureSpreadDefs();
        string text = (question ?? "").ToLowerInvariant();
        string currentScene = scene ?? CurrentSession?.scene ?? "";

        if (text.Contains("深入") || text.Contains("完整") || text.Contains("全貌") ||
            text.Contains("整体") || text.Contains("复杂") || text.Contains("很多") ||
            text.Contains("deep reading") || text.Contains("full picture"))
            return HasSpread("celtic_inspired_deep_cross") ? "celtic_inspired_deep_cross" : SpreadDefinitions[0].kind;

        if (text.Contains("自我价值") || text.Contains("羞耻") || text.Contains("不够好") ||
            text.Contains("讨厌自己") || text.Contains("睡前") || text.Contains("修复自己") ||
            text.Contains("照顾自己") || text.Contains("self worth") || text.Contains("self-care"))
            return HasSpread("self_repair") ? "self_repair" : SpreadDefinitions[0].kind;

        if (text.Contains("未来") || text.Contains("下个月") || text.Contains("长期") ||
            text.Contains("时间线") || text.Contains("七天") || text.Contains("7天") ||
            text.Contains("复合") || text.Contains("回来") || text.Contains("走向") ||
            text.Contains("timeline") || text.Contains("long term"))
            return HasSpread("timeline_thread") ? "timeline_thread" : SpreadDefinitions[0].kind;

        if (text.Contains("反复") || text.Contains("总是") || text.Contains("一直") ||
            text.Contains("模式") || text.Contains("循环") || text.Contains("拉扯") ||
            text.Contains("重复") || text.Contains("为什么每次"))
            return HasSpread("pattern_map") ? "pattern_map" : SpreadDefinitions[0].kind;

        if (text.Contains("要不要") || text.Contains("该不该") || text.Contains("选择") ||
            text.Contains("二选一") || text.Contains("option") || text.Contains("choose"))
            return HasSpread("choice_gate") ? "choice_gate" : SpreadDefinitions[0].kind;

        if (currentScene == "relationship_anxiety" || currentScene == "breakup_relapse" ||
            text.Contains("关系") || text.Contains("喜欢") || text.Contains("复合") ||
            text.Contains("暧昧") || text.Contains("前任") || text.Contains("对方"))
            return HasSpread("relationship_tension") ? "relationship_tension" : SpreadDefinitions[0].kind;

        if (currentScene == "career_uncertainty")
            return HasSpread("pattern_map") ? "pattern_map" : SpreadDefinitions[0].kind;

        if (text.Contains("一张") || text.Contains("快速") || text.Contains("此刻") || text.Contains("现在"))
            return HasSpread("mirror_card") ? "mirror_card" : SpreadDefinitions[0].kind;

        return HasSpread("mirror_card") ? "mirror_card" : SpreadDefinitions[0].kind;
    }

    /// <summary>
    /// 选择牌阵并抽牌
    /// </summary>
    public List<LockedCard> SelectSpread(string spreadKind)
    {
        if (CurrentSession == null)
        {
            Debug.LogWarning("[DivinationEngine] No active session for spread selection.");
            return new List<LockedCard>();
        }

        var spreadDef = GetSpreadDefinition(spreadKind);
        if (spreadDef == null)
        {
            Debug.LogError($"[DivinationEngine] Unknown spread kind: {spreadKind}");
            return new List<LockedCard>();
        }

        CurrentSession.divinationPlan = BuildDivinationPlan(spreadDef);

        int cardCount = spreadDef.cardCount;
        var draws = TarotDeck.DrawMultiple(cardCount);

        var lockedCards = new List<LockedCard>();
        for (int i = 0; i < draws.Count; i++)
        {
            var (card, upright) = draws[i];
            string positionKey = i < spreadDef.positions.Count ? spreadDef.positions[i].key : $"pos_{i + 1}";
            string positionLabel = i < spreadDef.positions.Count ? spreadDef.positions[i].label : $"第{i + 1}张";

            lockedCards.Add(new LockedCard
            {
                positionKey = positionKey,
                position = positionLabel,
                cardId = card.cardId,
                cardName = card.nameZh,
                orientation = upright ? "upright" : "reversed"
            });
        }

        // 创建 ReadingLock
        CurrentSession.readingLock = new ReadingLock
        {
            readingId = CurrentSession.readingId,
            readingType = spreadKind,
            allowedCards = lockedCards,
            locked = true
        };

        CurrentSession.spreadKind = spreadKind;
        CurrentSession.lockedCards = lockedCards;
        CurrentSession.phase = DivinationPhase.CardsLocked;
        UsageStatsManager.Instance?.TrackSpreadReading();

        Debug.Log($"[DivinationEngine] 选牌阵 [{spreadKind}], 抽 {cardCount} 张牌");
        foreach (var lc in lockedCards)
            Debug.Log($"  {lc.position}: {TarotDeck.FormatDisplayName(lc.cardName, lc.orientation)}");

        // 同步到 DialogSystem
        SyncToDialogSystem();

        return lockedCards;
    }

    private DivinationPlan BuildDivinationPlan(SpreadDefinition spreadDef)
    {
        if (spreadDef == null || CurrentSession == null) return null;
        return new DivinationPlan
        {
            planId = Guid.NewGuid().ToString("N").Substring(0, 12),
            userId = "",
            conversationId = "dialog",
            question = CurrentSession.question ?? "",
            scene = CurrentSession.scene ?? "",
            spreadKind = spreadDef.kind,
            cardCount = spreadDef.cardCount,
            complexity = spreadDef.cardCount <= 1 ? "light" : spreadDef.cardCount >= 5 ? "deep" : "standard",
            positions = spreadDef.positions != null
                ? new List<SpreadPosition>(spreadDef.positions)
                : new List<SpreadPosition>(),
            reasonForSpread = BuildSpreadReason(spreadDef.kind),
            professionalFrame = new List<string> { "牌是镜子，不是绝对预测", "只根据用户问题和已锁定牌面解读" },
            requiresProForFullReading = spreadDef.cardCount >= 5,
            createdAt = DateTime.Now.ToString("o")
        };
    }

    /// <summary>
    /// 为当前会话生成牌阵规划。供手动洗牌/互动牌阵 UI 在锁牌前补齐 Runtime 上下文。
    /// </summary>
    public DivinationPlan BuildActiveDivinationPlan(string spreadKind)
    {
        if (CurrentSession == null) return null;
        string resolvedKind = string.IsNullOrEmpty(spreadKind)
            ? CurrentSession.spreadKind
            : spreadKind;
        return BuildDivinationPlan(GetSpreadDefinition(resolvedKind));
    }

    private string BuildSpreadReason(string spreadKind)
    {
        return spreadKind switch
        {
            "mirror_card" => "问题需要先看清此刻核心状态。",
            "relationship_tension" => "关系问题需要区分用户位置、对方动态和互动张力。",
            "choice_gate" => "选择题需要并列比较两个方向，而不是直接替用户决定。",
            "pattern_map" => "反复出现的困境需要看过去影响、当前模式和可改变的行动。",
            "timeline_thread" => "带有时间线的问题需要看当前路径的倾向，而不是把未来说成定局。",
            "self_repair" => "自我议题需要从看见、放下到行动逐步落地。",
            "celtic_inspired_deep_cross" => "复杂局面需要看中心、压力、根部、外部场和干净行动。",
            _ => "这个牌阵能把问题拆成更容易行动的部分。"
        };
    }

    /// <summary>
    /// 标记牌面已揭晓 → 进入解读阶段
    /// </summary>
    public void RevealCards()
    {
        if (CurrentSession == null) return;
        CurrentSession.phase = DivinationPhase.GeneratingVerdict;
        Debug.Log("[DivinationEngine] 牌面已揭晓 → 生成解读");
        SyncToDialogSystem();
    }

    /// <summary>
    /// 标记占卜完成，并自动保存到 Firestore
    /// </summary>
    public void CompleteDivination(string shortVerdict = null, string fullResponse = null)
    {
        if (CurrentSession == null) return;
        CurrentSession.phase = DivinationPhase.Completed;
        CurrentSession.shortVerdict = shortVerdict ?? "";
        Debug.Log("[DivinationEngine] 占卜完成");
        SyncToDialogSystem();

        // 自动保存到 Firestore
        SaveCurrentSessionToFirestore(shortVerdict);
    }

    /// <summary>
    /// 将当前占卜会话保存到 Firestore
    /// </summary>
    private void SaveCurrentSessionToFirestore(string shortVerdict)
    {
        var firestore = DivinationRecordFirestore.Instance;
        if (firestore != null)
        {
            firestore.SaveFromSession(CurrentSession, shortVerdict, success =>
            {
                if (success)
                    Debug.Log($"[DivinationEngine] 占卜记录已同步至 Firestore: {CurrentSession.readingId}");
                else
                    Debug.LogWarning($"[DivinationEngine] 占卜记录已保存到本地缓存，云端稍后同步: {CurrentSession.readingId}");
            });
        }
        else
        {
            DivinationRecordData record = DivinationRecordBuilder.FromSession();
            if (record != null)
            {
                record.shortVerdict = shortVerdict ?? record.shortVerdict;
                DivinationRecordFirestore.SaveRecordLocal(record);
                Debug.LogWarning($"[DivinationEngine] 历史服务不可用，已保存占卜记录到本地缓存: {record.readingId}");
            }
            else
            {
                Debug.LogWarning("[DivinationEngine] 历史服务不可用，且当前会话无法构建历史记录");
            }
        }
    }

    /// <summary>
    /// 从历史记录恢复占卜会话（用于继续追问）
    /// </summary>
    public void RestoreSession(DivinationSession session)
    {
        if (session == null)
        {
            Debug.LogWarning("[DivinationEngine] RestoreSession: session is null");
            return;
        }
        CurrentSession = session;
        Debug.Log($"[DivinationEngine] 恢复占卜会话 [{session.readingId}] 问题: {session.question}");
        SyncToDialogSystem();
    }

    /// <summary>
    /// 用户继续追问
    /// </summary>
    public void EnterFollowUp()
    {
        if (CurrentSession == null) return;
        CurrentSession.phase = DivinationPhase.FollowUp;
        Debug.Log("[DivinationEngine] 进入追问模式");
        SyncToDialogSystem();
    }

    /// <summary>
    /// 创建明日钩子
    /// </summary>
    public TomorrowHook CreateTomorrowHook(string triggerText)
    {
        if (CurrentSession == null) return null;
        var hook = new TomorrowHook
        {
            hookId = Guid.NewGuid().ToString("N").Substring(0, 10),
            sourceReadingId = CurrentSession.readingId,
            hookType = "tomorrow_clue",
            triggerText = triggerText,
            scheduledForLocalDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"),
            status = "pending"
        };
        CurrentSession.tomorrowHook = hook;
        Debug.Log($"[DivinationEngine] 创建明日钩子: {triggerText}");
        return hook;
    }

    /// <summary>
    /// 清理占卜会话
    /// </summary>
    public void ClearSession()
    {
        CurrentSession = null;
        SyncToDialogSystem();
        Debug.Log("[DivinationEngine] 占卜会话已清除");
    }

    /// <summary>
    /// 获取牌阵选择提示文本（供 AI 选项使用）
    /// </summary>
    public string[] GetSpreadOptions()
    {
        EnsureSpreadDefs();
        var options = new List<string>();
        foreach (var sd in SpreadDefinitions)
            options.Add(sd.label);
        return options.ToArray();
    }

    /// <summary>
    /// 获取牌阵定义
    /// </summary>
    public SpreadDefinition GetSpreadDefinition(string kind)
    {
        EnsureSpreadDefs();
        foreach (var sd in SpreadDefinitions)
            if (sd.kind == kind) return sd;
        return null;
    }

    private bool HasSpread(string kind)
    {
        return GetSpreadDefinition(kind) != null;
    }

    /// <summary>
    /// 获取牌阵定义（按 label 匹配）
    /// </summary>
    public SpreadDefinition GetSpreadByLabel(string label)
    {
        EnsureSpreadDefs();
        foreach (var sd in SpreadDefinitions)
            if (sd.label == label) return sd;
        return null;
    }

    private void SyncToDialogSystem()
    {
        if (_dialogSystem == null) return;

        _dialogSystem.SetActiveReadingState(ActiveReadingState);
        _dialogSystem.SetActiveActionKind(ActiveActionKind);
        _dialogSystem.SetActiveReadingId(ActiveReadingId);
        _dialogSystem.SetActiveDivinationPlan(CurrentSession?.divinationPlan);

        if (CurrentSession?.readingLock != null)
            _dialogSystem.SetReadingLock(CurrentSession.readingLock);
        else
            _dialogSystem.ClearReadingLock();
    }

    private void EnsureSpreadDefs()
    {
        if (SpreadDefinitions == null || SpreadDefinitions.Length == 0)
        {
            SpreadDefinitions = new SpreadDefinition[]
            {
                new SpreadDefinition
                {
                    kind = "mirror_card", label = "镜子牌·快速觉察",
                    description = "一张牌，快速透析你的当下状态",
                    cardCount = 1,
                    positions = new List<SpreadPosition>
                    {
                        new SpreadPosition { key = "mirror", label = "镜中之我", prompt = "这张牌映照出你此刻的核心状态",
                            therapeuticRole = "自我觉察", tarotTheoryAxis = "自我认知", consentBoundary = "仅限自我觉察，不做预测" }
                    }
                },
                new SpreadDefinition
                {
                    kind = "self_repair", label = "自我修复·三步牌阵",
                    description = "看清困扰 · 放下执念 · 拥抱行动",
                    cardCount = 3,
                    positions = new List<SpreadPosition>
                    {
                        new SpreadPosition { key = "see", label = "看见", prompt = "你需要正视的内在真实",
                            therapeuticRole = "认知重构", tarotTheoryAxis = "问题核心" },
                        new SpreadPosition { key = "release", label = "放下", prompt = "你该释放的执念或限制",
                            therapeuticRole = "情绪脱钩 (ACT)", tarotTheoryAxis = "阻力" },
                        new SpreadPosition { key = "embrace", label = "拥抱", prompt = "你该拥抱的行动或新信念",
                            therapeuticRole = "价值驱动行动", tarotTheoryAxis = "出路" }
                    }
                },
                new SpreadDefinition
                {
                    kind = "relationship_tension", label = "关系张力·三卡牌阵",
                    description = "看清你的位置·TA的位置·关系张力的本质",
                    cardCount = 3,
                    positions = new List<SpreadPosition>
                    {
                        new SpreadPosition { key = "self", label = "你的位置", prompt = "你在关系中的姿态和需求",
                            therapeuticRole = "自我觉察", tarotTheoryAxis = "主动方" },
                        new SpreadPosition { key = "other", label = "对方的位置", prompt = "关系中另一方的动态模式",
                            therapeuticRole = "共情理解", tarotTheoryAxis = "被动方", consentBoundary = "不可断言他人内心" },
                        new SpreadPosition { key = "tension", label = "张力的本质", prompt = "关系张力的核心功课",
                            therapeuticRole = "关系模式觉察", tarotTheoryAxis = "互动场域" }
                    }
                },
                new SpreadDefinition
                {
                    kind = "choice_gate", label = "选择之门·五卡牌阵",
                    description = "路径A · 路径B · 恐惧声音 · 价值声音 · 干净行动",
                    cardCount = 5,
                    positions = new List<SpreadPosition>
                    {
                        new SpreadPosition { key = "option_a", label = "路径A", prompt = "选择A会打开什么",
                            therapeuticRole = "clarify", tarotTheoryAxis = "wands" },
                        new SpreadPosition { key = "option_b", label = "路径B", prompt = "选择B会打开什么",
                            therapeuticRole = "clarify", tarotTheoryAxis = "swords" },
                        new SpreadPosition { key = "fear_voice", label = "恐惧的声音", prompt = "哪个恐惧正在替你做决定",
                            therapeuticRole = "externalize", tarotTheoryAxis = "swords" },
                        new SpreadPosition { key = "values_voice", label = "价值的声音", prompt = "哪个价值真正想带路",
                            therapeuticRole = "reframe", tarotTheoryAxis = "cups" },
                        new SpreadPosition { key = "one_clean_action", label = "干净行动", prompt = "无论结果如何，未来24小时可以做的自尊行动",
                            therapeuticRole = "action", tarotTheoryAxis = "pentacles" }
                    }
                },
                new SpreadDefinition
                {
                    kind = "pattern_map", label = "模式地图·五卡牌阵",
                    description = "表层循环 · 底层需要 · 保护策略 · 模式代价 · 新行动",
                    cardCount = 5,
                    positions = new List<SpreadPosition>
                    {
                        new SpreadPosition { key = "surface_loop", label = "表层循环", prompt = "表面上一直重复的模式",
                            therapeuticRole = "mirror", tarotTheoryAxis = "swords" },
                        new SpreadPosition { key = "emotional_need", label = "底层需要", prompt = "循环下面真正未被照顾的需要",
                            therapeuticRole = "clarify", tarotTheoryAxis = "cups" },
                        new SpreadPosition { key = "protective_strategy", label = "保护策略", prompt = "哪个行为在试图保护你",
                            therapeuticRole = "externalize", tarotTheoryAxis = "court" },
                        new SpreadPosition { key = "cost_of_pattern", label = "代价", prompt = "这个模式继续无意识运转会消耗什么",
                            therapeuticRole = "reframe", tarotTheoryAxis = "pentacles" },
                        new SpreadPosition { key = "new_pattern_action", label = "新行动", prompt = "一个能打断循环且保住尊严的小行动",
                            therapeuticRole = "action", tarotTheoryAxis = "wands" }
                    }
                },
                new SpreadDefinition
                {
                    kind = "timeline_thread", label = "时间线·五卡牌阵",
                    description = "现在 · 下一步信号 · 转折点 · 当前路径倾向 · 锚定行动",
                    cardCount = 5,
                    positions = new List<SpreadPosition>
                    {
                        new SpreadPosition { key = "now", label = "现在", prompt = "当前情绪和关系天气",
                            therapeuticRole = "mirror", tarotTheoryAxis = "cups" },
                        new SpreadPosition { key = "next_step", label = "下一步信号", prompt = "如果什么都不改变，接下来可能出现的倾向",
                            therapeuticRole = "clarify", tarotTheoryAxis = "swords" },
                        new SpreadPosition { key = "turning_point", label = "转折点", prompt = "你可以中断旧路径的位置",
                            therapeuticRole = "reframe", tarotTheoryAxis = "wands" },
                        new SpreadPosition { key = "likely_tendency", label = "当前路径倾向", prompt = "当前模式继续时更可能形成的趋势，而非预测",
                            therapeuticRole = "integrate", tarotTheoryAxis = "major_arcana", consentBoundary = "仅作为觉察工具" },
                        new SpreadPosition { key = "anchor_action", label = "锚定行动", prompt = "贯穿这段时间线的稳定行动",
                            therapeuticRole = "action", tarotTheoryAxis = "pentacles" }
                    }
                },
                new SpreadDefinition
                {
                    kind = "celtic_inspired_deep_cross", label = "深层十字·五卡轻量版",
                    description = "问题中心 · 交叉压力 · 根部模式 · 外部场 · 干净行动",
                    cardCount = 5,
                    positions = new List<SpreadPosition>
                    {
                        new SpreadPosition { key = "center", label = "问题中心", prompt = "当前局面的核心",
                            therapeuticRole = "mirror", tarotTheoryAxis = "major_arcana" },
                        new SpreadPosition { key = "crossing", label = "交叉压力", prompt = "让问题复杂化的压力",
                            therapeuticRole = "externalize", tarotTheoryAxis = "swords" },
                        new SpreadPosition { key = "root", label = "根部模式", prompt = "当前事件下面更旧的模式",
                            therapeuticRole = "clarify", tarotTheoryAxis = "cups" },
                        new SpreadPosition { key = "field", label = "现实/关系场", prompt = "你可以观察到的外部环境",
                            therapeuticRole = "externalize", tarotTheoryAxis = "pentacles" },
                        new SpreadPosition { key = "clean_action", label = "干净行动", prompt = "现在保护主体性的行动",
                            therapeuticRole = "action", tarotTheoryAxis = "wands" }
                    }
                }
            };
        }
    }
}

/// <summary>
/// 占卜会话数据
/// </summary>
[Serializable]
public class DivinationSession
{
    public string readingId;
    public string question;
    public string scene;
    public DivinationPhase phase;
    public string spreadKind;
    public List<LockedCard> lockedCards;
    public ReadingLock readingLock;
    public DivinationPlan divinationPlan;
    public TomorrowHook tomorrowHook;
    public string shortVerdict;
    /// <summary>AI 生成的详细评判内容</summary>
    public string judgeContent;
    /// <summary>AI 生成的建议内容</summary>
    public string adviceContent;
    /// <summary>AI 建议的追问话题列表</summary>
    public List<string> topics;
    public string createdAt;
}

/// <summary>
/// 牌阵定义
/// </summary>
[Serializable]
public class SpreadDefinition
{
    public string kind;              // 牌阵类型标识
    public string label;             // UI 显示名
    public string description;       // 描述
    public int cardCount;            // 牌数
    public List<SpreadPosition> positions = new List<SpreadPosition>();
}
