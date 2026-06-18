using System;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;

public class SpinDatePicker : MonoBehaviour
{
	public LoopListView2 mLoopListViewYear;
	public LoopListView2 mLoopListViewMonth;
	public LoopListView2 mLoopListViewDay;
	public Color mColorReserved = new Color(0.63f, 0.60f, 0.66f, 1f);
	public Color mColorSelected = new Color(1f, 0.84f, 0.48f, 1f);
	public Text CurSelect;
	public Button ConfirmButton;

	[SerializeField] private int mFirstYear = 1900;
	[SerializeField] private int mYearCount = 201;
	[SerializeField] private int mFirstMonth = 1;
	[SerializeField] private int mMonthCount = 12;
	[SerializeField] private int mFirstDay = 1;

	private int mCurSelectedYear = 2000;
	private int mCurSelectedMonth = 1;
	private int mCurSelectedDay = 1;
	private bool mInitialized;
	private Action<string> mConfirmCallback;

	public int CurSelectedYear => mCurSelectedYear;
	public int CurSelectedMonth => mCurSelectedMonth;
	public int CurSelectedDay => mCurSelectedDay;
	public string SelectedValue => string.Format("{0:D4}-{1:D2}-{2:D2}", CurSelectedYear, CurSelectedMonth, CurSelectedDay);

	public void Show(string initialValue, Action<string> confirmCallback)
	{
		gameObject.SetActive(true);
		BindReferences();
		DateTime initialDate = ParseInitialDate(initialValue);
		mCurSelectedYear = Mathf.Clamp(initialDate.Year, mFirstYear, mFirstYear + mYearCount - 1);
		mCurSelectedMonth = Mathf.Clamp(initialDate.Month, 1, 12);
		mCurSelectedDay = Mathf.Clamp(initialDate.Day, 1, DateTime.DaysInMonth(mCurSelectedYear, mCurSelectedMonth));
		mConfirmCallback = confirmCallback;

		EnsureInitialized();
		MoveToCurrentDate();
		UpdateCurSelect();
	}

	private void Awake()
	{
		BindReferences();
	}

	private void BindReferences()
	{
		if (mLoopListViewYear == null)
		{
			Transform item = transform.Find("ScrollViewYear");
			if (item != null) mLoopListViewYear = item.GetComponent<LoopListView2>();
		}
		if (mLoopListViewMonth == null)
		{
			Transform item = transform.Find("ScrollViewMonth");
			if (item != null) mLoopListViewMonth = item.GetComponent<LoopListView2>();
		}
		if (mLoopListViewDay == null)
		{
			Transform item = transform.Find("ScrollViewDay");
			if (item != null) mLoopListViewDay = item.GetComponent<LoopListView2>();
		}
		if (CurSelect == null)
		{
			Transform item = transform.Find("CurSelect");
			if (item != null) CurSelect = item.GetComponent<Text>();
		}
		if (ConfirmButton == null)
		{
			Transform item = transform.Find("confirmBtn");
			if (item == null) item = transform.Find("[Button]Confirm");
			if (item != null) ConfirmButton = item.GetComponent<Button>();
		}
	}

	private void EnsureInitialized()
	{
		if (mInitialized) return;
		if (mLoopListViewYear == null || mLoopListViewMonth == null || mLoopListViewDay == null)
		{
			Debug.LogError("[SpinDatePicker] LoopListView2 reference is missing.");
			return;
		}

		mLoopListViewYear.mOnSnapNearestChanged = OnYearSnapTargetChanged;
		mLoopListViewMonth.mOnSnapNearestChanged = OnMonthSnapTargetChanged;
		mLoopListViewDay.mOnSnapNearestChanged = OnDaySnapTargetChanged;
		mLoopListViewYear.mOnSnapItemFinished = OnYearSnapTargetFinished;
		mLoopListViewMonth.mOnSnapItemFinished = OnMonthSnapTargetFinished;

		mLoopListViewYear.InitListView(-1, OnGetItemByIndexForYear);
		mLoopListViewMonth.InitListView(-1, OnGetItemByIndexForMonth);
		mLoopListViewDay.InitListView(-1, OnGetItemByIndexForDay);

		if (ConfirmButton != null)
		{
			ConfirmButton.onClick.RemoveListener(OnConfirmButtonClicked);
			ConfirmButton.onClick.AddListener(OnConfirmButtonClicked);
		}

		mInitialized = true;
	}

	private DateTime ParseInitialDate(string value)
	{
		if (!string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, out DateTime parsed))
		{
			return parsed;
		}
		return new DateTime(2000, 1, 1);
	}

	private void MoveToCurrentDate()
	{
		MoveListToValue(mLoopListViewYear, mCurSelectedYear, mFirstYear);
		MoveListToValue(mLoopListViewMonth, mCurSelectedMonth, mFirstMonth);
		MoveListToValue(mLoopListViewDay, mCurSelectedDay, mFirstDay);
	}

	private void MoveListToValue(LoopListView2 listView, int value, int firstValue)
	{
		if (listView == null) return;
		listView.MovePanelToItemIndex(value - firstValue - 1, 0);
		listView.FinishSnapImmediately();
	}

	private void UpdateCurSelect()
	{
		int daysInMonth = DateTime.DaysInMonth(CurSelectedYear, CurSelectedMonth);
		if (mCurSelectedDay > daysInMonth)
		{
			mCurSelectedDay = daysInMonth;
		}

		if (CurSelect != null)
		{
			CurSelect.text = string.Format("{0:D4}年{1:D2}月{2:D2}日", CurSelectedYear, CurSelectedMonth, CurSelectedDay);
		}
	}

	private LoopListViewItem2 OnGetItemByIndexForYear(LoopListView2 listView, int index)
	{
		return CreateNumberItem(listView, index, mFirstYear, mYearCount, "D4", "年");
	}

	private LoopListViewItem2 OnGetItemByIndexForMonth(LoopListView2 listView, int index)
	{
		return CreateNumberItem(listView, index, mFirstMonth, mMonthCount, "D2", "月");
	}

	private LoopListViewItem2 OnGetItemByIndexForDay(LoopListView2 listView, int index)
	{
		int dayCount = DateTime.DaysInMonth(CurSelectedYear, CurSelectedMonth);
		return CreateNumberItem(listView, index, mFirstDay, dayCount, "D2", "日");
	}

	private LoopListViewItem2 CreateNumberItem(LoopListView2 listView, int index, int firstValue, int count, string format, string suffix)
	{
		LoopListViewItem2 item = listView.NewListViewItem("ItemPrefab");
		if (item == null) return null;
		SpinPickerItem itemScript = item.GetComponent<SpinPickerItem>();
		if (item.IsInitHandlerCalled == false)
		{
			item.IsInitHandlerCalled = true;
			itemScript.Init();
		}

		int value = firstValue + NormalizeIndex(index, count);
		itemScript.Value = value;
		itemScript.mText.text = string.Format("{0:" + format + "}{1}", value, suffix);
		return item;
	}

	private int NormalizeIndex(int index, int count)
	{
		if (count <= 0) return 0;
		if (index >= 0) return index % count;
		return count + ((index + 1) % count) - 1;
	}

	private void OnYearSnapTargetChanged(LoopListView2 listView, LoopListViewItem2 item)
	{
		if (!TryUpdateSelectedValue(listView, item, out int value)) return;
		mCurSelectedYear = value;
		UpdateCurSelect();
	}

	private void OnMonthSnapTargetChanged(LoopListView2 listView, LoopListViewItem2 item)
	{
		if (!TryUpdateSelectedValue(listView, item, out int value)) return;
		mCurSelectedMonth = value;
		UpdateCurSelect();
	}

	private void OnDaySnapTargetChanged(LoopListView2 listView, LoopListViewItem2 item)
	{
		if (!TryUpdateSelectedValue(listView, item, out int value)) return;
		mCurSelectedDay = value;
		UpdateCurSelect();
	}

	private bool TryUpdateSelectedValue(LoopListView2 listView, LoopListViewItem2 item, out int value)
	{
		value = 0;
		int index = listView.GetIndexInShownItemList(item);
		if (index < 0) return false;
		SpinPickerItem itemScript = item.GetComponent<SpinPickerItem>();
		if (itemScript == null) return false;
		value = itemScript.Value;
		OnListViewSnapTargetChanged(listView, index);
		return true;
	}

	private void OnYearSnapTargetFinished(LoopListView2 listView, LoopListViewItem2 item)
	{
		RefreshDayColumn();
	}

	private void OnMonthSnapTargetFinished(LoopListView2 listView, LoopListViewItem2 item)
	{
		RefreshDayColumn();
	}

	private void RefreshDayColumn()
	{
		if (mLoopListViewDay == null || !mLoopListViewDay.ListViewInited) return;
		mCurSelectedDay = Mathf.Clamp(mCurSelectedDay, 1, DateTime.DaysInMonth(CurSelectedYear, CurSelectedMonth));
		mLoopListViewDay.RefreshAllShownItem();
		MoveListToValue(mLoopListViewDay, mCurSelectedDay, mFirstDay);
		UpdateCurSelect();
	}

	private void OnListViewSnapTargetChanged(LoopListView2 listView, int targetIndex)
	{
		int count = listView.ShownItemCount;
		for (int i = 0; i < count; ++i)
		{
			LoopListViewItem2 item = listView.GetShownItemByIndex(i);
			SpinPickerItem itemScript = item != null ? item.GetComponent<SpinPickerItem>() : null;
			if (itemScript != null && itemScript.mText != null)
			{
				itemScript.mText.color = i == targetIndex ? mColorSelected : mColorReserved;
			}
		}
	}

	private void OnConfirmButtonClicked()
	{
		mConfirmCallback?.Invoke(SelectedValue);
	}
}
