using System;
using System.Collections;
using System.Collections.Generic;
using GamerFrameWork;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;
using UnityEngine;
using UnityEngine.UI;
using XFGameFrameWork;

/// <summary>
/// 五排牌阵互动卡 —— AI 在对话中直接触发的五卡互动牌阵
///
/// 工作流程：
/// 1. AI 决定展示牌阵 → DialogSystem 添加 InteractionCard5 消息
/// 2. 面板显示牌阵信息 + 五张卡背，用户点击「开始抽牌」
/// 3. 随机抽 5 张塔罗牌 → 逐一翻牌动画 → 揭示牌面
/// 4. 显示底部操作按钮（选牌阵 / 继续追问）
/// </summary>
public class SpreadInteractionCard5 : MonoBehaviour
{
    [Header("标题")]
    public Text cardTitle;
    public Text cardSubtitle1;
    public Text cardSubtitle2;

    [Header("抽牌")]
    public Button drawCardBtn;
    public Text drawCardBtnText;           // 按钮文字（可选），默认"开始抽牌"

    [Header("先继续聊聊")]
    public Button chatFirstBtn;

    [Header("卡牌槽位")]
    public CardSlotItem cardSlotItem1;
    public CardSlotItem cardSlotItem2;
    public CardSlotItem cardSlotItem3;
    public CardSlotItem cardSlotItem4;
    public CardSlotItem cardSlotItem5;

    [Header("卡牌背面（占位图）")]
    public Sprite cardBackSprite;

    [Header("操作按钮")]
    public Button selectSpreadBtn;         // 「为这个问题选择牌阵」
    public Button continueAskBtn;          // 「继续追问」

    [Header("翻牌动画")]
    public float flipDuration = 0.5f;
    public float cardRevealGap = 0.2f;    // 两张牌之间的间隔（五牌阵间隔稍短）

    // ========== 内部状态 ==========
    private SpreadDefinition _currentSpread;
    private ChatMessageData _messageData;
    private List<(TarotCard card, bool upright)> _drawnCards;
    private bool _cardsDrawn;
    private Coroutine _revealCoroutine;

    // ========== 事件 ==========
    /// <summary>用户点击「选择牌阵」</summary>
    public event Action OnSelectSpreadClicked;
    /// <summary>用户点击「继续追问」</summary>
    public event Action OnContinueAskClicked;
    /// <summary>用户点击「先继续聊聊」</summary>
    public event Action OnChatFirstClicked;

    // ========== 生命周期 ==========

    private void Awake()
    {
        // 抽牌按钮
        if (drawCardBtn != null)
            drawCardBtn.onClick.AddListener(OnDrawCardClicked);

        // 先聊聊按钮
        if (chatFirstBtn != null)
            chatFirstBtn.onClick.AddListener(() => OnChatFirstClicked?.Invoke());

        // 操作按钮
        if (selectSpreadBtn != null)
            selectSpreadBtn.onClick.AddListener(OnSelectSpreadButtonClicked);
        if (continueAskBtn != null)
            continueAskBtn.onClick.AddListener(() => OnContinueAskClicked?.Invoke());
    }

    private void OnDestroy()
    {
        if (drawCardBtn != null) drawCardBtn.onClick.RemoveAllListeners();
        if (chatFirstBtn != null) chatFirstBtn.onClick.RemoveAllListeners();
        if (selectSpreadBtn != null) selectSpreadBtn.onClick.RemoveAllListeners();
        if (continueAskBtn != null) continueAskBtn.onClick.RemoveAllListeners();

        if (_revealCoroutine != null)
            StopCoroutine(_revealCoroutine);

        // 清理桥接订阅
        SpreadShuffleBridge.ShuffleCompleted -= OnShuffleCompleted;
    }

    // ========== 对外 API ==========

    /// <summary>
    /// 初始化牌阵面板
    /// </summary>
    /// <param name="spreadDef">牌阵定义（如 five_choice_gate）</param>
    public void Setup(SpreadDefinition spreadDef)
    {
        Setup(spreadDef, null);
    }

    /// <summary>
    /// 初始化牌阵面板，并绑定聊天消息数据用于状态恢复。
    /// </summary>
    public void Setup(SpreadDefinition spreadDef, ChatMessageData messageData)
    {
        ResetPanel();

        _currentSpread = spreadDef;
        _messageData = messageData;
        if (_currentSpread == null) return;

        // 标题
        if (cardTitle != null)
            cardTitle.text = string.IsNullOrEmpty(_currentSpread.label)
                ? "五牌牌阵"
                : _currentSpread.label;

        if (cardSubtitle1 != null)
            cardSubtitle1.text = string.IsNullOrEmpty(_currentSpread.description)
                ? "五牌选择门牌阵：多维度审视你的问题。"
                : _currentSpread.description;

        if (cardSubtitle2 != null)
            cardSubtitle2.text = ""; // SpreadDefinition 暂无 subtitle 字段，副标题可后续扩展

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

        // 「先继续聊聊」按钮初始显示
        if (chatFirstBtn != null)
            chatFirstBtn.gameObject.SetActive(true);

        // 操作按钮初始隐藏
        SetActionButtonsVisible(false);

        if (TryRestoreDrawnCardsFromMessage())
        {
            RenderDrawnCardsImmediate();
        }

        Debug.Log($"[SpreadInteractionCard5] Setup 完成: {_currentSpread.label}");
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
        if (_cardsDrawn)
        {
            OpenDivinationDetail();
            return;
        }

        drawCardBtn.interactable = false;
        if (drawCardBtnText != null)
            drawCardBtnText.text = "正在打开...";

        // 隐藏「先继续聊聊」按钮
        if (chatFirstBtn != null)
            chatFirstBtn.gameObject.SetActive(false);

        // 设置桥接数据，打开洗牌界面
        SpreadShuffleBridge.PendingSpread = _currentSpread;
        SpreadShuffleBridge.PendingMessageData = _messageData;
        SpreadShuffleBridge.ShuffleCompleted -= OnShuffleCompleted;
        SpreadShuffleBridge.ShuffleCompleted += OnShuffleCompleted;

        UIModule.Instance.PopUpWindow<TarorSingleSpreadShuffleUI>();
    }

    /// <summary>
    /// 洗牌界面完成后的回调
    /// </summary>
    private void OnShuffleCompleted(List<(TarotCard card, bool upright)> drawnCards)
    {
        SpreadShuffleBridge.ShuffleCompleted -= OnShuffleCompleted;

        if (drawnCards == null || drawnCards.Count == 0)
        {
            if (drawCardBtn != null)
                drawCardBtn.interactable = true;
            if (drawCardBtnText != null)
                drawCardBtnText.text = "开始抽牌";
            if (chatFirstBtn != null)
                chatFirstBtn.gameObject.SetActive(true);
            return;
        }

        _drawnCards = drawnCards;
        SaveDrawnCardsToMessage();

        // 逐一翻牌揭示
        _revealCoroutine = StartCoroutine(RevealCardsRoutine());
    }

    private IEnumerator RevealCardsRoutine()
    {
        // 逐一翻牌
        yield return StartCoroutine(FlipCard(cardSlotItem1?.cardImage, _drawnCards[0]));
        if (cardSlotItem1 != null)
            cardSlotItem1.cardTag.text = $"{_drawnCards[0].card.nameZh}（{(_drawnCards[0].upright ? "正" : "逆")}）";
        yield return new WaitForSeconds(cardRevealGap);

        yield return StartCoroutine(FlipCard(cardSlotItem2?.cardImage, _drawnCards[1]));
        if (cardSlotItem2 != null)
            cardSlotItem2.cardTag.text = $"{_drawnCards[1].card.nameZh}（{(_drawnCards[1].upright ? "正" : "逆")}）";
        yield return new WaitForSeconds(cardRevealGap);

        yield return StartCoroutine(FlipCard(cardSlotItem3?.cardImage, _drawnCards[2]));
        if (cardSlotItem3 != null)
            cardSlotItem3.cardTag.text = $"{_drawnCards[2].card.nameZh}（{(_drawnCards[2].upright ? "正" : "逆")}）";
        yield return new WaitForSeconds(cardRevealGap);

        yield return StartCoroutine(FlipCard(cardSlotItem4?.cardImage, _drawnCards[3]));
        if (cardSlotItem4 != null)
            cardSlotItem4.cardTag.text = $"{_drawnCards[3].card.nameZh}（{(_drawnCards[3].upright ? "正" : "逆")}）";
        yield return new WaitForSeconds(cardRevealGap);

        yield return StartCoroutine(FlipCard(cardSlotItem5?.cardImage, _drawnCards[4]));
        if (cardSlotItem5 != null)
            cardSlotItem5.cardTag.text = $"{_drawnCards[4].card.nameZh}（{(_drawnCards[4].upright ? "正" : "逆")}）";

        // 完成
        _cardsDrawn = true;

        SyncToDivinationEngine();
        ShowDetailButton();
        SetActionButtonsVisible(true);

        Debug.Log($"[SpreadInteractionCard5] 抽牌完成: "
            + $"{_drawnCards[0].card.nameZh}({(_drawnCards[0].upright ? "正" : "逆")}) | "
            + $"{_drawnCards[1].card.nameZh}({(_drawnCards[1].upright ? "正" : "逆")}) | "
            + $"{_drawnCards[2].card.nameZh}({(_drawnCards[2].upright ? "正" : "逆")}) | "
            + $"{_drawnCards[3].card.nameZh}({(_drawnCards[3].upright ? "正" : "逆")}) | "
            + $"{_drawnCards[4].card.nameZh}({(_drawnCards[4].upright ? "正" : "逆")})");
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

        var slots = new[] { cardSlotItem1, cardSlotItem2, cardSlotItem3, cardSlotItem4, cardSlotItem5 };

        // 按顺序映射槽位标签
        for (int i = 0; i < _currentSpread.positions.Count && i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].cardTag.text = _currentSpread.positions[i].label;
        }
    }

    private void SetActionButtonsVisible(bool visible)
    {
        if (selectSpreadBtn != null)
        {
            selectSpreadBtn.gameObject.SetActive(visible);
            SetButtonText(selectSpreadBtn, _cardsDrawn ? "查看占卜详情" : "选择牌阵");
        }
        if (continueAskBtn != null)
            continueAskBtn.gameObject.SetActive(visible);
    }

    private bool TryRestoreDrawnCardsFromMessage()
    {
        if (_messageData == null || !_messageData.spreadCardsDrawn)
            return false;

        if (_messageData.spreadDrawnCards == null || _messageData.spreadDrawnCards.Count == 0)
            return false;

        _drawnCards = new List<(TarotCard card, bool upright)>();
        foreach (var drawData in _messageData.spreadDrawnCards)
        {
            var card = TarotDeck.GetById(drawData.cardId);
            if (card != null)
                _drawnCards.Add((card, drawData.upright));
        }

        _cardsDrawn = _drawnCards.Count >= 5;
        return _cardsDrawn;
    }

    private void SaveDrawnCardsToMessage()
    {
        if (_messageData == null || _drawnCards == null) return;

        DialogSystem.Instance?.CaptureDivinationSnapshot(_messageData);
        _messageData.spreadCardsDrawn = true;
        _messageData.spreadDrawnCards = new List<TarotDrawData>();
        foreach (var (card, upright) in _drawnCards)
        {
            if (card == null) continue;
            _messageData.spreadDrawnCards.Add(new TarotDrawData
            {
                cardId = card.cardId,
                upright = upright
            });
        }
        DialogSystem.Instance?.RecordSpreadDrawResult(_messageData);
    }

    private void RenderDrawnCardsImmediate()
    {
        if (_drawnCards == null || _drawnCards.Count < 5) return;

        var slots = new[] { cardSlotItem1, cardSlotItem2, cardSlotItem3, cardSlotItem4, cardSlotItem5 };
        for (int i = 0; i < slots.Length; i++)
        {
            SetCardSlotFace(slots[i], _drawnCards[i]);
        }

        if (chatFirstBtn != null)
            chatFirstBtn.gameObject.SetActive(false);
        ShowDetailButton();
        SetActionButtonsVisible(true);
    }

    private void SetCardSlotFace(CardSlotItem slot, (TarotCard card, bool upright) draw)
    {
        if (slot == null) return;

        if (slot.cardImage != null)
        {
            slot.cardImage.sprite = LoadCardSprite(draw.card.cardId);
            slot.cardImage.transform.localScale = Vector3.one;
            slot.cardImage.transform.localRotation = draw.upright
                ? Quaternion.identity
                : Quaternion.Euler(0, 0, 180);
        }

        if (slot.cardTag != null)
            slot.cardTag.text = $"{draw.card.nameZh}（{(draw.upright ? "正" : "逆")}）";
    }

    private void SetButtonText(Button button, string text)
    {
        var label = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (label != null)
            label.text = text;
    }

    private void OnSelectSpreadButtonClicked()
    {
        if (!_cardsDrawn)
        {
            OnSelectSpreadClicked?.Invoke();
            return;
        }

        OpenDivinationDetail();
    }

    private void ShowDetailButton()
    {
        if (drawCardBtn == null) return;

        drawCardBtn.gameObject.SetActive(true);
        drawCardBtn.interactable = true;
        SetButtonText(drawCardBtn, "查看占卜详情");
        if (drawCardBtnText != null)
            drawCardBtnText.text = "查看占卜详情";
    }

    private void OpenDivinationDetail()
    {
        DivinationInfoUI.SelectedRecord = DivinationRecordBuilder.FromChatMessage(_messageData, _currentSpread);
        if (DivinationInfoUI.SelectedRecord == null)
        {
            SyncToDivinationEngine();
            DivinationInfoUI.SelectedRecord = DivinationRecordBuilder.FromSession();
        }

        DialogSystem.Instance?.ActivateReadingFromRecord(DivinationInfoUI.SelectedRecord, DivinationPhase.Completed);
        UIModule.Instance.PopUpWindow<DivinationInfoUI>();
    }

    private void SyncToDivinationEngine()
    {
        if (DivinationEngine.Instance == null) return;

        var session = DivinationEngine.Instance.CurrentSession;
        if (session == null) return;

        if (_drawnCards == null || _drawnCards.Count == 0) return;

        // 构建 LockedCard 列表
        var spreadKind = _currentSpread?.kind ?? "five_choice_gate";
        var lockedList = new List<LockedCard>();

        for (int i = 0; i < _drawnCards.Count; i++)
        {
            var (card, upright) = _drawnCards[i];
            string posKey = i < (_currentSpread?.positions?.Count ?? 0)
                ? _currentSpread.positions[i].key
                : $"pos_{i + 1}";
            string posLabel = i < (_currentSpread?.positions?.Count ?? 0)
                ? _currentSpread.positions[i].label
                : $"第{i + 1}张";

            lockedList.Add(new LockedCard
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
        session.readingLock = new ReadingLock
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

        Debug.Log($"[SpreadInteractionCard5] 已同步 {lockedList.Count} 张牌到 DivinationEngine, spreadKind={spreadKind}");
    }

    /// <summary>
    /// 加载卡牌图片 —— 尝试多种 Resources 路径
    /// </summary>
    private Sprite LoadCardSprite(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return cardBackSprite;
        return TarotSpriteLoader.Load(cardId) ?? cardBackSprite;
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
        _messageData = null;
        _drawnCards = null;
        _cardsDrawn = false;

        // 恢复卡牌背面
        var slots = new[] { cardSlotItem1, cardSlotItem2, cardSlotItem3, cardSlotItem4, cardSlotItem5 };
        if (cardBackSprite != null)
        {
            foreach (var slot in slots)
            {
                if (slot != null) slot.cardImage.sprite = cardBackSprite;
            }
        }

        // 恢复 Transform
        foreach (var slot in slots)
        {
            if (slot != null) ResetCardTransform(slot.cardImage);
        }

        // 按钮状态
        if (drawCardBtn != null)
        {
            drawCardBtn.gameObject.SetActive(true);
            drawCardBtn.interactable = true;
        }
        if (drawCardBtnText != null)
            drawCardBtnText.text = "开始抽牌";

        if (chatFirstBtn != null)
            chatFirstBtn.gameObject.SetActive(true);

        SetActionButtonsVisible(false);

        // 清空标签
        var labels = new[] { cardSlotItem1, cardSlotItem2, cardSlotItem3, cardSlotItem4, cardSlotItem5 };
        foreach (var item in labels)
        {
            if (item != null) item.cardTag.text = "";
        }
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
