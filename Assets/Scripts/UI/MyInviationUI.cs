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
	private static readonly Color ActiveTabTextColor = new Color32(224, 133, 54, 255);
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
		ResolveInvitationListView();
		InitInvitationListView();
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
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
			return;

		currentTab = tab;
		ApplyTabVisuals();
		LoadCurrentTab();
	}

	private void ApplyTabVisuals()
	{
		ApplyTabText(uiComponent?.initiateButton, currentTab == InvitationTab.Initiate);
		ApplyTabText(uiComponent?.receivedButton, currentTab == InvitationTab.Received);
	}

	private void ApplyTabText(Button button, bool active)
	{
		TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
		if (text != null)
			text.color = active ? ActiveTabTextColor : InactiveTabTextColor;
	}

	private void LoadCurrentTab()
	{
		loadVersion++;
		int version = loadVersion;
		currentRecords.Clear();
		RefreshInvitationList(true);
		SetEmptyText("加载中...");
		if (uiComponent != null)
			uiComponent.StartCoroutine(LoadCurrentTabRoutine(version));
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
		if (IsAccepted(record))
		{
			ApplyButtonState(item, "Accepted", AcceptedTextColor, false, false, null);
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

	private void OnAcceptInviteClick(RelationshipDivinationRecord record)
	{
		if (record == null) return;

		string readingId = record.readingId ?? "";
		if (acceptingReadingIds.Contains(readingId))
			return;

		RelationshipDivinationFirestore service = RelationshipDivinationFlow.GetOrCreateService();
		if (service == null || !service.IsReady)
		{
			ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
			return;
		}

		if (!record.CanCurrentUserReveal(RelationshipDivinationFlow.GetCurrentUid()))
		{
			RefreshInvitationList(false);
			return;
		}

		acceptingReadingIds.Add(readingId);
		RefreshInvitationList(false);
		service.RevealMyCard(record, updated =>
		{
			acceptingReadingIds.Remove(readingId);
			if (updated == null)
			{
				ToastManager.ShowToast("接受邀请失败，请稍后再试");
				RefreshInvitationList(false);
				return;
			}

			ReplaceRecord(record, updated);
			ToastManager.ShowToast("已接受邀请");
			RefreshInvitationList(false);
		}, false);
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
			return "Relationship development";
		return record.question;
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
		uiComponent.emptyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
		uiComponent.emptyText.text = value ?? "";
	}
	#endregion

	#region UI组件事件
	public void OnbackButtonClick()
	{
		HideWindow();
	}

	public void OnInitiateButtonClick()
	{
		SelectTab(InvitationTab.Initiate);
	}

	public void OnReceivedButtonClick()
	{
		SelectTab(InvitationTab.Received);
	}
	#endregion
}
