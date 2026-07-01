/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/20/2026 7:27:58 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using System.Collections;

public class TwoPersonDivinationInviteSentFlowUI : WindowBase
{
	public TwoPersonDivinationInviteSentFlowUIComponent uiComponent;
	private RelationshipDivinationRecord currentRecord;
	private FriendDataManager.FriendData currentFriend;
	private Coroutine countdownCoroutine;
	private bool isProcessing;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TwoPersonDivinationInviteSentFlowUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("TwoPersonDivinationInviteSentFlowUI 缺少 UI 组件绑定脚本：TwoPersonDivinationInviteSentFlowUIComponent");
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
		StopCountdown();
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
		isProcessing = false;
		if (HandleTerminalState())
			return;

		Render();
		StartCountdown();
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnCancelInviteButtonClick()
	{
		if (isProcessing || currentRecord == null) return;
		if (currentRecord.isLocalOnly)
		{
			HideWindow();
			return;
		}

		RelationshipDivinationFirestore service = RelationshipDivinationFlow.GetOrCreateService();
		if (service == null)
		{
			ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
			return;
		}

		isProcessing = true;
		UpdateButtons();
		service.CancelInvite(currentRecord, success =>
		{
			isProcessing = false;
			UpdateButtons();
			ToastManager.ShowToast(success ? "已取消双人占卜邀请" : "取消失败，请稍后再试");
			if (success) RelationshipDivinationFlow.HideFlowWindows();
		});
	}
		public void OnCopyInviteLinkButtonClick()
		{
			RelationshipDivinationFlow.CopyInviteText(currentRecord);
		}
		public void OnFlipMyCardButtonClick()
		{
			if (isProcessing || currentRecord == null) return;
			if (RelationshipDivinationFlow.GetInviteRemaining(currentRecord) <= System.TimeSpan.Zero && !currentRecord.isLocalOnly)
			{
				ToastManager.ShowToast("邀请已过期，请重新发起双人占卜");
				return;
			}

			if (RelationshipDivinationFlow.IsMyCardRevealed(currentRecord))
			{
				RelationshipDivinationFlow.ShowRecord(currentRecord, currentFriend);
				return;
			}

			if (!currentRecord.CanCurrentUserReveal(RelationshipDivinationFlow.GetCurrentUid()))
			{
				RelationshipDivinationFlow.ShowRecord(currentRecord, currentFriend);
				return;
			}

			DrawMyCardUI.Show(currentRecord, currentFriend);
		}
	#endregion

	private void Render()
	{
		if (uiComponent == null || currentRecord == null) return;

		string uid = RelationshipDivinationFlow.GetCurrentUid();
		bool isReceiver = currentRecord.IsCurrentUserReceiver(uid);
		string otherName = RelationshipDivinationFlow.GetOtherName(currentRecord, uid);

		if (uiComponent.WaitingAcceptTitleText != null)
			uiComponent.WaitingAcceptTitleText.text = isReceiver ? $"来自 {otherName} 的邀请" : $"等待 {otherName} 接受";
		if (uiComponent.InviteCountdownText != null)
			uiComponent.InviteCountdownText.text = RelationshipDivinationFlow.FormatRemaining(RelationshipDivinationFlow.GetInviteRemaining(currentRecord));

		RelationshipDivinationFlow.SetButtonText(uiComponent.CancelInviteButton, isReceiver ? "拒绝邀请" : "取消邀请");
		RelationshipDivinationFlow.SetButtonText(uiComponent.CopyInviteLinkButton, "复制邀请链接");
			string flipButtonText = RelationshipDivinationFlow.IsMyCardRevealed(currentRecord)
				? "进入占卜"
				: isReceiver ? "接受并抽取我的牌" : "先翻开我的牌";
			RelationshipDivinationFlow.SetButtonText(uiComponent.FlipMyCardButton, flipButtonText);

		if (RelationshipDivinationFlow.IsMyCardRevealed(currentRecord))
			ApplyCardSprite(uiComponent.MyCardImage, RelationshipDivinationFlow.GetMyPrivateCard(currentRecord));

		UpdateButtons();
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
			ToastManager.ShowToast("邀请已过期，请重新发起双人占卜");
			RelationshipDivinationFlow.HideFlowWindows();
			return true;
		}

		return false;
	}

	private void UpdateButtons()
	{
		if (uiComponent == null || currentRecord == null) return;

		string uid = RelationshipDivinationFlow.GetCurrentUid();
			bool expired = RelationshipDivinationFlow.GetInviteRemaining(currentRecord) <= System.TimeSpan.Zero && !currentRecord.isLocalOnly;
			bool canReveal = !expired && currentRecord.CanCurrentUserReveal(uid);
			if (uiComponent.FlipMyCardButton != null)
				uiComponent.FlipMyCardButton.interactable = !isProcessing && (canReveal || RelationshipDivinationFlow.IsMyCardRevealed(currentRecord));
		if (uiComponent.CancelInviteButton != null)
			uiComponent.CancelInviteButton.interactable = !isProcessing && !currentRecord.isLocalOnly && !currentRecord.IsCompleted && !currentRecord.IsCancelled;
		if (uiComponent.CopyInviteLinkButton != null)
			uiComponent.CopyInviteLinkButton.interactable = !isProcessing && !currentRecord.isLocalOnly;
	}

	private void ApplyCardSprite(Image target, RelationshipDivinationCard card)
	{
		Sprite sprite = RelationshipDivinationFlow.LoadCardSprite(card);
		if (target != null && sprite != null)
			target.sprite = sprite;
	}

	private void StartCountdown()
	{
		StopCountdown();
		if (uiComponent != null && currentRecord != null)
			countdownCoroutine = uiComponent.StartCoroutine(CountdownRoutine());
	}

	private void StopCountdown()
	{
		if (countdownCoroutine != null && uiComponent != null)
			uiComponent.StopCoroutine(countdownCoroutine);
		countdownCoroutine = null;
	}

	private IEnumerator CountdownRoutine()
	{
		while (currentRecord != null && gameObject != null && gameObject.activeInHierarchy)
		{
			if (HandleTerminalState())
				yield break;

			if (uiComponent?.InviteCountdownText != null)
				uiComponent.InviteCountdownText.text = RelationshipDivinationFlow.FormatRemaining(RelationshipDivinationFlow.GetInviteRemaining(currentRecord));
			UpdateButtons();
			yield return new WaitForSeconds(1f);
		}
	}
}
