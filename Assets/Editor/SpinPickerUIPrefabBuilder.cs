using System.IO;
using SuperScrollView;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SpinPickerUIPrefabBuilder
{
	private const string RuntimePrefabPath = "Assets/GameData/UI/Main/Friend/SpinPickerUI.prefab";
	private const string SourcePrefabPath = "Assets/Scripts/SpinPicker/SpinPickerUI.prefab";

	private static readonly Color Gold = new Color(0.92f, 0.72f, 0.38f, 1f);
	private static readonly Color SoftGold = new Color(1f, 0.86f, 0.58f, 1f);
	private static readonly Color MutedText = new Color(0.66f, 0.62f, 0.70f, 1f);
	private static readonly Color PanelColor = new Color(0.065f, 0.055f, 0.11f, 0.96f);
	private static readonly Color WheelColor = new Color(0.12f, 0.11f, 0.17f, 0.98f);
	private static readonly Color ButtonColor = new Color(0.34f, 0.16f, 0.48f, 0.98f);

	[MenuItem("Tools/UI/Build SpinPickerUI Prefab")]
	public static void BuildSpinPickerUI()
	{
		GameObject root = new GameObject("SpinPickerUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
		RectTransform rootRect = root.GetComponent<RectTransform>();
		rootRect.anchorMin = Vector2.zero;
		rootRect.anchorMax = Vector2.zero;
		rootRect.pivot = Vector2.zero;
		rootRect.sizeDelta = Vector2.zero;
		rootRect.localScale = Vector3.zero;

		Canvas canvas = root.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceCamera;

		CanvasScaler scaler = root.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1080, 1920);

		GameObject mask = CreatePanel("UIMask", root.transform, new Color(0f, 0f, 0f, 0.62f));
		Stretch(mask.GetComponent<RectTransform>());
		mask.AddComponent<CanvasGroup>();

		GameObject content = new GameObject("UIContent", typeof(RectTransform));
		content.transform.SetParent(root.transform, false);
		Stretch(content.GetComponent<RectTransform>());

		CreateBackground(content.transform);
		CreateCloseButton(content.transform);
		CreateDatePicker(content.transform);
		CreateTimePicker(content.transform);
		CreateRegionPicker(content.transform);

		SetLayerRecursive(root, LayerMask.NameToLayer("UI"));
		SavePrefab(root, RuntimePrefabPath);
		SavePrefab(root, SourcePrefabPath);
		Object.DestroyImmediate(root);
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("SpinPickerUI prefab generated: " + RuntimePrefabPath + " and " + SourcePrefabPath);
	}

	[MenuItem("Tools/UI/Build SpinPickerUI And Fix CreateFriendUI")]
	public static void BuildSpinPickerUIAndFixCreateFriendUI()
	{
		BuildSpinPickerUI();
		CreateFriendUIPrefabBindingFixer.FixCreateFriendUIBindings();
	}

	private static void CreateBackground(Transform parent)
	{
		Image bg = CreateImage("DimBackground", parent, null, new Color(0.01f, 0.008f, 0.025f, 0.12f));
		Stretch(bg.rectTransform);
	}

	private static void CreateCloseButton(Transform parent)
	{
		GameObject close = CreatePanel("[Button]Close", parent, new Color(0.025f, 0.02f, 0.045f, 0.82f));
		Rect(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, 370), new Vector2(72, 72), new Vector2(0.5f, 0.5f));
		CreateOutline(close.transform, Vector2.zero, new Vector2(72, 72), Gold, 1.8f);
		CreateText("Icon", close.transform, "<", 42, SoftGold, TextAnchor.MiddleCenter,
			Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyle.Bold, new Vector2(0.5f, 0.5f), true);
		Button button = close.AddComponent<Button>();
		button.targetGraphic = close.GetComponent<Image>();
	}

	private static void CreateDatePicker(Transform parent)
	{
		GameObject panel = CreatePickerPanel("SpinDatePicker", parent, "选择出生日期", "滚动选择年、月、日");
		SpinDatePicker picker = panel.AddComponent<SpinDatePicker>();
		picker.CurSelect = CreateText("CurSelect", panel.transform, "2000年01月01日", 34, SoftGold, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(620, 52), FontStyle.Bold);
		picker.mLoopListViewYear = CreateWheelColumn(panel.transform, "ScrollViewYear", "年", new Vector2(-260, -365), new Vector2(210, 360), 32);
		picker.mLoopListViewMonth = CreateWheelColumn(panel.transform, "ScrollViewMonth", "月", new Vector2(0, -365), new Vector2(210, 360), 34);
		picker.mLoopListViewDay = CreateWheelColumn(panel.transform, "ScrollViewDay", "日", new Vector2(260, -365), new Vector2(210, 360), 34);
		picker.ConfirmButton = CreateConfirmButton(panel.transform, "确认日期");
	}

	private static void CreateTimePicker(Transform parent)
	{
		GameObject panel = CreatePickerPanel("SpinTimePicker", parent, "选择出生时间", "滚动选择小时和分钟");
		SpinTimePicker picker = panel.AddComponent<SpinTimePicker>();
		picker.CurSelect = CreateText("CurSelect", panel.transform, "12:00", 38, SoftGold, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(620, 52), FontStyle.Bold);
		picker.mLoopListViewHour = CreateWheelColumn(panel.transform, "ScrollViewHour", "时", new Vector2(-150, -365), new Vector2(230, 360), 36);
		picker.mLoopListViewMinute = CreateWheelColumn(panel.transform, "ScrollViewMinute", "分", new Vector2(150, -365), new Vector2(230, 360), 36);
		picker.ConfirmButton = CreateConfirmButton(panel.transform, "确认时间");
		panel.SetActive(false);
	}

	private static void CreateRegionPicker(Transform parent)
	{
		GameObject panel = CreatePickerPanel("SpinRegionPicker", parent, "选择出生地区", "滚动选择国家、省/州和城市/区");
		SpinRegionPicker picker = panel.AddComponent<SpinRegionPicker>();
		picker.CurSelect = CreateText("CurSelect", panel.transform, "中国 · 广东 · 深圳", 34, SoftGold, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(700, 52), FontStyle.Bold);
		picker.mLoopListViewCountry = CreateWheelColumn(panel.transform, "ScrollViewCountry", "国家", new Vector2(-260, -365), new Vector2(220, 360), 30);
		picker.mLoopListViewProvince = CreateWheelColumn(panel.transform, "ScrollViewProvince", "省/州", new Vector2(0, -365), new Vector2(220, 360), 30);
		picker.mLoopListViewCity = CreateWheelColumn(panel.transform, "ScrollViewCity", "城市/区", new Vector2(260, -365), new Vector2(220, 360), 30);
		picker.ConfirmButton = CreateConfirmButton(panel.transform, "确认地区");
		panel.SetActive(false);
	}

	private static GameObject CreatePickerPanel(string name, Transform parent, string title, string subtitle)
	{
		GameObject panel = CreatePanel(name, parent, PanelColor);
		Rect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(860, 760), new Vector2(0.5f, 0.5f));
		CreateOutline(panel.transform, Vector2.zero, new Vector2(860, 760), new Color(0.64f, 0.46f, 0.34f, 0.82f), 2f);
		CreateText("Title", panel.transform, "── ✦  " + title + "  ✦ ──", 44, SoftGold, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(740, 64), FontStyle.Bold);
		CreateText("Subtitle", panel.transform, subtitle, 26, MutedText, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(620, 42), FontStyle.Normal);
		CreateText("Divider", panel.transform, "✦                                      ✦", 28, new Color(0.90f, 0.68f, 0.38f, 0.72f), TextAnchor.MiddleCenter,
			new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(760, 36), FontStyle.Normal);
		return panel;
	}

	private static LoopListView2 CreateWheelColumn(Transform parent, string name, string label, Vector2 pos, Vector2 size, int itemFontSize)
	{
		CreateText("Label_" + label, parent, label, 28, SoftGold, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos + new Vector2(0, size.y * 0.5f + 48), new Vector2(size.x, 40), FontStyle.Bold);

		GameObject column = CreatePanel(name, parent, WheelColor);
		Rect(column.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, size, new Vector2(0.5f, 0.5f));
		CreateOutline(column.transform, Vector2.zero, size, new Color(0.62f, 0.47f, 0.34f, 0.54f), 1.3f);

		ScrollRect scrollRect = column.AddComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Elastic;
		scrollRect.elasticity = 0.1f;
		scrollRect.inertia = true;
		scrollRect.decelerationRate = 0.135f;
		scrollRect.scrollSensitivity = 1f;

		GameObject viewport = CreatePanel("Viewport", column.transform, new Color(1f, 1f, 1f, 0.01f));
		Stretch(viewport.GetComponent<RectTransform>());
		viewport.AddComponent<Mask>().showMaskGraphic = false;

		GameObject content = new GameObject("Content", typeof(RectTransform));
		content.transform.SetParent(viewport.transform, false);
		Stretch(content.GetComponent<RectTransform>());

		scrollRect.viewport = viewport.GetComponent<RectTransform>();
		scrollRect.content = content.GetComponent<RectTransform>();

		CreateSelectionLine("LineTop", column.transform, new Vector2(0, 48), size);
		CreateSelectionLine("LineBottom", column.transform, new Vector2(0, -48), size);

		GameObject itemPrefab = new GameObject("ItemPrefab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LoopListViewItem2), typeof(SpinPickerItem));
		itemPrefab.transform.SetParent(column.transform, false);
		RectTransform itemRect = itemPrefab.GetComponent<RectTransform>();
		Rect(itemRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x, 96), new Vector2(0.5f, 0.5f));
		Text itemText = itemPrefab.GetComponent<Text>();
		itemText.font = GetUIFont();
		itemText.fontSize = itemFontSize;
		itemText.color = MutedText;
		itemText.alignment = TextAnchor.MiddleCenter;
		itemText.horizontalOverflow = HorizontalWrapMode.Overflow;
		itemText.verticalOverflow = VerticalWrapMode.Overflow;
		itemPrefab.GetComponent<SpinPickerItem>().mText = itemText;

		LoopListView2 loopListView = column.AddComponent<LoopListView2>();
		loopListView.ItemPrefabDataList.Add(new ItemPrefabConfData
		{
			mItemPrefab = itemPrefab,
			mPadding = 0,
			mInitCreateCount = 5,
			mStartPosOffset = 0
		});
		loopListView.ArrangeType = ListItemArrangeType.TopToBottom;
		SetLoopListViewSnap(loopListView);
		return loopListView;
	}

	private static void CreateSelectionLine(string name, Transform parent, Vector2 pos, Vector2 columnSize)
	{
		Image line = CreateImage(name, parent, null, new Color(0.95f, 0.82f, 0.62f, 0.88f));
		Rect(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(columnSize.x - 22, 2), new Vector2(0.5f, 0.5f));
	}

	private static Button CreateConfirmButton(Transform parent, string label)
	{
		GameObject buttonObj = CreatePanel("confirmBtn", parent, ButtonColor);
		Rect(buttonObj.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 72), new Vector2(650, 86), new Vector2(0.5f, 0.5f));
		CreateOutline(buttonObj.transform, Vector2.zero, new Vector2(650, 86), Gold, 2.2f);
		CreateText("Text", buttonObj.transform, "✦  " + label + "  ✦", 36, SoftGold, TextAnchor.MiddleCenter,
			Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyle.Bold, new Vector2(0.5f, 0.5f), true);
		Button button = buttonObj.AddComponent<Button>();
		button.targetGraphic = buttonObj.GetComponent<Image>();
		return button;
	}

	private static void SetLoopListViewSnap(LoopListView2 listView)
	{
		SerializedObject serializedObject = new SerializedObject(listView);
		serializedObject.FindProperty("mItemSnapEnable").boolValue = true;
		serializedObject.FindProperty("mSupportScrollBar").boolValue = false;
		serializedObject.FindProperty("mViewPortSnapPivot").vector2Value = new Vector2(0, 0.5f);
		serializedObject.FindProperty("mItemSnapPivot").vector2Value = new Vector2(0, 0.5f);
		serializedObject.ApplyModifiedPropertiesWithoutUndo();
	}

	private static void SavePrefab(GameObject root, string path)
	{
		string directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}
		PrefabUtility.SaveAsPrefabAsset(root, path);
	}

	private static GameObject CreatePanel(string name, Transform parent, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		go.transform.SetParent(parent, false);
		go.GetComponent<Image>().color = color;
		return go;
	}

	private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		go.transform.SetParent(parent, false);
		Image image = go.GetComponent<Image>();
		image.sprite = sprite;
		image.color = color;
		return image;
	}

	private static Text CreateText(string name, Transform parent, string value, int fontSize, Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, FontStyle style, Vector2? pivot = null, bool stretchSize = false)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
		go.transform.SetParent(parent, false);
		Rect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, anchoredPosition, size, pivot ?? new Vector2(0.5f, 0.5f), stretchSize);
		Text text = go.GetComponent<Text>();
		text.text = value;
		text.font = GetUIFont();
		text.fontSize = fontSize;
		text.fontStyle = style;
		text.color = color;
		text.alignment = alignment;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Overflow;

		Shadow shadow = go.GetComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.86f);
		shadow.effectDistance = Vector2.zero;
		return text;
	}

	private static void CreateOutline(Transform parent, Vector2 anchoredPosition, Vector2 size, Color color, float thickness)
	{
		CreateLine(parent, "OutlineTop", new Vector2(anchoredPosition.x, anchoredPosition.y + size.y * 0.5f), new Vector2(size.x, thickness), color);
		CreateLine(parent, "OutlineBottom", new Vector2(anchoredPosition.x, anchoredPosition.y - size.y * 0.5f), new Vector2(size.x, thickness), color);
		CreateLine(parent, "OutlineLeft", new Vector2(anchoredPosition.x - size.x * 0.5f, anchoredPosition.y), new Vector2(thickness, size.y), color);
		CreateLine(parent, "OutlineRight", new Vector2(anchoredPosition.x + size.x * 0.5f, anchoredPosition.y), new Vector2(thickness, size.y), color);
	}

	private static void CreateLine(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
	{
		Image line = CreateImage(name, parent, null, color);
		Rect(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, new Vector2(0.5f, 0.5f));
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		rect.pivot = new Vector2(0.5f, 0.5f);
	}

	private static void Rect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Vector2 pivot, bool stretchSize = false)
	{
		rect.anchorMin = anchorMin;
		rect.anchorMax = anchorMax;
		rect.pivot = pivot;
		rect.anchoredPosition = anchoredPosition;
		if (stretchSize)
		{
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}
		else
		{
			rect.sizeDelta = size;
		}
	}

	private static Font GetUIFont()
	{
		return AssetDatabase.LoadAssetAtPath<Font>("Assets/GamerFrameWork/I2/Localization/Examples/Resources/ARIAL.TTF")
			?? Resources.GetBuiltinResource<Font>("Arial.ttf");
	}

	private static void SetLayerRecursive(GameObject go, int layer)
	{
		go.layer = layer;
		foreach (Transform child in go.transform)
		{
			SetLayerRecursive(child.gameObject, layer);
		}
	}
}
