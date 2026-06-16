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
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork;
using XFGameFrameWork;

public class TarorSingleSpreadShuffleUI : WindowBase
{
    public TarorSingleSpreadShuffleUIComponent uiComponent;

    // ========== 内部状态 ==========
    private SpreadDefinition _spread;
    private int _cardCount;
    private List<(TarotCard card, bool upright)> _drawnCards;
    private Coroutine _shuffleCoroutine;
    private bool _shuffleDone;

    [Header("洗牌配置")]
    public float shuffleCycleDuration = 0.08f;   // 每张牌快速切换的间隔
    public int shuffleCycles = 3;                 // 洗牌动画循环次数
    public float flipDuration = 0.4f;             // 单张翻牌持续时间
    public float cardRevealGap = 0.25f;           // 两张牌之间揭示间隔
    public Sprite cardBackSprite;                 // 卡牌背面图（可在 Inspector 覆盖）

    // CardSlotItem 数组（懒加载）
    private CardSlotItem[] _slots;

    #region 生命周期函数

    public override void OnAwake()
    {
        uiComponent = gameObject.GetComponent<TarorSingleSpreadShuffleUIComponent>();
        uiComponent.InitComponent(this);
        this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
        base.OnAwake();
    }

    public override void OnShow()
    {
        base.OnShow();
        _shuffleDone = false;

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

        // 配置界面
        ConfigureUI();

        Debug.Log($"[TarorSingleSpreadShuffleUI] OnShow, cardCount={_cardCount}, spread={_spread?.label}");
    }

    public override void OnHide()
    {
        base.OnHide();
        StopShuffleCoroutine();
    }

    public override void OnDestroy()
    {
        StopShuffleCoroutine();
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
            comp.CardSlotItem4CardSlotItem,
            comp.CardSlotItem5CardSlotItem
        };
    }

    /// <summary>
    /// 根据牌阵信息配置标题、副标题、卡槽显示
    /// </summary>
    private void ConfigureUI()
    {
        var comp = uiComponent;

        // ---- 标题 ----
        if (comp.TitleTextText != null)
        {
            string label = _spread?.label;
            comp.TitleTextText.text = string.IsNullOrEmpty(label)
                ? GetDefaultTitle(_cardCount)
                : label;
        }

        // ---- 副标题 / 指令文字 ----
        if (comp.SubtitleTextText != null)
        {
            string desc = _spread?.description;
            comp.SubtitleTextText.text = string.IsNullOrEmpty(desc)
                ? GetDefaultSubtitle(_cardCount)
                : desc;
        }

        if (comp.InstructionTextText != null)
        {
            comp.InstructionTextText.text = GetInstruction(_cardCount);
        }

        // ---- 牌背图（优先从桥接的牌阵获取，否则使用本地 sprite 或默认） ----
        Sprite backSprite = cardBackSprite;
        if (backSprite == null && _spread != null)
        {
            backSprite = Resources.Load<Sprite>($"TarotCards/CardBack");
        }

        // ---- 卡槽显示/隐藏 ----
        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null) continue;

            if (i < _cardCount)
            {
                slot.gameObject.SetActive(true);

                // 设置卡牌背面
                if (backSprite != null)
                    slot.cardImage.sprite = backSprite;

                slot.cardImage.transform.localScale = Vector3.one;
                slot.cardImage.transform.localRotation = Quaternion.identity;

                // 设置槽位标签
                if (_spread?.positions != null && i < _spread.positions.Count)
                {
                    slot.cardTag.text = _spread.positions[i].label;
                }
                else
                {
                    slot.cardTag.text = GetDefaultSlotLabel(i, _cardCount);
                }
            }
            else
            {
                slot.gameObject.SetActive(false);
            }
        }

        // ---- 洗牌按钮 ----
        if (comp.StartShuffleButton != null)
        {
            comp.StartShuffleButton.gameObject.SetActive(true);
            comp.StartShuffleButton.interactable = true;
        }

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

    private string GetDefaultSlotLabel(int index, int count) => count switch
    {
        1 => "镜像",
        5 => index switch
        {
            0 => "选择A",
            1 => "选择B",
            2 => "选择C",
            3 => "选择D",
            4 => "关键"
        },
        _ => index switch
        {
            0 => "当下",
            1 => "阻碍",
            2 => "走向"
        }
    };

    #endregion

    #region UI组件事件

    /// <summary>
    /// 返回按钮 → 关闭洗牌窗口
    /// </summary>
    public void OnBackButtonClick()
    {
        Debug.Log("[TarorSingleSpreadShuffleUI] 用户取消洗牌");

        // 如果还没洗牌就关闭，清理桥接
        if (!_shuffleDone)
        {
            SpreadShuffleBridge.Clear();
        }

        UIModule.Instance.HideWindow<TarorSingleSpreadShuffleUI>();
    }

    /// <summary>
    /// 开始洗牌按钮 → 启动洗牌动画 + 抽牌 + 揭示
    /// </summary>
    public void OnStartShuffleButtonClick()
    {
        if (_shuffleDone) return;
        if (_shuffleCoroutine != null) return;

        // 禁用按钮
        if (uiComponent.StartShuffleButton != null)
        {
            uiComponent.StartShuffleButton.interactable = false;
        }

        _shuffleCoroutine = uiComponent.StartCoroutine(ShuffleAndReveal());
    }

    #endregion

    #region 洗牌动画 & 揭示

    private IEnumerator ShuffleAndReveal()
    {
        // ---- 阶段 1：洗牌动画 ----
        yield return uiComponent.StartCoroutine(PlayShuffleAnimation());

        // ---- 阶段 2：抽牌 ----
        _drawnCards = TarotDeck.DrawMultiple(_cardCount);

        // 同步到 DivinationEngine
        SyncToDivinationEngine();

        yield return new WaitForSeconds(0.3f);

        // ---- 阶段 3：逐一揭示牌面 ----
        for (int i = 0; i < _cardCount && i < _slots.Length; i++)
        {
            if (_slots[i] != null)
            {
                yield return uiComponent.StartCoroutine(FlipCard(_slots[i].cardImage, _drawnCards[i]));
                // 揭示后更新标签（显示牌名）
                string orient = _drawnCards[i].upright ? "正" : "逆";
                _slots[i].cardTag.text = $"{_drawnCards[i].card.nameZh}（{orient}）";
            }
            if (i < _cardCount - 1)
                yield return new WaitForSeconds(cardRevealGap);
        }

        yield return new WaitForSeconds(0.5f);

        // ---- 阶段 4：完成 ----
        _shuffleDone = true;

        // 通知 InteractionCard
        SpreadShuffleBridge.NotifyComplete(_drawnCards);

        // 关闭洗牌窗口
        UIModule.Instance.HideWindow<TarorSingleSpreadShuffleUI>();

        Debug.Log($"[TarorSingleSpreadShuffleUI] 洗牌完成: {_cardCount} 张牌已揭示");
    }

    /// <summary>
    /// 洗牌动画 —— 快速轮换卡牌背面 + 快速缩放效果
    /// </summary>
    private IEnumerator PlayShuffleAnimation()
    {
        // 分别给每张槽位的卡牌做快速缩放震动
        // 只对可见槽位洗牌
        var visibleSlots = new List<CardSlotItem>();
        for (int i = 0; i < _cardCount && i < _slots.Length; i++)
        {
            if (_slots[i] != null)
                visibleSlots.Add(_slots[i]);
        }

        for (int cycle = 0; cycle < shuffleCycles; cycle++)
        {
            foreach (var slot in visibleSlots)
            {
                // 快速缩放脉冲
                float elapsed = 0f;
                while (elapsed < shuffleCycleDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / shuffleCycleDuration;
                    float scale = 1f + 0.05f * Mathf.Sin(t * Mathf.PI * 4f);
                    slot.cardImage.transform.localScale = new Vector3(scale, scale, 1f);
                    yield return null;
                }
                slot.cardImage.transform.localScale = Vector3.one;
            }
        }

        // 完成时短暂停顿
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>
    /// 翻牌动画 —— 水平缩放模拟翻转（与 InteractionCard 一致）
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
            float scale = 1f - (t * t); // ease-in quad
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

    /// <summary>
    /// 加载卡牌正面图片
    /// </summary>
    private Sprite LoadCardSprite(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return cardBackSprite;
        return TarotSpriteLoader.Load(cardId) ?? cardBackSprite;
    }

    /// <summary>
    /// 同步抽牌结果到 DivinationEngine
    /// </summary>
    private void SyncToDivinationEngine()
    {
        if (DivinationEngine.Instance == null) return;

        var session = DivinationEngine.Instance.CurrentSession;
        if (session == null) return;

        // 如果已经有 lockedCards，跳过
        if (session.lockedCards != null && session.lockedCards.Count > 0) return;

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
        }

        Debug.Log($"[TarorSingleSpreadShuffleUI] 已同步 {lockedList.Count} 张牌到 DivinationEngine, spreadKind={spreadKind}");
    }

    #endregion

    #region API Function

    #endregion
}
