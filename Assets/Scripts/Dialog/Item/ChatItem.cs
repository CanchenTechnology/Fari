using GamerFrameWork.UIFrameWork;
using UnityEngine;
using UnityEngine.UI;
using XFGameFrameWork;

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

    public Transform msgTrans;

    [Header("发送文本信息")]
    // 文本消息
    public Text mMsgText;

    // 消息背景框:聊天气泡

    public Image mItemBg;

    // 气泡尾巴
    public Image mArrow;


    [Header("发送图片信息")]
    // 图片消息显示Image
    public Image mMsgPic;

    // 图片消息遮罩区域（控制图片尺寸）
    public RectTransform mMsgPicMask;
    // 图片缩放比例（宽高）
    float mMsgPicScaleX = 0.7f;
    float mMsgPicScaleY = 0.7f;


    [Header("今日牌")]
    public Transform dailyCardTrans;

    [Header("好友关系")]
    public Transform friendContentTrans;

    [Header("牌阵")]
    public SpreadInteractionCard3 interactionCard3;//三排阵
    public SpreadInteractionCard5 interactionCard5;//五排阵
    public SpreadInteractionCard1 interactionCard1; //单排阵

    [Header("TTS 语音播放")]
    public Button ttsPlayButton;        // 播放按钮（手动拖拽绑定或代码查找）
    public GameObject ttsLoadingIcon;   // 加载中旋转图标（可选）

    /// <summary>TTS 播放回调（由 DialogUI 绑定）</summary>
    public System.Action<ChatItem> onTTSPlayClicked;

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
        if (ttsPlayButton != null)
        {
            ttsPlayButton.onClick.RemoveAllListeners();
            ttsPlayButton.onClick.AddListener(OnTTSPlayButtonClick);
        }
    }
    /// <summary>
    /// 设置消息数据
    /// </summary>
    public void SetItemData(ChatMessageData data, int itemIndex)
    {
        msgTrans.gameObject.SetActive(false);
        dailyCardTrans.gameObject.SetActive(false);
        friendContentTrans.gameObject.SetActive(false);
        interactionCard3.gameObject.SetActive(false);
        interactionCard1.gameObject.SetActive(false);
        interactionCard5.gameObject.SetActive(false);
        switch (data.messageType)
        {
            case MsgType.Str:
                msgTrans.gameObject.SetActive(true);
                SetStrMessage(data);
                break;
            case MsgType.PopupWindow:
                SetPopupWindowMessage();
                break;
            case MsgType.Picture:
                msgTrans.gameObject.SetActive(true);
                SetPictureMessage();
                break;
            case MsgType.Voice:
                break;

            case MsgType.AtFriend:
                friendContentTrans.gameObject.SetActive(true);
                SetFriendContentMessage();
                break;
            case MsgType.DailyCard:
                dailyCardTrans.gameObject.SetActive(true);
                SetDailyCardMessage();
                break;
            case MsgType.InteractionCard3:
                interactionCard3.gameObject.SetActive(true);
                SetSpreadInteractionCard3(data.spreadKind);
                break;
            case MsgType.InteractionCard1:
                interactionCard1.gameObject.SetActive(true);
                SetSpreadInteractionCard1(data.spreadKind);
                break;
            case MsgType.InteractionCard5:
                interactionCard5.gameObject.SetActive(true);
                SetSpreadInteractionCard5(data.spreadKind);
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

        // TTS 按钮：仅 AI 消息显示
        if (ttsPlayButton != null)
        {
            bool isAIMessage = data.roleType == DialogRoleType.AI;
            ttsPlayButton.gameObject.SetActive(isAIMessage && !string.IsNullOrEmpty(data.content));
        }

        // 【修改核心区：动态计算文本宽度】
        // 1. 获取文本无折行情况下的实际理想宽度 (比如 "ss" 只有几十像素)
        float preferredWidth = mMsgText.preferredWidth;

        // 2. 设定一个最大宽度，防止长文本不换行超出屏幕边界
        // 你可以根据你实际的游戏 UI 比例修改这个值
        float maxTextWidth = Screen.width - 500;

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
        if (y < headImage.GetComponent<RectTransform>().sizeDelta.y)
        {
            y = headImage.GetComponent<RectTransform>().sizeDelta.y+10;
        }
        tf.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
    }

    /// <summary>
    /// 流式更新文本（仅更新文字+气泡大小，不切换 GameObject 避免闪烁）
    /// </summary>
    public void UpdateStreamingText(string newText)
    {
        if (mMsgText == null) return;

        mMsgText.text = newText;

        // 流式更新时也更新 TTS 按钮可见性
        if (ttsPlayButton != null)
        {
            ttsPlayButton.gameObject.SetActive(!string.IsNullOrEmpty(newText));
        }

        float preferredWidth = mMsgText.preferredWidth;
        float maxTextWidth = Screen.width - 500;
        float targetTextWidth = preferredWidth < maxTextWidth ? preferredWidth : maxTextWidth;

        mMsgText.rectTransform.sizeDelta = new Vector2(targetTextWidth, mMsgText.rectTransform.sizeDelta.y);
        LayoutRebuilder.ForceRebuildLayoutImmediate(mMsgText.rectTransform);

        if (mItemBg != null)
        {
            Vector2 size = mItemBg.rectTransform.sizeDelta;
            size.x = targetTextWidth + 20;
            size.y = mMsgText.rectTransform.sizeDelta.y + 34;
            mItemBg.rectTransform.sizeDelta = size;

            RectTransform tf = gameObject.GetComponent<RectTransform>();
            float y = size.y;
            if (y < headImage.GetComponent<RectTransform>().sizeDelta.y)
            {
                y = headImage.GetComponent<RectTransform>().sizeDelta.y + 10;
            }
            tf.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
        }
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
        if (y < headImage.GetComponent<RectTransform>().sizeDelta.y)
        {
            y = headImage.GetComponent<RectTransform>().sizeDelta.y+10;
        }
        tf.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
    }

    private void SetContentSizeMessage(Transform targetTrans)
    {
        Vector2 size = Vector2.zero;
        size.x = this.GetComponent<RectTransform>().sizeDelta.x;
        size.y = targetTrans.GetComponent<RectTransform>().sizeDelta.y;
        this.GetComponent<RectTransform>().sizeDelta = size;
    }
    private void SetDailyCardMessage()
    {
        // 从 dailyCardTrans 获取 DailyCardBox 组件并填充今日牌数据
        var dailyCardBox = dailyCardTrans?.GetComponent<DailyCardBox>();
        if (dailyCardBox != null && DivinationEngine.Instance?.TodayCard.HasValue == true)
        {
            var (card, upright) = DivinationEngine.Instance.TodayCard.Value;

            // 标题
            if (dailyCardBox.cardTitleText != null)
                dailyCardBox.cardTitleText.text = "今日塔罗";

            // 牌名
            if (dailyCardBox.cardNameText != null)
                dailyCardBox.cardNameText.text = card.DisplayName(upright);

            // 描述（正逆位）
            if (dailyCardBox.cardDesText != null)
                dailyCardBox.cardDesText.text = upright ? "正位" : "逆位";

            // 卡牌图片
            if (dailyCardBox.cardImage != null)
            {
                var sprite = TarotSpriteLoader.Load(card.cardId);
                if (sprite != null)
                {
                    dailyCardBox.cardImage.sprite = sprite;
                    // 逆位旋转
                    dailyCardBox.cardImage.rectTransform.localRotation = upright
                        ? Quaternion.identity
                        : Quaternion.Euler(0, 0, 180);
                    Debug.Log($"[ChatItem] DailyCardBox 图片设置成功: {card.cardId} → {sprite.name}");
                }
                else
                {
                    Debug.LogWarning($"[ChatItem] DailyCardBox TarotSpriteLoader.Load 返回 null: cardId={card.cardId}, 图集状态={TarotSpriteLoader.IsReady}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ChatItem] DailyCardBox 跳过赋值: dailyCardBox={dailyCardBox != null}, TodayCard={DivinationEngine.Instance?.TodayCard.HasValue}");
        }

        SetContentSizeMessage(dailyCardTrans);
    }
    public void SetFriendContentMessage()
    {
        SetContentSizeMessage(friendContentTrans);
    }
    public void SetSpreadInteractionCard3()
    {
        SetContentSizeMessage(interactionCard3.transform);
    }

    /// <summary>
    /// 初始化三排牌阵（带数据）
    /// </summary>
    public void SetSpreadInteractionCard3(string spreadKind)
    {
        if (interactionCard3 == null) return;

        // 从 DivinationEngine 获取牌阵定义
        SpreadDefinition spreadDef = null;
        if (DivinationEngine.Instance != null)
            spreadDef = DivinationEngine.Instance.GetSpreadDefinition(spreadKind);

        interactionCard3.Setup(spreadDef);

        // 绑定事件到 DialogUI
        var dialogUI = UIModule.Instance.GetWindow<DialogUI>();
        if (dialogUI != null)
            dialogUI.WireUpInteractionCard3(interactionCard3);

        SetContentSizeMessage(interactionCard3.transform);
    }

    public void SetSpreadInteractionCard1()
    {
        SetContentSizeMessage(interactionCard1.transform);
    }

    /// <summary>
    /// 初始化单排牌阵（带数据）
    /// </summary>
    public void SetSpreadInteractionCard1(string spreadKind)
    {
        if (interactionCard1 == null) return;

        // 从 DivinationEngine 获取牌阵定义
        SpreadDefinition spreadDef = null;
        if (DivinationEngine.Instance != null)
            spreadDef = DivinationEngine.Instance.GetSpreadDefinition(spreadKind);

        interactionCard1.Setup(spreadDef);

        // 绑定事件到 DialogUI
        var dialogUI = UIModule.Instance.GetWindow<DialogUI>();
        if (dialogUI != null)
            dialogUI.WireUpInteractionCard1(interactionCard1);

        SetContentSizeMessage(interactionCard1.transform);
    }

    public void SetSpreadInteractionCard5()
    {
        SetContentSizeMessage(interactionCard5.transform);
    }

    /// <summary>
    /// 初始化五排牌阵（带数据）
    /// </summary>
    public void SetSpreadInteractionCard5(string spreadKind)
    {
        if (interactionCard5 == null) return;

        // 从 DivinationEngine 获取牌阵定义
        SpreadDefinition spreadDef = null;
        if (DivinationEngine.Instance != null)
            spreadDef = DivinationEngine.Instance.GetSpreadDefinition(spreadKind);

        interactionCard5.Setup(spreadDef);

        // 绑定事件到 DialogUI
        var dialogUI = UIModule.Instance.GetWindow<DialogUI>();
        if (dialogUI != null)
            dialogUI.WireUpInteractionCard5(interactionCard5);

        SetContentSizeMessage(interactionCard5.transform);
    }

    private void LoadHeadIcon(string iconName)
    {
        // 如果项目中有资源管理器，可以在这里加载
        // 例如使用 ResManager.Get.GetSpriteByName(iconName)
        // 这里留空，根据实际项目资源管理方式实现
    }

    #region TTS 语音播放

    /// <summary>
    /// TTS 播放按钮点击
    /// </summary>
    private void OnTTSPlayButtonClick()
    {
        onTTSPlayClicked?.Invoke(this);
    }

    /// <summary>
    /// 显示加载中状态（旋转图标）
    /// </summary>
    public void ShowTTSLoading(bool show)
    {
        if (ttsLoadingIcon != null)
        {
            ttsLoadingIcon.SetActive(show);
        }
        if (ttsPlayButton != null)
        {
            ttsPlayButton.interactable = !show;
        }
    }

    /// <summary>
    /// 更新流式文本时同步更新 TTS 按钮可见性
    /// </summary>
    public void UpdateTTSButtonAfterStream(string text)
    {
        if (ttsPlayButton != null)
        {
            ttsPlayButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    #endregion
}
