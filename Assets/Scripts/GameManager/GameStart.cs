using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using YooAsset;
using UniFramework.Event;
public class GameStart : MonoBehaviour
{
    /// <summary>
    /// 资源系统运行模式
    /// </summary>
    public EPlayMode PlayMode = EPlayMode.OfflinePlayMode;

    [Header("Startup Splash")]
    [SerializeField] private GameObject startupSplashPrefab;
    [SerializeField] private bool showStartupSplash = true;
    [SerializeField] private float minimumSplashSeconds = 2.5f;
    [SerializeField] private float splashFadeSeconds = 0.35f;

    private readonly EventGroup _patchEventGroup = new EventGroup();
    private GameObject _patchWindowObject;
    private bool _patchWindowRequired;

    private void Awake()
    {
        Debug.Log($"资源系统运行模式：{PlayMode}");
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        DontDestroyOnLoad(this.gameObject);
    }

    private void OnDestroy()
    {
        _patchEventGroup.RemoveAllListener();
    }

    // Start is called before the first frame update
    IEnumerator Start()
    {
        // 游戏管理器
        YooManager.Instance.Behaviour = this;
        // 初始化事件系统
        UniEvent.Initalize();
        _patchEventGroup.AddListener<PatchEventDefine.PatchWindowRequired>(OnHandlePatchEventMessage);

        //初始化资源系统
        YooAssets.Initialize();

        // 启动资源检查流程：无更新时保持静默，有更新/错误时再显示 PatchWindow。
        var operation = new PatchOperation("DefaultPackage", PlayMode);
        YooAssets.StartOperation(operation);

        if (showStartupSplash)
            yield return PlayStartupSplash(() => operation.IsDone || _patchWindowRequired);

        yield return operation;

        // 设置默认的资源包
        var gamePackage = YooAssets.GetPackage("DefaultPackage");
        YooAssets.SetDefaultPackage(gamePackage);

        // 切换到主页面场景
        SceneEventDefine.ChangeToAppScene.SendEventMessage();

    }

    private IEnumerator PlayStartupSplash(Func<bool> canCloseSplash)
    {
        GameObject splashObject = GetStartupSplashObject();
        if (splashObject == null)
            yield break;

        CanvasGroup splashRoot = PrepareStartupSplash(splashObject);
        CanvasGroup splashContent = PrepareStartupSplashFadeContent(splashObject, splashRoot);
        if (minimumSplashSeconds > 0f)
            yield return new WaitForSecondsRealtime(minimumSplashSeconds);

        while (canCloseSplash != null && !canCloseSplash())
            yield return null;

        if (splashFadeSeconds > 0f)
        {
            float timer = 0f;
            while (timer < splashFadeSeconds)
            {
                timer += Time.unscaledDeltaTime;
                if (timer >= splashFadeSeconds)
                    break;

                splashContent.alpha = 1f - Mathf.Clamp01(timer / splashFadeSeconds);
                yield return null;
            }
        }

        splashContent.alpha = 0f;
        Destroy(splashRoot.gameObject);
    }

    private void OnHandlePatchEventMessage(IEventMessage message)
    {
        if (message is PatchEventDefine.PatchWindowRequired)
        {
            _patchWindowRequired = true;
            EnsurePatchWindow();
        }
    }

    private void EnsurePatchWindow()
    {
        if (_patchWindowObject != null)
            return;

        var patchWindowPrefab = Resources.Load<GameObject>("PatchWindow");
        if (patchWindowPrefab == null)
        {
            Debug.LogError("[GameStart] PatchWindow prefab not found in Resources.");
            return;
        }

        _patchWindowObject = Instantiate(patchWindowPrefab);
    }

    private GameObject GetStartupSplashObject()
    {
        if (startupSplashPrefab != null)
            return Instantiate(startupSplashPrefab);

        StartupSplashUIComponent sceneSplash = FindObjectOfType<StartupSplashUIComponent>(true);
        if (sceneSplash != null)
            return sceneSplash.gameObject;

        GameObject namedSplash = GameObject.Find("StartupSplashUI");
        if (namedSplash != null)
            return namedSplash;

        Debug.LogWarning("[GameStart] StartupSplashUI not found. Put StartupSplashUI in Start scene or assign startupSplashPrefab.");
        return null;
    }

    private static CanvasGroup PrepareStartupSplash(GameObject splashObject)
    {
        splashObject.SetActive(true);
        splashObject.name = "StartupSplashUI";

        RectTransform rectTransform = splashObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }
        else
        {
            splashObject.transform.localScale = Vector3.one;
        }

        Canvas canvas = splashObject.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
        }

        CanvasGroup canvasGroup = splashObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = splashObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        return canvasGroup;
    }

    private static CanvasGroup PrepareStartupSplashFadeContent(GameObject splashObject, CanvasGroup rootCanvasGroup)
    {
        Transform content = splashObject.transform.Find("UIContent");
        if (content == null)
            return rootCanvasGroup;

        CanvasGroup contentCanvasGroup = content.GetComponent<CanvasGroup>();
        if (contentCanvasGroup == null)
            contentCanvasGroup = content.gameObject.AddComponent<CanvasGroup>();

        contentCanvasGroup.alpha = 1f;
        contentCanvasGroup.interactable = false;
        contentCanvasGroup.blocksRaycasts = false;
        return contentCanvasGroup;
    }

}
