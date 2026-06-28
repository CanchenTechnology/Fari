using UnityEngine;

[DisallowMultipleComponent]
public class DialogQuestionInputLayoutSettings : MonoBehaviour
{
    [Header("Question Input Layout")]
    [Tooltip("Maximum visible input lines before the text area scrolls.")]
    [Min(1)] public int questionInputMaxVisibleLines = 5;

    [Tooltip("Extra width added to the input text area while it contains text.")]
    [Min(0f)] public float questionInputExpandedRightExtend = 44f;

    [Tooltip("Extra left padding inside the input text area.")]
    [Min(0f)] public float questionInputTextLeftOffset = 0f;

    [Tooltip("Top padding between input text and the bubble edge.")]
    [Min(0f)] public float questionInputTextTopPadding = 16f;

    [Tooltip("Right padding reserved for text so it does not touch the scrollbar.")]
    [Min(0f)] public float questionInputTextRightPadding = 28f;

    [Tooltip("Bottom padding between input text and the bubble edge.")]
    [Min(0f)] public float questionInputTextBottomPadding = 16f;

    [Tooltip("Extra top breathing room only when the input reaches its maximum scrollable height.")]
    [Min(0f)] public float questionInputMaxStateTopPadding = 16f;

    [Tooltip("Distance from the bottom edge to the left sparkle button.")]
    [Min(0f)] public float questionInputLeftButtonBottomInset = 4f;

    [Tooltip("Left inset for the text viewport, reserving room for the sparkle button.")]
    [Min(0f)] public float questionInputViewportLeftInset = 112f;

    [Tooltip("Right inset for the text viewport, reserving room near the send button.")]
    [Min(0f)] public float questionInputViewportRightInset = 64f;

    [Tooltip("Extra top inset applied to the text viewport.")]
    [Min(0f)] public float questionInputViewportTopInset = 0f;

    [Tooltip("Extra bottom inset applied to the text viewport.")]
    [Min(0f)] public float questionInputViewportBottomInset = 0f;

    [Tooltip("How far the input scrollbar sits to the right of the text area.")]
    [Min(0f)] public float questionInputScrollbarRightOutset = 24f;

    [Tooltip("Top and bottom inset of the input scrollbar.")]
    [Min(0f)] public float questionInputScrollbarVerticalInset = 10f;

    [Tooltip("Visible width of the input scrollbar.")]
    [Min(1f)] public float questionInputScrollbarWidth = 8f;

    [Tooltip("How long the scrollbar remains visible after scrolling.")]
    [Min(0.1f)] public float questionInputScrollbarVisibleSeconds = 0.7f;

    private void Reset()
    {
        ClampValues();
    }

    private void OnValidate()
    {
        ClampValues();
    }

    private void ClampValues()
    {
        questionInputMaxVisibleLines = Mathf.Max(1, questionInputMaxVisibleLines);
        questionInputExpandedRightExtend = Mathf.Max(0f, questionInputExpandedRightExtend);
        questionInputTextLeftOffset = Mathf.Max(0f, questionInputTextLeftOffset);
        questionInputTextTopPadding = Mathf.Max(0f, questionInputTextTopPadding);
        questionInputTextRightPadding = Mathf.Max(0f, questionInputTextRightPadding);
        questionInputTextBottomPadding = Mathf.Max(0f, questionInputTextBottomPadding);
        questionInputMaxStateTopPadding = Mathf.Max(0f, questionInputMaxStateTopPadding);
        questionInputLeftButtonBottomInset = Mathf.Max(0f, questionInputLeftButtonBottomInset);
        questionInputViewportLeftInset = Mathf.Max(0f, questionInputViewportLeftInset);
        questionInputViewportRightInset = Mathf.Max(0f, questionInputViewportRightInset);
        questionInputViewportTopInset = Mathf.Max(0f, questionInputViewportTopInset);
        questionInputViewportBottomInset = Mathf.Max(0f, questionInputViewportBottomInset);
        questionInputScrollbarRightOutset = Mathf.Max(0f, questionInputScrollbarRightOutset);
        questionInputScrollbarVerticalInset = Mathf.Max(0f, questionInputScrollbarVerticalInset);
        questionInputScrollbarWidth = Mathf.Max(1f, questionInputScrollbarWidth);
        questionInputScrollbarVisibleSeconds = Mathf.Max(0.1f, questionInputScrollbarVisibleSeconds);
    }
}
