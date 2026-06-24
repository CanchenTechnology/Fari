using System;
using System.Collections.Generic;
using System.Globalization;
using Firebase.Firestore;
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class RegistrationFlowUtility
{
    public const string UnknownBirthTime = "unknown";

    private const string PendingPhoneKey = "Registration_PendingPhoneNumber";
    private const string PendingPhoneCountryCodeKey = "Registration_PendingPhoneCountryCode";
    private const string PendingPhoneStatusKey = "Registration_PendingPhoneStatus";

    public static string NormalizeName(string input)
    {
        string value = string.IsNullOrWhiteSpace(input)
            ? string.Empty
            : input.Trim().Replace("\r", " ").Replace("\n", " ");

        while (value.Contains("  ", StringComparison.Ordinal))
            value = value.Replace("  ", " ");

        return value.Length > 32 ? value.Substring(0, 32) : value;
    }

    public static bool TryNormalizeBirthday(string input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string value = input.Trim()
            .Replace("年", "-")
            .Replace("月", "-")
            .Replace("日", "")
            .Replace("/", "-")
            .Replace(".", "-");

        string[] formats =
        {
            "yyyy-M-d",
            "yyyy-MM-dd",
            "yyyy-M-dd",
            "yyyy-MM-d"
        };

        if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            return false;

        if (date.Date > DateTime.Today || date.Year < 1900)
            return false;

        normalized = date.ToString("yyyy-MM-dd");
        return true;
    }

    public static bool TryNormalizeBirthTime(string input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string trimmed = input.Trim();
        if (IsUnknownBirthTime(trimmed))
        {
            normalized = UnknownBirthTime;
            return true;
        }

        string value = trimmed
            .Replace("点", ":")
            .Replace("时", ":")
            .Replace("分", "");

        if (value.EndsWith(":", StringComparison.Ordinal))
            value += "00";

        string[] formats =
        {
            "H:m",
            "H:mm",
            "HH:m",
            "HH:mm"
        };

        if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
            return false;

        normalized = time.ToString("HH:mm");
        return true;
    }

    public static string FormatBirthdayForDisplay(string value)
    {
        if (!TryNormalizeBirthday(value, out string normalized))
            return "选择日期";

        return DateTime.TryParseExact(normalized, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
            ? date.ToString("yyyy.MM.dd")
            : normalized.Replace("-", ".");
    }

    public static string FormatBirthTimeForDisplay(string value)
    {
        if (IsUnknownBirthTime(value))
            return "时间未知";

        return TryNormalizeBirthTime(value, out string normalized) && !string.IsNullOrEmpty(normalized)
            ? normalized
            : "选择时间";
    }

    public static bool IsUnknownBirthTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        string normalized = value.Trim().ToLowerInvariant();
        return normalized == UnknownBirthTime
            || normalized == "unknown_time"
            || normalized == "未知"
            || normalized == "时间未知"
            || normalized == "不确定";
    }

    public static bool TryGetBirthdayParts(string value, out string year, out string month, out string day)
    {
        year = "选择年份";
        month = "选择月份";
        day = "选择日期";

        if (!TryNormalizeBirthday(value, out string normalized))
            return false;

        DateTime date = DateTime.ParseExact(normalized, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        year = date.Year.ToString(CultureInfo.InvariantCulture);
        month = date.Month.ToString("00", CultureInfo.InvariantCulture) + "月";
        day = date.Day.ToString("00", CultureInfo.InvariantCulture) + "日";
        return true;
    }

    public static bool TryGetBirthTimeParts(string value, out string hour, out string minute, out string period)
    {
        hour = "选择小时";
        minute = "选择分钟";
        period = "选择时段";

        if (IsUnknownBirthTime(value))
        {
            hour = "未知";
            minute = "--";
            period = "无需选择";
            return true;
        }

        if (!TryNormalizeBirthTime(value, out string normalized) || normalized == UnknownBirthTime)
            return false;

        DateTime time = DateTime.ParseExact(normalized, "HH:mm", CultureInfo.InvariantCulture);
        hour = time.ToString("HH", CultureInfo.InvariantCulture);
        minute = time.ToString("mm", CultureInfo.InvariantCulture);
        period = time.Hour < 12 ? "AM" : "PM";
        return true;
    }

    public static string NormalizePhoneNumber(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        List<char> chars = new List<char>();
        foreach (char ch in input.Trim())
        {
            if (char.IsDigit(ch) || (ch == '+' && chars.Count == 0))
                chars.Add(ch);
        }

        return new string(chars.ToArray());
    }

    public static bool IsPlausiblePhoneNumber(string phone)
    {
        string normalized = NormalizePhoneNumber(phone);
        int digitCount = 0;
        foreach (char ch in normalized)
        {
            if (char.IsDigit(ch))
                digitCount++;
        }

        return digitCount >= 6 && digitCount <= 15;
    }

    public static bool IsPlausibleVerificationCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return true;

        string trimmed = code.Trim();
        if (trimmed.Length < 4 || trimmed.Length > 8) return false;

        foreach (char ch in trimmed)
        {
            if (!char.IsDigit(ch))
                return false;
        }

        return true;
    }

    public static void SetButtonText(Button button, string value)
    {
        if (button == null) return;

        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = value ?? string.Empty;
            return;
        }

        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
            text.text = value ?? string.Empty;
    }

    public static void SaveUserDataAndSyncCloud(Action<bool> onCloudSyncComplete = null)
    {
        UserDataManager manager = UserDataManager.Instance;
        if (manager == null)
        {
            onCloudSyncComplete?.Invoke(false);
            return;
        }

        manager.SaveData();
        SyncAuthUserProfile(manager);

        FirestoreManager firestore = FirestoreManager.Instance;
        if (firestore != null && firestore.IsInitialized && !string.IsNullOrWhiteSpace(manager.FirebaseUid))
        {
            firestore.SaveUserData(success =>
            {
                if (!success)
                    Debug.LogWarning("[RegistrationFlowUtility] 注册资料云端同步失败");
                onCloudSyncComplete?.Invoke(success);
            });
            return;
        }

        onCloudSyncComplete?.Invoke(false);
    }

    private static void SyncAuthUserProfile(UserDataManager manager)
    {
        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        if (authManager == null || authManager.CurrentUser == null || manager == null)
            return;

        string displayName = NormalizeName(manager.UserName);
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = authManager.CurrentUser.DisplayName ?? string.Empty;

        string photoUrl = NormalizeHttpUrl(manager.PhotoUrl);
        if (string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(photoUrl))
            return;

        authManager.UpdateUserProfile(displayName, photoUrl, success =>
        {
            if (!success)
                Debug.LogWarning("[RegistrationFlowUtility] Firebase Auth 用户资料同步失败");
        });
    }

    private static string NormalizeHttpUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return trimmed;
        }

        Debug.LogWarning("[RegistrationFlowUtility] 跳过无效头像 URL，避免 Firebase Auth Profile 同步失败");
        return string.Empty;
    }

    public static void SavePendingPhone(string countryCode, string phoneNumber, string status)
    {
        string cleanCountryCode = string.IsNullOrWhiteSpace(countryCode) ? "+1" : countryCode.Trim();
        string cleanPhone = NormalizePhoneNumber(phoneNumber);
        string cleanStatus = string.IsNullOrWhiteSpace(status) ? "pending_backend_verification" : status.Trim();

        PlayerPrefs.SetString(PendingPhoneCountryCodeKey, cleanCountryCode);
        PlayerPrefs.SetString(PendingPhoneKey, cleanPhone);
        PlayerPrefs.SetString(PendingPhoneStatusKey, cleanStatus);
        PlayerPrefs.Save();

        UserDataManager manager = UserDataManager.Instance;
        FirestoreManager firestore = FirestoreManager.Instance;
        if (firestore == null || !firestore.IsInitialized || manager == null || string.IsNullOrWhiteSpace(manager.FirebaseUid))
            return;

        bool isVerified = string.Equals(cleanStatus, "verified", StringComparison.OrdinalIgnoreCase);
        Dictionary<string, object> fields = new Dictionary<string, object>
        {
            { "phoneCountryCode", cleanCountryCode },
            { "phoneNumber", cleanPhone },
            { "phoneVerificationStatus", cleanStatus },
            { "phoneVerified", isVerified },
            { "phoneVerificationUpdatedAt", FieldValue.ServerTimestamp }
        };

        if (isVerified)
            fields["phoneVerifiedAt"] = FieldValue.ServerTimestamp;

        firestore.UpdateUserFields(fields);
    }
}
