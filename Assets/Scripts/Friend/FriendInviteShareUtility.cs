using System;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public static class FriendInviteShareUtility
{
    private const string AppName = "Moonly";

    public static string BuildInviteText()
    {
        string searchName = GetCurrentUserSearchName();
        if (string.IsNullOrWhiteSpace(searchName))
        {
            return $"我在 {AppName} 等你一起看每日神谕。打开 App 后进入添加好友，我们在那里互相关注。";
        }

        return $"我在 {AppName} 等你一起看每日神谕。打开 App 后进入「添加好友」，搜索「{searchName}」就能找到我。";
    }

    public static void CopyInviteText(string toast = "邀请文案已复制")
    {
        GUIUtility.systemCopyBuffer = BuildInviteText();
        ToastManager.ShowToast(toast);
    }

    public static void ShareInviteText(string title = "邀请好友")
    {
        string text = BuildInviteText();
        GUIUtility.systemCopyBuffer = text;

#if UNITY_IOS && !UNITY_EDITOR
        NativeIOSShare.ShareText(text);
        ToastManager.ShowToast("已打开系统分享");
#elif UNITY_ANDROID && !UNITY_EDITOR
        ShareTextToAndroid(text, title);
        ToastManager.ShowToast("已打开系统分享");
#else
        ToastManager.ShowToast("邀请文案已复制");
#endif
    }

    public static void OpenSmsInvite(string phone = "")
    {
        string text = BuildInviteText();
        GUIUtility.systemCopyBuffer = text;

#if UNITY_EDITOR
        ToastManager.ShowToast("Editor 已复制短信邀请文案");
#else
        string escapedText = Uri.EscapeDataString(text);
        string cleanPhone = string.IsNullOrWhiteSpace(phone) ? string.Empty : phone.Replace(" ", string.Empty);
        string separator = Application.platform == RuntimePlatform.IPhonePlayer ? "&" : "?";
        string url = string.IsNullOrWhiteSpace(cleanPhone)
            ? $"sms:?body={escapedText}"
            : $"sms:{cleanPhone}{separator}body={escapedText}";

        Application.OpenURL(url);
        ToastManager.ShowToast("已打开短信邀请");
#endif
    }

    public static void ShareFacebookInvite()
    {
        string sdkStatus = FacebookSignInHelper.Instance != null && FacebookSignInHelper.Instance.IsSDKAvailable()
            ? "Facebook SDK 可用"
            : "Facebook 好友发现需要 SDK 和应用权限，已改用分享邀请";

        Debug.Log($"[FriendInviteShareUtility] {sdkStatus}");
        ShareInviteText("Facebook 邀请好友");
    }

    private static string GetCurrentUserSearchName()
    {
        UserDataManager userData = UserDataManager.Instance;
        if (userData != null)
        {
            if (!string.IsNullOrWhiteSpace(userData.UserName))
                return userData.UserName.Trim();

            if (!string.IsNullOrWhiteSpace(userData.Email))
                return userData.Email.Trim();
        }

        FirebaseAuthManager auth = FirebaseAuthManager.Instance;
        if (auth != null && auth.CurrentUser != null)
        {
            if (!string.IsNullOrWhiteSpace(auth.CurrentUser.DisplayName))
                return auth.CurrentUser.DisplayName.Trim();

            if (!string.IsNullOrWhiteSpace(auth.CurrentUser.Email))
                return auth.CurrentUser.Email.Trim();
        }

        return string.Empty;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void ShareTextToAndroid(string text, string title)
    {
        using (AndroidJavaClass unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (AndroidJavaObject currentActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity"))
        using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
        using (AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent"))
        {
            intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
            intentObject.Call<AndroidJavaObject>("setType", "text/plain");
            intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text);
            currentActivity.Call("startActivity", intentClass.CallStatic<AndroidJavaObject>("createChooser", intentObject, title));
        }
    }
#endif
}
