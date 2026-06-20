/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/19/2026 6:16:25 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;

public class EditFriendUI : WindowBase
{
	private const int MaxNameLength = 20;
	private const string HistoryItemPrefabName = "HistoryCardItem";

	public EditFriendUIComponent uiComponent;
	private static FriendDataManager.FriendData sPendingFriend;
	private static Action<FriendDataManager.FriendData> sOnSaved;

	private FriendDataManager.FriendData currentFriend;
	private readonly List<FriendProfileHistoryEntry> historyEntries = new List<FriendProfileHistoryEntry>();
	private bool historyListInitialized;
	private bool isRefreshingSyncSwitch;
	private bool isRefreshingFields;
	private bool isSaving;
	private string editName = string.Empty;
	private string editSignature = string.Empty;
	private string editBirthday = string.Empty;
	private string editBirthTime = string.Empty;
	private string editCity = string.Empty;
	private Sprite selectedAvatarSprite;
	private Sprite defaultAvatarSprite;
	private string selectedAvatarImagePath = string.Empty;
	private int requestVersion;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<EditFriendUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("EditFriendUI 缺少 UI 组件绑定脚本：EditFriendUIComponent");
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
		if (currentFriend == null || !currentFriend.isVirtual)
		{
			ToastManager.ShowToast("只有自己创建的好友可以在这里编辑");
			HideWindow();
			return;
		}

		requestVersion++;
		PopulateFromCurrentFriend();
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
	public static void Show(FriendDataManager.FriendData friend, Action<FriendDataManager.FriendData> onSaved = null)
	{
		if (friend == null || !friend.isVirtual)
		{
			ToastManager.ShowToast("真实好友资料暂不在这里编辑");
			return;
		}

		sPendingFriend = friend;
		sOnSaved = onSaved;
		UIModule.Instance.PopUpWindow<EditFriendUI>();
	}

	private void PopulateFromCurrentFriend()
	{
		if (currentFriend == null || uiComponent == null) return;

		isRefreshingFields = true;
		editName = TrimName(currentFriend.name);
		editSignature = currentFriend.notes ?? string.Empty;
		editBirthday = currentFriend.birthday ?? string.Empty;
		editBirthTime = currentFriend.birthTime ?? string.Empty;
		editCity = currentFriend.city ?? string.Empty;
		selectedAvatarSprite = currentFriend.headSprite != null ? currentFriend.headSprite : defaultAvatarSprite;
		selectedAvatarImagePath = currentFriend.avatarImagePath ?? string.Empty;

		if (uiComponent.FriendNameInputField != null)
		{
			uiComponent.FriendNameInputField.characterLimit = MaxNameLength;
			uiComponent.FriendNameInputField.text = editName;
		}
		if (uiComponent.FriendSignatureInputField != null)
			uiComponent.FriendSignatureInputField.text = editSignature;
		SetText(uiComponent.BirthdayTextText, FormatOptional(editBirthday, "未填写"));
		SetText(uiComponent.BirthTimeTextText, FormatOptional(editBirthTime, "未填写"));
		SetText(uiComponent.BirthCityTextText, FormatOptional(editCity, "未填写"));
		ApplyAvatar(selectedAvatarSprite);
		LoadRemoteAvatarIfNeeded();
		SetLastSavedText("上次保存：刚刚");

		isRefreshingFields = false;
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
			selectedAvatarSprite = sprite;
			ApplyAvatar(sprite);
		}));
	}

	private void ApplySelectedAvatar(Sprite avatarSprite, int avatarIndex, string avatarImagePath)
	{
		if (avatarSprite == null) return;
		selectedAvatarSprite = avatarSprite;
		selectedAvatarImagePath = avatarImagePath ?? string.Empty;
		ApplyAvatar(selectedAvatarSprite);
	}

	private void ApplyBirthday(string value)
	{
		editBirthday = value ?? string.Empty;
		SetText(uiComponent.BirthdayTextText, FormatOptional(editBirthday, "未填写"));
	}

	private void ApplyBirthTime(string value)
	{
		editBirthTime = value ?? string.Empty;
		SetText(uiComponent.BirthTimeTextText, FormatOptional(editBirthTime, "未填写"));
	}

	private void ApplyCity(string value)
	{
		editCity = value ?? string.Empty;
		SetText(uiComponent.BirthCityTextText, FormatOptional(editCity, "未填写"));
	}

	private void SaveChanges()
	{
		if (isSaving || currentFriend == null) return;

		editName = TrimName(uiComponent.FriendNameInputField != null ? uiComponent.FriendNameInputField.text : editName);
		editSignature = uiComponent.FriendSignatureInputField != null ? uiComponent.FriendSignatureInputField.text.Trim() : editSignature.Trim();
		if (string.IsNullOrWhiteSpace(editName))
		{
			ToastManager.ShowToast("请先填写好友姓名");
			FocusInput(uiComponent.FriendNameInputField);
			return;
		}

		string previousAvatarPath = currentFriend.avatarImagePath ?? string.Empty;
		bool updated = FriendDataManager.Instance.UpdateVirtualFriend(
			currentFriend,
			editName,
			string.IsNullOrWhiteSpace(currentFriend.relationship) ? "好友" : currentFriend.relationship,
			editBirthday,
			editBirthTime,
			editCity,
			editSignature,
			selectedAvatarSprite,
			selectedAvatarImagePath);

		if (!updated)
		{
			ToastManager.ShowToast("保存失败，请返回好友列表重试");
			return;
		}

		isSaving = true;
		SetSaveInteractable(false);
		SetLastSavedText("已保存，正在同步云端...");
		Debug.Log($"[EditFriendUI] 保存虚拟好友资料: id={currentFriend.virtualFriendId}, name={currentFriend.name}");

		FinishLocalSaveAndClose("好友资料已保存");
		SyncVirtualFriendToCloud(previousAvatarPath);
	}

	private void SyncVirtualFriendToCloud(string previousAvatarPath)
	{
		FirestoreManager firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			ToastManager.ShowToast("已保存到本地，云端稍后同步");
			return;
		}

		if (ShouldUploadVirtualFriendAvatar(previousAvatarPath))
		{
			AvatarUploadManager.Instance.UploadVirtualFriendAvatarFromFile(
				currentFriend.virtualFriendId,
				currentFriend.avatarImagePath,
				result =>
				{
					FriendDataManager.Instance.SetVirtualFriendCloudAvatar(
						currentFriend,
						result.photoUrl,
						result.storagePath,
						result.previewSprite);
					SaveVirtualFriendDocument(firestore);
				},
				error =>
				{
					Debug.LogWarning("[EditFriendUI] 好友头像上传失败: " + error);
					SaveVirtualFriendDocument(firestore);
				});
			return;
		}

		SaveVirtualFriendDocument(firestore);
	}

	private bool ShouldUploadVirtualFriendAvatar(string previousAvatarPath)
	{
		return currentFriend != null
			&& AvatarUploadManager.Instance != null
			&& !string.IsNullOrWhiteSpace(currentFriend.avatarImagePath)
			&& File.Exists(currentFriend.avatarImagePath)
			&& (string.IsNullOrWhiteSpace(currentFriend.photoUrl)
				|| !string.Equals(previousAvatarPath, currentFriend.avatarImagePath, StringComparison.Ordinal));
	}

	private void SaveVirtualFriendDocument(FirestoreManager firestore)
	{
		firestore.SaveVirtualFriend(currentFriend, success =>
		{
			if (!success)
				ToastManager.ShowToast("本地已保存，云端同步失败");
		});
	}

	private void FinishLocalSaveAndClose(string toast)
	{
		isSaving = false;
		SetSaveInteractable(true);
		SetLastSavedText("上次保存：刚刚");
		ToastManager.ShowToast(toast);
		sOnSaved?.Invoke(currentFriend);
		PopulateFromCurrentFriend();
		HideWindow();
	}

	private void SetSaveInteractable(bool interactable)
	{
		if (uiComponent?.SaveChangesButton != null)
			uiComponent.SaveChangesButton.interactable = interactable;
	}

	private void SetLastSavedText(string text)
	{
		SetText(uiComponent.LastSavedTextText, text);
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

			if (settings.ShouldPublishToFeed)
				store.PublishTodaySummary(OnTodaySummarySyncUpdated);
			else
				store.DisableTodaySummary(OnTodaySummarySyncUpdated);
		});
	}

	private void OnTodaySummarySyncUpdated(bool success)
	{
		ToastManager.ShowToast(success ? "每日占卜动态同步已保存" : "设置已保存，今天还没有可同步的每日牌");
	}

	private string TrimName(string value)
	{
		if (string.IsNullOrEmpty(value)) return string.Empty;
		value = value.Trim();
		return value.Length <= MaxNameLength ? value : value.Substring(0, MaxNameLength);
	}

	private string FormatOptional(string value, string fallback)
	{
		return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
	}

	private void SetText(Text text, string value)
	{
		if (text != null)
			text.text = value ?? string.Empty;
	}

	private void FocusInput(InputField input)
	{
		if (input == null) return;
		input.Select();
		input.ActivateInputField();
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnEditAvatarButtonClick()
	{
		SelectFriendAvatarUI.Show(selectedAvatarSprite, 0, selectedAvatarImagePath, ApplySelectedAvatar);
	}
	public void OnFriendNameInputChange(string text)
	{
		if (isRefreshingFields) return;
		editName = TrimName(text);
		if (uiComponent.FriendNameInputField != null && uiComponent.FriendNameInputField.text != editName)
			uiComponent.FriendNameInputField.text = editName;
	}
	public void OnFriendNameInputEnd(string text)
	{
		if (isRefreshingFields) return;
		editName = TrimName(text);
	}
	public void OnFriendSignatureInputChange(string text)
	{
		if (isRefreshingFields) return;
		editSignature = text ?? string.Empty;
	}
	public void OnFriendSignatureInputEnd(string text)
	{
		if (isRefreshingFields) return;
		editSignature = text?.Trim() ?? string.Empty;
	}
	public void OnResetDefaultButtonClick()
	{
		PopulateFromCurrentFriend();
		ToastManager.ShowToast("已恢复到当前保存的资料");
	}
	public void OnBirthDateButtonClick()
	{
		SpinPickerUI.ShowDate(editBirthday, ApplyBirthday);
	}
	public void OnBirthTimeButtonClick()
	{
		SpinPickerUI.ShowTime(editBirthTime, ApplyBirthTime);
	}
	public void OnBirthCityButtonClick()
	{
		SpinPickerUI.ShowRegion(editCity, ApplyCity);
	}
	public void OnMoreRecordsButtonClick()
	{
		DailyOracleFirestore store = DailyOracleFirestore.Instance;
		if (store == null || !store.IsReady)
		{
			FriendOracleHistoryOverlay.Show(transform, "近期占卜记录", historyEntries, "每日占卜服务初始化中，稍后再试。");
			return;
		}

		ToastManager.ShowToast("正在读取更多占卜记录");
		store.LoadRecent(30, records =>
		{
			List<FriendProfileHistoryEntry> entries = BuildHistoryEntries(records);
			FriendOracleHistoryOverlay.Show(transform, "近期占卜记录", entries, "暂无占卜历史记录");
		});
	}
	public void OnSaveChangesButtonClick()
	{
		SaveChanges();
	}
	public void OnSyncSwitchClick()
	{
		if (isRefreshingSyncSwitch || uiComponent?.SwitchSwitch == null) return;
		SaveSyncSettingFromProfile(uiComponent.SwitchSwitch.IsToggled());
	}
	#endregion
}
