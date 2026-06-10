using System;
using GamerFrameWork.UIFrameWork;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 好友界面底部操作按钮 Item
/// 添加好友 / 创建好友
/// </summary>
public class FriendsActionButtonsItem : MonoBehaviour, IGamerUIViewListItem
{
    [Header("UI组件")]
    public Button addFriendButton;
    public Button createFriendButton;
    public Text addFriendText;
    public Text createFriendText;

    private Action mAddFriendCallback;
    private Action mCreateFriendCallback;

    #region IGamerUIViewListItem 实现

    public void InitListItem()
    {
        if (addFriendButton != null)
        {
            addFriendButton.onClick.AddListener(OnAddFriendClick);
        }

        if (createFriendButton != null)
        {
            createFriendButton.onClick.AddListener(OnCreateFriendClick);
        }
    }

    public void SetItemListData(int index, params object[] data)
    {
        // 此 Item 无需动态数据绑定
    }

    public void OnRelease() { }

    #endregion

    public void SetCallbacks(Action addFriend, Action createFriend)
    {
        mAddFriendCallback = addFriend;
        mCreateFriendCallback = createFriend;
    }

    private void OnAddFriendClick()
    {
        mAddFriendCallback?.Invoke();
    }

    private void OnCreateFriendClick()
    {
        mCreateFriendCallback?.Invoke();
    }
}
