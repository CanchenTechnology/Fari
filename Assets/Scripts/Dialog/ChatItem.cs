using I2.Loc;
using SuperScrollView;
using System.Drawing;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 用户聊天项
/// 显示用户发送的消息
/// </summary>
public class ChatItem : MonoBehaviour
{
    //头像
    public Image headImage;
    //说话人的名字
    public Text speakerName;


    // 文本消息
    public Text mMsgText;

    // 消息背景框:聊天气泡
    public Image mItemBg;

    // 气泡尾巴
    public Image mArrow;



    // 图片消息显示Image
    public Image mMsgPic;

    // 图片消息遮罩区域（控制图片尺寸）
    public RectTransform mMsgPicMask;
    // 图片缩放比例（宽高）
    float mMsgPicScaleX = 0.7f;
    float mMsgPicScaleY = 0.7f;


    // 当前Item索引
    int mItemIndex = -1;


    /// <summary>
    /// 当前Item索引（只读）
    /// </summary>
    public int ItemIndex
    {
        get { return mItemIndex; }
    }

    /// <summary>
    /// 初始化
    /// </summary>
    public void Init()
    {

    }
    /// <summary>
    /// 设置消息数据
    /// </summary>
    public void SetItemData(ChatMessageData data,int itemIndex)
    {
        switch (data.messageType)
        {
            case MsgType.Str:
                SetStrMessage(data);
                break;
            case MsgType.PopupWindow:
                break;
            case MsgType.Picture:
                break;
            case MsgType.Voice:
                break;
            default:
                break;
        }
    }
    /// <summary>
    /// 设置文本信息
    /// </summary>
    /// <param name="data"></param>
    /// <summary>
    /// 设置文本信息
    /// </summary>
    /// <param name="data"></param>
    private void SetStrMessage(ChatMessageData data)
    {
        // 隐藏图片区域
        mMsgPicMask.gameObject.SetActive(false);

        // 显示文本
        mMsgText.gameObject.SetActive(true);

        // 设置文本内容
        mMsgText.text = data.content;

        // 【修改核心区：动态计算文本宽度】
        // 1. 获取文本无折行情况下的实际理想宽度 (比如 "ss" 只有几十像素)
        float preferredWidth = mMsgText.preferredWidth;

        // 2. 设定一个最大宽度，防止长文本不换行超出屏幕边界
        // 你可以根据你实际的游戏 UI 比例修改这个值 (例如 500f 或 600f)
        float maxTextWidth = 500f;

        // 3. 取两者较小值：短文本自动缩框，长文本触达极限宽度以备换行
        float targetTextWidth = preferredWidth < maxTextWidth ? preferredWidth : maxTextWidth;

        // 4. 将计算出的真实宽度强行赋给 Text 的 RectTransform
        mMsgText.rectTransform.sizeDelta = new Vector2(targetTextWidth, mMsgText.rectTransform.sizeDelta.y);

        // 5. 触发布局刷新（此时 ContentSizeFitter 会根据刚才给的定宽，自动算出换行后正确的 sizeDelta.y）
        LayoutRebuilder.ForceRebuildLayoutImmediate(mMsgText.rectTransform);

        // 显示背景气泡
        mItemBg.gameObject.SetActive(true);

        // 根据 Text 最终的尺寸动态调整背景尺寸
        Vector2 size = mItemBg.rectTransform.sizeDelta;
        size.x = targetTextWidth + 20; // 左右边距
        size.y = mMsgText.rectTransform.sizeDelta.y + 34; // 上下边距
        mItemBg.rectTransform.sizeDelta = size;

        // 调整整个Item高度
        RectTransform tf = gameObject.GetComponent<RectTransform>();
        float y = size.y;

        // 最小高度限制
        if (y < 75)
        {
            y = 75;
        }
        tf.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
    }


    private void SetPopupWindowMessage()
    { 
              
    }
    private void SetPictureMessage()
    {
        // 显示图片区域
        mMsgPicMask.gameObject.SetActive(true);

        // 隐藏文本
        mMsgText.gameObject.SetActive(false);

        //// 设置图片内容
        //mMsgPic.sprite = ResManager.Get.GetSpriteByName(data.mPicMsgSpriteName);

        // 获取原图尺寸
        float w = mMsgPic.overrideSprite.rect.width;
        float h = mMsgPic.overrideSprite.rect.height;

        // 按比例缩放图片
        mMsgPicMask.sizeDelta = new Vector2(w * mMsgPicScaleX, h * mMsgPicScaleY);

        // 设置头像
        //mIcon.sprite = ResManager.Get.GetSpriteByName(person.mHeadIcon);

        // 图片消息不使用文字气泡背景
        mItemBg.gameObject.SetActive(false);

        // 计算Item整体尺寸
        Vector2 size = Vector2.zero;
        size.x = mMsgPicMask.sizeDelta.x + 20;
        size.y = mMsgPicMask.sizeDelta.y + 20;

        RectTransform tf = gameObject.GetComponent<RectTransform>();
        float y = size.y;

        // 最小高度限制
        if (y < 75)
        {
            y = 75;
        }
        tf.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
    }
    
    private void LoadHeadIcon(string iconName)
    {
        // 如果项目中有资源管理器，可以在这里加载
        // 例如使用 ResManager.Get.GetSpriteByName(iconName)
        // 这里留空，根据实际项目资源管理方式实现
    }
}
