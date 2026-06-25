using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;

public static class SpinPickerWheelUtility
{
	public const int VisibleRowCount = 5;
	public const float RowHeight = 72f;

	private const float ReservedFontScale = 0.82f;
	private const float MinAlpha = 0.55f;

	public static LoopListViewInitParam CreateInitParam(LoopListView2 listView)
	{
		float itemSizeWithPadding = GetItemSizeWithPadding(listView);
		LoopListViewInitParam initParam = LoopListViewInitParam.CopyDefaultInitParam();
		initParam.mItemDefaultWithPaddingSize = itemSizeWithPadding;
		initParam.mDistanceForNew0 = itemSizeWithPadding * 3f;
		initParam.mDistanceForNew1 = itemSizeWithPadding * 3f;
		initParam.mDistanceForRecycle0 = itemSizeWithPadding * 5f;
		initParam.mDistanceForRecycle1 = itemSizeWithPadding * 5f;
		initParam.mSmoothDumpRate = 0.18f;
		initParam.mSnapFinishThreshold = 0.02f;
		initParam.mSnapVecThreshold = 120f;
		return initParam;
	}

	public static void ConfigureWheel(LoopListView2 listView)
	{
		if (listView == null) return;

		listView.ItemSnapEnable = true;
		listView.SnapMoveDefaultMaxAbsVec = 2600f;

		ScrollRect scrollRect = listView.GetComponent<ScrollRect>();
		if (scrollRect != null)
		{
			float viewportHeight = GetVisibleViewportHeight(listView);
			scrollRect.decelerationRate = 0.12f;
			scrollRect.scrollSensitivity = 1.2f;
			SetHeight(scrollRect.GetComponent<RectTransform>(), viewportHeight);
			if (scrollRect.viewport != null)
			{
				SetHeight(scrollRect.viewport, viewportHeight);
			}
		}

		if (listView.ItemPrefabDataList == null || listView.ItemPrefabDataList.Count <= 0) return;
		GameObject itemPrefab = listView.ItemPrefabDataList[0].mItemPrefab;
		if (itemPrefab == null) return;

		RectTransform itemRect = itemPrefab.GetComponent<RectTransform>();
		SetHeight(itemRect, RowHeight);
	}

	public static void RefreshWheelVisuals(LoopListView2 listView, Color reservedColor, Color selectedColor)
	{
		if (listView == null || !listView.ListViewInited) return;

		listView.UpdateAllShownItemSnapData();
		for (int i = 0; i < listView.ShownItemCount; i++)
		{
			LoopListViewItem2 item = listView.GetShownItemByIndex(i);
			if (item == null) continue;

			SpinPickerItem itemScript = item.GetComponent<SpinPickerItem>();
			if (itemScript == null) continue;

			float itemSizeWithPadding = GetItemSizeWithPadding(listView);
			float distance = Mathf.Abs(item.DistanceWithViewPortSnapCenter);
			float normalizedDistance = Mathf.Clamp01(distance / (itemSizeWithPadding * 2f));
			bool selected = distance <= itemSizeWithPadding * 0.5f;
			itemScript.ApplyWheelVisual(normalizedDistance, selected, reservedColor, selectedColor, ReservedFontScale, MinAlpha);
		}
	}

	private static float GetVisibleViewportHeight(LoopListView2 listView)
	{
		float padding = GetItemPadding(listView);
		return RowHeight * VisibleRowCount + padding * (VisibleRowCount - 1);
	}

	private static float GetItemSizeWithPadding(LoopListView2 listView)
	{
		return RowHeight + GetItemPadding(listView);
	}

	private static float GetItemPadding(LoopListView2 listView)
	{
		if (listView == null || listView.ItemPrefabDataList == null || listView.ItemPrefabDataList.Count <= 0)
		{
			return 0f;
		}

		return Mathf.Max(0f, listView.ItemPrefabDataList[0].mPadding);
	}

	private static void SetHeight(RectTransform rectTransform, float height)
	{
		if (rectTransform == null) return;
		rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
	}
}
