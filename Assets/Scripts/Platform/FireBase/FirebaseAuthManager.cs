using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using XFGameFrameWork;
/// <summary>
/// 第三方登录提供商类型
/// </summary>
public enum AuthProvider
{
    Google,      // Google 登录
    Apple,       // Apple 登录
    Facebook,    // Facebook 登录
    Anonymous,   // 匿名登录
}

/// <summary>
/// Firebase 认证管理器
/// 负责 Firebase 初始化、第三方登录（Google/Apple/Facebook）、用户信息获取、登出等
/// </summary>
public class FirebaseAuthManager : MonoSingleton<FirebaseAuthManager>
{
    #region 事件

    /// <summary>
    /// 登录成功事件
    /// </summary>
    public event Action<AuthProvider, FirebaseUser> OnLoginSuccess;

    /// <summary>
    /// 登录失败事件
    /// </summary>
    public event Action<AuthProvider, string> OnLoginFailed;

    /// <summary>
    /// 登出事件
    /// </summary>
    public event Action OnLogout;

    /// <summary>
    /// Firebase 初始化完成事件
    /// </summary>
    public event Action OnFirebaseInitialized;

    /// <summary>
    /// 用户信息更新事件
    /// </summary>
    public event Action<FirebaseUser> OnUserInfoUpdated;

    #endregion

    #region 状态

    /// <summary>Firebase 是否已初始化</summary>
    public bool IsFirebaseInitialized { get; private set; } = false;

    /// <summary>当前是否正在登录中</summary>
    public bool IsLoggingIn { get; private set; } = false;

    /// <summary>当前登录的 Firebase 用户</summary>
    public FirebaseUser CurrentUser => FirebaseAuth.DefaultInstance.CurrentUser;

    /// <summary>当前登录的提供商类型</summary>
    public AuthProvider CurrentAuthProvider { get; private set; } = AuthProvider.Anonymous;

    /// <summary>是否已登录</summary>
    public bool IsLoggedIn => CurrentUser != null;

    #endregion

    #region 内部引用

    private FirebaseAuth _auth;
    private DependencyStatus _dependencyStatus = DependencyStatus.UnavailableDisabled;

    #endregion

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();
        InitializeFirebase();
    }

    void OnDestroy()
    {
        if (_auth != null)
        {
            _auth.StateChanged -= AuthStateChanged;
        }
    }

    #endregion

    #region Firebase 初始化

    /// <summary>
    /// 初始化 Firebase
    /// </summary>
    private void InitializeFirebase()
    {
        Debug.Log("[FirebaseAuthManager] 开始初始化 Firebase...");

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            _dependencyStatus = task.Result;
            if (_dependencyStatus == DependencyStatus.Available)
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;
                _auth = FirebaseAuth.DefaultInstance;
                _auth.StateChanged += AuthStateChanged;

                IsFirebaseInitialized = true;
                Debug.Log("[FirebaseAuthManager] Firebase 初始化成功");

                // 检查是否有已登录的用户
                if (CurrentUser != null)
                {
                    Debug.Log("[FirebaseAuthManager] 检测到已登录用户: " + CurrentUser.UserId);
                }

                OnFirebaseInitialized?.Invoke();
            }
            else
            {
                Debug.LogError($"[FirebaseAuthManager] Firebase 初始化失败: {_dependencyStatus}");
                IsFirebaseInitialized = false;
            }
        });
    }

    /// <summary>
    /// Firebase 认证状态变化回调
    /// </summary>
    private void AuthStateChanged(object sender, EventArgs e)
    {
        if (_auth.CurrentUser != CurrentUser)
        {
            bool signedIn = CurrentUser != null;
            if (signedIn)
            {
                Debug.Log("[FirebaseAuthManager] 用户已登录: " + CurrentUser.UserId);
                OnUserInfoUpdated?.Invoke(CurrentUser);
            }
            else
            {
                Debug.Log("[FirebaseAuthManager] 用户已登出");
            }
        }
    }

    #endregion

    #region 登录入口

    /// <summary>
    /// Google 登录（通过原生 Google Sign-In API）
    /// 流程：AndroidJavaClass → Google Sign-In → ID Token → GoogleAuthProvider → Firebase
    /// </summary>
    public void SignInWithGoogle()
    {
        if (!CheckFirebaseReady()) return;
        if (CheckAlreadyLoggingIn()) return;

        CurrentAuthProvider = AuthProvider.Google;
        IsLoggingIn = true;

        // 使用 GoogleSignInHelper 获取 ID Token
        GoogleSignInHelper.Instance.SignIn(
            onSuccess: (idToken) =>
            {
                // 用 ID Token 创建 Firebase credential
                Credential credential = GoogleAuthProvider.GetCredential(idToken, null);
                SignInWithCredential(credential, AuthProvider.Google);
            },
            onError: (error) =>
            {
                IsLoggingIn = false;
                Debug.LogError($"[FirebaseAuthManager] Google 登录失败: {error}");
                OnLoginFailed?.Invoke(AuthProvider.Google, error);
            }
        );
    }

    /// <summary>
    /// Apple 登录
    /// </summary>
    public void SignInWithApple()
    {
        if (!CheckFirebaseReady()) return;
        if (CheckAlreadyLoggingIn()) return;

        CurrentAuthProvider = AuthProvider.Apple;
        IsLoggingIn = true;

        // 使用 AppleSignInHelper 获取 Apple ID Token 和 Authorization Code
        AppleSignInHelper.Instance.SignIn(
            onSuccess: (idToken, authorizationCode, nonce) =>
            {
                // 用 Apple ID Token + Nonce 创建 Firebase credential
                // nonce 是原始值（Firebase 会用它验证 Apple 返回的 Token）
                Credential credential = OAuthProvider.GetCredential(
                    "apple.com", idToken, string.IsNullOrEmpty(nonce) ? null : nonce, null
                );
                SignInWithCredential(credential, AuthProvider.Apple);
            },
            onError: (error) =>
            {
                IsLoggingIn = false;
                Debug.LogError($"[FirebaseAuthManager] Apple 登录失败: {error}");
                OnLoginFailed?.Invoke(AuthProvider.Apple, error);
            }
        );
    }

    /// <summary>
    /// Facebook 登录
    /// </summary>
    public void SignInWithFacebook()
    {
        if (!CheckFirebaseReady()) return;
        if (CheckAlreadyLoggingIn()) return;

        CurrentAuthProvider = AuthProvider.Facebook;
        IsLoggingIn = true;

        // 使用 FacebookSignInHelper 获取 Access Token
        FacebookSignInHelper.Instance.SignIn(
            onSuccess: (accessToken) =>
            {
                // 用 Facebook Access Token 创建 Firebase credential
                Credential credential = FacebookAuthProvider.GetCredential(accessToken);
                SignInWithCredential(credential, AuthProvider.Facebook);
            },
            onError: (error) =>
            {
                IsLoggingIn = false;
                Debug.LogError($"[FirebaseAuthManager] Facebook 登录失败: {error}");
                OnLoginFailed?.Invoke(AuthProvider.Facebook, error);
            }
        );
    }

    /// <summary>
    /// 匿名登录
    /// </summary>
    public void SignInAnonymously()
    {
        if (!CheckFirebaseReady()) return;
        if (CheckAlreadyLoggingIn()) return;

        CurrentAuthProvider = AuthProvider.Anonymous;
        IsLoggingIn = true;

#if UNITY_EDITOR
        Debug.Log("[FirebaseAuthManager] Editor 模式：模拟匿名登录");
        SimulateLoginSuccess(AuthProvider.Anonymous);
#else
        _auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            IsLoggingIn = false;

            if (task.IsFaulted || task.IsCanceled)
            {
                string error = task.Exception?.InnerException?.Message ?? "匿名登录失败";
                Debug.LogError($"[FirebaseAuthManager] 匿名登录失败: {error}");
                OnLoginFailed?.Invoke(AuthProvider.Anonymous, error);
                return;
            }

            FirebaseUser newUser = task.Result.User;
            Debug.Log($"[FirebaseAuthManager] 匿名登录成功, UserId: {newUser.UserId}");

            // 同步用户数据到 UserDataManager
            SyncUserToDataManager(newUser, AuthProvider.Anonymous);

            // 云端数据同步
            FirestoreManager.Instance.SyncAfterLogin(success =>
            {
                Debug.Log($"[FirebaseAuthManager] 匿名登录 Firestore 同步{(success ? "成功" : "失败")}");
            });

            OnLoginSuccess?.Invoke(AuthProvider.Anonymous, newUser);
        });
#endif
    }

    #endregion

    #region 核心：Credential 登录

    /// <summary>
    /// 使用 Firebase Credential 登录
    /// 所有第三方登录最终都走这个方法
    /// </summary>
    private void SignInWithCredential(Credential credential, AuthProvider provider)
    {
#if UNITY_EDITOR
        // Editor 模式：不调用真实 Firebase（避免 USE_AUTH_EMULATOR 警告 + Editor 下无法访问真实 SDK）
        Debug.Log($"[FirebaseAuthManager] Editor 模式：跳过真实 Firebase，使用模拟 {provider} 登录");
        SimulateLoginSuccess(provider);
#else
        _auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
        {
            IsLoggingIn = false;

            if (task.IsFaulted || task.IsCanceled)
            {
                string error = task.Exception?.InnerException?.Message ?? "登录失败";
                Debug.LogError($"[FirebaseAuthManager] {provider} 登录失败: {error}");
                OnLoginFailed?.Invoke(provider, error);
                return;
            }

            FirebaseUser newUser = task.Result;
            Debug.Log($"[FirebaseAuthManager] {provider} 登录成功, UserId: {newUser.UserId}");

            // 同步用户数据到 UserDataManager
            SyncUserToDataManager(newUser, provider);

            // 下载并缓存头像（仅第三方登录有 photoUrl）
            if (!string.IsNullOrEmpty(newUser.PhotoUrl?.ToString()))
            {
                StartCoroutine(DownloadAvatarCoroutine(newUser.PhotoUrl.ToString(), provider));
            }
            FirestoreManager.Instance.LoadUserData(success =>
            {
                FirestoreManager.Instance.SaveUserData(); // 把最新本地数据推上去
            });
            // 云端数据同步：拉云端补本地 → 推最新到云端
            FirestoreManager.Instance.SyncAfterLogin(success =>
            {
                Debug.Log($"[FirebaseAuthManager] Firestore 同步{(success ? "成功" : "失败")}");
            });

            OnLoginSuccess?.Invoke(provider, newUser);
        });
#endif
    }

    #endregion

    #region Editor 模拟

#if UNITY_EDITOR
    /// <summary>
    /// Editor 模拟登录成功
    /// 构造假的用户信息，触发完整的登录成功链路，无需真实 Firebase / 第三方服务
    /// </summary>
    private void SimulateLoginSuccess(AuthProvider provider)
    {
        IsLoggingIn = false;

        Debug.Log($"[FirebaseAuthManager] ✅ Editor 模拟登录成功！({provider})");

        UserInfo mockUserInfo = new UserInfo
        {
            uid = $"mock_editor_user_{System.DateTime.Now.Ticks}",
            displayName = GetMockDisplayName(provider),
            email = GetMockEmail(provider),
            photoUrl = "",
            providerId = GetProviderIdString(provider),
            isAnonymous = provider == AuthProvider.Anonymous,
        };

        UserDataManager.Instance.SyncFromFirebaseUser(mockUserInfo, provider);

        // Editor 下没有真实 FirebaseUser 对象，传 null
        OnLoginSuccess?.Invoke(provider, null);
    }

    private static string GetMockDisplayName(AuthProvider provider) => provider switch
    {
        AuthProvider.Google => "Google 测试用户",
        AuthProvider.Apple => "Apple 测试用户",
        AuthProvider.Facebook => "Facebook 测试用户",
        AuthProvider.Anonymous => "游客用户",
        _ => "测试用户"
    };

    private static string GetMockEmail(AuthProvider provider) => provider switch
    {
        AuthProvider.Google => "test@google.com",
        AuthProvider.Apple => "test@icloud.com",
        AuthProvider.Facebook => "test@facebook.com",
        _ => ""
    };

    private static string GetProviderIdString(AuthProvider provider) => provider switch
    {
        AuthProvider.Google => "google.com",
        AuthProvider.Apple => "apple.com",
        AuthProvider.Facebook => "facebook.com",
        AuthProvider.Anonymous => "firebase",
        _ => "unknown"
    };
#endif

    #endregion

    #region 登出

    /// <summary>
    /// 登出当前用户
    /// </summary>
    public void SignOut()
    {
        if (_auth == null) return;

        // 登出对应的第三方 SDK
        switch (CurrentAuthProvider)
        {
            case AuthProvider.Google:
                GoogleSignInHelper.Instance.SignOut();
                break;
            case AuthProvider.Facebook:
                FacebookSignInHelper.Instance.SignOut();
                break;
        }

        _auth.SignOut();
        CurrentAuthProvider = AuthProvider.Anonymous;

        Debug.Log("[FirebaseAuthManager] 用户已登出");
        OnLogout?.Invoke();
    }

    /// <summary>
    /// 删除当前用户账户
    /// </summary>
    public void DeleteUser(Action onSuccess, Action<string> onError)
    {
        if (CurrentUser == null)
        {
            onError?.Invoke("没有登录的用户");
            return;
        }

        CurrentUser.DeleteAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                string error = task.Exception?.InnerException?.Message ?? "删除账户失败";
                Debug.LogError($"[FirebaseAuthManager] 删除账户失败: {error}");
                onError?.Invoke(error);
                return;
            }

            Debug.Log("[FirebaseAuthManager] 账户已删除");

            // 同步删除云端用户数据
            FirestoreManager.Instance.DeleteUserData();

            SignOut();
            onSuccess?.Invoke();
        });
    }

    #endregion

    #region 用户信息获取

    /// <summary>
    /// 获取当前用户的基本信息
    /// </summary>
    public UserInfo GetCurrentUserBasicInfo()
    {
        if (CurrentUser == null) return null;

        return new UserInfo
        {
            uid = CurrentUser.UserId,
            displayName = CurrentUser.DisplayName ?? string.Empty,
            email = CurrentUser.Email ?? string.Empty,
            photoUrl = CurrentUser.PhotoUrl?.ToString() ?? string.Empty,
            providerId = CurrentUser.ProviderId ?? string.Empty,
            isAnonymous = CurrentUser.IsAnonymous,
        };
    }

    /// <summary>
    /// 获取当前用户的提供商信息列表
    /// 一个用户可能关联了多个登录方式
    /// </summary>
    public List<ProviderInfo> GetProviderInfoList()
    {
        List<ProviderInfo> result = new List<ProviderInfo>();

        if (CurrentUser == null) return result;

        foreach (var userInfo in CurrentUser.ProviderData)
        {
            result.Add(new ProviderInfo
            {
                providerId = userInfo.ProviderId ?? string.Empty,
                uid = userInfo.UserId ?? string.Empty,
                displayName = userInfo.DisplayName ?? string.Empty,
                email = userInfo.Email ?? string.Empty,
                photoUrl = userInfo.PhotoUrl?.ToString() ?? string.Empty,
            });
        }

        return result;
    }

    /// <summary>
    /// 获取用户的所有关联登录方式
    /// </summary>
    public List<string> GetLinkedProviders()
    {
        List<string> providers = new List<string>();

        if (CurrentUser == null) return providers;

        foreach (var userInfo in CurrentUser.ProviderData)
        {
            providers.Add(userInfo.ProviderId);
        }

        return providers;
    }

    /// <summary>
    /// 更新用户显示名
    /// </summary>
    public void UpdateUserProfile(string displayName, string photoUrl = null, Action<bool> onComplete = null)
    {
        if (CurrentUser == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        UserProfile profile = new UserProfile
        {
            DisplayName = displayName,
        };

        if (!string.IsNullOrEmpty(photoUrl))
        {
            profile.PhotoUrl = new Uri(photoUrl);
        }

        CurrentUser.UpdateUserProfileAsync(profile).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirebaseAuthManager] 更新用户资料失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            Debug.Log("[FirebaseAuthManager] 用户资料已更新");
            OnUserInfoUpdated?.Invoke(CurrentUser);
            onComplete?.Invoke(true);
        });
    }

    #endregion

    #region 账号关联

    /// <summary>
    /// 将当前用户关联到另一个登录提供商
    /// 例如：先用匿名登录，之后关联 Google 账号
    /// </summary>
    public void LinkWithProvider(AuthProvider provider)
    {
        if (CurrentUser == null)
        {
            Debug.LogError("[FirebaseAuthManager] 没有当前用户，无法关联账号");
            return;
        }

        switch (provider)
        {
            case AuthProvider.Google:
                GoogleSignInHelper.Instance.SignIn(
                    onSuccess: (idToken) =>
                    {
                        Credential credential = GoogleAuthProvider.GetCredential(idToken, null);
                        LinkWithCredential(credential, provider);
                    },
                    onError: (error) =>
                    {
                        Debug.LogError($"[FirebaseAuthManager] 关联 Google 账号失败: {error}");
                    }
                );
                break;

            case AuthProvider.Apple:
                AppleSignInHelper.Instance.SignIn(
                    onSuccess: (idToken, authCode, nonce) =>
                    {
                        Credential credential = OAuthProvider.GetCredential("apple.com", idToken, string.IsNullOrEmpty(nonce) ? null : nonce, null);
                        LinkWithCredential(credential, provider);
                    },
                    onError: (error) =>
                    {
                        Debug.LogError($"[FirebaseAuthManager] 关联 Apple 账号失败: {error}");
                    }
                );
                break;

            case AuthProvider.Facebook:
                FacebookSignInHelper.Instance.SignIn(
                    onSuccess: (accessToken) =>
                    {
                        Credential credential = FacebookAuthProvider.GetCredential(accessToken);
                        LinkWithCredential(credential, provider);
                    },
                    onError: (error) =>
                    {
                        Debug.LogError($"[FirebaseAuthManager] 关联 Facebook 账号失败: {error}");
                    }
                );
                break;
        }
    }

    /// <summary>
    /// 关联 Credential 到当前用户
    /// </summary>
    private void LinkWithCredential(Credential credential, AuthProvider provider)
    {
        CurrentUser.LinkWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirebaseAuthManager] 关联 {provider} 账号失败: {task.Exception?.InnerException?.Message}");
                return;
            }

            Debug.Log($"[FirebaseAuthManager] 成功关联 {provider} 账号");
        });
    }

    #endregion

    #region 数据同步

    /// <summary>
    /// 将 Firebase 用户信息同步到 UserDataManager
    /// Google 登录时额外调用 GoogleUserInfoHelper 写入时间戳、邮箱验证等完整字段
    /// </summary>
    private void SyncUserToDataManager(FirebaseUser user, AuthProvider provider)
    {
        if (user == null) return;

        switch (provider)
        {
            case AuthProvider.Google:
                // Google 登录：直接走 Helper，包含时间戳 / 邮箱验证 / ProviderData 全量字段
                GoogleUserInfoHelper.SyncToUserDataManager(user);
                break;
            case AuthProvider.Apple:
                // Apple 登录：直接走 Helper，包含时间戳 / 邮箱验证 / ProviderData 全量字段
                AppleUserInfoHelper.SyncToUserDataManager(user);
                break;
            case AuthProvider.Facebook:
                // Facebook 登录：直接走 Helper，包含时间戳 / 邮箱验证 / ProviderData 全量字段
                FacebookUserInfoHelper.SyncToUserDataManager(user);
                break;
            default:
                // Anonymous 等其他登录方式走通用结构
                UserInfo userInfo = new UserInfo
                {
                    uid = user.UserId,
                    displayName = user.DisplayName ?? string.Empty,
                    email = user.Email ?? string.Empty,
                    photoUrl = user.PhotoUrl?.ToString() ?? string.Empty,
                    providerId = user.ProviderId ?? string.Empty,
                    isAnonymous = user.IsAnonymous,
                };
                UserDataManager.Instance.SyncFromFirebaseUser(userInfo, provider);
                break;
        }
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 登录成功后下载并缓存头像到本地
    /// 根据 provider 路由到对应的 Helper（Google / Apple / Facebook）
    /// </summary>
    private IEnumerator DownloadAvatarCoroutine(string photoUrl, AuthProvider provider)
    {
        switch (provider)
        {
            case AuthProvider.Apple:
                yield return AppleUserInfoHelper.LoadAndCacheAvatarCoroutine(
                    photoUrl,
                    sprite =>
                    {
                        if (sprite != null)
                        {
                            Debug.Log("[FirebaseAuthManager] Apple 头像下载并缓存成功");
                            OnUserInfoUpdated?.Invoke(CurrentUser);
                        }
                    }
                );
                break;
            case AuthProvider.Facebook:
                yield return FacebookUserInfoHelper.LoadAndCacheAvatarCoroutine(
                    photoUrl,
                    sprite =>
                    {
                        if (sprite != null)
                        {
                            Debug.Log("[FirebaseAuthManager] Facebook 头像下载并缓存成功");
                            OnUserInfoUpdated?.Invoke(CurrentUser);
                        }
                    }
                );
                break;
            default:
                // Google / Anonymous 走 Google Helper（共享缓存路径）
                yield return GoogleUserInfoHelper.LoadAndCacheAvatarCoroutine(
                    photoUrl,
                    sprite =>
                    {
                        if (sprite != null)
                        {
                            Debug.Log("[FirebaseAuthManager] 头像下载并缓存成功");
                            OnUserInfoUpdated?.Invoke(CurrentUser);
                        }
                    }
                );
                break;
        }
    }

    /// <summary>
    /// 检查 Firebase 是否就绪
    /// </summary>
    private bool CheckFirebaseReady()
    {
        if (!IsFirebaseInitialized)
        {
            Debug.LogWarning("[FirebaseAuthManager] Firebase 尚未初始化完成，请稍后重试");
            OnLoginFailed?.Invoke(CurrentAuthProvider, "Firebase 尚未初始化完成");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 检查是否正在登录中
    /// </summary>
    private bool CheckAlreadyLoggingIn()
    {
        if (IsLoggingIn)
        {
            Debug.LogWarning("[FirebaseAuthManager] 正在登录中，请勿重复操作");
            return true;
        }
        return false;
    }

    /// <summary>
    /// 获取提供商的显示名称
    /// </summary>
    public string GetProviderDisplayName(AuthProvider provider)
    {
        return provider switch
        {
            AuthProvider.Google => "Google",
            AuthProvider.Apple => "Apple",
            AuthProvider.Facebook => "Facebook",
            AuthProvider.Anonymous => "游客",
            _ => "未知"
        };
    }

    #endregion
}

/// <summary>
/// 用户基本信息数据类
/// </summary>
[System.Serializable]
public class UserInfo
{
    /// <summary>Firebase UID</summary>
    public string uid;

    /// <summary>显示名称</summary>
    public string displayName;

    /// <summary>邮箱</summary>
    public string email;

    /// <summary>头像 URL</summary>
    public string photoUrl;

    /// <summary>提供商 ID</summary>
    public string providerId;

    /// <summary>是否匿名用户</summary>
    public bool isAnonymous;
}

/// <summary>
/// 提供商信息数据类
/// </summary>
[System.Serializable]
public class ProviderInfo
{
    /// <summary>提供商 ID（如 google.com, apple.com, facebook.com）</summary>
    public string providerId;

    /// <summary>提供商方的用户 ID</summary>
    public string uid;

    /// <summary>显示名称</summary>
    public string displayName;

    /// <summary>邮箱</summary>
    public string email;

    /// <summary>头像 URL</summary>
    public string photoUrl;
}
