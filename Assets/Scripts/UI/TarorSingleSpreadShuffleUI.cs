/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/16/2026 9:59:31 AM
 * Description: 塔罗牌洗牌界面 —— 从 InteractionCard 触发，显示对应数量的卡槽并执行洗牌动画
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork;
using GamerFrameWork.OracleRuntime;
using XFGameFrameWork;
using TMPro;

public class TarorSingleSpreadShuffleUI : WindowBase
{
    public TarorSingleSpreadShuffleUIComponent uiComponent;

    // ========== 内部状态 ==========
    private SpreadDefinition _spread;
    private int _cardCount;
    private List<(TarotCard card, bool upright)> _drawnCards;
    private Coroutine _shuffleCoroutine;
    private bool _shuffleDone;
    private bool _cardsReadyToDraw;
    private int _nextDrawIndex;
    private TMP_Text _startShuffleButtonText;
    private int _latestRevealedIndex = -1;
    private int _cardInfoRequestVersion;
    private bool _isCardInfoLoading;
    private DeepSeekAPI _deepSeekAPI;

    // CardSlotItem 数组（懒加载）
    private CardSlotItem[] _slots;

    private readonly List<Image> _deckImages = new List<Image>();
    private RectTransform _cardContainer;
    private Image _cardTemplateImage;
    private Transform _slotLayer;
    private Transform _deckLayer;
    private Tween _deckInertiaTween;
    private Sequence _drawSequence;
    private Coroutine _longPressCoroutine;
    private bool _isChoosingCard;
    private bool _isAnimatingCard;
    private bool _dragMoved;
    private bool _dragStartedFromCard;
    private bool _isPullingCard;
    private bool _suppressPointerClick;
    private int _activeDragCardIndex = -1;
    private float _dragStartLocalX;
    private float _dragStartLocalY;
    private float _dragStartOffsetX;
    private float _deckOffsetX;
    private float _activePullY;
    private float _lastScrollSampleX;
    private float _lastScrollSampleTime;
    private float _scrollVelocityX;
    private float _viewportWidth;
    private float _viewportHeight;
    private float _fanWidth;
    private float _deckCardScale = 1f;

    #region 生命周期函数

    public override void OnAwake()
    {
        uiComponent = gameObject.GetComponent<TarorSingleSpreadShuffleUIComponent>();
        uiComponent.InitComponent(this);
        _deepSeekAPI = DeepSeekAPI.ResolveFor(gameObject);
        this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
        base.OnAwake();
    }

    public override void OnShow()
    {
        base.OnShow();
        StopShuffleCoroutine();
        KillDeckAnimation();
        _shuffleDone = false;
        _cardsReadyToDraw = false;
        _isChoosingCard = false;
        _isAnimatingCard = false;
        _nextDrawIndex = 0;
        _latestRevealedIndex = -1;
        _cardInfoRequestVersion++;
        _isCardInfoLoading = false;
        _drawnCards = null;

        // 从桥梁读取牌阵数据
        _spread = SpreadShuffleBridge.PendingSpread;
        if (_spread == null)
        {
            Debug.LogWarning("[TarorSingleSpreadShuffleUI] 未找到牌阵数据，使用默认三牌阵");
            _cardCount = 3;
        }
        else
        {
            _cardCount = _spread.cardCount > 0 ? _spread.cardCount : 3;
        }

        // 构建槽位数组
        BuildSlotArray();
        _cardCount = Mathf.Clamp(_cardCount, 1, Mathf.Max(1, CountAvailableSlots()));
        ResolveDeckReferences();
        ResolveVisualLayers();

        // 配置界面
        ConfigureUI();
        KeepDeckBehindSlotLayer();
        PrepareInteractiveDeck();

        Debug.Log($"[TarorSingleSpreadShuffleUI] OnShow, cardCount={_cardCount}, spread={_spread?.label}");
    }

    public override void OnHide()
    {
        base.OnHide();
        StopShuffleCoroutine();
        KillDeckAnimation();
        _cardInfoRequestVersion++;
    }

    public override void OnDestroy()
    {
        StopShuffleCoroutine();
        KillDeckAnimation();
        _cardInfoRequestVersion++;
        base.OnDestroy();
    }

    #endregion

    #region UI 配置

    private void BuildSlotArray()
    {
        var comp = uiComponent;
        _slots = new[]
        {
            comp.CardSlotItem1CardSlotItem,
            comp.CardSlotItem2CardSlotItem,
            comp.CardSlotItem3CardSlotItem,

        };
    }

    private void ResolveVisualLayers()
    {
        _slotLayer = null;
        _deckLayer = _cardContainer != null ? _cardContainer.parent : null;

        if (_slots != null)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;
                _slotLayer = _slots[i].transform.parent;
                break;
            }
        }
    }

    private void KeepDeckBehindSlotLayer()
    {
        if (_slotLayer == null || _deckLayer == null)
            ResolveVisualLayers();

        if (_slotLayer == null || _deckLayer == null || _slotLayer.parent != _deckLayer.parent)
            return;

        int slotIndex = _slotLayer.GetSiblingIndex();
        if (_deckLayer.GetSiblingIndex() > slotIndex)
            _deckLayer.SetSiblingIndex(slotIndex);
    }

    /// <summary>
    /// 根据牌阵信息配置标题、副标题、卡槽显示
    /// </summary>
    private void ConfigureUI()
    {
        var comp = uiComponent;

        SetReadingKeywordTitle();
        SetOperationStepText($"点击、长按，或向上拖动牌扇中的牌，依次抽出{_cardCount}张牌。");
        ClearCardInfoTexts();

        // ---- 卡槽显示/隐藏 ----
        Sprite backSprite = ResolveCardBackSprite();
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null) continue;

            if (i < _cardCount)
            {
                string label = _spread?.positions != null && i < _spread.positions.Count
                    ? _spread.positions[i].label
                    : GetDefaultSlotLabel(i, _cardCount);
                ResetSlotToBack(slot, label, backSprite);
            }
            else
            {
                ResetSlotToBack(slot, "", backSprite);
                slot.gameObject.SetActive(false);
            }
        }

        SetStartShuffleButtonText("");

        // ---- 返回按钮 ----
        if (comp.BackButton != null)
        {
            comp.BackButton.gameObject.SetActive(true);
        }
    }

    #endregion

    #region 默认文本

    private string GetDefaultTitle(int count) => count switch
    {
        1 => "单张镜像牌阵",
        5 => "五牌选择门",
        _ => "三牌牌阵"
    };

    private string GetDefaultSubtitle(int count) => count switch
    {
        1 => "1张牌覆于桌面——让我和你一起阅读它的信息。",
        5 => "5张牌从多维度揭示你的问题——每一张都是通往内在的门。",
        _ => "3张牌依次展开——看清当下、阻碍与走向。"
    };

    private string GetInstruction(int count) => count switch
    {
        1 => "在心中默念你的问题，然后点击下方按钮抽牌。",
        5 => "让直觉引导我的手触碰屏幕——5张牌将为你打开新的视野。",
        _ => "让直觉引领——点击下方按钮为当下抽出三张牌。"
    };

    private void SetReadingKeywordTitle(string title = null)
    {
        if (uiComponent.TitleTextText == null) return;

        string text = FirstNonEmpty(
            title,
            _spread?.description,
            GetDefaultSubtitle(_cardCount),
            _spread?.label,
            GetDefaultTitle(_cardCount));

        uiComponent.TitleTextText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        uiComponent.TitleTextText.text = text;
    }

    private void SetOperationStepText(string text)
    {
        if (uiComponent.SubtitleTextText == null) return;

        uiComponent.SubtitleTextText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        uiComponent.SubtitleTextText.text = text ?? "";
    }

    private static string BuildCardKeywordTitle(TarotCard card)
    {
        if (card?.keywords != null && card.keywords.Count > 0)
            return string.Join(" · ", card.keywords.GetRange(0, Mathf.Min(3, card.keywords.Count)));

        return card?.nameZh ?? "";
    }

    private string BuildRevealedKeywordTitle()
    {
        if (_drawnCards == null || _drawnCards.Count == 0)
            return "";

        var keywords = new List<string>();
        int count = Mathf.Clamp(_nextDrawIndex, 0, Mathf.Min(_cardCount, _drawnCards.Count));
        for (int i = 0; i < count; i++)
        {
            var card = _drawnCards[i].card;
            if (card?.keywords == null) continue;

            for (int j = 0; j < card.keywords.Count && keywords.Count < 5; j++)
            {
                string keyword = card.keywords[j];
                if (string.IsNullOrWhiteSpace(keyword) || keywords.Contains(keyword)) continue;
                keywords.Add(keyword);
            }
        }

        return keywords.Count > 0 ? string.Join(" · ", keywords) : "";
    }

    private string GetDefaultSlotLabel(int index, int count) => count switch
    {
        1 => "镜像",
        5 => index switch
        {
            0 => "选择A",
            1 => "选择B",
            2 => "选择C",
            3 => "选择D",
            4 => "关键",
            _ => $"第{index + 1}张"
        },
        _ => index switch
        {
            0 => "当下",
            1 => "阻碍",
            2 => "走向",
            _ => $"第{index + 1}张"
        }
    };

    #endregion

    #region UI组件事件

    /// <summary>
    /// 返回按钮 → 关闭洗牌窗口
    /// </summary>
    public void OnBackButtonClick()
    {
        if (_isCardInfoLoading)
        {
            ToastManager.ShowToast("请等这张牌解读完成后，再继续。");
            return;
        }

        if (!_shuffleDone && !AreAllCardsRevealed())
        {
            ToastManager.ShowToast("请先抽完所有牌，再查看结果。");
            Debug.Log("[TarorSingleSpreadShuffleUI] 未抽完牌，阻止退出");
            return;
        }

        if (!_shuffleDone && AreAllCardsRevealed())
        {
            CompleteShuffle();
            return;
        }

        UIModule.Instance.HideWindow<TarorSingleSpreadShuffleUI>();
    }



    private void ResolveDeckReferences()
    {
        _cardContainer = uiComponent.cardContainer as RectTransform;
        if (_cardContainer == null)
        {
            Transform container = FindChildRecursive(transform, "cardContainer");
            if (container != null)
            {
                uiComponent.cardContainer = container;
                _cardContainer = container as RectTransform;
            }
        }

        if (uiComponent.cardImageGo == null && _cardContainer != null)
        {
            Transform template = _cardContainer.Find("Card") ?? FindChildRecursive(_cardContainer, "Card");
            if (template != null)
                uiComponent.cardImageGo = template.gameObject;
        }

        _cardTemplateImage = uiComponent.cardImageGo != null
            ? uiComponent.cardImageGo.GetComponent<Image>()
            : null;

        if (_cardTemplateImage != null)
        {
            _cardTemplateImage.sprite = ResolveCardBackSprite();
            _cardTemplateImage.color = Color.white;
            _cardTemplateImage.raycastTarget = false;
            _cardTemplateImage.gameObject.SetActive(false);
        }

        if (_cardContainer != null)
            BindDeckDragSurface(_cardContainer.gameObject);
    }

    private void PrepareInteractiveDeck()
    {
        ResetVisibleSlotsToBack();
        ClearRuntimeDeckCards();

        _drawnCards = TarotDeck.DrawMultiple(_cardCount);
        SyncToDivinationEngine();

        _cardsReadyToDraw = true;
        _isChoosingCard = true;
        _isAnimatingCard = false;
        _nextDrawIndex = 0;
        _deckOffsetX = 0f;
        _scrollVelocityX = 0f;

        BuildDeckFan();

        SetReadingKeywordTitle();
        SetOperationStepText(GetDrawInstructionText());
    }

    private string GetDrawInstructionText()
    {
        if (_nextDrawIndex >= _cardCount)
            return "牌阵已经就位，点击返回查看结果。";

        return _nextDrawIndex switch
        {
            0 => "请选择第一张牌。",
            1 => "请选择第二张牌。",
            2 => "请选择第三张牌。",
            _ => $"请选择第{_nextDrawIndex + 1}张牌。"
        };
    }

    private void BuildDeckFan()
    {
        if (_cardContainer == null || _cardTemplateImage == null) return;

        Rect rect = _cardContainer.rect;
        _viewportWidth = Mathf.Max(1f, rect.width);
        _viewportHeight = Mathf.Max(1f, rect.height);
        _fanWidth = uiComponent.fanWidth > 0f
            ? uiComponent.fanWidth
            : Mathf.Max(uiComponent.minFanWidth, _viewportWidth * Mathf.Max(1f, uiComponent.fanViewportWidthMultiplier));
        _deckCardScale = ResolveDeckCardScale();

        int count = Mathf.Max(3, uiComponent.selectableCardCount);
        Sprite backSprite = ResolveCardBackSprite();
        for (int i = 0; i < count; i++)
        {
            GameObject cardObject = UnityEngine.Object.Instantiate(_cardTemplateImage.gameObject, _cardContainer);
            cardObject.name = $"SpreadDeckCard_{i + 1}";
            cardObject.SetActive(true);

            Image image = cardObject.GetComponent<Image>();
            if (image == null)
                image = cardObject.AddComponent<Image>();

            image.sprite = backSprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = true;

            RectTransform cardRect = image.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);

            int capturedIndex = i;
            BindDeckCardDrag(cardObject, capturedIndex);
            _deckImages.Add(image);
        }

        ApplyDeckLayout(false);
        SetDeckCardsInteractable(true);
    }

    private float ResolveDeckCardScale()
    {
        if (uiComponent.deckCardScale > 0f)
            return uiComponent.deckCardScale;

        RectTransform templateRect = _cardTemplateImage != null ? _cardTemplateImage.rectTransform : null;
        float sourceWidth = templateRect != null ? Mathf.Max(1f, templateRect.rect.width) : 285f;
        float sourceHeight = templateRect != null ? Mathf.Max(1f, templateRect.rect.height) : 487f;
        float widthScale = (_viewportWidth * 0.34f) / sourceWidth;
        float heightScale = (_viewportHeight * 0.56f) / sourceHeight;
        return Mathf.Clamp(Mathf.Min(widthScale, heightScale), uiComponent.minDeckCardScale, uiComponent.maxDeckCardScale);
    }

    private void BindDeckDragSurface(GameObject surfaceObject)
    {
        if (surfaceObject == null) return;

        EventTrigger trigger = surfaceObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = surfaceObject.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        RemoveEventTriggerEntry(trigger, EventTriggerType.PointerDown);
        RemoveEventTriggerEntry(trigger, EventTriggerType.BeginDrag);
        RemoveEventTriggerEntry(trigger, EventTriggerType.Drag);
        RemoveEventTriggerEntry(trigger, EventTriggerType.EndDrag);
        RemoveEventTriggerEntry(trigger, EventTriggerType.PointerUp);

        AddEventTriggerEntry(trigger, EventTriggerType.PointerDown, data => OnDeckPointerDown(data, -1));
        AddEventTriggerEntry(trigger, EventTriggerType.BeginDrag, OnDeckBeginDrag);
        AddEventTriggerEntry(trigger, EventTriggerType.Drag, OnDeckDrag);
        AddEventTriggerEntry(trigger, EventTriggerType.EndDrag, OnDeckEndDrag);
        AddEventTriggerEntry(trigger, EventTriggerType.PointerUp, OnDeckPointerUp);
    }

    private void BindDeckCardDrag(GameObject cardObject, int index)
    {
        if (cardObject == null) return;

        EventTrigger trigger = cardObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = cardObject.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        trigger.triggers.Clear();
        AddEventTriggerEntry(trigger, EventTriggerType.PointerDown, data => OnDeckPointerDown(data, index));
        AddEventTriggerEntry(trigger, EventTriggerType.PointerClick, data => OnDeckCardPointerClick(data, index));
        AddEventTriggerEntry(trigger, EventTriggerType.BeginDrag, OnDeckBeginDrag);
        AddEventTriggerEntry(trigger, EventTriggerType.Drag, OnDeckDrag);
        AddEventTriggerEntry(trigger, EventTriggerType.EndDrag, OnDeckEndDrag);
        AddEventTriggerEntry(trigger, EventTriggerType.PointerUp, OnDeckPointerUp);
    }

    private static void AddEventTriggerEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private static void RemoveEventTriggerEntry(EventTrigger trigger, EventTriggerType type)
    {
        if (trigger?.triggers == null) return;
        for (int i = trigger.triggers.Count - 1; i >= 0; i--)
        {
            if (trigger.triggers[i].eventID == type)
                trigger.triggers.RemoveAt(i);
        }
    }

    private void OnDeckPointerDown(BaseEventData eventData, int cardIndex)
    {
        if (!_isChoosingCard || _isAnimatingCard) return;
        if (!TryGetDeckLocalPoint(eventData, out Vector2 localPoint)) return;

        StopDeckInertia();
        StopLongPressSelection();

        _activeDragCardIndex = cardIndex;
        _dragStartedFromCard = cardIndex >= 0;
        _dragMoved = false;
        _suppressPointerClick = false;
        _isPullingCard = false;
        _activePullY = 0f;
        _dragStartLocalX = localPoint.x;
        _dragStartLocalY = localPoint.y;
        _dragStartOffsetX = _deckOffsetX;
        _lastScrollSampleX = localPoint.x;
        _lastScrollSampleTime = Time.unscaledTime;
        _scrollVelocityX = 0f;

        if (_dragStartedFromCard)
            _longPressCoroutine = uiComponent.StartCoroutine(LongPressSelectRoutine(cardIndex));
    }

    private void OnDeckBeginDrag(BaseEventData eventData)
    {
        if (!_isChoosingCard || _isAnimatingCard) return;
    }

    private void OnDeckDrag(BaseEventData eventData)
    {
        if (!_isChoosingCard || _isAnimatingCard) return;
        if (!TryGetDeckLocalPoint(eventData, out Vector2 localPoint)) return;

        float deltaX = localPoint.x - _dragStartLocalX;
        float deltaY = localPoint.y - _dragStartLocalY;
        float clickThreshold = Mathf.Max(1f, uiComponent.dragClickThreshold);

        if (!_dragMoved && new Vector2(deltaX, deltaY).magnitude >= clickThreshold)
        {
            _dragMoved = true;
            StopLongPressSelection();
        }

        if (_dragStartedFromCard && _activeDragCardIndex >= 0)
        {
            float directionBias = Mathf.Max(0.1f, uiComponent.dragPullDirectionBias);
            if (!_isPullingCard
                && deltaY > clickThreshold
                && deltaY > Mathf.Abs(deltaX) * directionBias)
            {
                _isPullingCard = true;
            }
        }

        if (_isPullingCard)
        {
            _activePullY = Mathf.Max(0f, deltaY);
            PreviewPulledDeckCard(_activeDragCardIndex, deltaX, deltaY);
            return;
        }

        _deckOffsetX = _dragStartOffsetX + deltaX * Mathf.Max(0.01f, uiComponent.dragSensitivity);

        float now = Time.unscaledTime;
        float deltaTime = Mathf.Max(0.001f, now - _lastScrollSampleTime);
        _scrollVelocityX = (localPoint.x - _lastScrollSampleX) / deltaTime;
        _lastScrollSampleX = localPoint.x;
        _lastScrollSampleTime = now;

        ApplyDeckLayout(false);
    }

    private void OnDeckEndDrag(BaseEventData eventData)
    {
        ReleaseDeckPointer();
    }

    private void OnDeckPointerUp(BaseEventData eventData)
    {
        ReleaseDeckPointer();
    }

    private void OnDeckCardPointerClick(BaseEventData eventData, int index)
    {
        if (!_isChoosingCard || _isAnimatingCard) return;
        if (_suppressPointerClick || _dragMoved) return;
        SelectDeckCard(index);
    }

    private void ReleaseDeckPointer()
    {
        StopLongPressSelection();

        if (_isPullingCard)
        {
            int releasedIndex = _activeDragCardIndex;
            bool shouldSelect = _activePullY >= Mathf.Max(1f, uiComponent.dragPullSelectThreshold);
            _isPullingCard = false;
            _activePullY = 0f;

            if (shouldSelect)
                SelectDeckCard(releasedIndex);
            else
                SnapPulledDeckCardBack(releasedIndex);

            _suppressPointerClick = true;
            return;
        }

        if (_dragMoved)
        {
            _suppressPointerClick = true;
            StartDeckInertia();
        }

        _dragStartedFromCard = false;
        _activeDragCardIndex = -1;
    }

    private IEnumerator LongPressSelectRoutine(int index)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, uiComponent.longPressSelectDuration));
        _longPressCoroutine = null;

        if (!_isChoosingCard || _isAnimatingCard) yield break;
        if (_dragMoved || _activeDragCardIndex != index) yield break;

        _suppressPointerClick = true;
        SelectDeckCard(index);
    }

    private void StopLongPressSelection()
    {
        if (_longPressCoroutine == null) return;
        uiComponent.StopCoroutine(_longPressCoroutine);
        _longPressCoroutine = null;
    }

    private bool TryGetDeckLocalPoint(BaseEventData eventData, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (_cardContainer == null || eventData is not PointerEventData pointerData)
            return false;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _cardContainer,
            pointerData.position,
            pointerData.pressEventCamera,
            out localPoint);
    }

    private void SelectDeckCard(int index)
    {
        if (!_isChoosingCard || _isAnimatingCard) return;
        if (index < 0 || index >= _deckImages.Count) return;
        if (_drawnCards == null || _nextDrawIndex >= _drawnCards.Count || _nextDrawIndex >= _cardCount) return;
        if (_nextDrawIndex >= _slots.Length || _slots[_nextDrawIndex] == null) return;

        StopLongPressSelection();
        StopDeckInertia();

        _isChoosingCard = false;
        _isAnimatingCard = true;
        _dragMoved = true;
        _suppressPointerClick = true;
        SetDeckCardsInteractable(false);

        int slotIndex = _nextDrawIndex;
        var draw = _drawnCards[slotIndex];
        PlayDeckCardToSlot(index, slotIndex, _slots[slotIndex], draw);
    }

    private void PlayDeckCardToSlot(int deckIndex, int slotIndex, CardSlotItem slot, (TarotCard card, bool upright) draw)
    {
        Image sourceImage = deckIndex >= 0 && deckIndex < _deckImages.Count ? _deckImages[deckIndex] : null;
        RectTransform slotPoseRect = ResolveSlotPoseRect(slot);
        if (sourceImage == null || slotPoseRect == null)
        {
            FinishSlotReveal(slotIndex, slot, draw);
            return;
        }

        Image flyImage = UnityEngine.Object.Instantiate(sourceImage, _cardContainer);
        flyImage.name = $"SpreadFlyingCard_{slotIndex + 1}";
        flyImage.raycastTarget = false;
        flyImage.sprite = ResolveCardBackSprite();
        flyImage.color = Color.white;
        flyImage.preserveAspect = true;

        RectTransform sourceRect = sourceImage.rectTransform;
        RectTransform flyRect = flyImage.rectTransform;
        flyRect.anchorMin = sourceRect.anchorMin;
        flyRect.anchorMax = sourceRect.anchorMax;
        flyRect.pivot = sourceRect.pivot;
        flyRect.sizeDelta = sourceRect.sizeDelta;
        flyRect.anchoredPosition = sourceRect.anchoredPosition;
        flyRect.localScale = sourceRect.localScale;
        flyRect.localRotation = sourceRect.localRotation;
        flyRect.SetAsLastSibling();

        Color sourceColor = sourceImage.color;
        sourceColor.a = 0f;
        sourceImage.color = sourceColor;

        ResetSlotToBack(slot, GetPositionLabel(slotIndex), ResolveCardBackSprite());
        ResolveSlotTargetPose(slotPoseRect, out Vector2 targetPosition, out float targetRotationZ, out float targetScale);

        float selectDuration = Mathf.Max(0.12f, uiComponent.selectDuration);
        float flipHalfDuration = Mathf.Max(0.08f, uiComponent.flipDuration * 0.5f);
        Sprite frontSprite = LoadCardSprite(draw.card.cardId);

        _drawSequence?.Kill(false);
        _drawSequence = DOTween.Sequence();
        _drawSequence.Append(flyRect.DOAnchorPos(targetPosition, selectDuration).SetEase(Ease.InOutCubic));
        _drawSequence.Join(flyRect.DORotate(new Vector3(0f, 0f, targetRotationZ), selectDuration).SetEase(Ease.InOutCubic));
        _drawSequence.Join(flyRect.DOScale(Vector3.one * targetScale, selectDuration).SetEase(Ease.OutBack, 0.82f));
        _drawSequence.Append(flyRect.DORotate(new Vector3(0f, 88f, targetRotationZ), flipHalfDuration).SetEase(Ease.InCubic));
        _drawSequence.AppendCallback(() =>
        {
            flyImage.sprite = frontSprite ?? ResolveCardBackSprite();
            flyImage.color = Color.white;
            flyRect.localRotation = Quaternion.Euler(0f, 88f, targetRotationZ);
        });
        _drawSequence.Append(flyRect.DORotate(new Vector3(0f, 0f, targetRotationZ), flipHalfDuration).SetEase(Ease.OutCubic));
        if (!draw.upright)
            _drawSequence.Append(flyRect.DORotate(new Vector3(0f, 0f, targetRotationZ + 180f), 0.22f).SetEase(Ease.InOutCubic));

        _drawSequence.OnComplete(() =>
        {
            if (flyImage != null)
                UnityEngine.Object.Destroy(flyImage.gameObject);

            Color restoredColor = sourceImage.color;
            restoredColor.a = 1f;
            sourceImage.color = restoredColor;
            sourceImage.sprite = ResolveCardBackSprite();

            FinishSlotReveal(slotIndex, slot, draw);
            _drawSequence = null;
        });
    }

    private void ResolveSlotTargetPose(RectTransform slotRect,
        out Vector2 targetPosition,
        out float targetRotationZ,
        out float targetScale)
    {
        targetPosition = Vector2.zero;
        targetRotationZ = 0f;
        targetScale = _deckCardScale;

        if (slotRect == null || _cardContainer == null || _cardTemplateImage == null)
            return;

        Vector3 worldCenter = slotRect.TransformPoint(slotRect.rect.center);
        Vector3 localCenter = _cardContainer.InverseTransformPoint(worldCenter);
        targetPosition = new Vector2(localCenter.x, localCenter.y);

        Quaternion localRotation = Quaternion.Inverse(_cardContainer.rotation) * slotRect.rotation;
        targetRotationZ = localRotation.eulerAngles.z;

        Vector3[] worldCorners = new Vector3[4];
        slotRect.GetWorldCorners(worldCorners);
        Vector3 localBottomLeft = _cardContainer.InverseTransformPoint(worldCorners[0]);
        Vector3 localTopLeft = _cardContainer.InverseTransformPoint(worldCorners[1]);
        Vector3 localBottomRight = _cardContainer.InverseTransformPoint(worldCorners[3]);

        float targetWidth = Vector3.Distance(localBottomLeft, localBottomRight);
        float targetHeight = Vector3.Distance(localBottomLeft, localTopLeft);
        RectTransform sourceRect = _cardTemplateImage.rectTransform;
        float sourceWidth = Mathf.Max(1f, sourceRect.rect.width);
        float sourceHeight = Mathf.Max(1f, sourceRect.rect.height);
        targetScale = Mathf.Max(0.01f, Mathf.Min(targetWidth / sourceWidth, targetHeight / sourceHeight));
    }

    private void FinishSlotReveal(int slotIndex, CardSlotItem slot, (TarotCard card, bool upright) draw)
    {
        SetSlotFace(slot, draw);
        SetRevealedCardInfo(slotIndex, draw);

        _nextDrawIndex++;
        KeepDeckBehindSlotLayer();
        _isAnimatingCard = false;
        _dragStartedFromCard = false;
        _activeDragCardIndex = -1;
        _isPullingCard = false;
        _activePullY = 0f;

        ApplyDeckLayout(true);

        if (_nextDrawIndex >= _cardCount)
        {
            _isChoosingCard = false;
            SetReadingKeywordTitle(BuildRevealedKeywordTitle());
            SetDeckVisible(false);
            SetOperationStepText($"{_cardCount}张牌已经就位，点击返回查看结果。");
            return;
        }

        _isChoosingCard = true;
        SetDeckCardsInteractable(true);
        SetOperationStepText(GetDrawInstructionText());
    }

    private void SetSlotFace(CardSlotItem slot, (TarotCard card, bool upright) draw)
    {
        if (slot == null || draw.card == null) return;

        slot.ShowFace(LoadCardSprite(draw.card.cardId), FormatCardName(draw.card, draw.upright), draw.upright);
    }

    private void PreviewPulledDeckCard(int index, float deltaX, float deltaY)
    {
        if (index < 0 || index >= _deckImages.Count) return;

        Image image = _deckImages[index];
        if (image == null) return;

        RectTransform rect = image.rectTransform;
        DOTween.Kill(rect);

        float pullY = Mathf.Max(0f, deltaY);
        float pullX = Mathf.Clamp(deltaX * 0.24f, -54f, 54f);
        float strength = Mathf.Clamp01(pullY / Mathf.Max(1f, uiComponent.dragPullSelectThreshold));
        rect.anchoredPosition = GetDeckFanPosition(index) + new Vector2(pullX, pullY);
        rect.localScale = Vector3.one * (_deckCardScale * GetSettledDeckScaleMultiplier() * (1f + 0.1f * strength));
        rect.localRotation = Quaternion.Euler(0f, 0f, GetDeckFanRotation(index) + pullX * 0.05f);
        rect.SetAsLastSibling();
    }

    private void SnapPulledDeckCardBack(int index)
    {
        if (index < 0 || index >= _deckImages.Count) return;

        Image image = _deckImages[index];
        if (image == null) return;

        RectTransform rect = image.rectTransform;
        DOTween.Kill(rect);
        Sequence sequence = DOTween.Sequence();
        sequence.Join(rect.DOAnchorPos(GetDeckFanPosition(index), 0.18f).SetEase(Ease.OutCubic));
        sequence.Join(rect.DORotate(new Vector3(0f, 0f, GetDeckFanRotation(index)), 0.18f).SetEase(Ease.OutCubic));
        sequence.Join(rect.DOScale(Vector3.one * (_deckCardScale * GetSettledDeckScaleMultiplier()), 0.18f).SetEase(Ease.OutCubic));
        sequence.OnComplete(() =>
        {
            ApplyDeckLayout(false);
            SetDeckCardsInteractable(_isChoosingCard && !_isAnimatingCard);
        });
    }

    private void ApplyDeckLayout(bool animated)
    {
        if (_deckImages.Count == 0) return;

        for (int i = 0; i < _deckImages.Count; i++)
        {
            Image image = _deckImages[i];
            if (image == null) continue;

            RectTransform rect = image.rectTransform;
            Vector2 targetPosition = GetDeckFanPosition(i);
            float targetRotation = GetDeckFanRotation(i);
            Vector3 targetScale = Vector3.one * (_deckCardScale * GetSettledDeckScaleMultiplier());

            if (animated)
            {
                rect.DOAnchorPos(targetPosition, 0.18f).SetEase(Ease.OutCubic);
                rect.DORotate(new Vector3(0f, 0f, targetRotation), 0.18f).SetEase(Ease.OutCubic);
                rect.DOScale(targetScale, 0.18f).SetEase(Ease.OutCubic);
            }
            else
            {
                rect.anchoredPosition = targetPosition;
                rect.localRotation = Quaternion.Euler(0f, 0f, targetRotation);
                rect.localScale = targetScale;
            }
        }
    }

    private Vector2 GetDeckFanPosition(int index)
    {
        ResolveDeckLayoutMetrics();
        float spacing = _deckImages.Count > 1 ? _fanWidth / (_deckImages.Count - 1) : _fanWidth;
        float cycleWidth = _fanWidth + spacing;
        float x = -_fanWidth * 0.5f + spacing * index + _deckOffsetX;
        x = Mathf.Repeat(x + cycleWidth * 0.5f, cycleWidth) - cycleWidth * 0.5f;

        float normalized = Mathf.Clamp(x / Mathf.Max(1f, _fanWidth * 0.5f), -1f, 1f);
        float y = uiComponent.fanHeightOffset + GetSettledDeckYOffset() + uiComponent.fanRiseOffset * (1f - normalized * normalized);
        return new Vector2(x, y);
    }

    private float GetSettledDeckYOffset()
    {
        if (_nextDrawIndex <= 0) return 0f;
        if (uiComponent.drawnFanLowerOffset > 0f)
            return -uiComponent.drawnFanLowerOffset;

        return -Mathf.Max(120f, _viewportHeight * 0.22f);
    }

    private float GetSettledDeckScaleMultiplier()
    {
        return _nextDrawIndex > 0
            ? Mathf.Clamp(uiComponent.drawnFanScaleMultiplier, 0.68f, 1f)
            : 1f;
    }

    private float GetDeckFanRotation(int index)
    {
        Vector2 position = GetDeckFanPosition(index);
        float normalized = Mathf.Clamp(position.x / Mathf.Max(1f, _fanWidth * 0.5f), -1f, 1f);
        return -normalized * uiComponent.fanRotation;
    }

    private void ResolveDeckLayoutMetrics()
    {
        if (_cardContainer == null) return;

        Rect rect = _cardContainer.rect;
        _viewportWidth = Mathf.Max(1f, rect.width);
        _viewportHeight = Mathf.Max(1f, rect.height);
        _fanWidth = uiComponent.fanWidth > 0f
            ? uiComponent.fanWidth
            : Mathf.Max(uiComponent.minFanWidth, _viewportWidth * Mathf.Max(1f, uiComponent.fanViewportWidthMultiplier));
        if (_cardTemplateImage != null)
            _deckCardScale = ResolveDeckCardScale();
    }

    private void StartDeckInertia()
    {
        if (!uiComponent.useScrollInertia || !_isChoosingCard || _isAnimatingCard)
            return;

        float velocity = _scrollVelocityX * Mathf.Max(0f, uiComponent.scrollInertiaMultiplier);
        velocity = Mathf.Clamp(velocity, -Mathf.Abs(uiComponent.maxFlickVelocity), Mathf.Abs(uiComponent.maxFlickVelocity));
        if (Mathf.Abs(velocity) < Mathf.Max(0f, uiComponent.minFlickVelocity))
            return;

        float deceleration = Mathf.Max(1f, uiComponent.scrollDeceleration);
        float duration = Mathf.Min(Mathf.Abs(velocity) / deceleration, Mathf.Max(0.05f, uiComponent.maxInertiaDuration));
        float distance = velocity * duration * 0.5f;
        float targetOffset = _deckOffsetX + distance;

        _deckInertiaTween?.Kill(false);
        _deckInertiaTween = DOTween.To(
                () => _deckOffsetX,
                value =>
                {
                    _deckOffsetX = value;
                    ApplyDeckLayout(false);
                },
                targetOffset,
                duration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() => _deckInertiaTween = null);
    }

    private void StopDeckInertia()
    {
        _deckInertiaTween?.Kill(false);
        _deckInertiaTween = null;
    }

    private void SetDeckCardsInteractable(bool interactable)
    {
        for (int i = 0; i < _deckImages.Count; i++)
        {
            if (_deckImages[i] != null)
                _deckImages[i].raycastTarget = interactable;
        }
    }

    private void SetDeckVisible(bool visible)
    {
        if (_cardContainer != null)
            _cardContainer.gameObject.SetActive(visible);
    }

    private void ClearRuntimeDeckCards()
    {
        StopDeckInertia();
        StopLongPressSelection();
        _drawSequence?.Kill(false);
        _drawSequence = null;

        for (int i = 0; i < _deckImages.Count; i++)
        {
            if (_deckImages[i] != null)
                UnityEngine.Object.Destroy(_deckImages[i].gameObject);
        }
        _deckImages.Clear();

        if (_cardContainer != null)
            _cardContainer.gameObject.SetActive(true);

        if (_cardTemplateImage != null)
            _cardTemplateImage.gameObject.SetActive(false);
    }

    private int CountAvailableSlots()
    {
        if (_slots == null || _slots.Length == 0) return 3;
        int count = 0;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] != null)
                count++;
        }
        return count > 0 ? count : 3;
    }

    #endregion

    #region 洗牌动画 & 揭示

    private IEnumerator ShuffleAndPrepareDraws()
    {
        ResetVisibleSlotsToBack();

        SetStartShuffleButtonText("洗牌中...");

        yield return uiComponent.StartCoroutine(PlayShuffleAnimation());

        _drawnCards = TarotDeck.DrawMultiple(_cardCount);
        // 牌一旦生成就先锁定到运行时上下文，避免 AI 在逐张翻牌期间被 OutputGuard 判定为引用未锁定卡牌。
        SyncToDivinationEngine();
        _cardsReadyToDraw = true;
        _nextDrawIndex = 0;
        _shuffleCoroutine = null;


        SetStartShuffleButtonText(GetDrawButtonText(_nextDrawIndex));
        SetOperationStepText($"牌已经洗好。请依次抽出{_cardCount}张牌。");
    }

    private void CompleteShuffle()
    {
        _shuffleDone = true;
        SyncToDivinationEngine();
        SaveDrawnCardsToPendingMessage();
        DivinationInfoUI.SelectedRecord = DivinationRecordBuilder.FromChatMessage(SpreadShuffleBridge.PendingMessageData, _spread)
            ?? DivinationRecordBuilder.FromSession();
        DialogSystem.Instance?.ActivateReadingFromRecord(DivinationInfoUI.SelectedRecord, DivinationPhase.CardsLocked);
        SpreadShuffleBridge.NotifyComplete(_drawnCards);
        SpreadShuffleBridge.PendingSpread = null;
        SpreadShuffleBridge.PendingMessageData = null;
        UIModule.Instance.HideWindow<TarorSingleSpreadShuffleUI>();
        UIModule.Instance.PopUpWindow<DivinationInfoUI>();
        Debug.Log($"[TarorSingleSpreadShuffleUI] 洗牌完成: {_cardCount} 张牌已揭示");
    }

    private void SaveDrawnCardsToPendingMessage()
    {
        var message = SpreadShuffleBridge.PendingMessageData;
        if (message == null || _drawnCards == null) return;

        DialogSystem.Instance?.CaptureDivinationSnapshot(message);
        message.spreadCardsDrawn = true;
        message.spreadDrawnCards = new List<TarotDrawData>();
        foreach (var (card, upright) in _drawnCards)
        {
            if (card == null) continue;
            message.spreadDrawnCards.Add(new TarotDrawData
            {
                cardId = card.cardId,
                upright = upright
            });
        }
        DialogSystem.Instance?.RecordSpreadDrawResult(message);
    }

    private void SetStartShuffleButtonText(string text)
    {
        if (_startShuffleButtonText != null)
            _startShuffleButtonText.text = text;
    }

    private string GetDrawButtonText(int index)
    {
        return index switch
        {
            0 => "抽第一张",
            1 => "抽第二张",
            2 => "抽第三张",
            3 => "抽第四张",
            4 => "抽第五张",
            _ => $"抽第{index + 1}张"
        };
    }

    private bool AreAllCardsRevealed()
    {
        return _drawnCards != null
            && _drawnCards.Count >= _cardCount
            && _nextDrawIndex >= _cardCount;
    }

    private void ClearCardInfoTexts()
    {
        TMP_Text standaloneCardTitle = GetStandaloneCardTitleText();
        if (standaloneCardTitle != null)
        {
            standaloneCardTitle.text = "";
            standaloneCardTitle.gameObject.SetActive(false);
        }

        var descriptionText = GetStandaloneDescriptionText();
        if (descriptionText != null)
        {
            descriptionText.text = "";
            descriptionText.gameObject.SetActive(false);
        }
    }


    private void SetRevealedCardInfo(int index, (TarotCard card, bool upright) draw)
    {
        if (draw.card == null) return;

        _latestRevealedIndex = index;
        int requestVersion = ++_cardInfoRequestVersion;
        _isCardInfoLoading = true;

        SetReadingKeywordTitle(BuildCardKeywordTitle(draw.card));

        TMP_Text standaloneCardTitle = GetStandaloneCardTitleText();
        if (standaloneCardTitle != null)
        {
            standaloneCardTitle.gameObject.SetActive(true);
            standaloneCardTitle.text = FormatCardName(draw.card, draw.upright);
        }

        string fallback = BuildCardInfoDescription(draw.card, draw.upright, index);
        var descriptionText = GetStandaloneDescriptionText();
        if (descriptionText != null)
        {
            descriptionText.gameObject.SetActive(true);
            descriptionText.text = "正在解读这张牌...";
        }

        RequestAiCardDescription(index, requestVersion, draw.card, draw.upright, fallback);
    }

    private void RequestAiCardDescription(int index, int requestVersion, TarotCard card, bool upright, string fallback)
    {
        if (card == null) return;

        if (_deepSeekAPI == null)
            _deepSeekAPI = DeepSeekAPI.ResolveFor(gameObject);
        if (_deepSeekAPI == null)
        {
            SetCardDescriptionIfCurrent(index, requestVersion, fallback);
            return;
        }

        var messages = BuildCardDescriptionMessages(card, upright, index);
        _deepSeekAPI.SendChatRequest(messages, response =>
        {
            if (uiComponent == null) return;
            if (_latestRevealedIndex != index || _cardInfoRequestVersion != requestVersion) return;

            string aiDescription = FirstNonEmpty(CleanAiCardDescription(response), fallback);
            SetCardDescriptionIfCurrent(index, requestVersion, aiDescription);
        }, error =>
        {
            Debug.LogWarning($"[TarorSingleSpreadShuffleUI] 卡牌描述 AI 生成失败，使用本地描述: {error}");
            SetCardDescriptionIfCurrent(index, requestVersion, fallback);
        });
    }

    private void SetCardDescriptionIfCurrent(int index, int requestVersion, string text)
    {
        if (uiComponent == null) return;
        if (_latestRevealedIndex != index || _cardInfoRequestVersion != requestVersion) return;

        _isCardInfoLoading = false;

        var descriptionText = GetStandaloneDescriptionText();
        if (descriptionText == null) return;

        descriptionText.gameObject.SetActive(true);
        descriptionText.text = text;

    }

    private TMP_Text GetStandaloneCardTitleText()
    {
        TMP_Text text = uiComponent.cardTitleText;
        if (text == null) return null;
        if (text == uiComponent.TitleTextText || text == uiComponent.SubtitleTextText) return null;
        return text;
    }

    private TMP_Text GetStandaloneDescriptionText()
    {
        TMP_Text text = uiComponent.InstructionTextText;
        if (text == null) return null;
        if (text == uiComponent.TitleTextText || text == uiComponent.SubtitleTextText || text == uiComponent.cardTitleText) return null;
        return text;
    }

    private List<DeepSeekAPI.Message> BuildCardDescriptionMessages(TarotCard card, bool upright, int index)
    {
        string position = GetPositionLabel(index);
        string spreadName = FirstNonEmpty(_spread?.label, GetDefaultTitle(_cardCount));
        string orientation = upright ? "正位" : "逆位";
        string keywords = card.keywords != null && card.keywords.Count > 0
            ? string.Join("、", card.keywords.GetRange(0, Mathf.Min(3, card.keywords.Count)))
            : card.nameZh;

        var payload = new ChatPayload
        {
            scene = "card_position_description",
            locale = "zh-CN",
            message = $"牌阵：{spreadName}\n"
                + $"位置：{position}\n"
                + $"卡牌：{card.nameZh}（{orientation}）\n"
                + $"关键词：{keywords}",
            user = new UserPayloadProfile
            {
                preferredTone = "tarot_reader",
                locale = "zh-CN"
            }
        };

        MemorySource memorySource = DialogSystem.Instance?.GetMemorySourceForPrompt();
        AssemblyResult assembly = ContextAssembler.AssembleSceneCall(
            "card_position_description",
            payload,
            memorySource,
            oracleVoiceId: "tarot_reader");

        if (assembly?.messages != null && assembly.messages.Count > 0)
            return assembly.messages.Select(message => new DeepSeekAPI.Message(message.role, message.content)).ToList();

        return new List<DeepSeekAPI.Message>
        {
            new DeepSeekAPI.Message("system", ScenePrompts.Get("card_position_description")),
            new DeepSeekAPI.Message("user", payload.message)
        };
    }

    private static string CleanAiCardDescription(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        string result = text.Trim()
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("卡牌描述：", "")
            .Replace("描述：", "")
            .Replace("解读：", "")
            .Trim();

        return result;
    }

    private string BuildCardInfoDescription(TarotCard card, bool upright, int index)
    {
        string position = GetPositionLabel(index);
        string keywords = card.keywords != null && card.keywords.Count > 0
            ? string.Join("、", card.keywords.GetRange(0, Mathf.Min(3, card.keywords.Count)))
            : card.nameZh;

        if (upright)
            return $"{position}抽到{FormatCardName(card, true)}。这张牌把「{keywords}」带到眼前，提醒你先确认一个已经变清楚的感受，再往前走。";

        return $"{position}抽到{FormatCardName(card, false)}。它把「{keywords}」里被卡住的部分翻出来，提醒你慢一点，看清阻碍来自哪里。";
    }

    private string GetPositionLabel(int index)
    {
        if (_spread?.positions != null && index >= 0 && index < _spread.positions.Count)
            return _spread.positions[index].label;

        return GetDefaultSlotLabel(index, _cardCount);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return "";
    }

    /// <summary>
    /// 洗牌动画 —— 快速轮换卡牌背面 + 快速缩放效果
    /// </summary>
    private IEnumerator PlayShuffleAnimation()
    {
        var visibleSlots = new List<CardSlotItem>();
        for (int i = 0; i < _cardCount && i < _slots.Length; i++)
        {
            if (ResolveSlotPoseRect(_slots[i]) != null)
                visibleSlots.Add(_slots[i]);
        }

        if (visibleSlots.Count == 0)
            yield break;

        var originalPositions = new Vector2[visibleSlots.Count];
        var originalRotations = new Quaternion[visibleSlots.Count];
        for (int i = 0; i < visibleSlots.Count; i++)
        {
            RectTransform rect = ResolveSlotPoseRect(visibleSlots[i]);
            if (rect == null) continue;

            originalPositions[i] = rect.anchoredPosition;
            originalRotations[i] = rect.localRotation;
        }

        float totalDuration = Mathf.Max(0.8f, uiComponent.shuffleCycleDuration * Mathf.Max(1, uiComponent.shuffleCycles) * Mathf.Max(1, visibleSlots.Count));
        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / totalDuration);
            float fade = Mathf.Sin(normalized * Mathf.PI);

            for (int i = 0; i < visibleSlots.Count; i++)
            {
                RectTransform rect = ResolveSlotPoseRect(visibleSlots[i]);
                if (rect == null) continue;

                float wave = elapsed * 18f + i * 0.85f;
                float scale = 1f + Mathf.Sin(wave) * 0.08f * fade;
                float rotation = Mathf.Sin(wave * 0.75f) * 7f * fade;
                float xOffset = Mathf.Sin(wave * 0.6f) * 12f * fade;
                float yOffset = Mathf.Cos(wave * 0.8f) * 4f * fade;

                rect.localScale = new Vector3(scale, scale, 1f);
                rect.localRotation = originalRotations[i] * Quaternion.Euler(0f, 0f, rotation);
                rect.anchoredPosition = originalPositions[i] + new Vector2(xOffset, yOffset);
            }

            yield return null;
        }

        for (int i = 0; i < visibleSlots.Count; i++)
        {
            RectTransform rect = ResolveSlotPoseRect(visibleSlots[i]);
            if (rect == null) continue;

            rect.localScale = Vector3.one;
            rect.localRotation = originalRotations[i];
            rect.anchoredPosition = originalPositions[i];
        }

        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>
    /// 翻牌动画 —— 水平缩放模拟翻转（与 InteractionCard 一致）
    /// </summary>
    private IEnumerator FlipSlotCard(CardSlotItem slot, (TarotCard card, bool upright) draw)
    {
        if (slot == null)
            yield break;

        RectTransform poseRect = ResolveSlotPoseRect(slot);
        if (poseRect == null)
        {
            SetSlotFace(slot, draw);
            yield break;
        }

        float halfDuration = uiComponent.flipDuration / 2f;
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float scale = 1f - (t * t);
            poseRect.localScale = new Vector3(scale, 1f, 1f);
            yield return null;
        }

        SetSlotFace(slot, draw);

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float scale = 1f - (1f - t) * (1f - t);
            poseRect.localScale = new Vector3(scale, 1f, 1f);
            yield return null;
        }

        poseRect.localScale = Vector3.one;
    }

    private IEnumerator FlipCard(Image cardImage, (TarotCard card, bool upright) draw)
    {
        if (cardImage == null) yield break;

        Sprite frontSprite = LoadCardSprite(draw.card.cardId);
        float halfDuration = uiComponent.flipDuration / 2f;

        // 阶段 1：缩小到 0（翻面）
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float scale = 1f - (t * t); // ease-in quad
            cardImage.transform.localScale = new Vector3(scale, 1f, 1f);
            yield return null;
        }

        // 切换图片
        cardImage.enabled = true;
        cardImage.color = Color.white;
        cardImage.sprite = frontSprite ?? ResolveCardBackSprite();

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
            float scale = 1f - (1f - t) * (1f - t); // ease-out quad
            cardImage.transform.localScale = new Vector3(scale, 1f, 1f);
            yield return null;
        }

        cardImage.transform.localScale = Vector3.one;
    }

    #endregion

    #region 辅助方法

    private void StopShuffleCoroutine()
    {
        if (_shuffleCoroutine != null)
        {
            uiComponent.StopCoroutine(_shuffleCoroutine);
            _shuffleCoroutine = null;
        }
    }

    private void KillDeckAnimation()
    {
        StopDeckInertia();
        StopLongPressSelection();

        _drawSequence?.Kill(false);
        _drawSequence = null;

        for (int i = 0; i < _deckImages.Count; i++)
        {
            Image image = _deckImages[i];
            if (image == null) continue;
            DOTween.Kill(image);
            DOTween.Kill(image.rectTransform);
            UnityEngine.Object.Destroy(image.gameObject);
        }
        _deckImages.Clear();

        if (_cardTemplateImage != null)
            _cardTemplateImage.gameObject.SetActive(false);
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform match = FindChildRecursive(child, childName);
            if (match != null)
                return match;
        }

        return null;
    }

    private Sprite ResolveCardBackSprite()
    {
        if (uiComponent.cardBackSprite != null) return uiComponent.cardBackSprite;

        uiComponent.cardBackSprite = Resources.Load<Sprite>("TarotCards/CardBack");
        if (uiComponent.cardBackSprite != null) return uiComponent.cardBackSprite;

        uiComponent.cardBackSprite = Resources.Load<Sprite>("CardBack");
        return uiComponent.cardBackSprite;
    }

    private void ResetVisibleSlotsToBack()
    {
        Sprite backSprite = ResolveCardBackSprite();
        for (int i = 0; i < _cardCount && i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null) continue;

            string label = _spread?.positions != null && i < _spread.positions.Count
                ? _spread.positions[i].label
                : GetDefaultSlotLabel(i, _cardCount);
            ResetSlotToBack(slot, label, backSprite);
        }
    }

    private void ResetSlotToBack(CardSlotItem slot, string label, Sprite backSprite)
    {
        if (slot == null) return;

        slot.gameObject.SetActive(true);
        slot.ShowBack(backSprite, label);
    }

    private RectTransform ResolveSlotPoseRect(CardSlotItem slot)
    {
        if (slot == null) return null;

        slot.ResolveReferences();
        return slot.GetPoseRect();
    }

    /// <summary>
    /// 加载卡牌正面图片
    /// </summary>
    private Sprite LoadCardSprite(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return ResolveCardBackSprite();

        Sprite sprite = TarotSpriteLoader.Load(cardId);
        if (sprite != null) return sprite;

        sprite = Resources.Load<Sprite>($"TarotCards/{cardId}");
        if (sprite != null) return sprite;

        Debug.LogWarning($"[TarorSingleSpreadShuffleUI] 未找到塔罗牌正面资源: {cardId}");
        return ResolveCardBackSprite();
    }

    private static string FormatCardName(TarotCard card, bool upright)
    {
        if (card == null) return "";
        return $"{card.nameZh}·{(upright ? "正位" : "逆位")}";
    }

    /// <summary>
    /// 同步抽牌结果到 DivinationEngine
    /// </summary>
    private void SyncToDivinationEngine()
    {
        if (DivinationEngine.Instance == null) return;

        var session = DivinationEngine.Instance.CurrentSession;
        if (session == null) return;

        var spreadKind = _spread?.kind ?? "self_repair";
        var lockedList = new List<GamerFrameWork.OracleRuntime.LockedCard>();

        for (int i = 0; i < _drawnCards.Count; i++)
        {
            var (card, upright) = _drawnCards[i];
            string posKey = i < (_spread?.positions?.Count ?? 0)
                ? _spread.positions[i].key
                : $"pos_{i + 1}";
            string posLabel = i < (_spread?.positions?.Count ?? 0)
                ? _spread.positions[i].label
                : GetDefaultSlotLabel(i, _cardCount);

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
        session.divinationPlan = DivinationEngine.Instance.BuildActiveDivinationPlan(spreadKind);
        session.phase = DivinationPhase.CardsLocked;

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
            dialogSystem.SetActiveDivinationPlan(session.divinationPlan);
        }

        Debug.Log($"[TarorSingleSpreadShuffleUI] 已同步 {lockedList.Count} 张牌到 DivinationEngine, spreadKind={spreadKind}");
    }

    #endregion

    #region API Function

    #endregion
}
