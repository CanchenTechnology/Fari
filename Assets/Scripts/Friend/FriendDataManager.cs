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
        public string info;
        public Sprite headSprite;
        public bool isVirtual; // true=虚拟好友, false=真实好友
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
        var data = new FriendData
        {
            id = nextId++,
            name = name,
            info = info,
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
        var data = new FriendData
        {
            id = nextId++,
            name = name,
            info = info,
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

    #endregion

    private void OnDestroy()
    {
        friendPool?.Clear();
        invitePool?.Clear();
    }
}
