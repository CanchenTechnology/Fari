using System;
using System.Collections;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;
using XFGameFrameWork;

/// <summary>
/// 后端会员状态客户端。
/// 只读取 Cloud Functions 返回的会员状态，支付结果由后端 webhook 写入 Firestore。
/// </summary>
public class BackendMembershipClient : MonoSingleton<BackendMembershipClient>
{
    private const string MEMBERSHIP_STATUS_URL = "https://us-central1-fari-app-b2fd2.cloudfunctions.net/membershipStatus";

    public void GetMembershipStatus(Action<MembershipStatusResponse> onSuccess, Action<string> onError = null)
    {
        StartCoroutine(GetMembershipStatusRoutine(onSuccess, onError));
    }

    private IEnumerator GetMembershipStatusRoutine(Action<MembershipStatusResponse> onSuccess, Action<string> onError)
    {
        string idToken = null;
        yield return GetFirebaseIdToken(
            token => idToken = token,
            error => onError?.Invoke(error));

        if (string.IsNullOrEmpty(idToken))
            yield break;

        using (UnityWebRequest request = UnityWebRequest.Get(MEMBERSHIP_STATUS_URL))
        {
            request.SetRequestHeader("Authorization", "Bearer " + idToken);
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool failed = request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError;
#else
            bool failed = request.isNetworkError || request.isHttpError;
#endif
            if (failed)
            {
                onError?.Invoke($"{request.error}: {request.downloadHandler.text}");
                yield break;
            }

            try
            {
                var response = JsonUtility.FromJson<MembershipStatusResponse>(request.downloadHandler.text);
                onSuccess?.Invoke(response);
            }
            catch (Exception e)
            {
                onError?.Invoke("会员状态解析失败: " + e.Message);
            }
        }
    }

    private IEnumerator GetFirebaseIdToken(Action<string> onToken, Action<string> onError)
    {
        var user = FirebaseAuth.DefaultInstance?.CurrentUser;
        if (user == null)
        {
            onError?.Invoke("用户未登录，无法校验会员状态");
            yield break;
        }

        var task = user.TokenAsync(false);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled)
        {
            onError?.Invoke(task.Exception?.InnerException?.Message ?? "获取 Firebase Token 失败");
            yield break;
        }

        onToken?.Invoke(task.Result);
    }
}

[Serializable]
public class MembershipStatusResponse
{
    public string uid;
    public string membershipStatus;
    public bool isPro;
    public string proExpiresAt;
}
