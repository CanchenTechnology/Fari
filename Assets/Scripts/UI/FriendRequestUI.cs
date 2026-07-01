/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/24/2026 4:22:52 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using I2.Loc;
using TMPro;
using UnityEngine.EventSystems;
using SuperScrollView;

public class FriendRequestUI : WindowBase
{
	private const string FriendRequestItemPrefabName = "FriendItem";
	private const string EmptyStateTerm = "FriendRequest/EmptyState";
	private const string AddFriendLinkId = "add_friend";
	private const string CreateFriendLinkId = "create_friend";
	private const string EmptyStateLinkColor = "#FE8E54";
	private const string DefaultEmptyStateText =
		"暂无好友请求，去 <link=\"add_friend\"><color=#FE8E54>添加</color></link> / <link=\"create_friend\"><color=#FE8E54>创建</color></link>";
	public FriendRequestUIComponent uiComponent;
	private readonly List<FriendDataManager.InviteData> requestInvites = new List<FriendDataManager.InviteData>();
	private readonly HashSet<string> processingRequestKeys = new HashSet<string>();
	private LoopListView2 friendLoopListView;
	private bool friendLoopListViewInited;
	private string focusedRequesterUid;
	private bool isLoadingRequests;
	private bool dataChangedSubscribed;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<FriendRequestUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("FriendRequestUI 缺少 UI 组件绑定脚本：FriendRequestUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		SetupEmptyStateText();
		ResolveFriendLoopListView();
		InitFriendLoopListView();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		SetupEmptyStateText();
		SubscribeDataChanged();
		ResolveFriendLoopListView();
		InitFriendLoopListView();
		isLoadingRequests = FirestoreManager.Instance != null && FirestoreManager.Instance.IsInitialized;
		RefreshRequestList();
		MarkCurrentRequestsSeen();
		LoadCloudFriendRequests();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		UnsubscribeDataChanged();
		isLoadingRequests = false;
		RefreshFriendLoopListView();
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		UnsubscribeDataChanged();
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public void FocusRequester(string requesterUid)
	{
		focusedRequesterUid = requesterUid ?? string.Empty;
		RefreshRequestList();
	}
	#endregion

	#region UI组件事件
	public void OnexitBtnButtonClick()
	{
		HideWindow();
	}
	#endregion

	private void SetupEmptyStateText()
	{
		if (uiComponent == null) return;

		uiComponent.ResolveReferences();
		TMP_Text text = ResolveEmptyStateText();
		if (text == null) return;

		text.richText = true;
		text.raycastTarget = true;
		text.text = GetEmptyStateText();
		text.ForceMeshUpdate();

		FriendRequestLinkClickHandler handler = text.GetComponent<FriendRequestLinkClickHandler>();
		if (handler == null)
			handler = text.gameObject.AddComponent<FriendRequestLinkClickHandler>();
		handler.Bind(text, HandleEmptyStateLinkClicked);
		RefreshEmptyStateText();
	}

	private TMP_Text ResolveEmptyStateText()
	{
		if (uiComponent.emptyStateText != null)
			return uiComponent.emptyStateText;

		RectTransform parent = uiComponent.noFriendContentRoot != null
			? uiComponent.noFriendContentRoot
			: transform as RectTransform;
		if (parent == null)
			return null;

		GameObject textObject = new GameObject("EmptyStateText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
		textObject.layer = gameObject.layer;
		RectTransform rect = textObject.GetComponent<RectTransform>();
		rect.SetParent(parent, false);
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = new Vector2(0f, -250f);
		rect.sizeDelta = new Vector2(760f, 120f);

		TMP_Text text = textObject.GetComponent<TMP_Text>();
		text.fontSize = 34f;
		text.alignment = TextAlignmentOptions.Center;
		text.color = Color.white;
		text.enableWordWrapping = true;
		text.overflowMode = TextOverflowModes.Overflow;
		CopyFontFromExistingText(text);

		uiComponent.emptyStateText = text;
		return text;
	}

	private void LoadCloudFriendRequests()
	{
		FirestoreManager firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			isLoadingRequests = false;
			RefreshRequestList();
			return;
		}

		isLoadingRequests = true;
		RefreshRequestList();
		firestore.LoadFriendRequests(success =>
		{
			isLoadingRequests = false;
			if (!success)
				Debug.LogWarning("[FriendRequestUI] 拉取好友请求失败，保留本地缓存");
			RefreshRequestList();
			MarkCurrentRequestsSeen();
		});
	}

	private void SubscribeDataChanged()
	{
		if (dataChangedSubscribed) return;

		FriendDataManager manager = FriendDataManager.Instance;
		if (manager == null) return;

		manager.DataChanged += HandleFriendDataChanged;
		dataChangedSubscribed = true;
	}

	private void UnsubscribeDataChanged()
	{
		if (!dataChangedSubscribed) return;

		FriendDataManager manager = FriendDataManager.Instance;
		if (manager != null)
			manager.DataChanged -= HandleFriendDataChanged;
		dataChangedSubscribed = false;
	}

	private void HandleFriendDataChanged()
	{
		if (gameObject == null || !gameObject.activeInHierarchy) return;
		RefreshRequestList();
	}

	private void RefreshRequestList()
	{
		if (uiComponent == null) return;

		uiComponent.ResolveReferences();
		SetupEmptyStateText();
		ResolveFriendLoopListView();
		InitFriendLoopListView();

		IReadOnlyList<FriendDataManager.InviteData> invites = FriendDataManager.Instance != null
			? FriendDataManager.Instance.InviteList
			: null;
		requestInvites.Clear();
		if (invites != null)
		{
			for (int i = 0; i < invites.Count; i++)
			{
				if (invites[i] != null)
					requestInvites.Add(invites[i]);
			}
		}
		if (!string.IsNullOrWhiteSpace(focusedRequesterUid))
			requestInvites.Sort(CompareFocusedInvite);

		bool hasRequests = requestInvites.Count > 0;
		bool showEmptyState = !isLoadingRequests && !hasRequests;

		SetRootActive(uiComponent.noFriendContentRoot, showEmptyState);
		SetRootActive(GetFriendScrollRoot(), hasRequests);
		RefreshEmptyStateText();
		RefreshFriendLoopListView();
	}

	private void MarkCurrentRequestsSeen()
	{
		if (requestInvites.Count == 0)
			return;

		List<FriendDataManager.InviteData> cachedInvites = new List<FriendDataManager.InviteData>(requestInvites);
		FriendRequestUnreadTracker.MarkSeen(cachedInvites);
		AppNotificationScheduler.Instance.NotifyFriendRequestCount(0);

		FirestoreManager firestore = FirestoreManager.Instance;
		if (firestore != null && firestore.IsInitialized)
			firestore.MarkFriendRequestsSeen(cachedInvites);
	}

	private void ResolveFriendLoopListView()
	{
		if (friendLoopListView != null) return;

		uiComponent.ResolveReferences();
		friendLoopListView = uiComponent.friendLoopListView;
		if (friendLoopListView == null)
			friendLoopListView = gameObject.GetComponentInChildren<LoopListView2>(true);

		if (uiComponent.friendLoopListView == null)
			uiComponent.friendLoopListView = friendLoopListView;
	}

	private bool InitFriendLoopListView()
	{
		if (friendLoopListView == null) return false;
		if (friendLoopListViewInited || friendLoopListView.ListViewInited)
		{
			friendLoopListViewInited = true;
			return true;
		}

		friendLoopListView.InitListView(0, OnGetFriendRequestItemByIndex);
		friendLoopListViewInited = true;
		return true;
	}

	private void RefreshFriendLoopListView()
	{
		if (!InitFriendLoopListView()) return;

		friendLoopListView.SetListItemCount(requestInvites.Count, false);
		friendLoopListView.RefreshAllShownItem();
	}

	private LoopListViewItem2 OnGetFriendRequestItemByIndex(LoopListView2 listView, int index)
	{
		if (index < 0 || index >= requestInvites.Count)
			return null;

		LoopListViewItem2 item = listView.NewListViewItem(FriendRequestItemPrefabName);
		if (item == null)
			return null;

		BindFriendRequestItem(item.gameObject, requestInvites[index]);
		return item;
	}

	private void BindFriendRequestItem(GameObject itemObject, FriendDataManager.InviteData invite)
	{
		if (itemObject == null || invite == null) return;

		FriendItem friendItem = itemObject.GetComponent<FriendItem>();
		if (friendItem != null)
		{
			string requestKey = GetRequestKey(invite);
			bool processing = processingRequestKeys.Contains(requestKey);
			friendItem.SetFriendRequestData(invite, IsInviteAlreadyAdded(invite), processing, HandleAcceptRequestFromItem);
			return;
		}

		TMP_Text nameText = FindTextByName(itemObject.transform, "name", "Name", "NameText");
		TMP_Text infoText = FindTextByName(itemObject.transform, "info", "Info", "InfoText");
		Button actionButton = FindButtonByName(itemObject.transform, "atBtn", "AcceptButton", "[Button]Accept", "moreBtn");

		if (nameText != null) nameText.text = GetInviteDisplayName(invite);
		if (infoText != null) infoText.text = GetInviteSignature(invite);
		BindAcceptButton(actionButton, invite);
	}

	private void BindAcceptButton(Button button, FriendDataManager.InviteData invite)
	{
		if (button == null || invite == null) return;

		bool alreadyAdded = IsInviteAlreadyAdded(invite);
		string requestKey = GetRequestKey(invite);
		bool processing = processingRequestKeys.Contains(requestKey);

		button.onClick.RemoveAllListeners();
		button.interactable = !alreadyAdded && !processing;
		SetButtonText(button, alreadyAdded ? "已添加" : "同意");
		SetButtonImageVisible(button, !alreadyAdded);
		if (!alreadyAdded && !processing)
			button.onClick.AddListener(() => HandleAcceptRequestFromItem(invite));
	}

	private void HandleAcceptRequestFromItem(FriendDataManager.InviteData invite)
	{
		if (invite == null || string.IsNullOrEmpty(invite.firebaseUid))
		{
			ToastManager.ShowToast("好友请求数据不完整");
			return;
		}

		if (IsInviteAlreadyAdded(invite))
		{
			RefreshRequestList();
			return;
		}

		AcceptFriendRequest(invite);
	}

	private RectTransform GetFriendScrollRoot()
	{
		if (friendLoopListView != null)
			return friendLoopListView.transform as RectTransform;
		return uiComponent.haveFriendContentRoot;
	}

	private bool IsInviteAlreadyAdded(FriendDataManager.InviteData invite)
	{
		return invite != null
			&& !string.IsNullOrWhiteSpace(invite.firebaseUid)
			&& FriendDataManager.Instance != null
			&& FriendDataManager.Instance.FindRealFriendByFirebaseUid(invite.firebaseUid) != null;
	}

	private int CompareFocusedInvite(FriendDataManager.InviteData a, FriendDataManager.InviteData b)
	{
		bool aFocused = IsFocusedInvite(a);
		bool bFocused = IsFocusedInvite(b);
		if (aFocused == bFocused) return 0;
		return aFocused ? -1 : 1;
	}

	private static string GetInviteDisplayName(FriendDataManager.InviteData invite)
	{
		if (invite == null) return "新的好友请求";
		if (!string.IsNullOrWhiteSpace(invite.name)) return invite.name.Trim();
		if (!string.IsNullOrWhiteSpace(invite.email)) return invite.email.Trim();
		return "新的好友请求";
	}

	private static string GetInviteSignature(FriendDataManager.InviteData invite)
	{
		if (invite == null || string.IsNullOrWhiteSpace(invite.info))
			return "想添加你为好友";
		return invite.info.Trim();
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

	private Button FindButtonByName(Transform root, params string[] names)
	{
		if (root == null || names == null) return null;
		if (NameMatches(root.name, names)) return root.GetComponent<Button>();

		for (int i = 0; i < root.childCount; i++)
		{
			Button result = FindButtonByName(root.GetChild(i), names);
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

	private void SetButtonText(Button button, string text)
	{
		if (button == null) return;
		TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
		if (label != null) label.text = text ?? string.Empty;
	}

	private void SetButtonImageVisible(Button button, bool visible)
	{
		if (button == null) return;
		Image image = button.GetComponent<Image>();
		if (image != null) image.enabled = visible;
	}

	private void AcceptFriendRequest(FriendDataManager.InviteData invite)
	{
		if (invite == null || string.IsNullOrEmpty(invite.firebaseUid))
		{
			ToastManager.ShowToast("好友请求数据不完整");
			return;
		}

		FirestoreManager firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			ToastManager.ShowToast("好友服务初始化中，请稍后再试");
			return;
		}

		string requestKey = GetRequestKey(invite);
		if (processingRequestKeys.Contains(requestKey))
			return;

		processingRequestKeys.Add(requestKey);
		RefreshFriendLoopListView();
		ToastManager.ShowToast($"正在接受 {invite.name} 的好友请求");
		firestore.AcceptFriendRequest(invite, success => HandleAcceptRequestComplete(requestKey, success));
	}

	private void HandleAcceptRequestComplete(string requestKey, bool success)
	{
		processingRequestKeys.Remove(requestKey);
		ToastManager.ShowToast(success ? "已添加好友" : "接受好友请求失败");
		RefreshRequestList();
	}

	private void RefreshEmptyStateText()
	{
		TMP_Text text = uiComponent != null ? ResolveEmptyStateText() : null;
		if (text == null) return;

		text.text = GetEmptyStateText();
		text.ForceMeshUpdate();
	}

	private bool IsFocusedInvite(FriendDataManager.InviteData invite)
	{
		return invite != null
			&& !string.IsNullOrWhiteSpace(focusedRequesterUid)
			&& string.Equals(invite.firebaseUid, focusedRequesterUid, System.StringComparison.OrdinalIgnoreCase);
	}

	private static string GetRequestKey(FriendDataManager.InviteData invite)
	{
		if (invite == null) return string.Empty;
		return !string.IsNullOrEmpty(invite.firebaseUid) ? invite.firebaseUid : invite.id.ToString();
	}

	private static void SetRootActive(RectTransform rectTransform, bool active)
	{
		if (rectTransform != null && rectTransform.gameObject.activeSelf != active)
			rectTransform.gameObject.SetActive(active);
	}

	private string GetEmptyStateText()
	{
		string emptyStateText = DefaultEmptyStateText;
		if (LocalizationManager.TryGetTranslation(EmptyStateTerm, out string translated, FixForRTL: false)
			&& !string.IsNullOrWhiteSpace(translated))
		{
			emptyStateText = translated;
		}

		return EnsureEmptyStateLinks(emptyStateText);
	}

	private string EnsureEmptyStateLinks(string emptyStateText)
	{
		if (string.IsNullOrWhiteSpace(emptyStateText))
			return DefaultEmptyStateText;

		if (emptyStateText.Contains("<link="))
			return NormalizeEmptyStateLinkColor(emptyStateText);

		string linkedText = emptyStateText;
		linkedText = ReplaceFirst(linkedText, "添加", BuildEmptyStateLink(AddFriendLinkId, "添加"));
		linkedText = ReplaceFirst(linkedText, "创建", BuildEmptyStateLink(CreateFriendLinkId, "创建"));
		linkedText = ReplaceFirst(linkedText, "adicionar", BuildEmptyStateLink(AddFriendLinkId, "adicionar"));
		linkedText = ReplaceFirst(linkedText, "criar", BuildEmptyStateLink(CreateFriendLinkId, "criar"));
		linkedText = ReplaceFirst(linkedText, "add", BuildEmptyStateLink(AddFriendLinkId, "add"));
		linkedText = ReplaceFirst(linkedText, "create", BuildEmptyStateLink(CreateFriendLinkId, "create"));
		return linkedText;
	}

	private string NormalizeEmptyStateLinkColor(string text)
	{
		if (string.IsNullOrEmpty(text)) return text;

		return text
			.Replace("#D58A3F", EmptyStateLinkColor)
			.Replace("#D58A3FCC", EmptyStateLinkColor)
			.Replace("#FE8E54CC", EmptyStateLinkColor);
	}

	private string BuildEmptyStateLink(string linkId, string label)
	{
		return $"<link=\"{linkId}\"><color={EmptyStateLinkColor}>{label}</color></link>";
	}

	private static string ReplaceFirst(string source, string oldValue, string newValue)
	{
		if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(oldValue))
			return source;

		int index = source.IndexOf(oldValue, System.StringComparison.Ordinal);
		if (index < 0)
			return source;

		return source.Substring(0, index) + newValue + source.Substring(index + oldValue.Length);
	}

	private void HandleEmptyStateLinkClicked(string linkId)
	{
		switch (linkId)
		{
			case AddFriendLinkId:
				UIModule.Instance.PopUpWindow<UserSearchUI>();
				break;
			case CreateFriendLinkId:
				UIModule.Instance.PopUpWindow<CreateFriendUI>();
				break;
			default:
				Debug.LogWarning($"[FriendRequestUI] 未处理的空状态链接: {linkId}");
				break;
		}
	}

	private void CopyFontFromExistingText(TMP_Text target)
	{
		if (target == null) return;

		TMP_Text[] texts = gameObject.GetComponentsInChildren<TMP_Text>(true);
		foreach (TMP_Text text in texts)
		{
			if (text != null && text != target && text.font != null)
			{
				target.font = text.font;
				return;
			}
		}
	}
}

public class FriendRequestLinkClickHandler : MonoBehaviour, IPointerClickHandler
{
	private TMP_Text text;
	private System.Action<string> onLinkClicked;

	public void Bind(TMP_Text targetText, System.Action<string> callback)
	{
		text = targetText;
		onLinkClicked = callback;
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (text == null || onLinkClicked == null)
			return;

		int linkIndex = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventData.pressEventCamera);
		if (linkIndex < 0)
			return;

		TMP_LinkInfo linkInfo = text.textInfo.linkInfo[linkIndex];
		onLinkClicked.Invoke(linkInfo.GetLinkID());
	}
}
