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
using TMPro;

public class MemoryPrivacySettingsUI : WindowBase
{
	public MemoryPrivacySettingsUIComponent uiComponent;
	private GameObject _clearConfirmModal;
	private Button _confirmClearButton;
	private Button _cancelClearButton;
	private Button _showClearButton;
	private bool _isRefreshing;
	private bool _isClearingMemory;

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
		if (_clearConfirmModal == null)
			_clearConfirmModal = FindObjectByName("ClearConfirmModal");
		if (_clearConfirmModal == null)
			_clearConfirmModal = CreateRuntimeClearConfirmModal();

		_showClearButton = FindButton("ShowClearConfirm");
		if (_showClearButton == null)
			_showClearButton = CreateRuntimeClearButton();
		if (_showClearButton != null)
		{
			_showClearButton.onClick.RemoveListener(ShowClearConfirm);
			_showClearButton.onClick.AddListener(ShowClearConfirm);
		}

		_cancelClearButton = FindButton("CancelClear");
		if (_cancelClearButton != null)
		{
			_cancelClearButton.onClick.RemoveListener(HideClearConfirm);
			_cancelClearButton.onClick.AddListener(HideClearConfirm);
		}

		_confirmClearButton = FindButton("ConfirmClear");
		if (_confirmClearButton != null)
		{
			_confirmClearButton.onClick.RemoveListener(ConfirmClearAllMemory);
			_confirmClearButton.onClick.AddListener(ConfirmClearAllMemory);
		}
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
		if (_clearConfirmModal == null)
			_clearConfirmModal = CreateRuntimeClearConfirmModal();

		SetClearButtonsInteractable(!_isClearingMemory);
		_clearConfirmModal.SetActive(true);
	}

	private void HideClearConfirm()
	{
		if (_clearConfirmModal != null)
			_clearConfirmModal.SetActive(false);
	}

	private void ConfirmClearAllMemory()
	{
		if (_isClearingMemory) return;

		_isClearingMemory = true;
		SetClearButtonsInteractable(false);
		MemoryUiStore.ClearAll(success =>
		{
			_isClearingMemory = false;
			SetClearButtonsInteractable(true);
			HideClearConfirm();
			UIModule.Instance.GetWindow<MemoryManageUI>()?.RefreshFromExternal();
			UIModule.Instance.GetWindow<MemoryManageListUI>()?.RefreshFromExternal();
			UIModule.Instance.GetWindow<MemoryDetailUI>()?.RefreshFromExternal();
			ToastManager.ShowToast(success ? "记忆已清空" : "本地已清空，云端稍后同步");
		});
	}

	private void SaveSettingToast(string text)
	{
		PlayerPrefs.Save();
		ToastManager.ShowToast(text);
	}

	private Button CreateRuntimeClearButton()
	{
		GameObject go = new GameObject("ShowClearConfirm", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
		go.transform.SetParent(transform, false);

		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0f);
		rect.anchorMax = new Vector2(0.5f, 0f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = new Vector2(0f, 104f);
		rect.sizeDelta = new Vector2(360f, 66f);

		Image image = go.GetComponent<Image>();
		image.color = new Color(0.24f, 0.075f, 0.095f, 0.94f);

		Button button = go.GetComponent<Button>();
		ColorBlock colors = button.colors;
		colors.normalColor = image.color;
		colors.highlightedColor = new Color(0.34f, 0.11f, 0.13f, 1f);
		colors.pressedColor = new Color(0.16f, 0.045f, 0.055f, 1f);
		button.colors = colors;

		CreateText("Text", go.transform, "清空全部记忆", 28, FontStyles.Bold, Color.white, Vector2.zero, rect.sizeDelta, TextAlignmentOptions.Center);
		return button;
	}

	private GameObject CreateRuntimeClearConfirmModal()
	{
		GameObject overlay = new GameObject("ClearConfirmModal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		overlay.transform.SetParent(transform, false);

		RectTransform overlayRect = overlay.GetComponent<RectTransform>();
		overlayRect.anchorMin = Vector2.zero;
		overlayRect.anchorMax = Vector2.one;
		overlayRect.offsetMin = Vector2.zero;
		overlayRect.offsetMax = Vector2.zero;

		Image overlayImage = overlay.GetComponent<Image>();
		overlayImage.color = new Color(0f, 0f, 0f, 0.58f);

		GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		panel.transform.SetParent(overlay.transform, false);
		RectTransform panelRect = panel.GetComponent<RectTransform>();
		panelRect.anchorMin = new Vector2(0.5f, 0.5f);
		panelRect.anchorMax = new Vector2(0.5f, 0.5f);
		panelRect.pivot = new Vector2(0.5f, 0.5f);
		panelRect.anchoredPosition = Vector2.zero;
		panelRect.sizeDelta = new Vector2(640f, 360f);
		panel.GetComponent<Image>().color = new Color(0.075f, 0.055f, 0.095f, 0.98f);

		CreateText("Title", panel.transform, "清空全部记忆？", 34, FontStyles.Bold, new Color(1f, 0.82f, 0.55f, 1f), new Vector2(0f, 106f), new Vector2(560f, 54f), TextAlignmentOptions.Center);
		CreateText("Body", panel.transform, "这会删除本地和云端 AI 记忆，后续对话将不再使用这些记忆。", 24, FontStyles.Normal, new Color(0.88f, 0.84f, 0.92f, 1f), new Vector2(0f, 26f), new Vector2(540f, 96f), TextAlignmentOptions.Center);

		CreateModalButton(panel.transform, "CancelClear", "取消", new Vector2(-132f, -116f), new Color(0.16f, 0.12f, 0.22f, 0.96f), new Color(0.88f, 0.84f, 0.92f, 1f));
		CreateModalButton(panel.transform, "ConfirmClear", "确认清空", new Vector2(132f, -116f), new Color(0.48f, 0.12f, 0.14f, 0.96f), Color.white);

		overlay.SetActive(false);
		return overlay;
	}

	private Button CreateModalButton(Transform parent, string name, string label, Vector2 position, Color background, Color textColor)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = position;
		rect.sizeDelta = new Vector2(210f, 64f);

		Image image = go.GetComponent<Image>();
		image.color = background;

		Button button = go.GetComponent<Button>();
		ColorBlock colors = button.colors;
		colors.normalColor = background;
		colors.highlightedColor = Color.Lerp(background, Color.white, 0.12f);
		colors.pressedColor = Color.Lerp(background, Color.black, 0.18f);
		button.colors = colors;

		CreateText("Text", go.transform, label, 25, FontStyles.Bold, textColor, Vector2.zero, rect.sizeDelta, TextAlignmentOptions.Center);
		return button;
	}

	private TMP_Text CreateText(string name, Transform parent, string value, int size, FontStyles style, Color color, Vector2 position, Vector2 sizeDelta, TextAlignmentOptions alignment)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = position;
		rect.sizeDelta = sizeDelta;

		TMP_Text text = go.GetComponent<TMP_Text>();
		text.font = TMP_Settings.defaultFontAsset;
		text.fontSize = size;
		text.fontStyle = style;
		text.color = color;
		text.alignment = alignment;
		text.enableWordWrapping = true;
		text.text = value;
		return text;
	}

	private void SetClearButtonsInteractable(bool interactable)
	{
		if (_confirmClearButton != null)
			_confirmClearButton.interactable = interactable;
		if (_cancelClearButton != null)
			_cancelClearButton.interactable = interactable;
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
