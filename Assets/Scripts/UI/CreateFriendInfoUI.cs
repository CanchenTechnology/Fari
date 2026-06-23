/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/19/2026 6:36:01 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using TMPro;

public class CreateFriendInfoUI : WindowBase
{
	private const string HistoryItemPrefabName = "HistoryCardItem";
	private const string RelationshipDivinationButtonName = "RelationshipDivinationButton";

	public CreateFriendInfoUIComponent uiComponent;
	private static FriendDataManager.FriendData sPendingFriend;

	private FriendDataManager.FriendData currentFriend;
	private Button relationshipDivinationButton;
	private readonly List<FriendProfileHistoryEntry> historyEntries = new List<FriendProfileHistoryEntry>();
	private bool historyListInitialized;
	private bool isRefreshingSyncSwitch;
	private Sprite defaultAvatarSprite;
	private int requestVersion;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<CreateFriendInfoUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("CreateFriendInfoUI 缺少 UI 组件绑定脚本：CreateFriendInfoUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		defaultAvatarSprite = uiComponent.FriendAvatarImage != null ? uiComponent.FriendAvatarImage.sprite : null;
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
			ToastManager.ShowToast("只有自己创建的好友会显示这个主页");
			HideWindow();
			return;
		}

		requestVersion++;
		RefreshProfile();
		RefreshRelationshipDivinationButton();
		LoadRecentOracleHistory();
		LoadSyncSettingsThenRefresh();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		requestVersion++;
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
		return friend != null && friend.isVirtual;
	}

	public static void Show(FriendDataManager.FriendData friend)
	{
		if (!CanShowForFriend(friend))
		{
			ToastManager.ShowToast("真实好友请打开好友资料页");
			return;
		}

		sPendingFriend = friend;
		UIModule.Instance.PopUpWindow<CreateFriendInfoUI>();
	}

	private void RefreshProfile()
	{
		if (currentFriend == null || uiComponent == null) return;

		ApplyAvatar(currentFriend.headSprite != null ? currentFriend.headSprite : defaultAvatarSprite);
		LoadRemoteAvatarIfNeeded();
		SetText(uiComponent.FriendNameText, FormatOptional(currentFriend.name, "未命名好友"));
		SetText(uiComponent.SignatureTextText, BuildSignatureText());
		SetText(uiComponent.birthdayDateText, FormatOptional(currentFriend.birthday, "未填写"));
		SetText(uiComponent.birthdayTimeText, FormatOptional(currentFriend.birthTime, "未填写"));
		SetText(uiComponent.birthdayCityText, FormatOptional(currentFriend.city, "未填写"));
		SetText(uiComponent.userNameText, FormatOptional(currentFriend.name, "未命名好友"));
	}

	private void RefreshRelationshipDivinationButton()
	{
		if (uiComponent == null) return;

		relationshipDivinationButton = relationshipDivinationButton != null
			? relationshipDivinationButton
			: FindRelationshipDivinationButton();
		if (relationshipDivinationButton == null)
			relationshipDivinationButton = CreateRelationshipDivinationButton();
		if (relationshipDivinationButton == null) return;

		bool visible = CreatedFriendRelationshipDivinationLocalFlow.CanHandle(currentFriend);
		relationshipDivinationButton.gameObject.SetActive(visible);
		relationshipDivinationButton.interactable = visible;
		relationshipDivinationButton.onClick.RemoveAllListeners();
		relationshipDivinationButton.onClick.AddListener(OnRelationshipDivinationButtonClick);
		RelationshipDivinationFlow.SetButtonText(relationshipDivinationButton, "关系占卜");
	}

	private Button FindRelationshipDivinationButton()
	{
		Button[] buttons = this.gameObject.GetComponentsInChildren<Button>(true);
		foreach (Button button in buttons)
		{
			if (button != null && button.gameObject.name == RelationshipDivinationButtonName)
				return button;
		}

		return null;
	}

	private Button CreateRelationshipDivinationButton()
	{
		Button sourceButton = uiComponent.EditProfileButton != null
			? uiComponent.EditProfileButton
			: uiComponent.MoreRecordsButton;
		if (sourceButton == null || sourceButton.transform.parent == null)
			return null;

		GameObject buttonObject = UnityEngine.Object.Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
		buttonObject.name = RelationshipDivinationButtonName;
		Button button = buttonObject.GetComponent<Button>();
		if (button == null)
		{
			UnityEngine.Object.Destroy(buttonObject);
			return null;
		}

		RectTransform rect = buttonObject.GetComponent<RectTransform>();
		RectTransform sourceRect = sourceButton.GetComponent<RectTransform>();
		if (rect != null && sourceRect != null)
		{
			rect.anchorMin = sourceRect.anchorMin;
			rect.anchorMax = sourceRect.anchorMax;
			rect.pivot = sourceRect.pivot;
			rect.sizeDelta = sourceRect.sizeDelta;
			float verticalOffset = Mathf.Max(sourceRect.rect.height + 16f, 72f);
			rect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -verticalOffset);
		}

		return button;
	}

	private string BuildSignatureText()
	{
		if (!string.IsNullOrWhiteSpace(currentFriend?.notes))
			return currentFriend.notes.Trim();
		return "以星罗为锚，为你指引前路。";
	}

	private string FormatOptional(string value, string fallback)
	{
		return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
	}

	private void ApplyAvatar(Sprite avatar)
	{
		if (avatar == null || uiComponent?.FriendAvatarImage == null) return;
		uiComponent.FriendAvatarImage.sprite = avatar;
		uiComponent.FriendAvatarImage.preserveAspect = true;
	}

	private void LoadRemoteAvatarIfNeeded()
	{
		if (currentFriend == null
			|| currentFriend.headSprite != null
			|| string.IsNullOrWhiteSpace(currentFriend.photoUrl)
			|| uiComponent == null)
		{
			return;
		}

		int requestId = requestVersion;
		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadSpriteFromUrlCoroutine(currentFriend.photoUrl, sprite =>
		{
			if (requestId != requestVersion || currentFriend == null || sprite == null) return;
			currentFriend.headSprite = sprite;
			ApplyAvatar(sprite);
		}));
	}

	private void LoadRecentOracleHistory()
	{
		int requestId = requestVersion;
		SetHistoryEntries(new List<FriendProfileHistoryEntry>
		{
			FriendProfileHistoryEntry.Empty("正在读取近期占卜记录...", "正在同步你的每日占卜历史。")
		});

		DailyOracleFirestore store = DailyOracleFirestore.Instance;
		if (store == null || !store.IsReady)
		{
			SetHistoryEntries(BuildFallbackHistoryEntries("每日占卜服务初始化中，稍后再试。"));
			return;
		}

		store.LoadRecent(10, records =>
		{
			if (requestId != requestVersion) return;

			List<FriendProfileHistoryEntry> entries = BuildHistoryEntries(records);
			if (entries.Count == 0)
				entries = BuildFallbackHistoryEntries("暂无占卜历史记录");
			SetHistoryEntries(entries);
		});
	}

	private List<FriendProfileHistoryEntry> BuildHistoryEntries(List<DailyOracleCloudRecord> records)
	{
		var entries = new List<FriendProfileHistoryEntry>();
		if (records == null) return entries;

		foreach (DailyOracleCloudRecord record in records)
		{
			if (record == null || !record.HasPayload) continue;
			string orientation = record.IsUpright ? "正位" : "逆位";
			string cardName = string.IsNullOrWhiteSpace(record.cardName) ? "每日牌" : record.cardName;
			entries.Add(new FriendProfileHistoryEntry
			{
				title = string.IsNullOrWhiteSpace(record.title) ? $"{cardName} · {orientation}" : record.title,
				content = string.IsNullOrWhiteSpace(record.oracle) ? $"{cardName} · {orientation}" : record.oracle,
				date = string.IsNullOrWhiteSpace(record.date) ? DateTime.Now.ToString("yyyy-MM-dd") : record.date,
				cardSprite = !string.IsNullOrWhiteSpace(record.cardId) ? TarotSpriteLoader.Load(record.cardId) : null,
				summary = null
			});
		}

		return entries;
	}

	private List<FriendProfileHistoryEntry> BuildFallbackHistoryEntries(string message)
	{
		DailyOracleService service = DailyOracleService.Instance;
		if (service != null && service.CachedPayload != null && service.CurrentCard != null)
		{
			return new List<FriendProfileHistoryEntry>
			{
				new FriendProfileHistoryEntry
				{
					title = string.IsNullOrWhiteSpace(service.CachedPayload.title)
						? service.CurrentCard.DisplayName(service.CurrentUpright)
						: service.CachedPayload.title,
					content = string.IsNullOrWhiteSpace(service.CachedPayload.oracle)
						? service.CurrentCard.DisplayName(service.CurrentUpright)
						: service.CachedPayload.oracle,
					date = DateTime.Now.ToString("yyyy-MM-dd"),
					cardSprite = TarotSpriteLoader.Load(service.CurrentCard.cardId),
					summary = null
				}
			};
		}

		return new List<FriendProfileHistoryEntry>
		{
			FriendProfileHistoryEntry.Empty(message, "完成每日占卜后，会在这里展示近期记录。")
		};
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

		SetText(item.oracleHistoryName, entry.title);
		SetText(item.oracleHistoryContent, entry.content);
		SetText(item.dateText, entry.date);
		if (item.historyImage != null)
		{
			item.historyImage.enabled = entry.cardSprite != null;
			if (entry.cardSprite != null)
				item.historyImage.sprite = entry.cardSprite;
		}

		if (item.oracleViewBtn != null)
		{
			item.oracleViewBtn.onClick.RemoveAllListeners();
			item.oracleViewBtn.interactable = false;
		}
	}

	private void LoadSyncSettingsThenRefresh()
	{
		FirestoreManager firestore = FirestoreManager.Instance;
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
		if (uiComponent?.SwitchSwitch == null) return;

		isRefreshingSyncSwitch = true;
		bool enabled = DailyDivinationSyncSettingsManager.Instance.Enabled
			&& DailyDivinationSyncSettingsManager.Instance.Visibility != DailyDivinationSyncVisibility.OnlyMe;
		if (uiComponent.SwitchSwitch.IsToggled() != enabled)
			uiComponent.SwitchSwitch.Toggle();
		isRefreshingSyncSwitch = false;
	}

	private void SaveSyncSettingFromProfile(bool enabled)
	{
		DailyDivinationSyncSettingsManager manager = DailyDivinationSyncSettingsManager.Instance;
		manager.SetEnabled(enabled, false);
		if (enabled && manager.Visibility == DailyDivinationSyncVisibility.OnlyMe)
			manager.SetVisibility(DailyDivinationSyncVisibility.RealFriends, false);
		manager.SaveLocal();

		DailyDivinationSyncSettings settings = manager.GetSettings();
		FirestoreManager firestore = FirestoreManager.Instance;
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

			DailyOracleFirestore store = DailyOracleFirestore.Instance;
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
		ToastManager.ShowToast(success ? "每日占卜动态同步已保存" : "设置已保存，今天还没有可同步的每日牌");
	}

	private void SetText(TMP_Text text, string value)
	{
		if (text != null)
			text.text = value ?? string.Empty;
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnMoreRecordsButtonClick()
	{
		DailyOracleFirestore store = DailyOracleFirestore.Instance;
		if (store == null || !store.IsReady)
		{
			FriendOracleHistoryOverlay.Show(transform, "占卜历史记录", historyEntries, "每日占卜服务初始化中，稍后再试。");
			return;
		}

		ToastManager.ShowToast("正在读取更多占卜记录");
		store.LoadRecent(30, records =>
		{
			List<FriendProfileHistoryEntry> entries = BuildHistoryEntries(records);
			FriendOracleHistoryOverlay.Show(transform, "占卜历史记录", entries, "暂无占卜历史记录");
		});
	}
	public void OnEditProfileButtonClick()
	{
		if (currentFriend == null) return;
		EditFriendUI.Show(currentFriend, updatedFriend =>
		{
			currentFriend = updatedFriend;
			RefreshProfile();
			RefreshRelationshipDivinationButton();
			LoadRecentOracleHistory();
		});
	}

	public void OnRelationshipDivinationButtonClick()
	{
		if (currentFriend == null)
		{
			ToastManager.ShowToast("好友资料不完整");
			return;
		}

		CreatedFriendRelationshipDivinationLocalFlow.TryStart(currentFriend);
	}

	public void OnSyncSwitchClick()
	{
		if (isRefreshingSyncSwitch || uiComponent?.SwitchSwitch == null) return;
		SaveSyncSettingFromProfile(uiComponent.SwitchSwitch.IsToggled());
	}
	#endregion
}
