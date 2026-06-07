using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using YooAsset;
public class GameStart : MonoBehaviour
{
    /// <summary>
    /// 资源系统运行模式
    /// </summary>
    public EPlayMode PlayMode = EPlayMode.EditorSimulateMode;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Application.runInBackground = true;


        GameManager.Instance.Behaviour = this;

        //初始化资源系统
        YooAssets.Initialize();
        // 加载更新页面
        var go = Resources.Load<GameObject>("PatchWindow");
        GameObject.Instantiate(go);
        // 开始补丁更新流程


        UIModule.Instance.Initialize();
    }
    // Start is called before the first frame update
    void Start()
    {
        UIModule.Instance.PopUpWindow<LoginUI>();
    }

    // Update is called once per frame
    void Update()
    {

    }
}
