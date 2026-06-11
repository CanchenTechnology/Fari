using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用 GameObject 对象池
/// 用法：通过 prefab 创建池，Get 获取对象，Release 回收对象
/// </summary>
public class SimpleObjectPool
{
    private readonly GameObject prefab;
    private readonly Transform poolRoot;
    private readonly Queue<GameObject> poolQueue = new Queue<GameObject>();
    private readonly List<GameObject> activeList = new List<GameObject>();

    /// <summary>
    /// 当前活跃（正在使用）的对象列表
    /// </summary>
    public IReadOnlyList<GameObject> ActiveObjects => activeList;

    /// <summary>
    /// 池中可用对象数量
    /// </summary>
    public int CountInactive => poolQueue.Count;

    /// <summary>
    /// 当前活跃对象数量
    /// </summary>
    public int CountActive => activeList.Count;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="prefab">预制体</param>
    /// <param name="poolRoot">池对象挂载的父节点（可传 null，会自动创建）</param>
    /// <param name="preloadCount">预加载数量</param>
    public SimpleObjectPool(GameObject prefab, Transform poolRoot = null, int preloadCount = 0)
    {
        this.prefab = prefab;
        this.poolRoot = poolRoot;

        for (int i = 0; i < preloadCount; i++)
        {
            GameObject obj = CreateNewObject();
            obj.SetActive(false);
            poolQueue.Enqueue(obj);
        }
    }

    /// <summary>
    /// 从池中获取一个对象
    /// </summary>
    /// <param name="parent">激活时挂载的父节点</param>
    public GameObject Get(Transform parent = null)
    {
        GameObject obj;

        if (poolQueue.Count > 0)
        {
            obj = poolQueue.Dequeue();
        }
        else
        {
            obj = CreateNewObject();
        }

        if (parent != null)
        {
            obj.transform.SetParent(parent, false);
        }

        obj.SetActive(true);
        activeList.Add(obj);
        return obj;
    }

    /// <summary>
    /// 回收一个对象到池中
    /// </summary>
    public void Release(GameObject obj)
    {
        if (obj == null) return;

        if (!activeList.Contains(obj)) return;

        activeList.Remove(obj);
        obj.SetActive(false);
        obj.transform.SetParent(poolRoot, false);
        poolQueue.Enqueue(obj);
    }

    /// <summary>
    /// 回收所有活跃对象
    /// </summary>
    public void ReleaseAll()
    {
        while (activeList.Count > 0)
        {
            Release(activeList[activeList.Count - 1]);
        }
    }

    /// <summary>
    /// 清空池（销毁所有缓存和活跃对象）
    /// </summary>
    public void Clear()
    {
        foreach (var obj in activeList)
        {
            if (obj != null) Object.Destroy(obj);
        }
        activeList.Clear();

        while (poolQueue.Count > 0)
        {
            var obj = poolQueue.Dequeue();
            if (obj != null) Object.Destroy(obj);
        }
    }

    private GameObject CreateNewObject()
    {
        GameObject obj = Object.Instantiate(prefab);
        obj.name = prefab.name;
        return obj;
    }
}
