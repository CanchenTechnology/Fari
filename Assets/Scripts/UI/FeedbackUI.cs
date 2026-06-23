/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 15:28:49
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using TMPro;

public class FeedbackUI : WindowBase
{
	public FeedbackUIComponent uiComponent;
	private const string DEFAULT_TAG = "Bug";
	private const string FEEDBACK_CACHE_KEY_PREFIX = "FeedbackUI_CachedEntries_v1_";
	private const int MAX_LOCAL_FEEDBACK = 50;
	private readonly List<GameObject> _communityItems = new List<GameObject>();
	private readonly List<GameObject> _chatItems = new List<GameObject>();
	private static readonly List<FeedbackEntry> _feedbackEntries = new List<FeedbackEntry>();
	private static readonly List<FeedbackChatMessage> _chatMessages = new List<FeedbackChatMessage>();
	private static string _loadedFeedbackUid = string.Empty;
	private string _selectedTag = DEFAULT_TAG;
	private string _searchText = string.Empty;
	private bool _isSyncingPendingFeedback;

	[Serializable]
	private class FeedbackEntry
	{
		public string feedbackId;
		public string category;
		public string tag;
		public string content;
		public string createdAt;
		public string status;
	}

	[Serializable]
	private class FeedbackEntryCache
	{
		public List<FeedbackEntry> entries = new List<FeedbackEntry>();
	}

	private class FeedbackChatMessage
	{
		public bool fromUser;
		public string text;
	}

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<FeedbackUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		EnsureInitialState();
		LoadCloudFeedbackThenRefresh();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		ClearObjects(_communityItems);
		ClearObjects(_chatItems);
		base.OnDestroy();
	}
	#endregion

	#region API Function
	private void EnsureInitialState()
	{
		string currentUid = GetFeedbackCacheUid();
		if (_loadedFeedbackUid != currentUid)
		{
			_feedbackEntries.Clear();
			_chatMessages.Clear();
			_loadedFeedbackUid = currentUid;
		}

		if (_feedbackEntries.Count == 0)
		{
			LoadCachedFeedbackEntries();
			MergeLocalPendingFeedbackEntries();
		}

		if (_chatMessages.Count == 0)
		{
			_chatMessages.Add(new FeedbackChatMessage
			{
				fromUser = false,
				text = "你好，我会记录你的问题和建议。写下具体场景会更容易定位。"
			});
		}

		if (uiComponent.TabCommunityToggle != null && uiComponent.TabChatToggle != null
			&& !uiComponent.TabCommunityToggle.isOn && !uiComponent.TabChatToggle.isOn)
		{
			uiComponent.TabCommunityToggle.isOn = true;
		}
	}

	private void RefreshViews()
	{
		bool showCommunity = uiComponent.TabCommunityToggle == null || uiComponent.TabCommunityToggle.isOn;
		if (uiComponent.CommunityViewTransform != null)
			uiComponent.CommunityViewTransform.gameObject.SetActive(showCommunity);
		if (uiComponent.ChatViewTransform != null)
			uiComponent.ChatViewTransform.gameObject.SetActive(!showCommunity);

		RenderCommunityList();
		RenderChatList();
	}

	private void LoadCloudFeedbackThenRefresh()
	{
		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			RefreshViews();
			return;
		}

		if (UserDataManager.Instance == null || string.IsNullOrEmpty(UserDataManager.Instance.FirebaseUid))
		{
			RefreshViews();
			return;
		}

		firestore.LoadFeedback(entries =>
		{
			if (entries != null)
			{
				List<FeedbackEntry> pendingEntries = CollectPendingFeedbackEntries();
				_feedbackEntries.Clear();
				foreach (var entry in entries)
				{
					if (entry == null || string.IsNullOrWhiteSpace(entry.content)) continue;
					_feedbackEntries.Add(new FeedbackEntry
					{
						feedbackId = entry.feedbackId,
						category = entry.category,
						tag = entry.tag,
						content = entry.content,
						createdAt = entry.createdAt,
						status = GetStatusDisplay(entry.status)
					});
				}
				MergePendingFeedbackEntries(pendingEntries);
				SaveFeedbackEntriesToCache();
			}

			RefreshViews();
			TrySyncPendingFeedbackEntries();
		});
	}

	private void RenderCommunityList()
	{
		ClearObjects(_communityItems);
		RectTransform content = uiComponent.CommunityScrollScrollRect != null
			? uiComponent.CommunityScrollScrollRect.content
			: null;
		if (content == null) return;

		float y = 16f;
		AddText(content, _communityItems, "反馈社区", 22, FontStyles.Bold, new Color(0.95f, 0.86f, 1f), ref y, 32f);
		AddText(content, _communityItems, $"当前标签：{GetTagDisplay(_selectedTag)}", 14, FontStyles.Normal, new Color(0.72f, 0.66f, 0.82f), ref y, 24f);

		int count = 0;
		foreach (var entry in _feedbackEntries)
		{
			if (!PassesSearch(entry)) continue;
			AddFeedbackCard(content, entry, ref y);
			count++;
		}

		if (count == 0)
		{
			string emptyText = string.IsNullOrWhiteSpace(_searchText)
				? "暂无反馈记录，写下你的想法后会显示在这里。"
				: "没有匹配的反馈。";
			AddText(content, _communityItems, emptyText, 16, FontStyles.Normal, new Color(0.72f, 0.66f, 0.82f), ref y, 52f);
		}

		SetContentHeight(content, y + 24f, 420f);
	}

	private void RenderChatList()
	{
		ClearObjects(_chatItems);
		RectTransform content = uiComponent.ChatScrollScrollRect != null
			? uiComponent.ChatScrollScrollRect.content
			: null;
		if (content == null) return;

		float y = 16f;
		foreach (var message in _chatMessages)
		{
			AddChatBubble(content, message, ref y);
		}

		SetContentHeight(content, y + 24f, 420f);
		if (uiComponent.ChatScrollScrollRect != null)
			uiComponent.ChatScrollScrollRect.verticalNormalizedPosition = 0f;
	}

	private bool PassesSearch(FeedbackEntry entry)
	{
		if (string.IsNullOrWhiteSpace(_searchText)) return true;
		string needle = _searchText.Trim().ToLowerInvariant();
		return (entry.content ?? string.Empty).ToLowerInvariant().Contains(needle)
			|| (entry.tag ?? string.Empty).ToLowerInvariant().Contains(needle)
			|| GetTagDisplay(entry.tag).ToLowerInvariant().Contains(needle);
	}

	private void AddFeedbackCard(RectTransform parent, FeedbackEntry entry, ref float y)
	{
		string text = $"[{GetTagDisplay(entry.tag)}] {entry.content}\n{entry.createdAt} · {entry.status}";
		AddText(parent, _communityItems, text, 15, FontStyles.Normal, new Color(0.86f, 0.84f, 0.92f), ref y, EstimateHeight(text, 15, parent.rect.width - 56f), new Color(0.16f, 0.12f, 0.24f));
		y += 8f;
	}

	private void AddChatBubble(RectTransform parent, FeedbackChatMessage message, ref float y)
	{
		Color textColor = message.fromUser
			? new Color(0.95f, 0.9f, 1f)
			: new Color(0.84f, 0.82f, 0.92f);
		Color bgColor = message.fromUser
			? new Color(0.33f, 0.18f, 0.52f)
			: new Color(0.14f, 0.12f, 0.2f);
		float height = EstimateHeight(message.text, 15, parent.rect.width - 80f);
		AddText(parent, _chatItems, message.text, 15, FontStyles.Normal, textColor, ref y, height, bgColor, message.fromUser);
		y += 8f;
	}

	private void AddText(RectTransform parent, List<GameObject> owner, string text, int fontSize, FontStyles style, Color color, ref float y, float height, Color? background = null, bool alignRight = false)
	{
		GameObject go = new GameObject("FeedbackText", typeof(RectTransform));
		go.transform.SetParent(parent, false);
		var rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = new Vector2(alignRight ? 36f : 20f, -y);
		rect.sizeDelta = new Vector2(alignRight ? -56f : -40f, height);

		if (background.HasValue)
		{
			var image = go.AddComponent<Image>();
			image.color = background.Value;
		}

		var labelGo = new GameObject("Text", typeof(RectTransform));
		labelGo.transform.SetParent(go.transform, false);
		var labelRect = labelGo.GetComponent<RectTransform>();
		labelRect.anchorMin = Vector2.zero;
		labelRect.anchorMax = Vector2.one;
		labelRect.offsetMin = new Vector2(12f, 6f);
		labelRect.offsetMax = new Vector2(-12f, -6f);

		var label = labelGo.AddComponent<TextMeshProUGUI>();
		label.font = TMP_Settings.defaultFontAsset;
		label.fontSize = fontSize;
		label.fontStyle = style;
		label.color = color;
		label.alignment = alignRight ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
		label.enableWordWrapping = true;
		label.overflowMode = TextOverflowModes.Overflow;
		label.text = text ?? string.Empty;

		owner.Add(go);
		y += height;
	}

	private float EstimateHeight(string text, int fontSize, float maxWidth)
	{
		if (string.IsNullOrEmpty(text)) return 44f;
		float safeWidth = Mathf.Max(180f, maxWidth);
		float charsPerLine = Mathf.Max(8f, safeWidth / (fontSize * 0.62f));
		float lines = Mathf.Ceil(text.Length / charsPerLine) + Mathf.Max(0, CountLines(text) - 1);
		return Mathf.Max(52f, lines * fontSize * 1.55f + 18f);
	}

	private int CountLines(string text)
	{
		int count = 1;
		foreach (char c in text)
		{
			if (c == '\n') count++;
		}
		return count;
	}

	private void SetContentHeight(RectTransform content, float height, float minHeight)
	{
		content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(height, minHeight));
	}

	private void ClearObjects(List<GameObject> objects)
	{
		foreach (var item in objects)
		{
			if (item != null)
				GameObject.Destroy(item);
		}
		objects.Clear();
	}

	private void SelectTag(string tag)
	{
		_selectedTag = string.IsNullOrEmpty(tag) ? DEFAULT_TAG : tag;
		RenderCommunityList();
		ToastManager.ShowToast($"已选择：{GetTagDisplay(_selectedTag)}");
	}

	private string GetPublishText()
	{
		if (uiComponent.SearchInputInputField != null && !string.IsNullOrWhiteSpace(uiComponent.SearchInputInputField.text))
			return uiComponent.SearchInputInputField.text.Trim();
		if (uiComponent.ChatInputInputField != null && !string.IsNullOrWhiteSpace(uiComponent.ChatInputInputField.text))
			return uiComponent.ChatInputInputField.text.Trim();
		return string.Empty;
	}

	private string GetChatInputText()
	{
		if (uiComponent.ChatInputInputField != null && !string.IsNullOrWhiteSpace(uiComponent.ChatInputInputField.text))
			return uiComponent.ChatInputInputField.text.Trim();
		if (uiComponent.ChatInputInputCaretInputField != null && !string.IsNullOrWhiteSpace(uiComponent.ChatInputInputCaretInputField.text))
			return uiComponent.ChatInputInputCaretInputField.text.Trim();
		return string.Empty;
	}

	private void ClearFeedbackInputs()
	{
		if (uiComponent.SearchInputInputField != null)
			uiComponent.SearchInputInputField.text = string.Empty;
		if (uiComponent.ChatInputInputField != null)
			uiComponent.ChatInputInputField.text = string.Empty;
		if (uiComponent.ChatInputInputCaretInputField != null)
			uiComponent.ChatInputInputCaretInputField.text = string.Empty;
		_searchText = string.Empty;
	}

	private void SubmitFeedback(string category, string tag, string content, string source, Action<bool> onComplete)
	{
		var firestore = FirestoreManager.Instance;
		bool canSyncNow = firestore != null
			&& firestore.IsInitialized
			&& UserDataManager.Instance != null
			&& !string.IsNullOrEmpty(UserDataManager.Instance.FirebaseUid);

		var entry = new FeedbackEntry
		{
			feedbackId = Guid.NewGuid().ToString("N"),
			category = category,
			tag = tag,
			content = content,
			createdAt = DateTime.Now.ToString("MM/dd HH:mm"),
			status = canSyncNow ? "提交中" : "待同步"
		};
		_feedbackEntries.Insert(0, entry);
		SaveFeedbackEntriesToCache();
		RenderCommunityList();

		if (!canSyncNow)
		{
			onComplete?.Invoke(false);
			return;
		}

		firestore.SaveFeedback(category, tag, content, source, success =>
		{
			entry.status = success ? "已提交" : "待同步";
			SaveFeedbackEntriesToCache();
			RenderCommunityList();
			onComplete?.Invoke(success);
		}, entry.feedbackId);
	}

	private string GetTagDisplay(string tag)
	{
		return tag switch
		{
			"Bug" => "问题反馈",
			"Feature" => "功能建议",
			"Role" => "神谕师体验",
			"VIP" => "会员与付费",
			_ => "综合反馈"
		};
	}

	private string GetStatusDisplay(string status)
	{
		return status switch
		{
			"new" => "已提交",
			"reviewing" => "处理中",
			"closed" => "已处理",
			"pending" => "待同步",
			"syncing" => "同步中",
			_ => string.IsNullOrWhiteSpace(status) ? "已提交" : status
		};
	}

	private void LoadCachedFeedbackEntries()
	{
		LoadCachedFeedbackEntriesForUid(GetFeedbackCacheUid(), _feedbackEntries, clearTarget: true);
	}

	private void LoadCachedFeedbackEntriesForUid(string uid, List<FeedbackEntry> target, bool clearTarget)
	{
		if (target == null) return;
		if (clearTarget)
			target.Clear();

		string json = PlayerPrefs.GetString(GetFeedbackCacheKey(uid), string.Empty);
		if (string.IsNullOrEmpty(json)) return;

		try
		{
			var cache = JsonUtility.FromJson<FeedbackEntryCache>(json);
			if (cache?.entries == null) return;
			foreach (var entry in cache.entries)
			{
				if (entry != null && !string.IsNullOrWhiteSpace(entry.content))
				{
					if (string.IsNullOrEmpty(entry.feedbackId))
						entry.feedbackId = Guid.NewGuid().ToString("N");
					target.Add(entry);
				}
			}
		}
		catch (Exception e)
		{
			Debug.LogWarning("[FeedbackUI] 本地反馈缓存读取失败: " + e.Message);
		}
	}

	private void SaveFeedbackEntriesToCache()
	{
		SaveFeedbackEntriesToCacheForUid(GetFeedbackCacheUid(), _feedbackEntries);
	}

	private void SaveFeedbackEntriesToCacheForUid(string uid, List<FeedbackEntry> source)
	{
		var entries = new List<FeedbackEntry>();
		if (source == null)
			source = new List<FeedbackEntry>();

		foreach (var entry in source)
		{
			if (entry != null && !string.IsNullOrWhiteSpace(entry.content))
				entries.Add(entry);
			if (entries.Count >= MAX_LOCAL_FEEDBACK)
				break;
		}

		string json = JsonUtility.ToJson(new FeedbackEntryCache { entries = entries });
		PlayerPrefs.SetString(GetFeedbackCacheKey(uid), json);
		PlayerPrefs.Save();
	}

	private string GetFeedbackCacheKey()
	{
		return GetFeedbackCacheKey(GetFeedbackCacheUid());
	}

	private string GetFeedbackCacheKey(string uid)
	{
		return FEEDBACK_CACHE_KEY_PREFIX + (string.IsNullOrEmpty(uid) ? "local" : uid);
	}

	private string GetFeedbackCacheUid()
	{
		string uid = UserDataManager.Instance != null ? UserDataManager.Instance.FirebaseUid : string.Empty;
		if (string.IsNullOrEmpty(uid))
			uid = "local";
		return uid;
	}

	private void MergeLocalPendingFeedbackEntries()
	{
		string currentUid = GetFeedbackCacheUid();
		if (currentUid == "local") return;

		var localEntries = new List<FeedbackEntry>();
		LoadCachedFeedbackEntriesForUid("local", localEntries, clearTarget: true);
		if (localEntries.Count == 0) return;

		var remainingLocal = new List<FeedbackEntry>();
		bool migratedAny = false;
		foreach (FeedbackEntry entry in localEntries)
		{
			if (entry == null || string.IsNullOrWhiteSpace(entry.content))
				continue;

			if (!IsPendingStatus(entry.status))
			{
				remainingLocal.Add(entry);
				continue;
			}

			if (string.IsNullOrEmpty(entry.feedbackId))
				entry.feedbackId = Guid.NewGuid().ToString("N");

			bool exists = _feedbackEntries.Exists(existing =>
				existing != null
				&& !string.IsNullOrEmpty(existing.feedbackId)
				&& existing.feedbackId == entry.feedbackId);
			if (!exists)
			{
				entry.status = "待同步";
				_feedbackEntries.Add(entry);
				migratedAny = true;
			}
		}

		if (!migratedAny) return;

		SaveFeedbackEntriesToCache();
		SaveFeedbackEntriesToCacheForUid("local", remainingLocal);
	}

	private List<FeedbackEntry> CollectPendingFeedbackEntries()
	{
		var pending = new List<FeedbackEntry>();
		foreach (var entry in _feedbackEntries)
		{
			if (entry != null && IsPendingStatus(entry.status))
				pending.Add(entry);
		}
		return pending;
	}

	private void MergePendingFeedbackEntries(List<FeedbackEntry> pendingEntries)
	{
		if (pendingEntries == null || pendingEntries.Count == 0) return;

		foreach (var pending in pendingEntries)
		{
			if (pending == null || string.IsNullOrWhiteSpace(pending.content)) continue;
			bool exists = _feedbackEntries.Exists(entry =>
				entry != null
				&& !string.IsNullOrEmpty(entry.feedbackId)
				&& entry.feedbackId == pending.feedbackId);
			if (!exists)
				_feedbackEntries.Add(pending);
		}
	}

	private bool IsPendingStatus(string status)
	{
		return status == "待同步" || status == "提交中" || status == "同步中" || status == "pending" || status == "syncing";
	}

	private void TrySyncPendingFeedbackEntries()
	{
		if (_isSyncingPendingFeedback) return;

		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized) return;
		if (UserDataManager.Instance == null || string.IsNullOrEmpty(UserDataManager.Instance.FirebaseUid)) return;

		List<FeedbackEntry> pendingEntries = CollectPendingFeedbackEntries();
		if (pendingEntries.Count == 0) return;

		_isSyncingPendingFeedback = true;
		int remaining = pendingEntries.Count;
		foreach (var entry in pendingEntries)
		{
			if (string.IsNullOrEmpty(entry.feedbackId))
				entry.feedbackId = Guid.NewGuid().ToString("N");
			entry.status = "同步中";

			firestore.SaveFeedback(entry.category, entry.tag, entry.content, "pending_feedback_sync", success =>
			{
				entry.status = success ? "已提交" : "待同步";
				remaining--;
				SaveFeedbackEntriesToCache();
				RenderCommunityList();

				if (remaining <= 0)
					_isSyncingPendingFeedback = false;
			}, entry.feedbackId);
		}

		SaveFeedbackEntriesToCache();
		RenderCommunityList();
	}

	#endregion

	#region UI组件事件		 
	public void OnChatInputInputCaretInputEnd(string text)
		 {
		
		 }
				 public void OnChatInputInputCaretInputChange(string text)
		 {
		
		 }
				 
	public void OnChatInputInputField(string text)
	{

	}
	public void OnTopTabsToggleChange(bool state, Toggle toggle)
	{

	}
		
	public void OnBackButtonClick()
	{
		HideWindow();
	}

	public void OnTabCommunityToggleChange(bool state, Toggle toggle)
	{
		if (!state) return;
		if (uiComponent.CommunityViewTransform != null)
			uiComponent.CommunityViewTransform.gameObject.SetActive(true);
		if (uiComponent.ChatViewTransform != null)
			uiComponent.ChatViewTransform.gameObject.SetActive(false);
		RenderCommunityList();
	}
	public void OnTabChatToggleChange(bool state, Toggle toggle)
	{
		if (!state) return;
		if (uiComponent.CommunityViewTransform != null)
			uiComponent.CommunityViewTransform.gameObject.SetActive(false);
		if (uiComponent.ChatViewTransform != null)
			uiComponent.ChatViewTransform.gameObject.SetActive(true);
		RenderChatList();
	}
	public void OnSearchInputInputChange(string text)
	{
		_searchText = text ?? string.Empty;
		RenderCommunityList();
	}
	public void OnSearchInputInputEnd(string text)
	{
		_searchText = text ?? string.Empty;
		RenderCommunityList();
	}
	public void OnPublishFeedbackButtonClick()
	{
		string content = GetPublishText();
		if (string.IsNullOrWhiteSpace(content))
		{
			ToastManager.ShowToast("先写下你的反馈内容");
			return;
		}

		SubmitFeedback("community", _selectedTag, content, "community_publish", success =>
		{
			ToastManager.ShowToast(success ? "反馈已提交" : "已暂存，登录后可同步");
		});
		ClearFeedbackInputs();
		RenderCommunityList();
	}
	public void OnTagBugButtonClick()
	{
		SelectTag("Bug");
	}
	public void OnTagFeatureButtonClick()
	{
		SelectTag("Feature");
	}
	public void OnTagRoleButtonClick()
	{
		SelectTag("Role");
	}
	public void OnTagVIPButtonClick()
	{
		SelectTag("VIP");
	}
	public void OnChatInputInputChange(string text)
	{
	}
	public void OnChatInputInputEnd(string text)
	{
	}
	public void OnSendMessageButtonClick()
	{
		string content = GetChatInputText();
		if (string.IsNullOrWhiteSpace(content))
		{
			ToastManager.ShowToast("请输入要反馈的问题");
			return;
		}

		_chatMessages.Add(new FeedbackChatMessage { fromUser = true, text = content });
		_chatMessages.Add(new FeedbackChatMessage { fromUser = false, text = "已记录。我们会把这条反馈和当前账号关联，后续用于排查和优化。" });
		RenderChatList();

		SubmitFeedback("chat", _selectedTag, content, "feedback_chat", success =>
		{
			ToastManager.ShowToast(success ? "反馈消息已发送" : "反馈已暂存");
		});
		ClearFeedbackInputs();
		RenderChatList();
	}
	#endregion
}
