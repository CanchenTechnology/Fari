using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;
namespace XFGameFrameWork.ResLoader
{

    public class AddressablesLoader : IResourceLoader
    {
        private Dictionary<string, Object> cache = new Dictionary<string, Object>();

        public T Load<T>(string path) where T : Object
        {
            Debug.LogError("[AddressablesLoader] 不支持同步加载，请使用LoadAsync！");
            return null;
        }


        public async Task<T> LoadAsync<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[AddressablesLoader] 路径不能为空！");
                return null;
            }

            if (cache.TryGetValue(path, out Object obj))
                return obj as T;

            var handle = Addressables.LoadAssetAsync<T>(path);
            await handle.Task; // 等待异步完成

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                cache[path] = handle.Result;
                return handle.Result;
            }
            else
            {
                Debug.LogError($"[AddressablesLoader] 加载失败: {path}");
                return null;
            }
        }

        public void Unload(string path)
        {
            if (cache.TryGetValue(path, out Object obj))
            {
                Addressables.Release(obj);
                cache.Remove(path);
            }
        }

        public void UnloadAll()
        {
            foreach (var obj in cache.Values)
            {
                Addressables.Release(obj);
            }
            cache.Clear();
        }
        public async Task<IList<T>> LoadAllAsync<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[AddressablesLoader] 路径不能为空！");
                return null;
            }

            // 已缓存则直接返回
            List<T> resultList = new List<T>();
            foreach (var kvp in cache)
            {
                if (kvp.Value is T asset && kvp.Key.StartsWith(path))
                {
                    resultList.Add(asset);
                }
            }

            if (resultList.Count > 0)
                return resultList;

            // 特殊处理：如果是 Sprite 且可能来自 Multiple Sprite 纹理
            if (typeof(T) == typeof(Sprite))
            {
                // 尝试以 Sprite[] 形式加载
                var spriteArrayHandle = Addressables.LoadAssetAsync<Sprite[]>(path);
                await spriteArrayHandle.Task;

                if (spriteArrayHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    foreach (Sprite sprite in spriteArrayHandle.Result)
                    {
                        string cacheKey = $"{path}_{sprite.name}";
                        if (!cache.ContainsKey(cacheKey))
                        {
                            cache[cacheKey] = sprite;
                        }
                        resultList.Add(sprite as T);
                    }
                    return resultList;
                }
            }

            // 默认情况：普通资源加载
            var handle = Addressables.LoadAssetsAsync<T>(path, null);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (var asset in handle.Result)
                {
                    string cacheKey = $"{path}_{asset.name}";
                    if (!cache.ContainsKey(cacheKey))
                    {
                        cache[cacheKey] = asset;
                    }
                    resultList.Add(asset);
                }
                return resultList;
            }
            else
            {
                Debug.LogError($"[AddressablesLoader] 多资源加载失败: {path}");
                return null;
            }
        }


    }

}