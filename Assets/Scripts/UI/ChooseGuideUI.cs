/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 11:22:15
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class ChooseGuideUI : WindowBase
{
	public ChooseGuideUIComponent uiComponent;
	private int selectedRoleId = (int)CharacterType.TarotReader;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<ChooseGuideUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		selectedRoleId = RoleManager.Instance != null
			? RoleManager.Instance.CurrentRoleId
			: (int)CharacterType.TarotReader;
		RefreshGuideButtons();
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
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnAstrologerButtonClick()
	{
		SelectGuide(CharacterType.Astrologer);
	}
	public void OnSageButtonClick()
	{
		SelectGuide(CharacterType.Meditator);
	}
	public void OnTarotButtonClick()
	{
		SelectGuide(CharacterType.TarotReader);
	}
	public void OnContinueButtonClick()
	{
		ApplySelectedGuide();
		UIModule.Instance.PopUpWindow<RegisterFindFriendsUI>();
	}

	private void SelectGuide(CharacterType type)
	{
		selectedRoleId = (int)type;
		ApplySelectedGuide();
		RefreshGuideButtons();
	}

	private void ApplySelectedGuide()
	{
		if (RoleManager.Instance != null)
			RoleManager.Instance.ChangeRole(selectedRoleId);
	}

	private void RefreshGuideButtons()
	{
		SetGuideButtonState(uiComponent?.TarotButton, selectedRoleId == (int)CharacterType.TarotReader);
		SetGuideButtonState(uiComponent?.AstrologerButton, selectedRoleId == (int)CharacterType.Astrologer);
		SetGuideButtonState(uiComponent?.SageButton, selectedRoleId == (int)CharacterType.Meditator);
	}

	private void SetGuideButtonState(Button button, bool selected)
	{
		if (button == null) return;
		button.interactable = !selected;
		if (button.image != null)
			button.image.color = selected ? new Color(0.74f, 0.60f, 1f, 1f) : Color.white;
	}
	#endregion
}
