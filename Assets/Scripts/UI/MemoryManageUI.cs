/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/21/2026 6:30:18 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using System.Collections.Generic;

public class MemoryManageUI : WindowBase
{
	public MemoryManageUIComponent uiComponent;
	private const float ClearConfirmSeconds = 6f;
	private bool _clearConfirmArmed;
	private float _clearConfirmDeadline;
	private bool _isClearing;
	private LoopListView2 _memoryLoopListView;
	private readonly List<MemoryUiItem> _overviewItems = new List<MemoryUiItem>();

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<MemoryManageUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("MemoryManageUI 缺少 UI 组件绑定脚本：MemoryManageUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		BindMemoryListView();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		ResetClearConfirm();
		RefreshFromCloud();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		ResetClearConfirm();
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		_overviewItems.Clear();
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public void RefreshFromExternal()
	{
		RefreshUI();
	}

	private void RefreshFromCloud()
	{
		MemoryUiStore.LoadLatest(_ => RefreshUI());
	}

	private void RefreshUI()
	{
		if (uiComponent == null) return;

		RefreshOverviewData();
		if (uiComponent.TopicCountText != null)
			uiComponent.TopicCountText.text = MemoryUiStore.GetCount(MemoryUiCategory.Topic).ToString();
		if (uiComponent.PreferenceCountText != null)
			uiComponent.PreferenceCountText.text = MemoryUiStore.GetCount(MemoryUiCategory.Preference).ToString();
		if (uiComponent.EmotionCountText != null)
			uiComponent.EmotionCountText.text = MemoryUiStore.GetCount(MemoryUiCategory.Emotion).ToString();
		if (uiComponent.GrowthCountText != null)
			uiComponent.GrowthCountText.text = MemoryUiStore.GetCount(MemoryUiCategory.Growth).ToString();
		RefreshMemoryListView();
	}

	private void BindMemoryListView()
	{
		if (uiComponent?.MemoryOverviewScrollScrollRect == null) return;
		_memoryLoopListView = uiComponent.MemoryOverviewScrollScrollRect.GetComponent<LoopListView2>();
		if (_memoryLoopListView == null)
			Debug.LogWarning("[MemoryManageUI] MemoryOverviewScroll 没有挂 LoopListView2，记忆概览列表无法使用 SuperScrollView。");
	}

	private void RefreshOverviewData()
	{
		_overviewItems.Clear();
		_overviewItems.AddRange(MemoryUiStore.GetItems(MemoryUiCategory.All));
		if (_overviewItems.Count == 0)
		{
			_overviewItems.Add(new MemoryUiItem
			{
				Id = "",
				Text = "暂无记忆内容，继续对话后 AI 会自动总结。",
				DateText = "",
				Source = "空状态",
				Category = MemoryUiCategory.All,
				Enabled = false
			});
		}
	}

	private void RefreshMemoryListView()
	{
		if (_memoryLoopListView == null)
			BindMemoryListView();
		if (_memoryLoopListView == null) return;

		int count = Mathf.Max(1, _overviewItems.Count);
		if (!_memoryLoopListView.ListViewInited)
		{
			_memoryLoopListView.InitListView(count, OnGetMemoryItemByIndex);
			return;
		}

		_memoryLoopListView.SetListItemCount(count, false);
		_memoryLoopListView.RefreshAllShownItem();
	}

	private LoopListViewItem2 OnGetMemoryItemByIndex(LoopListView2 listView, int index)
	{
		if (index < 0 || index >= _overviewItems.Count || listView.ItemPrefabDataList.Count == 0)
			return null;

		GameObject itemPrefab = listView.ItemPrefabDataList[0].mItemPrefab;
		if (itemPrefab == null)
			return null;

		LoopListViewItem2 item = listView.NewListViewItem(itemPrefab.name);
		if (item == null) return null;

		MemorySingleItem itemScript = item.GetComponent<MemorySingleItem>();
		if (itemScript == null) return item;

		if (!item.IsInitHandlerCalled)
		{
			item.IsInitHandlerCalled = true;
			itemScript.InitListItem();
		}

		itemScript.SetListViewContext(listView, index);
		itemScript.SetItemListData(index, _overviewItems[index]);
		return item;
	}

	private void OpenList(MemoryUiCategory category)
	{
		ResetClearConfirm();
		MemoryManageListUI list = UIModule.Instance.PopUpWindow<MemoryManageListUI>();
		if (list != null)
			list.SetCategory(category);
	}

	private void ResetClearConfirm()
	{
		_clearConfirmArmed = false;
		_clearConfirmDeadline = 0f;
	}

	private void RefreshVisibleMemoryWindows()
	{
		UIModule.Instance.GetWindow<MemoryManageListUI>()?.RefreshFromExternal();
		UIModule.Instance.GetWindow<MemoryDetailUI>()?.RefreshFromExternal();
	}

	#endregion

	#region UI组件事件
	public void OnbackButtonClick()
	{
		HideWindow();
	}
	public void OnOpenPrivacySettingsButtonClick()
	{
		ResetClearConfirm();
		UIModule.Instance.PopUpWindow<MemoryPrivacySettingsUI>();
	}
	public void OnTopicMetricButtonClick()
	{
		OpenList(MemoryUiCategory.Topic);
	}
	public void OnPreferenceMetricButtonClick()
	{
		OpenList(MemoryUiCategory.Preference);
	}
	public void OnEmotionMetricButtonClick()
	{
		OpenList(MemoryUiCategory.Emotion);
	}
	public void OnGrowthMetricButtonClick()
	{
		OpenList(MemoryUiCategory.Growth);
	}
	public void OnviewAllButtonClick()
	{
		OpenList(MemoryUiCategory.All);
	}
	public void OnManageMemoryButtonClick()
	{
		OpenList(MemoryUiCategory.All);
	}
	public void OnClearAllMemoryButtonClick()
	{
		if (_isClearing) return;

		if (!_clearConfirmArmed || Time.time > _clearConfirmDeadline)
		{
			_clearConfirmArmed = true;
			_clearConfirmDeadline = Time.time + ClearConfirmSeconds;
			ToastManager.ShowToast("再次点击将清除全部记忆");
			return;
		}

		_isClearing = true;
		MemoryUiStore.ClearAll(success =>
		{
			_isClearing = false;
			ResetClearConfirm();
			RefreshUI();
			RefreshVisibleMemoryWindows();
			ToastManager.ShowToast(success ? "记忆已清空" : "本地已清空，云端稍后同步");
		});
	}
	#endregion
}
