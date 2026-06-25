using System;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GamerFrameWork.UIFrameWork;

public class SpinTimePicker : MonoBehaviour
{
	public LoopListView2 mLoopListViewHour;
	public LoopListView2 mLoopListViewMinute;
	public Color mColorReserved = new Color(0.63f, 0.60f, 0.66f, 1f);
	public Color mColorSelected = new Color(1f, 0.84f, 0.48f, 1f);
	public TMP_Text CurSelect;
	public Button ConfirmButton;
	public Button CancelButton;
	public Image onImage;

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
		RefreshAllWheelVisuals();
	}

	private void Awake()
	{
		BindReferences();
	}

	private void Update()
	{
		if (!mInitialized || !gameObject.activeInHierarchy) return;
		RefreshAllWheelVisuals();
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
		if (CancelButton == null)
		{
			Transform item = transform.Find("cancelBtn");
			if (item == null) item = transform.Find("[Button]Cancel");
			if (item == null) item = transform.Find("CancelButton");
			if (item != null) CancelButton = item.GetComponent<Button>();
		}
		if (onImage == null)
		{
			Transform item = transform.Find("On");
			if (item == null) item = transform.Find("onImage");
			if (item == null) item = transform.Find("OnImage");
			if (item != null) onImage = item.GetComponent<Image>();
		}
	}

	private void EnsureInitialized()
	{
		if (mInitialized)
		{
			BindButtonListeners();
			return;
		}
		if (mLoopListViewHour == null || mLoopListViewMinute == null)
		{
			Debug.LogError("[SpinTimePicker] LoopListView2 reference is missing.");
			return;
		}

		SpinPickerWheelUtility.ConfigureWheel(mLoopListViewHour);
		SpinPickerWheelUtility.ConfigureWheel(mLoopListViewMinute);

		mLoopListViewHour.mOnSnapNearestChanged = OnSnapTargetChanged;
		mLoopListViewMinute.mOnSnapNearestChanged = OnSnapTargetChanged;
		mLoopListViewHour.mOnSnapItemFinished = OnHourSnapTargetFinished;
		mLoopListViewMinute.mOnSnapItemFinished = OnMinuteSnapTargetFinished;

		mLoopListViewHour.InitListView(-1, OnGetItemByIndexForHour, SpinPickerWheelUtility.CreateInitParam(mLoopListViewHour));
		mLoopListViewMinute.InitListView(-1, OnGetItemByIndexForMinute, SpinPickerWheelUtility.CreateInitParam(mLoopListViewMinute));

		BindButtonListeners();

		mInitialized = true;
	}

	private void BindButtonListeners()
	{
		if (ConfirmButton != null)
		{
			ConfirmButton.onClick.RemoveListener(OnConfirmButtonClicked);
			ConfirmButton.onClick.AddListener(OnConfirmButtonClicked);
		}

		if (CancelButton != null)
		{
			CancelButton.onClick.RemoveListener(OnCancelButtonClicked);
			CancelButton.onClick.AddListener(OnCancelButtonClicked);
		}
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
		if (itemScript == null) return item;
		if (item.IsInitHandlerCalled == false)
		{
			item.IsInitHandlerCalled = true;
			itemScript.Init();
		}

		int value = firstValue + NormalizeIndex(index, count);
		itemScript.Value = value;
		if (itemScript.mText != null)
		{
			itemScript.mText.text = string.Format("{0:D2}{1}", value, suffix);
			itemScript.mText.color = mColorReserved;
		}
		return item;
	}

	private int NormalizeIndex(int index, int count)
	{
		if (count <= 0) return 0;
		if (index >= 0) return index % count;
		return count + ((index + 1) % count) - 1;
	}

	private void OnSnapTargetChanged(LoopListView2 listView, LoopListViewItem2 item)
	{
		SpinPickerWheelUtility.RefreshWheelVisuals(listView, mColorReserved, mColorSelected);
	}

	private bool TryGetItemValue(LoopListViewItem2 item, out int value)
	{
		value = 0;
		if (item == null) return false;
		SpinPickerItem itemScript = item.GetComponent<SpinPickerItem>();
		if (itemScript == null) return false;
		value = itemScript.Value;
		return true;
	}

	private void OnHourSnapTargetFinished(LoopListView2 listView, LoopListViewItem2 item)
	{
		if (TryGetItemValue(item, out int value))
		{
			mCurSelectedHour = value;
		}
		SpinPickerWheelUtility.RefreshWheelVisuals(listView, mColorReserved, mColorSelected);
		UpdateCurSelect();
	}

	private void OnMinuteSnapTargetFinished(LoopListView2 listView, LoopListViewItem2 item)
	{
		if (TryGetItemValue(item, out int value))
		{
			mCurSelectedMinute = value;
		}
		SpinPickerWheelUtility.RefreshWheelVisuals(listView, mColorReserved, mColorSelected);
		UpdateCurSelect();
	}

	private void RefreshAllWheelVisuals()
	{
		SpinPickerWheelUtility.RefreshWheelVisuals(mLoopListViewHour, mColorReserved, mColorSelected);
		SpinPickerWheelUtility.RefreshWheelVisuals(mLoopListViewMinute, mColorReserved, mColorSelected);
		UpdateOnImagePosition();
	}

	private void UpdateOnImagePosition()
	{
		if (onImage == null) return;

		LoopListViewItem2 centerItem = GetNearestCenterItem(mLoopListViewMinute)
			?? GetNearestCenterItem(mLoopListViewHour);
		if (centerItem == null) return;

		RectTransform imageRect = onImage.rectTransform;
		RectTransform itemRect = centerItem.CachedRectTransform;
		Vector3 itemWorldCenter = itemRect.TransformPoint(itemRect.rect.center);
		Vector3 imagePosition = imageRect.position;
		imagePosition.y = itemWorldCenter.y;
		imageRect.position = imagePosition;
	}

	private LoopListViewItem2 GetNearestCenterItem(LoopListView2 listView)
	{
		if (listView == null || !listView.ListViewInited) return null;

		listView.UpdateAllShownItemSnapData();
		LoopListViewItem2 nearestItem = null;
		float nearestDistance = float.MaxValue;
		for (int i = 0; i < listView.ShownItemCount; i++)
		{
			LoopListViewItem2 item = listView.GetShownItemByIndex(i);
			if (item == null) continue;

			float distance = Mathf.Abs(item.DistanceWithViewPortSnapCenter);
			if (distance >= nearestDistance) continue;

			nearestDistance = distance;
			nearestItem = item;
		}

		return nearestItem;
	}

	private void OnConfirmButtonClicked()
	{
		mConfirmCallback?.Invoke(SelectedValue);
	}

	private void OnCancelButtonClicked()
	{
		UIModule.Instance.HideWindow<SpinPickerUI>();
	}
}
