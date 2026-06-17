using System.Collections.Generic;
using UnityEngine;
using XFGameFrameWork;

public class FriendDataManager : MonoSingleton<FriendDataManager>
{
    /// <summary>
    /// 好友数据模型
    /// </summary>
    public class FriendData
    {
        public int id;
        public string name;
        public string handle;
        public string info;
        public string relationship;
        public string birthday;
        public string birthTime;
        public string city;
        public string notes;
        public string source;
        public Sprite headSprite;
        public bool isVirtual; // true=虚拟好友, false=真实好友

        public string BuildOracleContext()
        {
            var parts = new List<string>();
            parts.Add($"姓名：{name}");
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
            name = name,
            handle = handle,
            info = info,
            relationship = "好友",
            source = source,
            headSprite = headSprite,
            isVirtual = false
        };
        realFriendList.Add(data);
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
        Sprite headSprite = null)
    {
        var data = new FriendData
        {
            id = nextId++,
            name = name,
            handle = string.Empty,
            info = BuildVirtualFriendInfo(relationship, birthday, city),
            relationship = relationship,
            birthday = birthday,
            birthTime = birthTime,
            city = city,
            notes = notes,
            source = "创建档案",
            headSprite = headSprite,
            isVirtual = true
        };
        virtualFriendList.Add(data);
        return data;
    }

    /// <summary>
    /// 添加邀请
    /// </summary>
    public InviteData AddInvite(string name, string info, Sprite headSprite = null)
    {
        var data = new InviteData
        {
            id = nextId++,
            name = name,
            info = info,
            headSprite = headSprite
        };
        inviteList.Add(data);
        return data;
    }

    /// <summary>
    /// 删除真实好友
    /// </summary>
    public bool RemoveRealFriend(int id)
    {
        return realFriendList.RemoveAll(d => d.id == id) > 0;
    }

    /// <summary>
    /// 删除虚拟好友
    /// </summary>
    public bool RemoveVirtualFriend(int id)
    {
        return virtualFriendList.RemoveAll(d => d.id == id) > 0;
    }

    /// <summary>
    /// 删除邀请
    /// </summary>
    public bool RemoveInvite(int id)
    {
        return inviteList.RemoveAll(d => d.id == id) > 0;
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void ClearAllData()
    {
        realFriendList.Clear();
        virtualFriendList.Clear();
        inviteList.Clear();
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

    #endregion

    private void OnDestroy()
    {
        friendPool?.Clear();
        invitePool?.Clear();
    }
}
