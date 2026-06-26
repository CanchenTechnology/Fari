/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/19/2026 3:15:18 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class DailyDivinationSyncSettingsUI : WindowBase
{
	public DailyDivinationSyncSettingsUIComponent uiComponent;
	private const string PrivacyNoticeAcceptedKey = "DailyDivinationSyncSettings_PrivacyNoticeAccepted";
	private bool _isRefreshing;
	private bool _isSaving;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<DailyDivinationSyncSettingsUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("DailyDivinationSyncSettingsUI 缺少 UI 组件绑定脚本：DailyDivinationSyncSettingsUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		AddButtonClickListener(uiComponent.privacyBtn, OnPrivacyNoticeButtonClick);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		LoadSettingsThenRefresh();
		SetPrivacyNoticeVisible(ShouldShowPrivacyNotice());
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
	private DailyDivinationSyncSettingsManager SettingsManager => DailyDivinationSyncSettingsManager.Instance;

	private void LoadSettingsThenRefresh()
	{
		if (SettingsManager == null)
		{
			RefreshUI();
			return;
		}

		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			RefreshUI();
			return;
		}

		if (SettingsManager.HasPendingCloudSync)
		{
			RefreshUI();
			SaveSettings(false);
			return;
		}

		firestore.LoadDailyDivinationSyncSettings(cloud =>
		{
			if (cloud != null)
			{
				SettingsManager.ApplySettings(cloud.enabled, cloud.visibility);
			}

			RefreshUI();
		});
	}

	private void RefreshUI()
	{
		if (uiComponent == null || SettingsManager == null) return;

		_isRefreshing = true;
		SetSwitchState(SettingsManager.Enabled);

		if (uiComponent.VisibilityAllFriendsToggle != null)
			uiComponent.VisibilityAllFriendsToggle.isOn = SettingsManager.Visibility == DailyDivinationSyncVisibility.AllFriends;
		if (uiComponent.VisibilityRealFriendsToggle != null)
			uiComponent.VisibilityRealFriendsToggle.isOn = SettingsManager.Visibility == DailyDivinationSyncVisibility.RealFriends;
		if (uiComponent.MeibilityOnlyMeToggle != null)
			uiComponent.MeibilityOnlyMeToggle.isOn = SettingsManager.Visibility == DailyDivinationSyncVisibility.OnlyMe;

		SetVisibilityInteractable(SettingsManager.Enabled);
		if (uiComponent.SaveSettingsButton != null)
			uiComponent.SaveSettingsButton.interactable = !_isSaving;

		_isRefreshing = false;
	}

	private void SetSwitchState(bool enabled)
	{
		if (uiComponent?.SwitchSwitch == null) return;
		if (uiComponent.SwitchSwitch.IsToggled() != enabled)
		{
			uiComponent.SwitchSwitch.Toggle();
		}
	}

	private void SetVisibilityInteractable(bool interactable)
	{
		if (uiComponent.VisibilityAllFriendsToggle != null)
			uiComponent.VisibilityAllFriendsToggle.interactable = interactable;
		if (uiComponent.VisibilityRealFriendsToggle != null)
			uiComponent.VisibilityRealFriendsToggle.interactable = interactable;
		if (uiComponent.MeibilityOnlyMeToggle != null)
			uiComponent.MeibilityOnlyMeToggle.interactable = interactable;
	}

	private void SaveSettings(bool showToast = true)
	{
		if (_isSaving || SettingsManager == null) return;

		_isSaving = true;
		RefreshUI();

		var settings = SettingsManager.GetSettings();
		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			SettingsManager.MarkCloudSyncPending();
			_isSaving = false;
			RefreshUI();
			if (showToast)
			{
				ToastManager.ShowToast("已保存到本地，云端稍后同步");
			}
			return;
		}

		firestore.SaveDailyDivinationSyncSettings(settings, success =>
		{
			if (!success)
			{
				SettingsManager.MarkCloudSyncPending();
				_isSaving = false;
				RefreshUI();
				if (showToast)
				{
					ToastManager.ShowToast("已保存到本地，云端稍后同步");
				}
				return;
			}

			SettingsManager.MarkCloudSyncComplete();
			UpdatePublishedSummaries(settings, showToast);
		});
	}

	private void UpdatePublishedSummaries(DailyDivinationSyncSettings settings, bool showToast)
	{
		var store = DailyOracleFirestore.Instance;
		if (store == null || !store.IsReady)
		{
			_isSaving = false;
			RefreshUI();
			if (showToast)
			{
				ToastManager.ShowToast("设置已保存");
			}
			return;
		}

		store.ApplySyncSettingsToPublishedSummaries(settings, 30, success => OnSummaryUpdated(success, showToast));
	}

	private void OnSummaryUpdated(bool success, bool showToast)
	{
		_isSaving = false;
		RefreshUI();
		if (showToast)
		{
			ToastManager.ShowToast(success ? "每日占卜同步设置已保存" : "设置已保存，今天还没有可同步的每日牌");
		}
	}

	private DailyDivinationSyncVisibility GetSelectedVisibility()
	{
		if (uiComponent.VisibilityAllFriendsToggle != null && uiComponent.VisibilityAllFriendsToggle.isOn)
			return DailyDivinationSyncVisibility.AllFriends;
		if (uiComponent.VisibilityRealFriendsToggle != null && uiComponent.VisibilityRealFriendsToggle.isOn)
			return DailyDivinationSyncVisibility.RealFriends;
		return DailyDivinationSyncVisibility.OnlyMe;
	}

	private bool ShouldShowPrivacyNotice()
	{
		return PlayerPrefs.GetInt(PrivacyNoticeAcceptedKey, 0) == 0;
	}

	private void SetPrivacyNoticeVisible(bool visible)
	{
		if (uiComponent?.privacyNoticePanel != null)
		{
			uiComponent.privacyNoticePanel.SetActive(visible);
		}
	}

	private void AcceptPrivacyNotice()
	{
		PlayerPrefs.SetInt(PrivacyNoticeAcceptedKey, 1);
		PlayerPrefs.Save();
		SetPrivacyNoticeVisible(false);
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnInfoButtonClick()
	{
		SetPrivacyNoticeVisible(true);
	}
	public void OnPrivacyNoticeButtonClick()
	{
		AcceptPrivacyNotice();
	}
	public void OnSyncToggleButtonClick()
	{
		if (_isRefreshing || SettingsManager == null) return;
		SettingsManager.SetEnabled(uiComponent.SwitchSwitch == null || uiComponent.SwitchSwitch.IsToggled());
		RefreshUI();
	}
	public void OnVisibilityAllFriendsToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing || !state || SettingsManager == null) return;
		SettingsManager.SetVisibility(DailyDivinationSyncVisibility.AllFriends);
		RefreshUI();
	}
	public void OnVisibilityRealFriendsToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing || !state || SettingsManager == null) return;
		SettingsManager.SetVisibility(DailyDivinationSyncVisibility.RealFriends);
		RefreshUI();
	}
	public void OnMeibilityOnlyMeToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing || !state || SettingsManager == null) return;
		SettingsManager.SetVisibility(DailyDivinationSyncVisibility.OnlyMe);
		RefreshUI();
	}
	public void OnSaveSettingsButtonClick()
	{
		if (SettingsManager != null)
		{
			SettingsManager.SetEnabled(uiComponent.SwitchSwitch == null || uiComponent.SwitchSwitch.IsToggled(), false);
			SettingsManager.SetVisibility(GetSelectedVisibility(), false);
			SettingsManager.SaveLocal();
		}

		SaveSettings();
	}
	#endregion
}
