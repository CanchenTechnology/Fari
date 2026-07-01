using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFGameFrameWork;

/// <summary>
/// 登录成功后的静默预热。不要阻塞登录跳转，只在后台填充常用缓存。
/// </summary>
public class LoginPreloadService : MonoSingleton<LoginPreloadService>
{
    private const float FirestoreReadyTimeoutSeconds = 8f;
    private const float CloudRequestTimeoutSeconds = 12f;

    private Coroutine preloadRoutine;
    private Coroutine avatarPreloadRoutine;
    private int preloadVersion;
    private int avatarPreloadVersion;
    private bool isSubscribedToFriendData;

    protected override void Awake()
    {
        base.Awake();
        SubscribeFriendDataChanged();
    }

    private void OnDestroy()
    {
        UnsubscribeFriendDataChanged();
    }

    public void BeginAfterLogin(AuthProvider provider)
    {
        int version = ++preloadVersion;
        if (preloadRoutine != null)
            StopCoroutine(preloadRoutine);

        SubscribeFriendDataChanged();
        preloadRoutine = StartCoroutine(PreloadAfterLoginRoutine(version, provider));
    }

    private IEnumerator PreloadAfterLoginRoutine(int version, AuthProvider provider)
    {
        yield return null;

        WarmupStaticAssets();
        PreloadCurrentUserAvatar();

        FriendDataManager friendData = FriendDataManager.Instance;
        if (friendData != null)
            friendData.ReloadLocalDataForCurrentUser();

        QueueKnownAvatarPreload();

        FirestoreManager firestore = FirestoreManager.Instance;
        yield return WaitForFirestoreReady(firestore);
        if (version != preloadVersion)
            yield break;

        if (firestore != null && firestore.IsInitialized)
        {
            yield return RefreshCloudFriendData(firestore, version);
            WarmupHistoryCache();
            WarmupPublicConfig(firestore);
        }

        if (version == preloadVersion)
            preloadRoutine = null;

        Debug.Log($"[LoginPreloadService] 登录后静默预热完成: {provider}");
    }

    private void WarmupStaticAssets()
    {
        try
        {
            TarotSpriteLoader.Initialize();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LoginPreloadService] 预热塔罗图集失败: " + e.Message);
        }
    }

    private void PreloadCurrentUserAvatar()
    {
        UserDataManager userData = UserDataManager.Instance;
        if (userData == null || string.IsNullOrWhiteSpace(userData.PhotoUrl))
            return;

        StartCoroutine(FriendAvatarImageUtility.PreloadRemoteAvatarCoroutine(
            userData.FirebaseUid,
            userData.UserName,
            userData.PhotoUrl));

        StartCoroutine(FriendAvatarImageUtility.LoadCurrentUserAvatarCoroutine((_, __) => { }));
    }

    private IEnumerator RefreshCloudFriendData(FirestoreManager firestore, int version)
    {
        if (firestore == null || !firestore.IsInitialized)
            yield break;

        bool friendsDone = false;
        firestore.LoadFriends(_ => friendsDone = true);
        yield return WaitForCloudRequest(() => friendsDone);
        if (version != preloadVersion) yield break;
        QueueKnownAvatarPreload();

        bool requestsDone = false;
        firestore.LoadFriendRequests(_ => requestsDone = true);
        yield return WaitForCloudRequest(() => requestsDone);
        if (version != preloadVersion) yield break;
        QueueKnownAvatarPreload();

        bool virtualFriendsDone = false;
        firestore.LoadVirtualFriends(_ => virtualFriendsDone = true);
        yield return WaitForCloudRequest(() => virtualFriendsDone);
        if (version != preloadVersion) yield break;
        QueueKnownAvatarPreload();
    }

    private IEnumerator WaitForFirestoreReady(FirestoreManager firestore)
    {
        float elapsed = 0f;
        while (firestore != null
            && !firestore.IsInitialized
            && elapsed < FirestoreReadyTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForCloudRequest(Func<bool> isDone)
    {
        float elapsed = 0f;
        while (isDone != null
            && !isDone()
            && elapsed < CloudRequestTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void WarmupHistoryCache()
    {
        try
        {
            DivinationHistoryCacheService.Instance.Warmup(false);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LoginPreloadService] 预热占卜历史失败: " + e.Message);
        }
    }

    private void WarmupPublicConfig(FirestoreManager firestore)
    {
        if (firestore == null || !firestore.IsInitialized)
            return;

        try
        {
            firestore.LoadPublicAppConfig(_ => { });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LoginPreloadService] 预热公开配置失败: " + e.Message);
        }
    }

    private void HandleFriendDataChanged()
    {
        QueueKnownAvatarPreload();
    }

    private void QueueKnownAvatarPreload()
    {
        if (!isActiveAndEnabled)
            return;

        int version = ++avatarPreloadVersion;
        if (avatarPreloadRoutine != null)
            StopCoroutine(avatarPreloadRoutine);

        avatarPreloadRoutine = StartCoroutine(PreloadKnownAvatarsRoutine(version));
    }

    private IEnumerator PreloadKnownAvatarsRoutine(int version)
    {
        yield return null;

        List<AvatarPreloadEntry> entries = CollectKnownAvatarEntries();
        foreach (AvatarPreloadEntry entry in entries)
        {
            if (version != avatarPreloadVersion)
                yield break;

            if (entry == null || string.IsNullOrWhiteSpace(entry.photoUrl))
                continue;

            if (FriendAvatarImageUtility.TryResolveCachedRemoteAvatar(entry.uid, entry.photoUrl, out Sprite cachedSprite))
            {
                ApplyAvatarEntry(entry, cachedSprite);
                continue;
            }

            Sprite loadedSprite = null;
            yield return FriendAvatarImageUtility.PreloadRemoteAvatarCoroutine(
                entry.uid,
                entry.displayName,
                entry.photoUrl,
                sprite => loadedSprite = sprite);

            ApplyAvatarEntry(entry, loadedSprite);
            yield return null;
        }

        if (version == avatarPreloadVersion)
            avatarPreloadRoutine = null;
    }

    private List<AvatarPreloadEntry> CollectKnownAvatarEntries()
    {
        List<AvatarPreloadEntry> entries = new List<AvatarPreloadEntry>();
        HashSet<string> seenKeys = new HashSet<string>();

        UserDataManager userData = UserDataManager.Instance;
        if (userData != null)
            AddAvatarEntry(entries, seenKeys, userData.FirebaseUid, userData.PhotoUrl, null, null);

        FriendDataManager friendData = FriendDataManager.Instance;
        if (friendData == null)
            return entries;

        foreach (FriendDataManager.FriendData friend in friendData.RealFriendList)
            AddAvatarEntry(entries, seenKeys, GetFriendAvatarUid(friend), friend.photoUrl, friend, null);

        foreach (FriendDataManager.FriendData friend in friendData.VirtualFriendList)
            AddAvatarEntry(entries, seenKeys, GetFriendAvatarUid(friend), friend.photoUrl, friend, null);

        foreach (FriendDataManager.InviteData invite in friendData.InviteList)
            AddAvatarEntry(entries, seenKeys, invite.firebaseUid, invite.photoUrl, null, invite);

        return entries;
    }

    private static void AddAvatarEntry(
        List<AvatarPreloadEntry> entries,
        HashSet<string> seenKeys,
        string uid,
        string photoUrl,
        FriendDataManager.FriendData friend,
        FriendDataManager.InviteData invite)
    {
        if (entries == null || seenKeys == null || string.IsNullOrWhiteSpace(photoUrl))
            return;

        string key = $"{(uid ?? string.Empty).Trim().ToLowerInvariant()}|{photoUrl.Trim()}";
        if (!seenKeys.Add(key))
            return;

        entries.Add(new AvatarPreloadEntry
        {
            uid = uid,
            photoUrl = photoUrl,
            displayName = ResolveAvatarDisplayName(uid, friend, invite),
            friend = friend,
            invite = invite
        });
    }

    private static string ResolveAvatarDisplayName(string uid, FriendDataManager.FriendData friend, FriendDataManager.InviteData invite)
    {
        if (!string.IsNullOrWhiteSpace(friend?.name)) return friend.name.Trim();
        if (!string.IsNullOrWhiteSpace(invite?.name)) return invite.name.Trim();
        if (!string.IsNullOrWhiteSpace(invite?.email)) return invite.email.Trim();
        if (!string.IsNullOrWhiteSpace(UserDataManager.Instance?.UserName)) return UserDataManager.Instance.UserName.Trim();
        return string.IsNullOrWhiteSpace(uid) ? "未知用户" : uid.Trim();
    }

    private static void ApplyAvatarEntry(AvatarPreloadEntry entry, Sprite sprite)
    {
        if (entry == null || sprite == null)
            return;

        if (entry.friend != null)
            entry.friend.headSprite = sprite;
        if (entry.invite != null)
            entry.invite.headSprite = sprite;
    }

    private static string GetFriendAvatarUid(FriendDataManager.FriendData friend)
    {
        if (friend == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(friend.firebaseUid)) return friend.firebaseUid;
        if (!string.IsNullOrWhiteSpace(friend.virtualFriendId)) return friend.virtualFriendId;
        return friend.id > 0 ? friend.id.ToString() : string.Empty;
    }

    private void SubscribeFriendDataChanged()
    {
        if (isSubscribedToFriendData || FriendDataManager.Instance == null)
            return;

        FriendDataManager.Instance.DataChanged += HandleFriendDataChanged;
        isSubscribedToFriendData = true;
    }

    private void UnsubscribeFriendDataChanged()
    {
        if (!isSubscribedToFriendData || FriendDataManager.Instance == null)
            return;

        FriendDataManager.Instance.DataChanged -= HandleFriendDataChanged;
        isSubscribedToFriendData = false;
    }

    private class AvatarPreloadEntry
    {
        public string uid;
        public string displayName;
        public string photoUrl;
        public FriendDataManager.FriendData friend;
        public FriendDataManager.InviteData invite;
    }
}
