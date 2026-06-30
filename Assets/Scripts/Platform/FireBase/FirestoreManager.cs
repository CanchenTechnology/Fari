using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XFGameFrameWork;
using GamerFrameWork.OracleRuntime;

/// <summary>
/// Firestore 数据库管理器
/// 负责用户数据的云端读写（文档型数据库）
///
/// 数据结构：
///   users/{firebaseUid}        ← 用户资料文档
///     - displayName, email, photoUrl, avatarStoragePath, birthday, birthTime, city
///     - avatarType, loginType, isEmailVerified, selectedOracle, timezone
///     - createdAt, lastSignInAt
///
/// 使用前确保：
///   1. Firebase Unity SDK 已导入 Firestore 模块
///   2. FirebaseAuthManager 已初始化（Firestore 依赖 FirebaseApp）
/// </summary>
public class FirestoreManager : MonoSingleton<FirestoreManager>
{
    public class UserSearchResult
    {
        public string uid;
        public string displayName;
        public string email;
        public string photoUrl;
        public string birthday;
        public string birthTime;
        public string city;
        public bool isSelf;

        public string Handle => string.IsNullOrEmpty(email) ? $"@{uid}" : $"@{email.Split('@')[0]}";
        public string Info => string.IsNullOrEmpty(email) ? "Firebase 注册用户" : email;
    }

    #region 状态

    private const string PUBLIC_APP_CONFIG_CACHE_KEY = "PublicAppConfigCache_v1";
    private const string LEGACY_IAP_MONTHLY_PRODUCT_ID = "moonly.pro.monthly";
    private const string LEGACY_IAP_YEARLY_PRODUCT_ID = "moonly.pro.yearly";
    private const string PENDING_REAL_FRIEND_DELETE_KEY_PREFIX = "PendingRealFriendDeletes_";
    private const string PENDING_REAL_FRIEND_BLOCK_KEY_PREFIX = "PendingRealFriendBlocks_";
    private const string PENDING_VIRTUAL_FRIEND_SAVE_KEY_PREFIX = "PendingVirtualFriendSaves_";
    private const string PENDING_VIRTUAL_FRIEND_DELETE_KEY_PREFIX = "PendingVirtualFriendDeletes_";
    private const int FIRESTORE_DELETE_BATCH_LIMIT = 450;
    private const float USER_SEARCH_STORE_READY_TIMEOUT_SECONDS = 5f;
    private const float USER_SEARCH_STORE_READY_POLL_SECONDS = 0.2f;
    private const long ONLINE_PRESENCE_TIMEOUT_MS = 90L * 1000L;

    private FirebaseFirestore _db;
    private bool _isInitialized = false;
    private bool _hasSubscribedFirebaseInit = false;
    private bool _initRetryScheduled = false;

    /// <summary>Firestore 是否已初始化</summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>最近一次用户搜索失败原因，给 UI 做更明确的提示。</summary>
    public string LastUserSearchError { get; private set; } = string.Empty;

    [Serializable]
    private class PendingUidList
    {
        public List<string> uids = new List<string>();
    }

    #endregion

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();
        InitFirestore();
    }

    private void InitFirestore()
    {
        if (_isInitialized && _db != null) return;

        // 核心逻辑：Firestore 的运行依赖于 FirebaseApp 的成功初始化。
        // 所以这里先检查身份验证管理器（FirebaseAuthManager）是否已经准备好了。
        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        if (authManager == null)
        {
            ScheduleInitRetry();
            return;
        }

        if (authManager.IsFirebaseInitialized)
        {
            // 如果已经好了，直接初始化 Firestore
            OnFirebaseReady();
        }
        else if (!_hasSubscribedFirebaseInit)
        {
            // 如果还没好，就订阅初始化完成的事件，等它好了再回调 OnFirebaseReady
            _hasSubscribedFirebaseInit = true;
            authManager.OnFirebaseInitialized += OnFirebaseReady;
        }
    }

    private void ScheduleInitRetry()
    {
        if (_initRetryScheduled) return;

        _initRetryScheduled = true;
        Invoke(nameof(RetryInitFirestore), 0.5f);
    }

    private void RetryInitFirestore()
    {
        _initRetryScheduled = false;
        InitFirestore();
    }

    private void OnFirebaseReady()
    {
        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        if (authManager != null && _hasSubscribedFirebaseInit)
            authManager.OnFirebaseInitialized -= OnFirebaseReady;
        _hasSubscribedFirebaseInit = false;

        try
        {
            // 获取当前默认的 Firestore 数据库实例
            _db = FirebaseFirestore.DefaultInstance;
            _isInitialized = true;
            Debug.Log("[FirestoreManager] Firestore 初始化完成");
            SyncPendingRealFriendDeletes();
            SyncPendingRealFriendBlocks();
            SyncPendingVirtualFriendSaves();
            SyncPendingVirtualFriendDeletes();
        }
        catch (Exception e)
        {
            Debug.LogError($"[FirestoreManager] Firestore 初始化失败: {e.Message}");
            ScheduleInitRetry();
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
        string facebookProviderId = GetFacebookProviderIdForCurrentUser(ud);
        // 定位到当前用户的专属文档路径：集合 "users" -> 文档 "当前用户的 UID"
        DocumentReference docRef = _db.Collection("users").Document(ud.FirebaseUid);

        // 将本地内存中的数据打包成字典（Firestore 接收键值对形式的数据）
        var data = new Dictionary<string, object>
        {
            { "displayName",     ud.UserName },
            { "displayNameLower", NormalizeSearchText(ud.UserName) },
            { "searchKeywords", BuildSearchKeywords(ud.UserName, ud.Email) },
            { "email",           ud.Email },
            { "emailLower",       NormalizeSearchText(ud.Email) },
            { "photoUrl",        ud.PhotoUrl },
            { "avatarStoragePath", ud.AvatarStoragePath },
            { "facebookProviderId", facebookProviderId },
            { "birthday",        ud.Birthday },
            { "birthTime",       ud.BirthTime },
            { "city",            ud.City },
            { "bio",             ud.ProfileBio },
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
            SavePublicProfile();
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
        string facebookProviderId = GetFacebookProviderIdForCurrentUser(ud);
        DocumentReference docRef = _db.Collection("users").Document(ud.FirebaseUid);

        var data = new Dictionary<string, object>
        {
            { "displayName",     ud.UserName },
            { "displayNameLower", NormalizeSearchText(ud.UserName) },
            { "searchKeywords", BuildSearchKeywords(ud.UserName, ud.Email) },
            { "email",           ud.Email },
            { "emailLower",       NormalizeSearchText(ud.Email) },
            { "photoUrl",        ud.PhotoUrl },
            { "avatarStoragePath", ud.AvatarStoragePath },
            { "facebookProviderId", facebookProviderId },
            { "birthday",        ud.Birthday },
            { "birthTime",       ud.BirthTime },
            { "city",            ud.City },
            { "bio",             ud.ProfileBio },
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
            SavePublicProfile();
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
    /// 按用户名前缀搜索已注册用户。
    /// 注意：Firestore 不支持任意 contains 模糊搜索。
    /// 输入“土豆”会匹配所有 displayNameLower 以“土豆”开头的用户。
    /// </summary>
    public void SearchUsersByName(string keyword, Action<List<UserSearchResult>> onComplete, int limit = 3)
    {
        LastUserSearchError = string.Empty;

        if (!IsUserSearchStoreReady())
        {
            InitFirestore();
            StartCoroutine(WaitForUserSearchStoreReadyThenSearch(keyword, onComplete, limit));
            return;
        }

        SearchUsersByNameReady(keyword, onComplete, limit);
    }

    private IEnumerator WaitForUserSearchStoreReadyThenSearch(string keyword, Action<List<UserSearchResult>> onComplete, int limit)
    {
        float deadline = Time.realtimeSinceStartup + USER_SEARCH_STORE_READY_TIMEOUT_SECONDS;

        while (Time.realtimeSinceStartup < deadline)
        {
            if (IsUserSearchStoreReady())
            {
                SearchUsersByNameReady(keyword, onComplete, limit);
                yield break;
            }

            if (!_isInitialized || _db == null)
                InitFirestore();

            yield return new WaitForSecondsRealtime(USER_SEARCH_STORE_READY_POLL_SECONDS);
        }

#if UNITY_EDITOR
        string normalizedKeyword = NormalizeSearchText(keyword);
        if (!string.IsNullOrEmpty(normalizedKeyword))
        {
            int resultLimit = Math.Max(1, limit);
            int fetchLimit = GetUserSearchFetchLimit(resultLimit);
            Debug.Log("[FirestoreManager] Editor 搜索未等到 Firestore SDK 就绪，改用公开资料 REST 查询");
            SearchUsersByNameViaRestInEditor(keyword, normalizedKeyword, GetCurrentUserSearchUid(), resultLimit, fetchLimit, onComplete);
            yield break;
        }
#endif

        LastUserSearchError = BuildUserSearchStoreNotReadyMessage();
        Debug.LogWarning($"[FirestoreManager] {LastUserSearchError}");
        onComplete?.Invoke(new List<UserSearchResult>());
    }

    private void SearchUsersByNameReady(string keyword, Action<List<UserSearchResult>> onComplete, int limit)
    {
        if (!CheckReady(_ =>
        {
            LastUserSearchError = "Firestore 尚未初始化";
            onComplete?.Invoke(new List<UserSearchResult>());
        })) return;

        string normalizedKeyword = NormalizeSearchText(keyword);
        if (string.IsNullOrEmpty(normalizedKeyword))
        {
            onComplete?.Invoke(new List<UserSearchResult>());
            return;
        }

        string currentUid = GetCurrentUserSearchUid();
        int resultLimit = Math.Max(1, limit);
        int fetchLimit = GetUserSearchFetchLimit(resultLimit);
        CollectionReference profilesRef = _db.Collection("public_profiles");
        profilesRef
            .OrderBy("displayNameLower")
            .StartAt(normalizedKeyword)
            .EndAt(normalizedKeyword + "\uf8ff")
            .Limit(fetchLimit)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                List<UserSearchResult> results = new List<UserSearchResult>();
                if (task.IsFaulted || task.IsCanceled)
                {
                    LastUserSearchError = $"前缀搜索用户失败: {GetTaskError(task.Exception)}";
                    Debug.LogError($"[FirestoreManager] {LastUserSearchError}");
#if UNITY_EDITOR
                    SearchUsersByNameViaRestInEditor(keyword, normalizedKeyword, currentUid, resultLimit, fetchLimit, onComplete);
#else
                    onComplete?.Invoke(results);
#endif
                    return;
                }

                AddUserSearchResults(results, task.Result.Documents, currentUid);
                Debug.Log($"[FirestoreManager] SDK 前缀搜索 keyword={normalizedKeyword}, count={results.Count}, currentUid={currentUid}");
                if (CountNonSelfResults(results, currentUid) < resultLimit)
                {
                    SearchUsersByKeywordArray(normalizedKeyword, currentUid, resultLimit, fetchLimit, results, onComplete);
                    return;
                }

                results = LimitUserSearchResults(results, currentUid, resultLimit);
#if UNITY_EDITOR
                if (results.Count == 0)
                {
                    SearchUsersByNameViaRestInEditor(keyword, normalizedKeyword, currentUid, resultLimit, fetchLimit, onComplete);
                    return;
                }
#endif
                onComplete?.Invoke(results);
            });
    }

    /// <summary>
    /// 加载好友推荐：从公开资料索引中取一批候选，排除自己、已有好友和已屏蔽用户。
    /// </summary>
    public void LoadRecommendedUsers(Action<List<UserSearchResult>> onComplete, int limit = 3)
    {
        if (!IsUserSearchStoreReady())
        {
            InitFirestore();
            StartCoroutine(WaitForRecommendationStoreReadyThenLoad(onComplete, limit));
            return;
        }

        LoadRecommendedUsersReady(onComplete, limit);
    }

    private IEnumerator WaitForRecommendationStoreReadyThenLoad(Action<List<UserSearchResult>> onComplete, int limit)
    {
        float deadline = Time.realtimeSinceStartup + USER_SEARCH_STORE_READY_TIMEOUT_SECONDS;

        while (Time.realtimeSinceStartup < deadline)
        {
            if (IsUserSearchStoreReady())
            {
                LoadRecommendedUsersReady(onComplete, limit);
                yield break;
            }

            if (!_isInitialized || _db == null)
                InitFirestore();

            yield return new WaitForSecondsRealtime(USER_SEARCH_STORE_READY_POLL_SECONDS);
        }

#if UNITY_EDITOR
        LoadRecommendedUsersViaRestInEditor(onComplete, limit);
#else
        onComplete?.Invoke(new List<UserSearchResult>());
#endif
    }

    private void LoadRecommendedUsersReady(Action<List<UserSearchResult>> onComplete, int limit)
    {
        if (!CheckReady(_ => onComplete?.Invoke(new List<UserSearchResult>()))) return;

        string currentUid = GetCurrentUserSearchUid();
        int resultLimit = Math.Max(1, limit);
        int fetchLimit = GetUserRecommendationFetchLimit(resultLimit);

        _db.Collection("public_profiles")
            .OrderBy("displayNameLower")
            .Limit(fetchLimit)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                List<UserSearchResult> results = new List<UserSearchResult>();
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[FirestoreManager] 加载好友推荐失败: {GetTaskError(task.Exception)}");
#if UNITY_EDITOR
                    LoadRecommendedUsersViaRestInEditor(onComplete, limit);
#else
                    onComplete?.Invoke(results);
#endif
                    return;
                }

                AddUserSearchResults(results, task.Result.Documents, currentUid);
                results = BuildRecommendedUsers(results, currentUid, resultLimit);
                Debug.Log($"[FirestoreManager] 好友推荐加载完成: {results.Count}");
                onComplete?.Invoke(results);
            });
    }

    private bool IsUserSearchStoreReady()
    {
        return _isInitialized && _db != null;
    }

    private string BuildUserSearchStoreNotReadyMessage()
    {
        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        if (authManager == null)
            return "Firebase 认证服务未初始化";

        if (!authManager.IsFirebaseInitialized)
            return "Firebase 尚未初始化完成，请稍后再试";

        return "Firestore 尚未初始化";
    }

    private static string GetCurrentUserSearchUid()
    {
        string uid = UserDataManager.Instance != null ? UserDataManager.Instance.FirebaseUid : string.Empty;
        if (!string.IsNullOrEmpty(uid))
            return uid;

        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        return authManager != null && authManager.CurrentUser != null
            ? authManager.CurrentUser.UserId
            : string.Empty;
    }

    private void SearchUsersByKeywordArray(
        string normalizedKeyword,
        string currentUid,
        int resultLimit,
        int fetchLimit,
        List<UserSearchResult> seedResults,
        Action<List<UserSearchResult>> onComplete)
    {
        _db.Collection("public_profiles")
            .WhereArrayContains("searchKeywords", normalizedKeyword)
            .Limit(fetchLimit)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                List<UserSearchResult> results = seedResults ?? new List<UserSearchResult>();
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[FirestoreManager] 关键词搜索用户失败: {GetTaskError(task.Exception)}");
                    SearchUsersByLocalProfileScan(normalizedKeyword, currentUid, resultLimit, results, onComplete);
                    return;
                }

                AddUserSearchResults(results, task.Result.Documents, currentUid);
                if (CountNonSelfResults(results, currentUid) < resultLimit)
                {
                    SearchUsersByLocalProfileScan(normalizedKeyword, currentUid, resultLimit, results, onComplete);
                    return;
                }

                onComplete?.Invoke(LimitUserSearchResults(results, currentUid, resultLimit));
            });
    }

    private void SearchUsersByLocalProfileScan(
        string normalizedKeyword,
        string currentUid,
        int resultLimit,
        List<UserSearchResult> seedResults,
        Action<List<UserSearchResult>> onComplete)
    {
        int scanLimit = GetUserSearchScanLimit(resultLimit);
        _db.Collection("public_profiles")
            .OrderBy("displayNameLower")
            .Limit(scanLimit)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                List<UserSearchResult> results = seedResults ?? new List<UserSearchResult>();
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[FirestoreManager] 公开资料扫描搜索失败: {GetTaskError(task.Exception)}");
                    onComplete?.Invoke(LimitUserSearchResults(results, currentUid, resultLimit));
                    return;
                }

                AddMatchingUserSearchResults(results, task.Result.Documents, currentUid, normalizedKeyword);
                onComplete?.Invoke(LimitUserSearchResults(results, currentUid, resultLimit));
            });
    }

    /// <summary>
    /// 发送好友请求并在当前用户的 friends 子集合中留下待确认记录，但不展示为已有好友。
    /// </summary>
    public void SendFriendRequest(UserSearchResult targetUser, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        if (targetUser == null || string.IsNullOrEmpty(targetUser.uid))
        {
            onComplete?.Invoke(false);
            return;
        }

        var ud = UserDataManager.Instance;
        string currentUid = ud.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid) || currentUid == targetUser.uid)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (FriendDataManager.Instance != null && FriendDataManager.Instance.IsUserBlocked(targetUser.uid))
        {
            Debug.LogWarning($"[FirestoreManager] 已屏蔽用户，不能发送好友请求: {targetUser.uid}");
            onComplete?.Invoke(false);
            return;
        }

        DocumentReference outgoingRef = _db.Collection("users")
            .Document(currentUid)
            .Collection("friends")
            .Document(targetUser.uid);
        outgoingRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (!task.IsFaulted && !task.IsCanceled && task.Result.Exists)
            {
                Dictionary<string, object> data = task.Result.ToDictionary();
                string status = GetString(data, "status", string.Empty);
                if (status == "friend" || status == "pendingSent")
                {
                    Debug.Log($"[FirestoreManager] 好友请求跳过，已有状态: {status}, uid={targetUser.uid}");
                    onComplete?.Invoke(true);
                    return;
                }
            }

            CommitFriendRequest(targetUser, ud, currentUid, outgoingRef, onComplete);
        });
    }

    private void CommitFriendRequest(
        UserSearchResult targetUser,
        UserDataManager ud,
        string currentUid,
        DocumentReference outgoingRef,
        Action<bool> onComplete)
    {
        Dictionary<string, object> outgoingData = new Dictionary<string, object>
        {
            { "uid", targetUser.uid },
            { "displayName", targetUser.displayName },
            { "email", targetUser.email },
            { "photoUrl", targetUser.photoUrl },
            { "status", "pendingSent" },
            { "source", "firebaseSearch" },
            { "createdAt", FieldValue.ServerTimestamp },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        Dictionary<string, object> incomingData = new Dictionary<string, object>
        {
            { "uid", currentUid },
            { "displayName", ud.UserName },
            { "email", ud.Email },
            { "photoUrl", ud.PhotoUrl },
            { "status", "pendingReceived" },
            { "source", "firebaseSearch" },
            { "createdAt", FieldValue.ServerTimestamp },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        WriteBatch batch = _db.StartBatch();
        DocumentReference incomingRef = _db.Collection("users")
            .Document(targetUser.uid)
            .Collection("friend_requests")
            .Document(currentUid);

        batch.Set(outgoingRef, outgoingData, SetOptions.MergeAll);
        batch.Set(incomingRef, incomingData, SetOptions.MergeAll);
        batch.CommitAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 发送好友请求失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            FriendDataManager.Instance.RemoveRealFriendByFirebaseUid(targetUser.uid);

            Debug.Log($"[FirestoreManager] 好友请求已发送: {targetUser.uid}");
            onComplete?.Invoke(true);
        });
    }

    /// <summary>
    /// 拉取当前用户已经发出的待确认好友请求，用于搜索结果显示“取消”状态。
    /// </summary>
    public void LoadPendingSentFriendUids(Action<HashSet<string>> onComplete)
    {
        HashSet<string> sentUids = new HashSet<string>();
        if (!_isInitialized || _db == null)
        {
            Debug.LogWarning("[FirestoreManager] Firestore 尚未初始化，无法拉取已发送好友请求");
            onComplete?.Invoke(sentUids);
            return;
        }

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(sentUids);
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("friends")
            .WhereEqualTo("status", "pendingSent")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 拉取已发送好友请求失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(sentUids);
                    return;
                }

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    if (doc.Exists && !string.IsNullOrEmpty(doc.Id))
                        sentUids.Add(doc.Id);
                }

                onComplete?.Invoke(sentUids);
            });
    }

    /// <summary>
    /// 取消已经发出的好友请求：删除自己的 pendingSent 和对方收到的 friend_requests。
    /// </summary>
    public void CancelSentFriendRequest(string targetUid, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid) || string.IsNullOrEmpty(targetUid) || currentUid == targetUid)
        {
            onComplete?.Invoke(false);
            return;
        }

        WriteBatch batch = _db.StartBatch();
        DocumentReference outgoingRef = _db.Collection("users")
            .Document(currentUid)
            .Collection("friends")
            .Document(targetUid);
        DocumentReference incomingRef = _db.Collection("users")
            .Document(targetUid)
            .Collection("friend_requests")
            .Document(currentUid);

        batch.Delete(outgoingRef);
        batch.Delete(incomingRef);
        batch.CommitAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 取消好友请求失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            FriendDataManager.Instance.RemoveRealFriendByFirebaseUid(targetUid);
            Debug.Log($"[FirestoreManager] 已取消好友请求: {targetUid}");
            onComplete?.Invoke(true);
        });
    }

    /// <summary>
    /// 删除真实好友：双方 friends 记录都删除，本地真实好友缓存同步移除。
    /// </summary>
    public void RemoveRealFriend(FriendDataManager.FriendData friend, Action<bool> onComplete = null)
    {
        if (friend == null || friend.isVirtual)
        {
            onComplete?.Invoke(false);
            return;
        }

        string friendUid = friend.firebaseUid;
        if (string.IsNullOrEmpty(friendUid))
        {
            bool removed = FriendDataManager.Instance != null && FriendDataManager.Instance.RemoveRealFriend(friend.id);
            Debug.LogWarning($"[FirestoreManager] 真实好友缺少 Firebase UID，已尝试仅本地删除: id={friend.id}, success={removed}");
            onComplete?.Invoke(removed);
            return;
        }

        string currentUid = GetCurrentFirebaseUid();
        if (string.IsNullOrEmpty(currentUid))
        {
            QueuePendingRealFriendDelete(friendUid);
            FriendDataManager.Instance?.RemoveRealFriendByFirebaseUid(friendUid);
            Debug.Log($"[FirestoreManager] 当前用户 UID 未就绪，已本地删除真实好友并等待同步: {friendUid}");
            onComplete?.Invoke(true);
            return;
        }

        if (currentUid == friendUid)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (!_isInitialized || _db == null)
        {
            QueuePendingRealFriendDelete(friendUid);
            FriendDataManager.Instance?.RemoveRealFriendByFirebaseUid(friendUid);
            Debug.Log($"[FirestoreManager] Firestore 未初始化，已本地删除真实好友并等待同步: {friendUid}");
            onComplete?.Invoke(true);
            return;
        }

        CommitRemoveRealFriend(currentUid, friendUid, success =>
        {
            if (!success)
            {
                QueuePendingRealFriendDelete(friendUid);
                FriendDataManager.Instance?.RemoveRealFriendByFirebaseUid(friendUid);
                Debug.LogWarning($"[FirestoreManager] 真实好友云端删除失败，已加入待同步删除队列: {friendUid}");
                onComplete?.Invoke(true);
                return;
            }

            if (success)
            {
                FriendDataManager.Instance?.RemoveRealFriendByFirebaseUid(friendUid);
                RemovePendingRealFriendDelete(friendUid);
                Debug.Log($"[FirestoreManager] 已删除真实好友: {friendUid}");
            }

            onComplete?.Invoke(success);
        });
    }

    private void CommitRemoveRealFriend(string currentUid, string friendUid, Action<bool> onComplete)
    {
        DocumentReference myFriendRef = _db.Collection("users")
            .Document(currentUid)
            .Collection("friends")
            .Document(friendUid);
        DocumentReference theirFriendRef = _db.Collection("users")
            .Document(friendUid)
            .Collection("friends")
            .Document(currentUid);
        DocumentReference archiveRef = _db.Collection("deleted_friend_relationships")
            .Document(BuildFriendRelationshipPairId(currentUid, friendUid));

        Task<DocumentSnapshot> myFriendTask = myFriendRef.GetSnapshotAsync();
        Task<DocumentSnapshot> theirFriendTask = theirFriendRef.GetSnapshotAsync();
        Task.WhenAll(myFriendTask, theirFriendTask).ContinueWithOnMainThread(snapshotTask =>
        {
            if (snapshotTask.IsFaulted || snapshotTask.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 删除真实好友前读取归档失败: {snapshotTask.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            Dictionary<string, object> mySnapshot = myFriendTask.Result.Exists
                ? myFriendTask.Result.ToDictionary()
                : new Dictionary<string, object>();
            Dictionary<string, object> theirSnapshot = theirFriendTask.Result.Exists
                ? theirFriendTask.Result.ToDictionary()
                : new Dictionary<string, object>();

            Dictionary<string, object> snapshots = new Dictionary<string, object>
            {
                { currentUid, mySnapshot },
                { friendUid, theirSnapshot }
            };
            Dictionary<string, object> archiveData = new Dictionary<string, object>
            {
                { "pairId", BuildFriendRelationshipPairId(currentUid, friendUid) },
                { "uidA", string.CompareOrdinal(currentUid, friendUid) <= 0 ? currentUid : friendUid },
                { "uidB", string.CompareOrdinal(currentUid, friendUid) <= 0 ? friendUid : currentUid },
                { "memberUids", new List<string> { currentUid, friendUid } },
                { "deletedByUid", currentUid },
                { "deletedBySide", "client" },
                { "status", "deleted" },
                { "snapshots", snapshots },
                { "snapshotA", string.CompareOrdinal(currentUid, friendUid) <= 0 ? mySnapshot : theirSnapshot },
                { "snapshotB", string.CompareOrdinal(currentUid, friendUid) <= 0 ? theirSnapshot : mySnapshot },
                { "deletedAt", FieldValue.ServerTimestamp },
                { "updatedAt", FieldValue.ServerTimestamp },
                { "expiresAt", Timestamp.FromDateTime(DateTime.UtcNow.AddDays(90)) }
            };

            WriteBatch batch = _db.StartBatch();
            batch.Set(archiveRef, archiveData, SetOptions.MergeAll);
            batch.Delete(myFriendRef);
            batch.Delete(theirFriendRef);
            batch.CommitAsync().ContinueWithOnMainThread(commitTask =>
            {
                if (commitTask.IsFaulted || commitTask.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 删除真实好友失败: {commitTask.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                onComplete?.Invoke(true);
            });
        });
    }

    private static string BuildFriendRelationshipPairId(string uidA, string uidB)
    {
        string a = string.IsNullOrWhiteSpace(uidA) ? "unknown_a" : uidA.Trim();
        string b = string.IsNullOrWhiteSpace(uidB) ? "unknown_b" : uidB.Trim();
        return string.CompareOrdinal(a, b) <= 0 ? $"{a}__{b}" : $"{b}__{a}";
    }

    public static void QueueRealFriendDeleteLocal(string friendUid)
    {
        QueuePendingRealFriendDelete(friendUid);
    }

    public static void QueueRealFriendBlockLocal(string friendUid)
    {
        QueuePendingRealFriendBlock(friendUid);
    }

    public static bool IsRealFriendDeleteQueuedLocal(string friendUid)
    {
        return IsRealFriendPendingDelete(friendUid);
    }

    public static bool IsRealFriendBlockQueuedLocal(string friendUid)
    {
        return IsRealFriendPendingBlock(friendUid);
    }

    public static void QueueVirtualFriendSaveLocal(FriendDataManager.FriendData virtualFriend)
    {
        if (virtualFriend == null || !virtualFriend.isVirtual) return;
        EnsureVirtualFriendId(virtualFriend);
        QueuePendingVirtualFriendSave(virtualFriend.virtualFriendId);
    }

    public static void QueueVirtualFriendDeleteLocal(FriendDataManager.FriendData virtualFriend)
    {
        if (virtualFriend == null || !virtualFriend.isVirtual) return;
        QueueVirtualFriendDeleteLocal(virtualFriend.virtualFriendId);
    }

    public static void QueueVirtualFriendDeleteLocal(string virtualFriendId)
    {
        if (string.IsNullOrWhiteSpace(virtualFriendId)) return;
        RemovePendingVirtualFriendSave(virtualFriendId);
        QueuePendingVirtualFriendDelete(virtualFriendId);
    }

    public static bool IsVirtualFriendDeleteQueuedLocal(string virtualFriendId)
    {
        return IsVirtualFriendPendingDelete(virtualFriendId);
    }

    private static void QueuePendingRealFriendDelete(string friendUid)
    {
        if (string.IsNullOrWhiteSpace(friendUid)) return;

        string key = GetPendingRealFriendDeleteKey();
        List<string> pending = LoadPendingRealFriendDeletes(key);
        if (!pending.Exists(uid => uid == friendUid))
            pending.Add(friendUid);

        SavePendingRealFriendDeletes(key, pending);
    }

    private static void QueuePendingRealFriendBlock(string friendUid)
    {
        if (string.IsNullOrWhiteSpace(friendUid)) return;

        string key = GetPendingRealFriendBlockKey();
        List<string> pending = LoadPendingRealFriendBlocks(key);
        if (!pending.Exists(uid => uid == friendUid))
            pending.Add(friendUid);

        SavePendingRealFriendBlocks(key, pending);
    }

    private static void QueuePendingVirtualFriendSave(string virtualFriendId)
    {
        if (string.IsNullOrWhiteSpace(virtualFriendId)) return;

        string key = GetPendingVirtualFriendSaveKey();
        List<string> pending = LoadPendingVirtualFriendSaves(key);
        if (!pending.Exists(id => id == virtualFriendId))
            pending.Add(virtualFriendId);

        SavePendingVirtualFriendSaves(key, pending);
    }

    private static void QueuePendingVirtualFriendDelete(string virtualFriendId)
    {
        if (string.IsNullOrWhiteSpace(virtualFriendId)) return;

        string key = GetPendingVirtualFriendDeleteKey();
        List<string> pending = LoadPendingVirtualFriendDeletes(key);
        if (!pending.Exists(id => id == virtualFriendId))
            pending.Add(virtualFriendId);

        SavePendingVirtualFriendDeletes(key, pending);
    }

    private static void RemovePendingRealFriendDelete(string friendUid)
    {
        if (string.IsNullOrWhiteSpace(friendUid)) return;

        foreach (string key in GetPendingRealFriendDeleteKeysForSync())
        {
            List<string> pending = LoadPendingRealFriendDeletes(key);
            if (pending.RemoveAll(uid => uid == friendUid) > 0)
                SavePendingRealFriendDeletes(key, pending);
        }
    }

    private static void RemovePendingRealFriendBlock(string friendUid)
    {
        if (string.IsNullOrWhiteSpace(friendUid)) return;

        foreach (string key in GetPendingRealFriendBlockKeysForSync())
        {
            List<string> pending = LoadPendingRealFriendBlocks(key);
            if (pending.RemoveAll(uid => uid == friendUid) > 0)
                SavePendingRealFriendBlocks(key, pending);
        }
    }

    private static void RemovePendingVirtualFriendSave(string virtualFriendId)
    {
        if (string.IsNullOrWhiteSpace(virtualFriendId)) return;

        foreach (string key in GetPendingVirtualFriendSaveKeysForSync())
        {
            List<string> pending = LoadPendingVirtualFriendSaves(key);
            if (pending.RemoveAll(id => id == virtualFriendId) > 0)
                SavePendingVirtualFriendSaves(key, pending);
        }
    }

    private static void RemovePendingVirtualFriendDelete(string virtualFriendId)
    {
        if (string.IsNullOrWhiteSpace(virtualFriendId)) return;

        foreach (string key in GetPendingVirtualFriendDeleteKeysForSync())
        {
            List<string> pending = LoadPendingVirtualFriendDeletes(key);
            if (pending.RemoveAll(id => id == virtualFriendId) > 0)
                SavePendingVirtualFriendDeletes(key, pending);
        }
    }

    private void SyncPendingRealFriendDeletes()
    {
        if (!_isInitialized || _db == null) return;

        string currentUid = GetCurrentFirebaseUid();
        if (string.IsNullOrWhiteSpace(currentUid)) return;

        foreach (string key in GetPendingRealFriendDeleteKeysForSync())
        {
            List<string> pending = LoadPendingRealFriendDeletes(key);
            if (pending.Count == 0) continue;

            foreach (string friendUid in new List<string>(pending))
            {
                if (string.IsNullOrWhiteSpace(friendUid) || friendUid == currentUid)
                {
                    RemovePendingRealFriendDelete(friendUid);
                    continue;
                }

                CommitRemoveRealFriend(currentUid, friendUid, success =>
                {
                    if (!success) return;
                    RemovePendingRealFriendDelete(friendUid);
                    Debug.Log($"[FirestoreManager] 已同步待删除真实好友: {friendUid}");
                });
            }
        }
    }

    private void SyncPendingRealFriendBlocks()
    {
        if (!_isInitialized || _db == null) return;

        string currentUid = GetCurrentFirebaseUid();
        if (string.IsNullOrWhiteSpace(currentUid)) return;

        foreach (string key in GetPendingRealFriendBlockKeysForSync())
        {
            List<string> pending = LoadPendingRealFriendBlocks(key);
            if (pending.Count == 0) continue;

            foreach (string friendUid in new List<string>(pending))
            {
                if (string.IsNullOrWhiteSpace(friendUid) || friendUid == currentUid)
                {
                    RemovePendingRealFriendBlock(friendUid);
                    continue;
                }

                CommitBlockRealFriend(currentUid, friendUid, null, success =>
                {
                    if (!success) return;
                    FriendDataManager.Instance?.AddBlockedUser(friendUid);
                    RemovePendingRealFriendBlock(friendUid);
                    Debug.Log($"[FirestoreManager] 已同步待屏蔽真实好友: {friendUid}");
                });
            }
        }
    }

    private void SyncPendingVirtualFriendSaves()
    {
        if (!_isInitialized || _db == null) return;

        string currentUid = GetCurrentFirebaseUid();
        if (string.IsNullOrWhiteSpace(currentUid)) return;

        foreach (string key in GetPendingVirtualFriendSaveKeysForSync())
        {
            List<string> pending = LoadPendingVirtualFriendSaves(key);
            if (pending.Count == 0) continue;

            foreach (string virtualFriendId in new List<string>(pending))
            {
                if (string.IsNullOrWhiteSpace(virtualFriendId) || IsVirtualFriendPendingDelete(virtualFriendId))
                {
                    RemovePendingVirtualFriendSave(virtualFriendId);
                    continue;
                }

                FriendDataManager.FriendData friend = FriendDataManager.Instance?.FindVirtualFriendById(virtualFriendId);
                if (friend == null)
                {
                    RemovePendingVirtualFriendSave(virtualFriendId);
                    continue;
                }

                CommitSaveVirtualFriendWithAvatar(currentUid, friend, success =>
                {
                    if (!success) return;
                    RemovePendingVirtualFriendSave(friend.virtualFriendId);
                    Debug.Log($"[FirestoreManager] 已同步待保存虚拟好友: {friend.virtualFriendId}");
                });
            }
        }
    }

    private void SyncPendingVirtualFriendDeletes()
    {
        if (!_isInitialized || _db == null) return;

        string currentUid = GetCurrentFirebaseUid();
        if (string.IsNullOrWhiteSpace(currentUid)) return;

        foreach (string key in GetPendingVirtualFriendDeleteKeysForSync())
        {
            List<string> pending = LoadPendingVirtualFriendDeletes(key);
            if (pending.Count == 0) continue;

            foreach (string virtualFriendId in new List<string>(pending))
            {
                if (string.IsNullOrWhiteSpace(virtualFriendId))
                {
                    RemovePendingVirtualFriendDelete(virtualFriendId);
                    continue;
                }

                CommitDeleteVirtualFriend(currentUid, virtualFriendId, success =>
                {
                    if (!success) return;
                    RemovePendingVirtualFriendDelete(virtualFriendId);
                    RemovePendingVirtualFriendSave(virtualFriendId);
                    FriendDataManager.Instance?.RemoveVirtualFriendById(virtualFriendId);
                    Debug.Log($"[FirestoreManager] 已同步待删除虚拟好友: {virtualFriendId}");
                });
            }
        }
    }

    private static string GetPendingRealFriendDeleteKey()
    {
        string currentUid = GetCurrentFirebaseUid();
        if (!string.IsNullOrWhiteSpace(currentUid))
            return BuildPendingRealFriendDeleteKey(currentUid);

        string localUserId = UserDataManager.Instance?.UserId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(localUserId))
            return BuildPendingRealFriendDeleteKey(localUserId);

        return BuildPendingRealFriendDeleteKey("local");
    }

    private static string GetPendingRealFriendBlockKey()
    {
        string currentUid = GetCurrentFirebaseUid();
        if (!string.IsNullOrWhiteSpace(currentUid))
            return BuildPendingRealFriendBlockKey(currentUid);

        string localUserId = UserDataManager.Instance?.UserId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(localUserId))
            return BuildPendingRealFriendBlockKey(localUserId);

        return BuildPendingRealFriendBlockKey("local");
    }

    private static string GetPendingVirtualFriendSaveKey()
    {
        string currentUid = GetCurrentFirebaseUid();
        if (!string.IsNullOrWhiteSpace(currentUid))
            return BuildPendingVirtualFriendSaveKey(currentUid);

        string localUserId = UserDataManager.Instance?.UserId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(localUserId))
            return BuildPendingVirtualFriendSaveKey(localUserId);

        return BuildPendingVirtualFriendSaveKey("local");
    }

    private static string GetPendingVirtualFriendDeleteKey()
    {
        string currentUid = GetCurrentFirebaseUid();
        if (!string.IsNullOrWhiteSpace(currentUid))
            return BuildPendingVirtualFriendDeleteKey(currentUid);

        string localUserId = UserDataManager.Instance?.UserId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(localUserId))
            return BuildPendingVirtualFriendDeleteKey(localUserId);

        return BuildPendingVirtualFriendDeleteKey("local");
    }

    private static List<string> GetPendingRealFriendDeleteKeysForSync()
    {
        List<string> keys = new List<string>();

        void AddKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string key = BuildPendingRealFriendDeleteKey(value);
            if (!keys.Contains(key))
                keys.Add(key);
        }

        AddKey(GetCurrentFirebaseUid());
        AddKey(UserDataManager.Instance?.UserId);
        AddKey("local");
        return keys;
    }

    private static List<string> GetPendingRealFriendBlockKeysForSync()
    {
        List<string> keys = new List<string>();

        void AddKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string key = BuildPendingRealFriendBlockKey(value);
            if (!keys.Contains(key))
                keys.Add(key);
        }

        AddKey(GetCurrentFirebaseUid());
        AddKey(UserDataManager.Instance?.UserId);
        AddKey("local");
        return keys;
    }

    private static List<string> GetPendingVirtualFriendSaveKeysForSync()
    {
        List<string> keys = new List<string>();

        void AddKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string key = BuildPendingVirtualFriendSaveKey(value);
            if (!keys.Contains(key))
                keys.Add(key);
        }

        AddKey(GetCurrentFirebaseUid());
        AddKey(UserDataManager.Instance?.UserId);
        AddKey("local");
        return keys;
    }

    private static List<string> GetPendingVirtualFriendDeleteKeysForSync()
    {
        List<string> keys = new List<string>();

        void AddKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string key = BuildPendingVirtualFriendDeleteKey(value);
            if (!keys.Contains(key))
                keys.Add(key);
        }

        AddKey(GetCurrentFirebaseUid());
        AddKey(UserDataManager.Instance?.UserId);
        AddKey("local");
        return keys;
    }

    private static string GetCurrentFirebaseUid()
    {
        string uid = UserDataManager.Instance?.FirebaseUid ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(uid))
            return uid.Trim();

        uid = Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? string.Empty;
        return string.IsNullOrWhiteSpace(uid) ? string.Empty : uid.Trim();
    }

    private static string BuildPendingRealFriendDeleteKey(string userKey)
    {
        return PENDING_REAL_FRIEND_DELETE_KEY_PREFIX + (string.IsNullOrWhiteSpace(userKey) ? "local" : userKey.Trim());
    }

    private static string BuildPendingRealFriendBlockKey(string userKey)
    {
        return PENDING_REAL_FRIEND_BLOCK_KEY_PREFIX + (string.IsNullOrWhiteSpace(userKey) ? "local" : userKey.Trim());
    }

    private static string BuildPendingVirtualFriendSaveKey(string userKey)
    {
        return PENDING_VIRTUAL_FRIEND_SAVE_KEY_PREFIX + (string.IsNullOrWhiteSpace(userKey) ? "local" : userKey.Trim());
    }

    private static string BuildPendingVirtualFriendDeleteKey(string userKey)
    {
        return PENDING_VIRTUAL_FRIEND_DELETE_KEY_PREFIX + (string.IsNullOrWhiteSpace(userKey) ? "local" : userKey.Trim());
    }

    private static List<string> LoadPendingRealFriendDeletes(string key)
    {
        if (string.IsNullOrEmpty(key)) return new List<string>();

        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();

        try
        {
            PendingUidList data = JsonUtility.FromJson<PendingUidList>(json);
            return data?.uids ?? new List<string>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FirestoreManager] 待同步好友删除队列读取失败，已重置。{ex.Message}");
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return new List<string>();
        }
    }

    private static List<string> LoadPendingRealFriendBlocks(string key)
    {
        if (string.IsNullOrEmpty(key)) return new List<string>();

        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();

        try
        {
            PendingUidList data = JsonUtility.FromJson<PendingUidList>(json);
            return data?.uids ?? new List<string>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FirestoreManager] 待同步好友屏蔽队列读取失败，已重置。{ex.Message}");
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return new List<string>();
        }
    }

    private static List<string> LoadPendingVirtualFriendSaves(string key)
    {
        if (string.IsNullOrEmpty(key)) return new List<string>();

        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();

        try
        {
            PendingUidList data = JsonUtility.FromJson<PendingUidList>(json);
            return data?.uids ?? new List<string>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FirestoreManager] 待同步虚拟好友保存队列读取失败，已重置。{ex.Message}");
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return new List<string>();
        }
    }

    private static List<string> LoadPendingVirtualFriendDeletes(string key)
    {
        if (string.IsNullOrEmpty(key)) return new List<string>();

        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();

        try
        {
            PendingUidList data = JsonUtility.FromJson<PendingUidList>(json);
            return data?.uids ?? new List<string>();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FirestoreManager] 待同步虚拟好友删除队列读取失败，已重置。{ex.Message}");
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            return new List<string>();
        }
    }

    private static void SavePendingRealFriendDeletes(string key, List<string> pending)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (pending == null || pending.Count == 0)
        {
            PlayerPrefs.DeleteKey(key);
        }
        else
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(new PendingUidList { uids = pending }));
        }

        PlayerPrefs.Save();
    }

    private static void SavePendingVirtualFriendSaves(string key, List<string> pending)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (pending == null || pending.Count == 0)
        {
            PlayerPrefs.DeleteKey(key);
        }
        else
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(new PendingUidList { uids = pending }));
        }

        PlayerPrefs.Save();
    }

    private static void SavePendingVirtualFriendDeletes(string key, List<string> pending)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (pending == null || pending.Count == 0)
        {
            PlayerPrefs.DeleteKey(key);
        }
        else
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(new PendingUidList { uids = pending }));
        }

        PlayerPrefs.Save();
    }

    private static bool IsVirtualFriendPendingSave(string virtualFriendId)
    {
        if (string.IsNullOrWhiteSpace(virtualFriendId)) return false;

        foreach (string key in GetPendingVirtualFriendSaveKeysForSync())
        {
            if (LoadPendingVirtualFriendSaves(key).Exists(id => id == virtualFriendId))
                return true;
        }

        return false;
    }

    private static bool IsRealFriendPendingDelete(string friendUid)
    {
        if (string.IsNullOrWhiteSpace(friendUid)) return false;

        foreach (string key in GetPendingRealFriendDeleteKeysForSync())
        {
            if (LoadPendingRealFriendDeletes(key).Exists(uid => uid == friendUid))
                return true;
        }

        return false;
    }

    private static bool IsRealFriendPendingBlock(string friendUid)
    {
        if (string.IsNullOrWhiteSpace(friendUid)) return false;

        foreach (string key in GetPendingRealFriendBlockKeysForSync())
        {
            if (LoadPendingRealFriendBlocks(key).Exists(uid => uid == friendUid))
                return true;
        }

        return false;
    }

    private static bool IsVirtualFriendPendingDelete(string virtualFriendId)
    {
        if (string.IsNullOrWhiteSpace(virtualFriendId)) return false;

        foreach (string key in GetPendingVirtualFriendDeleteKeysForSync())
        {
            if (LoadPendingVirtualFriendDeletes(key).Exists(id => id == virtualFriendId))
                return true;
        }

        return false;
    }

    private static void EnsureVirtualFriendId(FriendDataManager.FriendData virtualFriend)
    {
        if (virtualFriend == null || !virtualFriend.isVirtual) return;
        if (!string.IsNullOrWhiteSpace(virtualFriend.virtualFriendId)) return;

        virtualFriend.virtualFriendId = Guid.NewGuid().ToString("N");
        FriendDataManager.Instance?.SaveLocalData();
    }

    private static void SavePendingRealFriendBlocks(string key, List<string> pending)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (pending == null || pending.Count == 0)
        {
            PlayerPrefs.DeleteKey(key);
        }
        else
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(new PendingUidList { uids = pending }));
        }

        PlayerPrefs.Save();
    }

    public void LoadBlockedUsers(Action<HashSet<string>> onComplete)
    {
        HashSet<string> blocked = new HashSet<string>();
        if (!_isInitialized || _db == null)
        {
            onComplete?.Invoke(blocked);
            return;
        }

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(blocked);
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("blocked_users")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 拉取屏蔽用户失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(blocked);
                    return;
                }

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    if (!doc.Exists || string.IsNullOrEmpty(doc.Id)) continue;
                    blocked.Add(doc.Id);
                    FriendDataManager.Instance.AddBlockedUser(doc.Id);
                }

                onComplete?.Invoke(blocked);
            });
    }

    public void BlockRealFriend(FriendDataManager.FriendData friend, Action<bool> onComplete = null)
    {
        if (friend == null || friend.isVirtual)
        {
            onComplete?.Invoke(false);
            return;
        }

        string friendUid = friend.firebaseUid;
        if (string.IsNullOrEmpty(friendUid))
        {
            bool removed = FriendDataManager.Instance != null && FriendDataManager.Instance.RemoveRealFriend(friend.id);
            Debug.LogWarning($"[FirestoreManager] 真实好友缺少 Firebase UID，无法写入屏蔽列表，已尝试仅本地移除: id={friend.id}, success={removed}");
            onComplete?.Invoke(removed);
            return;
        }

        string currentUid = GetCurrentFirebaseUid();
        if (string.IsNullOrEmpty(currentUid))
        {
            QueuePendingRealFriendBlock(friendUid);
            FriendDataManager.Instance?.AddBlockedUser(friendUid);
            Debug.Log($"[FirestoreManager] 当前用户 UID 未就绪，已本地屏蔽真实好友并等待同步: {friendUid}");
            onComplete?.Invoke(true);
            return;
        }

        if (currentUid == friendUid)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (!_isInitialized || _db == null)
        {
            QueuePendingRealFriendBlock(friendUid);
            FriendDataManager.Instance?.AddBlockedUser(friendUid);
            Debug.Log($"[FirestoreManager] Firestore 未初始化，已本地屏蔽真实好友并等待同步: {friendUid}");
            onComplete?.Invoke(true);
            return;
        }

        CommitBlockRealFriend(currentUid, friendUid, friend, success =>
        {
            if (!success)
            {
                QueuePendingRealFriendBlock(friendUid);
                FriendDataManager.Instance?.AddBlockedUser(friendUid);
                Debug.LogWarning($"[FirestoreManager] 真实好友云端屏蔽失败，已加入待同步屏蔽队列: {friendUid}");
                onComplete?.Invoke(true);
                return;
            }

            if (success)
            {
                FriendDataManager.Instance?.AddBlockedUser(friendUid);
                RemovePendingRealFriendBlock(friendUid);
                Debug.Log($"[FirestoreManager] 已屏蔽用户: {friendUid}");
            }

            onComplete?.Invoke(success);
        });
    }

    private void CommitBlockRealFriend(string currentUid, string friendUid, FriendDataManager.FriendData friend, Action<bool> onComplete)
    {
        Dictionary<string, object> blockedData = new Dictionary<string, object>
        {
            { "uid", friendUid },
            { "displayName", friend?.name ?? string.Empty },
            { "email", string.IsNullOrWhiteSpace(friend?.handle) ? string.Empty : friend.handle.TrimStart('@') },
            { "photoUrl", friend?.photoUrl ?? string.Empty },
            { "createdAt", FieldValue.ServerTimestamp },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        WriteBatch batch = _db.StartBatch();
        DocumentReference blockedRef = _db.Collection("users")
            .Document(currentUid)
            .Collection("blocked_users")
            .Document(friendUid);
        batch.Set(blockedRef, blockedData, SetOptions.MergeAll);
        batch.Delete(_db.Collection("users").Document(currentUid).Collection("friends").Document(friendUid));
        batch.Delete(_db.Collection("users").Document(friendUid).Collection("friends").Document(currentUid));
        batch.Delete(_db.Collection("users").Document(currentUid).Collection("friend_requests").Document(friendUid));
        batch.Delete(_db.Collection("users").Document(friendUid).Collection("friend_requests").Document(currentUid));

        batch.CommitAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 屏蔽好友失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            onComplete?.Invoke(true);
        });
    }

    public void UnblockUser(string targetUid, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid) || string.IsNullOrEmpty(targetUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("blocked_users")
            .Document(targetUid)
            .DeleteAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 解除屏蔽失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                FriendDataManager.Instance.RemoveBlockedUser(targetUid);
                Debug.Log($"[FirestoreManager] 已解除屏蔽: {targetUid}");
                onComplete?.Invoke(true);
            });
    }

    /// <summary>
    /// 从当前用户的 friends 子集合拉取已接受的真实好友，并合并到本地缓存。
    /// </summary>
    public void LoadFriends(Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("friends")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 拉取好友失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                int friendDocCount = 0;
                foreach (DocumentSnapshot _ in task.Result.Documents)
                    friendDocCount++;

                FriendDataManager.Instance.RemoveLocalDebugRealFriends();
                Debug.Log($"[FirestoreManager] 拉取好友成功: localUid={currentUid}, authUid={GetFirebaseAuthUid()}, count={friendDocCount}");

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    if (!doc.Exists) continue;

                    Dictionary<string, object> data = doc.ToDictionary();
                    string status = GetString(data, "status", "friend");
                    if (status != "friend")
                    {
                        FriendDataManager.Instance.RemoveRealFriendByFirebaseUid(doc.Id);
                        continue;
                    }

                    string displayName = GetString(data, "displayName", "未命名用户");
                    string email = GetString(data, "email", string.Empty);
                    string photoUrl = GetString(data, "photoUrl", string.Empty);
                    string handle = string.IsNullOrEmpty(email) ? $"@{doc.Id}" : $"@{email.Split('@')[0]}";
                    long lastLoginUnixMs = GetPresenceUnixMs(data);
                    bool isOnline = ResolvePresenceOnline(data, lastLoginUnixMs);

                    FriendDataManager.Instance.UpsertRealFriendFromFirebase(
                        doc.Id,
                        displayName,
                        handle,
                        GetFriendStatusText(status),
                        null,
                        "Firebase",
                        photoUrl,
                        isOnline,
                        lastLoginUnixMs);
                    LoadFriendPublicProfile(doc.Id, displayName, handle, photoUrl, lastLoginUnixMs);
                }

                onComplete?.Invoke(true);
            });
    }

    private static string GetFirebaseAuthUid()
    {
        try
        {
            return FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void LoadFriendPublicProfile(string friendUid, string fallbackName, string fallbackHandle, string fallbackPhotoUrl, long fallbackLastLoginUnixMs)
    {
        if (!_isInitialized || _db == null || string.IsNullOrWhiteSpace(friendUid))
            return;

        _db.Collection("public_profiles")
            .Document(friendUid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || task.Result == null || !task.Result.Exists)
                    return;

                Dictionary<string, object> data = task.Result.ToDictionary();
                string displayName = GetString(data, "displayName", fallbackName);
                string email = GetString(data, "email", string.Empty);
                string photoUrl = GetString(data, "photoUrl", fallbackPhotoUrl);
                string handle = string.IsNullOrEmpty(email) ? fallbackHandle : $"@{email.Split('@')[0]}";

                long lastLoginUnixMs = GetPresenceUnixMs(data, fallbackLastLoginUnixMs);
                bool isOnline = ResolvePresenceOnline(data, lastLoginUnixMs);
                FriendDataManager.Instance.UpsertRealFriendFromFirebase(
                    friendUid,
                    displayName,
                    handle,
                    isOnline ? "online" : "offline",
                    null,
                    "Firebase",
                    photoUrl,
                    isOnline,
                    lastLoginUnixMs);
            });
    }

    /// <summary>
    /// 拉取别人发给当前用户的好友请求。
    /// </summary>
    public void LoadFriendRequests(Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("friend_requests")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 拉取好友请求失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    if (!doc.Exists) continue;

                    Dictionary<string, object> data = doc.ToDictionary();
                    string status = GetString(data, "status", "pendingReceived");
                    if (status != "pendingReceived") continue;

                    string displayName = GetString(data, "displayName", "未命名用户");
                    string email = GetString(data, "email", string.Empty);
                    FriendDataManager.Instance.UpsertInvite(
                        doc.Id,
                        displayName,
                        email,
                        GetString(data, "photoUrl", string.Empty),
                        status,
                        "请求添加你为好友");
                }

                onComplete?.Invoke(true);
            });
    }

    /// <summary>
    /// 接受好友请求：双方 friends 状态置为 friend，并删除当前用户的请求记录。
    /// </summary>
    public void AcceptFriendRequest(FriendDataManager.InviteData invite, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        if (invite == null || string.IsNullOrEmpty(invite.firebaseUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        var ud = UserDataManager.Instance;
        string currentUid = ud.FirebaseUid;
        string requesterUid = invite.firebaseUid;
        if (string.IsNullOrEmpty(currentUid) || currentUid == requesterUid)
        {
            onComplete?.Invoke(false);
            return;
        }

        Dictionary<string, object> requesterAsFriend = new Dictionary<string, object>
        {
            { "uid", requesterUid },
            { "displayName", invite.name },
            { "email", invite.email },
            { "photoUrl", invite.photoUrl },
            { "status", "friend" },
            { "source", "friendRequest" },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        Dictionary<string, object> currentAsFriend = new Dictionary<string, object>
        {
            { "uid", currentUid },
            { "displayName", ud.UserName },
            { "email", ud.Email },
            { "photoUrl", ud.PhotoUrl },
            { "status", "friend" },
            { "source", "friendRequest" },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        WriteBatch batch = _db.StartBatch();
        DocumentReference currentFriendRef = _db.Collection("users")
            .Document(currentUid)
            .Collection("friends")
            .Document(requesterUid);
        DocumentReference requesterFriendRef = _db.Collection("users")
            .Document(requesterUid)
            .Collection("friends")
            .Document(currentUid);
        DocumentReference currentRequestRef = _db.Collection("users")
            .Document(currentUid)
            .Collection("friend_requests")
            .Document(requesterUid);

        batch.Set(currentFriendRef, requesterAsFriend, SetOptions.MergeAll);
        batch.Set(requesterFriendRef, currentAsFriend, SetOptions.MergeAll);
        batch.Delete(currentRequestRef);

        batch.CommitAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 接受好友请求失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            string handle = string.IsNullOrEmpty(invite.email) ? $"@{requesterUid}" : $"@{invite.email.Split('@')[0]}";
            FriendDataManager.Instance.UpsertRealFriendFromFirebase(
                requesterUid,
                invite.name,
                handle,
                "真实好友",
                invite.headSprite,
                "Firebase 请求",
                invite.photoUrl);
            FriendDataManager.Instance.RemoveInviteByFirebaseUid(requesterUid);

            Debug.Log($"[FirestoreManager] 已接受好友请求: {requesterUid}");
            onComplete?.Invoke(true);
        });
    }

    public void LoadPublicProfile(string uid, Action<UserSearchResult> onComplete)
    {
        if (!CheckReady(_ => onComplete?.Invoke(null))) return;
        if (string.IsNullOrEmpty(uid))
        {
            onComplete?.Invoke(null);
            return;
        }

        _db.Collection("public_profiles")
            .Document(uid)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                {
                    onComplete?.Invoke(null);
                    return;
                }

                Dictionary<string, object> data = task.Result.ToDictionary();
                onComplete?.Invoke(new UserSearchResult
                {
                    uid = uid,
                    displayName = GetString(data, "displayName", "未命名用户"),
                    email = GetString(data, "email", string.Empty),
                    photoUrl = GetString(data, "photoUrl", string.Empty),
                    birthday = GetString(data, "birthday", string.Empty),
                    birthTime = GetString(data, "birthTime", string.Empty),
                    city = GetString(data, "city", string.Empty),
                    isSelf = uid == UserDataManager.Instance.FirebaseUid
                });
            });
    }

    public void FindPublicProfilesByFacebookIds(
        IReadOnlyList<string> facebookIds,
        int limit,
        Action<List<UserSearchResult>> onComplete)
    {
        if (!CheckReady(_ => onComplete?.Invoke(new List<UserSearchResult>()))) return;

        List<string> ids = new List<string>();
        if (facebookIds != null)
        {
            foreach (string id in facebookIds)
            {
                string normalizedId = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
                if (string.IsNullOrEmpty(normalizedId) || ids.Contains(normalizedId)) continue;
                ids.Add(normalizedId);
            }
        }

        int resultLimit = Math.Max(1, limit);
        List<UserSearchResult> results = new List<UserSearchResult>();
        QueryFacebookProfileById(ids, 0, resultLimit, results, onComplete);
    }

    private void QueryFacebookProfileById(
        List<string> facebookIds,
        int index,
        int resultLimit,
        List<UserSearchResult> results,
        Action<List<UserSearchResult>> onComplete)
    {
        if (facebookIds == null || index >= facebookIds.Count || results.Count >= resultLimit)
        {
            onComplete?.Invoke(results ?? new List<UserSearchResult>());
            return;
        }

        string facebookId = facebookIds[index];
        _db.Collection("public_profiles")
            .WhereEqualTo("facebookProviderId", facebookId)
            .Limit(1)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (!task.IsFaulted && !task.IsCanceled)
                {
                    AddUserSearchResults(results, task.Result.Documents, UserDataManager.Instance.FirebaseUid);
                }
                else
                {
                    Debug.LogWarning($"[FirestoreManager] Facebook 好友映射查询失败: {GetTaskError(task.Exception)}");
                }

                QueryFacebookProfileById(facebookIds, index + 1, resultLimit, results, onComplete);
            });
    }

    /// <summary>
    /// 拒绝好友请求：删除当前用户收到的请求。
    /// </summary>
    public void RejectFriendRequest(FriendDataManager.InviteData invite, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        if (invite == null || string.IsNullOrEmpty(invite.firebaseUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        string currentUid = UserDataManager.Instance.FirebaseUid;
        DocumentReference requestRef = _db.Collection("users")
            .Document(currentUid)
            .Collection("friend_requests")
            .Document(invite.firebaseUid);

        requestRef.DeleteAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 拒绝好友请求失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            FriendDataManager.Instance.RemoveInviteByFirebaseUid(invite.firebaseUid);
            onComplete?.Invoke(true);
        });
    }

    /// <summary>
    /// 保存用户创建的虚拟好友档案到 Firestore。
    /// 路径：users/{uid}/virtual_friends/{virtualFriendId}
    /// </summary>
    public void SaveVirtualFriend(FriendDataManager.FriendData virtualFriend, Action<bool> onComplete = null)
    {
        if (virtualFriend == null || !virtualFriend.isVirtual)
        {
            onComplete?.Invoke(false);
            return;
        }

        EnsureVirtualFriendId(virtualFriend);

        string currentUid = GetCurrentFirebaseUid();
        if (string.IsNullOrEmpty(currentUid) || !_isInitialized || _db == null)
        {
            QueuePendingVirtualFriendSave(virtualFriend.virtualFriendId);
            onComplete?.Invoke(false);
            return;
        }

        CommitSaveVirtualFriendWithAvatar(currentUid, virtualFriend, success =>
        {
            if (success)
            {
                RemovePendingVirtualFriendSave(virtualFriend.virtualFriendId);
            }
            else
            {
                QueuePendingVirtualFriendSave(virtualFriend.virtualFriendId);
            }

            onComplete?.Invoke(success);
        });
    }

    private void CommitSaveVirtualFriendWithAvatar(string currentUid, FriendDataManager.FriendData virtualFriend, Action<bool> onComplete)
    {
        if (ShouldUploadPendingVirtualFriendAvatar(virtualFriend))
        {
            AvatarUploadManager.Instance.UploadVirtualFriendAvatarFromFile(
                virtualFriend.virtualFriendId,
                virtualFriend.avatarImagePath,
                result =>
                {
                    FriendDataManager.Instance?.SetVirtualFriendCloudAvatar(
                        virtualFriend,
                        result.photoUrl,
                        result.storagePath,
                        result.previewSprite);
                    CommitSaveVirtualFriend(currentUid, virtualFriend, onComplete);
                },
                error =>
                {
                    Debug.LogWarning("[FirestoreManager] 待同步虚拟好友头像上传失败，将先同步文字档案: " + error);
                    CommitSaveVirtualFriend(currentUid, virtualFriend, onComplete);
                });
            return;
        }

        CommitSaveVirtualFriend(currentUid, virtualFriend, onComplete);
    }

    private bool ShouldUploadPendingVirtualFriendAvatar(FriendDataManager.FriendData virtualFriend)
    {
        return virtualFriend != null
            && AvatarUploadManager.Instance != null
            && !string.IsNullOrWhiteSpace(virtualFriend.virtualFriendId)
            && !string.IsNullOrWhiteSpace(virtualFriend.avatarImagePath)
            && File.Exists(virtualFriend.avatarImagePath)
            && (string.IsNullOrWhiteSpace(virtualFriend.photoUrl)
                || string.IsNullOrWhiteSpace(virtualFriend.avatarStoragePath));
    }

    private void CommitSaveVirtualFriend(string currentUid, FriendDataManager.FriendData virtualFriend, Action<bool> onComplete)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "virtualFriendId", virtualFriend.virtualFriendId },
            { "name", virtualFriend.name },
            { "relationship", virtualFriend.relationship },
            { "birthday", virtualFriend.birthday },
            { "birthTime", virtualFriend.birthTime },
            { "city", virtualFriend.city },
            { "notes", virtualFriend.notes },
            { "avatarKey", string.Empty },
            { "avatarUrl", virtualFriend.photoUrl ?? string.Empty },
            { "avatarStoragePath", virtualFriend.avatarStoragePath ?? string.Empty },
            { "lastOperatedUnixMs", virtualFriend.virtualFriendLastOperatedUnixMs },
            { "isDeleted", false },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        DocumentReference docRef = _db.Collection("users")
            .Document(currentUid)
            .Collection("virtual_friends")
            .Document(virtualFriend.virtualFriendId);

        docRef.GetSnapshotAsync().ContinueWithOnMainThread(readTask =>
        {
            if (!readTask.IsFaulted && !readTask.IsCanceled && !readTask.Result.Exists)
            {
                data["createdAt"] = FieldValue.ServerTimestamp;
            }

            docRef.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(writeTask =>
            {
                if (writeTask.IsFaulted || writeTask.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 保存虚拟好友失败: {writeTask.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                Debug.Log($"[FirestoreManager] 虚拟好友已保存: {virtualFriend.virtualFriendId}");
                onComplete?.Invoke(true);
            });
        });
    }

    /// <summary>
    /// 从 Firestore 拉取虚拟好友档案并合并到本地缓存。
    /// </summary>
    public void LoadVirtualFriends(Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        SyncPendingVirtualFriendSaves();
        SyncPendingVirtualFriendDeletes();

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("virtual_friends")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 拉取虚拟好友失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    if (!doc.Exists) continue;
                    if (IsVirtualFriendPendingSave(doc.Id) || IsVirtualFriendPendingDelete(doc.Id)) continue;

                    Dictionary<string, object> data = doc.ToDictionary();
                    if (GetBool(data, "isDeleted", false)) continue;

                    FriendDataManager.Instance.UpsertVirtualFriendFromFirebase(
                        doc.Id,
                        GetString(data, "name", "未命名好友"),
                        GetString(data, "relationship", "好友"),
                        GetString(data, "birthday", string.Empty),
                        GetString(data, "birthTime", string.Empty),
                        GetString(data, "city", string.Empty),
                        GetString(data, "notes", string.Empty),
                        null,
                        GetString(data, "avatarUrl", string.Empty),
                        GetString(data, "avatarStoragePath", string.Empty),
                        GetLong(data, "lastOperatedUnixMs", GetUnixMs(data, "updatedAt")));
                }

                onComplete?.Invoke(true);
            });
    }

    /// <summary>
    /// 删除用户创建的虚拟好友：云端软删除，本地缓存立即移除。
    /// </summary>
    public void DeleteVirtualFriend(FriendDataManager.FriendData virtualFriend, Action<bool> onComplete = null)
    {
        if (virtualFriend == null || !virtualFriend.isVirtual || string.IsNullOrWhiteSpace(virtualFriend.virtualFriendId))
        {
            onComplete?.Invoke(false);
            return;
        }

        string virtualFriendId = virtualFriend.virtualFriendId;
        string currentUid = GetCurrentFirebaseUid();
        if (string.IsNullOrEmpty(currentUid) || !_isInitialized || _db == null)
        {
            QueueVirtualFriendDeleteLocal(virtualFriendId);
            FriendDataManager.Instance?.RemoveVirtualFriendById(virtualFriendId);
            onComplete?.Invoke(true);
            return;
        }

        CommitDeleteVirtualFriend(currentUid, virtualFriendId, success =>
        {
            if (!success)
            {
                QueueVirtualFriendDeleteLocal(virtualFriendId);
                FriendDataManager.Instance?.RemoveVirtualFriendById(virtualFriendId);
                Debug.LogWarning($"[FirestoreManager] 虚拟好友云端删除失败，已加入待同步删除队列: {virtualFriendId}");
                onComplete?.Invoke(true);
                return;
            }

            RemovePendingVirtualFriendDelete(virtualFriendId);
            RemovePendingVirtualFriendSave(virtualFriendId);
            FriendDataManager.Instance.RemoveVirtualFriendById(virtualFriendId);
            Debug.Log($"[FirestoreManager] 已删除虚拟好友: {virtualFriendId}");
            onComplete?.Invoke(true);
        });
    }

    private void CommitDeleteVirtualFriend(string currentUid, string virtualFriendId, Action<bool> onComplete)
    {
        DocumentReference docRef = _db.Collection("users")
            .Document(currentUid)
            .Collection("virtual_friends")
            .Document(virtualFriendId);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "isDeleted", true },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        docRef.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 删除虚拟好友失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            onComplete?.Invoke(true);
        });
    }

    /// <summary>
    /// 保存当前 AI 记忆到 Firestore。
    /// 路径：users/{uid}/memories/runtime
    /// </summary>
    public void SaveMemorySource(MemorySource source, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        DocumentReference docRef = _db.Collection("users")
            .Document(currentUid)
            .Collection("memories")
            .Document("runtime");

        Dictionary<string, object> data = SerializeMemorySource(source ?? new MemorySource());
        data["updatedAt"] = FieldValue.ServerTimestamp;

        docRef.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 保存 AI 记忆失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            Debug.Log("[FirestoreManager] AI 记忆已保存");
            onComplete?.Invoke(true);
        });
    }

    /// <summary>
    /// 加载当前用户 AI 记忆。
    /// </summary>
    public void LoadMemorySource(Action<MemorySource> onComplete)
    {
        if (!CheckReady(_ => onComplete?.Invoke(null))) return;

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(null);
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("memories")
            .Document("runtime")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                {
                    onComplete?.Invoke(null);
                    return;
                }

                onComplete?.Invoke(DeserializeMemorySource(task.Result.ToDictionary()));
            });
    }

    /// <summary>
    /// 删除当前用户 AI 记忆。
    /// </summary>
    public void DeleteMemorySource(Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("memories")
            .Document("runtime")
            .DeleteAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 删除 AI 记忆失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                onComplete?.Invoke(true);
            });
    }

    /// <summary>
    /// 保存明日钩子。
    /// 路径：users/{uid}/tomorrow_hooks/{hookId}
    /// 同时镜像到 memories/runtime.tomorrowHooks，供 Oracle Runtime 组装上下文。
    /// </summary>
    public void SaveTomorrowHook(TomorrowHook hook, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        if (hook == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(hook.hookId))
            hook.hookId = Guid.NewGuid().ToString("N").Substring(0, 10);
        if (string.IsNullOrWhiteSpace(hook.userId))
            hook.userId = currentUid;
        if (string.IsNullOrWhiteSpace(hook.scheduledForLocalDate))
            hook.scheduledForLocalDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
        if (string.IsNullOrWhiteSpace(hook.status))
            hook.status = "pending";

        Dictionary<string, object> data = SerializeTomorrowHook(hook);
        data["createdAt"] = FieldValue.ServerTimestamp;
        data["updatedAt"] = FieldValue.ServerTimestamp;

        _db.Collection("users")
            .Document(currentUid)
            .Collection("tomorrow_hooks")
            .Document(hook.hookId)
            .SetAsync(data, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 保存明日钩子失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                UpsertTomorrowHookInRuntimeMemory(hook);
                Debug.Log($"[FirestoreManager] 明日钩子已保存: {hook.hookId}");
                onComplete?.Invoke(true);
            });
    }

    /// <summary>
    /// 读取今天及更早到期的明日钩子。
    /// </summary>
    public void LoadDueTomorrowHooks(Action<List<TomorrowHook>> onComplete)
    {
        if (!CheckReady(_ => onComplete?.Invoke(new List<TomorrowHook>()))) return;

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(new List<TomorrowHook>());
            return;
        }

        string today = DateTime.Now.ToString("yyyy-MM-dd");
        _db.Collection("users")
            .Document(currentUid)
            .Collection("tomorrow_hooks")
            .WhereEqualTo("status", "pending")
            .Limit(30)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                List<TomorrowHook> hooks = new List<TomorrowHook>();
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[FirestoreManager] 读取明日钩子失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(hooks);
                    return;
                }

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    if (!doc.Exists) continue;
                    TomorrowHook hook = DeserializeTomorrowHook(doc.ToDictionary());
                    if (hook == null) continue;
                    if (string.IsNullOrWhiteSpace(hook.hookId))
                        hook.hookId = doc.Id;
                    if (string.CompareOrdinal(hook.scheduledForLocalDate, today) <= 0)
                        hooks.Add(hook);
                }

                hooks.Sort((a, b) => string.CompareOrdinal(a.scheduledForLocalDate, b.scheduledForLocalDate));
                onComplete?.Invoke(hooks);
            });
    }

    public void MarkTomorrowHookOpened(string hookId, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid) || string.IsNullOrWhiteSpace(hookId))
        {
            onComplete?.Invoke(false);
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("tomorrow_hooks")
            .Document(hookId)
            .SetAsync(new Dictionary<string, object>
            {
                { "status", "opened" },
                { "openedAt", FieldValue.ServerTimestamp },
                { "updatedAt", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                bool success = !(task.IsFaulted || task.IsCanceled);
                if (!success)
                    Debug.LogWarning($"[FirestoreManager] 标记明日钩子失败: {task.Exception?.InnerException?.Message}");
                else
                    UpdateTomorrowHookStatusInRuntimeMemory(hookId, "opened");

                onComplete?.Invoke(success);
            });
    }

    private void UpsertTomorrowHookInRuntimeMemory(TomorrowHook hook)
    {
        if (hook == null || string.IsNullOrWhiteSpace(hook.hookId)) return;

        LoadMemorySource(source =>
        {
            source ??= new MemorySource();
            source.tomorrowHooks ??= new List<TomorrowHook>();
            int index = source.tomorrowHooks.FindIndex(item => item != null && item.hookId == hook.hookId);
            if (index >= 0)
                source.tomorrowHooks[index] = hook;
            else
                source.tomorrowHooks.Add(hook);

            DialogSystem.Instance?.SetMemorySource(source);
            SaveMemorySource(source);
        });
    }

    private void UpdateTomorrowHookStatusInRuntimeMemory(string hookId, string status)
    {
        if (string.IsNullOrWhiteSpace(hookId)) return;

        LoadMemorySource(source =>
        {
            if (source?.tomorrowHooks == null) return;
            bool changed = false;
            foreach (TomorrowHook hook in source.tomorrowHooks)
            {
                if (hook == null || hook.hookId != hookId) continue;
                hook.status = status;
                changed = true;
            }

            if (!changed) return;
            DialogSystem.Instance?.SetMemorySource(source);
            SaveMemorySource(source);
        });
    }

    /// <summary>
    /// 保存用户反馈。
    /// 路径：users/{uid}/feedback/{feedbackId}
    /// 后台镜像：feedback/{feedbackId}
    /// </summary>
    public void SaveFeedback(string category, string tag, string content, string source, Action<bool> onComplete = null, string feedbackId = null)
    {
        if (!CheckReady(onComplete)) return;

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        UserDataManager ud = UserDataManager.Instance;
        feedbackId = string.IsNullOrWhiteSpace(feedbackId) ? Guid.NewGuid().ToString("N") : feedbackId.Trim();
        DocumentReference docRef = _db.Collection("users")
            .Document(currentUid)
            .Collection("feedback")
            .Document(feedbackId);

        var data = new Dictionary<string, object>
        {
            { "feedbackId", feedbackId },
            { "uid", currentUid },
            { "displayName", ud != null ? ud.UserName : string.Empty },
            { "email", ud != null ? ud.Email : string.Empty },
            { "category", category ?? "community" },
            { "tag", tag ?? "general" },
            { "content", content ?? string.Empty },
            { "source", source ?? "app" },
            { "status", "new" },
            { "appVersion", Application.version },
            { "platform", Application.platform.ToString() },
            { "deviceModel", SystemInfo.deviceModel },
            { "createdAt", FieldValue.ServerTimestamp },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        docRef.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 保存反馈失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            Debug.Log($"[FirestoreManager] 反馈已保存: {feedbackId}");
            MirrorFeedbackForBackend(feedbackId, data);
            onComplete?.Invoke(true);
        });
    }

    /// <summary>
    /// 加载用户最近提交的反馈。
    /// </summary>
    public void LoadFeedback(Action<List<CloudFeedbackEntry>> onComplete, int limit = 30)
    {
        if (!CheckReady(_ => onComplete?.Invoke(new List<CloudFeedbackEntry>()))) return;

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(new List<CloudFeedbackEntry>());
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("feedback")
            .OrderByDescending("createdAt")
            .Limit(Mathf.Clamp(limit, 1, 100))
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                var result = new List<CloudFeedbackEntry>();
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 加载反馈失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(null);
                    return;
                }

                foreach (var doc in task.Result.Documents)
                {
                    if (!doc.Exists) continue;
                    Dictionary<string, object> data = doc.ToDictionary();
                    string createdAt = string.Empty;
                    if (doc.TryGetValue("createdAt", out Timestamp ts))
                        createdAt = ts.ToDateTime().ToLocalTime().ToString("MM/dd HH:mm");
                    else
                        createdAt = GetString(data, "createdAt", string.Empty);

                    result.Add(new CloudFeedbackEntry
                    {
                        feedbackId = GetString(data, "feedbackId", doc.Id),
                        category = GetString(data, "category", "community"),
                        tag = GetString(data, "tag", "general"),
                        content = GetString(data, "content", string.Empty),
                        source = GetString(data, "source", "app"),
                        status = GetString(data, "status", "new"),
                        createdAt = createdAt,
                    });
                }

                onComplete?.Invoke(result);
            });
    }

    /// <summary>
    /// 保存通知偏好。
    /// 路径：users/{uid}/settings/notifications
    /// </summary>
    public void SaveNotificationSettings(NotificationSettingsManager settings, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        if (settings == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        DocumentReference docRef = _db.Collection("users")
            .Document(currentUid)
            .Collection("settings")
            .Document("notifications");

        var data = new Dictionary<string, object>
        {
            { "dailyOracleEnabled", settings.DailyOracleEnabled },
            { "dialogueReplyEnabled", settings.DialogueReplyEnabled },
            { "divinationReturnEnabled", settings.DivinationReturnEnabled },
            { "friendInteractionEnabled", settings.FriendInteractionEnabled },
            { "activitySystemEnabled", settings.ActivitySystemEnabled },
            { "reminderTime", settings.ReminderTime },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        docRef.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 保存通知设置失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            onComplete?.Invoke(true);
        });
    }

    /// <summary>
    /// 加载通知偏好。
    /// </summary>
    public void LoadNotificationSettings(Action<CloudNotificationSettings> onComplete)
    {
        if (!CheckReady(_ => onComplete?.Invoke(null))) return;

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(null);
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("settings")
            .Document("notifications")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                {
                    onComplete?.Invoke(null);
                    return;
                }

                Dictionary<string, object> data = task.Result.ToDictionary();
                onComplete?.Invoke(new CloudNotificationSettings
                {
                    dailyOracleEnabled = GetBool(data, "dailyOracleEnabled", true),
                    dialogueReplyEnabled = GetBool(data, "dialogueReplyEnabled", true),
                    divinationReturnEnabled = GetBool(data, "divinationReturnEnabled", true),
                    friendInteractionEnabled = GetBool(data, "friendInteractionEnabled", false),
                    activitySystemEnabled = GetBool(data, "activitySystemEnabled", true),
                    reminderTime = GetString(data, "reminderTime", "08:30"),
                });
            });
    }

    /// <summary>
    /// 保存每日占卜同步设置。
    /// 路径：users/{uid}/settings/daily_divination_sync
    /// </summary>
    public void SaveDailyDivinationSyncSettings(DailyDivinationSyncSettings settings, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        settings ??= DailyDivinationSyncSettingsManager.Instance.GetSettings();

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        var data = new Dictionary<string, object>
        {
            { "enabled", settings.enabled },
            { "visibility", settings.VisibilityKey },
            { "summaryOnly", true },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        _db.Collection("users")
            .Document(currentUid)
            .Collection("settings")
            .Document("daily_divination_sync")
            .SetAsync(data, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 保存每日占卜同步设置失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                onComplete?.Invoke(true);
            });
    }

    /// <summary>
    /// 加载每日占卜同步设置。
    /// </summary>
    public void LoadDailyDivinationSyncSettings(Action<CloudDailyDivinationSyncSettings> onComplete)
    {
        if (!CheckReady(_ => onComplete?.Invoke(null))) return;

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(null);
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("settings")
            .Document("daily_divination_sync")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                {
                    onComplete?.Invoke(null);
                    return;
                }

                Dictionary<string, object> data = task.Result.ToDictionary();
                onComplete?.Invoke(new CloudDailyDivinationSyncSettings
                {
                    enabled = GetBool(data, "enabled", true),
                    visibility = GetString(data, "visibility", "only_me"),
                    summaryOnly = GetBool(data, "summaryOnly", true),
                });
            });
    }

    /// <summary>
    /// 保存记忆隐私设置。
    /// 路径：users/{uid}/settings/memory_privacy
    /// </summary>
    public void SaveMemoryPrivacySettings(Action<bool> onComplete = null)
    {
        SaveMemoryPrivacySettings(MemoryPrivacySettings.CreateSnapshot(), onComplete);
    }

    public void SaveMemoryPrivacySettings(MemoryPrivacySettingsSnapshot settings, Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;
        settings ??= new MemoryPrivacySettingsSnapshot();

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        var data = new Dictionary<string, object>
        {
            { "shareAllMemoryEnabled", settings.shareAllMemoryEnabled },
            { "autoTopicEnabled", settings.autoTopicEnabled },
            { "autoPreferenceEnabled", settings.autoPreferenceEnabled },
            { "autoEmotionEnabled", settings.autoEmotionEnabled },
            { "autoGrowthEnabled", settings.autoGrowthEnabled },
            { "requireConfirmBeforeAdd", settings.requireConfirmBeforeAdd },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        _db.Collection("users")
            .Document(currentUid)
            .Collection("settings")
            .Document("memory_privacy")
            .SetAsync(data, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 保存记忆隐私设置失败: {task.Exception?.InnerException?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                onComplete?.Invoke(true);
            });
    }

    /// <summary>
    /// 加载记忆隐私设置。
    /// </summary>
    public void LoadMemoryPrivacySettings(Action<bool> onComplete = null)
    {
        if (!CheckReady(onComplete)) return;

        string currentUid = UserDataManager.Instance.FirebaseUid;
        if (string.IsNullOrEmpty(currentUid))
        {
            onComplete?.Invoke(false);
            return;
        }

        _db.Collection("users")
            .Document(currentUid)
            .Collection("settings")
            .Document("memory_privacy")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                {
                    onComplete?.Invoke(false);
                    return;
                }

                Dictionary<string, object> data = task.Result.ToDictionary();
                MemoryPrivacySettings.ApplySnapshot(new MemoryPrivacySettingsSnapshot
                {
                    shareAllMemoryEnabled = GetBool(data, "shareAllMemoryEnabled", true),
                    autoTopicEnabled = GetBool(data, "autoTopicEnabled", true),
                    autoPreferenceEnabled = GetBool(data, "autoPreferenceEnabled", true),
                    autoEmotionEnabled = GetBool(data, "autoEmotionEnabled", true),
                    autoGrowthEnabled = GetBool(data, "autoGrowthEnabled", false),
                    requireConfirmBeforeAdd = GetBool(data, "requireConfirmBeforeAdd", true),
                });

                onComplete?.Invoke(true);
            });
    }

    /// <summary>
    /// 加载公开配置：社媒链接、IAP 商品 ID 等。
    /// 路径：app_config/public
    /// </summary>
    public void LoadPublicAppConfig(Action<PublicAppConfig> onComplete)
    {
        PublicAppConfig fallback = NormalizePublicAppConfig(LoadCachedPublicAppConfig() ?? PublicAppConfig.Default);
        if (!_isInitialized || _db == null)
        {
            onComplete?.Invoke(fallback);
            return;
        }

        _db.Collection("app_config")
            .Document("public")
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                {
                    onComplete?.Invoke(fallback);
                    return;
                }

                PublicAppConfig config = DeserializePublicAppConfig(task.Result.ToDictionary());
                config = NormalizePublicAppConfig(config);
                SavePublicAppConfigCache(config);
                onComplete?.Invoke(config);
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

        string uid = UserDataManager.Instance?.FirebaseUid ?? string.Empty;
        if (string.IsNullOrEmpty(uid))
            uid = Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? string.Empty;
        if (string.IsNullOrEmpty(uid))
        {
            onComplete?.Invoke(false);
            return;
        }

        DeleteKnownUserCollections(uid, success =>
        {
            if (!success)
            {
                onComplete?.Invoke(false);
                return;
            }

            DeleteTopLevelUserReferences(uid, topLevelDeleted =>
            {
                if (!topLevelDeleted)
                {
                    onComplete?.Invoke(false);
                    return;
                }

                WriteBatch batch = _db.StartBatch();
                batch.Delete(_db.Collection("public_profiles").Document(uid));
                batch.Delete(_db.Collection("users").Document(uid));

                batch.CommitAsync().ContinueWithOnMainThread(task =>
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
            });
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
        void CompleteAfterLogin(bool success)
        {
            if (success)
            {
                SyncPendingRealFriendDeletes();
                SyncPendingRealFriendBlocks();
            }
            onComplete?.Invoke(success);
        }

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
                CreateUserData(CompleteAfterLogin);
            }
            else
            {
                // 情况 2：云端有数据 → 老用户换设备/重新登录
                // 第一步：先用云端数据补齐本地的空缺部分
                ApplyCloudToLocal(snapshot);
                LoadMemorySource(source =>
                {
                    if (source != null)
                        DialogSystem.Instance?.SetMemorySource(source);
                });
                // 第二步：再把本地最终的合并结果推给云端保存（同时刷新最后登录时间 lastSignInAt）
                SaveUserData(CompleteAfterLogin);
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

        // === 基础信息策略：本地为空才用云端的；已知错误默认名不再覆盖社交账号名 ===
        // 这样做的原因是：如果玩家在无网时改了名字（存在本地），连网时这里不会拿云端的旧名字去冲掉玩家刚改的新名字。
        // TryGetValue 是一种安全的取值方式，如果云端没有该字段，不会报错，只会返回 false
        if (snapshot.TryGetValue("displayName", out string cloudName) && ShouldApplyCloudDisplayName(cloudName, ud.UserName))
            ud.SetUserName(cloudName);

        if (string.IsNullOrWhiteSpace(ud.Birthday) && snapshot.TryGetValue("birthday", out string cloudBirthday))
            ud.SetBirthday(cloudBirthday);

        if (string.IsNullOrWhiteSpace(ud.BirthTime) && snapshot.TryGetValue("birthTime", out string cloudBirthTime))
            ud.SetBirthTime(cloudBirthTime);

        if (string.IsNullOrWhiteSpace(ud.City) && snapshot.TryGetValue("city", out string cloudCity))
            ud.SetCity(cloudCity);

        if (string.IsNullOrWhiteSpace(ud.ProfileBio) && snapshot.TryGetValue("bio", out string cloudBio))
            ud.SetProfileBio(cloudBio);

        // === 账户信息策略：云端优先 ===
        // 因为这些数据（如邮箱、头像验证状态等）是高权限或第三方获取的，必须以服务器状态为准
        if (snapshot.TryGetValue("email", out string cloudEmail))
            ud.SetEmail(cloudEmail);

        if (snapshot.TryGetValue("photoUrl", out string cloudPhotoUrl))
            ud.SetPhotoUrl(cloudPhotoUrl);

        if (snapshot.TryGetValue("avatarStoragePath", out string cloudAvatarStoragePath))
            ud.SetAvatarStoragePath(cloudAvatarStoragePath);

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

    private static bool ShouldApplyCloudDisplayName(string cloudName, string localName)
    {
        string normalizedCloudName = (cloudName ?? string.Empty).Trim();
        string normalizedLocalName = (localName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedCloudName))
            return false;

        if (UserDataManager.IsKnownGeneratedDisplayName(normalizedCloudName))
            return false;

        if (string.IsNullOrWhiteSpace(normalizedLocalName))
            return true;

        if (UserDataManager.IsKnownGeneratedDisplayName(normalizedLocalName))
            return true;

        string providerName = GetCurrentProviderDisplayName();
        return !string.IsNullOrWhiteSpace(providerName)
            && normalizedLocalName == providerName.Trim()
            && normalizedCloudName != normalizedLocalName;
    }

    private static string GetCurrentProviderDisplayName()
    {
        var user = Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser;
        if (user == null) return string.Empty;

        string preferredProviderId = UserDataManager.Instance != null
            ? UserDataManager.Instance.CurrentLoginType switch
            {
                LoginType.Google => "google.com",
                LoginType.Apple => "apple.com",
                LoginType.Facebook => "facebook.com",
                LoginType.GameCenter => Firebase.Auth.GameCenterAuthProvider.ProviderId,
                _ => string.Empty,
            }
            : string.Empty;

        foreach (var provider in user.ProviderData)
        {
            if (!string.IsNullOrWhiteSpace(preferredProviderId) && provider.ProviderId != preferredProviderId)
                continue;

            if (!string.IsNullOrWhiteSpace(provider.DisplayName))
                return provider.DisplayName.Trim();
        }

        return string.IsNullOrWhiteSpace(user.DisplayName)
            ? string.Empty
            : user.DisplayName.Trim();
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

    private void SavePublicProfile()
    {
        if (!_isInitialized || _db == null || UserDataManager.Instance == null) return;

        var ud = UserDataManager.Instance;
        if (string.IsNullOrEmpty(ud.FirebaseUid)) return;
        string facebookProviderId = GetFacebookProviderIdForCurrentUser(ud);
        long nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "uid", ud.FirebaseUid },
            { "displayName", ud.UserName },
            { "displayNameLower", NormalizeSearchText(ud.UserName) },
            { "searchKeywords", BuildSearchKeywords(ud.UserName, ud.Email) },
            { "email", ud.Email },
            { "emailLower", NormalizeSearchText(ud.Email) },
            { "photoUrl", ud.PhotoUrl },
            { "avatarStoragePath", ud.AvatarStoragePath },
            { "bio", ud.ProfileBio },
            { "facebookProviderId", facebookProviderId },
            { "isOnline", true },
            { "lastActiveUnixMs", nowUnixMs },
            { "lastActiveAt", FieldValue.ServerTimestamp },
            { "lastSignInUnixMs", ud.LastSignInTimestamp },
            { "lastSignInAt", FieldValue.ServerTimestamp },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        _db.Collection("public_profiles")
            .Document(ud.FirebaseUid)
            .SetAsync(data, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[FirestoreManager] 公开资料保存失败: {task.Exception?.InnerException?.Message}");
                }
            });
    }

    private static string GetFacebookProviderIdForCurrentUser(UserDataManager userData)
    {
        if (userData == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(userData.FacebookProviderUserId))
            return userData.FacebookProviderUserId;

        string providerId = FacebookUserInfoHelper.GetFacebookProviderUserId();
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            userData.SetFacebookProviderUserId(providerId);
            userData.SaveData();
        }

        return providerId ?? string.Empty;
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

    private static string NormalizeSearchText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static string GetTaskError(Exception exception)
    {
        if (exception == null) return "未知错误";
        return exception.InnerException?.Message ?? exception.Message;
    }

    private static int GetUserSearchFetchLimit(int resultLimit)
    {
        resultLimit = Math.Max(1, resultLimit);
        return Math.Min(Math.Max(resultLimit * 3, resultLimit + 3), 20);
    }

    private static int GetUserRecommendationFetchLimit(int resultLimit)
    {
        resultLimit = Math.Max(1, resultLimit);
        return Math.Min(Math.Max(resultLimit * 20, 60), 120);
    }

    private static int GetUserSearchScanLimit(int resultLimit)
    {
        resultLimit = Math.Max(1, resultLimit);
        return Math.Min(Math.Max(resultLimit * 20, 80), 240);
    }

    private static int CountNonSelfResults(List<UserSearchResult> results, string currentUid)
    {
        if (results == null) return 0;

        int count = 0;
        foreach (UserSearchResult result in results)
        {
            if (result == null) continue;
            if (!string.IsNullOrEmpty(currentUid) && result.uid == currentUid) continue;
            count++;
        }

        return count;
    }

    private static List<UserSearchResult> BuildRecommendedUsers(List<UserSearchResult> candidates, string currentUid, int resultLimit)
    {
        List<UserSearchResult> recommended = new List<UserSearchResult>();
        if (candidates == null) return recommended;

        ShuffleUserSearchResults(candidates);
        foreach (UserSearchResult result in candidates)
        {
            if (result == null || string.IsNullOrEmpty(result.uid)) continue;
            if (!string.IsNullOrEmpty(currentUid) && result.uid == currentUid) continue;
            if (FriendDataManager.Instance != null)
            {
                if (FriendDataManager.Instance.FindRealFriendByFirebaseUid(result.uid) != null) continue;
                if (FriendDataManager.Instance.IsUserBlocked(result.uid)) continue;
            }

            if (recommended.Exists(item => item.uid == result.uid)) continue;
            recommended.Add(result);
            if (recommended.Count >= resultLimit) break;
        }

        return recommended;
    }

    private static void ShuffleUserSearchResults(List<UserSearchResult> results)
    {
        if (results == null || results.Count <= 1) return;

        System.Random random = new System.Random(Environment.TickCount);
        for (int i = results.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            UserSearchResult temp = results[i];
            results[i] = results[swapIndex];
            results[swapIndex] = temp;
        }
    }

    private static List<string> BuildSearchKeywords(string displayName, string email)
    {
        HashSet<string> keywords = new HashSet<string>();
        AddSearchKeywordsForValue(keywords, displayName);
        AddSearchKeywordsForValue(keywords, email);

        string emailLower = NormalizeSearchText(email);
        int atIndex = emailLower.IndexOf('@');
        if (atIndex > 0)
            AddSearchKeywordsForValue(keywords, emailLower.Substring(0, atIndex));

        return new List<string>(keywords);
    }

    private static void AddSearchKeywordsForValue(HashSet<string> keywords, string rawValue)
    {
        string value = NormalizeSearchText(rawValue);
        if (string.IsNullOrEmpty(value) || keywords == null) return;

        AddKeyword(keywords, value);
        for (int len = 1; len <= value.Length && keywords.Count < 80; len++)
        {
            AddKeyword(keywords, value.Substring(0, len));
        }

        for (int start = 0; start < value.Length && keywords.Count < 80; start++)
        {
            for (int len = 2; start + len <= value.Length && keywords.Count < 80; len++)
            {
                AddKeyword(keywords, value.Substring(start, len));
            }
        }

        string[] tokens = value.Split(new[] { ' ', '.', '_', '-', '@' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string token in tokens)
            AddKeyword(keywords, token);
    }

    private static void AddKeyword(HashSet<string> keywords, string keyword)
    {
        if (keywords == null || string.IsNullOrWhiteSpace(keyword)) return;
        keyword = NormalizeSearchText(keyword);
        if (keyword.Length > 32)
            keyword = keyword.Substring(0, 32);
        keywords.Add(keyword);
    }

    private static List<UserSearchResult> LimitUserSearchResults(
        List<UserSearchResult> results,
        string currentUid,
        int resultLimit)
    {
        List<UserSearchResult> limited = new List<UserSearchResult>();
        UserSearchResult selfResult = null;
        int nonSelfCount = 0;

        foreach (UserSearchResult result in results)
        {
            if (result == null) continue;

            if (!string.IsNullOrEmpty(currentUid) && result.uid == currentUid)
            {
                selfResult ??= result;
                continue;
            }

            if (nonSelfCount >= resultLimit) continue;
            limited.Add(result);
            nonSelfCount++;
        }

        if (selfResult != null)
        {
            limited.Insert(0, selfResult);
        }

        return limited;
    }

#if UNITY_EDITOR
    private class EditorRestSearchResult
    {
        public List<UserSearchResult> users = new List<UserSearchResult>();
        public string proxyLabel = string.Empty;
        public string error = string.Empty;
    }

    private void SearchUsersByNameViaRestInEditor(
        string keyword,
        string normalizedKeyword,
        string currentUid,
        int resultLimit,
        int fetchLimit,
        Action<List<UserSearchResult>> onComplete)
    {
        string projectId = GetFirebaseProjectIdForRest();
        string trimmedKeyword = keyword.Trim();

        Task.Run(() => SearchUsersByNameViaRest(projectId, trimmedKeyword, normalizedKeyword, currentUid, resultLimit, fetchLimit))
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    LastUserSearchError = $"Editor REST 搜索失败: {GetTaskError(task.Exception)}";
                    Debug.LogWarning($"[FirestoreManager] {LastUserSearchError}");
                    onComplete?.Invoke(new List<UserSearchResult>());
                    return;
                }

                EditorRestSearchResult result = task.Result;
                if (!string.IsNullOrEmpty(result.error))
                {
                    LastUserSearchError = result.error;
                    Debug.LogWarning($"[FirestoreManager] {LastUserSearchError}");
                }
                else
                {
                    LastUserSearchError = string.Empty;
                    Debug.Log($"[FirestoreManager] Editor REST 搜索成功，来源={result.proxyLabel}，数量={result.users.Count}");
                    foreach (UserSearchResult user in result.users)
                    {
                        if (user == null) continue;
                        Debug.Log($"[FirestoreManager] Editor REST 搜索结果 uid={user.uid}, name={user.displayName}, self={user.isSelf}");
                    }
                }

                onComplete?.Invoke(result.users);
            });
    }

    private void LoadRecommendedUsersViaRestInEditor(Action<List<UserSearchResult>> onComplete, int limit)
    {
        string projectId = GetFirebaseProjectIdForRest();
        string currentUid = GetCurrentUserSearchUid();
        int resultLimit = Math.Max(1, limit);
        int fetchLimit = GetUserRecommendationFetchLimit(resultLimit);

        Task.Run(() => LoadRecommendedUsersViaRest(projectId, currentUid, fetchLimit))
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[FirestoreManager] Editor REST 好友推荐失败: {GetTaskError(task.Exception)}");
                    onComplete?.Invoke(new List<UserSearchResult>());
                    return;
                }

                EditorRestSearchResult result = task.Result;
                if (!string.IsNullOrEmpty(result.error))
                {
                    Debug.LogWarning($"[FirestoreManager] {result.error}");
                    onComplete?.Invoke(new List<UserSearchResult>());
                    return;
                }

                List<UserSearchResult> users = BuildRecommendedUsers(result.users, currentUid, resultLimit);
                Debug.Log($"[FirestoreManager] Editor REST 好友推荐成功，来源={result.proxyLabel}，数量={users.Count}");
                onComplete?.Invoke(users);
            });
    }

    private static EditorRestSearchResult SearchUsersByNameViaRest(
        string projectId,
        string keyword,
        string normalizedKeyword,
        string currentUid,
        int resultLimit,
        int fetchLimit)
    {
        EditorRestSearchResult finalResult = new EditorRestSearchResult();
        if (string.IsNullOrEmpty(projectId))
        {
            finalResult.error = "Editor REST 搜索失败：Firebase ProjectId 为空";
            return finalResult;
        }

        string endpoint = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents:runQuery";
        string prefixBody = BuildPrefixUserSearchBody(normalizedKeyword, fetchLimit);
        string keywordBody = BuildKeywordUserSearchBody(normalizedKeyword, fetchLimit);
        string scanBody = BuildScanUserSearchBody(GetUserSearchScanLimit(resultLimit));
        string lastError = string.Empty;

        foreach (string proxyUrl in GetEditorFirestoreProxyCandidates())
        {
            try
            {
                List<UserSearchResult> users = new List<UserSearchResult>();
                string prefixJson = PostFirestoreRestJson(endpoint, prefixBody, proxyUrl);
                AddUserSearchResultsFromRestJson(users, prefixJson, currentUid);

                if (CountNonSelfResults(users, currentUid) < resultLimit)
                {
                    try
                    {
                        string keywordJson = PostFirestoreRestJson(endpoint, keywordBody, proxyUrl);
                        AddUserSearchResultsFromRestJson(users, keywordJson, currentUid);
                    }
                    catch (Exception keywordException)
                    {
                        Debug.LogWarning($"[FirestoreManager] Editor REST 关键词搜索失败: {keywordException.Message}");
                    }
                }

                if (CountNonSelfResults(users, currentUid) < resultLimit)
                {
                    try
                    {
                        string scanJson = PostFirestoreRestJson(endpoint, scanBody, proxyUrl);
                        AddUserSearchResultsFromRestJson(users, scanJson, currentUid, normalizedKeyword);
                    }
                    catch (Exception scanException)
                    {
                        Debug.LogWarning($"[FirestoreManager] Editor REST 扫描搜索失败: {scanException.Message}");
                    }
                }

                finalResult.users = LimitUserSearchResults(users, currentUid, resultLimit);
                finalResult.proxyLabel = DescribeProxy(proxyUrl);
                return finalResult;
            }
            catch (Exception e)
            {
                lastError = $"{DescribeProxy(proxyUrl)}: {e.Message}";
            }
        }

        finalResult.error = string.IsNullOrEmpty(lastError)
            ? "Editor REST 搜索失败：没有可用网络通道"
            : $"Editor REST 搜索失败，最后错误：{lastError}";
        return finalResult;
    }

    private static EditorRestSearchResult LoadRecommendedUsersViaRest(string projectId, string currentUid, int fetchLimit)
    {
        EditorRestSearchResult finalResult = new EditorRestSearchResult();
        if (string.IsNullOrEmpty(projectId))
        {
            finalResult.error = "Editor REST 好友推荐失败：Firebase ProjectId 为空";
            return finalResult;
        }

        string endpoint = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents:runQuery";
        string scanBody = BuildScanUserSearchBody(fetchLimit);
        string lastError = string.Empty;

        foreach (string proxyUrl in GetEditorFirestoreProxyCandidates())
        {
            try
            {
                List<UserSearchResult> users = new List<UserSearchResult>();
                string json = PostFirestoreRestJson(endpoint, scanBody, proxyUrl);
                AddUserSearchResultsFromRestJson(users, json, currentUid);
                finalResult.users = users;
                finalResult.proxyLabel = DescribeProxy(proxyUrl);
                return finalResult;
            }
            catch (Exception e)
            {
                lastError = $"{DescribeProxy(proxyUrl)}: {e.Message}";
            }
        }

        finalResult.error = string.IsNullOrEmpty(lastError)
            ? "Editor REST 好友推荐失败：没有可用网络通道"
            : $"Editor REST 好友推荐失败，最后错误：{lastError}";
        return finalResult;
    }

    private static string GetFirebaseProjectIdForRest()
    {
        try
        {
            object options = FirebaseApp.DefaultInstance?.Options;
            object value = options?.GetType().GetProperty("ProjectId")?.GetValue(options, null);
            string projectId = value?.ToString();
            if (!string.IsNullOrEmpty(projectId)) return projectId;
        }
        catch
        {
            // Editor fallback will use the known project id below.
        }

        return "fari-app-b2fd2";
    }

    private static List<string> GetEditorFirestoreProxyCandidates()
    {
        List<string> candidates = new List<string>();
        AddProxyCandidate(candidates, Environment.GetEnvironmentVariable("HTTPS_PROXY"));
        AddProxyCandidate(candidates, Environment.GetEnvironmentVariable("HTTP_PROXY"));
        AddProxyCandidate(candidates, "http://127.0.0.1:7897");
        AddProxyCandidate(candidates, "http://127.0.0.1:7890");
        AddProxyCandidate(candidates, "http://127.0.0.1:1087");
        AddProxyCandidate(candidates, "http://127.0.0.1:1080");
        AddProxyCandidate(candidates, string.Empty);
        return candidates;
    }

    private static void AddProxyCandidate(List<string> candidates, string value)
    {
        string normalized = NormalizeProxyUrl(value);
        if (candidates.Contains(normalized)) return;
        candidates.Add(normalized);
    }

    private static string NormalizeProxyUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string trimmed = value.Trim();
        if (!trimmed.Contains("://")) trimmed = "http://" + trimmed;
        return trimmed;
    }

    private static string DescribeProxy(string proxyUrl)
    {
        return string.IsNullOrEmpty(proxyUrl) ? "direct" : proxyUrl;
    }

    private static string BuildPrefixUserSearchBody(string normalizedKeyword, int limit)
    {
        JObject body = new JObject
        {
            ["structuredQuery"] = new JObject
            {
                ["from"] = new JArray(new JObject { ["collectionId"] = "public_profiles" }),
                ["where"] = new JObject
                {
                    ["compositeFilter"] = new JObject
                    {
                        ["op"] = "AND",
                        ["filters"] = new JArray
                        {
                            new JObject
                            {
                                ["fieldFilter"] = new JObject
                                {
                                    ["field"] = new JObject { ["fieldPath"] = "displayNameLower" },
                                    ["op"] = "GREATER_THAN_OR_EQUAL",
                                    ["value"] = new JObject { ["stringValue"] = normalizedKeyword }
                                }
                            },
                            new JObject
                            {
                                ["fieldFilter"] = new JObject
                                {
                                    ["field"] = new JObject { ["fieldPath"] = "displayNameLower" },
                                    ["op"] = "LESS_THAN_OR_EQUAL",
                                    ["value"] = new JObject { ["stringValue"] = normalizedKeyword + "\uf8ff" }
                                }
                            }
                        }
                    }
                },
                ["orderBy"] = new JArray(new JObject
                {
                    ["field"] = new JObject { ["fieldPath"] = "displayNameLower" },
                    ["direction"] = "ASCENDING"
                }),
                ["limit"] = limit
            }
        };

        return body.ToString(Formatting.None);
    }

    private static string BuildKeywordUserSearchBody(string normalizedKeyword, int limit)
    {
        JObject body = new JObject
        {
            ["structuredQuery"] = new JObject
            {
                ["from"] = new JArray(new JObject { ["collectionId"] = "public_profiles" }),
                ["where"] = new JObject
                {
                    ["fieldFilter"] = new JObject
                    {
                        ["field"] = new JObject { ["fieldPath"] = "searchKeywords" },
                        ["op"] = "ARRAY_CONTAINS",
                        ["value"] = new JObject { ["stringValue"] = normalizedKeyword }
                    }
                },
                ["limit"] = limit
            }
        };

        return body.ToString(Formatting.None);
    }

    private static string BuildScanUserSearchBody(int limit)
    {
        JObject body = new JObject
        {
            ["structuredQuery"] = new JObject
            {
                ["from"] = new JArray(new JObject { ["collectionId"] = "public_profiles" }),
                ["orderBy"] = new JArray(new JObject
                {
                    ["field"] = new JObject { ["fieldPath"] = "displayNameLower" },
                    ["direction"] = "ASCENDING"
                }),
                ["limit"] = limit
            }
        };

        return body.ToString(Formatting.None);
    }

    private static string PostFirestoreRestJson(string endpoint, string body, string proxyUrl)
    {
        byte[] payload = Encoding.UTF8.GetBytes(body);
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
        request.Method = "POST";
        request.Accept = "application/json";
        request.ContentType = "application/json; charset=utf-8";
        request.Timeout = 5000;
        request.ReadWriteTimeout = 5000;
        request.Proxy = string.IsNullOrEmpty(proxyUrl) ? null : new WebProxy(proxyUrl);
        request.ContentLength = payload.Length;

        using (Stream stream = request.GetRequestStream())
        {
            stream.Write(payload, 0, payload.Length);
        }

        try
        {
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(responseStream ?? Stream.Null))
            {
                return reader.ReadToEnd();
            }
        }
        catch (WebException e)
        {
            string responseText = string.Empty;
            if (e.Response != null)
            {
                using (Stream responseStream = e.Response.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream ?? Stream.Null))
                {
                    responseText = reader.ReadToEnd();
                }
            }

            throw new Exception(string.IsNullOrEmpty(responseText) ? e.Message : responseText);
        }
    }

    private static void AddUserSearchResultsFromRestJson(
        List<UserSearchResult> results,
        string json,
        string currentUid,
        string normalizedKeywordFilter = "")
    {
        if (string.IsNullOrEmpty(json)) return;

        JArray rows = JArray.Parse(json);
        foreach (JToken row in rows)
        {
            JToken document = row["document"];
            if (document == null) continue;

            string documentName = document.Value<string>("name");
            string documentId = ExtractDocumentId(documentName);
            JObject fields = document["fields"] as JObject;
            string uid = GetRestString(fields, "uid", documentId);
            if (string.IsNullOrEmpty(uid)) uid = documentId;
            if (string.IsNullOrEmpty(uid)) continue;
            if (results.Exists(result => result.uid == uid)) continue;

            string displayName = GetRestString(fields, "displayName", "未命名用户");
            string email = GetRestString(fields, "email", string.Empty);
            string displayNameLower = GetRestString(fields, "displayNameLower", displayName);
            string emailLower = GetRestString(fields, "emailLower", email);
            if (!string.IsNullOrEmpty(normalizedKeywordFilter)
                && !MatchesUserSearchKeyword(displayName, displayNameLower, email, emailLower, normalizedKeywordFilter))
            {
                continue;
            }

            results.Add(new UserSearchResult
            {
                uid = uid,
                displayName = displayName,
                email = email,
                photoUrl = GetRestString(fields, "photoUrl", string.Empty),
                birthday = GetRestString(fields, "birthday", string.Empty),
                birthTime = GetRestString(fields, "birthTime", string.Empty),
                city = GetRestString(fields, "city", string.Empty),
                isSelf = uid == currentUid,
            });
        }
    }

    private static string ExtractDocumentId(string documentName)
    {
        if (string.IsNullOrEmpty(documentName)) return string.Empty;
        int index = documentName.LastIndexOf("/", StringComparison.Ordinal);
        return index >= 0 && index + 1 < documentName.Length ? documentName.Substring(index + 1) : documentName;
    }

    private static string GetRestString(JObject fields, string key, string fallback)
    {
        JToken field = fields?[key];
        if (field == null) return fallback;
        return field["stringValue"]?.ToString()
            ?? field["integerValue"]?.ToString()
            ?? field["doubleValue"]?.ToString()
            ?? field["booleanValue"]?.ToString()
            ?? fallback;
    }
#endif

    private static string GetString(Dictionary<string, object> data, string key, string fallback)
    {
        if (data != null && data.TryGetValue(key, out object value) && value != null)
        {
            return value.ToString();
        }

        return fallback;
    }

    private static long GetLong(Dictionary<string, object> data, string key, long fallback)
    {
        if (data == null || !data.TryGetValue(key, out object value) || value == null)
            return fallback;

        try
        {
            if (value is long longValue) return longValue;
            if (value is int intValue) return intValue;
            if (value is double doubleValue) return (long)doubleValue;
            if (value is float floatValue) return (long)floatValue;
            if (long.TryParse(value.ToString(), out long parsed)) return parsed;
        }
        catch
        {
            return fallback;
        }

        return fallback;
    }

    private static long GetUnixMs(Dictionary<string, object> data, string key)
    {
        if (data == null || !data.TryGetValue(key, out object value) || value == null)
            return 0;

        if (value is Timestamp timestamp)
            return new DateTimeOffset(timestamp.ToDateTime().ToUniversalTime()).ToUnixTimeMilliseconds();

        if (value is DateTime dateTime)
            return new DateTimeOffset(dateTime.ToUniversalTime()).ToUnixTimeMilliseconds();

        return GetLong(data, key, 0);
    }

    private static long GetPresenceUnixMs(Dictionary<string, object> data, long fallback = 0)
    {
        long value = GetUnixMs(data, "lastActiveAt");
        if (value <= 0) value = GetLong(data, "lastActiveUnixMs", 0);
        if (value <= 0) value = GetUnixMs(data, "presenceUpdatedAt");
        if (value <= 0) value = GetUnixMs(data, "lastSignInAt");
        if (value <= 0) value = GetLong(data, "lastSignInUnixMs", 0);
        if (value <= 0) value = GetUnixMs(data, "updatedAt");
        return value > 0 ? value : fallback;
    }

    private static bool ResolvePresenceOnline(Dictionary<string, object> data, long lastActiveUnixMs)
    {
        if (data != null && data.ContainsKey("isOnline"))
        {
            bool explicitOnline = GetBool(data, "isOnline", false);
            return explicitOnline && (lastActiveUnixMs <= 0 || IsRecentlyOnline(lastActiveUnixMs));
        }

        return IsRecentlyOnline(lastActiveUnixMs);
    }

    private static bool IsRecentlyOnline(long unixMs)
    {
        if (unixMs <= 0) return false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return now - unixMs <= ONLINE_PRESENCE_TIMEOUT_MS;
    }

    private static void AddUserSearchResults(
        List<UserSearchResult> results,
        IEnumerable<DocumentSnapshot> documents,
        string currentUid)
    {
        foreach (DocumentSnapshot doc in documents)
        {
            if (!doc.Exists) continue;
            if (results.Exists(result => result.uid == doc.Id)) continue;

            Dictionary<string, object> data = doc.ToDictionary();
            results.Add(new UserSearchResult
            {
                uid = doc.Id,
                displayName = GetString(data, "displayName", "未命名用户"),
                email = GetString(data, "email", string.Empty),
                photoUrl = GetString(data, "photoUrl", string.Empty),
                birthday = GetString(data, "birthday", string.Empty),
                birthTime = GetString(data, "birthTime", string.Empty),
                city = GetString(data, "city", string.Empty),
                isSelf = doc.Id == currentUid,
            });
        }
    }

    private static void AddMatchingUserSearchResults(
        List<UserSearchResult> results,
        IEnumerable<DocumentSnapshot> documents,
        string currentUid,
        string normalizedKeyword)
    {
        if (results == null || documents == null || string.IsNullOrEmpty(normalizedKeyword)) return;

        foreach (DocumentSnapshot doc in documents)
        {
            if (!doc.Exists) continue;
            if (results.Exists(result => result.uid == doc.Id)) continue;

            Dictionary<string, object> data = doc.ToDictionary();
            string displayName = GetString(data, "displayName", "未命名用户");
            string displayNameLower = GetString(data, "displayNameLower", displayName);
            string email = GetString(data, "email", string.Empty);
            string emailLower = GetString(data, "emailLower", email);
            if (!MatchesUserSearchKeyword(displayName, displayNameLower, email, emailLower, normalizedKeyword))
                continue;

            results.Add(new UserSearchResult
            {
                uid = doc.Id,
                displayName = displayName,
                email = email,
                photoUrl = GetString(data, "photoUrl", string.Empty),
                birthday = GetString(data, "birthday", string.Empty),
                birthTime = GetString(data, "birthTime", string.Empty),
                city = GetString(data, "city", string.Empty),
                isSelf = doc.Id == currentUid,
            });
        }
    }

    private static bool MatchesUserSearchKeyword(
        string displayName,
        string displayNameLower,
        string email,
        string emailLower,
        string normalizedKeyword)
    {
        if (string.IsNullOrEmpty(normalizedKeyword)) return false;
        if (NormalizeSearchText(displayName).Contains(normalizedKeyword)) return true;
        if (NormalizeSearchText(displayNameLower).Contains(normalizedKeyword)) return true;
        if (NormalizeSearchText(email).Contains(normalizedKeyword)) return true;
        string normalizedEmailLower = NormalizeSearchText(emailLower);
        if (normalizedEmailLower.Contains(normalizedKeyword)) return true;

        int atIndex = normalizedEmailLower.IndexOf('@');
        return atIndex > 0 && normalizedEmailLower.Substring(0, atIndex).Contains(normalizedKeyword);
    }

    private static bool GetBool(Dictionary<string, object> data, string key, bool fallback)
    {
        if (data != null && data.TryGetValue(key, out object value))
        {
            if (value is bool boolValue) return boolValue;
            if (value != null && bool.TryParse(value.ToString(), out bool parsed)) return parsed;
        }

        return fallback;
    }

    private static Dictionary<string, object> SerializeMemorySource(MemorySource source)
    {
        source ??= new MemorySource();
        return new Dictionary<string, object>
        {
            { "stableProfile", SerializeStableProfile(source.stableProfile) },
            { "relationships", SerializeList(source.relationships, SerializeRelationshipMemory) },
            { "readingContinuity", SerializeList(source.readingContinuity, SerializeReadingContinuity) },
            { "candidates", SerializeList(source.candidates, SerializeMemoryCandidate) },
            { "tomorrowHooks", SerializeList(source.tomorrowHooks, SerializeTomorrowHook) },
        };
    }

    private static Dictionary<string, object> SerializeStableProfile(StableProfile profile)
    {
        profile ??= new StableProfile();
        return new Dictionary<string, object>
        {
            { "preferredName", profile.preferredName ?? "" },
            { "preferredTone", profile.preferredTone ?? "" },
            { "recurringThemes", profile.recurringThemes ?? new List<string>() },
            { "doNotSay", profile.doNotSay ?? new List<string>() },
            { "safetyNotes", profile.safetyNotes ?? new List<string>() },
        };
    }

    private static Dictionary<string, object> SerializeRelationshipMemory(RelationshipMemory memory)
    {
        memory ??= new RelationshipMemory();
        return new Dictionary<string, object>
        {
            { "relationshipId", memory.relationshipId ?? "" },
            { "displayName", memory.displayName ?? "" },
            { "entityType", memory.entityType ?? "" },
            { "consentMode", memory.consentMode ?? "" },
            { "knownFacts", memory.knownFacts ?? new List<string>() },
            { "openLoops", memory.openLoops ?? new List<string>() },
            { "lastActionAdvice", memory.lastActionAdvice ?? "" },
            { "lastReadingIds", memory.lastReadingIds ?? new List<string>() },
            { "mentionCount30d", memory.mentionCount30d },
            { "lastTouchedAt", memory.lastTouchedAt ?? "" },
        };
    }

    private static Dictionary<string, object> SerializeReadingContinuity(ReadingContinuityEntry entry)
    {
        entry ??= new ReadingContinuityEntry();
        return new Dictionary<string, object>
        {
            { "readingId", entry.readingId ?? "" },
            { "question", entry.question ?? "" },
            { "shortVerdict", entry.shortVerdict ?? "" },
            { "relationshipId", entry.relationshipId ?? "" },
            { "cards", SerializeList(entry.cards, SerializeReadingCardEntry) },
            { "createdAt", entry.createdAt ?? "" },
        };
    }

    private static Dictionary<string, object> SerializeReadingCardEntry(ReadingCardEntry card)
    {
        card ??= new ReadingCardEntry();
        return new Dictionary<string, object>
        {
            { "position", card.position ?? "" },
            { "positionName", card.positionName ?? "" },
            { "cardId", card.cardId ?? "" },
            { "cardName", card.cardName ?? "" },
            { "orientation", card.orientation ?? "" },
        };
    }

    private static Dictionary<string, object> SerializeMemoryCandidate(MemoryCandidate candidate)
    {
        candidate ??= new MemoryCandidate();
        return new Dictionary<string, object>
        {
            { "id", candidate.id ?? "" },
            { "userId", candidate.userId ?? "" },
            { "type", candidate.type ?? "" },
            { "text", candidate.text ?? "" },
            { "status", candidate.status ?? "" },
            { "confidence", candidate.confidence },
            { "relationshipId", candidate.relationshipId ?? "" },
            { "sourceConversationId", candidate.sourceConversationId ?? "" },
            { "sourceMessageId", candidate.sourceMessageId ?? "" },
            { "createdAt", candidate.createdAt ?? "" },
            { "important", candidate.important },
        };
    }

    private static Dictionary<string, object> SerializeTomorrowHook(TomorrowHook hook)
    {
        hook ??= new TomorrowHook();
        return new Dictionary<string, object>
        {
            { "hookId", hook.hookId ?? "" },
            { "userId", hook.userId ?? "" },
            { "relationshipId", hook.relationshipId ?? "" },
            { "sourceReadingId", hook.sourceReadingId ?? "" },
            { "sourceConversationId", hook.sourceConversationId ?? "" },
            { "hookType", hook.hookType ?? "" },
            { "triggerText", hook.triggerText ?? "" },
            { "scheduledForLocalDate", hook.scheduledForLocalDate ?? "" },
            { "status", hook.status ?? "" },
        };
    }

    private static List<object> SerializeList<T>(List<T> source, Func<T, Dictionary<string, object>> serialize)
    {
        var result = new List<object>();
        if (source == null || serialize == null) return result;
        foreach (var item in source)
            result.Add(serialize(item));
        return result;
    }

    private static MemorySource DeserializeMemorySource(Dictionary<string, object> data)
    {
        var source = new MemorySource();
        if (data == null) return source;

        source.stableProfile = DeserializeStableProfile(GetMap(data, "stableProfile"));
        source.relationships = DeserializeList(data, "relationships", DeserializeRelationshipMemory);
        source.readingContinuity = DeserializeList(data, "readingContinuity", DeserializeReadingContinuity);
        source.candidates = DeserializeList(data, "candidates", DeserializeMemoryCandidate);
        source.tomorrowHooks = DeserializeList(data, "tomorrowHooks", DeserializeTomorrowHook);
        return source;
    }

    private static StableProfile DeserializeStableProfile(Dictionary<string, object> data)
    {
        return new StableProfile
        {
            preferredName = GetString(data, "preferredName", ""),
            preferredTone = GetString(data, "preferredTone", ""),
            recurringThemes = GetStringList(data, "recurringThemes"),
            doNotSay = GetStringList(data, "doNotSay"),
            safetyNotes = GetStringList(data, "safetyNotes"),
        };
    }

    private static RelationshipMemory DeserializeRelationshipMemory(Dictionary<string, object> data)
    {
        return new RelationshipMemory
        {
            relationshipId = GetString(data, "relationshipId", ""),
            displayName = GetString(data, "displayName", ""),
            entityType = GetString(data, "entityType", ""),
            consentMode = GetString(data, "consentMode", ""),
            knownFacts = GetStringList(data, "knownFacts"),
            openLoops = GetStringList(data, "openLoops"),
            lastActionAdvice = GetString(data, "lastActionAdvice", ""),
            lastReadingIds = GetStringList(data, "lastReadingIds"),
            mentionCount30d = GetInt(data, "mentionCount30d", 0),
            lastTouchedAt = GetString(data, "lastTouchedAt", ""),
        };
    }

    private static ReadingContinuityEntry DeserializeReadingContinuity(Dictionary<string, object> data)
    {
        return new ReadingContinuityEntry
        {
            readingId = GetString(data, "readingId", ""),
            question = GetString(data, "question", ""),
            shortVerdict = GetString(data, "shortVerdict", ""),
            relationshipId = GetString(data, "relationshipId", ""),
            cards = DeserializeList(data, "cards", DeserializeReadingCardEntry),
            createdAt = GetString(data, "createdAt", ""),
        };
    }

    private static ReadingCardEntry DeserializeReadingCardEntry(Dictionary<string, object> data)
    {
        return new ReadingCardEntry
        {
            position = GetString(data, "position", ""),
            positionName = GetString(data, "positionName", ""),
            cardId = GetString(data, "cardId", ""),
            cardName = GetString(data, "cardName", ""),
            orientation = GetString(data, "orientation", ""),
        };
    }

    private static MemoryCandidate DeserializeMemoryCandidate(Dictionary<string, object> data)
    {
        return new MemoryCandidate
        {
            id = GetString(data, "id", ""),
            userId = GetString(data, "userId", ""),
            type = GetString(data, "type", ""),
            text = GetString(data, "text", ""),
            status = GetString(data, "status", "pending"),
            confidence = GetFloat(data, "confidence", 0f),
            relationshipId = GetString(data, "relationshipId", ""),
            sourceConversationId = GetString(data, "sourceConversationId", ""),
            sourceMessageId = GetString(data, "sourceMessageId", ""),
            createdAt = GetString(data, "createdAt", ""),
            important = GetBool(data, "important", false),
        };
    }

    private static TomorrowHook DeserializeTomorrowHook(Dictionary<string, object> data)
    {
        return new TomorrowHook
        {
            hookId = GetString(data, "hookId", ""),
            userId = GetString(data, "userId", ""),
            relationshipId = GetString(data, "relationshipId", ""),
            sourceReadingId = GetString(data, "sourceReadingId", ""),
            sourceConversationId = GetString(data, "sourceConversationId", ""),
            hookType = GetString(data, "hookType", ""),
            triggerText = GetString(data, "triggerText", ""),
            scheduledForLocalDate = GetString(data, "scheduledForLocalDate", ""),
            status = GetString(data, "status", ""),
        };
    }

    private static List<T> DeserializeList<T>(
        Dictionary<string, object> data,
        string key,
        Func<Dictionary<string, object>, T> deserialize)
    {
        var result = new List<T>();
        if (data == null || !data.TryGetValue(key, out object value) || deserialize == null)
            return result;

        if (value is IEnumerable<object> values)
        {
            foreach (var item in values)
            {
                var map = ToMap(item);
                if (map != null)
                    result.Add(deserialize(map));
            }
        }

        return result;
    }

    private static Dictionary<string, object> GetMap(Dictionary<string, object> data, string key)
    {
        if (data != null && data.TryGetValue(key, out object value))
            return ToMap(value);
        return null;
    }

    private static Dictionary<string, object> ToMap(object value)
    {
        if (value is Dictionary<string, object> dict) return dict;
        if (value is IDictionary<string, object> idict) return new Dictionary<string, object>(idict);
        return value as Dictionary<string, object>;
    }

    private static List<string> GetStringList(Dictionary<string, object> data, string key)
    {
        var result = new List<string>();
        if (data == null || !data.TryGetValue(key, out object value) || value == null)
            return result;

        if (value is IEnumerable<object> objects)
        {
            foreach (var item in objects)
            {
                if (item != null)
                    result.Add(item.ToString());
            }
        }
        else if (value is IEnumerable<string> strings)
        {
            result.AddRange(strings);
        }

        return result;
    }

    private static int GetInt(Dictionary<string, object> data, string key, int fallback)
    {
        if (data != null && data.TryGetValue(key, out object value) && value != null)
        {
            if (value is int intValue) return intValue;
            if (value is long longValue) return (int)longValue;
            if (int.TryParse(value.ToString(), out int parsed)) return parsed;
        }

        return fallback;
    }

    private static float GetFloat(Dictionary<string, object> data, string key, float fallback)
    {
        if (data != null && data.TryGetValue(key, out object value) && value != null)
        {
            if (value is float floatValue) return floatValue;
            if (value is double doubleValue) return (float)doubleValue;
            if (float.TryParse(value.ToString(), out float parsed)) return parsed;
        }

        return fallback;
    }

    private static PublicAppConfig DeserializePublicAppConfig(Dictionary<string, object> data)
    {
        PublicAppConfig config = PublicAppConfig.Default;
        Dictionary<string, object> socialLinks = GetMap(data, "socialLinks");
        Dictionary<string, object> iapProducts = GetMap(data, "iapProducts");

        config.socialLinks.instagram = GetString(socialLinks, "instagram", config.socialLinks.instagram);
        config.socialLinks.facebook = GetString(socialLinks, "facebook", config.socialLinks.facebook);
        config.socialLinks.x = GetString(socialLinks, "x", config.socialLinks.x);
        config.socialLinks.tiktok = GetString(socialLinks, "tiktok", config.socialLinks.tiktok);
        config.socialLinks.pinterest = GetString(socialLinks, "pinterest", config.socialLinks.pinterest);

        config.iapProducts.proMonthly = DeserializeIapProduct(
            GetMap(iapProducts, "proMonthly"),
            config.iapProducts.proMonthly);
        config.iapProducts.proYearly = DeserializeIapProduct(
            GetMap(iapProducts, "proYearly"),
            config.iapProducts.proYearly);

        return NormalizePublicAppConfig(config);
    }

    private static IapProductConfig DeserializeIapProduct(Dictionary<string, object> data, IapProductConfig fallback)
    {
        fallback ??= new IapProductConfig();
        return new IapProductConfig
        {
            productId = GetString(data, "productId", fallback.productId),
            type = GetString(data, "type", fallback.type),
            store = GetString(data, "store", fallback.store),
            displayName = GetString(data, "displayName", fallback.displayName),
            priceLabel = GetString(data, "priceLabel", fallback.priceLabel),
        };
    }

    private static PublicAppConfig NormalizePublicAppConfig(PublicAppConfig config)
    {
        if (config == null)
            return PublicAppConfig.Default;

        config.iapProducts ??= IapProductsConfig.Default;
        config.iapProducts.proMonthly = NormalizeIapProduct(
            config.iapProducts.proMonthly,
            IapProductConfig.MonthlyDefault,
            LEGACY_IAP_MONTHLY_PRODUCT_ID);
        config.iapProducts.proYearly = NormalizeIapProduct(
            config.iapProducts.proYearly,
            IapProductConfig.YearlyDefault,
            LEGACY_IAP_YEARLY_PRODUCT_ID);
        return config;
    }

    private static IapProductConfig NormalizeIapProduct(
        IapProductConfig product,
        IapProductConfig fallback,
        string legacyProductId)
    {
        product ??= fallback;
        if (string.IsNullOrWhiteSpace(product.productId) || product.productId == legacyProductId)
            product.productId = fallback.productId;
        if (string.IsNullOrWhiteSpace(product.type))
            product.type = fallback.type;
        if (string.IsNullOrWhiteSpace(product.store))
            product.store = fallback.store;
        if (string.IsNullOrWhiteSpace(product.displayName))
            product.displayName = fallback.displayName;
        return product;
    }

    private static PublicAppConfig LoadCachedPublicAppConfig()
    {
        string json = PlayerPrefs.GetString(PUBLIC_APP_CONFIG_CACHE_KEY, string.Empty);
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            return NormalizePublicAppConfig(JsonUtility.FromJson<PublicAppConfig>(json));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FirestoreManager] 公开配置缓存读取失败: " + e.Message);
            return null;
        }
    }

    private static void SavePublicAppConfigCache(PublicAppConfig config)
    {
        if (config == null) return;

        try
        {
            PlayerPrefs.SetString(PUBLIC_APP_CONFIG_CACHE_KEY, JsonUtility.ToJson(config));
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FirestoreManager] 公开配置缓存保存失败: " + e.Message);
        }
    }

    private void MirrorFeedbackForBackend(string feedbackId, Dictionary<string, object> data)
    {
        if (string.IsNullOrEmpty(feedbackId) || data == null || _db == null) return;

        _db.Collection("feedback")
            .Document(feedbackId)
            .SetAsync(data, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[FirestoreManager] 反馈后台镜像保存失败: {task.Exception?.InnerException?.Message}");
                }
            });
    }

    private void DeleteKnownUserCollections(string uid, Action<bool> onComplete)
    {
        string[] collectionNames =
        {
            "daily_oracles",
            "divination_records",
            "dialog_sessions",
            "memories",
            "tomorrow_hooks",
            "friends",
            "friend_requests",
            "blocked_users",
            "virtual_friends",
            "feedback",
            "settings",
        };

        var userRef = _db.Collection("users").Document(uid);
        DeleteKnownUserCollectionsAsync(userRef, collectionNames).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 删除用户子集合失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            onComplete?.Invoke(true);
        });
    }

    private async Task DeleteKnownUserCollectionsAsync(DocumentReference userRef, string[] collectionNames)
    {
        foreach (string collectionName in collectionNames)
            await DeleteQueryInBatchesAsync(userRef.Collection(collectionName));
    }

    private void DeleteTopLevelUserReferences(string uid, Action<bool> onComplete)
    {
        DeleteTopLevelUserReferencesAsync(uid).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[FirestoreManager] 删除顶层用户关联记录失败: {task.Exception?.InnerException?.Message}");
                onComplete?.Invoke(false);
                return;
            }

            onComplete?.Invoke(true);
        });
    }

    private async Task DeleteTopLevelUserReferencesAsync(string uid)
    {
        await DeleteQueryInBatchesAsync(_db.Collection("daily_oracle_summaries").WhereEqualTo("ownerUid", uid));
        await DeleteQueryInBatchesAsync(_db.Collection("relationship_divinations").WhereEqualTo("initiatorUid", uid));
        await DeleteQueryInBatchesAsync(_db.Collection("relationship_divinations").WhereEqualTo("receiverUid", uid));
        await DeleteQueryInBatchesAsync(_db.Collection("feedback").WhereEqualTo("uid", uid));
        await DeleteQueryInBatchesAsync(_db.Collection("iap_receipts").WhereEqualTo("uid", uid));
        await DeleteQueryInBatchesAsync(_db.Collection("usage_limits").WhereEqualTo("uid", uid));
        await DeleteQueryInBatchesAsync(_db.Collection("payment_events").WhereEqualTo("uid", uid));
    }

    private async Task DeleteQueryInBatchesAsync(Query query)
    {
        while (true)
        {
            QuerySnapshot snapshot = await query.Limit(FIRESTORE_DELETE_BATCH_LIMIT).GetSnapshotAsync();
            WriteBatch batch = _db.StartBatch();
            int deleteCount = 0;

            foreach (var doc in snapshot.Documents)
            {
                batch.Delete(doc.Reference);
                deleteCount++;
            }

            if (deleteCount == 0)
                return;

            await batch.CommitAsync();
            if (deleteCount < FIRESTORE_DELETE_BATCH_LIMIT)
                return;
        }
    }

    private static string GetFriendStatusText(string status)
    {
        return status switch
        {
            "pendingSent" => "好友请求已发送",
            "pendingReceived" => "等待你确认好友请求",
            "friend" => "真实好友",
            _ => "真实好友"
        };
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

public class CloudNotificationSettings
{
    public bool dailyOracleEnabled;
    public bool dialogueReplyEnabled;
    public bool divinationReturnEnabled;
    public bool friendInteractionEnabled;
    public bool activitySystemEnabled;
    public string reminderTime;
}

public class CloudDailyDivinationSyncSettings
{
    public bool enabled;
    public string visibility;
    public bool summaryOnly;
}

public class CloudFeedbackEntry
{
    public string feedbackId;
    public string category;
    public string tag;
    public string content;
    public string source;
    public string status;
    public string createdAt;
}

[Serializable]
public class PublicAppConfig
{
    public SocialLinksConfig socialLinks = SocialLinksConfig.Default;
    public IapProductsConfig iapProducts = IapProductsConfig.Default;

    public static PublicAppConfig Default => new PublicAppConfig
    {
        socialLinks = SocialLinksConfig.Default,
        iapProducts = IapProductsConfig.Default,
    };
}

[Serializable]
public class SocialLinksConfig
{
    public string instagram;
    public string facebook;
    public string x;
    public string tiktok;
    public string pinterest;

    public static SocialLinksConfig Default => new SocialLinksConfig
    {
        instagram = "https://www.instagram.com/",
        facebook = "https://www.facebook.com/",
        x = "https://x.com/",
        tiktok = "https://www.tiktok.com/",
        pinterest = "https://www.pinterest.com/",
    };
}

[Serializable]
public class IapProductsConfig
{
    public IapProductConfig proMonthly = IapProductConfig.MonthlyDefault;
    public IapProductConfig proYearly = IapProductConfig.YearlyDefault;

    public static IapProductsConfig Default => new IapProductsConfig
    {
        proMonthly = IapProductConfig.MonthlyDefault,
        proYearly = IapProductConfig.YearlyDefault,
    };
}

[Serializable]
public class IapProductConfig
{
    public string productId;
    public string type;
    public string store;
    public string displayName;
    public string priceLabel;

    public static IapProductConfig MonthlyDefault => new IapProductConfig
    {
        productId = "fari.pro.monthly",
        type = "subscription",
        store = "app_store_google_play",
        displayName = "Fari Pro 月度会员",
        priceLabel = string.Empty,
    };

    public static IapProductConfig YearlyDefault => new IapProductConfig
    {
        productId = "fari.pro.yearly",
        type = "subscription",
        store = "app_store_google_play",
        displayName = "Fari Pro 年度会员",
        priceLabel = string.Empty,
    };
}
