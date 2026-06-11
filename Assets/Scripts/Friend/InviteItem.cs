using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InviteItem : MonoBehaviour
{
    public Image headImage;
    public Text nameText;
    public Text infoText;
    public Button atBtn;

    public Sprite defaultSprite;

    private FriendDataManager.InviteData data;

    /// <summary>
    /// 当前绑定的邀请数据
    /// </summary>
    public FriendDataManager.InviteData Data => data;

    /// <summary>
    /// 设置邀请数据显示
    /// </summary>
    public void SetData(Sprite sprite, string name, string info)
    {
        headImage.sprite = sprite ? sprite : defaultSprite;
        nameText.text = name;
        infoText.text = info;
    }

    /// <summary>
    /// 通过 InviteData 设置邀请数据显示
    /// </summary>
    public void SetData(FriendDataManager.InviteData inviteData)
    {
        data = inviteData;
        SetData(inviteData.headSprite, inviteData.name, inviteData.info);
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
