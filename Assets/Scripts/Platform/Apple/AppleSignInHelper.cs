using System;
using UnityEngine;
using System.Collections;
using XFGameFrameWork;
/// <summary>
/// Apple 登录辅助类
/// 封装 Sign In With Apple 的调用流程
/// 
/// 依赖：
/// 1. Unity 2019.4+ 内置 Apple Sign-In 支持（iOS 13+）
/// 2. 或使用插件：https://github.com/lupidan/apple-signin-unity
/// 3. 在 Firebase 控制台启用 Apple 登录提供商
/// 4. 在 Apple Developer Console 配置 Sign In With Apple 能力
/// 
/// 配置步骤：
/// 1. Apple Developer Console → Certificates, Identifiers & Profiles
/// 2. 在 App ID 中启用 "Sign In With Apple" 能力
/// 3. 在 Firebase Console → Authentication → Sign-in method 中启用 Apple
/// 4. 下载 Apple 的 Services ID 配置并上传到 Firebase
/// 5. iOS 构建设置中确保 Capability 包含 "Sign In With Apple"
/// </summary>
public class AppleSignInHelper : MonoSingleton<AppleSignInHelper>
{
    #region 配置

    [Header("Apple Sign-In 配置")]
    [Tooltip("是否在登录时请求全名")]
    public bool RequestFullName = true;

    [Tooltip("是否在登录时请求邮箱")]
    public bool RequestEmail = true;

    #endregion

    #region 状态

    /// <summary>是否正在登录中</summary>
    public bool IsSigningIn { get; private set; } = false;

    #endregion

    #region 回调

    private Action<string, string, string> _onSuccess; // idToken, authorizationCode, nonce
    private Action<string> _onError;

    #endregion

    #region 公开方法

    /// <summary>
    /// 发起 Apple 登录
    /// </summary>
    /// <param name="onSuccess">登录成功回调，参数为 (idToken, authorizationCode, nonce)</param>
    /// <param name="onError">登录失败回调，参数为错误信息</param>
    public void SignIn(Action<string, string, string> onSuccess, Action<string> onError)
    {
        if (IsSigningIn)
        {
            onError?.Invoke("正在登录中，请勿重复操作");
            return;
        }

        IsSigningIn = true;
        _onSuccess = onSuccess;
        _onError = onError;

        // Apple Sign-In 仅在 iOS 13+ / macOS 10.15+ 可用
#if UNITY_IOS && !UNITY_EDITOR
        StartAppleSignIn();
#else
        // 在编辑器或非 iOS 平台上，使用模拟登录
        Debug.LogWarning("[AppleSignInHelper] Apple Sign-In 仅在 iOS 设备上可用，当前使用模拟模式");
        SimulateAppleSignIn();
#endif
    }

    /// <summary>
    /// 检查当前平台是否支持 Apple Sign-In
    /// </summary>
    public bool IsSupported()
    {
#if UNITY_IOS && !UNITY_EDITOR
        return true;
#else
        return false;
#endif
    }

    #endregion

    #region iOS 原生实现

#if UNITY_IOS && !UNITY_EDITOR

    /// <summary>
    /// 在 iOS 设备上发起 Apple Sign-In
    /// 使用 Unity 内置的 Apple Sign-In API（需要 Unity 2019.4+）
    /// </summary>
    private void StartAppleSignIn()
    {
        try
        {
            // 使用 Unity 的 Apple Sign-In API（UnityEngine.SocialPlatforms 或第三方插件）
            // 这里使用苹果原生的 Sign In With Apple API
            
            // 方案1：使用 UnityEngine.SocialPlatforms（Unity 2019.4+ 内置支持）
            // UnityEngine.Social.Active.Authenticate() - 但这不直接返回 Apple ID Token
            
            // 方案2：使用第三方插件 apple-signin-unity 的反射调用
            StartAppleSignInWithPlugin();
        }
        catch (Exception e)
        {
            IsSigningIn = false;
            Debug.LogError($"[AppleSignInHelper] Apple Sign-In 调用异常: {e.Message}");
            _onError?.Invoke($"Apple Sign-In 异常: {e.Message}");
        }
    }

    /// <summary>
    /// 使用 apple-signin-unity 插件的反射调用
    /// </summary>
    private void StartAppleSignInWithPlugin()
    {
        // 尝试加载 Apple Sign-In 插件类型
        var appleAuthManagerType = System.Type.GetType("AppleAuth.AppleAuthManager, AppleAuth");
        
        if (appleAuthManagerType == null)
        {
            // 尝试使用 Unity 内置 API
            StartAppleSignInWithBuiltInAPI();
            return;
        }

        // 使用插件的 QuickLogin 或 LoginWithAppleId
        try
        {
            var loginWithAppleIdType = System.Type.GetType("AppleAuth.Enums.LoginOptions, AppleAuth");
            if (loginWithAppleIdType != null)
            {
                // 构造 LoginOptions
                int options = 0;
                if (RequestFullName) options |= 1; // IncludeFullName
                if (RequestEmail) options |= 2;     // IncludeEmail
                
                // 调用 AppleAuthManager.LoginWithAppleId
                Debug.Log("[AppleSignInHelper] 正在使用 apple-signin-unity 插件发起 Apple 登录...");
                
                // 由于反射调用复杂，实际使用时建议直接引用插件
                // 这里标记为需要手动集成
                IsSigningIn = false;
                _onError?.Invoke(
                    "Apple Sign-In 需要安装 apple-signin-unity 插件并在代码中直接调用。\n" +
                    "请参考: https://github.com/lupidan/apple-signin-unity\n" +
                    "安装后，在 AppleAuthManager 的回调中调用 AppleSignInHelper.Instance.OnSignInSuccess(idToken, authCode, nonce)"
                );
            }
        }
        catch (Exception e)
        {
            IsSigningIn = false;
            _onError?.Invoke($"Apple Sign-In 插件调用失败: {e.Message}");
        }
    }

    /// <summary>
    /// 使用 Unity 内置 API 进行 Apple Sign-In
    /// Unity 2022.1+ 支持 UnityEngine.Apple.SignIn
    /// </summary>
    private void StartAppleSignInWithBuiltInAPI()
    {
        try
        {
            var appleSignInType = System.Type.GetType("UnityEngine.Apple.SignIn.AppleSignIn, UnityEngine.Apple");
            if (appleSignInType != null)
            {
                Debug.Log("[AppleSignInHelper] 正在使用 Unity 内置 Apple Sign-In API...");
                
                // 调用 AppleSignIn.StartSignIn()
                var startSignInMethod = appleSignInType.GetMethod("StartSignIn");
                if (startSignInMethod != null)
                {
                    startSignInMethod.Invoke(null, null);
                    StartCoroutine(WaitForAppleSignInResult());
                    return;
                }
            }

            IsSigningIn = false;
            _onError?.Invoke(
                "未找到 Apple Sign-In API。请确保：\n" +
                "1. 使用 Unity 2019.4+ 并在 iOS 构建设置中启用 Sign In With Apple Capability\n" +
                "2. 或安装 apple-signin-unity 插件: https://github.com/lupidan/apple-signin-unity"
            );
        }
        catch (Exception e)
        {
            IsSigningIn = false;
            _onError?.Invoke($"Unity 内置 Apple Sign-In 调用失败: {e.Message}");
        }
    }

    /// <summary>
    /// 协程等待 Apple Sign-In 结果
    /// </summary>
    private IEnumerator WaitForAppleSignInResult()
    {
        float timeout = 60f;
        float elapsed = 0f;

        while (IsSigningIn && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= timeout)
        {
            IsSigningIn = false;
            _onError?.Invoke("Apple 登录超时");
        }
    }

#endif

    #endregion

    #region 编辑器模拟

    /// <summary>
    /// 在编辑器中模拟 Apple 登录
    /// </summary>
    private void SimulateAppleSignIn()
    {
        StartCoroutine(SimulateAppleSignInCoroutine());
    }

    private IEnumerator SimulateAppleSignInCoroutine()
    {
        Debug.Log("[AppleSignInHelper] 模拟 Apple 登录中...");
        yield return new WaitForSeconds(1f);

        string mockIdToken = "mock_apple_id_token_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
        string mockAuthCode = "mock_apple_auth_code_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
        string mockNonce = GenerateNonce();

        IsSigningIn = false;
        _onSuccess?.Invoke(mockIdToken, mockAuthCode, mockNonce);
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 生成随机 Nonce（用于 Apple Sign-In 安全验证）
    /// </summary>
    public static string GenerateNonce(int length = 32)
    {
        const string chars = "0123456789abcdef";
        System.Random random = new System.Random();
        char[] result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        return new string(result);
    }

    /// <summary>
    /// 生成 SHA256 哈希的 Nonce
    /// </summary>
    public static string GenerateSHA256Nonce()
    {
        string rawNonce = GenerateNonce();
        return SHA256Hash(rawNonce);
    }

    /// <summary>
    /// SHA256 哈希
    /// </summary>
    private static string SHA256Hash(string input)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input);
            byte[] hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

    #endregion

    #region SDK 回调处理（由 SDK 回调触发）

    /// <summary>
    /// Apple 登录成功回调
    /// 
    /// 使用方式：
    /// 在 Apple Sign-In SDK 的回调中，调用此方法并传入 Apple ID Token 和 Authorization Code
    /// 
    /// 示例（使用 apple-signin-unity 插件）：
    /// <code>
    /// appleAuthManager.LoginWithAppleId(
    ///     LoginOptions.IncludeEmail | LoginOptions.IncludeFullName,
    ///     credential =>
    ///     {
    ///         var appleIdCredential = credential as IAppleIDCredential;
    ///         if (appleIdCredential != null)
    ///         {
    ///             string idToken = System.Text.Encoding.UTF8.GetString(appleIdCredential.IdentityToken);
    ///             string authCode = System.Text.Encoding.UTF8.GetString(appleIdCredential.AuthorizationCode);
    ///             AppleSignInHelper.Instance.OnSignInSuccess(idToken, authCode, "");
    ///         }
    ///     },
    ///     error =>
    ///     {
    ///         AppleSignInHelper.Instance.OnSignInError(error.ToString());
    ///     }
    /// );
    /// </code>
    /// </summary>
    public void OnSignInSuccess(string idToken, string authorizationCode, string nonce)
    {
        IsSigningIn = false;
        Debug.Log("[AppleSignInHelper] Apple 登录成功，已获取 ID Token");
        _onSuccess?.Invoke(idToken, authorizationCode, nonce);
    }

    /// <summary>
    /// Apple 登录失败回调
    /// </summary>
    public void OnSignInError(string error)
    {
        IsSigningIn = false;
        Debug.LogError($"[AppleSignInHelper] Apple 登录失败: {error}");
        _onError?.Invoke(error);
    }

    #endregion
}
