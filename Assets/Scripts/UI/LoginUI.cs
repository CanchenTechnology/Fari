/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/6 18:32:19
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using Firebase.Auth;
using TMPro;

public class LoginUI : WindowBase
{
	public LoginUIComponent uiComponent;
	private TMP_InputField emailInputField;
	private TMP_InputField passwordInputField;
	private Button emailLoginButton;
	private Button createAccountButton;
	private Button gameCenterSignInButton;
	private Button anonymousSignInButton;
	private Button dontSignInButton;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
	private const string TestEmailLoginEmail = "1255755615qq@gmail.com";
	private const string TestEmailLoginPassword = "12345678";
#endif

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<LoginUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		BindOptionalRuntimeButtons();
		ApplyPlatformButtonVisibility();
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		RegisterFirebaseEvents();
		BindOptionalRuntimeButtons();
		ApplyPlatformButtonVisibility();
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
			if (emailLoginButton != null)
				emailLoginButton.interactable = interactable;
			if (createAccountButton != null)
				createAccountButton.interactable = interactable;
			if (gameCenterSignInButton != null)
				gameCenterSignInButton.interactable = interactable;
			Button guestButton = anonymousSignInButton != null ? anonymousSignInButton : uiComponent.youkeLoginButton;
			if (guestButton != null)
				guestButton.interactable = interactable;
			if (dontSignInButton != null)
				dontSignInButton.interactable = interactable;
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

	private void BindOptionalRuntimeButtons()
	{
		emailInputField = emailInputField != null
			? emailInputField
			: FindInputFieldByName(transform, "[InputField]Email", "EmailInput", "Email");
		passwordInputField = passwordInputField != null
			? passwordInputField
			: FindInputFieldByName(transform, "[InputField]Password", "PasswordInput", "Password");
		if (passwordInputField != null)
			passwordInputField.contentType = TMP_InputField.ContentType.Password;

		emailLoginButton = BindButton(emailLoginButton, OnEmailSignInButtonClick, "[Button]EmailLogin", "EmailLogin", "EmailSignInButton");
		createAccountButton = BindButton(createAccountButton, OnCreateAccountButtonClick, "[Button]CreateAccount", "CreateAccountButton", "CreateAccount");
		gameCenterSignInButton = BindButton(gameCenterSignInButton, OnGameCenterSignInButtonClick, "[Button]GameCenterLogin", "GameCenterLogin", "GameCenterSignInButton");
		anonymousSignInButton = anonymousSignInButton != null ? anonymousSignInButton : uiComponent?.youkeLoginButton;
		anonymousSignInButton = BindButton(anonymousSignInButton, OnAnonymousSignInButtonClick, "[Button]AnonymousLogin", "AnonymousLogin", "[Button]AnonymousSignInButton", "AnonymousSignInButton");
		dontSignInButton = BindButton(dontSignInButton, OnDontSignInButtonClick, "[Button]DontSignIn", "DontSignInButton", "SkipLoginButton");
	}

	private void ApplyPlatformButtonVisibility()
	{
		SetOptionalButtonVisible(uiComponent?.GooglePlaySignInButton, IsGoogleSignInVisible());
		SetOptionalButtonVisible(uiComponent?.AppleSignInButton, IsAppleSignInVisible());
		SetOptionalButtonVisible(uiComponent?.FaceBookSignInButton, IsFacebookSignInVisible());
		SetOptionalButtonVisible(gameCenterSignInButton, IsGameCenterSignInVisible());
	}

	private static void SetOptionalButtonVisible(Button button, bool visible)
	{
		if (button != null)
			button.gameObject.SetActive(visible);
	}

	private static bool IsAppleSignInVisible()
	{
#if UNITY_IOS || UNITY_EDITOR
		return true;
#else
		return false;
#endif
	}

	private static bool IsGoogleSignInVisible()
	{
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
		return true;
#else
		return false;
#endif
	}

	private static bool IsFacebookSignInVisible()
	{
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
		return true;
#else
		return false;
#endif
	}

	private static bool IsGameCenterSignInVisible()
	{
#if UNITY_IOS || UNITY_TVOS
		return true;
#else
		return false;
#endif
	}

	private Button BindButton(Button cached, UnityEngine.Events.UnityAction action, params string[] names)
	{
		Button button = cached != null ? cached : FindButtonByName(transform, names);
		if (button == null)
		{
			return null;
		}

		button.onClick.RemoveListener(action);
		button.onClick.AddListener(action);
		return button;
	}

	private string GetPrefilledEmail()
	{
		string email = emailInputField != null ? emailInputField.text : string.Empty;
		return (email ?? string.Empty).Trim();
	}

	private string GetPrefilledPassword()
	{
		return passwordInputField != null ? passwordInputField.text : string.Empty;
	}

	private bool HasInlineEmailPasswordFields()
	{
		return emailInputField != null && passwordInputField != null;
	}

	private bool ValidateInlineEmailPassword(string email, string password)
	{
		if (string.IsNullOrWhiteSpace(email) || !email.Contains("@") || !email.Contains("."))
		{
			ToastManager.ShowToast("请输入有效邮箱");
			return false;
		}

		if (string.IsNullOrEmpty(password) || password.Length < 6)
		{
			ToastManager.ShowToast("密码至少需要 6 位");
			return false;
		}

		return true;
	}

	private bool TrySubmitTestEmailLogin()
	{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
		if (emailInputField != null)
			emailInputField.text = TestEmailLoginEmail;
		if (passwordInputField != null)
			passwordInputField.text = TestEmailLoginPassword;

		SubmitEmailSignIn(TestEmailLoginEmail, TestEmailLoginPassword);
		return true;
#else
		return false;
#endif
	}

	private void ShowEmailAuthDialog()
	{
		if (!CheckFirebaseReady()) return;

		LoginEmailAuthOverlay.Show(
			transform,
			GetPrefilledEmail(),
			SubmitEmailSignIn,
			SubmitEmailCreateAccount,
			SubmitPasswordReset);
	}

	private void SubmitEmailSignIn(string email, string password)
	{
		if (!CheckFirebaseReady()) return;
		SetButtonsInteractable(false);
		ToastManager.ShowToast("正在邮箱登录...");
		FirebaseAuthManager.Instance.SignInWithEmail(email, password);
	}

	private void SubmitEmailCreateAccount(string email, string password, string displayName)
	{
		if (!CheckFirebaseReady()) return;
		SetButtonsInteractable(false);
		ToastManager.ShowToast("正在创建邮箱账号...");
		FirebaseAuthManager.Instance.CreateAccountWithEmail(email, password, displayName);
	}

	private void SubmitPasswordReset(string email)
	{
		if (!CheckFirebaseReady()) return;
		ToastManager.ShowToast("正在发送重置邮件...");
		FirebaseAuthManager.Instance.SendPasswordResetEmail(email, (success, message) =>
		{
			ToastManager.ShowToast(message);
		});
	}

	private static Button FindButtonByName(Transform root, params string[] names)
	{
		if (root == null || names == null) return null;

		Button[] buttons = root.GetComponentsInChildren<Button>(true);
		foreach (Button button in buttons)
		{
			foreach (string name in names)
			{
				if (button.transform.name == name)
					return button;
			}
		}

		return null;
	}

	private static TMP_InputField FindInputFieldByName(Transform root, params string[] names)
	{
		if (root == null || names == null) return null;

		TMP_InputField[] fields = root.GetComponentsInChildren<TMP_InputField>(true);
		foreach (TMP_InputField field in fields)
		{
			foreach (string name in names)
			{
				if (field.transform.name == name)
					return field;
			}
		}

		return null;
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
		if (!CheckFirebaseReady()) return;
		SetButtonsInteractable(false);
		FirebaseAuthManager.Instance.SignInWithGameCenter();
	}
	public void OnEmailSignInButtonClick()
	{
		BindOptionalRuntimeButtons();
		if (TrySubmitTestEmailLogin()) return;

		if (!HasInlineEmailPasswordFields())
		{
			ShowEmailAuthDialog();
			return;
		}

		string email = GetPrefilledEmail();
		string password = GetPrefilledPassword();
		if (!ValidateInlineEmailPassword(email, password)) return;

		SubmitEmailSignIn(email, password);
	}
	public void OnCreateAccountButtonClick()
	{
		ShowEmailAuthDialog();
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

public static class LoginEmailAuthOverlay
{
	public static void Show(Transform anchor, string initialEmail, Action<string, string> onSignIn, Action<string, string, string> onCreate, Action<string> onResetPassword)
	{
		Transform parent = ResolveOverlayParent(anchor);
		GameObject overlay = CreateOverlay(parent);
		RectTransform root = overlay.GetComponent<RectTransform>();

		GameObject panel = CreatePanel(root);
		VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(28, 28, 24, 24);
		layout.spacing = 12f;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		TMP_Text title = CreateText(panel.transform, "邮箱登录", 28, FontStyles.Bold, new Color(1f, 0.78f, 0.45f, 1f));
		title.alignment = TextAlignmentOptions.Center;

		TMP_InputField emailField = CreateInput(panel.transform, "邮箱", false);
		emailField.text = initialEmail ?? string.Empty;
		TMP_InputField passwordField = CreateInput(panel.transform, "密码（至少 6 位）", true);
		TMP_InputField nameField = CreateInput(panel.transform, "昵称（创建账号时使用，可选）", false);

		GameObject row = new GameObject("EmailAuthButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
		row.transform.SetParent(panel.transform, false);
		HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = 12f;
		rowLayout.childControlWidth = true;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = true;
		rowLayout.childForceExpandHeight = false;
		row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 52f);

		Button signInButton = CreateButton(row.transform, "登录", false);
		Button createButton = CreateButton(row.transform, "创建账号", false);
		Button resetButton = CreateButton(panel.transform, "忘记密码？发送重置邮件", true);
		Button cancelButton = CreateButton(panel.transform, "取消", true);

		signInButton.onClick.AddListener(() =>
		{
			if (!Validate(emailField.text, passwordField.text)) return;
			UnityEngine.Object.Destroy(overlay);
			onSignIn?.Invoke(emailField.text.Trim(), passwordField.text);
		});

		createButton.onClick.AddListener(() =>
		{
			if (!Validate(emailField.text, passwordField.text)) return;
			UnityEngine.Object.Destroy(overlay);
			onCreate?.Invoke(emailField.text.Trim(), passwordField.text, nameField.text?.Trim());
		});

		resetButton.onClick.AddListener(() =>
		{
			if (!ValidateEmail(emailField.text)) return;
			UnityEngine.Object.Destroy(overlay);
			onResetPassword?.Invoke(emailField.text.Trim());
		});

		cancelButton.onClick.AddListener(() => UnityEngine.Object.Destroy(overlay));
		passwordField.ActivateInputField();
	}

	private static bool Validate(string email, string password)
	{
		if (!ValidateEmail(email)) return false;

		if (string.IsNullOrEmpty(password) || password.Length < 6)
		{
			ToastManager.ShowToast("密码至少需要 6 位");
			return false;
		}

		return true;
	}

	private static bool ValidateEmail(string email)
	{
		email = (email ?? string.Empty).Trim();
		if (string.IsNullOrEmpty(email) || !email.Contains("@") || !email.Contains("."))
		{
			ToastManager.ShowToast("请输入有效邮箱");
			return false;
		}

		return true;
	}

	private static Transform ResolveOverlayParent(Transform anchor)
	{
		Canvas canvas = anchor != null ? anchor.GetComponentInParent<Canvas>() : null;
		return canvas != null ? canvas.transform : anchor;
	}

	private static GameObject CreateOverlay(Transform parent)
	{
		GameObject overlay = new GameObject("LoginEmailAuthOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		overlay.transform.SetParent(parent, false);
		RectTransform rect = overlay.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		Image dim = overlay.GetComponent<Image>();
		dim.color = new Color(0f, 0f, 0f, 0.64f);
		dim.raycastTarget = true;

		Button blocker = overlay.AddComponent<Button>();
		blocker.transition = Selectable.Transition.None;
		blocker.targetGraphic = dim;
		blocker.onClick.AddListener(() => UnityEngine.Object.Destroy(overlay));
		return overlay;
	}

	private static GameObject CreatePanel(RectTransform root)
	{
		GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		panel.transform.SetParent(root, false);
		RectTransform rect = panel.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(560f, 500f);
		rect.anchoredPosition = Vector2.zero;

		Image image = panel.GetComponent<Image>();
		image.color = new Color(0.12f, 0.08f, 0.16f, 0.98f);
		image.raycastTarget = true;
		return panel;
	}

	private static TMP_Text CreateText(Transform parent, string value, int size, FontStyles style, Color color)
	{
		GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
		go.transform.SetParent(parent, false);
		TMP_Text text = go.GetComponent<TMP_Text>();
		text.font = TMP_Settings.defaultFontAsset;
		text.fontSize = size;
		text.fontStyle = style;
		text.color = color;
		text.text = value ?? string.Empty;
		text.enableWordWrapping = true;
		text.overflowMode = TextOverflowModes.Overflow;
		text.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, Mathf.Max(size * 2.1f, 42f));
		return text;
	}

	private static TMP_InputField CreateInput(Transform parent, string placeholder, bool password)
	{
		GameObject go = new GameObject(placeholder, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.sizeDelta = new Vector2(0f, 54f);

		Image image = go.GetComponent<Image>();
		image.color = new Color(0.06f, 0.04f, 0.10f, 1f);

		TMP_InputField input = go.GetComponent<TMP_InputField>();
		input.targetGraphic = image;
		input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
		input.textViewport = rect;
		input.fontAsset = TMP_Settings.defaultFontAsset;

		TMP_Text text = CreateText(go.transform, string.Empty, 20, FontStyles.Normal, Color.white);
		text.alignment = TextAlignmentOptions.Left;
		RectTransform textRect = text.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = new Vector2(18f, 0f);
		textRect.offsetMax = new Vector2(-18f, 0f);
		input.textComponent = text;
		input.pointSize = 20;

		TMP_Text placeholderText = CreateText(go.transform, placeholder, 18, FontStyles.Italic, new Color(0.55f, 0.50f, 0.62f, 1f));
		placeholderText.alignment = TextAlignmentOptions.Left;
		RectTransform placeholderRect = placeholderText.GetComponent<RectTransform>();
		placeholderRect.anchorMin = Vector2.zero;
		placeholderRect.anchorMax = Vector2.one;
		placeholderRect.offsetMin = new Vector2(18f, 0f);
		placeholderRect.offsetMax = new Vector2(-18f, 0f);
		input.placeholder = placeholderText;

		return input;
	}

	private static Button CreateButton(Transform parent, string label, bool secondary)
	{
		GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.sizeDelta = new Vector2(0f, 50f);
		go.GetComponent<LayoutElement>().preferredHeight = 50f;

		Image image = go.GetComponent<Image>();
		image.color = secondary ? new Color(0.22f, 0.16f, 0.28f, 1f) : new Color(0.35f, 0.08f, 0.58f, 1f);

		Button button = go.GetComponent<Button>();
		button.targetGraphic = image;

		TMP_Text text = CreateText(go.transform, label, 20, FontStyles.Bold, Color.white);
		text.alignment = TextAlignmentOptions.Center;
		RectTransform textRect = text.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = Vector2.zero;
		textRect.offsetMax = Vector2.zero;
		return button;
	}
}
