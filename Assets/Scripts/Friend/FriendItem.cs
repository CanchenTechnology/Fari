using System;
using System.Collections;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using UnityEngine;
using UnityEngine.UI;

public class FriendItem : MonoBehaviour
{
    public Sprite defaultSprite;
    public Image headImage;
    public Text nameText;
    public Text infoText;
    public Button atBtn;

    private FriendDataManager.FriendData data;
    private void OnEnable()
    {
        atBtn.onClick.AddListener(ContactFriendToUI);
    }
    private void OnDisable()
    {
        atBtn.onClick.RemoveListener(ContactFriendToUI);
    }
    private void ContactFriendToUI()
    {
        UIModule.Instance.GetWindow<NavigationUI>().OpenDialogUI();
        UIModule.Instance.GetWindow<DialogUI>().SendAtFriendsMessage(data);
    }
    /// <summary>
    /// 当前绑定的好友数据
    /// </summary>
    public FriendDataManager.FriendData Data => data;

    /// <summary>
    /// 设置好友数据显示
    /// </summary>
    public void SetData(Sprite sprite, string name, string info)
    {
        headImage.sprite = sprite ? sprite : defaultSprite;
        nameText.text = name;
        infoText.text = info;
    }

    /// <summary>
    /// 通过 FriendData 设置好友数据显示
    /// </summary>
    public void SetData(FriendDataManager.FriendData friendData)
    {
        data = friendData;
        SetData(friendData.headSprite, friendData.name, friendData.info);
    }

    /// <summary>
    /// 重置为池化状态（回收前调用）
    /// </summary>
    public void ResetForPool()
    {
        data = null;
        headImage.sprite = defaultSprite;
        nameText.text = string.Empty;
        infoText.text = string.Empty;
        
    }
}
