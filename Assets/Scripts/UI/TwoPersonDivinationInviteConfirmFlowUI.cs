/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/20/2026 7:04:50 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class TwoPersonDivinationInviteConfirmFlowUI : WindowBase
{
	public TwoPersonDivinationInviteConfirmFlowUIComponent uiComponent;
	private FriendDataManager.FriendData currentFriend;
	private bool isSubmitting;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TwoPersonDivinationInviteConfirmFlowUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("TwoPersonDivinationInviteConfirmFlowUI 缺少 UI 组件绑定脚本：TwoPersonDivinationInviteConfirmFlowUIComponent");
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
		currentFriend = RelationshipDivinationFlow.CurrentFriend;
		isSubmitting = false;

		if (uiComponent == null || currentFriend == null) return;

		string friendName = RelationshipDivinationFlow.GetFriendName(currentFriend);
		if (uiComponent.PairNameText != null)
			uiComponent.PairNameText.text = $"你 与 {friendName}";
		if (uiComponent.DailyPairLimitText != null)
			uiComponent.DailyPairLimitText.text = RelationshipDivinationFlow.GetDailyLimitText(currentFriend);

		ApplyAvatar(uiComponent.FriendAvatarImage, ResolveFriendAvatar(currentFriend));
		LoadFriendRemoteAvatarIfNeeded();
		LoadUserAvatar();
		RelationshipDivinationFlow.SetButtonText(uiComponent.SendInviteButton, "发送占卜邀请");
		UpdateSubmitState();
		CheckExistingActiveInvite();
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnMyCardButtonClick()
	{
		ToastManager.ShowToast("先发送邀请，房间创建后即可翻开你的牌");
	}
	public void OnSendInviteButtonClick()
	{
		if (isSubmitting || currentFriend == null) return;
		Debug.Log($"[TwoPersonDivinationInviteConfirmFlowUI] SendInvite clicked. friend={currentFriend.name}, uid={currentFriend.firebaseUid}, isVirtual={currentFriend.isVirtual}");
		RelationshipDivinationFlow.TryOpenActiveOrCreate(currentFriend, CreateInvite);
	}
	#endregion

	private void CreateInvite()
	{
		if (CreatedFriendRelationshipDivinationLocalFlow.TryStart(currentFriend))
		{
			isSubmitting = false;
			UpdateSubmitState();
			return;
		}

		RelationshipDivinationFirestore service = RelationshipDivinationFlow.GetOrCreateService();
		if (service == null)
		{
			ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
			return;
		}

		isSubmitting = true;
		UpdateSubmitState();
		Debug.Log($"[TwoPersonDivinationInviteConfirmFlowUI] Creating invite for {currentFriend.name} ({currentFriend.firebaseUid})");
		service.CreateInvite(currentFriend, RelationshipDivinationFlow.BuildQuestion(currentFriend), record =>
		{
			isSubmitting = false;
			UpdateSubmitState();
			if (record == null) return;

			RelationshipDivinationFlow.ShowRecord(record, currentFriend);
		});
	}

	private void CheckExistingActiveInvite()
	{
		if (currentFriend == null || currentFriend.isVirtual || string.IsNullOrWhiteSpace(currentFriend.firebaseUid))
			return;

		RelationshipDivinationFirestore service = RelationshipDivinationFlow.GetOrCreateService();
		if (service == null || !service.IsReady) return;

		service.LoadActiveWithFriend(currentFriend.firebaseUid, active =>
		{
			if (active != null && gameObject != null && gameObject.activeInHierarchy)
				RelationshipDivinationFlow.ShowRecord(active, currentFriend);
		});
	}

	private void UpdateSubmitState()
	{
		if (uiComponent?.SendInviteButton == null) return;
		bool canCreate = currentFriend != null && RelationshipDivinationFlow.CanCreateNewReading(currentFriend, false);
		uiComponent.SendInviteButton.interactable = !isSubmitting && canCreate;
	}

	private void ApplyAvatar(Image target, Sprite sprite)
	{
		FriendAvatarImageUtility.ApplyAvatar(target, sprite);
	}

	private Sprite ResolveFriendAvatar(FriendDataManager.FriendData friend)
	{
		return FriendAvatarImageUtility.ResolveFriendAvatar(friend, uiComponent?.FriendAvatarImage);
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
			ApplyAvatar(uiComponent.FriendAvatarImage, sprite);
		}));
	}

	private void LoadUserAvatar()
	{
		if (uiComponent?.YouAvatarImage == null || uiComponent == null) return;
		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadCurrentUserAvatarCoroutine((sprite, _) =>
		{
			ApplyAvatar(uiComponent.YouAvatarImage, sprite);
		}));
	}
}
