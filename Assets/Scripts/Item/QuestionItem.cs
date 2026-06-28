using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestionItem : MonoBehaviour
{
    public Button btn;
    public TMP_Text desText;
    public float minHeight = 60f;
    public float verticalPadding = 20f;
    public float preferredWidthFallback = 760f;
    public TMP_Text iconText;  // "✦" 符号（Optional，没有就忽略）

    private Action mOnClickCallback;
    private RectTransform mRectTransform;
    private RectTransform mButtonRect;

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
        RefreshLayout();
    }

    /// <summary>
    /// 兼容旧接口（无回调）
    /// </summary>
    public void SetItemData(string question)
    {
        SetItemData(question, null);
    }

    public float RefreshLayout()
    {
        if (mRectTransform == null)
            mRectTransform = transform as RectTransform;
        if (mButtonRect == null && btn != null)
            mButtonRect = btn.transform as RectTransform;

        float targetHeight = EstimatePreferredHeight(
            desText,
            desText != null ? desText.text : string.Empty,
            minHeight,
            verticalPadding,
            preferredWidthFallback);

        if (mRectTransform != null)
            mRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        if (desText != null)
            desText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        if (mButtonRect != null)
            mButtonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        if (mRectTransform != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(mRectTransform);

        return targetHeight;
    }

    public static float EstimatePreferredHeight(string text, RectTransform widthSource, float minHeight)
    {
        float width = widthSource != null ? widthSource.rect.width : 0f;
        width = Mathf.Max(1f, width - 140f);
        int charsPerLine = Mathf.Max(8, Mathf.FloorToInt(width / 30f));
        int lineCount = EstimateLineCount(text, charsPerLine);
        return Mathf.Max(minHeight, lineCount * 36f + 20f);
    }

    private static float EstimatePreferredHeight(
        TMP_Text textComponent,
        string text,
        float minHeight,
        float verticalPadding,
        float fallbackWidth)
    {
        if (textComponent == null)
            return Mathf.Max(minHeight, EstimateLineCount(text, 24) * 36f + verticalPadding);

        float width = Mathf.Max(1f, textComponent.rectTransform.rect.width);
        if (width <= 1f)
            width = Mathf.Max(1f, fallbackWidth);

        float preferredHeight = textComponent.GetPreferredValues(
            string.IsNullOrEmpty(text) ? " " : text,
            width,
            Mathf.Infinity).y;

        return Mathf.Max(minHeight, preferredHeight + Mathf.Max(0f, verticalPadding));
    }

    private static int EstimateLineCount(string text, int charsPerLine)
    {
        if (string.IsNullOrEmpty(text))
            return 1;

        int lines = 0;
        string[] parts = text.Split('\n');
        foreach (string part in parts)
        {
            int length = string.IsNullOrEmpty(part) ? 1 : part.Length;
            lines += Mathf.Max(1, Mathf.CeilToInt(length / (float)Mathf.Max(1, charsPerLine)));
        }

        return Mathf.Max(1, lines);
    }

    private void OnQuestionClick()
    {
        Debug.Log($"[QuestionItem] 点击问题: {desText?.text}");
        mOnClickCallback?.Invoke();
    }
}
