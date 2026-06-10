using System;
using GamerFrameWork.UIFrameWork;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 好友卡片子项 Item
/// 显示头像、名字、好友类型、城市、@按钮
/// </summary>
public class FriendsChildItem : MonoBehaviour, IGamerUIViewListItem
{
    [Header("UI组件")]
    public Image avatarImage;
    public Text nameText;
    public Text typeText;
    public Text cityText;
    public Button atButton;

    private FriendData mFriendData;
    private int mGroupIndex;
    private int mChildIndex;
    private Action<FriendData> mAtCallback;

    #region IGamerUIViewListItem 实现

    public void InitListItem()
    {
        if (atButton != null)
        {
            atButton.onClick.AddListener(OnAtButtonClick);
        }
    }

    public void SetItemListData(int index, params object[] data)
    {
        // data[0] = FriendData
        // data[1] = groupIndex (int)
        // data[2] = childIndex (int)
        if (data.Length >= 3)
        {
            mFriendData = data[0] as FriendData;
            mGroupIndex = (int)data[1];
            mChildIndex = (int)data[2];

            BindFriendData();
        }
    }

    public void OnRelease()
    {
        mFriendData = null;
    }

    #endregion

    public void SetAtButtonCallback(Action<FriendData> callback)
    {
        mAtCallback = callback;
    }

    private void BindFriendData()
    {
        if (mFriendData == null)
            return;

        if (nameText != null)
            nameText.text = mFriendData.friendName;

        if (typeText != null)
            typeText.text = mFriendData.friendType;

        if (cityText != null)
            cityText.text = mFriendData.friendCity;

        if (avatarImage != null && mFriendData.friendAvatar != null)
        {
            avatarImage.sprite = mFriendData.friendAvatar;
        }
    }

    private void OnAtButtonClick()
    {
        if (mFriendData != null)
        {
            mAtCallback?.Invoke(mFriendData);
        }
    }
}
