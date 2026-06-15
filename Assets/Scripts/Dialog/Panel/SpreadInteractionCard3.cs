using System;
using System.Collections;
using System.Collections.Generic;
using GamerFrameWork;
using UnityEngine;
using UnityEngine.UI;
using XFGameFrameWork;

/// <summary>
/// 三排牌阵互动卡 —— AI 在对话中直接触发的三卡互动牌阵
///
/// 工作流程：
/// 1. AI 决定展示牌阵 → DialogSystem 添加 InteractionCard3 消息
/// 2. 面板显示牌阵信息 + 三张卡背，用户点击「开始抽牌」
/// 3. 随机抽 3 张塔罗牌 → 逐一翻牌动画 → 揭示牌面
/// 4. 显示底部操作按钮（选牌阵 / 继续追问 / 明天再看）
/// </summary>
public class SpreadInteractionCard3 : MonoBehaviour
{
    [Header("标题")]
    public Text cardTitle;
    public Text cardSubtitle;

    [Header("抽牌")]
    public Button drawCardBtn;
    public Text drawCardBtnText;           // 按钮文字（可选），默认"开始抽牌"

    [Header("卡牌槽位（左→中→右）")]
    public Image cardSlot1Image;
    public Image cardSlot2Image;
    public Image cardSlot3Image;

    [Header("槽位标签")]
    public Text slot1Label;                // 如"当下"
    public Text slot2Label;                // 如"阻碍"
    public Text slot3Label;                // 如"走向"

    [Header("卡牌背面（占位图）")]
    public Sprite cardBackSprite;

    [Header("操作按钮")]
    public Button selectSpreadBtn;         // 「为这个问题选择牌阵」
    public Button continueAskBtn;          // 「继续追问」
    public Button checkTomorrowBtn;        // 「明天再看这条线索」

    [Header("翻牌动画")]
    public float flipDuration = 0.5f;
    public float cardRevealGap = 0.25f;    // 两张牌之间的间隔

    // ========== 内部状态 ==========
    private SpreadDefinition _currentSpread;
    private List<(TarotCard card, bool upright)> _drawnCards;
    private bool _cardsDrawn;
    private Coroutine _revealCoroutine;

    // ========== 事件 ==========
    /// <summary>用户点击「选择牌阵」</summary>
    public event Action OnSelectSpreadClicked;
    /// <summary>用户点击「继续追问」</summary>
    public event Action OnContinueAskClicked;
    /// <summary>用户点击「明天再看」</summary>
    public event Action OnCheckTomorrowClicked;

    // ========== 生命周期 ==========

    private void Awake()
    {
        // 抽牌按钮
        if (drawCardBtn != null)
            drawCardBtn.onClick.AddListener(OnDrawCardClicked);

        // 操作按钮
        if (selectSpreadBtn != null)
            selectSpreadBtn.onClick.AddListener(() => OnSelectSpreadClicked?.Invoke());
        if (continueAskBtn != null)
            continueAskBtn.onClick.AddListener(() => OnContinueAskClicked?.Invoke());
        if (checkTomorrowBtn != null)
            checkTomorrowBtn.onClick.AddListener(() => OnCheckTomorrowClicked?.Invoke());
    }

    private void OnDestroy()
    {
        if (drawCardBtn != null) drawCardBtn.onClick.RemoveAllListeners();
        if (selectSpreadBtn != null) selectSpreadBtn.onClick.RemoveAllListeners();
        if (continueAskBtn != null) continueAskBtn.onClick.RemoveAllListeners();
        if (checkTomorrowBtn != null) checkTomorrowBtn.onClick.RemoveAllListeners();

        if (_revealCoroutine != null)
            StopCoroutine(_revealCoroutine);
    }

    // ========== 对外 API ==========

    /// <summary>
    /// 初始化牌阵面板
    /// </summary>
    /// <param name="spreadDef">牌阵定义（如 self_repair）</param>
    public void Setup(SpreadDefinition spreadDef)
    {
        ResetPanel();

        _currentSpread = spreadDef;
        if (_currentSpread == null) return;

        // 标题
        if (cardTitle != null)
            cardTitle.text = string.IsNullOrEmpty(_currentSpread.label)
                ? "三牌牌阵"
                : _currentSpread.label;

        if (cardSubtitle != null)
            cardSubtitle.text = string.IsNullOrEmpty(_currentSpread.description)
                ? "三牌牌阵：看清当下、阻碍、走向。"
                : _currentSpread.description;

        // 槽位标签
        ApplySlotLabels();

        // 抽牌按钮
        if (drawCardBtn != null)
        {
            drawCardBtn.gameObject.SetActive(true);
            drawCardBtn.interactable = true;
        }
        if (drawCardBtnText != null)
            drawCardBtnText.text = "开始抽牌";

        // 操作按钮初始隐藏
        SetActionButtonsVisible(false);

        Debug.Log($"[SpreadInteractionCard3] Setup 完成: {_currentSpread.label}");
    }

    /// <summary>
    /// 获取抽牌结果（外部读取，如 DialogUI 发送给 AI）
    /// </summary>
    public List<(TarotCard card, bool upright)> GetDrawnCards() => _drawnCards;

    /// <summary>
    /// 当前牌阵
    /// </summary>
    public SpreadDefinition CurrentSpread => _currentSpread;

    /// <summary>
    /// 是否已抽牌
    /// </summary>
    public bool CardsDrawn => _cardsDrawn;

    // ========== 抽牌 & 翻牌 ==========

    private void OnDrawCardClicked()
    {
        if (_cardsDrawn) return;

        drawCardBtn.interactable = false;
        if (drawCardBtnText != null)
            drawCardBtnText.text = "抽牌中...";

        _revealCoroutine = StartCoroutine(DrawAndRevealCards());
    }

    private IEnumerator DrawAndRevealCards()
    {
        // 1. 从塔罗牌组随机抽 3 张（不重复）
        _drawnCards = TarotDeck.DrawMultiple(3);

        // 2. 同步到 DivinationEngine（如果有活跃会话）
        SyncToDivinationEngine();

        yield return new WaitForSeconds(0.3f);

        // 3. 逐一翻牌
        yield return StartCoroutine(FlipCard(cardSlot1Image, _drawnCards[0]));
        yield return new WaitForSeconds(cardRevealGap);

        yield return StartCoroutine(FlipCard(cardSlot2Image, _drawnCards[1]));
        yield return new WaitForSeconds(cardRevealGap);

        yield return StartCoroutine(FlipCard(cardSlot3Image, _drawnCards[2]));

        // 4. 完成
        _cardsDrawn = true;

        if (drawCardBtn != null)
            drawCardBtn.gameObject.SetActive(false);

        SetActionButtonsVisible(true);

        Debug.Log($"[SpreadInteractionCard3] 抽牌完成: "
            + $"{_drawnCards[0].card.nameZh}({(_drawnCards[0].upright ? "正" : "逆")}) | "
            + $"{_drawnCards[1].card.nameZh}({(_drawnCards[1].upright ? "正" : "逆")}) | "
            + $"{_drawnCards[2].card.nameZh}({(_drawnCards[2].upright ? "正" : "逆")})");
    }

    /// <summary>
    /// 翻牌动画 —— 水平缩放模拟翻转
    /// </summary>
    private IEnumerator FlipCard(Image cardImage, (TarotCard card, bool upright) draw)
    {
        if (cardImage == null) yield break;

        Sprite frontSprite = LoadCardSprite(draw.card.cardId);
        float halfDuration = flipDuration / 2f;

        // 阶段 1：缩小到 0（翻面）
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            // ease-in
            float scale = 1f - EaseInQuad(t);
            cardImage.transform.localScale = new Vector3(scale, 1f, 1f);
            yield return null;
        }

        // 切换图片
        cardImage.sprite = frontSprite ?? cardBackSprite;

        // 逆位旋转
        cardImage.transform.localRotation = draw.upright
            ? Quaternion.identity
            : Quaternion.Euler(0, 0, 180);

        // 阶段 2：放大回来
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float scale = EaseOutQuad(t);
            cardImage.transform.localScale = new Vector3(scale, 1f, 1f);
            yield return null;
        }

        cardImage.transform.localScale = Vector3.one;
    }

    // ========== 辅助方法 ==========

    private void ApplySlotLabels()
    {
        if (_currentSpread?.positions == null) return;

        // 按顺序映射槽位标签
        for (int i = 0; i < _currentSpread.positions.Count && i < 3; i++)
        {
            string label = _currentSpread.positions[i].label;
            switch (i)
            {
                case 0: if (slot1Label != null) slot1Label.text = label; break;
                case 1: if (slot2Label != null) slot2Label.text = label; break;
                case 2: if (slot3Label != null) slot3Label.text = label; break;
            }
        }
    }

    private void SetActionButtonsVisible(bool visible)
    {
        if (selectSpreadBtn != null)
            selectSpreadBtn.gameObject.SetActive(visible);
        if (continueAskBtn != null)
            continueAskBtn.gameObject.SetActive(visible);
        if (checkTomorrowBtn != null)
            checkTomorrowBtn.gameObject.SetActive(visible);
    }

    private void SyncToDivinationEngine()
    {
        if (DivinationEngine.Instance == null) return;

        var session = DivinationEngine.Instance.CurrentSession;
        if (session == null) return;

        // 如果已经有 lockedCards，跳过
        if (session.lockedCards != null && session.lockedCards.Count > 0) return;

        // 构建 LockedCard 列表
        var spreadKind = _currentSpread?.kind ?? "self_repair";
        var lockedList = new List<GamerFrameWork.OracleRuntime.LockedCard>();

        for (int i = 0; i < _drawnCards.Count; i++)
        {
            var (card, upright) = _drawnCards[i];
            string posKey = i < (_currentSpread?.positions?.Count ?? 0)
                ? _currentSpread.positions[i].key
                : $"pos_{i + 1}";
            string posLabel = i < (_currentSpread?.positions?.Count ?? 0)
                ? _currentSpread.positions[i].label
                : $"第{i + 1}张";

            lockedList.Add(new GamerFrameWork.OracleRuntime.LockedCard
            {
                positionKey = posKey,
                position = posLabel,
                cardId = card.cardId,
                cardName = card.nameZh,
                orientation = upright ? "upright" : "reversed"
            });
        }

        session.lockedCards = lockedList;
        session.spreadKind = spreadKind;
        session.phase = DivinationPhase.CardsLocked;

        // 创建 ReadingLock
        session.readingLock = new GamerFrameWork.OracleRuntime.ReadingLock
        {
            readingId = session.readingId,
            readingType = spreadKind,
            allowedCards = lockedList,
            locked = true
        };

        // 同步到 DialogSystem
        var dialogSystem = DialogSystem.Instance;
        if (dialogSystem != null)
        {
            dialogSystem.SetReadingLock(session.readingLock);
            dialogSystem.SetActiveReadingState("cards_locked");
            dialogSystem.SetActiveActionKind("reveal_card");
            dialogSystem.SetActiveReadingId(session.readingId);
        }

        Debug.Log($"[SpreadInteractionCard3] 已同步 {lockedList.Count} 张牌到 DivinationEngine, spreadKind={spreadKind}");
    }

    /// <summary>
    /// 加载卡牌图片 —— 尝试多种 Resources 路径
    /// </summary>
    private Sprite LoadCardSprite(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return cardBackSprite;

        // 优先级：TarotCards/ → Cards/ → CardBack
        var sprite = Resources.Load<Sprite>($"TarotCards/{cardId}");
        if (sprite != null) return sprite;

        sprite = Resources.Load<Sprite>($"Cards/{cardId}");
        if (sprite != null) return sprite;

        Debug.LogWarning($"[SpreadInteractionCard3] 未找到卡牌图片: {cardId}");
        return cardBackSprite;
    }

    /// <summary>
    /// 重置面板到初始状态
    /// </summary>
    public void ResetPanel()
    {
        if (_revealCoroutine != null)
        {
            StopCoroutine(_revealCoroutine);
            _revealCoroutine = null;
        }

        _currentSpread = null;
        _drawnCards = null;
        _cardsDrawn = false;

        // 恢复卡牌背面
        if (cardBackSprite != null)
        {
            if (cardSlot1Image != null) cardSlot1Image.sprite = cardBackSprite;
            if (cardSlot2Image != null) cardSlot2Image.sprite = cardBackSprite;
            if (cardSlot3Image != null) cardSlot3Image.sprite = cardBackSprite;
        }

        // 恢复 Transform
        ResetCardTransform(cardSlot1Image);
        ResetCardTransform(cardSlot2Image);
        ResetCardTransform(cardSlot3Image);

        // 按钮状态
        if (drawCardBtn != null)
        {
            drawCardBtn.gameObject.SetActive(true);
            drawCardBtn.interactable = true;
        }
        if (drawCardBtnText != null)
            drawCardBtnText.text = "开始抽牌";

        SetActionButtonsVisible(false);

        // 清空标签
        if (slot1Label != null) slot1Label.text = "";
        if (slot2Label != null) slot2Label.text = "";
        if (slot3Label != null) slot3Label.text = "";
    }

    private void ResetCardTransform(Image img)
    {
        if (img == null) return;
        img.transform.localScale = Vector3.one;
        img.transform.localRotation = Quaternion.identity;
    }

    // ========== 缓动函数 ==========

    private static float EaseInQuad(float t) => t * t;
    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
}
