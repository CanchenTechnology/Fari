using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using XFGameFrameWork;

/// <summary>
/// Firestore 数据库管理器
/// 负责用户数据的云端读写（文档型数据库）
///
/// 数据结构：
///   users/{firebaseUid}        ← 用户资料文档
///     - displayName, email, photoUrl, birthday, birthTime, city
///     - avatarType, loginType, isEmailVerified, selectedOracle, timezone
///     - createdAt, lastSignInAt
///
/// 使用前确保：
///   1. Firebase Unity SDK 已导入 Firestore 模块
///   2. FirebaseAuthManager 已初始化（Firestore 依赖 FirebaseApp）
/// </summary>
public class FirestoreManager : MonoSingleton<FirestoreManager>
{
    #region 状态

    private FirebaseFirestore _db;
    private bool _isInitialized = false;

    /// <summary>Firestore 是否已初始化</summary>
    public bool IsInitialized => _isInitialized;

    #endregion

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();
        InitFirestore();
    }

    private void InitFirestore()
    {
        // 核心逻辑：Firestore 的运行依赖于 FirebaseApp 的成功初始化。
        // 所以这里先检查身份验证管理器（FirebaseAuthManager）是否已经准备好了。
        if (FirebaseAuthManager.Instance != null && FirebaseAuthManager.Instance.IsFirebaseInitialized)
        {
            // 如果已经好了，直接初始化 Firestore
            OnFirebaseReady();
        }
        else
        {
            // 如果还没好，就订阅初始化完成的事件，等它好了再回调 OnFirebaseReady
            FirebaseAuthManager.Instance.OnFirebaseInitialized += OnFirebaseReady;
        }
    }

    private void OnFirebaseReady()
    {
        try
        {
            // 获取当前默认的 Firestore 数据库实例
            _db = FirebaseFirestore.DefaultInstance;
            _isInitialized = true;
            Debug.Log("[FirestoreManager] Firestore 初始化完成");
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirestoreManager] Firestore 初始化失败: {e.Message}");
        }
    }

    #endregion

    #region 写入

    /// <summary>
    /// 保存当前用户数据到 Firestore（合并模式，不覆盖未传字段）
    /// 适用于：登录后同步、用户修改资料后推送
    /// </summary>
    public void SaveUserData(Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;

        var ud = UserDataManager.Instance;
        // 定位到当前用户的专属文档路径：集合 "users" -> 文档 "当前用户的 UID"
        DocumentReference docRef = _db.Collection("users").Document(ud.FirebaseUid);

        // 将本地内存中的数据打包成字典（Firestore 接收键值对形式的数据）
        var data = new Dictionary<string, object>
        {
            { "displayName",     ud.UserName },
            { "email",           ud.Email },
            { "photoUrl",        ud.PhotoUrl },
            { "birthday",        ud.Birthday },
            { "birthTime",       ud.BirthTime },
            { "city",            ud.City },
            { "avatarType",      (int)ud.CurrentAvatar },
            { "loginType",       ud.CurrentLoginType.ToString() },
            { "isEmailVerified", ud.IsEmailVerified },
            { "selectedOracle",  GetCurrentOracleId() },
            { "timezone",        GetLocalTimezoneId() },
            { "profileUpdatedAt", FieldValue.ServerTimestamp },
            { "lastSignInAt",    FieldValue.ServerTimestamp }, // 这里使用服务器时间，防止玩家本地设备时间不准
        };

        // SetOptions.MergeAll 是关键：它是“合并”操作而不是“覆盖”操作。
        // 这意味着云端有、但 data 字典里没有的字段，依然会保留在云端，不会被删掉。
        docRef.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            // ContinueWithOnMainThread 确保回调回到 Unity 的主线程执行，防止报错
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 保存失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }
            Debug.Log("[FirestoreManager] 用户数据已保存到云端");
            onComplete?.Invoke(true);
        });
    }

    /// <summary>
    /// 首次注册时写入完整文档（含 createdAt）
    /// 适用于：新用户首次登录
    /// </summary>
    public void CreateUserData(Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;

        var ud = UserDataManager.Instance;
        DocumentReference docRef = _db.Collection("users").Document(ud.FirebaseUid);

        var data = new Dictionary<string, object>
        {
            { "displayName",     ud.UserName },
            { "email",           ud.Email },
            { "photoUrl",        ud.PhotoUrl },
            { "birthday",        ud.Birthday },
            { "birthTime",       ud.BirthTime },
            { "city",            ud.City },
            { "avatarType",      (int)ud.CurrentAvatar },
            { "loginType",       ud.CurrentLoginType.ToString() },
            { "isEmailVerified", ud.IsEmailVerified },
            { "selectedOracle",  GetCurrentOracleId() },
            { "timezone",        GetLocalTimezoneId() },
            { "membershipStatus", "free" },
            { "profileUpdatedAt", FieldValue.ServerTimestamp },
            { "createdAt",       FieldValue.ServerTimestamp }, // 唯一和 Save 的区别：写入账号创建的服务器时间
            { "lastSignInAt",    FieldValue.ServerTimestamp },
        };

        // 首次创建直接 SetAsync，不需要合并模式
        docRef.SetAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 创建失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }
            Debug.Log("[FirestoreManager] 用户数据已创建到云端");
            onComplete?.Invoke(true);
        });
    }

    /// <summary>
    /// 只更新部分字段（比 SaveUserData 更轻量）
    /// 适用于：改生日、改城市等局部修改
    /// </summary>
    public void UpdateUserFields(Dictionary<string, object> fields, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        if (fields == null || fields.Count == 0)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (!fields.ContainsKey("profileUpdatedAt"))
            fields["profileUpdatedAt"] = FieldValue.ServerTimestamp;

        string uid = UserDataManager.Instance.FirebaseUid;
        DocumentReference docRef = _db.Collection("users").Document(uid);

        // UpdateAsync 只会修改传入字典里包含的字段，网络开销极小
        docRef.UpdateAsync(fields).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 更新字段失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }
            // string.Join 方便在控制台打印出具体更新了哪些字段
            Debug.Log($"[FirestoreManager] 字段已更新: {string.Join(", ", fields.Keys)}");
            onComplete?.Invoke(true);
        });
    }

    #endregion

    #region 读取

    /// <summary>
    /// 从 Firestore 读取当前用户数据并应用到本地
    /// </summary>
    public void LoadUserData(Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;

        string uid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning("[FirestoreManager] Firebase UID 为空，无法读取");
            onComplete?.Invoke(false);
            return;
        }

        DocumentReference docRef = _db.Collection("users").Document(uid);
        // GetSnapshotAsync 会拉取当前文档的最新快照（相当于下载一次文档状态）
        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 读取失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            // task.Result 就是拉下来的快照数据
            DocumentSnapshot snapshot = task.Result;
            if (!snapshot.Exists)
            {
                // 如果 Exists 为 false，说明云端查无此人（可能是刚注册还没来得及上传，或者文档被删了）
                Debug.Log("[FirestoreManager] 云端无用户数据，首次登录");
                onComplete?.Invoke(false);
                return;
            }

            // 如果有数据，就执行本地解析和同步
            ApplyCloudToLocal(snapshot);
            Debug.Log("[FirestoreManager] 云端用户数据已加载到本地");
            onComplete?.Invoke(true);
        });
    }

    /// <summary>
    /// 读取指定用户的数据（查看他人资料等场景）
    /// </summary>
    public void LoadUserDataByUid(string uid, Action<Dictionary<string, object>> onComplete)
    {
        // 传入弃元 _ 因为这里不需要知道检查结果的 bool，只管失败时返回 null
        if (!CheckReady(_ => onComplete?.Invoke(null))) return;

        DocumentReference docRef = _db.Collection("users").Document(uid);
        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
            {
                onComplete?.Invoke(null);
                return;
            }

            // 将云端的文档数据直接转成字典返回给业务逻辑处理
            var data = task.Result.ToDictionary();
            onComplete?.Invoke(data);
        });
    }

    /// <summary>
    /// 检查云端是否已有该用户数据
    /// </summary>
    public void CheckUserExists(string uid, Action<bool> onComplete)
    {
        if (!CheckReady(_ => onComplete?.Invoke(false))) return;

        DocumentReference docRef = _db.Collection("users").Document(uid);
        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                onComplete?.Invoke(false);
                return;
            }
            // 返回该文档是否真实存在的布尔值
            onComplete?.Invoke(task.Result.Exists);
        });
    }

    #endregion

    #region 删除

    /// <summary>
    /// 删除当前用户云端数据（注销账号时调用）
    /// </summary>
    public void DeleteUserData(Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;

        string uid = UserDataManager.Instance.FirebaseUid;
        DocumentReference docRef = _db.Collection("users").Document(uid);

        // DeleteAsync 会彻底清除这个文档节点
        docRef.DeleteAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 删除失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }
            Debug.Log("[FirestoreManager] 云端用户数据已删除");
            onComplete?.Invoke(true);
        });
    }

    #endregion

    #region 登录后完整同步流程

    /// <summary>
    /// 登录成功后调用的完整同步流程
    /// </summary>
    public void SyncAfterLogin(Action<bool> onComplete = null)
    {
        if (!_isInitialized)
        {
            onComplete?.Invoke(false);
            return;
        }

        string uid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            onComplete?.Invoke(false);
            return;
        }

        DocumentReference docRef = _db.Collection("users").Document(uid);
        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 同步失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            DocumentSnapshot snapshot = task.Result;

            if (!snapshot.Exists)
            {
                // 情况 1：云端无数据 → 首次登录，执行全新创建逻辑，打上创建时间戳
                CreateUserData(onComplete);
            }
            else
            {
                // 情况 2：云端有数据 → 老用户换设备/重新登录
                // 第一步：先用云端数据补齐本地的空缺部分
                ApplyCloudToLocal(snapshot);
                // 第二步：再把本地最终的合并结果推给云端保存（同时刷新最后登录时间 lastSignInAt）
                SaveUserData(onComplete);
            }
        });
    }

    #endregion

    #region 内部方法

    /// <summary>
    /// 将云端快照应用到本地 UserDataManager
    /// </summary>
    private void ApplyCloudToLocal(DocumentSnapshot snapshot)
    {
        if (snapshot == null || !snapshot.Exists) return;

        var ud = UserDataManager.Instance;

        // === 基础信息策略：本地为空才用云端的 ===
        // 这样做的原因是：如果玩家在无网时改了名字（存在本地），连网时这里不会拿云端的旧名字去冲掉玩家刚改的新名字
        // TryGetValue 是一种安全的取值方式，如果云端没有该字段，不会报错，只会返回 false
        if (string.IsNullOrWhiteSpace(ud.UserName) && snapshot.TryGetValue("displayName", out string cloudName))
            ud.SetUserName(cloudName);

        if (string.IsNullOrWhiteSpace(ud.Birthday) && snapshot.TryGetValue("birthday", out string cloudBirthday))
            ud.SetBirthday(cloudBirthday);

        if (string.IsNullOrWhiteSpace(ud.BirthTime) && snapshot.TryGetValue("birthTime", out string cloudBirthTime))
            ud.SetBirthTime(cloudBirthTime);

        if (string.IsNullOrWhiteSpace(ud.City) && snapshot.TryGetValue("city", out string cloudCity))
            ud.SetCity(cloudCity);

        // === 账户信息策略：云端优先 ===
        // 因为这些数据（如邮箱、头像验证状态等）是高权限或第三方获取的，必须以服务器状态为准
        if (snapshot.TryGetValue("email", out string cloudEmail))
            ud.SetEmail(cloudEmail);

        if (snapshot.TryGetValue("photoUrl", out string cloudPhotoUrl))
            ud.SetPhotoUrl(cloudPhotoUrl);

        if (snapshot.TryGetValue("isEmailVerified", out bool cloudVerified))
            ud.SetEmailVerified(cloudVerified);

        if (snapshot.TryGetValue("avatarType", out int cloudAvatar))
            ud.SetAvatarType((AvatarType)cloudAvatar);

        if (snapshot.TryGetValue("selectedOracle", out string selectedOracle))
            ApplySelectedOracle(selectedOracle);

        // 数据覆盖完毕后，通知本地数据管理器写盘（保存到 PlayerPrefs 或本地 JSON）
        ud.SaveData();

        Debug.Log("[FirestoreManager] 云端数据已应用到本地");
    }

    /// <summary>
    /// 检查 Firestore 是否就绪（防呆保护）
    /// </summary>
    private bool CheckReady(Action<bool> onComplete = null)
    {
        if (!_isInitialized || _db == null)
        {
            Debug.LogWarning("[FirestoreManager] Firestore 尚未初始化，操作被拦截");
            onComplete?.Invoke(false);
            return false;
        }
        return true;
    }

    private static string GetCurrentOracleId()
    {
        if (RoleManager.Instance == null) return "tarot";
        return RoleManager.Instance.characterType switch
        {
            CharacterType.Astrologer => "astrology",
            CharacterType.Meditator => "sage",
            _ => "tarot",
        };
    }

    private static string GetLocalTimezoneId()
    {
        try
        {
            return TimeZoneInfo.Local.Id;
        }
        catch
        {
            return "";
        }
    }

    private static void ApplySelectedOracle(string oracleId)
    {
        if (RoleManager.Instance == null || string.IsNullOrEmpty(oracleId)) return;

        int roleId = oracleId switch
        {
            "astrology" => (int)CharacterType.Astrologer,
            "sage" => (int)CharacterType.Meditator,
            _ => (int)CharacterType.TarotReader,
        };

        RoleManager.Instance.ChangeRole(roleId);
    }

    #endregion
}
