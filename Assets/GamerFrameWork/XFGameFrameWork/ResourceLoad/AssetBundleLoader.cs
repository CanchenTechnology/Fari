using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
namespace XFGameFrameWork.ResLoader
{
    public class AssetBundleLoader : IResourceLoader
    {
        private Dictionary<string, Object> cache = new Dictionary<string, Object>();
        private Dictionary<string, AssetBundle> bundles = new Dictionary<string, AssetBundle>();

        public T Load<T>(string path) where T : Object
        {
            if (cache.TryGetValue(path, out Object obj))
                return obj as T;

            string bundleName = GetBundleName(path);
            if (!bundles.TryGetValue(bundleName, out AssetBundle bundle))
            {
                string fullPath = Path.Combine(Application.streamingAssetsPath, bundleName);
                bundle = AssetBundle.LoadFromFile(fullPath);
                if (bundle != null)
                    bundles[bundleName] = bundle;
                else
                {
                    Debug.LogError($"[AssetBundleLoader] 加载Bundle失败: {fullPath}");
                    return null;
                }
            }

            T asset = bundle.LoadAsset<T>(path);
            if (asset != null)
                cache[path] = asset;
            return asset;
        }

        public async Task<T> LoadAsync<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[AssetBundleLoader] 路径不能为空！");
                return null;
            }

            if (cache.TryGetValue(path, out Object obj))
                return obj as T;

            string bundleName = GetBundleName(path);

            if (!bundles.TryGetValue(bundleName, out AssetBundle bundle))
            {
                string fullPath = Path.Combine(Application.streamingAssetsPath, bundleName);
                var bundleRequest = AssetBundle.LoadFromFileAsync(fullPath);
                while (!bundleRequest.isDone)
                {
                    await Task.Yield();
                }
                bundle = bundleRequest.assetBundle;

                if (bundle != null)
                    bundles[bundleName] = bundle;
                else
                {
                    Debug.LogError($"[AssetBundleLoader] 加载Bundle失败: {fullPath}");
                    return null;
                }
            }

            var assetRequest = bundle.LoadAssetAsync<T>(path);
            while (!assetRequest.isDone)
            {
                await Task.Yield();
            }

            if (assetRequest.asset != null)
            {
                cache[path] = assetRequest.asset;
                return assetRequest.asset as T;
            }
            else
            {
                Debug.LogError($"[AssetBundleLoader] 加载资源失败: {path}");
                return null;
            }
        }

        private string GetBundleName(string path)
        {
            return path.ToLower() + ".ab"; // Bundle名字默认是路径小写+.ab
        }

        public void Unload(string path)
        {
            if (cache.TryGetValue(path, out Object obj))
            {
                cache.Remove(path);
                // 注意这里只清资源引用，不卸载Bundle本体
            }
        }

        public void UnloadAllBundles(bool unloadAllLoadedObjects = false)
        {
            foreach (var bundle in bundles.Values)
            {
                bundle.Unload(unloadAllLoadedObjects);
            }
            bundles.Clear();
            cache.Clear();
        }

        public async Task<IList<T>> LoadAllAsync<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[AssetBundleLoader] 路径不能为空！");
                return null;
            }

            // 如果已经加载了图集中的某个元素，可以判断是否已经缓存整组资源
            List<T> results = new List<T>();

            // NOTE: 这里不使用 cache[path]，因为是多个资源
            string bundleName = GetBundleName(path);

            if (!bundles.TryGetValue(bundleName, out AssetBundle bundle))
            {
                string fullPath = Path.Combine(Application.streamingAssetsPath, bundleName);
                var bundleRequest = AssetBundle.LoadFromFileAsync(fullPath);
                while (!bundleRequest.isDone)
                {
                    await Task.Yield();
                }
                bundle = bundleRequest.assetBundle;

                if (bundle != null)
                    bundles[bundleName] = bundle;
                else
                {
                    Debug.LogError($"[AssetBundleLoader] 加载Bundle失败: {fullPath}");
                    return null;
                }
            }

            var assetRequest = bundle.LoadAllAssetsAsync<T>();
            while (!assetRequest.isDone)
            {
                await Task.Yield();
            }

            foreach (var item in assetRequest.allAssets)
            {
                T asset = item as T;
                if (asset != null)
                {
                    // 缓存每个资源：path + asset.name 作为唯一 key
                    string subAssetPath = $"{path}_{asset.name}";
                    if (!cache.ContainsKey(subAssetPath))
                        cache[subAssetPath] = asset;
                    results.Add(asset);
                }
            }

            return results;
        }

    }
}