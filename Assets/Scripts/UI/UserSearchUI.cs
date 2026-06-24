/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 11:59:17
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using TMPro;
using DG.Tweening;

public class UserSearchUI : WindowBase
{
	private const string SearchItemPrefabName = "InviteItem";
	private const int SearchResultLimit = 20;
	private const int SearchHistoryLimit = 5;
	private const int RecommendationLimit = 3;
	private const float DuplicateSearchSuppressSeconds = 0.35f;
	private const string SearchHistoryPrefsKey = "UserSearchUI_SearchHistory_v1";

	public UserSearchUIComponent uiComponent;

	private string currentSearchText = string.Empty;
	private readonly List<FirestoreManager.UserSearchResult> currentResults = new List<FirestoreManager.UserSearchResult>();
	private readonly List<FirestoreManager.UserSearchResult> recommendedResults = new List<FirestoreManager.UserSearchResult>();
	private readonly List<string> searchHistory = new List<string>();
	private readonly HashSet<string> requestedUserIds = new HashSet<string>();

	private LoopListView2 searchFriendListView;
	private bool searchFriendListViewInited;
	private int searchRequestVersion;
	private int recommendationRequestVersion;
	private string lastSubmittedSearchKeyword = string.Empty;
	private float lastSubmittedSearchTime = -1000f;

	private TMP_Text[] fallbackResultNameTexts;
	private TMP_Text[] fallbackResultHandleTexts;
	private Image[] fallbackResultHeadImages;
	private Button[] fallbackInviteButtons;
	private GameObject[] fallbackResultRoots;
	private InviteItem[] recommendationItems;
	private SearchHistoryItem[] searchHistoryItems;
	private GameObject friendRecommendRoot;
	private GameObject historyRecordRoot;
	private Button refreshRecommendationsButton;
	private GameObject mainCenterRoot;
	private GameObject searchResultRoot;
	private bool isShowingSearchResults;
	private Tween refreshRecommendationsTween;
	private bool isLoadingRecommendations;
	private bool refreshRecommendationsButtonPreviousInteractable = true;
	private bool hasRefreshRecommendationsButtonInitialEuler;
	private Vector3 refreshRecommendationsButtonInitialEuler;

	[Serializable]
	private class SearchHistoryStore
	{
		public List<string> keywords = new List<string>();
	}

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<UserSearchUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;

			ResolveSearchModeViews();
			ResolveSearchFriendListView();
			ResolveFallbackResultViews();
			InitSearchFriendListView();
			SetSearchResultMode(false);
			ResolveRecommendationViews();
			ResolveSearchHistoryViews();
			BindRefreshRecommendationsButton();
			LoadSearchHistory();
			RefreshSearchHistoryViews();
			RefreshRecommendationViews();
			RefreshResultViews();

			base.OnAwake();
			NotificationUnreadBadge.Attach(uiComponent.NotificationsButton);
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		currentSearchText = uiComponent.SearchInputInputField != null ? uiComponent.SearchInputInputField.text : string.Empty;
			ResolveSearchModeViews();
			ResolveSearchFriendListView();
			ResolveFallbackResultViews();
			InitSearchFriendListView();
			currentResults.Clear();
			SetSearchResultMode(false);
			ResolveRecommendationViews();
			ResolveSearchHistoryViews();
			BindRefreshRecommendationsButton();
			LoadSearchHistory();
			RefreshSearchHistoryViews();
			LoadPendingSentRequests();
			LoadBlockedUsers();
			LoadFriendRecommendations();
			RefreshResultViews();
			NotificationUnreadBadge.Attach(uiComponent.NotificationsButton);
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
	}
	// 物体销毁时执行
		public override void OnDestroy()
		{
			searchRequestVersion++;
			recommendationRequestVersion++;
			if (refreshRecommendationsButton != null)
				refreshRecommendationsButton.onClick.RemoveListener(OnRefreshRecommendationsButtonClick);
			StopRefreshRecommendationsAnimation(false);
			base.OnDestroy();
		}
	#endregion

	#region API Function

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		if (isShowingSearchResults)
		{
			currentResults.Clear();
			RefreshResultViews();
			SetSearchResultMode(false);
			return;
		}

		HideWindow();
	}
	public void OnNotificationsButtonClick()
	{
		UIModule.Instance.PopUpWindow<NotionUI>();
	}
	public void OnSearchInputInputChange(string text)
	{
		currentSearchText = text ?? string.Empty;
	}
	public void OnSearchInputInputEnd(string text)
	{
		currentSearchText = text ?? string.Empty;
		SubmitSearch(false);
	}
	public void OnSearchFriendsButtonClick()
	{
		SubmitSearch(true);
	}

	private void SubmitSearch(bool showValidationToast)
	{
		string keyword = (currentSearchText ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(keyword))
		{
			if (showValidationToast)
				ToastManager.ShowToast("请输入用户名再搜索");
			return;
		}

		if (IsDuplicateSearch(keyword))
			return;

		if (FirestoreManager.Instance == null)
		{
			if (showValidationToast)
				ToastManager.ShowToast("用户搜索服务未初始化");
			return;
		}

		lastSubmittedSearchKeyword = keyword;
		lastSubmittedSearchTime = Time.unscaledTime;
		int requestVersion = ++searchRequestVersion;
		SetSearchButtonInteractable(false);
		SetSearchResultMode(true);
		currentResults.Clear();
		RefreshResultViews();
		SaveSearchHistory(keyword);
		RefreshSearchHistoryViews();

		ToastManager.ShowToast($"正在搜索：{keyword}");
		FirestoreManager.Instance.SearchUsersByName(keyword, results =>
		{
			if (requestVersion != searchRequestVersion)
			{
				return;
			}

			SetSearchButtonInteractable(true);
			currentResults.Clear();

			bool hasSelfMatch = false;
			int rawResultCount = results?.Count ?? 0;
			if (results != null)
			{
				foreach (var result in results)
				{
					if (result == null)
					{
						continue;
					}

					if (result.isSelf)
					{
						hasSelfMatch = true;
					}

					currentResults.Add(result);
				}
			}

			Debug.Log($"[UserSearchUI] 搜索完成 keyword={keyword}, raw={rawResultCount}, shown={currentResults.Count}, hasSelf={hasSelfMatch}");
			SetSearchResultMode(true);
			RefreshResultViews();
			ShowSearchResultToast(hasSelfMatch);
		}, SearchResultLimit);
	}

	private bool IsDuplicateSearch(string keyword)
	{
		return string.Equals(keyword, lastSubmittedSearchKeyword, StringComparison.Ordinal)
			&& Time.unscaledTime - lastSubmittedSearchTime <= DuplicateSearchSuppressSeconds;
	}
	public void OnInvite1ButtonClick()
	{
		InviteSearchResult(0);
	}
	public void OnInvite2ButtonClick()
	{
		InviteSearchResult(1);
	}
	public void OnInvite3ButtonClick()
	{
		InviteSearchResult(2);
	}
	public void OnRefreshRecommendationsButtonClick()
	{
		LoadFriendRecommendations(true);
	}
	#endregion

	private void ShowSearchResultToast(bool hasSelfMatch)
	{
		if (currentResults.Count > 0)
		{
			ToastManager.ShowToast($"找到 {currentResults.Count} 个用户");
			return;
		}

		string searchError = FirestoreManager.Instance != null ? FirestoreManager.Instance.LastUserSearchError : string.Empty;
		if (!string.IsNullOrEmpty(searchError))
		{
			ToastManager.ShowToast(searchError);
		}
		else
		{
			ToastManager.ShowToast(hasSelfMatch ? "这是你自己，不能添加自己" : "没有找到匹配用户");
		}
	}

	private void ResolveSearchFriendListView()
	{
		if (searchFriendListView != null)
		{
			return;
		}

		searchFriendListView = uiComponent.SearchFriendScrollViewLoopListView2;
		if (searchFriendListView == null)
		{
			LoopListView2[] listViews = gameObject.GetComponentsInChildren<LoopListView2>(true);
			foreach (LoopListView2 listView in listViews)
			{
				string listName = listView.transform.name;
				if (listName.Contains("UserSearchScrollView") || listName.Contains("SearchFriendScrollView"))
				{
					searchFriendListView = listView;
					break;
				}
			}

			if (searchFriendListView == null && listViews.Length > 0)
			{
				searchFriendListView = listViews[0];
			}
		}

		if (uiComponent.SearchFriendScrollViewLoopListView2 == null)
		{
			uiComponent.SearchFriendScrollViewLoopListView2 = searchFriendListView;
		}
	}

	private void ResolveSearchModeViews()
	{
		if (mainCenterRoot == null)
		{
			if (uiComponent.mainCenterBody != null)
				mainCenterRoot = uiComponent.mainCenterBody;
			else
			{
				Transform root = FindTransformByName(gameObject.transform, "MainCenterBody", "MainCenter");
				mainCenterRoot = root != null ? root.gameObject : null;
			}
		}

		if (searchResultRoot == null)
		{
			if (uiComponent.searchCenterBody != null)
				searchResultRoot = uiComponent.searchCenterBody.gameObject;
			else
			{
				Transform root = FindTransformByName(gameObject.transform, "SearchCenterBody", "UserSearchScrollView");
				searchResultRoot = root != null ? root.gameObject : null;
			}
		}
	}

	private void SetSearchResultMode(bool showResults)
	{
		ResolveSearchModeViews();
		isShowingSearchResults = showResults;

		if (mainCenterRoot != null)
			mainCenterRoot.SetActive(!showResults);

		if (searchResultRoot != null)
			searchResultRoot.SetActive(showResults);

		if (showResults)
		{
			ResolveSearchFriendListView();
			InitSearchFriendListView();
		}
	}

	private bool InitSearchFriendListView()
	{
		if (searchFriendListView == null)
		{
			return false;
		}

		if (searchFriendListViewInited || searchFriendListView.ListViewInited)
		{
			searchFriendListViewInited = true;
			return true;
		}

		searchFriendListView.InitListView(0, OnGetSearchFriendItemByIndex);
		searchFriendListViewInited = true;
		return true;
	}

	private LoopListViewItem2 OnGetSearchFriendItemByIndex(LoopListView2 listView, int index)
	{
		if (index < 0 || index >= currentResults.Count)
		{
			return null;
		}

		LoopListViewItem2 item = listView.NewListViewItem(SearchItemPrefabName);
		if (item == null)
		{
			return null;
		}

		BindSearchFriendItem(item.gameObject, index);
		return item;
	}

	private void BindSearchFriendItem(GameObject itemObject, int index)
	{
		if (itemObject == null || index < 0 || index >= currentResults.Count)
		{
			return;
		}

		FirestoreManager.UserSearchResult result = currentResults[index];
		InviteItem inviteItem = itemObject.GetComponent<InviteItem>();
		if (inviteItem != null)
		{
			ApplySearchResultAvatar(result, inviteItem.headImage);

			if (inviteItem.nameText != null)
			{
				inviteItem.nameText.text = GetDisplayName(result);
			}

			if (inviteItem.infoText != null)
			{
				inviteItem.infoText.text = result.Handle;
			}

			BindInviteButton(inviteItem.infoBtn, index);
			return;
		}

		TMP_Text nameText = FindTextByName(itemObject.transform, "NameText", "NameText1");
		TMP_Text handleText = FindTextByName(itemObject.transform, "HandleText", "HandleText1");
		Image headImage = FindImageByName(itemObject.transform, "AvatarImage1", "headImage", "HeadImage", "AvatarImage");
		Button inviteButton = FindButtonByName(itemObject.transform, "[Button]Invite", "[Button]Invite1");

		if (nameText != null)
		{
			nameText.text = GetDisplayName(result);
		}

		if (handleText != null)
		{
			handleText.text = result.Handle;
		}

		BindInviteButton(inviteButton, index);
		ApplySearchResultAvatar(result, headImage);
	}

	private void ApplySearchResultAvatar(FirestoreManager.UserSearchResult result, Image target)
	{
		if (target == null) return;

		string token = result == null ? string.Empty : $"{result.uid}|{result.photoUrl}";
		FriendAvatarImageUtility.SetAvatarTargetToken(target, token);
		FriendAvatarImageUtility.ApplyAvatar(target, null, FriendAvatarImageUtility.DefaultAvatarSprite);

		if (result == null || string.IsNullOrWhiteSpace(result.photoUrl) || uiComponent == null)
		{
			return;
		}

		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadSpriteFromUrlCoroutine(result.photoUrl, sprite =>
		{
			if (sprite == null || !FriendAvatarImageUtility.IsAvatarTargetTokenValid(target, token))
			{
				return;
			}

			FriendAvatarImageUtility.ApplyAvatar(target, sprite, FriendAvatarImageUtility.DefaultAvatarSprite);
		}));
	}

		private void BindInviteButton(Button button, int index)
		{
			FirestoreManager.UserSearchResult result = index >= 0 && index < currentResults.Count ? currentResults[index] : null;
			ConfigureUserActionButton(button, result, RefreshResultViews);
		}

		private void ConfigureUserActionButton(Button button, FirestoreManager.UserSearchResult result, Action onRefresh)
		{
			if (button == null) return;

			bool hasResult = result != null && !string.IsNullOrEmpty(result.uid);
			bool isSelf = result != null && result.isSelf;
			bool alreadyFriend = IsResultAlreadyFriend(result);
			bool alreadyRequested = IsResultAlreadyRequested(result);
			bool blocked = IsResultBlocked(result);

			button.interactable = hasResult && !isSelf && !alreadyFriend;
			button.onClick.RemoveAllListeners();

			TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
			if (buttonText != null)
			{
				buttonText.text = !hasResult ? string.Empty : isSelf ? "自己" : alreadyFriend ? "已添加" : blocked ? "解除" : alreadyRequested ? "取消" : "邀请";
			}

			if (button.interactable)
			{
				button.onClick.AddListener(() => HandleUserAction(result, onRefresh));
			}
		}

		private void InviteSearchResult(int index)
		{
			if (index < 0 || index >= currentResults.Count)
		{
			ToastManager.ShowToast("请先搜索用户");
				return;
			}

			HandleUserAction(currentResults[index], RefreshResultViews);
		}

	private void CancelSearchResultRequest(int index)
	{
		if (index < 0 || index >= currentResults.Count)
		{
			ToastManager.ShowToast("请先搜索用户");
			return;
		}

		FirestoreManager.UserSearchResult user = currentResults[index];
		if (user == null || string.IsNullOrEmpty(user.uid))
		{
			ToastManager.ShowToast("用户信息不完整");
			return;
		}

			CancelUserRequest(user, RefreshResultViews);
		}

		private void HandleUserAction(FirestoreManager.UserSearchResult user, Action onRefresh)
		{
			if (user == null || string.IsNullOrEmpty(user.uid))
			{
				ToastManager.ShowToast("用户信息不完整");
				return;
			}

			if (user.isSelf)
			{
				ToastManager.ShowToast("这是你自己，不能添加自己");
				return;
			}

			if (IsResultAlreadyFriend(user))
			{
				ToastManager.ShowToast("已经是好友");
				return;
			}

			if (IsResultBlocked(user))
			{
				UnblockUser(user, onRefresh);
				return;
			}

			if (IsResultAlreadyRequested(user))
			{
				CancelUserRequest(user, onRefresh);
				return;
			}

			SendFriendRequest(user, onRefresh);
		}

		private void SendFriendRequest(FirestoreManager.UserSearchResult user, Action onRefresh)
		{
			if (FirestoreManager.Instance == null)
			{
				ToastManager.ShowToast("好友请求服务未初始化");
				return;
			}

			ToastManager.ShowToast($"正在发送给 {GetDisplayName(user)}");
			FirestoreManager.Instance.SendFriendRequest(user, success =>
			{
				if (success)
				{
					requestedUserIds.Add(user.uid);
					RemoveRecommendedUser(user.uid);
					onRefresh?.Invoke();
					RefreshRecommendationViews();
				}

				ToastManager.ShowToast(success ? $"已发送给 {GetDisplayName(user)}" : "发送失败，请稍后再试");
			});
		}

		private void CancelUserRequest(FirestoreManager.UserSearchResult user, Action onRefresh)
		{
			if (FirestoreManager.Instance == null)
			{
				ToastManager.ShowToast("好友请求服务未初始化");
				return;
			}

			ToastManager.ShowToast($"正在取消给 {GetDisplayName(user)} 的请求");
			FirestoreManager.Instance.CancelSentFriendRequest(user.uid, success =>
			{
				if (success)
				{
					requestedUserIds.Remove(user.uid);
					onRefresh?.Invoke();
					RefreshRecommendationViews();
				}

				ToastManager.ShowToast(success ? "已取消好友请求" : "取消失败，请稍后再试");
			});
		}

		private void UnblockUser(FirestoreManager.UserSearchResult user, Action onRefresh)
		{
			if (FirestoreManager.Instance == null)
			{
				ToastManager.ShowToast("好友服务未初始化");
				return;
			}

			ToastManager.ShowToast($"正在解除屏蔽 {GetDisplayName(user)}");
			FirestoreManager.Instance.UnblockUser(user.uid, success =>
			{
				onRefresh?.Invoke();
				RefreshRecommendationViews();
				ToastManager.ShowToast(success ? "已解除屏蔽，可以重新添加好友" : "解除屏蔽失败，请稍后再试");
			});
		}

	private bool IsResultAlreadyRequested(FirestoreManager.UserSearchResult result)
	{
		return result != null && !string.IsNullOrEmpty(result.uid) && requestedUserIds.Contains(result.uid);
	}

	private bool IsResultAlreadyFriend(FirestoreManager.UserSearchResult result)
	{
		return result != null
			&& !string.IsNullOrEmpty(result.uid)
			&& FriendDataManager.Instance != null
			&& FriendDataManager.Instance.FindRealFriendByFirebaseUid(result.uid) != null;
	}

	private bool IsResultBlocked(FirestoreManager.UserSearchResult result)
	{
		return result != null
			&& !string.IsNullOrEmpty(result.uid)
			&& FriendDataManager.Instance != null
			&& FriendDataManager.Instance.IsUserBlocked(result.uid);
	}

	private void LoadPendingSentRequests()
	{
		if (FirestoreManager.Instance == null)
		{
			return;
		}

		FirestoreManager.Instance.LoadPendingSentFriendUids(uids =>
		{
			requestedUserIds.Clear();
			if (uids != null)
			{
				foreach (string uid in uids)
				{
					if (!string.IsNullOrEmpty(uid))
						requestedUserIds.Add(uid);
				}
			}

				RefreshResultViews();
				RefreshRecommendationViews();
			});
		}

	private void LoadBlockedUsers()
	{
		if (FirestoreManager.Instance == null)
		{
			return;
		}

			FirestoreManager.Instance.LoadBlockedUsers(_ =>
			{
				RefreshResultViews();
				RefreshRecommendationViews();
			});
	}

		private void UnblockSearchResult(int index)
		{
			if (index < 0 || index >= currentResults.Count)
			{
				ToastManager.ShowToast("请先搜索用户");
			return;
		}

		FirestoreManager.UserSearchResult user = currentResults[index];
		if (user == null || string.IsNullOrEmpty(user.uid))
		{
			ToastManager.ShowToast("用户信息不完整");
			return;
		}

			UnblockUser(user, RefreshResultViews);
		}

		private void ResolveRecommendationViews()
		{
			recommendationItems = new[]
			{
				uiComponent.item1,
				uiComponent.item2,
				uiComponent.item3
			};

			if (friendRecommendRoot == null)
			{
				Transform root = FindTransformByName(gameObject.transform, "friendRecommend");
				if (root == null && uiComponent.item1 != null && uiComponent.item1.transform.parent != null)
					root = uiComponent.item1.transform.parent.parent;
				friendRecommendRoot = root != null ? root.gameObject : null;
			}

			if (refreshRecommendationsButton == null)
				refreshRecommendationsButton = uiComponent.refreshBtn != null
					? uiComponent.refreshBtn
					: FindButtonByName(gameObject.transform, "[Button]refreshBtn", "refreshBtn");
		}

		private void BindRefreshRecommendationsButton()
		{
			if (refreshRecommendationsButton == null) return;

			refreshRecommendationsButton.onClick.RemoveListener(OnRefreshRecommendationsButtonClick);
			refreshRecommendationsButton.onClick.AddListener(OnRefreshRecommendationsButtonClick);
		}

		private void LoadFriendRecommendations()
		{
			LoadFriendRecommendations(false);
		}

		private void LoadFriendRecommendations(bool showRefreshAnimation)
		{
			ResolveRecommendationViews();
			int requestVersion = ++recommendationRequestVersion;
			isLoadingRecommendations = true;
			RefreshSearchHistoryViews();

			if (showRefreshAnimation)
				StartRefreshRecommendationsAnimation();
			else
			{
				recommendedResults.Clear();
				RefreshRecommendationViews();
			}

			if (FirestoreManager.Instance == null)
			{
				isLoadingRecommendations = false;
				RefreshSearchHistoryViews();
				StopRefreshRecommendationsAnimation(true);
				return;
			}

			FirestoreManager.Instance.LoadRecommendedUsers(results =>
			{
				if (requestVersion != recommendationRequestVersion)
					return;

				isLoadingRecommendations = false;
				recommendedResults.Clear();
				if (results != null)
				{
					foreach (FirestoreManager.UserSearchResult result in results)
					{
						if (IsRecommendableUser(result) && !recommendedResults.Exists(item => item.uid == result.uid))
							recommendedResults.Add(result);
					}
				}

				RefreshRecommendationViews();
				RefreshSearchHistoryViews();
				StopRefreshRecommendationsAnimation(true);
			}, RecommendationLimit * 3);
		}

		private void StartRefreshRecommendationsAnimation()
		{
			if (refreshRecommendationsButton == null)
				return;

			Transform buttonTransform = refreshRecommendationsButton.transform;
			if (!hasRefreshRecommendationsButtonInitialEuler)
			{
				refreshRecommendationsButtonInitialEuler = buttonTransform.localEulerAngles;
				hasRefreshRecommendationsButtonInitialEuler = true;
			}

			StopRefreshRecommendationsAnimation(false);
			refreshRecommendationsButtonPreviousInteractable = refreshRecommendationsButton.interactable;
			refreshRecommendationsButton.interactable = false;

			buttonTransform.localEulerAngles = refreshRecommendationsButtonInitialEuler;
			refreshRecommendationsTween = buttonTransform
				.DOLocalRotate(refreshRecommendationsButtonInitialEuler + new Vector3(0f, 0f, -360f), 0.7f, RotateMode.FastBeyond360)
				.SetEase(Ease.Linear)
				.SetLoops(-1, LoopType.Restart)
				.SetUpdate(true);
		}

		private void StopRefreshRecommendationsAnimation(bool snapBack)
		{
			if (refreshRecommendationsTween != null)
			{
				refreshRecommendationsTween.Kill();
				refreshRecommendationsTween = null;
			}

			if (refreshRecommendationsButton == null)
				return;

			refreshRecommendationsButton.transform.DOKill();
			refreshRecommendationsButton.interactable = refreshRecommendationsButtonPreviousInteractable;

			if (!hasRefreshRecommendationsButtonInitialEuler)
				return;

			Transform buttonTransform = refreshRecommendationsButton.transform;
			if (snapBack)
				buttonTransform.DOLocalRotate(refreshRecommendationsButtonInitialEuler, 0.16f).SetEase(Ease.OutQuad).SetUpdate(true);
			else
				buttonTransform.localEulerAngles = refreshRecommendationsButtonInitialEuler;
		}

		private void RefreshRecommendationViews()
		{
			ResolveRecommendationViews();
			List<FirestoreManager.UserSearchResult> visibleUsers = GetVisibleRecommendations();
			bool hasRecommendation = visibleUsers.Count > 0;

			if (friendRecommendRoot != null)
				friendRecommendRoot.SetActive(hasRecommendation);

			if (recommendationItems == null) return;

			for (int i = 0; i < recommendationItems.Length; i++)
			{
				InviteItem item = recommendationItems[i];
				if (item == null) continue;

				bool hasUser = i < visibleUsers.Count;
				item.gameObject.SetActive(hasUser);
				if (hasUser)
					BindRecommendationItem(item, visibleUsers[i]);
			}
		}

		private List<FirestoreManager.UserSearchResult> GetVisibleRecommendations()
		{
			List<FirestoreManager.UserSearchResult> visible = new List<FirestoreManager.UserSearchResult>();
			foreach (FirestoreManager.UserSearchResult result in recommendedResults)
			{
				if (!IsRecommendableUser(result)) continue;
				visible.Add(result);
				if (visible.Count >= RecommendationLimit) break;
			}

			return visible;
		}

		private bool IsRecommendableUser(FirestoreManager.UserSearchResult result)
		{
			return result != null
				&& !string.IsNullOrEmpty(result.uid)
				&& !result.isSelf
				&& !IsResultAlreadyFriend(result)
				&& !IsResultAlreadyRequested(result)
				&& !IsResultBlocked(result);
		}

		private void BindRecommendationItem(InviteItem item, FirestoreManager.UserSearchResult result)
		{
			if (item == null || result == null) return;

			ApplySearchResultAvatar(result, item.headImage);

			if (item.nameText != null)
				item.nameText.text = GetDisplayName(result);

			if (item.infoText != null)
				item.infoText.text = result.Handle;

			if (item.rejectBtn != null)
				item.rejectBtn.gameObject.SetActive(false);

			ConfigureUserActionButton(item.infoBtn, result, RefreshRecommendationViews);
		}

		private void RemoveRecommendedUser(string uid)
		{
			if (string.IsNullOrEmpty(uid)) return;
			recommendedResults.RemoveAll(item => item != null && item.uid == uid);
		}

		private void ResolveSearchHistoryViews()
		{
			if (historyRecordRoot == null)
			{
				Transform root = FindTransformByName(gameObject.transform, "HistoryRecord");
				if (root == null && uiComponent.recordSearchContent != null)
					root = uiComponent.recordSearchContent.parent;
				historyRecordRoot = root != null ? root.gameObject : null;
			}

			if (uiComponent.recordSearchContent == null)
				return;

			List<SearchHistoryItem> items = new List<SearchHistoryItem>(uiComponent.recordSearchContent.GetComponentsInChildren<SearchHistoryItem>(true));
			items.Sort((left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));

			SearchHistoryItem template = items.Count > 0 ? items[0] : null;
			while (template != null && items.Count < SearchHistoryLimit)
			{
				GameObject cloneObject = UnityEngine.Object.Instantiate(template.gameObject, uiComponent.recordSearchContent);
				SearchHistoryItem clone = cloneObject.GetComponent<SearchHistoryItem>();
				clone.name = $"searchHistoryItem_{items.Count + 1}";
				items.Add(clone);
			}

			searchHistoryItems = items.ToArray();
		}

		private void LoadSearchHistory()
		{
			searchHistory.Clear();
			string json = PlayerPrefs.GetString(SearchHistoryPrefsKey, string.Empty);
			if (!string.IsNullOrWhiteSpace(json))
			{
				try
				{
					SearchHistoryStore store = JsonUtility.FromJson<SearchHistoryStore>(json);
					if (store?.keywords != null)
					{
						foreach (string keyword in store.keywords)
							AddSearchHistoryKeyword(keyword, false);
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"[UserSearchUI] 搜索历史读取失败，已重置: {ex.Message}");
				}
			}

			TrimSearchHistory();
		}

		private void SaveSearchHistory(string keyword)
		{
			if (!AddSearchHistoryKeyword(keyword, true)) return;

			SearchHistoryStore store = new SearchHistoryStore { keywords = new List<string>(searchHistory) };
			PlayerPrefs.SetString(SearchHistoryPrefsKey, JsonUtility.ToJson(store));
			PlayerPrefs.Save();
		}

		private bool AddSearchHistoryKeyword(string keyword, bool newestFirst)
		{
			keyword = (keyword ?? string.Empty).Trim();
			if (string.IsNullOrEmpty(keyword)) return false;

			searchHistory.RemoveAll(item => string.Equals(item, keyword, StringComparison.OrdinalIgnoreCase));
			if (newestFirst)
				searchHistory.Insert(0, keyword);
			else
				searchHistory.Add(keyword);

			TrimSearchHistory();
			return true;
		}

		private void TrimSearchHistory()
		{
			while (searchHistory.Count > SearchHistoryLimit)
				searchHistory.RemoveAt(searchHistory.Count - 1);
		}

		private void RemoveSearchHistoryKeyword(string keyword)
		{
			keyword = (keyword ?? string.Empty).Trim();
			if (string.IsNullOrEmpty(keyword)) return;

			searchHistory.RemoveAll(item => string.Equals(item, keyword, StringComparison.OrdinalIgnoreCase));
			SearchHistoryStore store = new SearchHistoryStore { keywords = new List<string>(searchHistory) };
			PlayerPrefs.SetString(SearchHistoryPrefsKey, JsonUtility.ToJson(store));
			PlayerPrefs.Save();
			RefreshSearchHistoryViews();
		}

		private void RefreshSearchHistoryViews()
		{
			ResolveSearchHistoryViews();
			bool hasHistory = searchHistory.Count > 0 && !isLoadingRecommendations;

			if (historyRecordRoot != null)
				historyRecordRoot.SetActive(hasHistory);

			if (searchHistoryItems == null) return;

			for (int i = 0; i < searchHistoryItems.Length; i++)
			{
				SearchHistoryItem item = searchHistoryItems[i];
				if (item == null) continue;

				bool hasKeyword = i < searchHistory.Count;
				item.gameObject.SetActive(hasHistory && hasKeyword);
				if (hasKeyword)
					item.Bind(searchHistory[i], OnSearchHistorySelected, RemoveSearchHistoryKeyword);
			}
		}

		private void OnSearchHistorySelected(string keyword)
		{
			keyword = (keyword ?? string.Empty).Trim();
			if (string.IsNullOrEmpty(keyword)) return;

			currentSearchText = keyword;
			if (uiComponent.SearchInputInputField != null)
				uiComponent.SearchInputInputField.SetTextWithoutNotify(keyword);

			OnSearchFriendsButtonClick();
		}

		private static string GetDisplayName(FirestoreManager.UserSearchResult result)
		{
			if (result == null)
		{
			return "未命名用户";
		}

		if (!string.IsNullOrWhiteSpace(result.displayName))
		{
			return result.displayName;
		}

		return string.IsNullOrWhiteSpace(result.Handle) ? "未命名用户" : result.Handle;
	}

	private void SetSearchButtonInteractable(bool interactable)
	{
		if (uiComponent.SearchFriendsButton != null)
		{
			uiComponent.SearchFriendsButton.interactable = interactable;
		}
	}

	private void RefreshResultViews()
	{
		if (InitSearchFriendListView())
		{
			searchFriendListView.SetListItemCount(currentResults.Count, true);
			searchFriendListView.RefreshAllShownItem();
			return;
		}

		RefreshFallbackResultViews();
	}


	private void RefreshFallbackResultViews()
	{
		ResolveFallbackResultViews();
		if (fallbackInviteButtons == null)
		{
			return;
		}

		for (int i = 0; i < fallbackInviteButtons.Length; i++)
		{
			bool hasResult = i < currentResults.Count;
			if (fallbackResultRoots != null && i < fallbackResultRoots.Length && fallbackResultRoots[i] != null)
			{
				fallbackResultRoots[i].SetActive(hasResult);
			}

			BindInviteButton(fallbackInviteButtons[i], hasResult ? i : -1);

			if (fallbackResultNameTexts != null && i < fallbackResultNameTexts.Length && fallbackResultNameTexts[i] != null)
			{
				fallbackResultNameTexts[i].text = hasResult ? GetDisplayName(currentResults[i]) : string.Empty;
			}

			if (fallbackResultHandleTexts != null && i < fallbackResultHandleTexts.Length && fallbackResultHandleTexts[i] != null)
			{
				fallbackResultHandleTexts[i].text = hasResult ? currentResults[i].Handle : string.Empty;
			}

			if (fallbackResultHeadImages != null && i < fallbackResultHeadImages.Length)
			{
				ApplySearchResultAvatar(hasResult ? currentResults[i] : null, fallbackResultHeadImages[i]);
			}
		}
	}

	private void ResolveFallbackResultViews()
	{
		if (fallbackInviteButtons != null)
		{
			return;
		}

		ResolveSearchModeViews();
		ResolveRecommendationViews();

		Transform root = searchResultRoot != null ? searchResultRoot.transform : gameObject.transform;
		List<InviteItem> resultItems = new List<InviteItem>();
		InviteItem[] candidates = root.GetComponentsInChildren<InviteItem>(true);
		foreach (InviteItem candidate in candidates)
		{
			if (candidate == null || IsRecommendationItem(candidate))
			{
				continue;
			}

			resultItems.Add(candidate);
		}

		resultItems.Sort((left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
		if (resultItems.Count > 0)
		{
			fallbackResultRoots = new GameObject[resultItems.Count];
			fallbackResultNameTexts = new TMP_Text[resultItems.Count];
			fallbackResultHandleTexts = new TMP_Text[resultItems.Count];
			fallbackResultHeadImages = new Image[resultItems.Count];
			fallbackInviteButtons = new Button[resultItems.Count];

			for (int i = 0; i < resultItems.Count; i++)
			{
				InviteItem item = resultItems[i];
				fallbackResultRoots[i] = item.gameObject;
				fallbackResultNameTexts[i] = item.nameText != null ? item.nameText : FindTextByName(item.transform, "NameText", "NameText1");
				fallbackResultHandleTexts[i] = item.infoText != null ? item.infoText : FindTextByName(item.transform, "HandleText", "HandleText1");
				fallbackResultHeadImages[i] = item.headImage != null ? item.headImage : FindImageByName(item.transform, "AvatarImage1", "headImage", "HeadImage", "AvatarImage");
				fallbackInviteButtons[i] = item.infoBtn != null ? item.infoBtn : FindButtonByName(item.transform, "[Button]Invite", "[Button]Invite1", "Invite1");
			}

			return;
		}

		Button[] buttons = root.GetComponentsInChildren<Button>(true);
		List<Button> inviteButtons = new List<Button>();
		foreach (Button button in buttons)
		{
			if (button != null && button.transform.name.Contains("Invite", StringComparison.OrdinalIgnoreCase))
			{
				inviteButtons.Add(button);
			}
		}

		inviteButtons.Sort((left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
		fallbackInviteButtons = inviteButtons.ToArray();
		fallbackResultRoots = new GameObject[fallbackInviteButtons.Length];
		fallbackResultNameTexts = new TMP_Text[fallbackInviteButtons.Length];
		fallbackResultHandleTexts = new TMP_Text[fallbackInviteButtons.Length];
		fallbackResultHeadImages = new Image[fallbackInviteButtons.Length];

		for (int i = 0; i < fallbackInviteButtons.Length; i++)
		{
			Transform itemRoot = fallbackInviteButtons[i].transform.parent;
			fallbackResultRoots[i] = itemRoot != null ? itemRoot.gameObject : fallbackInviteButtons[i].gameObject;
			fallbackResultNameTexts[i] = FindTextByName(itemRoot, "NameText", "NameText1");
			fallbackResultHandleTexts[i] = FindTextByName(itemRoot, "HandleText", "HandleText1");
			fallbackResultHeadImages[i] = FindImageByName(itemRoot, "AvatarImage1", "headImage", "HeadImage", "AvatarImage");
		}
	}

	private bool IsRecommendationItem(InviteItem item)
	{
		if (recommendationItems == null || item == null)
		{
			return false;
		}

		foreach (InviteItem recommendationItem in recommendationItems)
		{
			if (recommendationItem == item)
			{
				return true;
			}
		}

		return false;
	}

	private static TMP_Text FindTextByName(Transform root, params string[] targetNames)
	{
		if (root == null)
		{
			return null;
		}

		TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
		foreach (TMP_Text text in texts)
		{
			foreach (string targetName in targetNames)
			{
				if (text.transform.name == targetName)
				{
					return text;
				}
			}
		}

		return null;
	}

	private static Image FindImageByName(Transform root, params string[] targetNames)
	{
		if (root == null)
		{
			return null;
		}

		Image[] images = root.GetComponentsInChildren<Image>(true);
		foreach (Image image in images)
		{
			foreach (string targetName in targetNames)
			{
				if (image.transform.name == targetName)
				{
					return image;
				}
			}
		}

		return null;
	}

		private static Button FindButtonByName(Transform root, params string[] targetNames)
		{
			if (root == null)
			{
				return null;
		}

		Button[] buttons = root.GetComponentsInChildren<Button>(true);
		foreach (Button button in buttons)
		{
			foreach (string targetName in targetNames)
			{
				if (button.transform.name == targetName)
				{
					return button;
				}
			}
		}

			return null;
		}

		private static Transform FindTransformByName(Transform root, params string[] targetNames)
		{
			if (root == null)
				return null;

			Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
			foreach (Transform transform in transforms)
			{
				foreach (string targetName in targetNames)
				{
					if (transform.name == targetName)
						return transform;
				}
			}

			return null;
		}
	}
