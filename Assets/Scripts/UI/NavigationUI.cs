/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/9/2026 10:00:04 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class NavigationUI : WindowBase
{
	public NavigationUIComponent uiComponent;

	private const string FriendListWindowName = nameof(FriendUI);
	private const string NoFriendWindowName = nameof(NoFriendUI);

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
		uiComponent.todayOracleToggle.isOn = true;
		mCurrentActiveWindow = nameof(TodayOracleUI);
		UIModule.Instance.PopUpWindow<TodayOracleUI>();

		FriendDataManager.Instance.DataChanged -= HandleFriendDataChanged;
		FriendDataManager.Instance.DataChanged += HandleFriendDataChanged;
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		FriendDataManager.Instance.DataChanged -= HandleFriendDataChanged;
		base.OnDestroy();
	}
	#endregion

	#region API Function

	private bool HasAnyFriend()
	{
		return FriendDataManager.Instance.RealFriendList.Count > 0
			|| FriendDataManager.Instance.VirtualFriendList.Count > 0;
	}

	private bool IsFriendEntryActive()
	{
		return mCurrentActiveWindow == FriendListWindowName
			|| mCurrentActiveWindow == NoFriendWindowName;
	}

	private void ShowFriendEntry()
	{
		bool hasAnyFriend = HasAnyFriend();
		string targetWindowName = hasAnyFriend ? FriendListWindowName : NoFriendWindowName;
		if (mCurrentActiveWindow == targetWindowName) return;

		UIModule.Instance.HideWindow<FriendUI>();
		UIModule.Instance.HideWindow<NoFriendUI>();

		mCurrentActiveWindow = targetWindowName;
		if (hasAnyFriend)
		{
			UIModule.Instance.PopUpWindow<FriendUI>();
		}
		else
		{
			UIModule.Instance.PopUpWindow<NoFriendUI>();
		}
	}

	private void HideFriendEntry()
	{
		if (IsFriendEntryActive()) mCurrentActiveWindow = "";
		UIModule.Instance.HideWindow<FriendUI>();
		UIModule.Instance.HideWindow<NoFriendUI>();
	}

	private void HandleFriendDataChanged()
	{
		if (uiComponent == null || uiComponent.friendToggle == null || !uiComponent.friendToggle.isOn)
		{
			return;
		}

		ShowFriendEntry();
	}

	#endregion

	#region UI组件事件
	public void OntodayOracleToggleChange(bool state, Toggle toggle)
	{
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
	}

	public void OpenFriendUI()
	{
		uiComponent.friendToggle.isOn = true;
		ShowFriendEntry();
	}


	#endregion
}
