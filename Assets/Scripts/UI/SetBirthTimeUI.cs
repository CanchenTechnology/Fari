/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 10:51:59
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class SetBirthTimeUI : WindowBase
{
	public SetBirthTimeUIComponent uiComponent;
	private string selectedBirthTime = string.Empty;
	private bool unknownTime;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<SetBirthTimeUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		selectedBirthTime = UserDataManager.Instance != null ? UserDataManager.Instance.BirthTime : string.Empty;
		unknownTime = RegistrationFlowUtility.IsUnknownBirthTime(selectedBirthTime);
		if (uiComponent?.UnknownTimeToggle != null)
			uiComponent.UnknownTimeToggle.isOn = unknownTime;
		RefreshBirthTimeButtons();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnHourPickerButtonClick()
	{
		OpenBirthTimePicker();
	}
	public void OnMinutePickerButtonClick()
	{
		OpenBirthTimePicker();
	}
	public void OnPeriodPickerButtonClick()
	{
		OpenBirthTimePicker();
	}
	public void OnUnknownTimeToggleChange(bool state, Toggle toggle)
	{
		unknownTime = state;
		if (unknownTime)
		{
			selectedBirthTime = RegistrationFlowUtility.UnknownBirthTime;
			UserDataManager.Instance?.SetBirthTime(selectedBirthTime);
			RegistrationFlowUtility.SaveUserDataAndSyncCloud();
		}
		else if (RegistrationFlowUtility.IsUnknownBirthTime(selectedBirthTime))
		{
			selectedBirthTime = string.Empty;
			UserDataManager.Instance?.SetBirthTime(selectedBirthTime);
			RegistrationFlowUtility.SaveUserDataAndSyncCloud();
		}
		RefreshBirthTimeButtons();
	}
	public void OnContinueButtonClick()
	{
		if (string.IsNullOrWhiteSpace(selectedBirthTime))
		{
			ToastManager.ShowToast("请选择出生时间，或勾选时间未知");
			return;
		}

		UserDataManager.Instance?.SetBirthTime(selectedBirthTime);
		RegistrationFlowUtility.SaveUserDataAndSyncCloud();
		UIModule.Instance.PopUpWindow<SetBirthCityUI>();
	}

	private void OpenBirthTimePicker()
	{
		if (unknownTime && uiComponent?.UnknownTimeToggle != null)
			uiComponent.UnknownTimeToggle.isOn = false;

		SpinPickerUI.ShowTime(RegistrationFlowUtility.IsUnknownBirthTime(selectedBirthTime) ? string.Empty : selectedBirthTime, value =>
		{
			if (!RegistrationFlowUtility.TryNormalizeBirthTime(value, out string normalized))
			{
				ToastManager.ShowToast("出生时间格式无效");
				return;
			}

			selectedBirthTime = normalized;
			unknownTime = false;
			if (uiComponent?.UnknownTimeToggle != null)
				uiComponent.UnknownTimeToggle.isOn = false;
			UserDataManager.Instance?.SetBirthTime(selectedBirthTime);
			RegistrationFlowUtility.SaveUserDataAndSyncCloud();
			RefreshBirthTimeButtons();
		});
	}

	private void RefreshBirthTimeButtons()
	{
		RegistrationFlowUtility.TryGetBirthTimeParts(selectedBirthTime, out string hour, out string minute, out string period);
		RegistrationFlowUtility.SetButtonText(uiComponent?.HourPickerButton, hour);
		RegistrationFlowUtility.SetButtonText(uiComponent?.MinutePickerButton, minute);
		RegistrationFlowUtility.SetButtonText(uiComponent?.PeriodPickerButton, period);
	}
	#endregion
}
