/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 12:49:47
 * Description: UI 表现层 —— 占卜历史记录列表
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;
using XFGameFrameWork;
using TMPro;

public class HistoryUI : WindowBase
{
	public HistoryUIComponent uiComponent;

	/// <summary>当前选中的占卜记录（打开详情前设置）</summary>
	public static DivinationRecordData SelectedRecord { get; set; }

	private const string HistoryItemPrefabName = "HistoryItem";

	/// <summary>已加载的记录列表</summary>
	private List<DivinationRecordData> _records = new List<DivinationRecordData>();

	/// <summary>是否正在加载</summary>
	private bool _isLoading = false;

	private LoopListView2 _historyListView;
	private bool _listInitialized;
	private GameObject _emptyStateObject;
	private int _loadRequestId;

	[Header("列表配置")]
	public Color itemBgColor = new Color(0.08f, 0.11f, 0.24f, 0.86f);
	public Color itemTextColor = new Color(0.92f, 0.90f, 0.98f, 1f);
	public Color itemSubTextColor = new Color(0.56f, 0.58f, 0.70f, 1f);
	public Color itemAccentColor = new Color(0.68f, 0.42f, 0.96f, 1f);
	public Color itemCompleteColor = new Color(0.93f, 0.68f, 0.25f, 1f);
	public Color itemProgressColor = new Color(0.68f, 0.42f, 0.96f, 1f);

	#region 生命周期函数

	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<HistoryUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();

		InitHistoryListView();
		SetLoadingVisible(false);
	}

	public override void OnShow()
	{
		base.OnShow();
		RefreshList();
	}

	public override void OnHide()
	{
		_loadRequestId++;
		SetLoadingVisible(false);
		base.OnHide();
	}

	public override void OnDestroy()
	{
		SelectedRecord = null;
		base.OnDestroy();
	}

	#endregion

	#region API Function

	/// <summary>
	/// 刷新列表（只读取 Firebase 历史记录）
	/// </summary>
	public void RefreshList()
	{
		int requestId = ++_loadRequestId;
		DivinationHistoryCacheService cache = GetHistoryCache();
		List<DivinationRecordData> cachedRecords = cache.GetSnapshot();
		bool hasWarmCache = cache.HasLoadedOnce || cachedRecords.Count > 0;

		if (hasWarmCache)
		{
			_isLoading = false;
			SetLoadingVisible(false);
			ApplyRecords(cachedRecords);
		}
		else
		{
			_isLoading = true;
			SetLoadingVisible(true);
			HideEmptyState();
		}

		cache.Refresh(false, (records, success) =>
		{
			if (requestId != _loadRequestId) return;

			_isLoading = false;
			SetLoadingVisible(false);
			ApplyRecords(records);

			if (!success && (_records == null || _records.Count == 0))
				ToastManager.ShowToast("历史服务暂不可用");
		});
	}

	private DivinationHistoryCacheService GetHistoryCache()
	{
		return DivinationHistoryCacheService.Instance;
	}

	private void ApplyRecords(List<DivinationRecordData> records)
	{
		_records = records ?? new List<DivinationRecordData>();
		ValidateSelectedRecord();
		RenderList();
		Debug.Log($"[HistoryUI] 渲染了 {_records.Count} 条记录");
	}

	/// <summary>
	/// 渲染列表项
	/// </summary>
	private void RenderList()
	{
		InitHistoryListView();
		SetLoadingVisible(_isLoading);
		if (_isLoading)
		{
			HideEmptyState();
			return;
		}

		int count = _records?.Count ?? 0;
		if (count == 0)
		{
			ShowEmptyState();
			RefreshLoopListCount(0);
			return;
		}

		HideEmptyState();
		RefreshLoopListCount(count);
	}

	#endregion

	#region SuperScrollView

	private void InitHistoryListView()
	{
		if (_listInitialized) return;

		if (_historyListView == null && uiComponent != null)
			_historyListView = uiComponent.HistoryListScrollLoopListView2;

		if (_historyListView == null)
		{
			Debug.LogError("[HistoryUI] HistoryListScrollLoopListView2 未绑定，无法渲染占卜历史");
			return;
		}

		_historyListView.InitListView(0, OnGetHistoryItemByIndex);
		_listInitialized = true;
	}

	private void RefreshLoopListCount(int count)
	{
		if (!_listInitialized || _historyListView == null)
			return;

		_historyListView.SetListItemCount(count, true);
		_historyListView.RefreshAllShownItem();
	}

	private LoopListViewItem2 OnGetHistoryItemByIndex(LoopListView2 listView, int index)
	{
		if (_records == null || index < 0 || index >= _records.Count)
			return null;

		LoopListViewItem2 item = listView.NewListViewItem(HistoryItemPrefabName);
		if (item == null)
		{
			Debug.LogError($"[HistoryUI] 找不到 SuperScrollView Item 预制体: {HistoryItemPrefabName}");
			return null;
		}

		BindHistoryItem(item.gameObject, _records[index]);
		return item;
	}

	#endregion

	#region 列表项绑定

	private void BindHistoryItem(GameObject itemObject, DivinationRecordData record)
	{
		if (itemObject == null || record == null)
			return;

		OracleHistoryItem historyItem = itemObject.GetComponent<OracleHistoryItem>();
		TMP_Text questionText = historyItem?.contentText ?? FindText(itemObject.transform, "contentText");
		TMP_Text timeText = historyItem?.timeText ?? FindText(itemObject.transform, "timeText");
		TMP_Text stateText = historyItem?.stateText ?? FindText(itemObject.transform, "stateText");
		TMP_Text scopeText = historyItem?.tag1Text ?? FindTagText(itemObject.transform, "Tag1");
		TMP_Text typeText = historyItem?.tag2Text ?? FindTagText(itemObject.transform, "Tag1_1");

		SetText(questionText, BuildQuestionText(record));
		SetText(timeText, BuildDisplayTime(record));
		SetText(scopeText, BuildScopeLabel(record));
		SetText(typeText, BuildTypeLabel(record));

		bool isComplete = IsRecordComplete(record);
		SetText(stateText, isComplete ? "已完成" : "进行中");

		ApplyItemVisuals(itemObject, questionText, timeText, scopeText, typeText, stateText, isComplete);
		BindItemClick(itemObject, record, historyItem?.viewBtn);
	}

	private void ApplyItemVisuals(
		GameObject itemObject,
		TMP_Text questionText,
		TMP_Text timeText,
		TMP_Text scopeText,
		TMP_Text typeText,
		TMP_Text stateText,
		bool isComplete)
	{
		Image bg = itemObject.GetComponent<Image>();
		if (bg != null)
		{
			bg.color = itemBgColor;
			bg.raycastTarget = true;
		}

		if (questionText != null)
		{
			questionText.color = itemTextColor;
			questionText.enableAutoSizing = true;
			questionText.fontSizeMin = 24;
			questionText.fontSizeMax = Mathf.Max(questionText.fontSize, 36);
			questionText.enableWordWrapping = true;
			questionText.overflowMode = TextOverflowModes.Truncate;
		}

		if (timeText != null)
			timeText.color = itemSubTextColor;

		ApplyTagTextStyle(scopeText);
		ApplyTagTextStyle(typeText);

		if (stateText != null)
			stateText.color = isComplete ? itemCompleteColor : itemProgressColor;

		Transform stateTransform = FindDeepChild(itemObject.transform, "state");
		Image stateBg = stateTransform != null ? stateTransform.GetComponent<Image>() : null;
		if (stateBg != null)
		{
			Color statusColor = isComplete ? itemCompleteColor : itemProgressColor;
			stateBg.color = new Color(statusColor.r, statusColor.g, statusColor.b, 0.12f);
		}
	}

	private void ApplyTagTextStyle(TMP_Text text)
	{
		if (text == null) return;

		text.color = itemAccentColor;
		text.enableAutoSizing = true;
		text.fontSizeMin = 18;
		text.fontSizeMax = Mathf.Max(text.fontSize, 30);
		text.enableWordWrapping = true;
		text.overflowMode = TextOverflowModes.Truncate;
	}

	private void BindItemClick(GameObject itemObject, DivinationRecordData record, Button viewButton)
	{
		Image bg = itemObject.GetComponent<Image>();
		Button button = viewButton != null ? viewButton : itemObject.GetComponent<Button>();
		if (button == null)
			button = itemObject.AddComponent<Button>();

		if (button.targetGraphic == null)
			button.targetGraphic = button.GetComponent<Image>() ?? bg;
		button.onClick.RemoveAllListeners();
		button.onClick.AddListener(() => OnRecordItemClick(record));

		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
		colors.pressedColor = new Color(0.82f, 0.78f, 0.92f, 1f);
		colors.selectedColor = Color.white;
		colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
		colors.colorMultiplier = 1f;
		button.colors = colors;
	}

	/// <summary>
	/// 列表项点击 → 打开占卜详情页
	/// </summary>
	private void OnRecordItemClick(DivinationRecordData record)
	{
		if (record == null) return;

		Debug.Log($"[HistoryUI] 点击记录: {record.readingId} - {BuildQuestionText(record)}");

		SelectedRecord = record;
		DivinationHistoryUI.SelectedRecord = record;
		DivinationHistoryUI detailWindow = UIModule.Instance.PopUpWindow<DivinationHistoryUI>();
		if (detailWindow != null)
			detailWindow.SetRecord(record);
	}

	private void ValidateSelectedRecord()
	{
		if (SelectedRecord == null || _records == null || _records.Count == 0)
		{
			SelectedRecord = null;
			return;
		}

		string selectedId = SelectedRecord.readingId;
		if (string.IsNullOrEmpty(selectedId))
		{
			SelectedRecord = null;
			return;
		}

		foreach (DivinationRecordData record in _records)
		{
			if (record != null && record.readingId == selectedId)
				return;
		}

		SelectedRecord = null;
	}

	#endregion

	#region 显示文本

	private string BuildQuestionText(DivinationRecordData record)
	{
		if (record == null) return "未命名占卜";

		if (!string.IsNullOrWhiteSpace(record.question))
			return record.question.Trim();

		if (!string.IsNullOrWhiteSpace(record.shortVerdict))
			return record.shortVerdict.Trim();

		return "未命名占卜";
	}

	private string BuildDisplayTime(DivinationRecordData record)
	{
		if (record == null || string.IsNullOrWhiteSpace(record.createdAt))
			return "";

		if (!DateTime.TryParse(record.createdAt, out DateTime createdAt))
			return record.DisplayTime;

		DateTime date = createdAt.Date;
		DateTime today = DateTime.Now.Date;
		string time = createdAt.ToString("HH:mm");

		if (date == today)
			return $"今天 {time}";

		if (date == today.AddDays(-1))
			return $"昨天 {time}";

		if (date == today.AddDays(-2))
			return $"前天 {time}";

		if (createdAt.Year == DateTime.Now.Year)
			return createdAt.ToString("MM/dd HH:mm");

		return createdAt.ToString("yyyy/MM/dd HH:mm");
	}

	private string BuildScopeLabel(DivinationRecordData record)
	{
		string scene = (record?.scene ?? "").ToLowerInvariant();
		string spreadKind = (record?.spreadKind ?? "").ToLowerInvariant();
		string label = record?.SpreadLabel ?? "";

		if (scene.Contains("friend")
			|| scene.Contains("friendship")
			|| spreadKind.Contains("friend")
			|| label.Contains("双人")
			|| label.Contains("朋友"))
			return "朋友";

		return "个人";
	}

	private string BuildTypeLabel(DivinationRecordData record)
	{
		string spreadKind = (record?.spreadKind ?? "").ToLowerInvariant();
		string scene = (record?.scene ?? "").ToLowerInvariant();
		string oracleId = (record?.oracleId ?? "").ToLowerInvariant();
		string label = record?.SpreadLabel ?? "";

		if (scene.Contains("daily") || spreadKind.Contains("daily") || label.Contains("今日"))
			return "今日神谕";

		if (scene.Contains("meditation") || spreadKind.Contains("meditation") || label.Contains("冥想"))
			return "冥想引导";

		if (scene.Contains("astrology") || spreadKind.Contains("astrology") || oracleId.Contains("astrology") || label.Contains("占星"))
			return "占星";

		if (spreadKind.Contains("relationship") || label.Contains("关系") || scene.Contains("friend_relationship"))
			return "关系占卜";

		if (label.Contains("塔罗") || label.Contains("牌阵") || oracleId.Contains("tarot"))
			return "塔罗";

		if (!string.IsNullOrWhiteSpace(label))
			return label.Length > 5 ? label.Substring(0, 5) : label;

		return "塔罗";
	}

	private DateTime ParseRecordTime(DivinationRecordData record)
	{
		if (record != null && DateTime.TryParse(record.createdAt, out DateTime parsed))
			return parsed;
		return DateTime.MinValue;
	}

	private bool IsRecordComplete(DivinationRecordData record)
	{
		if (record == null) return false;

		if (!string.IsNullOrWhiteSpace(record.shortVerdict)
			|| !string.IsNullOrWhiteSpace(record.judgeContent)
			|| !string.IsNullOrWhiteSpace(record.adviceContent))
			return true;

		return record.lockedCards != null && record.lockedCards.Count > 0;
	}

	#endregion

	#region 辅助方法

	private TMP_Text FindText(Transform root, string name)
	{
		Transform target = FindDeepChild(root, name);
		return target != null ? target.GetComponent<TMP_Text>() : null;
	}

	private TMP_Text FindTagText(Transform root, string tagRootName)
	{
		Transform tagRoot = FindDeepChild(root, tagRootName);
		return tagRoot != null ? tagRoot.GetComponentInChildren<TMP_Text>(true) : null;
	}

	private Transform FindDeepChild(Transform parent, string name)
	{
		if (parent == null) return null;

		for (int i = 0; i < parent.childCount; i++)
		{
			Transform child = parent.GetChild(i);
			if (child.name == name)
				return child;

			Transform result = FindDeepChild(child, name);
			if (result != null)
				return result;
		}

		return null;
	}

	private void SetText(TMP_Text text, string value)
	{
		if (text != null)
			text.text = value ?? "";
	}

	/// <summary>
	/// 显示空状态提示
	/// </summary>
	private void ShowEmptyState()
	{
		if (_emptyStateObject == null)
			_emptyStateObject = CreateEmptyState();

		if (_emptyStateObject != null)
			_emptyStateObject.SetActive(true);
	}

	private void HideEmptyState()
	{
		if (_emptyStateObject != null)
			_emptyStateObject.SetActive(false);
	}

	private void SetLoadingVisible(bool visible)
	{
		if (uiComponent?.SpinnerDotsGameObject == null) return;
		uiComponent.SpinnerDotsGameObject.SetActive(visible);
	}

	private GameObject CreateEmptyState()
	{
		Transform parent = _historyListView != null ? _historyListView.transform : uiComponent?.transform;
		if (parent == null) return null;

		GameObject emptyGo = new GameObject("EmptyState", typeof(RectTransform), typeof(TextMeshProUGUI));
		emptyGo.transform.SetParent(parent, false);

		RectTransform rect = emptyGo.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 0.5f);
		rect.anchorMax = new Vector2(1f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = Vector2.zero;
		rect.sizeDelta = new Vector2(-80f, 120f);

		TMP_Text emptyText = emptyGo.GetComponent<TMP_Text>();
		emptyText.text = "暂无占卜记录\n完成一次占卜后会自动保存到这里";
		emptyText.font = TMP_Settings.defaultFontAsset;
		emptyText.fontSize = 28;
		emptyText.alignment = TextAlignmentOptions.Center;
		emptyText.color = itemSubTextColor;
		emptyText.raycastTarget = false;
		emptyText.enableWordWrapping = true;
		emptyText.overflowMode = TextOverflowModes.Truncate;

		return emptyGo;
	}

	#endregion

	#region UI组件事件

	public void OnBackButtonClick()
	{
		HideWindow();
	}

	#endregion
}
