using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

//**********************************************
//创建人：玖一
//功能说明：
//**********************************************
namespace XFGameFrameWork.SceneLoader
{
    public enum LoaderType
    {
        Addressables,
        SceneManager,
    }
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Loading UI")]
        public GameObject loadingUIPrefab;
        private GameObject loadingUIInstance;
        //private LoadingUI loadingUIScript;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        public Coroutine LoadScene(string sceneName, Func<Task> beforeLoadAsync, Action onComplete,LoaderType type = LoaderType.SceneManager)
        {
            return StartCoroutine(LoadSceneAsyncInternal(sceneName, beforeLoadAsync, onComplete,type));
        }

        private IEnumerator LoadSceneAsyncInternal(
            string sceneName,
            Func<Task> beforeLoadAsync,
            Action onComplete,
            LoaderType type,
            LoadSceneMode loadMode = LoadSceneMode.Single)
        {
            // 等待异步前置加载逻辑完成
            if (beforeLoadAsync != null)
            {
                var task = beforeLoadAsync.Invoke();
                while (!task.IsCompleted)
                    yield return null;

                if (task.IsFaulted)
                {
                    Debug.LogError($"[SceneLoader] 异步准备失败: {task.Exception}");
                    yield break;
                }
            }

            ShowLoadingUI();

            AsyncOperation async = null;

            if (type == LoaderType.SceneManager)
            {
                async = SceneManager.LoadSceneAsync(sceneName, loadMode);
            }
            else if (type == LoaderType.Addressables)
            {
                var handle = Addressables.LoadSceneAsync(sceneName, loadMode);
                while (!handle.IsDone)
                {
                    UpdateProgress(handle.PercentComplete);
                    yield return null;
                }

                if (handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"[SceneLoader] Addressables 加载失败: {sceneName}");
                    yield break;
                }

                // Addressables 场景加载成功
                UpdateProgress(1f);
                yield return new WaitForSeconds(0.2f); // 可选：视觉延迟
                HideLoadingUI();
                onComplete?.Invoke();
                yield break;
            }

            // SceneManager 加载流程
            async.allowSceneActivation = false;

            while (async.progress < 0.9f)
            {
                UpdateProgress(async.progress);
                yield return null;
            }

            // 平滑过渡进度条
            float timer = 0f;
            while (timer < 0.5f)
            {
                timer += Time.deltaTime;
                UpdateProgress(Mathf.Lerp(0.9f, 1f, timer / 0.5f));
                yield return null;
            }

            UpdateProgress(1f);
            async.allowSceneActivation = true;

            while (!async.isDone)
                yield return null;

            HideLoadingUI();
            onComplete?.Invoke();
        }


        private void ShowLoadingUI()
        {
            if (loadingUIPrefab != null && loadingUIPrefab == null)
            {
                loadingUIInstance = Instantiate(loadingUIPrefab);
                DontDestroyOnLoad(loadingUIInstance);
                //loadingUIScript = loadingUIInstance.GetComponent<LoadingUI>();
            }
        }
        private void HideLoadingUI()
        {
            if (loadingUIInstance != null)
            {
                Destroy(loadingUIInstance);
                loadingUIInstance = null;
            }
        }
        private void UpdateProgress(float progress)
        {
            //loadingUIScript?.SetProgress(progress);
        }



    }
}


