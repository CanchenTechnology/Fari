using System;
using GamerFrameWork.UIFrameWork;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class MemoryEditOverlay
{
	private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.58f);
	private static readonly Color SheetColor = new Color(0.045f, 0.040f, 0.075f, 0.98f);
	private static readonly Color PurpleColor = new Color(0.34f, 0.18f, 0.64f, 0.95f);
	private static readonly Color DimColor = new Color(0.080f, 0.060f, 0.095f, 0.94f);
	private static readonly Color TextColor = new Color(0.90f, 0.87f, 0.92f, 1f);
	private static readonly Color SubTextColor = new Color(0.66f, 0.63f, 0.70f, 1f);
	private static readonly Color GoldColor = new Color(0.94f, 0.73f, 0.46f, 1f);

	public static void Show(Transform parent, MemoryUiItem item, Action<MemoryEditResult> onSave)
	{
		if (parent == null) return;

		GameObject overlay = CreatePanel("MemoryEditOverlay", parent, BackgroundColor);
		RectTransform overlayRect = overlay.GetComponent<RectTransform>();
		SetStretch(overlayRect);

		GameObject sheet = CreatePanel("MemoryEditSheet", overlay.transform, SheetColor);
		RectTransform sheetRect = sheet.GetComponent<RectTransform>();
		sheetRect.anchorMin = new Vector2(0.5f, 0.5f);
		sheetRect.anchorMax = new Vector2(0.5f, 0.5f);
		sheetRect.pivot = new Vector2(0.5f, 0.5f);
		sheetRect.anchoredPosition = Vector2.zero;
		sheetRect.sizeDelta = new Vector2(900f, 760f);

		CreateText("Title", sheet.transform, item == null ? "新增记忆" : "编辑记忆", 44, FontStyles.Bold, GoldColor, new Vector2(0, 298), new Vector2(760, 64), TextAnchor.MiddleCenter);
		CreateText("Hint", sheet.transform, "这条记忆会用于之后的神谕回复，你可以随时关闭或删除。", 27, FontStyles.Normal, SubTextColor, new Vector2(0, 245), new Vector2(760, 44), TextAnchor.MiddleCenter);

		TMP_InputField input = CreateInput(sheet.transform, item?.Text ?? "");

		MemoryUiCategory selectedCategory = item?.Category ?? MemoryUiCategory.Topic;
		bool wasPending = item?.PendingConfirm ?? false;
		bool enabled = item == null || item.Enabled || wasPending;
		bool important = item?.Important ?? false;

		Button topicBtn = CreateButton(sheet.transform, "对话主题", 26, new Vector2(-315, 70), new Vector2(160, 58));
		Button preferenceBtn = CreateButton(sheet.transform, "个人偏好", 26, new Vector2(-105, 70), new Vector2(160, 58));
		Button emotionBtn = CreateButton(sheet.transform, "情感模式", 26, new Vector2(105, 70), new Vector2(160, 58));
		Button growthBtn = CreateButton(sheet.transform, "成长轨迹", 26, new Vector2(315, 70), new Vector2(160, 58));

		Button enabledBtn = CreateButton(sheet.transform, "", 28, new Vector2(-215, -25), new Vector2(250, 62));
		Button importantBtn = CreateButton(sheet.transform, "", 28, new Vector2(215, -25), new Vector2(250, 62));

		Button cancelBtn = CreateButton(sheet.transform, "取消", 34, new Vector2(-230, -270), new Vector2(360, 88));
		Button saveBtn = CreateButton(sheet.transform, item == null ? "保存记忆" : "保存修改", 34, new Vector2(230, -270), new Vector2(360, 88));

		void Refresh()
		{
			SetCategoryButton(topicBtn, selectedCategory == MemoryUiCategory.Topic);
			SetCategoryButton(preferenceBtn, selectedCategory == MemoryUiCategory.Preference);
			SetCategoryButton(emotionBtn, selectedCategory == MemoryUiCategory.Emotion);
			SetCategoryButton(growthBtn, selectedCategory == MemoryUiCategory.Growth);
			SetToggleButton(enabledBtn, wasPending && enabled ? "保存后确认" : enabled ? "已启用" : "已关闭", enabled);
			SetToggleButton(importantBtn, important ? "重要记忆" : "普通记忆", important);
		}

		topicBtn.onClick.AddListener(() => { selectedCategory = MemoryUiCategory.Topic; Refresh(); });
		preferenceBtn.onClick.AddListener(() => { selectedCategory = MemoryUiCategory.Preference; Refresh(); });
		emotionBtn.onClick.AddListener(() => { selectedCategory = MemoryUiCategory.Emotion; Refresh(); });
		growthBtn.onClick.AddListener(() => { selectedCategory = MemoryUiCategory.Growth; Refresh(); });
		enabledBtn.onClick.AddListener(() => { enabled = !enabled; Refresh(); });
		importantBtn.onClick.AddListener(() => { important = !important; Refresh(); });
		cancelBtn.onClick.AddListener(() => UnityEngine.Object.Destroy(overlay));
		saveBtn.onClick.AddListener(() =>
		{
			string value = input != null ? input.text.Trim() : "";
			if (string.IsNullOrEmpty(value))
			{
				ToastManager.ShowToast("记忆内容不能为空");
				return;
			}

			onSave?.Invoke(new MemoryEditResult
			{
				Text = value,
				Category = selectedCategory,
				Enabled = enabled,
				Important = important
			});
			UnityEngine.Object.Destroy(overlay);
		});

		Refresh();
	}

	private static TMP_InputField CreateInput(Transform parent, string value)
	{
		GameObject go = CreatePanel("MemoryTextInput", parent, new Color(0.060f, 0.052f, 0.090f, 1f));
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = new Vector2(0, 160);
		rect.sizeDelta = new Vector2(760, 128);

		TMP_InputField input = go.AddComponent<TMP_InputField>();
		input.lineType = TMP_InputField.LineType.MultiLineNewline;
		input.text = value ?? "";

		GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
		textGo.transform.SetParent(go.transform, false);
		RectTransform textRect = textGo.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = new Vector2(24, 14);
		textRect.offsetMax = new Vector2(-24, -14);

		TMP_Text text = textGo.GetComponent<TMP_Text>();
		text.font = TMP_Settings.defaultFontAsset;
		text.fontSize = 30;
		text.color = TextColor;
		text.alignment = TextAlignmentOptions.TopLeft;
		text.enableWordWrapping = true;
		text.overflowMode = TextOverflowModes.Truncate;

		GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
		placeholderGo.transform.SetParent(go.transform, false);
		RectTransform placeholderRect = placeholderGo.GetComponent<RectTransform>();
		placeholderRect.anchorMin = Vector2.zero;
		placeholderRect.anchorMax = Vector2.one;
		placeholderRect.offsetMin = new Vector2(24, 14);
		placeholderRect.offsetMax = new Vector2(-24, -14);

		TMP_Text placeholder = placeholderGo.GetComponent<TMP_Text>();
		placeholder.font = TMP_Settings.defaultFontAsset;
		placeholder.fontSize = 30;
		placeholder.color = new Color(0.55f, 0.52f, 0.60f, 0.9f);
		placeholder.alignment = TextAlignmentOptions.TopLeft;
		placeholder.text = "输入一条你希望 AI 记住的信息";

		input.textComponent = text;
		input.placeholder = placeholder;
		input.fontAsset = TMP_Settings.defaultFontAsset;
		input.pointSize = 30;
		input.targetGraphic = go.GetComponent<Image>();
		input.text = value ?? "";
		return input;
	}

	private static GameObject CreatePanel(string name, Transform parent, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		go.transform.SetParent(parent, false);
		Image image = go.GetComponent<Image>();
		image.color = color;
		return go;
	}

	private static TMP_Text CreateText(string name, Transform parent, string value, int size, FontStyles style, Color color, Vector2 position, Vector2 sizeDelta, TextAnchor alignment)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = position;
		rect.sizeDelta = sizeDelta;

		TMP_Text text = go.GetComponent<TMP_Text>();
		text.font = TMP_Settings.defaultFontAsset;
		text.text = value ?? "";
		text.fontSize = size;
		text.fontStyle = style;
		text.color = color;
		text.alignment = TMPTextBridge.ToAlignment(alignment);
		text.enableWordWrapping = true;
		text.overflowMode = TextOverflowModes.Truncate;
		return text;
	}

	private static Button CreateButton(Transform parent, string label, int fontSize, Vector2 position, Vector2 size)
	{
		GameObject go = CreatePanel(label + "Button", parent, DimColor);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = position;
		rect.sizeDelta = size;

		Button button = go.AddComponent<Button>();
		button.targetGraphic = go.GetComponent<Image>();
		TMP_Text text = CreateText("Text", go.transform, label, fontSize, FontStyles.Normal, TextColor, Vector2.zero, size, TextAnchor.MiddleCenter);
		text.raycastTarget = false;
		return button;
	}

	private static void SetCategoryButton(Button button, bool selected)
	{
		if (button == null) return;
		Image image = button.GetComponent<Image>();
		if (image != null) image.color = selected ? PurpleColor : DimColor;
		TMP_Text text = button.GetComponentInChildren<TMP_Text>();
		if (text != null) text.color = selected ? TextColor : SubTextColor;
	}

	private static void SetToggleButton(Button button, string label, bool selected)
	{
		if (button == null) return;
		Image image = button.GetComponent<Image>();
		if (image != null) image.color = selected ? PurpleColor : DimColor;
		TMP_Text text = button.GetComponentInChildren<TMP_Text>();
		if (text != null)
		{
			text.text = label;
			text.color = selected ? TextColor : SubTextColor;
		}
	}

	private static void SetStretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}

}
