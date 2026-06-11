using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 用户聊天项
/// 显示用户发送的消息
/// </summary>
public class ChatItem : MonoBehaviour
{
    public Image headImage;
    public Text speakerName;
    public Text speakerContent;

    /// <summary>
    /// 设置消息数据
    /// </summary>
    public void SetData(ChatMessageData data)
    {
        if (data == null) return;

        if (speakerName != null)
        {
            speakerName.text = data.speakerName;
        }

        if (speakerContent != null)
        {
            speakerContent.text = data.content;
        }

        // 设置头像（如果资源管理器可用）
        if (headImage != null && !string.IsNullOrEmpty(data.headIconName))
        {
            // 尝试从资源管理器加载头像
            LoadHeadIcon(data.headIconName);
        }
    }

    /// <summary>
    /// 设置发言者名字
    /// </summary>
    public void SetSpeakerName(string name)
    {
        if (speakerName != null)
        {
            speakerName.text = name;
        }
    }

    /// <summary>
    /// 设置消息内容
    /// </summary>
    public void SetSpeakerContent(string content)
    {
        if (speakerContent != null)
        {
            speakerContent.text = content;
        }
    }

    /// <summary>
    /// 设置头像精灵
    /// </summary>
    public void SetHeadSprite(Sprite sprite)
    {
        if (headImage != null)
        {
            headImage.sprite = sprite;
        }
    }

    /// <summary>
    /// 加载头像资源
    /// </summary>
    private void LoadHeadIcon(string iconName)
    {
        // 如果项目中有资源管理器，可以在这里加载
        // 例如使用 ResManager.Get.GetSpriteByName(iconName)
        // 这里留空，根据实际项目资源管理方式实现
    }
}
