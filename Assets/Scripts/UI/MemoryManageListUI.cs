/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/21/2026 7:00:06 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using System.Collections.Generic;

public class MemoryManageListUI : WindowBase
{
	public MemoryManageListUIComponent uiComponent;
	private readonly List<GameObject> _runtimeRows = new List<GameObject>();
	private MemoryUiCategory _currentCategory = MemoryUiCategory.All;
	private string _searchText = "";
	private bool _isRefreshing;
	private bool _templateChildrenHidden;
	private RectTransform _contentRect;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<MemoryManageListUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("MemoryManageListUI 缺少 UI 组件绑定脚本：MemoryManageListUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		_contentRect = uiComponent.MemoryListScrollScrollRect != null
			? uiComponent.MemoryListScrollScrollRect.content
			: null;
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		MemoryUiStore.LoadLatest(_ => RefreshUI());
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		ClearRuntimeRows();
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public void SetCategory(MemoryUiCategory category)
	{
		_currentCategory = category;
		RefreshUI();
	}

	public void RefreshFromExternal()
	{
		RefreshUI();
	}

	private void RefreshUI()
	{
		if (uiComponent == null) return;

		_isRefreshing = true;
		if (uiComponent.TabAllToggle != null) uiComponent.TabAllToggle.isOn = _currentCategory == MemoryUiCategory.All;
		if (uiComponent.TabTopicToggle != null) uiComponent.TabTopicToggle.isOn = _currentCategory == MemoryUiCategory.Topic;
		if (uiComponent.TabPreferenceToggle != null) uiComponent.TabPreferenceToggle.isOn = _currentCategory == MemoryUiCategory.Preference;
		if (uiComponent.TabEmotionToggle != null) uiComponent.TabEmotionToggle.isOn = _currentCategory == MemoryUiCategory.Emotion;
		if (uiComponent.TabGrowthToggle != null) uiComponent.TabGrowthToggle.isOn = _currentCategory == MemoryUiCategory.Growth;
		_isRefreshing = false;

		RenderList();
	}

	private void RenderList()
	{
		PrepareContent();
		ClearRuntimeRows();
		if (_contentRect == null) return;

		List<MemoryUiItem> items = MemoryUiStore.GetItems(_currentCategory, _searchText);
		float y = 16f;
		if (items.Count == 0)
		{
			AddEmptyState();
			SetContentHeight(640f);
			return;
		}

		for (int i = 0; i < items.Count; i++)
		{
			AddMemoryRow(items[i], ref y);
			y += 22f;
		}

		SetContentHeight(y + 120f);
	}

	private void PrepareContent()
	{
		if (_contentRect == null && uiComponent?.MemoryListScrollScrollRect != null)
			_contentRect = uiComponent.MemoryListScrollScrollRect.content;
		if (_contentRect == null || _templateChildrenHidden) return;

		for (int i = 0; i < _contentRect.childCount; i++)
			_contentRect.GetChild(i).gameObject.SetActive(false);
		_templateChildrenHidden = true;
	}

	private void AddEmptyState()
	{
		GameObject go = CreatePanel("EmptyMemoryState", _contentRect, new Color(0.040f, 0.038f, 0.073f, 0.92f));
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.anchoredPosition = new Vector2(0, -24);
		rect.sizeDelta = new Vector2(960, 360);
		CreateText("EmptyIcon", go.transform, "◇", 66, FontStyle.Normal, new Color(0.48f, 0.39f, 0.60f, 1f), new Vector2(0, 70), new Vector2(140, 90), TextAnchor.MiddleCenter);
		CreateText("EmptyText", go.transform, "暂无记忆内容", 36, FontStyle.Bold, new Color(0.78f, 0.75f, 0.82f, 1f), new Vector2(0, -18), new Vector2(420, 60), TextAnchor.MiddleCenter);
		CreateText("EmptyHint", go.transform, "可以手动新增，或继续对话后由 AI 总结。", 28, FontStyle.Normal, new Color(0.58f, 0.56f, 0.64f, 1f), new Vector2(0, -82), new Vector2(700, 46), TextAnchor.MiddleCenter);
		_runtimeRows.Add(go);
	}

	private void AddMemoryRow(MemoryUiItem item, ref float y)
	{
		float rowHeight = 190f;
		string stateLabel = string.IsNullOrEmpty(item.StatusLabel)
			? item.Enabled ? "已启用" : "已关闭"
			: item.StatusLabel;
		bool activeOrPending = item.Enabled || item.PendingConfirm;
		GameObject row = CreatePanel("MemoryRow_" + item.Id, _contentRect,
			activeOrPending ? new Color(0.040f, 0.038f, 0.073f, 0.92f) : new Color(0.032f, 0.030f, 0.052f, 0.78f));
		RectTransform rect = row.GetComponent<RectTransform>();
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
		rect.pivot = new Vector2(0.5f, 1f);
		rect.anchoredPosition = new Vector2(0, -y);
		rect.sizeDelta = new Vector2(960, rowHeight);

		Button rowButton = row.AddComponent<Button>();
		rowButton.targetGraphic = row.GetComponent<Image>();
		rowButton.onClick.AddListener(() => OpenDetail(item.Id));

		CreateText("Category", row.transform, MemoryUiStore.GetCategoryLabel(item.Category), 26, FontStyle.Normal, new Color(0.74f, 0.45f, 0.96f, 1f), new Vector2(-360, 52), new Vector2(190, 38), TextAnchor.MiddleLeft);
		CreateText("Content", row.transform, item.Text, 34, FontStyle.Normal, activeOrPending ? new Color(0.90f, 0.88f, 0.92f, 1f) : new Color(0.58f, 0.56f, 0.64f, 1f), new Vector2(-105, 8), new Vector2(640, 58), TextAnchor.MiddleLeft);
		CreateText("Meta", row.transform, $"{item.Source} · {item.DateText}", 25, FontStyle.Normal, item.PendingConfirm ? new Color(0.94f, 0.73f, 0.46f, 1f) : new Color(0.55f, 0.53f, 0.62f, 1f), new Vector2(-215, -54), new Vector2(520, 38), TextAnchor.MiddleLeft);

		if (item.Important)
			CreateText("Important", row.transform, "★", 34, FontStyle.Normal, new Color(0.94f, 0.73f, 0.46f, 1f), new Vector2(210, 50), new Vector2(52, 52), TextAnchor.MiddleCenter);

		Button enableButton = CreateButton(row.transform, stateLabel, 23, new Vector2(330, 38), new Vector2(118, 54), item.PendingConfirm ? new Color(0.44f, 0.28f, 0.12f, 0.94f) : item.Enabled ? new Color(0.34f, 0.18f, 0.64f, 0.92f) : new Color(0.080f, 0.060f, 0.095f, 0.90f), activeOrPending ? new Color(0.92f, 0.86f, 0.94f, 1f) : new Color(0.66f, 0.63f, 0.70f, 1f));
		enableButton.onClick.AddListener(() => ToggleMemory(item));

		Button editButton = CreateButton(row.transform, "✎", 30, new Vector2(410, -48), new Vector2(58, 58), new Color(0.070f, 0.055f, 0.092f, 0.88f), new Color(0.90f, 0.70f, 0.45f, 1f));
		editButton.onClick.AddListener(() => EditMemory(item));

		Button deleteButton = CreateButton(row.transform, "⌫", 30, new Vector2(478, -48), new Vector2(58, 58), new Color(0.070f, 0.055f, 0.092f, 0.88f), new Color(0.90f, 0.38f, 0.34f, 1f));
		deleteButton.onClick.AddListener(() => DeleteMemory(item));

		_runtimeRows.Add(row);
		y += rowHeight;
	}

	private void OpenDetail(string itemId)
	{
		MemoryDetailUI detail = UIModule.Instance.PopUpWindow<MemoryDetailUI>();
		if (detail != null)
			detail.SetMemoryItem(itemId);
	}

	private void ToggleMemory(MemoryUiItem item)
	{
		bool nextEnabled = item.PendingConfirm || !item.Enabled;
		MemoryUiStore.SetEnabled(item.Id, nextEnabled);
		SaveAndRefresh(item.PendingConfirm ? "记忆已确认" : item.Enabled ? "记忆已关闭" : "记忆已启用");
	}

	private void EditMemory(MemoryUiItem item)
	{
		MemoryEditOverlay.Show(transform, item, result =>
		{
			MemoryUiStore.UpdateMemory(item.Id, result);
			SaveAndRefresh("记忆已更新");
		});
	}

	private void DeleteMemory(MemoryUiItem item)
	{
		if (!MemoryUiStore.DeleteMemory(item.Id))
		{
			ToastManager.ShowToast("记忆不存在");
			return;
		}
		SaveAndRefresh("记忆已删除");
	}

	private void SaveAndRefresh(string toast)
	{
		MemoryUiStore.SaveCurrent(success =>
		{
			RenderList();
			UIModule.Instance.GetWindow<MemoryManageUI>()?.RefreshFromExternal();
			UIModule.Instance.GetWindow<MemoryDetailUI>()?.RefreshFromExternal();
			ToastManager.ShowToast(success ? toast : "已保存到本地，云端同步失败");
		});
	}

	private void ClearRuntimeRows()
	{
		foreach (GameObject row in _runtimeRows)
		{
			if (row != null)
				GameObject.Destroy(row);
		}
		_runtimeRows.Clear();
	}

	private void SetContentHeight(float height)
	{
		if (_contentRect != null)
			_contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(640f, height));
	}

	private GameObject CreatePanel(string name, Transform parent, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		go.transform.SetParent(parent, false);
		go.GetComponent<Image>().color = color;
		return go;
	}

	private Text CreateText(string name, Transform parent, string text, int fontSize, FontStyle fontStyle, Color color, Vector2 position, Vector2 size, TextAnchor alignment)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = position;
		rect.sizeDelta = size;

		Text label = go.GetComponent<Text>();
		label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		label.text = text ?? "";
		label.fontSize = fontSize;
		label.fontStyle = fontStyle;
		label.color = color;
		label.alignment = alignment;
		label.horizontalOverflow = HorizontalWrapMode.Wrap;
		label.verticalOverflow = VerticalWrapMode.Truncate;
		label.raycastTarget = false;
		return label;
	}

	private Button CreateButton(Transform parent, string text, int fontSize, Vector2 position, Vector2 size, Color bgColor, Color textColor)
	{
		GameObject go = CreatePanel(text + "Button", parent, bgColor);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = position;
		rect.sizeDelta = size;

		Button button = go.AddComponent<Button>();
		button.targetGraphic = go.GetComponent<Image>();
		CreateText("Text", go.transform, text, fontSize, FontStyle.Normal, textColor, Vector2.zero, size, TextAnchor.MiddleCenter);
		return button;
	}

	#endregion

	#region UI组件事件	
	public void OnTabGrowthToggleChange(bool state, Toggle toggle)
	{
		if (!_isRefreshing && state) SetCategory(MemoryUiCategory.Growth);
	}
	public void OnTabEmotionToggleChange(bool state, Toggle toggle)
	{
		if (!_isRefreshing && state) SetCategory(MemoryUiCategory.Emotion);
	}
	public void OnTabPreferenceToggleChange(bool state, Toggle toggle)
	{
		if (!_isRefreshing && state) SetCategory(MemoryUiCategory.Preference);
	}
	public void OnTabTopicToggleChange(bool state, Toggle toggle)
	{
		if (!_isRefreshing && state) SetCategory(MemoryUiCategory.Topic);
	}
	public void OnTabAllToggleChange(bool state, Toggle toggle)
	{
		if (!_isRefreshing && state) SetCategory(MemoryUiCategory.All);
	}
	public void OnCategoryTabsToggleChange(bool state, Toggle toggle)
	{

	}

	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnMemorySearchInputInputChange(string text)
	{
		_searchText = text ?? "";
		RenderList();
	}
	public void OnMemorySearchInputInputEnd(string text)
	{
		_searchText = text ?? "";
		RenderList();
	}
	public void OnTabAllButtonClick()
	{
		SetCategory(MemoryUiCategory.All);
	}
	public void OnTabTopicButtonClick()
	{
		SetCategory(MemoryUiCategory.Topic);
	}
	public void OnTabPreferenceButtonClick()
	{
		SetCategory(MemoryUiCategory.Preference);
	}
	public void OnTabEmotionButtonClick()
	{
		SetCategory(MemoryUiCategory.Emotion);
	}
	public void OnTabGrowthButtonClick()
	{
		SetCategory(MemoryUiCategory.Growth);
	}
	public void OnAddMemoryButtonClick()
	{
		MemoryEditOverlay.Show(transform, null, result =>
		{
			MemoryUiStore.AddManualMemory(result);
			_currentCategory = result.Category;
			SaveAndRefresh("记忆已添加");
		});
	}
	#endregion
}
