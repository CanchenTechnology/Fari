/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 11:59:17
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;

public class UserSearchUI : WindowBase
{
	private const string SearchItemPrefabName = "InviteItem";
	private const int SearchResultLimit = 20;

	public UserSearchUIComponent uiComponent;

	private string currentSearchText = string.Empty;
	private readonly List<FirestoreManager.UserSearchResult> currentResults = new List<FirestoreManager.UserSearchResult>();
	private readonly HashSet<string> requestedUserIds = new HashSet<string>();

	private LoopListView2 searchFriendListView;
	private bool searchFriendListViewInited;
	private int searchRequestVersion;

	private Text[] fallbackResultNameTexts;
	private Text[] fallbackResultHandleTexts;
	private Button[] fallbackInviteButtons;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<UserSearchUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;

		ResolveSearchFriendListView();
		InitSearchFriendListView();
		CacheFallbackResultRefs();
		RefreshResultViews();

		base.OnAwake();
		NotificationUnreadBadge.Attach(uiComponent.NotificationsButton);
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		currentSearchText = uiComponent.SearchInputInputField != null ? uiComponent.SearchInputInputField.text : string.Empty;
		ResolveSearchFriendListView();
		InitSearchFriendListView();
		LoadPendingSentRequests();
		LoadBlockedUsers();
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
		base.OnDestroy();
	}
	#endregion

	#region API Function

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
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
	}
	public void OnSearchFriendsButtonClick()
	{
		string keyword = (currentSearchText ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(keyword))
		{
			ToastManager.ShowToast("请输入用户名再搜索");
			return;
		}

		if (FirestoreManager.Instance == null)
		{
			ToastManager.ShowToast("用户搜索服务未初始化");
			return;
		}

		int requestVersion = ++searchRequestVersion;
		SetSearchButtonInteractable(false);
		currentResults.Clear();
		RefreshResultViews();

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
			RefreshResultViews();
			ShowSearchResultToast(hasSelfMatch);
		}, SearchResultLimit);
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
				if (listView.transform.name.Contains("SearchFriendScrollView"))
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

		Text nameText = FindTextByName(itemObject.transform, "NameText", "NameText1");
		Text handleText = FindTextByName(itemObject.transform, "HandleText", "HandleText1");
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
	}

	private void BindInviteButton(Button button, int index)
	{
		if (button == null)
		{
			return;
		}

			bool hasResult = index >= 0 && index < currentResults.Count;
			FirestoreManager.UserSearchResult result = hasResult ? currentResults[index] : null;
			bool isSelf = result != null && result.isSelf;
			bool alreadyFriend = IsResultAlreadyFriend(result);
			bool alreadyRequested = IsResultAlreadyRequested(result);
			bool blocked = IsResultBlocked(result);

			button.interactable = hasResult && !isSelf && !alreadyFriend;
			button.onClick.RemoveAllListeners();

			Text buttonText = button.GetComponentInChildren<Text>(true);
			if (buttonText != null)
			{
				buttonText.text = !hasResult ? string.Empty : isSelf ? "自己" : alreadyFriend ? "已添加" : blocked ? "解除" : alreadyRequested ? "取消" : "添加";
			}

			if (hasResult && !isSelf && !alreadyFriend)
			{
				int capturedIndex = index;
				button.onClick.AddListener(() =>
				{
				if (capturedIndex < 0 || capturedIndex >= currentResults.Count)
				{
					ToastManager.ShowToast("搜索结果已刷新，请重新选择");
					return;
				}

				if (IsResultBlocked(currentResults[capturedIndex]))
					UnblockSearchResult(capturedIndex);
				else if (IsResultAlreadyRequested(currentResults[capturedIndex]))
					CancelSearchResultRequest(capturedIndex);
				else
					InviteSearchResult(capturedIndex);
			});
		}
	}

	private void InviteSearchResult(int index)
	{
		if (index < 0 || index >= currentResults.Count)
		{
			ToastManager.ShowToast("请先搜索用户");
			return;
		}

		if (FirestoreManager.Instance == null)
		{
			ToastManager.ShowToast("好友请求服务未初始化");
			return;
		}

		FirestoreManager.UserSearchResult user = currentResults[index];
		if (user == null || string.IsNullOrEmpty(user.uid))
		{
			ToastManager.ShowToast("用户信息不完整");
			return;
		}

		if (IsResultAlreadyRequested(user))
		{
			CancelSearchResultRequest(index);
			return;
		}

		if (IsResultBlocked(user))
		{
			UnblockSearchResult(index);
			return;
		}

		ToastManager.ShowToast($"正在发送给 {GetDisplayName(user)}");
		FirestoreManager.Instance.SendFriendRequest(user, success =>
		{
			if (success)
			{
				requestedUserIds.Add(user.uid);
				RefreshResultViews();
			}

			ToastManager.ShowToast(success ? $"已发送给 {GetDisplayName(user)}" : "发送失败，请稍后再试");
		});
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
				RefreshResultViews();
			}

			ToastManager.ShowToast(success ? "已取消好友请求" : "取消失败，请稍后再试");
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
		});
	}

	private void LoadBlockedUsers()
	{
		if (FirestoreManager.Instance == null)
		{
			return;
		}

		FirestoreManager.Instance.LoadBlockedUsers(_ => RefreshResultViews());
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

		if (FirestoreManager.Instance == null)
		{
			ToastManager.ShowToast("好友服务未初始化");
			return;
		}

		ToastManager.ShowToast($"正在解除屏蔽 {GetDisplayName(user)}");
		FirestoreManager.Instance.UnblockUser(user.uid, success =>
		{
			RefreshResultViews();
			ToastManager.ShowToast(success ? "已解除屏蔽，可以重新添加好友" : "解除屏蔽失败，请稍后再试");
		});
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

	private void CacheFallbackResultRefs()
	{
		fallbackResultNameTexts = new[]
		{
			FindTextByName(gameObject.transform, "NameText1"),
			FindTextByName(gameObject.transform, "NameText2"),
			FindTextByName(gameObject.transform, "NameText3")
		};
		fallbackResultHandleTexts = new[]
		{
			FindTextByName(gameObject.transform, "HandleText1"),
			FindTextByName(gameObject.transform, "HandleText2"),
			FindTextByName(gameObject.transform, "HandleText3")
		};
		fallbackInviteButtons = new[]
		{
			uiComponent.Invite1Button,
			FindButtonByName(gameObject.transform, "[Button]Invite2"),
			FindButtonByName(gameObject.transform, "[Button]Invite3")
		};
	}

	private void RefreshFallbackResultViews()
	{
		for (int i = 0; i < fallbackInviteButtons.Length; i++)
		{
			bool hasResult = i < currentResults.Count;
			BindInviteButton(fallbackInviteButtons[i], hasResult ? i : -1);

			if (fallbackResultNameTexts != null && i < fallbackResultNameTexts.Length && fallbackResultNameTexts[i] != null)
			{
				fallbackResultNameTexts[i].text = hasResult ? GetDisplayName(currentResults[i]) : string.Empty;
			}

			if (fallbackResultHandleTexts != null && i < fallbackResultHandleTexts.Length && fallbackResultHandleTexts[i] != null)
			{
				fallbackResultHandleTexts[i].text = hasResult ? currentResults[i].Handle : string.Empty;
			}
		}
	}

	private static Text FindTextByName(Transform root, params string[] targetNames)
	{
		if (root == null)
		{
			return null;
		}

		Text[] texts = root.GetComponentsInChildren<Text>(true);
		foreach (Text text in texts)
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
}
