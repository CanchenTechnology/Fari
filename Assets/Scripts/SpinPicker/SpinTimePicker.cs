using System;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpinTimePicker : MonoBehaviour
{
	public LoopListView2 mLoopListViewHour;
	public LoopListView2 mLoopListViewMinute;
	public Color mColorReserved = new Color(0.63f, 0.60f, 0.66f, 1f);
	public Color mColorSelected = new Color(1f, 0.84f, 0.48f, 1f);
	public TMP_Text CurSelect;
	public Button ConfirmButton;

	[SerializeField] private int mFirstHour = 0;
	[SerializeField] private int mHourCount = 24;
	[SerializeField] private int mFirstMinute = 0;
	[SerializeField] private int mMinuteCount = 60;

	private int mCurSelectedHour = 12;
	private int mCurSelectedMinute;
	private bool mInitialized;
	private Action<string> mConfirmCallback;

	public int CurSelectedHour => mCurSelectedHour;
	public int CurSelectedMinute => mCurSelectedMinute;
	public string SelectedValue => string.Format("{0:D2}:{1:D2}", CurSelectedHour, CurSelectedMinute);

	public void Show(string initialValue, Action<string> confirmCallback)
	{
		gameObject.SetActive(true);
		BindReferences();
		ParseInitialTime(initialValue, out mCurSelectedHour, out mCurSelectedMinute);
		mConfirmCallback = confirmCallback;

		EnsureInitialized();
		MoveToCurrentTime();
		UpdateCurSelect();
	}

	private void Awake()
	{
		BindReferences();
	}

	private void BindReferences()
	{
		if (mLoopListViewHour == null)
		{
			Transform item = transform.Find("ScrollViewHour");
			if (item != null) mLoopListViewHour = item.GetComponent<LoopListView2>();
		}
		if (mLoopListViewMinute == null)
		{
			Transform item = transform.Find("ScrollVievMinute");
			if (item == null) item = transform.Find("ScrollViewMinute");
			if (item != null) mLoopListViewMinute = item.GetComponent<LoopListView2>();
		}
		if (CurSelect == null)
		{
			Transform item = transform.Find("CurSelect");
			if (item != null) CurSelect = item.GetComponent<TMP_Text>();
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
		if (mLoopListViewHour == null || mLoopListViewMinute == null)
		{
			Debug.LogError("[SpinTimePicker] LoopListView2 reference is missing.");
			return;
		}

		mLoopListViewHour.mOnSnapNearestChanged = OnHourSnapTargetChanged;
		mLoopListViewMinute.mOnSnapNearestChanged = OnMinuteSnapTargetChanged;

		mLoopListViewHour.InitListView(-1, OnGetItemByIndexForHour);
		mLoopListViewMinute.InitListView(-1, OnGetItemByIndexForMinute);

		if (ConfirmButton != null)
		{
			ConfirmButton.onClick.RemoveListener(OnConfirmButtonClicked);
			ConfirmButton.onClick.AddListener(OnConfirmButtonClicked);
		}

		mInitialized = true;
	}

	private void ParseInitialTime(string value, out int hour, out int minute)
	{
		hour = 12;
		minute = 0;
		if (string.IsNullOrWhiteSpace(value)) return;

		string[] parts = value.Trim().Split(':');
		if (parts.Length < 2) return;
		if (int.TryParse(parts[0], out int parsedHour))
		{
			hour = Mathf.Clamp(parsedHour, 0, 23);
		}
		if (int.TryParse(parts[1], out int parsedMinute))
		{
			minute = Mathf.Clamp(parsedMinute, 0, 59);
		}
	}

	private void MoveToCurrentTime()
	{
		MoveListToValue(mLoopListViewHour, mCurSelectedHour, mFirstHour);
		MoveListToValue(mLoopListViewMinute, mCurSelectedMinute, mFirstMinute);
	}

	private void MoveListToValue(LoopListView2 listView, int value, int firstValue)
	{
		if (listView == null) return;
		listView.MovePanelToItemIndex(value - firstValue, 0);
		listView.FinishSnapImmediately();
	}

	private void UpdateCurSelect()
	{
		if (CurSelect != null)
		{
			CurSelect.text = SelectedValue;
		}
	}

	private LoopListViewItem2 OnGetItemByIndexForHour(LoopListView2 listView, int index)
	{
		return CreateNumberItem(listView, index, mFirstHour, mHourCount, "时");
	}

	private LoopListViewItem2 OnGetItemByIndexForMinute(LoopListView2 listView, int index)
	{
		return CreateNumberItem(listView, index, mFirstMinute, mMinuteCount, "分");
	}

	private LoopListViewItem2 CreateNumberItem(LoopListView2 listView, int index, int firstValue, int count, string suffix)
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
		itemScript.mText.text = string.Format("{0:D2}{1}", value, suffix);
		return item;
	}

	private int NormalizeIndex(int index, int count)
	{
		if (count <= 0) return 0;
		if (index >= 0) return index % count;
		return count + ((index + 1) % count) - 1;
	}

	private void OnHourSnapTargetChanged(LoopListView2 listView, LoopListViewItem2 item)
	{
		if (!TryUpdateSelectedValue(listView, item, out int value)) return;
		mCurSelectedHour = value;
		UpdateCurSelect();
	}

	private void OnMinuteSnapTargetChanged(LoopListView2 listView, LoopListViewItem2 item)
	{
		if (!TryUpdateSelectedValue(listView, item, out int value)) return;
		mCurSelectedMinute = value;
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
