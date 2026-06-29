using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TarotCardDescriptionPrefabBuilder
{
	private const string PrefabPath = "Assets/GameData/Prefabs/TarotCardDescriptionList.prefab";

	private static readonly Color Panel = new Color(0.105f, 0.112f, 0.128f, 1f);
	private static readonly Color RowText = new Color(0.70f, 0.70f, 0.74f, 1f);
	private static readonly Color Title = new Color(1f, 0.55f, 0.33f, 1f);
	private static readonly Color Line = new Color(1f, 0.55f, 0.33f, 0.34f);

	[InitializeOnLoadMethod]
	private static void BuildOnceWhenMissing()
	{
		if (File.Exists(PrefabPath))
		{
			return;
		}

		EditorApplication.delayCall += Build;
	}

	[MenuItem("Tools/UI/Build Tarot Card Description List Prefab")]
	public static void Build()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

		GameObject root = CreatePanel("TarotCardDescriptionList", null, Panel);
		RectTransform rootRect = root.GetComponent<RectTransform>();
		rootRect.anchorMin = new Vector2(0.5f, 0.5f);
		rootRect.anchorMax = new Vector2(0.5f, 0.5f);
		rootRect.pivot = new Vector2(0.5f, 0.5f);
		rootRect.sizeDelta = new Vector2(980f, 620f);

		LayoutElement rootLayout = root.AddComponent<LayoutElement>();
		rootLayout.preferredWidth = 980f;
		rootLayout.minHeight = 300f;

		VerticalLayoutGroup rootGroup = root.AddComponent<VerticalLayoutGroup>();
		rootGroup.padding = new RectOffset(52, 52, 52, 52);
		rootGroup.spacing = 42f;
		rootGroup.childAlignment = TextAnchor.UpperCenter;
		rootGroup.childControlWidth = true;
		rootGroup.childControlHeight = true;
		rootGroup.childForceExpandWidth = true;
		rootGroup.childForceExpandHeight = false;

		ContentSizeFitter rootFitter = root.AddComponent<ContentSizeFitter>();
		rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		Outline outline = root.AddComponent<Outline>();
		outline.effectColor = new Color(0f, 0f, 0f, 0.34f);
		outline.effectDistance = new Vector2(4f, -4f);

		BuildItem(
			root.transform,
			"Card",
			"Assets/GameData/Arts/Sprites/MajorArcana/RWS_Tarot_02_High_Priestess.jpg",
			"Priestess",
			"The High Priestess usually represents inner wisdom, intuition, and insights from the subconscious.");

		BuildItem(
			root.transform,
			"Card_1",
			"Assets/GameData/Arts/Sprites/MajorArcana/RWS_Tarot_21_World.jpg",
			"The World",
			"The World card symbolizes completion, wholeness, and new beginnings.");

		BuildItem(
			root.transform,
			"Card_2",
			"Assets/GameData/Arts/Sprites/MajorArcana/RWS_Tarot_04_Emperor.jpg",
			"The King",
			"The king symbolizes maturity, authority, leadership, and the ability to take control.");

		SetLayerRecursive(root, LayerMask.NameToLayer("UI"));
		PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
		Object.DestroyImmediate(root);

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("Generated tarot card description prefab: " + PrefabPath);
	}

	private static void BuildItem(Transform parent, string name, string spritePath, string title, string description)
	{
		GameObject item = CreateNode(name, parent);
		LayoutElement itemLayout = item.AddComponent<LayoutElement>();
		itemLayout.preferredWidth = 876f;
		itemLayout.minHeight = 174f;

		HorizontalLayoutGroup row = item.AddComponent<HorizontalLayoutGroup>();
		row.padding = new RectOffset(0, 0, 0, 0);
		row.spacing = 40f;
		row.childAlignment = TextAnchor.UpperLeft;
		row.childControlWidth = true;
		row.childControlHeight = true;
		row.childForceExpandWidth = false;
		row.childForceExpandHeight = false;

		ContentSizeFitter itemFitter = item.AddComponent<ContentSizeFitter>();
		itemFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		DivinationInfoItem binding = item.AddComponent<DivinationInfoItem>();

		GameObject thumb = CreateNode("CardThumbnail", item.transform);
		LayoutElement thumbLayout = thumb.AddComponent<LayoutElement>();
		thumbLayout.preferredWidth = 128f;
		thumbLayout.preferredHeight = 180f;
		thumbLayout.minWidth = 128f;
		thumbLayout.minHeight = 180f;
		RectTransform thumbRect = thumb.GetComponent<RectTransform>();
		thumbRect.sizeDelta = new Vector2(128f, 180f);

		Image bg = CreateImage("CardBackground", thumb.transform, SpriteAt("Assets/GameData/Arts/UI/Card/icon_card_bg.png"), Color.white);
		Stretch(bg.rectTransform);

		Image art = CreateImage("iconImage", thumb.transform, SpriteAt(spritePath), Color.white);
		Stretch(art.rectTransform, 11f, 13f, 11f, 13f);
		art.preserveAspect = false;

		Image frame = CreateImage("CardFrame", thumb.transform, SpriteAt("Assets/GameData/Arts/UI/Card/cardframe.png"), Color.white);
		Stretch(frame.rectTransform);
		frame.raycastTarget = false;

		GameObject textColumn = CreateNode("TextColumn", item.transform);
		LayoutElement textLayout = textColumn.AddComponent<LayoutElement>();
		textLayout.preferredWidth = 708f;
		textLayout.flexibleWidth = 1f;
		textLayout.minHeight = 174f;

		VerticalLayoutGroup textGroup = textColumn.AddComponent<VerticalLayoutGroup>();
		textGroup.padding = new RectOffset(0, 0, 0, 0);
		textGroup.spacing = 18f;
		textGroup.childAlignment = TextAnchor.UpperLeft;
		textGroup.childControlWidth = true;
		textGroup.childControlHeight = true;
		textGroup.childForceExpandWidth = true;
		textGroup.childForceExpandHeight = false;

		ContentSizeFitter columnFitter = textColumn.AddComponent<ContentSizeFitter>();
		columnFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		TMP_Text titleText = CreateText("cardName", textColumn.transform, title, 42f, Title, FontStyle.Bold);
		AddTextShadow(titleText, new Color(0.10f, 0.04f, 0.03f, 0.88f), new Vector2(2f, -2f));

		TMP_Text descText = CreateText("cardDes", textColumn.transform, description, 31f, RowText, FontStyle.Bold);
		descText.lineSpacing = -4f;
		AddTextShadow(descText, new Color(0f, 0f, 0f, 0.55f), new Vector2(2f, -2f));

		GameObject divider = CreatePanel("Divider", item.transform, Line);
		divider.SetActive(false);

		binding.iconImage = art;
		binding.cardName = titleText;
		binding.cardDesText = descText;
	}

	private static GameObject CreateNode(string name, Transform parent)
	{
		GameObject go = new GameObject(name, typeof(RectTransform));
		if (parent != null)
		{
			go.transform.SetParent(parent, false);
		}

		return go;
	}

	private static GameObject CreatePanel(string name, Transform parent, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		if (parent != null)
		{
			go.transform.SetParent(parent, false);
		}

		Image image = go.GetComponent<Image>();
		image.color = color;
		image.raycastTarget = false;
		return go;
	}

	private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		go.transform.SetParent(parent, false);

		Image image = go.GetComponent<Image>();
		image.sprite = sprite;
		image.color = color;
		image.raycastTarget = false;
		return image;
	}

	private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color, FontStyle style)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
		go.transform.SetParent(parent, false);

		TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
		tmp.text = text;
		tmp.font = style == FontStyle.Bold ? TitleFont() : BodyFont();
		tmp.fontSize = size;
		tmp.fontStyle = EditorTMPTextFactory.ToFontStyle(style);
		tmp.color = color;
		tmp.alignment = TextAlignmentOptions.TopLeft;
		tmp.enableWordWrapping = true;
		tmp.overflowMode = TextOverflowModes.Overflow;
		tmp.raycastTarget = false;

		RectTransform rect = tmp.rectTransform;
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.sizeDelta = new Vector2(0f, size + 16f);

		return tmp;
	}

	private static void AddTextShadow(TMP_Text text, Color color, Vector2 distance)
	{
		Shadow shadow = text.gameObject.AddComponent<Shadow>();
		shadow.effectColor = color;
		shadow.effectDistance = distance;
		shadow.useGraphicAlpha = true;
	}

	private static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.offsetMin = new Vector2(left, bottom);
		rect.offsetMax = new Vector2(-right, -top);
	}

	private static Sprite SpriteAt(string path)
	{
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
	}

	private static TMP_FontAsset BodyFont()
	{
		return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GameData/Arts/Fonts/TMP/Regular SDF.asset")
			?? TMP_Settings.defaultFontAsset;
	}

	private static TMP_FontAsset TitleFont()
	{
		return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GameData/Arts/Fonts/TMP/Bluu Next Cyrillic 2 SDF.asset")
			?? BodyFont();
	}

	private static void SetLayerRecursive(GameObject go, int layer)
	{
		if (layer >= 0)
		{
			go.layer = layer;
		}

		for (int i = 0; i < go.transform.childCount; i++)
		{
			SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
		}
	}
}
