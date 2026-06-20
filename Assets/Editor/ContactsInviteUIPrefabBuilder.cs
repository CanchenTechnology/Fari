using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ContactsInviteUIPrefabBuilder
{
	private const string PrefabPath = "Assets/GameData/UI/Main/Friend/ContactsInviteUI.prefab";
	private const string RebuildRequestPath = "Temp/RebuildContactsInviteUI.request";

	private static readonly Color Bg = new Color(0.006f, 0.005f, 0.018f, 1f);
	private static readonly Color Panel = new Color(0.060f, 0.045f, 0.105f, 0.88f);
	private static readonly Color PanelDeep = new Color(0.024f, 0.024f, 0.052f, 0.90f);
	private static readonly Color Gold = new Color(0.94f, 0.72f, 0.36f, 1f);
	private static readonly Color SoftGold = new Color(1.00f, 0.84f, 0.55f, 1f);
	private static readonly Color TextMain = new Color(0.90f, 0.86f, 0.84f, 1f);
	private static readonly Color TextMuted = new Color(0.70f, 0.66f, 0.68f, 1f);
	private static readonly Color Purple = new Color(0.30f, 0.12f, 0.46f, 0.95f);
	private static readonly Color PurpleBright = new Color(0.70f, 0.28f, 1.00f, 1f);

	[InitializeOnLoadMethod]
	private static void AutoBuildFromRequest()
	{
		if (!File.Exists(RebuildRequestPath))
		{
			return;
		}

		File.Delete(RebuildRequestPath);
		EditorApplication.delayCall += Build;
	}

	[MenuItem("Tools/UI/Rebuild ContactsInviteUI")]
	public static void Build()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

		GameObject root = new GameObject("ContactsInviteUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(ContactsInviteUIComponent));
		RectTransform rootRect = root.GetComponent<RectTransform>();
		Stretch(rootRect);

		Canvas canvas = root.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceCamera;
		CanvasScaler scaler = root.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1080, 1920);
		scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		scaler.matchWidthOrHeight = 0f;

		GameObject uiMask = CreatePanel("UIMask", root.transform, new Color(0f, 0f, 0f, 0.34f));
		Stretch(uiMask.GetComponent<RectTransform>());
		uiMask.AddComponent<CanvasGroup>();

		GameObject content = CreateNode("UIContent", root.transform);
		Stretch(content.GetComponent<RectTransform>());
		BuildBackground(content.transform);
		Button back = BuildTopBar(content.transform);
		BuildMainScroll(content.transform);
		BuildBottomBar(content.transform);
		CreateNode("FloatingLayer", content.transform);

		GameObject popup = CreateNode("PopupLayer", root.transform);
		Stretch(popup.GetComponent<RectTransform>());

		ContactsInviteUIComponent component = root.GetComponent<ContactsInviteUIComponent>();
		component.BackButton = back;
		component.NotificationsButton = FindButton(root.transform, "[Button]Notifications");

		SetLayerRecursive(root, LayerMask.NameToLayer("UI"));
		PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
		Object.DestroyImmediate(root);
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("Rebuilt ContactsInviteUI with MD screen adaptation rules: " + PrefabPath);
	}

	private static void BuildBackground(Transform parent)
	{
		Image bg = CreateImage("Background", parent, SpriteAt("Assets/GameData/Arts/flow-1-flip.png"), Color.white, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
		Stretch(bg.rectTransform);
		bg.preserveAspect = false;

		Image dark = CreatePanel("DarkVeil", parent, new Color(Bg.r, Bg.g, Bg.b, 0.54f)).GetComponent<Image>();
		Stretch(dark.rectTransform);

		RectTransform topShade = CreatePanel("TopShade", parent, new Color(0.010f, 0.008f, 0.026f, 0.64f)).GetComponent<RectTransform>();
		Rect(topShade, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 260), new Vector2(0.5f, 1));

		CreateText("StarField", parent, "✦        ✧        ✦\n\n      ✧       ✦       ✧\n\n✧             ✦             ✧", 28, new Color(0.88f, 0.54f, 1f, 0.22f), TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 250), new Vector2(920, 520), FontStyle.Normal);
	}

	private static Button BuildTopBar(Transform parent)
	{
		GameObject top = CreateNode("TopBar", parent);
		Rect(top.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 220), new Vector2(0.5f, 1));

		Button back = CreateIconButton(top.transform, "[Button]Back", new Vector2(64, -122), "‹", 80, true);
		CreateText("OracleTitle", top.transform, "✦  Nocturne Oracle  ✦", 36, SoftGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(540, 58), FontStyle.Normal);
		CreateLine("OracleRule", top.transform, new Vector2(0, -166), new Vector2(210, 2), new Color(Gold.r, Gold.g, Gold.b, 0.38f), new Vector2(0.5f, 1), new Vector2(0.5f, 1));
		CreateText("OracleV", top.transform, "⌄", 28, new Color(Gold.r, Gold.g, Gold.b, 0.45f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -166), new Vector2(50, 34), FontStyle.Normal);
		CreateIconButton(top.transform, "[Button]Notifications", new Vector2(-64, -122), "♢", 72, false);
		return back;
	}

	private static void BuildMainScroll(Transform parent)
	{
		GameObject scrollGo = CreateNode("MainScroll", parent);
		RectTransform scrollRectTransform = scrollGo.GetComponent<RectTransform>();
		Stretch(scrollRectTransform);
		scrollRectTransform.offsetMin = new Vector2(0, 230);
		scrollRectTransform.offsetMax = new Vector2(0, -210);

		ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
		scroll.horizontal = false;
		scroll.movementType = ScrollRect.MovementType.Clamped;
		scroll.inertia = true;

		GameObject viewport = CreatePanel("Viewport", scrollGo.transform, new Color(0, 0, 0, 0));
		Stretch(viewport.GetComponent<RectTransform>());
		Mask mask = viewport.AddComponent<Mask>();
		mask.showMaskGraphic = false;
		scroll.viewport = viewport.GetComponent<RectTransform>();

		GameObject content = CreateNode("Content", viewport.transform);
		RectTransform contentRect = content.GetComponent<RectTransform>();
		contentRect.anchorMin = new Vector2(0, 1);
		contentRect.anchorMax = new Vector2(1, 1);
		contentRect.pivot = new Vector2(0.5f, 1);
		contentRect.offsetMin = new Vector2(58, 0);
		contentRect.offsetMax = new Vector2(-58, 0);
		contentRect.anchoredPosition = Vector2.zero;
		scroll.content = contentRect;

		VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
		layout.childAlignment = TextAnchor.UpperCenter;
		layout.spacing = 28;
		layout.padding = new RectOffset(0, 0, 0, 78);
		layout.childControlWidth = true;
		layout.childControlHeight = false;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		BuildHero(content.transform);
		BuildPrivacyNotice(content.transform);
		BuildContactsHeader(content.transform);
		BuildContactsCard(content.transform);
		BuildSmsButton(content.transform);
		BuildPrivacyLine(content.transform);
	}

	private static void BuildHero(Transform parent)
	{
		GameObject hero = CreateNode("HeaderTitles", parent);
		Layout(hero, 960, 165);
		CreateText("TitleText", hero.transform, "通过通讯录添加", 54, SoftGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -45), new Vector2(720, 76), FontStyle.Bold);
		CreateText("SubtitleText", hero.transform, "邀请现实中的朋友一起使用 Nocturne Oracle", 28, TextMain, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(820, 46), FontStyle.Normal);
	}

	private static void BuildPrivacyNotice(Transform parent)
	{
		GameObject notice = CreatePanel("NotificationContainer", parent, new Color(0.075f, 0.050f, 0.120f, 0.72f));
		Layout(notice, 960, 152);
		Frame(notice.transform, new Vector2(960, 152), new Color(Gold.r, Gold.g, Gold.b, 0.50f), 1.4f);
		GameObject lockBg = CreatePanel("BadgeBg", notice.transform, new Color(0.25f, 0.10f, 0.40f, 0.72f));
		Rect(lockBg.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(78, 0), new Vector2(78, 78), Center());
		Frame(lockBg.transform, new Vector2(78, 78), new Color(Gold.r, Gold.g, Gold.b, 0.35f), 1.2f);
		CreateText("IconLock", lockBg.transform, "▣", 42, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), Vector2.zero, new Vector2(66, 66), FontStyle.Normal);
		CreateText("InstructionText", notice.transform, "我们不会自动给任何人发送消息。\n只有你点击邀请后，才会发送短信。", 29, TextMain, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(178, 0), new Vector2(-260, 96), FontStyle.Normal, new Vector2(0, 0.5f));
	}

	private static void BuildContactsHeader(Transform parent)
	{
		GameObject header = CreateNode("ContactsSectionHeader", parent);
		Layout(header, 960, 72);
		CreateText("ContactsIcon", header.transform, "☷", 44, SoftGold, TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(64, 64), FontStyle.Bold);
		CreateText("ContactsTitle", header.transform, "联系人", 36, SoftGold, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(105, 0), new Vector2(240, 58), FontStyle.Bold, new Vector2(0, 0.5f));
		CreateText("ContactsCount", header.transform, "共 68 位联系人", 27, PurpleBright, TextAnchor.MiddleRight, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(300, 52), FontStyle.Normal, new Vector2(1, 0.5f));
	}

	private static void BuildContactsCard(Transform parent)
	{
		GameObject card = CreatePanel("ContactsListCard", parent, new Color(0.026f, 0.026f, 0.058f, 0.78f));
		Layout(card, 960, 720);
		Frame(card.transform, new Vector2(960, 720), new Color(1f, 1f, 1f, 0.12f), 1.2f);

		string[] names = { "Alice", "Bob", "Cindy", "David", "Emma", "Frank" };
		string[] phones = { "138 **** 8888", "139 **** 6666", "137 **** 1234", "136 **** 4321", "138 **** 1010", "137 **** 5678" };
		Sprite[] portraits =
		{
			SpriteAt("Assets/GameData/Arts/witch-full.png"),
			SpriteAt("Assets/GameData/Arts/astrologer_oracle_portrait.png"),
			SpriteAt("Assets/GameData/Arts/meditation_oracle_portrait.png")
		};

		for (int i = 0; i < names.Length; i++)
		{
			float y = 288 - i * 112;
			BuildContactRow(card.transform, i + 1, y, names[i], phones[i], portraits[i % portraits.Length], i < names.Length - 1);
		}
	}

	private static void BuildContactRow(Transform parent, int index, float y, string contactName, string phone, Sprite portrait, bool divider)
	{
		GameObject row = CreateNode("ContactItem" + index, parent);
		Rect(row.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(900, 108), Center());

		CreateAvatar(row.transform, "AvatarImage" + index, new Vector2(-388, 0), 92, portrait);
		CreateText("NameText" + index, row.transform, contactName, 34, SoftGold, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-295, 18), new Vector2(430, 48), FontStyle.Bold, new Vector2(0, 0.5f));
		CreateText("PhoneText" + index, row.transform, phone, 28, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-295, -28), new Vector2(430, 42), FontStyle.Normal, new Vector2(0, 0.5f));
		Button invite = CreateInviteButton(row.transform, "[Button]ContactInvite" + index, new Vector2(345, 0), new Vector2(160, 70));
		invite.name = "[Button]ContactInvite" + index;

		if (divider)
		{
			CreateLine("Divider" + index, row.transform, new Vector2(26, -55), new Vector2(830, 1), new Color(1f, 1f, 1f, 0.08f));
		}
	}

	private static Button CreateInviteButton(Transform parent, string name, Vector2 pos, Vector2 size)
	{
		GameObject go = CreatePanel(name, parent, Purple);
		Rect(go.GetComponent<RectTransform>(), Center(), Center(), pos, size, Center());
		Frame(go.transform, size, new Color(Gold.r, Gold.g, Gold.b, 0.42f), 1.2f);
		Button button = go.AddComponent<Button>();
		button.targetGraphic = go.GetComponent<Image>();
		CreateText("Icon", go.transform, "✈", 36, Color.white, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-42, 0), new Vector2(48, 48), FontStyle.Normal);
		CreateText("Text", go.transform, "邀请", 29, Color.white, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(28, 0), new Vector2(82, 48), FontStyle.Normal);
		return button;
	}

	private static void BuildSmsButton(Transform parent)
	{
		GameObject go = CreatePanel("[Button]SmsInvite", parent, Purple);
		Layout(go, 960, 92);
		Frame(go.transform, new Vector2(960, 92), new Color(Gold.r, Gold.g, Gold.b, 0.40f), 1.2f);
		Button button = go.AddComponent<Button>();
		button.targetGraphic = go.GetComponent<Image>();
		CreateText("SmsIcon", go.transform, "☏", 42, Color.white, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-112, 0), new Vector2(70, 70), FontStyle.Normal);
		CreateText("SmsText", go.transform, "一键短信邀请", 32, Color.white, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(52, 0), new Vector2(360, 64), FontStyle.Normal);
	}

	private static void BuildPrivacyLine(Transform parent)
	{
		GameObject line = CreateNode("PrivacyLine", parent);
		Layout(line, 960, 48);
		CreateText("Shield", line.transform, "♢", 28, SoftGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-325, 0), new Vector2(40, 40), FontStyle.Bold);
		CreateText("Text", line.transform, "你的通讯录信息将仅用于邀请，不会泄露给任何人。", 25, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(30, 0), new Vector2(690, 42), FontStyle.Normal, new Vector2(0.5f, 0.5f));
	}

	private static void BuildBottomBar(Transform parent)
	{
		GameObject nav = CreatePanel("BottomBar", parent, new Color(0.030f, 0.032f, 0.055f, 0.94f));
		Rect(nav.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 48), new Vector2(0, 182), new Vector2(0.5f, 0));
		CreateLine("TopBorder", nav.transform, new Vector2(0, 91), new Vector2(1080, 1.2f), new Color(1f, 1f, 1f, 0.10f));
		CreateNavItem(nav.transform, -360, "⌂", "今日神谕", false);
		CreateNavItem(nav.transform, -120, "☏", "对话", false);
		CreateNavItem(nav.transform, 120, "♊", "朋友", true);
		CreateNavItem(nav.transform, 360, "♙", "我的", false);
		CreateLine("HomeIndicator", nav.transform, new Vector2(0, -72), new Vector2(310, 7), Color.white);
	}

	private static void CreateNavItem(Transform parent, float x, string icon, string label, bool selected)
	{
		Color color = selected ? Color.white : new Color(0.72f, 0.61f, 0.54f, 1f);
		if (selected)
		{
			GameObject halo = CreatePanel("SelectedHalo", parent, new Color(PurpleBright.r, PurpleBright.g, PurpleBright.b, 0.26f));
			Rect(halo.GetComponent<RectTransform>(), Center(), Center(), new Vector2(x, 38), new Vector2(96, 96), Center());
		}
		CreateText("Icon_" + label, parent, icon, 46, color, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(x, 42), new Vector2(92, 58), FontStyle.Bold);
		CreateText("Label_" + label, parent, label, 25, color, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(x, -22), new Vector2(150, 38), FontStyle.Normal);
	}

	private static Button CreateIconButton(Transform parent, string name, Vector2 pos, string icon, float size, bool leftAnchor)
	{
		Vector2 anchor = leftAnchor ? new Vector2(0, 1) : new Vector2(1, 1);
		GameObject go = CreatePanel(name, parent, new Color(0, 0, 0, 0));
		Rect(go.GetComponent<RectTransform>(), anchor, anchor, pos, new Vector2(size, size), Center());
		Button button = go.AddComponent<Button>();
		button.targetGraphic = go.GetComponent<Image>();
		CreateText("Icon", go.transform, icon, Mathf.RoundToInt(size * 0.72f), SoftGold, TextAnchor.MiddleCenter, Center(), Center(), Vector2.zero, new Vector2(size, size), FontStyle.Normal);
		return button;
	}

	private static void CreateAvatar(Transform parent, string name, Vector2 pos, float size, Sprite sprite)
	{
		GameObject frame = CreatePanel(name + "Frame", parent, new Color(0.018f, 0.014f, 0.030f, 0.90f));
		Rect(frame.GetComponent<RectTransform>(), Center(), Center(), pos, new Vector2(size, size), Center());
		Frame(frame.transform, new Vector2(size, size), SoftGold, 1.6f);
		Image image = CreateImage(name, frame.transform, sprite, Color.white, Vector2.zero, new Vector2(size - 10, size - 10), Center(), Center());
		image.preserveAspect = true;
	}

	private static GameObject CreateNode(string name, Transform parent)
	{
		GameObject go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false);
		go.GetComponent<RectTransform>().localScale = Vector3.one;
		return go;
	}

	private static GameObject CreatePanel(string name, Transform parent, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		go.transform.SetParent(parent, false);
		Image image = go.GetComponent<Image>();
		image.color = color;
		image.raycastTarget = color.a > 0.001f;
		return go;
	}

	private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		go.transform.SetParent(parent, false);
		Image image = go.GetComponent<Image>();
		image.sprite = sprite;
		image.color = color;
		Rect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, pos, size, Center());
		return image;
	}

	private static Text CreateText(string name, Transform parent, string value, int fontSize, Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, FontStyle style, Vector2? pivot = null)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
		go.transform.SetParent(parent, false);
		Rect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, anchoredPosition, size, pivot ?? Center());
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
		shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
		shadow.effectDistance = new Vector2(0, -1);
		return text;
	}

	private static void Frame(Transform parent, Vector2 size, Color color, float thickness)
	{
		CreateLine("TopBorder", parent, new Vector2(0, size.y * 0.5f), new Vector2(size.x, thickness), color);
		CreateLine("BottomBorder", parent, new Vector2(0, -size.y * 0.5f), new Vector2(size.x, thickness), color);
		CreateLine("LeftBorder", parent, new Vector2(-size.x * 0.5f, 0), new Vector2(thickness, size.y), color);
		CreateLine("RightBorder", parent, new Vector2(size.x * 0.5f, 0), new Vector2(thickness, size.y), color);
	}

	private static void CreateLine(string name, Transform parent, Vector2 position, Vector2 size, Color color)
	{
		CreateLine(name, parent, position, size, color, Center(), Center());
	}

	private static void CreateLine(string name, Transform parent, Vector2 position, Vector2 size, Color color, Vector2 anchorMin, Vector2 anchorMax)
	{
		Image line = CreateImage(name, parent, null, color, position, size, anchorMin, anchorMax);
		line.raycastTarget = false;
	}

	private static void Layout(GameObject go, float minWidth, float preferredHeight)
	{
		LayoutElement element = go.AddComponent<LayoutElement>();
		element.minWidth = minWidth;
		element.preferredWidth = minWidth;
		element.preferredHeight = preferredHeight;
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		rect.pivot = Center();
		rect.localScale = Vector3.one;
	}

	private static void Rect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
	{
		rect.anchorMin = anchorMin;
		rect.anchorMax = anchorMax;
		rect.pivot = pivot;
		rect.localScale = Vector3.one;
		rect.anchoredPosition = anchoredPosition;
		rect.sizeDelta = size;
	}

	private static Button FindButton(Transform root, string objectName)
	{
		Transform child = FindDeepChild(root, objectName);
		return child != null ? child.GetComponent<Button>() : null;
	}

	private static Transform FindDeepChild(Transform root, string objectName)
	{
		if (root.name == objectName) return root;
		foreach (Transform child in root)
		{
			Transform found = FindDeepChild(child, objectName);
			if (found != null) return found;
		}
		return null;
	}

	private static Vector2 Center()
	{
		return new Vector2(0.5f, 0.5f);
	}

	private static Sprite SpriteAt(string path)
	{
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
	}

	private static void SetLayerRecursive(GameObject go, int layer)
	{
		if (layer >= 0) go.layer = layer;
		foreach (Transform child in go.transform)
		{
			SetLayerRecursive(child.gameObject, layer);
		}
	}
}
