using System;
using System.Collections.Generic;
using UnityEngine;
using XFGameFrameWork;

public class FriendDataManager : MonoSingleton<FriendDataManager>
{
    private const string FRIENDS_STORAGE_KEY = "Fari_FriendData_v1";

    public event Action DataChanged;

    [Serializable]
    private class FriendSaveData
    {
        public int nextId = 1;
        public List<FriendRecord> realFriends = new List<FriendRecord>();
        public List<FriendRecord> virtualFriends = new List<FriendRecord>();
        public List<InviteRecord> invites = new List<InviteRecord>();
        public List<string> blockedUserIds = new List<string>();
    }

    [Serializable]
    private class FriendRecord
    {
        public int id;
        public string firebaseUid;
        public string virtualFriendId;
        public string name;
        public string handle;
        public string info;
        public string relationship;
        public string birthday;
        public string birthTime;
        public string city;
        public string notes;
        public string source;
        public string photoUrl;
        public string avatarImagePath;
        public string avatarStoragePath;
        public bool isVirtual;
    }

    [Serializable]
    private class InviteRecord
    {
        public int id;
        public string firebaseUid;
        public string email;
        public string photoUrl;
        public string status;
        public string name;
        public string info;
    }

    /// <summary>
    /// 好友数据模型
    /// </summary>
    public class FriendData
    {
        public int id;
        public string firebaseUid;
        public string virtualFriendId;
        public string name;
        public string handle;
        public string info;
        public string relationship;
        public string birthday;
        public string birthTime;
        public string city;
        public string notes;
        public string source;
        public string photoUrl;
        public string avatarImagePath;
        public string avatarStoragePath;
        public Sprite headSprite;
        public bool isVirtual; // true=虚拟好友, false=真实好友

        public string BuildOracleContext()
        {
            var parts = new List<string>();
            parts.Add($"姓名：{name}");
            if (!string.IsNullOrWhiteSpace(firebaseUid)) parts.Add($"Firebase UID：{firebaseUid}");
            if (!string.IsNullOrWhiteSpace(virtualFriendId)) parts.Add($"虚拟好友ID：{virtualFriendId}");
            if (!string.IsNullOrWhiteSpace(handle)) parts.Add($"用户名：{handle}");
            if (!string.IsNullOrWhiteSpace(relationship)) parts.Add($"关系：{relationship}");
            if (!string.IsNullOrWhiteSpace(birthday)) parts.Add($"生日：{birthday}");
            if (!string.IsNullOrWhiteSpace(birthTime)) parts.Add($"出生时间：{birthTime}");
            if (!string.IsNullOrWhiteSpace(city)) parts.Add($"城市：{city}");
            if (!string.IsNullOrWhiteSpace(notes)) parts.Add($"背景：{notes}");
            parts.Add(isVirtual ? "类型：创建的好友档案" : "类型：真实好友");
            return string.Join("\n", parts);
        }
    }

    /// <summary>
    /// 邀请数据模型
    /// </summary>
    public class InviteData
    {
        public int id;
        public string firebaseUid;
        public string email;
        public string photoUrl;
        public string status;
        public string name;
        public string info;
        public Sprite headSprite;
    }

    // 对象池
    private SimpleObjectPool friendPool;
    private SimpleObjectPool invitePool;

    // 数据列表
    private List<FriendData> realFriendList = new List<FriendData>();
    private List<FriendData> virtualFriendList = new List<FriendData>();
    private List<InviteData> inviteList = new List<InviteData>();
    private List<string> blockedUserIdList = new List<string>();

    // 自增ID
    private int nextId = 1;

    protected override void Awake()
    {
        base.Awake();
        LoadLocalData();
    }

    /// <summary>
    /// 真实好友数据列表
    /// </summary>
    public IReadOnlyList<FriendData> RealFriendList => realFriendList;

    /// <summary>
    /// 虚拟好友数据列表
    /// </summary>
    public IReadOnlyList<FriendData> VirtualFriendList => virtualFriendList;

    /// <summary>
    /// 邀请数据列表
    /// </summary>
    public IReadOnlyList<InviteData> InviteList => inviteList;

    public IReadOnlyList<string> BlockedUserIds => blockedUserIdList;

    /// <summary>
    /// 初始化对象池（由 FriendUI 在首次使用时调用）
    /// </summary>
    public void InitPools(GameObject friendPrefab, GameObject invitePrefab, Transform uiTransform)
    {
        if (friendPool == null)
        {
            GameObject poolRootGO = new GameObject("FriendPoolRoot");
            poolRootGO.SetActive(false);
            Transform poolRoot = poolRootGO.transform;
            poolRoot.SetParent(uiTransform);
            friendPool = new SimpleObjectPool(friendPrefab, poolRoot);
        }
        if (invitePool == null)
        {
            GameObject poolRootGO = new GameObject("InvitePoolRoot");
            poolRootGO.SetActive(false);
            Transform poolRoot = poolRootGO.transform;
            poolRoot.SetParent(uiTransform);
            invitePool = new SimpleObjectPool(invitePrefab, poolRoot);
        }
    }

    #region 对象池操作

    /// <summary>
    /// 从好友池中获取一个 FriendItem GameObject
    /// </summary>
    public GameObject GetFriendItem(Transform parent)
    {
        return friendPool.Get(parent);
    }

    /// <summary>
    /// 从邀请池中获取一个 InviteItem GameObject
    /// </summary>
    public GameObject GetInviteItem(Transform parent)
    {
        return invitePool.Get(parent);
    }

    /// <summary>
    /// 回收一个 FriendItem GameObject
    /// </summary>
    public void ReleaseFriendItem(GameObject obj)
    {
        friendPool.Release(obj);
    }

    /// <summary>
    /// 回收一个 InviteItem GameObject
    /// </summary>
    public void ReleaseInviteItem(GameObject obj)
    {
        invitePool.Release(obj);
    }

    /// <summary>
    /// 回收所有好友对象
    /// </summary>
    public void ReleaseAllFriendItems()
    {
        friendPool.ReleaseAll();
    }

    /// <summary>
    /// 回收所有邀请对象
    /// </summary>
    public void ReleaseAllInviteItems()
    {
        invitePool.ReleaseAll();
    }

    #endregion

    #region 数据操作

    /// <summary>
    /// 添加真实好友
    /// </summary>
    public FriendData AddRealFriend(string name, string info, Sprite headSprite = null)
    {
        return AddRealFriend(name, string.Empty, info, headSprite, "用户名搜索");
    }

    public FriendData AddRealFriend(string name, string handle, string info, Sprite headSprite = null, string source = "用户名搜索")
    {
        var existing = FindRealFriendByHandleOrName(handle, name);
        if (existing != null) return existing;

        var data = new FriendData
        {
            id = nextId++,
            firebaseUid = string.Empty,
            virtualFriendId = string.Empty,
            name = name,
            handle = handle,
            info = info,
            relationship = "好友",
            source = source,
            headSprite = headSprite,
            isVirtual = false
        };
        realFriendList.Add(data);
        SaveAndNotify();
        return data;
    }

    public bool EnsureDebugRealFriends()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool changed = false;
        changed |= UpsertDebugRealFriend("test_real_friend_luna_001", "Luna 测试好友", "@luna.test", "真实好友测试账号 · 双人占卜流程", "好友");
        changed |= UpsertDebugRealFriend("test_real_friend_orion_002", "Orion 测试好友", "@orion.test", "真实好友测试账号 · 邀请/等待测试", "同事");
        changed |= UpsertDebugRealFriend("test_real_friend_mira_003", "Mira 测试好友", "@mira.test", "真实好友测试账号 · 结果页测试", "朋友");

        if (changed)
            SaveAndNotify();
        return changed;
#else
        return false;
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool UpsertDebugRealFriend(string firebaseUid, string name, string handle, string info, string relationship)
    {
        FriendData existing = FindRealFriendByFirebaseUid(firebaseUid);
        if (existing == null)
            existing = FindRealFriendByHandleOrName(handle, name);

        if (existing == null)
        {
            realFriendList.Add(new FriendData
            {
                id = nextId++,
                firebaseUid = firebaseUid,
                virtualFriendId = string.Empty,
                name = name,
                handle = handle,
                info = info,
                relationship = relationship,
                birthday = string.Empty,
                birthTime = string.Empty,
                city = string.Empty,
                notes = string.Empty,
                source = "本地测试数据",
                photoUrl = string.Empty,
                avatarImagePath = string.Empty,
                avatarStoragePath = string.Empty,
                isVirtual = false
            });
            return true;
        }

        bool changed = false;
        changed |= SetIfDifferent(ref existing.firebaseUid, firebaseUid);
        changed |= SetIfDifferent(ref existing.virtualFriendId, string.Empty);
        changed |= SetIfDifferent(ref existing.name, name);
        changed |= SetIfDifferent(ref existing.handle, handle);
        changed |= SetIfDifferent(ref existing.info, info);
        changed |= SetIfDifferent(ref existing.relationship, relationship);
        changed |= SetIfDifferent(ref existing.source, "本地测试数据");
        if (existing.isVirtual)
        {
            existing.isVirtual = false;
            changed = true;
        }
        return changed;
    }

    private static bool SetIfDifferent(ref string target, string value)
    {
        value ??= string.Empty;
        if (target == value) return false;
        target = value;
        return true;
    }
#endif

    /// <summary>
    /// 添加虚拟好友
    /// </summary>
    public FriendData AddVirtualFriend(string name, string info, Sprite headSprite = null)
    {
        return AddVirtualFriend(name, "好友", string.Empty, string.Empty, string.Empty, info, headSprite);
    }

    public FriendData AddVirtualFriend(
        string name,
        string relationship,
        string birthday,
        string birthTime,
        string city,
        string notes,
        Sprite headSprite = null,
        string avatarImagePath = "")
    {
        var data = new FriendData
        {
            id = nextId++,
            firebaseUid = string.Empty,
            virtualFriendId = Guid.NewGuid().ToString("N"),
            name = name,
            handle = string.Empty,
            info = BuildVirtualFriendInfo(relationship, birthday, city),
            relationship = relationship,
            birthday = birthday,
            birthTime = birthTime,
            city = city,
            notes = notes,
            source = "创建档案",
            avatarImagePath = avatarImagePath ?? string.Empty,
            avatarStoragePath = string.Empty,
            headSprite = headSprite,
            isVirtual = true
        };
        virtualFriendList.Add(data);
        SaveAndNotify();
        return data;
    }

    /// <summary>
    /// 添加邀请
    /// </summary>
    public InviteData AddInvite(string name, string info, Sprite headSprite = null)
    {
        return UpsertInvite(string.Empty, name, string.Empty, string.Empty, "pendingReceived", info, headSprite);
    }

    public InviteData UpsertInvite(
        string firebaseUid,
        string name,
        string email,
        string photoUrl,
        string status,
        string info,
        Sprite headSprite = null)
    {
        InviteData existing = FindInviteByFirebaseUid(firebaseUid);
        if (existing != null)
        {
            existing.name = name;
            existing.email = email;
            existing.photoUrl = photoUrl;
            existing.status = status;
            existing.info = info;
            if (headSprite != null) existing.headSprite = headSprite;
            SaveAndNotify();
            return existing;
        }

        var data = new InviteData
        {
            id = nextId++,
            firebaseUid = firebaseUid,
            email = email,
            photoUrl = photoUrl,
            status = status,
            name = name,
            info = info,
            headSprite = headSprite
        };
        inviteList.Add(data);
        SaveAndNotify();
        return data;
    }

    /// <summary>
    /// 删除真实好友
    /// </summary>
    public bool RemoveRealFriend(int id)
    {
        bool removed = realFriendList.RemoveAll(d => d.id == id) > 0;
        if (removed) SaveAndNotify();
        return removed;
    }

    public bool RemoveRealFriendByFirebaseUid(string firebaseUid)
    {
        string normalizedUid = NormalizeKey(firebaseUid);
        if (string.IsNullOrEmpty(normalizedUid)) return false;

        bool removed = realFriendList.RemoveAll(d => NormalizeKey(d.firebaseUid) == normalizedUid) > 0;
        if (removed) SaveAndNotify();
        return removed;
    }

    /// <summary>
    /// 删除虚拟好友
    /// </summary>
    public bool RemoveVirtualFriend(int id)
    {
        bool removed = virtualFriendList.RemoveAll(d => d.id == id) > 0;
        if (removed) SaveAndNotify();
        return removed;
    }

    public bool RemoveVirtualFriendById(string virtualFriendId)
    {
        string normalizedId = NormalizeKey(virtualFriendId);
        if (string.IsNullOrEmpty(normalizedId)) return false;

        bool removed = virtualFriendList.RemoveAll(d => NormalizeKey(d.virtualFriendId) == normalizedId) > 0;
        if (removed) SaveAndNotify();
        return removed;
    }

    public bool UpdateVirtualFriend(
        FriendData virtualFriend,
        string name,
        string relationship,
        string birthday,
        string birthTime,
        string city,
        string notes,
        Sprite headSprite = null,
        string avatarImagePath = null,
        string photoUrl = null,
        string avatarStoragePath = null)
    {
        if (virtualFriend == null || !virtualFriend.isVirtual) return false;

        FriendData existing = FindVirtualFriendById(virtualFriend.virtualFriendId);
        if (existing == null)
        {
            existing = virtualFriendList.Find(d => d.id == virtualFriend.id);
        }
        if (existing == null) return false;

        existing.name = string.IsNullOrWhiteSpace(name) ? "未命名好友" : name.Trim();
        existing.relationship = string.IsNullOrWhiteSpace(relationship) ? "好友" : relationship.Trim();
        existing.birthday = birthday?.Trim() ?? string.Empty;
        existing.birthTime = birthTime?.Trim() ?? string.Empty;
        existing.city = city?.Trim() ?? string.Empty;
        existing.notes = notes?.Trim() ?? string.Empty;
        existing.info = BuildVirtualFriendInfo(existing.relationship, existing.birthday, existing.city);
        existing.source = string.IsNullOrWhiteSpace(existing.source) ? "创建档案" : existing.source;

        if (avatarImagePath != null)
            existing.avatarImagePath = avatarImagePath;
        if (photoUrl != null)
            existing.photoUrl = photoUrl;
        if (avatarStoragePath != null)
            existing.avatarStoragePath = avatarStoragePath;
        if (headSprite != null)
            existing.headSprite = headSprite;

        virtualFriend.name = existing.name;
        virtualFriend.relationship = existing.relationship;
        virtualFriend.birthday = existing.birthday;
        virtualFriend.birthTime = existing.birthTime;
        virtualFriend.city = existing.city;
        virtualFriend.notes = existing.notes;
        virtualFriend.info = existing.info;
        virtualFriend.source = existing.source;
        virtualFriend.avatarImagePath = existing.avatarImagePath;
        virtualFriend.photoUrl = existing.photoUrl;
        virtualFriend.avatarStoragePath = existing.avatarStoragePath;
        virtualFriend.headSprite = existing.headSprite;

        SaveAndNotify();
        return true;
    }

    public bool SetVirtualFriendCloudAvatar(
        FriendData virtualFriend,
        string photoUrl,
        string avatarStoragePath,
        Sprite previewSprite = null)
    {
        if (virtualFriend == null || !virtualFriend.isVirtual) return false;

        FriendData existing = FindVirtualFriendById(virtualFriend.virtualFriendId);
        if (existing == null)
        {
            existing = virtualFriendList.Find(d => d.id == virtualFriend.id);
        }
        if (existing == null) return false;

        existing.photoUrl = photoUrl ?? string.Empty;
        existing.avatarStoragePath = avatarStoragePath ?? string.Empty;
        if (previewSprite != null) existing.headSprite = previewSprite;

        virtualFriend.photoUrl = existing.photoUrl;
        virtualFriend.avatarStoragePath = existing.avatarStoragePath;
        if (previewSprite != null) virtualFriend.headSprite = previewSprite;

        SaveAndNotify();
        return true;
    }

    public FriendData UpsertVirtualFriendFromFirebase(
        string virtualFriendId,
        string name,
        string relationship,
        string birthday,
        string birthTime,
        string city,
        string notes,
        Sprite headSprite = null,
        string photoUrl = "",
        string avatarStoragePath = "")
    {
        FriendData existing = FindVirtualFriendById(virtualFriendId);
        if (existing == null)
        {
            existing = new FriendData
            {
                id = nextId++,
                firebaseUid = string.Empty,
                virtualFriendId = virtualFriendId,
                name = name,
                handle = string.Empty,
                info = BuildVirtualFriendInfo(relationship, birthday, city),
                relationship = relationship,
                birthday = birthday,
                birthTime = birthTime,
                city = city,
                notes = notes,
                source = "Firebase 档案",
                photoUrl = photoUrl ?? string.Empty,
                avatarStoragePath = avatarStoragePath ?? string.Empty,
                headSprite = headSprite,
                isVirtual = true
            };
            virtualFriendList.Add(existing);
        }
        else
        {
            existing.name = name;
            existing.relationship = relationship;
            existing.birthday = birthday;
            existing.birthTime = birthTime;
            existing.city = city;
            existing.notes = notes;
            existing.info = BuildVirtualFriendInfo(relationship, birthday, city);
            existing.source = "Firebase 档案";
            existing.photoUrl = photoUrl ?? string.Empty;
            existing.avatarStoragePath = avatarStoragePath ?? string.Empty;
            if (headSprite != null) existing.headSprite = headSprite;
            existing.isVirtual = true;
        }

        SaveAndNotify();
        return existing;
    }

    /// <summary>
    /// 删除邀请
    /// </summary>
    public bool RemoveInvite(int id)
    {
        bool removed = inviteList.RemoveAll(d => d.id == id) > 0;
        if (removed) SaveAndNotify();
        return removed;
    }

    public bool RemoveInviteByFirebaseUid(string firebaseUid)
    {
        string normalizedUid = NormalizeKey(firebaseUid);
        bool removed = inviteList.RemoveAll(d => NormalizeKey(d.firebaseUid) == normalizedUid) > 0;
        if (removed) SaveAndNotify();
        return removed;
    }

    public bool AddBlockedUser(string firebaseUid)
    {
        string normalizedUid = NormalizeKey(firebaseUid);
        if (string.IsNullOrEmpty(normalizedUid)) return false;

        if (!blockedUserIdList.Exists(uid => NormalizeKey(uid) == normalizedUid))
            blockedUserIdList.Add(firebaseUid);

        RemoveRealFriendByFirebaseUid(firebaseUid);
        RemoveInviteByFirebaseUid(firebaseUid);
        SaveAndNotify();
        return true;
    }

    public bool RemoveBlockedUser(string firebaseUid)
    {
        string normalizedUid = NormalizeKey(firebaseUid);
        if (string.IsNullOrEmpty(normalizedUid)) return false;

        bool removed = blockedUserIdList.RemoveAll(uid => NormalizeKey(uid) == normalizedUid) > 0;
        if (removed) SaveAndNotify();
        return removed;
    }

    public bool IsUserBlocked(string firebaseUid)
    {
        string normalizedUid = NormalizeKey(firebaseUid);
        if (string.IsNullOrEmpty(normalizedUid)) return false;
        return blockedUserIdList.Exists(uid => NormalizeKey(uid) == normalizedUid);
    }

    public FriendData UpsertRealFriendFromFirebase(
        string firebaseUid,
        string name,
        string handle,
        string info,
        Sprite headSprite = null,
        string source = "Firebase",
        string photoUrl = "")
    {
        FriendData existing = FindRealFriendByFirebaseUid(firebaseUid);
        if (existing == null)
        {
            existing = FindRealFriendByHandleOrName(handle, name);
        }

        if (existing == null)
        {
            existing = new FriendData
            {
                id = nextId++,
                firebaseUid = firebaseUid,
                name = name,
                handle = handle,
                info = info,
                relationship = "好友",
                source = source,
                photoUrl = photoUrl ?? string.Empty,
                headSprite = headSprite,
                isVirtual = false
            };
            realFriendList.Add(existing);
        }
        else
        {
            existing.firebaseUid = firebaseUid;
            existing.name = name;
            existing.handle = handle;
            existing.info = info;
            existing.source = source;
            if (!string.IsNullOrWhiteSpace(photoUrl)) existing.photoUrl = photoUrl;
            if (headSprite != null) existing.headSprite = headSprite;
            existing.isVirtual = false;
        }

        SaveAndNotify();
        return existing;
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void ClearAllData()
    {
        realFriendList.Clear();
        virtualFriendList.Clear();
        inviteList.Clear();
        blockedUserIdList.Clear();
        nextId = 1;
        SaveAndNotify();
    }

    /// <summary>
    /// 保存好友与邀请数据到本地。
    /// Sprite 不进入本地序列化，后续接入头像上传/资源ID时可在记录中扩展 avatarKey。
    /// </summary>
    public void SaveLocalData()
    {
        FriendSaveData saveData = new FriendSaveData
        {
            nextId = nextId,
            realFriends = BuildFriendRecords(realFriendList),
            virtualFriends = BuildFriendRecords(virtualFriendList),
            invites = BuildInviteRecords(inviteList),
            blockedUserIds = new List<string>(blockedUserIdList)
        };

        PlayerPrefs.SetString(FRIENDS_STORAGE_KEY, JsonUtility.ToJson(saveData));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 从本地恢复好友与邀请数据。
    /// </summary>
    public void LoadLocalData()
    {
        string json = PlayerPrefs.GetString(FRIENDS_STORAGE_KEY, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            FriendSaveData saveData = JsonUtility.FromJson<FriendSaveData>(json);
            if (saveData == null) return;

            realFriendList = BuildFriendData(saveData.realFriends);
            virtualFriendList = BuildFriendData(saveData.virtualFriends);
            inviteList = BuildInviteData(saveData.invites);
            blockedUserIdList = saveData.blockedUserIds ?? new List<string>();
            nextId = Mathf.Max(FindMaxId() + 1, saveData.nextId);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FriendDataManager] 本地好友数据加载失败，将继续使用空数据。{ex.Message}");
            realFriendList.Clear();
            virtualFriendList.Clear();
            inviteList.Clear();
            blockedUserIdList.Clear();
            nextId = 1;
        }
    }

    public FriendData FindRealFriendByHandleOrName(string handle, string name)
    {
        string normalizedHandle = NormalizeKey(handle);
        string normalizedName = NormalizeKey(name);

        foreach (var friend in realFriendList)
        {
            if (!string.IsNullOrEmpty(normalizedHandle) && NormalizeKey(friend.handle) == normalizedHandle)
            {
                return friend;
            }
            if (!string.IsNullOrEmpty(normalizedName) && NormalizeKey(friend.name) == normalizedName)
            {
                return friend;
            }
        }

        return null;
    }

    public FriendData FindRealFriendByFirebaseUid(string firebaseUid)
    {
        string normalizedUid = NormalizeKey(firebaseUid);
        if (string.IsNullOrEmpty(normalizedUid)) return null;

        foreach (var friend in realFriendList)
        {
            if (NormalizeKey(friend.firebaseUid) == normalizedUid)
            {
                return friend;
            }
        }

        return null;
    }

    public FriendData FindVirtualFriendById(string virtualFriendId)
    {
        string normalizedId = NormalizeKey(virtualFriendId);
        if (string.IsNullOrEmpty(normalizedId)) return null;

        foreach (var friend in virtualFriendList)
        {
            if (NormalizeKey(friend.virtualFriendId) == normalizedId)
            {
                return friend;
            }
        }

        return null;
    }

    public InviteData FindInviteByFirebaseUid(string firebaseUid)
    {
        string normalizedUid = NormalizeKey(firebaseUid);
        if (string.IsNullOrEmpty(normalizedUid)) return null;

        foreach (var invite in inviteList)
        {
            if (NormalizeKey(invite.firebaseUid) == normalizedUid)
            {
                return invite;
            }
        }

        return null;
    }

    private string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private string BuildVirtualFriendInfo(string relationship, string birthday, string city)
    {
        var parts = new List<string>();
        parts.Add(string.IsNullOrWhiteSpace(relationship) ? "创建好友" : relationship.Trim());
        if (!string.IsNullOrWhiteSpace(birthday)) parts.Add(birthday.Trim());
        if (!string.IsNullOrWhiteSpace(city)) parts.Add(city.Trim());
        return string.Join(" · ", parts);
    }

    private void SaveAndNotify()
    {
        SaveLocalData();
        DataChanged?.Invoke();
    }

    private List<FriendRecord> BuildFriendRecords(List<FriendData> source)
    {
        List<FriendRecord> records = new List<FriendRecord>();
        foreach (FriendData friend in source)
        {
            records.Add(new FriendRecord
            {
                id = friend.id,
                firebaseUid = friend.firebaseUid,
                virtualFriendId = friend.virtualFriendId,
                name = friend.name,
                handle = friend.handle,
                info = friend.info,
                relationship = friend.relationship,
                birthday = friend.birthday,
                birthTime = friend.birthTime,
                city = friend.city,
                notes = friend.notes,
                source = friend.source,
                photoUrl = friend.photoUrl,
                avatarImagePath = friend.avatarImagePath,
                avatarStoragePath = friend.avatarStoragePath,
                isVirtual = friend.isVirtual
            });
        }

        return records;
    }

    private List<InviteRecord> BuildInviteRecords(List<InviteData> source)
    {
        List<InviteRecord> records = new List<InviteRecord>();
        foreach (InviteData invite in source)
        {
            records.Add(new InviteRecord
            {
                id = invite.id,
                firebaseUid = invite.firebaseUid,
                email = invite.email,
                photoUrl = invite.photoUrl,
                status = invite.status,
                name = invite.name,
                info = invite.info
            });
        }

        return records;
    }

    private List<FriendData> BuildFriendData(List<FriendRecord> records)
    {
        List<FriendData> friends = new List<FriendData>();
        if (records == null) return friends;

        foreach (FriendRecord record in records)
        {
            friends.Add(new FriendData
            {
                id = record.id,
                firebaseUid = record.firebaseUid,
                virtualFriendId = record.virtualFriendId,
                name = record.name,
                handle = record.handle,
                info = record.info,
                relationship = record.relationship,
                birthday = record.birthday,
                birthTime = record.birthTime,
                city = record.city,
                notes = record.notes,
                source = record.source,
                photoUrl = record.photoUrl,
                avatarImagePath = record.avatarImagePath,
                avatarStoragePath = record.avatarStoragePath,
                headSprite = FriendAvatarImageUtility.LoadSpriteFromPath(record.avatarImagePath),
                isVirtual = record.isVirtual
            });
        }

        return friends;
    }

    private List<InviteData> BuildInviteData(List<InviteRecord> records)
    {
        List<InviteData> invites = new List<InviteData>();
        if (records == null) return invites;

        foreach (InviteRecord record in records)
        {
            invites.Add(new InviteData
            {
                id = record.id,
                firebaseUid = record.firebaseUid,
                email = record.email,
                photoUrl = record.photoUrl,
                status = record.status,
                name = record.name,
                info = record.info,
                headSprite = null
            });
        }

        return invites;
    }

    private int FindMaxId()
    {
        int maxId = 0;
        foreach (FriendData friend in realFriendList) maxId = Mathf.Max(maxId, friend.id);
        foreach (FriendData friend in virtualFriendList) maxId = Mathf.Max(maxId, friend.id);
        foreach (InviteData invite in inviteList) maxId = Mathf.Max(maxId, invite.id);
        return maxId;
    }

    #endregion

    private void OnDestroy()
    {
        friendPool?.Clear();
        invitePool?.Clear();
    }
}
