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
    ThirdParty  // 第三方登录
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

    #endregion

    #region 常量

    private const string KEY_USER_NAME = "UserData_Name";
    private const string KEY_BIRTHDAY = "UserData_Birthday";
    private const string KEY_BIRTH_TIME = "UserData_BirthTime";
    private const string KEY_CITY = "UserData_City";
    private const string KEY_AVATAR_TYPE = "UserData_AvatarType";

    // 账户信息 Key
    private const string KEY_EMAIL = "UserData_Email";
    private const string KEY_USER_ID = "UserData_UserId";
    private const string KEY_REG_TIME = "UserData_RegTime";
    private const string KEY_LOGIN_TYPE = "UserData_LoginType";
    private const string KEY_STATUS = "UserData_Status";

    #endregion

    #region 生命周期

    protected override void Awake()
    {
        base.Awake();
        LoadData();
    }

    #endregion

    #region 数据操作

    /// <summary>
    /// 设置用户名
    /// </summary>
    public void SetUserName(string name)
    {
        UserName = name ?? string.Empty;
    }

    /// <summary>
    /// 设置生日
    /// </summary>
    public void SetBirthday(string birthday)
    {
        Birthday = birthday ?? string.Empty;
    }

    /// <summary>
    /// 设置出生时间
    /// </summary>
    public void SetBirthTime(string time)
    {
        BirthTime = time ?? string.Empty;
    }

    /// <summary>
    /// 设置出生城市
    /// </summary>
    public void SetCity(string city)
    {
        City = city ?? string.Empty;
    }

    /// <summary>
    /// 设置头像类型
    /// </summary>
    public void SetAvatarType(AvatarType type)
    {
        CurrentAvatar = type;
    }

    /// <summary>
    /// 切换头像类型
    /// </summary>
    public void ToggleAvatar()
    {
        CurrentAvatar = CurrentAvatar == AvatarType.Moon ? AvatarType.Person : AvatarType.Moon;
    }

    // ========== 账户信息操作方法 ==========

    /// <summary>
    /// 设置邮箱
    /// </summary>
    public void SetEmail(string email)
    {
        Email = email ?? string.Empty;
    }

    /// <summary>
    /// 设置用户ID
    /// </summary>
    public void SetUserId(string userId)
    {
        UserId = userId ?? string.Empty;
    }

    /// <summary>
    /// 设置注册时间
    /// </summary>
    public void SetRegTime(string regTime)
    {
        RegTime = regTime ?? string.Empty;
    }

    /// <summary>
    /// 设置登录类型
    /// </summary>
    public void SetLoginType(LoginType loginType)
    {
        CurrentLoginType = loginType;
    }

    /// <summary>
    /// 设置账号状态
    /// </summary>
    public void SetStatus(AccountStatus status)
    {
        Status = status;
    }

    #endregion

    #region 持久化

    /// <summary>
    /// 保存所有数据到本地
    /// </summary>
    public void SaveData()
    {
        // 基础信息
        PlayerPrefs.SetString(KEY_USER_NAME, UserName);
        PlayerPrefs.SetString(KEY_BIRTHDAY, Birthday);
        PlayerPrefs.SetString(KEY_BIRTH_TIME, BirthTime);
        PlayerPrefs.SetString(KEY_CITY, City);
        PlayerPrefs.SetInt(KEY_AVATAR_TYPE, (int)CurrentAvatar);

        // 账户信息
        PlayerPrefs.SetString(KEY_EMAIL, Email);
        PlayerPrefs.SetString(KEY_USER_ID, UserId);
        PlayerPrefs.SetString(KEY_REG_TIME, RegTime);
        PlayerPrefs.SetInt(KEY_LOGIN_TYPE, (int)CurrentLoginType);
        PlayerPrefs.SetInt(KEY_STATUS, (int)Status);

        PlayerPrefs.Save();

        Debug.Log("[UserDataManager] 用户数据已保存");
    }

    /// <summary>
    /// 从本地加载数据
    /// </summary>
    public void LoadData()
    {
        // 基础信息
        UserName = PlayerPrefs.GetString(KEY_USER_NAME, string.Empty);
        Birthday = PlayerPrefs.GetString(KEY_BIRTHDAY, string.Empty);
        BirthTime = PlayerPrefs.GetString(KEY_BIRTH_TIME, string.Empty);
        City = PlayerPrefs.GetString(KEY_CITY, string.Empty);
        CurrentAvatar = (AvatarType)PlayerPrefs.GetInt(KEY_AVATAR_TYPE, (int)AvatarType.Moon);

        // 账户信息
        Email = PlayerPrefs.GetString(KEY_EMAIL, string.Empty);
        UserId = PlayerPrefs.GetString(KEY_USER_ID, string.Empty);
        RegTime = PlayerPrefs.GetString(KEY_REG_TIME, string.Empty);
        CurrentLoginType = (LoginType)PlayerPrefs.GetInt(KEY_LOGIN_TYPE, (int)LoginType.Email);
        Status = (AccountStatus)PlayerPrefs.GetInt(KEY_STATUS, (int)AccountStatus.Normal);

        Debug.Log("[UserDataManager] 用户数据已加载");
    }

    /// <summary>
    /// 清除所有用户数据
    /// </summary>
    public void ClearData()
    {
        UserName = string.Empty;
        Birthday = string.Empty;
        BirthTime = string.Empty;
        City = string.Empty;
        CurrentAvatar = AvatarType.Moon;

        Email = string.Empty;
        UserId = string.Empty;
        RegTime = string.Empty;
        CurrentLoginType = LoginType.Email;
        Status = AccountStatus.Normal;

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
        PlayerPrefs.Save();

        Debug.Log("[UserDataManager] 用户数据已清除");
    }

    #endregion

    #region 数据验证

    /// <summary>
    /// 检查用户资料是否已填写完整
    /// </summary>
    public bool IsProfileComplete()
    {
        return !string.IsNullOrWhiteSpace(UserName)
            && !string.IsNullOrWhiteSpace(Birthday)
            && !string.IsNullOrWhiteSpace(BirthTime)
            && !string.IsNullOrWhiteSpace(City);
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
            LoginType.Email => "邮箱登录",
            LoginType.Phone => "手机登录",
            LoginType.Guest => "游客登录",
            LoginType.ThirdParty => "第三方登录",
            _ => "未知登录"
        };
    }

    /// <summary>
    /// 获取账号状态的显示文本
    /// </summary>
    public string GetStatusDisplayText()
    {
        return Status switch
        {
            AccountStatus.Normal => "正常",
            AccountStatus.Frozen => "冻结",
            AccountStatus.Banned => "封禁",
            AccountStatus.Pending => "待验证",
            _ => "未知状态"
        };
    }

    /// <summary>
    /// 格式化注册时间显示
    /// </summary>
    public string GetFormattedRegTime()
    {
        if (string.IsNullOrWhiteSpace(RegTime))
            return "未注册";

        // 如果已经是格式化好的字符串，直接返回
        if (RegTime.Contains("T"))
        {
            // 将 ISO 8601 格式转换为更易读的格式
            if (DateTime.TryParse(RegTime, out DateTime dt))
            {
                return dt.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        return RegTime;
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
    /// 生成默认用户ID
    /// </summary>
    public void GenerateDefaultUserId()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            string prefix = string.IsNullOrWhiteSpace(Email) ? "user" : Email.Split('@')[0];
            UserId = $"{prefix}_{SystemInfo.deviceUniqueIdentifier.GetHashCode():x8}";
            SaveData();
        }
    }

    /// <summary>
    /// 退出登录（清除账户相关数据但保留基础用户资料）
    /// </summary>
    public void Logout()
    {
        Email = string.Empty;
        UserId = string.Empty;
        RegTime = string.Empty;
        CurrentLoginType = LoginType.Email;
        Status = AccountStatus.Normal;

        PlayerPrefs.DeleteKey(KEY_EMAIL);
        PlayerPrefs.DeleteKey(KEY_USER_ID);
        PlayerPrefs.DeleteKey(KEY_REG_TIME);
        PlayerPrefs.DeleteKey(KEY_LOGIN_TYPE);
        PlayerPrefs.DeleteKey(KEY_STATUS);
        PlayerPrefs.Save();

        Debug.Log("[UserDataManager] 已退出登录");
    }

    #endregion
}
