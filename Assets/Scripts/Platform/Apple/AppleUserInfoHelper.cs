using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using Firebase.Auth;

/// <summary>
/// Apple 登录后提取的用户信息数据类
/// FirebaseUser 能拿到的字段全在这里
/// 注意：Apple 首次登录后会返回完整姓名和邮箱，后续登录 FirebaseUser 只保留 UID+providerId
/// </summary>
[Serializable]
public class AppleUserInfo
{
    [Header("基础信息")]
    public string firebaseUid;      // Firebase 唯一 ID
    public string email;            // 邮箱（首次登录有，后续可能为空 / private relay）
    public string displayName;      // 显示名称（首次登录有全名，后续可能为空）
    public string photoUrl;         // 头像 URL（Apple 通常不提供头像）
    public string providerId;       // 登录提供方 "apple.com"

    [Header("时间信息")]
    public long creationTimestamp;   // 账号创建时间（毫秒时间戳）
    public long lastSignInTimestamp; // 上次登录时间（毫秒时间戳）

    [Header("账号状态")]
    public bool isEmailVerified;    // 邮箱是否已验证（Apple 登录后通常为 true）
    public bool isAnonymous;        // 是否匿名用户

    /// <summary>账号创建时间（本地时间）</summary>
    public DateTime CreationTime => creationTimestamp > 0
        ? DateTimeOffset.FromUnixTimeMilliseconds(creationTimestamp).LocalDateTime
        : DateTime.MinValue;

    /// <summary>上次登录时间（本地时间）</summary>
    public DateTime LastSignInTime => lastSignInTimestamp > 0
        ? DateTimeOffset.FromUnixTimeMilliseconds(lastSignInTimestamp).LocalDateTime
        : DateTime.MinValue;

    public override string ToString()
    {
        return $"[AppleUserInfo] UID={firebaseUid}, Email={email}, Name={displayName}, " +
               $"Photo={(string.IsNullOrEmpty(photoUrl) ? "无" : "有")}, Provider={providerId}, " +
               $"EmailVerified={isEmailVerified}, " +
               $"Created={CreationTime:yyyy-MM-dd}, LastSignIn={LastSignInTime:yyyy-MM-dd HH:mm}";
    }
}

/// <summary>
/// 从 FirebaseUser 提取 Apple 账号信息，并写入 UserDataManager 的工具类
/// 登录成功后直接调用，无需修改任何原生代码
/// </summary>
public static class AppleUserInfoHelper
{
    #region 提取信息

    /// <summary>
    /// 从当前 Firebase 登录用户提取信息
    /// 在 FirebaseAuthManager 登录成功后随时调用
    /// </summary>
    public static AppleUserInfo GetCurrentUser()
    {
        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null)
        {
            Debug.LogWarning("[AppleUserInfoHelper] 当前无 Firebase 登录用户");
            return null;
        }
        return ExtractFromFirebaseUser(user);
    }

    /// <summary>
    /// 从指定 FirebaseUser 提取主用户信息
    /// 包含：UID、邮箱、显示名、头像、providerId、时间戳、邮箱验证状态
    /// </summary>
    public static AppleUserInfo ExtractFromFirebaseUser(FirebaseUser user)
    {
        if (user == null) return null;

        var info = new AppleUserInfo
        {
            firebaseUid      = user.UserId ?? string.Empty,
            email            = user.Email ?? string.Empty,
            displayName      = user.DisplayName ?? string.Empty,
            photoUrl         = user.PhotoUrl?.ToString() ?? string.Empty,
            providerId       = user.ProviderId ?? string.Empty,
            isEmailVerified  = user.IsEmailVerified,
            isAnonymous      = user.IsAnonymous,
        };

        if (user.Metadata != null)
        {
            info.creationTimestamp   = (long)user.Metadata.CreationTimestamp;
            info.lastSignInTimestamp = (long)user.Metadata.LastSignInTimestamp;
        }

        return info;
    }

    /// <summary>
    /// 从 Apple 提供方 ProviderData 中提取信息（更精准）
    /// 如果用户同时绑定了多种登录方式，专门取 apple.com 那条记录
    /// 注意：Apple 首次登录后 DisplayName/Email 已落 FirebaseUser 主字段，
    /// 后续登录可能为空，这是 Firebase Auth 的正常行为，非 bug
    /// </summary>
    public static AppleUserInfo ExtractFromAppleProvider(FirebaseUser user)
    {
        if (user == null) return null;

        foreach (var userInfo in user.ProviderData)
        {
            if (userInfo.ProviderId == "apple.com")
            {
                return new AppleUserInfo
                {
                    firebaseUid      = user.UserId ?? string.Empty,   // 主 user 才有 Firebase UID
                    email            = userInfo.Email ?? string.Empty,
                    displayName      = userInfo.DisplayName ?? string.Empty,
                    photoUrl         = userInfo.PhotoUrl?.ToString() ?? string.Empty,
                    providerId       = userInfo.ProviderId,
                    creationTimestamp   = (long)(user.Metadata?.CreationTimestamp   ?? 0),
                    lastSignInTimestamp = (long)(user.Metadata?.LastSignInTimestamp ?? 0),
                    isEmailVerified  = user.IsEmailVerified,
                    isAnonymous      = user.IsAnonymous,
                };
            }
        }

        // 没找到 apple.com 提供方，降级为主用户信息
        Debug.LogWarning("[AppleUserInfoHelper] 未找到 Apple 提供方数据，降级返回主用户信息");
        return ExtractFromFirebaseUser(user);
    }

    #endregion

    #region 同步到 UserDataManager

    /// <summary>
    /// 提取 Apple 用户信息并同步到 UserDataManager（一步到位）
    /// Apple 登录成功后在 FirebaseAuthManager.OnLoginSuccess 回调里调用此方法即可
    /// 优先从 apple.com ProviderData 取信息（更精准），失败则降级到主用户
    /// </summary>
    /// <param name="user">登录成功的 FirebaseUser</param>
    public static void SyncToUserDataManager(FirebaseUser user)
    {
        if (user == null)
        {
            Debug.LogWarning("[AppleUserInfoHelper] SyncToUserDataManager: FirebaseUser 为 null，跳过");
            return;
        }

        AppleUserInfo info = ExtractFromAppleProvider(user);
        if (info == null)
        {
            Debug.LogWarning("[AppleUserInfoHelper] SyncToUserDataManager: 无法提取用户信息");
            return;
        }

        Debug.Log($"[AppleUserInfoHelper] 开始同步 Apple 用户数据: {info}");

        UserDataManager.Instance.SyncFromAppleUserInfo(info);
    }

    /// <summary>
    /// 直接将已提取的 AppleUserInfo 同步到 UserDataManager
    /// </summary>
    public static void SyncToUserDataManager(AppleUserInfo info)
    {
        if (info == null)
        {
            Debug.LogWarning("[AppleUserInfoHelper] SyncToUserDataManager: AppleUserInfo 为 null，跳过");
            return;
        }

        Debug.Log($"[AppleUserInfoHelper] 同步 Apple 用户数据: {info}");
        UserDataManager.Instance.SyncFromAppleUserInfo(info);
    }

    #endregion

    #region 头像下载与本地缓存

    private const string AVATAR_CACHE_FILENAME = "user_avatar_apple.png";

    /// <summary>
    /// 本地头像缓存路径（persistentDataPath/user_avatar_apple.png）
    /// </summary>
    public static string LocalAvatarPath =>
        System.IO.Path.Combine(Application.persistentDataPath, AVATAR_CACHE_FILENAME);

    /// <summary>
    /// 本地是否存在头像缓存
    /// </summary>
    public static bool HasLocalAvatarCache => System.IO.File.Exists(LocalAvatarPath);

    /// <summary>
    /// 下载头像并缓存到本地（persistentDataPath/user_avatar_apple.png）
    /// 优先读本地缓存，无缓存才走网络下载
    /// 用法：StartCoroutine(AppleUserInfoHelper.LoadAndCacheAvatarCoroutine(url, sprite => avatarImage.sprite = sprite));
    /// </summary>
    /// <param name="url">photoUrl，来自 UserDataManager.Instance.PhotoUrl</param>
    /// <param name="onComplete">回调：Sprite（失败时为 null）</param>
    /// <param name="forceRefresh">强制重新下载，忽略本地缓存</param>
    public static IEnumerator LoadAndCacheAvatarCoroutine(
        string url,
        Action<Sprite> onComplete,
        bool forceRefresh = false)
    {
        // 1. 优先读本地缓存
        if (!forceRefresh && System.IO.File.Exists(LocalAvatarPath))
        {
            byte[] bytes = System.IO.File.ReadAllBytes(LocalAvatarPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
            {
                Debug.Log("[AppleUserInfoHelper] 头像已从本地缓存加载");
                onComplete?.Invoke(TextureToSprite(tex));
                yield break;
            }
            // LoadImage 失败，继续走网络下载
            UnityEngine.Object.Destroy(tex);
        }

        // 2. 网络下载
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogWarning("[AppleUserInfoHelper] 头像 URL 为空，跳过下载");
            onComplete?.Invoke(null);
            yield break;
        }

        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = ((DownloadHandlerTexture)req.downloadHandler).texture;

                // 3. 保存到本地 PNG
                byte[] pngBytes = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes(LocalAvatarPath, pngBytes);
                Debug.Log($"[AppleUserInfoHelper] 头像已下载并缓存到: {LocalAvatarPath}");

                onComplete?.Invoke(TextureToSprite(tex));
            }
            else
            {
                Debug.LogWarning($"[AppleUserInfoHelper] 头像下载失败: {req.error}");
                onComplete?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// 删除本地头像缓存（退出登录时调用）
    /// </summary>
    public static void ClearLocalAvatarCache()
    {
        if (System.IO.File.Exists(LocalAvatarPath))
        {
            System.IO.File.Delete(LocalAvatarPath);
            Debug.Log("[AppleUserInfoHelper] 本地头像缓存已清除");
        }
    }

    private static Sprite TextureToSprite(Texture2D tex)
    {
        return Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            Vector2.one * 0.5f
        );
    }

    #endregion
}
