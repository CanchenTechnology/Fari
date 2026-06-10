using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using UnityEngine;

/// <summary>
/// 朋友界面主窗口
/// 支持好友分组展开/折叠、@按钮交互、添加/创建好友
/// </summary>
public class FriendsUI : WindowBase
{
    public FriendsUIComponent uiComponent;

    // 数据源
    private List<FriendGroupData> mGroupDataList = new List<FriendGroupData>();
    private TreeViewItemCountMgr mTreeItemCountMgr = new TreeViewItemCountMgr();

    // 邀请通知数量（模拟数据）
    private int mInviteCount = 1;

    #region 生命周期

    public override void OnAwake()
    {
        uiComponent = gameObject.GetComponent<FriendsUIComponent>();
        uiComponent.InitComponent(this);
        this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
        base.OnAwake();
    }

    public override void OnShow()
    {
        base.OnShow();
        InitFriendData();
        InitHeaderUI();
        InitTreeView();
    }

    public override void OnHide()
    {
        base.OnHide();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
    }

    #endregion

    #region 数据初始化

    /// <summary>
    /// 初始化好友数据（实际项目中应从服务器或本地存储加载）
    /// </summary>
    void InitFriendData()
    {
        mGroupDataList.Clear();
        mTreeItemCountMgr.Clear();

        // 已有好友分组
        var existingGroup = new FriendGroupData
        {
            groupName = "已有好友",
            isExpanded = true
        };
        existingGroup.friends.Add(new FriendData
        {
            friendId = "1",
            friendName = "Luna",
            friendType = "真实好友",
            friendCity = "Los Angeles",
            isRealFriend = true
        });
        existingGroup.friends.Add(new FriendData
        {
            friendId = "2",
            friendName = "Morgan",
            friendType = "真实好友",
            friendCity = "New York",
            isRealFriend = true
        });
        mGroupDataList.Add(existingGroup);

        // 创建的好友分组
        var createdGroup = new FriendGroupData
        {
            groupName = "创建的好友",
            isExpanded = true
        };
        createdGroup.friends.Add(new FriendData
        {
            friendId = "3",
            friendName = "Ava",
            friendType = "创建好友",
            friendCity = "Seattle",
            isRealFriend = false
        });
        mGroupDataList.Add(createdGroup);
    }

    /// <summary>
    /// 初始化顶部固定区域UI
    /// </summary>
    void InitHeaderUI()
    {
        if (uiComponent.titleText != null)
            uiComponent.titleText.text = "朋友";

        // 邀请通知 Badge
        if (uiComponent.notificationBadgeText != null)
        {
            if (mInviteCount > 0)
            {
                uiComponent.notificationBadgeText.text = mInviteCount.ToString();
                uiComponent.notificationBadgeText.gameObject.SetActive(true);
            }
            else
            {
                uiComponent.notificationBadgeText.gameObject.SetActive(false);
            }
        }

        // 通知卡片
        if (uiComponent.inviteNotificationGo != null)
        {
            uiComponent.inviteNotificationGo.SetActive(mInviteCount > 0);
        }
    }

    #endregion

    #region TreeView 列表初始化与渲染

    /// <summary>
    /// 初始化树形列表
    /// </summary>
    void InitTreeView()
    {
        for (int i = 0; i < mGroupDataList.Count; ++i)
        {
            int childCount = mGroupDataList[i].friends.Count;
            mTreeItemCountMgr.AddTreeItem(childCount, mGroupDataList[i].isExpanded);
        }

        int totalCount = mTreeItemCountMgr.GetTotalItemAndChildCount() + 1; // +1 底部操作按钮

        if (uiComponent.loopListView == null)
        {
            Debug.LogError("[FriendsUI] loopListView is null!");
            return;
        }

        if (!uiComponent.loopListView.ListViewInited)
        {
            uiComponent.loopListView.InitListView(totalCount, OnGetItemByIndex);
        }
        else
        {
            uiComponent.loopListView.SetListItemCount(totalCount, false);
            uiComponent.loopListView.RefreshAllShownItem();
        }
    }

    /// <summary>
    /// LoopListView2 回调：根据索引获取对应 Item
    /// </summary>
    LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
    {
        if (index < 0)
            return null;

        int treeTotalCount = mTreeItemCountMgr.GetTotalItemAndChildCount();

        // 最后一项是底部操作按钮
        if (index == treeTotalCount)
        {
            return GetActionButtonsItem(listView);
        }

        // TreeView 区域
        TreeViewItemCountData countData = mTreeItemCountMgr.QueryTreeItemByTotalIndex(index);
        if (countData == null)
            return null;

        int groupIndex = countData.mTreeItemIndex;
        FriendGroupData groupData = mGroupDataList[groupIndex];

        // 分组头部
        if (!countData.IsChild(index))
        {
            LoopListViewItem2 item = listView.NewListViewItem("FriendsGroupItem");
            FriendsGroupItem itemScript = item.GetComponent<FriendsGroupItem>();

            if (!item.IsInitHandlerCalled)
            {
                item.IsInitHandlerCalled = true;
                itemScript.InitListItem();
                itemScript.SetClickCallback(OnGroupExpandClicked);
            }

            itemScript.SetItemListData(index, groupIndex, groupData.groupName,
                groupData.friends.Count, countData.mIsExpand);
            return item;
        }
        // 好友卡片子项
        else
        {
            int childIndex = countData.GetChildIndex(index);
            FriendData friendData = groupData.friends[childIndex];

            LoopListViewItem2 item = listView.NewListViewItem("FriendsChildItem");
            FriendsChildItem itemScript = item.GetComponent<FriendsChildItem>();

            if (!item.IsInitHandlerCalled)
            {
                item.IsInitHandlerCalled = true;
                itemScript.InitListItem();
                itemScript.SetAtButtonCallback(OnAtButtonClicked);
            }

            itemScript.SetItemListData(index, friendData, groupIndex, childIndex);
            return item;
        }
    }

    /// <summary>
    /// 获取底部操作按钮 Item
    /// </summary>
    LoopListViewItem2 GetActionButtonsItem(LoopListView2 listView)
    {
        LoopListViewItem2 item = listView.NewListViewItem("FriendsActionButtonsItem");
        FriendsActionButtonsItem itemScript = item.GetComponent<FriendsActionButtonsItem>();

        if (!item.IsInitHandlerCalled)
        {
            item.IsInitHandlerCalled = true;
            itemScript.InitListItem();
            itemScript.SetCallbacks(OnAddFriendButtonClick, OnCreateFriendButtonClick);
        }
        return item;
    }

    #endregion

    #region 展开/折叠交互

    /// <summary>
    /// 分组头部点击回调：切换展开/折叠状态
    /// </summary>
    void OnGroupExpandClicked(int groupIndex)
    {
        mTreeItemCountMgr.ToggleItemExpand(groupIndex);
        mGroupDataList[groupIndex].isExpanded = !mGroupDataList[groupIndex].isExpanded;

        int totalCount = mTreeItemCountMgr.GetTotalItemAndChildCount() + 1;
        uiComponent.loopListView.SetListItemCount(totalCount, false);
        uiComponent.loopListView.RefreshAllShownItem();
    }

    #endregion

    #region 按钮事件回调

    /// <summary>
    /// 通知铃铛按钮点击
    /// </summary>
    public void OnNotificationButtonClick()
    {
        Debug.Log("[FriendsUI] Notification button clicked");
        // TODO: 打开通知列表弹窗
    }

    /// <summary>
    /// 查看邀请按钮点击
    /// </summary>
    public void OnViewInviteButtonClick()
    {
        Debug.Log("[FriendsUI] View invite clicked");
        // TODO: 跳转到邀请详情
    }

    /// <summary>
    /// Friends上下文区域添加按钮点击
    /// </summary>
    public void OnContextAddButtonClick()
    {
        Debug.Log("[FriendsUI] Context add button clicked");
        // TODO: 添加关系上下文
    }

    /// <summary>
    /// 好友卡片 @ 按钮点击
    /// </summary>
    void OnAtButtonClicked(FriendData friendData)
    {
        Debug.Log($"[FriendsUI] @ button clicked for {friendData.friendName}");
        // TODO: 打开与该好友的聊天/占卜界面
    }

    /// <summary>
    /// 底部添加好友按钮点击
    /// </summary>
    void OnAddFriendButtonClick()
    {
        Debug.Log("[FriendsUI] Add friend button clicked");
        // TODO: 打开添加好友界面
    }

    /// <summary>
    /// 底部创建好友按钮点击
    /// </summary>
    void OnCreateFriendButtonClick()
    {
        Debug.Log("[FriendsUI] Create friend button clicked");
        // TODO: 打开创建AI好友界面
    }

    #endregion
}
