using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using XFGameFrameWork;

/// <summary>
/// Facebook 登录辅助类
/// 封装 Facebook SDK 的登录调用流程
///
/// 依赖：
/// Android + iOS:
///   1. Facebook SDK for Unity（https://developers.facebook.com/docs/unity/downloads/）
///   2. Firebase 控制台启用 Facebook 登录提供商
///   3. Facebook Developer Console 创建应用并获取 App ID 和 App Secret
///
/// 配置步骤：
///   1. 下载 Facebook SDK for Unity → Assets → Import Package → Custom Package
///   2. 在 Facebook Developer Console 创建应用
///   3. 获取 App ID 和 App Secret
///   4. Firebase Console → Auth → Facebook → 填入 App ID + App Secret
///   5. 将 Firebase 提供的 OAuth Redirect URI 添加到 Facebook 应用设置
///   6. Unity → Facebook → Edit Settings → 填入 App ID
///
/// Android 额外配置（SDK 导入时自动处理）：
///   - Assets/Plugins/Android/FacebookSDK.androidlib（Manifest + strings.xml）
///   - strings.xml 中 facebook_app_id 值
/// iOS 额外配置（SDK 导入时自动处理）：
///   - Info.plist: FacebookAppID, CFBundleURLSchemes (fb{APP_ID})
///   - LSApplicationQueriesSchemes: fbapi, fb-messenger-share-api
///
/// 登录流程：
/// 1. FB.Init() → 等待 SDK 初始化
/// 2. FB.LogInWithReadPermissions(permissions, callback)
/// 3. 弹出 Facebook 登录页 / Facebook App
/// 4. 用户授权 → ILoginResult.AccessToken.TokenString
/// 5. C# → FacebookAuthProvider.GetCredential(accessToken) → Firebase
/// </summary>
public class FacebookSignInHelper : MonoSingleton<FacebookSignInHelper>
{
    private const string FBTypeName = "Facebook.Unity.FB, Facebook.Unity";
    private const string FacebookSettingsTypeName = "Facebook.Unity.Settings.FacebookSettings, Facebook.Unity.Settings";
    private const string InitDelegateTypeName = "Facebook.Unity.InitDelegate, Facebook.Unity";
    private const string HideUnityDelegateTypeName = "Facebook.Unity.HideUnityDelegate, Facebook.Unity";
    private const string FacebookDelegateTypeName = "Facebook.Unity.FacebookDelegate`1, Facebook.Unity";
    private const string LoginResultTypeName = "Facebook.Unity.ILoginResult, Facebook.Unity";
    private const string AccessTokenTypeName = "Facebook.Unity.AccessToken, Facebook.Unity";

    #region 枚举与配置

    /// <summary>登录状态</summary>
    public enum SignInState
    {
        Idle,
        Initializing,
        SigningIn,
    }

    [Header("Facebook SDK 配置")]
    [Tooltip("Facebook App ID（来自 Facebook Developer Console）")]
    public string FacebookAppId = "";

    [Tooltip("登录时请求的权限列表")]
    public string[] Permissions = new string[] { "public_profile", "email" };

    [Tooltip("好友发现时按需请求的权限列表")]
    public string[] FriendDiscoveryPermissions = new string[] { "public_profile", "email", "user_friends" };

    [Header("Editor 模拟设置")]
    [Tooltip("Editor 模拟延迟（秒）")]
    public float EditorSimulateDelay = 1f;

    [Tooltip("Editor 模拟是否成功")]
    public bool EditorSimulateSuccess = true;

    #endregion

    #region 状态

    /// <summary>当前登录状态</summary>
    public SignInState State { get; private set; } = SignInState.Idle;

    /// <summary>是否正在登录中</summary>
    public bool IsSigningIn => State == SignInState.SigningIn;

    /// <summary>Facebook SDK 是否已初始化</summary>
    public bool IsSDKInitialized { get; private set; } = false;

    #endregion

    #region 回调

    private Action<string> _onSuccess; // accessToken
    private Action<string> _onError;
    private string[] _pendingPermissions;

    #endregion

    #region 公开方法

    /// <summary>
    /// 发起 Facebook 登录
    /// </summary>
    /// <param name="onSuccess">登录成功回调，参数为 Access Token</param>
    /// <param name="onError">登录失败回调，参数为错误信息</param>
    public void SignIn(Action<string> onSuccess, Action<string> onError)
    {
        BeginSignIn(Permissions, onSuccess, onError);
    }

    /// <summary>
    /// 为 Facebook 好友发现请求 access token。
    /// 不会切换 Firebase 当前用户，只用于 Graph API 好友查询。
    /// </summary>
    public void RequestFriendDiscoveryAccess(Action<string> onSuccess, Action<string> onError)
    {
        BeginSignIn(FriendDiscoveryPermissions, onSuccess, onError);
    }

    private void BeginSignIn(IEnumerable<string> permissions, Action<string> onSuccess, Action<string> onError)
    {
        if (IsSigningIn)
        {
            onError?.Invoke("正在登录中，请勿重复操作");
            return;
        }

        if (string.IsNullOrEmpty(ResolveConfiguredFacebookAppId()))
        {
#if UNITY_EDITOR
            Debug.LogWarning("[FacebookSignInHelper] Facebook App ID 未配置，Editor 将使用模拟模式；真机登录前请配置 Facebook Settings");
#else
            Debug.LogWarning("[FacebookSignInHelper] Facebook App ID 未配置，请检查 FacebookSettings、AndroidManifest 或 iOS Info.plist");
#endif
        }

        State = SignInState.SigningIn;
        _onSuccess = onSuccess;
        _onError = onError;
        _pendingPermissions = NormalizePermissions(permissions);

        try
        {
            StartFacebookSignIn();
        }
        catch (Exception e)
        {
            CancelSignIn();
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
            var fbType = GetFBType();
            if (fbType != null)
            {
                var logOutMethod = fbType.GetMethod("LogOut");
                if (logOutMethod != null)
                {
                    logOutMethod.Invoke(null, null);
                    Debug.Log("[FacebookSignInHelper] Facebook 已登出");
                }
            }
            else
            {
                Debug.Log("[FacebookSignInHelper] Facebook SDK 未安装，跳过登出");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FacebookSignInHelper] Facebook 登出异常: {e.Message}");
        }

        State = SignInState.Idle;
    }

    /// <summary>
    /// 检查是否已安装 Facebook SDK
    /// </summary>
    public bool IsSDKAvailable()
    {
        return GetFBType() != null;
    }

    /// <summary>
    /// 检查当前是否有缓存的登录状态
    /// </summary>
    public bool HasCachedLogin()
    {
        try
        {
            var fbType = GetFBType();
            if (fbType != null)
            {
                var isLoggedInProp = fbType.GetProperty("IsLoggedIn");
                if (isLoggedInProp != null)
                {
                    return (bool)isLoggedInProp.GetValue(null);
                }
            }
        }
        catch { }

        return false;
    }

    #endregion

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);
        InitializeFacebookSDK();
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化 Facebook SDK
    /// 使用反射避免编译期依赖 Facebook.Unity.dll
    /// </summary>
    private void InitializeFacebookSDK()
    {
        State = SignInState.Initializing;

        try
        {
            ResolveConfiguredFacebookAppId();

            var fbType = GetFBType();
            if (fbType != null)
            {
                if (TryInvokeFacebookInit(fbType))
                {
                    Debug.Log("[FacebookSignInHelper] Facebook SDK 初始化中...");
                    return;
                }
            }

            // SDK 未安装 → 编辑器下正常，真机上会报错
#if UNITY_EDITOR
            Debug.Log("[FacebookSignInHelper] Facebook SDK 未安装，Editor 下将使用模拟模式");
            IsSDKInitialized = true;
#else
            Debug.LogWarning("[FacebookSignInHelper] Facebook SDK 未安装，登录功能将不可用。请安装: https://developers.facebook.com/docs/unity/downloads/");
            IsSDKInitialized = false;
#endif
            State = SignInState.Idle;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FacebookSignInHelper] Facebook SDK 初始化异常: {e.Message}");
#if UNITY_EDITOR
            IsSDKInitialized = true;
#endif
            State = SignInState.Idle;
        }
    }

    /// <summary>
    /// Facebook SDK 初始化完成回调
    /// </summary>
    private void OnFacebookInitComplete()
    {
        IsSDKInitialized = true;
        State = SignInState.Idle;
        Debug.Log("[FacebookSignInHelper] Facebook SDK 初始化完成");

        // 检查是否已有登录状态（SDK 缓存）
        if (HasCachedLogin())
        {
            Debug.Log("[FacebookSignInHelper] 检测到已登录的 Facebook 账号（缓存）");
        }
    }

    #endregion

    #region 核心：发起登录

    /// <summary>
    /// 启动 Facebook 登录流程
    /// 真机走 Facebook SDK，Editor 走模拟
    /// </summary>
    private void StartFacebookSignIn()
    {
        var fbType = GetFBType();

#if UNITY_EDITOR
        // Editor → 模拟模式
        Debug.Log("[FacebookSignInHelper] Editor 模式：模拟 Facebook 登录");
        StartCoroutine(SimulateFacebookSignInCoroutine());
        return;
#else

        if (fbType == null)
        {
            // 真机但 SDK 未安装
            CancelSignIn();
            _onError?.Invoke("Facebook SDK 未安装，无法在真机上登录。请安装: https://developers.facebook.com/docs/unity/downloads/");
            return;
        }

        // 确保 SDK 已初始化
        if (!IsSDKInitialized)
        {
            CancelSignIn();
            _onError?.Invoke("Facebook SDK 尚未初始化完成，请稍后重试");
            return;
        }

        var iLoginResultType = System.Type.GetType(LoginResultTypeName);
        var facebookDelegateType = System.Type.GetType(FacebookDelegateTypeName);
        if (iLoginResultType == null || facebookDelegateType == null)
        {
            CancelSignIn();
            _onError?.Invoke("Facebook SDK 类型加载失败");
            return;
        }

        var callbackType = facebookDelegateType.MakeGenericType(iLoginResultType);

        // 调用 FB.LogInWithReadPermissions
        var logInMethod = fbType.GetMethod("LogInWithReadPermissions",
            new Type[] { typeof(IEnumerable<string>), callbackType });

        if (logInMethod == null)
        {
            CancelSignIn();
            _onError?.Invoke("Facebook SDK 方法未找到，请检查 SDK 版本");
            return;
        }

        MethodInfo callbackMethod = GetType().GetMethod(
            nameof(OnFacebookLoginResult),
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new Type[] { typeof(object) },
            null);

        var callback = Delegate.CreateDelegate(callbackType, this, callbackMethod);
        IEnumerable<string> permissions = _pendingPermissions ?? NormalizePermissions(Permissions);
        logInMethod.Invoke(null, new object[] { permissions, callback });

        Debug.Log("[FacebookSignInHelper] 已发起 Facebook 登录请求，等待用户授权...");
#endif
    }

    #endregion

    #region 登录结果回调

    /// <summary>
    /// Facebook 登录结果回调（由 Facebook SDK 反射调用）
    /// </summary>
    private void OnFacebookLoginResult(object result)
    {
        try
        {
            if (result == null)
            {
                CancelSignIn();
                _onError?.Invoke("Facebook 登录返回空结果");
                return;
            }

            // 检查是否取消
            var cancelledProp = result.GetType().GetProperty("Cancelled");
            if (cancelledProp != null && (bool)cancelledProp.GetValue(result))
            {
                CancelSignIn();
                _onError?.Invoke("用户取消了 Facebook 登录");
                return;
            }

            // 检查错误
            var errorProp = result.GetType().GetProperty("Error");
            if (errorProp != null)
            {
                var error = errorProp.GetValue(result);
                if (!string.IsNullOrEmpty(error?.ToString()))
                {
                    CancelSignIn();
                    _onError?.Invoke($"Facebook 登录错误: {error}");
                    return;
                }
            }

            // 获取 Access Token
            object resultAccessToken = result.GetType().GetProperty("AccessToken")?.GetValue(result);
            if (TryFinishWithAccessToken(resultAccessToken))
            {
                return;
            }

            var accessTokenType = System.Type.GetType(AccessTokenTypeName);
            if (accessTokenType != null)
            {
                var currentAccessTokenProp = accessTokenType.GetProperty("CurrentAccessToken");
                if (currentAccessTokenProp != null)
                {
                    var accessToken = currentAccessTokenProp.GetValue(null);
                    if (TryFinishWithAccessToken(accessToken))
                        return;
                }
            }

            CancelSignIn();
            _onError?.Invoke("无法获取 Facebook Access Token");
        }
        catch (Exception e)
        {
            CancelSignIn();
            _onError?.Invoke($"Facebook 登录结果处理异常: {e.Message}");
        }
    }

    #endregion

    #region 手动回调入口（供 SDK 回调直接调用）

    /// <summary>
    /// 登录成功 — 手动调用入口
    /// 如果你在别处直接使用了 Facebook SDK 回调，可以用这个方法通知 Helper
    ///
    /// 示例:
    /// <code>
    /// FB.LogInWithReadPermissions(permissions, result =>
    /// {
    ///     if (result.Cancelled) return;
    ///     FacebookSignInHelper.Instance.OnSignInSuccess(result.AccessToken.TokenString);
    /// });
    /// </code>
    /// </summary>
    public void OnSignInSuccess(string accessToken)
    {
        State = SignInState.Idle;
        Debug.Log("[FacebookSignInHelper] Facebook 登录成功（手动回调）");
        _onSuccess?.Invoke(accessToken);
        ClearPendingCallbacks();
    }

    /// <summary>
    /// 登录失败 — 手动调用入口
    /// </summary>
    public void OnSignInError(string error)
    {
        CancelSignIn();
        Debug.LogError($"[FacebookSignInHelper] Facebook 登录失败: {error}");
        _onError?.Invoke(error);
    }

    #endregion

    #region Editor 模拟

    /// <summary>
    /// Editor 模拟 Facebook 登录
    /// SDK 未安装或非真机环境时自动使用
    /// </summary>
    private System.Collections.IEnumerator SimulateFacebookSignInCoroutine()
    {
        Debug.Log("[FacebookSignInHelper] 模拟 Facebook 登录中...");
        yield return new WaitForSeconds(EditorSimulateDelay);

        if (EditorSimulateSuccess)
        {
            string mockToken = $"mock_fb_access_token_{DateTime.Now.Ticks}_editor_sim";
            Debug.Log($"[FacebookSignInHelper] Editor 模拟 Facebook 登录成功");
            State = SignInState.Idle;
            _onSuccess?.Invoke(mockToken);
        }
        else
        {
            Debug.LogWarning("[FacebookSignInHelper] Editor 模拟 Facebook 登录失败（配置为模拟失败）");
            State = SignInState.Idle;
            _onError?.Invoke("模拟 Facebook 登录失败（测试用）");
        }

        ClearPendingCallbacks();
    }

    #endregion

    #region 工具方法

    private static Type GetFBType()
    {
        return System.Type.GetType(FBTypeName);
    }

    private bool TryInvokeFacebookInit(Type fbType)
    {
        Type initDelegateType = System.Type.GetType(InitDelegateTypeName);
        Type hideUnityDelegateType = System.Type.GetType(HideUnityDelegateTypeName);
        if (initDelegateType == null || hideUnityDelegateType == null)
            return false;

        MethodInfo initMethod = fbType.GetMethod("Init", new Type[] { initDelegateType, hideUnityDelegateType, typeof(string) });
        if (initMethod == null)
            return false;

        Delegate initCallback = Delegate.CreateDelegate(initDelegateType, this, nameof(OnFacebookInitComplete));
        initMethod.Invoke(null, new object[] { initCallback, null, null });
        return true;
    }

    private string ResolveConfiguredFacebookAppId()
    {
        if (!string.IsNullOrWhiteSpace(FacebookAppId))
            return FacebookAppId.Trim();

        try
        {
            Type settingsType = System.Type.GetType(FacebookSettingsTypeName);
            object settings = settingsType?.GetProperty("NullableInstance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? settingsType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            string settingsAppId = settingsType?.GetProperty("AppId", BindingFlags.Public | BindingFlags.Instance)?.GetValue(settings) as string;
            if (!string.IsNullOrWhiteSpace(settingsAppId))
            {
                FacebookAppId = settingsAppId.Trim();
                return FacebookAppId;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FacebookSignInHelper] 读取 Facebook Settings App ID 失败: {e.Message}");
        }

        return string.Empty;
    }

    private bool TryFinishWithAccessToken(object accessToken)
    {
        if (accessToken == null)
            return false;

        var tokenStringProp = accessToken.GetType().GetProperty("TokenString");
        string tokenString = tokenStringProp?.GetValue(accessToken) as string;
        if (string.IsNullOrEmpty(tokenString))
        {
            CancelSignIn();
            _onError?.Invoke("Facebook Access Token 为空，请确认权限配置正确");
            return true;
        }

        Debug.Log("[FacebookSignInHelper] Facebook 登录成功，已获取 Access Token");
        State = SignInState.Idle;
        _onSuccess?.Invoke(tokenString);
        ClearPendingCallbacks();
        return true;
    }

    private void CancelSignIn()
    {
        State = SignInState.Idle;
        _pendingPermissions = null;
    }

    private void ClearPendingCallbacks()
    {
        _onSuccess = null;
        _onError = null;
        _pendingPermissions = null;
    }

    private static string[] NormalizePermissions(IEnumerable<string> permissions)
    {
        List<string> normalized = new List<string>();
        if (permissions != null)
        {
            foreach (string permission in permissions)
            {
                if (string.IsNullOrWhiteSpace(permission))
                    continue;

                string value = permission.Trim();
                if (!normalized.Contains(value))
                    normalized.Add(value);
            }
        }

        if (normalized.Count == 0)
        {
            normalized.Add("public_profile");
            normalized.Add("email");
        }

        return normalized.ToArray();
    }

    #endregion
}
