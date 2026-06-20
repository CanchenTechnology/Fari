/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/20/2026 10:47:05 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class FriendMoveUI : WindowBase
{
	public FriendMoveUIComponent uiComponent;
	private static FriendDataManager.FriendData sPendingFriend;

	private FriendDataManager.FriendData currentFriend;
	private bool isProcessing;

	public static void Show(FriendDataManager.FriendData friend)
	{
		if (friend == null)
		{
			ToastManager.ShowToast("好友资料不完整");
			return;
		}

		sPendingFriend = friend;
		UIModule.Instance.PopUpWindow<FriendMoveUI>();
	}

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<FriendMoveUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("FriendMoveUI 缺少 UI 组件绑定脚本：FriendMoveUIComponent");
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
		currentFriend = sPendingFriend;
		isProcessing = false;
		RefreshView();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		currentFriend = null;
		sPendingFriend = null;
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
	public void OnViewFriendButtonClick()
	{
		if (currentFriend == null)
		{
			ToastManager.ShowToast("好友资料不完整");
			return;
		}

		if (currentFriend.isVirtual)
		{
			HideWindow();
			CreateFriendInfoUI.Show(currentFriend);
			return;
		}

		if (CanOpenRealFriendProfile(currentFriend))
		{
			HideWindow();
			FriendProfileUI.Show(currentFriend);
			return;
		}

		ToastManager.ShowToast("好友关系确认后才能查看资料");
	}
	public void OnSendOracleRelatonButtonClick()
	{
		if (currentFriend == null || isProcessing)
		{
			return;
		}

		if (!RelationshipDivinationFlow.CanUseTwoPersonDivination(currentFriend, true))
		{
			RefreshView();
			return;
		}

		if (!currentFriend.isVirtual && !CanOpenRealFriendProfile(currentFriend))
		{
			ToastManager.ShowToast("好友关系确认后才能发起双人占卜");
			RefreshView();
			return;
		}

		FriendDataManager.FriendData captured = currentFriend;
		HideWindow();
		RelationshipDivinationOverlay.StartForFriend(transform, captured);
	}
	public void OnDeleteFriendButtonClick()
	{
		if (currentFriend == null || isProcessing)
		{
			return;
		}

		ShowDeleteConfirm(currentFriend);
	}
	public void OnCancelButtonClick()
	{
		HideWindow();
	}
	#endregion

	private void RefreshView()
	{
		if (uiComponent == null) return;

		bool hasFriend = currentFriend != null;
		bool canOpenProfile = hasFriend && (currentFriend.isVirtual || CanOpenRealFriendProfile(currentFriend));
		bool canUseRelationshipDivination = hasFriend
			&& (currentFriend.isVirtual || CanOpenRealFriendProfile(currentFriend))
			&& RelationshipDivinationFlow.CanUseTwoPersonDivination(currentFriend, false);

		SetButtonVisible(uiComponent.ViewFriendButton, canOpenProfile);
		SetButtonVisible(uiComponent.SendOracleRelatonButton, canUseRelationshipDivination);
		SetButtonVisible(uiComponent.DeleteFriendButton, hasFriend);
		SetButtonVisible(uiComponent.CancelButton, true);

		SetButtonInteractable(uiComponent.ViewFriendButton, !isProcessing && canOpenProfile);
		SetButtonInteractable(uiComponent.SendOracleRelatonButton, !isProcessing && canUseRelationshipDivination);
		SetButtonInteractable(uiComponent.DeleteFriendButton, !isProcessing && hasFriend);
		SetButtonInteractable(uiComponent.CancelButton, !isProcessing);

		SetButtonText(uiComponent.ViewFriendButton, currentFriend != null && currentFriend.isVirtual ? "编辑好友资料" : "查看好友资料");
		SetButtonText(uiComponent.SendOracleRelatonButton, "发起关系占卜");
		SetButtonText(uiComponent.DeleteFriendButton, "删除好友");
		SetButtonText(uiComponent.CancelButton, "取消");
	}

	private void ShowDeleteConfirm(FriendDataManager.FriendData friend)
	{
		if (friend == null) return;

		string name = FormatFriendName(friend);
		string content = friend.isVirtual
			? $"确定删除「{name}」吗？删除后会从你创建的好友列表中移除。"
			: $"确定删除「{name}」吗？双方好友关系会解除。";

		SelectWindow selectWindow = UIModule.Instance.PopUpWindow<SelectWindow>();
		if (selectWindow == null)
		{
			ToastManager.ShowToast("确认窗口打开失败");
			return;
		}

		selectWindow.InitViewState(
			SelectType.Normal,
			content,
			() => BeginDeleteFriend(friend),
			null,
			"删除",
			"取消",
			"删除好友",
			TextAnchor.MiddleCenter);
	}

	private void BeginDeleteFriend(FriendDataManager.FriendData friend)
	{
		if (friend == null || isProcessing)
		{
			return;
		}

		isProcessing = true;
		RefreshView();

		if (friend.isVirtual)
		{
			DeleteVirtualFriend(friend);
			return;
		}

		DeleteRealFriend(friend);
	}

	private void DeleteVirtualFriend(FriendDataManager.FriendData friend)
	{
		if (FirestoreManager.Instance != null && FirestoreManager.Instance.IsInitialized)
		{
			ToastManager.ShowToast($"正在删除 {FormatFriendName(friend)}");
			FirestoreManager.Instance.DeleteVirtualFriend(friend, success =>
			{
				isProcessing = false;
				RefreshView();
				if (!success)
				{
					ToastManager.ShowToast("云端删除失败，已保留本地好友");
					return;
				}

				ToastManager.ShowToast("已删除创建的好友");
				HideWindow();
			});
			return;
		}

		bool removed = FriendDataManager.Instance != null && FriendDataManager.Instance.RemoveVirtualFriend(friend.id);
		isProcessing = false;
		RefreshView();
		ToastManager.ShowToast(removed ? "已删除本地创建好友" : "删除失败");
		if (removed)
			HideWindow();
	}

	private void DeleteRealFriend(FriendDataManager.FriendData friend)
	{
		if (FirestoreManager.Instance == null || !FirestoreManager.Instance.IsInitialized)
		{
			ToastManager.ShowToast("好友服务未初始化，无法删除真实好友");
			isProcessing = false;
			RefreshView();
			return;
		}

		ToastManager.ShowToast($"正在删除 {FormatFriendName(friend)}");
		FirestoreManager.Instance.RemoveRealFriend(friend, success =>
		{
			isProcessing = false;
			RefreshView();
			ToastManager.ShowToast(success ? "已删除好友" : "删除好友失败，请稍后再试");
			if (success)
				HideWindow();
		});
	}

	private bool CanOpenRealFriendProfile(FriendDataManager.FriendData friend)
	{
		return FriendProfileUI.CanShowForFriend(friend);
	}

	private string FormatFriendName(FriendDataManager.FriendData friend)
	{
		return string.IsNullOrWhiteSpace(friend?.name) ? "好友" : friend.name.Trim();
	}

	private void SetButtonVisible(Button button, bool visible)
	{
		if (button != null)
			button.gameObject.SetActive(visible);
	}

	private void SetButtonInteractable(Button button, bool interactable)
	{
		if (button != null)
			button.interactable = interactable;
	}

	private void SetButtonText(Button button, string text)
	{
		if (button == null) return;
		Text label = button.GetComponentInChildren<Text>(true);
		if (label != null)
			label.text = text ?? string.Empty;
	}
}
