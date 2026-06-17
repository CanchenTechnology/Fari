using System.Collections;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 塔罗牌阅读展示容器
/// 挂在 TodayOracleUI 的 ReadingCardContainerTransform 下
/// </summary>
public class ReadingCardContainer : MonoBehaviour
{
    [Header("标题")]
    public Text titleText;

    [Header("描述")]
    public Text descriptText;


    [Header("塔罗牌")]
    public Text cardNameText;
    public Image cardImage;

    public Button deepChatBtn;

    public Button viewFullBtn;

    private void Awake()
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

    private void OnDeepChatButtonClick()
    {
        UIModule.Instance.GetWindow<TodayOracleUI>().OnDeepChatButtonClick();
    }

    private void OnViewFullButtonClick()
    {
        var completeUI = UIModule.Instance.PopUpWindow<CompleteInterpretationUI>();
        if (completeUI != null && DivinationEngine.Instance?.TodayCard.HasValue == true)
        {
            var (card, upright) = DivinationEngine.Instance.TodayCard.Value;
            completeUI.SetCard(card, upright);
        }
    }

}
