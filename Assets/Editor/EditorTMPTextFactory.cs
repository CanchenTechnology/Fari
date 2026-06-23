using TMPro;
using UnityEditor;
using UnityEngine;

public static class EditorTMPTextFactory
{
	public static TMP_FontAsset GetUIFont()
	{
		return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/GameData/Arts/Fonts/TMP/Regular SDF.asset")
			?? TMP_Settings.defaultFontAsset;
	}

	public static FontStyles ToFontStyle(FontStyle style)
	{
		switch (style)
		{
			case FontStyle.Bold:
				return FontStyles.Bold;
			case FontStyle.Italic:
				return FontStyles.Italic;
			case FontStyle.BoldAndItalic:
				return FontStyles.Bold | FontStyles.Italic;
			default:
				return FontStyles.Normal;
		}
	}

	public static TextAlignmentOptions ToAlignment(TextAnchor alignment)
	{
		switch (alignment)
		{
			case TextAnchor.UpperLeft:
				return TextAlignmentOptions.TopLeft;
			case TextAnchor.UpperCenter:
				return TextAlignmentOptions.Top;
			case TextAnchor.UpperRight:
				return TextAlignmentOptions.TopRight;
			case TextAnchor.MiddleLeft:
				return TextAlignmentOptions.Left;
			case TextAnchor.MiddleCenter:
				return TextAlignmentOptions.Center;
			case TextAnchor.MiddleRight:
				return TextAlignmentOptions.Right;
			case TextAnchor.LowerLeft:
				return TextAlignmentOptions.BottomLeft;
			case TextAnchor.LowerCenter:
				return TextAlignmentOptions.Bottom;
			case TextAnchor.LowerRight:
				return TextAlignmentOptions.BottomRight;
			default:
				return TextAlignmentOptions.Center;
		}
	}
}
