/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/19/2026 11:24:19 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class ConfirmFriendInfoUI : WindowBase
{
	public ConfirmFriendInfoUIComponent uiComponent;

	public enum EditTarget
	{
		None,
		Name,
		Birthday,
		BirthTime,
		City
	}

	public class FriendDraft
	{
		public string name = string.Empty;
		public string birthday = string.Empty;
		public string birthTime = string.Empty;
		public string city = string.Empty;
		public string notes = string.Empty;
		public string avatarImagePath = string.Empty;
		public Sprite avatarSprite;
	}

	private static FriendDraft sPendingDraft;
	private static Action<FriendDraft> sConfirmCallback;
	private static Action<EditTarget> sEditCallback;

	private FriendDraft currentDraft;
	private bool isCreating;

	public static void Show(
		FriendDraft draft,
		Action<FriendDraft> confirmCallback,
		Action<EditTarget> editCallback = null)
	{
		sPendingDraft = draft;
		sConfirmCallback = confirmCallback;
		sEditCallback = editCallback;
		UIModule.Instance.PopUpWindow<ConfirmFriendInfoUI>();
	}

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<ConfirmFriendInfoUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("ConfirmFriendInfoUI 缺少 UI 组件绑定脚本：ConfirmFriendInfoUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		Layer = WindowLayer.Popup;
		this.Canvas.sortingOrder = (int)Layer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		currentDraft = sPendingDraft;
		isCreating = false;
		SetCreateButtonInteractable(true);
		RefreshDraftView();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		currentDraft = null;
		isCreating = false;
		sPendingDraft = null;
		sConfirmCallback = null;
		sEditCallback = null;
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function
	private void RefreshDraftView()
	{
		if (currentDraft == null)
		{
			SetText(uiComponent.FriendNameText, "未命名好友");
			SetText(uiComponent.BirthdayValueText, "未填写");
			SetText(uiComponent.BirthTimeValueText, "未填写");
			SetText(uiComponent.BirthCityValueText, "未填写");
			return;
		}

		SetText(uiComponent.FriendNameText, string.IsNullOrWhiteSpace(currentDraft.name) ? "未命名好友" : currentDraft.name.Trim());
		SetText(uiComponent.BirthdayValueText, FormatOptionalValue(currentDraft.birthday));
		SetText(uiComponent.BirthTimeValueText, FormatOptionalValue(currentDraft.birthTime));
		SetText(uiComponent.BirthCityValueText, FormatOptionalValue(currentDraft.city));

		if (uiComponent.AvatarImage != null && currentDraft.avatarSprite != null)
		{
			uiComponent.AvatarImage.sprite = currentDraft.avatarSprite;
			uiComponent.AvatarImage.preserveAspect = true;
		}
	}

	private void SetCreateButtonInteractable(bool interactable)
	{
		if (uiComponent != null && uiComponent.CreateFriendButton != null)
		{
			uiComponent.CreateFriendButton.interactable = interactable;
		}
	}

	private string FormatOptionalValue(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? "未填写" : value.Trim();
	}

	private void SetText(Text text, string value)
	{
		if (text != null)
		{
			text.text = value;
		}
	}

	private void ReturnToEdit(EditTarget target)
	{
		Action<EditTarget> callback = sEditCallback;
		HideWindow();
		callback?.Invoke(target);
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		ReturnToEdit(EditTarget.None);
	}
	public void OnEditNameButtonClick()
	{
		ReturnToEdit(EditTarget.Name);
	}
	public void OnEditBirthdayButtonClick()
	{
		ReturnToEdit(EditTarget.Birthday);
	}
	public void OnEditBirthTimeButtonClick()
	{
		ReturnToEdit(EditTarget.BirthTime);
	}
	public void OnEditBirthCityButtonClick()
	{
		ReturnToEdit(EditTarget.City);
	}
	public void OnCreateFriendButtonClick()
	{
		if (isCreating) return;
		if (currentDraft == null || string.IsNullOrWhiteSpace(currentDraft.name))
		{
			ToastManager.ShowToast("好友信息缺失，请返回重试");
			return;
		}

		isCreating = true;
		SetCreateButtonInteractable(false);
		Action<FriendDraft> callback = sConfirmCallback;
		callback?.Invoke(currentDraft);
		HideWindow();
	}
	public void OnBackEditButtonClick()
	{
		ReturnToEdit(EditTarget.None);
	}
	#endregion
}
