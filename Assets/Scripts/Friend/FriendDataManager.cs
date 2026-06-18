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
        public string avatarImagePath;
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
        public string avatarImagePath;
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

    /// <summary>
    /// 删除虚拟好友
    /// </summary>
    public bool RemoveVirtualFriend(int id)
    {
        bool removed = virtualFriendList.RemoveAll(d => d.id == id) > 0;
        if (removed) SaveAndNotify();
        return removed;
    }

    public FriendData UpsertVirtualFriendFromFirebase(
        string virtualFriendId,
        string name,
        string relationship,
        string birthday,
        string birthTime,
        string city,
        string notes,
        Sprite headSprite = null)
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

    public FriendData UpsertRealFriendFromFirebase(
        string firebaseUid,
        string name,
        string handle,
        string info,
        Sprite headSprite = null,
        string source = "Firebase")
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
            invites = BuildInviteRecords(inviteList)
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
            nextId = Mathf.Max(FindMaxId() + 1, saveData.nextId);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[FriendDataManager] 本地好友数据加载失败，将继续使用空数据。{ex.Message}");
            realFriendList.Clear();
            virtualFriendList.Clear();
            inviteList.Clear();
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
                avatarImagePath = friend.avatarImagePath,
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
                avatarImagePath = record.avatarImagePath,
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
