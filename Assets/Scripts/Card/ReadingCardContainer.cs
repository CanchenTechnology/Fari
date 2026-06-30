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
        ResolveReferences();
        RefreshButtonBindings();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshButtonBindings();
        if (!_hasCard && ResolveCurrentCard(out TarotCard card, out bool upright))
            SetCard(card, upright);
        else
        {
            RenderCardImage();
            SetActionButtonsInteractable(_hasCard);
        }
    }

    public void SetCard(TarotCard card, bool upright)
    {
        ResolveReferences();
        _card = card;
        _upright = upright;
        _hasCard = card != null;
        RenderCardImage();
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

    private void RenderCardImage()
    {
        if (!_hasCard || _card == null || cardImage == null)
            return;

        Sprite sprite = TarotSpriteLoader.Load(_card.cardId);
        if (sprite == null)
        {
            Debug.LogWarning($"[ReadingCardContainer] 找不到今日牌图片: cardId={_card.cardId}, name={_card.nameZh}");
            return;
        }

        cardImage.gameObject.SetActive(true);
        cardImage.enabled = true;
        cardImage.sprite = sprite;
        cardImage.color = Color.white;
        cardImage.preserveAspect = true;
        cardImage.rectTransform.localRotation = _upright
            ? Quaternion.identity
            : Quaternion.Euler(0f, 0f, 180f);
    }

    private void ResolveReferences()
    {
        if (cardImage == null)
            cardImage = FindImageByName(transform, "CardImage", "cardImage", "CardArt", "cardArt");
        if (cardNameText == null)
            cardNameText = FindTextByName(transform, "CardName", "cardName", "CardTitle", "cardTitle");
        if (titleText == null)
            titleText = FindTextByName(transform, "Title", "title");
        if (descriptText == null)
            descriptText = FindTextByName(transform, "Description", "description", "Descript", "descript");
        if (deepChatBtn == null)
            deepChatBtn = FindButtonByName(transform, "DeepChatButton", "deepChatBtn", "ChatButton");
        if (viewFullBtn == null)
            viewFullBtn = FindButtonByName(transform, "ViewFullButton", "viewFullBtn", "FullButton");
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

    private static Image FindImageByName(Transform root, params string[] names)
    {
        Transform target = FindChildByName(root, names);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private static TMP_Text FindTextByName(Transform root, params string[] names)
    {
        Transform target = FindChildByName(root, names);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private static Button FindButtonByName(Transform root, params string[] names)
    {
        Transform target = FindChildByName(root, names);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private static Transform FindChildByName(Transform root, params string[] names)
    {
        if (root == null || names == null) return null;
        for (int i = 0; i < names.Length; i++)
        {
            if (root.name == names[i])
                return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByName(root.GetChild(i), names);
            if (result != null) return result;
        }

        return null;
    }

}
