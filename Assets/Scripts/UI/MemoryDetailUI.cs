/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/21/2026 7:35:11 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using UltimateClean;
using TMPro;

public class MemoryDetailUI : WindowBase
{
	public MemoryDetailUIComponent uiComponent;
	private const float DeleteConfirmSeconds = 6f;
	private string _itemId;
	private MemoryUiItem _currentItem;
	private bool _deleteConfirmArmed;
	private float _deleteConfirmDeadline;
	private bool _isRefreshingSwitches;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<MemoryDetailUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("MemoryDetailUI 缺少 UI 组件绑定脚本：MemoryDetailUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		BindSwitchButtons();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		RefreshUI();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		ResetDeleteConfirm();
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public void SetMemoryItem(string itemId)
	{
		_itemId = itemId;
		ResetDeleteConfirm();
		RefreshUI();
	}

	public void RefreshFromExternal()
	{
		RefreshUI();
	}

	private void BindSwitchButtons()
	{
		BindSwitchButton(uiComponent?.MemorySwitchSwitch, OnMemorySwitchButtonClick);
		BindSwitchButton(uiComponent?.labelSwitchSwitch, OnImportantSwitchButtonClick);
	}

	private void BindSwitchButton(Switch sw, UnityEngine.Events.UnityAction action)
	{
		if (sw == null) return;
		Button button = sw.GetComponent<Button>();
		if (button == null) return;
		button.onClick.RemoveListener(action);
		button.onClick.AddListener(action);
	}

	private void RefreshUI()
	{
		if (uiComponent == null) return;
		_currentItem = MemoryUiStore.FindItem(_itemId);
		if (_currentItem == null)
			_currentItem = MemoryUiStore.GetItems().Count > 0 ? MemoryUiStore.GetItems()[0] : null;

		if (_currentItem == null)
		{
			SetText(uiComponent.TypePillText, "暂无记忆");
			SetText(uiComponent.MemoryDateTextText, "");
			SetText(uiComponent.MemorySourceTextText, "你的记忆空间还没有内容");
			SetText(uiComponent.MemoryContentTextText, "可以从管理记忆页手动新增，或继续对话后由 AI 总结。");
			SetSwitchState(uiComponent.MemorySwitchSwitch, false);
			SetSwitchState(uiComponent.labelSwitchSwitch, false);
			return;
		}

		_itemId = _currentItem.Id;
		SetText(uiComponent.TypePillText, MemoryUiStore.GetCategoryLabel(_currentItem.Category));
		SetText(uiComponent.MemoryDateTextText, _currentItem.DateText);
		SetText(uiComponent.MemorySourceTextText, _currentItem.PendingConfirm ? $"{_currentItem.Source} · 待确认" : _currentItem.Source);
		SetText(uiComponent.MemoryContentTextText, _currentItem.Text);

		_isRefreshingSwitches = true;
		SetSwitchState(uiComponent.MemorySwitchSwitch, _currentItem.Enabled);
		SetSwitchState(uiComponent.labelSwitchSwitch, _currentItem.Important);
		_isRefreshingSwitches = false;
	}

	private void SetText(TMP_Text target, string value)
	{
		if (target != null)
			target.text = value ?? "";
	}

	private void SetSwitchState(Switch sw, bool enabled)
	{
		if (sw == null) return;
		if (sw.IsToggled() != enabled)
			sw.Toggle();
	}

	private bool TryGetCurrentItem()
	{
		_currentItem = MemoryUiStore.FindItem(_itemId);
		if (_currentItem == null)
		{
			ToastManager.ShowToast("记忆不存在");
			HideWindow();
			return false;
		}
		return true;
	}

	private void SaveAndRefresh(string toast)
	{
		MemoryUiStore.SaveCurrent(success =>
		{
			RefreshUI();
			UIModule.Instance.GetWindow<MemoryManageListUI>()?.RefreshFromExternal();
			UIModule.Instance.GetWindow<MemoryManageUI>()?.RefreshFromExternal();
			ToastManager.ShowToast(success ? toast : "已保存到本地，云端同步失败");
		});
	}

	private void ResetDeleteConfirm()
	{
		_deleteConfirmArmed = false;
		_deleteConfirmDeadline = 0f;
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnEditMemoryButtonClick()
	{
		if (!TryGetCurrentItem()) return;
		MemoryEditOverlay.Show(transform, _currentItem, result =>
		{
			MemoryUiItem updated = MemoryUiStore.UpdateMemory(_currentItem.Id, result);
			if (updated != null)
				_itemId = updated.Id;
			SaveAndRefresh("记忆已更新");
		});
	}
	public void OnDeleteMemoryButtonClick()
	{
		if (!TryGetCurrentItem()) return;

		if (!_deleteConfirmArmed || Time.time > _deleteConfirmDeadline)
		{
			_deleteConfirmArmed = true;
			_deleteConfirmDeadline = Time.time + DeleteConfirmSeconds;
			ToastManager.ShowToast("再次点击删除这条记忆");
			return;
		}

		if (!MemoryUiStore.DeleteMemory(_currentItem.Id))
		{
			ToastManager.ShowToast("记忆不存在");
			return;
		}

		_itemId = null;
		MemoryUiStore.SaveCurrent(success =>
		{
			UIModule.Instance.GetWindow<MemoryManageListUI>()?.RefreshFromExternal();
			UIModule.Instance.GetWindow<MemoryManageUI>()?.RefreshFromExternal();
			ToastManager.ShowToast(success ? "记忆已删除" : "本地已删除，云端同步失败");
			HideWindow();
		});
	}

	public void OnMemorySwitchButtonClick()
	{
		if (_isRefreshingSwitches || !TryGetCurrentItem()) return;
		bool enabled = uiComponent.MemorySwitchSwitch == null || uiComponent.MemorySwitchSwitch.IsToggled();
		bool wasPending = _currentItem.PendingConfirm;
		MemoryUiItem updated = MemoryUiStore.SetEnabled(_currentItem.Id, enabled);
		if (updated != null)
			_itemId = updated.Id;
		SaveAndRefresh(wasPending && enabled ? "记忆已确认" : enabled ? "记忆已启用" : "记忆已关闭");
	}

	public void OnImportantSwitchButtonClick()
	{
		if (_isRefreshingSwitches || !TryGetCurrentItem()) return;
		bool important = uiComponent.labelSwitchSwitch != null && uiComponent.labelSwitchSwitch.IsToggled();
		MemoryUiItem updated = MemoryUiStore.SetImportant(_currentItem.Id, important);
		if (updated != null)
			_itemId = updated.Id;
		SaveAndRefresh(important ? "已标记为重要记忆" : "已取消重要标记");
	}
	#endregion
}
