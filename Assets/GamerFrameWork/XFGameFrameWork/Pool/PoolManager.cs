using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//**********************************************
//创建人：玖一
//功能说明：
//使用方法：
//①创建池子:PoolManager.Instance.CreatePool("Bullet", bulletPrefab, 20);
//②获取对象:var bullet = PoolManager.Instance.GetPool<Bullet>("Bullet").Get();
//③释放对象:PoolManager.Instance.GetPool<Bullet>("Bullet").Release(this);
//**********************************************
namespace XFGameFrameWork.Pool
{
    public class PoolManager : MonoSingleton<PoolManager>
    {
        //obj实际上放的是Stack<T>
        private Dictionary<string,object> _pools = new Dictionary<string,object>();
        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(this);  
        }
        public Pool<T> CreatePool<T>(string key, T prefab, int initCount = 5, Transform parent = null) where T : Component, IPooable
        {
            if (_pools.ContainsKey(key)) return _pools[key] as Pool<T>;
            var pool = new Pool<T>(prefab,initCount,parent);
            _pools[key] = pool;
            return pool;
        }
        public Pool<T> GetPool<T>(string key) where T : Component, IPooable
        {
            if (_pools.TryGetValue(key, out var pool))
            {
                return pool as Pool<T>;
            }
            return null;
        }
        public void ClearAll()
        {
            foreach (var pool in _pools.Values)
            {
                var method = pool.GetType().GetMethod("Clear");
                method?.Invoke(pool, null);
            }
            _pools.Clear();
        }

    }
}


