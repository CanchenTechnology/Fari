/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 12:25:22
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class CreateFriendUI : WindowBase
{
	public CreateFriendUIComponent uiComponent;
	private int selectedAvatarIndex = 1;
	private string username = string.Empty;
	private string birthday = string.Empty;
	private string birthTime = string.Empty;
	private string city = string.Empty;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<CreateFriendUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		ReadFormValues();
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
	public void OnAvatar4ButtonClick()
	{
		selectedAvatarIndex = 4;
		ToastManager.ShowToast("已选择头像 4");
	}
	public void OnAvatar3ButtonClick()
	{
		selectedAvatarIndex = 3;
		ToastManager.ShowToast("已选择头像 3");
	}
	public void OnAvatar2ButtonClick()
	{
		selectedAvatarIndex = 2;
		ToastManager.ShowToast("已选择头像 2");
	}
	public void OnAvatar1ButtonClick()
	{
		selectedAvatarIndex = 1;
		ToastManager.ShowToast("已选择头像 1");
	}
		
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnNotificationsButtonClick()
	{
		ToastManager.ShowDebug();
	}
	public void OnAvatarSelectionToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnAvatar1ToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnAvatar2ToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnAvatar3ToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnAvatar4ToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnUploadAvatarButtonClick()
	{
		ToastManager.ShowToast("头像上传将在正式版开放");
	}
	public void OnUsernameInputChange(string text)
	{
		username = text;
	}
	public void OnUsernameInputEnd(string text)
	{
		username = text;
	}
	public void OnBirthdayInputChange(string text)
	{
		birthday = text;
	}
	public void OnBirthdayInputEnd(string text)
	{
		birthday = text;
	}
	public void OnBirthTimeInputChange(string text)
	{
		birthTime = text;
	}
	public void OnBirthTimeInputEnd(string text)
	{
		birthTime = text;
	}
	public void OnCityInputChange(string text)
	{
		city = text;
	}
	public void OnCityInputEnd(string text)
	{
		city = text;
	}
	public void OnSubmitButtonClick()
	{
		ReadFormValues();
		if (string.IsNullOrWhiteSpace(username))
		{
			ToastManager.ShowToast("请先填写好友名字");
			return;
		}

		string notes = $"虚拟好友档案 · 头像 {selectedAvatarIndex}";
		FriendDataManager.Instance.AddVirtualFriend(
			username.Trim(),
			"好友",
			birthday.Trim(),
			birthTime.Trim(),
			city.Trim(),
			notes);

		ToastManager.ShowToast($"已创建 {username.Trim()}");
		HideWindow();
	}
	public void OnBottomNavToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnTabOracleToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnTabChatToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnTabFriendsToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnTabProfileToggleChange(bool state, Toggle toggle)
	{
	}

	private void ReadFormValues()
	{
		username = uiComponent.UsernameInputField != null ? uiComponent.UsernameInputField.text : username;
		birthday = uiComponent.BirthdayInputField != null ? uiComponent.BirthdayInputField.text : birthday;
		birthTime = uiComponent.BirthTimeInputField != null ? uiComponent.BirthTimeInputField.text : birthTime;
		city = uiComponent.CityInputField != null ? uiComponent.CityInputField.text : city;
	}
	#endregion
}
