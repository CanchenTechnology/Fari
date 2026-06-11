/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/11/2026 1:53:22 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class PersonalProfileUI : WindowBase
{
	public PersonalProfileUIComponent uiComponent;

	// 头像精灵引用（需要在 Inspector 中配置或通过 Resource 加载）
	public Sprite moonAvatarSprite;
	public Sprite personAvatarSprite;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<PersonalProfileUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		LoadDataToUI();
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

	#region 数据加载与刷新

	/// <summary>
	/// 从 UserDataManager 加载数据到界面
	/// </summary>
	private void LoadDataToUI()
	{
		var manager = UserDataManager.Instance;

		// 加载文本数据
		uiComponent.UserNameInputInputField.text = manager.UserName;
		uiComponent.BirthdayInputInputField.text = manager.Birthday;
		uiComponent.TimeInputInputField.text = manager.BirthTime;
		uiComponent.CityInputInputField.text = manager.City;

		// 加载头像
		RefreshAvatar();
	}

	/// <summary>
	/// 刷新头像显示
	/// </summary>
	private void RefreshAvatar()
	{
		var manager = UserDataManager.Instance;
		Sprite sprite = null;

		switch (manager.CurrentAvatar)
		{
			case AvatarType.Moon:
				sprite = moonAvatarSprite;
				break;
			case AvatarType.Person:
				sprite = personAvatarSprite;
				break;
		}

		if (sprite != null)
		{
			uiComponent.AvatarImageImage.sprite = sprite;
		}
	}

	#endregion

	#region API Function

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		// 返回上一界面
		HideWindow();
		
	}
	public void OnMoonAvatarBtnButtonClick()
	{
		// 切换到月亮头像
		UserDataManager.Instance.SetAvatarType(AvatarType.Moon);
		RefreshAvatar();
	}
	public void OnPersonAvatarBtnButtonClick()
	{
		// 切换到人物头像
		UserDataManager.Instance.SetAvatarType(AvatarType.Person);
		RefreshAvatar();
	}
	public void OnUserNameInputInputChange(string text)
	{
		// 输入变化时实时更新数据
		UserDataManager.Instance.SetUserName(text);
	}
	public void OnUserNameInputInputEnd(string text)
	{
		// 输入结束时更新数据
		UserDataManager.Instance.SetUserName(text);
	}
	public void OnBirthdayInputInputChange(string text)
	{
		UserDataManager.Instance.SetBirthday(text);
	}
	public void OnBirthdayInputInputEnd(string text)
	{
		UserDataManager.Instance.SetBirthday(text);
	}
	public void OnTimeInputInputChange(string text)
	{
		UserDataManager.Instance.SetBirthTime(text);
	}
	public void OnTimeInputInputEnd(string text)
	{
		UserDataManager.Instance.SetBirthTime(text);
	}
	public void OnCityInputInputChange(string text)
	{
		UserDataManager.Instance.SetCity(text);
	}
	public void OnCityInputInputEnd(string text)
	{
		UserDataManager.Instance.SetCity(text);
	}
	public void OnSaveBtnButtonClick()
	{
		// 保存数据到本地
		UserDataManager.Instance.SaveData();

		// 可选：校验数据完整性
		if (UserDataManager.Instance.IsProfileComplete())
		{
			ToastManager.ShowToast("保存成功！");
		}
		else
		{
			ToastManager.ShowToast("资料已保存，但信息尚未填写完整");
		}
	}
	#endregion
}
