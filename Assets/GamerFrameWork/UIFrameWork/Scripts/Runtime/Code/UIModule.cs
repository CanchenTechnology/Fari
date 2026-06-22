//using GamerFrameWork.HotUpdate;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;
namespace GamerFrameWork.UIFrameWork
{
    public enum UIPrefabLoadType
    {
        None = 0,
        AssetBundle,
        Resources,
    }
    /// <summary>
    /// UIModule:管理所有UI的创建-隐藏-销毁-以及声明周期的初始化
    /// </summary>
    public class UIModule
    {
        #region 单例
        private static UIModule _instance;
        public static UIModule Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new UIModule();
                }
                return _instance;
            }
        }
        #endregion

        #region 属性 字段
        /// <summary>
        /// UI相机
        /// </summary>
        private Camera mUICamera;
        public Camera Camera { get { return mUICamera; } }
        /// <summary>
        /// UI节点
        /// </summary>
        private Transform mUIRoot;
        public Transform UIRoot { get { return mUIRoot; } }
        /// <summary>
        /// 窗口配置表
        /// </summary>
        private WindowConfig mWindowConfig;
        /// <summary>
        /// 所有已克隆的窗口的字典(包含显示及隐藏的窗口,不含已销毁的窗口)
        /// </summary>
        private Dictionary<string, WindowBase> mAllWindowDic = new Dictionary<string, WindowBase>();//所有窗口的Dic
        /// <summary>
        /// 所有已克隆的窗口列表(包含显示及隐藏的窗口,不含已销毁的窗口)
        /// </summary>
        private List<WindowBase> mAllWindowList = new List<WindowBase>();//所有窗口的列表
        /// <summary>
        /// 所有可见窗口的列表
        /// </summary>
        private List<WindowBase> mVisibleWindowList = new List<WindowBase>();//所有可见窗口的列表
        /// <summary>
        /// 队列,用来管理弹窗的循环弹出
        /// </summary>
        private Queue<WindowBase> mWindowStack = new Queue<WindowBase>();//队列，用来管理弹窗的循环弹出
        private bool mStartPopStackWndStatus = false;//开始弹堆栈的标志，可用来处理多种情况，比如:正在出栈中有其他界面弹出，可以直接放进栈内进行弹出等
        private Dictionary<WindowLayer, List<WindowBase>> mLayerWindows = new Dictionary<WindowLayer, List<WindowBase>>();
        #endregion

        #region 框架初始化接口(外部调用)
        /// <summary>
        /// 初始化模块
        /// </summary>
        public void Initialize()
        {
            GameObject uiParent = GameObject.Instantiate(Resources.Load<GameObject>("Window/UIParent"));

            mUICamera = uiParent.transform.Find("UICamera").GetComponent<Camera>();
            mUIRoot = uiParent.transform.Find("UIRoot").transform;
            //mWindowConfig = HotFixAssetsFrame.LoadScriptableObject<WindowConfig>("Assets/GameData/HallWorld/CfgData/WindowConfig.asset");
            mWindowConfig = Resources.Load<WindowConfig>("WindowConfig");
            if (mWindowConfig == null)
            {
                Debug.LogError("mWindowConfig is null,请先配置生成");
                return;
            }
#if UNITY_EDITOR
            mWindowConfig.GeneratorWindowConfig();
#endif
        }
        #endregion

        #region 窗口管理
        /// <summary>
        /// 只加载物体，不调用生命周期
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void PreLoadWindow<T>(UIPrefabLoadType loadType = UIPrefabLoadType.AssetBundle) where T : WindowBase, new()
        {
            System.Type type = typeof(T);
            string wndName = type.Name;
            T windowBase = new T();
            //克隆界面,初始化界面信息
            //1.生成对应的窗口预制体
            GameObject nWnd = null;
            nWnd = LoadWindow(wndName, loadType);

            //2.初始出对应管理类
            if (nWnd != null)
            {
                windowBase.gameObject = nWnd;
                windowBase.transform = nWnd.transform;
                windowBase.Canvas = nWnd.GetComponent<Canvas>();
                windowBase.Canvas.worldCamera = mUICamera;
                windowBase.Name = nWnd.name;
                windowBase.OnAwake();
                windowBase.SetVisible(false);
                RectTransform rectTrans = nWnd.GetComponent<RectTransform>();
                rectTrans.anchorMax = Vector2.one;
                rectTrans.offsetMax = Vector2.zero;
                rectTrans.offsetMin = Vector2.zero;
                mAllWindowDic.Add(wndName, windowBase);
                mAllWindowList.Add(windowBase);
                Debug.Log($"预加载窗口,窗口名称:" + wndName);
            }
            else
            {
                Debug.LogError($"预加载窗口失败,窗口名称:" + wndName);
            }

        }
        /// <summary>
        /// 弹出弹窗
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T PopUpWindow<T>(UIPrefabLoadType loadType = UIPrefabLoadType.AssetBundle) where T : WindowBase, new()
        {
            System.Type type = typeof(T);
            string wndName = type.Name;
            WindowBase wnd = GetWindow(wndName);
            if (wnd != null)
            {
                return ShowWindow(wndName) as T;
            }
            T t = new T();
            return InitializeWindow(t, wndName, loadType) as T;
        }
        private WindowBase PopUpWindow(WindowBase window, UIPrefabLoadType loadType = UIPrefabLoadType.AssetBundle)
        {
            System.Type type = window.GetType();
            string wndName = type.Name;
            WindowBase wnd = GetWindow(wndName);
            if (wnd != null)
            {
                return ShowWindow(wndName);
            }
            return InitializeWindow(window, wndName, loadType);
        }
        /// <summary>
        /// 初始化窗口(生成窗口)
        /// </summary>
        /// <param name="windowBase"></param>
        /// <param name="wndName"></param>
        /// <returns></returns>
        private WindowBase InitializeWindow(WindowBase windowBase, string wndName, UIPrefabLoadType loadType)
        {
            //1.生成对应的窗口预制体
            GameObject nWnd = LoadWindow(wndName, loadType);
            //2.初始化出对应管理类
            if (nWnd != null)
            {
                windowBase.gameObject = nWnd;
                windowBase.transform = nWnd.transform;
                windowBase.Canvas = nWnd.GetComponent<Canvas>();
                windowBase.Canvas.worldCamera = mUICamera;
                windowBase.transform.SetAsLastSibling();
                windowBase.Name = nWnd.name;
                windowBase.OnAwake();
                RegisterWindow(windowBase);
                windowBase.SetVisible(true);
                windowBase.OnShow();
                RectTransform rectTrans = nWnd.GetComponent<RectTransform>();
                rectTrans.anchorMax = Vector2.one;
                rectTrans.offsetMax = Vector2.zero;
                rectTrans.offsetMin = Vector2.zero;
                mAllWindowDic.Add(wndName, windowBase);
                mAllWindowList.Add(windowBase);
                mVisibleWindowList.Add(windowBase);

                return windowBase;
            }
            Debug.LogError($"没有加载到对应的窗口,窗口名称:" + wndName);
            return null;
        }
        /// <summary>
        /// 显示窗口
        /// </summary>
        /// <param name="winName"></param>
        /// <returns></returns>
        private WindowBase ShowWindow(string winName)
        {
            WindowBase window = null;
            if (mAllWindowDic.ContainsKey(winName))
            {
                window = mAllWindowDic[winName];
                if (window.gameObject != null && !window.Visible)
                {
                    mVisibleWindowList.Add(window);
                    window.transform.SetAsLastSibling();
                    window.Canvas.sortingOrder = window.Canvas.sortingOrder + 1;
                    window.SetVisible(true);

                    RegisterWindow(window);
                    window.OnShow();
                }
            }
            else
            {
                Debug.LogError($"{winName}+ 窗口不存在,请调用PopUpWindow进行弹出");
            }
            return window;
        }
        /// <summary>
        /// 根据ui名字获取对应的WindowBase对象
        /// </summary>
        /// <param name="winName"></param>
        /// <returns></returns>
        private WindowBase GetWindow(string winName)
        {
            if (mAllWindowDic.ContainsKey(winName))
            {
                return mAllWindowDic[winName];
            }
            return null;
        }
        /// <summary>
        /// 获取已经弹出的弹窗
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetWindow<T>() where T : WindowBase
        {
            System.Type type = typeof(T);
            foreach (var item in mVisibleWindowList)
            {
                if (string.Equals(item.Name, type.Name))
                {
                    return (T)item;
                }
            }
            Debug.LogError($"该窗口没有获取到:{type.Name}");
            return null;
        }
        /// <summary>
        /// 获取当前最顶层打开的界面(最后一个弹出的可见窗口)
        /// </summary>
        /// <returns></returns>
        public WindowBase GetTopWindow()
        {
            if (mVisibleWindowList.Count > 0)
            {
                return mVisibleWindowList[mVisibleWindowList.Count - 1];
            }
            return null;
        }
        /// <summary>
        /// 获取上一个当前打开的界面(倒数第二个可见窗口)
        /// </summary>
        /// <returns></returns>
        public WindowBase GetPreviousVisibleWindow()
        {
            if (mVisibleWindowList.Count >= 2)
            {
                return mVisibleWindowList[mVisibleWindowList.Count - 2];
            }
            return null;
        }
        private void HideWindow(WindowBase window)
        {
            if (window != null && window.Visible)
            {
                mVisibleWindowList.Remove(window);
                window.SetVisible(false);//隐藏弹窗物体

                UnregisterWindow(window);
                window.OnHide();
            }
            //在出栈的情况下，上一个界面隐藏时,自动打开堆栈中的下一个界面
            PopNextStackWindow(window);
        }
        public void HideWindow(string wndName)
        {
            WindowBase window = GetWindow(wndName);
            HideWindow(window);
        }
        public void HideWindow<T>() where T : WindowBase
        {
            HideWindow(typeof(T).Name);
        }
        public void DestroyWindow(string wndName)
        {
            WindowBase window = GetWindow(wndName);
            DestroyWindow(window);
        }
        private void DestroyWindow(WindowBase window)
        {
            if (window != null)
            {
                if (mAllWindowDic.ContainsKey(window.Name))
                {
                    mAllWindowDic.Remove(window.Name);
                    mAllWindowList.Remove(window);
                    mVisibleWindowList.Remove(window);
                }
                if (window.Visible)
                {
                    window.OnHide();
                }
                window.SetVisible(false);
                UnregisterWindow(window);
                window.OnDestroy();
                DestroyWindow2FrameWork(window.gameObject);
                //在出栈的情况下，上一个界面销毁时,自动打开堆栈中的下一个界面
                PopNextStackWindow(window);
                window = null;
                //Resources.UnloadUnusedAssets();
            }
        }
        public void DestroyWindow<T>() where T : WindowBase
        {
            DestroyWindow(typeof(T).Name);
        }
        public void DestroyAllWindow(List<string> filterList = null)
        {
            ClearStackWindows();
            mStartPopStackWndStatus = false;

            for (int i = mAllWindowList.Count - 1; i >= 0; i--)
            {
                WindowBase window = mAllWindowList[i];
                if (window == null || (filterList != null && filterList.Contains(window.Name)))
                {
                    continue;
                }
                DestroyWindow(window.Name);
            }
            Resources.UnloadUnusedAssets();
        }

        public void DestroyAllWindows(List<string> filterList = null)
        {
            DestroyAllWindow(filterList);
        }

        #endregion

        #region 加载和释放接口--可以在接口中修改为自己的资源框架加载和释放接口

        /// <summary>
        /// 加载方法
        /// </summary>
        /// <param name="wndName"></param>
        /// <returns></returns>
        public GameObject LoadWindow(string wndName, UIPrefabLoadType loadType)
        {
            GameObject window = null;
            if (loadType == UIPrefabLoadType.Resources)
            {
                window = LoadWindow2Res(wndName);
            }
            else if (loadType == UIPrefabLoadType.AssetBundle)
            {
                window = LoadWindow2AB(wndName);
            }

            if (window == null)
            {
                Debug.LogError($"[LoadWindow] load failed, wndName={wndName}, loadType={loadType}");
                return null;
            }

            window.transform.localScale = Vector3.one;
            window.transform.localPosition = Vector3.zero;
            window.transform.rotation = Quaternion.identity;
            window.name = wndName;
            return window;
        }
        /// <summary>
        /// 通过Resource加载预制体
        /// </summary>
        /// <param name="wndName"></param>
        /// <returns></returns>
        private GameObject LoadWindow2Res(string wndName)
        {
            var path = mWindowConfig.GetWindowData(wndName)?.path;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"[LoadWindow2Res] path is null, wndName={wndName}");
                return null;
            }

            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[LoadWindow2Res] load failed, path={path}");
                return null;
            }

            GameObject window = GameObject.Instantiate<GameObject>(prefab, mUIRoot);
            return window;
        }
        /// <summary>
        /// 通过Asset Bundle加载预制体
        /// </summary>
        /// <param name="wndName"></param>
        /// <returns></returns>
        private GameObject LoadWindow2AB(string wndName)
        {
            var path = mWindowConfig.GetWindowData(wndName)?.path;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"[LoadWindow] path is null, wndName={wndName}");
                return null;
            }

            var handle = YooAssets.LoadAssetSync<GameObject>(path);
            if (handle == null || handle.AssetObject == null)
            {
                Debug.LogError($"[LoadWindow] load failed, path={path}");
                return null;
            }

            GameObject go = GameObject.Instantiate(handle.AssetObject as GameObject, mUIRoot);
            handle.Release(); // 实例化后释放 handle，资源引用计数 -1
            return go;
        }


        private void DestroyWindow2FrameWork(GameObject windowObj)
        {
            if (windowObj != null)
            {
                GameObject.Destroy(windowObj);
            }

            //调用资源框架的释放接口
            //HotFixAssetsFrame.Release(windowObj,true);

        }
        #endregion

        #region 渲染帧更新接口(为节省性能不默认开启,需要在外部调用)
        public void OnUpdate()
        {
            for (int i = 0; i < mVisibleWindowList.Count; i++)
            {
                WindowBase win = mVisibleWindowList[i];
                if (win.IsUpdate)
                {
                    win.OnUpdate();
                }
            }
        }

        #endregion

        #region 堆栈系统
        /// <summary>
        /// 进栈一个界面,并不进行弹出
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="popCallBack"></param>
        public void PushWindowToStack<T>(Action<WindowBase> popCallBack = null) where T : WindowBase, new()
        {
            T wndBase = new T();
            wndBase.PopStackListener = popCallBack;
            mWindowStack.Enqueue(wndBase);
        }
        /// <summary>
        /// 开始将栈内的弹窗弹出,弹出第一个弹窗
        /// </summary>
        public void StartPopFirstStackWindow()
        {
            if (mStartPopStackWndStatus) return;
            mStartPopStackWndStatus = true;//已经开始进行堆栈弹出的流程
            PopStackWindow();
        }
        /// <summary>
        /// 压入并且弹出堆栈弹窗--堆栈弹窗:用法方法
        /// UIModule.Instance.PushAndPopStackWindow<HallWindow>();
        /// UIModule.Instance.PushAndPopStackWindow<FriendWindow>();
        /// HallWindow界面会弹出来，然后关闭之后接着FriendWindow会弹出来
        /// 后续如果在加入其他的话，也有接着弹出来
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="popCallBack"></param>
        public void PushAndPopStackWindow<T>(Action<WindowBase> popCallBack = null) where T : WindowBase, new()
        {
            PushWindowToStack<T>(popCallBack);
            StartPopFirstStackWindow();
        }
        /// <summary>
        /// 弹出堆栈中的下一个窗口
        /// </summary>
        /// <param name="windowBase"></param>
        private void PopNextStackWindow(WindowBase windowBase)
        {
            if (windowBase != null && mStartPopStackWndStatus && windowBase.PopStack)
            {
                windowBase.PopStack = false;
                PopStackWindow();
            }
        }
        /// <summary>
        /// 弹出堆栈弹窗
        /// </summary>
        /// <returns></returns>
        private bool PopStackWindow()
        {
            if (mWindowStack.Count > 0)
            {
                WindowBase window = mWindowStack.Dequeue();
                WindowBase popWindow = PopUpWindow(window);
                popWindow.PopStackListener = window.PopStackListener;
                popWindow.PopStack = true;
                popWindow.PopStackListener?.Invoke(popWindow);
                popWindow.PopStackListener = null;
                return true;
            }
            else
            {
                mStartPopStackWndStatus = false;
                return false;
            }
        }
        public void ClearStackWindows()
        {
            mWindowStack.Clear();
        }
        #endregion

        #region 分层系统
        private void RegisterWindow(WindowBase windowBase)
        {
            if (!mLayerWindows.ContainsKey(windowBase.Layer))
                mLayerWindows[windowBase.Layer] = new List<WindowBase>();
            mLayerWindows[windowBase.Layer].Add(windowBase);

            ApplyLayerSorting(windowBase.Layer); // 每加一次重排一次
        }
        /// <summary>
        /// 隐藏掉上一个界面，重新排序layer
        /// </summary>
        /// <param name="windowBase"></param>
        private void UnregisterWindow(WindowBase windowBase)
        {
            if (mLayerWindows.ContainsKey(windowBase.Layer))
            {
                mLayerWindows[windowBase.Layer].Remove(windowBase);
                windowBase.Canvas.sortingOrder = (int)windowBase.Layer;
                ApplyLayerSorting(windowBase.Layer); // 每减一次重排一次
            }
        }
        private void ApplyLayerSorting(WindowLayer layer)
        {
            if (!mLayerWindows.ContainsKey(layer)) return;

            int baseOrder = (int)layer;
            List<WindowBase> windows = mLayerWindows[layer];

            for (int i = 0; i < windows.Count; i++)
            {
                var win = windows[i];
                if (win == null || win.Canvas == null) continue;
                win.Canvas.overrideSorting = true;
                win.Canvas.sortingOrder = baseOrder + i; // 按列表顺序依次排列
                win.SetMaskVisible(i == windows.Count - 1);
            }
        }

        #endregion
    }

}
