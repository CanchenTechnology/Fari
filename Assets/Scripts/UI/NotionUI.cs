/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/11/2026 3:17:28 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using UltimateClean;

public class NotionUI : WindowBase
{
	public NotionUIComponent uiComponent;
	private bool _isRefreshing;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<NotionUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		BindSwitchButtons();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		BindSwitchButtons();
		NotificationUnreadState.ClearUnread();
		LoadCloudSettingsThenRefresh();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		UnbindSwitchButtons();
		base.OnDestroy();
	}
	#endregion

	#region API Function
	private void BindSwitchButtons()
	{
		BindSwitchButton(uiComponent?.DailyOracleSwitch, OnDailyOracleSwitchClick);
		BindSwitchButton(uiComponent?.DialogueSwitch, OnDialogueSwitchClick);
		BindSwitchButton(uiComponent?.DivinationReturnSwitch, OnDivinationReturnSwitchClick);
		BindSwitchButton(uiComponent?.FriendInteractionSwitch, OnFriendInteractionSwitchClick);
		BindSwitchButton(uiComponent?.ActivitySystemSwitch, OnActivitySystemSwitchClick);
	}

	private void UnbindSwitchButtons()
	{
		UnbindSwitchButton(uiComponent?.DailyOracleSwitch, OnDailyOracleSwitchClick);
		UnbindSwitchButton(uiComponent?.DialogueSwitch, OnDialogueSwitchClick);
		UnbindSwitchButton(uiComponent?.DivinationReturnSwitch, OnDivinationReturnSwitchClick);
		UnbindSwitchButton(uiComponent?.FriendInteractionSwitch, OnFriendInteractionSwitchClick);
		UnbindSwitchButton(uiComponent?.ActivitySystemSwitch, OnActivitySystemSwitchClick);
	}

	private void BindSwitchButton(Switch sw, UnityEngine.Events.UnityAction action)
	{
		if (sw == null) return;
		Button button = sw.GetComponent<Button>();
		if (button == null) return;
		button.onClick.RemoveListener(action);
		button.onClick.AddListener(action);
	}

	private void UnbindSwitchButton(Switch sw, UnityEngine.Events.UnityAction action)
	{
		if (sw == null) return;
		Button button = sw.GetComponent<Button>();
		if (button == null) return;
		button.onClick.RemoveListener(action);
	}

	/// <summary>
	/// 刷新通知设置UI显示
	/// </summary>
	private void RefreshUI()
	{
		if (uiComponent == null) return;

		var settings = GetSettingsManager();
		if (settings == null) return;
		_isRefreshing = true;

		SetSwitchState(uiComponent.DailyOracleSwitch, settings.DailyOracleEnabled);
		SetSwitchState(uiComponent.DialogueSwitch, settings.DialogueReplyEnabled);
		SetSwitchState(uiComponent.DivinationReturnSwitch, settings.DivinationReturnEnabled);
		SetSwitchState(uiComponent.FriendInteractionSwitch, settings.FriendInteractionEnabled);
		SetSwitchState(uiComponent.ActivitySystemSwitch, settings.ActivitySystemEnabled);

		// 刷新提醒时间显示
		if (uiComponent.TimeValueText != null)
			uiComponent.TimeValueText.text = settings.ReminderTime;

		if (uiComponent.DivinationDescText != null)
			uiComponent.DivinationDescText.text = settings.DivinationReturnEnabled
				? "占卜结果回访提醒已开启"
				: "占卜结果回访提醒已关闭";

		_isRefreshing = false;
	}

	private void SetSwitchState(Switch sw, bool enabled)
	{
		if (sw == null) return;
		if (sw.IsToggled() != enabled)
			sw.Toggle();
	}

	private void LoadCloudSettingsThenRefresh()
	{
		var settings = GetSettingsManager();
		if (settings == null)
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

		if (settings.HasPendingCloudSync)
		{
			RefreshUI();
			SaveCloudSettings(false);
			return;
		}

		firestore.LoadNotificationSettings(cloud =>
		{
			if (cloud != null)
			{
				settings.ApplySettings(
					cloud.dailyOracleEnabled,
					cloud.dialogueReplyEnabled,
					cloud.divinationReturnEnabled,
					cloud.friendInteractionEnabled,
					cloud.activitySystemEnabled,
					cloud.reminderTime);
			}
			RefreshUI();
		});
	}

	private void SaveCloudSettings(bool showOfflineToast = true)
	{
		var settings = GetSettingsManager();
		if (settings == null) return;

		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			settings.MarkCloudSyncPending();
			if (showOfflineToast)
				ToastManager.ShowToast("通知设置已保存到本地，云端稍后同步");
			Debug.LogWarning("[NotionUI] 通知设置云端暂未就绪，已标记稍后同步");
			return;
		}

		firestore.SaveNotificationSettings(settings, success =>
		{
			if (success)
			{
				settings.MarkCloudSyncComplete();
			}
			else
			{
				settings.MarkCloudSyncPending();
				if (showOfflineToast)
					ToastManager.ShowToast("通知设置已保存到本地，云端稍后同步");
				Debug.LogWarning("[NotionUI] 通知设置云端同步失败");
			}
		});
	}

	private NotificationSettingsManager GetSettingsManager()
	{
		var settings = NotificationSettingsManager.Instance;
		if (settings != null)
			return settings;

		var go = new GameObject("NotificationSettingsManager");
		return go.AddComponent<NotificationSettingsManager>();
	}

	private void SaveDailyOraclePreference(bool enabled)
	{
		GetSettingsManager()?.SetDailyOracle(enabled);
		ToastManager.ShowToast(enabled ? "已开启每日神谕提醒" : "已关闭每日神谕提醒");
		SaveCloudSettings();
	}

	private void SaveDialogueReplyPreference(bool enabled)
	{
		GetSettingsManager()?.SetDialogueReply(enabled);
		ToastManager.ShowToast(enabled ? "已开启对话回复提醒" : "已关闭对话回复提醒");
		SaveCloudSettings();
	}

	private void SaveDivinationReturnPreference(bool enabled)
	{
		GetSettingsManager()?.SetDivinationReturn(enabled);
		RefreshUI();
		ToastManager.ShowToast(enabled ? "已开启占卜回访提醒" : "已关闭占卜回访提醒");
		SaveCloudSettings();
	}

	private void SaveFriendInteractionPreference(bool enabled)
	{
		GetSettingsManager()?.SetFriendInteraction(enabled);
		ToastManager.ShowToast(enabled ? "已开启好友互动提醒" : "已关闭好友互动提醒");
		SaveCloudSettings();
	}

	private void SaveActivitySystemPreference(bool enabled)
	{
		GetSettingsManager()?.SetActivitySystem(enabled);
		ToastManager.ShowToast(enabled ? "已开启活动与系统通知" : "已关闭活动与系统通知");
		SaveCloudSettings();
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}

	public void OnDailyOracleSwitchClick()
	{
		if (_isRefreshing) return;
		bool enabled = uiComponent?.DailyOracleSwitch == null || uiComponent.DailyOracleSwitch.IsToggled();
		SaveDailyOraclePreference(enabled);
	}

	public void OnDialogueSwitchClick()
	{
		if (_isRefreshing) return;
		bool enabled = uiComponent?.DialogueSwitch == null || uiComponent.DialogueSwitch.IsToggled();
		SaveDialogueReplyPreference(enabled);
	}

	public void OnDivinationReturnSwitchClick()
	{
		if (_isRefreshing) return;
		bool enabled = uiComponent?.DivinationReturnSwitch == null || uiComponent.DivinationReturnSwitch.IsToggled();
		SaveDivinationReturnPreference(enabled);
	}

	public void OnFriendInteractionSwitchClick()
	{
		if (_isRefreshing) return;
		bool enabled = uiComponent?.FriendInteractionSwitch != null && uiComponent.FriendInteractionSwitch.IsToggled();
		SaveFriendInteractionPreference(enabled);
	}

	public void OnActivitySystemSwitchClick()
	{
		if (_isRefreshing) return;
		bool enabled = uiComponent?.ActivitySystemSwitch == null || uiComponent.ActivitySystemSwitch.IsToggled();
		SaveActivitySystemPreference(enabled);
	}

	public void OnDailyOracleToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing) return;
		SaveDailyOraclePreference(state);
	}
	public void OnDialogueReplyToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing) return;
		SaveDialogueReplyPreference(state);
	}
	public void OnDivinationReturnToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing) return;
		SaveDivinationReturnPreference(state);
	}
	public void OnFriendInteractionToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing) return;
		SaveFriendInteractionPreference(state);
	}
	public void OnActivitySystemToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing) return;
		SaveActivitySystemPreference(state);
	}
	public void OnTimeSettingButtonClick()
	{
		var settings = GetSettingsManager();
		if (settings == null) return;

		string newTime = settings.ToggleReminderTime();
		if (uiComponent.TimeValueText != null)
			uiComponent.TimeValueText.text = newTime;

		ToastManager.ShowToast($"每日提醒时间设置为 {newTime}");
		SaveCloudSettings();
		Debug.Log($"[NotionUI] 每日提醒时间切换为：{newTime}");
	}
	#endregion
}
