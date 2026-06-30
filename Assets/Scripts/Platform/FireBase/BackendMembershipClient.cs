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
    public const string MembershipStatusFunctionUrl = "https://us-central1-fari-app-b2fd2.cloudfunctions.net/membershipStatus";
    public const string ReadinessStatusFunctionUrl = "https://us-central1-fari-app-b2fd2.cloudfunctions.net/readinessStatus";
    private const float CACHE_SECONDS = 30f;
    private const string CACHE_KEY_PREFIX = "MembershipStatusCache_";
    private MembershipStatusResponse _cachedStatus;
    private float _cachedAt;
    private string _cachedUid;

    public void GetMembershipStatus(Action<MembershipStatusResponse> onSuccess, Action<string> onError = null, bool forceRefresh = false)
    {
        string currentUid = FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? string.Empty;
        if (string.IsNullOrEmpty(currentUid))
        {
            _cachedStatus = CreateFreeStatus(string.Empty);
            _cachedUid = string.Empty;
            _cachedAt = Time.realtimeSinceStartup;
            onSuccess?.Invoke(_cachedStatus);
            return;
        }

        if (_cachedStatus == null)
            LoadPersistedCache(currentUid);

        if (!forceRefresh && IsCacheUsableForUid(currentUid))
        {
            ApplyUsageResetSignal(_cachedStatus);
            onSuccess?.Invoke(_cachedStatus);
            return;
        }

        StartCoroutine(GetMembershipStatusRoutine(onSuccess, onError));
    }

    public MembershipStatusResponse GetCachedOrFreeStatus()
    {
        string currentUid = FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? string.Empty;
        if (_cachedStatus == null)
            LoadPersistedCache(currentUid);

        if (_cachedStatus != null && _cachedUid == currentUid)
        {
            ApplyUsageResetSignal(_cachedStatus);
            return _cachedStatus;
        }

        return CreateFreeStatus(currentUid);
    }

    private void ApplyUsageResetSignal(MembershipStatusResponse response)
    {
        if (response == null) return;
        UsageStatsManager.Instance?.ApplyDailyOracleResetSignal(
            response.uid,
            response.dailyOracleUsageResetVersion,
            response.dailyOracleUsageResetAt);
    }

    private IEnumerator GetMembershipStatusRoutine(Action<MembershipStatusResponse> onSuccess, Action<string> onError)
    {
        string idToken = null;
        string requestUid = FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? string.Empty;
        yield return GetFirebaseIdToken(
            token => idToken = token,
            error => HandleError(requestUid, error, onSuccess, onError));

        if (string.IsNullOrEmpty(idToken))
            yield break;

        using (UnityWebRequest request = UnityWebRequest.Get(MembershipStatusFunctionUrl))
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
                HandleError(requestUid, $"{request.error}: {request.downloadHandler.text}", onSuccess, onError);
                yield break;
            }

            try
            {
                var response = JsonUtility.FromJson<MembershipStatusResponse>(request.downloadHandler.text);
                _cachedStatus = response;
                _cachedAt = Time.realtimeSinceStartup;
                _cachedUid = string.IsNullOrEmpty(response?.uid) ? requestUid : response.uid;
                ApplyUsageResetSignal(response);
                PersistCache();
                onSuccess?.Invoke(response);
            }
            catch (Exception e)
            {
                HandleError(requestUid, "会员状态解析失败: " + e.Message, onSuccess, onError);
            }
        }
    }

    private bool IsCacheUsableForUid(string uid)
    {
        return _cachedStatus != null
            && _cachedUid == uid
            && Time.realtimeSinceStartup - _cachedAt < CACHE_SECONDS;
    }

    private bool HasStaleCacheForUid(string uid)
    {
        return _cachedStatus != null && _cachedUid == uid;
    }

    private void HandleError(string uid, string error, Action<MembershipStatusResponse> onSuccess, Action<string> onError)
    {
        if (HasStaleCacheForUid(uid))
        {
            onSuccess?.Invoke(_cachedStatus);
            return;
        }

        onError?.Invoke(error);
    }

    private void LoadPersistedCache(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return;

        string json = PlayerPrefs.GetString(CACHE_KEY_PREFIX + uid, string.Empty);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var cache = JsonUtility.FromJson<MembershipStatusCache>(json);
            if (cache == null || cache.status == null || cache.uid != uid) return;

            _cachedStatus = cache.status;
            _cachedUid = cache.uid;
            _cachedAt = Time.realtimeSinceStartup - Mathf.Max(0f, TimeSinceCached(cache.cachedAtUnix));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BackendMembershipClient] 会员状态缓存读取失败: " + e.Message);
        }
    }

    private void PersistCache()
    {
        if (_cachedStatus == null || string.IsNullOrEmpty(_cachedUid)) return;

        var cache = new MembershipStatusCache
        {
            uid = _cachedUid,
            cachedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            status = _cachedStatus,
        };

        PlayerPrefs.SetString(CACHE_KEY_PREFIX + _cachedUid, JsonUtility.ToJson(cache));
        PlayerPrefs.Save();
    }

    private static MembershipStatusResponse CreateFreeStatus(string uid)
    {
        return new MembershipStatusResponse
        {
            uid = uid ?? string.Empty,
            membershipStatus = "free",
            isPro = false,
            proExpiresAt = string.Empty,
            dailyOracleUsageResetVersion = string.Empty,
            dailyOracleUsageResetAt = string.Empty,
            dailyOracleUsageResetDay = string.Empty,
        };
    }

    private float TimeSinceCached(long cachedAtUnix)
    {
        if (cachedAtUnix <= 0) return CACHE_SECONDS + 1f;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Mathf.Max(0f, now - cachedAtUnix);
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
    public string dailyOracleUsageResetVersion;
    public string dailyOracleUsageResetAt;
    public string dailyOracleUsageResetDay;
}

[Serializable]
public class MembershipStatusCache
{
    public string uid;
    public long cachedAtUnix;
    public MembershipStatusResponse status;
}
