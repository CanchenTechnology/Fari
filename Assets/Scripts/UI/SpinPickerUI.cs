/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/18/2026 6:16:02 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class SpinPickerUI : WindowBase
{
	public SpinPickerUIComponent uiComponent;

	private enum PickerMode
	{
		Date,
		Time,
		Region
	}

	private static PickerMode sPendingMode = PickerMode.Date;
	private static string sPendingInitialValue = string.Empty;
	private static Action<string> sPendingConfirmCallback;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<SpinPickerUIComponent>();
		uiComponent.InitComponent(this);
		Layer = WindowLayer.Popup;
		this.Canvas.sortingOrder = (int)Layer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		OpenPendingPicker();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		sPendingConfirmCallback = null;
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public static void ShowDate(string initialValue, Action<string> confirmCallback)
	{
		Show(PickerMode.Date, initialValue, confirmCallback);
	}

	public static void ShowTime(string initialValue, Action<string> confirmCallback)
	{
		Show(PickerMode.Time, initialValue, confirmCallback);
	}

	public static void ShowRegion(string initialValue, Action<string> confirmCallback)
	{
		Show(PickerMode.Region, initialValue, confirmCallback);
	}

	private static void Show(PickerMode mode, string initialValue, Action<string> confirmCallback)
	{
		sPendingMode = mode;
		sPendingInitialValue = initialValue ?? string.Empty;
		sPendingConfirmCallback = confirmCallback;
		UIModule.Instance.PopUpWindow<SpinPickerUI>();
	}
	#endregion

	#region UI组件事件
	private void OpenPendingPicker()
	{
		SpinDatePicker datePicker = uiComponent != null ? uiComponent.SpinDatePickerSpinDatePicker : null;
		SpinTimePicker timePicker = uiComponent != null ? uiComponent.SpinTimePickerSpinTimePicker : null;
		SpinRegionPicker regionPicker = uiComponent != null ? uiComponent.SpinRegionPickerSpinRegionPicker : null;

		SetPickerVisible(datePicker, sPendingMode == PickerMode.Date);
		SetPickerVisible(timePicker, sPendingMode == PickerMode.Time);
		SetPickerVisible(regionPicker, sPendingMode == PickerMode.Region);

		switch (sPendingMode)
		{
			case PickerMode.Time:
				if (timePicker != null)
				{
					timePicker.Show(sPendingInitialValue, CompleteSelection);
				}
				break;
			case PickerMode.Region:
				if (regionPicker != null)
				{
					regionPicker.Show(sPendingInitialValue, CompleteSelection);
				}
				break;
			default:
				if (datePicker != null)
				{
					datePicker.Show(sPendingInitialValue, CompleteSelection);
				}
				break;
		}
	}

	private void SetPickerVisible(Component picker, bool visible)
	{
		if (picker != null)
		{
			picker.gameObject.SetActive(visible);
		}
	}

	private void CompleteSelection(string value)
	{
		Action<string> callback = sPendingConfirmCallback;
		sPendingConfirmCallback = null;
		callback?.Invoke(value);
		HideWindow();
	}
	#endregion
}
