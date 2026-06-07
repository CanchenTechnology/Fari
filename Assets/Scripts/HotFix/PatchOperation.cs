using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;
using UniFramework.Machine;
using UniFramework.Event;
using System;
/// <summary>
/// 补丁操作
/// </summary>
public class PatchOperation : GameAsyncOperation
{
    private enum ESteps
    {
        None,
        Update,
        Done,
    }
    private EventGroup _eventGroup = new EventGroup(); //事件管理中心
    private StateMachine _machine;//状态机   
    private string _packageName;
    private EPlayMode _playMode;
    private ESteps _steps = ESteps.None;

    /// <summary>
    /// // 操作开始
    /// </summary>
    protected override void OnStart()
    {
        _steps = ESteps.Update;
        _machine.Run<FsmInitializePackage>();

    }
    /// <summary>
    /// // 每帧执行
    /// </summary> <summary>
    /// 
    /// </summary>
    protected override void OnUpdate()
    {
        if (_steps == ESteps.None || _steps == ESteps.Done)
            return;

        if (_steps == ESteps.Update)
        {
            _machine.Update();
        }
    }
    /// <summary>
    /// // 操作被手动取消时触发
    /// </summary>
    protected override void OnAbort()
    {

    }
    public PatchOperation(string packageName, EPlayMode playMode)
    {
        _packageName = packageName;
        _playMode = playMode;


        // 注册监听事件
        _eventGroup.AddListener<UserEventDefine.UserTryInitialize>(OnHandleEventMessage);//用户尝试再次初始化资源包
        _eventGroup.AddListener<UserEventDefine.UserBeginDownloadWebFiles>(OnHandleEventMessage);//用户开始下载网络文件
        _eventGroup.AddListener<UserEventDefine.UserTryRequestPackageVersion>(OnHandleEventMessage);//用户尝试再次请求资源版本
        _eventGroup.AddListener<UserEventDefine.UserTryUpdatePackageManifest>(OnHandleEventMessage);//用户尝试更新资源清单
        _eventGroup.AddListener<UserEventDefine.UserTryDownloadWebFiles>(OnHandleEventMessage);//用户尝试下载网络文件

        //创建状态机
        _machine = new StateMachine(this);
        _machine.AddNode<FsmInitializePackage>(); //初始化包体
        _machine.AddNode<FsmRequestPackageVersion>(); //获取包体的版本号
        _machine.AddNode<FsmUpdatePackageManifest>(); //更新包体清单
        _machine.AddNode<FsmCreateDownloader>(); //创建下载器
        _machine.AddNode<FsmDownloadPackageFiles>();//下载资源文件
        _machine.AddNode<FsmDownloadPackageOver>(); //包体下载结束
        _machine.AddNode<FsmClearCacheBundle>(); //清理未使用的缓存文件
        _machine.AddNode<FsmStartGame>();  //开始游戏

        _machine.SetBlackboardValue("PackageName", packageName);
        _machine.SetBlackboardValue("PlayMode", playMode);

    }

    private void OnHandleEventMessage(IEventMessage message)
    {
        if (message is UserEventDefine.UserTryInitialize)
        {
            _machine.ChangeState<FsmInitializePackage>();
        }
        else if (message is UserEventDefine.UserBeginDownloadWebFiles)
        {
            _machine.ChangeState<FsmDownloadPackageFiles>();
        }
        else if (message is UserEventDefine.UserTryRequestPackageVersion)
        {
            _machine.ChangeState<FsmRequestPackageVersion>();
        }
        else if (message is UserEventDefine.UserTryUpdatePackageManifest)
        {
            _machine.ChangeState<FsmUpdatePackageManifest>();
        }
        else if (message is UserEventDefine.UserTryDownloadWebFiles)
        {
            _machine.ChangeState<FsmCreateDownloader>();
        }
        else
        {
            throw new System.NotImplementedException($"{message.GetType()}");
        }
    }
}
