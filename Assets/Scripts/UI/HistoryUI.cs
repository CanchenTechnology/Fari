/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 12:49:47
 * Description: UI 表现层 —— 占卜历史记录列表
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using XFGameFrameWork;

public class HistoryUI : WindowBase
{
	public HistoryUIComponent uiComponent;

	/// <summary>当前选中的占卜记录（打开详情前设置）</summary>
	public static DivinationRecordData SelectedRecord { get; set; }

	/// <summary>已加载的记录列表</summary>
	private List<DivinationRecordData> _records = new List<DivinationRecordData>();

	/// <summary>动态生成的列表项（用于清理）</summary>
	private List<GameObject> _itemObjects = new List<GameObject>();

	/// <summary>是否正在加载</summary>
	private bool _isLoading = false;

	/// <summary>列表内容容器 Transform</summary>
	private RectTransform _contentRect;

	/// <summary>列表项预制体（如果手动拖入则使用，否则动态创建）</summary>
	[Header("列表配置")]
	public GameObject recordItemPrefab;          // 可选预制体
	public float itemHeight = 120f;              // 每个列表项高度
	public float itemSpacing = 12f;              // 列表项间距
	public Color itemBgColor = new Color(0.15f, 0.12f, 0.22f, 1f);
	public Color itemTextColor = new Color(0.9f, 0.88f, 0.95f, 1f);
	public Color itemSubTextColor = new Color(0.65f, 0.62f, 0.75f, 1f);

	#region 生命周期函数

	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<HistoryUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();

		// 缓存 Content RectTransform
		if (uiComponent.HistoryListScrollScrollRect != null)
			_contentRect = uiComponent.HistoryListScrollScrollRect.content;
	}

	public override void OnShow()
	{
		base.OnShow();
		RefreshList();
	}

	public override void OnHide()
	{
		base.OnHide();
	}

	public override void OnDestroy()
	{
		ClearItems();
		base.OnDestroy();
	}

	#endregion

	#region API Function

	/// <summary>
	/// 刷新列表（从 Firestore 重新加载）
	/// </summary>
	public void RefreshList()
	{
		if (_isLoading) return;

		var firestore = DivinationRecordFirestore.Instance;
		if (firestore == null)
		{
			var go = new GameObject("DivinationRecordFirestore");
			firestore = go.AddComponent<DivinationRecordFirestore>();
		}

		LoadFromFirestore();
	}

	/// <summary>
	/// 从 Firestore 加载并渲染列表
	/// </summary>
	private void LoadFromFirestore()
	{
		_isLoading = true;

		// 可选：显示加载转圈
		// ShowLoadingIndicator();

		DivinationRecordFirestore.Instance.LoadAllRecords(records =>
		{
			_isLoading = false;

			if (records == null) records = new List<DivinationRecordData>();
			_records = records;
			ValidateSelectedRecord();

			RenderList();

			Debug.Log($"[HistoryUI] 渲染了 {records.Count} 条记录");
			if (records.Count > 0 && !DivinationRecordFirestore.Instance.IsReady)
				ToastManager.ShowToast("已显示本地历史缓存");
		});
	}

	/// <summary>
	/// 渲染列表项
	/// </summary>
	private void RenderList()
	{
		ClearItems();

		if (_records == null || _records.Count == 0)
		{
			// 显示空状态
			ShowEmptyState();
			SetContentHeight(0);
			return;
		}

		// 隐藏空状态占位（如果有的话）
		HideEmptyState();

		for (int i = 0; i < _records.Count; i++)
		{
			var record = _records[i];
			GameObject item = CreateRecordItem(record, i);
			if (item != null)
			{
				_itemObjects.Add(item);
			}
		}

		// 计算 Content 总高度
		float totalHeight = _records.Count * (itemHeight + itemSpacing) + itemSpacing;
		SetContentHeight(totalHeight);
	}

	#endregion

	#region 列表项创建

	/// <summary>
	/// 创建单条记录 UI
	/// </summary>
	private GameObject CreateRecordItem(DivinationRecordData record, int index)
	{
		if (_contentRect == null) return null;

		// --- 1. 创建根物体 ---
		GameObject go;
		if (recordItemPrefab != null)
		{
			go = GameObject.Instantiate(recordItemPrefab, _contentRect);
		}
		else
		{
			go = new GameObject($"RecordItem_{index}", typeof(RectTransform));
			go.transform.SetParent(_contentRect, false);
			go.transform.localScale = Vector3.one;

			RectTransform rt = go.GetComponent<RectTransform>();
			rt.anchorMin = new Vector2(0f, 1f);
			rt.anchorMax = new Vector2(1f, 1f);
			rt.pivot = new Vector2(0.5f, 1f);
			float yOffset = index * (itemHeight + itemSpacing) + itemSpacing;
			rt.anchoredPosition = new Vector2(0f, -yOffset);
			rt.sizeDelta = new Vector2(0f, itemHeight);

			// 背景图 (用于点击检测 + 视觉)
			Image bg = go.AddComponent<Image>();
			bg.color = itemBgColor;

			// Button 组件（可点击跳转详情）
			Button btn = go.AddComponent<Button>();
			btn.targetGraphic = bg;
			ColorBlock cb = btn.colors;
			cb.normalColor = itemBgColor;
			cb.highlightedColor = itemBgColor * 1.2f;
			cb.pressedColor = new Color(0.12f, 0.09f, 0.18f, 1f);
			btn.colors = cb;

			// --- 2. 牌阵标签（左上小字） ---
			GameObject spreadLabelGo = new GameObject("SpreadLabel", typeof(RectTransform));
			spreadLabelGo.transform.SetParent(go.transform, false);
			RectTransform slRt = spreadLabelGo.GetComponent<RectTransform>();
			slRt.anchorMin = new Vector2(0f, 1f);
			slRt.anchorMax = new Vector2(1f, 1f);
			slRt.pivot = new Vector2(0f, 1f);
			slRt.anchoredPosition = new Vector2(16f, -12f);
			slRt.sizeDelta = new Vector2(0f, 22f);
			Text spreadLabel = spreadLabelGo.AddComponent<Text>();
			spreadLabel.text = record.SpreadLabel;
			spreadLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			spreadLabel.fontSize = 14;
			spreadLabel.color = new Color(0.75f, 0.55f, 0.9f, 1f);
			spreadLabel.alignment = TextAnchor.MiddleLeft;

			// --- 3. 问题文本（主标题） ---
			GameObject questionGo = new GameObject("Question", typeof(RectTransform));
			questionGo.transform.SetParent(go.transform, false);
			RectTransform qRt = questionGo.GetComponent<RectTransform>();
			qRt.anchorMin = new Vector2(0f, 1f);
			qRt.anchorMax = new Vector2(1f, 1f);
			qRt.pivot = new Vector2(0f, 1f);
			qRt.anchoredPosition = new Vector2(16f, -40f);
			qRt.sizeDelta = new Vector2(-32f, 40f);
			Text questionText = questionGo.AddComponent<Text>();
			questionText.text = record.QuestionPreview;
			questionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			questionText.fontSize = 18;
			questionText.fontStyle = FontStyle.Bold;
			questionText.color = itemTextColor;
			questionText.alignment = TextAnchor.MiddleLeft;

			// --- 4. 卡牌摘要（副标题） ---
			GameObject cardsGo = new GameObject("CardsSummary", typeof(RectTransform));
			cardsGo.transform.SetParent(go.transform, false);
			RectTransform cRt = cardsGo.GetComponent<RectTransform>();
			cRt.anchorMin = new Vector2(0f, 1f);
			cRt.anchorMax = new Vector2(1f, 1f);
			cRt.pivot = new Vector2(0f, 1f);
			cRt.anchoredPosition = new Vector2(16f, -72f);
			cRt.sizeDelta = new Vector2(-32f, 22f);
			Text cardsText = cardsGo.AddComponent<Text>();
			cardsText.text = record.CardsSummary;
			cardsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			cardsText.fontSize = 14;
			cardsText.color = itemSubTextColor;
			cardsText.alignment = TextAnchor.MiddleLeft;

			// --- 5. 时间标签（右下） ---
			GameObject timeGo = new GameObject("DateTime", typeof(RectTransform));
			timeGo.transform.SetParent(go.transform, false);
			RectTransform tRt = timeGo.GetComponent<RectTransform>();
			tRt.anchorMin = new Vector2(1f, 1f);
			tRt.anchorMax = new Vector2(1f, 1f);
			tRt.pivot = new Vector2(1f, 1f);
			tRt.anchoredPosition = new Vector2(-16f, -12f);
			tRt.sizeDelta = new Vector2(120f, 22f);
			Text timeText = timeGo.AddComponent<Text>();
			timeText.text = record.DisplayTime;
			timeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			timeText.fontSize = 13;
			timeText.color = itemSubTextColor;
			timeText.alignment = TextAnchor.MiddleRight;

			// --- 6. 绑定点击跳转到详情页 ---
			var capturedRecord = record;
			btn.onClick.AddListener(() => OnRecordItemClick(capturedRecord));
		}

		return go;
	}

	/// <summary>
	/// 列表项点击 → 打开占卜详情页
	/// </summary>
	private void OnRecordItemClick(DivinationRecordData record)
	{
		if (record == null) return;

		Debug.Log($"[HistoryUI] 点击记录: {record.readingId} - {record.QuestionPreview}");

		// 通过静态变量传递选中的记录给 DivinationRecordUI
		SelectedRecord = record;
		UIModule.Instance.PopUpWindow<DivinationRecordUI>();
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

	#region 辅助方法

	/// <summary>
	/// 清理所有列表项
	/// </summary>
	private void ClearItems()
	{
		foreach (var obj in _itemObjects)
		{
			if (obj != null)
				GameObject.Destroy(obj);
		}
		_itemObjects.Clear();
	}

	/// <summary>
	/// 设置 ScrollRect Content 高度
	/// </summary>
	private void SetContentHeight(float height)
	{
		if (_contentRect != null)
		{
			_contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
		}
	}

	/// <summary>
	/// 显示空状态提示
	/// 如果 Prefab 中有一个 EmptyState 子物体，就显示它；否则动态创建一个临时 Text
	/// </summary>
	private void ShowEmptyState()
	{
		if (uiComponent == null) return;

		Transform empty = uiComponent.transform.Find("EmptyState");
		if (empty != null)
		{
			empty.gameObject.SetActive(true);
			return;
		}

		// 动态创建空状态提示
		if (_contentRect != null)
		{
			GameObject emptyGo = new GameObject("EmptyState_Temp", typeof(RectTransform));
			emptyGo.transform.SetParent(_contentRect, false);
			RectTransform ert = emptyGo.GetComponent<RectTransform>();
			ert.anchorMin = new Vector2(0f, 0.5f);
			ert.anchorMax = new Vector2(1f, 0.5f);
			ert.sizeDelta = new Vector2(0f, 60f);
			ert.anchoredPosition = Vector2.zero;

			Text emptyText = emptyGo.AddComponent<Text>();
			emptyText.text = "暂无占卜记录\n开始你的第一次占卜吧 ✨";
			emptyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			emptyText.fontSize = 16;
			emptyText.alignment = TextAnchor.MiddleCenter;
			emptyText.color = itemSubTextColor;
			_itemObjects.Add(emptyGo); // 确保清理时也会删除
		}
	}

	private void HideEmptyState()
	{
		if (uiComponent == null) return;

		Transform empty = uiComponent.transform.Find("EmptyState");
		if (empty != null)
		{
			empty.gameObject.SetActive(false);
		}
	}

	#endregion

	#region UI组件事件

	public void OnviewButtonClick()
	{
		// 如果已有选中的记录，直接打开详情
		// 否则打开最新的第一条记录
		if (SelectedRecord != null)
		{
			UIModule.Instance.PopUpWindow<DivinationRecordUI>();
		}
		else if (_records != null && _records.Count > 0)
		{
			SelectedRecord = _records[0];
			UIModule.Instance.PopUpWindow<DivinationRecordUI>();
		}
		else
		{
			Debug.Log("[HistoryUI] 没有可用记录");
			ToastManager.ShowToast("暂无占卜记录");
		}
	}

	public void OnBackButtonClick()
	{
		HideWindow();
	}

	#endregion
}
