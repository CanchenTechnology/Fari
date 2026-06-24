/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/19/2026 4:42:06 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Networking;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using TMPro;

public class FriendProfileUI : WindowBase
{
	private const string HistoryItemPrefabName = "HistoryCardItem";

	public FriendProfileUIComponent uiComponent;
	private static FriendDataManager.FriendData sPendingFriend;

	private FriendDataManager.FriendData currentFriend;
	private DailyOracleSummaryRecord currentTodaySummary;
	private readonly List<FriendProfileHistoryEntry> historyEntries = new List<FriendProfileHistoryEntry>();
	private bool historyListInitialized;
	private bool isRefreshingSyncSwitch;
	private bool isRemovingFriend;
	private Sprite defaultAvatarSprite;
	private int requestVersion;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<FriendProfileUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("FriendProfileUI 缺少 UI 组件绑定脚本：FriendProfileUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		defaultAvatarSprite = uiComponent.FriendAvatarImage != null ? uiComponent.FriendAvatarImage.sprite : null;
		defaultAvatarSprite = FriendAvatarImageUtility.ResolveAvatar(defaultAvatarSprite);
		InitHistoryListView();
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		currentFriend = sPendingFriend;
		if (!CanShowForFriend(currentFriend))
		{
			ToastManager.ShowToast("创建的好友档案不显示真实好友资料页");
			HideWindow();
			return;
		}

		requestVersion++;
		RefreshProfile(true);
		LoadPublicProfileThenRefresh();
		LoadSyncSettingsThenRefresh();
		LoadFriendTodaySummary();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		requestVersion++;
		isRemovingFriend = false;
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public static bool CanShowForFriend(FriendDataManager.FriendData friend)
	{
		if (friend == null || friend.isVirtual) return false;
		if (string.IsNullOrWhiteSpace(friend.firebaseUid)) return false;
		return true;
	}

	public static void Show(FriendDataManager.FriendData friend)
	{
		if (!CanShowForFriend(friend))
		{
			ToastManager.ShowToast("创建的好友档案不显示真实好友资料页");
			return;
		}

		sPendingFriend = friend;
		UIModule.Instance.PopUpWindow<FriendProfileUI>();
	}

	private void RefreshProfile(bool resetHistory)
	{
		if (currentFriend == null || uiComponent == null) return;

		ApplyAvatar(FriendAvatarImageUtility.ResolveFriendAvatar(currentFriend, uiComponent.FriendAvatarImage, defaultAvatarSprite));

		string displayName = string.IsNullOrWhiteSpace(currentFriend.name) ? "好友" : currentFriend.name;
		if (uiComponent.FriendNameText != null)
			uiComponent.FriendNameText.text = displayName;

		SetTextByName("userName", displayName);
		string relationStatus = GetRelationStatusText();
		SetTextByName("relationText", relationStatus);
		SetTextByName("RelationBadgeText", $"♧ {relationStatus}");
		SetTextByName("QuoteText", BuildQuoteText());
		SetTextByName("JoinInfoText", BuildJoinInfoText());

		if (resetHistory)
		{
			SetHistoryEntries(new List<FriendProfileHistoryEntry>
			{
				FriendProfileHistoryEntry.Empty("正在读取好友公开同步记录...")
			});
		}
	}

	private void LoadPublicProfileThenRefresh()
	{
		if (currentFriend == null || string.IsNullOrEmpty(currentFriend.firebaseUid)) return;

		int requestId = requestVersion;
		if (!string.IsNullOrWhiteSpace(currentFriend.photoUrl))
			uiComponent.StartCoroutine(LoadFriendAvatarCoroutine(currentFriend.photoUrl, requestId));

		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized) return;

		firestore.LoadPublicProfile(currentFriend.firebaseUid, profile =>
		{
			if (requestId != requestVersion || currentFriend == null || profile == null) return;

			if (!string.IsNullOrWhiteSpace(profile.displayName))
				currentFriend.name = profile.displayName;
			if (!string.IsNullOrWhiteSpace(profile.email))
				currentFriend.handle = profile.Handle;
			if (!string.IsNullOrWhiteSpace(profile.photoUrl))
				currentFriend.photoUrl = profile.photoUrl;

			RefreshProfile(false);

			if (!string.IsNullOrWhiteSpace(profile.photoUrl))
				uiComponent.StartCoroutine(LoadFriendAvatarCoroutine(profile.photoUrl, requestId));
		});
	}

	private IEnumerator LoadFriendAvatarCoroutine(string url, int requestId)
	{
		if (string.IsNullOrWhiteSpace(url)) yield break;

		using UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
		yield return request.SendWebRequest();

		if (requestId != requestVersion || currentFriend == null) yield break;
		if (request.result != UnityWebRequest.Result.Success)
		{
			Debug.LogWarning("[FriendProfileUI] 好友头像下载失败: " + request.error);
			yield break;
		}

		Texture2D texture = DownloadHandlerTexture.GetContent(request);
		if (texture == null) yield break;

		Sprite avatar = Sprite.Create(
			texture,
			new Rect(0, 0, texture.width, texture.height),
			new Vector2(0.5f, 0.5f));
		currentFriend.headSprite = avatar;
		ApplyAvatar(avatar);
	}

	private void ApplyAvatar(Sprite avatar)
	{
		avatar = FriendAvatarImageUtility.ResolveAvatar(avatar, uiComponent?.FriendAvatarImage, defaultAvatarSprite);
		if (uiComponent.FriendAvatarImage != null)
			FriendAvatarImageUtility.ApplyAvatar(uiComponent.FriendAvatarImage, avatar, defaultAvatarSprite);
		SetImagesByName("AvatarThumb", avatar);
	}

	private string BuildQuoteText()
	{
		if (!string.IsNullOrWhiteSpace(currentFriend?.notes))
			return $"“{currentFriend.notes}”";
		if (!string.IsNullOrWhiteSpace(currentFriend?.info) && currentFriend.info != "真实好友")
			return $"“{currentFriend.info}”";
		return "“宇宙会为真诚的人，悄悄安排好运。”";
	}

	private string BuildJoinInfoText()
	{
		string handle = string.IsNullOrWhiteSpace(currentFriend?.handle) ? "Firebase 好友" : currentFriend.handle;
		return $"好友状态   {GetRelationStatusText()}    |    {handle}";
	}

	private string GetRelationStatusText()
	{
		if (!string.IsNullOrWhiteSpace(currentFriend?.info))
			return currentFriend.info;
		return "真实好友";
	}

	private void LoadFriendTodaySummary()
	{
		int requestId = requestVersion;
		if (currentFriend == null || string.IsNullOrEmpty(currentFriend.firebaseUid))
		{
			SetNoPublicHistory();
			return;
		}

		var store = DailyOracleFirestore.Instance;
		if (store == null || !store.IsReady)
		{
			SetHistoryEntries(new List<FriendProfileHistoryEntry>
			{
				FriendProfileHistoryEntry.Empty("每日牌动态服务初始化中，稍后再试。")
			});
			return;
		}

		store.LoadFriendSummary(currentFriend.firebaseUid, DateTime.Now.ToString("yyyy-MM-dd"), summary =>
		{
			if (requestId != requestVersion) return;
			currentTodaySummary = summary != null && summary.IsVisibleInFriendFeed ? summary : null;
			if (currentTodaySummary == null)
			{
				SetNoPublicHistory();
				return;
			}

			SetHistoryEntries(new List<FriendProfileHistoryEntry>
			{
				FriendProfileHistoryEntry.FromSummary(currentTodaySummary)
			});
		});
	}

	private void SetNoPublicHistory()
	{
		SetHistoryEntries(new List<FriendProfileHistoryEntry>
		{
			FriendProfileHistoryEntry.Empty("暂无公开同步记录")
		});
	}

	private void SetHistoryEntries(List<FriendProfileHistoryEntry> entries)
	{
		historyEntries.Clear();
		if (entries != null) historyEntries.AddRange(entries);
		RefreshHistoryList();
	}

	private void InitHistoryListView()
	{
		if (historyListInitialized || uiComponent?.OracleHistoryScrollViewLoopListView2 == null) return;
		uiComponent.OracleHistoryScrollViewLoopListView2.InitListView(0, OnGetHistoryItemByIndex);
		historyListInitialized = true;
	}

	private void RefreshHistoryList()
	{
		InitHistoryListView();
		if (historyListInitialized && uiComponent?.OracleHistoryScrollViewLoopListView2 != null)
		{
			uiComponent.OracleHistoryScrollViewLoopListView2.SetListItemCount(historyEntries.Count, true);
			uiComponent.OracleHistoryScrollViewLoopListView2.RefreshAllShownItem();
			return;
		}

		RefreshFallbackHistoryItems();
	}

	private LoopListViewItem2 OnGetHistoryItemByIndex(LoopListView2 listView, int index)
	{
		if (index < 0 || index >= historyEntries.Count) return null;

		LoopListViewItem2 item = listView.NewListViewItem(HistoryItemPrefabName);
		if (item == null) return null;

		BindHistoryItem(item.gameObject, historyEntries[index]);
		return item;
	}

	private void RefreshFallbackHistoryItems()
	{
		HistoryCardItem[] items = gameObject.GetComponentsInChildren<HistoryCardItem>(true);
		for (int i = 0; i < items.Length; i++)
		{
			bool hasEntry = i < historyEntries.Count;
			items[i].gameObject.SetActive(hasEntry);
			if (hasEntry) BindHistoryItem(items[i].gameObject, historyEntries[i]);
		}
	}

	private void BindHistoryItem(GameObject itemObject, FriendProfileHistoryEntry entry)
	{
		if (itemObject == null || entry == null) return;

		HistoryCardItem item = itemObject.GetComponent<HistoryCardItem>();
		if (item == null) item = itemObject.GetComponentInChildren<HistoryCardItem>(true);
		if (item == null) return;

		if (item.oracleHistoryName != null) item.oracleHistoryName.text = entry.title;
		if (item.oracleHistoryContent != null) item.oracleHistoryContent.text = entry.content;
		if (item.dateText != null) item.dateText.text = entry.date;
		if (item.historyImage != null)
		{
			item.historyImage.enabled = entry.cardSprite != null;
			if (entry.cardSprite != null)
				item.historyImage.sprite = entry.cardSprite;
		}

		if (item.oracleViewBtn != null)
		{
			item.oracleViewBtn.onClick.RemoveAllListeners();
			item.oracleViewBtn.interactable = entry.summary != null;
			if (entry.summary != null)
				item.oracleViewBtn.onClick.AddListener(() => OpenTodaySummaryDialog(entry.summary));
		}
	}

	private void OpenTodaySummaryDialog(DailyOracleSummaryRecord summary)
	{
		if (summary == null || currentFriend == null) return;
		UIModule.Instance.GetWindow<NavigationUI>()?.OpenDialogUI();
		UIModule.Instance.GetWindow<DialogUI>()?.SendAtFriendsMessage(currentFriend, BuildSummaryContext(summary));
		ToastManager.ShowToast($"已带入 {currentFriend.name} 的每日牌摘要");
	}

	private string BuildSummaryContext(DailyOracleSummaryRecord summary)
	{
		string orientation = summary.IsUpright ? "正位" : "逆位";
		string friendName = string.IsNullOrWhiteSpace(currentFriend?.name) ? "该好友" : currentFriend.name;
		string cardName = string.IsNullOrWhiteSpace(summary.cardName) ? "未知牌" : summary.cardName;

		return $"【{friendName} 公开同步的每日牌摘要】\n"
			+ $"日期：{summary.date}\n"
			+ $"牌面：{cardName}（{orientation}）\n"
			+ $"标题：{summary.title}\n"
			+ $"摘要：{summary.oracle}\n"
			+ $"微行动：{summary.microAction}\n"
			+ "提醒：这是好友公开同步的摘要，不包含完整解读。";
	}

	private void LoadSyncSettingsThenRefresh()
	{
		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			RefreshSyncSwitch();
			return;
		}

		firestore.LoadDailyDivinationSyncSettings(cloud =>
		{
			if (cloud != null)
				DailyDivinationSyncSettingsManager.Instance.ApplySettings(cloud.enabled, cloud.visibility);
			RefreshSyncSwitch();
		});
	}

	private void RefreshSyncSwitch()
	{
		if (uiComponent?.SyncSwitchSwitch == null) return;

		isRefreshingSyncSwitch = true;
		bool enabled = DailyDivinationSyncSettingsManager.Instance.Enabled
			&& DailyDivinationSyncSettingsManager.Instance.Visibility != DailyDivinationSyncVisibility.OnlyMe;
		if (uiComponent.SyncSwitchSwitch.IsToggled() != enabled)
			uiComponent.SyncSwitchSwitch.Toggle();
		isRefreshingSyncSwitch = false;
	}

	private void SaveSyncSettingFromProfile(bool enabled)
	{
		var manager = DailyDivinationSyncSettingsManager.Instance;
		manager.SetEnabled(enabled, false);
		if (enabled && manager.Visibility == DailyDivinationSyncVisibility.OnlyMe)
			manager.SetVisibility(DailyDivinationSyncVisibility.RealFriends, false);
		manager.SaveLocal();

		var settings = manager.GetSettings();
		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			ToastManager.ShowToast("同步设置已保存到本地");
			return;
		}

		firestore.SaveDailyDivinationSyncSettings(settings, success =>
		{
			if (!success)
			{
				ToastManager.ShowToast("同步设置保存失败");
				return;
			}

			var store = DailyOracleFirestore.Instance;
			if (store == null || !store.IsReady)
			{
				ToastManager.ShowToast("同步设置已保存");
				return;
			}

			store.ApplySyncSettingsToPublishedSummaries(settings, 30, OnTodaySummarySyncUpdated);
		});
	}

	private void OnTodaySummarySyncUpdated(bool success)
	{
		ToastManager.ShowToast(success ? "动态同步设置已保存" : "设置已保存，今天还没有可同步的每日牌");
	}

	private void SetTextByName(string objectName, string value)
	{
		TMP_Text text = FindTextByName(transform, objectName);
		if (text != null) text.text = value ?? "";
	}

	private TMP_Text FindTextByName(Transform root, string objectName)
	{
		if (root == null) return null;
		if (root.name == objectName)
			return root.GetComponent<TMP_Text>();

		for (int i = 0; i < root.childCount; i++)
		{
			TMP_Text result = FindTextByName(root.GetChild(i), objectName);
			if (result != null) return result;
		}

		return null;
	}

	private void SetImagesByName(string objectName, Sprite sprite)
	{
		if (sprite == null) return;
		SetImagesByNameRecursive(transform, objectName, sprite);
	}

	private void SetImagesByNameRecursive(Transform root, string objectName, Sprite sprite)
	{
		if (root == null) return;
		if (root.name == objectName)
		{
			Image image = root.GetComponent<Image>();
			if (image != null) image.sprite = sprite;
		}

		for (int i = 0; i < root.childCount; i++)
			SetImagesByNameRecursive(root.GetChild(i), objectName, sprite);
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnMoreButtonClick()
	{
		if (isRemovingFriend || currentFriend == null)
		{
			return;
		}

		FriendMoveUI.Show(currentFriend);
	}

	public void OnAllRecordsButtonClick()
	{
		if (currentFriend == null || string.IsNullOrWhiteSpace(currentFriend.firebaseUid))
		{
			ToastManager.ShowToast("好友资料不完整");
			return;
		}

		DailyOracleFirestore store = DailyOracleFirestore.Instance;
		if (store == null || !store.IsReady)
		{
			FriendOracleHistoryOverlay.Show(transform, "好友公开同步记录", historyEntries, "每日牌动态服务初始化中，稍后再试。");
			return;
		}

		ToastManager.ShowToast("正在读取好友公开同步记录");
		store.LoadRecentFriendSummaries(currentFriend.firebaseUid, 30, 20, summaries =>
		{
			List<FriendProfileHistoryEntry> entries = new List<FriendProfileHistoryEntry>();
			if (summaries != null)
			{
				foreach (DailyOracleSummaryRecord summary in summaries)
				{
					if (summary != null && summary.IsVisibleInFriendFeed)
						entries.Add(FriendProfileHistoryEntry.FromSummary(summary));
				}
			}

			FriendOracleHistoryOverlay.Show(transform, "好友公开同步记录", entries, "暂无公开同步记录");
		});
	}
	public void OnSyncSwitchClick()
	{
		if (isRefreshingSyncSwitch || uiComponent?.SyncSwitchSwitch == null) return;
		SaveSyncSettingFromProfile(uiComponent.SyncSwitchSwitch.IsToggled());
	}
	#endregion
}

public class FriendProfileHistoryEntry
{
	public string title;
	public string content;
	public string date;
	public Sprite cardSprite;
	public DailyOracleSummaryRecord summary;

	public static FriendProfileHistoryEntry Empty(string message, string content = null)
	{
		return new FriendProfileHistoryEntry
		{
			title = message,
			content = string.IsNullOrWhiteSpace(content) ? "好友开启每日占卜同步后，会在这里展示公开摘要。" : content,
			date = DateTime.Now.ToString("yyyy-MM-dd"),
			summary = null
		};
	}

	public static FriendProfileHistoryEntry FromSummary(DailyOracleSummaryRecord summary)
	{
		string orientation = summary != null && summary.IsUpright ? "正位" : "逆位";
		string cardName = string.IsNullOrWhiteSpace(summary?.cardName) ? "每日牌" : summary.cardName;
		return new FriendProfileHistoryEntry
		{
			title = string.IsNullOrWhiteSpace(summary?.title) ? $"{cardName} · {orientation}" : summary.title,
			content = string.IsNullOrWhiteSpace(summary?.oracle) ? $"{cardName} · {orientation}" : summary.oracle,
			date = string.IsNullOrWhiteSpace(summary?.date) ? DateTime.Now.ToString("yyyy-MM-dd") : summary.date,
			cardSprite = !string.IsNullOrWhiteSpace(summary?.cardId) ? TarotSpriteLoader.Load(summary.cardId) : null,
			summary = summary
		};
	}
}
