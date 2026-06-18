/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/11/2026 2:46:47 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class AccountUI : WindowBase
{
	public AccountUIComponent uiComponent;
	private bool _deleteConfirmArmed;
	private float _deleteConfirmArmedAt;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<AccountUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		_deleteConfirmArmed = false;
		RefreshUI();
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

	/// <summary>
	/// 刷新账户信息UI显示
	/// </summary>
	private void RefreshUI()
	{
		if (uiComponent == null) return;

		var userData = UserDataManager.Instance;

		// 电子邮件
		if (uiComponent.EmailValueText != null)
		{
			uiComponent.EmailValueText.text = string.IsNullOrWhiteSpace(userData.Email)
				? "未绑定"
				: userData.Email;
		}

		// 登录类型
		if (uiComponent.LoginTypeValueText != null)
		{
			uiComponent.LoginTypeValueText.text = userData.GetLoginTypeDisplayText();
		}

		// 用户ID
		if (uiComponent.UserIdValueText != null)
		{
			uiComponent.UserIdValueText.text = string.IsNullOrWhiteSpace(userData.UserId)
				? "未设置"
				: userData.UserId;
		}

		// 注册时间
		if (uiComponent.RegTimeValueText != null)
		{
			uiComponent.RegTimeValueText.text = userData.GetFormattedRegTime();
		}

		// 账号状态
		if (uiComponent.StatusValueText != null)
		{
			uiComponent.StatusValueText.text = userData.GetStatusDisplayText();
		}
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonButtonClick()
	{
		HideWindow();
	}

	/// <summary>
	/// 退出登录
	/// </summary>
	public void OnexitButtonClick()
	{
		// 弹出确认对话框（如果有确认窗口的话，这里使用简单的确认逻辑）
		// TODO: 可以替换为 UIManager 打开确认弹窗
		UserDataManager.Instance.Logout();
		Debug.Log("[AccountUI] 用户已退出登录");

		// 退出后返回登录界面或主界面
		HideWindow();
	}

	/// <summary>
	/// 删除账户（强确认流程）
	/// </summary>
	public void OndeleteButtonClick()
	{
		if (!_deleteConfirmArmed || Time.time - _deleteConfirmArmedAt > 8f)
		{
			_deleteConfirmArmed = true;
			_deleteConfirmArmedAt = Time.time;
			ToastManager.ShowToast("再次点击删除账户，将清除云端数据且不可恢复");
			return;
		}

		_deleteConfirmArmed = false;
		var authManager = FirebaseAuthManager.Instance;
		if (authManager == null || !authManager.IsLoggedIn)
		{
			UserDataManager.Instance.ClearData();
			ToastManager.ShowToast("本地账户数据已清除");
			HideWindow();
			return;
		}

		authManager.DeleteUser(
			() =>
			{
				UserDataManager.Instance.ClearData();
				ToastManager.ShowToast("账户已删除");
				HideWindow();
			},
			error =>
			{
				ToastManager.ShowToast("删除失败：" + error);
				Debug.LogError("[AccountUI] 删除账户失败: " + error);
			});
	}
	#endregion
}
