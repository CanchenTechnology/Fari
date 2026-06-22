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
	private bool _isDeletingAccount;

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
		ShowSelectWindow(
			"退出账号",
			"确定要退出当前账号吗？本地资料会保留，云端账号不会删除。",
			"退出",
			ConfirmLogout);
	}

	private void ConfirmLogout()
	{
		FirebaseAuthManager.Instance?.SignOut();
		UserDataManager.Instance.Logout();
		Debug.Log("[AccountUI] 用户已退出登录");

		ReturnToLogin();
	}

	/// <summary>
	/// 删除账户（强确认流程）
	/// </summary>
	public void OndeleteButtonClick()
	{
		ShowSelectWindow(
			"删除账号",
			"确定永久删除当前账号吗？后台账号、云端资料、公开资料、占卜记录、好友记录和本地账户数据都会被清除，此操作不可恢复。",
			"永久删除",
			ConfirmDeleteAccount);
	}

	private void ConfirmDeleteAccount()
	{
		if (_isDeletingAccount)
		{
			ToastManager.ShowToast("正在删除账号，请稍候");
			return;
		}

		var authManager = FirebaseAuthManager.Instance;
		if (authManager == null || !authManager.IsLoggedIn)
		{
			ClearDeletedAccountLocalCaches();
			UserDataManager.Instance.ClearData();
			ToastManager.ShowToast("本地账户数据已清除");
			ReturnToLogin();
			return;
		}

		_isDeletingAccount = true;
		SetAccountButtonsInteractable(false);
		ToastManager.ShowToast("正在删除账号数据...");

		authManager.DeleteUser(
			() =>
			{
				_isDeletingAccount = false;
				SetAccountButtonsInteractable(true);
				ClearDeletedAccountLocalCaches();
				UserDataManager.Instance.ClearData();
				ToastManager.ShowToast("账户已删除");
				ReturnToLogin();
			},
			error =>
			{
				_isDeletingAccount = false;
				SetAccountButtonsInteractable(true);
				ToastManager.ShowToast("删除失败：" + error);
				Debug.LogError("[AccountUI] 删除账户失败: " + error);
			});
	}

	private void ShowSelectWindow(string title, string content, string sureText, System.Action onSure)
	{
		SelectWindow selectWindow = UIModule.Instance.PopUpWindow<SelectWindow>();
		if (selectWindow == null)
		{
			ToastManager.ShowToast("确认窗口打开失败");
			return;
		}

		selectWindow.InitViewState(
			SelectType.Normal,
			content,
			onSure,
			null,
			sureText,
			"取消",
			title,
			TextAnchor.MiddleCenter);
	}

	private void ReturnToLogin()
	{
		UIModule.Instance.DestroyAllWindows();
		UIModule.Instance.PopUpWindow<LoginUI>();
	}

	private void ClearDeletedAccountLocalCaches()
	{
		HistoryUI.SelectedRecord = null;
		DivinationHistoryUI.SelectedRecord = null;
		DivinationInfoUI.SelectedRecord = null;
		DivinationRecordFirestore.ClearLocalCacheForCurrentUser();
		DailyOracleFirestore.ClearLocalCacheForCurrentUser();
		DialogHistoryFirestore.ClearLocalDefault();
		DailyOracleService.Instance?.ClearCache();
		DivinationEngine.Instance?.ClearTodayCard();
		DivinationEngine.Instance?.ClearSession();
		UsageStatsManager.Instance?.ClearLocalUsageStats();
	}

	private void SetAccountButtonsInteractable(bool interactable)
	{
		if (uiComponent == null) return;
		if (uiComponent.exitButton != null)
			uiComponent.exitButton.interactable = interactable;
		if (uiComponent.deleteButton != null)
			uiComponent.deleteButton.interactable = interactable;
	}
	#endregion
}
