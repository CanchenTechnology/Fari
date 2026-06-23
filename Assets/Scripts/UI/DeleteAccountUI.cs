/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/21/2026 2:09:31 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using TMPro;

public class DeleteAccountUI : WindowBase
{
	public DeleteAccountUIComponent uiComponent;
	private static DeleteConfirmConfig sPendingConfig;

	private DeleteConfirmConfig _currentConfig;
	private Action _confirmAction;
	private bool _isSubmitting;

	private class DeleteConfirmConfig
	{
		public string title;
		public string deleteButtonText;
		public string headline;
		public string description;
		public string listHeader;
		public string toggleText;
		public string item1Title;
		public string item1Desc;
		public string item2Title;
		public string item2Desc;
		public string item3Title;
		public string item3Desc;
		public string item4Title;
		public string item4Desc;
		public string item5Title;
		public string item5Desc;
		public Action onConfirm;
	}

	public static void ShowDeleteFriend(FriendDataManager.FriendData friend, Action onConfirm)
	{
		if (friend == null)
		{
			ToastManager.ShowToast("好友资料不完整");
			return;
		}

		string name = FormatFriendName(friend);
		bool isVirtual = friend.isVirtual;
		sPendingConfig = new DeleteConfirmConfig
		{
			title = "删除好友",
			deleteButtonText = "删除好友",
			headline = $"确认删除「{name}」？",
			description = isVirtual
				? $"此操作无法撤销。删除后，「{name}」会从你创建的好友列表中移除。"
				: $"此操作无法撤销。删除后，你和「{name}」的好友关系会解除。",
			listHeader = "删除后将会：",
			toggleText = "我已理解，确认删除该好友",
			item1Title = "好友资料",
			item1Desc = isVirtual ? "你创建的好友资料会从列表中移除。" : "该好友会从你的好友列表中移除。",
			item2Title = "好友关系",
			item2Desc = isVirtual ? "与该创建好友相关的入口会关闭。" : "双方好友关系会解除，需要重新添加才能恢复。",
			item3Title = "关系占卜",
			item3Desc = "不能再从好友资料页发起新的双人占卜。",
			item4Title = "互动入口",
			item4Desc = "好友资料、聊天跳转等互动入口会从当前列表消失。",
			item5Title = "本地列表",
			item5Desc = isVirtual ? "该创建好友会从本地列表中移除。" : "该好友会从当前好友列表中移除。",
			onConfirm = onConfirm
		};

		UIModule.Instance.PopUpWindow<DeleteAccountUI>();
	}

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<DeleteAccountUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("DeleteAccountUI 缺少 UI 组件绑定脚本：DeleteAccountUIComponent");
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
		_currentConfig = sPendingConfig;
		sPendingConfig = null;
		_confirmAction = _currentConfig?.onConfirm;
		_isSubmitting = false;
		ResetConfirmToggle();
		ApplyConfigText();
		RefreshConfirmButton();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		_confirmAction = null;
		_currentConfig = null;
		_isSubmitting = false;
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
	public void OnconfirmToggleChange(bool state, Toggle toggle)
	{
		RefreshConfirmButton();
	}
	public void OnDeleteAccountButtonClick()
	{
		if (_isSubmitting) return;
		if (uiComponent?.confirmToggle != null && !uiComponent.confirmToggle.isOn)
		{
			ToastManager.ShowToast("请先勾选确认");
			return;
		}

		_isSubmitting = true;
		RefreshConfirmButton();
		Action callback = _confirmAction;
		HideWindow();
		callback?.Invoke();
	}
	public void OnCancelButtonClick()
	{
		HideWindow();
	}
	#endregion

	private void ResetConfirmToggle()
	{
		if (uiComponent?.confirmToggle == null) return;
		uiComponent.confirmToggle.SetIsOnWithoutNotify(false);
	}

	private void RefreshConfirmButton()
	{
		if (uiComponent?.DeleteAccountButton == null) return;
		bool confirmed = uiComponent.confirmToggle == null || uiComponent.confirmToggle.isOn;
		uiComponent.DeleteAccountButton.interactable = !_isSubmitting && confirmed && _confirmAction != null;
	}

	private void ApplyConfigText()
	{
		if (_currentConfig == null) return;

		SetTextByName("TitleText", _currentConfig.title);
		SetTextByName("DeleteButtonText", _currentConfig.deleteButtonText);
		SetTextByName("ConfirmHeadline", _currentConfig.headline);
		SetTextByName("ConfirmDescription", _currentConfig.description);
		SetTextByName("DeletedDataTitle", _currentConfig.listHeader);
		SetTextByName("CancelButtonText", "取消");
		SetToggleLabel(_currentConfig.toggleText);
		SetTextByName("ProfileDataTitle", _currentConfig.item1Title);
		SetTextByName("ProfileDataDesc", _currentConfig.item1Desc);
		SetTextByName("AccountInfoTitle", _currentConfig.item2Title);
		SetTextByName("AccountInfoDesc", _currentConfig.item2Desc);
		SetTextByName("TarotHistoryTitle", _currentConfig.item3Title);
		SetTextByName("TarotHistoryDesc", _currentConfig.item3Desc);
		SetTextByName("DialogHistoryTitle", _currentConfig.item4Title);
		SetTextByName("DialogHistoryDesc", _currentConfig.item4Desc);
		SetTextByName("MemoryDataTitle", _currentConfig.item5Title);
		SetTextByName("MemoryDataDesc", _currentConfig.item5Desc);
	}

	private void SetTextByName(string objectName, string value)
	{
		foreach (Transform child in gameObject.GetComponentsInChildren<Transform>(true))
		{
			if (child == null || child.name != objectName) continue;
			SetText(child.gameObject, value);
			return;
		}
	}

	private void SetToggleLabel(string value)
	{
		if (uiComponent?.confirmToggle == null) return;
		foreach (TMP_Text text in uiComponent.confirmToggle.GetComponentsInChildren<TMP_Text>(true))
		{
			if (text != null && text.gameObject.name == "Label")
			{
				text.text = value ?? string.Empty;
				return;
			}
		}

		foreach (TMP_Text text in uiComponent.confirmToggle.GetComponentsInChildren<TMP_Text>(true))
		{
			if (text != null && text.gameObject.name == "Label")
			{
				text.text = value ?? string.Empty;
				return;
			}
		}
	}

	private void SetText(GameObject target, string value)
	{
		if (target == null) return;
		TMP_Text text = target.GetComponent<TMP_Text>();
		if (text != null)
		{
			text.text = value ?? string.Empty;
			return;
		}

		TMP_Text tmp = target.GetComponent<TMP_Text>();
		if (tmp != null)
		{
			tmp.text = value ?? string.Empty;
		}
	}

	private static string FormatFriendName(FriendDataManager.FriendData friend)
	{
		return string.IsNullOrWhiteSpace(friend?.name) ? "好友" : friend.name.Trim();
	}
}
