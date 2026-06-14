using System;
using UnityEngine;
using XFGameFrameWork;
using System.Runtime.InteropServices;

#if UNITY_ANDROID && !UNITY_EDITOR
using Firebase.Auth;
#endif

/// <summary>
/// Google 登录辅助类
/// 通过原生插件调用 Google Sign-In API（Android: Java 插件 / iOS: Objective-C++ 插件）
/// 不依赖 Play Games SDK，适用于非游戏类 App
///
/// 依赖：
/// Android:
///   1. FariGoogleSignIn.androidlib（Assets/Plugins/Android/FariGoogleSignIn.androidlib/）
///   2. com.google.android.gms:play-services-auth:21.2.0（mainTemplate.gradle）
/// iOS:
///   1. FariGoogleSignIn.mm（Assets/Plugins/iOS/FariGoogleSignIn.mm）
///   2. GoogleSignIn (~> 7.0) CocoaPod（FariGoogleSignInDependencies.xml）
///   3. GoogleService-Info.plist 已配置（Firebase 控制台下载）
/// 共用:
///   4. Firebase Auth SDK
///   5. Firebase 控制台启用 Google 登录
///
/// 登录流程（双平台一致）：
/// 1. 尝试静默登录 — 已授权用户直接获取 ID Token
/// 2. 静默失败 → 启动原生插件的交互式登录
/// 3. 原生插件通过 UnitySendMessage 回传 ID Token
/// 4. ID Token → GoogleAuthProvider.GetCredential → Firebase
/// </summary>
public class GoogleSignInHelper : MonoSingleton<GoogleSignInHelper>
{
    #region 枚举与配置

    /// <summary>登录状态</summary>
    public enum SignInState
    {
        Idle,
        SigningIn,
        SigningOut,
    }

    [Header("Google 登录配置")]
    [Tooltip("Firebase 控制台 → Authentication → Google → Web Client ID\n" +
             "格式类似: 86394...ffm.apps.googleusercontent.com")]
    public string WebClientId = "";

    [Header("Editor 模拟设置")]
    [Tooltip("Editor 模拟延迟（秒）")]
    public float EditorSimulateDelay = 1f;

    [Tooltip("Editor 模拟时是否模拟成功")]
    public bool EditorSimulateSuccess = true;

    [Tooltip("Editor 模拟失败时的错误信息")]
    public string EditorSimulateErrorMsg = "模拟登录失败（测试用）";

    #endregion

    #region 状态

    /// <summary>当前登录状态</summary>
    public SignInState State { get; private set; } = SignInState.Idle;

    /// <summary>是否正在登录中</summary>
    public bool IsSigningIn => State == SignInState.SigningIn;

    /// <summary>挂起的成功回调</summary>
    private Action<string> _pendingOnSuccess;

    /// <summary>挂起的失败回调</summary>
    private Action<string> _pendingOnError;

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>GoogleSignInClient 实例（缓存复用，用于登出和静默登录）</summary>
    private AndroidJavaObject _signInClient;
#endif

    #endregion

    #region 公开方法

    /// <summary>
    /// 发起 Google 登录
    /// </summary>
    public void SignIn(Action<string> onSuccess, Action<string> onError)
    {
        if (IsSigningIn)
        {
            onError?.Invoke("正在登录中，请勿重复操作");
            return;
        }

#if UNITY_EDITOR
        Debug.Log("[GoogleSignInHelper] Editor 模拟模式");
        StartEditorSimulation(onSuccess, onError);
        return;
#elif UNITY_ANDROID
        if (string.IsNullOrEmpty(WebClientId))
        {
            onError?.Invoke("WebClientId 未配置！请在 Inspector 中填写");
            return;
        }

        State = SignInState.SigningIn;
        _pendingOnSuccess = onSuccess;
        _pendingOnError = onError;

        // 先尝试静默登录，失败则启动 Java 插件的交互式登录
        TrySilentSignIn();
#elif UNITY_IOS
        if (string.IsNullOrEmpty(WebClientId))
        {
            onError?.Invoke("WebClientId 未配置！请在 Inspector 中填写");
            return;
        }

        State = SignInState.SigningIn;
        _pendingOnSuccess = onSuccess;
        _pendingOnError = onError;

        TrySilentSignInIOS();
#else
        onError?.Invoke($"Google 登录不支持当前平台 ({Application.platform})");
#endif
    }

    /// <summary>
    /// 取消当前正在进行的登录操作
    /// </summary>
    public void CancelSignIn()
    {
        if (!IsSigningIn) return;
        ClearPendingCallbacks();
        State = SignInState.Idle;
        Debug.Log("[GoogleSignInHelper] 用户取消了 Google 登录");
    }

    /// <summary>
    /// 登出 Google 账号
    /// </summary>
    public void SignOut()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (_signInClient != null)
            {
                _signInClient.Call<AndroidJavaObject>("signOut");
                Debug.Log("[GoogleSignInHelper] Google 已登出");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GoogleSignInHelper] Google 登出异常: {e.Message}");
        }
#elif UNITY_IOS && !UNITY_EDITOR
        try
        {
            _fariGoogleSignOut();
            Debug.Log("[GoogleSignInHelper] Google 已登出");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GoogleSignInHelper] iOS Google 登出异常: {e.Message}");
        }
#endif
        State = SignInState.Idle;
    }

    #endregion

    #region Android 真机

#if UNITY_ANDROID && !UNITY_EDITOR

    /// <summary>
    /// 获取或创建 GoogleSignInClient（用于静默登录和登出）
    /// </summary>
    private AndroidJavaObject GetOrCreateSignInClient()
    {
        if (_signInClient != null) return _signInClient;

        try
        {
            var optionsBuilder = new AndroidJavaObject(
                "com.google.android.gms.auth.api.signin.GoogleSignInOptions$Builder");
            optionsBuilder.Call<AndroidJavaObject>("requestIdToken", WebClientId);
            optionsBuilder.Call<AndroidJavaObject>("requestEmail");
            var options = optionsBuilder.Call<AndroidJavaObject>("build");

            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            var signInClass = new AndroidJavaClass(
                "com.google.android.gms.auth.api.signin.GoogleSignIn");

            _signInClient = signInClass.CallStatic<AndroidJavaObject>("getClient", activity, options);
            Debug.Log("[GoogleSignInHelper] GoogleSignInClient 创建成功");
            return _signInClient;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GoogleSignInHelper] 创建 GoogleSignInClient 失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 尝试静默登录
    /// 成功 → 直接获取 ID Token
    /// 失败 → 启动 Java 插件的交互式登录
    /// </summary>
    private void TrySilentSignIn()
    {
        var client = GetOrCreateSignInClient();
        if (client == null)
        {
            FinishWithError("GoogleSignInClient 创建失败");
            return;
        }

        try
        {
            var task = client.Call<AndroidJavaObject>("silentSignIn");

            task.Call<AndroidJavaObject>("addOnSuccessListener",
                new SuccessListener(account =>
                {
                    try
                    {
                        var idToken = account?.Call<string>("getIdToken");
                        if (!string.IsNullOrEmpty(idToken))
                        {
                            Debug.Log("[GoogleSignInHelper] 静默登录成功");
                            FinishWithSuccess(idToken);
                        }
                        else
                        {
                            // ID Token 为空，可能是 WebClientId 配置问题
                            FinishWithError("静默登录成功但 ID Token 为空（WebClientId 可能配置错误）");
                        }
                    }
                    catch (Exception e)
                    {
                        FinishWithError($"获取 ID Token 异常: {e.Message}");
                    }
                }));

            task.Call<AndroidJavaObject>("addOnFailureListener",
                new FailureListener(exception =>
                {
                    Debug.Log("[GoogleSignInHelper] 静默登录失败，启动交互式登录...");
                    StartInteractiveSignIn();
                }));
        }
        catch (Exception e)
        {
            FinishWithError($"silentSignIn 调用异常: {e.Message}");
        }
    }

    /// <summary>
    /// 启动 Java 插件的交互式登录
    /// FariSignInActivity 会自己处理 startActivityForResult 和 onActivityResult
    /// 完成后通过 UnitySendMessage 回调 OnNativeSignInResult
    /// </summary>
    private void StartInteractiveSignIn()
    {
        try
        {
            var helperClass = new AndroidJavaClass("com.fari.googlesignin.FariSignInActivity");
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            // 调用 Java: FariSignInActivity.startSignIn(activity, webClientId, gameObjectName, callbackMethod)
            helperClass.CallStatic("startSignIn", activity, WebClientId, gameObject.name, "OnNativeSignInResult");

            Debug.Log("[GoogleSignInHelper] 已启动 FariSignInActivity，等待回调...");
        }
        catch (Exception e)
        {
            FinishWithError($"启动 FariSignInActivity 失败: {e.Message}");
        }
    }

    #region AndroidJavaProxy 回调

    private class SuccessListener : AndroidJavaProxy
    {
        private readonly Action<AndroidJavaObject> _callback;

        public SuccessListener(Action<AndroidJavaObject> callback)
            : base("com.google.android.gms.tasks.OnSuccessListener")
        {
            _callback = callback;
        }

        public void onSuccess(AndroidJavaObject result)
        {
            _callback?.Invoke(result);
        }
    }

    private class FailureListener : AndroidJavaProxy
    {
        private readonly Action<AndroidJavaObject> _callback;

        public FailureListener(Action<AndroidJavaObject> callback)
            : base("com.google.android.gms.tasks.OnFailureListener")
        {
            _callback = callback;
        }

        public void onFailure(AndroidJavaObject exception)
        {
            _callback?.Invoke(exception);
        }
    }

    #endregion

#endif

    #endregion

    #region 回调与清理

    /// <summary>
    /// 登录成功，触发回调
    /// </summary>
    private void FinishWithSuccess(string idToken)
    {
        State = SignInState.Idle;
        Debug.Log("[GoogleSignInHelper] Google 登录成功，已获取 ID Token");
        _pendingOnSuccess?.Invoke(idToken);
        ClearPendingCallbacks();
    }

    /// <summary>
    /// 登录失败，触发回调
    /// </summary>
    private void FinishWithError(string message)
    {
        State = SignInState.Idle;
        Debug.LogError($"[GoogleSignInHelper] {message}");
        _pendingOnError?.Invoke(message);
        ClearPendingCallbacks();
    }

    /// <summary>
    /// 清除挂起的回调引用
    /// </summary>
    private void ClearPendingCallbacks()
    {
        _pendingOnSuccess = null;
        _pendingOnError = null;
    }

    /// <summary>
    /// 原生插件回调入口（Android/iOS 共用）
    /// 由 UnitySendMessage 调用（Java 插件 / iOS .mm 插件都走这里）
    ///
    /// result 格式：
    ///   成功: "SUCCESS:<idToken>"
    ///   失败: "FAILURE:<statusCode>:<message>"
    ///   取消: "CANCELED"
    /// </summary>
    private void OnNativeSignInResult(string result)
    {
        Debug.Log($"[GoogleSignInHelper] 收到原生插件回调: {(result?.Length > 50 ? result.Substring(0, 50) + "..." : result)}");

        if (string.IsNullOrEmpty(result))
        {
            FinishWithError("原生插件返回空结果");
            return;
        }

        if (result.StartsWith("SUCCESS:"))
        {
            string idToken = result.Substring(8);
            if (!string.IsNullOrEmpty(idToken))
            {
                FinishWithSuccess(idToken);
            }
            else
            {
                FinishWithError("ID Token 为空");
            }
        }
        else if (result == "CANCELED")
        {
            FinishWithError("用户取消了 Google 登录");
        }
        else if (result.StartsWith("FAILURE:"))
        {
            string errorDetail = result.Substring(8);
            FinishWithError($"Google 登录失败: {errorDetail}");
        }
        else
        {
            FinishWithError($"未知的登录结果格式: {result}");
        }
    }

    #endregion

    #region iOS 真机

#if UNITY_IOS && !UNITY_EDITOR

    [DllImport("__Internal")]
    private static extern void _fariRestorePreviousSignIn(string gameObjectName, string callbackMethod);

    [DllImport("__Internal")]
    private static extern void _fariStartGoogleSignIn(string webClientId, string gameObjectName, string callbackMethod);

    [DllImport("__Internal")]
    private static extern void _fariGoogleSignOut();

    /// <summary>
    /// iOS: 尝试静默登录（恢复 Keychain 中缓存的登录状态）
    /// 回调: OnSilentSignInResultIOS
    /// </summary>
    private void TrySilentSignInIOS()
    {
        try
        {
            _fariRestorePreviousSignIn(gameObject.name, "OnSilentSignInResultIOS");
            Debug.Log("[GoogleSignInHelper] iOS: 尝试恢复之前的 Google 登录...");
        }
        catch (Exception e)
        {
            FinishWithError($"iOS 静默登录调用异常: {e.Message}");
        }
    }

    /// <summary>
    /// iOS 静默登录回调 — 由 iOS 原生插件通过 UnitySendMessage 调用
    /// 静默失败 → 自动启动交互式登录
    /// </summary>
    private void OnSilentSignInResultIOS(string result)
    {
        Debug.Log($"[GoogleSignInHelper] iOS 静默登录结果: {(result?.Length > 50 ? result.Substring(0, 50) + "..." : result)}");

        if (!string.IsNullOrEmpty(result) && result.StartsWith("SUCCESS:"))
        {
            string idToken = result.Substring(8);
            if (!string.IsNullOrEmpty(idToken))
            {
                Debug.Log("[GoogleSignInHelper] iOS 静默登录成功");
                FinishWithSuccess(idToken);
            }
            else
            {
                FinishWithError("iOS 静默登录成功但 ID Token 为空");
            }
        }
        else
        {
            // 静默登录失败（无缓存/已撤销），启动交互式登录
            Debug.Log("[GoogleSignInHelper] iOS 静默登录失败，启动交互式登录...");
            StartInteractiveSignInIOS();
        }
    }

    /// <summary>
    /// iOS: 启动交互式 Google 登录
    /// 原生插件内部会弹出 Safari/ASWebAuthenticationSession 登录页
    /// 完成后通过 UnitySendMessage 回调 OnNativeSignInResult
    /// </summary>
    private void StartInteractiveSignInIOS()
    {
        try
        {
            _fariStartGoogleSignIn(WebClientId, gameObject.name, "OnNativeSignInResult");
            Debug.Log("[GoogleSignInHelper] iOS: 已启动 Google Sign-In，等待回调...");
        }
        catch (Exception e)
        {
            FinishWithError($"iOS Google Sign-In 启动失败: {e.Message}");
        }
    }

#endif

    #endregion

    #region Editor 模拟

    private System.Collections.IEnumerator EditorSimulateCoroutine(Action<string> onSuccess, Action<string> onError)
    {
        float elapsed = 0f;
        while (elapsed < EditorSimulateDelay)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= EditorSimulateDelay)
            {
                State = SignInState.Idle;
                if (EditorSimulateSuccess)
                {
                    string mockIdToken = $"mock_id_token_{System.DateTime.Now.Ticks}";
                    Debug.Log("[GoogleSignInHelper] 模拟 Google 登录成功");
                    onSuccess?.Invoke(mockIdToken);
                }
                else
                {
                    Debug.LogWarning($"[GoogleSignInHelper] 模拟登录失败：{EditorSimulateErrorMsg}");
                    onError?.Invoke(EditorSimulateErrorMsg);
                }
                yield break;
            }
            yield return null;
        }
    }

    private void StartEditorSimulation(Action<string> onSuccess, Action<string> onError)
    {
        State = SignInState.SigningIn;
        StartCoroutine(EditorSimulateCoroutine(onSuccess, onError));
    }

    #endregion

    #region 辅助方法

    private void CancelActiveSignIn()
    {
        ClearPendingCallbacks();
        State = SignInState.Idle;
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
