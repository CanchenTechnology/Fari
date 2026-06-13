/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/11/2026 11:34:57 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class FriendUI : WindowBase
{
	public FriendUIComponent uiComponent;

	// 当前活跃的 FriendItem 列表（用于管理回收）
	private List<FriendItem> activeRealFriendItems = new List<FriendItem>();
	private List<FriendItem> activeVirtualFriendItems = new List<FriendItem>();
	private List<InviteItem> activeInviteItems = new List<InviteItem>();

	// 折叠状态
	private bool realFriendExpanded = true;
	private bool virtualFriendExpanded = true;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<FriendUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		InitPoolIfNeeded();
		RefreshAllViews();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
		// 隐藏时回收所有对象到池中
		ReleaseAllItems();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region 初始化

	/// <summary>
	/// 初始化对象池（仅首次调用）
	/// </summary>
	private void InitPoolIfNeeded()
	{
		FriendDataManager.Instance.InitPools(
			uiComponent.friendPrefab,
			uiComponent.invitePrefab,
			transform
		);
	}

	#endregion

	#region 视图刷新

	/// <summary>
	/// 刷新所有视图
	/// </summary>
	private void RefreshAllViews()
	{
		RefreshRealFriendView();
		RefreshVirtualFriendView();
		RefreshInviteView();
		UpdateCountText();
	}

	/// <summary>
	/// 刷新真实好友列表
	/// </summary>
	private void RefreshRealFriendView()
	{
		// 回收旧对象
		ReleaseRealFriendItems();

		var dataList = FriendDataManager.Instance.RealFriendList;
		foreach (var data in dataList)
		{
			GameObject friendGO = FriendDataManager.Instance.GetFriendItem(uiComponent.RealFriendContentTransform);
			FriendItem item = friendGO.GetComponent<FriendItem>();
			item.SetData(data);
			activeRealFriendItems.Add(item);
		}

		// 强制刷新 ContentSizeFitter 布局
		ForceRebuildLayout(uiComponent.RealFriendContentTransform);

		// 控制折叠
		uiComponent.RealFriendContentTransform.gameObject.SetActive(realFriendExpanded);
	}

	/// <summary>
	/// 刷新虚拟好友列表
	/// </summary>
	private void RefreshVirtualFriendView()
	{
		// 回收旧对象
		ReleaseVirtualFriendItems();

		var dataList = FriendDataManager.Instance.VirtualFriendList;
		foreach (var data in dataList)
		{
			GameObject friendGO = FriendDataManager.Instance.GetFriendItem(uiComponent.VirtualFriendContentTransform);
			FriendItem item = friendGO.GetComponent<FriendItem>();
			item.SetData(data);
			activeVirtualFriendItems.Add(item);
		}

		// 强制刷新 ContentSizeFitter 布局
		ForceRebuildLayout(uiComponent.VirtualFriendContentTransform);

		// 控制折叠
		uiComponent.VirtualFriendContentTransform.gameObject.SetActive(virtualFriendExpanded);
	}

	/// <summary>
	/// 刷新邀请列表
	/// </summary>
	private void RefreshInviteView()
	{
		// 回收旧对象
		ReleaseInviteItems();

		var dataList = FriendDataManager.Instance.InviteList;
		foreach (var data in dataList)
		{
			GameObject inviteGO = FriendDataManager.Instance.GetInviteItem(uiComponent.InviteBannerTransform);
			InviteItem item = inviteGO.GetComponent<InviteItem>();
			item.SetData(data);
			activeInviteItems.Add(item);
		}

		// 强制刷新 ContentSizeFitter 布局
		ForceRebuildLayout(uiComponent.InviteBannerTransform);
	}

	/// <summary>
	/// 更新数量显示
	/// </summary>
	private void UpdateCountText()
	{
		if (uiComponent.ExistingCountText != null)
		{
			uiComponent.ExistingCountText.text = FriendDataManager.Instance.RealFriendList.Count.ToString();
		}
		if (uiComponent.CreatedCountText != null)
		{
			uiComponent.CreatedCountText.text = FriendDataManager.Instance.VirtualFriendList.Count.ToString();
		}
	}

	#endregion

	#region 对象回收

	private void ReleaseRealFriendItems()
	{
		// 1. 回收 tracked 的对象
		foreach (var item in activeRealFriendItems)
		{
			if (item != null)
			{
				item.ResetForPool();
				FriendDataManager.Instance.ReleaseFriendItem(item.gameObject);
			}
		}
		activeRealFriendItems.Clear();

		// 2. 清理 ContentTransform 下所有残留的 FriendItem（防止堆叠）
		var remainItems = uiComponent.RealFriendContentTransform.GetComponentsInChildren<FriendItem>(true);
		foreach (var item in remainItems)
		{
			if (item != null)
			{
				item.ResetForPool();
				FriendDataManager.Instance.ReleaseFriendItem(item.gameObject);
			}
		}
	}

	private void ReleaseVirtualFriendItems()
	{
		// 1. 回收 tracked 的对象
		foreach (var item in activeVirtualFriendItems)
		{
			if (item != null)
			{
				item.ResetForPool();
				FriendDataManager.Instance.ReleaseFriendItem(item.gameObject);
			}
		}
		activeVirtualFriendItems.Clear();

		// 2. 清理 ContentTransform 下所有残留的 FriendItem（防止堆叠）
		var remainItems = uiComponent.VirtualFriendContentTransform.GetComponentsInChildren<FriendItem>(true);
		foreach (var item in remainItems)
		{
			if (item != null)
			{
				item.ResetForPool();
				FriendDataManager.Instance.ReleaseFriendItem(item.gameObject);
			}
		}
	}

	private void ReleaseInviteItems()
	{
		// 1. 回收 tracked 的对象
		foreach (var item in activeInviteItems)
		{
			if (item != null)
			{
				item.ResetForPool();
				FriendDataManager.Instance.ReleaseInviteItem(item.gameObject);
			}
		}
		activeInviteItems.Clear();

		// 2. 清理 ContentTransform 下所有残留的 InviteItem（防止堆叠）
		var remainItems = uiComponent.InviteBannerTransform.GetComponentsInChildren<InviteItem>(true);
		foreach (var item in remainItems)
		{
			if (item != null)
			{
				item.ResetForPool();
				FriendDataManager.Instance.ReleaseInviteItem(item.gameObject);
			}
		}
	}

	private void ReleaseAllItems()
	{
		ReleaseRealFriendItems();
		ReleaseVirtualFriendItems();
		ReleaseInviteItems();
	}

	#endregion

	#region API Function

	/// <summary>
	/// 强制重建布局，解决 ContentSizeFitter 不及时刷新的问题。
	/// 从目标节点向上逐级刷新，确保父级 LayoutGroup + ContentSizeFitter 也同步更新。
	/// </summary>
	private void ForceRebuildLayout(Transform contentTransform)
	{
		LayoutRebuilder.ForceRebuildLayoutImmediate(contentTransform as RectTransform);

		// 向上逐级刷新，确保父节点（可能有 ContentSizeFitter）也同步
		Transform parent = contentTransform.parent;
		while (parent != null)
		{
			if (parent.GetComponent<LayoutGroup>() != null || parent.GetComponent<ContentSizeFitter>() != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(parent as RectTransform);
			}
			parent = parent.parent;
		}
	}

	#endregion

	#region UI组件事件
	public void OnNotionButtonClick()
	{
		ToastManager.ShowToast("功能待开发。。。");
	}
	public void OnAddRelationButtonClick()
	{
		UIModule.Instance.PopUpWindow<AddFriendUI>();
	}
	public void OnExpandRealFriendButtonClick()
	{
		realFriendExpanded = !realFriendExpanded;
		uiComponent.RealFriendContentTransform.gameObject.SetActive(realFriendExpanded);
		// 展开/折叠后强制刷新布局
		ForceRebuildLayout(uiComponent.RealFriendContentTransform);
	}
	public void OnExpandVirtualFriendButtonClick()
	{
		virtualFriendExpanded = !virtualFriendExpanded;
		uiComponent.VirtualFriendContentTransform.gameObject.SetActive(virtualFriendExpanded);
		// 展开/折叠后强制刷新布局
		ForceRebuildLayout(uiComponent.VirtualFriendContentTransform);
	}
	public void OnAddFriendButtonClick()
	{
		// 通过 DataManager 添加好友数据
		FriendDataManager.FriendData data = FriendDataManager.Instance.AddRealFriend("新好友", "真实好友·新好友");

		// 从对象池获取 FriendItem 并设置数据
		GameObject friendGO = FriendDataManager.Instance.GetFriendItem(uiComponent.RealFriendContentTransform);
		FriendItem item = friendGO.GetComponent<FriendItem>();
		item.SetData(data);
		activeRealFriendItems.Add(item);

		// 强制刷新 ContentSizeFitter 布局
		ForceRebuildLayout(uiComponent.RealFriendContentTransform);

		UpdateCountText();
		UIModule.Instance.PopUpWindow<AddFriendUI>();
	}
	public void OnCreateFriendButtonClick()
	{
		UIModule.Instance.PopUpWindow<CreateFriendUI>();
		// 通过 DataManager 添加虚拟好友数据
		FriendDataManager.FriendData data = FriendDataManager.Instance.AddVirtualFriend("AI好友", "创建好友·AI好友");

		// 从对象池获取 FriendItem 并设置数据
		GameObject friendGO = FriendDataManager.Instance.GetFriendItem(uiComponent.VirtualFriendContentTransform);
		FriendItem item = friendGO.GetComponent<FriendItem>();
		item.SetData(data);
		activeVirtualFriendItems.Add(item);

		// 强制刷新 ContentSizeFitter 布局
		ForceRebuildLayout(uiComponent.VirtualFriendContentTransform);

		UpdateCountText();
	}
	#endregion
}
