/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/16/2026 4:35:16 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class OracleReadingUI : WindowBase
{
	public OracleReadingUIComponent uiComponent;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<OracleReadingUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		PopulateTodayCard();
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

	/// <summary>
	/// 赋值今日塔罗牌图片和名字
	/// </summary>
	private void PopulateTodayCard()
	{
		if (DivinationEngine.Instance == null) return;

		var result = DivinationEngine.Instance.DrawDailyCard();
		var card = result.card;
		bool upright = result.upright;

		// 牌名
		if (uiComponent.CardNameTextText != null)
			uiComponent.CardNameTextText.text = card.DisplayName(upright);

		// 牌图
		if (uiComponent.CardImageImage != null)
		{
			var sprite = TarotSpriteLoader.Load(card.cardId);
			if (sprite != null)
			{
				uiComponent.CardImageImage.sprite = sprite;
				uiComponent.CardImageImage.preserveAspect = true;
				// 逆位旋转 180°
				uiComponent.CardImageImage.rectTransform.localRotation = upright
					? Quaternion.identity
					: Quaternion.Euler(0, 0, 180);
			}
		}
	}

	#endregion

	#region UI组件事件
	public void OnSwitchOracleButtonClick()
	{
		PopulateTodayCard();
	}
	public void OnanyWhereBtnButtonClick()
	{
		HideWindow();
	}
	#endregion
}
