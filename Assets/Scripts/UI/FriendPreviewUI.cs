/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/26/2026 11:34:51 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using DG.Tweening;

public class FriendPreviewUI : WindowBase
{
	private const int PreviewHistoryLimit = 3;
	private const int FullHistoryLimit = 20;
	private const int HistoryDaysBack = 30;
	private const float SettingPanelTweenDuration = 0.22f;
	private const float SettingPanelHiddenPadding = 80f;
	private const string PrivateValueText = "用户未公开";
	private const string MissingLocalValueText = "未填写";
	private const string LocalRelationshipReadingIdPrefix = "rel_local_";
	private static readonly Color DisabledEditFriendTextColor = new Color(0.55f, 0.55f, 0.55f, 1f);

	public FriendPreviewUIComponent uiComponent;

	private static FriendDataManager.FriendData sPendingFriend;
	private static FirestoreManager.UserSearchResult sPendingUser;
	private static bool sPendingFromAddFlow;

	private FriendDataManager.FriendData currentFriend;
	private FirestoreManager.UserSearchResult currentUser;
	private bool currentFromAddFlow;
	private bool isSendingRequest;
	private int requestVersion;
	private int fullHistoryRequestVersion;
	private Sprite defaultAvatarSprite;
	private Transform basicInfoPanelRoot;
	private Transform historyPanelRoot;
	private RectTransform settingBtnContainerRect;
	private RectTransform settingEditButtonRect;
	private RectTransform settingDeleteButtonRect;
	private TMP_Text settingEditButtonLabel;
	private Vector2 settingBtnContainerShownPosition;
	private Vector2 settingBtnContainerHiddenPosition;
	private Vector2 settingDeleteButtonShownPosition;
	private bool settingPanelPositionsInitialized;
	private bool settingPanelVisible;
	private bool isDeletingFriend;
	private bool hasSettingEditButtonOriginalTextColor;
	private Color settingEditButtonOriginalTextColor;
	private Coroutine rebuildHistoryLayoutCoroutine;
	private readonly HashSet<string> pendingSentUserIds = new HashSet<string>();
	private readonly List<FriendPreviewHistoryEntry> historyEntries = new List<FriendPreviewHistoryEntry>();

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<FriendPreviewUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("FriendPreviewUI 缺少 UI 组件绑定脚本：FriendPreviewUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		defaultAvatarSprite = FriendAvatarImageUtility.ResolveAvatar(uiComponent.AvatarImage != null ? uiComponent.AvatarImage.sprite : null);
		InitializeSettingPanel();
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		currentFriend = sPendingFriend;
		currentUser = sPendingUser;
		currentFromAddFlow = sPendingFromAddFlow;
		isSendingRequest = false;
		isDeletingFriend = false;
		requestVersion++;

		RefreshSettingControls();
		HideSettingPanel(true);
		RefreshProfile(true);
		RefreshAddButton();
		RefreshInviteButton();
		LoadPendingSentRequestsForButton();
		LoadPublicProfileThenRefresh();
		LoadPreviewHistory();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		requestVersion++;
		fullHistoryRequestVersion++;
		isSendingRequest = false;
		isDeletingFriend = false;
		StopRebuildHistoryLayoutCoroutine();
		HideSettingPanel(true);
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		StopRebuildHistoryLayoutCoroutine();
		KillSettingTween();
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public static void Show(FirestoreManager.UserSearchResult user)
	{
		if (user == null || string.IsNullOrWhiteSpace(user.uid))
		{
			ToastManager.ShowToast("用户资料不完整");
			return;
		}

		sPendingFriend = null;
		sPendingUser = user;
		sPendingFromAddFlow = true;
		UIModule.Instance.PopUpWindow<FriendPreviewUI>();
	}

	public static void Show(FriendDataManager.FriendData friend)
	{
		if (friend == null)
		{
			ToastManager.ShowToast("好友资料不完整");
			return;
		}

		sPendingFriend = friend;
		sPendingUser = null;
		sPendingFromAddFlow = false;
		UIModule.Instance.PopUpWindow<FriendPreviewUI>();
	}

	private void RefreshProfile(bool refreshAvatar)
	{
		if (uiComponent == null) return;

		if (uiComponent.FriendNameTextMeshProUGUI != null)
			uiComponent.FriendNameTextMeshProUGUI.text = GetDisplayName();
		if (uiComponent.infoNameTextMeshProUGUI != null)
			uiComponent.infoNameTextMeshProUGUI.text = GetSubtitleText();

		RefreshBasicInfo();
		if (refreshAvatar)
			RefreshAvatar(requestVersion);
	}

	private void RefreshAvatar(int requestId)
	{
		if (uiComponent?.AvatarImage == null) return;

		string uid = CurrentUid();
		string photoUrl = CurrentPhotoUrl();
		string token = $"{uid}|{photoUrl}";
		FriendAvatarImageUtility.SetAvatarTargetToken(uiComponent.AvatarImage, token);

		if (currentFriend != null)
		{
			FriendAvatarImageUtility.ApplyAvatar(
				uiComponent.AvatarImage,
				FriendAvatarImageUtility.ResolveFriendAvatar(currentFriend, uiComponent.AvatarImage, defaultAvatarSprite),
				defaultAvatarSprite);
		}
		else
		{
			FriendAvatarImageUtility.ApplyAvatar(uiComponent.AvatarImage, null, defaultAvatarSprite);
		}

		if (string.IsNullOrWhiteSpace(photoUrl))
			return;

		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadSpriteFromUrlCoroutine(photoUrl, sprite =>
		{
			if (requestId != requestVersion || sprite == null || uiComponent?.AvatarImage == null)
				return;
			if (!FriendAvatarImageUtility.IsAvatarTargetTokenValid(uiComponent.AvatarImage, token))
				return;

			if (currentFriend != null)
				currentFriend.headSprite = sprite;
			FriendAvatarImageUtility.ApplyAvatar(uiComponent.AvatarImage, sprite, defaultAvatarSprite);
		}));
	}

	private void LoadPublicProfileThenRefresh()
	{
		string uid = CurrentUid();
		if (string.IsNullOrWhiteSpace(uid))
			return;

		FirestoreManager firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
			return;

		int requestId = requestVersion;
		firestore.LoadPublicProfile(uid, profile =>
		{
			if (requestId != requestVersion || profile == null)
				return;

			ApplyPublicProfile(profile);
			RefreshProfile(true);
			RefreshInviteButton();
		});
	}

	private void ApplyPublicProfile(FirestoreManager.UserSearchResult profile)
	{
		if (profile == null) return;

		if (currentUser != null)
		{
			currentUser.displayName = FirstNonEmpty(profile.displayName, currentUser.displayName);
			currentUser.email = FirstNonEmpty(profile.email, currentUser.email);
			currentUser.photoUrl = FirstNonEmpty(profile.photoUrl, currentUser.photoUrl);
			currentUser.birthday = FirstNonEmpty(profile.birthday, currentUser.birthday);
			currentUser.birthTime = FirstNonEmpty(profile.birthTime, currentUser.birthTime);
			currentUser.city = FirstNonEmpty(profile.city, currentUser.city);
		}

		if (currentFriend != null)
		{
			currentFriend.name = FirstNonEmpty(profile.displayName, currentFriend.name);
			currentFriend.handle = FirstNonEmpty(profile.Handle, currentFriend.handle);
			currentFriend.photoUrl = FirstNonEmpty(profile.photoUrl, currentFriend.photoUrl);
			currentFriend.birthday = FirstNonEmpty(profile.birthday, currentFriend.birthday);
			currentFriend.birthTime = FirstNonEmpty(profile.birthTime, currentFriend.birthTime);
			currentFriend.city = FirstNonEmpty(profile.city, currentFriend.city);
		}
	}

	private void RefreshBasicInfo()
	{
		SetBasicInfoField("BirthdayDay", uiComponent.birthdayTextTextMeshProUGUI, FormatBirthDate(CurrentBirthday()));
		SetBasicInfoField("BirthdayTime", uiComponent.birthdayTimeTextTextMeshProUGUI, CurrentBirthTime());
		SetBasicInfoField("BirthdayCity", uiComponent.birthdayPlaceTextTextMeshProUGUI, CurrentCity());
		SetBasicInfoPanelVisible(true);
	}

	private void SetBasicInfoField(string rowName, TMP_Text text, string value)
	{
		bool hasValue = !string.IsNullOrWhiteSpace(value);
		if (text != null)
			text.text = hasValue ? value.Trim() : GetMissingBasicInfoText();

		Transform row = FindTransformByName(transform, rowName);
		if (row != null)
			row.gameObject.SetActive(true);
		else if (text != null && text.transform.parent != null)
			text.transform.parent.gameObject.SetActive(true);
	}

	private void SetBasicInfoPanelVisible(bool visible)
	{
		if (basicInfoPanelRoot == null)
			basicInfoPanelRoot = FindTransformByName(transform, "BasicInfoPanel");
		if (basicInfoPanelRoot != null)
			basicInfoPanelRoot.gameObject.SetActive(visible);
	}

	private void LoadPreviewHistory()
	{
		if (IsVirtualFriendPreview())
		{
			LoadVirtualFriendHistory();
			return;
		}

		if (!CanLoadFriendHistory())
		{
			SetHistoryPlaceholder(
				PrivateValueText,
				"对方暂未向你公开占卜历史。",
				"占卜历史",
				string.Empty);
			return;
		}

		SetHistoryPlaceholder(
			"正在加载",
			"正在同步对方公开的占卜摘要...",
			"占卜历史",
			string.Empty);
		int requestId = requestVersion;
		LoadHistoryEntries(PreviewHistoryLimit, PreviewHistoryLimit, entries =>
		{
			if (requestId != requestVersion) return;
			if (entries != null && entries.Count > 0)
			{
				SetHistoryEntries(entries);
				return;
			}

			SetHistoryPlaceholder(
				"暂无可见占卜记录",
				"对方最近还没有公开的占卜摘要。",
				"占卜历史",
				string.Empty);
		});
	}

	private void LoadVirtualFriendHistory()
	{
		SetHistoryPlaceholder(
			"正在加载",
			"正在整理这个好友的本地占卜记录...",
			"占卜历史",
			string.Empty);

		int requestId = requestVersion;
		DivinationRecordFirestore historyStore = RelationshipDivinationFlow.GetOrCreateHistoryService();
		if (historyStore == null)
		{
			SetVirtualFriendEmptyHistoryPlaceholder();
			return;
		}

		historyStore.LoadAllRecords(records =>
		{
			if (requestId != requestVersion) return;

			List<FriendPreviewHistoryEntry> entries = BuildVirtualFriendHistoryEntries(records, PreviewHistoryLimit);
			if (entries.Count > 0)
			{
				SetHistoryEntries(entries);
				return;
			}

			SetVirtualFriendEmptyHistoryPlaceholder();
		});
	}

	private void SetVirtualFriendEmptyHistoryPlaceholder()
	{
		SetHistoryPlaceholder(
			"暂无占卜记录",
			"还没有和这个好友完成占卜记录。",
			"占卜历史",
			string.Empty);
	}

	private void OpenFullHistoryWindow()
	{
		int historyRequestId = ++fullHistoryRequestVersion;
		string loadingDescription = IsVirtualFriendPreview()
			? "正在整理这个好友的本地占卜记录..."
			: "正在同步对方公开的占卜摘要...";

		AllDivinationHistoryUI historyWindow = AllDivinationHistoryUI.ShowLoading(loadingDescription, GetDisplayName());
		if (historyWindow == null)
			return;

		if (IsVirtualFriendPreview())
		{
			LoadVirtualFriendFullHistory(historyWindow, historyRequestId);
			return;
		}

		if (!CanLoadFriendHistory())
		{
			historyWindow.SetEntries(
				new List<AllDivinationHistoryEntry>
				{
					AllDivinationHistoryEntry.Placeholder(
						PrivateValueText,
						"对方暂未向你公开占卜历史。",
						"占卜历史",
						string.Empty)
				},
				PrivateValueText,
				"对方暂未向你公开占卜历史。");
			return;
		}

		LoadHistoryEntries(FullHistoryLimit, FullHistoryLimit, entries =>
		{
			if (historyRequestId != fullHistoryRequestVersion)
				return;

			if (entries != null && entries.Count > 0)
			{
				historyWindow.SetEntries(
					ToAllDivinationHistoryEntries(entries),
					"暂无可见占卜记录",
					"对方最近还没有公开的占卜摘要。");
				return;
			}

			historyWindow.SetEntries(
				null,
				"暂无可见占卜记录",
				"对方最近还没有公开的占卜摘要。");
		});
	}

	private void LoadVirtualFriendFullHistory(AllDivinationHistoryUI historyWindow, int historyRequestId)
	{
		DivinationRecordFirestore historyStore = RelationshipDivinationFlow.GetOrCreateHistoryService();
		if (historyStore == null)
		{
			historyWindow.SetEntries(
				null,
				"暂无占卜记录",
				"还没有和这个好友完成占卜记录。");
			return;
		}

		historyStore.LoadAllRecords(records =>
		{
			if (historyRequestId != fullHistoryRequestVersion)
				return;

			List<FriendPreviewHistoryEntry> entries = BuildVirtualFriendHistoryEntries(records, FullHistoryLimit);
			if (entries.Count > 0)
			{
				historyWindow.SetEntries(
					ToAllDivinationHistoryEntries(entries),
					"暂无占卜记录",
					"还没有和这个好友完成占卜记录。");
				return;
			}

			historyWindow.SetEntries(
				null,
				"暂无占卜记录",
				"还没有和这个好友完成占卜记录。");
		});
	}

	private void LoadHistoryEntries(int dailyLimit, int finalLimit, Action<List<FriendPreviewHistoryEntry>> onComplete)
	{
		List<FriendPreviewHistoryEntry> entries = new List<FriendPreviewHistoryEntry>();
		string friendUid = CurrentUid();
		int pending = 0;

		void CompleteOne()
		{
			pending--;
			if (pending > 0)
				return;

			onComplete?.Invoke(SortAndLimitEntries(entries, finalLimit));
		}

		DailyOracleFirestore dailyStore = DailyOracleFirestore.Instance;
		if (dailyStore != null && dailyStore.IsReady)
		{
			pending++;
			dailyStore.LoadRecentFriendSummaries(friendUid, HistoryDaysBack, dailyLimit, summaries =>
			{
				entries.AddRange(BuildDailyHistoryEntries(summaries, dailyLimit));
				CompleteOne();
			});
		}

		RelationshipDivinationFirestore relationshipStore = RelationshipDivinationFlow.GetOrCreateService();
		if (relationshipStore != null && relationshipStore.IsReady)
		{
			pending++;
			relationshipStore.LoadCompletedTodayWithFriend(friendUid, (record, succeeded) =>
			{
				if (succeeded && record != null && record.IsCompleted)
					entries.Add(FriendPreviewHistoryEntry.FromRelationshipRecord(record, GetCurrentUid()));
				CompleteOne();
			});
		}

		if (pending == 0)
			onComplete?.Invoke(entries);
	}

	private List<FriendPreviewHistoryEntry> BuildDailyHistoryEntries(List<DailyOracleSummaryRecord> summaries, int limit)
	{
		List<FriendPreviewHistoryEntry> entries = new List<FriendPreviewHistoryEntry>();
		if (summaries == null) return entries;

		int safeLimit = Mathf.Max(1, limit);
		foreach (DailyOracleSummaryRecord summary in summaries)
		{
			if (summary == null || !summary.IsVisibleInFriendFeed)
				continue;

			entries.Add(FriendPreviewHistoryEntry.FromSummary(summary));
			if (entries.Count >= safeLimit)
				break;
		}

		return entries;
	}

	private List<FriendPreviewHistoryEntry> SortAndLimitEntries(List<FriendPreviewHistoryEntry> entries, int limit)
	{
		if (entries == null) return new List<FriendPreviewHistoryEntry>();

		entries.Sort((left, right) => right.SortTime.CompareTo(left.SortTime));
		int safeLimit = Mathf.Max(1, limit);
		if (entries.Count > safeLimit)
			entries.RemoveRange(safeLimit, entries.Count - safeLimit);
		return entries;
	}

	private List<FriendPreviewHistoryEntry> BuildVirtualFriendHistoryEntries(List<DivinationRecordData> records, int limit)
	{
		List<FriendPreviewHistoryEntry> entries = new List<FriendPreviewHistoryEntry>();
		if (records == null) return entries;

		int safeLimit = Mathf.Max(1, limit);
		foreach (DivinationRecordData record in records)
		{
			if (!IsCurrentVirtualFriendHistoryRecord(record))
				continue;

			entries.Add(FriendPreviewHistoryEntry.FromDivinationRecord(record));
			if (entries.Count >= safeLimit)
				break;
		}

		return SortAndLimitEntries(entries, safeLimit);
	}

	private bool IsCurrentVirtualFriendHistoryRecord(DivinationRecordData record)
	{
		if (record == null || !IsVirtualFriendPreview())
			return false;
		if (!string.Equals(record.scene, "friend_relationship_divination", StringComparison.Ordinal)
			&& !string.Equals(record.spreadKind, "relationship_tension", StringComparison.Ordinal))
			return false;
		if (string.IsNullOrWhiteSpace(record.readingId)
			|| !record.readingId.StartsWith(LocalRelationshipReadingIdPrefix, StringComparison.Ordinal))
			return false;

		return RecordMentionsCurrentFriend(record);
	}

	private bool RecordMentionsCurrentFriend(DivinationRecordData record)
	{
		if (record == null || currentFriend == null)
			return false;

		string haystack = string.Join("\n", new[]
		{
			record.question,
			record.shortVerdict,
			record.judgeContent,
			record.adviceContent
		});
		if (string.IsNullOrWhiteSpace(haystack))
			return false;

		string[] tokens =
		{
			currentFriend.name,
			currentFriend.handle
		};
		foreach (string token in tokens)
		{
			if (string.IsNullOrWhiteSpace(token))
				continue;
			if (haystack.IndexOf(token.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
				return true;
		}

		return false;
	}

	private void SetHistoryEntries(List<FriendPreviewHistoryEntry> entries)
	{
		historyEntries.Clear();
		if (entries != null)
			historyEntries.AddRange(entries);
		RefreshHistoryItems();
	}

	private void SetHistoryPlaceholder(string title, string description, string source, string time)
	{
		historyEntries.Clear();
		historyEntries.Add(FriendPreviewHistoryEntry.Placeholder(title, description, source, time));
		RefreshHistoryItems();
	}

	private void RefreshHistoryItems()
	{
		int visibleCount = 0;
		if (uiComponent?.divinationItems != null)
		{
			int count = Mathf.Min(uiComponent.divinationItems.Count, Mathf.Min(historyEntries.Count, PreviewHistoryLimit));
			for (int i = 0; i < uiComponent.divinationItems.Count; i++)
			{
				DivinationItem item = uiComponent.divinationItems[i];
				if (item == null) continue;

				bool hasEntry = i < count;
				item.gameObject.SetActive(hasEntry);
				if (hasEntry)
				{
					FriendPreviewHistoryEntry entry = historyEntries[i];
					item.SetData(entry.cardSprites, entry.cardNames, entry.description, entry.source, entry.time);
					visibleCount++;
				}
				else
				{
					item.Clear();
				}
			}
		}

		SetHistoryPanelVisible(true);
		RequestRebuildHistoryLayout();
	}

	private void SetHistoryPanelVisible(bool visible)
	{
		if (historyPanelRoot == null)
			historyPanelRoot = FindTransformByName(transform, "DivinationHistoryPanel");
		if (historyPanelRoot != null)
			historyPanelRoot.gameObject.SetActive(visible);
	}

	private void RequestRebuildHistoryLayout()
	{
		ForceRebuildHistoryLayout();
		if (uiComponent == null || !gameObject.activeInHierarchy)
			return;

		StopRebuildHistoryLayoutCoroutine();
		rebuildHistoryLayoutCoroutine = uiComponent.StartCoroutine(RebuildHistoryLayoutNextFrame());
	}

	private void StopRebuildHistoryLayoutCoroutine()
	{
		if (rebuildHistoryLayoutCoroutine == null || uiComponent == null)
			return;

		uiComponent.StopCoroutine(rebuildHistoryLayoutCoroutine);
		rebuildHistoryLayoutCoroutine = null;
	}

	private IEnumerator RebuildHistoryLayoutNextFrame()
	{
		yield return null;
		ForceRebuildHistoryLayout();
		rebuildHistoryLayoutCoroutine = null;
	}

	private void ForceRebuildHistoryLayout()
	{
		Canvas.ForceUpdateCanvases();

		if (uiComponent?.divinationItems != null)
		{
			foreach (DivinationItem item in uiComponent.divinationItems)
			{
				if (item == null || !item.gameObject.activeSelf)
					continue;
				ForceRebuildRect(item.transform as RectTransform);
			}
		}

		if (historyPanelRoot == null)
			historyPanelRoot = FindTransformByName(transform, "DivinationHistoryPanel");
		ForceRebuildRect(historyPanelRoot as RectTransform);

		Transform cursor = historyPanelRoot != null ? historyPanelRoot.parent : null;
		while (cursor != null)
		{
			ForceRebuildRect(cursor as RectTransform);
			cursor = cursor.parent;
		}
	}

	private void ForceRebuildRect(RectTransform rect)
	{
		if (rect != null)
			LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
	}

	private void InitializeSettingPanel()
	{
		if (uiComponent == null || settingPanelPositionsInitialized)
			return;

		settingBtnContainerRect = uiComponent.BtnContainer as RectTransform;
		settingEditButtonRect = uiComponent.editFriendBtn != null ? uiComponent.editFriendBtn.transform as RectTransform : null;
		settingDeleteButtonRect = uiComponent.deleteBtn != null ? uiComponent.deleteBtn.transform as RectTransform : null;

		if (settingBtnContainerRect != null)
		{
			settingBtnContainerShownPosition = settingBtnContainerRect.anchoredPosition;
			float panelHeight = Mathf.Max(settingBtnContainerRect.rect.height, settingBtnContainerRect.sizeDelta.y, Screen.height * 0.35f);
			settingBtnContainerHiddenPosition = settingBtnContainerShownPosition - new Vector2(0f, panelHeight + SettingPanelHiddenPadding);
		}

		if (settingDeleteButtonRect != null)
			settingDeleteButtonShownPosition = settingDeleteButtonRect.anchoredPosition;

		settingPanelPositionsInitialized = true;
		HideSettingPanel(true);
	}

	private void RefreshSettingControls()
	{
		bool canOpenSettings = CanOpenSettingPanel();
		if (uiComponent?.settingButton != null)
			uiComponent.settingButton.gameObject.SetActive(canOpenSettings);

		ConfigureSettingButtons();
		if (!canOpenSettings)
			HideSettingPanel(true);
	}

	private bool CanOpenSettingPanel()
	{
		return !currentFromAddFlow && currentFriend != null;
	}

	private bool CanEditFriend()
	{
		return currentFriend != null && currentFriend.isVirtual && !currentFromAddFlow;
	}

	private bool IsVirtualFriendPreview()
	{
		return currentFriend != null && currentFriend.isVirtual && !currentFromAddFlow;
	}

	private bool CanDeleteFriend()
	{
		return currentFriend != null && !currentFromAddFlow;
	}

	private void ConfigureSettingButtons()
	{
		bool showEdit = CanOpenSettingPanel();
		bool showDelete = CanDeleteFriend();
		bool canEdit = CanEditFriend();

		if (uiComponent?.editFriendBtn != null)
		{
			uiComponent.editFriendBtn.gameObject.SetActive(showEdit);
			uiComponent.editFriendBtn.interactable = showEdit;
			SetEditFriendButtonVisual(canEdit);
		}
		if (uiComponent?.deleteBtn != null)
			uiComponent.deleteBtn.gameObject.SetActive(showDelete);

		if (settingDeleteButtonRect != null)
			settingDeleteButtonRect.anchoredPosition = !showEdit && settingEditButtonRect != null
				? settingEditButtonRect.anchoredPosition
				: settingDeleteButtonShownPosition;
	}

	private void SetEditFriendButtonVisual(bool canEdit)
	{
		if (uiComponent?.editFriendBtn == null)
			return;

		if (settingEditButtonLabel == null)
			settingEditButtonLabel = uiComponent.editFriendBtn.GetComponentInChildren<TMP_Text>(true);
		if (settingEditButtonLabel == null)
			return;

		if (!hasSettingEditButtonOriginalTextColor)
		{
			settingEditButtonOriginalTextColor = settingEditButtonLabel.color;
			hasSettingEditButtonOriginalTextColor = true;
		}

		settingEditButtonLabel.text = "编辑好友";
		settingEditButtonLabel.color = canEdit ? settingEditButtonOriginalTextColor : DisabledEditFriendTextColor;
	}

	private void ShowSettingPanel()
	{
		if (!CanOpenSettingPanel())
			return;

		InitializeSettingPanel();
		ConfigureSettingButtons();
		if (uiComponent?.friendSettingPanel == null)
			return;

		settingPanelVisible = true;
		uiComponent.friendSettingPanel.gameObject.SetActive(true);
		uiComponent.friendSettingPanel.SetAsLastSibling();

		if (settingBtnContainerRect == null)
			return;

		KillSettingTween();
		settingBtnContainerRect.anchoredPosition = settingBtnContainerHiddenPosition;
		settingBtnContainerRect
			.DOAnchorPos(settingBtnContainerShownPosition, SettingPanelTweenDuration)
			.SetEase(Ease.OutCubic)
			.SetUpdate(true);
	}

	private void HideSettingPanel(bool immediate)
	{
		InitializeSettingPanel();
		settingPanelVisible = false;

		if (uiComponent?.friendSettingPanel == null)
			return;

		if (settingBtnContainerRect == null || immediate || !uiComponent.friendSettingPanel.gameObject.activeSelf)
		{
			KillSettingTween();
			if (settingBtnContainerRect != null)
				settingBtnContainerRect.anchoredPosition = settingBtnContainerHiddenPosition;
			uiComponent.friendSettingPanel.gameObject.SetActive(false);
			return;
		}

		KillSettingTween();
		settingBtnContainerRect
			.DOAnchorPos(settingBtnContainerHiddenPosition, SettingPanelTweenDuration)
			.SetEase(Ease.InCubic)
			.SetUpdate(true)
			.OnComplete(() =>
			{
				if (!settingPanelVisible && uiComponent?.friendSettingPanel != null)
					uiComponent.friendSettingPanel.gameObject.SetActive(false);
			});
	}

	private void KillSettingTween()
	{
		if (settingBtnContainerRect != null)
			settingBtnContainerRect.DOKill();
	}

	private void LoadPendingSentRequestsForButton()
	{
		if (!currentFromAddFlow || FirestoreManager.Instance == null)
			return;

		int requestId = requestVersion;
		FirestoreManager.Instance.LoadPendingSentFriendUids(uids =>
		{
			if (requestId != requestVersion)
				return;

			pendingSentUserIds.Clear();
			if (uids != null)
			{
				foreach (string uid in uids)
				{
					if (!string.IsNullOrWhiteSpace(uid))
						pendingSentUserIds.Add(uid);
				}
			}

			RefreshAddButton();
		});
	}

	private void RefreshAddButton()
	{
		if (uiComponent?.AddButton == null) return;

		uiComponent.AddButton.gameObject.SetActive(currentFromAddFlow);
		if (!currentFromAddFlow)
			return;

		string uid = CurrentUid();
		bool hasUid = !string.IsNullOrWhiteSpace(uid);
		bool isSelf = IsCurrentUserSelf(uid);
		bool alreadyFriend = IsAcceptedFriend(uid);
		bool alreadyRequested = hasUid && pendingSentUserIds.Contains(uid);
		bool blocked = IsBlockedUser(uid);
		bool canAdd = hasUid && !isSelf && !alreadyFriend && !alreadyRequested && !blocked && !isSendingRequest;

		uiComponent.AddButton.interactable = canAdd;
		if (uiComponent.AddLabelText != null)
		{
			uiComponent.AddLabelText.text = isSendingRequest ? "发送中"
				: !hasUid ? "不可添加"
				: isSelf ? "自己"
				: alreadyFriend ? "已添加"
				: blocked ? "已屏蔽"
				: alreadyRequested ? "已发送"
				: "添加";
		}
	}

	private void RefreshInviteButton()
	{
		if (uiComponent?.inviteBtn == null)
			return;

		FriendDataManager.FriendData targetFriend = GetRelationshipTargetFriend();
		bool canShow = targetFriend != null;
		bool canUse = canShow && RelationshipDivinationFlow.CanUseTwoPersonDivination(targetFriend, false);

		uiComponent.inviteBtn.gameObject.SetActive(canShow);
		uiComponent.inviteBtn.interactable = canUse && !isDeletingFriend;

		TMP_Text label = uiComponent.inviteBtn.GetComponentInChildren<TMP_Text>(true);
		if (label != null)
		{
			label.text = !canUse
				? "暂不可占卜"
				: targetFriend.isVirtual
					? "发起关系占卜"
					: "邀请一起占卜";
		}
	}

	private bool CanSubmitAddRequest()
	{
		string uid = CurrentUid();
		return !string.IsNullOrWhiteSpace(uid)
			&& !IsCurrentUserSelf(uid)
			&& !IsAcceptedFriend(uid)
			&& !pendingSentUserIds.Contains(uid)
			&& !IsBlockedUser(uid)
			&& !isSendingRequest;
	}

	private FriendDataManager.FriendData GetRelationshipTargetFriend()
	{
		if (currentFriend != null)
			return currentFriend;

		string uid = CurrentUid();
		if (string.IsNullOrWhiteSpace(uid) || FriendDataManager.Instance == null)
			return null;

		return FriendDataManager.Instance.FindRealFriendByFirebaseUid(uid);
	}

	private bool CanLoadFriendHistory()
	{
		return IsAcceptedFriend(CurrentUid());
	}

	private string GetMissingBasicInfoText()
	{
		return IsVirtualFriendPreview() ? MissingLocalValueText : PrivateValueText;
	}

	private bool IsAcceptedFriend(string uid)
	{
		return !string.IsNullOrWhiteSpace(uid)
			&& FriendDataManager.Instance != null
			&& FriendDataManager.Instance.FindRealFriendByFirebaseUid(uid) != null;
	}

	private bool IsBlockedUser(string uid)
	{
		return !string.IsNullOrWhiteSpace(uid)
			&& FriendDataManager.Instance != null
			&& FriendDataManager.Instance.IsUserBlocked(uid);
	}

	private bool IsCurrentUserSelf(string uid)
	{
		return !string.IsNullOrWhiteSpace(uid)
			&& UserDataManager.Instance != null
			&& string.Equals(UserDataManager.Instance.FirebaseUid, uid, StringComparison.Ordinal);
	}

	private string GetCurrentUid()
	{
		return UserDataManager.Instance != null ? UserDataManager.Instance.FirebaseUid : string.Empty;
	}

	private FirestoreManager.UserSearchResult BuildAddTargetUser()
	{
		if (currentUser != null)
			return currentUser;

		return new FirestoreManager.UserSearchResult
		{
			uid = CurrentUid(),
			displayName = GetDisplayName(),
			email = string.Empty,
			photoUrl = CurrentPhotoUrl(),
			birthday = CurrentBirthday(),
			birthTime = CurrentBirthTime(),
			city = CurrentCity(),
			isSelf = false
		};
	}

	private string CurrentUid()
	{
		if (currentFromAddFlow && currentUser != null)
			return currentUser.uid;
		return currentFriend != null ? currentFriend.firebaseUid : string.Empty;
	}

	private string GetDisplayName()
	{
		if (currentFromAddFlow && currentUser != null)
			return FirstNonEmpty(currentUser.displayName, currentUser.Handle, "好友");
		return FirstNonEmpty(currentFriend?.name, currentFriend?.handle, "好友");
	}

	private string GetSubtitleText()
	{
		if (currentFromAddFlow && currentUser != null)
			return FirstNonEmpty(currentUser.Handle, currentUser.Info, currentUser.uid);
		return FirstNonEmpty(currentFriend?.handle, currentFriend?.info, currentFriend?.relationship, "真实好友");
	}

	private string CurrentPhotoUrl()
	{
		if (currentFromAddFlow && currentUser != null)
			return currentUser.photoUrl;
		return currentFriend != null ? currentFriend.photoUrl : string.Empty;
	}

	private string CurrentBirthday()
	{
		if (currentFromAddFlow && currentUser != null)
			return currentUser.birthday;
		return currentFriend != null ? currentFriend.birthday : string.Empty;
	}

	private string CurrentBirthTime()
	{
		if (currentFromAddFlow && currentUser != null)
			return currentUser.birthTime;
		return currentFriend != null ? currentFriend.birthTime : string.Empty;
	}

	private string CurrentCity()
	{
		if (currentFromAddFlow && currentUser != null)
			return currentUser.city;
		return currentFriend != null ? currentFriend.city : string.Empty;
	}

	private string FormatBirthDate(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return string.Empty;
		if (TryParseDate(value, out DateTime date))
			return date.ToString("yyyy.MM.dd");
		return value.Trim();
	}

	private static string FormatHistoryDate(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return string.Empty;
		if (DateTime.TryParse(value.Trim(), out DateTime parsed))
			return $"{parsed.Year}.{parsed.Month}.{parsed.Day}";
		if (TryParseDate(value, out DateTime date))
			return $"{date.Year}.{date.Month}.{date.Day}";
		return value.Trim();
	}

	private static DateTime ParseSortTime(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return DateTime.MinValue;
		if (DateTime.TryParse(value.Trim(), out DateTime parsed))
			return parsed;
		if (TryParseDate(value, out DateTime date))
			return date;
		return DateTime.MinValue;
	}

	private static bool TryParseDate(string value, out DateTime date)
	{
		string[] formats =
		{
			"yyyy-MM-dd",
			"yyyy-M-d",
			"yyyy.MM.dd",
			"yyyy.M.d",
			"yyyy/MM/dd",
			"yyyy/M/d"
		};

		return DateTime.TryParseExact(
			value.Trim(),
			formats,
			CultureInfo.InvariantCulture,
			DateTimeStyles.None,
			out date);
	}

	private static string FirstNonEmpty(params string[] values)
	{
		if (values == null) return string.Empty;
		foreach (string value in values)
		{
			if (!string.IsNullOrWhiteSpace(value))
				return value.Trim();
		}

		return string.Empty;
	}

	private Transform FindTransformByName(Transform root, params string[] names)
	{
		if (root == null || names == null) return null;
		for (int i = 0; i < names.Length; i++)
		{
			if (root.name == names[i])
				return root;
		}

		for (int i = 0; i < root.childCount; i++)
		{
			Transform result = FindTransformByName(root.GetChild(i), names);
			if (result != null) return result;
		}

		return null;
	}

	#endregion

	#region UI组件事件
	public void OnbackButtonClick()
	{
		HideWindow();
	}
	public void OnsettingButtonClick()
	{
		ShowSettingPanel();
	}
	public void OnAddButtonClick()
	{
		if (!CanSubmitAddRequest())
		{
			RefreshAddButton();
			return;
		}

		FirestoreManager firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			ToastManager.ShowToast("好友请求服务未初始化");
			return;
		}

		FirestoreManager.UserSearchResult target = BuildAddTargetUser();
		isSendingRequest = true;
		RefreshAddButton();
		ToastManager.ShowToast($"正在发送给 {GetDisplayName()}");
		firestore.SendFriendRequest(target, success =>
		{
			isSendingRequest = false;
			if (success && !string.IsNullOrWhiteSpace(target.uid))
				pendingSentUserIds.Add(target.uid);

			RefreshAddButton();
			ToastManager.ShowToast(success ? $"已发送给 {GetDisplayName()}" : "发送失败，请稍后再试");
		});
	}
	public void OnViewHistoryBtnClick()
	{
		OpenFullHistoryWindow();
	}
	public void OnInviteButtonClick()
	{
		FriendDataManager.FriendData targetFriend = GetRelationshipTargetFriend();
		if (targetFriend == null)
		{
			ToastManager.ShowToast("好友资料不完整");
			RefreshInviteButton();
			return;
		}

		if (!RelationshipDivinationFlow.CanUseTwoPersonDivination(targetFriend, true))
		{
			RefreshInviteButton();
			return;
		}

		HideSettingPanel(true);
		RelationshipDivinationFlow.TryOpenActiveOrCreate(targetFriend, () =>
		{
			DivinationDirectionUI.Show(targetFriend);
		});
	}
	public void OnExitSettingButtonClick()
	{
		HideSettingPanel(false);
	}
	public void OnEditFriendButtonClick()
	{
		if (!CanEditFriend())
		{
			ToastManager.ShowToast("没有权限");
			return;
		}

		FriendDataManager.FriendData editingFriend = currentFriend;
		HideSettingPanel(false);
		EditFriendUI.Show(editingFriend, updatedFriend =>
		{
			currentFriend = updatedFriend;
			RefreshSettingControls();
			RefreshProfile(true);
			RefreshInviteButton();
			LoadPreviewHistory();
		});
	}
	public void OnDeleteFriendButtonClick()
	{
		if (!CanDeleteFriend() || isDeletingFriend)
		{
			return;
		}

		FriendDataManager.FriendData deletingFriend = currentFriend;
		HideSettingPanel(false);
		DeleteAccountUI.ShowDeleteFriend(deletingFriend, () => BeginDeleteFriend(deletingFriend));
	}
	#endregion

	private void BeginDeleteFriend(FriendDataManager.FriendData friend)
	{
		if (friend == null || isDeletingFriend)
			return;

		isDeletingFriend = true;
		RefreshSettingControls();
		RefreshInviteButton();
		if (friend.isVirtual)
		{
			DeleteVirtualFriend(friend);
			return;
		}

		DeleteRealFriend(friend);
	}

	private void DeleteRealFriend(FriendDataManager.FriendData friend)
	{
		if (friend == null)
		{
			FinishDeleteFriend(false, "好友资料不完整");
			return;
		}

		if (FirestoreManager.Instance == null)
		{
			bool removed = RemoveRealFriendLocal(friend);
			FinishDeleteFriend(removed, removed ? "已删除好友" : "删除好友失败");
			return;
		}

		ToastManager.ShowToast($"正在删除 {FormatFriendName(friend)}");
		FirestoreManager.Instance.RemoveRealFriend(friend, success =>
		{
			bool queued = FirestoreManager.IsRealFriendDeleteQueuedLocal(friend.firebaseUid);
			string message = success
				? (queued ? "已删除好友，云端稍后同步" : "已删除好友")
				: "删除好友失败，请稍后再试";
			FinishDeleteFriend(success, message);
		});
	}

	private void DeleteVirtualFriend(FriendDataManager.FriendData friend)
	{
		if (friend == null)
		{
			FinishDeleteFriend(false, "好友资料不完整");
			return;
		}

		if (FirestoreManager.Instance == null)
		{
			bool removed = FriendDataManager.Instance != null && FriendDataManager.Instance.RemoveVirtualFriend(friend.id);
			FinishDeleteFriend(removed, removed ? "已删除创建的好友" : "删除失败");
			return;
		}

		ToastManager.ShowToast($"正在删除 {FormatFriendName(friend)}");
		FirestoreManager.Instance.DeleteVirtualFriend(friend, success =>
		{
			bool queued = FirestoreManager.IsVirtualFriendDeleteQueuedLocal(friend.virtualFriendId);
			string message = success
				? (queued ? "已删除创建的好友，云端稍后同步" : "已删除创建的好友")
				: "删除失败";
			FinishDeleteFriend(success, message);
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

	private void FinishDeleteFriend(bool success, string message)
	{
		isDeletingFriend = false;
		RefreshSettingControls();
		RefreshInviteButton();
		if (!string.IsNullOrWhiteSpace(message))
			ToastManager.ShowToast(message);

		if (success)
			HideWindow();
	}

	private string FormatFriendName(FriendDataManager.FriendData friend)
	{
		if (friend == null) return "好友";
		if (!string.IsNullOrWhiteSpace(friend.name)) return friend.name.Trim();
		if (!string.IsNullOrWhiteSpace(friend.handle)) return friend.handle.Trim();
		return "好友";
	}

	private List<FriendProfileHistoryEntry> ToOverlayEntries(List<FriendPreviewHistoryEntry> entries)
	{
		List<FriendProfileHistoryEntry> overlayEntries = new List<FriendProfileHistoryEntry>();
		if (entries == null) return overlayEntries;

		foreach (FriendPreviewHistoryEntry entry in entries)
		{
			if (entry == null) continue;
			overlayEntries.Add(entry.ToOverlayEntry());
		}

		return overlayEntries;
	}

	private List<AllDivinationHistoryEntry> ToAllDivinationHistoryEntries(List<FriendPreviewHistoryEntry> entries)
	{
		List<AllDivinationHistoryEntry> allEntries = new List<AllDivinationHistoryEntry>();
		if (entries == null) return allEntries;

		foreach (FriendPreviewHistoryEntry entry in entries)
		{
			if (entry == null) continue;
			allEntries.Add(new AllDivinationHistoryEntry
			{
				cardSprites = entry.cardSprites != null ? new List<Sprite>(entry.cardSprites) : new List<Sprite>(),
				cardNames = entry.cardNames ?? string.Empty,
				description = entry.description ?? string.Empty,
				source = entry.source ?? string.Empty,
				time = entry.time ?? string.Empty,
				ownerName = GetDisplayName(),
				detailRecord = entry.detailRecord
			});
		}

		return allEntries;
	}

	private class FriendPreviewHistoryEntry
	{
		public List<Sprite> cardSprites = new List<Sprite>();
		public string cardNames;
		public string description;
		public string source;
		public string time;
		public DailyOracleSummaryRecord summary;
		public DivinationRecordData detailRecord;
		private DateTime sortTime;

		public DateTime SortTime => sortTime;

		public static FriendPreviewHistoryEntry Placeholder(string title, string description, string source, string time)
		{
			return new FriendPreviewHistoryEntry
			{
				cardSprites = new List<Sprite>(),
				cardNames = FirstNonEmpty(title, PrivateValueText),
				description = FirstNonEmpty(description, PrivateValueText),
				source = FirstNonEmpty(source, "占卜历史"),
				time = time ?? string.Empty,
				sortTime = DateTime.MinValue
			};
		}

		public static FriendPreviewHistoryEntry FromSummary(DailyOracleSummaryRecord summary)
		{
			string cardNames = TarotDeck.FormatDisplayName(FirstNonEmpty(summary?.cardName, "每日牌"), summary != null && summary.IsUpright);
			string description = FirstNonEmpty(summary?.oracle, summary?.title, summary?.microAction, cardNames);
			List<Sprite> sprites = new List<Sprite>();
			Sprite sprite = LoadTarotSprite(summary?.cardId, summary?.cardName, cardNames);
			if (sprite != null)
				sprites.Add(sprite);

			return new FriendPreviewHistoryEntry
			{
				cardSprites = sprites,
				cardNames = cardNames,
				description = description,
				source = "今日占卜",
				time = FormatHistoryDate(summary?.date),
				summary = summary,
				detailRecord = BuildDailyDetailRecord(summary, cardNames, description),
				sortTime = ParseSortTime(summary?.date)
			};
		}

		public static FriendPreviewHistoryEntry FromRelationshipRecord(RelationshipDivinationRecord record, string currentUid)
		{
			List<RelationshipDivinationCard> visibleCards = GetVisibleRelationshipCards(record, currentUid);
			List<Sprite> sprites = new List<Sprite>();
			List<string> names = new List<string>();
			foreach (RelationshipDivinationCard card in visibleCards)
			{
				names.Add(card != null ? card.DisplayName : "关系牌");
				Sprite sprite = LoadTarotSprite(card?.cardId, card?.cardName, card?.DisplayName)
					?? RelationshipDivinationFlow.LoadCardSprite(card);
				if (sprite != null)
					sprites.Add(sprite);
			}

			string cardNames = names.Count > 0 ? string.Join(" & ", names) : "双人关系牌";
			string timeValue = FirstNonEmpty(record?.completedAt, record?.createdAt);
			return new FriendPreviewHistoryEntry
			{
				cardSprites = sprites,
				cardNames = cardNames,
				description = BuildRelationshipDescription(record, cardNames),
				source = "双人关系占卜",
				time = FormatHistoryDate(timeValue),
				detailRecord = record != null ? RelationshipDivinationFlow.BuildDivinationRecord(record) : null,
				sortTime = ParseSortTime(timeValue)
			};
		}

		public static FriendPreviewHistoryEntry FromDivinationRecord(DivinationRecordData record)
		{
			List<Sprite> sprites = new List<Sprite>();
			List<string> names = new List<string>();
			if (record?.lockedCards != null)
			{
				foreach (LockedCard card in record.lockedCards)
				{
					if (card == null) continue;
					names.Add(TarotDeck.FormatDisplayName(FirstNonEmpty(card.cardName, card.cardId, "关系牌"), card.orientation));
					Sprite sprite = LoadTarotSprite(card.cardId, card.cardName);
					if (sprite != null)
						sprites.Add(sprite);
				}
			}

			string cardNames = names.Count > 0 ? string.Join(" & ", names) : FirstNonEmpty(record?.CardsSummary, "双人关系牌");
			return new FriendPreviewHistoryEntry
			{
				cardSprites = sprites,
				cardNames = cardNames,
				description = FirstNonEmpty(record?.shortVerdict, record?.judgeContent, record?.question, "已完成一次关系占卜。"),
				source = "双人关系占卜",
				time = FormatHistoryDate(record?.createdAt),
				detailRecord = record,
				sortTime = ParseSortTime(record?.createdAt)
			};
		}

		private static DivinationRecordData BuildDailyDetailRecord(DailyOracleSummaryRecord summary, string cardNames, string description)
		{
			if (summary == null)
				return null;

			string resolvedCardId = ResolveTarotCardId(summary.cardId, summary.cardName, cardNames);
			return new DivinationRecordData
			{
				readingId = FirstNonEmpty(summary.oracleId, $"daily_{summary.ownerUid}_{summary.date}"),
				question = FirstNonEmpty(summary.title, "每日占卜"),
				scene = "daily_oracle",
				spreadKind = "daily_oracle",
				lockedCards = new List<LockedCard>
				{
					new LockedCard
					{
						position = "今日牌",
						positionKey = "daily_card",
						cardId = FirstNonEmpty(resolvedCardId, summary.cardId),
						cardName = FirstNonEmpty(summary.cardName, cardNames, "每日牌"),
						orientation = summary.IsUpright ? "upright" : "reversed"
					}
				},
				shortVerdict = description,
				judgeContent = description,
				adviceContent = FirstNonEmpty(summary.microAction, "把这张牌当成今天的小提醒，选择一个能立刻完成的行动。"),
				topics = new List<string>
				{
					"这张牌今天最想提醒什么？",
					"我可以从哪个小行动开始？",
					"这份提醒和当前关系有什么关联？"
				},
				oracleId = FirstNonEmpty(summary.oracleId, "daily_oracle"),
				createdAt = summary.date
			};
		}

		private static Sprite LoadTarotSprite(params string[] cardTokens)
		{
			string cardId = ResolveTarotCardId(cardTokens);
			return string.IsNullOrWhiteSpace(cardId) ? null : TarotSpriteLoader.Load(cardId);
		}

		private static string ResolveTarotCardId(params string[] cardTokens)
		{
			if (cardTokens == null)
				return string.Empty;

			foreach (string token in cardTokens)
			{
				string candidate = CleanTarotCardToken(token);
				if (string.IsNullOrWhiteSpace(candidate))
					continue;

				TarotCard card = TarotDeck.GetById(candidate);
				if (card != null)
					return card.cardId;

				string normalizedId = NormalizeTarotCardId(candidate);
				card = TarotDeck.GetById(normalizedId);
				if (card != null)
					return card.cardId;

				card = FindTarotCardByName(candidate);
				if (card != null)
					return card.cardId;
			}

			return string.Empty;
		}

		private static string CleanTarotCardToken(string token)
		{
			if (string.IsNullOrWhiteSpace(token))
				return string.Empty;

			string result = token.Trim();
			int colonIndex = Mathf.Max(result.LastIndexOf(':'), result.LastIndexOf('：'));
			if (colonIndex >= 0 && colonIndex < result.Length - 1)
				result = result.Substring(colonIndex + 1).Trim();

			result = TarotDeck.StripOrientationSuffix(result);
			string[] orientationSuffixes = { "正位", "逆位", "upright", "reversed", "reverse" };
			foreach (string suffix in orientationSuffixes)
			{
				if (result.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
				{
					result = result.Substring(0, result.Length - suffix.Length).Trim();
					break;
				}
			}

			return result.Trim(' ', '·', '-', '_', '(', ')', '（', '）');
		}

		private static string NormalizeTarotCardId(string token)
		{
			if (string.IsNullOrWhiteSpace(token))
				return string.Empty;

			string value = token.Trim().ToLowerInvariant()
				.Replace('-', '_')
				.Replace(' ', '_');

			string[] suits = { "major", "cups", "wands", "swords", "pentacles" };
			foreach (string suit in suits)
			{
				string numberText = string.Empty;
				if (value.StartsWith($"{suit}_", StringComparison.OrdinalIgnoreCase))
					numberText = value.Substring(suit.Length + 1);
				else if (value.StartsWith(suit, StringComparison.OrdinalIgnoreCase))
					numberText = value.Substring(suit.Length);

				if (int.TryParse(numberText, out int number))
					return $"{suit}_{number:D2}";
			}

			return value;
		}

		private static TarotCard FindTarotCardByName(string token)
		{
			string normalizedToken = NormalizeTarotName(token);
			if (string.IsNullOrWhiteSpace(normalizedToken))
				return null;

			foreach (TarotCard card in TarotDeck.FullDeck)
			{
				if (card == null)
					continue;

				string zh = NormalizeTarotName(card.nameZh);
				string en = NormalizeTarotName(card.nameEn);
				string id = NormalizeTarotName(card.cardId);
				if (normalizedToken == zh || normalizedToken == en || normalizedToken == id)
					return card;
				if (normalizedToken.Length >= 3
					&& ((!string.IsNullOrWhiteSpace(en) && en.Contains(normalizedToken))
						|| (!string.IsNullOrWhiteSpace(zh) && zh.Contains(normalizedToken))))
					return card;
			}

			return null;
		}

		private static string NormalizeTarotName(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return string.Empty;

			value = CleanTarotCardToken(value).ToLowerInvariant();
			if (value.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
				value = value.Substring(4);

			return value
				.Replace("the", "")
				.Replace(" ", "")
				.Replace("_", "")
				.Replace("-", "")
				.Replace("·", "")
				.Replace("of", "");
		}

		public FriendProfileHistoryEntry ToOverlayEntry()
		{
			return new FriendProfileHistoryEntry
			{
				title = cardNames,
				content = description,
				date = time,
				cardSprite = cardSprites != null && cardSprites.Count > 0 ? cardSprites[0] : null,
				summary = summary
			};
		}

		private static List<RelationshipDivinationCard> GetVisibleRelationshipCards(RelationshipDivinationRecord record, string currentUid)
		{
			List<RelationshipDivinationCard> result = new List<RelationshipDivinationCard>();
			if (record == null) return result;

			AddVisibleCard(result, record.InitiatorCard, record, currentUid);
			AddVisibleCard(result, record.SharedCard, record, currentUid);
			AddVisibleCard(result, record.ReceiverCard, record, currentUid);
			return result;
		}

		private static void AddVisibleCard(
			List<RelationshipDivinationCard> result,
			RelationshipDivinationCard card,
			RelationshipDivinationRecord record,
			string currentUid)
		{
			if (card == null || result == null || !IsRelationshipCardVisible(card, record, currentUid))
				return;
			result.Add(card);
		}

		private static bool IsRelationshipCardVisible(RelationshipDivinationCard card, RelationshipDivinationRecord record, string currentUid)
		{
			if (card == null || record == null) return false;
			if (record.isLocalOnly) return true;
			if (card.visibleTo == "both") return record.IsCompleted;
			if (card.visibleTo == "initiator")
				return record.IsCurrentUserInitiator(currentUid) && record.initiatorRevealed;
			if (card.visibleTo == "receiver")
				return record.IsCurrentUserReceiver(currentUid) && record.receiverRevealed;
			return false;
		}

		private static string BuildRelationshipDescription(RelationshipDivinationRecord record, string cardNames)
		{
			string question = FirstNonEmpty(record?.question);
			if (!string.IsNullOrWhiteSpace(question))
				return question;
			return $"你们完成了一次双人关系占卜，可见牌面：{cardNames}";
		}
	}
}
