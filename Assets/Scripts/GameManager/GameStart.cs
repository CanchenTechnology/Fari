using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using YooAsset;
using UniFramework.Event;
public class GameStart : MonoBehaviour
{
    /// <summary>
    /// 资源系统运行模式
    /// </summary>
    public EPlayMode PlayMode = EPlayMode.EditorSimulateMode;

    [Header("Startup Splash")]
    [SerializeField] private GameObject startupSplashPrefab;
    [SerializeField] private bool showStartupSplash = true;
    [SerializeField] private float minimumSplashSeconds = 2.5f;
    [SerializeField] private float splashFadeSeconds = 0.35f;

    private void Awake()
    {
        Debug.Log($"资源系统运行模式：{PlayMode}");
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        DontDestroyOnLoad(this.gameObject);
    }
    // Start is called before the first frame update
    IEnumerator Start()
    {
        if (showStartupSplash)
            yield return PlayStartupSplash();

        // 游戏管理器
        YooManager.Instance.Behaviour = this;
        // 初始化事件系统
        UniEvent.Initalize();

        //初始化资源系统
        YooAssets.Initialize();

        // 加载更新页面
        var go = Resources.Load<GameObject>("PatchWindow");
        GameObject.Instantiate(go);


        // 开始补丁更新流程
        var operation = new PatchOperation("DefaultPackage", PlayMode);
        YooAssets.StartOperation(operation);
        yield return operation;

        // 设置默认的资源包
        var gamePackage = YooAssets.GetPackage("DefaultPackage");
        YooAssets.SetDefaultPackage(gamePackage);

        // 切换到主页面场景
        SceneEventDefine.ChangeToAppScene.SendEventMessage();

    }

    private IEnumerator PlayStartupSplash()
    {
        GameObject splashObject = GetStartupSplashObject();
        if (splashObject == null)
            yield break;

        CanvasGroup splash = PrepareStartupSplash(splashObject);
        if (minimumSplashSeconds > 0f)
            yield return new WaitForSecondsRealtime(minimumSplashSeconds);

        if (splashFadeSeconds > 0f)
        {
            float timer = 0f;
            while (timer < splashFadeSeconds)
            {
                timer += Time.unscaledDeltaTime;
                splash.alpha = 1f - Mathf.Clamp01(timer / splashFadeSeconds);
                yield return null;
            }
        }

        Destroy(splash.gameObject);
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

}
