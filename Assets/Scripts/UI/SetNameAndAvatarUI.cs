/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 11:12:50
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class SetNameAndAvatarUI : WindowBase
{
	public SetNameAndAvatarUIComponent uiComponent;
	private AvatarType selectedAvatar = AvatarType.Moon;
	private string selectedName = string.Empty;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<SetNameAndAvatarUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		UserDataManager manager = UserDataManager.Instance;
		selectedName = manager != null ? manager.UserName : string.Empty;
		selectedAvatar = manager != null ? manager.CurrentAvatar : AvatarType.Moon;

		if (uiComponent?.NameInputField != null)
			uiComponent.NameInputField.text = selectedName;
		RefreshAvatarToggles();
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
	public void OnAvatarSelectionToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnAvatarOption1ToggleChange(bool state, Toggle toggle)
	{
		if (state) SelectAvatar(AvatarType.Moon);
	}
	public void OnAvatarOption2ToggleChange(bool state, Toggle toggle)
	{
		if (state) SelectAvatar(AvatarType.Person);
	}
	public void OnAvatarOption3ToggleChange(bool state, Toggle toggle)
	{
		if (state) SelectAvatar(AvatarType.Moon);
	}
	public void OnNameInputChange(string text)
	{
		selectedName = text ?? string.Empty;
	}
	public void OnNameInputEnd(string text)
	{
		selectedName = RegistrationFlowUtility.NormalizeName(text);
		if (uiComponent?.NameInputField != null)
			uiComponent.NameInputField.text = selectedName;
	}
	public void OnContinueButtonClick()
	{
		selectedName = RegistrationFlowUtility.NormalizeName(uiComponent?.NameInputField != null
			? uiComponent.NameInputField.text
			: selectedName);

		if (string.IsNullOrWhiteSpace(selectedName))
		{
			ToastManager.ShowToast("请先填写昵称");
			return;
		}

		UserDataManager manager = UserDataManager.Instance;
		if (manager == null)
		{
			ToastManager.ShowToast("用户资料服务暂不可用");
			return;
		}

		manager.SetUserName(selectedName);
		manager.SetAvatarType(selectedAvatar);
		RegistrationFlowUtility.SaveUserDataAndSyncCloud();
		UIModule.Instance.PopUpWindow<ChooseGuideUI>();
	}

	private void SelectAvatar(AvatarType avatar)
	{
		selectedAvatar = avatar;
		UserDataManager.Instance?.SetAvatarType(selectedAvatar);
	}

	private void RefreshAvatarToggles()
	{
		if (uiComponent == null) return;

		if (uiComponent.AvatarOption1Toggle != null)
			uiComponent.AvatarOption1Toggle.isOn = selectedAvatar == AvatarType.Moon;
		if (uiComponent.AvatarOption2Toggle != null)
			uiComponent.AvatarOption2Toggle.isOn = selectedAvatar == AvatarType.Person;
		if (uiComponent.AvatarOption3Toggle != null)
			uiComponent.AvatarOption3Toggle.isOn = false;
	}
	#endregion
}
