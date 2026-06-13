using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniFramework.Event;
using YooAsset;
using System;

public class YooManager
{
    private static YooManager _instance;
    public static YooManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = new YooManager();
            return _instance;
        }
    }

    private readonly EventGroup _eventGroup = new EventGroup();

    /// <summary>
    /// 协程启动器
    /// </summary>
    public MonoBehaviour Behaviour;


    private YooManager()
    {
        // 注册监听事件
        _eventGroup.AddListener<SceneEventDefine.ChangeToAppScene>(OnHandleEventMessage);

    }

    private void OnHandleEventMessage(IEventMessage message)
    {
        if (message is SceneEventDefine.ChangeToAppScene)
        {
            YooAssets.LoadSceneAsync("App");
        }
    }

    /// <summary>
    /// 开启一个协程
    /// </summary>
    public void StartCoroutine(IEnumerator enumerator)
    {
        Behaviour.StartCoroutine(enumerator);
    }

}