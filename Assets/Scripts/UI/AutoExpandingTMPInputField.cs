using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_InputField))]
public class AutoExpandingTMPInputField : MonoBehaviour
{
	[Header("References")]
	public TMP_InputField inputField;
	public RectTransform targetRect;
	public LayoutElement layoutElement;

	[Header("Sizing")]
	[Min(1f)] public float minHeight = 80f;
	[Min(1f)] public float maxHeight = 280f;
	[Min(0f)] public float verticalPadding = 28f;
	public bool syncLayoutElement = true;

	[Header("Text Alignment")]
	public TextAlignmentOptions collapsedAlignment = TextAlignmentOptions.MidlineLeft;
	public TextAlignmentOptions expandedAlignment = TextAlignmentOptions.TopLeft;

	private string lastText;
	private float lastMeasureWidth = -1f;
	private float lastFontSize = -1f;
	private float lastAppliedHeight = -1f;

	private void Reset()
	{
		ResolveReferences();
	}

	private void OnEnable()
	{
		ResolveReferences();

		if (inputField != null)
		{
			inputField.onValueChanged.AddListener(HandleValueChanged);
		}

		RefreshHeight();
	}

	private void OnDisable()
	{
		if (inputField != null)
		{
			inputField.onValueChanged.RemoveListener(HandleValueChanged);
		}
	}

	private void OnValidate()
	{
		minHeight = Mathf.Max(1f, minHeight);
		maxHeight = Mathf.Max(minHeight, maxHeight);
		verticalPadding = Mathf.Max(0f, verticalPadding);

		ResolveReferences();

		if (isActiveAndEnabled)
		{
			RefreshHeight();
		}
	}

	private void LateUpdate()
	{
		RefreshIfNeeded();
	}

	private void OnRectTransformDimensionsChange()
	{
		RefreshIfNeeded();
	}

	public void RefreshHeight()
	{
		if (inputField == null || inputField.textComponent == null)
		{
			return;
		}

		if (targetRect == null)
		{
			targetRect = transform as RectTransform;
		}

		if (targetRect == null)
		{
			return;
		}

		TMP_Text textComponent = inputField.textComponent;
		float measureWidth = GetMeasureWidth(textComponent);
		float targetHeight = CalculateTargetHeight(textComponent, measureWidth);

		ApplyHeight(targetHeight);
		ApplyTextAlignment(targetHeight);
		CacheMeasureState(measureWidth, targetHeight);
	}

	private void ResolveReferences()
	{
		if (inputField == null)
		{
			inputField = GetComponent<TMP_InputField>();
		}

		if (targetRect == null)
		{
			targetRect = transform as RectTransform;
		}

		if (layoutElement == null)
		{
			layoutElement = GetComponent<LayoutElement>();
		}
	}

	private void HandleValueChanged(string _)
	{
		RefreshHeight();
	}

	private void RefreshIfNeeded()
	{
		if (inputField == null || inputField.textComponent == null)
		{
			return;
		}

		float measureWidth = GetMeasureWidth(inputField.textComponent);
		float fontSize = inputField.textComponent.fontSize;

		if (lastText != inputField.text ||
			!Mathf.Approximately(lastMeasureWidth, measureWidth) ||
			!Mathf.Approximately(lastFontSize, fontSize))
		{
			RefreshHeight();
		}
	}

	private float CalculateTargetHeight(TMP_Text textComponent, float measureWidth)
	{
		string measureText = inputField.text;
		bool hasTrailingNewLine = !string.IsNullOrEmpty(measureText) && measureText.EndsWith("\n");

		if (string.IsNullOrEmpty(measureText))
		{
			measureText = " ";
		}

		float singleLineHeight = textComponent.GetPreferredValues("Ag", measureWidth, Mathf.Infinity).y;

		if (singleLineHeight <= 0f)
		{
			singleLineHeight = textComponent.fontSize + textComponent.lineSpacing;
		}

		float preferredTextHeight = textComponent.GetPreferredValues(measureText, measureWidth, Mathf.Infinity).y;
		if (hasTrailingNewLine)
		{
			preferredTextHeight += singleLineHeight;
		}

		float textHeight = Mathf.Max(singleLineHeight, preferredTextHeight);
		float wantedHeight = Mathf.Ceil(textHeight + verticalPadding);
		return Mathf.Clamp(wantedHeight, minHeight, maxHeight);
	}

	private float GetMeasureWidth(TMP_Text textComponent)
	{
		float width = 0f;

		if (inputField.textViewport != null)
		{
			width = inputField.textViewport.rect.width;
		}

		if (width <= 0f && targetRect != null)
		{
			width = targetRect.rect.width;
		}

		Vector4 margin = textComponent.margin;
		width -= margin.x + margin.z;
		return Mathf.Max(1f, width);
	}

	private void ApplyHeight(float targetHeight)
	{
		if (!Mathf.Approximately(lastAppliedHeight, targetHeight))
		{
			targetRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
			lastAppliedHeight = targetHeight;
		}

		if (syncLayoutElement && layoutElement != null)
		{
			layoutElement.minHeight = minHeight;
			layoutElement.preferredHeight = targetHeight;
			layoutElement.flexibleHeight = 0f;
		}

		LayoutRebuilder.MarkLayoutForRebuild(targetRect);

		if (targetRect.parent is RectTransform parentRect)
		{
			LayoutRebuilder.MarkLayoutForRebuild(parentRect);
		}
	}

	private void ApplyTextAlignment(float targetHeight)
	{
		TextAlignmentOptions alignment = targetHeight > minHeight + 0.5f
			? expandedAlignment
			: collapsedAlignment;

		inputField.textComponent.alignment = alignment;

		if (inputField.placeholder is TMP_Text placeholderText)
		{
			placeholderText.alignment = collapsedAlignment;
		}
	}

	private void CacheMeasureState(float measureWidth, float appliedHeight)
	{
		lastText = inputField.text;
		lastMeasureWidth = measureWidth;
		lastFontSize = inputField.textComponent.fontSize;
		lastAppliedHeight = appliedHeight;
	}
}
