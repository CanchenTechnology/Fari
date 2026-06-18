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
        Debug.Log($"[DivinationEngine] 今日牌: {result.card.nameZh} ({(result.upright ? "正位" : "逆位")})");
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
        Debug.Log($"[DivinationEngine] 使用云端今日牌: {card.nameZh} ({(upright ? "正位" : "逆位")})");
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
        Debug.Log($"[DivinationEngine] 快速占卜——跳过澄清，直接选牌阵");
        SyncToDialogSystem();
        return session;
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
            Debug.Log($"  {lc.position}: {lc.cardName} ({(lc.orientation == "upright" ? "正" : "逆")})");

        // 同步到 DialogSystem
        SyncToDialogSystem();

        return lockedCards;
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
        if (firestore != null && firestore.IsReady)
        {
            firestore.SaveFromSession(CurrentSession, shortVerdict, success =>
            {
                if (success)
                    Debug.Log($"[DivinationEngine] 占卜记录已同步至 Firestore: {CurrentSession.readingId}");
                else
                    Debug.LogWarning($"[DivinationEngine] Firestore 同步失败，记录仅在内存中: {CurrentSession.readingId}");
            });
        }
        else
        {
            Debug.Log("[DivinationEngine] Firestore 未就绪，跳过自动保存（记录仅在内存中）");
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
                    kind = "choice_gate", label = "选择之门·二选一牌阵",
                    description = "选项A vs 选项B · 哪条路更适合你",
                    cardCount = 3,
                    positions = new List<SpreadPosition>
                    {
                        new SpreadPosition { key = "option_a", label = "选项A", prompt = "选择A的能量走向",
                            therapeuticRole = "视角拓展", tarotTheoryAxis = "路径A" },
                        new SpreadPosition { key = "option_b", label = "选项B", prompt = "选择B的能量走向",
                            therapeuticRole = "视角拓展", tarotTheoryAxis = "路径B" },
                        new SpreadPosition { key = "advice", label = "指南", prompt = "穿越纠结的内在指引",
                            therapeuticRole = "价值澄清", tarotTheoryAxis = "超然视角" }
                    }
                },
                new SpreadDefinition
                {
                    kind = "pattern_map", label = "模式地图·五卡牌阵",
                    description = "过去影响·当前模式·隐藏动力·建议行动·可能结果",
                    cardCount = 5,
                    positions = new List<SpreadPosition>
                    {
                        new SpreadPosition { key = "past", label = "过去影响", prompt = "影响当前局面的过去因素",
                            therapeuticRole = "模式溯源", tarotTheoryAxis = "时间线-过去" },
                        new SpreadPosition { key = "present", label = "当前模式", prompt = "你正在经历的重复模式",
                            therapeuticRole = "认知觉察", tarotTheoryAxis = "时间线-现在" },
                        new SpreadPosition { key = "hidden", label = "隐藏动力", prompt = "表面之下的潜意识驱力",
                            therapeuticRole = "阴影觉察", tarotTheoryAxis = "阴影面" },
                        new SpreadPosition { key = "advice", label = "建议行动", prompt = "打破循环的具体行动",
                            therapeuticRole = "行为激活", tarotTheoryAxis = "出路" },
                        new SpreadPosition { key = "outcome", label = "可能结果", prompt = "当前路径的可能走向（非预测）",
                            therapeuticRole = "正念觉察", tarotTheoryAxis = "时间线-未来", consentBoundary = "仅作为觉察工具" }
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
