/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/11/2026 1:13:40 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using XFGameFrameWork;

public class MyUI : WindowBase
{
	public MyUIComponent uiComponent;
	private bool _isPro;
	private DivinationRecordData _latestRecord;
	private int _dashboardRequestId;
	private string _profileSubtitle = string.Empty;
	private Button _latestRecordButton;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<MyUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		EnsureLatestRecordEntryClick();
		NotificationUnreadBadge.Attach(uiComponent.noticeButton);
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		EnsureLatestRecordEntryClick();
		NotificationUnreadBadge.Attach(uiComponent.noticeButton);
		RefreshDashboard();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		_dashboardRequestId++;
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
	/// 设置基本信息
	/// </summary>
	private void SetBaseInfo(int requestId)
	{
		var userData = UserDataManager.Instance;
		string userName = string.IsNullOrWhiteSpace(userData.UserName)
			? "小夜"
			: userData.UserName;

		if (uiComponent.UsernameText != null)
			uiComponent.UsernameText.text = userName;

		if (uiComponent.UserSubtitleText != null)
		{
			_profileSubtitle = BuildProfileSubtitle();
			ApplyMembershipSubtitle();
		}

		if (!string.IsNullOrEmpty(userData.PhotoUrl) && GameManager.Instance != null)
		{
			GameManager.Instance.StartCoroutine(GoogleUserInfoHelper.LoadAndCacheAvatarCoroutine(
					userData.PhotoUrl,
					sprite =>
					{
						if (requestId != _dashboardRequestId) return;
						if (sprite != null && uiComponent.AvatarImage != null)
							uiComponent.AvatarImage.sprite = sprite;
					}
			));
		}
	}

	private void RefreshDashboard()
	{
		int requestId = ++_dashboardRequestId;
		SetBaseInfo(requestId);
		RefreshUsageStats();
		RefreshMembership(requestId);
		LoadLatestDivination(requestId);
	}

	private void RefreshUsageStats()
	{
		var stats = UsageStatsManager.Instance;
		if (stats == null) return;

		if (uiComponent.tatTodayCardValueText != null)
			uiComponent.tatTodayCardValueText.text = stats.GetReadingDisplay(_isPro);

		if (uiComponent.StatTodaydialouNumText != null)
			uiComponent.StatTodaydialouNumText.text = stats.GetDialogDisplay(_isPro);
	}

	private string BuildProfileSubtitle()
	{
		var userData = UserDataManager.Instance;
		if (!string.IsNullOrWhiteSpace(userData.ProfileBio))
			return userData.ProfileBio.Length > 40 ? userData.ProfileBio.Substring(0, 40) + "..." : userData.ProfileBio;

		return userData.IsProfileComplete()
			? $"{userData.City} · {userData.Birthday} {userData.BirthTime}"
			: "完善出生信息，让神谕更贴近你";
	}

	private void ApplyMembershipSubtitle()
	{
		if (uiComponent.UserSubtitleText == null) return;
		string profileText = string.IsNullOrEmpty(_profileSubtitle) ? BuildProfileSubtitle() : _profileSubtitle;
		uiComponent.UserSubtitleText.text = _isPro ? $"Pro · {profileText}" : $"Free · {profileText}";
	}

	private void RefreshMembership(int requestId)
	{
		var client = BackendMembershipClient.Instance;
		if (client == null)
		{
			var go = new GameObject("BackendMembershipClient");
			client = go.AddComponent<BackendMembershipClient>();
		}

		client.GetMembershipStatus(
			status =>
			{
				if (requestId != _dashboardRequestId) return;
				_isPro = status != null && status.isPro;
				ApplyMembershipSubtitle();
				RefreshUsageStats();
			},
			error =>
			{
				if (requestId != _dashboardRequestId) return;
				_isPro = false;
				Debug.LogWarning("[MyUI] 会员状态加载失败: " + error);
				ApplyMembershipSubtitle();
				RefreshUsageStats();
			});
	}

	private void LoadLatestDivination(int requestId)
	{
		DivinationHistoryCacheService cache = DivinationHistoryCacheService.Instance;
		List<DivinationRecordData> cachedRecords = cache.GetSnapshot();
		if (cachedRecords.Count > 0)
		{
			_latestRecord = cachedRecords[0];
			SetLatestRecord(_latestRecord);
		}
		else if (uiComponent.StatTodaydesText != null && !cache.HasLoadedOnce)
		{
			uiComponent.StatTodaydesText.text = "加载中...";
		}
		else
		{
			SetLatestRecord(null);
		}

		cache.Refresh(false, (records, success) =>
		{
			if (requestId != _dashboardRequestId) return;
			_latestRecord = records != null && records.Count > 0 ? records[0] : null;
			SetLatestRecord(_latestRecord);
		});
	}

	private void SetLatestRecord(DivinationRecordData record)
	{
		if (uiComponent.StatTodaydesText == null) return;

		if (record == null)
		{
			uiComponent.StatTodaydesText.text = "暂无记录";
			return;
		}

		string title = GetDisplaySpreadTitle(record.SpreadLabel);
		uiComponent.StatTodaydesText.text = string.IsNullOrEmpty(title) ? "占卜记录" : title;
	}

	private string GetDisplaySpreadTitle(string spreadLabel)
	{
		if (string.IsNullOrWhiteSpace(spreadLabel)) return "占卜记录";

		string normalized = spreadLabel.Trim();
		string lower = normalized.ToLowerInvariant();
		if (lower == "daily_oracle" || lower == "today_oracle")
		{
			return "今日神谕";
		}

		if (lower.Contains("_"))
		{
			return "";
		}

		return normalized;
	}

	private void EnsureLatestRecordEntryClick()
	{
		if (uiComponent == null || uiComponent.StatTodaydesText == null) return;

		_latestRecordButton = uiComponent.StatTodaydesText.GetComponent<Button>();
		if (_latestRecordButton == null)
		{
			_latestRecordButton = uiComponent.StatTodaydesText.gameObject.AddComponent<Button>();
			_latestRecordButton.transition = Selectable.Transition.None;
		}

		uiComponent.StatTodaydesText.raycastTarget = true;
		_latestRecordButton.onClick.RemoveListener(OnLatestRecordEntryClick);
		_latestRecordButton.onClick.AddListener(OnLatestRecordEntryClick);
	}

	#endregion

	#region UI组件事件		 
	public void OnnoticeButtonClick()
	{
		UIModule.Instance.PopUpWindow<NotionUI>();
	}

	public void Onbtn_divinationButtonClick()
	{
		HistoryUI.SelectedRecord = _latestRecord;
		UIModule.Instance.PopUpWindow<HistoryUI>();
	}
	public void OnLatestRecordEntryClick()
	{
		if (_latestRecord == null)
		{
			UIModule.Instance.PopUpWindow<HistoryUI>();
			return;
		}

		HistoryUI.SelectedRecord = _latestRecord;
		UIModule.Instance.PopUpWindow<DivinationRecordUI>();
	}
	public void Onbtn_myinfoButtonClick()
	{
		UIModule.Instance.PopUpWindow<PersonalProfileUI>();
	}
	public void OnmemoryButtonClick()
	{
		UIModule.Instance.PopUpWindow<MemoryManageUI>();
	}
	public void OnproButtonClick()
	{
		UIModule.Instance.PopUpWindow<UnlockProUI>();
	}
	public void OnaccountButtonClick()
	{
		UIModule.Instance.PopUpWindow<AccountUI>();
	}
	public void OnfeedbackButtonClick()
	{
		UIModule.Instance.PopUpWindow<FeedbackUI>();
	}
	public void OnshareButtonClick()
	{
		UIModule.Instance.PopUpWindow<FollowusUI>();
	}
	#endregion
}
