/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/9/2026 10:00:04 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using TMPro;

public class NavigationUI : WindowBase
{
	public NavigationUIComponent uiComponent;

	private const string FriendListWindowName = nameof(FriendUI);
	private static readonly Color SelectedNavigationLabelColor = new Color32(0xD5, 0x8A, 0x3F, 0xFF);
	private readonly Dictionary<Toggle, Color> navigationDefaultLabelColors = new Dictionary<Toggle, Color>();

	/// <summary>
	/// 当前活跃的导航窗口名称，用于防止重复点击同一导航栏
	/// </summary>
	private string mCurrentActiveWindow = "";

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<NavigationUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		ConfigureNavigationToggleGroup();
		CacheNavigationLabelColors();
		uiComponent.todayOracleToggle.isOn = true;
		UpdateNavigationVisuals();
		mCurrentActiveWindow = nameof(TodayOracleUI);
		UIModule.Instance.PopUpWindow<TodayOracleUI>();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		UpdateNavigationVisuals();
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

	private bool IsFriendEntryActive()
	{
		return mCurrentActiveWindow == FriendListWindowName;
	}

	private void ShowFriendEntry()
	{
		if (mCurrentActiveWindow == FriendListWindowName) return;

		UIModule.Instance.HideWindow<NoFriendUI>();

		mCurrentActiveWindow = FriendListWindowName;
		UIModule.Instance.PopUpWindow<FriendUI>();
	}

	private void HideFriendEntry()
	{
		if (IsFriendEntryActive()) mCurrentActiveWindow = "";
		UIModule.Instance.HideWindow<FriendUI>();
		UIModule.Instance.HideWindow<NoFriendUI>();
	}

	private void CacheNavigationLabelColors()
	{
		CacheToggleLabelColor(uiComponent.todayOracleToggle);
		CacheToggleLabelColor(uiComponent.dialogueToggle);
		CacheToggleLabelColor(uiComponent.friendToggle);
		CacheToggleLabelColor(uiComponent.myToggle);
	}

	private void ConfigureNavigationToggleGroup()
	{
		if (uiComponent.navigationToggleGroup != null)
			uiComponent.navigationToggleGroup.allowSwitchOff = false;
	}

	private void CacheToggleLabelColor(Toggle toggle)
	{
		if (toggle == null || navigationDefaultLabelColors.ContainsKey(toggle))
			return;

		TMP_Text label = GetToggleLabel(toggle);
		if (label != null)
			navigationDefaultLabelColors.Add(toggle, label.color);
	}

	private void UpdateNavigationVisuals()
	{
		UpdateToggleVisualState(uiComponent.todayOracleToggle);
		UpdateToggleVisualState(uiComponent.dialogueToggle);
		UpdateToggleVisualState(uiComponent.friendToggle);
		UpdateToggleVisualState(uiComponent.myToggle);
	}

	private void UpdateToggleVisualState(Toggle toggle)
	{
		if (toggle == null) return;

		UpdateToggleLabelColor(toggle);
		SetToggleBackgroundVisible(toggle, !toggle.isOn);
		toggle.interactable = !toggle.isOn;
	}

	private void UpdateToggleLabelColor(Toggle toggle)
	{
		if (toggle == null) return;

		TMP_Text label = GetToggleLabel(toggle);
		if (label == null) return;

		if (!navigationDefaultLabelColors.TryGetValue(toggle, out Color defaultColor))
		{
			defaultColor = label.color;
			navigationDefaultLabelColors[toggle] = defaultColor;
		}

		label.color = toggle.isOn ? SelectedNavigationLabelColor : defaultColor;
	}

	private void SetToggleBackgroundVisible(Toggle toggle, bool visible)
	{
		if (toggle == null) return;

		Image backgroundImage = null;
		if (toggle.targetGraphic is Image targetImage && targetImage.transform.name == "Background")
			backgroundImage = targetImage;

		if (backgroundImage == null)
		{
			Transform backgroundTransform = FindChildByName(toggle.transform, "Background");
			if (backgroundTransform != null)
				backgroundImage = backgroundTransform.GetComponent<Image>();
		}

		if (backgroundImage != null)
			backgroundImage.enabled = visible;
	}

	private TMP_Text GetToggleLabel(Toggle toggle)
	{
		if (toggle == null) return null;

		Transform labelTransform = FindChildByName(toggle.transform, "Label");
		if (labelTransform != null)
		{
			TMP_Text label = labelTransform.GetComponent<TMP_Text>();
			if (label != null) return label;
		}

		return toggle.GetComponentInChildren<TMP_Text>(true);
	}

	private Transform FindChildByName(Transform root, string objectName)
	{
		if (root == null || string.IsNullOrEmpty(objectName)) return null;
		if (root.name == objectName) return root;

		for (int i = 0; i < root.childCount; i++)
		{
			Transform result = FindChildByName(root.GetChild(i), objectName);
			if (result != null) return result;
		}

		return null;
	}

	#endregion

	#region UI组件事件
	public void OntodayOracleToggleChange(bool state, Toggle toggle)
	{
		UpdateNavigationVisuals();
		if(state)
		{
			if (mCurrentActiveWindow == nameof(TodayOracleUI)) return;
			mCurrentActiveWindow = nameof(TodayOracleUI);
			UIModule.Instance.PopUpWindow<TodayOracleUI>();
		}
		else
		{
			if (mCurrentActiveWindow == nameof(TodayOracleUI)) mCurrentActiveWindow = "";
			UIModule.Instance.HideWindow<TodayOracleUI>();
		}
	}
	public void OndialogueToggleChange(bool state, Toggle toggle)
	{
		UpdateNavigationVisuals();
		if(state)
		{
			if (mCurrentActiveWindow == nameof(DialogUI)) return;
			mCurrentActiveWindow = nameof(DialogUI);
			UIModule.Instance.PopUpWindow<DialogUI>();
		}
		else
		{
			if (mCurrentActiveWindow == nameof(DialogUI)) mCurrentActiveWindow = "";
			UIModule.Instance.HideWindow<DialogUI>();
		}
	}
	public void OnfriendToggleChange(bool state, Toggle toggle)
	{
		UpdateNavigationVisuals();
		if(state)
		{
			ShowFriendEntry();
		}
		else
		{
			HideFriendEntry();
		}
	}
	public void OnmyToggleChange(bool state, Toggle toggle)
	{
		UpdateNavigationVisuals();
		if(state)
		{
			if (mCurrentActiveWindow == nameof(MyUI)) return;
			mCurrentActiveWindow = nameof(MyUI);
			UIModule.Instance.PopUpWindow<MyUI>();
		}
		else
		{
			if (mCurrentActiveWindow == nameof(MyUI)) mCurrentActiveWindow = "";
			UIModule.Instance.GetTopWindow().HideWindow();
			UIModule.Instance.HideWindow<MyUI>();
		}
	}
	public void OpenDialogUI()
	{
		uiComponent.dialogueToggle.isOn = true;
		UpdateNavigationVisuals();
	}

	public void OpenTodayOracleUI()
	{
		uiComponent.todayOracleToggle.isOn = true;
		UpdateNavigationVisuals();
	}

	public void OpenFriendUI()
	{
		uiComponent.friendToggle.isOn = true;
		UpdateNavigationVisuals();
		ShowFriendEntry();
	}


	#endregion
}
