using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// 今日占卡界面，TodayCardUI里的QuestRow用的
/// </summary>
public class QuestRowItem : MonoBehaviour
{
    public Text desText;
    public Button btn;

    private string _question;
    private Action<string> _onClick;

    public string Question => _question;

    public void SetData(string question, Action<string> onClick = null)
    {
        _question = question ?? "";
        _onClick = onClick;

        if (desText != null)
            desText.text = _question;

        if (btn != null)
        {
            btn.onClick.RemoveListener(OnButtonClick);
            btn.onClick.AddListener(OnButtonClick);
        }
    }

    private void OnDisable()
    {
        if (btn != null)
            btn.onClick.RemoveListener(OnButtonClick);
    }

    private void OnButtonClick()
    {
        _onClick?.Invoke(_question);
    }
}
