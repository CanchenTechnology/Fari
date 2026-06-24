/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 11:09:29
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using Firebase.Auth;
using Firebase.Extensions;
using System;

public class VerifyPhoneUI : WindowBase
{
	public VerifyPhoneUIComponent uiComponent;
	private readonly string[] countryCodes = { "+1", "+86", "+44", "+81" };
	private int countryCodeIndex = 0;
	private const uint SmsTimeoutMilliseconds = 60000u;
#if !UNITY_EDITOR
	private string verificationId = string.Empty;
	private string lastRequestedPhoneNumber = string.Empty;
	private ForceResendingToken forceResendingToken;
	private bool isSendingCode;
	private bool isUpdatingCredential;
#endif

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<VerifyPhoneUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		RefreshCountryCodeButton();
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
	public void OnCountryCodeButtonClick()
	{
		countryCodeIndex = (countryCodeIndex + 1) % countryCodes.Length;
		RefreshCountryCodeButton();
	}
	public void OnPhoneNumberInputChange(string text)
	{
	}
	public void OnPhoneNumberInputEnd(string text)
	{
		if (uiComponent?.PhoneNumberInputField != null)
			uiComponent.PhoneNumberInputField.text = RegistrationFlowUtility.NormalizePhoneNumber(text);
	}
	public void OnResendCodeButtonClick()
	{
		StartPhoneVerification(true);
	}
	public void OnVerificationCodeInputChange(string text)
	{
	}
	public void OnVerificationCodeInputEnd(string text)
	{
		if (uiComponent?.VerificationCodeInputField != null)
			uiComponent.VerificationCodeInputField.text = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
	}
	public void OnVerifyButtonClick()
	{
		string fullPhone = GetFullPhoneNumber();
		string code = uiComponent?.VerificationCodeInputField != null
			? uiComponent.VerificationCodeInputField.text
			: string.Empty;

		if (string.IsNullOrWhiteSpace(fullPhone))
		{
			UIModule.Instance.PopUpWindow<SetNameAndAvatarUI>();
			return;
		}

		if (!RegistrationFlowUtility.IsPlausiblePhoneNumber(fullPhone))
		{
			ToastManager.ShowToast("请输入有效手机号");
			return;
		}

		if (string.IsNullOrWhiteSpace(code))
		{
			StartPhoneVerification(false);
			return;
		}

		if (!RegistrationFlowUtility.IsPlausibleVerificationCode(code))
		{
			ToastManager.ShowToast("验证码格式无效");
			return;
		}

#if UNITY_EDITOR
		SubmitVerificationCode(code.Trim());
#else
		if (string.IsNullOrWhiteSpace(verificationId) || lastRequestedPhoneNumber != fullPhone)
		{
			ToastManager.ShowToast("请先获取验证码");
			StartPhoneVerification(false);
			return;
		}

		SubmitVerificationCode(code.Trim());
#endif
	}

	private void StartPhoneVerification(bool forceResend)
	{
		string fullPhone = GetFullPhoneNumber();
		if (!RegistrationFlowUtility.IsPlausiblePhoneNumber(fullPhone))
		{
			ToastManager.ShowToast("请输入有效手机号");
			return;
		}

#if UNITY_EDITOR
		RegistrationFlowUtility.SavePendingPhone(GetCountryCode(), fullPhone, "pending_device_verification");
		ToastManager.ShowToast("请在 Android/iOS 真机上完成短信验证");
		return;
#else
		if (isSendingCode)
		{
			ToastManager.ShowToast("验证码发送中，请稍候");
			return;
		}

		FirebaseAuth auth = GetReadyAuth();
		if (auth == null) return;

		FirebaseUser user = auth.CurrentUser;
		if (user == null)
		{
			RegistrationFlowUtility.SavePendingPhone(GetCountryCode(), fullPhone, "pending_login_phone_verification");
			ToastManager.ShowToast("请先登录后验证手机号");
			return;
		}

		try
		{
			isSendingCode = true;
			lastRequestedPhoneNumber = fullPhone;
			RegistrationFlowUtility.SavePendingPhone(GetCountryCode(), fullPhone, forceResend ? "verification_resend_requested" : "verification_requested");

			PhoneAuthOptions options = new PhoneAuthOptions
			{
				PhoneNumber = fullPhone,
				TimeoutInMilliseconds = SmsTimeoutMilliseconds
			};

			if (forceResend && forceResendingToken != null)
				options.ForceResendingToken = forceResendingToken;

			PhoneAuthProvider.GetInstance(auth).VerifyPhoneNumber(
				options,
				OnPhoneVerificationCompleted,
				OnPhoneVerificationFailed,
				OnPhoneCodeSent,
				OnPhoneAutoRetrievalTimeout);
			ToastManager.ShowToast("正在发送验证码");
		}
		catch (Exception ex)
		{
			isSendingCode = false;
			RegistrationFlowUtility.SavePendingPhone(GetCountryCode(), fullPhone, "verification_request_failed");
			Debug.LogError("[VerifyPhoneUI] 发送短信验证码失败: " + ex.Message);
			ToastManager.ShowToast("验证码发送失败，请稍后重试");
		}
#endif
	}

	private void SubmitVerificationCode(string code)
	{
#if UNITY_EDITOR
		RegistrationFlowUtility.SavePendingPhone(GetCountryCode(), GetFullPhoneNumber(), "pending_device_verification");
		ToastManager.ShowToast("请在 Android/iOS 真机上完成短信验证");
#else
		if (isUpdatingCredential)
		{
			ToastManager.ShowToast("正在验证，请稍候");
			return;
		}

		FirebaseAuth auth = GetReadyAuth();
		if (auth == null) return;

		try
		{
			PhoneAuthCredential credential = PhoneAuthProvider.GetInstance(auth).GetCredential(verificationId, code);
			ApplyPhoneCredential(credential);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[VerifyPhoneUI] 验证码凭证创建失败: " + ex.Message);
			ToastManager.ShowToast("验证码无效或已过期");
		}
#endif
	}

#if !UNITY_EDITOR
	private FirebaseAuth GetReadyAuth()
	{
		FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
		if (authManager == null || !authManager.IsFirebaseInitialized)
		{
			ToastManager.ShowToast("Firebase 初始化中，请稍后重试");
			return null;
		}

		return FirebaseAuth.DefaultInstance;
	}

	private void OnPhoneCodeSent(string newVerificationId, ForceResendingToken newForceResendingToken)
	{
		isSendingCode = false;
		verificationId = newVerificationId ?? string.Empty;
		forceResendingToken = newForceResendingToken;
		RegistrationFlowUtility.SavePendingPhone(GetCountryCode(), lastRequestedPhoneNumber, "code_sent");
		ToastManager.ShowToast("验证码已发送");
	}

	private void OnPhoneVerificationCompleted(PhoneAuthCredential credential)
	{
		isSendingCode = false;
		if (credential == null)
		{
			ToastManager.ShowToast("自动验证失败，请输入验证码");
			return;
		}

		ApplyPhoneCredential(credential);
	}

	private void OnPhoneVerificationFailed(string error)
	{
		isSendingCode = false;
		RegistrationFlowUtility.SavePendingPhone(GetCountryCode(), GetFullPhoneNumber(), "verification_failed");
		Debug.LogWarning("[VerifyPhoneUI] 手机号验证失败: " + error);
		ToastManager.ShowToast(string.IsNullOrWhiteSpace(error) ? "手机号验证失败" : error);
	}

	private void OnPhoneAutoRetrievalTimeout(string timeoutVerificationId)
	{
		isSendingCode = false;
		if (!string.IsNullOrWhiteSpace(timeoutVerificationId))
			verificationId = timeoutVerificationId;

		RegistrationFlowUtility.SavePendingPhone(GetCountryCode(), lastRequestedPhoneNumber, "code_auto_retrieval_timeout");
		ToastManager.ShowToast("验证码已发送，请手动输入");
	}

	private void ApplyPhoneCredential(PhoneAuthCredential credential)
	{
		FirebaseAuth auth = GetReadyAuth();
		if (auth == null) return;

		FirebaseUser user = auth.CurrentUser;
		if (user == null)
		{
			RegistrationFlowUtility.SavePendingPhone(GetCountryCode(), GetFullPhoneNumber(), "pending_login_phone_verification");
			ToastManager.ShowToast("请先登录后验证手机号");
			return;
		}

		isUpdatingCredential = true;
		RegistrationFlowUtility.SavePendingPhone(GetCountryCode(), GetFullPhoneNumber(), "credential_verification_started");
		user.UpdatePhoneNumberCredentialAsync(credential).ContinueWithOnMainThread(task =>
		{
			isUpdatingCredential = false;
			if (task.IsFaulted || task.IsCanceled)
			{
				string error = task.Exception?.Flatten().InnerException?.Message ?? "手机号验证失败";
				Debug.LogWarning("[VerifyPhoneUI] 手机号绑定失败: " + error);
				RegistrationFlowUtility.SavePendingPhone(GetCountryCode(), GetFullPhoneNumber(), "credential_update_failed");
				ToastManager.ShowToast(error);
				return;
			}

			string verifiedPhone = !string.IsNullOrWhiteSpace(task.Result?.PhoneNumber)
				? task.Result.PhoneNumber
				: GetFullPhoneNumber();
			RegistrationFlowUtility.SavePendingPhone(GetCountryCode(), verifiedPhone, "verified");
			ToastManager.ShowToast("手机号验证成功");
			UIModule.Instance.PopUpWindow<SetNameAndAvatarUI>();
		});
	}
#endif

	private string GetCountryCode()
	{
		return countryCodes[Mathf.Clamp(countryCodeIndex, 0, countryCodes.Length - 1)];
	}

	private string GetPhoneNumber()
	{
		return RegistrationFlowUtility.NormalizePhoneNumber(uiComponent?.PhoneNumberInputField != null
			? uiComponent.PhoneNumberInputField.text
			: string.Empty);
	}

	private string GetFullPhoneNumber()
	{
		string phone = GetPhoneNumber();
		if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
		return phone.StartsWith("+", StringComparison.Ordinal) ? phone : GetCountryCode() + phone;
	}

	private void RefreshCountryCodeButton()
	{
		RegistrationFlowUtility.SetButtonText(uiComponent?.CountryCodeButton, GetCountryCode());
	}
	#endregion
}
