using System;
using UnityEngine;
using XFGameFrameWork;

/// <summary>
/// 头像类型
/// </summary>
public enum AvatarType
{
    Moon,   // 月亮头像
    Person  // 人物头像
}

/// <summary>
/// 登录类型
/// </summary>
public enum LoginType
{
    Email,      // 邮箱登录
    Phone,      // 手机登录
    Guest,      // 游客登录
    ThirdParty, // 第三方登录
    Google,     // Google 登录
    Apple,      // Apple 登录
    Facebook,   // Facebook 登录
    GameCenter  // Game Center 登录
}

/// <summary>
/// 账号状态
/// </summary>
public enum AccountStatus
{
    Normal,     // 正常
    Frozen,     // 冻结
    Banned,     // 封禁
    Pending     // 待验证
}

/// <summary>
/// 用户数据管理器
/// 负责用户个人资料数据的内存管理和本地持久化
/// 支持 Email / Google / Apple / Facebook / Game Center / Guest 多种登录类型
/// </summary>
public class UserDataManager : MonoSingleton<UserDataManager>
{
    #region 数据字段

    /// <summary>用户名</summary>
    public string UserName { get; private set; } = string.Empty;

    /// <summary>生日（格式：YYYY-MM-DD）</summary>
    public string Birthday { get; private set; } = string.Empty;

    /// <summary>出生时间（格式：HH:MM）</summary>
    public string BirthTime { get; private set; } = string.Empty;

    /// <summary>出生城市</summary>
    public string City { get; private set; } = string.Empty;

    /// <summary>当前头像类型</summary>
    public AvatarType CurrentAvatar { get; private set; } = AvatarType.Moon;

    // ========== 账户信息字段 ==========

    /// <summary>用户邮箱</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>用户ID</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>注册时间（ISO 8601 格式）</summary>
    public string RegTime { get; private set; } = string.Empty;

    /// <summary>登录类型</summary>
    public LoginType CurrentLoginType { get; private set; } = LoginType.Email;

    /// <summary>账号状态</summary>
    public AccountStatus Status { get; private set; } = AccountStatus.Normal;

    // ========== Firebase Auth 扩展字段 ==========

    /// <summary>头像 URL（Google 头像 / Apple 头像等）</summary>
    public string PhotoUrl { get; private set; } = string.Empty;

    /// <summary>Firebase Storage 中的用户上传头像路径</summary>
    public string AvatarStoragePath { get; private set; } = string.Empty;

    /// <summary>Firebase UID</summary>
    public string FirebaseUid { get; private set; } = string.Empty;

    /// <summary>认证提供商 ID（如 google.com, apple.com, facebook.com）</summary>
    public string AuthProviderId { get; private set; } = string.Empty;

    /// <summary>Facebook 提供方用户 ID，用于好友发现映射</summary>
    public string FacebookProviderUserId { get; private set; } = string.Empty;

    /// <summary>是否已通过 Firebase 认证</summary>
    public bool IsFirebaseAuthenticated { get; private set; } = false;

    /// <summary>邮箱是否已验证（Google/Apple 登录时一般为 true）</summary>
    public bool IsEmailVerified { get; private set; } = false;

    /// <summary>账号创建时间戳（毫秒，来自 Firebase Metadata）</summary>
    public long CreationTimestamp { get; private set; } = 0;

    /// <summary>上次登录时间戳（毫秒，来自 Firebase Metadata）</summary>
    public long LastSignInTimestamp { get; private set; } = 0;

    #endregion

    #region 便捷属性

    /// <summary>是否是游客（匿名）</summary>
    public bool IsGuest => CurrentLoginType == LoginType.Guest;

    /// <summary>是否是第三方登录（Google/Apple/Facebook）</summary>
    public bool IsThirdPartyLogin =>
        CurrentLoginType == LoginType.Google ||
        CurrentLoginType == LoginType.Apple ||
        CurrentLoginType == LoginType.Facebook ||
        CurrentLoginType == LoginType.GameCenter ||
        CurrentLoginType == LoginType.ThirdParty;

    /// <summary>是否有头像 URL</summary>
    public bool HasPhotoUrl => !string.IsNullOrEmpty(PhotoUrl);

    /// <summary>账号创建时间（本地时间）</summary>
    public DateTime CreationTime => CreationTimestamp > 0
        ? DateTimeOffset.FromUnixTimeMilliseconds(CreationTimestamp).LocalDateTime
        : DateTime.MinValue;

    /// <summary>上次登录时间（本地时间）</summary>
    public DateTime LastSignInTime => LastSignInTimestamp > 0
        ? DateTimeOffset.FromUnixTimeMilliseconds(LastSignInTimestamp).LocalDateTime
        : DateTime.MinValue;

    #endregion

    #region 常量

    private const string KEY_USER_NAME        = "UserData_Name";
    private const string KEY_BIRTHDAY         = "UserData_Birthday";
    private const string KEY_BIRTH_TIME       = "UserData_BirthTime";
    private const string KEY_CITY             = "UserData_City";
    private const string KEY_AVATAR_TYPE      = "UserData_AvatarType";

    // 账户信息 Key
    private const string KEY_EMAIL            = "UserData_Email";
    private const string KEY_USER_ID          = "UserData_UserId";
    private const string KEY_REG_TIME         = "UserData_RegTime";
    private const string KEY_LOGIN_TYPE       = "UserData_LoginType";
    private const string KEY_STATUS           = "UserData_Status";

    // Firebase Auth 扩展 Key
    private const string KEY_PHOTO_URL           = "UserData_PhotoUrl";
    private const string KEY_AVATAR_STORAGE_PATH = "UserData_AvatarStoragePath";
    private const string KEY_FIREBASE_UID        = "UserData_FirebaseUid";
    private const string KEY_AUTH_PROVIDER_ID    = "UserData_AuthProviderId";
    private const string KEY_FACEBOOK_PROVIDER_UID = "UserData_FacebookProviderUid";
    private const string KEY_IS_FIREBASE_AUTH    = "UserData_IsFirebaseAuth";
    private const string KEY_IS_EMAIL_VERIFIED   = "UserData_IsEmailVerified";
    private const string KEY_CREATION_TIMESTAMP  = "UserData_CreationTimestamp";
    private const string KEY_LAST_SIGNIN_TS      = "UserData_LastSignInTimestamp";

    #endregion

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();
        LoadData();
    }

    #endregion

    #region 数据操作

    /// <summary>设置用户名</summary>
    public void SetUserName(string name)
    {
        UserName = name ?? string.Empty;
    }

    /// <summary>设置生日</summary>
    public void SetBirthday(string birthday)
    {
        Birthday = birthday ?? string.Empty;
    }

    /// <summary>设置出生时间</summary>
    public void SetBirthTime(string time)
    {
        BirthTime = time ?? string.Empty;
    }

    /// <summary>设置出生城市</summary>
    public void SetCity(string city)
    {
        City = city ?? string.Empty;
    }

    /// <summary>设置头像类型</summary>
    public void SetAvatarType(AvatarType type)
    {
        CurrentAvatar = type;
    }

    /// <summary>切换头像类型</summary>
    public void ToggleAvatar()
    {
        CurrentAvatar = CurrentAvatar == AvatarType.Moon ? AvatarType.Person : AvatarType.Moon;
    }

    // ========== 账户信息操作方法 ==========

    /// <summary>设置邮箱</summary>
    public void SetEmail(string email)
    {
        Email = email ?? string.Empty;
    }

    /// <summary>设置用户ID</summary>
    public void SetUserId(string userId)
    {
        UserId = userId ?? string.Empty;
    }

    /// <summary>设置注册时间</summary>
    public void SetRegTime(string regTime)
    {
        RegTime = regTime ?? string.Empty;
    }

    /// <summary>设置登录类型</summary>
    public void SetLoginType(LoginType loginType)
    {
        CurrentLoginType = loginType;
    }

    /// <summary>设置账号状态</summary>
    public void SetStatus(AccountStatus status)
    {
        Status = status;
    }

    // ========== Firebase Auth 扩展操作方法 ==========

    /// <summary>设置头像 URL</summary>
    public void SetPhotoUrl(string photoUrl)
    {
        PhotoUrl = photoUrl ?? string.Empty;
    }

    /// <summary>设置 Firebase Storage 头像路径</summary>
    public void SetAvatarStoragePath(string avatarStoragePath)
    {
        AvatarStoragePath = avatarStoragePath ?? string.Empty;
    }

    /// <summary>设置 Firebase UID</summary>
    public void SetFirebaseUid(string firebaseUid)
    {
        FirebaseUid = firebaseUid ?? string.Empty;
    }

    /// <summary>设置认证提供商 ID</summary>
    public void SetAuthProviderId(string providerId)
    {
        AuthProviderId = providerId ?? string.Empty;
    }

    /// <summary>设置 Facebook 提供方用户 ID</summary>
    public void SetFacebookProviderUserId(string providerUserId)
    {
        FacebookProviderUserId = providerUserId ?? string.Empty;
    }

    /// <summary>设置是否已通过 Firebase 认证</summary>
    public void SetFirebaseAuthenticated(bool authenticated)
    {
        IsFirebaseAuthenticated = authenticated;
    }

    /// <summary>设置邮箱验证状态</summary>
    public void SetEmailVerified(bool verified)
    {
        IsEmailVerified = verified;
    }

    /// <summary>设置账号创建时间戳（毫秒）</summary>
    public void SetCreationTimestamp(long timestamp)
    {
        CreationTimestamp = timestamp;
    }

    /// <summary>设置上次登录时间戳（毫秒）</summary>
    public void SetLastSignInTimestamp(long timestamp)
    {
        LastSignInTimestamp = timestamp;
    }

    #endregion

    #region 持久化

    /// <summary>
    /// 保存所有数据到本地
    /// </summary>
    public void SaveData()
    {
        // 基础信息
        PlayerPrefs.SetString(KEY_USER_NAME,   UserName);
        PlayerPrefs.SetString(KEY_BIRTHDAY,    Birthday);
        PlayerPrefs.SetString(KEY_BIRTH_TIME,  BirthTime);
        PlayerPrefs.SetString(KEY_CITY,        City);
        PlayerPrefs.SetInt(KEY_AVATAR_TYPE,    (int)CurrentAvatar);

        // 账户信息
        PlayerPrefs.SetString(KEY_EMAIL,       Email);
        PlayerPrefs.SetString(KEY_USER_ID,     UserId);
        PlayerPrefs.SetString(KEY_REG_TIME,    RegTime);
        PlayerPrefs.SetInt(KEY_LOGIN_TYPE,     (int)CurrentLoginType);
        PlayerPrefs.SetInt(KEY_STATUS,         (int)Status);

        // Firebase Auth 扩展
        PlayerPrefs.SetString(KEY_PHOTO_URL,          PhotoUrl);
        PlayerPrefs.SetString(KEY_AVATAR_STORAGE_PATH, AvatarStoragePath);
        PlayerPrefs.SetString(KEY_FIREBASE_UID,       FirebaseUid);
        PlayerPrefs.SetString(KEY_AUTH_PROVIDER_ID,   AuthProviderId);
        PlayerPrefs.SetString(KEY_FACEBOOK_PROVIDER_UID, FacebookProviderUserId);
        PlayerPrefs.SetInt(KEY_IS_FIREBASE_AUTH,      IsFirebaseAuthenticated ? 1 : 0);
        PlayerPrefs.SetInt(KEY_IS_EMAIL_VERIFIED,     IsEmailVerified ? 1 : 0);
        PlayerPrefs.SetString(KEY_CREATION_TIMESTAMP, CreationTimestamp.ToString());
        PlayerPrefs.SetString(KEY_LAST_SIGNIN_TS,     LastSignInTimestamp.ToString());

        PlayerPrefs.Save();

        Debug.Log("[UserDataManager] 用户数据已保存");
    }

    /// <summary>
    /// 从本地加载数据
    /// </summary>
    public void LoadData()
    {
        // 基础信息
        UserName      = PlayerPrefs.GetString(KEY_USER_NAME,  string.Empty);
        Birthday      = PlayerPrefs.GetString(KEY_BIRTHDAY,   string.Empty);
        BirthTime     = PlayerPrefs.GetString(KEY_BIRTH_TIME, string.Empty);
        City          = PlayerPrefs.GetString(KEY_CITY,       string.Empty);
        CurrentAvatar = (AvatarType)PlayerPrefs.GetInt(KEY_AVATAR_TYPE, (int)AvatarType.Moon);

        // 账户信息
        Email             = PlayerPrefs.GetString(KEY_EMAIL,     string.Empty);
        UserId            = PlayerPrefs.GetString(KEY_USER_ID,   string.Empty);
        RegTime           = PlayerPrefs.GetString(KEY_REG_TIME,  string.Empty);
        CurrentLoginType  = (LoginType)PlayerPrefs.GetInt(KEY_LOGIN_TYPE, (int)LoginType.Email);
        Status            = (AccountStatus)PlayerPrefs.GetInt(KEY_STATUS, (int)AccountStatus.Normal);

        // Firebase Auth 扩展
        PhotoUrl             = PlayerPrefs.GetString(KEY_PHOTO_URL,        string.Empty);
        AvatarStoragePath    = PlayerPrefs.GetString(KEY_AVATAR_STORAGE_PATH, string.Empty);
        FirebaseUid          = PlayerPrefs.GetString(KEY_FIREBASE_UID,     string.Empty);
        AuthProviderId       = PlayerPrefs.GetString(KEY_AUTH_PROVIDER_ID, string.Empty);
        FacebookProviderUserId = PlayerPrefs.GetString(KEY_FACEBOOK_PROVIDER_UID, string.Empty);
        IsFirebaseAuthenticated = PlayerPrefs.GetInt(KEY_IS_FIREBASE_AUTH, 0) == 1;
        IsEmailVerified      = PlayerPrefs.GetInt(KEY_IS_EMAIL_VERIFIED, 0) == 1;

        string creationStr  = PlayerPrefs.GetString(KEY_CREATION_TIMESTAMP, "0");
        string lastSignStr  = PlayerPrefs.GetString(KEY_LAST_SIGNIN_TS,     "0");
        long.TryParse(creationStr, out long creation);
        long.TryParse(lastSignStr, out long lastSign);
        CreationTimestamp    = creation;
        LastSignInTimestamp  = lastSign;

        Debug.Log("[UserDataManager] 用户数据已加载");
    }

    /// <summary>
    /// 清除所有用户数据（包括本地缓存）
    /// </summary>
    public void ClearData()
    {
        UserName      = string.Empty;
        Birthday      = string.Empty;
        BirthTime     = string.Empty;
        City          = string.Empty;
        CurrentAvatar = AvatarType.Moon;

        Email            = string.Empty;
        UserId           = string.Empty;
        RegTime          = string.Empty;
        CurrentLoginType = LoginType.Email;
        Status           = AccountStatus.Normal;

        PhotoUrl             = string.Empty;
        AvatarStoragePath    = string.Empty;
        FirebaseUid          = string.Empty;
        AuthProviderId       = string.Empty;
        FacebookProviderUserId = string.Empty;
        IsFirebaseAuthenticated = false;
        IsEmailVerified      = false;
        CreationTimestamp    = 0;
        LastSignInTimestamp  = 0;

        PlayerPrefs.DeleteKey(KEY_USER_NAME);
        PlayerPrefs.DeleteKey(KEY_BIRTHDAY);
        PlayerPrefs.DeleteKey(KEY_BIRTH_TIME);
        PlayerPrefs.DeleteKey(KEY_CITY);
        PlayerPrefs.DeleteKey(KEY_AVATAR_TYPE);
        PlayerPrefs.DeleteKey(KEY_EMAIL);
        PlayerPrefs.DeleteKey(KEY_USER_ID);
        PlayerPrefs.DeleteKey(KEY_REG_TIME);
        PlayerPrefs.DeleteKey(KEY_LOGIN_TYPE);
        PlayerPrefs.DeleteKey(KEY_STATUS);
        PlayerPrefs.DeleteKey(KEY_PHOTO_URL);
        PlayerPrefs.DeleteKey(KEY_AVATAR_STORAGE_PATH);
        PlayerPrefs.DeleteKey(KEY_FIREBASE_UID);
        PlayerPrefs.DeleteKey(KEY_AUTH_PROVIDER_ID);
        PlayerPrefs.DeleteKey(KEY_FACEBOOK_PROVIDER_UID);
        PlayerPrefs.DeleteKey(KEY_IS_FIREBASE_AUTH);
        PlayerPrefs.DeleteKey(KEY_IS_EMAIL_VERIFIED);
        PlayerPrefs.DeleteKey(KEY_CREATION_TIMESTAMP);
        PlayerPrefs.DeleteKey(KEY_LAST_SIGNIN_TS);
        PlayerPrefs.Save();

        Debug.Log("[UserDataManager] 用户数据已清除");
    }

    #endregion

    #region 数据验证

    /// <summary>
    /// 检查用户资料是否已填写完整（用于引导用户完善信息）
    /// </summary>
    public bool IsProfileComplete()
    {
        return !string.IsNullOrWhiteSpace(UserName)
            && !string.IsNullOrWhiteSpace(Birthday)
            && !string.IsNullOrWhiteSpace(BirthTime)
            && !string.IsNullOrWhiteSpace(City);
    }

    /// <summary>
    /// 是否已登录（有 UserId 即视为登录状态）
    /// </summary>
    public bool IsLoggedIn()
    {
        return !string.IsNullOrEmpty(UserId);
    }

    #endregion

    #region 便捷方法

    /// <summary>
    /// 获取登录类型的显示文本
    /// </summary>
    public string GetLoginTypeDisplayText()
    {
        return CurrentLoginType switch
        {
            LoginType.Email      => "邮箱登录",
            LoginType.Phone      => "手机登录",
            LoginType.Guest      => "游客登录",
            LoginType.ThirdParty => "第三方登录",
            LoginType.Google     => "Google 登录",
            LoginType.Apple      => "Apple 登录",
            LoginType.Facebook   => "Facebook 登录",
            LoginType.GameCenter => "Game Center 登录",
            _                    => "未知登录"
        };
    }

    /// <summary>
    /// 获取账号状态的显示文本
    /// </summary>
    public string GetStatusDisplayText()
    {
        return Status switch
        {
            AccountStatus.Normal  => "正常",
            AccountStatus.Frozen  => "冻结",
            AccountStatus.Banned  => "封禁",
            AccountStatus.Pending => "待验证",
            _                     => "未知状态"
        };
    }

    /// <summary>
    /// 格式化注册时间显示
    /// </summary>
    public string GetFormattedRegTime()
    {
        if (string.IsNullOrWhiteSpace(RegTime))
            return "未注册";

        if (RegTime.Contains("T") && DateTime.TryParse(RegTime, out DateTime dt))
            return dt.ToString("yyyy-MM-dd HH:mm:ss");

        return RegTime;
    }

    /// <summary>
    /// 格式化账号创建时间显示
    /// </summary>
    public string GetFormattedCreationTime()
    {
        return CreationTimestamp > 0
            ? CreationTime.ToString("yyyy-MM-dd HH:mm:ss")
            : "未知";
    }

    /// <summary>
    /// 格式化上次登录时间显示
    /// </summary>
    public string GetFormattedLastSignInTime()
    {
        return LastSignInTimestamp > 0
            ? LastSignInTime.ToString("yyyy-MM-dd HH:mm:ss")
            : "未知";
    }

    /// <summary>
    /// 初始化注册时间（首次注册时调用）
    /// </summary>
    public void InitRegTime()
    {
        if (string.IsNullOrWhiteSpace(RegTime))
        {
            RegTime = DateTime.UtcNow.ToString("O"); // ISO 8601 格式
            SaveData();
        }
    }

    /// <summary>
    /// 生成默认用户ID（Email 登录 / 游客登录时使用，第三方登录请用 Firebase UID）
    /// </summary>
    public void GenerateDefaultUserId()
    {
        // 第三方登录已有 Firebase UID，不需要生成
        if (IsThirdPartyLogin) return;

        if (string.IsNullOrWhiteSpace(UserId))
        {
            string prefix = string.IsNullOrWhiteSpace(Email) ? "user" : Email.Split('@')[0];
            UserId = $"{prefix}_{SystemInfo.deviceUniqueIdentifier.GetHashCode():x8}";
            SaveData();
        }
    }

    /// <summary>
    /// 退出登录：清除账户相关数据，保留用户填写的基础资料（生日/城市等）
    /// 注意：Firebase 登出由 FirebaseAuthManager 统一发起，此处不重复调用
    /// </summary>
    public void Logout()
    {
        Email            = string.Empty;
        UserId           = string.Empty;
        RegTime          = string.Empty;
        CurrentLoginType = LoginType.Email;
        Status           = AccountStatus.Normal;

        PhotoUrl             = string.Empty;
        AvatarStoragePath    = string.Empty;
        FirebaseUid          = string.Empty;
        AuthProviderId       = string.Empty;
        FacebookProviderUserId = string.Empty;
        IsFirebaseAuthenticated = false;
        IsEmailVerified      = false;
        CreationTimestamp    = 0;
        LastSignInTimestamp  = 0;

        PlayerPrefs.DeleteKey(KEY_EMAIL);
        PlayerPrefs.DeleteKey(KEY_USER_ID);
        PlayerPrefs.DeleteKey(KEY_REG_TIME);
        PlayerPrefs.DeleteKey(KEY_LOGIN_TYPE);
        PlayerPrefs.DeleteKey(KEY_STATUS);
        PlayerPrefs.DeleteKey(KEY_PHOTO_URL);
        PlayerPrefs.DeleteKey(KEY_AVATAR_STORAGE_PATH);
        PlayerPrefs.DeleteKey(KEY_FIREBASE_UID);
        PlayerPrefs.DeleteKey(KEY_AUTH_PROVIDER_ID);
        PlayerPrefs.DeleteKey(KEY_FACEBOOK_PROVIDER_UID);
        PlayerPrefs.DeleteKey(KEY_IS_FIREBASE_AUTH);
        PlayerPrefs.DeleteKey(KEY_IS_EMAIL_VERIFIED);
        PlayerPrefs.DeleteKey(KEY_CREATION_TIMESTAMP);
        PlayerPrefs.DeleteKey(KEY_LAST_SIGNIN_TS);
        PlayerPrefs.Save();

        // 清理本地头像缓存（所有 provider）
        GoogleUserInfoHelper.ClearLocalAvatarCache();
        AppleUserInfoHelper.ClearLocalAvatarCache();
        FacebookUserInfoHelper.ClearLocalAvatarCache();

        Debug.Log("[UserDataManager] 已退出登录");
    }

    #endregion

    #region 数据同步

    /// <summary>
    /// 从 UserInfo（通用结构）同步 Firebase 数据到本地
    /// 由 FirebaseAuthManager.SyncUserToDataManager 在登录成功后调用
    /// </summary>
    public void SyncFromFirebaseUser(UserInfo userInfo, AuthProvider authProvider)
    {
        if (userInfo == null) return;

        // Firebase UID
        SetFirebaseUid(userInfo.uid);
        // 邮箱
        if (!string.IsNullOrEmpty(userInfo.email))
            SetEmail(userInfo.email);
        // 用户名（不覆盖用户已填写的本地名称）
        if (!string.IsNullOrEmpty(userInfo.displayName) && string.IsNullOrWhiteSpace(UserName))
            SetUserName(userInfo.displayName);
        // 头像 URL
        if (!string.IsNullOrEmpty(userInfo.photoUrl))
            SetPhotoUrl(userInfo.photoUrl);
        // 认证提供商
        SetAuthProviderId(userInfo.providerId);
        SetFirebaseAuthenticated(true);
        // 匿名字段不单独存，用 LoginType 判断

        // 登录类型
        LoginType loginType = authProvider switch
        {
            AuthProvider.Google    => LoginType.Google,
            AuthProvider.Apple     => LoginType.Apple,
            AuthProvider.Facebook  => LoginType.Facebook,
            AuthProvider.GameCenter => LoginType.GameCenter,
            AuthProvider.Email     => LoginType.Email,
            AuthProvider.Anonymous => LoginType.Guest,
            _                      => LoginType.ThirdParty
        };
        SetLoginType(loginType);

        // UserId 统一用 Firebase UID
        SetUserId(userInfo.uid);

        // 初始化注册时间（首次登录写入，之后不再覆盖）
        InitRegTime();

        SaveData();

            Debug.Log($"[UserDataManager] Firebase 用户数据已同步: UID={userInfo.uid}, " +
                  $"Name={userInfo.displayName}, Provider={authProvider}");
    }

    /// <summary>
    /// 从 GoogleUserInfo 同步 Google 登录专属数据到本地
    /// 在 Google 登录成功后调用，可获取到更完整的字段（时间戳、邮箱验证状态等）
    /// 调用此方法前请先调用 SyncFromFirebaseUser 或确保 LoginType 已设置为 Google
    /// </summary>
    public void SyncFromGoogleUserInfo(GoogleUserInfo googleInfo)
    {
        if (googleInfo == null) return;
        Debug.Log($"google用户登录信息:{googleInfo.ToString()}");
        // UID（确保一致）
        if (!string.IsNullOrEmpty(googleInfo.firebaseUid))
        {
            SetFirebaseUid(googleInfo.firebaseUid);
            SetUserId(googleInfo.firebaseUid);
        }

        // 邮箱
        if (!string.IsNullOrEmpty(googleInfo.email))
            SetEmail(googleInfo.email);

        // 用户名（不覆盖已填写的本地名）
        if (!string.IsNullOrEmpty(googleInfo.displayName) && string.IsNullOrWhiteSpace(UserName))
            SetUserName(googleInfo.displayName);

        // 头像 URL
        if (!string.IsNullOrEmpty(googleInfo.photoUrl))
            SetPhotoUrl(googleInfo.photoUrl);

        // 提供商
        if (!string.IsNullOrEmpty(googleInfo.providerId))
            SetAuthProviderId(googleInfo.providerId);

        // Google 特有字段
        SetEmailVerified(googleInfo.isEmailVerified);
        SetCreationTimestamp(googleInfo.creationTimestamp);
        SetLastSignInTimestamp(googleInfo.lastSignInTimestamp);

        SetFirebaseAuthenticated(true);
        SetLoginType(LoginType.Google);

        // 注册时间：优先用 Firebase 创建时间戳转换
        if (string.IsNullOrWhiteSpace(RegTime) && googleInfo.creationTimestamp > 0)
        {
            RegTime = googleInfo.CreationTime.ToUniversalTime().ToString("O");
        }

        SaveData();

        Debug.Log($"[UserDataManager] Google 用户数据已同步: UID={googleInfo.firebaseUid}, " +
                  $"Email={googleInfo.email}, EmailVerified={googleInfo.isEmailVerified}, " +
                  $"Created={googleInfo.CreationTime:yyyy-MM-dd}");
    }

    /// <summary>
    /// 从 AppleUserInfo 同步 Apple 登录专属数据到本地
    /// 在 Apple 登录成功后调用，可获取时间戳、邮箱验证状态等完整字段
    /// 注意：Apple 后续登录时 displayName/email 可能为空（Firebase Auth 正常行为）
    /// </summary>
    public void SyncFromAppleUserInfo(AppleUserInfo appleInfo)
    {
        if (appleInfo == null) return;
        Debug.Log($"apple用户登录信息:{appleInfo.ToString()}");

        // UID
        if (!string.IsNullOrEmpty(appleInfo.firebaseUid))
        {
            SetFirebaseUid(appleInfo.firebaseUid);
            SetUserId(appleInfo.firebaseUid);
        }

        // 邮箱（Apple 首次登录有，后续可能为空，不覆盖已存的值）
        if (!string.IsNullOrEmpty(appleInfo.email))
            SetEmail(appleInfo.email);

        // 用户名（不覆盖已填写的本地名；Apple 后续登录 displayName 为空是正常的）
        if (!string.IsNullOrEmpty(appleInfo.displayName) && string.IsNullOrWhiteSpace(UserName))
            SetUserName(appleInfo.displayName);

        // 头像 URL（Apple 通常不提供头像）
        if (!string.IsNullOrEmpty(appleInfo.photoUrl))
            SetPhotoUrl(appleInfo.photoUrl);

        // 提供商
        if (!string.IsNullOrEmpty(appleInfo.providerId))
            SetAuthProviderId(appleInfo.providerId);

        // Apple 特有字段
        SetEmailVerified(appleInfo.isEmailVerified);
        SetCreationTimestamp(appleInfo.creationTimestamp);
        SetLastSignInTimestamp(appleInfo.lastSignInTimestamp);

        SetFirebaseAuthenticated(true);
        SetLoginType(LoginType.Apple);

        // 注册时间：优先用 Firebase 创建时间戳转换
        if (string.IsNullOrWhiteSpace(RegTime) && appleInfo.creationTimestamp > 0)
        {
            RegTime = appleInfo.CreationTime.ToUniversalTime().ToString("O");
        }

        SaveData();

        Debug.Log($"[UserDataManager] Apple 用户数据已同步: UID={appleInfo.firebaseUid}, " +
                  $"Email={appleInfo.email}, EmailVerified={appleInfo.isEmailVerified}, " +
                  $"Created={appleInfo.CreationTime:yyyy-MM-dd}");
    }

    /// <summary>
    /// 从 FacebookUserInfo 同步 Facebook 登录专属数据到本地
    /// 在 Facebook 登录成功后调用，可获取时间戳、邮箱验证状态等完整字段
    /// </summary>
    public void SyncFromFacebookUserInfo(FacebookUserInfo facebookInfo)
    {
        if (facebookInfo == null) return;
        Debug.Log($"facebook用户登录信息:{facebookInfo.ToString()}");

        // UID
        if (!string.IsNullOrEmpty(facebookInfo.firebaseUid))
        {
            SetFirebaseUid(facebookInfo.firebaseUid);
            SetUserId(facebookInfo.firebaseUid);
        }

        // 邮箱
        if (!string.IsNullOrEmpty(facebookInfo.email))
            SetEmail(facebookInfo.email);

        // 用户名（不覆盖已填写的本地名）
        if (!string.IsNullOrEmpty(facebookInfo.displayName) && string.IsNullOrWhiteSpace(UserName))
            SetUserName(facebookInfo.displayName);

        // 头像 URL（Facebook 通常提供方形头像）
        if (!string.IsNullOrEmpty(facebookInfo.photoUrl))
            SetPhotoUrl(facebookInfo.photoUrl);

        // 提供商
        if (!string.IsNullOrEmpty(facebookInfo.providerId))
            SetAuthProviderId(facebookInfo.providerId);

        // Facebook 特有字段
        if (!string.IsNullOrEmpty(facebookInfo.providerUserId))
            SetFacebookProviderUserId(facebookInfo.providerUserId);

        SetEmailVerified(facebookInfo.isEmailVerified);
        SetCreationTimestamp(facebookInfo.creationTimestamp);
        SetLastSignInTimestamp(facebookInfo.lastSignInTimestamp);

        SetFirebaseAuthenticated(true);
        SetLoginType(LoginType.Facebook);

        // 注册时间：优先用 Firebase 创建时间戳转换
        if (string.IsNullOrWhiteSpace(RegTime) && facebookInfo.creationTimestamp > 0)
        {
            RegTime = facebookInfo.CreationTime.ToUniversalTime().ToString("O");
        }

        SaveData();

        Debug.Log($"[UserDataManager] Facebook 用户数据已同步: UID={facebookInfo.firebaseUid}, " +
                  $"Email={facebookInfo.email}, EmailVerified={facebookInfo.isEmailVerified}, " +
                  $"Created={facebookInfo.CreationTime:yyyy-MM-dd}");
    }

    #endregion
}
