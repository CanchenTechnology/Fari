/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/20/2026 7:18:27 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using System.Collections;

public class TwoPersonDivinationWaitingFriendFlowUI : WindowBase
{
	public TwoPersonDivinationWaitingFriendFlowUIComponent uiComponent;
	private RelationshipDivinationRecord currentRecord;
	private FriendDataManager.FriendData currentFriend;
	private Coroutine pollCoroutine;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TwoPersonDivinationWaitingFriendFlowUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("TwoPersonDivinationWaitingFriendFlowUI 缺少 UI 组件绑定脚本：TwoPersonDivinationWaitingFriendFlowUIComponent");
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
		StopPolling();
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
		if (HandleTerminalState())
			return;

		Render();
		StartPolling();
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnRemindFriendButtonClick()
	{
		RelationshipDivinationFlow.CopyInviteText(currentRecord, "提醒文案已复制，可以发给好友");
	}
	#endregion

	private void Render()
	{
		if (uiComponent == null || currentRecord == null) return;

		string uid = RelationshipDivinationFlow.GetCurrentUid();
		string otherName = RelationshipDivinationFlow.GetOtherName(currentRecord, uid);
		bool friendRevealed = RelationshipDivinationFlow.IsFriendCardRevealed(currentRecord);

		if (uiComponent.WaitingTitleText != null)
			uiComponent.WaitingTitleText.text = friendRevealed ? "等待共同揭示开启" : $"等待 {otherName} 翻牌";
		if (uiComponent.FriendCardStateText != null)
			uiComponent.FriendCardStateText.text = friendRevealed ? "对方已翻开，正在同步结果" : $"等待 {otherName} 翻开自己的牌";
		if (uiComponent.FriendNameText != null)
			uiComponent.FriendNameText.text = otherName;
		if (uiComponent.FriendStateTextText != null)
			uiComponent.FriendStateTextText.text = friendRevealed ? "已翻牌" : "等待翻牌";

		RelationshipDivinationFlow.SetButtonText(uiComponent.RemindFriendButton, "提醒好友");
		ApplyAvatar(uiComponent.FriendStateAvatarImage, ResolveFriendAvatar(currentFriend));
		LoadFriendRemoteAvatarIfNeeded();
		LoadUserAvatar();

		if (RelationshipDivinationFlow.IsMyCardRevealed(currentRecord))
			ApplyCardSprite(uiComponent.MyCardSlotImage, RelationshipDivinationFlow.GetMyPrivateCard(currentRecord));
		if (currentRecord.IsCompleted)
			ApplyCardSprite(uiComponent.FriendCardSlotImage, RelationshipDivinationFlow.GetFriendPrivateCard(currentRecord));
	}

	private void StartPolling()
	{
		StopPolling();
		if (uiComponent != null && currentRecord != null && !currentRecord.isLocalOnly && !currentRecord.IsCompleted && !currentRecord.IsCancelled)
		{
			pollCoroutine = uiComponent.StartCoroutine(PollRoutine());
			RefreshRemoteRecord();
		}
	}

	private void StopPolling()
	{
		if (pollCoroutine != null && uiComponent != null)
			uiComponent.StopCoroutine(pollCoroutine);
		pollCoroutine = null;
	}

	private IEnumerator PollRoutine()
	{
		while (currentRecord != null && gameObject != null && gameObject.activeInHierarchy)
		{
			yield return new WaitForSeconds(8f);
			RefreshRemoteRecord();
		}
	}

		private void RefreshRemoteRecord()
		{
			RelationshipDivinationFlow.RefreshCurrentRecord(updated =>
			{
				if (updated == null || gameObject == null || !gameObject.activeInHierarchy) return;

				currentRecord = updated;
				if (HandleTerminalState())
					return;

				Render();
			});
		}

		private bool HandleTerminalState()
		{
			if (currentRecord == null) return false;

			if (currentRecord.IsCancelled)
			{
				ToastManager.ShowToast("这次双人占卜邀请已取消");
				RelationshipDivinationFlow.HideFlowWindows();
				return true;
			}

			if (currentRecord.IsCompleted || currentRecord.isLocalOnly)
			{
				RelationshipDivinationFlow.ShowRevealReady(currentRecord, currentFriend);
				return true;
			}

			if (RelationshipDivinationFlow.IsInviteExpired(currentRecord))
			{
				ToastManager.ShowToast("这次双人占卜邀请已过期");
				RelationshipDivinationFlow.HideFlowWindows();
				return true;
			}

			return false;
		}

	private void ApplyCardSprite(Image target, RelationshipDivinationCard card)
	{
		Sprite sprite = RelationshipDivinationFlow.LoadCardSprite(card);
		if (target != null && sprite != null)
			target.sprite = sprite;
	}

	private void ApplyAvatar(Image target, Sprite sprite)
	{
		FriendAvatarImageUtility.ApplyAvatar(target, sprite);
	}

	private Sprite ResolveFriendAvatar(FriendDataManager.FriendData friend)
	{
		return FriendAvatarImageUtility.ResolveFriendAvatar(friend, uiComponent?.FriendStateAvatarImage);
	}

	private void LoadFriendRemoteAvatarIfNeeded()
	{
		if (currentFriend == null
			|| currentFriend.headSprite != null
			|| string.IsNullOrWhiteSpace(currentFriend.photoUrl)
			|| uiComponent == null)
			return;

		FriendDataManager.FriendData friend = currentFriend;
		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadSpriteFromUrlCoroutine(currentFriend.photoUrl, sprite =>
		{
			if (currentFriend != friend || sprite == null) return;
			currentFriend.headSprite = sprite;
			ApplyAvatar(uiComponent.FriendStateAvatarImage, sprite);
		}));
	}

	private void LoadUserAvatar()
	{
		if (uiComponent?.YouStateAvatarImage == null || uiComponent == null) return;
		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadCurrentUserAvatarCoroutine((sprite, _) =>
		{
			ApplyAvatar(uiComponent.YouStateAvatarImage, sprite);
		}));
	}
}
