/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/18/2026
 * Description: 好友空状态界面
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class NoFriendUI : WindowBase
{
	public NoFriendUIComponent uiComponent;

	#region 生命周期函数
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<NoFriendUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}

	public override void OnShow()
	{
		base.OnShow();
	}

	public override void OnHide()
	{
		base.OnHide();
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region UI组件事件
	public void OnAddFriendButtonClick()
	{
		UIModule.Instance.PopUpWindow<AddFriendUI>();
	}

	public void OnCreateFriendButtonClick()
	{
		UIModule.Instance.PopUpWindow<CreateFriendUI>();
	}
	#endregion
}
