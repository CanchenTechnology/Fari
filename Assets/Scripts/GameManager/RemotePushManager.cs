using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using Firebase.Messaging;
using UnityEngine;
using UnityEngine.Networking;
using XFGameFrameWork;

/// <summary>
/// Registers this device for Firebase Cloud Messaging remote push.
/// </summary>
public class RemotePushManager : MonoSingleton<RemotePushManager>
{
    public const string SendTestPushFunctionUrl = "https://us-central1-fari-app-b2fd2.cloudfunctions.net/sendTestPush";
    private const string PushTokenCollection = "push_tokens";

    private bool initialized;
    private bool authEventsAttached;
    private bool messagingEventsAttached;
    private bool tokenRequestInFlight;
    private string lastRegisteredUid;
    private string lastTokenDocumentId;

    public string LastRegisteredUid => lastRegisteredUid;

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        tokenRequestInFlight = false;

        AttachAuthEvents();
        AttachMessagingEvents();
        StartCoroutine(RegisterWhenReadyRoutine());
    }

    public void RegisterCurrentDevice()
    {
        if (tokenRequestInFlight) return;

        string uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))
        {
            Debug.Log("[RemotePushManager] 用户未登录，暂不注册远程推送 token");
            return;
        }

#if UNITY_EDITOR
        Debug.Log("[RemotePushManager] Editor 不注册 FCM token，真机包会在 Android/iOS 上注册");
        return;
#else
        tokenRequestInFlight = true;
#if UNITY_IOS
        FirebaseMessaging.RequestPermissionAsync().ContinueWithOnMainThread(permissionTask =>
        {
            if (permissionTask.IsFaulted || permissionTask.IsCanceled)
            {
                tokenRequestInFlight = false;
                Debug.LogWarning("[RemotePushManager] iOS 推送授权请求失败: " + permissionTask.Exception?.GetBaseException().Message);
                return;
            }

            RequestToken(uid);
        });
#else
        RequestToken(uid);
#endif
#endif
    }

    private void RequestToken(string uid)
    {
        FirebaseMessaging.GetTokenAsync().ContinueWithOnMainThread(task =>
        {
            tokenRequestInFlight = false;
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogWarning("[RemotePushManager] 获取 FCM token 失败: " + task.Exception?.GetBaseException().Message);
                return;
            }

            SaveToken(uid, task.Result);
        });
    }

    public void SendDiagnosticPush(Action<bool, string> onComplete = null)
    {
        StartCoroutine(SendDiagnosticPushRoutine(onComplete));
    }

    private IEnumerator SendDiagnosticPushRoutine(Action<bool, string> onComplete)
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance?.CurrentUser;
        if (user == null)
        {
            onComplete?.Invoke(false, "用户未登录，无法发送远程推送测试");
            yield break;
        }

        var tokenTask = user.TokenAsync(false);
        yield return new WaitUntil(() => tokenTask.IsCompleted);

        if (tokenTask.IsFaulted || tokenTask.IsCanceled)
        {
            onComplete?.Invoke(false, tokenTask.Exception?.GetBaseException().Message ?? "获取 Firebase ID token 失败");
            yield break;
        }

        DiagnosticPushRequest body = new DiagnosticPushRequest
        {
            title = "Nocturne Oracle",
            body = "远程推送测试成功",
        };
        byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));

        using (UnityWebRequest request = new UnityWebRequest(SendTestPushFunctionUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(payload);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + tokenTask.Result);
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool failed = request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError;
#else
            bool failed = request.isNetworkError || request.isHttpError;
#endif
            if (failed)
            {
                onComplete?.Invoke(false, $"{request.error}: {request.downloadHandler.text}");
                yield break;
            }

            onComplete?.Invoke(true, request.downloadHandler.text);
        }
    }

    private IEnumerator RegisterWhenReadyRoutine()
    {
        const int maxAttempts = 60;
        for (int i = 0; i < maxAttempts; i++)
        {
            if (!string.IsNullOrEmpty(GetCurrentUid()))
            {
                RegisterCurrentDevice();
                yield break;
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private void AttachAuthEvents()
    {
        if (authEventsAttached) return;

        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        if (authManager == null) return;

        authManager.OnFirebaseInitialized += OnFirebaseInitialized;
        authManager.OnLoginSuccess += OnLoginSuccess;
        authManager.OnLogout += OnLogout;
        authEventsAttached = true;
    }

    private void OnFirebaseInitialized()
    {
        RegisterCurrentDevice();
    }

    private void OnLoginSuccess(AuthProvider provider, FirebaseUser user)
    {
        RegisterCurrentDevice();
    }

    private void OnLogout()
    {
        DisableLastRegisteredToken();
        lastRegisteredUid = string.Empty;
        lastTokenDocumentId = string.Empty;
    }

    private void AttachMessagingEvents()
    {
        if (messagingEventsAttached) return;

        FirebaseMessaging.TokenReceived += HandleTokenReceived;
        FirebaseMessaging.MessageReceived += HandleMessageReceived;
        messagingEventsAttached = true;
    }

    private void HandleTokenReceived(object sender, TokenReceivedEventArgs args)
    {
        string uid = GetCurrentUid();
        string token = args?.Token;
        if (!string.IsNullOrEmpty(uid) && !string.IsNullOrEmpty(token))
            SaveToken(uid, token);
    }

    private void HandleMessageReceived(object sender, MessageReceivedEventArgs args)
    {
        string from = args?.Message?.From;
        Debug.Log("[RemotePushManager] 收到前台远程推送" + (string.IsNullOrEmpty(from) ? string.Empty : $": {from}"));

        IDictionary<string, string> data = args?.Message?.Data;
        if (Application.isFocused && IsDialogueReplyPush(data))
        {
            Debug.Log("[RemotePushManager] 前台收到对话回复推送，已交给当前对话界面处理");
            return;
        }

        if (data != null && data.Count > 0)
            AppNotificationScheduler.Instance?.HandleRemotePushData(data);
    }

    private static bool IsDialogueReplyPush(IDictionary<string, string> data)
    {
        if (data == null || data.Count == 0)
            return false;

        string action = GetPushValue(data, "clickAction");
        if (string.IsNullOrWhiteSpace(action)) action = GetPushValue(data, "type");
        if (string.IsNullOrWhiteSpace(action)) action = GetPushValue(data, "source");
        return string.Equals(action, "dialogue_reply", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "chat_reply", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPushValue(IDictionary<string, string> data, string key)
    {
        if (data == null || string.IsNullOrWhiteSpace(key)) return string.Empty;
        foreach (KeyValuePair<string, string> entry in data)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }

        return string.Empty;
    }

    private void SaveToken(string uid, string token)
    {
        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token)) return;

        try
        {
            string docId = HashToken(token);
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                { "token", token },
                { "enabled", true },
                { "platform", GetPlatformName() },
                { "appIdentifier", Application.identifier ?? string.Empty },
                { "appVersion", Application.version ?? string.Empty },
                { "deviceModel", SystemInfo.deviceModel ?? string.Empty },
                { "deviceName", SystemInfo.deviceName ?? string.Empty },
                { "deviceUniqueIdentifier", SystemInfo.deviceUniqueIdentifier ?? string.Empty },
                { "systemLanguage", Application.systemLanguage.ToString() },
                { "updatedAt", FieldValue.ServerTimestamp },
            };

            FirebaseFirestore.DefaultInstance
                .Collection("users")
                .Document(uid)
                .Collection(PushTokenCollection)
                .Document(docId)
                .SetAsync(data, SetOptions.MergeAll)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogWarning("[RemotePushManager] 保存 FCM token 失败: " + task.Exception?.GetBaseException().Message);
                        return;
                    }

                    lastRegisteredUid = uid;
                    lastTokenDocumentId = docId;
                    Debug.Log($"[RemotePushManager] 已注册远程推送 token: uid={uid}, platform={GetPlatformName()}");
                });
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[RemotePushManager] 保存 FCM token 异常: " + ex.GetBaseException().Message);
        }
    }

    private void DisableLastRegisteredToken()
    {
        if (string.IsNullOrEmpty(lastRegisteredUid) || string.IsNullOrEmpty(lastTokenDocumentId)) return;

        try
        {
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                { "enabled", false },
                { "updatedAt", FieldValue.ServerTimestamp },
            };

            FirebaseFirestore.DefaultInstance
                .Collection("users")
                .Document(lastRegisteredUid)
                .Collection(PushTokenCollection)
                .Document(lastTokenDocumentId)
                .SetAsync(data, SetOptions.MergeAll);
        }
        catch
        {
            // Logout should never be blocked by token cleanup.
        }
    }

    private static string GetCurrentUid()
    {
        try
        {
            string authUid = FirebaseAuth.DefaultInstance?.CurrentUser?.UserId;
            if (!string.IsNullOrEmpty(authUid)) return authUid;
        }
        catch
        {
            // Firebase may still be initializing.
        }

        return UserDataManager.Instance != null ? UserDataManager.Instance.FirebaseUid : string.Empty;
    }

    private static string GetPlatformName()
    {
#if UNITY_ANDROID
        return "android";
#elif UNITY_IOS
        return "ios";
#else
        return Application.platform.ToString().ToLowerInvariant();
#endif
    }

    private static string HashToken(string token)
    {
        using SHA256 sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        StringBuilder builder = new StringBuilder(bytes.Length * 2);
        foreach (byte value in bytes)
            builder.Append(value.ToString("x2"));
        return builder.ToString();
    }

    private void OnDestroy()
    {
        if (authEventsAttached && FirebaseAuthManager.Instance != null)
        {
            FirebaseAuthManager.Instance.OnFirebaseInitialized -= OnFirebaseInitialized;
            FirebaseAuthManager.Instance.OnLoginSuccess -= OnLoginSuccess;
            FirebaseAuthManager.Instance.OnLogout -= OnLogout;
            authEventsAttached = false;
        }

        if (messagingEventsAttached)
        {
            FirebaseMessaging.TokenReceived -= HandleTokenReceived;
            FirebaseMessaging.MessageReceived -= HandleMessageReceived;
            messagingEventsAttached = false;
        }
    }

    [Serializable]
    private class DiagnosticPushRequest
    {
        public string title;
        public string body;
    }
}
