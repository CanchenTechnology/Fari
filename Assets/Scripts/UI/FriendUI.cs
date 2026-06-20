/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/11/2026 11:34:57 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class FriendUI : WindowBase
{
	public FriendUIComponent uiComponent;

	// 当前活跃的 FriendItem 列表（用于管理回收）
	private List<FriendItem> activeRealFriendItems = new List<FriendItem>();
	private List<FriendItem> activeVirtualFriendItems = new List<FriendItem>();
	private List<InviteItem> activeInviteItems = new List<InviteItem>();
	private List<GameObject> activeDailyOracleFeedItems = new List<GameObject>();
	private List<GameObject> activeRelationshipInviteItems = new List<GameObject>();

	private Transform dailyOracleFeedRoot;
	private Text dailyOracleFeedStatusText;
	private int dailyOracleFeedRequestId;
	private Transform relationshipInviteRoot;
	private Text relationshipInviteStatusText;
	private int relationshipInviteRequestId;

	// 折叠状态
	private bool realFriendExpanded = true;
	private bool virtualFriendExpanded = true;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<FriendUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		InitPoolIfNeeded();
		FriendDataManager.Instance.EnsureDebugRealFriends();
		FriendDataManager.Instance.DataChanged -= HandleFriendDataChanged;
		FriendDataManager.Instance.DataChanged += HandleFriendDataChanged;
		RefreshAllViews();
		RefreshRelationshipDivinationInvites();
		RefreshDailyOracleFeed();
		FirestoreManager.Instance.LoadFriends(_ =>
		{
			RefreshAllViews();
			RefreshRelationshipDivinationInvites();
			RefreshDailyOracleFeed();
		});
		FirestoreManager.Instance.LoadFriendRequests(_ => RefreshAllViews());
		FirestoreManager.Instance.LoadVirtualFriends(_ => RefreshAllViews());
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
		dailyOracleFeedRequestId++;
		relationshipInviteRequestId++;
		FriendDataManager.Instance.DataChanged -= HandleFriendDataChanged;
		// 隐藏时回收所有对象到池中
		ReleaseAllItems();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region 初始化

	/// <summary>
	/// 初始化对象池（仅首次调用）
	/// </summary>
	private void InitPoolIfNeeded()
	{
		FriendDataManager.Instance.InitPools(
			uiComponent.friendPrefab,
			uiComponent.invitePrefab,
			transform
		);
	}

	#endregion

	#region 视图刷新

	/// <summary>
	/// 刷新所有视图
	/// </summary>
	private void RefreshAllViews()
	{
		RefreshRealFriendView();
		RefreshVirtualFriendView();
		RefreshInviteView();
		UpdateCountText();
	}

	private void HandleFriendDataChanged()
	{
		if (gameObject.activeInHierarchy)
		{
			RefreshAllViews();
			RefreshRelationshipDivinationInvites();
			RefreshDailyOracleFeed();
		}
	}

	/// <summary>
	/// 刷新真实好友列表
	/// </summary>
	private void RefreshRealFriendView()
	{
		// 回收旧对象
		ReleaseRealFriendItems();

		var dataList = FriendDataManager.Instance.RealFriendList;
		foreach (var data in dataList)
		{
			GameObject friendGO = FriendDataManager.Instance.GetFriendItem(uiComponent.RealFriendContentTransform);
			FriendItem item = friendGO.GetComponent<FriendItem>();
			item.SetData(data);
			activeRealFriendItems.Add(item);
		}

		// 强制刷新 ContentSizeFitter 布局
		ForceRebuildLayout(uiComponent.RealFriendContentTransform);

		// 控制折叠
		uiComponent.RealFriendContentTransform.gameObject.SetActive(realFriendExpanded);
	}

	/// <summary>
	/// 刷新虚拟好友列表
	/// </summary>
	private void RefreshVirtualFriendView()
	{
		// 回收旧对象
		ReleaseVirtualFriendItems();

		var dataList = FriendDataManager.Instance.VirtualFriendList;
		foreach (var data in dataList)
		{
			GameObject friendGO = FriendDataManager.Instance.GetFriendItem(uiComponent.VirtualFriendContentTransform);
			FriendItem item = friendGO.GetComponent<FriendItem>();
			item.SetData(data);
			activeVirtualFriendItems.Add(item);
		}

		// 强制刷新 ContentSizeFitter 布局
		ForceRebuildLayout(uiComponent.VirtualFriendContentTransform);

		// 控制折叠
		uiComponent.VirtualFriendContentTransform.gameObject.SetActive(virtualFriendExpanded);
	}

	/// <summary>
	/// 刷新邀请列表
	/// </summary>
	private void RefreshInviteView()
	{
		// 回收旧对象
		ReleaseInviteItems();

		var dataList = FriendDataManager.Instance.InviteList;
		foreach (var data in dataList)
		{
			GameObject inviteGO = FriendDataManager.Instance.GetInviteItem(uiComponent.InviteBannerTransform);
			InviteItem item = inviteGO.GetComponent<InviteItem>();
			item.SetData(data);
			activeInviteItems.Add(item);
		}
		AppNotificationScheduler.Instance.NotifyFriendRequestCount(dataList.Count);

		// 强制刷新 ContentSizeFitter 布局
		ForceRebuildLayout(uiComponent.InviteBannerTransform);
	}

	private void RefreshRelationshipDivinationInvites()
	{
		EnsureRelationshipInviteRoot();
		if (relationshipInviteRoot == null) return;

		int requestId = ++relationshipInviteRequestId;
		SetRelationshipInviteStatus("正在读取关系占卜邀请...");

		RelationshipDivinationFirestore store = RelationshipDivinationFirestore.Instance;
		if (store == null || !store.IsReady)
		{
			RenderRelationshipInvites(null);
			return;
		}

		store.LoadIncomingInvites(records =>
		{
			if (requestId != relationshipInviteRequestId || !gameObject.activeInHierarchy) return;
			RenderRelationshipInvites(records);
		});
	}

	private void RenderRelationshipInvites(List<RelationshipDivinationRecord> records)
	{
		ReleaseRelationshipInviteItems();

		if (records == null || records.Count == 0)
		{
			if (relationshipInviteRoot != null)
				relationshipInviteRoot.gameObject.SetActive(false);
			return;
		}

		relationshipInviteRoot.gameObject.SetActive(true);
		SetRelationshipInviteStatus($"双人关系占卜邀请 · {records.Count}");
		AppNotificationScheduler.Instance.NotifyRelationshipInviteCount(records.Count);
		foreach (RelationshipDivinationRecord record in records)
		{
			if (record == null) continue;
			GameObject card = CreateRelationshipInviteCard(record);
			activeRelationshipInviteItems.Add(card);
		}
		ForceRebuildLayout(relationshipInviteRoot);
	}

	private void RefreshDailyOracleFeed()
	{
		EnsureDailyOracleFeedRoot();
		if (dailyOracleFeedRoot == null) return;

		int requestId = ++dailyOracleFeedRequestId;
		SetDailyOracleFeedStatus("正在读取好友今日牌...");

		if (FriendDataManager.Instance.RealFriendList.Count == 0)
		{
			RenderDailyOracleFeed(null, "添加真实好友后，可以在这里看到 TA 们同步的每日牌摘要。");
			return;
		}

		var store = DailyOracleFirestore.Instance;
		if (store == null || !store.IsReady)
		{
			RenderDailyOracleFeed(null, "每日牌动态服务初始化中，稍后再试。");
			return;
		}

		store.LoadTodayFriendSummaries(records =>
		{
			if (requestId != dailyOracleFeedRequestId || !gameObject.activeInHierarchy) return;
			RenderDailyOracleFeed(records, "今天还没有好友同步每日牌摘要。");
		});
	}

	private void RenderDailyOracleFeed(List<DailyOracleSummaryRecord> records, string emptyText)
	{
		ReleaseDailyOracleFeedItems();

		if (records == null || records.Count == 0)
		{
			SetDailyOracleFeedStatus(emptyText);
			ForceRebuildLayout(dailyOracleFeedRoot);
			return;
		}

		SetDailyOracleFeedStatus($"今日好友每日牌 · {records.Count}");
		foreach (DailyOracleSummaryRecord record in records)
		{
			if (record == null || !record.IsVisibleInFriendFeed) continue;
			GameObject card = CreateDailyOracleSummaryCard(record);
			activeDailyOracleFeedItems.Add(card);
		}

		if (activeDailyOracleFeedItems.Count == 0)
			SetDailyOracleFeedStatus(emptyText);
		else
			AppNotificationScheduler.Instance.NotifyFriendDailyOracleCount(activeDailyOracleFeedItems.Count);

		ForceRebuildLayout(dailyOracleFeedRoot);
	}

	/// <summary>
	/// 更新数量显示
	/// </summary>
	private void UpdateCountText()
	{
		if (uiComponent.ExistingCountText != null)
		{
			uiComponent.ExistingCountText.text = FriendDataManager.Instance.RealFriendList.Count.ToString();
		}
		if (uiComponent.CreatedCountText != null)
		{
			uiComponent.CreatedCountText.text = FriendDataManager.Instance.VirtualFriendList.Count.ToString();
		}
	}

	#endregion

	#region 对象回收

	private void ReleaseRealFriendItems()
	{
		// 1. 回收 tracked 的对象
		foreach (var item in activeRealFriendItems)
		{
			if (item != null)
			{
				item.ResetForPool();
				FriendDataManager.Instance.ReleaseFriendItem(item.gameObject);
			}
		}
		activeRealFriendItems.Clear();

		// 2. 清理 ContentTransform 下所有残留的 FriendItem（防止堆叠）
		var remainItems = uiComponent.RealFriendContentTransform.GetComponentsInChildren<FriendItem>(true);
		foreach (var item in remainItems)
		{
			if (item != null)
			{
				item.ResetForPool();
				FriendDataManager.Instance.ReleaseFriendItem(item.gameObject);
			}
		}
	}

	private void ReleaseVirtualFriendItems()
	{
		// 1. 回收 tracked 的对象
		foreach (var item in activeVirtualFriendItems)
		{
			if (item != null)
			{
				item.ResetForPool();
				FriendDataManager.Instance.ReleaseFriendItem(item.gameObject);
			}
		}
		activeVirtualFriendItems.Clear();

		// 2. 清理 ContentTransform 下所有残留的 FriendItem（防止堆叠）
		var remainItems = uiComponent.VirtualFriendContentTransform.GetComponentsInChildren<FriendItem>(true);
		foreach (var item in remainItems)
		{
			if (item != null)
			{
				item.ResetForPool();
				FriendDataManager.Instance.ReleaseFriendItem(item.gameObject);
			}
		}
	}

	private void ReleaseInviteItems()
	{
		// 1. 回收 tracked 的对象
		foreach (var item in activeInviteItems)
		{
			if (item != null)
			{
				item.ResetForPool();
				FriendDataManager.Instance.ReleaseInviteItem(item.gameObject);
			}
		}
		activeInviteItems.Clear();

		// 2. 清理 ContentTransform 下所有残留的 InviteItem（防止堆叠）
		var remainItems = uiComponent.InviteBannerTransform.GetComponentsInChildren<InviteItem>(true);
		foreach (var item in remainItems)
		{
			if (item != null)
			{
				item.ResetForPool();
				FriendDataManager.Instance.ReleaseInviteItem(item.gameObject);
			}
		}
	}

	private void ReleaseDailyOracleFeedItems()
	{
		foreach (GameObject item in activeDailyOracleFeedItems)
		{
			if (item != null)
				UnityEngine.Object.Destroy(item);
		}
		activeDailyOracleFeedItems.Clear();
	}

	private void ReleaseRelationshipInviteItems()
	{
		foreach (GameObject item in activeRelationshipInviteItems)
		{
			if (item != null)
				UnityEngine.Object.Destroy(item);
		}
		activeRelationshipInviteItems.Clear();
	}

	private void ReleaseAllItems()
	{
		ReleaseRealFriendItems();
		ReleaseVirtualFriendItems();
		ReleaseInviteItems();
		ReleaseRelationshipInviteItems();
		ReleaseDailyOracleFeedItems();
	}

	#endregion

	#region API Function

	private void EnsureDailyOracleFeedRoot()
	{
		if (dailyOracleFeedRoot != null) return;

		Transform parent = uiComponent.InviteBannerTransform != null
			? uiComponent.InviteBannerTransform.parent
			: uiComponent.RealFriendContentTransform != null
				? uiComponent.RealFriendContentTransform.parent
				: null;
		if (parent == null) return;

		GameObject section = new GameObject("DailyOracleFeedSection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
		section.transform.SetParent(parent, false);
		dailyOracleFeedRoot = section.transform;

		RectTransform rect = section.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		var layout = section.GetComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(0, 0, 8, 8);
		layout.spacing = 8f;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		var fitter = section.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		var element = section.GetComponent<LayoutElement>();
		element.flexibleWidth = 1f;

		if (uiComponent.InviteBannerTransform != null)
			section.transform.SetSiblingIndex(uiComponent.InviteBannerTransform.GetSiblingIndex());

		dailyOracleFeedStatusText = CreateFeedText(
			"DailyOracleFeedStatus",
			dailyOracleFeedRoot,
			"正在读取好友今日牌...",
			18,
			new Color(0.96f, 0.79f, 0.38f),
			28f,
			FontStyle.Bold);
	}

	private Text CreateFeedText(string name, Transform parent, string content, int fontSize, Color color, float minHeight, FontStyle style = FontStyle.Normal)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
		go.transform.SetParent(parent, false);

		Text text = go.GetComponent<Text>();
		text.font = GetFeedFont();
		text.text = content;
		text.fontSize = fontSize;
		text.fontStyle = style;
		text.color = color;
		text.alignment = TextAnchor.MiddleLeft;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Overflow;

		var element = go.GetComponent<LayoutElement>();
		element.minHeight = minHeight;
		element.flexibleWidth = 1f;
		return text;
	}

	private Font GetFeedFont()
	{
		if (uiComponent.ExistingCountText != null && uiComponent.ExistingCountText.font != null)
			return uiComponent.ExistingCountText.font;
		if (uiComponent.CreatedCountText != null && uiComponent.CreatedCountText.font != null)
			return uiComponent.CreatedCountText.font;
		return Resources.GetBuiltinResource<Font>("Arial.ttf");
	}

	private void EnsureRelationshipInviteRoot()
	{
		if (relationshipInviteRoot != null) return;

		Transform parent = uiComponent.InviteBannerTransform != null
			? uiComponent.InviteBannerTransform.parent
			: uiComponent.RealFriendContentTransform != null
				? uiComponent.RealFriendContentTransform.parent
				: null;
		if (parent == null) return;

		GameObject section = new GameObject("RelationshipDivinationInviteSection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
		section.transform.SetParent(parent, false);
		relationshipInviteRoot = section.transform;

		RectTransform rect = section.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		VerticalLayoutGroup layout = section.GetComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(0, 0, 8, 8);
		layout.spacing = 8f;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		section.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		section.GetComponent<LayoutElement>().flexibleWidth = 1f;

		if (uiComponent.InviteBannerTransform != null)
			section.transform.SetSiblingIndex(uiComponent.InviteBannerTransform.GetSiblingIndex() + 1);

		relationshipInviteStatusText = CreateFeedText(
			"RelationshipInviteStatus",
			relationshipInviteRoot,
			"正在读取关系占卜邀请...",
			18,
			new Color(0.96f, 0.79f, 0.38f),
			28f,
			FontStyle.Bold);
		section.SetActive(false);
	}

	private void SetRelationshipInviteStatus(string text)
	{
		EnsureRelationshipInviteRoot();
		if (relationshipInviteStatusText != null)
			relationshipInviteStatusText.text = text;
	}

	private GameObject CreateRelationshipInviteCard(RelationshipDivinationRecord record)
	{
		GameObject card = new GameObject("RelationshipDivinationInviteCard", typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
		card.transform.SetParent(relationshipInviteRoot, false);

		Image bg = card.GetComponent<Image>();
		bg.color = new Color(0.16f, 0.08f, 0.20f, 0.98f);

		Button button = card.GetComponent<Button>();
		button.targetGraphic = bg;
		button.onClick.AddListener(() => RelationshipDivinationOverlay.Show(transform, record, FindRealFriendByFirebaseUid(record.initiatorUid)));

		VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(14, 14, 10, 10);
		layout.spacing = 5f;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		card.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		LayoutElement element = card.GetComponent<LayoutElement>();
		element.minHeight = 118f;
		element.flexibleWidth = 1f;

		string inviter = string.IsNullOrWhiteSpace(record.initiatorName) ? "好友" : record.initiatorName;
		CreateFeedText("InviteTitleText", card.transform, $"{inviter} 邀请你进行双人关系占卜", 16, new Color(0.95f, 0.82f, 0.48f), 24f, FontStyle.Bold);
		CreateFeedText("InviteQuestionText", card.transform, TrimForFeed(record.question, 80), 14, new Color(0.84f, 0.80f, 0.92f), 44f);
		CreateFeedText("InviteActionText", card.transform, "点按加入并翻开你的私牌", 13, new Color(0.70f, 0.62f, 0.86f), 22f);
		return card;
	}

	private GameObject CreateDailyOracleSummaryCard(DailyOracleSummaryRecord record)
	{
		GameObject card = new GameObject("DailyOracleSummaryCard", typeof(RectTransform), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
		card.transform.SetParent(dailyOracleFeedRoot, false);

		Image bg = card.GetComponent<Image>();
		bg.color = new Color(0.08f, 0.06f, 0.14f, 0.96f);

		Button button = card.GetComponent<Button>();
		button.targetGraphic = bg;
		button.onClick.AddListener(() => OpenFriendDailyOracleDialog(record));

		var layout = card.GetComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(14, 14, 10, 10);
		layout.spacing = 5f;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		var fitter = card.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		var element = card.GetComponent<LayoutElement>();
		element.minHeight = 124f;
		element.flexibleWidth = 1f;

		string friendName = GetFriendDisplayName(record.ownerUid);
		string orientation = record.IsUpright ? "正位" : "逆位";
		string cardName = string.IsNullOrWhiteSpace(record.cardName) ? "未知牌" : record.cardName;
		string title = string.IsNullOrWhiteSpace(record.title) ? $"{cardName} · 今日牌" : record.title;
		string oracle = string.IsNullOrWhiteSpace(record.oracle) ? "好友同步了今日牌摘要。" : record.oracle;
		string action = string.IsNullOrWhiteSpace(record.microAction) ? "点按进入对话，结合好友上下文继续询问。" : record.microAction;

		CreateFeedText("FriendNameText", card.transform, $"{friendName} 的每日牌", 16, new Color(0.95f, 0.82f, 0.48f), 24f, FontStyle.Bold);
		CreateFeedText("CardTitleText", card.transform, $"{cardName} · {orientation}｜{title}", 15, Color.white, 24f, FontStyle.Bold);
		CreateFeedText("OracleText", card.transform, TrimForFeed(oracle, 72), 14, new Color(0.82f, 0.80f, 0.9f), 42f);
		CreateFeedText("ActionText", card.transform, $"✦ {TrimForFeed(action, 42)}", 13, new Color(0.70f, 0.62f, 0.86f), 22f);
		return card;
	}

	private void SetDailyOracleFeedStatus(string text)
	{
		EnsureDailyOracleFeedRoot();
		if (dailyOracleFeedStatusText != null)
			dailyOracleFeedStatusText.text = text;
	}

	private string GetFriendDisplayName(string firebaseUid)
	{
		var friend = FindRealFriendByFirebaseUid(firebaseUid);
		if (friend != null && !string.IsNullOrWhiteSpace(friend.name))
			return friend.name;
		return "好友";
	}

	private FriendDataManager.FriendData FindRealFriendByFirebaseUid(string firebaseUid)
	{
		if (string.IsNullOrWhiteSpace(firebaseUid)) return null;
		return FriendDataManager.Instance.FindRealFriendByFirebaseUid(firebaseUid);
	}

	private void OpenFriendDailyOracleDialog(DailyOracleSummaryRecord record)
	{
		if (record == null) return;

		var friend = FindRealFriendByFirebaseUid(record.ownerUid);
		if (friend == null)
		{
			ToastManager.ShowToast("好友资料还在同步中，请稍后再试");
			return;
		}

		UIModule.Instance.GetWindow<NavigationUI>()?.OpenDialogUI();
		DialogUI dialog = UIModule.Instance.GetWindow<DialogUI>();
		dialog?.SendAtFriendsMessage(friend, BuildDailyOracleFriendContext(record, friend));
		ToastManager.ShowToast($"已带入 {friend.name} 的每日牌摘要");
	}

	private string BuildDailyOracleFriendContext(DailyOracleSummaryRecord record, FriendDataManager.FriendData friend)
	{
		string friendName = friend != null && !string.IsNullOrWhiteSpace(friend.name) ? friend.name : "该好友";
		string orientation = record.IsUpright ? "正位" : "逆位";
		string cardName = string.IsNullOrWhiteSpace(record.cardName) ? "未知牌" : record.cardName;

		return $"【{friendName} 今天同步的每日牌摘要】\n"
			+ $"日期：{record.date}\n"
			+ $"牌面：{cardName}（{orientation}）\n"
			+ $"标题：{record.title}\n"
			+ $"摘要：{record.oracle}\n"
			+ $"微行动：{record.microAction}\n"
			+ "提醒：这只是好友公开同步的每日牌摘要，不包含完整解读。";
	}

	private string TrimForFeed(string text, int maxLength)
	{
		if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
			return text ?? "";
		return text.Substring(0, maxLength).TrimEnd() + "...";
	}

	/// <summary>
	/// 强制重建布局，解决 ContentSizeFitter 不及时刷新的问题。
	/// 从目标节点向上逐级刷新，确保父级 LayoutGroup + ContentSizeFitter 也同步更新。
	/// </summary>
	private void ForceRebuildLayout(Transform contentTransform)
	{
		LayoutRebuilder.ForceRebuildLayoutImmediate(contentTransform as RectTransform);

		// 向上逐级刷新，确保父节点（可能有 ContentSizeFitter）也同步
		Transform parent = contentTransform.parent;
		while (parent != null)
		{
			if (parent.GetComponent<LayoutGroup>() != null || parent.GetComponent<ContentSizeFitter>() != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(parent as RectTransform);
			}
			parent = parent.parent;
		}
	}

	#endregion

	#region UI组件事件
	public void OnNotionButtonClick()
	{
		UIModule.Instance.PopUpWindow<DailyDivinationSyncSettingsUI>();
	}
	public void OnAddRelationButtonClick()
	{
		UIModule.Instance.PopUpWindow<AddFriendUI>();
	}
	public void OnExpandRealFriendButtonClick()
	{
		realFriendExpanded = !realFriendExpanded;
		uiComponent.RealFriendContentTransform.gameObject.SetActive(realFriendExpanded);
		// 展开/折叠后强制刷新布局
		ForceRebuildLayout(uiComponent.RealFriendContentTransform);
	}
	public void OnExpandVirtualFriendButtonClick()
	{
		virtualFriendExpanded = !virtualFriendExpanded;
		uiComponent.VirtualFriendContentTransform.gameObject.SetActive(virtualFriendExpanded);
		// 展开/折叠后强制刷新布局
		ForceRebuildLayout(uiComponent.VirtualFriendContentTransform);
	}
	public void OnAddFriendButtonClick()
	{
		UIModule.Instance.PopUpWindow<AddFriendUI>();
	}
	public void OnCreateFriendButtonClick()
	{
		UIModule.Instance.PopUpWindow<CreateFriendUI>();
	}
	#endregion
}
