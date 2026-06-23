using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestionItem : MonoBehaviour
{
    public Button btn;
    public TMP_Text desText;
    public TMP_Text iconText;  // "✦" 符号（Optional，没有就忽略）

    private Action mOnClickCallback;

    public void Init()
    {
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnQuestionClick);
        }
    }

    /// <summary>
    /// 设置问题文本和点击回调
    /// </summary>
    public void SetItemData(string question, Action onClick = null)
    {
        if (desText != null)
        {
            desText.text = question ?? "";
        }

        mOnClickCallback = onClick;
    }

    /// <summary>
    /// 兼容旧接口（无回调）
    /// </summary>
    public void SetItemData(string question)
    {
        SetItemData(question, null);
    }

    private void OnQuestionClick()
    {
        Debug.Log($"[QuestionItem] 点击问题: {desText?.text}");
        mOnClickCallback?.Invoke();
    }
}
