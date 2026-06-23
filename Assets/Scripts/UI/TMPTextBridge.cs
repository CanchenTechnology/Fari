using TMPro;
using UnityEngine;

public static class TMPTextBridge
{
	public static TextAlignmentOptions ToAlignment(TextAnchor anchor)
	{
		switch (anchor)
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
