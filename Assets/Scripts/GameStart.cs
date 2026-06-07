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
        // 游戏管理器
        GameManager.Instance.Behaviour = this;
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

        
        UIModule.Instance.Initialize();
        UIModule.Instance.PopUpWindow<LoginUI>();
    }

    // Update is called once per frame
    void Update()
    {

    }
}
