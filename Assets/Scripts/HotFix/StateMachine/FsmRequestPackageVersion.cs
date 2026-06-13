using System.Collections;
using UniFramework.Machine;
using UnityEngine;
using YooAsset;
/// <summary>
/// 请求包体版本
/// </summary>
public class FsmRequestPackageVersion : IStateNode
{
    private StateMachine _machine;
    public void OnCreate(StateMachine machine)
    {
        _machine = machine;
    }

    public void OnEnter()
    {
        PatchEventDefine.PatchStepsChange.SendEventMessage("请求资源版本 !");
        YooManager.Instance.StartCoroutine(UpdatePackageVersion());
    }

    public void OnExit()
    {

    }

    public void OnUpdate()
    {

    }
    /// <summary>
    /// 获取包体的版本号
    /// </summary>
    /// <returns></returns>
    private IEnumerator UpdatePackageVersion()
    {
        var packageName = (string)_machine.GetBlackboardValue("PackageName");
        var package = YooAssets.GetPackage(packageName);
        var operation = package.RequestPackageVersionAsync();
        yield return operation;

        if (operation.Status != EOperationStatus.Succeed)
        {
            Debug.LogWarning(operation.Error);
            PatchEventDefine.PackageVersionRequestFailed.SendEventMessage();
        }
        else
        {
            Debug.Log($"Request package version : {operation.PackageVersion}");
            _machine.SetBlackboardValue("PackageVersion", operation.PackageVersion);
            _machine.ChangeState<FsmUpdatePackageManifest>();
        }
    }
}