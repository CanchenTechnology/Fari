/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/9/2026 10:43:40 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class TodayOracleUI : WindowBase
{
	public TodayOracleUIComponent uiComponent;

	private DivinationEngine _divinationEngine;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TodayOracleUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();

		_divinationEngine = DivinationEngine.Instance;
		if (_divinationEngine == null)
		{
			var go = new GameObject("DivinationEngine");
			_divinationEngine = go.AddComponent<DivinationEngine>();
		}
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
	public void OnDeepChatButtonClick()
	{
		// 先翻牌（如果还没翻）
		if (_divinationEngine != null && !_divinationEngine.TodayCard.HasValue)
		{
			_divinationEngine.DrawDailyCard();
		}

		// 把今日牌数据同步到 DialogSystem
		if (_divinationEngine?.TodayCard.HasValue == true)
		{
			var payload = _divinationEngine.GetTodayCardPayload();
			DialogSystem.Instance?.SetTodayCardPayload(payload);
			Debug.Log($"[TodayOracleUI] DeepChat 携带今日牌: {payload.displayName}");
		}

		UIModule.Instance.GetWindow<NavigationUI>().OpenDialogUI();
		UIModule.Instance.GetWindow<DialogUI>().SendTodayOracleMessage();
	}
		
	public void OnswitchDivinerButtonClick()
	{
		UIModule.Instance.PopUpWindow<SwitchRoleUI>();
	}

	public void OnflipCardButtonClick()
	{
		uiComponent.flipCardButton.gameObject.SetActive(false);
		uiComponent.ReadingCardContainerTransform.gameObject.SetActive(true);

		// 绘制今日牌
		if (_divinationEngine != null)
		{
			var (card, upright) = _divinationEngine.DrawDailyCard();
			Debug.Log($"[TodayOracleUI] 翻牌: {card.nameZh} ({(upright ? "正位" : "逆位")})");
		}
	}
	#endregion
}
