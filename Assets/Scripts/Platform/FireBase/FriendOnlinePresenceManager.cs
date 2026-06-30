using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.Networking;
using XFGameFrameWork;

/// <summary>
/// 通过 Realtime Database 的 .info/connected + OnDisconnect 维护好友在线状态。
/// Firestore 只镜像一份公开资料字段，作为旧数据和搜索资料的兜底。
/// </summary>
public class FriendOnlinePresenceManager : MonoSingleton<FriendOnlinePresenceManager>
{
    private const string ConnectedInfoPath = ".info/connected";
    private const string PresenceRootPath = "presence";
    private const string DatabaseUrl = "https://fari-app-b2fd2-default-rtdb.firebaseio.com";
    private const float HeartbeatIntervalSeconds = 30f;
    private const float RetryIntervalSeconds = 1f;
    private const long OnlinePresenceTimeoutMs = 90L * 1000L;

    private FirebaseDatabase realtimeDb;
    private FirebaseFirestore firestoreDb;
    private DatabaseReference connectedReference;
    private DatabaseReference currentPresenceReference;
    private EventHandler<ValueChangedEventArgs> connectedChangedHandler;
    private Coroutine heartbeatCoroutine;
    private Coroutine startRetryCoroutine;
    private Coroutine watchRetryCoroutine;
    private string currentUid = string.Empty;
#if UNITY_EDITOR
    private bool currentUsesEditorRestPresence;
#endif
    private bool isQuitting;

    private readonly Dictionary<string, RealtimePresenceWatcher> friendPresenceWatchers = new Dictionary<string, RealtimePresenceWatcher>();
    private readonly HashSet<string> desiredFriendUids = new HashSet<string>();

    public bool IsPresenceOnline => !string.IsNullOrEmpty(currentUid) && heartbeatCoroutine != null;

    protected override void Awake()
    {
        base.Awake();
        StartPresence();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            StopPresence(true);
            return;
        }

        StartPresence();
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
        StopPresence(true);
        ClearFriendWatchers();
    }

    private void OnDestroy()
    {
        ClearFriendWatchers();
        if (!isQuitting)
            StopPresence(true);
    }

    public void StartPresence()
    {
        if (!TryResolveReadyState(out string uid, out bool shouldRetry))
        {
            if (shouldRetry)
                ScheduleStartRetry();
            else
                StopStartRetry();
            return;
        }

        StopStartRetry();

#if UNITY_EDITOR
        bool usesEditorRestPresence = ShouldUseEditorRestPresence(uid);
#endif
        if (!string.IsNullOrEmpty(currentUid) && currentUid != uid)
        {
            WritePresence(currentUid, false, true);
            DetachConnectionListener();
        }

        currentUid = uid;
#if UNITY_EDITOR
        currentUsesEditorRestPresence = usesEditorRestPresence;
        if (currentUsesEditorRestPresence)
        {
            DetachConnectionListener();
            WritePresence(uid, true, true);
        }
        else
        {
            AttachConnectionListener();
            RegisterOnlinePresence();
        }
#else
        AttachConnectionListener();
        RegisterOnlinePresence();
#endif

        if (heartbeatCoroutine == null)
            heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());
    }

    public void StopPresence(bool writeOffline)
    {
        StopStartRetry();

        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }

        DetachConnectionListener();

        if (writeOffline && !string.IsNullOrEmpty(currentUid))
            WritePresence(currentUid, false, true);

        currentPresenceReference = null;
        currentUid = string.Empty;
#if UNITY_EDITOR
        currentUsesEditorRestPresence = false;
#endif
    }

    public void WatchFriends(IEnumerable<FriendDataManager.FriendData> friends)
    {
        desiredFriendUids.Clear();

        if (friends != null)
        {
            foreach (FriendDataManager.FriendData friend in friends)
            {
                if (friend == null || friend.isVirtual) continue;
                string uid = NormalizeUid(friend.firebaseUid);
                if (!string.IsNullOrEmpty(uid))
                    desiredFriendUids.Add(uid);
            }
        }

        if (!TryEnsureRealtimeDatabaseReady())
        {
            ScheduleWatchRetry();
            return;
        }

        StopWatchRetry();
        ApplyFriendWatchers();
    }

    public void ClearFriendWatchers()
    {
        StopWatchRetry();

        foreach (RealtimePresenceWatcher watcher in friendPresenceWatchers.Values)
        {
            watcher.Stop();
        }

        friendPresenceWatchers.Clear();
        desiredFriendUids.Clear();
    }

    private IEnumerator HeartbeatRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(HeartbeatIntervalSeconds);

            if (string.IsNullOrEmpty(currentUid))
            {
                heartbeatCoroutine = null;
                yield break;
            }

#if UNITY_EDITOR
            if (currentUsesEditorRestPresence)
                WritePresence(currentUid, true, true);
            else
#endif
                WriteRealtimePresence(currentUid, true);
        }
    }

    private void ScheduleStartRetry()
    {
        if (startRetryCoroutine != null || !Application.isPlaying) return;
        startRetryCoroutine = StartCoroutine(StartRetryRoutine());
    }

    private IEnumerator StartRetryRoutine()
    {
        while (string.IsNullOrEmpty(currentUid))
        {
            yield return new WaitForSeconds(RetryIntervalSeconds);

            if (TryResolveReadyState(out string uid, out bool shouldRetry))
            {
                startRetryCoroutine = null;
                currentUid = uid;
#if UNITY_EDITOR
                currentUsesEditorRestPresence = ShouldUseEditorRestPresence(uid);
                if (currentUsesEditorRestPresence)
                {
                    DetachConnectionListener();
                    WritePresence(uid, true, true);
                }
                else
                {
                    AttachConnectionListener();
                    RegisterOnlinePresence();
                }
#else
                AttachConnectionListener();
                RegisterOnlinePresence();
#endif

                if (heartbeatCoroutine == null)
                    heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());
                yield break;
            }

            if (!shouldRetry)
                break;
        }

        startRetryCoroutine = null;
    }

    private void StopStartRetry()
    {
        if (startRetryCoroutine == null) return;
        StopCoroutine(startRetryCoroutine);
        startRetryCoroutine = null;
    }

    private void ScheduleWatchRetry()
    {
        if (watchRetryCoroutine != null || !Application.isPlaying) return;
        watchRetryCoroutine = StartCoroutine(WatchRetryRoutine());
    }

    private IEnumerator WatchRetryRoutine()
    {
        while (desiredFriendUids.Count > 0)
        {
            yield return new WaitForSeconds(RetryIntervalSeconds);

            if (TryEnsureRealtimeDatabaseReady())
            {
                watchRetryCoroutine = null;
                ApplyFriendWatchers();
                yield break;
            }
        }

        watchRetryCoroutine = null;
    }

    private void StopWatchRetry()
    {
        if (watchRetryCoroutine == null) return;
        StopCoroutine(watchRetryCoroutine);
        watchRetryCoroutine = null;
    }

    private void AttachConnectionListener()
    {
        if (realtimeDb == null || connectedChangedHandler != null) return;

        connectedReference = realtimeDb.GetReference(ConnectedInfoPath);
        connectedChangedHandler = HandleRealtimeConnectionChanged;
        connectedReference.ValueChanged += connectedChangedHandler;
    }

    private void DetachConnectionListener()
    {
        if (connectedReference != null && connectedChangedHandler != null)
            connectedReference.ValueChanged -= connectedChangedHandler;

        connectedReference = null;
        connectedChangedHandler = null;
    }

    private void HandleRealtimeConnectionChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogWarning($"[FriendOnlinePresenceManager] .info/connected 监听失败: {args.DatabaseError.Message}");
            return;
        }

        bool connected = GetBoolValue(args.Snapshot?.Value, false);
        if (connected)
            RegisterOnlinePresence();
    }

    private void RegisterOnlinePresence()
    {
        if (string.IsNullOrEmpty(currentUid) || !TryEnsureRealtimeDatabaseReady()) return;

        string uid = currentUid;
        currentPresenceReference = realtimeDb.GetReference(BuildPresencePath(uid));
        Dictionary<string, object> offlinePayload = BuildRealtimePresencePayload(false);

        currentPresenceReference.OnDisconnect()
            .SetValue(offlinePayload)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[FriendOnlinePresenceManager] 注册 OnDisconnect 失败: {task.Exception?.InnerException?.Message}");
                    return;
                }

                if (currentUid == uid)
                    WritePresence(uid, true, true);
            });
    }

    private void ApplyFriendWatchers()
    {
        List<string> staleUids = new List<string>();
        foreach (string uid in friendPresenceWatchers.Keys)
        {
            if (!desiredFriendUids.Contains(uid))
                staleUids.Add(uid);
        }

        foreach (string uid in staleUids)
        {
            friendPresenceWatchers[uid].Stop();
            friendPresenceWatchers.Remove(uid);
        }

        foreach (string uid in desiredFriendUids)
        {
            if (friendPresenceWatchers.ContainsKey(uid)) continue;
            ListenToFriendPresence(uid);
        }
    }

    private void ListenToFriendPresence(string uid)
    {
        if (realtimeDb == null || string.IsNullOrEmpty(uid)) return;

        DatabaseReference reference = realtimeDb.GetReference(BuildPresencePath(uid));
        EventHandler<ValueChangedEventArgs> handler = (sender, args) =>
        {
            if (args.DatabaseError != null)
            {
                Debug.LogWarning($"[FriendOnlinePresenceManager] 好友在线监听失败({uid}): {args.DatabaseError.Message}");
                return;
            }

            DataSnapshot snapshot = args.Snapshot;
            if (snapshot == null || !snapshot.Exists)
            {
                FriendDataManager.Instance.SetRealFriendPresence(uid, false, 0);
                return;
            }

            long lastActiveUnixMs = GetSnapshotUnixMs(snapshot, "lastActiveUnixMs");
            bool isOnline = ResolveRealtimePresenceOnline(snapshot, lastActiveUnixMs);
            FriendDataManager.Instance.SetRealFriendPresence(uid, isOnline, lastActiveUnixMs);
        };

        reference.ValueChanged += handler;
        friendPresenceWatchers[uid] = new RealtimePresenceWatcher(reference, handler);
    }

    private bool TryResolveReadyState(out string uid, out bool shouldRetry)
    {
        uid = string.Empty;
        shouldRetry = true;

        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        if (authManager == null || !authManager.IsFirebaseInitialized)
            return false;

        FirebaseUser user = authManager.CurrentUser;
        if (user == null || string.IsNullOrEmpty(user.UserId))
        {
#if UNITY_EDITOR
            if (authManager.TryGetEditorRestAuth(out string editorRestUid, out string editorRestToken)
                && !string.IsNullOrEmpty(editorRestUid)
                && !string.IsNullOrEmpty(editorRestToken))
            {
                uid = editorRestUid;
                shouldRetry = false;
                return true;
            }
#endif
            shouldRetry = false;
            return false;
        }

        if (!TryEnsureRealtimeDatabaseReady())
            return false;

        TryEnsureFirestoreReady();

        uid = user.UserId;
        shouldRetry = false;
        return true;
    }

    private bool TryEnsureRealtimeDatabaseReady()
    {
        if (realtimeDb != null)
            return true;

        try
        {
            realtimeDb = FirebaseDatabase.GetInstance(DatabaseUrl);
            return realtimeDb != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FriendOnlinePresenceManager] Realtime Database 尚未可用: {e.Message}");
            return false;
        }
    }

    private bool TryEnsureFirestoreReady()
    {
        if (firestoreDb != null)
            return true;

        FirestoreManager firestoreManager = FirestoreManager.Instance;
        if (firestoreManager == null || !firestoreManager.IsInitialized)
            return false;

        try
        {
            firestoreDb = FirebaseFirestore.DefaultInstance;
            return firestoreDb != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FriendOnlinePresenceManager] Firestore 尚未可用: {e.Message}");
            return false;
        }
    }

#if UNITY_EDITOR
    private static bool ShouldUseEditorRestPresence(string uid)
    {
        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        if (authManager == null || authManager.CurrentUser != null)
            return false;

        return authManager.TryGetEditorRestAuth(out string editorRestUid, out string editorRestToken)
            && !string.IsNullOrEmpty(editorRestToken)
            && NormalizeUid(editorRestUid) == NormalizeUid(uid);
    }
#endif

    private void WritePresence(string uid, bool online, bool mirrorToFirestore)
    {
#if UNITY_EDITOR
        if (currentUsesEditorRestPresence)
        {
            StartCoroutine(WriteEditorRestPresence(uid, online, mirrorToFirestore));
            return;
        }
#endif
        WriteRealtimePresence(uid, online);

        if (mirrorToFirestore)
            WriteFirestorePresence(uid, online);
    }

#if UNITY_EDITOR
    private IEnumerator WriteEditorRestPresence(string uid, bool online, bool mirrorToFirestore)
    {
        if (string.IsNullOrEmpty(uid)) yield break;

        FirebaseAuthManager authManager = FirebaseAuthManager.Instance;
        if (authManager == null
            || !authManager.TryGetEditorRestAuth(out string editorRestUid, out string idToken)
            || NormalizeUid(editorRestUid) != NormalizeUid(uid)
            || string.IsNullOrEmpty(idToken))
        {
            yield break;
        }

        long nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string realtimeUrl = $"{DatabaseUrl}/{PresenceRootPath}/{Uri.EscapeDataString(uid)}.json?auth={Uri.EscapeDataString(idToken)}";
        string realtimeBody = "{\"isOnline\":"
            + (online ? "true" : "false")
            + ",\"lastActiveUnixMs\":"
            + nowUnixMs
            + ",\"updatedAt\":"
            + nowUnixMs
            + "}";

        yield return SendEditorRestRequest(
            realtimeUrl,
            "PUT",
            realtimeBody,
            null,
            "Editor REST Realtime Database presence");

        if (!mirrorToFirestore) yield break;

        string timestamp = DateTimeOffset.UtcNow.ToString("o");
        string firestoreUrl = $"https://firestore.googleapis.com/v1/projects/fari-app-b2fd2/databases/(default)/documents/public_profiles/{Uri.EscapeDataString(uid)}"
            + "?updateMask.fieldPaths=isOnline"
            + "&updateMask.fieldPaths=lastActiveUnixMs"
            + "&updateMask.fieldPaths=lastActiveAt"
            + "&updateMask.fieldPaths=presenceUpdatedAt";
        string firestoreBody = "{\"fields\":{"
            + "\"isOnline\":{\"booleanValue\":" + (online ? "true" : "false") + "},"
            + "\"lastActiveUnixMs\":{\"integerValue\":\"" + nowUnixMs + "\"},"
            + "\"lastActiveAt\":{\"timestampValue\":\"" + timestamp + "\"},"
            + "\"presenceUpdatedAt\":{\"timestampValue\":\"" + timestamp + "\"}"
            + "}}";

        yield return SendEditorRestRequest(
            firestoreUrl,
            "PATCH",
            firestoreBody,
            idToken,
            "Editor REST Firestore presence");
    }

    private static IEnumerator SendEditorRestRequest(string url, string method, string body, string bearerToken, string label)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body ?? string.Empty));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(bearerToken))
                request.SetRequestHeader("Authorization", "Bearer " + bearerToken);

            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool failed = request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError;
#else
            bool failed = request.isNetworkError || request.isHttpError;
#endif
            if (failed)
            {
                string response = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                Debug.LogWarning($"[FriendOnlinePresenceManager] {label} failed: {request.error} {response}");
            }
        }
    }
#endif

    private void WriteRealtimePresence(string uid, bool online)
    {
        if (string.IsNullOrEmpty(uid) || !TryEnsureRealtimeDatabaseReady()) return;

        realtimeDb.GetReference(BuildPresencePath(uid))
            .SetValueAsync(BuildRealtimePresencePayload(online))
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[FriendOnlinePresenceManager] 写入 Realtime Database 在线状态失败: {task.Exception?.InnerException?.Message}");
                }
            });
    }

    private void WriteFirestorePresence(string uid, bool online)
    {
        if (string.IsNullOrEmpty(uid) || !TryEnsureFirestoreReady()) return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "isOnline", online },
            { "lastActiveUnixMs", now },
            { "lastActiveAt", FieldValue.ServerTimestamp },
            { "presenceUpdatedAt", FieldValue.ServerTimestamp },
        };

        firestoreDb.Collection("public_profiles")
            .Document(uid)
            .SetAsync(data, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[FriendOnlinePresenceManager] 镜像 Firestore 在线状态失败: {task.Exception?.InnerException?.Message}");
                }
            });
    }

    private static Dictionary<string, object> BuildRealtimePresencePayload(bool online)
    {
        return new Dictionary<string, object>
        {
            { "isOnline", online },
            { "lastActiveUnixMs", ServerValue.Timestamp },
            { "updatedAt", ServerValue.Timestamp },
        };
    }

    private static bool ResolveRealtimePresenceOnline(DataSnapshot snapshot, long lastActiveUnixMs)
    {
        if (snapshot == null || !snapshot.Exists) return false;

        bool explicitOnline = GetSnapshotBool(snapshot, "isOnline", false);
        return explicitOnline && (lastActiveUnixMs <= 0 || IsRecentlyOnline(lastActiveUnixMs));
    }

    private static bool IsRecentlyOnline(long unixMs)
    {
        if (unixMs <= 0) return false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return now - unixMs <= OnlinePresenceTimeoutMs;
    }

    private static long GetSnapshotUnixMs(DataSnapshot snapshot, string key)
    {
        if (snapshot == null || string.IsNullOrEmpty(key)) return 0;
        return GetLongValue(snapshot.Child(key)?.Value, 0);
    }

    private static bool GetSnapshotBool(DataSnapshot snapshot, string key, bool fallback)
    {
        if (snapshot == null || string.IsNullOrEmpty(key)) return fallback;
        return GetBoolValue(snapshot.Child(key)?.Value, fallback);
    }

    private static long GetLongValue(object value, long fallback)
    {
        if (value == null) return fallback;

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

    private static bool GetBoolValue(object value, bool fallback)
    {
        if (value == null) return fallback;

        try
        {
            if (value is bool boolValue) return boolValue;
            if (bool.TryParse(value.ToString(), out bool parsed)) return parsed;
        }
        catch
        {
            return fallback;
        }

        return fallback;
    }

    private static string BuildPresencePath(string uid)
    {
        return $"{PresenceRootPath}/{NormalizeUid(uid)}";
    }

    private static string NormalizeUid(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private class RealtimePresenceWatcher
    {
        private readonly DatabaseReference reference;
        private readonly EventHandler<ValueChangedEventArgs> handler;

        public RealtimePresenceWatcher(DatabaseReference reference, EventHandler<ValueChangedEventArgs> handler)
        {
            this.reference = reference;
            this.handler = handler;
        }

        public void Stop()
        {
            if (reference == null || handler == null) return;
            reference.ValueChanged -= handler;
        }
    }
}
