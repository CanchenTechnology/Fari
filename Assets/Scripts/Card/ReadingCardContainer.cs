using System.Collections;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 塔罗牌阅读展示容器
/// 挂在 TodayOracleUI 的 ReadingCardContainerTransform 下
/// </summary>
public class ReadingCardContainer : MonoBehaviour
{
    [Header("标题")]
    public TMP_Text titleText;

    [Header("描述")]
    public TMP_Text descriptText;


    [Header("塔罗牌")]
    public TMP_Text cardNameText;
    public Image cardImage;

    public Button deepChatBtn;

    public Button viewFullBtn;

    private TarotCard _card;
    private bool _upright = true;
    private bool _hasCard;

    private void Awake()
    {
        RefreshButtonBindings();
    }

    private void OnEnable()
    {
        RefreshButtonBindings();
        if (!_hasCard && ResolveCurrentCard(out TarotCard card, out bool upright))
            SetCard(card, upright);
        else
            SetActionButtonsInteractable(_hasCard);
    }

    public void SetCard(TarotCard card, bool upright)
    {
        _card = card;
        _upright = upright;
        _hasCard = card != null;
        SetActionButtonsInteractable(_hasCard);
    }

    public void RefreshButtonBindings()
    {
        RemoveButtonListeners();

        if (deepChatBtn != null)
            deepChatBtn.onClick.AddListener(OnDeepChatButtonClick);
        if (viewFullBtn != null)
            viewFullBtn.onClick.AddListener(OnViewFullButtonClick);
    }

    private void RemoveButtonListeners()
    {
        if (deepChatBtn != null)
            deepChatBtn.onClick.RemoveListener(OnDeepChatButtonClick);
        if (viewFullBtn != null)
            viewFullBtn.onClick.RemoveListener(OnViewFullButtonClick);
    }

    private void SetActionButtonsInteractable(bool interactable)
    {
        if (deepChatBtn != null)
            deepChatBtn.interactable = interactable;
        if (viewFullBtn != null)
            viewFullBtn.interactable = interactable;
    }

    private void OnDeepChatButtonClick()
    {
        RefreshButtonBindings();
        SetActionButtonsInteractable(true);

        TodayOracleUI todayOracleUI = UIModule.Instance.GetWindow<TodayOracleUI>();
        if (todayOracleUI != null)
            todayOracleUI.OnDeepChatButtonClick();
    }

    private void OnViewFullButtonClick()
    {
        RefreshButtonBindings();
        SetActionButtonsInteractable(true);

        TarotCard card = _card;
        bool upright = _upright;
        if (card == null)
        {
            ResolveCurrentCard(out card, out upright);
            SetCard(card, upright);
        }

        if (card == null)
        {
            Debug.LogWarning("[ReadingCardContainer] 无当前牌数据，无法打开完整解读。");
            return;
        }

        var completeUI = UIModule.Instance.PopUpWindow<CompleteInterpretationUI>();
        if (completeUI != null)
        {
            completeUI.SetCard(card, upright);
        }
    }

    private static bool ResolveCurrentCard(out TarotCard card, out bool upright)
    {
        card = null;
        upright = true;

        DailyOracleService oracleService = DailyOracleService.Instance;
        if (oracleService?.CurrentCard != null)
        {
            card = oracleService.CurrentCard;
            upright = oracleService.CurrentUpright;
            return true;
        }

        if (oracleService?.CachedPreparedReading?.card != null)
        {
            card = oracleService.CachedPreparedReading.card;
            upright = oracleService.CachedPreparedReading.upright;
            return true;
        }

        if (DivinationEngine.Instance?.TodayCard.HasValue == true)
        {
            (card, upright) = DivinationEngine.Instance.TodayCard.Value;
            return card != null;
        }

        return false;
    }

}
