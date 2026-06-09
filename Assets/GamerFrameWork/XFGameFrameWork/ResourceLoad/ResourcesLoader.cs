using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
//**********************************************
//创建人：玖一
//功能说明：
//**********************************************
namespace XFGameFrameWork.ResLoader
{
    public class ResourcesLoader : MonoBehaviour, IResourceLoader
    {
        private Dictionary<string, Object> cache = new Dictionary<string, Object>();
        public T Load<T>(string path) where T : UnityEngine.Object
        {
            if (cache.TryGetValue(path, out Object obj))
                return obj as T;
            T resource = Resources.Load<T>(path);
            if (resource != null)
                cache[path] = resource;
            return resource;
        }

        public async Task<IList<T>> LoadAllAsync<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[ResourcesLoader] 路径不能为空！");
                return null;
            }

            Object[] assets = Resources.LoadAll(path, typeof(T));
            List<T> result = new List<T>();

            foreach (var asset in assets)
            {
                if (asset is T typedAsset)
                {
                    string subAssetPath = $"{path}_{asset.name}";
                    if (!cache.ContainsKey(subAssetPath))
                        cache[subAssetPath] = typedAsset;
                    result.Add(typedAsset);
                }
            }

            await Task.Yield(); // 模拟异步（虽然Resources.LoadAll是同步的）

            if (result.Count == 0)
            {
                Debug.LogWarning($"[ResourcesLoader] 没有找到类型为 {typeof(T)} 的资源: {path}");
            }

            return result;
        }

        public async Task<T> LoadAsync<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[ResourcesLoader] 路径不能为空！");
                return null;
            }

            if (cache.TryGetValue(path, out var obj))
            {
                return obj as T;
            }

            ResourceRequest request = Resources.LoadAsync<T>(path);
            while (!request.isDone)
            {
                await Task.Yield(); // 挂起一帧
            }

            if (request.asset != null)
            {
                cache[path] = request.asset;
                return request.asset as T;
            }
            else
            {
                Debug.LogError($"[ResourcesLoader] 加载失败: {path}");
                return null;
            }
        }



        public void Unload(string path)
        {
            if (cache.TryGetValue(path, out Object obj))
            {
                Resources.UnloadAsset(obj);
                cache.Remove(path);
            }
        }


    }

}