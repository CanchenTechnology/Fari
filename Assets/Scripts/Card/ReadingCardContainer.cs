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

    private void Start()
    {
        deepChatBtn.onClick.AddListener(OnDeepChatButtonClick);
        viewFullBtn.onClick.AddListener(OnViewFullButtonClick);
    }
    private void OnDisable()
    {
        deepChatBtn.onClick.RemoveListener(OnDeepChatButtonClick);
        viewFullBtn.onClick.RemoveListener(OnViewFullButtonClick);
    }

    private void OnDeepChatButtonClick()
    {
        UIModule.Instance.GetWindow<TodayOracleUI>().OnDeepChatButtonClick();
    }

    private void OnViewFullButtonClick()
    {
        UIModule.Instance.PopUpWindow<CompleteInterpretationUI>();
    }

}
