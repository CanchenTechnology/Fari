/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/20/2026 7:39:10 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class TwoPersonDivinationRevealReadyFlowUI : WindowBase
{
	public TwoPersonDivinationRevealReadyFlowUIComponent uiComponent;
	private RelationshipDivinationRecord currentRecord;
	private FriendDataManager.FriendData currentFriend;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TwoPersonDivinationRevealReadyFlowUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("TwoPersonDivinationRevealReadyFlowUI 缺少 UI 组件绑定脚本：TwoPersonDivinationRevealReadyFlowUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		RefreshFromFlow();
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
	public void RefreshFromFlow()
	{
		currentRecord = RelationshipDivinationFlow.CurrentRecord;
		currentFriend = RelationshipDivinationFlow.CurrentFriend;
		Render();
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnRevealResultButtonClick()
	{
		RelationshipDivinationFlow.OpenResult(currentRecord);
	}
	#endregion

	private void Render()
	{
		if (uiComponent == null || currentRecord == null) return;

		RelationshipDivinationFlow.SetButtonText(uiComponent.RevealResultButton, "查看共同结果");
		ApplyCardSprite(uiComponent.MyCardPersonShapeImage, RelationshipDivinationFlow.GetMyPrivateCard(currentRecord));
		ApplyCardSprite(uiComponent.CenterCardStarImage, currentRecord.SharedCard);
	}

	private void ApplyCardSprite(Image target, RelationshipDivinationCard card)
	{
		Sprite sprite = RelationshipDivinationFlow.LoadCardSprite(card);
		if (target != null && sprite != null)
			target.sprite = sprite;
	}
}
