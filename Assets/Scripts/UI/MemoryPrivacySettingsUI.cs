/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/21/2026 7:53:54 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using UltimateClean;

public class MemoryPrivacySettingsUI : WindowBase
{
	public MemoryPrivacySettingsUIComponent uiComponent;
	private GameObject _clearConfirmModal;
	private bool _isRefreshing;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<MemoryPrivacySettingsUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("MemoryPrivacySettingsUI 缺少 UI 组件绑定脚本：MemoryPrivacySettingsUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		BindSwitchButtons();
		BindOptionalButtons();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		BindOptionalButtons();
		HideClearConfirm();
		RefreshUI();
		MemoryPrivacySettings.LoadFromCloud(_ => RefreshUI());
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function
	private void BindSwitchButtons()
	{
		BindSwitchButton(uiComponent?.autoRecordSwitch, OnAutoTopicSwitchClick);
		BindSwitchButton(uiComponent?.recordPersonalPreferencesSwitch, OnPreferenceSwitchClick);
		BindSwitchButton(uiComponent?.RecordingEmotionalPatternsSwitch, OnEmotionSwitchClick);
		BindSwitchButton(uiComponent?.RecordingGrowthTrajectorySwitch, OnGrowthSwitchClick);
		BindSwitchButton(uiComponent?.AddMemorySwitch, OnRequireConfirmSwitchClick);
	}

	private void BindSwitchButton(Switch sw, UnityEngine.Events.UnityAction action)
	{
		if (sw == null) return;
		Button button = sw.GetComponent<Button>();
		if (button == null) return;
		button.onClick.RemoveListener(action);
		button.onClick.AddListener(action);
	}

	private void BindOptionalButtons()
	{
		Button showClear = FindButton("ShowClearConfirm");
		if (showClear != null)
		{
			showClear.onClick.RemoveListener(ShowClearConfirm);
			showClear.onClick.AddListener(ShowClearConfirm);
		}

		Button cancelClear = FindButton("CancelClear");
		if (cancelClear != null)
		{
			cancelClear.onClick.RemoveListener(HideClearConfirm);
			cancelClear.onClick.AddListener(HideClearConfirm);
		}

		Button confirmClear = FindButton("ConfirmClear");
		if (confirmClear != null)
		{
			confirmClear.onClick.RemoveListener(ConfirmClearAllMemory);
			confirmClear.onClick.AddListener(ConfirmClearAllMemory);
		}

		if (_clearConfirmModal == null)
			_clearConfirmModal = FindObjectByName("ClearConfirmModal");
	}

	private void RefreshUI()
	{
		_isRefreshing = true;
		SetSwitchState(uiComponent?.autoRecordSwitch, MemoryPrivacySettings.AutoTopicEnabled);
		SetSwitchState(uiComponent?.recordPersonalPreferencesSwitch, MemoryPrivacySettings.AutoPreferenceEnabled);
		SetSwitchState(uiComponent?.RecordingEmotionalPatternsSwitch, MemoryPrivacySettings.AutoEmotionEnabled);
		SetSwitchState(uiComponent?.RecordingGrowthTrajectorySwitch, MemoryPrivacySettings.AutoGrowthEnabled);
		SetSwitchState(uiComponent?.AddMemorySwitch, MemoryPrivacySettings.RequireConfirmBeforeAdd);
		_isRefreshing = false;
	}

	private void SetSwitchState(Switch sw, bool enabled)
	{
		if (sw == null) return;
		if (sw.IsToggled() != enabled)
			sw.Toggle();
	}

	private Button FindButton(string shortName)
	{
		Button[] buttons = gameObject.GetComponentsInChildren<Button>(true);
		foreach (Button button in buttons)
		{
			if (button == null) continue;
			string objectName = button.gameObject.name;
			if (objectName == shortName || objectName == "[Button]" + shortName)
				return button;
		}
		return null;
	}

	private GameObject FindObjectByName(string shortName)
	{
		Transform[] children = gameObject.GetComponentsInChildren<Transform>(true);
		foreach (Transform child in children)
		{
			if (child == null) continue;
			string objectName = child.gameObject.name;
			if (objectName == shortName || objectName == "[Panel]" + shortName)
				return child.gameObject;
		}
		return null;
	}

	private void ShowClearConfirm()
	{
		if (_clearConfirmModal != null)
		{
			_clearConfirmModal.SetActive(true);
			return;
		}

		ToastManager.ShowToast("清空确认弹窗还未生成");
	}

	private void HideClearConfirm()
	{
		if (_clearConfirmModal != null)
			_clearConfirmModal.SetActive(false);
	}

	private void ConfirmClearAllMemory()
	{
		MemoryUiStore.ClearAll(success =>
		{
			HideClearConfirm();
			UIModule.Instance.GetWindow<MemoryManageUI>()?.RefreshFromExternal();
			UIModule.Instance.GetWindow<MemoryManageListUI>()?.RefreshFromExternal();
			UIModule.Instance.GetWindow<MemoryDetailUI>()?.RefreshFromExternal();
			ToastManager.ShowToast(success ? "记忆已清空" : "本地已清空，云端删除失败");
		});
	}

	private void SaveSettingToast(string text)
	{
		PlayerPrefs.Save();
		ToastManager.ShowToast(text);
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}

	public void OnAutoTopicSwitchClick()
	{
		if (_isRefreshing) return;
		MemoryPrivacySettings.AutoTopicEnabled = uiComponent?.autoRecordSwitch == null || uiComponent.autoRecordSwitch.IsToggled();
		SaveSettingToast(MemoryPrivacySettings.AutoTopicEnabled ? "已开启对话主题记忆" : "已关闭对话主题记忆");
	}

	public void OnPreferenceSwitchClick()
	{
		if (_isRefreshing) return;
		MemoryPrivacySettings.AutoPreferenceEnabled = uiComponent?.recordPersonalPreferencesSwitch == null || uiComponent.recordPersonalPreferencesSwitch.IsToggled();
		SaveSettingToast(MemoryPrivacySettings.AutoPreferenceEnabled ? "已开启个人偏好记忆" : "已关闭个人偏好记忆");
	}

	public void OnEmotionSwitchClick()
	{
		if (_isRefreshing) return;
		MemoryPrivacySettings.AutoEmotionEnabled = uiComponent?.RecordingEmotionalPatternsSwitch == null || uiComponent.RecordingEmotionalPatternsSwitch.IsToggled();
		SaveSettingToast(MemoryPrivacySettings.AutoEmotionEnabled ? "已开启情感模式记忆" : "已关闭情感模式记忆");
	}

	public void OnGrowthSwitchClick()
	{
		if (_isRefreshing) return;
		MemoryPrivacySettings.AutoGrowthEnabled = uiComponent?.RecordingGrowthTrajectorySwitch != null && uiComponent.RecordingGrowthTrajectorySwitch.IsToggled();
		SaveSettingToast(MemoryPrivacySettings.AutoGrowthEnabled ? "已开启成长轨迹记忆" : "已关闭成长轨迹记忆");
	}

	public void OnRequireConfirmSwitchClick()
	{
		if (_isRefreshing) return;
		MemoryPrivacySettings.RequireConfirmBeforeAdd = uiComponent?.AddMemorySwitch == null || uiComponent.AddMemorySwitch.IsToggled();
		SaveSettingToast(MemoryPrivacySettings.RequireConfirmBeforeAdd ? "新增记忆将需要确认" : "新增记忆不再二次确认");
	}
	#endregion
}
