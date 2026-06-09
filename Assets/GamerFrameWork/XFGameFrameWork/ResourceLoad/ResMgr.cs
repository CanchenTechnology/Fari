using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
using XFGameFrameWork.ResLoader;
//**********************************************
//创建人：玖一
//功能说明：先再GameStart初始化ResMgr.Init();
//**********************************************
public enum LoadMode
{
    Resources,
    AssetBundle,
    Addressables,
}
public static class ResMgr
{
    private static IResourceLoader loader;
    public static LoadMode Mode { get; private set; }
    public static void Init(LoadMode mode)
    {
        Mode = mode;
        loader = mode switch
        {
            LoadMode.Resources => new ResourcesLoader(),
            LoadMode.AssetBundle=>new AssetBundleLoader(),
            LoadMode.Addressables => new AddressablesLoader(),
            _=>throw new ArgumentOutOfRangeException(nameof(mode),mode,null),
        };
    }
    // 同步加载（不推荐用于大资源）
    public static T Load<T>(string path) where T : Object
    {
        if (loader == null) throw new InvalidOperationException("Resource loader not initialized");
        return loader.Load<T>(path);
    }
    // 异步加载
    public static ResourceRequest<T> LoadAsync<T>(string path) where T : Object
    {
        if (loader == null) throw new InvalidOperationException("Resource loader not initialized");
        return new ResourceRequest<T>(loader.LoadAsync<T>(path));
    }


    public static void Unload(string path)
    {
        loader?.Unload(path);
    }
    // 异步加载多个资源（如Sprite图集）
    public static ResourceListRequest<T> LoadAllAsync<T>(string path) where T : Object
    {
        if (loader == null) throw new InvalidOperationException("Resource loader not initialized");
        return new ResourceListRequest<T>(loader.LoadAllAsync<T>(path));
    }

}
/// <summary>
/// 资源请求包装类
/// </summary>
/// <typeparam name="T"></typeparam>
public class ResourceRequest<T> where T : Object
{
    private readonly Task<T> _loadTask;
    public ResourceRequest(Task<T> loadTask)
    {
        _loadTask = loadTask ?? throw new ArgumentNullException(nameof(loadTask));
    }
    // 完成回调
    public ResourceRequest<T> OnCompleted(Action<T> callback)
    {
        _loadTask.ContinueWith(t =>
        {
            if (t.Status == TaskStatus.RanToCompletion)
            {
                callback?.Invoke(t.Result);
            }
            else if (t.IsFaulted)
            {
                Debug.LogError($"资源加载失败（异常）: {t.Exception}");
            }
            else if (t.IsCanceled)
            {
                Debug.LogWarning("资源加载被取消");
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());

        return this;
    }

    // 错误处理
    public ResourceRequest<T> OnError(Action<Exception> errorHandler)
    {
        _loadTask.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                errorHandler?.Invoke(t.Exception);
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
        return this;
    }

}


public class ResourceListRequest<T> where T : Object
{
    private readonly Task<IList<T>> _loadTask;

    public ResourceListRequest(Task<IList<T>> loadTask)
    {
        _loadTask = loadTask ?? throw new ArgumentNullException(nameof(loadTask));
    }

    public ResourceListRequest<T> OnCompleted(Action<IList<T>> callback)
    {
        _loadTask.ContinueWith(t =>
        {
            if (t.Status == TaskStatus.RanToCompletion)
            {
                callback?.Invoke(t.Result);
            }
            else if (t.IsFaulted)
            {
                Debug.LogError($"资源列表加载失败: {t.Exception}");
            }
            else if (t.IsCanceled)
            {
                Debug.LogWarning("资源加载任务被取消");
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());

        return this;
    }


    public ResourceListRequest<T> OnError(Action<Exception> errorHandler)
    {
        _loadTask.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                errorHandler?.Invoke(t.Exception);
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
        return this;
    }
}
