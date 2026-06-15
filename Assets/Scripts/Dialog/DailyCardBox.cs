using System;
using System.Collections;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using UnityEngine;
using UnityEngine.UI;

public class DailyCardBox : MonoBehaviour
{
    public Text cardTitleText;
    public Image cardImage;
    public Text cardNameText;
    public Text cardDesText;

    public Button detailBtn;

    private void OnEnable() 
    {
        detailBtn.onClick.AddListener(OpenDailyCard);
    }
    
    private void OnDisable()
    {
        detailBtn.onClick.RemoveListener(OpenDailyCard);     
    }

    private void OpenDailyCard()
    {
          UIModule.Instance.PopUpWindow<TodayCardUI>();
    }
}
