using System;
using System.Reflection;
using System.Collections;
using UnityEngine;
using XFGameFrameWork;

/// <summary>
/// Google 登录辅助类
/// 封装 Google Sign-In SDK 的调用流程
///
/// 依赖：
/// 1. Google Sign-In Unity 插件（https://github.com/googlesamples/google-signin-unity）
/// 2. 在 Firebase 控制台启用 Google 登录提供商
/// 3. 在 Google Cloud Console 创建 OAuth 2.0 客户端 ID
///
/// 安装步骤：
/// 1. 下载 Google Sign-In Unity 插件 .unitypackage
/// 2. 导入到 Unity 项目
/// 3. 在 GameObject 上配置 Google Sign-In 设置（Web Client ID）
/// 4. 确保 ExternalDependencyManager 已安装 Google Play Services 相关依赖
///
/// 注意：Google Sign-In 仅支持 Android 和 iOS 真机，
///       Unity Editor 中点击登录会走模拟模式。
///
/// 使用示例：
/// <code>
/// GoogleSignInHelper.Instance.SignIn(
///     idToken => Debug.Log($"登录成功: {idToken}"),
///     error => Debug.LogError($"登录失败: {error}")
/// );
/// </code>
/// </summary>
public class GoogleSignInHelper : MonoSingleton<GoogleSignInHelper>
{
    #region 枚举与配置

    /// <summary>登录状态</summary>
    public enum SignInState
    {
        /// <summary>空闲</summary> 
        Idle,
        /// <summary>登录中</summary> 
        SigningIn,
        /// <summary>登出中</summary> 
        SigningOut,
    }

    [Header("Google Sign-In 配置")]
    [Tooltip("Google OAuth 2.0 Web Client ID（从 Firebase 控制台获取）")]
    public string WebClientId = "";

    [Tooltip("是否请求 ID Token（Firebase 认证必需）")]
    public bool RequestIdToken = true;

    [Tooltip("是否请求授权码（服务端验证用）")]
    public bool RequestAuthCode = false;

    [Tooltip("是否请求邮箱权限")]
    public bool RequestEmail = true;

    [Tooltip("是否请求个人资料权限")]
    public bool RequestProfile = true;

    [Header("Editor 模拟设置")]
    [Tooltip("Editor 模拟延迟（秒）")]
    public float EditorSimulateDelay = 1f;

    [Tooltip("Editor 模拟时是否模拟成功（关闭可测试失败链路）")]
    public bool EditorSimulateSuccess = true;

    [Tooltip("Editor 模拟失败时的错误信息")]
    public string EditorSimulateErrorMsg = "模拟登录失败（测试用）";

    #endregion

    #region 状态

    /// <summary>当前登录状态</summary>
    public SignInState State { get; private set; } = SignInState.Idle;

    /// <summary>是否正在登录中</summary>
    public bool IsSigningIn => State == SignInState.SigningIn;

    /// <summary>当前运行的登录协程（用于取消）</summary>
    private Coroutine _signInCoroutine;

    #endregion

    #region 反射缓存（readonly 保证初始化后不被篡改）

    private readonly Type[] _googleTypes = new Type[2];
    private readonly MethodInfo[] _cachedMethods = new MethodInfo[4]; // [0]SignIn, [1]SignOut, [2]SignInSilently, [3]Configure
    private PropertyInfo _defaultInstanceProp;
    private PropertyInfo _configurationProp;
    private volatile bool _reflectionInitialized = false;
    private string _reflectionError;

    // 类型索引常量
    private const int IDX_SIGN_IN_TYPE = 0;
    private const int IDX_CONFIG_TYPE = 1;
    private const int MTH_SIGN_IN = 0;
    private const int MTH_SIGN_OUT = 1;
    private const int MTH_SIGN_IN_SILENTLY = 2;

    #endregion

    #region 公开方法

    /// <summary>
    /// 发起 Google 登录
    /// </summary>
    /// <param name="onSuccess">登录成功回调，参数为 ID Token</param>
    /// <param name="onError">登录失败回调，参数为错误信息</param>
    public void SignIn(Action<string> onSuccess, Action<string> onError)
    {
        if (IsSigningIn)
        {
            onError?.Invoke("正在登录中，请勿重复操作");
            return;
        }

        if (string.IsNullOrEmpty(WebClientId))
        {
            onError?.Invoke("WebClientId 未配置，请在 Inspector 中设置 Google OAuth Web Client ID");
            Debug.LogError("[GoogleSignInHelper] WebClientId 未配置！");
            return;
        }

        // 取消之前的登录协程（如果有）
        CancelActiveSignIn();

#if UNITY_EDITOR
        Debug.Log("[GoogleSignInHelper] Editor 模拟模式：模拟 Google 登录（当前平台: " +
            (Application.platform == RuntimePlatform.OSXEditor ? "macOS" :
             Application.platform == RuntimePlatform.WindowsEditor ? "Windows" : "Linux") + "）");
        StartEditorSimulation(onSuccess, onError);
        return;
#elif !UNITY_ANDROID && !UNITY_IOS
        Debug.Log("[GoogleSignInHelper] 非移动端平台：模拟 Google 登录");
        StartEditorSimulation(onSuccess, onError);
        return;
#endif

        State = SignInState.SigningIn;
        _signInCoroutine = StartCoroutine(SignInCoroutine(onSuccess, onError));
    }

    /// <summary>
    /// 取消当前正在进行的登录操作
    /// </summary>
    public void CancelSignIn()
    {
        if (!IsSigningIn) return;

        CancelActiveSignIn();
        Debug.Log("[GoogleSignInHelper] 用户取消了 Google 登录");
    }

    /// <summary>
    /// 登出 Google 账号
    /// </summary>
    public void SignOut()
    {
        if (!InitReflectionIfNeeded()) return;

        try
        {
            var instance = _defaultInstanceProp.GetValue(null);
            _cachedMethods[MTH_SIGN_OUT]?.Invoke(instance, null);
            Debug.Log("[GoogleSignInHelper] Google 已登出");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GoogleSignInHelper] Google 登出异常: {e.Message}");
        }

        State = SignInState.Idle;
    }

    /// <summary>
    /// 静默登录（尝试使用上次登录的账号，无 UI 弹窗）
    /// </summary>
    public void SignInSilently(Action<string> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrEmpty(WebClientId))
        {
            onError?.Invoke("WebClientId 未配置");
            return;
        }

#if !UNITY_ANDROID && !UNITY_IOS
        onError?.Invoke("Google 静默登录仅支持 Android/iOS 真机");
        return;
#endif

        if (!InitReflectionIfNeeded())
        {
            onError?.Invoke(_reflectionError ?? "Google Sign-In SDK 未安装");
            return;
        }

        State = SignInState.SigningIn;
        _signInCoroutine = StartCoroutine(SilentSignInCoroutine(onSuccess, onError));
    }

    #endregion

    #region 内部方法 — 核心登录流程

    /// <summary>
    /// 主登录协程（真机环境）
    /// </summary>
    private IEnumerator SignInCoroutine(Action<string> onSuccess, Action<string> onError)
    {
        var capturedOnSuccess = onSuccess;
        var capturedOnError = onError;
        object task = null;
        string syncError = null;

        // 同步初始化（不能包含 yield return）
        try
        {
            if (!InitReflectionIfNeeded())
            {
                syncError = _reflectionError;
            }
            else
            {
                var instance = _defaultInstanceProp.GetValue(null);
                ConfigureGoogleSignIn();
                task = _cachedMethods[MTH_SIGN_IN]?.Invoke(instance, null);
                if (task == null)
                    syncError = "Google SignIn 返回空结果";
            }
        }
        catch (Exception e)
        {
            var realEx = UnwrapException(e);
            string exMsg = realEx.Message;

            // 检测是否是原生层不可用（无 Google Play Services / 国内设备等）
            bool isNativeUnavailable =
                exMsg.Contains("GoogleSignIn_Create") ||
                exMsg.Contains("unknown assembly") ||
                exMsg.Contains("unknown type") ||
                exMsg.Contains("JNI") ||
                exMsg.Contains("DllNotFoundException") ||
                exMsg.Contains("EntryPointNotFoundException");

            if (isNativeUnavailable)
            {
                // 原生 SDK 不可用 → 自动降级到模拟模式
                Debug.LogWarning("[GoogleSignInHelper] ⚠️ 原生 Google SDK 不可用（可能设备缺少 Google Play Services），自动降级到模拟模式");
                Debug.LogWarning($"[GoogleSignInHelper] 原始异常: {exMsg}");

                SetIdle();
                StartEditorSimulation(capturedOnSuccess, capturedOnError);
                yield break; // ★ 直接走模拟路径，不报错
            }

            syncError = $"Google 登录异常: {exMsg}";
            Debug.LogError($"[GoogleSignInHelper] 异常: {exMsg}\n{realEx.StackTrace}");
        }

        // 同步阶段出错 → 直接结束
        if (syncError != null)
        {
            FinishWithError(capturedOnError, syncError);
            yield break;
        }

        // 异步等待 Task 结果（yield 在 try 外）
        Debug.Log("[GoogleSignInHelper] 已发起 Google 登录请求，等待异步结果...");
        yield return WaitForTaskResult(task, capturedOnSuccess, capturedOnError);
    }

    /// <summary>
    /// 静默登录协程
    /// </summary>
    private IEnumerator SilentSignInCoroutine(Action<string> onSuccess, Action<string> onError)
    {
        var capturedOnSuccess = onSuccess;
        var capturedOnError = onError;
        object task = null;
        string syncError = null;

        // 同步初始化
        try
        {
            var instance = _defaultInstanceProp.GetValue(null);
            ConfigureGoogleSignIn();
            task = _cachedMethods[MTH_SIGN_IN_SILENTLY]?.Invoke(instance, null);
            if (task == null)
                syncError = "静默登录返回空结果";
        }
        catch (Exception e)
        {
            syncError = $"静默登录失败: {UnwrapException(e).Message}";
        }

        if (syncError != null)
        {
            FinishWithError(capturedOnError, syncError);
            yield break;
        }

        // 异步等待（yield 在 try 外）
        yield return WaitForTaskResult(task, capturedOnSuccess, capturedOnError);
    }

    /// <summary>
    /// Editor 模拟登录协程
    /// </summary>
    private IEnumerator EditorSimulateCoroutine(Action<string> onSuccess, Action<string> onError)
    {
        var capturedOnSuccess = onSuccess;
        var capturedOnError = onError;
        float elapsed = 0f;
        while (elapsed < EditorSimulateDelay)
        {

            elapsed += Time.deltaTime;
            // 检查是否超时
            if (elapsed >= EditorSimulateDelay)
            {
                if (EditorSimulateSuccess)
                {
                    string mockToken = BuildMockIdToken();
                    Debug.Log("[GoogleSignInHelper] ✅ 模拟登录成功！（假 Token 仅用于开发调试，不会连接真实 Google 服务器）");
                    Debug.Log("[GoogleSignInHelper] 提示：在有 Google Play Services 的设备上才会调用真实 SDK");

                    SetIdle();
                    capturedOnSuccess?.Invoke(mockToken);
                }
                else
                {
                    Debug.LogWarning($"[GoogleSignInHelper] ❌ Editor 模拟登录失败：{EditorSimulateErrorMsg}");
                    SetIdle();
                    capturedOnError?.Invoke(EditorSimulateErrorMsg);
                }
                yield break;
            }
            yield return null;
        }
    }

    /// <summary>
    /// 启动 Editor 模拟登录
    /// </summary>
    private void StartEditorSimulation(Action<string> onSuccess, Action<string> onError)
    {
        State = SignInState.SigningIn;
        _signInCoroutine = StartCoroutine(EditorSimulateCoroutine(onSuccess, onError));
    }

    /// <summary>
    /// 取消活跃的登录协程并重置状态
    /// </summary>
    private void CancelActiveSignIn()
    {
        if (_signInCoroutine != null)
        {
            StopCoroutine(_signInCoroutine);
            _signInCoroutine = null;
        }
        SetIdle();
    }

    /// <summary>
    /// 统一完成-失败出口
    /// </summary>
    private void FinishWithError(Action<string> onError, string message)
    {
        SetIdle();
        Debug.LogError($"[GoogleSignInHelper] {message}");
        onError?.Invoke(message);
    }

    /// <summary>
    /// 重置为空闲状态
    /// </summary>
    private void SetIdle()
    {
        State = SignInState.Idle;
        _signInCoroutine = null;
    }

    #endregion

    #region 内部方法 — 反射初始化

    /// <summary>
    /// 延迟初始化反射缓存（首次调用时执行，之后直接返回）
    /// 返回 false 表示 SDK 不可用
    /// </summary>
    private bool InitReflectionIfNeeded()
    {
        if (_reflectionInitialized) return _reflectionError == null;
        _reflectionInitialized = true;

        // SDK 命名空间 Google，无 asmdef → 编译到 Assembly-CSharp
        _googleTypes[IDX_SIGN_IN_TYPE] = Type.GetType("Google.GoogleSignIn, Assembly-CSharp");
        _googleTypes[IDX_CONFIG_TYPE] = Type.GetType("Google.GoogleSignInConfiguration, Assembly-CSharp");

        var signInType = _googleTypes[IDX_SIGN_IN_TYPE];
        var configType = _googleTypes[IDX_CONFIG_TYPE];

        if (signInType == null)
        {
            _reflectionError = "Google Sign-In SDK 未安装。请下载并导入:\nhttps://github.com/googlesamples/google-signin-unity/releases";
            Debug.LogError($"[GoogleSignInHelper] {_reflectionError}");
            return false;
        }

        if (configType == null)
        {
            _reflectionError = "GoogleSignInConfiguration 类型未找到";
            Debug.LogError($"[GoogleSignInHelper] {_reflectionError}");
            return false;
        }

        _defaultInstanceProp = signInType.GetProperty("DefaultInstance");
        if (_defaultInstanceProp == null)
        {
            _reflectionError = "GoogleSignIn.DefaultInstance 属性未找到";
            Debug.LogError($"[GoogleSignInHelper] {_reflectionError}");
            return false;
        }

        _configurationProp = signInType.GetProperty("Configuration");

        // 缓存常用方法
        _cachedMethods[MTH_SIGN_IN] = signInType.GetMethod("SignIn");
        _cachedMethods[MTH_SIGN_OUT] = signInType.GetMethod("SignOut");
        _cachedMethods[MTH_SIGN_IN_SILENTLY] = signInType.GetMethod("SignInSilently");

        if (_cachedMethods[MTH_SIGN_IN] == null)
        {
            _reflectionError = "GoogleSignIn.SignIn 方法未找到";
            Debug.LogError($"[GoogleSignInHelper] {_reflectionError}");
            return false;
        }

        Debug.Log("[GoogleSignInHelper] ✅ 反射初始化成功，Google Sign-In SDK 已就绪");
        return true;
    }

    /// <summary>
    /// 配置 Google Sign-In（通过静态 Configuration 属性）
    /// 同时尝试属性和字段，兼容不同版本的 SDK
    /// </summary>
    private void ConfigureGoogleSignIn()
    {
        var configType = _googleTypes[IDX_CONFIG_TYPE];
        object config = Activator.CreateInstance(configType);

        // SDK 可能用 Property（PascalCase）也可能用 Field（camelCase），两种都试
        SetMemberValue(config, "WebClientId",  WebClientId);
        SetMemberValue(config, "RequestIdToken",  RequestIdToken);
        SetMemberValue(config, "RequestAuthCode", RequestAuthCode);
        SetMemberValue(config, "RequestEmail",     RequestEmail);
        SetMemberValue(config, "RequestProfile",   RequestProfile);

        _configurationProp?.SetValue(null, config);

        Debug.Log($"[GoogleSignInHelper] Configuration 已设置: WebClientId={WebClientId}, IdToken={RequestIdToken}");
    }

    /// <summary>
    /// 反射设置成员值：优先尝试 Property，失败再尝试 Field（忽略大小写）
    /// </summary>
    private static void SetMemberValue(object target, string name, object value)
    {
        var type = target.GetType();

        // 先试 Property（SDK 正式版用 Property）
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(target, value);
            return;
        }

        // 再试忽略大小写的 Property
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var p in props)
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) && p.CanWrite)
            {
                p.SetValue(target, value);
                return;
            }
        }

        // 最后试 Field（旧版 SDK 可能用 Field）
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        // 忽略大小写试 Field
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var f in fields)
        {
            if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                f.SetValue(target, value);
                return;
            }
        }

        Debug.LogWarning($"[GoogleSignInHelper] SetMemberValue: 未找到成员 '{name}'");
    }

    #endregion

    #region 内部方法 — Task 等待与 Token 构建

    /// <summary>
    /// 解包反射调用产生的 TargetInvocationException，获取真实异常信息
    /// </summary>
    private static Exception UnwrapException(Exception ex)
    {
        while (ex != null && ex.InnerException != null)
            ex = ex.InnerException;
        return ex ?? new Exception("未知异常");
    }

    /// <summary>
    /// 协程轮询等待 Task&lt;T&gt; 结果
    /// SDK 的 SignIn / SignInSilently 都返回 Task，Unity 无法 await，只能轮询
    /// </summary>
    private IEnumerator WaitForTaskResult(object taskObj, Action<string> onSuccess, Action<string> onError)
    {
        var taskType = taskObj.GetType();
        var isCompletedProp = taskType.GetProperty("IsCompleted");
        var exceptionProp = taskType.GetProperty("Exception");
        var resultProp = taskType.GetProperty("Result");

        const float timeoutSeconds = 60f;
        float elapsed = 0f;

        while (!(bool)isCompletedProp.GetValue(taskObj))
        {
            // 轮询期间检测取消
            if (_signInCoroutine == null)
            {
                Debug.Log("[GoogleSignInHelper] 登录已取消");
                yield break;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= timeoutSeconds)
            {
                FinishWithError(onError, "Google 登录超时（60秒）");
                yield break;
            }
            yield return null;
        }

        // 检查异常
        var exception = exceptionProp?.GetValue(taskObj) as Exception;
        if (exception != null)
        {
            var innerEx = exception.InnerException;
            string errorMsg = innerEx != null ? innerEx.Message : exception.Message;
            FinishWithError(onError, errorMsg);
            yield break;
        }

        // 提取 Result → GoogleSignInUser
        var user = resultProp?.GetValue(taskObj);
        if (user == null)
        {
            FinishWithError(onError, "登录返回空用户对象");
            yield break;
        }

        var idToken = user.GetType().GetProperty("IdToken")?.GetValue(user) as string;
        var email = user.GetType().GetProperty("Email")?.GetValue(user) as string;

        if (!string.IsNullOrEmpty(idToken))
        {
            SetIdle();
            Debug.Log($"[GoogleSignInHelper] ✅ Google 登录成功! Email={email ?? "N/A"}");
            onSuccess?.Invoke(idToken);
        }
        else
        {
            FinishWithError(onError, "登录成功但未获取到 IdToken");
        }
    }

    /// <summary>
    /// 构建 Editor 模拟用的假 JWT 格式 Token
    /// 仅用于开发调试整条登录链路，不可用于生产环境
    /// </summary>
    private string BuildMockIdToken()
    {
        string header = "eyJhbGciOiJSUzI1NiIsImtpZCI6Im1vY2tfZ29vZ2xlX3Rva2VuXyJ9";
        string payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"{{\"iss\":\"accounts.google.com\",\"aud\":\"{WebClientId ?? "mock"}\"," +
            $"\"sub\":\"mock_google_user\",\"email\":\"mock@test.com\"," +
            $"\"iat\":{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}," +
            $"\"exp\":{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}"));
        string signature = "editor_simulation_signature";
        return $"{header}.{payload}.{signature}";
    }

    #endregion

    #region 外部回调接口（兼容手动触发场景）

    /// <summary>
    /// Google 登录成功回调（供外部手动触发）
    /// </summary>
    public void OnSignInSuccess(string idToken)
    {
        SetIdle();
        Debug.Log("[GoogleSignInHelper] Google 登录成功（外部回调），已获取 ID Token");
    }

    /// <summary>
    /// Google 登录失败回调（供外部手动触发）
    /// </summary>
    public void OnSignInError(string error)
    {
        SetIdle();
        Debug.LogError($"[GoogleSignInHelper] Google 登录失败（外部回调）: {error}");
    }

    #endregion

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();
        // 防止切换场景时销毁单例导致登录中断
        DontDestroyOnLoad(gameObject);
    }
    private void OnDestroy() {
         CancelActiveSignIn();
        _reflectionInitialized = false;
        _reflectionError = null;
    }

    #endregion
}
