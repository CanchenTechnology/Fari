using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//**********************************************
//创建人：玖一
//功能说明：
//**********************************************
namespace XFGameFrameWork.Pool
{
    public class Pool<T> where T : Component, IPooable
    {
        private Stack<T> _stack = new Stack<T>();
        private T _prefab;
        private Transform _root;
        private Action<T> _onGet;
        private Action<T> _onRelease;
        public Pool(T prefab,int initCount,Transform parent=null,Action<T> onGet=null,Action<T> onRelease=null)
        {
            _prefab = prefab;
            _root = new GameObject($"{typeof(T).Name}_Pool").transform;
            if(parent!=null) _root.SetParent(parent);
            _onGet = onGet;
            _onRelease = onRelease;
            for (int i = 0; i < initCount; i++)
            {
                var obj = GameObject.Instantiate(_prefab,_root);
                obj.gameObject.SetActive(false);
                _stack.Push(obj);
            }
        }
        public T Get()
        {
            T item = _stack.Count>0?_stack.Pop():GameObject.Instantiate(_prefab,_root);
            item.gameObject.SetActive(true);
            item.OnSpawn();
            _onGet?.Invoke(item);
            return item;
        }
        public void Release(T item)
        {
            item.OnRecycle();
            item.gameObject.SetActive(false);
            item.transform.SetParent(_root);
            _onRelease?.Invoke(item);
            _stack.Push(item);
        }
        public void Clear()
        {
            foreach (var item in _stack)
                GameObject.Destroy(item.gameObject);
            _stack.Clear(); 
        }

    }
}


