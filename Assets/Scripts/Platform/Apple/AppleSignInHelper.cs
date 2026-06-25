using System;
using UnityEngine;
using System.Runtime.InteropServices;
using XFGameFrameWork;

/// <summary>
/// Apple 登录辅助类
/// 封装 Sign In With Apple 的调用流程
///
/// 依赖：
/// iOS 13+ 真机:
///   1. FariAppleSignIn.mm（Assets/Plugins/iOS/FariAppleSignIn.mm）— 原生插件，无需 CocoaPod
///   2. Xcode Capability 中启用 "Sign In With Apple"
///   3. Apple Developer Console 中 App ID 已启用 Sign In With Apple
/// 共用:
///   4. Firebase Auth SDK
///   5. Firebase 控制台启用 Apple 登录提供商
///
/// 登录流程：
/// 1. 生成随机 Nonce + SHA256 哈希（安全验证用）
/// 2. iOS → P/Invoke _fariAppleStartSignIn → ASAuthorizationController
/// 3. 用户 Face ID / Touch ID / 密码验证
/// 4. 原生插件通过 UnitySendMessage 回调 "SUCCESS:idToken|authCode|fullName|email"
/// 5. C# 用 idToken + rawNonce → OAuthProvider.GetCredential("apple.com", ...) → Firebase
///
/// 回调节奏（同 GoogleSignInHelper）：
///   SUCCESS:<idToken>|<authCode>|<fullName>|<email>
///   FAILURE:<code>:<message>
///   CANCELED
/// </summary>
public class AppleSignInHelper : MonoSingleton<AppleSignInHelper>
{
    #region 枚举与配置

    /// <summary>登录状态</summary>
    public enum SignInState
    {
        Idle,
        SigningIn,
    }

    [Header("Apple Sign-In 配置")]
    [Tooltip("是否使用 Nonce 进行安全验证（Firebase 要求）")]
    public bool UseNonce = true;

    [Header("Editor 模拟设置")]
    [Tooltip("Editor 模拟延迟（秒）")]
    public float EditorSimulateDelay = 1f;

    #endregion

    #region 状态

    /// <summary>当前登录状态</summary>
    public SignInState State { get; private set; } = SignInState.Idle;

    /// <summary>是否正在登录中</summary>
    public bool IsSigningIn => State == SignInState.SigningIn;

    /// <summary>本次登录使用的原始 nonce（Firebase 验证时需要）</summary>
    public string CurrentRawNonce { get; private set; }

    #endregion

    #region 回调

    private Action<string, string, string> _onSuccess; // idToken, authCode, rawNonce
    private Action<string> _onError;

    #endregion

    #region iOS 原生接口 (P/Invoke)

#if UNITY_IOS && !UNITY_EDITOR
    /// <summary>检查设备是否支持 Apple Sign-In（iOS 13+）</summary>
    [DllImport("__Internal")]
    private static extern string _fariAppleIsSupported();

    /// <summary>启动 Apple Sign-In</summary>
    /// <param name="gameObjectName">C# GameObject 名称</param>
    /// <param name="callbackMethod">C# 回调方法名</param>
    /// <param name="sha256Nonce">SHA256 哈希后的 nonce（可选）</param>
    [DllImport("__Internal")]
    private static extern void _fariAppleStartSignIn(string gameObjectName, string callbackMethod, string sha256Nonce);
#endif

    #endregion

    #region 公开方法

    /// <summary>
    /// 发起 Apple 登录
    /// </summary>
    /// <param name="onSuccess">成功回调 (idToken, authCode, rawNonce)</param>
    /// <param name="onError">失败回调 (错误信息)</param>
    public void SignIn(Action<string, string, string> onSuccess, Action<string> onError)
    {
        if (IsSigningIn)
        {
            onError?.Invoke("正在登录中，请勿重复操作");
            return;
        }

        State = SignInState.SigningIn;
        _onSuccess = onSuccess;
        _onError = onError;

#if UNITY_IOS && !UNITY_EDITOR
        // iOS 真机 → 原生插件
        StartAppleSignInNative();
#elif UNITY_EDITOR
        // Editor → 模拟模式
        Debug.Log("[AppleSignInHelper] Editor 模拟模式");
        StartCoroutine(SimulateAppleSignInCoroutine());
#else
        FinishWithError("Apple 登录仅支持 iOS 设备");
#endif
    }

    /// <summary>
    /// 检查当前设备是否支持 Apple Sign-In
    /// </summary>
    public bool IsSupported()
    {
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            string result = _fariAppleIsSupported();
            return result == "1";
        }
        catch
        {
            return false;
        }
#elif UNITY_EDITOR
        // Editor 下模拟支持
        return true;
#else
        return false;
#endif
    }

    #endregion

    #region iOS 原生实现

#if UNITY_IOS && !UNITY_EDITOR

    /// <summary>
    /// 在 iOS 设备上通过原生插件发起 Apple Sign-In
    /// </summary>
    private void StartAppleSignInNative()
    {
        try
        {
            // 生成 Nonce 用于 Firebase 安全验证
            CurrentRawNonce = null;
            string sha256Nonce = "";

            if (UseNonce)
            {
                CurrentRawNonce = GenerateNonce();
                sha256Nonce = SHA256Hash(CurrentRawNonce);
                Debug.Log($"[AppleSignInHelper] 已生成 Nonce: SHA256={sha256Nonce.Substring(0, 16)}...");
            }

            _fariAppleStartSignIn(gameObject.name, "OnNativeSignInResult", sha256Nonce);
            Debug.Log("[AppleSignInHelper] iOS: 已启动 Apple Sign-In，等待用户验证...");
        }
        catch (Exception e)
        {
            State = SignInState.Idle;
            Debug.LogError($"[AppleSignInHelper] Apple Sign-In 调用异常: {e.Message}");
            _onError?.Invoke($"Apple Sign-In 启动失败: {e.Message}");
        }
    }

#endif

    #endregion

    #region 原生回调处理

    /// <summary>
    /// 原生插件回调入口（FariAppleSignIn.mm → UnitySendMessage）
    ///
    /// result 格式：
    ///   成功: "SUCCESS:idToken|authCode|fullName|email"
    ///   失败: "FAILURE:<code>:<message>"
    ///   取消: "CANCELED"
    /// </summary>
    private void OnNativeSignInResult(string result)
    {
        Debug.Log($"[AppleSignInHelper] 收到原生插件回调: {(result?.Length > 80 ? result.Substring(0, 80) + "..." : result)}");

        if (string.IsNullOrEmpty(result))
        {
            FinishWithError("原生插件返回空结果");
            return;
        }

        if (result.StartsWith("SUCCESS:"))
        {
            // 格式: SUCCESS:idToken|authCode|fullName|email
            string payload = result.Substring(8);
            string[] parts = payload.Split('|');

            string idToken = parts.Length > 0 ? parts[0] : "";
            string authCode = parts.Length > 1 ? parts[1] : "";
            string fullName = parts.Length > 2 ? parts[2] : "";
            string email = parts.Length > 3 ? parts[3] : "";

            if (string.IsNullOrEmpty(idToken))
            {
                FinishWithError("Apple ID Token 为空");
                return;
            }

            if (!string.IsNullOrEmpty(fullName))
            {
                Debug.Log($"[AppleSignInHelper] Apple 用户: {fullName}, {email}");
            }

            FinishWithSuccess(idToken, authCode);
        }
        else if (result == "CANCELED")
        {
            FinishWithError("用户取消了 Apple 登录");
        }
        else if (result.StartsWith("FAILURE:"))
        {
            string errorDetail = result.Substring(8);
            FinishWithError($"Apple 登录失败: {errorDetail}");
        }
        else
        {
            FinishWithError($"未知的登录结果格式: {result}");
        }
    }

    #endregion

    #region 结果处理

    /// <summary>
    /// 登录成功
    /// </summary>
    private void FinishWithSuccess(string idToken, string authCode)
    {
        State = SignInState.Idle;
        Debug.Log("[AppleSignInHelper] Apple 登录成功，已获取 ID Token");
        _onSuccess?.Invoke(idToken, authCode, CurrentRawNonce ?? "");
        ClearPendingCallbacks();
    }

    /// <summary>
    /// 登录失败
    /// </summary>
    private void FinishWithError(string message)
    {
        State = SignInState.Idle;
        Debug.LogError($"[AppleSignInHelper] {message}");
        _onError?.Invoke(message);
        ClearPendingCallbacks();
    }

    private void ClearPendingCallbacks()
    {
        _onSuccess = null;
        _onError = null;
        CurrentRawNonce = null;
    }

    #endregion

    #region Editor 模拟

    /// <summary>
    /// Editor 模拟 Apple Sign-In（非 iOS 平台 / 开发调试用）
    /// </summary>
    private System.Collections.IEnumerator SimulateAppleSignInCoroutine()
    {
        Debug.Log("[AppleSignInHelper] 模拟 Apple 登录中...");
        yield return new WaitForSeconds(EditorSimulateDelay);

        string mockIdToken = "mock_apple_id_token_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string mockAuthCode = "mock_apple_auth_code_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string mockNonce = UseNonce ? GenerateNonce() : "";

        State = SignInState.Idle;
        CurrentRawNonce = mockNonce;
        Debug.Log($"[AppleSignInHelper] Editor 模拟 Apple 登录成功");
        _onSuccess?.Invoke(mockIdToken, mockAuthCode, mockNonce);
        ClearPendingCallbacks();
    }

    #endregion

    #region 工具方法 — Nonce 生成与 SHA256

    /// <summary>
    /// 生成随机 Nonce（用于 Apple Sign-In 安全验证）
    /// 随机生成 32 位十六进制字符串
    /// </summary>
    public static string GenerateNonce(int length = 32)
    {
        const string chars = "0123456789abcdef";
        var random = new System.Random();
        char[] result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[random.Next(chars.Length)];
        return new string(result);
    }

    /// <summary>
    /// SHA256 哈希（用于 Nonce 的 Apple Sign-In 格式要求）
    /// Apple 要求传入的是 SHA256(nonce)，Firebase 验证时传入原始 nonce
    /// </summary>
    public static string SHA256Hash(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
            byte[] hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

    #endregion

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);
    }

    #endregion
}
