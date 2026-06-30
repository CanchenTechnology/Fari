/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/11/2026 11:34:57 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FriendUI : WindowBase, IPointerClickHandler
{
	private const string FriendItemPrefabName = "FriendItem";

	public FriendUIComponent uiComponent;

	private readonly List<FriendDataManager.FriendData> sortedFriends = new List<FriendDataManager.FriendData>();
	private LoopListView2 friendListView;
	private bool friendListViewInited;
	private int cloudFriendSyncRequestId;
	private Coroutine cloudFriendSyncRetryCoroutine;
	private int avatarRequestVersion;
	private FriendDataManager.FriendData pendingDeleteFriend;
	private bool isDeletingFriend;

	#region 生命周期函数
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<FriendUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();

		ResolveFriendListView();
		InitFriendListView();
		SetAddPanelVisible(false);
		SetDeleteFriendConfirmVisible(false);
	}

	public override void OnShow()
	{
		base.OnShow();
		uiComponent.ResolveReferences();
		ResolveFriendListView();
		InitFriendListView();
		SetAddPanelVisible(false);
		pendingDeleteFriend = null;
		isDeletingFriend = false;
		SetDeleteFriendConfirmVisible(false);
		RefreshUserHeader();
		RefreshFriendRequestCountText();
		SetCountText(uiComponent.friendInvitationNum, 0);

		FriendDataManager.Instance.EnsureDebugRealFriends();
		FriendDataManager.Instance.DataChanged -= HandleFriendDataChanged;
		FriendDataManager.Instance.DataChanged += HandleFriendDataChanged;

		RebuildFriendList(true);
		RefreshCloudFriendData();
		NotifyRelationshipInviteCount();
	}

	public override void OnHide()
	{
		base.OnHide();
		avatarRequestVersion++;
		cloudFriendSyncRequestId++;
		pendingDeleteFriend = null;
		isDeletingFriend = false;
		SetDeleteFriendConfirmVisible(false);
		StopCloudFriendSyncRetry();
		FriendDataManager.Instance.DataChanged -= HandleFriendDataChanged;
		FriendOnlinePresenceManager.Instance.ClearFriendWatchers();
	}

	public override void OnDestroy()
	{
		avatarRequestVersion++;
		cloudFriendSyncRequestId++;
		StopCloudFriendSyncRetry();
		FriendOnlinePresenceManager.Instance.ClearFriendWatchers();
		base.OnDestroy();
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		CloseOpenFriendSwipe();
	}
	#endregion

	#region 数据刷新
	private void HandleFriendDataChanged()
	{
		if (!gameObject.activeInHierarchy) return;

		RefreshUserHeader();
		RebuildFriendList(false);
		NotifyFriendRequestCount();
	}

	private void RefreshCloudFriendData()
	{
		int requestId = ++cloudFriendSyncRequestId;
		FirestoreManager firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			ScheduleCloudFriendSyncRetry(requestId);
			return;
		}

		LoadCloudFriendData(firestore, requestId);
	}

	private void LoadCloudFriendData(FirestoreManager firestore, int requestId)
	{
		if (firestore == null) return;

		firestore.LoadFriends(_ =>
		{
			if (!IsValidCloudRequest(requestId)) return;
			RebuildFriendList(false);
			NotifyFriendDailyOracleCount();
		});
		firestore.LoadFriendRequests(_ =>
		{
			if (!IsValidCloudRequest(requestId)) return;
			NotifyFriendRequestCount();
			NotifyRelationshipInviteCount();
		});
		firestore.LoadVirtualFriends(_ =>
		{
			if (!IsValidCloudRequest(requestId)) return;
			RebuildFriendList(false);
		});
	}

	private bool IsValidCloudRequest(int requestId)
	{
		return requestId == cloudFriendSyncRequestId && gameObject != null && gameObject.activeInHierarchy;
	}

	private void ScheduleCloudFriendSyncRetry(int requestId)
	{
		StopCloudFriendSyncRetry();
		if (uiComponent == null || !gameObject.activeInHierarchy) return;
		cloudFriendSyncRetryCoroutine = uiComponent.StartCoroutine(CloudFriendSyncRetryRoutine(requestId));
	}

	private void StopCloudFriendSyncRetry()
	{
		if (cloudFriendSyncRetryCoroutine != null)
			uiComponent.StopCoroutine(cloudFriendSyncRetryCoroutine);
		cloudFriendSyncRetryCoroutine = null;
	}

	private IEnumerator CloudFriendSyncRetryRoutine(int requestId)
	{
		const int maxAttempts = 10;
		for (int i = 0; i < maxAttempts; i++)
		{
			yield return new WaitForSeconds(1.5f);
			if (!IsValidCloudRequest(requestId)) yield break;

			FirestoreManager firestore = FirestoreManager.Instance;
			if (firestore != null && firestore.IsInitialized)
			{
				cloudFriendSyncRetryCoroutine = null;
				LoadCloudFriendData(firestore, requestId);
				yield break;
			}
		}

		cloudFriendSyncRetryCoroutine = null;
	}

	private void NotifyFriendRequestCount()
	{
		int count = FriendDataManager.Instance != null && FriendDataManager.Instance.InviteList != null
			? FriendDataManager.Instance.InviteList.Count
			: 0;
		AppNotificationScheduler.Instance.NotifyFriendRequestCount(count);
		SetCountText(uiComponent != null ? uiComponent.friendRequestText : null, count);
	}

	private void NotifyRelationshipInviteCount()
	{
		RelationshipDivinationFirestore service = RelationshipDivinationFlow.GetOrCreateService();
		if (service == null || !service.IsReady)
		{
			SetCountText(uiComponent != null ? uiComponent.friendInvitationNum : null, 0);
			return;
		}

		service.LoadIncomingInvites((records, succeeded) =>
		{
			int count = succeeded && records != null ? records.Count : 0;
			AppNotificationScheduler.Instance.NotifyRelationshipInviteCount(count);
			SetCountText(uiComponent != null ? uiComponent.friendInvitationNum : null, count);
		});
	}

	private void RefreshFriendRequestCountText()
	{
		int count = FriendDataManager.Instance != null && FriendDataManager.Instance.InviteList != null
			? FriendDataManager.Instance.InviteList.Count
			: 0;
		SetCountText(uiComponent != null ? uiComponent.friendRequestText : null, count);
	}

	private void SetCountText(TMP_Text text, int count)
	{
		if (text == null) return;

		bool visible = count > 0;
		text.gameObject.SetActive(visible);
		if (!visible) return;

		text.text = count > 99 ? "99+" : count.ToString();
	}

	private void NotifyFriendDailyOracleCount()
	{
		DailyOracleFirestore store = GetOrCreateDailyOracleStore();
		if (store == null || !store.IsReady)
			return;

		store.LoadTodayFriendSummaries(summaries =>
		{
			AppNotificationScheduler.Instance.NotifyFriendDailyOracleCount(summaries != null ? summaries.Count : 0);
		});
	}

	private DailyOracleFirestore GetOrCreateDailyOracleStore()
	{
		DailyOracleFirestore store = DailyOracleFirestore.Instance;
		if (store != null)
			return store;

		GameObject go = new GameObject("DailyOracleFirestore");
		return go.AddComponent<DailyOracleFirestore>();
	}
	#endregion

	#region 用户头部
	private void RefreshUserHeader()
	{
		uiComponent.ResolveReferences();

		if (uiComponent.nameText != null)
			uiComponent.nameText.text = GetCurrentUserName();

		if (uiComponent.stateText != null)
			uiComponent.stateText.text = IsCurrentUserOnline() ? "online" : "offline";

		LoadCurrentUserAvatar();
	}

	private string GetCurrentUserName()
	{
		UserDataManager userData = UserDataManager.Instance;
		if (userData != null && !string.IsNullOrWhiteSpace(userData.UserName))
			return userData.UserName.Trim();
		if (userData != null && !string.IsNullOrWhiteSpace(userData.Email))
			return userData.Email.Trim();
		return "Gato";
	}

	private bool IsCurrentUserOnline()
	{
		UserDataManager userData = UserDataManager.Instance;
		if (userData == null) return Application.internetReachability != NetworkReachability.NotReachable;
		return userData.IsFirebaseAuthenticated || userData.IsLoggedIn();
	}

	private void LoadCurrentUserAvatar()
	{
		if (uiComponent.headImage == null) return;

		FriendAvatarImageUtility.ApplyAvatar(uiComponent.headImage, null);
		int requestId = ++avatarRequestVersion;
		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadCurrentUserAvatarCoroutine((sprite, _) =>
		{
			if (requestId != avatarRequestVersion || uiComponent == null || uiComponent.headImage == null)
				return;
			FriendAvatarImageUtility.ApplyAvatar(uiComponent.headImage, sprite);
		}));
	}
	#endregion

	#region 好友列表
	private void ResolveFriendListView()
	{
		if (friendListView != null) return;

		friendListView = uiComponent.friendListView;
		if (friendListView == null)
		{
			LoopListView2[] listViews = gameObject.GetComponentsInChildren<LoopListView2>(true);
			if (listViews.Length > 0)
				friendListView = listViews[0];
		}

		if (uiComponent.friendListView == null)
			uiComponent.friendListView = friendListView;
	}

	private bool InitFriendListView()
	{
		if (friendListView == null) return false;
		if (friendListViewInited || friendListView.ListViewInited)
		{
			friendListViewInited = true;
			return true;
		}

		friendListView.InitListView(0, OnGetFriendItemByIndex);
		friendListViewInited = true;
		return true;
	}

	private void RebuildFriendList(bool resetPos)
	{
		sortedFriends.Clear();

		foreach (FriendDataManager.FriendData friend in FriendDataManager.Instance.RealFriendList)
		{
			if (friend != null) sortedFriends.Add(friend);
		}
		foreach (FriendDataManager.FriendData friend in FriendDataManager.Instance.VirtualFriendList)
		{
			if (friend != null) sortedFriends.Add(friend);
		}

		sortedFriends.Sort(CompareFriendForList);
		RefreshFriendListView(resetPos);
		RefreshFriendPresenceWatchers();
	}

	private int CompareFriendForList(FriendDataManager.FriendData a, FriendDataManager.FriendData b)
	{
		if (a == null && b == null) return 0;
		if (a == null) return 1;
		if (b == null) return -1;

		int onlineCompare = IsFriendOnline(b).CompareTo(IsFriendOnline(a));
		if (onlineCompare != 0) return onlineCompare;

		int timeCompare = GetFriendSortTime(b).CompareTo(GetFriendSortTime(a));
		if (timeCompare != 0) return timeCompare;

		return string.Compare(GetFriendDisplayName(a), GetFriendDisplayName(b), StringComparison.CurrentCultureIgnoreCase);
	}

	private bool IsFriendOnline(FriendDataManager.FriendData friend)
	{
		return friend != null && !friend.isVirtual && friend.isOnline;
	}

	private long GetFriendSortTime(FriendDataManager.FriendData friend)
	{
		if (friend == null) return 0;
		return friend.isVirtual ? friend.virtualFriendLastOperatedUnixMs : friend.lastLoginUnixMs;
	}

	private void RefreshFriendListView(bool resetPos)
	{
		if (!InitFriendListView()) return;

		friendListView.SetListItemCount(sortedFriends.Count, resetPos);
		friendListView.RefreshAllShownItem();
	}

	private void RefreshFriendPresenceWatchers()
	{
		FriendOnlinePresenceManager.Instance.WatchFriends(FriendDataManager.Instance.RealFriendList);
	}

	private LoopListViewItem2 OnGetFriendItemByIndex(LoopListView2 listView, int index)
	{
		if (index < 0 || index >= sortedFriends.Count)
			return null;

		LoopListViewItem2 item = listView.NewListViewItem(FriendItemPrefabName);
		if (item == null)
			return null;

		BindFriendItem(item.gameObject, sortedFriends[index]);
		return item;
	}

	private void BindFriendItem(GameObject itemObject, FriendDataManager.FriendData friend)
	{
		if (itemObject == null || friend == null) return;

		FriendItem friendItem = itemObject.GetComponent<FriendItem>();
		if (friendItem != null)
		{
			friendItem.SetData(friend, BuildFriendStatusText(friend));
			return;
		}

		TMP_Text nameText = FindTextByName(itemObject.transform, "name", "NameText");
		TMP_Text infoText = FindTextByName(itemObject.transform, "info", "InfoText");
		Image headImage = FindImageByName(itemObject.transform, "headImage", "HeadImage", "AvatarImage", "MeAvatar");
		if (nameText != null) nameText.text = GetFriendDisplayName(friend);
		if (infoText != null)
		{
			infoText.richText = true;
			infoText.text = BuildFriendStatusText(friend);
		}
		FriendAvatarImageUtility.ApplyAvatar(headImage, FriendAvatarImageUtility.ResolveFriendAvatar(friend, headImage));
	}

	private string BuildFriendStatusText(FriendDataManager.FriendData friend)
	{
		if (friend == null) return string.Empty;

		if (friend.isVirtual)
		{
			string accessText = friend.virtualFriendLastOperatedUnixMs > 0
				? $"上次访问{FormatRelativeTime(friend.virtualFriendLastOperatedUnixMs)}"
				: "暂无访问记录";
			return friend.virtualFriendLastOperatedUnixMs > 0
				? $"{ColorText("创建好友", "#D6A15C")}·{ColorText(accessText, "#C9B8D8")}"
				: $"{ColorText("创建好友", "#D6A15C")}·{ColorText(accessText, "#8F8598")}";
		}

		string stateText = friend.isOnline ? "在线" : "离线";
		string stateColor = friend.isOnline ? "#58D878" : "#8F8598";
		if (friend.isOnline)
			return $"{ColorText("真实好友", "#D6A15C")}·{ColorText(stateText, stateColor)}";

		string lastOnlineText = friend.lastLoginUnixMs > 0
			? $"上次上线{FormatRelativeTime(friend.lastLoginUnixMs)}"
			: "暂无上线记录";

		return $"{ColorText("真实好友", "#D6A15C")}·{ColorText(stateText, stateColor)}，{ColorText(lastOnlineText, "#C9B8D8")}";
	}

	private string ColorText(string text, string color)
	{
		return $"<color={color}>{text}</color>";
	}

	private string FormatRelativeTime(long unixMs)
	{
		long deltaMs = Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - unixMs);
		TimeSpan delta = TimeSpan.FromMilliseconds(deltaMs);
		if (delta.TotalMinutes < 1) return "刚刚";
		if (delta.TotalHours < 1) return $"{Mathf.FloorToInt((float)delta.TotalMinutes)}分钟前";
		if (delta.TotalDays < 1) return $"{Mathf.FloorToInt((float)delta.TotalHours)}小时前";
		if (delta.TotalDays < 7) return $"{Mathf.FloorToInt((float)delta.TotalDays)}天前";
		return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime.ToString("MM/dd");
	}

	private string GetFriendDisplayName(FriendDataManager.FriendData friend)
	{
		if (friend == null) return "好友";
		if (!string.IsNullOrWhiteSpace(friend.name)) return friend.name.Trim();
		if (!string.IsNullOrWhiteSpace(friend.handle)) return friend.handle.Trim();
		return friend.isVirtual ? "虚拟好友" : "好友";
	}

	private TMP_Text FindTextByName(Transform root, params string[] names)
	{
		if (root == null || names == null) return null;
		if (NameMatches(root.name, names)) return root.GetComponent<TMP_Text>();

		for (int i = 0; i < root.childCount; i++)
		{
			TMP_Text result = FindTextByName(root.GetChild(i), names);
			if (result != null) return result;
		}

		return null;
	}

	private Image FindImageByName(Transform root, params string[] names)
	{
		if (root == null || names == null) return null;
		if (NameMatches(root.name, names)) return root.GetComponent<Image>();

		for (int i = 0; i < root.childCount; i++)
		{
			Image result = FindImageByName(root.GetChild(i), names);
			if (result != null) return result;
		}

		return null;
	}

	private bool NameMatches(string objectName, params string[] names)
	{
		if (string.IsNullOrEmpty(objectName)) return false;
		foreach (string name in names)
		{
			if (objectName == name) return true;
		}
		return false;
	}
	#endregion

	#region 添加面板
	private void CloseOpenFriendSwipe()
	{
		FriendSwipeRevealItem.CloseCurrentOpen();
	}

	private void SetAddPanelVisible(bool visible)
	{
		if (uiComponent.addPanelGO != null)
			uiComponent.addPanelGO.SetActive(visible);
	}

	public void ShowDeleteFriendConfirm(FriendDataManager.FriendData friend)
	{
		if (friend == null)
		{
			ToastManager.ShowToast("好友资料不完整");
			return;
		}

		if (isDeletingFriend)
			return;

		uiComponent.ResolveReferences();
		pendingDeleteFriend = friend;
		SetAddPanelVisible(false);
		CloseOpenFriendSwipe();
		RefreshDeleteFriendConfirmText(friend);
		SetDeleteFriendButtonsInteractable(true);
		SetDeleteFriendConfirmVisible(true);
	}

	private void RefreshDeleteFriendConfirmText(FriendDataManager.FriendData friend)
	{
		if (uiComponent.deleteContent == null) return;

		uiComponent.deleteContent.richText = true;
		string friendName = EscapeTmpRichText(GetFriendDisplayName(friend));
		uiComponent.deleteContent.text = $"<color=#FFFFFF>是否确定要删除</color><color=#D58A3F>{friendName}</color><color=#FFFFFF>?</color>";
	}

	private string EscapeTmpRichText(string value)
	{
		if (string.IsNullOrEmpty(value)) return string.Empty;
		return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
	}

	private void SetDeleteFriendConfirmVisible(bool visible)
	{
		if (uiComponent == null) return;

		uiComponent.ResolveReferences();
		if (uiComponent.deleteFriendRect == null) return;

		uiComponent.deleteFriendRect.gameObject.SetActive(visible);

		Image overlayImage = uiComponent.deleteFriendRect.GetComponent<Image>();
		if (overlayImage != null)
			overlayImage.raycastTarget = visible;
	}

	private void SetDeleteFriendButtonsInteractable(bool interactable)
	{
		if (uiComponent.cancelBtn != null)
			uiComponent.cancelBtn.interactable = interactable;
		if (uiComponent.sureBtn != null)
			uiComponent.sureBtn.interactable = interactable;
	}

	public void OnCancelDeleteFriendButtonClick()
	{
		if (isDeletingFriend)
			return;

		pendingDeleteFriend = null;
		SetDeleteFriendConfirmVisible(false);
	}

	public void OnConfirmDeleteFriendButtonClick()
	{
		if (pendingDeleteFriend == null || isDeletingFriend)
			return;

		isDeletingFriend = true;
		SetDeleteFriendButtonsInteractable(false);

		if (pendingDeleteFriend.isVirtual)
		{
			DeleteVirtualFriendFromConfirm(pendingDeleteFriend);
			return;
		}

		DeleteRealFriendFromConfirm(pendingDeleteFriend);
	}

	private void DeleteVirtualFriendFromConfirm(FriendDataManager.FriendData friend)
	{
		FirestoreManager firestore = FirestoreManager.Instance;
		if (firestore != null)
		{
			ToastManager.ShowToast($"正在删除 {GetFriendDisplayName(friend)}");
			firestore.DeleteVirtualFriend(friend, success =>
			{
				isDeletingFriend = false;
				if (!success)
				{
					SetDeleteFriendButtonsInteractable(true);
					ToastManager.ShowToast("删除失败");
					return;
				}

				bool queued = FirestoreManager.IsVirtualFriendDeleteQueuedLocal(friend.virtualFriendId);
				ToastManager.ShowToast(queued ? "已删除创建的好友，云端稍后同步" : "已删除创建的好友");
				CloseAfterFriendDeleted();
			});
			return;
		}

		FirestoreManager.QueueVirtualFriendDeleteLocal(friend);
		bool removed = FriendDataManager.Instance != null && FriendDataManager.Instance.RemoveVirtualFriend(friend.id);
		isDeletingFriend = false;
		ToastManager.ShowToast(removed ? "已删除创建的好友，云端稍后同步" : "删除失败");
		if (removed)
			CloseAfterFriendDeleted();
		else
			SetDeleteFriendButtonsInteractable(true);
	}

	private void DeleteRealFriendFromConfirm(FriendDataManager.FriendData friend)
	{
		FirestoreManager firestore = FirestoreManager.Instance;
		if (firestore == null)
		{
			string friendUid = friend.firebaseUid;
			bool queued = !string.IsNullOrWhiteSpace(friendUid);
			FirestoreManager.QueueRealFriendDeleteLocal(friendUid);
			bool removed = RemoveRealFriendLocal(friend);
			isDeletingFriend = false;
			ToastManager.ShowToast(BuildLocalDeleteToast(removed, queued));
			if (removed || queued)
				CloseAfterFriendDeleted();
			else
				SetDeleteFriendButtonsInteractable(true);
			return;
		}

		if (!firestore.IsInitialized)
		{
			ToastManager.ShowToast($"正在删除 {GetFriendDisplayName(friend)}");
			firestore.RemoveRealFriend(friend, success =>
			{
				isDeletingFriend = false;
				bool queued = FirestoreManager.IsRealFriendDeleteQueuedLocal(friend.firebaseUid);
				ToastManager.ShowToast(success ? BuildRealFriendDeleteSuccessToast(queued) : "删除好友失败，请稍后再试");
				if (success)
					CloseAfterFriendDeleted();
				else
					SetDeleteFriendButtonsInteractable(true);
			});
			return;
		}

		ToastManager.ShowToast($"正在删除 {GetFriendDisplayName(friend)}");
		firestore.RemoveRealFriend(friend, success =>
		{
			isDeletingFriend = false;
			bool queued = FirestoreManager.IsRealFriendDeleteQueuedLocal(friend.firebaseUid);
			ToastManager.ShowToast(success ? BuildRealFriendDeleteSuccessToast(queued) : "删除好友失败，请稍后再试");
			if (success)
				CloseAfterFriendDeleted();
			else
				SetDeleteFriendButtonsInteractable(true);
		});
	}

	private bool RemoveRealFriendLocal(FriendDataManager.FriendData friend)
	{
		if (friend == null || FriendDataManager.Instance == null)
			return false;

		if (!string.IsNullOrWhiteSpace(friend.firebaseUid)
			&& FriendDataManager.Instance.RemoveRealFriendByFirebaseUid(friend.firebaseUid))
			return true;

		return FriendDataManager.Instance.RemoveRealFriend(friend.id);
	}

	private string BuildLocalDeleteToast(bool removed, bool queued)
	{
		if (removed && queued) return "已删除好友，云端稍后同步";
		if (removed) return "已从本地好友列表移除";
		if (queued) return "已加入云端删除队列";
		return "删除好友失败，请稍后再试";
	}

	private string BuildRealFriendDeleteSuccessToast(bool queued)
	{
		return queued ? "已删除好友，云端稍后同步" : "已删除好友";
	}

	private void CloseAfterFriendDeleted()
	{
		pendingDeleteFriend = null;
		isDeletingFriend = false;
		SetDeleteFriendButtonsInteractable(true);
		SetDeleteFriendConfirmVisible(false);
		UIModule.Instance.HideWindow<FriendProfileUI>();
		RebuildFriendList(false);
	}

	public void OnAddExpandButtonClick()
	{
		CloseOpenFriendSwipe();
		bool nextVisible = uiComponent.addPanelGO == null || !uiComponent.addPanelGO.activeSelf;
		SetAddPanelVisible(nextVisible);
	}

	public void OnExitAddPanelButtonClick()
	{
		CloseOpenFriendSwipe();
		SetAddPanelVisible(false);
	}

	public void OnAddFriendButtonClick()
	{
		CloseOpenFriendSwipe();
		SetAddPanelVisible(false);
		UIModule.Instance.PopUpWindow<UserSearchUI>();
	}

	public void OnCreateFriendButtonClick()
	{
		CloseOpenFriendSwipe();
		SetAddPanelVisible(false);
		UIModule.Instance.PopUpWindow<CreateFriendUI>();
	}

	public void OnSettingButtonClick()
	{
		CloseOpenFriendSwipe();
		SetAddPanelVisible(false);
		UIModule.Instance.PopUpWindow<MemoryPrivacySettingsUI>();
	}
	#endregion

	#region UI组件事件
	public void OnSearchButtonClick()
	{
		CloseOpenFriendSwipe();
		UIModule.Instance.PopUpWindow<UserSearchUI>();
	}

	public void OnFriendRequestButtonClick()
	{
		CloseOpenFriendSwipe();
		UIModule.Instance.PopUpWindow<FriendRequestUI>();
	}

	public void OnAlreadyReceiveInviteButtonClick()
	{
		CloseOpenFriendSwipe();
		UIModule.Instance.PopUpWindow<MyInviationUI>();
	}

	public void OnNotionButtonClick()
	{
		CloseOpenFriendSwipe();
		UIModule.Instance.PopUpWindow<NotionUI>();
	}

	private IEnumerator OpenLatestIncomingRelationshipInviteRoutine()
	{
		RelationshipDivinationFirestore service = RelationshipDivinationFlow.GetOrCreateService();
		const int maxAttempts = 20;
		for (int i = 0; i < maxAttempts; i++)
		{
			if (service != null && service.IsReady)
				break;

			yield return new WaitForSeconds(0.5f);
			service = RelationshipDivinationFlow.GetOrCreateService();
		}

		if (service == null || !service.IsReady)
		{
			ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
			yield break;
		}

		bool completed = false;
		service.LoadIncomingInvites((records, succeeded) =>
		{
			completed = true;
			if (!succeeded)
			{
				ToastManager.ShowToast("双人占卜邀请同步失败，请稍后再试");
				return;
			}

			if (records == null || records.Count == 0)
			{
				ToastManager.ShowToast("暂无待加入的双人占卜邀请");
				return;
			}

			RelationshipDivinationFlow.ShowRecord(records[0]);
		});

		while (!completed)
			yield return null;
	}
	#endregion
}
