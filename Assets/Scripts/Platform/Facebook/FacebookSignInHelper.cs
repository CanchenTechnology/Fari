using System;
using UnityEngine;
using System.Collections;
using XFGameFrameWork;
/// <summary>
/// Facebook 登录辅助类
/// 封装 Facebook SDK 的登录调用流程
/// 
/// 依赖：
/// 1. Facebook SDK for Unity（https://developers.facebook.com/docs/unity/）
/// 2. 在 Firebase 控制台启用 Facebook 登录提供商
/// 3. 在 Facebook Developer Console 创建应用并获取 App ID 和 App Secret
/// 
/// 配置步骤：
/// 1. 下载 Facebook SDK for Unity: https://developers.facebook.com/docs/unity/downloads/
/// 2. 导入 .unitypackage 到 Unity 项目
/// 3. 在 Facebook Developer Console 创建应用
/// 4. 获取 App ID 和 App Secret
/// 5. 在 Firebase Console → Authentication → Sign-in method → Facebook 中填入 App ID 和 App Secret
/// 6. 将 Firebase 提供的 OAuth Redirect URI 添加到 Facebook 应用设置
/// 7. 在 Unity 的 Facebook Settings 中填入 App ID
/// </summary>
public class FacebookSignInHelper : MonoSingleton<FacebookSignInHelper>
{
    #region 配置

    [Header("Facebook SDK 配置")]
    [Tooltip("Facebook App ID")]
    public string FacebookAppId = "";

    [Tooltip("登录时请求的权限列表")]
    public string[] Permissions = new string[] { "public_profile", "email" };

    #endregion

    #region 状态

    /// <summary>是否正在登录中</summary>
    public bool IsSigningIn { get; private set; } = false;

    /// <summary>Facebook SDK 是否已初始化</summary>
    public bool IsSDKInitialized { get; private set; } = false;

    #endregion

    #region 回调

    private Action<string> _onSuccess; // accessToken
    private Action<string> _onError;

    #endregion

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();
        InitializeFacebookSDK();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化 Facebook SDK
    /// </summary>
    private void InitializeFacebookSDK()
    {
        try
        {
            var fbType = System.Type.GetType("FB, Facebook.Unity");
            if (fbType != null)
            {
                var initMethod = fbType.GetMethod("Init", new Type[] { typeof(Action) });
                if (initMethod != null)
                {
                    initMethod.Invoke(null, new object[] { (Action)OnFacebookInitComplete });
                    Debug.Log("[FacebookSignInHelper] Facebook SDK 初始化中...");
                    return;
                }
            }

            Debug.LogWarning("[FacebookSignInHelper] Facebook SDK 未安装，登录功能将不可用");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FacebookSignInHelper] Facebook SDK 初始化异常: {e.Message}");
        }
    }

    /// <summary>
    /// Facebook SDK 初始化完成回调
    /// </summary>
    private void OnFacebookInitComplete()
    {
        IsSDKInitialized = true;
        Debug.Log("[FacebookSignInHelper] Facebook SDK 初始化完成");

        // 检查是否已有登录状态
        try
        {
            var fbType = System.Type.GetType("FB, Facebook.Unity");
            if (fbType != null)
            {
                var isLoggedInProp = fbType.GetProperty("IsLoggedIn");
                if (isLoggedInProp != null)
                {
                    bool isLoggedIn = (bool)isLoggedInProp.GetValue(null);
                    if (isLoggedIn)
                    {
                        Debug.Log("[FacebookSignInHelper] 检测到已登录的 Facebook 账号");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FacebookSignInHelper] 检查登录状态异常: {e.Message}");
        }
    }

    #endregion

    #region 公开方法

    /// <summary>
    /// 发起 Facebook 登录
    /// </summary>
    /// <param name="onSuccess">登录成功回调，参数为 Access Token</param>
    /// <param name="onError">登录失败回调，参数为错误信息</param>
    public void SignIn(Action<string> onSuccess, Action<string> onError)
    {
        if (IsSigningIn)
        {
            onError?.Invoke("正在登录中，请勿重复操作");
            return;
        }

        IsSigningIn = true;
        _onSuccess = onSuccess;
        _onError = onError;

        try
        {
            StartFacebookSignIn();
        }
        catch (Exception e)
        {
            IsSigningIn = false;
            Debug.LogError($"[FacebookSignInHelper] Facebook Sign-In 调用异常: {e.Message}");
            _onError?.Invoke($"Facebook 登录异常: {e.Message}");
        }
    }

    /// <summary>
    /// 登出 Facebook 账号
    /// </summary>
    public void SignOut()
    {
        try
        {
            var fbType = System.Type.GetType("FB, Facebook.Unity");
            if (fbType != null)
            {
                var logOutMethod = fbType.GetMethod("LogOut");
                logOutMethod?.Invoke(null, null);
                Debug.Log("[FacebookSignInHelper] Facebook 已登出");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FacebookSignInHelper] Facebook 登出异常: {e.Message}");
        }
    }

    /// <summary>
    /// 检查是否已安装 Facebook SDK
    /// </summary>
    public bool IsSDKAvailable()
    {
        return System.Type.GetType("FB, Facebook.Unity") != null;
    }

    #endregion

    #region 内部方法

    /// <summary>
    /// 启动 Facebook 登录流程
    /// 使用反射调用以避免编译期依赖
    /// </summary>
    private void StartFacebookSignIn()
    {
        var fbType = System.Type.GetType("FB, Facebook.Unity");
        if (fbType == null)
        {
            // Facebook SDK 未安装 → Editor 模拟模式
            Debug.Log("[FacebookSignInHelper] Facebook SDK 未安装，使用 Editor 模拟模式");
            SimulateFacebookSignIn();
            return;
        }

        // 检查 SDK 是否已初始化
        if (!IsSDKInitialized)
        {
            // 尝试先初始化
            InitializeFacebookSDK();

            if (!IsSDKInitialized)
            {
                IsSigningIn = false;
                _onError?.Invoke("Facebook SDK 尚未初始化完成");
                return;
            }
        }

        // 调用 FB.LogInWithReadPermissions
        var logInMethod = fbType.GetMethod("LogInWithReadPermissions",
            new Type[] { typeof(string[]), typeof(Action<>) });

        if (logInMethod != null)
        {
            // 构造回调类型
            var iLoginResultType = System.Type.GetType("Facebook.Unity.ILoginResult, Facebook.Unity");
            if (iLoginResultType != null)
            {
                var actionType = typeof(Action<>).MakeGenericType(iLoginResultType);
                var callback = Delegate.CreateDelegate(actionType, this, nameof(OnFacebookLoginResult));
                logInMethod.Invoke(null, new object[] { Permissions, callback });
                Debug.Log("[FacebookSignInHelper] 已发起 Facebook 登录请求...");
            }
            else
            {
                IsSigningIn = false;
                _onError?.Invoke("Facebook SDK 类型加载失败");
            }
        }
        else
        {
            IsSigningIn = false;
            _onError?.Invoke("Facebook SDK 方法未找到，请检查 SDK 版本");
        }
    }

    /// <summary>
    /// Facebook 登录结果回调（由反射调用）
    /// </summary>
    private void OnFacebookLoginResult(object result)
    {
        try
        {
            if (result == null)
            {
                IsSigningIn = false;
                _onError?.Invoke("Facebook 登录返回空结果");
                return;
            }

            // 检查是否取消
            var cancelledProp = result.GetType().GetProperty("Cancelled");
            if (cancelledProp != null && (bool)cancelledProp.GetValue(result))
            {
                IsSigningIn = false;
                _onError?.Invoke("用户取消了 Facebook 登录");
                return;
            }

            // 检查错误
            var errorProp = result.GetType().GetProperty("Error");
            if (errorProp != null)
            {
                var error = errorProp.GetValue(result);
                if (error != null)
                {
                    IsSigningIn = false;
                    _onError?.Invoke($"Facebook 登录错误: {error}");
                    return;
                }
            }

            // 获取 Access Token
            var accessTokenType = System.Type.GetType("Facebook.Unity.AccessToken, Facebook.Unity");
            if (accessTokenType != null)
            {
                var currentAccessTokenProp = accessTokenType.GetProperty("CurrentAccessToken");
                if (currentAccessTokenProp != null)
                {
                    var accessToken = currentAccessTokenProp.GetValue(null);
                    if (accessToken != null)
                    {
                        var tokenStringProp = accessToken.GetType().GetProperty("TokenString");
                        if (tokenStringProp != null)
                        {
                            string tokenString = tokenStringProp.GetValue(accessToken) as string;
                            IsSigningIn = false;
                            Debug.Log("[FacebookSignInHelper] Facebook 登录成功，已获取 Access Token");
                            _onSuccess?.Invoke(tokenString);
                            return;
                        }
                    }
                }
            }

            IsSigningIn = false;
            _onError?.Invoke("无法获取 Facebook Access Token");
        }
        catch (Exception e)
        {
            IsSigningIn = false;
            _onError?.Invoke($"Facebook 登录结果处理异常: {e.Message}");
        }
    }

    #endregion

    #region Editor 模拟

    /// <summary>
    /// Editor 模拟 Facebook 登录（SDK 未安装或非真机环境时自动使用）
    /// </summary>
    private void SimulateFacebookSignIn()
    {
        StartCoroutine(SimulateFacebookSignInCoroutine());
    }

    private System.Collections.IEnumerator SimulateFacebookSignInCoroutine()
    {
        // 模拟网络延迟
        yield return new WaitForSeconds(1.0f);

        IsSigningIn = false;

        string mockToken = $"mock_fb_access_token_{System.DateTime.Now.Ticks}_editor_simulation";

        Debug.Log($"[FacebookSignInHelper] ✅ Editor 模拟登录成功！（假 Token 仅用于开发调试）");
        Debug.Log($"[FacebookSignInHelper] 提示：安装 Facebook SDK for Unity 后在真机上会调用真实 SDK");

        _onSuccess?.Invoke(mockToken);
    }

    #endregion

    #region SDK 回调处理（手动调用方式）

    /// <summary>
    /// Facebook 登录成功回调
    /// 
    /// 如果使用 Facebook SDK 的标准回调方式，可以在回调中手动调用此方法
    /// 
    /// 示例：
    /// <code>
    /// private void OnFacebookLoginResult(ILoginResult result)
    /// {
    ///     if (FB.IsLoggedIn && result.AccessToken != null)
    ///     {
    ///         string accessToken = result.AccessToken.TokenString;
    ///         FacebookSignInHelper.Instance.OnSignInSuccess(accessToken);
    ///     }
    ///     else
    ///     {
    ///         FacebookSignInHelper.Instance.OnSignInError(result.Error ?? "登录取消");
    ///     }
    /// }
    /// </code>
    /// </summary>
    public void OnSignInSuccess(string accessToken)
    {
        IsSigningIn = false;
        Debug.Log("[FacebookSignInHelper] Facebook 登录成功，已获取 Access Token");
        _onSuccess?.Invoke(accessToken);
    }

    /// <summary>
    /// Facebook 登录失败回调
    /// </summary>
    public void OnSignInError(string error)
    {
        IsSigningIn = false;
        Debug.LogError($"[FacebookSignInHelper] Facebook 登录失败: {error}");
        _onError?.Invoke(error);
    }

    #endregion
}
