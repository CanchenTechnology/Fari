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
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
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
		base.OnDestroy();
	}
	#endregion

	#region API Function

	/// <summary>
	/// 刷新通知设置UI显示
	/// </summary>
	private void RefreshUI()
	{
		if (uiComponent == null) return;

		var settings = GetSettingsManager();
		if (settings == null) return;
		_isRefreshing = true;

		// 刷新各 Toggle 状态（避免触发事件循环）
		if (uiComponent.DailyOracleToggle != null)
			uiComponent.DailyOracleToggle.isOn = settings.DailyOracleEnabled;

		if (uiComponent.DialogueReplyToggle != null)
			uiComponent.DialogueReplyToggle.isOn = settings.DialogueReplyEnabled;

		if (uiComponent.DivinationReturnToggle != null)
			uiComponent.DivinationReturnToggle.isOn = settings.DivinationReturnEnabled;

		if (uiComponent.FriendInteractionToggle != null)
			uiComponent.FriendInteractionToggle.isOn = settings.FriendInteractionEnabled;

		if (uiComponent.ActivitySystemToggle != null)
			uiComponent.ActivitySystemToggle.isOn = settings.ActivitySystemEnabled;

		// 刷新提醒时间显示
		if (uiComponent.TimeValueText != null)
			uiComponent.TimeValueText.text = settings.ReminderTime;

		if (uiComponent.DivinationDescText != null)
			uiComponent.DivinationDescText.text = settings.DivinationReturnEnabled
				? "占卜结果回访提醒已开启"
				: "占卜结果回访提醒已关闭";

		_isRefreshing = false;
	}

	private void LoadCloudSettingsThenRefresh()
	{
		if (GetSettingsManager() == null)
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

		firestore.LoadNotificationSettings(cloud =>
		{
			if (cloud != null)
			{
				GetSettingsManager()?.ApplySettings(
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

	private void SaveCloudSettings()
	{
		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized) return;

		var settings = GetSettingsManager();
		if (settings == null) return;

		firestore.SaveNotificationSettings(settings, success =>
		{
			if (!success)
				Debug.LogWarning("[NotionUI] 通知设置云端同步失败");
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

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnDailyOracleToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing) return;
		GetSettingsManager()?.SetDailyOracle(state);
		SaveCloudSettings();
		ToastManager.ShowToast("偏好设置已保存");
	}
	public void OnDialogueReplyToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing) return;
		GetSettingsManager()?.SetDialogueReply(state);
		SaveCloudSettings();
		ToastManager.ShowToast("偏好设置已保存");
	}
	public void OnDivinationReturnToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing) return;
		GetSettingsManager()?.SetDivinationReturn(state);
		SaveCloudSettings();
		RefreshUI();
		ToastManager.ShowToast("偏好设置已保存");
	}
	public void OnFriendInteractionToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing) return;
		GetSettingsManager()?.SetFriendInteraction(state);
		SaveCloudSettings();
		ToastManager.ShowToast("偏好设置已保存");
	}
	public void OnActivitySystemToggleChange(bool state, Toggle toggle)
	{
		if (_isRefreshing) return;
		GetSettingsManager()?.SetActivitySystem(state);
		SaveCloudSettings();
		ToastManager.ShowToast("偏好设置已保存");
	}
	public void OnTimeSettingButtonClick()
	{
		var settings = GetSettingsManager();
		if (settings == null) return;

		string newTime = settings.ToggleReminderTime();
		if (uiComponent.TimeValueText != null)
			uiComponent.TimeValueText.text = newTime;

		SaveCloudSettings();
		ToastManager.ShowToast($"每日提醒时间设置为 {newTime}");
		Debug.Log($"[NotionUI] 每日提醒时间切换为：{newTime}");
	}
	#endregion
}
