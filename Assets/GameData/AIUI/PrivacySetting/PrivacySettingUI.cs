using GamerFrameWork.UIFrameWork;
using UnityEngine;

public class PrivacySettingUI : WindowBase
{
	public PrivacySettingUIComponent uiComponent;

	private static readonly Vector2 DesignSize = new Vector2(390f, 844f);
	private bool _isRefreshing;

	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<PrivacySettingUIComponent>();
		if (uiComponent != null)
		{
			uiComponent.InitComponent(this);
			if (Canvas != null)
			{
				Canvas.sortingOrder = (int)uiComponent.windowLayer;
			}
		}

		base.OnAwake();
		ApplyDesignFit();
		RefreshUI();
		HidePrivacyNotice();
	}

	public override void OnShow()
	{
		base.OnShow();
		ApplyDesignFit();
		RefreshUI();
		HidePrivacyNotice();
	}

	public void ApplyDesignFit()
	{
		if (uiComponent == null || uiComponent.designRoot == null)
		{
			return;
		}

		RectTransform designRoot = uiComponent.designRoot;
		RectTransform parent = designRoot.parent as RectTransform;
		float parentWidth = parent != null && parent.rect.width > 1f ? parent.rect.width : 1080f;
		float parentHeight = parent != null && parent.rect.height > 1f ? parent.rect.height : 1920f;
		float scale = Mathf.Min(parentWidth / DesignSize.x, parentHeight / DesignSize.y);

		designRoot.anchorMin = new Vector2(0.5f, 0.5f);
		designRoot.anchorMax = new Vector2(0.5f, 0.5f);
		designRoot.pivot = new Vector2(0.5f, 0.5f);
		designRoot.anchoredPosition = Vector2.zero;
		designRoot.sizeDelta = DesignSize;
		designRoot.localScale = Vector3.one * scale;
	}

	private void RefreshUI()
	{
		if (uiComponent == null)
		{
			return;
		}

		_isRefreshing = true;
		DailyDivinationSyncSettingsManager manager = DailyDivinationSyncSettingsManager.Instance;
		DailyDivinationSyncVisibility visibility = manager != null
			? manager.Visibility
			: DailyDivinationSyncVisibility.OnlyMe;

		if (uiComponent.visibilityEveryoneToggle != null)
		{
			uiComponent.visibilityEveryoneToggle.isOn = visibility == DailyDivinationSyncVisibility.AllFriends;
		}

		if (uiComponent.visibilityFriendsOnlyToggle != null)
		{
			uiComponent.visibilityFriendsOnlyToggle.isOn = visibility == DailyDivinationSyncVisibility.RealFriends;
		}

		if (uiComponent.visibilityOnlyMeToggle != null)
		{
			uiComponent.visibilityOnlyMeToggle.isOn = visibility == DailyDivinationSyncVisibility.OnlyMe;
		}

		_isRefreshing = false;
	}

	private DailyDivinationSyncVisibility GetSelectedVisibility()
	{
		if (uiComponent?.visibilityEveryoneToggle != null && uiComponent.visibilityEveryoneToggle.isOn)
		{
			return DailyDivinationSyncVisibility.AllFriends;
		}

		if (uiComponent?.visibilityFriendsOnlyToggle != null && uiComponent.visibilityFriendsOnlyToggle.isOn)
		{
			return DailyDivinationSyncVisibility.RealFriends;
		}

		return DailyDivinationSyncVisibility.OnlyMe;
	}

	public void OnBackButtonClick()
	{
		HideWindow();
	}

	public void OnPrivacyNoticeButtonClick()
	{
		if (uiComponent?.privacyNoticeOverlay != null)
		{
			uiComponent.privacyNoticeOverlay.SetActive(true);
		}
	}

	public void OnDoneButtonClick()
	{
		HidePrivacyNotice();
	}

	public void OnSaveButtonClick()
	{
		DailyDivinationSyncSettingsManager manager = DailyDivinationSyncSettingsManager.Instance;
		if (manager != null)
		{
			manager.SetEnabled(true, false);
			manager.SetVisibility(GetSelectedVisibility(), false);
			manager.SaveLocal();
		}

		ToastManager.ShowToast("Settings saved");
	}

	public void OnVisibilityEveryoneToggleChange(bool state, UnityEngine.UI.Toggle toggle)
	{
		if (_isRefreshing || !state)
		{
			return;
		}

		DailyDivinationSyncSettingsManager.Instance?.SetVisibility(DailyDivinationSyncVisibility.AllFriends);
	}

	public void OnVisibilityFriendsOnlyToggleChange(bool state, UnityEngine.UI.Toggle toggle)
	{
		if (_isRefreshing || !state)
		{
			return;
		}

		DailyDivinationSyncSettingsManager.Instance?.SetVisibility(DailyDivinationSyncVisibility.RealFriends);
	}

	public void OnVisibilityOnlyMeToggleChange(bool state, UnityEngine.UI.Toggle toggle)
	{
		if (_isRefreshing || !state)
		{
			return;
		}

		DailyDivinationSyncSettingsManager.Instance?.SetVisibility(DailyDivinationSyncVisibility.OnlyMe);
	}

	private void HidePrivacyNotice()
	{
		if (uiComponent?.privacyNoticeOverlay != null)
		{
			uiComponent.privacyNoticeOverlay.SetActive(false);
		}
	}
}
