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
    private const string DetailEntryPrompt = "点击任意位置查看三牌占卜详情";

    private const float DeckClipHorizontalPadding = 24f;
    private const float DeckVisibleSlotCount = 7f;
    private const float DeckCardSpacing = 12f;
    private const int VisualDeckCardCount = 12;
    private const float DefaultRevealMaskAlpha = 0.8f;
    private const float DefaultShuffleScatterDuration = 1.6f;
    private const float DefaultShuffleGatherDuration = 0.83f;

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
    private DashScopeAPI _dashScopeAPI;

    // CardSlotItem 数组（懒加载）
    private CardSlotItem[] _slots;

    private readonly List<Image> _deckImages = new List<Image>();
    private RectTransform _cardContainer;
    private RectTransform _deckClipRoot;
    private Image _cardTemplateImage;
    private Tween _deckInertiaTween;
    private Sequence _drawSequence;
    private Sequence _deckShuffleSequence;
    private readonly List<int> _deckSiblingOrder = new List<int>();
    private Coroutine _singleClickCoroutine;
    private Coroutine _longPressCoroutine;
    private Coroutine _groupReadingLoadingCoroutine;
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
    private float _minDeckOffsetX;
    private float _maxDeckOffsetX;
    private float _deckCardScale = 1f;
    private Vector2 _lastDeckViewportSize;
    private float _revealMaskTargetAlpha = DefaultRevealMaskAlpha;
    private int _groupReadingLoadingVersion;
    private int _pendingClickDeckIndex = -1;
    private float _lastDeckClickTime = -100f;

    #region 生命周期函数

    public override void OnAwake()
    {
        uiComponent = gameObject.GetComponent<TarorSingleSpreadShuffleUIComponent>();
        uiComponent.InitComponent(this);
        ConfigureDeckSettings();
        AddButtonClickListener(uiComponent.hideBtn, OnHideBtnClick);
        SetBackButtonVisible(false);
        SetDetailEntryVisible(false);
        _dashScopeAPI = DashScopeAPI.ResolveFor(gameObject);
        ResolveRevealMaskReference();
        SetRevealMaskVisible(false, true);
        this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
        base.OnAwake();
    }

    public override void OnShow()
    {
        base.OnShow();
        StopShuffleCoroutine();
        KillDeckAnimation();
        StopGroupReadingLoading();
        _shuffleDone = false;
        _cardsReadyToDraw = false;
        _isChoosingCard = false;
        _isAnimatingCard = false;
        _nextDrawIndex = 0;
        _latestRevealedIndex = -1;
        _cardInfoRequestVersion++;
        _isCardInfoLoading = false;
        _drawnCards = null;
        SetBackButtonVisible(false);
        SetDetailEntryVisible(false);

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
        SetRevealMaskVisible(false, true);
        ClearRuntimeDeckCards();

        // 配置界面
        ConfigureUI();
        PrepareInteractiveDeck();

        Debug.Log($"[TarorSingleSpreadShuffleUI] OnShow, cardCount={_cardCount}, spread={_spread?.label}");
    }

    public override void OnHide()
    {
        base.OnHide();
        StopShuffleCoroutine();
        StopGroupReadingLoading();
        KillDeckAnimation();
        ClearRuntimeDeckCards();
        _cardInfoRequestVersion++;
        SetDetailEntryVisible(false);
    }

    public override void OnDestroy()
    {
        StopShuffleCoroutine();
        StopGroupReadingLoading();
        KillDeckAnimation();
        ClearRuntimeDeckCards();
        _cardInfoRequestVersion++;
        base.OnDestroy();
    }

    private void LateUpdate()
    {
        if (!_isChoosingCard || _deckImages.Count <= 1)
            return;

        RefreshDeckLayoutIfViewportChanged();
        SanitizeDeckVisuals();
        RefreshDeckSiblingOrder();
    }

    #endregion

    #region UI 配置

    private void BuildSlotArray()
    {
        var comp = uiComponent;
        var slots = new List<CardSlotItem>
        {
            comp.CardSlotItem1CardSlotItem,
            comp.CardSlotItem2CardSlotItem,
            comp.CardSlotItem3CardSlotItem,
        };

        CardSlotItem[] sceneSlots = gameObject.GetComponentsInChildren<CardSlotItem>(true);
        for (int i = 0; i < sceneSlots.Length; i++)
        {
            CardSlotItem slot = sceneSlots[i];
            if (slot != null && !slots.Contains(slot))
                slots.Add(slot);
        }

        slots.RemoveAll(slot => slot == null);
        slots.Sort(CompareSlotOrder);
        _slots = slots.ToArray();
    }

    private static int CompareSlotOrder(CardSlotItem left, CardSlotItem right)
    {
        if (left == right) return 0;
        if (left == null) return 1;
        if (right == null) return -1;

        int leftNumber = ExtractLastNumber(left.gameObject.name);
        int rightNumber = ExtractLastNumber(right.gameObject.name);
        if (leftNumber != rightNumber)
            return leftNumber.CompareTo(rightNumber);

        return left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
    }

    private static int ExtractLastNumber(string text)
    {
        if (string.IsNullOrEmpty(text)) return int.MaxValue;

        int value = 0;
        int multiplier = 1;
        bool found = false;
        for (int i = text.Length - 1; i >= 0; i--)
        {
            char c = text[i];
            if (c >= '0' && c <= '9')
            {
                found = true;
                value += (c - '0') * multiplier;
                multiplier *= 10;
                continue;
            }

            if (found)
                break;
        }

        return found ? value : int.MaxValue;
    }

    /// <summary>
    /// 根据牌阵信息配置标题、副标题、卡槽显示
    /// </summary>
    private void ConfigureUI()
    {
        var comp = uiComponent;

        SetSpreadTitle();
        SetOperationStepText($"点击、长按，或向上拖动牌扇中的牌，依次抽出{_cardCount}张牌。");
        ClearCardInfoTexts();
        SetDetailEntryVisible(false);

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

        SetBackButtonVisible(false);
    }

    #endregion

    #region 默认文本

    private string GetDefaultTitle(int count) => count switch
    {
        1 => "单张镜像牌阵",
        5 => "五牌选择门",
        _ => "当前关注·三牌占卜"
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

    private void SetSpreadTitle()
    {
        if (uiComponent.TitleTextText == null) return;

        string text = _cardCount == 3
            ? "当前关注·三牌占卜"
            : GetDefaultTitle(_cardCount);

        uiComponent.TitleTextText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        uiComponent.TitleTextText.text = text;
    }

    private void SetOperationStepText(string text, bool trimPunctuation = true)
    {
        if (uiComponent.SubtitleTextText == null) return;

        if (CanEnterDetail())
            text = DetailEntryPrompt;

        if (trimPunctuation)
            text = TrimSubtitlePunctuation(text);
        uiComponent.SubtitleTextText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        uiComponent.SubtitleTextText.text = text;
    }

    private void SetBackButtonVisible(bool visible)
    {
        if (uiComponent?.BackButton != null)
            uiComponent.BackButton.gameObject.SetActive(visible);
    }

    private static string TrimSubtitlePunctuation(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return text.Trim().TrimEnd('。', '.', '！', '!', '？', '?');
    }

    private string BuildQuestionKeywordTitle()
    {
        string question = FirstNonEmpty(
            SpreadShuffleBridge.PendingMessageData?.divinationQuestion,
            DivinationEngine.Instance?.CurrentSession?.divinationPlan?.question,
            DivinationEngine.Instance?.CurrentSession?.question,
            SpreadShuffleBridge.PendingMessageData?.content);

        string keyword = BuildQuestionKeyword(question);
        return FirstNonEmpty(keyword, "当前问题");
    }

    private static string BuildQuestionKeyword(string question)
    {
        string value = NormalizeQuestionText(question);
        if (string.IsNullOrWhiteSpace(value)) return "";

        string mapped = MapQuestionKeyword(value);
        if (!string.IsNullOrWhiteSpace(mapped))
            return mapped;

        value = StripQuestionNoise(value);
        if (string.IsNullOrWhiteSpace(value)) return "";

        return value.Length > 10 ? value.Substring(0, 10) : value;
    }

    private static string MapQuestionKeyword(string value)
    {
        string lower = value.ToLowerInvariant();

        if (ContainsAny(lower, "真实感受", "真实想法", "怎么想"))
            return "真实感受";
        if (ContainsAny(lower, "复合", "回来", "回头"))
            return "复合可能";
        if (ContainsAny(lower, "关系") && ContainsAny(lower, "未来", "走向", "发展"))
            return "关系走向";
        if (ContainsAny(lower, "喜欢", "暧昧", "感情", "对方", "他", "她", "ta"))
            return "情感关系";
        if (ContainsAny(lower, "关注", "最需要"))
            return "当前关注";
        if (ContainsAny(lower, "选择", "要不要", "该不该", "二选一"))
            return "选择判断";
        if (ContainsAny(lower, "工作", "事业", "职业"))
            return "事业方向";
        if (ContainsAny(lower, "机会", "回报", "结果"))
            return "机会结果";
        if (ContainsAny(lower, "焦虑", "不安", "害怕", "担心"))
            return "内在不安";
        if (ContainsAny(lower, "放下", "执念", "消耗"))
            return "需要放下的事";
        if (ContainsAny(lower, "行动", "怎么做", "怎么办", "下一步"))
            return "下一步行动";

        return "";
    }

    private static string NormalizeQuestionText(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return "";

        string value = question.Trim();
        value = value.Replace("\r", " ").Replace("\n", " ");
        value = value.Replace("“", "").Replace("”", "").Replace("\"", "");
        value = value.Replace("？", "").Replace("?", "").Replace("。", "").Replace(".", "");
        value = value.Replace("，", " ").Replace(",", " ").Replace("：", " ").Replace(":", " ");

        while (value.Contains("  "))
            value = value.Replace("  ", " ");

        return value.Trim();
    }

    private static string StripQuestionNoise(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        string result = value.Trim();
        string[] noise =
        {
            "帮我", "请帮我", "我想知道", "我想问", "我当前", "我现在", "我的",
            "是什么", "什么是", "什么", "为何", "为什么", "怎样", "如何",
            "请问", "占卜", "塔罗", "抽牌", "三张牌", "三牌", "牌阵",
            "一个", "一下", "看看", "看一看", "关于"
        };

        foreach (string token in noise)
            result = result.Replace(token, "");

        return result.Trim(' ', '的', '了', '吗', '呢', '吧', '？', '?', '。', '.', '，', ',');
    }

    private static bool ContainsAny(string value, params string[] keywords)
    {
        if (string.IsNullOrEmpty(value) || keywords == null) return false;
        foreach (string keyword in keywords)
        {
            if (!string.IsNullOrEmpty(keyword) && value.Contains(keyword))
                return true;
        }
        return false;
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



    public void OnHideBtnClick()
    {
        if (!CanEnterDetail())
            return;

        CompleteShuffle();
    }

    private void ResolveDeckReferences()
    {
        ConfigureDeckSettings();
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

        ResolveRevealMaskReference();

        if (_cardTemplateImage != null)
        {
            _cardTemplateImage.sprite = ResolveCardBackSprite();
            _cardTemplateImage.color = Color.white;
            _cardTemplateImage.raycastTarget = false;
            _cardTemplateImage.gameObject.SetActive(false);
        }

        _deckClipRoot = null;
        ClearGeneratedDeckChildren();

        if (_cardContainer != null)
            BindDeckDragSurface(_cardContainer.gameObject);
    }

    private void ConfigureDeckClipRoot()
    {
        _deckClipRoot = null;
        if (_cardContainer == null) return;

        const string clipRootName = "DeckClipRoot";
        Transform existing = _cardContainer.Find(clipRootName);
        if (existing == null)
        {
            GameObject clipObject = new GameObject(clipRootName, typeof(RectTransform));
            clipObject.layer = _cardContainer.gameObject.layer;
            existing = clipObject.transform;
            existing.SetParent(_cardContainer, false);
        }

        _deckClipRoot = existing as RectTransform;
        if (_deckClipRoot == null) return;

        _deckClipRoot.anchorMin = Vector2.zero;
        _deckClipRoot.anchorMax = Vector2.one;
        _deckClipRoot.pivot = new Vector2(0.5f, 0.5f);
        _deckClipRoot.anchoredPosition = Vector2.zero;
        _deckClipRoot.sizeDelta = new Vector2(-DeckClipHorizontalPadding * 2f, 0f);
        _deckClipRoot.localRotation = Quaternion.identity;
        _deckClipRoot.localScale = Vector3.one;
        _deckClipRoot.SetAsLastSibling();

        RectMask2D clipMask = _deckClipRoot.GetComponent<RectMask2D>();
        if (clipMask == null)
            clipMask = _deckClipRoot.gameObject.AddComponent<RectMask2D>();
        clipMask.enabled = true;

        Image raycastSurface = _deckClipRoot.GetComponent<Image>();
        if (raycastSurface == null)
            raycastSurface = _deckClipRoot.gameObject.AddComponent<Image>();
        raycastSurface.enabled = true;
        raycastSurface.color = Color.clear;
        raycastSurface.raycastTarget = true;
    }

    private RectTransform GetDeckViewportRoot()
    {
        return _cardContainer;
    }

    private Image ResolveRevealMaskReference()
    {
        if (uiComponent == null) return null;

        if (uiComponent.maskUI == null)
        {
            Transform maskTransform = FindChildRecursive(transform, "MaskUI");
            if (maskTransform != null)
                uiComponent.maskUI = maskTransform.GetComponent<Image>();
        }

        if (uiComponent.maskUI != null)
        {
            Color maskColor = uiComponent.maskUI.color;
            if (maskColor.a > 0.01f)
                _revealMaskTargetAlpha = maskColor.a;

            uiComponent.maskUI.raycastTarget = false;
        }

        return uiComponent.maskUI;
    }

    private void SetRevealMaskVisible(bool visible, bool immediate)
    {
        Image mask = ResolveRevealMaskReference();
        if (mask == null) return;

        DOTween.Kill(mask);
        float fadeDuration = Mathf.Max(0f, uiComponent.centerRevealMaskFadeDuration);
        if (_revealMaskTargetAlpha <= 0.01f)
            _revealMaskTargetAlpha = DefaultRevealMaskAlpha;

        if (visible)
        {
            mask.gameObject.SetActive(true);
            mask.enabled = true;
            mask.raycastTarget = false;
            mask.transform.SetAsLastSibling();

            Color color = mask.color;
            if (immediate || fadeDuration <= 0f)
            {
                color.a = _revealMaskTargetAlpha;
                mask.color = color;
                return;
            }

            color.a = 0f;
            mask.color = color;
            mask.DOFade(_revealMaskTargetAlpha, fadeDuration).SetTarget(mask);
            return;
        }

        if (immediate || fadeDuration <= 0f)
        {
            Color color = mask.color;
            color.a = _revealMaskTargetAlpha;
            mask.color = color;
            mask.enabled = false;
            mask.gameObject.SetActive(false);
            return;
        }

        mask.DOFade(0f, fadeDuration)
            .SetTarget(mask)
            .OnComplete(() =>
            {
                if (mask == null) return;

                Color color = mask.color;
                color.a = _revealMaskTargetAlpha;
                mask.color = color;
                mask.enabled = false;
                mask.gameObject.SetActive(false);
            });
    }

    private void ConfigureDeckSettings()
    {
        if (uiComponent == null) return;

        uiComponent.selectableCardCount = Mathf.Max(uiComponent.selectableCardCount, VisualDeckCardCount);
        uiComponent.fanWidth = 0f;
        uiComponent.fanViewportWidthMultiplier = Mathf.Max(uiComponent.fanViewportWidthMultiplier, 1.42f);
        uiComponent.minFanWidth = Mathf.Max(uiComponent.minFanWidth, 1120f);
        uiComponent.fanRiseOffset = uiComponent.fanRiseOffset <= 0f ? 108f : uiComponent.fanRiseOffset;
        uiComponent.fanRotation = uiComponent.fanRotation <= 0f ? 18f : uiComponent.fanRotation;
        uiComponent.deckCardScale = 0f;
        uiComponent.minDeckCardScale = Mathf.Max(uiComponent.minDeckCardScale, 0.5f);
        uiComponent.maxDeckCardScale = Mathf.Max(uiComponent.maxDeckCardScale, 0.64f);
        uiComponent.drawnFanLowerOffset = 0f;
        uiComponent.drawnFanScaleMultiplier = Mathf.Clamp(uiComponent.drawnFanScaleMultiplier <= 0f ? 0.88f : uiComponent.drawnFanScaleMultiplier, 0.68f, 1f);
        uiComponent.flipRevealScaleMultiplier = Mathf.Max(uiComponent.flipRevealScaleMultiplier, 1.16f);
        uiComponent.cardRevealGap = Mathf.Max(uiComponent.cardRevealGap, 0.85f);
    }

    private void PrepareInteractiveDeck()
    {
        ResetVisibleSlotsToBack();
        ClearRuntimeDeckCards();

        _drawnCards = TarotDeck.DrawMultiple(_cardCount);
        SyncToDivinationEngine();

        _cardsReadyToDraw = false;
        _isChoosingCard = false;
        _isAnimatingCard = true;
        _nextDrawIndex = 0;
        _deckOffsetX = 0f;
        _scrollVelocityX = 0f;
        _lastDeckViewportSize = Vector2.zero;

        BuildDeckFan();

        SetSpreadTitle();
        SetOperationStepText("正在洗牌", false);
        SetDetailEntryVisible(false);
        PlayDeckShuffleIntro();
    }

    private string GetDrawInstructionText()
    {
        if (_nextDrawIndex >= _cardCount)
            return _cardCount == 3 ? DetailEntryPrompt : "牌阵已经就位";

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

        RectTransform viewportRoot = GetDeckViewportRoot();
        Rect rect = viewportRoot != null ? viewportRoot.rect : _cardContainer.rect;
        _viewportWidth = Mathf.Max(1f, rect.width);
        _viewportHeight = Mathf.Max(1f, rect.height);
        _fanWidth = uiComponent.fanWidth > 0f
            ? uiComponent.fanWidth
            : Mathf.Max(uiComponent.minFanWidth, _viewportWidth * Mathf.Max(1f, uiComponent.fanViewportWidthMultiplier));
        _deckCardScale = ResolveDeckCardScale();

        int count = GetVisualDeckCardCount();
        _fanWidth = ResolveDeckFanWidth(count);
        ResolveDeckScrollBounds();
        Sprite backSprite = ResolveCardBackSprite();
        Transform deckParent = _cardContainer;
        for (int i = 0; i < count; i++)
        {
            GameObject cardObject = UnityEngine.Object.Instantiate(_cardTemplateImage.gameObject, deckParent);
            cardObject.name = $"SpreadDeckCard_{i + 1}";
            cardObject.SetActive(true);

            Image image = cardObject.GetComponent<Image>();
            if (image == null)
                image = cardObject.AddComponent<Image>();

            image.sprite = backSprite;
            image.color = Color.white;
            image.raycastTarget = true;
            EnsureDeckCardCanvas(cardObject);

            RectTransform cardRect = image.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);

            int capturedIndex = i;
            BindDeckCardDrag(cardObject, capturedIndex);
            _deckImages.Add(image);
        }

        PlaceDeckAtStackPose();
        SetDeckCardsInteractable(false);
    }

    private void PlayDeckShuffleIntro()
    {
        StopDeckShuffleSequence();
        StopDeckInertia();
        StopLongPressSelection();
        StopPendingSingleClick();

        if (_deckImages.Count == 0)
        {
            FinishDeckShuffleIntro();
            return;
        }

        _deckOffsetX = 0f;
        _isChoosingCard = false;
        _isAnimatingCard = true;
        SetDeckCardsInteractable(false);
        SetOperationStepText("正在洗牌", false);
        PlayDeckScatterAnimation(() => PlayDeckGatherAnimation(PlayDeckFanOutFromStack));
    }

    private void PlayDeckScatterAnimation(Action onComplete)
    {
        StopDeckShuffleSequence();
        ResolveDeckLayoutMetrics();

        _deckShuffleSequence = DOTween.Sequence();
        float scatterDuration = Mathf.Max(0.58f, uiComponent.shuffleScatterDuration > 0f
            ? uiComponent.shuffleScatterDuration
            : DefaultShuffleScatterDuration);

        for (int i = 0; i < _deckImages.Count; i++)
        {
            Image image = _deckImages[i];
            if (image == null) continue;

            RectTransform rect = image.rectTransform;
            DOTween.Kill(image);
            DOTween.Kill(rect);

            float delay = Mathf.Lerp(0f, 0.18f, Hash01(i * 23 + 5));
            float duration = Mathf.Max(0.58f, scatterDuration - delay - Mathf.Lerp(0.04f, 0.16f, Hash01(i * 19 + 7)));

            _deckShuffleSequence.Insert(delay, rect.DOAnchorPos(GetDeckChaosPose(i, _deckImages.Count), duration)
                .SetEase(Ease.InOutCubic));
            _deckShuffleSequence.Insert(delay, rect.DORotate(new Vector3(0f, 0f, GetDeckChaosRotation(i)), duration)
                .SetEase(Ease.InOutCubic));
            _deckShuffleSequence.Insert(delay, rect.DOScale(Vector3.one * _deckCardScale, duration)
                .SetEase(Ease.InOutCubic));
        }

        _deckShuffleSequence.AppendInterval(Mathf.Max(0f, scatterDuration - _deckShuffleSequence.Duration()));
        _deckShuffleSequence.OnComplete(() =>
        {
            _deckShuffleSequence = null;
            onComplete?.Invoke();
        });
    }

    private void PlayDeckGatherAnimation(Action onComplete)
    {
        StopDeckShuffleSequence();
        ResolveDeckLayoutMetrics();

        _deckShuffleSequence = DOTween.Sequence();
        float gatherDuration = Mathf.Max(0.28f, uiComponent.shuffleGatherDuration > 0f
            ? uiComponent.shuffleGatherDuration
            : DefaultShuffleGatherDuration);

        for (int i = 0; i < _deckImages.Count; i++)
        {
            Image image = _deckImages[i];
            if (image == null) continue;

            RectTransform rect = image.rectTransform;
            DOTween.Kill(rect);
            _deckShuffleSequence.Insert(0f, rect.DOAnchorPos(GetDeckShuffleStackPose(i, _deckImages.Count), gatherDuration)
                .SetEase(Ease.OutBack, 0.95f));
            _deckShuffleSequence.Insert(0f, rect.DORotate(new Vector3(0f, 0f, GetDeckShuffleStackRotation(i)), gatherDuration)
                .SetEase(Ease.OutBack, 0.95f));
            _deckShuffleSequence.Insert(0f, rect.DOScale(Vector3.one * _deckCardScale, gatherDuration)
                .SetEase(Ease.OutBack, 0.95f));
        }

        _deckShuffleSequence.OnComplete(() =>
        {
            _deckShuffleSequence = null;
            onComplete?.Invoke();
        });
    }

    private void PlayDeckFanOutFromStack()
    {
        StopDeckShuffleSequence();
        ResolveDeckLayoutMetrics();

        _deckShuffleSequence = DOTween.Sequence();
        float fanOutDuration = Mathf.Max(0.32f, uiComponent.shuffleFanOutDuration);
        float fanOutGap = Mathf.Max(0.035f, uiComponent.shuffleFanOutGap);
        List<int> fanOutOrder = BuildLeftToRightDeckFanOutOrder();

        for (int orderIndex = 0; orderIndex < fanOutOrder.Count; orderIndex++)
        {
            int deckIndex = fanOutOrder[orderIndex];
            Image image = _deckImages[deckIndex];
            if (image == null) continue;

            RectTransform rect = image.rectTransform;
            DOTween.Kill(image);
            DOTween.Kill(rect);

            Vector2 sourcePosition = rect.anchoredPosition;
            Vector2 targetPosition = GetDeckFanPosition(deckIndex);
            float targetRotation = GetDeckFanRotation(deckIndex);
            float lift = Mathf.Clamp(_viewportHeight * 0.03f, 28f, 58f);
            Vector2 stagingPosition = Vector2.Lerp(sourcePosition, targetPosition, 0.54f) + new Vector2(0f, lift);
            Vector3 targetScale = Vector3.one * (_deckCardScale * GetSettledDeckScaleMultiplier());

            float startTime = orderIndex * fanOutGap;
            float liftDuration = fanOutDuration * 0.42f;
            float settleDuration = fanOutDuration - liftDuration;
            Image capturedImage = image;
            int capturedOrderIndex = orderIndex;
            Sequence cardSequence = DOTween.Sequence();
            cardSequence.Append(rect.DOAnchorPos(stagingPosition, liftDuration).SetEase(Ease.OutCubic));
            cardSequence.Append(rect.DOAnchorPos(targetPosition, settleDuration).SetEase(Ease.OutQuart));

            _deckShuffleSequence.InsertCallback(startTime, () => BringDeckFanOutCardToTop(capturedImage, capturedOrderIndex));
            _deckShuffleSequence.Insert(startTime, image.DOFade(1f, fanOutDuration * 0.45f));
            _deckShuffleSequence.Insert(startTime, cardSequence);
            _deckShuffleSequence.Insert(startTime, rect.DORotate(new Vector3(0f, 0f, targetRotation), fanOutDuration)
                .SetEase(Ease.InOutCubic));
            _deckShuffleSequence.Insert(startTime, rect.DOScale(targetScale * 1.025f, liftDuration).SetEase(Ease.OutSine));
            _deckShuffleSequence.Insert(startTime + liftDuration, rect.DOScale(targetScale, settleDuration).SetEase(Ease.OutCubic));
        }

        _deckShuffleSequence.OnComplete(FinishDeckShuffleIntro);
    }

    private void FinishDeckShuffleIntro()
    {
        _deckShuffleSequence = null;
        _cardsReadyToDraw = true;
        _isChoosingCard = true;
        _isAnimatingCard = false;
        RefreshDeckSiblingOrder();
        SetDeckCardsInteractable(true);
        SetOperationStepText(GetDrawInstructionText());
    }

    private void PlaceDeckAtStackPose()
    {
        ResolveDeckLayoutMetrics();

        for (int i = 0; i < _deckImages.Count; i++)
        {
            Image image = _deckImages[i];
            if (image == null) continue;

            RectTransform rect = image.rectTransform;
            DOTween.Kill(image);
            DOTween.Kill(rect);
            image.enabled = true;
            image.color = Color.white;
            image.raycastTarget = false;
            rect.anchoredPosition = GetDeckShuffleStackPose(i, _deckImages.Count);
            rect.localRotation = Quaternion.Euler(0f, 0f, GetDeckShuffleStackRotation(i));
            rect.localScale = Vector3.one * _deckCardScale;
        }

        RefreshDeckSiblingOrder();
    }

    private List<int> BuildLeftToRightDeckFanOutOrder()
    {
        List<int> order = new List<int>(_deckImages.Count);
        for (int i = 0; i < _deckImages.Count; i++)
        {
            if (_deckImages[i] != null)
                order.Add(i);
        }

        order.Sort((left, right) =>
        {
            float leftX = GetDeckFanPosition(left).x;
            float rightX = GetDeckFanPosition(right).x;
            int xCompare = leftX.CompareTo(rightX);
            return xCompare != 0 ? xCompare : left.CompareTo(right);
        });

        return order;
    }

    private void BringDeckFanOutCardToTop(Image image, int orderIndex)
    {
        if (image == null) return;

        image.rectTransform.SetAsLastSibling();
        SetDeckCardSortingOrder(image, _deckImages.Count + 30 + orderIndex);
    }

    private Vector2 GetDeckShuffleAnchor()
    {
        return new Vector2(0f, Mathf.Clamp(_viewportHeight * 0.1f, 58f, 128f));
    }

    private Vector2 GetDeckShuffleStackPose(int index, int count)
    {
        Vector2 anchor = GetDeckShuffleAnchor();
        float centeredIndex = index - (count - 1) * 0.5f;
        float x = anchor.x + centeredIndex * 0.34f + Mathf.Sin(index * 0.61f) * 2.2f;
        float y = anchor.y - centeredIndex * 0.5f + (index % 7) * 0.55f;
        return new Vector2(x, y);
    }

    private float GetDeckShuffleStackRotation(int index)
    {
        return Mathf.Sin(index * 0.7f) * 1.05f;
    }

    private Vector2 GetDeckChaosPose(int index, int count)
    {
        Vector2 anchor = GetDeckShuffleAnchor();
        float maxRadius = Mathf.Clamp(_viewportWidth * 0.23f, 132f, 220f);
        float radius = Mathf.Lerp(44f, maxRadius, Hash01(index * 17 + 3));
        float angle = (index * 137.507f + Hash01(index * 31 + 9) * 48f) * Mathf.Deg2Rad;
        float x = Mathf.Cos(angle) * radius;
        float y = Mathf.Sin(angle) * radius * 0.68f + Mathf.Sin(index * 0.33f) * 18f;
        return anchor + new Vector2(x, y);
    }

    private float GetDeckChaosRotation(int index)
    {
        return Mathf.Lerp(-34f, 34f, Hash01(index * 29 + 11));
    }

    private static float Hash01(int seed)
    {
        return Mathf.Repeat(Mathf.Sin(seed * 12.9898f) * 43758.5453f, 1f);
    }

    private float ResolveDeckCardScale()
    {
        if (uiComponent.deckCardScale > 0f)
            return uiComponent.deckCardScale;

        float sourceWidth = ResolveDeckCardSourceWidth();
        float sourceHeight = ResolveDeckCardSourceHeight();
        float widthScale = (_viewportWidth * 0.34f) / Mathf.Max(1f, sourceWidth);
        float heightScale = (_viewportHeight * 0.56f) / Mathf.Max(1f, sourceHeight);
        return Mathf.Clamp(Mathf.Min(widthScale, heightScale), uiComponent.minDeckCardScale, uiComponent.maxDeckCardScale);
    }

    private int GetVisualDeckCardCount()
    {
        return Mathf.Max(3, uiComponent.selectableCardCount);
    }

    private float ResolveFittedDeckCardScale(float sourceWidth, float sourceHeight)
    {
        float availableWidth = Mathf.Max(1f, _viewportWidth - DeckCardSpacing * (DeckVisibleSlotCount - 1f));
        float widthScale = (availableWidth / DeckVisibleSlotCount) / Mathf.Max(1f, sourceWidth);
        float heightScale = (_viewportHeight * 0.56f) / Mathf.Max(1f, sourceHeight);
        return Mathf.Max(0.1f, Mathf.Min(widthScale, heightScale));
    }

    private float ResolveDeckFanWidth(int cardCount)
    {
        if (uiComponent.fanWidth > 0f)
            return uiComponent.fanWidth;

        return Mathf.Max(uiComponent.minFanWidth, _viewportWidth * Mathf.Max(1f, uiComponent.fanViewportWidthMultiplier));
    }

    private float ResolveDeckCardSourceWidth()
    {
        RectTransform templateRect = _cardTemplateImage != null ? _cardTemplateImage.rectTransform : null;
        return templateRect != null ? Mathf.Max(1f, templateRect.rect.width) : 285f;
    }

    private float ResolveDeckCardSourceHeight()
    {
        RectTransform templateRect = _cardTemplateImage != null ? _cardTemplateImage.rectTransform : null;
        return templateRect != null ? Mathf.Max(1f, templateRect.rect.height) : 487f;
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

        _deckOffsetX = NormalizeDeckOffset(_dragStartOffsetX + deltaX * Mathf.Max(0.01f, uiComponent.dragSensitivity));

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

        float now = Time.unscaledTime;
        float interval = Mathf.Clamp(uiComponent.doubleClickSelectInterval, 0.05f, 0.6f);
        bool doubleClick = eventData is PointerEventData pointerData && pointerData.clickCount >= 2;
        doubleClick = doubleClick || (_pendingClickDeckIndex == index && now - _lastDeckClickTime <= interval);

        if (doubleClick)
        {
            StopPendingSingleClick();
            SelectDeckCard(index);
            return;
        }

        StopPendingSingleClick();
        _pendingClickDeckIndex = index;
        _lastDeckClickTime = now;
        _singleClickCoroutine = uiComponent.StartCoroutine(SingleClickSelectRoutine(index, interval));
    }

    private IEnumerator SingleClickSelectRoutine(int index, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        _singleClickCoroutine = null;
        _pendingClickDeckIndex = -1;

        if (!_isChoosingCard || _isAnimatingCard) yield break;
        if (_suppressPointerClick || _dragMoved) yield break;
        SelectDeckCard(index);
    }

    private void StopPendingSingleClick()
    {
        if (_singleClickCoroutine != null && uiComponent != null)
        {
            uiComponent.StopCoroutine(_singleClickCoroutine);
            _singleClickCoroutine = null;
        }
        else
        {
            _singleClickCoroutine = null;
        }

        _pendingClickDeckIndex = -1;
    }

    private void ReleaseDeckPointer()
    {
        StopLongPressSelection();
        StopPendingSingleClick();

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
        RectTransform viewportRoot = GetDeckViewportRoot();
        if (viewportRoot == null || eventData is not PointerEventData pointerData)
            return false;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewportRoot,
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

        RectTransform flyParent = ResolveFlyingCardParent(slotPoseRect);
        Image flyImage = UnityEngine.Object.Instantiate(sourceImage, flyParent);
        flyImage.name = $"SpreadFlyingCard_{slotIndex + 1}";
        flyImage.raycastTarget = false;
        flyImage.sprite = ResolveCardBackSprite();
        flyImage.color = Color.white;

        RectTransform sourceRect = sourceImage.rectTransform;
        RectTransform flyRect = flyImage.rectTransform;
        CopyRectWorldPose(sourceRect, flyRect, flyParent);
        flyRect.SetAsLastSibling();
        SetDeckCardSortingOrder(flyImage, _deckImages.Count + 40);

        Color sourceColor = sourceImage.color;
        sourceColor.a = 0f;
        sourceImage.color = sourceColor;

        ResetSlotToBack(slot, GetPositionLabel(slotIndex), ResolveCardBackSprite());
        ResolveSlotTargetPose(slotPoseRect, flyParent, sourceRect, out Vector2 targetPosition, out float targetRotationZ, out float targetScale);
        ResolveCenterRevealPose(flyParent, sourceRect, targetScale, out Vector2 centerPosition, out float centerScale);

        float centerDuration = Mathf.Max(0.12f, uiComponent.centerRevealDuration);
        float selectDuration = Mathf.Max(0.12f, uiComponent.selectDuration);
        float flipHalfDuration = Mathf.Max(0.08f, uiComponent.flipDuration * 0.5f);
        Sprite frontSprite = LoadCardSprite(draw.card.cardId);
        float finalRotationZ = targetRotationZ + (draw.upright ? 0f : 180f);
        float centerRevealRotationZ = draw.upright ? 0f : 180f;
        float flipRevealScale = centerScale * Mathf.Max(1.16f, uiComponent.flipRevealScaleMultiplier);
        float revealHoldDuration = Mathf.Max(0.85f, uiComponent.cardRevealGap);

        _drawSequence?.Kill(false);
        _drawSequence = DOTween.Sequence();
        _drawSequence.Append(flyRect.DOAnchorPos(centerPosition, centerDuration).SetEase(Ease.InOutCubic));
        _drawSequence.Join(flyRect.DORotate(Vector3.zero, centerDuration).SetEase(Ease.InOutCubic));
        _drawSequence.Join(flyRect.DOScale(Vector3.one * centerScale, centerDuration).SetEase(Ease.OutBack, 0.82f));
        _drawSequence.AppendCallback(() =>
        {
            SetRevealMaskVisible(true, false);
            BringFlyingCardAboveMask(flyImage);
        });
        if (uiComponent.centerRevealMaskFadeDuration > 0f)
            _drawSequence.AppendInterval(uiComponent.centerRevealMaskFadeDuration);
        AppendCenterRevealShake(_drawSequence, flyRect, centerPosition, centerScale, 0f);
        _drawSequence.Append(flyRect.DORotate(new Vector3(0f, 88f, 0f), flipHalfDuration).SetEase(Ease.InCubic));
        _drawSequence.Join(flyRect.DOScale(Vector3.one * flipRevealScale, flipHalfDuration).SetEase(Ease.InOutSine));
        _drawSequence.AppendCallback(() =>
        {
            flyImage.sprite = frontSprite ?? ResolveCardBackSprite();
            flyImage.color = Color.white;
            flyRect.localRotation = Quaternion.Euler(0f, 88f, centerRevealRotationZ);
        });
        _drawSequence.Append(flyRect.DORotate(new Vector3(0f, 0f, centerRevealRotationZ), flipHalfDuration).SetEase(Ease.OutCubic));
        _drawSequence.Join(flyRect.DOScale(Vector3.one * flipRevealScale, flipHalfDuration).SetEase(Ease.OutCubic));
        if (revealHoldDuration > 0f)
            _drawSequence.AppendInterval(revealHoldDuration);
        _drawSequence.AppendCallback(() =>
        {
            slot.SetCardSlotVisible(false);
            SetRevealMaskVisible(false, false);
            BringFlyingCardAboveMask(flyImage);
        });
        _drawSequence.Append(flyRect.DOAnchorPos(targetPosition, selectDuration).SetEase(Ease.InOutCubic));
        _drawSequence.Join(flyRect.DORotate(new Vector3(0f, 0f, finalRotationZ), selectDuration).SetEase(Ease.InOutCubic));
        _drawSequence.Join(flyRect.DOScale(Vector3.one * targetScale, selectDuration).SetEase(Ease.InCubic));

        _drawSequence.OnComplete(() =>
        {
            SetRevealMaskVisible(false, true);

            if (flyImage != null)
                DestroyDeckObject(flyImage.gameObject);

            Color restoredColor = sourceImage.color;
            restoredColor.a = 1f;
            sourceImage.color = restoredColor;
            sourceImage.sprite = ResolveCardBackSprite();

            FinishSlotReveal(slotIndex, slot, draw);
            _drawSequence = null;
        });
    }

    private RectTransform ResolveFlyingCardParent(RectTransform slotRect)
    {
        if (transform is RectTransform rootRect)
            return rootRect;

        return _cardContainer;
    }

    private void BringFlyingCardAboveMask(Image flyImage)
    {
        if (flyImage == null) return;

        flyImage.transform.SetAsLastSibling();
        SetDeckCardSortingOrder(flyImage, _deckImages.Count + 80);
    }

    private void ResolveCenterRevealPose(RectTransform motionParent,
        RectTransform sourceRect,
        float slotTargetScale,
        out Vector2 centerPosition,
        out float centerScale)
    {
        centerPosition = Vector2.zero;
        centerScale = Mathf.Max(_deckCardScale, slotTargetScale);

        if (motionParent == null || sourceRect == null)
            return;

        centerPosition = motionParent.rect.center;

        float sourceWidth = Mathf.Max(1f, sourceRect.rect.width);
        float sourceHeight = Mathf.Max(1f, sourceRect.rect.height);
        float fittedScale = Mathf.Min(
            (motionParent.rect.width * 0.56f) / sourceWidth,
            (motionParent.rect.height * 0.62f) / sourceHeight);

        float maxScale = Mathf.Max(0.1f, uiComponent.centerRevealMaxScale);
        float preferredMin = Mathf.Min(fittedScale, Mathf.Max(_deckCardScale, slotTargetScale) * 1.45f);
        centerScale = Mathf.Max(Mathf.Min(fittedScale, maxScale), preferredMin);
    }

    private void AppendCenterRevealShake(Sequence sequence,
        RectTransform flyRect,
        Vector2 centerPosition,
        float centerScale,
        float centerRotationZ)
    {
        if (sequence == null || flyRect == null || uiComponent == null)
            return;

        float duration = Mathf.Max(0f, uiComponent.centerRevealShakeDuration);
        float positionStrength = Mathf.Max(0f, uiComponent.centerRevealShakePosition);
        float rotationStrength = Mathf.Max(0f, uiComponent.centerRevealShakeRotation);
        if (duration <= 0f || (positionStrength <= 0f && rotationStrength <= 0f))
            return;

        float[] shakeOffsets = { 1f, -0.86f, 0.72f, -0.56f, 0.4f, -0.26f, 0.14f, 0f };
        float stepDuration = Mathf.Max(0.018f, duration / shakeOffsets.Length);
        Vector3 centerScaleVector = Vector3.one * centerScale;

        for (int i = 0; i < shakeOffsets.Length; i++)
        {
            float offset = shakeOffsets[i];
            sequence.Append(flyRect.DOAnchorPos(centerPosition + new Vector2(positionStrength * offset, 0f), stepDuration).SetEase(Ease.InOutSine));
            sequence.Join(flyRect.DORotate(new Vector3(0f, 0f, centerRotationZ + rotationStrength * offset), stepDuration).SetEase(Ease.InOutSine));
            sequence.Join(flyRect.DOScale(centerScaleVector, stepDuration).SetEase(Ease.InOutSine));
        }
    }

    private static void CopyRectWorldPose(RectTransform sourceRect, RectTransform targetRect, RectTransform targetParent)
    {
        if (sourceRect == null || targetRect == null || targetParent == null) return;

        targetRect.anchorMin = new Vector2(0.5f, 0.5f);
        targetRect.anchorMax = new Vector2(0.5f, 0.5f);
        targetRect.pivot = new Vector2(0.5f, 0.5f);
        targetRect.sizeDelta = sourceRect.rect.size;

        Vector3 worldCenter = sourceRect.TransformPoint(sourceRect.rect.center);
        Vector3 localCenter = targetParent.InverseTransformPoint(worldCenter);
        targetRect.anchoredPosition = new Vector2(localCenter.x, localCenter.y);
        targetRect.localRotation = Quaternion.Inverse(targetParent.rotation) * sourceRect.rotation;

        Vector3 sourceScale = sourceRect.lossyScale;
        Vector3 parentScale = targetParent.lossyScale;
        targetRect.localScale = new Vector3(
            SafeDivide(sourceScale.x, parentScale.x),
            SafeDivide(sourceScale.y, parentScale.y),
            1f);
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) <= 0.0001f ? value : value / divisor;
    }

    private void ResolveSlotTargetPose(RectTransform slotRect,
        RectTransform motionParent,
        RectTransform sourceRect,
        out Vector2 targetPosition,
        out float targetRotationZ,
        out float targetScale)
    {
        targetPosition = Vector2.zero;
        targetRotationZ = 0f;
        targetScale = _deckCardScale;

        if (slotRect == null || motionParent == null || sourceRect == null)
            return;

        Vector3 worldCenter = slotRect.TransformPoint(slotRect.rect.center);
        Vector3 localCenter = motionParent.InverseTransformPoint(worldCenter);
        targetPosition = new Vector2(localCenter.x, localCenter.y);

        Quaternion localRotation = Quaternion.Inverse(motionParent.rotation) * slotRect.rotation;
        targetRotationZ = localRotation.eulerAngles.z;

        Vector3[] worldCorners = new Vector3[4];
        slotRect.GetWorldCorners(worldCorners);
        Vector3 localBottomLeft = motionParent.InverseTransformPoint(worldCorners[0]);
        Vector3 localTopLeft = motionParent.InverseTransformPoint(worldCorners[1]);
        Vector3 localBottomRight = motionParent.InverseTransformPoint(worldCorners[3]);

        float targetWidth = Vector3.Distance(localBottomLeft, localBottomRight);
        float targetHeight = Vector3.Distance(localBottomLeft, localTopLeft);
        float sourceWidth = Mathf.Max(1f, sourceRect.rect.width);
        float sourceHeight = Mathf.Max(1f, sourceRect.rect.height);
        targetScale = Mathf.Max(0.01f, Mathf.Min(targetWidth / sourceWidth, targetHeight / sourceHeight));
    }

    private void FinishSlotReveal(int slotIndex, CardSlotItem slot, (TarotCard card, bool upright) draw)
    {
        SetSlotFace(slot, draw);
        SetRevealedCardInfo(slotIndex, draw);

        _nextDrawIndex++;
        _isAnimatingCard = false;
        _dragStartedFromCard = false;
        _activeDragCardIndex = -1;
        _isPullingCard = false;
        _activePullY = 0f;

        ApplyDeckLayout(true);

        if (_nextDrawIndex >= _cardCount)
        {
            _isChoosingCard = false;
            SetDeckVisible(false);
            if (_cardCount == 3)
            {
                if (_isCardInfoLoading)
                    StartGroupReadingLoading();
                else
                    RefreshDetailEntryState();
            }
            else
            {
                SetOperationStepText($"{_cardCount}张牌已经就位");
            }
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

        _lastDeckViewportSize = GetDeckViewportSize();

        for (int i = 0; i < _deckImages.Count; i++)
        {
            Image image = _deckImages[i];
            if (image == null) continue;

            RectTransform rect = image.rectTransform;
            Vector2 targetPosition = GetDeckFanPosition(i);
            float targetRotation = GetDeckFanRotation(i);
            Vector3 targetScale = Vector3.one * (_deckCardScale * GetSettledDeckScaleMultiplier());
            bool visibleInViewport = IsDeckCardInsideViewport(targetPosition, targetScale.x);
            DOTween.Kill(rect);
            SetDeckCardViewportVisible(image, visibleInViewport);

            if (animated)
            {
                rect.DOAnchorPos(targetPosition, 0.18f)
                    .SetEase(Ease.OutCubic)
                    .OnUpdate(RefreshDeckSiblingOrder)
                    .OnComplete(RefreshDeckSiblingOrder);
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

        RefreshDeckSiblingOrder();
    }

    private void RefreshDeckLayoutIfViewportChanged()
    {
        Vector2 viewportSize = GetDeckViewportSize();
        if (viewportSize.x <= 1f || viewportSize.y <= 1f)
            return;

        if (Mathf.Abs(viewportSize.x - _lastDeckViewportSize.x) <= 0.5f
            && Mathf.Abs(viewportSize.y - _lastDeckViewportSize.y) <= 0.5f)
            return;

        ApplyDeckLayout(false);
    }

    private Vector2 GetDeckViewportSize()
    {
        RectTransform viewportRoot = GetDeckViewportRoot();
        return viewportRoot != null ? viewportRoot.rect.size : Vector2.zero;
    }

    private void RefreshDeckSiblingOrder()
    {
        if (_deckImages.Count <= 1) return;

        _deckSiblingOrder.Clear();
        for (int i = 0; i < _deckImages.Count; i++)
        {
            if (_deckImages[i] != null)
                _deckSiblingOrder.Add(i);
        }

        _deckSiblingOrder.Sort(CompareDeckSiblingOrder);
        for (int i = 0; i < _deckSiblingOrder.Count; i++)
        {
            Image image = _deckImages[_deckSiblingOrder[i]];
            if (image != null)
            {
                image.rectTransform.SetSiblingIndex(i);
                SetDeckCardSortingOrder(image, i);
            }
        }

        if (_isPullingCard && _activeDragCardIndex >= 0 && _activeDragCardIndex < _deckImages.Count)
        {
            Image activeImage = _deckImages[_activeDragCardIndex];
            if (activeImage != null)
            {
                activeImage.rectTransform.SetAsLastSibling();
                SetDeckCardSortingOrder(activeImage, _deckImages.Count + 20);
            }
        }
    }

    private bool IsDeckCardInsideViewport(Vector2 anchoredPosition, float scale)
    {
        return true;
    }

    private void SetDeckCardViewportVisible(Image image, bool visible)
    {
        if (image == null) return;

        image.enabled = visible;
        image.raycastTarget = visible && _isChoosingCard && !_isAnimatingCard;
    }

    private void EnsureDeckCardCanvas(GameObject cardObject)
    {
        if (cardObject == null) return;

        Canvas sortingCanvas = cardObject.GetComponent<Canvas>();
        if (sortingCanvas != null)
        {
            sortingCanvas.overrideSorting = false;
            sortingCanvas.sortingOrder = 0;
            sortingCanvas.enabled = false;
        }

        GraphicRaycaster raycaster = cardObject.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;

        MaskableGraphic[] graphics = cardObject.GetComponentsInChildren<MaskableGraphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].maskable = true;
        }
    }

    private void SetDeckCardSortingOrder(Image image, int visualOrder)
    {
        if (image == null) return;

        Canvas sortingCanvas = image.GetComponent<Canvas>();
        if (sortingCanvas != null)
        {
            sortingCanvas.overrideSorting = false;
            sortingCanvas.sortingOrder = 0;
            sortingCanvas.enabled = false;
        }
    }

    private int CompareDeckSiblingOrder(int leftIndex, int rightIndex)
    {
        Vector2 leftPosition = GetDeckSiblingPosition(leftIndex);
        Vector2 rightPosition = GetDeckSiblingPosition(rightIndex);

        int xCompare = Mathf.RoundToInt(leftPosition.x * 100f)
            .CompareTo(Mathf.RoundToInt(rightPosition.x * 100f));
        if (xCompare != 0)
            return xCompare;

        int yCompare = Mathf.RoundToInt(leftPosition.y * 100f)
            .CompareTo(Mathf.RoundToInt(rightPosition.y * 100f));
        if (yCompare != 0)
            return yCompare;

        return leftIndex.CompareTo(rightIndex);
    }

    private Vector2 GetDeckSiblingPosition(int index)
    {
        if (index >= 0 && index < _deckImages.Count)
        {
            Image image = _deckImages[index];
            if (image != null)
                return image.rectTransform.anchoredPosition;
        }

        return GetDeckFanPosition(index);
    }

    private Vector2 GetDeckFanPosition(int index)
    {
        ResolveDeckLayoutMetrics();
        float spacing = _deckImages.Count > 1 ? _fanWidth / (_deckImages.Count - 1) : _fanWidth;
        float cycleWidth = Mathf.Max(1f, _fanWidth + spacing);
        float x = -_fanWidth * 0.5f + spacing * index + _deckOffsetX;
        x = Mathf.Repeat(x + cycleWidth * 0.5f, cycleWidth) - cycleWidth * 0.5f;

        float normalized = Mathf.Clamp(x / Mathf.Max(1f, _fanWidth * 0.5f), -1f, 1f);
        float y = uiComponent.fanHeightOffset
            + GetSettledDeckYOffset()
            + uiComponent.fanRiseOffset * (1f - normalized * normalized);
        return new Vector2(x, y);
    }

    private float GetDeckSpacing()
    {
        return ResolveDeckCardSourceWidth() * _deckCardScale + DeckCardSpacing;
    }

    private float NormalizeDeckOffset(float offset)
    {
        ResolveDeckLayoutMetrics();
        float spacing = _deckImages.Count > 1 ? _fanWidth / (_deckImages.Count - 1) : _fanWidth;
        float cycleWidth = Mathf.Max(1f, _fanWidth + spacing);
        return Mathf.Repeat(offset + cycleWidth * 0.5f, cycleWidth) - cycleWidth * 0.5f;
    }

    private float GetSettledDeckYOffset()
    {
        if (_nextDrawIndex <= 0) return 0f;
        if (uiComponent.drawnFanLowerOffset > 0f)
            return -uiComponent.drawnFanLowerOffset;

        return 0f;
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
        RectTransform viewportRoot = GetDeckViewportRoot();
        if (viewportRoot == null) return;

        Rect rect = viewportRoot.rect;
        _viewportWidth = Mathf.Max(1f, rect.width);
        _viewportHeight = Mathf.Max(1f, rect.height);
        _fanWidth = uiComponent.fanWidth > 0f
            ? uiComponent.fanWidth
            : Mathf.Max(uiComponent.minFanWidth, _viewportWidth * Mathf.Max(1f, uiComponent.fanViewportWidthMultiplier));
        if (_cardTemplateImage != null)
        {
            _deckCardScale = ResolveDeckCardScale();
            _fanWidth = ResolveDeckFanWidth(Mathf.Max(1, _deckImages.Count));
            ResolveDeckScrollBounds();
        }
    }

    private void ResolveDeckScrollBounds()
    {
        _minDeckOffsetX = float.NegativeInfinity;
        _maxDeckOffsetX = float.PositiveInfinity;
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
        float targetOffset = NormalizeDeckOffset(_deckOffsetX + distance);

        _deckInertiaTween?.Kill(false);
        _deckInertiaTween = DOTween.To(
                () => _deckOffsetX,
                value =>
                {
                    _deckOffsetX = NormalizeDeckOffset(value);
                    ApplyDeckLayout(false);
                },
                targetOffset,
                duration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                _deckOffsetX = NormalizeDeckOffset(targetOffset);
                ApplyDeckLayout(false);
                _deckInertiaTween = null;
            });
    }

    private void StopDeckInertia()
    {
        _deckInertiaTween?.Kill(false);
        _deckInertiaTween = null;
    }

    private void StopDeckShuffleSequence()
    {
        _deckShuffleSequence?.Kill(false);
        _deckShuffleSequence = null;
    }

    private void SetDeckCardsInteractable(bool interactable)
    {
        for (int i = 0; i < _deckImages.Count; i++)
        {
            if (_deckImages[i] != null)
                _deckImages[i].raycastTarget = interactable && _deckImages[i].enabled;
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
        StopPendingSingleClick();
        StopGroupReadingLoading();
        SetRevealMaskVisible(false, true);
        StopDeckShuffleSequence();
        _drawSequence?.Kill(false);
        _drawSequence = null;
        ClearRuntimeFlyingCards();

        for (int i = 0; i < _deckImages.Count; i++)
        {
            if (_deckImages[i] != null)
            {
                DestroyDeckObject(_deckImages[i].gameObject);
            }
        }
        _deckImages.Clear();
        ClearGeneratedDeckChildren();
        ClearRuntimeFlyingCards();

        if (_cardContainer != null)
            _cardContainer.gameObject.SetActive(true);

        if (_cardTemplateImage != null)
            _cardTemplateImage.gameObject.SetActive(false);
    }

    private void ClearGeneratedDeckChildren()
    {
        ClearGeneratedDeckChildrenUnder(_deckClipRoot);
        ClearGeneratedDeckChildrenUnder(_cardContainer);
    }

    private void SanitizeDeckVisuals()
    {
        bool deckChanged = TrimTrackedDeckImages();
        ClearGeneratedDeckChildren();
        if (deckChanged && !_isPullingCard && _deckImages.Count > 0)
            ApplyDeckLayout(false);
    }

    private bool TrimTrackedDeckImages()
    {
        bool changed = false;
        Transform deckParent = GetDeckViewportRoot();
        int maxRuntimeCards = GetVisualDeckCardCount();
        for (int i = _deckImages.Count - 1; i >= 0; i--)
        {
            Image image = _deckImages[i];
            if (image == null)
            {
                _deckImages.RemoveAt(i);
                changed = true;
                continue;
            }

            if (i >= maxRuntimeCards)
            {
                DestroyDeckObject(image.gameObject);
                _deckImages.RemoveAt(i);
                changed = true;
                continue;
            }

            EnsureDeckCardCanvas(image.gameObject);
            if (!_isPullingCard && deckParent != null && image.transform.parent != deckParent)
            {
                image.transform.SetParent(deckParent, false);
                changed = true;
            }
        }

        return changed;
    }

    private void ClearRuntimeFlyingCards()
    {
        ClearRuntimeFlyingCardsUnder(transform);
    }

    private void ClearRuntimeFlyingCardsUnder(Transform root)
    {
        if (root == null) return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child == null) continue;

            if (IsRuntimeFlyingCardName(child.name))
            {
                DestroyDeckObject(child.gameObject);
                continue;
            }

            ClearRuntimeFlyingCardsUnder(child);
        }
    }

    private void ClearGeneratedDeckChildrenUnder(Transform root)
    {
        if (root == null) return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child == null) continue;
            if (_deckClipRoot != null && child == _deckClipRoot) continue;
            if (_cardTemplateImage != null && child == _cardTemplateImage.transform) continue;

            if (ShouldClearDeckChild(child))
            {
                DestroyDeckObject(child.gameObject);
                continue;
            }

            ClearGeneratedDeckChildrenUnder(child);
        }
    }

    private bool ShouldClearDeckChild(Transform child)
    {
        if (child == null) return false;
        if (_deckClipRoot != null && child == _deckClipRoot) return false;
        if (_cardTemplateImage != null && child == _cardTemplateImage.transform) return false;
        if (IsTrackedDeckChild(child)) return false;

        if (IsGeneratedDeckCardName(child.name))
            return true;

        if (child.name.StartsWith("DeckClipRoot", StringComparison.Ordinal))
            return true;

        if (child.GetComponent<Image>() == null)
            return false;

        return child.name.StartsWith("Card", StringComparison.Ordinal)
            || child.GetComponent<Button>() != null
            || child.GetComponent<EventTrigger>() != null;
    }

    private bool IsTrackedDeckChild(Transform child)
    {
        for (int i = 0; i < _deckImages.Count; i++)
        {
            Image image = _deckImages[i];
            if (image != null && image.transform == child)
                return true;
        }

        return false;
    }

    private static bool IsGeneratedDeckCardName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && (objectName.StartsWith("SpreadDeckCard_", StringComparison.Ordinal)
                || objectName.StartsWith("DrawCard_", StringComparison.Ordinal)
                || objectName.StartsWith("Card(Clone)", StringComparison.Ordinal));
    }

    private static bool IsRuntimeFlyingCardName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.StartsWith("SpreadFlyingCard_", StringComparison.Ordinal);
    }

    private static void DestroyDeckObject(GameObject target)
    {
        if (target == null) return;

        target.SetActive(false);
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
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
        SetDetailEntryVisible(false);
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
        SpreadShuffleBridge.MarkPendingDialogReveal(message);
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

    private bool CanEnterDetail()
    {
        return !_shuffleDone
            && _cardCount == 3
            && AreAllCardsRevealed()
            && !_isCardInfoLoading;
    }

    private void RefreshDetailEntryState()
    {
        bool canEnterDetail = CanEnterDetail();
        if (canEnterDetail)
        {
            StopGroupReadingLoading();
            SetDetailEntryVisible(true);
            SetOperationStepText(DetailEntryPrompt);
            return;
        }

        SetDetailEntryVisible(false);
        if (_cardCount == 3 && AreAllCardsRevealed() && _isCardInfoLoading)
            StartGroupReadingLoading();
    }

    private void SetDetailEntryVisible(bool visible)
    {
        if (uiComponent == null || uiComponent.hideBtn == null)
            return;

        uiComponent.hideBtn.gameObject.SetActive(visible);
        uiComponent.hideBtn.interactable = visible;
    }

    private void StartGroupReadingLoading()
    {
        if (uiComponent == null) return;

        SetDetailEntryVisible(false);
        if (_groupReadingLoadingCoroutine != null)
            return;

        int version = ++_groupReadingLoadingVersion;
        _groupReadingLoadingCoroutine = uiComponent.StartCoroutine(GroupReadingLoadingRoutine(version));
    }

    private IEnumerator GroupReadingLoadingRoutine(int version)
    {
        string[] frames =
        {
            "正在解读牌组.",
            "正在解读牌组..",
            "正在解读牌组..."
        };
        int frameIndex = 0;

        while (version == _groupReadingLoadingVersion
            && _cardCount == 3
            && AreAllCardsRevealed()
            && _isCardInfoLoading
            && !_shuffleDone)
        {
            SetOperationStepText(frames[frameIndex], false);
            frameIndex = (frameIndex + 1) % frames.Length;
            yield return new WaitForSecondsRealtime(0.35f);
        }

        if (version == _groupReadingLoadingVersion)
            _groupReadingLoadingCoroutine = null;
    }

    private void StopGroupReadingLoading()
    {
        _groupReadingLoadingVersion++;

        if (_groupReadingLoadingCoroutine != null && uiComponent != null)
            uiComponent.StopCoroutine(_groupReadingLoadingCoroutine);

        _groupReadingLoadingCoroutine = null;
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
        SetDetailEntryVisible(false);

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

        if (_dashScopeAPI == null)
            _dashScopeAPI = DashScopeAPI.ResolveFor(gameObject);
        if (_dashScopeAPI == null)
        {
            SetCardDescriptionIfCurrent(index, requestVersion, fallback);
            return;
        }

        var messages = BuildCardDescriptionMessages(card, upright, index);
        _dashScopeAPI.SendChatRequest(messages, response =>
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
        if (descriptionText != null)
        {
            descriptionText.gameObject.SetActive(true);
            descriptionText.text = text;
        }

        RefreshDetailEntryState();
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

    private List<DashScopeAPI.Message> BuildCardDescriptionMessages(TarotCard card, bool upright, int index)
    {
        string position = GetPositionLabel(index);
        string spreadName = FirstNonEmpty(_spread?.label, GetDefaultTitle(_cardCount));
        string keywords = card.keywords != null && card.keywords.Count > 0
            ? string.Join("、", card.keywords.GetRange(0, Mathf.Min(3, card.keywords.Count)))
            : card.nameZh;

        var payload = new ChatPayload
        {
            scene = "card_position_description",
            locale = "zh-CN",
            message = $"牌阵：{spreadName}\n"
                + $"位置：{position}\n"
                + $"卡牌：{FormatCardName(card, upright)}\n"
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
            return assembly.messages.Select(message => new DashScopeAPI.Message(message.role, message.content)).ToList();

        return new List<DashScopeAPI.Message>
        {
            new DashScopeAPI.Message("system", ScenePrompts.Get("card_position_description")),
            new DashScopeAPI.Message("user", payload.message)
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
        StopPendingSingleClick();
        StopGroupReadingLoading();
        SetRevealMaskVisible(false, true);
        StopDeckShuffleSequence();

        _drawSequence?.Kill(false);
        _drawSequence = null;

        for (int i = 0; i < _deckImages.Count; i++)
        {
            Image image = _deckImages[i];
            if (image == null) continue;
            DOTween.Kill(image);
            DOTween.Kill(image.rectTransform);
            DestroyDeckObject(image.gameObject);
        }
        _deckImages.Clear();
        ClearGeneratedDeckChildren();

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
        return uiComponent != null ? uiComponent.cardBackSprite : null;
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
        return slot.GetPoseRect();
    }

    /// <summary>
    /// 加载卡牌正面图片
    /// </summary>
    private Sprite LoadCardSprite(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return ResolveCardBackSprite();

        Sprite sprite = TarotSpriteLoader.Load(cardId);
        if (sprite == null)
            Debug.LogWarning($"[TarorSingleSpreadShuffleUI] 未找到塔罗牌正面资源: {cardId}");
        return sprite != null ? sprite : ResolveCardBackSprite();
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
