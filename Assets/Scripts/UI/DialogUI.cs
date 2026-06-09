/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/9/2026 1:01:55 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class DialogUI : WindowBase
{
	public DialogUIComponent uiComponent;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<DialogUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function

	#endregion

	#region UI组件事件
	public void OnswitchDivinerButtonClick()
	{
		Debug.Log("switchDivinerButton is clicked");	
	}
	public void OnquestionButtonClick()
	{
		Debug.Log("questionButton is clicked");
	}
	public void OnsendButtonClick()
	{
		Debug.Log("sendButton is clicked");
		if(uiComponent.questionInputField.text=="")
		{
			ToastManager.ShowToast("请写下你想问的问题。");
			return;
		}
	}
	public void OnquestionInputChange(string text)
	{
	}
	public void OnquestionInputEnd(string text)
	{
	}
	#endregion
}
