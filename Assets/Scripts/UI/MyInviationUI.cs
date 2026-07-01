/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/30/2026 5:24:54 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;

public class MyInviationUI : WindowBase
{
	public MyInviationUIComponent uiComponent;

	private enum InvitationTab
	{
		Initiate,
		Received
	}

	private const int MaxServiceWaitAttempts = 20;
	private const string InvitationItemPrefabName = "UserInvitationItem";
	private static readonly Color ActiveTabTextColor = new Color32(254, 142, 84, 255);
	private static readonly Color InactiveTabTextColor = new Color32(226, 226, 230, 255);
	private static readonly Color OnlineTextColor = new Color32(63, 213, 138, 255);
	private static readonly Color OfflineTextColor = new Color32(150, 145, 154, 255);
	private static readonly Color AcceptedTextColor = new Color32(63, 213, 138, 255);
	private static readonly Color PendingTextColor = new Color32(176, 170, 180, 255);
	private static readonly Color ActionTextColor = Color.white;

	private readonly List<RelationshipDivinationRecord> currentRecords = new List<RelationshipDivinationRecord>();
	private readonly HashSet<string> acceptingReadingIds = new HashSet<string>();
	private InvitationTab currentTab = InvitationTab.Received;
	private int loadVersion;
	private LoopListView2 invitationListView;
	private bool listViewInitialized;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<MyInviationUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("MyInviationUI 缺少 UI 组件绑定脚本：MyInviationUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		PrepareTabButtons();
		ResolveInvitationListView();
		InitInvitationListView();
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		PrepareTabButtons();
		SelectTab(InvitationTab.Received, true);
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		loadVersion++;
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function
	private void SelectTab(InvitationTab tab, bool forceReload = false)
	{
		if (!forceReload && currentTab == tab)
		{
			ApplyTabVisuals();
			return;
		}

		currentTab = tab;
		ApplyTabVisuals();
		LoadCurrentTab();
	}

	private void ApplyTabVisuals()
	{
		ApplyTabButton(uiComponent?.initiateButton, currentTab == InvitationTab.Initiate);
		ApplyTabButton(uiComponent?.receivedButton, currentTab == InvitationTab.Received);
		BringTabButtonsToFront();
	}

	private void ApplyTabButton(Button button, bool active)
	{
		if (button == null) return;

		TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
		if (text != null)
		{
			text.color = active ? ActiveTabTextColor : InactiveTabTextColor;
			text.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
		}

	}

	private void LoadCurrentTab()
	{
		loadVersion++;
		int version = loadVersion;
		currentRecords.Clear();
		RefreshInvitationList(true);
		SetEmptyText(currentTab == InvitationTab.Initiate ? "正在加载发起的邀请..." : "正在加载收到的邀请...");
		if (uiComponent != null)
			uiComponent.StartCoroutine(LoadCurrentTabRoutine(version));
	}

	private void PrepareTabButtons()
	{
		if (uiComponent?.emptyText != null)
			uiComponent.emptyText.raycastTarget = false;

		BringTabButtonsToFront();
		ApplyTabVisuals();
	}

	private void BringTabButtonsToFront()
	{
		Transform tabRoot = GetTabRoot();
		if (tabRoot != null)
			tabRoot.SetAsLastSibling();
	}

	private Transform GetTabRoot()
	{
		if (uiComponent == null) return null;

		Transform initiateParent = uiComponent.initiateButton != null ? uiComponent.initiateButton.transform.parent : null;
		Transform receivedParent = uiComponent.receivedButton != null ? uiComponent.receivedButton.transform.parent : null;
		if (initiateParent != null && initiateParent == receivedParent)
			return initiateParent;

		return initiateParent != null ? initiateParent : receivedParent;
	}

	private IEnumerator LoadCurrentTabRoutine(int version)
	{
		RelationshipDivinationFirestore service = RelationshipDivinationFlow.GetOrCreateService();
		for (int i = 0; i < MaxServiceWaitAttempts; i++)
		{
			if (service != null && service.IsReady)
				break;

			yield return new WaitForSeconds(0.5f);
			service = RelationshipDivinationFlow.GetOrCreateService();
		}

		if (version != loadVersion)
			yield break;

		if (service == null || !service.IsReady)
		{
			SetEmptyText("邀请服务初始化中，请稍后再试");
			yield break;
		}

		bool completed = false;
		Action<List<RelationshipDivinationRecord>, bool> onLoaded = (records, succeeded) =>
		{
			completed = true;
			if (version != loadVersion)
				return;

			currentRecords.Clear();
			if (succeeded && records != null)
				currentRecords.AddRange(records);

			if (!succeeded)
			{
				SetEmptyText("邀请同步失败，请稍后再试");
				return;
			}

			RenderRecords();
		};

		if (currentTab == InvitationTab.Initiate)
			service.LoadOutgoingInvitationList(onLoaded);
		else
			service.LoadReceivedInvitationList(onLoaded);

		while (!completed && version == loadVersion)
			yield return null;
	}

	private void RenderRecords()
	{
		if (!InitInvitationListView())
		{
			SetEmptyText("邀请列表未绑定");
			return;
		}

		if (currentRecords.Count == 0)
		{
			SetEmptyText(currentTab == InvitationTab.Initiate ? "暂无发起的占卜邀请" : "暂无收到的占卜邀请");
			return;
		}

		SetEmptyText("");
		RefreshInvitationList(true);
	}

	private void ResolveInvitationListView()
	{
		if (invitationListView != null) return;

		if (uiComponent != null)
		{
			uiComponent.ResolveReferences();
			invitationListView = uiComponent.invitationListView;
		}

		if (invitationListView == null)
			invitationListView = gameObject.GetComponentInChildren<LoopListView2>(true);

		if (uiComponent != null && uiComponent.invitationListView == null)
			uiComponent.invitationListView = invitationListView;
	}

	private bool InitInvitationListView()
	{
		ResolveInvitationListView();
		if (invitationListView == null)
			return false;

		if (listViewInitialized || invitationListView.ListViewInited)
		{
			listViewInitialized = true;
			return true;
		}

		invitationListView.InitListView(0, OnGetInvitationItemByIndex);
		listViewInitialized = true;
		return true;
	}

	private void RefreshInvitationList(bool resetScrollPosition)
	{
		if (!InitInvitationListView())
			return;

		invitationListView.SetListItemCount(currentRecords.Count, resetScrollPosition);
		invitationListView.RefreshAllShownItem();
	}

	private LoopListViewItem2 OnGetInvitationItemByIndex(LoopListView2 listView, int index)
	{
		if (index < 0 || index >= currentRecords.Count)
			return null;

		LoopListViewItem2 item = listView.NewListViewItem(InvitationItemPrefabName);
		if (item == null)
		{
			Debug.LogError($"[MyInviationUI] 找不到 SuperScrollView Item 预制体: {InvitationItemPrefabName}");
			return null;
		}

		UserInvitationItem invitationItem = item.GetComponent<UserInvitationItem>();
		if (invitationItem == null)
			invitationItem = item.GetComponentInChildren<UserInvitationItem>(true);

		BindItem(invitationItem, currentRecords[index]);
		return item;
	}

	private void BindItem(UserInvitationItem item, RelationshipDivinationRecord record)
	{
		if (item == null || record == null) return;

		bool showingInitiated = currentTab == InvitationTab.Initiate;
		string otherUid = showingInitiated ? record.receiverUid : record.initiatorUid;
		FriendDataManager.FriendData friend = FindFriend(otherUid);

		SetText(item.userName, ResolveOtherName(record, showingInitiated));
		SetText(item.stateText, ResolveStateText(friend));
		if (item.stateText != null)
			item.stateText.color = friend != null && friend.isOnline ? OnlineTextColor : OfflineTextColor;
		SetText(item.divinationType, ResolveDivinationType(record));
		SetText(item.divinationTime, FormatRecordDate(record));
		ApplyAvatar(item.avatarImage, friend);
		BindInfoButton(item, record, friend, showingInitiated);

		if (showingInitiated)
			ApplyInitiatedStatus(item, record);
		else
			ApplyReceivedStatus(item, record);
	}

	private void ApplyInitiatedStatus(UserInvitationItem item, RelationshipDivinationRecord record)
	{
		bool accepted = IsAccepted(record);
		ApplyButtonState(
			item,
			accepted ? "Accepted" : "Pending approval",
			accepted ? AcceptedTextColor : PendingTextColor,
			false,
			false,
			null);
	}

	private void ApplyReceivedStatus(UserInvitationItem item, RelationshipDivinationRecord record)
	{
		if (record != null && record.IsCompleted)
		{
			ApplyButtonState(item, "View result", AcceptedTextColor, true, true, () => OpenRelationshipRecord(record));
			return;
		}

		if (record != null && record.receiverJoined && !record.receiverRevealed)
		{
			ApplyButtonState(item, "Continue", ActionTextColor, true, true, () => OpenRelationshipRecord(record));
			return;
		}

		if (record != null && record.receiverRevealed)
		{
			ApplyButtonState(item, "Waiting", PendingTextColor, false, false, null);
			return;
		}

		string readingId = record.readingId ?? "";
		bool accepting = acceptingReadingIds.Contains(readingId);
		ApplyButtonState(
			item,
			accepting ? "Accepting" : "Accept",
			ActionTextColor,
			true,
			!accepting,
			() => OnAcceptInviteClick(record));
	}

	private void ApplyButtonState(UserInvitationItem item, string text, Color textColor, bool showImage, bool interactable, UnityAction action)
	{
		if (item == null) return;

		if (item.btnText == null && item.btn != null)
			item.btnText = item.btn.GetComponentInChildren<TMP_Text>(true);

		if (item.btnText != null)
		{
			item.btnText.text = text;
			item.btnText.color = textColor;
		}
		ApplyButtonAreaTextLayout(item, showImage);

		if (item.btn == null)
			return;

		Image image = item.btn.GetComponent<Image>();
		if (image != null)
			image.enabled = showImage;

		item.btn.interactable = interactable;
		item.btn.onClick.RemoveAllListeners();
		if (action != null)
			item.btn.onClick.AddListener(action);
	}

	private void ApplyButtonAreaTextLayout(UserInvitationItem item, bool buttonVisible)
	{
		if (item == null) return;

		ApplyRightSideTextLayout(item.btnText, buttonVisible);
	}

	private void ApplyRightSideTextLayout(TMP_Text text, bool buttonVisible)
	{
		if (text == null) return;

		text.enableAutoSizing = buttonVisible;
		text.alignment = buttonVisible ? TextAlignmentOptions.Center : TextAlignmentOptions.Right;
	}

	private void OnAcceptInviteClick(RelationshipDivinationRecord record)
	{
		if (record == null) return;

		if (record.IsCancelled)
		{
			ToastManager.ShowToast("这次双人占卜邀请已取消");
			LoadCurrentTab();
			return;
		}

		if (RelationshipDivinationFlow.IsInviteExpired(record))
		{
			ToastManager.ShowToast("这次双人占卜邀请已过期");
			LoadCurrentTab();
			return;
		}

		string readingId = record.readingId ?? "";
		if (acceptingReadingIds.Contains(readingId))
			return;

		if (record.IsCompleted || record.receiverJoined || !record.CanCurrentUserReveal(RelationshipDivinationFlow.GetCurrentUid()))
		{
			OpenRelationshipRecord(record);
			return;
		}

		RelationshipDivinationFirestore service = RelationshipDivinationFlow.GetOrCreateService();
		if (service == null)
		{
			ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
			return;
		}

		acceptingReadingIds.Add(readingId);
		RefreshInvitationList(false);
		service.JoinInvite(record, updated =>
		{
			acceptingReadingIds.Remove(readingId);
			if (updated == null)
			{
				ToastManager.ShowToast("接受邀请失败，请稍后再试");
				RefreshInvitationList(false);
				return;
			}

			ReplaceRecord(record, updated);
			if (!updated.receiverJoined && !updated.IsCompleted)
			{
				RefreshInvitationList(false);
				return;
			}

			ToastManager.ShowToast("已接受邀请");
			OpenRelationshipRecord(updated);
		}, false);
	}

	private void BindInfoButton(UserInvitationItem item, RelationshipDivinationRecord record, FriendDataManager.FriendData friend, bool showingInitiated)
	{
		if (item == null || item.infoBtn == null) return;

		string otherUid = GetOtherUid(record, showingInitiated);
		bool canOpen = friend != null || !string.IsNullOrWhiteSpace(otherUid);
		item.infoBtn.gameObject.SetActive(canOpen);
		item.infoBtn.interactable = canOpen;
		item.infoBtn.onClick.RemoveAllListeners();
		if (canOpen)
			item.infoBtn.onClick.AddListener(() => OnInfoButtonClick(record, showingInitiated, friend));
	}

	private void OnInfoButtonClick(RelationshipDivinationRecord record, bool showingInitiated, FriendDataManager.FriendData friend)
	{
		FriendDataManager.FriendData previewFriend = friend
			?? FindFriend(GetOtherUid(record, showingInitiated))
			?? BuildPreviewFriend(record, showingInitiated);

		if (previewFriend == null)
		{
			ToastManager.ShowToast("好友资料不完整");
			return;
		}

		if (showingInitiated)
			FriendPreviewUI.Show(previewFriend);
		else
			FriendPreviewUI.Show(previewFriend, record);
	}

	private void OpenRelationshipRecord(RelationshipDivinationRecord record)
	{
		if (record == null) return;

		FriendDataManager.FriendData friend = ResolveOtherFriend(record, currentTab == InvitationTab.Initiate);
		HideWindow();
		RelationshipDivinationFlow.ShowRecord(record, friend);
	}

	private void ReplaceRecord(RelationshipDivinationRecord oldRecord, RelationshipDivinationRecord updated)
	{
		if (updated == null) return;

		string oldId = oldRecord?.readingId ?? "";
		string newId = updated.readingId ?? "";
		for (int i = 0; i < currentRecords.Count; i++)
		{
			string id = currentRecords[i]?.readingId ?? "";
			if (!string.IsNullOrEmpty(id) && (id == oldId || id == newId))
			{
				currentRecords[i] = updated;
				return;
			}
		}

		currentRecords.Insert(0, updated);
	}

	private bool IsAccepted(RelationshipDivinationRecord record)
	{
		return record != null && (record.IsCompleted || record.receiverJoined || record.receiverRevealed);
	}

	private FriendDataManager.FriendData FindFriend(string firebaseUid)
	{
		if (FriendDataManager.Instance == null || string.IsNullOrWhiteSpace(firebaseUid))
			return null;
		return FriendDataManager.Instance.FindRealFriendByFirebaseUid(firebaseUid);
	}

	private FriendDataManager.FriendData ResolveOtherFriend(RelationshipDivinationRecord record, bool showingInitiated)
	{
		return FindFriend(GetOtherUid(record, showingInitiated)) ?? BuildPreviewFriend(record, showingInitiated);
	}

	private FriendDataManager.FriendData BuildPreviewFriend(RelationshipDivinationRecord record, bool showingInitiated)
	{
		string otherUid = GetOtherUid(record, showingInitiated);
		if (string.IsNullOrWhiteSpace(otherUid))
			return null;

		return new FriendDataManager.FriendData
		{
			firebaseUid = otherUid,
			name = ResolveOtherName(record, showingInitiated),
			handle = $"@{otherUid}",
			info = "双人占卜邀请",
			relationship = "真实好友",
			source = "关系占卜邀请",
			isVirtual = false
		};
	}

	private string GetOtherUid(RelationshipDivinationRecord record, bool showingInitiated)
	{
		if (record == null) return string.Empty;
		return showingInitiated ? record.receiverUid : record.initiatorUid;
	}

	private string ResolveOtherName(RelationshipDivinationRecord record, bool showingInitiated)
	{
		if (record == null) return "好友";
		string name = showingInitiated ? record.receiverName : record.initiatorName;
		return string.IsNullOrWhiteSpace(name) ? "好友" : name;
	}

	private string ResolveStateText(FriendDataManager.FriendData friend)
	{
		if (friend == null)
			return "offline";
		return friend.isOnline ? "online" : "offline";
	}

	private string ResolveDivinationType(RelationshipDivinationRecord record)
	{
		if (record == null || string.IsNullOrWhiteSpace(record.question))
			return "关系占卜";

		string direction = ExtractDirection(record.question);
		string userQuestion = ExtractUserQuestion(record.question);
		string summary = BuildDivinationSummary(record, direction, userQuestion);
		if (!string.IsNullOrWhiteSpace(summary))
			return summary;

		return !string.IsNullOrWhiteSpace(direction) ? TrimSummary(direction, 8) : "关系占卜";
	}

	private string BuildDivinationSummary(RelationshipDivinationRecord record, string direction, string userQuestion)
	{
		string cleanDirection = TrimSummary(direction, 8);
		string cleanQuestion = CleanQuestionText(record, userQuestion);
		if (string.IsNullOrWhiteSpace(cleanQuestion))
			return cleanDirection;

		if (IsGenericRelationshipQuestion(record, cleanQuestion))
			return !string.IsNullOrWhiteSpace(cleanDirection) ? cleanDirection : "关系发展";

		string keywordSummary = ResolveQuestionKeywordSummary(cleanQuestion);
		if (!string.IsNullOrWhiteSpace(keywordSummary))
			return keywordSummary;

		if (!string.IsNullOrWhiteSpace(cleanDirection))
			return cleanDirection;

		return TrimSummary(cleanQuestion, 8);
	}

	private string ResolveQuestionKeywordSummary(string question)
	{
		if (ContainsAny(question, "复合", "回头", "重来"))
			return "复合可能";
		if (ContainsAny(question, "喜欢", "爱", "心意", "在意", "想我", "暧昧"))
			return "心意确认";
		if (ContainsAny(question, "吵架", "矛盾", "冲突", "误会", "冷战"))
			return "冲突化解";
		if (ContainsAny(question, "沟通", "聊天", "联系", "回复", "回我", "不回"))
			return "联系沟通";
		if (ContainsAny(question, "冷淡", "疏远", "距离", "忽冷忽热"))
			return "关系距离";
		if (ContainsAny(question, "相处", "建议", "怎么做", "怎么办", "如何做"))
			return "相处建议";
		if (ContainsAny(question, "信任", "安全感", "稳定"))
			return "信任安全";
		if (ContainsAny(question, "告白", "表白"))
			return "告白时机";
		if (ContainsAny(question, "选择", "要不要"))
			return "关系选择";
		if (ContainsAny(question, "结局", "结果"))
			return "关系结果";
		if (ContainsAny(question, "未来", "接下来", "发展", "走向", "趋势"))
			return "关系发展";

		return string.Empty;
	}

	private bool IsGenericRelationshipQuestion(RelationshipDivinationRecord record, string question)
	{
		string text = NormalizeSummaryText(question);
		text = RemoveNameToken(text, record?.initiatorName);
		text = RemoveNameToken(text, record?.receiverName);

		return text.Contains("关系接下来会如何发展", StringComparison.Ordinal)
			|| text.Contains("关系接下来会怎样", StringComparison.Ordinal)
			|| text.Contains("关系会如何发展", StringComparison.Ordinal)
			|| text.Contains("接下来会如何发展", StringComparison.Ordinal)
			|| (text.Contains("关系", StringComparison.Ordinal)
				&& ContainsAny(text, "发展", "趋势", "走向"));
	}

	private string CleanQuestionText(RelationshipDivinationRecord record, string question)
	{
		if (string.IsNullOrWhiteSpace(question))
			return string.Empty;

		string text = question.Trim();
		text = RemoveNameToken(text, record?.initiatorName);
		text = RemoveNameToken(text, record?.receiverName);
		text = text.Replace("我和", "")
			.Replace("我与", "")
			.Replace("和我", "")
			.Replace("与我", "")
			.Replace("我们的", "")
			.Replace("我們的", "")
			.Replace("这段", "")
			.Replace("這段", "")
			.Trim();

		return text.Trim(' ', '，', ',', '。', '.', '？', '?', '！', '!', '：', ':', '·', '-');
	}

	private string NormalizeSummaryText(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		return value.Trim()
			.Replace(" ", "")
			.Replace("　", "")
			.Replace("，", "")
			.Replace(",", "")
			.Replace("。", "")
			.Replace("？", "")
			.Replace("?", "")
			.Replace("！", "")
			.Replace("!", "");
	}

	private string RemoveNameToken(string value, string name)
	{
		if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(name))
			return value ?? string.Empty;

		return value.Replace(name.Trim(), "");
	}

	private bool ContainsAny(string value, params string[] keywords)
	{
		if (string.IsNullOrWhiteSpace(value) || keywords == null)
			return false;

		foreach (string keyword in keywords)
		{
			if (!string.IsNullOrWhiteSpace(keyword) && value.Contains(keyword, StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	private string ExtractDirection(string question)
	{
		if (string.IsNullOrWhiteSpace(question))
			return string.Empty;

		int start = question.IndexOf('「');
		int end = question.IndexOf('」');
		if (start >= 0 && end > start)
			return question.Substring(start + 1, end - start - 1).Trim();

		if (question.Contains("关系", StringComparison.Ordinal))
			return "关系发展";
		return string.Empty;
	}

	private string ExtractUserQuestion(string question)
	{
		if (string.IsNullOrWhiteSpace(question))
			return string.Empty;

		int directionEnd = question.IndexOf('」');
		int colon = directionEnd >= 0 ? question.IndexOf('：', directionEnd) : question.IndexOf('：');
		if (colon < 0)
			colon = directionEnd >= 0 ? question.IndexOf(':', directionEnd) : question.IndexOf(':');
		if (colon >= 0 && colon < question.Length - 1)
			return question.Substring(colon + 1).Trim();

		return question.Trim();
	}

	private string TrimSummary(string value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		string text = value.Trim();
		int safeLength = Mathf.Max(4, maxLength);
		return text.Length <= safeLength ? text : text.Substring(0, safeLength - 1) + "…";
	}

	private string FormatRecordDate(RelationshipDivinationRecord record)
	{
		if (record == null) return "";

		DateTime time = ParseTime(record.updatedAt);
		if (time == DateTime.MinValue) time = ParseTime(record.createdAt);
		if (time == DateTime.MinValue) return "";
		return $"{time.Year}.{time.Month}.{time.Day}";
	}

	private DateTime ParseTime(string value)
	{
		return DateTime.TryParse(value, out DateTime parsed) ? parsed : DateTime.MinValue;
	}

	private void ApplyAvatar(Image image, FriendDataManager.FriendData friend)
	{
		if (image == null) return;
		Sprite fallback = image.sprite;
		image.sprite = FriendAvatarImageUtility.ResolveFriendAvatar(friend, image, fallback);
	}

	private void SetText(TMP_Text text, string value)
	{
		if (text == null) return;
		text.text = value ?? "";
	}

	private void SetEmptyText(string value)
	{
		if (uiComponent?.emptyText == null) return;
		uiComponent.emptyText.raycastTarget = false;
		uiComponent.emptyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
		uiComponent.emptyText.text = value ?? "";
		BringTabButtonsToFront();
	}
	#endregion

	#region UI组件事件
	public void OnbackButtonClick()
	{
		HideWindow();
	}

	public void OnInitiateButtonClick()
	{
		SelectTab(InvitationTab.Initiate, true);
	}

	public void OnReceivedButtonClick()
	{
		SelectTab(InvitationTab.Received, true);
	}
	#endregion
}
