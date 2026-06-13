using System.Collections;
using UnityEngine;
using UniFramework.Machine;
using YooAsset;
/// <summary>
/// 下载资源文件
/// </summary> <summary>
/// 
/// </summary>
public class FsmDownloadPackageFiles : IStateNode
{
    private StateMachine _machine;

    void IStateNode.OnCreate(StateMachine machine)
    {
        _machine = machine;
    }
    void IStateNode.OnEnter()
    {
        PatchEventDefine.PatchStepsChange.SendEventMessage("开始下载资源文件！");
        YooManager.Instance.StartCoroutine(BeginDownload());
    }
    void IStateNode.OnUpdate()
    {
    }
    void IStateNode.OnExit()
    {
    }

    private IEnumerator BeginDownload()
    {
        var downloader = (ResourceDownloaderOperation)_machine.GetBlackboardValue("Downloader");
        downloader.DownloadErrorCallback = PatchEventDefine.WebFileDownloadFailed.SendEventMessage; //下载失败的回调
        downloader.DownloadUpdateCallback = PatchEventDefine.DownloadUpdate.SendEventMessage; //下载进度更新的回调
        downloader.BeginDownload();
        yield return downloader;

        // 检测下载结果
        if (downloader.Status != EOperationStatus.Succeed)
            yield break;

        _machine.ChangeState<FsmDownloadPackageOver>();   //切换到下一个状态
    }
}