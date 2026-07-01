/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/30/2026 3:07:09 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using System;
using System.Collections;
using TMPro;

public class TwoPersonDivinationUI : WindowBase
{
	private static RelationshipDivinationRecord sPendingRecord;
	private static FriendDataManager.FriendData sPendingFriend;

	public TwoPersonDivinationUIComponent uiComponent;
	private RelationshipDivinationRecord currentRecord;
	private FriendDataManager.FriendData currentFriend;
	private bool isProcessing;
	private bool isRefreshingRemote;
	private Coroutine pollCoroutine;
	private int avatarRequestVersion;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TwoPersonDivinationUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("TwoPersonDivinationUI 缺少 UI 组件绑定脚本：TwoPersonDivinationUIComponent");
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
		currentRecord = sPendingRecord ?? RelationshipDivinationFlow.CurrentRecord;
		currentFriend = sPendingFriend ?? RelationshipDivinationFlow.CurrentFriend;
		isProcessing = false;
		avatarRequestVersion++;
		Render();
		StartPolling();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		StopPolling();
		avatarRequestVersion++;
		isProcessing = false;
		isRefreshingRemote = false;
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		StopPolling();
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public static TwoPersonDivinationUI Show(RelationshipDivinationRecord record, FriendDataManager.FriendData friend = null)
	{
		if (record == null)
		{
			ToastManager.ShowToast("双人占卜记录不存在");
			return null;
		}

		sPendingRecord = record;
		sPendingFriend = friend;
		TwoPersonDivinationUI window = UIModule.Instance.PopUpWindow<TwoPersonDivinationUI>();
		window?.SetRecord(record, friend);
		return window;
	}

	public void SetRecord(RelationshipDivinationRecord record, FriendDataManager.FriendData friend = null)
	{
		if (record == null)
			return;

		sPendingRecord = record;
		sPendingFriend = friend;
		currentRecord = record;
		currentFriend = friend ?? currentFriend ?? RelationshipDivinationFlow.CurrentFriend;
		isProcessing = false;
		RelationshipDivinationFlow.UpdateCurrentRecordState(record, currentFriend);

		if (gameObject != null && gameObject.activeInHierarchy)
		{
			Render();
			StartPolling();
		}
	}

	#endregion

	#region UI组件事件
	public void OnbackButtonClick()
	{
		HideWindow();
	}
	public void OnsettingButtonClick()
	{
		if (currentRecord == null)
		{
			ToastManager.ShowToast("暂无邀请信息");
			return;
		}

		if (currentRecord.isLocalOnly)
		{
			ToastManager.ShowToast("本地关系占卜无需发送邀请");
			return;
		}

		RelationshipDivinationFlow.CopyInviteText(currentRecord);
	}
	public void OnFlipButtonClick()
	{
		if (isProcessing || currentRecord == null)
			return;

		if (currentRecord.IsCompleted || currentRecord.isLocalOnly)
		{
			RelationshipDivinationFlow.OpenResult(currentRecord);
			return;
		}

		if (RelationshipDivinationFlow.IsInviteExpired(currentRecord))
		{
			ToastManager.ShowToast("邀请已过期，请重新发起双人占卜");
			Render();
			return;
		}

		string uid = RelationshipDivinationFlow.GetCurrentUid();
		if (!currentRecord.CanCurrentUserReveal(uid))
		{
			ToastManager.ShowToast(RelationshipDivinationFlow.IsMyCardRevealed(currentRecord) ? "已翻开你的牌，等待对方完成" : "当前没有可翻开的牌");
			Render();
			return;
		}

		DrawMyCardUI.Show(currentRecord, currentFriend);
	}
	#endregion

	private void Render()
	{
		if (uiComponent == null)
			return;

		RenderFriendInfo();
		RenderMeta();
		RenderCards();
		UpdateFlipButton();
	}

	private void StartPolling()
	{
		StopPolling();
		if (!ShouldPollRemote())
			return;

		pollCoroutine = uiComponent.StartCoroutine(PollRoutine());
		RefreshRemoteRecord();
	}

	private void StopPolling()
	{
		if (pollCoroutine != null && uiComponent != null)
			uiComponent.StopCoroutine(pollCoroutine);
		pollCoroutine = null;
	}

	private IEnumerator PollRoutine()
	{
		while (gameObject != null && gameObject.activeInHierarchy && ShouldPollRemote())
		{
			yield return new WaitForSeconds(6f);
			RefreshRemoteRecord();
		}

		pollCoroutine = null;
	}

	private bool ShouldPollRemote()
	{
		return uiComponent != null
			&& currentRecord != null
			&& !currentRecord.isLocalOnly
			&& !currentRecord.IsCancelled
			&& !currentRecord.IsCompleted;
	}

	private void RefreshRemoteRecord()
	{
		if (isRefreshingRemote || currentRecord == null || currentRecord.isLocalOnly)
			return;

		isRefreshingRemote = true;
		string readingId = currentRecord.readingId;
		RelationshipDivinationFlow.RefreshCurrentRecord(updated =>
		{
			isRefreshingRemote = false;
			if (updated == null || gameObject == null || !gameObject.activeInHierarchy)
				return;

			if (!string.IsNullOrWhiteSpace(readingId)
				&& !string.Equals(readingId, updated.readingId, StringComparison.Ordinal))
			{
				return;
			}

			currentRecord = updated;
			sPendingRecord = updated;
			RelationshipDivinationFlow.UpdateCurrentRecordState(updated, currentFriend);
			Render();
			if (!ShouldPollRemote())
				StopPolling();
		});
	}

	private void RenderFriendInfo()
	{
		FriendDataManager.FriendData friend = currentFriend;
		string uid = RelationshipDivinationFlow.GetCurrentUid();
		string friendName = friend != null
			? RelationshipDivinationFlow.GetFriendName(friend)
			: RelationshipDivinationFlow.GetOtherName(currentRecord, uid);

		SetText(uiComponent.friendName, friendName);
		SetText(uiComponent.friendNameInCard, friendName);

		if (uiComponent.infoText != null)
			uiComponent.infoText.text = BuildStatusText();

		Sprite avatar = friend != null
			? FriendAvatarImageUtility.ResolveFriendAvatar(friend, uiComponent.friendHead)
			: null;
		FriendAvatarImageUtility.ApplyAvatar(uiComponent.friendHead, avatar);
		LoadRemoteAvatarIfNeeded();
	}

	private void RenderMeta()
	{
		SetText(uiComponent.divinationType, ExtractDirectionLabel(currentRecord?.question));
		SetText(uiComponent.timeType, FormatDisplayTime(currentRecord?.createdAt));
	}

	private void RenderCards()
	{
		string friendName = GetFriendDisplayName();
		RenderSlot(uiComponent.cardSlotitem1, RelationshipDivinationFlow.GetMyPrivateCard(currentRecord), RelationshipDivinationFlow.IsMyCardRevealed(currentRecord), "你");
		RenderSlot(uiComponent.cardSlotitem2, currentRecord?.SharedCard, currentRecord != null && (currentRecord.IsCompleted || currentRecord.isLocalOnly), "结果");
		RenderSlot(uiComponent.cardSlotitem3, RelationshipDivinationFlow.GetFriendPrivateCard(currentRecord), RelationshipDivinationFlow.IsFriendCardRevealed(currentRecord), friendName);
	}

	private void RenderSlot(CardSlotItem slot, RelationshipDivinationCard card, bool isVisible, string label)
	{
		if (slot == null)
			return;

		if (card != null && isVisible)
		{
			slot.ShowFace(RelationshipDivinationFlow.LoadCardSprite(card), label, card.IsUpright);
			return;
		}

		slot.ShowBack(null, label);
	}

	private void UpdateFlipButton()
	{
		if (uiComponent?.flipBtn == null)
			return;

		string label = "翻开我的牌";
		bool interactable = false;

		if (currentRecord == null)
		{
			label = "暂无占卜";
		}
		else if (isProcessing)
		{
			label = "翻牌中...";
		}
		else if (currentRecord.IsCompleted || currentRecord.isLocalOnly)
		{
			label = "查看占卜结果";
			interactable = true;
		}
		else if (RelationshipDivinationFlow.IsInviteExpired(currentRecord))
		{
			label = "邀请已过期";
		}
			else if (currentRecord.CanCurrentUserReveal(RelationshipDivinationFlow.GetCurrentUid()))
			{
				label = currentRecord.IsCurrentUserReceiver(RelationshipDivinationFlow.GetCurrentUid())
					? "抽取我的牌"
					: "翻开我的牌";
				interactable = true;
			}
		else if (RelationshipDivinationFlow.IsMyCardRevealed(currentRecord))
		{
			label = "等待对方翻牌";
		}

		uiComponent.flipBtn.interactable = interactable && !isProcessing;
		RelationshipDivinationFlow.SetButtonText(uiComponent.flipBtn, label);
	}

	private string BuildStatusText()
	{
		if (currentRecord == null)
			return "双人占卜";
		if (currentRecord.isLocalOnly)
			return "本地关系占卜 · 已完成";
		if (currentRecord.IsCompleted)
			return "双方已完成翻牌";
		if (RelationshipDivinationFlow.IsInviteExpired(currentRecord))
			return "邀请已过期";
			if (currentRecord.CanCurrentUserReveal(RelationshipDivinationFlow.GetCurrentUid()))
				return currentRecord.IsCurrentUserReceiver(RelationshipDivinationFlow.GetCurrentUid())
					? "好友已翻开牌，等待你抽牌"
					: "邀请已发送，先翻开你的牌";
		if (RelationshipDivinationFlow.IsMyCardRevealed(currentRecord))
			return "已翻开你的牌，等待好友";
		return currentRecord.GetStatusText(RelationshipDivinationFlow.GetCurrentUid());
	}

	private string GetFriendDisplayName()
	{
		string uid = RelationshipDivinationFlow.GetCurrentUid();
		return currentFriend != null
			? RelationshipDivinationFlow.GetFriendName(currentFriend)
			: RelationshipDivinationFlow.GetOtherName(currentRecord, uid);
	}

	private string ExtractDirectionLabel(string question)
	{
		if (!string.IsNullOrWhiteSpace(question))
		{
			int start = question.IndexOf('「');
			int end = question.IndexOf('」');
			if (start >= 0 && end > start)
				return question.Substring(start + 1, end - start - 1).Trim();
		}

		return "双人占卜";
	}

	private string FormatDisplayTime(string value)
	{
		if (DateTime.TryParse(value, out DateTime parsed))
			return $"{parsed.Year}.{parsed.Month}.{parsed.Day}";
		return string.IsNullOrWhiteSpace(value) ? DateTime.Now.ToString("yyyy.M.d") : value.Trim();
	}

	private void LoadRemoteAvatarIfNeeded()
	{
		if (currentFriend == null
			|| currentFriend.headSprite != null
			|| string.IsNullOrWhiteSpace(currentFriend.photoUrl)
			|| uiComponent == null)
			return;

		int requestId = ++avatarRequestVersion;
		FriendDataManager.FriendData friend = currentFriend;
		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadUserSpriteFromUrlCoroutine(currentFriend.name, currentFriend.photoUrl, sprite =>
		{
			if (requestId != avatarRequestVersion || currentFriend != friend || sprite == null)
				return;

			currentFriend.headSprite = sprite;
			FriendAvatarImageUtility.ApplyAvatar(uiComponent.friendHead, sprite);
		}));
	}

	private void SetText(TMP_Text text, string value)
	{
		if (text != null)
			text.text = value ?? string.Empty;
	}
}
