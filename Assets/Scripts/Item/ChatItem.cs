using System.Collections.Generic;
using System.Text;
using GamerFrameWork.UIFrameWork;
using UnityEngine;
using UnityEngine.UI;
using XFGameFrameWork;
using TMPro;

/// <summary>
/// 用户聊天项
/// 显示用户发送的消息
/// </summary>
public class ChatItem : MonoBehaviour
{
    [Header("聊天气泡尺寸控制")]
    [Tooltip("文字区域的最小宽度。文字很短时，气泡也不会比这个更窄。")]
    [SerializeField] private float minTextWidth = 40f;
    [Tooltip("文字最大宽度的保底值。即使可用空间很小，也会尽量保证至少这个宽度。")]
    [SerializeField] private float minMaxTextWidth = 120f;
    [Tooltip("气泡左右内边距总和。数值越大，文字左右留白越多。")]
    [SerializeField] private float bubbleHorizontalPadding = 20f;
    [Tooltip("气泡上下内边距总和。数值越大，文字上下留白越多。")]
    [SerializeField] private float bubbleVerticalPadding = 34f;
    [Tooltip("气泡距离聊天项边缘预留的外边距，用来限制最长气泡宽度。")]
    [SerializeField] private float bubbleOuterMargin = 64f;
    [Tooltip("AI 语音按钮/时长区域需要额外预留的高度。")]
    [SerializeField] private float voiceReservedHeight = 70f;
    [Tooltip("当文字气泡比头像矮时，聊天项高度在头像高度基础上额外增加的高度。")]
    [SerializeField] private float minTextItemAvatarExtraHeight = 12f;
    [Header("Message vertical spacing")]
    [SerializeField] private float leftTextMessageTopOffset = 72f;
    [SerializeField] private float rightTextMessageTopOffset = 18f;

    [Header("图片消息尺寸控制")]
    [Tooltip("图片消息的宽度缩放比例。")]
    [SerializeField] private float msgPicScaleX = 0.7f;
    [Tooltip("图片消息的高度缩放比例。")]
    [SerializeField] private float msgPicScaleY = 0.7f;
    [Tooltip("图片消息整体额外留白。")]
    [SerializeField] private float pictureMessagePadding = 20f;
    [Tooltip("当图片消息比头像矮时，聊天项高度在头像高度基础上额外增加的高度。")]
    [SerializeField] private float minPictureItemAvatarExtraHeight = 10f;

    //头像
    public Image headImage;
    //说话人的名字
    public TMP_Text speakerName;

    public Transform msgTrans;

    [Header("发送文本信息")]
    // 文本消息
    public TMP_Text mMsgText;

    // 消息背景框:聊天气泡

    public Image mItemBg;

    // 气泡尾巴
    public Image mArrow;


    [Header("发送图片信息")]
    // 图片消息显示Image
    public Image mMsgPic;

    // 图片消息遮罩区域（控制图片尺寸）
    public RectTransform mMsgPicMask;
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
    public TMP_Text ttsTimeText;  //记录语音的时常

    [Header("列表底部安全距离")]
    [Tooltip("最后一条消息底部额外留白，避免被底部输入框挡住。")]
    [SerializeField] private float lastMessageBottomPadding = 110f;

    /// <summary>TTS 播放回调（由 DialogUI 绑定）</summary>
    public System.Action<ChatItem> onTTSPlayClicked;

    // 当前Item索引
    int mItemIndex = -1;
    private int avatarRequestVersion;


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
        mItemIndex = itemIndex;

        // 隐藏所有内容区域
        msgTrans.gameObject.SetActive(false);
        dailyCardTrans.gameObject.SetActive(false);
        friendContentTrans.gameObject.SetActive(false);
        interactionCard3.gameObject.SetActive(false);
        interactionCard1.gameObject.SetActive(false);
        interactionCard5.gameObject.SetActive(false);
        if (ttsPlayButton != null)
            ttsPlayButton.gameObject.SetActive(false);
        if (ttsTimeText != null)
            ttsTimeText.gameObject.SetActive(false);
        ShowTTSLoading(false);


        SetSpeakerInfo(data);

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
                SetFriendContentMessage(data);
                break;
            case MsgType.DailyCard:
                dailyCardTrans.gameObject.SetActive(true);
                SetDailyCardMessage();
                break;
            case MsgType.InteractionCard3:
                interactionCard3.gameObject.SetActive(true);
                SetSpreadInteractionCard3(data);
                break;
            case MsgType.InteractionCard1:
                interactionCard1.gameObject.SetActive(true);
                SetSpreadInteractionCard1(data);
                break;
            case MsgType.InteractionCard5:
                interactionCard5.gameObject.SetActive(true);
                SetSpreadInteractionCard5(data);
                break;
            default:
                break;
        }
    }

    private void SetSpeakerInfo(ChatMessageData data)
    {
        if (data == null) return;
        avatarRequestVersion++;

        if (data.roleType == DialogRoleType.User)
        {
            if (speakerName != null)
                speakerName.text = DialogSystem.Instance != null
                    ? DialogSystem.Instance.UserName
                    : "我";

            SetUserAvatarFromCurrentConfig(avatarRequestVersion);
            return;
        }

        if (speakerName != null)
            speakerName.text = GetDivinerName(data.divinerType);

        // AI 回复头像暂时不自动赋值，保留 prefab 默认状态。
        // 后续接入角色头像时调用 SetAIAvatar / SetAIAvatarByResourceName。
    }

    private string GetDivinerName(DivinerType divinerType)
    {
        var dialogSystem = DialogSystem.Instance;
        if (dialogSystem == null)
            return divinerType == DivinerType.Tarot ? "塔罗师" : "占星师";

        return divinerType == DivinerType.Tarot
            ? dialogSystem.TarotDivinerName
            : dialogSystem.AstrologyDivinerName;
    }

    private void SetUserAvatarFromCurrentConfig(int requestVersion)
    {
        var iconName = DialogSystem.Instance != null ? DialogSystem.Instance.UserHeadIcon : "";
        if (!string.IsNullOrEmpty(iconName))
            SetAvatarByResourceName(iconName, false);

        if (isActiveAndEnabled)
            StartCoroutine(FriendAvatarImageUtility.LoadCurrentUserAvatarCoroutine((sprite, _) =>
            {
                if (requestVersion != avatarRequestVersion || sprite == null || headImage == null)
                    return;

                headImage.sprite = sprite;
                headImage.preserveAspect = true;
                headImage.enabled = true;
            }));
    }

    public void SetAIAvatar(Sprite avatarSprite)
    {
        if (avatarSprite == null || headImage == null) return;
        headImage.sprite = avatarSprite;
        headImage.preserveAspect = true;
        headImage.enabled = true;
    }

    public void SetAIAvatarByResourceName(string iconName)
    {
        SetAvatarByResourceName(iconName, true);
    }

    public void SetUserAvatar(Sprite avatarSprite)
    {
        if (avatarSprite == null || headImage == null) return;
        headImage.sprite = avatarSprite;
        headImage.preserveAspect = true;
        headImage.enabled = true;
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
        mMsgText.text = BuildDisplayText(data);

        // TTS 按钮：仅 AI 消息显示
        bool isAIMessage = data.roleType == DialogRoleType.AI;
        bool shouldShowTTSButton = isAIMessage
            && !string.IsNullOrEmpty(data.content)
            && data.ttsAudioReady;
        if (ttsPlayButton != null)
        {
            ttsPlayButton.gameObject.SetActive(shouldShowTTSButton);
        }
        SetTTSLength(isAIMessage ? data.ttsDurationSeconds : 0f);

        ApplyTextBubbleLayout(reserveVoiceSpace: shouldShowTTSButton);
    }

    private string BuildDisplayText(ChatMessageData data)
    {
        return NormalizeDisplayText(data?.content);
    }

    private string NormalizeDisplayText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        string value = text.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] lines = value.Split('\n');
        StringBuilder builder = new StringBuilder(value.Length);

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (builder.Length > 0)
                builder.Append('\n');
            builder.Append(line);
        }

        return builder.ToString();
    }

    private float ApplyLastMessageBottomPadding(float height)
    {
        if (lastMessageBottomPadding <= 0f || DialogSystem.Instance == null)
            return height;

        int messageCount = DialogSystem.Instance.GetMessageCount();
        bool isLastMessage = messageCount > 0 && mItemIndex == messageCount - 1;
        return isLastMessage ? height + lastMessageBottomPadding : height;
    }

    private void ApplyTextBubbleLayout(bool reserveVoiceSpace, string layoutText = null)
    {
        if (mMsgText == null || mItemBg == null) return;

        string originalText = mMsgText.text;
        if (layoutText != null)
            mMsgText.text = layoutText;

        float preferredWidth = mMsgText.preferredWidth;
        float maxTextWidth = GetMaxTextWidth();
        float targetTextWidth = Mathf.Clamp(preferredWidth, minTextWidth, maxTextWidth);

        RectTransform textRect = mMsgText.rectTransform;
        RectTransform bubbleRect = mItemBg.rectTransform;

        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetTextWidth);
        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        float targetTextHeight = mMsgText.GetPreferredValues(mMsgText.text, targetTextWidth, Mathf.Infinity).y;
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetTextHeight);

        // 显示背景气泡
        mItemBg.gameObject.SetActive(true);

        // 根据 Text 最终的尺寸动态调整背景尺寸
        Vector2 size = bubbleRect.sizeDelta;
        size.x = targetTextWidth + bubbleHorizontalPadding;
        size.y = textRect.sizeDelta.y + bubbleVerticalPadding;
        bubbleRect.sizeDelta = size;
        ApplyTextPaddingToBubble(textRect, bubbleRect);
        ApplyTextMessageTopOffset(bubbleRect, reserveVoiceSpace);

        ApplyTextMessageItemHeight(size.y, reserveVoiceSpace);

        if (layoutText != null)
            mMsgText.text = originalText;

        // // ---- 渲染 AI 选项按钮 ----
        // if (data.options != null && data.options.Count > 0)
        // {
        //     RenderOptionButtons(data.options);
        // }
    }

    private void ApplyTextMessageItemHeight(float bubbleHeight, bool reserveVoiceSpace)
    {
        RectTransform tf = transform as RectTransform;
        if (tf == null) return;

        float topOffset = GetTopOffset(msgTrans);
        float y = topOffset + bubbleHeight;

        if (reserveVoiceSpace)
            y = Mathf.Max(y, bubbleHeight + GetVoiceAreaHeight());

        if (headImage != null && headImage.gameObject.activeSelf && headImage.TryGetComponent(out RectTransform headRect))
            y = Mathf.Max(y, headRect.sizeDelta.y + minTextItemAvatarExtraHeight);

        y = ApplyLastMessageBottomPadding(y);
        tf.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
    }

    private float GetVoiceAreaHeight()
    {
        float height = voiceReservedHeight;

        RectTransform voiceRoot = null;
        if (ttsPlayButton != null)
            voiceRoot = ttsPlayButton.transform.parent as RectTransform;

        if (voiceRoot != null)
            height = Mathf.Max(height, ResolveRectHeight(voiceRoot));
        else if (ttsPlayButton != null && ttsPlayButton.transform is RectTransform buttonRect)
            height = Mathf.Max(height, ResolveRectHeight(buttonRect));

        if (ttsTimeText != null && ttsTimeText.transform is RectTransform timeRect)
            height = Mathf.Max(height, ResolveRectHeight(timeRect));

        return height;
    }

    private float ResolveRectHeight(RectTransform rect)
    {
        if (rect == null) return 0f;

        float height = rect.rect.height;
        if (height <= 0f)
            height = Mathf.Abs(rect.sizeDelta.y);
        return height;
    }

    private void ApplyTextPaddingToBubble(RectTransform textRect, RectTransform bubbleRect)
    {
        if (textRect == null || bubbleRect == null) return;

        float horizontalInset = bubbleHorizontalPadding * 0.5f;
        float verticalInset = bubbleVerticalPadding * 0.5f;
        Vector2 textPos = textRect.anchoredPosition;
        bool rightAligned = bubbleRect.pivot.x > 0.5f;

        textPos.x = rightAligned ? -horizontalInset : horizontalInset;
        textPos.y = -verticalInset;
        textRect.anchoredPosition = textPos;
    }

    private void ApplyTextMessageTopOffset(RectTransform bubbleRect, bool reserveVoiceSpace)
    {
        if (!(msgTrans is RectTransform msgRect) || bubbleRect == null) return;

        bool rightAligned = bubbleRect.pivot.x > 0.5f;
        float targetTopOffset = rightAligned ? rightTextMessageTopOffset : leftTextMessageTopOffset;
        if (reserveVoiceSpace && !rightAligned)
            targetTopOffset = Mathf.Max(targetTopOffset, GetVoiceAreaHeight());
        if (targetTopOffset < 0f) return;

        float currentTopOffset = GetTopOffset(msgRect);
        if (Mathf.Abs(currentTopOffset - targetTopOffset) <= 0.1f) return;

        Vector2 pos = msgRect.anchoredPosition;
        pos.y = -targetTopOffset;
        msgRect.anchoredPosition = pos;
    }

    private void ApplyItemHeight(float contentHeight, Transform contentTrans, float avatarExtraHeight, bool usePivotAwareTopOffset = false)
    {
        RectTransform tf = gameObject.GetComponent<RectTransform>();
        if (tf == null) return;

        float topOffset = usePivotAwareTopOffset
            ? GetPivotAwareTopOffset(contentTrans)
            : GetTopOffset(contentTrans);
        float y = topOffset + contentHeight;

        if (headImage != null && headImage.gameObject.activeSelf && headImage.TryGetComponent(out RectTransform headRect))
            y = Mathf.Max(y, headRect.sizeDelta.y + avatarExtraHeight);

        y = ApplyLastMessageBottomPadding(y);
        tf.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
    }

    private float GetTopOffset(Transform contentTrans)
    {
        if (contentTrans is RectTransform contentRect)
            return Mathf.Max(0f, -contentRect.anchoredPosition.y);

        return 0f;
    }

    private float GetPivotAwareTopOffset(Transform contentTrans)
    {
        if (!(contentTrans is RectTransform contentRect))
            return GetTopOffset(contentTrans);

        float height = contentRect.rect.height;
        if (height <= 0f)
            height = Mathf.Abs(contentRect.sizeDelta.y);

        return Mathf.Max(0f, -contentRect.anchoredPosition.y - (1f - contentRect.pivot.y) * height);
    }

    private float GetMaxTextWidth()
    {
        RectTransform itemRect = transform as RectTransform;
        float availableWidth = itemRect != null ? itemRect.rect.width : 0f;

        if (availableWidth <= 0f && transform.parent is RectTransform parentRect)
            availableWidth = parentRect.rect.width;

        if (availableWidth <= 0f)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            availableWidth = canvasRect != null ? canvasRect.rect.width : 0f;
        }

        if (availableWidth <= 0f)
            availableWidth = 600f;

        float anchorInset = 0f;
        if (msgTrans is RectTransform msgRect)
            anchorInset = Mathf.Abs(msgRect.anchoredPosition.x);

        float maxWidth = availableWidth - anchorInset - bubbleHorizontalPadding - bubbleOuterMargin;
        return Mathf.Max(minMaxTextWidth, maxWidth);
    }

    /// <summary>
    /// 流式更新文本（仅更新文字+气泡大小，不切换 GameObject 避免闪烁）
    /// </summary>
    public void UpdateStreamingText(string newText)
    {
        if (mMsgText == null) return;

        mMsgText.text = NormalizeDisplayText(newText);

        // 流式输出未完成前不开放 TTS，避免合成半句文本。
        if (ttsPlayButton != null)
        {
            ttsPlayButton.gameObject.SetActive(false);
        }

        ApplyTextBubbleLayout(reserveVoiceSpace: false);
    }

    public void PrepareSyncedSpeech(string fullText, float durationSeconds)
    {
        if (mMsgText == null) return;

        if (ttsPlayButton != null)
            ttsPlayButton.gameObject.SetActive(!string.IsNullOrEmpty(fullText));

        SetTTSLength(durationSeconds);
        mMsgText.text = "";
        ApplyTextBubbleLayout(reserveVoiceSpace: true, layoutText: NormalizeDisplayText(fullText));
    }

    public void SetSyncedSpeechProgress(string fullText, float normalized)
    {
        if (mMsgText == null) return;

        fullText = NormalizeDisplayText(fullText);
        int visibleCount = Mathf.Clamp(
            Mathf.CeilToInt(fullText.Length * Mathf.Clamp01(normalized)),
            0,
            fullText.Length);
        mMsgText.text = visibleCount >= fullText.Length
            ? fullText
            : fullText.Substring(0, visibleCount);
    }

    public void CompleteSyncedSpeech(string fullText)
    {
        if (mMsgText == null) return;

        mMsgText.text = NormalizeDisplayText(fullText);
        ApplyTextBubbleLayout(reserveVoiceSpace: true);
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
        mMsgPicMask.sizeDelta = new Vector2(w * msgPicScaleX, h * msgPicScaleY);

        // 设置头像
        //mIcon.sprite = ResManager.Get.GetSpriteByName(person.mHeadIcon);

        // 图片消息不使用文字气泡背景
        mItemBg.gameObject.SetActive(false);

        // 计算Item整体尺寸
        Vector2 size = Vector2.zero;
        size.x = mMsgPicMask.sizeDelta.x + pictureMessagePadding;
        size.y = mMsgPicMask.sizeDelta.y + pictureMessagePadding;

        ApplyItemHeight(size.y, msgTrans, minPictureItemAvatarExtraHeight);

    }

    private void SetContentSizeMessage(Transform targetTrans, bool usePivotAwareTopOffset = false)
    {
        if (targetTrans == null) return;

        RectTransform targetRect = targetTrans.GetComponent<RectTransform>();
        if (targetRect == null) return;

        float height = targetRect.rect.height;
        if (height <= 0f)
            height = Mathf.Abs(targetRect.sizeDelta.y);

        ApplyItemHeight(height, targetTrans, 0f, usePivotAwareTopOffset);
    }

    private void SetDailyCardContentSizeMessage(Transform targetTrans)
    {
        if (targetTrans == null) return;

        RectTransform targetRect = targetTrans.GetComponent<RectTransform>();
        if (targetRect == null) return;

        float height = targetRect.rect.height;
        if (height <= 0f)
            height = Mathf.Abs(targetRect.sizeDelta.y);

        ApplyItemHeight(height, targetTrans, 0f, true);
    }
    private void SetDailyCardMessage()
    {
        // 从 dailyCardTrans 获取 DailyCardBox 组件并填充今日牌数据
        var dailyCardBox = dailyCardTrans?.GetComponent<DailyCardBox>();
        if (dailyCardBox != null && DivinationEngine.Instance?.TodayCard.HasValue == true)
        {
            var (card, upright) = DivinationEngine.Instance.TodayCard.Value;
            var oracleService = DailyOracleService.Instance;
            var preparedReading = oracleService?.CachedPreparedReading;
            var oraclePayload = preparedReading != null && preparedReading.IsFor(card, upright)
                ? preparedReading.oraclePayload
                : oracleService != null && oracleService.IsCachedOracleFor(card, upright)
                    ? oracleService.CachedPayload
                    : null;
            var cardSprite = preparedReading != null && preparedReading.IsFor(card, upright)
                ? preparedReading.cardIcon
                : TarotSpriteLoader.Load(card.cardId);

            dailyCardBox.SetCardData(card, upright, oraclePayload, cardSprite);

            if (oraclePayload == null && oracleService != null)
            {
                oracleService.RequestDailyOracle(card, upright, (payload) =>
                {
                    if (dailyCardBox != null && dailyCardBox.gameObject.activeInHierarchy)
                    {
                        dailyCardBox.SetCardData(card, upright, payload, cardSprite);
                        SetDailyCardContentSizeMessage(dailyCardTrans);
                    }
                });
            }
        }
        else
        {
            Debug.LogWarning($"[ChatItem] DailyCardBox 跳过赋值: dailyCardBox={dailyCardBox != null}, TodayCard={DivinationEngine.Instance?.TodayCard.HasValue}");
        }

        SetDailyCardContentSizeMessage(dailyCardTrans);
    }
    public void SetFriendContentMessage()
    {
        SetContentSizeMessage(friendContentTrans);
    }

    public void SetFriendContentMessage(ChatMessageData data)
    {
        if (data != null && friendContentTrans != null)
        {
            var texts = friendContentTrans.GetComponentsInChildren<TMP_Text>(true);
            string title = string.IsNullOrWhiteSpace(data.friendName) ? "@好友" : $"@{data.friendName}";
            string detail = string.IsNullOrWhiteSpace(data.friendContext) ? data.content : data.friendContext;

            if (texts.Length > 0) texts[0].text = title;
            if (texts.Length > 1) texts[1].text = detail;
        }

        SetContentSizeMessage(friendContentTrans);
    }
    public void SetSpreadInteractionCard3()
    {
        SetContentSizeMessage(interactionCard3.transform, true);
    }

    /// <summary>
    /// 初始化三排牌阵（带数据）
    /// </summary>
    public void SetSpreadInteractionCard3(string spreadKind)
    {
        SetSpreadInteractionCard3(new ChatMessageData { spreadKind = spreadKind });
    }

    /// <summary>
    /// 初始化三排牌阵（带消息数据）
    /// </summary>
    public void SetSpreadInteractionCard3(ChatMessageData data)
    {
        if (interactionCard3 == null) return;

        string spreadKind = data?.spreadKind;

        // 从 DivinationEngine 获取牌阵定义
        SpreadDefinition spreadDef = null;
        if (DivinationEngine.Instance != null)
            spreadDef = DivinationEngine.Instance.GetSpreadDefinition(spreadKind);

        interactionCard3.Setup(spreadDef, data);

        // 绑定事件到 DialogUI
        var dialogUI = UIModule.Instance.GetWindow<DialogUI>();
        if (dialogUI != null)
            dialogUI.WireUpInteractionCard3(interactionCard3);

        SetContentSizeMessage(interactionCard3.transform, true);
    }

    public void SetSpreadInteractionCard1()
    {
        SetContentSizeMessage(interactionCard1.transform, true);
    }

    /// <summary>
    /// 初始化单排牌阵（带数据）
    /// </summary>
    public void SetSpreadInteractionCard1(string spreadKind)
    {
        SetSpreadInteractionCard1(new ChatMessageData { spreadKind = spreadKind });
    }

    /// <summary>
    /// 初始化单排牌阵（带消息数据）
    /// </summary>
    public void SetSpreadInteractionCard1(ChatMessageData data)
    {
        if (interactionCard1 == null) return;

        string spreadKind = data?.spreadKind;

        // 从 DivinationEngine 获取牌阵定义
        SpreadDefinition spreadDef = null;
        if (DivinationEngine.Instance != null)
            spreadDef = DivinationEngine.Instance.GetSpreadDefinition(spreadKind);

        interactionCard1.Setup(spreadDef, data);

        // 绑定事件到 DialogUI
        var dialogUI = UIModule.Instance.GetWindow<DialogUI>();
        if (dialogUI != null)
            dialogUI.WireUpInteractionCard1(interactionCard1);

        SetContentSizeMessage(interactionCard1.transform, true);
    }

    public void SetSpreadInteractionCard5()
    {
        SetContentSizeMessage(interactionCard5.transform, true);
    }

    /// <summary>
    /// 初始化五排牌阵（带数据）
    /// </summary>
    public void SetSpreadInteractionCard5(string spreadKind)
    {
        SetSpreadInteractionCard5(new ChatMessageData { spreadKind = spreadKind });
    }

    /// <summary>
    /// 初始化五排牌阵（带消息数据）
    /// </summary>
    public void SetSpreadInteractionCard5(ChatMessageData data)
    {
        if (interactionCard5 == null) return;

        string spreadKind = data?.spreadKind;

        // 从 DivinationEngine 获取牌阵定义
        SpreadDefinition spreadDef = null;
        if (DivinationEngine.Instance != null)
            spreadDef = DivinationEngine.Instance.GetSpreadDefinition(spreadKind);

        interactionCard5.Setup(spreadDef, data);

        // 绑定事件到 DialogUI
        var dialogUI = UIModule.Instance.GetWindow<DialogUI>();
        if (dialogUI != null)
            dialogUI.WireUpInteractionCard5(interactionCard5);

        SetContentSizeMessage(interactionCard5.transform, true);
    }

    private void LoadHeadIcon(string iconName)
    {
        SetAvatarByResourceName(iconName, true);
    }

    private void SetAvatarByResourceName(string iconName, bool logMissing = true)
    {
        if (headImage == null || string.IsNullOrEmpty(iconName)) return;

        var sprite = Resources.Load<Sprite>(iconName);
        if (sprite != null)
        {
            headImage.sprite = sprite;
            headImage.preserveAspect = true;
            headImage.enabled = true;
        }
        else if (logMissing)
        {
            Debug.LogWarning($"[ChatItem] 头像资源未找到: {iconName}");
        }
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
        if (ttsLoadingIcon != null && (ttsPlayButton == null || ttsLoadingIcon != ttsPlayButton.gameObject))
        {
            ttsLoadingIcon.SetActive(show);
        }
        if (ttsPlayButton != null)
        {
            ttsPlayButton.interactable = !show;
        }
    }

    public void SetTTSLength(float seconds)
    {
        if (ttsTimeText == null) return;

        bool hasDuration = seconds > 0.01f;
        ttsTimeText.gameObject.SetActive(hasDuration);
        ttsTimeText.text = hasDuration ? FormatDuration(seconds) : "";
    }

    private string FormatDuration(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainSeconds = totalSeconds % 60;
        return $"{minutes}:{remainSeconds:00}";
    }

    /// <summary>
    /// 更新流式文本时同步更新 TTS 按钮可见性
    /// </summary>
    public void UpdateTTSButtonAfterStream(string text)
    {
        bool hasText = !string.IsNullOrEmpty(text);
        if (ttsPlayButton != null)
        {
            ttsPlayButton.gameObject.SetActive(hasText);
        }

        if (hasText)
            ApplyTextBubbleLayout(reserveVoiceSpace: true, layoutText: NormalizeDisplayText(text));
    }

    #endregion

}
