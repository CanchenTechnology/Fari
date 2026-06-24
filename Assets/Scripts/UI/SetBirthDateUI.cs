/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 10:40:19
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class SetBirthDateUI : WindowBase
{
	public SetBirthDateUIComponent uiComponent;
	private string selectedBirthday = string.Empty;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<SetBirthDateUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		selectedBirthday = UserDataManager.Instance != null ? UserDataManager.Instance.Birthday : string.Empty;
		RefreshBirthdayButtons();
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
	public void OnMonthPickerButtonClick()
	{
		OpenBirthdayPicker();
	}
	public void OnDayPickerButtonClick()
	{
		OpenBirthdayPicker();
	}
	public void OnYearPickerButtonClick()
	{
		OpenBirthdayPicker();
	}
	public void OnContinueButtonClick()
	{
		if (string.IsNullOrWhiteSpace(selectedBirthday))
		{
			ToastManager.ShowToast("请先选择生日");
			return;
		}

		UserDataManager.Instance?.SetBirthday(selectedBirthday);
		RegistrationFlowUtility.SaveUserDataAndSyncCloud();
		UIModule.Instance.PopUpWindow<SetBirthTimeUI>();
	}

	private void OpenBirthdayPicker()
	{
		SpinPickerUI.ShowDate(selectedBirthday, value =>
		{
			if (!RegistrationFlowUtility.TryNormalizeBirthday(value, out string normalized))
			{
				ToastManager.ShowToast("生日格式无效");
				return;
			}

			selectedBirthday = normalized;
			UserDataManager.Instance?.SetBirthday(selectedBirthday);
			RegistrationFlowUtility.SaveUserDataAndSyncCloud();
			RefreshBirthdayButtons();
		});
	}

	private void RefreshBirthdayButtons()
	{
		RegistrationFlowUtility.TryGetBirthdayParts(selectedBirthday, out string year, out string month, out string day);
		RegistrationFlowUtility.SetButtonText(uiComponent?.YearPickerButton, year);
		RegistrationFlowUtility.SetButtonText(uiComponent?.MonthPickerButton, month);
		RegistrationFlowUtility.SetButtonText(uiComponent?.DayPickerButton, day);
	}
	#endregion
}
