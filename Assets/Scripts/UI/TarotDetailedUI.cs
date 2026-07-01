/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/30/2026 9:56:27 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class TarotDetailedUI : WindowBase
{
	public TarotDetailedUIComponent uiComponent;

	private static DivinationRecordData sPendingRecord;
	private static string sPendingOwnerName = "好友";
	private static bool sPendingCanContinueChat = true;
	private static bool sPendingShowOwnerInTitle;
	private static FriendDataManager.FriendData sPendingRelationshipFriend;

	private DivinationRecordData currentRecord;
	private string currentOwnerName = "好友";
	private bool currentCanContinueChat = true;
	private bool currentShowOwnerInTitle;
	private FriendDataManager.FriendData currentRelationshipFriend;
	private Button continueChatButton;
	private int avatarRequestVersion;
	private RelationshipDivinationRecord loadedRelationshipRecord;
	private string relationshipRecordStateReadingId;
	private string relationshipRecordLoadingId;
	private string relationshipRecordFailedId;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TarotDetailedUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("TarotDetailedUI 缺少 UI 组件绑定脚本：TarotDetailedUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		ResolveContinueChatButton();
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		currentRecord = sPendingRecord;
		currentOwnerName = ResolveOwnerName(sPendingOwnerName);
		currentCanContinueChat = sPendingCanContinueChat;
		currentShowOwnerInTitle = sPendingShowOwnerInTitle;
		currentRelationshipFriend = sPendingRelationshipFriend;
		PrepareRelationshipRecordState(currentRecord);
		RenderRecord();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		avatarRequestVersion++;
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public static TarotDetailedUI Show(
		DivinationRecordData record,
		string ownerName = null,
		bool canContinueChat = true,
		bool showOwnerInTitle = false,
		FriendDataManager.FriendData relationshipFriend = null)
	{
		if (record == null)
		{
			ToastManager.ShowToast("暂无占卜详情");
			return null;
		}

		sPendingRecord = record;
		sPendingOwnerName = ResolveOwnerName(ownerName);
		sPendingCanContinueChat = canContinueChat;
		sPendingShowOwnerInTitle = showOwnerInTitle;
		sPendingRelationshipFriend = relationshipFriend;
		TarotDetailedUI window = UIModule.Instance.PopUpWindow<TarotDetailedUI>();
		window?.SetRecord(record, ownerName, canContinueChat, showOwnerInTitle, relationshipFriend);
		return window;
	}

	public void SetRecord(
		DivinationRecordData record,
		string ownerName = null,
		bool canContinueChat = true,
		bool showOwnerInTitle = false,
		FriendDataManager.FriendData relationshipFriend = null)
	{
		currentRecord = record;
		currentOwnerName = ResolveOwnerName(ownerName);
		currentCanContinueChat = canContinueChat;
		currentShowOwnerInTitle = showOwnerInTitle;
		currentRelationshipFriend = relationshipFriend;
		if (record != null)
			sPendingRecord = record;
		if (!string.IsNullOrWhiteSpace(ownerName))
			sPendingOwnerName = ResolveOwnerName(ownerName);
		sPendingCanContinueChat = canContinueChat;
		sPendingShowOwnerInTitle = showOwnerInTitle;
		sPendingRelationshipFriend = relationshipFriend;
		PrepareRelationshipRecordState(currentRecord);

		if (gameObject != null && gameObject.activeInHierarchy)
			RenderRecord();
	}

	#endregion

	#region Render
	private void PrepareRelationshipRecordState(DivinationRecordData record)
	{
		string readingId = FirstNonEmpty(record?.readingId);
		if (string.Equals(relationshipRecordStateReadingId, readingId, StringComparison.Ordinal))
			return;

		relationshipRecordStateReadingId = readingId;
		loadedRelationshipRecord = null;
		relationshipRecordLoadingId = null;
		relationshipRecordFailedId = null;
	}

	private void RenderRecord()
	{
		if (uiComponent == null)
			return;

		if (currentRecord == null)
		{
			RenderEmptyState();
			return;
		}

		bool isRelationship = IsRelationshipRecord(currentRecord);
		SetActive(uiComponent.singlePersonTarotType, !isRelationship);
		SetActive(uiComponent.twoPersonTarotType, isRelationship);

		string typeLabel = BuildTypeLabel(currentRecord);
		string sourceLabel = BuildSourceLabel(currentRecord);
		string timeLabel = BuildDisplayDate(currentRecord);
		if (currentShowOwnerInTitle && !isRelationship)
			typeLabel = BuildOwnerTypeLabel(typeLabel);

		SetText(uiComponent.singleTypeText, typeLabel);
		SetText(uiComponent.singleSourceText, sourceLabel);
		SetText(uiComponent.singleDivinationTimeText, timeLabel);
		SetText(uiComponent.twoPersonTypeText, typeLabel);
		SetText(uiComponent.twoPersonSourceText, sourceLabel);
		SetText(uiComponent.twoPersonDivinationTimeText, timeLabel);

		RenderPeopleInfo(isRelationship);
		RenderCards(currentRecord);
		SetText(uiComponent.OverallJudgmentText, BuildOverallJudgment(currentRecord));
		SetText(uiComponent.actionAdviceText, BuildActionAdvice(currentRecord));
		RefreshContinueChatButton();
	}

	private void RenderEmptyState()
	{
		SetActive(uiComponent.singlePersonTarotType, true);
		SetActive(uiComponent.twoPersonTarotType, false);
		SetActive(uiComponent.threeCardContainer, false);
		SetActive(uiComponent.tarotCardDesList, false);
		SetText(uiComponent.singleTypeText, "占卜详情");
		SetText(uiComponent.singleSourceText, "占卜历史");
		SetText(uiComponent.singleDivinationTimeText, string.Empty);
		SetText(uiComponent.OverallJudgmentText, "暂无占卜详情");
		SetText(uiComponent.actionAdviceText, string.Empty);
		RefreshContinueChatButton(false);
	}

	private void RenderPeopleInfo(bool isRelationship)
	{
		if (!isRelationship)
			return;

		RelationshipDivinationRecord relationshipRecord = ResolveRelationshipRecordForCurrentDetail();
		string currentUid = RelationshipDivinationFlow.GetCurrentUid();
		string userName = ResolveCurrentUserDisplayName(relationshipRecord, currentUid);
		FriendDataManager.FriendData friend = ResolveRelationshipFriend(relationshipRecord, ResolveOwnerName(currentOwnerName));
		string friendName = ResolveRelationshipFriendDisplayName(relationshipRecord, currentUid, friend, userName);

		SetText(uiComponent.userName, userName);
		SetText(uiComponent.userInfo, "当前用户");
		SetText(uiComponent.friendName, friendName);
		SetText(uiComponent.friendInfo, "占卜对象");
		RenderRelationshipAvatars(friend, friendName);
		RequestRelationshipRecordIfNeeded(relationshipRecord);
	}

	private string ResolveCurrentUserDisplayName(RelationshipDivinationRecord relationshipRecord, string currentUid)
	{
		string fallbackName = UserDataManager.Instance != null && !string.IsNullOrWhiteSpace(UserDataManager.Instance.UserName)
			? UserDataManager.Instance.UserName.Trim()
			: "我";

		if (relationshipRecord != null)
		{
			if (relationshipRecord.IsCurrentUserInitiator(currentUid))
				return NormalizeCurrentUserName(relationshipRecord.initiatorName, fallbackName);
			if (relationshipRecord.IsCurrentUserReceiver(currentUid))
				return NormalizeCurrentUserName(relationshipRecord.receiverName, fallbackName);
		}

		return fallbackName;
	}

	private string NormalizeCurrentUserName(string recordName, string fallbackName)
	{
		string name = FirstNonEmpty(recordName);
		if (string.IsNullOrWhiteSpace(name) || name == "我")
			return FirstNonEmpty(fallbackName, "我");
		return name;
	}

	private RelationshipDivinationRecord ResolveRelationshipRecordForCurrentDetail()
	{
		if (loadedRelationshipRecord != null
			&& currentRecord != null
			&& !string.IsNullOrWhiteSpace(currentRecord.readingId)
			&& string.Equals(loadedRelationshipRecord.readingId, currentRecord.readingId, StringComparison.Ordinal))
		{
			return loadedRelationshipRecord;
		}

		RelationshipDivinationRecord record = RelationshipDivinationFlow.CurrentRecord;
		if (record == null || currentRecord == null)
			return null;

		if (!string.IsNullOrWhiteSpace(currentRecord.readingId)
			&& string.Equals(record.readingId, currentRecord.readingId, StringComparison.Ordinal))
			return record;

		return null;
	}

	private void RequestRelationshipRecordIfNeeded(RelationshipDivinationRecord existingRecord)
	{
		if (existingRecord != null
			|| currentRecord == null
			|| !IsRelationshipRecord(currentRecord)
			|| string.IsNullOrWhiteSpace(currentRecord.readingId)
			|| HasRelationshipFriendMetadata(currentRecord)
			|| string.Equals(relationshipRecordLoadingId, currentRecord.readingId, StringComparison.Ordinal)
			|| string.Equals(relationshipRecordFailedId, currentRecord.readingId, StringComparison.Ordinal))
		{
			return;
		}

		RelationshipDivinationFirestore service = RelationshipDivinationFlow.GetOrCreateService();
		if (service == null)
			return;

		string requestedReadingId = currentRecord.readingId;
		relationshipRecordLoadingId = requestedReadingId;
		service.LoadReading(requestedReadingId, record =>
		{
			if (currentRecord == null
				|| !string.Equals(currentRecord.readingId, requestedReadingId, StringComparison.Ordinal))
			{
				return;
			}

			relationshipRecordLoadingId = null;
			if (record == null)
			{
				relationshipRecordFailedId = requestedReadingId;
				return;
			}

			loadedRelationshipRecord = record;
			RenderRecord();
		});
	}

	private void RenderRelationshipAvatars(FriendDataManager.FriendData friend, string friendName)
	{
		int requestId = ++avatarRequestVersion;
		FriendAvatarImageUtility.ApplyAvatar(
			uiComponent.userImage,
			FriendAvatarImageUtility.ResolveCurrentUserAvatar(uiComponent.userImage));
		if (uiComponent != null && uiComponent.userImage != null)
		{
			uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadCurrentUserAvatarCoroutine((sprite, _) =>
			{
				if (requestId != avatarRequestVersion || sprite == null || uiComponent?.userImage == null)
					return;

				FriendAvatarImageUtility.ApplyAvatar(uiComponent.userImage, sprite);
			}));
		}

		string friendAvatarUrl = FirstNonEmpty(friend?.photoUrl, currentRecord?.relationshipFriendAvatarUrl);
		Sprite friendAvatar = friend != null
			? FriendAvatarImageUtility.ResolveFriendAvatar(friend, uiComponent.friendImage)
			: null;
		FriendAvatarImageUtility.ApplyAvatar(uiComponent.friendImage, friendAvatar);
		LoadRelationshipFriendAvatarIfNeeded(friend, friendName, friendAvatarUrl, requestId);
	}

	private FriendDataManager.FriendData ResolveRelationshipFriend(RelationshipDivinationRecord relationshipRecord, string friendName)
	{
		FriendDataManager.FriendData friend = currentRelationshipFriend;
		if (friend == null && IsCurrentFlowFriendForThisRecord(relationshipRecord))
			friend = RelationshipDivinationFlow.CurrentFriend;

		if (friend == null)
			friend = FindRealFriendByUid(currentRecord?.relationshipFriendUid);

		if (relationshipRecord != null && FriendDataManager.Instance != null)
		{
			string currentUid = RelationshipDivinationFlow.GetCurrentUid();
			if (friend == null)
				friend = FindRealFriendByUid(RelationshipDivinationFlow.GetOtherUid(relationshipRecord, currentUid));
			if (friend == null && relationshipRecord.IsCurrentUserInitiator(currentUid))
				friend = FindRealFriendByUid(relationshipRecord.receiverUid);
			if (friend == null && relationshipRecord.IsCurrentUserReceiver(currentUid))
				friend = FindRealFriendByUid(relationshipRecord.initiatorUid);
			if (friend == null)
				friend = FindRealFriendByUid(relationshipRecord.receiverUid);
			if (friend == null)
				friend = FindRealFriendByUid(relationshipRecord.initiatorUid);
		}

		if (friend == null && IsCurrentFlowFriendUsable(relationshipRecord))
			friend = RelationshipDivinationFlow.CurrentFriend;

		if (friend == null && FriendDataManager.Instance != null)
		{
			string currentUserName = UserDataManager.Instance != null ? UserDataManager.Instance.UserName : string.Empty;
			friend = FindRealFriendByUsableName(friendName, currentUserName);
			if (friend == null)
				friend = FindRealFriendByUsableName(currentRecord?.relationshipFriendName, currentUserName);
			if (friend == null)
				friend = FindRealFriendByUsableName(currentOwnerName, currentUserName);
			if (friend == null && relationshipRecord != null)
				friend = FindRealFriendByUsableName(relationshipRecord.receiverName, currentUserName);
			if (friend == null && relationshipRecord != null)
				friend = FindRealFriendByUsableName(relationshipRecord.initiatorName, currentUserName);
		}

		return friend ?? BuildRelationshipFriendFallback();
	}

	private bool IsCurrentFlowFriendForThisRecord(RelationshipDivinationRecord relationshipRecord)
	{
		if (RelationshipDivinationFlow.CurrentFriend == null)
			return false;

		RelationshipDivinationRecord flowRecord = RelationshipDivinationFlow.CurrentRecord;
		if (relationshipRecord != null && flowRecord != null)
			return !string.IsNullOrWhiteSpace(relationshipRecord.readingId)
				&& string.Equals(relationshipRecord.readingId, flowRecord.readingId, StringComparison.Ordinal);

		return flowRecord != null
			&& currentRecord != null
			&& !string.IsNullOrWhiteSpace(currentRecord.readingId)
			&& string.Equals(currentRecord.readingId, flowRecord.readingId, StringComparison.Ordinal);
	}

	private FriendDataManager.FriendData FindRealFriendByUid(string uid)
	{
		if (FriendDataManager.Instance == null || string.IsNullOrWhiteSpace(uid))
			return null;
		return FriendDataManager.Instance.FindRealFriendByFirebaseUid(uid);
	}

	private FriendDataManager.FriendData FindRealFriendByUsableName(string name, string currentUserName)
	{
		if (FriendDataManager.Instance == null || !IsUsableFriendDisplayName(name, currentUserName))
			return null;
		return FriendDataManager.Instance.FindRealFriendByHandleOrName(string.Empty, name.Trim());
	}

	private string ResolveRelationshipFriendDisplayName(
		RelationshipDivinationRecord relationshipRecord,
		string currentUid,
		FriendDataManager.FriendData friend,
		string currentUserName)
	{
		if (!string.IsNullOrWhiteSpace(friend?.name))
			return friend.name.Trim();

		if (relationshipRecord != null)
		{
			string directName = relationshipRecord.IsCurrentUserInitiator(currentUid)
				? relationshipRecord.receiverName
				: relationshipRecord.IsCurrentUserReceiver(currentUid)
					? relationshipRecord.initiatorName
					: string.Empty;
			if (IsUsableFriendDisplayName(directName, currentUserName))
				return directName.Trim();

			string candidate = FirstUsableFriendDisplayName(
				currentUserName,
				relationshipRecord.receiverName,
				relationshipRecord.initiatorName);
			if (!string.IsNullOrWhiteSpace(candidate))
				return candidate;
		}

		if (IsUsableFriendDisplayName(currentRecord?.relationshipFriendName, currentUserName))
			return currentRecord.relationshipFriendName.Trim();

		if (IsUsableFriendDisplayName(currentOwnerName, currentUserName))
			return currentOwnerName.Trim();

		return "好友";
	}

	private FriendDataManager.FriendData BuildRelationshipFriendFallback()
	{
		if (!HasRelationshipFriendMetadata(currentRecord))
			return null;

		string uid = FirstNonEmpty(currentRecord.relationshipFriendUid);
		return new FriendDataManager.FriendData
		{
			firebaseUid = uid,
			name = FirstNonEmpty(currentRecord.relationshipFriendName, "好友"),
			photoUrl = FirstNonEmpty(currentRecord.relationshipFriendAvatarUrl),
			relationship = "好友",
			info = "占卜对象",
			isVirtual = !string.IsNullOrWhiteSpace(uid) && uid.StartsWith("virtual:", StringComparison.Ordinal)
		};
	}

	private bool HasRelationshipFriendMetadata(DivinationRecordData record)
	{
		return record != null
			&& (!string.IsNullOrWhiteSpace(record.relationshipFriendUid)
				|| !string.IsNullOrWhiteSpace(record.relationshipFriendName)
				|| !string.IsNullOrWhiteSpace(record.relationshipFriendAvatarUrl));
	}

	private string FirstUsableFriendDisplayName(string currentUserName, params string[] names)
	{
		if (names == null)
			return string.Empty;
		foreach (string name in names)
		{
			if (IsUsableFriendDisplayName(name, currentUserName))
				return name.Trim();
		}
		return string.Empty;
	}

	private bool IsUsableFriendDisplayName(string name, string currentUserName)
	{
		string value = FirstNonEmpty(name);
		if (string.IsNullOrWhiteSpace(value))
			return false;
		if (value == "我" || value == "当前用户" || value == "好友")
			return false;
		if (!string.IsNullOrWhiteSpace(currentUserName)
			&& string.Equals(value, currentUserName.Trim(), StringComparison.Ordinal))
			return false;
		return true;
	}

	private bool IsCurrentFlowFriendUsable(RelationshipDivinationRecord relationshipRecord)
	{
		FriendDataManager.FriendData currentFriend = RelationshipDivinationFlow.CurrentFriend;
		if (currentFriend == null)
			return false;

		if (relationshipRecord == null)
			return !string.IsNullOrWhiteSpace(currentOwnerName)
				&& !string.Equals(currentOwnerName, "我", StringComparison.Ordinal)
				&& string.Equals(currentFriend.name, currentOwnerName, StringComparison.Ordinal);

		string otherUid = RelationshipDivinationFlow.GetOtherUid(relationshipRecord, RelationshipDivinationFlow.GetCurrentUid());
		return string.IsNullOrWhiteSpace(otherUid)
			|| string.Equals(currentFriend.firebaseUid, otherUid, StringComparison.Ordinal);
	}

	private void LoadRelationshipFriendAvatarIfNeeded(
		FriendDataManager.FriendData friend,
		string friendName,
		string avatarUrl,
		int requestId)
	{
		if ((friend != null && friend.headSprite != null)
			|| string.IsNullOrWhiteSpace(avatarUrl)
			|| uiComponent == null
			|| uiComponent.friendImage == null)
		{
			return;
		}

		string displayName = FirstNonEmpty(friendName, friend?.name, currentRecord?.relationshipFriendName, "好友");
		string friendUid = FirstNonEmpty(friend?.firebaseUid, currentRecord?.relationshipFriendUid);
		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadUserSpriteFromUrlCoroutine(friendUid, displayName, avatarUrl, sprite =>
		{
			if (requestId != avatarRequestVersion || sprite == null || uiComponent?.friendImage == null)
				return;

			if (friend != null)
				friend.headSprite = sprite;
			FriendAvatarImageUtility.ApplyAvatar(uiComponent.friendImage, sprite);
		}));
	}

	private void RenderCards(DivinationRecordData record)
	{
		List<LockedCard> cards = record?.lockedCards ?? new List<LockedCard>();
		int visibleCount = GetVisibleCardCount(record, cards.Count);
		bool hasCards = visibleCount > 0;
		bool showCardContainer = ShouldShowCardContainer(record, visibleCount);

		SetActive(uiComponent.threeCardContainer, showCardContainer);
		SetActive(uiComponent.tarotCardDesList, hasCards);

		RenderCardImage(uiComponent.tarot1Image, cards, 0, visibleCount);
		RenderCardImage(uiComponent.tarot2Image, cards, 1, visibleCount);
		RenderCardImage(uiComponent.tarot3Image, cards, 2, visibleCount);

		RenderInfoItem(uiComponent.tarotItem1, cards, 0, visibleCount);
		RenderInfoItem(uiComponent.tarotItem2, cards, 1, visibleCount);
		RenderInfoItem(uiComponent.tarotItem3, cards, 2, visibleCount);
	}

	private void RenderCardImage(Image image, List<LockedCard> cards, int index, int visibleCount)
	{
		if (image == null)
			return;

		bool show = index < visibleCount && index < cards.Count;
		image.gameObject.SetActive(show);
		image.rectTransform.localRotation = Quaternion.identity;
		if (!show)
		{
			image.sprite = null;
			image.enabled = false;
			return;
		}

		LockedCard card = cards[index];
		Sprite sprite = LoadCardSprite(card.cardId);
		image.sprite = sprite;
		image.enabled = sprite != null;
		image.rectTransform.localRotation = IsUpright(card)
			? Quaternion.identity
			: Quaternion.Euler(0f, 0f, 180f);
	}

	private void RenderInfoItem(DivinationInfoItem item, List<LockedCard> cards, int index, int visibleCount)
	{
		if (item == null)
			return;

		bool show = index < visibleCount && index < cards.Count;
		item.gameObject.SetActive(show);
		if (!show)
			return;

		LockedCard card = cards[index];
		Sprite sprite = LoadCardSprite(card.cardId);
		string cardName = BuildCardDisplayName(card);
		string description = BuildCardDescription(currentRecord, card, index);
		item.SetItemData(sprite, cardName, description);

		if (item.iconImage != null)
		{
			item.iconImage.enabled = sprite != null;
			item.iconImage.rectTransform.localRotation = IsUpright(card)
				? Quaternion.identity
				: Quaternion.Euler(0f, 0f, 180f);
		}
	}

	private void RefreshContinueChatButton(bool canContinue = true)
	{
		Button button = ResolveContinueChatButton();
		if (button == null)
			return;

		bool active = canContinue
			&& currentCanContinueChat
			&& currentRecord != null
			&& currentRecord.lockedCards != null
			&& currentRecord.lockedCards.Count > 0;
		button.gameObject.SetActive(active);
		button.interactable = active;

		TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
		if (label != null)
			label.text = IsRelationshipRecord(currentRecord) ? "继续和她聊聊" : "继续聊聊";
	}
	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}

	public void OnContinueChatButtonClick()
	{
		if (currentRecord == null)
		{
			ToastManager.ShowToast("暂无可继续的占卜记录");
			return;
		}
		if (!currentCanContinueChat)
		{
			ToastManager.ShowToast("这条占卜记录仅可查看");
			return;
		}

		DialogSystem.Instance?.ActivateReadingFromRecord(currentRecord, DivinationPhase.FollowUp);
		UIModule.Instance.GetWindow<NavigationUI>()?.OpenDialogUI();
		HideWindow();
		UIModule.Instance.PopUpWindow<DialogUI>();
		if (uiComponent != null)
			uiComponent.StartCoroutine(SendContinuePromptNextFrame());
	}
	#endregion

	#region Helpers
	private IEnumerator SendContinuePromptNextFrame()
	{
		yield return null;

		DialogUI dialog = UIModule.Instance.GetWindow<DialogUI>();
		if (dialog == null)
			dialog = UIModule.Instance.PopUpWindow<DialogUI>();
		if (dialog == null)
			yield break;

		string prompt = IsRelationshipRecord(currentRecord)
			? $"继续聊聊我和{ResolveOwnerName(currentOwnerName)}的这次占卜。"
			: "继续聊聊这次占卜。";
		dialog.SendMessageFromExternal(prompt);
	}

	private Button ResolveContinueChatButton()
	{
		if (continueChatButton != null)
			return continueChatButton;

		continueChatButton = uiComponent != null ? uiComponent.ContinueChatButton : null;
		if (continueChatButton == null)
			continueChatButton = FindButtonByName(transform, "ContinueChat");
		if (continueChatButton != null && uiComponent != null && uiComponent.ContinueChatButton == null)
			uiComponent.ContinueChatButton = continueChatButton;
		return continueChatButton;
	}

	private Button FindButtonByName(Transform root, string objectName)
	{
		if (root == null || string.IsNullOrWhiteSpace(objectName))
			return null;
		if (root.name == objectName)
			return root.GetComponent<Button>();

		for (int i = 0; i < root.childCount; i++)
		{
			Button result = FindButtonByName(root.GetChild(i), objectName);
			if (result != null)
				return result;
		}

		return null;
	}

	private int GetVisibleCardCount(DivinationRecordData record, int cardCount)
	{
		if (cardCount <= 0)
			return 0;
		if (IsDailyRecord(record))
			return 1;
		return Mathf.Min(3, cardCount);
	}

	private bool ShouldShowCardContainer(DivinationRecordData record, int visibleCount)
	{
		if (visibleCount <= 0 || IsDailyRecord(record))
			return false;

		return IsRelationshipRecord(record) || visibleCount >= 3;
	}

	private string BuildTypeLabel(DivinationRecordData record)
	{
		if (record == null)
			return "占卜详情";
		if (IsDailyRecord(record))
			return "每日占卜";
		if (IsRelationshipRecord(record))
			return "双人关系占卜";
		return FirstNonEmpty(record.SpreadLabel, "塔罗占卜");
	}

	private string BuildOwnerTypeLabel(string typeLabel)
	{
		string ownerName = ResolveOwnerName(currentOwnerName);
		string label = FirstNonEmpty(typeLabel, "占卜详情");
		if (string.IsNullOrWhiteSpace(ownerName) || label.Contains(ownerName))
			return label;

		return $"{ownerName}的{label}";
	}

	private string BuildSourceLabel(DivinationRecordData record)
	{
		if (record == null)
			return "占卜历史";
		string questionKeyword = BuildQuestionKeywordLabel(record);
		if (!string.IsNullOrWhiteSpace(questionKeyword))
			return questionKeyword;
		if (IsDailyRecord(record))
			return "今日牌";
		if (IsRelationshipRecord(record))
			return "关系占卜";
		return FirstNonEmpty(record.SpreadLabel, "占卜历史");
	}

	private string BuildQuestionKeywordLabel(DivinationRecordData record)
	{
		string question = FirstNonEmpty(record?.question);
		if (string.IsNullOrWhiteSpace(question))
			return string.Empty;

		string rawComposite = MatchCompositeQuestionKeyword(question);
		if (!string.IsNullOrWhiteSpace(rawComposite))
			return rawComposite;

		string normalized = NormalizeQuestionText(question);
		if (IsGenericQuestionText(normalized))
			return string.Empty;

		string composite = MatchCompositeQuestionKeyword(normalized);
		if (!string.IsNullOrWhiteSpace(composite))
			return composite;

		List<string> keywords = CollectQuestionKeywords(normalized);
		if (keywords.Count > 0)
			return string.Join(" · ", keywords);

		return TrimQuestionToKeyword(normalized);
	}

	private string NormalizeQuestionText(string question)
	{
		string result = FirstNonEmpty(question);
		if (string.IsNullOrWhiteSpace(result))
			return string.Empty;

		string[] punctuation = { "？", "?", "。", ".", "！", "!", "，", ",", "；", ";", "：", ":", "、", "\n", "\r", "\t" };
		foreach (string token in punctuation)
			result = result.Replace(token, " ");

		string[] removableNames =
		{
			currentOwnerName,
			UserDataManager.Instance != null ? UserDataManager.Instance.UserName : string.Empty
		};
		foreach (string name in removableNames)
		{
			if (!string.IsNullOrWhiteSpace(name))
				result = result.Replace(name.Trim(), " ");
		}

		string[] fillerPhrases =
		{
			"我想知道",
			"想知道",
			"帮我看看",
			"帮我看",
			"请问",
			"我和",
			"我跟",
			"我与",
			"我们",
			"我的",
			"对方",
			"这个人",
			"接下来",
			"最近",
			"这次",
			"一下",
			"会不会",
			"能不能",
			"是否",
			"是不是",
			"怎么样",
			"如何",
			"什么",
			"怎么",
			"的",
			"吗",
			"呢",
			"呀",
			"占卜",
			"问题"
		};
		foreach (string phrase in fillerPhrases)
			result = result.Replace(phrase, " ");

		while (result.Contains("  "))
			result = result.Replace("  ", " ");

		return result.Trim();
	}

	private bool IsGenericQuestionText(string text)
	{
		string normalized = (text ?? string.Empty).Replace(" ", string.Empty);
		return string.IsNullOrWhiteSpace(normalized)
			|| normalized == "每日"
			|| normalized == "今日"
			|| normalized == "今日牌"
			|| normalized == "每日牌"
			|| normalized == "每日塔罗"
			|| normalized == "今日塔罗"
			|| normalized == "塔罗";
	}

	private string MatchCompositeQuestionKeyword(string text)
	{
		if (ContainsAny(text, "心意", "想法", "感觉", "喜欢", "在意"))
			return "心意确认";
		if (ContainsAny(text, "复合", "和好", "修复", "挽回"))
			return "关系修复";
		if (ContainsAny(text, "关系", "相处", "感情") && ContainsAny(text, "发展", "走向", "趋势", "结果"))
			return "关系发展";
		if (ContainsAny(text, "事业", "工作", "职场") && ContainsAny(text, "发展", "选择", "机会", "方向"))
			return "事业方向";
		if (ContainsAny(text, "财运", "金钱", "收入", "财富"))
			return "财运趋势";
		if (ContainsAny(text, "学业", "考试", "学习"))
			return "学业状态";
		if (ContainsAny(text, "健康", "身体", "睡眠", "情绪"))
			return "身心状态";
		if (ContainsAny(text, "选择", "决定", "要不要"))
			return "选择建议";

		return string.Empty;
	}

	private List<string> CollectQuestionKeywords(string text)
	{
		List<string> result = new List<string>();
		string[] candidates =
		{
			"关系",
			"感情",
			"事业",
			"工作",
			"财运",
			"学业",
			"健康",
			"家庭",
			"友情",
			"未来",
			"选择",
			"机会",
			"状态",
			"方向",
			"趋势"
		};

		foreach (string candidate in candidates)
		{
			if (result.Count >= 2)
				break;
			if (!string.IsNullOrWhiteSpace(text) && text.Contains(candidate) && !result.Contains(candidate))
				result.Add(candidate);
		}

		return result;
	}

	private bool ContainsAny(string text, params string[] values)
	{
		if (string.IsNullOrWhiteSpace(text) || values == null)
			return false;
		foreach (string value in values)
		{
			if (!string.IsNullOrWhiteSpace(value) && text.Contains(value))
				return true;
		}
		return false;
	}

	private string TrimQuestionToKeyword(string text)
	{
		string value = FirstNonEmpty(text).Replace(" ", string.Empty);
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		int maxLength = ContainsLatin(value) ? 16 : 8;
		return value.Length <= maxLength ? value : value.Substring(0, maxLength);
	}

	private bool ContainsLatin(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return false;
		foreach (char ch in value)
		{
			if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
				return true;
		}
		return false;
	}

	private string BuildDisplayDate(DivinationRecordData record)
	{
		if (record == null)
			return string.Empty;
		if (DateTime.TryParse(record.createdAt, out DateTime createdAt))
			return $"{createdAt.Year}.{createdAt.Month}.{createdAt.Day}";
		return record.DisplayTime;
	}

	private string BuildOverallJudgment(DivinationRecordData record)
	{
		if (record == null)
			return "暂无占卜详情";

		return FirstNonEmpty(record.judgeContent, record.shortVerdict, BuildDefaultJudgeText(record));
	}

	private string BuildActionAdvice(DivinationRecordData record)
	{
		if (record == null)
			return string.Empty;

		return FirstNonEmpty(record.adviceContent, BuildDefaultActionAdvice(record));
	}

	private string BuildDefaultJudgeText(DivinationRecordData record)
	{
		List<LockedCard> cards = record?.lockedCards;
		if (cards == null || cards.Count == 0)
			return "这条记录暂时没有抽牌数据。";
		if (cards.Count >= 3)
			return $"这组牌由「{BuildCardDisplayName(cards[0])}」「{BuildCardDisplayName(cards[1])}」「{BuildCardDisplayName(cards[2])}」组成，提醒你先看清当下状态，再选择下一步行动。";
		return $"这次牌面以「{BuildCardDisplayName(cards[0])}」为核心，重点是看见此刻真正影响你的能量。";
	}

	private string BuildDefaultActionAdvice(DivinationRecordData record)
	{
		List<LockedCard> cards = record?.lockedCards;
		if (cards == null || cards.Count == 0)
			return string.Empty;

		string keyword = BuildQuestionKeywordLabel(record);
		if (string.IsNullOrWhiteSpace(keyword))
			keyword = IsDailyRecord(record) ? "今天状态" : "这个问题";

		if (IsRelationshipRecord(record))
			return $"围绕「{keyword}」，先做一次真实但不施压的确认：把你想表达的重点写成一句话，再决定是否发送。";

		if (IsDailyRecord(record))
			return $"围绕「{keyword}」，今天先完成一件能恢复掌控感的小事，不要一次处理太多。";

		if (cards.Count >= 3)
			return $"围绕「{keyword}」，先选出牌面里最让你有反应的一张牌，把它对应成今天可以执行的一步。";

		return $"围绕「{keyword}」，先从一个低风险的小行动开始验证，不急着一次做最终决定。";
	}

	private string BuildCardDisplayName(LockedCard card)
	{
		if (card == null)
			return "未知牌";

		TarotCard tarotData = TarotDeck.GetById(card.cardId);
		if (tarotData != null)
			return tarotData.DisplayName(IsUpright(card));

		return TarotDeck.FormatDisplayName(FirstNonEmpty(card.cardName, card.cardId, "未知牌"), IsUpright(card));
	}

	private string BuildCardDescription(DivinationRecordData record, LockedCard card, int index)
	{
		if (card == null)
			return string.Empty;

		TarotCard tarotData = TarotDeck.GetById(card.cardId);
		bool upright = IsUpright(card);
		string cardName = BuildCompactCardName(card, tarotData, upright);
		string questionKeyword = BuildQuestionKeywordLabel(record);
		if (string.IsNullOrWhiteSpace(questionKeyword))
			questionKeyword = IsDailyRecord(record) ? "今天状态" : "这个问题";

		string positionIntro = BuildCardPositionIntro(record, card, index);
		string energy = BuildCardEnergyPhrase(tarotData, card, upright);
		string context = BuildCardQuestionContext(record, questionKeyword, upright);
		return $"{positionIntro}{cardName}{energy}{context}";
	}

	private string BuildCompactCardName(LockedCard card, TarotCard tarotData, bool upright)
	{
		string name = FirstNonEmpty(tarotData?.nameZh, card?.cardName, card?.cardId, "这张牌");
		name = TarotDeck.StripOrientationSuffix(name);
		return $"{name}{(upright ? "正位" : "逆位")}";
	}

	private string BuildCardPositionIntro(DivinationRecordData record, LockedCard card, int index)
	{
		string position = FirstNonEmpty(card?.position);
		if (string.IsNullOrWhiteSpace(position))
			return string.Empty;

		if (IsDailyRecord(record))
			return $"作为「{position}」，";

		if (IsRelationshipRecord(record))
			return $"在「{position}」上，";

		return $"第{index + 1}张「{position}」里，";
	}

	private string BuildCardEnergyPhrase(TarotCard tarotData, LockedCard card, bool upright)
	{
		List<string> keywords = tarotData?.keywords;
		string first = keywords != null && keywords.Count > 0 ? keywords[0] : string.Empty;
		string second = keywords != null && keywords.Count > 1 ? keywords[1] : string.Empty;
		string core = string.IsNullOrWhiteSpace(second)
			? FirstNonEmpty(first, TarotDeck.StripOrientationSuffix(card?.cardName), "当下能量")
			: $"{first}与{second}";

		return upright
			? $"强调{core}，"
			: $"提醒{core}里有需要放慢整理的部分，";
	}

	private string BuildCardQuestionContext(DivinationRecordData record, string questionKeyword, bool upright)
	{
		string category = ResolveQuestionCategory(record, questionKeyword);
		string keyword = FirstNonEmpty(questionKeyword, "这个问题");

		if (category == "relationship")
		{
			return upright
				? $"放到「{keyword}」里，是先看见真实需求，再让关系自然推进。"
				: $"放到「{keyword}」里，是先暂停硬推，弄清误会或压力来自哪里。";
		}

		if (category == "career")
		{
			return upright
				? $"放到「{keyword}」里，适合把注意力放回可执行的步骤和正在形成的机会。"
				: $"放到「{keyword}」里，先处理卡住的节奏，再决定下一步投入。";
		}

		if (category == "choice")
		{
			return upright
				? $"放到「{keyword}」里，答案更偏向清楚地选择一个能承担的方向。"
				: $"放到「{keyword}」里，先别急着定案，重要的是看清你真正害怕失去什么。";
		}

		if (category == "wealth")
		{
			return upright
				? $"放到「{keyword}」里，重点是稳住资源，选择更长期有效的安排。"
				: $"放到「{keyword}」里，提醒你先看清消耗点，再谈增长。";
		}

		if (category == "study")
		{
			return upright
				? $"放到「{keyword}」里，适合回到方法和节奏，让努力更有方向。"
				: $"放到「{keyword}」里，先拆开压力来源，再调整学习方式。";
		}

		if (category == "health")
		{
			return upright
				? $"放到「{keyword}」里，提醒你照顾身体感受，用稳定的小行动恢复秩序。"
				: $"放到「{keyword}」里，说明被忽略的疲惫需要先被认真对待。";
		}

		if (IsDailyRecord(record))
		{
			return upright
				? $"放到「{keyword}」里，今天更适合稳住节奏，选择一个能立刻完成的小行动。"
				: $"放到「{keyword}」里，今天先放慢一点，回看真正牵动你的情绪。";
		}

		return upright
			? $"放到「{keyword}」里，它支持你从已经清楚的部分开始行动。"
			: $"放到「{keyword}」里，它提醒你先看清阻碍，再继续往前。";
	}

	private string ResolveQuestionCategory(DivinationRecordData record, string questionKeyword)
	{
		string text = $"{record?.question} {questionKeyword}";
		if (ContainsAny(text, "关系", "感情", "心意", "喜欢", "复合", "和好", "相处", "友情"))
			return "relationship";
		if (ContainsAny(text, "事业", "工作", "职场", "职业", "机会"))
			return "career";
		if (ContainsAny(text, "选择", "决定", "要不要", "该不该", "二选一"))
			return "choice";
		if (ContainsAny(text, "财运", "金钱", "收入", "财富", "资源"))
			return "wealth";
		if (ContainsAny(text, "学业", "考试", "学习"))
			return "study";
		if (ContainsAny(text, "健康", "身体", "睡眠", "情绪", "身心"))
			return "health";
		return string.Empty;
	}

	private Sprite LoadCardSprite(string cardId)
	{
		if (string.IsNullOrWhiteSpace(cardId))
			return null;
		return TarotSpriteLoader.Load(cardId);
	}

	private bool IsDailyRecord(DivinationRecordData record)
	{
		string scene = (record?.scene ?? "").ToLowerInvariant();
		string spreadKind = (record?.spreadKind ?? "").ToLowerInvariant();
		string readingId = (record?.readingId ?? "").ToLowerInvariant();
		return scene.Contains("daily")
			|| scene.Contains("today")
			|| spreadKind.Contains("daily")
			|| spreadKind.Contains("today")
			|| readingId.Contains("daily")
			|| readingId.Contains("today");
	}

	private bool IsRelationshipRecord(DivinationRecordData record)
	{
		string scene = (record?.scene ?? "").ToLowerInvariant();
		string spreadKind = (record?.spreadKind ?? "").ToLowerInvariant();
		string label = record?.SpreadLabel ?? string.Empty;
		return scene.Contains("friend")
			|| scene.Contains("relationship")
			|| spreadKind.Contains("friend")
			|| spreadKind.Contains("relationship")
			|| label.Contains("双人")
			|| label.Contains("关系");
	}

	private static bool IsUpright(LockedCard card)
	{
		return !string.Equals(card?.orientation, "reversed", StringComparison.OrdinalIgnoreCase);
	}

	private void SetText(TMP_Text text, string value)
	{
		if (text != null)
			text.text = value ?? string.Empty;
	}

	private void SetActive(GameObject target, bool active)
	{
		if (target != null)
			target.SetActive(active);
	}

	private static string ResolveOwnerName(string ownerName)
	{
		return string.IsNullOrWhiteSpace(ownerName) ? "好友" : ownerName.Trim();
	}

	private static string FirstNonEmpty(params string[] values)
	{
		if (values == null)
			return string.Empty;
		foreach (string value in values)
		{
			if (!string.IsNullOrWhiteSpace(value))
				return value.Trim();
		}
		return string.Empty;
	}
	#endregion
}
