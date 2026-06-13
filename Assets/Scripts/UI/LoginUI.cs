/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/6 18:32:19
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using Firebase.Auth;

public class LoginUI : WindowBase
{
	public LoginUIComponent uiComponent;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<LoginUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		RegisterFirebaseEvents();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
		UnregisterFirebaseEvents();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
		UnregisterFirebaseEvents();
	}
	#endregion

	#region Firebase 事件注册

	private void RegisterFirebaseEvents()
	{
		var authManager = FirebaseAuthManager.Instance;
		if (authManager != null)
		{
			authManager.OnLoginSuccess += OnFirebaseLoginSuccess;
			authManager.OnLoginFailed += OnFirebaseLoginFailed;
		}
	}

	private void UnregisterFirebaseEvents()
	{
		var authManager = FirebaseAuthManager.Instance;
		if (authManager != null)
		{
			authManager.OnLoginSuccess -= OnFirebaseLoginSuccess;
			authManager.OnLoginFailed -= OnFirebaseLoginFailed;
		}
	}

	/// <summary>
	/// Firebase 登录成功回调
	/// </summary>
	private void OnFirebaseLoginSuccess(AuthProvider provider, FirebaseUser user)
	{
		SetButtonsInteractable(true);

		string providerName = FirebaseAuthManager.Instance.GetProviderDisplayName(provider);
		ToastManager.ShowToast($"{providerName} 登录成功");

		if (GameManager.Instance.isRegister)
		{
			UIModule.Instance.HideWindow<LoginUI>();
			UIModule.Instance.PopUpWindow<NavigationUI>();
		}
        else
		{
			UIModule.Instance.PopUpWindow<SetBirthDateUI>();
        }

    }

	/// <summary>
	/// Firebase 登录失败回调
	/// </summary>
	private void OnFirebaseLoginFailed(AuthProvider provider, string error)
	{
		SetButtonsInteractable(true);

		string providerName = FirebaseAuthManager.Instance.GetProviderDisplayName(provider);
		ToastManager.ShowToast($"{providerName} 登录失败: {error}");
	}

	#endregion

	#region UI 交互辅助

	/// <summary>
	/// 设置所有登录按钮的交互状态
	/// </summary>
	private void SetButtonsInteractable(bool interactable)
	{
		if (uiComponent != null)
		{
			if (uiComponent.GooglePlaySignInButton != null)
				uiComponent.GooglePlaySignInButton.interactable = interactable;
			if (uiComponent.AppleSignInButton != null)
				uiComponent.AppleSignInButton.interactable = interactable;
			if (uiComponent.FaceBookSignInButton != null)
				uiComponent.FaceBookSignInButton.interactable = interactable;
		}
	}

	/// <summary>
	/// 检查 Firebase 是否就绪，未就绪时显示提示
	/// </summary>
	private bool CheckFirebaseReady()
	{
		var authManager = FirebaseAuthManager.Instance;
		if (authManager == null || !authManager.IsFirebaseInitialized)
		{
			ToastManager.ShowToast("Firebase 初始化中，请稍后重试");
			return false;
		}
		if (authManager.IsLoggingIn)
		{
			ToastManager.ShowToast("正在登录中，请勿重复操作");
			return false;
		}
		return true;
	}

	#endregion

	#region API Function

	#endregion

	#region UI组件事件		 
	public void OnFaceBookSignInButtonClick()
	{
		if (!CheckFirebaseReady()) return;
		SetButtonsInteractable(false);
		FirebaseAuthManager.Instance.SignInWithFacebook();
	}
	public void OnAppleSignInButtonClick()
	{
		if (!CheckFirebaseReady()) return;
		SetButtonsInteractable(false);
		FirebaseAuthManager.Instance.SignInWithApple();
	}

	public void OnGooglePlaySignInButtonClick()
	{
		if (!CheckFirebaseReady()) return;
		SetButtonsInteractable(false);
		FirebaseAuthManager.Instance.SignInWithGoogle();
	}
	public void OnGameCenterSignInButtonClick()
	{
		ToastManager.ShowDebug();
	}
	public void OnEmailSignInButtonClick()
	{
		// TODO: 邮箱登录功能待实现
		ToastManager.ShowDebug();
	}
	public void OnCreateAccountButtonClick()
	{
		// TODO: 创建账号功能待实现
		ToastManager.ShowDebug();
	}
	public void OnAnonymousSignInButtonClick()
	{
		if (!CheckFirebaseReady()) return;
		SetButtonsInteractable(false);
		FirebaseAuthManager.Instance.SignInAnonymously();
	}
	public void OnDontSignInButtonClick()
	{
		// 跳过登录，直接进入应用（以游客身份）
		UIModule.Instance.HideWindow<LoginUI>();
		UIModule.Instance.PopUpWindow<NavigationUI>();
	}
	#endregion
}
