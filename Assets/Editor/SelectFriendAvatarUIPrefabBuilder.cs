using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SelectFriendAvatarUIPrefabBuilder
{
	private const string PrefabPath = "Assets/GameData/UI/Main/Friend/SelectFriendAvatarUI.prefab";

	private static readonly Color Gold = new Color(0.92f, 0.72f, 0.38f, 1f);
	private static readonly Color SoftGold = new Color(1f, 0.86f, 0.58f, 1f);
	private static readonly Color MutedText = new Color(0.70f, 0.66f, 0.70f, 1f);
	private static readonly Color PanelColor = new Color(0.075f, 0.06f, 0.12f, 0.92f);
	private static readonly Color FieldColor = new Color(0.13f, 0.09f, 0.18f, 0.86f);
	private static readonly Color PurpleButton = new Color(0.33f, 0.12f, 0.43f, 0.97f);

	[MenuItem("Tools/UI/Build SelectFriendAvatarUI Prefab")]
	public static void BuildSelectFriendAvatarUI()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

		GameObject root = new GameObject("SelectFriendAvatarUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(SelectFriendAvatarUIComponent));
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

		GameObject mask = CreatePanel("UIMask", root.transform, new Color(0f, 0f, 0f, 0f));
		Stretch(mask.GetComponent<RectTransform>());
		mask.AddComponent<CanvasGroup>();

		GameObject content = new GameObject("UIContent", typeof(RectTransform));
		content.transform.SetParent(root.transform, false);
		Stretch(content.GetComponent<RectTransform>());

		SelectFriendAvatarUIComponent component = root.GetComponent<SelectFriendAvatarUIComponent>();
		component.windowLayer = GamerFrameWork.UIFrameWork.WindowLayer.MainUI;

		CreateBackground(content.transform);
		CreateStatusBar(content.transform);
		component.BackButton = CreateBackButton(content.transform);
		CreateHeader(content.transform);
		CreateMainPanel(content.transform, component);
		CreateBottomNav(content.transform);

		SetLayerRecursive(root, LayerMask.NameToLayer("UI"));
		PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
		Object.DestroyImmediate(root);
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("SelectFriendAvatarUI prefab generated: " + PrefabPath);
	}

	private static void CreateBackground(Transform parent)
	{
		Image bg = CreateImage("Background", parent, LoadSprite("Assets/GameData/Arts/flow-1-flip.png"), Color.white);
		Stretch(bg.rectTransform);
		bg.preserveAspect = false;

		Image veil = CreateImage("DarkVeil", parent, null, new Color(0.01f, 0.008f, 0.025f, 0.60f));
		Stretch(veil.rectTransform);
	}

	private static void CreateStatusBar(Transform parent)
	{
		CreateText("StatusTime", parent, "9:41", 32, Color.white, TextAnchor.MiddleLeft,
			new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -46), new Vector2(150, 44), FontStyle.Bold, new Vector2(0, 0.5f));
		CreateText("StatusIcons", parent, "▮▮▮  ◠  ▭", 28, Color.white, TextAnchor.MiddleRight,
			new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -46), new Vector2(260, 44), FontStyle.Bold, new Vector2(1, 0.5f));
	}

	private static Button CreateBackButton(Transform parent)
	{
		GameObject go = CreatePanel("[Button]Back", parent, new Color(0.02f, 0.018f, 0.04f, 0.46f));
		Rect(go.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(86, -150), new Vector2(84, 84), new Vector2(0.5f, 0.5f));
		CreateOutline(go.transform, Vector2.zero, new Vector2(84, 84), Gold, 2f);
		CreateText("Icon", go.transform, "<", 48, SoftGold, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72, 72), FontStyle.Bold);
		Button button = go.AddComponent<Button>();
		button.targetGraphic = go.GetComponent<Image>();
		return button;
	}

	private static void CreateHeader(Transform parent)
	{
		CreateText("Title", parent, "── ✦✦  选择好友头像  ✦✦ ──", 48, SoftGold, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(790, 72), FontStyle.Bold);
		CreateText("Subtitle", parent, "为你的朋友设置一个独特的头像", 30, MutedText, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -220), new Vector2(680, 48), FontStyle.Normal);
	}

	private static void CreateMainPanel(Transform parent, SelectFriendAvatarUIComponent component)
	{
		GameObject panel = CreatePanel("AvatarPanel", parent, PanelColor);
		Rect(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -945), new Vector2(910, 1320), new Vector2(0.5f, 0.5f));
		CreateOutline(panel.transform, Vector2.zero, new Vector2(910, 1320), new Color(0.63f, 0.45f, 0.34f, 0.72f), 2f);
		CreateText("CornerDecor", panel.transform, "✦                                      ✦\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n✦                                      ✦", 28, new Color(0.88f, 0.65f, 0.36f, 0.68f), TextAnchor.MiddleCenter,
			new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840, 1240), FontStyle.Normal);

		Image aura = CreateImage("PreviewAura", panel.transform, LoadSprite("Assets/GameData/Arts/images/asset-105.png"), new Color(0.88f, 0.44f, 1f, 0.30f));
		Rect(aura.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -235), new Vector2(360, 360), new Vector2(0.5f, 0.5f));

		GameObject previewFrame = CreatePanel("PreviewFrame", panel.transform, new Color(0.02f, 0.016f, 0.04f, 0.78f));
		Rect(previewFrame.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -235), new Vector2(250, 250), new Vector2(0.5f, 0.5f));
		CreateOutline(previewFrame.transform, Vector2.zero, new Vector2(250, 250), SoftGold, 3f);
		component.PreviewAvatarImage = CreateImage("[Image]PreviewAvatar", previewFrame.transform, LoadSprite("Assets/GameData/Arts/images/asset-105.png"), Color.white);
		Rect(component.PreviewAvatarImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(205, 205), new Vector2(0.5f, 0.5f));
		component.PreviewAvatarImage.preserveAspect = true;

		GameObject cameraBadge = CreatePanel("CameraBadge", previewFrame.transform, new Color(0.42f, 0.28f, 0.22f, 0.96f));
		Rect(cameraBadge.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 40), new Vector2(70, 70), new Vector2(0.5f, 0.5f));
		CreateOutline(cameraBadge.transform, Vector2.zero, new Vector2(70, 70), SoftGold, 2f);
		CreateText("Icon", cameraBadge.transform, "▣", 36, SoftGold, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60), FontStyle.Bold);

		component.PhotoUploadButton = CreateUploadOption(panel.transform, new Vector2(-270, -470), "▣", "拍照上传");
		component.AlbumSelectButton = CreateUploadOption(panel.transform, new Vector2(0, -470), "▧", "从相册选择");
		component.AIAvatarButton = CreateUploadOption(panel.transform, new Vector2(270, -470), "✦", "使用 AI 头像");

		CreateText("StyleTitle", panel.transform, "── ✦  选择头像风格  ✦ ──", 32, SoftGold, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -625), new Vector2(660, 54), FontStyle.Bold);

		CreateAvatarGrid(panel.transform, component);

		CreateText("Tip", panel.transform, "✦ 小贴士：选择符合朋友个性的头像，让你们的连接更加特别 ✦", 23, new Color(0.90f, 0.70f, 0.44f, 1f), TextAnchor.MiddleCenter,
			new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 155), new Vector2(790, 44), FontStyle.Normal);
		component.ConfirmButton = CreateConfirmButton(panel.transform);
	}

	private static Button CreateUploadOption(Transform parent, Vector2 pos, string icon, string label)
	{
		GameObject go = CreatePanel("[Button]" + label, parent, FieldColor);
		Rect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, new Vector2(250, 132), new Vector2(0.5f, 0.5f));
		CreateOutline(go.transform, Vector2.zero, new Vector2(250, 132), new Color(0.61f, 0.45f, 0.34f, 0.78f), 1.6f);
		CreateText("Icon", go.transform, icon, 38, new Color(0.83f, 0.33f, 1f, 1f), TextAnchor.MiddleCenter,
			new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(72, 46), FontStyle.Bold);
		CreateText("Label", go.transform, label, 28, new Color(0.86f, 0.82f, 0.80f, 1f), TextAnchor.MiddleCenter,
			new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(210, 44), FontStyle.Normal);
		Button button = go.AddComponent<Button>();
		button.targetGraphic = go.GetComponent<Image>();
		return button;
	}

	private static void CreateAvatarGrid(Transform parent, SelectFriendAvatarUIComponent component)
	{
		string[] spritePaths =
		{
			"Assets/GameData/Arts/images/asset-105.png",
			"Assets/GameData/Arts/Sprites/MajorArcana/RWS_Tarot_18_Moon.jpg",
			"Assets/GameData/Arts/Sprites/Cups/Cups02.jpg",
			"Assets/GameData/Arts/images/asset-080.png",
			"Assets/GameData/Arts/Sprites/MajorArcana/RWS_Tarot_21_World.jpg",
			"Assets/GameData/Arts/witch-full.png",
			"Assets/GameData/Arts/images/asset-078.png",
			"Assets/GameData/Arts/images/asset-077.png"
		};

		component.AvatarStyleButtons = new Button[spritePaths.Length];
		component.AvatarStyleImages = new Image[spritePaths.Length];
		component.SelectedMarks = new GameObject[spritePaths.Length];

		float startX = -315f;
		float startY = -760f;
		float gapX = 210f;
		float gapY = 190f;
		for (int i = 0; i < spritePaths.Length; i++)
		{
			int row = i / 4;
			int col = i % 4;
			Vector2 pos = new Vector2(startX + col * gapX, startY - row * gapY);
			CreateAvatarCell(parent, i, pos, LoadSprite(spritePaths[i]), component);
		}
	}

	private static void CreateAvatarCell(Transform parent, int index, Vector2 pos, Sprite sprite, SelectFriendAvatarUIComponent component)
	{
		GameObject cell = CreatePanel("[Button]AvatarStyle" + (index + 1), parent, new Color(0.02f, 0.016f, 0.04f, 0.74f));
		Rect(cell.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, new Vector2(152, 152), new Vector2(0.5f, 0.5f));
		CreateOutline(cell.transform, Vector2.zero, new Vector2(152, 152), index == 0 ? new Color(0.78f, 0.26f, 1f, 1f) : Gold, index == 0 ? 4f : 2f);

		Image image = CreateImage("AvatarImage", cell.transform, sprite, Color.white);
		Rect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130, 130), new Vector2(0.5f, 0.5f));
		image.preserveAspect = true;

		GameObject mark = CreatePanel("SelectedMark", cell.transform, new Color(0.43f, 0.22f, 0.72f, 0.98f));
		Rect(mark.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -18), new Vector2(58, 58), new Vector2(0.5f, 0.5f));
		CreateOutline(mark.transform, Vector2.zero, new Vector2(58, 58), new Color(0.94f, 0.75f, 1f, 1f), 2f);
		CreateText("Check", mark.transform, "✓", 36, Color.white, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 52), FontStyle.Bold);
		mark.SetActive(index == 0);

		Button button = cell.AddComponent<Button>();
		button.targetGraphic = cell.GetComponent<Image>();
		component.AvatarStyleButtons[index] = button;
		component.AvatarStyleImages[index] = image;
		component.SelectedMarks[index] = mark;
	}

	private static Button CreateConfirmButton(Transform parent)
	{
		GameObject go = CreatePanel("[Button]Confirm", parent, PurpleButton);
		Rect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 72), new Vector2(790, 88), new Vector2(0.5f, 0.5f));
		CreateOutline(go.transform, Vector2.zero, new Vector2(790, 88), Gold, 2.4f);
		CreateText("Label", go.transform, "✦   确认头像   ✦", 40, SoftGold, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(650, 74), FontStyle.Bold);
		Button button = go.AddComponent<Button>();
		button.targetGraphic = go.GetComponent<Image>();
		return button;
	}

	private static void CreateBottomNav(Transform parent)
	{
		GameObject nav = CreatePanel("BottomNavigation", parent, new Color(0.055f, 0.055f, 0.08f, 0.92f));
		Rect(nav.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(930, 150), new Vector2(0.5f, 0.5f));
		CreateOutline(nav.transform, Vector2.zero, new Vector2(930, 150), new Color(0.40f, 0.32f, 0.34f, 0.52f), 1.2f);

		CreateNavItem(nav.transform, new Vector2(-360, 8), "◌", "首页", false);
		CreateNavItem(nav.transform, new Vector2(-180, 8), "☽", "占卜", false);
		CreateNavItem(nav.transform, new Vector2(0, 8), "✦", "探索", false);
		CreateNavItem(nav.transform, new Vector2(180, 8), "♊", "朋友", true);
		CreateNavItem(nav.transform, new Vector2(360, 8), "◎", "我的", false);
	}

	private static void CreateNavItem(Transform parent, Vector2 pos, string icon, string label, bool selected)
	{
		Color color = selected ? new Color(0.92f, 0.45f, 1f, 1f) : new Color(0.68f, 0.60f, 0.56f, 1f);
		CreateText("NavIcon_" + label, parent, icon, 42, color, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos + new Vector2(0, 25), new Vector2(100, 48), FontStyle.Bold);
		CreateText("NavText_" + label, parent, label, 24, color, TextAnchor.MiddleCenter,
			new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos + new Vector2(0, -32), new Vector2(100, 36), FontStyle.Normal);
		if (selected)
		{
			CreateText("NavDot", parent, "•", 34, color, TextAnchor.MiddleCenter,
				new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos + new Vector2(0, -64), new Vector2(80, 24), FontStyle.Bold);
		}
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
		text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/GamerFrameWork/I2/Localization/Examples/Resources/ARIAL.TTF") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
		text.fontSize = fontSize;
		text.fontStyle = style;
		text.color = color;
		text.alignment = alignment;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		Shadow shadow = go.GetComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
		shadow.effectDistance = Vector2.zero;
		return text;
	}

	private static void CreateOutline(Transform parent, Vector2 position, Vector2 size, Color color, float thickness)
	{
		CreateLine("TopBorder", parent, new Vector2(position.x, position.y + size.y * 0.5f), new Vector2(size.x, thickness), color);
		CreateLine("BottomBorder", parent, new Vector2(position.x, position.y - size.y * 0.5f), new Vector2(size.x, thickness), color);
		CreateLine("LeftBorder", parent, new Vector2(position.x - size.x * 0.5f, position.y), new Vector2(thickness, size.y), color);
		CreateLine("RightBorder", parent, new Vector2(position.x + size.x * 0.5f, position.y), new Vector2(thickness, size.y), color);
	}

	private static void CreateLine(string name, Transform parent, Vector2 position, Vector2 size, Color color)
	{
		Image line = CreateImage(name, parent, null, color);
		Rect(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size, new Vector2(0.5f, 0.5f));
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.localScale = Vector3.one;
	}

	private static void Rect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Vector2 pivot, bool offsetMode = false)
	{
		rect.anchorMin = anchorMin;
		rect.anchorMax = anchorMax;
		rect.pivot = pivot;
		rect.localScale = Vector3.one;
		if (offsetMode)
		{
			rect.offsetMin = anchoredPosition;
			rect.offsetMax = size;
		}
		else
		{
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = size;
		}
	}

	private static Sprite LoadSprite(string path)
	{
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
	}

	private static void SetLayerRecursive(GameObject go, int layer)
	{
		if (layer < 0) return;
		go.layer = layer;
		foreach (Transform child in go.transform)
		{
			SetLayerRecursive(child.gameObject, layer);
		}
	}
}
