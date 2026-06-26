/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/9/2026 1:01:55 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using System.Collections;
using System.Collections.Generic;
using GamerFrameWork;
using XFGameFrameWork;
using GamerFrameWork.OracleRuntime;
using TMPro;
using UnityEngine.Video;


public class DialogUI : WindowBase
{
    public DialogUIComponent uiComponent;

    private LoopListView2 chatListView;

    /// <summary>对话系统实例</summary>
    private DialogSystem dialogSystem;
    /// <summary>占卜引擎实例</summary>
    private DivinationEngine divinationEngine;

    [Header("TTS 语音配置")]
    [Tooltip("是否启用 AI 消息的语音播放按钮")]
    public bool enableTTS = true;
    [Tooltip("塔罗师 TTS 音色")]
    public string tarotVoiceType = "zh_female_qingxin";
    [Tooltip("占星师 TTS 音色")]
    public string astrologyVoiceType = "zh_female_shuangkuaidv2";
    [Tooltip("单次 TTS 请求分段长度，需小于服务端 1200 字限制")]
    public int ttsSegmentMaxLength = 1100;
    [Tooltip("已废弃：现在 AI 文本先显示，TTS 只由用户点击语音按钮触发")]
    public bool autoPlayAITextWithVoice = false;
    [Tooltip("已废弃：现在不再等待语音生成后才显示 AI 文本")]
    public bool delayAITextUntilVoiceReady = false;

    private TTSManager ttsManager;
    private ChatItem _currentTTSItem; // 当前正在播放语音的 ChatItem
    private int _currentTTSMessageId = -1;
    private Coroutine _ttsPlaybackCoroutine;
    private Coroutine _cloudDialogLoadCoroutine;
    private Coroutine _dialogBackgroundVideoCoroutine;
    private VideoPlayer _dialogBackgroundVideoPlayer;
    private RenderTexture _dialogBackgroundVideoTextureInstance;
    private RenderTexture _sharedDialogBackgroundVideoTexture;
    private int _dialogBackgroundVideoPlayRequestId;
    private bool _dialogBackgroundVideoHasStarted;
    private readonly Dictionary<int, PreparedTTSAudio> _preparedTTSByMessageId = new Dictionary<int, PreparedTTSAudio>();
    private readonly Queue<System.Action> _pendingAIRequests = new Queue<System.Action>();
    private FriendDataManager.FriendData _activeAtFriend;
    private MobileKeyboardInputAdapter _keyboardInputAdapter;
    private const float CLOUD_DIALOG_LOAD_RETRY_SECONDS = 8f;
    private const float CLOUD_DIALOG_LOAD_RETRY_INTERVAL = 0.5f;

    private class PreparedTTSAudio
    {
        public readonly List<string> segments = new List<string>();
        public readonly List<AudioClip> clips = new List<AudioClip>();
        public float totalDuration;
    }

    #region 生命周期函数
    // 调用机制与 Mono Awake 一致
    public override void OnAwake()
    {
        uiComponent = gameObject.GetComponent<DialogUIComponent>();
        uiComponent.InitComponent(this);
        ResolveDialogBackgroundVideoReferences();
        ConfigureDialogBackgroundVideoPlayer();
        this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
        base.OnAwake();

        dialogSystem = DialogSystem.Instance;

        // 初始化 TTS 管理器
        InitTTSManager();

        // 初始化占卜引擎
        divinationEngine = DivinationEngine.Instance;
        if (divinationEngine == null)
        {
            var go = new GameObject("DivinationEngine");
            divinationEngine = go.AddComponent<DivinationEngine>();
        }

        chatListView = uiComponent.ChatScrollViewLoopListView2;

        chatListView.InitListView(0, OnGetChatItemByIndex);
        SetupKeyboardInputAdapter();

        // 订阅事件
        EventSystem.AddEvent(GameDataStr.RefreshChatUI, OnRefreshChatUI);
        EventSystem.AddEventListener<string>(GameDataStr.QuickQuestionSelected, OnQuickQuestionSelected);
        EventSystem.AddEventListener<string>(GameDataStr.CardTopicSelected, OnCardTopicSelected);

        UpdateDivinerInfo();
        RefreshAtFriendBox();
    }
    // 物体显示时执行
    public override void OnShow()
    {
        base.OnShow();
        PlayDialogBackgroundVideo();
        OracleForegroundEffects.Attach(this.Canvas, OracleForegroundEffectStyle.Dialog);
        ClearAtFriendSelection(false, false);
        RefreshSendButtonState();
        LoadCloudDialogState();
    }
    // 物体隐藏时执行
    public override void OnHide()
    {
        PauseDialogBackgroundVideo();
        OracleForegroundEffects.Detach(this.Canvas);
        base.OnHide();
    }
    // 物体销毁时执行
    public override void OnDestroy()
    {
        EventSystem.RemoveEvent(GameDataStr.RefreshChatUI, OnRefreshChatUI);
        EventSystem.RemoveEventListener<string>(GameDataStr.QuickQuestionSelected, OnQuickQuestionSelected);
        EventSystem.RemoveEventListener<string>(GameDataStr.CardTopicSelected, OnCardTopicSelected);
        ReleaseDialogBackgroundVideoTextureInstance();
        base.OnDestroy();
    }
    #endregion

    #region 初始化

    private void ResolveDialogBackgroundVideoReferences()
    {
        if (_dialogBackgroundVideoPlayer != null)
            return;

        VideoPlayer[] videoPlayers = gameObject.GetComponentsInChildren<VideoPlayer>(true);
        if (videoPlayers == null || videoPlayers.Length == 0)
            return;

        foreach (VideoPlayer videoPlayer in videoPlayers)
        {
            if (videoPlayer == null)
                continue;

            if (string.Equals(videoPlayer.gameObject.name, "video", System.StringComparison.OrdinalIgnoreCase))
            {
                _dialogBackgroundVideoPlayer = videoPlayer;
                break;
            }
        }

        if (_dialogBackgroundVideoPlayer == null)
            _dialogBackgroundVideoPlayer = videoPlayers[0];
    }

    private void ConfigureDialogBackgroundVideoPlayer()
    {
        ResolveDialogBackgroundVideoReferences();

        VideoPlayer videoPlayer = _dialogBackgroundVideoPlayer;
        if (videoPlayer == null)
            return;

        EnsureDialogBackgroundVideoTextureInstance(videoPlayer);
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = false;
        videoPlayer.isLooping = true;
    }

    private void EnsureDialogBackgroundVideoTextureInstance(VideoPlayer videoPlayer)
    {
        if (videoPlayer == null || _dialogBackgroundVideoTextureInstance != null)
            return;

        RenderTexture sourceTexture = videoPlayer.targetTexture;
        if (sourceTexture == null)
            return;

        _sharedDialogBackgroundVideoTexture = sourceTexture;
        RenderTextureDescriptor descriptor = sourceTexture.descriptor;
        _dialogBackgroundVideoTextureInstance = new RenderTexture(descriptor)
        {
            name = $"{sourceTexture.name}_DialogRuntime",
            filterMode = sourceTexture.filterMode,
            wrapMode = sourceTexture.wrapMode,
            anisoLevel = sourceTexture.anisoLevel,
        };
        _dialogBackgroundVideoTextureInstance.Create();

        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = _dialogBackgroundVideoTextureInstance;
        ApplyDialogBackgroundVideoTextureToRawImages(_sharedDialogBackgroundVideoTexture, _dialogBackgroundVideoTextureInstance);
    }

    private void ApplyDialogBackgroundVideoTextureToRawImages(Texture fromTexture, Texture toTexture)
    {
        if (fromTexture == null || toTexture == null)
            return;

        RawImage[] rawImages = gameObject.GetComponentsInChildren<RawImage>(true);
        if (rawImages == null)
            return;

        foreach (RawImage rawImage in rawImages)
        {
            if (rawImage != null && rawImage.texture == fromTexture)
                rawImage.texture = toTexture;
        }
    }

    private void PlayDialogBackgroundVideo()
    {
        ConfigureDialogBackgroundVideoPlayer();

        VideoPlayer videoPlayer = _dialogBackgroundVideoPlayer;
        if (videoPlayer == null)
            return;

        if (videoPlayer.isPlaying || _dialogBackgroundVideoCoroutine != null)
            return;

        _dialogBackgroundVideoCoroutine = uiComponent.StartCoroutine(PlayDialogBackgroundVideoRoutine(++_dialogBackgroundVideoPlayRequestId));
    }

    private void PauseDialogBackgroundVideo()
    {
        _dialogBackgroundVideoPlayRequestId++;
        if (_dialogBackgroundVideoCoroutine != null)
        {
            uiComponent.StopCoroutine(_dialogBackgroundVideoCoroutine);
            _dialogBackgroundVideoCoroutine = null;
        }

        VideoPlayer videoPlayer = _dialogBackgroundVideoPlayer;
        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Pause();
    }

    private IEnumerator PlayDialogBackgroundVideoRoutine(int requestId)
    {
        VideoPlayer videoPlayer = _dialogBackgroundVideoPlayer;
        if (videoPlayer == null)
        {
            _dialogBackgroundVideoCoroutine = null;
            yield break;
        }

        if (videoPlayer.clip == null && string.IsNullOrEmpty(videoPlayer.url))
        {
            _dialogBackgroundVideoCoroutine = null;
            yield break;
        }

        ConfigureDialogBackgroundVideoPlayer();

        if (!videoPlayer.isPrepared)
        {
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared)
            {
                if (requestId != _dialogBackgroundVideoPlayRequestId)
                {
                    _dialogBackgroundVideoCoroutine = null;
                    yield break;
                }

                yield return null;
            }
        }

        if (requestId != _dialogBackgroundVideoPlayRequestId)
        {
            _dialogBackgroundVideoCoroutine = null;
            yield break;
        }

        if (!_dialogBackgroundVideoHasStarted || (videoPlayer.length > 0 && videoPlayer.time >= videoPlayer.length - 0.05f))
        {
            videoPlayer.time = 0;
            _dialogBackgroundVideoHasStarted = true;
        }

        videoPlayer.Play();
        _dialogBackgroundVideoCoroutine = null;
    }

    private void ReleaseDialogBackgroundVideoTextureInstance()
    {
        PauseDialogBackgroundVideo();

        if (_dialogBackgroundVideoTextureInstance == null)
            return;

        if (_dialogBackgroundVideoPlayer != null && _dialogBackgroundVideoPlayer.targetTexture == _dialogBackgroundVideoTextureInstance)
            _dialogBackgroundVideoPlayer.targetTexture = _sharedDialogBackgroundVideoTexture;

        ApplyDialogBackgroundVideoTextureToRawImages(_dialogBackgroundVideoTextureInstance, _sharedDialogBackgroundVideoTexture);
        _dialogBackgroundVideoTextureInstance.Release();
        UnityEngine.Object.Destroy(_dialogBackgroundVideoTextureInstance);
        _dialogBackgroundVideoTextureInstance = null;
        _sharedDialogBackgroundVideoTexture = null;
    }

    private void LoadCloudDialogState()
    {
        if (dialogSystem == null) return;
        if (_cloudDialogLoadCoroutine != null) return;

        if (uiComponent != null)
            _cloudDialogLoadCoroutine = uiComponent.StartCoroutine(LoadCloudDialogStateRoutine());
    }

    private IEnumerator LoadCloudDialogStateRoutine()
    {
        bool memoryRequested = false;
        bool memoryCloudRefreshRequested = false;
        bool historyCompleted = false;
        bool historyRequestInFlight = false;
        float elapsed = 0f;

        while (elapsed <= CLOUD_DIALOG_LOAD_RETRY_SECONDS)
        {
            if (!memoryRequested)
            {
                memoryRequested = true;
                memoryCloudRefreshRequested = FirestoreManager.Instance != null && FirestoreManager.Instance.IsInitialized;
                MemoryUiStore.LoadLatest(source =>
                {
                    if (source != null)
                        dialogSystem.SetMemorySource(source);
                });
            }
            else if (!memoryCloudRefreshRequested && FirestoreManager.Instance != null && FirestoreManager.Instance.IsInitialized)
            {
                memoryCloudRefreshRequested = true;
                MemoryUiStore.LoadLatest(source =>
                {
                    if (source != null)
                        dialogSystem.SetMemorySource(source);
                });
            }

            var dialogHistoryStore = DialogHistoryFirestore.Instance;
            if (!historyCompleted && !historyRequestInFlight && dialogHistoryStore != null)
            {
                historyRequestInFlight = true;
                bool attemptedCloud = dialogHistoryStore.IsReady;
                dialogSystem.LoadCloudDialogHistoryOnce(loaded =>
                {
                    historyRequestInFlight = false;
                    if (loaded)
                    {
                        historyCompleted = true;
                        UpdateChatScrollView();
                        SyncActiveFriendContextToDialogSystem();
                    }
                    else if (attemptedCloud)
                    {
                        historyCompleted = true;
                        SyncActiveFriendContextToDialogSystem();
                    }
                });
            }

            if (dialogHistoryStore != null && dialogHistoryStore.IsReady && dialogSystem.GetMessageCount() > 0)
            {
                dialogSystem.FlushCloudDialogHistory();
            }

            if (memoryRequested && historyCompleted)
                break;

            elapsed += CLOUD_DIALOG_LOAD_RETRY_INTERVAL;
            yield return new WaitForSeconds(CLOUD_DIALOG_LOAD_RETRY_INTERVAL);
        }

        _cloudDialogLoadCoroutine = null;
        SyncActiveFriendContextToDialogSystem();
    }

    /// <summary>
    /// 初始化 TTS 管理器
    /// </summary>
    private void InitTTSManager()
    {
        if (!enableTTS) return;

        ttsManager = TTSManager.ResolveFor(gameObject);
    }

    private void SetupKeyboardInputAdapter()
    {
        if (uiComponent?.questionInputField == null)
            return;

        RectTransform footerPanel = FindChildRectTransform(transform, "FooterInputPanel");
        if (footerPanel == null)
        {
            footerPanel = uiComponent.questionInputField.GetComponentInParent<RectTransform>();
        }

        if (footerPanel == null)
            return;

        _keyboardInputAdapter = footerPanel.GetComponent<MobileKeyboardInputAdapter>();
        if (_keyboardInputAdapter == null)
            _keyboardInputAdapter = footerPanel.gameObject.AddComponent<MobileKeyboardInputAdapter>();

        _keyboardInputAdapter.Bind(footerPanel, uiComponent.questionInputField);
    }

    private static RectTransform FindChildRectTransform(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        if (root.name == childName)
            return root as RectTransform;

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform result = FindChildRectTransform(root.GetChild(i), childName);
            if (result != null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// 初始化聊天列表
    /// </summary>
    private void InitChatListView()
    {
        // 如果未在Inspector中赋值，尝试自动查找
        if (chatListView == null)
        {
            chatListView = uiComponent.ChatScrollViewLoopListView2;
            if (chatListView == null)
            {
                Debug.LogError("ChatListView is not found! Please add LoopListView2 component to a child GameObject.");
                return;
            }
        }

        chatListView.InitListView(0, OnGetChatItemByIndex);
    }

    /// <summary>
    /// 更新占卜师信息显示
    /// </summary>
    private void UpdateDivinerInfo()
    {
        if (dialogSystem == null) return;

        if (uiComponent.NameText != null)
        {
            uiComponent.NameText.text = dialogSystem.GetCurrentDivinerName();
        }
    }

    #endregion

    #region 列表项获取

    /// <summary>
    /// LoopListView2 获取项回调
    /// </summary>
    LoopListViewItem2 OnGetChatItemByIndex(LoopListView2 listView, int index)
    {

        if (index < 0 || index >= dialogSystem.GetMessageCount())
        {
            return null;
        }

        ChatMessageData msgData = dialogSystem.GetMessageByIndex(index);
        if (msgData == null)
        {
            return null;
        }

        LoopListViewItem2 item = null;

        if (msgData.roleType == DialogRoleType.AI)
        {
            item = listView.NewListViewItem("LeftDialogItem");
        }
        else
        {
            item = listView.NewListViewItem("RightDialogItem");
        }
        ChatItem itemScript = item.GetComponent<ChatItem>();
        // 只初始化一次（避免重复执行）
        if (item.IsInitHandlerCalled == false)
        {
            item.IsInitHandlerCalled = true;
            itemScript.Init();
        }

        // 设置数据（刷新UI内容）
        itemScript.SetItemData(msgData, index);

        // 绑定 TTS 播放回调
        if (enableTTS && msgData.roleType == DialogRoleType.AI)
        {
            itemScript.onTTSPlayClicked = OnChatItemTTSPlay;
        }
        else
        {
            itemScript.onTTSPlayClicked = null;
        }

        return item;
    }

    #endregion

    #region 发送和接收消息

    /// <summary>
    /// 发送用户消息
    /// </summary>
	private void SendUserMessage(string content)
	{
		if (mIsLoading)
		{
			EnqueueAIRequest(() => SendUserMessage(content));
			return;
		}

		if (!MembershipGate.CanUse(MembershipFeature.DialogMessage)) return;

		// 添加用户消息到数据层
		dialogSystem.AddUserMessage(content);

        UpdateChatScrollView();

        // 发送消息到AI
        SendMessageToAI();
    }

    /// <summary>
    /// 发送消息到AI（流式输出）
    /// </summary>
    private void SendMessageToAI()
    {
        if (dialogSystem == null) return;
        Debug.Log("发送消息到AI（流式）");

        // 显示加载中提示
        ShowLoadingIndicator();

        mIsFirstChunk = true;
        mLastChunkRefreshTime = 0f;

        int streamingMessageIndex = -1;
        bool delayTextUntilVoiceReady = ShouldDelayAITextUntilVoiceReady();

        // 调用流式 API
        streamingMessageIndex = dialogSystem.SendMessageToAIStream(
            // ---- onChunk: 每收到一个 token ----
            (chunk) =>
            {
                if (delayTextUntilVoiceReady)
                    return;

                if (mIsFirstChunk)
                {
                    // 首个 chunk：列表需要刷新以显示新条目
                    mIsFirstChunk = false;
                    int msgCount = dialogSystem.GetMessageCount();
                    Debug.Log($"[DialogUI] 流式开始，当前消息数: {msgCount}");
                    chatListView.SetListItemCount(msgCount, false);
                    chatListView.MovePanelToItemIndex(streamingMessageIndex, 0);
                    mLastChunkRefreshTime = Time.time;
                }
                else if (Time.time - mLastChunkRefreshTime > STREAMING_REFRESH_INTERVAL)
                {
                    // 节流刷新：直接更新本次流式请求对应的消息（避免并发时写错最后一条）
                    var streamingItem = chatListView.GetShownItemByItemIndex(streamingMessageIndex);
                    if (streamingItem != null)
                    {
                        var msgData = dialogSystem.GetMessageByIndex(streamingMessageIndex);
                        if (msgData != null)
                        {
                            streamingItem.GetComponent<ChatItem>().UpdateStreamingText(msgData.content);
                        }
                    }
                    chatListView.MovePanelToItemIndex(streamingMessageIndex, 0);
                    mLastChunkRefreshTime = Time.time;
                }
            },
            // ---- onComplete: 流式完成 ----
            (fullContent) =>
            {
                // ---- 占卜完成检测：如果处于 CardsLocked 阶段（AI 已完成解读），自动保存到 Firestore ----
                if (divinationEngine != null
                    && divinationEngine.CurrentSession != null
                    && divinationEngine.CurrentPhase == DivinationPhase.CardsLocked)
                {
                    divinationEngine.CompleteDivination(fullContent);
                    dialogSystem.ApplyDivinationReplyToActiveSpread(fullContent);
                }

                // ---- 执行 AI 显式请求的客户端动作（例如展示牌阵）----
                if (TryExecuteClientAction(streamingMessageIndex, fullContent))
                {
                    ProcessNextQueuedAIRequest();
                    return;
                }

                // 普通 AI 回复：设置选项按钮
                List<string> options = dialogSystem.GetCurrentDivinerOptions();
                dialogSystem.SetAIMessageOptions(streamingMessageIndex, options);

                if (ShouldAutoPlayAITextWithVoice(fullContent))
                {
                    uiComponent.StartCoroutine(ShowAITextAfterVoiceReady(streamingMessageIndex, fullContent));
                    return;
                }

                HideLoadingIndicator();
                RefreshChatAfterAIMessage(streamingMessageIndex);
                PrepareTTSForCompletedAIMessage(streamingMessageIndex);
                ProcessNextQueuedAIRequest();
            },
            // ---- onError: 流式出错 ----
            (error) =>
            {
                HideLoadingIndicator();

                Debug.LogError("AI响应错误: " + error);
                ToastManager.ShowToast("AI响应失败，请稍后重试。");

                // 刷新列表（移除占位消息已在 DialogSystem 中处理）
                int msgCount = dialogSystem.GetMessageCount();
                chatListView.SetListItemCount(msgCount, false);
                chatListView.RefreshAllShownItem();
                ProcessNextQueuedAIRequest();
            }
        );
    }

    private bool ShouldDelayAITextUntilVoiceReady()
    {
        return false;
    }

    private bool ShouldAutoPlayAITextWithVoice(string text)
    {
        return false;
    }

    private void RefreshChatAfterAIMessage(int messageIndex)
    {
        int msgCount = dialogSystem.GetMessageCount();
        Debug.Log($"[DialogUI] 流式完成，最终消息数: {msgCount}");
        chatListView.SetListItemCount(msgCount, false);
        chatListView.MovePanelToItemIndex(messageIndex >= 0 ? messageIndex : msgCount - 1, 0);
        chatListView.RefreshAllShownItem();
    }

    private ChatItem GetShownChatItem(int messageIndex)
    {
        var shownItem = chatListView.GetShownItemByItemIndex(messageIndex);
        return shownItem != null ? shownItem.GetComponent<ChatItem>() : null;
    }

    private void RefreshChatItemLayoutAfterRuntimeSizeChange(int messageIndex, bool scrollIfLastMessage)
    {
        if (chatListView == null || dialogSystem == null || messageIndex < 0) return;

        chatListView.OnItemSizeChanged(messageIndex);

        bool isLastMessage = messageIndex == dialogSystem.GetMessageCount() - 1;
        if (scrollIfLastMessage && isLastMessage)
            chatListView.MovePanelToItemIndex(messageIndex, 0);
    }

    private void PrepareTTSForCompletedAIMessage(int messageIndex)
    {
        if (!enableTTS || ttsManager == null || dialogSystem == null) return;

        var msgData = dialogSystem.GetMessageByIndex(messageIndex);
        if (msgData == null || msgData.roleType != DialogRoleType.AI) return;
        if (msgData.messageType != MsgType.Str) return;
        if (string.IsNullOrWhiteSpace(msgData.content)) return;
        if (msgData.ttsAudioReady && _preparedTTSByMessageId.ContainsKey(msgData.id)) return;

        uiComponent.StartCoroutine(PrepareTTSForCompletedAIMessageRoutine(messageIndex, msgData));
    }

    private IEnumerator PrepareTTSForCompletedAIMessageRoutine(int messageIndex, ChatMessageData msgData)
    {
        PreparedTTSAudio prepared = null;
        string error = null;

        yield return uiComponent.StartCoroutine(PrepareTTSAudio(msgData.id, msgData.content, msgData.divinerType,
            (result, synthError) =>
            {
                prepared = result;
                error = synthError;
            }));

        if (prepared == null || prepared.totalDuration <= 0f)
        {
            Debug.LogWarning($"[DialogUI] TTS 后台准备失败: {error}");
            dialogSystem.SetAIMessageTTSInfo(messageIndex, 0f, false);
            yield break;
        }

        dialogSystem.SetAIMessageTTSInfo(messageIndex, prepared.totalDuration, true);

        var item = GetShownChatItem(messageIndex);
        if (item != null)
        {
            item.SetTTSLength(prepared.totalDuration);
            item.UpdateTTSButtonAfterStream(msgData.content);
            item.ShowTTSLoading(false);
        }
        RefreshChatItemLayoutAfterRuntimeSizeChange(messageIndex, true);
    }

    /// <summary>
    /// 将最后一条流式消息转换为 InteractionCard 类型
    /// </summary>
    private void ConvertLastMessageToInteractionCard(string fullContent)
    {
        ConvertMessageToInteractionCard(dialogSystem.GetMessageCount() - 1, fullContent);
    }

    private void ConvertMessageToInteractionCard(int messageIndex, string fullContent)
    {
        ConvertMessageToInteractionCard(messageIndex, fullContent, 0);
    }

    private void ConvertMessageToInteractionCard(int messageIndex, string fullContent, int forcedCardCount)
    {
        var spreadDef = GetCurrentSpreadDefinition();
        int targetCardCount = forcedCardCount > 0
            ? forcedCardCount
            : (spreadDef?.cardCount ?? GetCurrentSpreadCardCount());
        string spreadKind = spreadDef?.kind ?? FindSpreadKindByCardCount(targetCardCount);
        MsgType msgType;

        switch (targetCardCount)
        {
            case 1:
                msgType = MsgType.InteractionCard1;
                Debug.Log($"[DialogUI] 流式完成 → InteractionCard1, spreadKind={spreadKind}");
                break;
            case 5:
                msgType = MsgType.InteractionCard5;
                Debug.Log($"[DialogUI] 流式完成 → InteractionCard5, spreadKind={spreadKind}");
                break;
            default:
                msgType = MsgType.InteractionCard3;
                Debug.Log($"[DialogUI] 流式完成 → InteractionCard3, spreadKind={spreadKind}");
                break;
        }

        // 修改本次流式回复对应的消息类型
        var lastMsg = dialogSystem.GetMessageByIndex(messageIndex);
        if (lastMsg != null)
        {
            lastMsg.messageType = msgType;
            lastMsg.spreadKind = spreadKind;
            lastMsg.options = dialogSystem.GetCurrentDivinerOptions();
            dialogSystem.CaptureDivinationSnapshot(lastMsg);
        }
    }
    /// <summary>
    /// 
    /// </summary>
    public void SendTodayOracleMessage()
    {
        string content = dialogSystem != null ? dialogSystem.BuildTodayCardMessageContent() : string.Empty;
        dialogSystem?.AddTodayDivinationMessage(content);
        UpdateChatScrollView();
    }
    public void SendAtFriendsMessage()
    {
        SendAtFriendsMessage(null);
    }

    public void SendAtFriendsMessage(FriendDataManager.FriendData friend)
    {
        SendAtFriendsMessage(friend, "");
    }

    public void SendAtFriendsMessage(FriendDataManager.FriendData friend, string extraContext)
    {
        if (friend == null)
        {
            ClearAtFriendSelection(false);
            return;
        }

        _activeAtFriend = friend;
        RefreshAtFriendBox();
        dialogSystem.AddFriendChatContext(friend, extraContext);
        Debug.Log($"关联好友：{GetAtFriendDisplayName(friend)}");
    }

    public void AddTarotCardChatContext(TarotCard card, bool upright, string sourceTitle = "塔罗牌", string description = "")
    {
        dialogSystem?.AddTarotCardChatContext(card, upright, sourceTitle, description);
        RefreshAtFriendBox();
    }

    public void AddTodayCardChatContext(TodayCardPayload payload)
    {
        dialogSystem?.AddTodayCardChatContext(payload);
        RefreshAtFriendBox();
    }

    public void AddReadingChatContext(string readingId, string title, string preview, string payload, string source = "reading")
    {
        dialogSystem?.AddReadingChatContext(readingId, title, preview, payload, source);
        RefreshAtFriendBox();
    }

    public void AddConditionChatContext(string id, string title, string description)
    {
        dialogSystem?.AddConditionChatContext(id, title, description);
        RefreshAtFriendBox();
    }

    public void ClearAllChatContexts()
    {
        _activeAtFriend = null;
        dialogSystem?.ClearActiveChatContexts();
        RefreshAtFriendBox();
        ToastManager.ShowToast("已清除带入条件");
    }

    /// <summary>
    /// 外部调用：从其他界面（如 CompleteInterpretationUI）发送消息并触发 AI 对话
    /// </summary>
    public void SendMessageFromExternal(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (dialogSystem == null) return;

		if (mIsLoading)
		{
			EnqueueAIRequest(() => SendMessageFromExternal(message));
			return;
		}

		if (!MembershipGate.CanUse(MembershipFeature.DialogMessage)) return;

		// 如果有今日卡牌数据，同步到 DialogSystem
        var oracleService = DailyOracleService.Instance;
        if (oracleService?.CurrentCard != null)
        {
            var cardPayload = oracleService.GetTodayCardPayload();
            if (cardPayload != null)
                dialogSystem.SetTodayCardPayload(cardPayload);
        }

        // 添加用户消息
        dialogSystem.AddUserMessage(message);

        // 更新 UI
        UpdateChatScrollView();

        // 发送到 AI
        SendMessageToAI();
    }

    /// <summary>
    /// 卡牌话题选中事件回调（从 CompleteInterpretationUI 通过 EventSystem 触发）
    /// </summary>
    private void OnCardTopicSelected(string topic)
    {
        Debug.Log($"[DialogUI] 卡牌话题选中: {topic}");
        SendMessageFromExternal(topic);
    }
    private void UpdateChatScrollView()
    {
        // 更新列表后刷新已显示项，避免上一条“最后消息”继续保留底部安全距离。
        int msgCount = dialogSystem.GetMessageCount();
        Debug.Log($"发送用户消息后，当前消息数量：{msgCount}");
        chatListView.SetListItemCount(msgCount, false);
        chatListView.RefreshAllShownItem();

        // 滚动到最后一条
        chatListView.MovePanelToItemIndex(
            msgCount - 1,
            0
        );
    }

    private void ClearAtFriendSelection(bool showToast)
    {
        ClearAtFriendSelection(showToast, true);
    }

    private void ClearAtFriendSelection(bool showToast, bool clearContext)
    {
        _activeAtFriend = null;
        RefreshAtFriendBox();
        if (clearContext)
            dialogSystem?.ClearActiveFriendContext();

        if (showToast)
        {
            ToastManager.ShowToast("已取消 @ 好友");
        }
    }

    private void SyncActiveFriendContextToDialogSystem()
    {
        if (dialogSystem == null) return;

        if (_activeAtFriend != null)
        {
            dialogSystem.ActivateFriendContext(_activeAtFriend);
        }
        else
        {
            // 没有 UI 上的 @ 好友时，不主动清除数据层上下文。
            // 用户点击取消按钮时会通过 ClearAtFriendSelection 显式清除。
        }

        RefreshAtFriendBox();
    }

    private void RefreshAtFriendBox()
    {
        bool hasFriend = _activeAtFriend != null;
        string activeContextPreview = !hasFriend && dialogSystem != null
            ? dialogSystem.BuildActiveContextPreview()
            : "";
        bool hasContextPreview = !string.IsNullOrWhiteSpace(activeContextPreview);
        if (uiComponent?.artFriendRectTransfrom != null)
        {
            uiComponent.artFriendRectTransfrom.gameObject.SetActive(hasFriend || hasContextPreview);
        }

        string label = hasFriend ? $"@{GetAtFriendDisplayName(_activeAtFriend)}" : activeContextPreview;
        if (uiComponent?.artFriendNameText != null)
        {
            uiComponent.artFriendNameText.text = label;
        }

        // @ 好友框已经独立放到输入框上方，输入框布局完全交给 prefab 控制。
    }

    private string GetAtFriendDisplayName(FriendDataManager.FriendData friend)
    {
        if (friend == null || string.IsNullOrWhiteSpace(friend.name))
        {
            return "好友";
        }

        return friend.name.Trim();
    }


    #endregion

    #region 加载指示器

    private bool mIsLoading = false;
    private bool mIsFirstChunk = true;
    private float mLastChunkRefreshTime = 0f;
    private const float STREAMING_REFRESH_INTERVAL = 0.08f;

    private void EnqueueAIRequest(System.Action request)
    {
        if (request == null) return;
        _pendingAIRequests.Enqueue(request);
        Debug.Log($"[DialogUI] AI 正在回复中，新请求已排队。pending={_pendingAIRequests.Count}");
    }

    private void ProcessNextQueuedAIRequest()
    {
        if (mIsLoading) return;
        if (_pendingAIRequests.Count == 0) return;

        var next = _pendingAIRequests.Dequeue();
        next?.Invoke();
    }

    /// <summary>
    /// 显示加载中指示器
    /// </summary>
    private void ShowLoadingIndicator()
    {
        mIsLoading = true;
        // 可以在这里实现加载动画或提示
        Debug.Log("AI思考中...");
    }

    /// <summary>
    /// 隐藏加载中指示器
    /// </summary>
    private void HideLoadingIndicator()
    {
        mIsLoading = false;
    }

    #endregion

    #region API Function

    
    #endregion

    #region UI组件事件

    /// <summary>
    /// 切换占卜师按钮点击
    /// </summary>
    public void OnswitchDivinerButtonClick()
    {
        Debug.Log("switchDivinerButton is clicked");

        if (dialogSystem == null) return;

        // 切换占卜师类型
        dialogSystem.SwitchDivinerType();

        // 更新UI显示
        UpdateDivinerInfo();

        // 显示切换提示
        string divinerName = dialogSystem.GetCurrentDivinerName();
        ToastManager.ShowToast("已切换为" + divinerName);
    }
    private bool isShowQuickDivinationPanel =false;
    /// <summary>
    /// 快速占卜
    /// </summary>
    public void OnquestionButtonClick()
    {
        // 显示快速占卜面板
        QuickDivinationPanel panel = uiComponent.QuickDivinationPanelTransform.GetComponent<QuickDivinationPanel>();
        isShowQuickDivinationPanel = !isShowQuickDivinationPanel;
        if(isShowQuickDivinationPanel)
        {
            panel.ShowPanel();           
        }
        else
        {
            panel.HidePanel();
        }
    }

    /// <summary>
    /// 发送按钮点击
    /// </summary>
    public void OnsendButtonClick()
    {
        if (uiComponent.questionInputField == null) return;

        string inputText = (uiComponent.questionInputField.text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(inputText))
        {
            ToastManager.ShowToast("请写下你想问的问题。");
            RefreshSendButtonState(inputText);
            uiComponent.questionInputField.ActivateInputField();
            return;
        }

        if (!MembershipGate.CanUse(MembershipFeature.DialogMessage))
        {
            RefreshSendButtonState(inputText);
            return;
        }

        if (mIsLoading)
        {
            SendUserMessage(inputText);
            ClearQuestionInput();
            ToastManager.ShowToast("AI正在回复，已为你排队发送。");
            return;
        }
        Debug.Log($"发送信息：{inputText}");
        // 发送消息
        SendUserMessage(inputText);

        // 清空输入框
        ClearQuestionInput();
    }
    public void OncancelArtButtonClick()
    {
        if (_activeAtFriend != null)
            ClearAtFriendSelection(true);
        else
            ClearAllChatContexts();
    }

    /// <summary>
    /// 输入框内容变化
    /// </summary>
    public void OnquestionInputChange(string text)
    {
        RefreshSendButtonState(text);
        _keyboardInputAdapter?.KeepLatestTextVisible();
    }

    /// <summary>
    /// 输入框结束编辑
    /// </summary>
    public void OnquestionInputEnd(string text)
    {
        RefreshSendButtonState(text);
        _keyboardInputAdapter?.KeepLatestTextVisible();
    }

    private void ClearQuestionInput()
    {
        if (uiComponent.questionInputField != null)
            uiComponent.questionInputField.text = string.Empty;
        RefreshSendButtonState(string.Empty);
    }

    private void RefreshSendButtonState(string text = null)
    {
        if (uiComponent?.sendButton == null) return;

        string value = text ?? uiComponent.questionInputField?.text ?? string.Empty;
        uiComponent.sendButton.interactable = !string.IsNullOrWhiteSpace(value);
    }

    #endregion

    #region AI消息回调

    /// <summary>
    /// 刷新聊天UI事件回调
    /// </summary>
    private void OnRefreshChatUI(object data)
    {
        if (chatListView == null || dialogSystem == null) return;
        int msgCount = dialogSystem.GetMessageCount();
        if (msgCount <= 0) return;

        chatListView.SetListItemCount(msgCount, false);
        chatListView.RefreshAllShownItem();
        chatListView.MovePanelToItemIndex(msgCount - 1, 0);
    }

    /// <summary>
    /// 快速占卜问题选中事件回调
    /// </summary>
    private void OnQuickQuestionSelected(string question)
    {
        Debug.Log($"[DialogUI] 快速占卜问题: {question}");
        if (string.IsNullOrEmpty(question)) return;
        if (mIsLoading)
        {
            EnqueueAIRequest(() => OnQuickQuestionSelected(question));
            return;
        }

        // 快速问题只负责把玩家的问题发给 AI。
        // 是否进入牌阵、进入哪种牌阵，由 Oracle Runtime 的 scene/stage/plan 决定。
        SendUserMessage(question);
    }

    /// <summary>
    /// AI选项按钮点击回调
    /// </summary>
    private void OnAIOptionClick(int optionIndex)
    {
        Debug.Log("AI选项按钮点击: " + optionIndex);

        if (dialogSystem == null) return;

        List<string> options = dialogSystem.GetCurrentDivinerOptions();
        if (options == null || optionIndex < 0 || optionIndex >= options.Count) return;

        string selectedOption = options[optionIndex];
        Debug.Log("用户选择了: " + selectedOption);

        // 根据选项执行不同操作
        switch (selectedOption)
        {
            case "为这个问题选牌阵":
                HandleSpreadSelection();
                break;

            case "继续追问":
                // 进入追问模式
                divinationEngine?.EnterFollowUp();
                if (uiComponent.questionInputField != null)
                {
                    uiComponent.questionInputField.ActivateInputField();
                }
                break;

            case "明天再看这条线索":
                HandleSaveTomorrowHook(selectedOption);
                break;

            case "看这段关系的周期":
                HandleRelationshipCycle();
                break;

            case "分析今日星象":
                HandleAstrologyAnalysis();
                break;

            case "看下一周趋势":
                HandleWeeklyTrend();
                break;

            case "保存明日回看":
                HandleSaveTomorrowHook(selectedOption);
                break;

            default:
                // 检测是否是牌阵选择
                if (TryHandleSpreadChoice(selectedOption))
                    break;

                // 将选项作为问题发送
                SendUserMessage(selectedOption);
                break;
        }
    }

    #endregion

    #region 占卜流程

    /// <summary>
    /// 展示牌阵选择（AI 选项按钮形式）
    /// </summary>
    private void HandleSpreadSelection()
    {
        if (divinationEngine == null)
        {
            ToastManager.ShowToast("占卜引擎未就绪");
            return;
        }

        var spreadOptions = divinationEngine.GetSpreadOptions();
        if (spreadOptions.Length == 0)
        {
            ToastManager.ShowToast("暂无可用的牌阵");
            return;
        }

        // 将牌阵选项设置到 DialogSystem 的选项列表中
        var optionList = new List<string>(spreadOptions);
        dialogSystem.SetDivinerOptions(optionList);

        // 触发 UI 刷新以显示新选项
        UpdateChatScrollView();
        ToastManager.ShowToast("请选择一个牌阵");
    }

    /// <summary>
    /// 检查用户选择的选项是否是牌阵，如果是则执行牌阵选择 + 发送解读请求
    /// </summary>
    private bool TryHandleSpreadChoice(string selectedOption)
    {
        if (divinationEngine == null) return false;

        var spreadDef = divinationEngine.GetSpreadByLabel(selectedOption);
        if (spreadDef == null) return false;

        Debug.Log($"[DialogUI] 用户选择牌阵: {spreadDef.label} ({spreadDef.kind})");

        // 抽牌并锁定
        var lockedCards = divinationEngine.SelectSpread(spreadDef.kind);
        if (lockedCards.Count == 0)
        {
            ToastManager.ShowToast("抽牌失败，请重试");
            return true;
        }

        // 构建抽牌结果文本，发送给 AI
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"我选择了牌阵「{spreadDef.label}」，请帮我解读：");
        foreach (var lc in lockedCards)
        {
            var orientLabel = lc.orientation == "upright" ? "正位" : "逆位";
            sb.AppendLine($"- {lc.position}：{lc.cardName}（{orientLabel}）");
        }

        SendUserMessage(sb.ToString().TrimEnd());
        return true;
    }

    /// <summary>
    /// 保存明日钩子
    /// </summary>
    private void HandleSaveTomorrowHook(string optionLabel)
    {
        if (divinationEngine == null)
        {
            ToastManager.ShowToast("占卜引擎未就绪");
            return;
        }

        // 取最后一条 AI 消息作为触发文本
        int msgCount = dialogSystem.GetMessageCount();
        string triggerText = msgCount > 0
            ? dialogSystem.GetMessageSnippet(msgCount - 1, 60)
            : optionLabel;

        var hook = divinationEngine.CreateTomorrowHook(triggerText);
        if (hook != null)
        {
            Debug.Log($"[DialogUI] 保存明日钩子: hookId={hook.hookId}, text={triggerText}");
            PersistTomorrowHook(hook);
        }
        else
        {
            ToastManager.ShowToast("暂无活跃占卜，无法保存");
        }
    }

    /// <summary>
    /// 关系周期分析 —— 启动新占卜并发送特定消息
    /// </summary>
    private void HandleRelationshipCycle()
    {
        SendUserMessage("分析这段关系的周期");
    }

    /// <summary>
    /// 今日星象分析 —— 发送占星 prompt
    /// </summary>
    private void HandleAstrologyAnalysis()
    {
        SendUserMessage("请分析今日星象和我当前的能量状态");
    }

    /// <summary>
    /// 下周趋势 —— 发送趋势 prompt
    /// </summary>
    private void HandleWeeklyTrend()
    {
        SendUserMessage("请分析下一周的趋势和需要注意的事项");
    }

    #endregion

    #region 语音

    /// <summary>
    /// ChatItem TTS 播放按钮回调
    /// </summary>
    private void OnChatItemTTSPlay(ChatItem item)
    {
        if (ttsManager == null)
        {
            ToastManager.ShowToast("TTS 服务未就绪");
            return;
        }

        // 如果正在播放同一段语音，停止播放
        if (_currentTTSItem == item && AudioManager.Instance != null && AudioManager.Instance.IsVoicePlaying())
        {
            StopTTSPlayback();
            return;
        }

        // 获取该消息的文本内容
        int itemIndex = item.ItemIndex;
        if (itemIndex < 0 || dialogSystem == null) return;

        var msgData = dialogSystem.GetMessageByIndex(itemIndex);
        if (msgData == null || string.IsNullOrEmpty(msgData.content)) return;

        // 停止之前的播放
        StopTTSPlayback();

        // 标记当前播放项
        _currentTTSItem = item;
        _currentTTSMessageId = msgData.id;
        item.ShowTTSLoading(true);

        Debug.Log($"[DialogUI] TTS 请求播放: {msgData.content.Substring(0, Mathf.Min(msgData.content.Length, 30))}...");

        _ttsPlaybackCoroutine = uiComponent.StartCoroutine(PrepareAndPlayTTSForItem(item, itemIndex, msgData));
    }

    private IEnumerator ShowAITextAfterVoiceReady(int messageIndex, string fullText)
    {
        var msgData = dialogSystem.GetMessageByIndex(messageIndex);
        if (msgData == null)
        {
            HideLoadingIndicator();
            RefreshChatAfterAIMessage(messageIndex);
            ProcessNextQueuedAIRequest();
            yield break;
        }

        string displayText = string.IsNullOrEmpty(msgData.content) ? fullText : msgData.content;
        PreparedTTSAudio prepared = null;
        string error = null;
        yield return uiComponent.StartCoroutine(PrepareTTSAudio(msgData.id, displayText, msgData.divinerType,
            (result, synthError) =>
            {
                prepared = result;
                error = synthError;
            }));

        if (prepared == null || prepared.totalDuration <= 0f)
        {
            Debug.LogWarning($"[DialogUI] TTS 准备失败，直接显示文本: {error}");
            HideLoadingIndicator();
            RefreshChatAfterAIMessage(messageIndex);
            ProcessNextQueuedAIRequest();
            yield break;
        }

        dialogSystem.SetAIMessageTTSInfo(messageIndex, prepared.totalDuration, true);

        int msgCount = dialogSystem.GetMessageCount();
        chatListView.SetListItemCount(msgCount, false);
        chatListView.MovePanelToItemIndex(messageIndex, 0);
        chatListView.RefreshAllShownItem();
        yield return null;

        ChatItem item = GetShownChatItem(messageIndex);
        if (item == null)
        {
            HideLoadingIndicator();
            ProcessNextQueuedAIRequest();
            yield break;
        }

        item.onTTSPlayClicked = OnChatItemTTSPlay;
        _currentTTSItem = item;
        _currentTTSMessageId = msgData.id;
        item.ShowTTSLoading(false);
        item.PrepareSyncedSpeech(displayText, prepared.totalDuration);
        RefreshChatItemLayoutAfterRuntimeSizeChange(messageIndex, true);

        HideLoadingIndicator();
        _ttsPlaybackCoroutine = uiComponent.StartCoroutine(PlayPreparedTTSWithSyncedText(item, msgData.id, displayText, prepared,
            onComplete: ProcessNextQueuedAIRequest));
    }

    private IEnumerator PrepareAndPlayTTSForItem(ChatItem item, int messageIndex, ChatMessageData msgData)
    {
        PreparedTTSAudio prepared = null;
        string error = null;
        yield return uiComponent.StartCoroutine(PrepareTTSAudio(msgData.id, msgData.content, msgData.divinerType,
            (result, synthError) =>
            {
                prepared = result;
                error = synthError;
            }));

        if (_currentTTSMessageId != msgData.id)
            yield break;

        item.ShowTTSLoading(false);

        if (prepared == null || prepared.totalDuration <= 0f)
        {
            Debug.LogError($"[DialogUI] TTS 合成失败: {error ?? "AudioClip 为空"}");
            ToastManager.ShowToast(TTSManager.ToUserFacingError(error));
            _currentTTSItem = null;
            _currentTTSMessageId = -1;
            _ttsPlaybackCoroutine = null;
            yield break;
        }

        dialogSystem.SetAIMessageTTSInfo(messageIndex, prepared.totalDuration, true);
        item.SetTTSLength(prepared.totalDuration);
        item.UpdateTTSButtonAfterStream(msgData.content);
        RefreshChatItemLayoutAfterRuntimeSizeChange(messageIndex, false);
        yield return uiComponent.StartCoroutine(PlayPreparedTTSOnly(item, msgData.id, prepared));
    }

    private IEnumerator PrepareTTSAudio(int messageId, string text, DivinerType divinerType,
        System.Action<PreparedTTSAudio, string> onComplete)
    {
        PreparedTTSAudio cached;
        if (_preparedTTSByMessageId.TryGetValue(messageId, out cached) && cached != null && cached.totalDuration > 0f)
        {
            onComplete?.Invoke(cached, null);
            yield break;
        }

        ConfigureTTSVoice(divinerType);

        var segments = SplitTTSText(text, Mathf.Max(100, ttsSegmentMaxLength));
        if (segments.Count == 0)
        {
            onComplete?.Invoke(null, "文本为空");
            yield break;
        }

        var prepared = new PreparedTTSAudio();
        prepared.segments.AddRange(segments);

        for (int i = 0; i < segments.Count; i++)
        {
            AudioClip clip = null;
            string synthError = null;
            bool done = false;

            while (ttsManager != null && ttsManager.IsSynthesizing)
                yield return null;

            ttsManager.Speak(segments[i],
                (result) =>
                {
                    clip = result;
                    done = true;
                },
                (err) =>
                {
                    synthError = err;
                    done = true;
                });

            yield return new WaitUntil(() => done || ttsManager == null || !ttsManager.IsSynthesizing);

            if (!done)
            {
                onComplete?.Invoke(null, "TTS 请求已被取消");
                yield break;
            }

            if (!string.IsNullOrEmpty(synthError) || clip == null)
            {
                onComplete?.Invoke(null, synthError ?? "AudioClip 为空");
                yield break;
            }

            prepared.clips.Add(clip);
            prepared.totalDuration += clip.length;
        }

        _preparedTTSByMessageId[messageId] = prepared;
        onComplete?.Invoke(prepared, null);
    }

    private IEnumerator PlayPreparedTTSOnly(ChatItem item, int messageId, PreparedTTSAudio prepared)
    {
        if (AudioManager.Instance == null)
        {
            ToastManager.ShowToast("音频管理器未就绪");
            CleanupCurrentTTS(messageId);
            yield break;
        }

        for (int i = 0; i < prepared.clips.Count; i++)
        {
            if (_currentTTSMessageId != messageId)
                yield break;

            var clip = prepared.clips[i];
            AudioManager.Instance.PlayVoice(clip, interrupt: true);
            Debug.Log($"[DialogUI] TTS 播放第 {i + 1}/{prepared.clips.Count} 段, 总时长={prepared.totalDuration:F1}s");
            yield return new WaitForSeconds(clip.length + 0.05f);
        }

        CleanupCurrentTTS(messageId);
        _ttsPlaybackCoroutine = null;
    }

    private IEnumerator PlayPreparedTTSWithSyncedText(ChatItem item, int messageId, string fullText,
        PreparedTTSAudio prepared, System.Action onComplete = null)
    {
        if (AudioManager.Instance == null)
        {
            item.CompleteSyncedSpeech(fullText);
            ToastManager.ShowToast("音频管理器未就绪");
            CleanupCurrentTTS(messageId);
            onComplete?.Invoke();
            yield break;
        }

        float totalDuration = Mathf.Max(0.01f, prepared.totalDuration);
        float elapsedTotal = 0f;

        for (int i = 0; i < prepared.clips.Count; i++)
        {
            if (_currentTTSMessageId != messageId)
                yield break;

            var clip = prepared.clips[i];
            AudioManager.Instance.PlayVoice(clip, interrupt: true);
            Debug.Log($"[DialogUI] TTS 播放第 {i + 1}/{prepared.clips.Count} 段, 总时长={prepared.totalDuration:F1}s");

            float clipElapsed = 0f;
            while (clipElapsed < clip.length)
            {
                if (_currentTTSMessageId != messageId)
                    yield break;

                clipElapsed += Time.deltaTime;
                float progress = Mathf.Clamp01((elapsedTotal + clipElapsed) / totalDuration);
                item.SetSyncedSpeechProgress(fullText, progress);
                yield return null;
            }

            elapsedTotal += clip.length;
        }

        item.CompleteSyncedSpeech(fullText);
        CleanupCurrentTTS(messageId);
        _ttsPlaybackCoroutine = null;
        onComplete?.Invoke();
    }

    private void CleanupCurrentTTS(int messageId)
    {
        if (_currentTTSMessageId == messageId)
        {
            _currentTTSItem = null;
            _currentTTSMessageId = -1;
        }
    }

    /// <summary>
    /// 停止当前 TTS 播放
    /// </summary>
    private void StopTTSPlayback()
    {
        if (_ttsPlaybackCoroutine != null)
        {
            uiComponent.StopCoroutine(_ttsPlaybackCoroutine);
            _ttsPlaybackCoroutine = null;
        }

        if (ttsManager != null)
            ttsManager.CancelSynthesis();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopVoice();
        }

        if (_currentTTSItem != null)
        {
            _currentTTSItem.ShowTTSLoading(false);
            _currentTTSItem = null;
        }
        _currentTTSMessageId = -1;
    }

    private void ConfigureTTSVoice(DivinerType divinerType)
    {
        if (ttsManager == null) return;

        ttsManager.voiceType = divinerType == DivinerType.Astrology
            ? astrologyVoiceType
            : tarotVoiceType;
    }

    private List<string> SplitTTSText(string text, int maxLength)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text)) return result;

        int start = 0;
        while (start < text.Length)
        {
            int remaining = text.Length - start;
            int length = Mathf.Min(maxLength, remaining);

            if (remaining > maxLength)
            {
                int split = FindBestTTSSplit(text, start, length);
                if (split > start)
                    length = split - start + 1;
            }

            string segment = text.Substring(start, length).Trim();
            if (!string.IsNullOrEmpty(segment))
                result.Add(segment);

            start += length;
        }

        return result;
    }

    private int FindBestTTSSplit(string text, int start, int maxLength)
    {
        int end = Mathf.Min(text.Length - 1, start + maxLength - 1);
        const string punctuation = "。！？!?；;\n";

        for (int i = end; i > start + maxLength / 2; i--)
        {
            if (punctuation.IndexOf(text[i]) >= 0)
                return i;
        }

        return end;
    }

    /// <summary>
    /// 重新生成声音按钮点击回调
    /// </summary>
    public void OnRegenerateVoiceClick()
    {
        Debug.Log("重新生成声音按钮点击");
        if (ttsManager == null)
        {
            ToastManager.ShowToast("TTS 服务未就绪，请稍后重试");
            return;
        }

        int messageIndex = FindLatestRegenerableVoiceMessageIndex();
        if (messageIndex < 0)
        {
            ToastManager.ShowToast("暂无可重新生成的 AI 语音");
            return;
        }

        ChatMessageData msgData = dialogSystem.GetMessageByIndex(messageIndex);
        RegenerateVoiceForMessage(messageIndex, msgData);
    }

    private int FindLatestRegenerableVoiceMessageIndex()
    {
        if (dialogSystem == null) return -1;

        for (int i = dialogSystem.GetMessageCount() - 1; i >= 0; i--)
        {
            ChatMessageData message = dialogSystem.GetMessageByIndex(i);
            if (message == null) continue;
            if (message.roleType != DialogRoleType.AI) continue;
            if (string.IsNullOrWhiteSpace(message.content)) continue;
            return i;
        }

        return -1;
    }

    private void RegenerateVoiceForMessage(int messageIndex, ChatMessageData msgData)
    {
        if (msgData == null || string.IsNullOrWhiteSpace(msgData.content))
        {
            ToastManager.ShowToast("这条消息没有可生成的语音内容");
            return;
        }

        StopTTSPlayback();
        ConfigureTTSVoice(msgData.divinerType);

        List<string> segments = SplitTTSText(msgData.content, Mathf.Max(100, ttsSegmentMaxLength));
        foreach (string segment in segments)
            ttsManager.ClearCacheForText(segment);

        _preparedTTSByMessageId.Remove(msgData.id);
        dialogSystem.SetAIMessageTTSInfo(messageIndex, 0f, false);
        chatListView.RefreshAllShownItem();

        ChatItem item = GetShownChatItem(messageIndex);
        if (item == null)
        {
            chatListView.MovePanelToItemIndex(messageIndex, 0);
            chatListView.RefreshAllShownItem();
            item = GetShownChatItem(messageIndex);
        }

        if (item == null)
        {
            ToastManager.ShowToast("已清除语音缓存，请点击该消息的语音按钮重新播放");
            return;
        }

        ToastManager.ShowToast("正在重新生成语音...");
        _currentTTSItem = item;
        _currentTTSMessageId = msgData.id;
        item.ShowTTSLoading(true);
        _ttsPlaybackCoroutine = uiComponent.StartCoroutine(PrepareAndPlayTTSForItem(item, messageIndex, msgData));
    }

    #endregion

    #region InteractionCard 互动牌阵（1/3/5 牌阵）

    /// <summary>
    /// 判断当前是否应该触发互动牌阵交互卡
    /// AI 返回后，如果占卜引擎处于 ChoosingSpread 阶段，展示 InteractionCard
    /// </summary>
    private bool ShouldTriggerInteractionCard()
    {
        if (divinationEngine == null) return false;

        var phase = divinationEngine.CurrentPhase;
        // ChoosingSpread 阶段 → AI 已给出牌阵计划，展示交互卡
        return phase == DivinationPhase.ChoosingSpread;
    }

    private bool TryExecuteClientAction(int streamingMessageIndex, string aiResponse)
    {
        if (dialogSystem == null) return false;

        var action = dialogSystem.GetLastClientActionRequest();
        if (action == null) return false;

        string actionName = FirstNonEmpty(action.action, action.kind).Trim().ToLowerInvariant();
        switch (actionName)
        {
            case "show_spread":
            case "start_spread":
                return TryShowSpreadFromClientAction(streamingMessageIndex, aiResponse, action);
            case "show_relationship_divination":
                return TryShowRelationshipDivinationFromClientAction(streamingMessageIndex);
            default:
                Debug.LogWarning($"[DialogUI] 未支持的 client_action: {actionName}");
                return false;
        }
    }

    private bool TryShowSpreadFromClientAction(
        int streamingMessageIndex,
        string aiResponse,
        OracleClientActionRequest action)
    {
        if (divinationEngine == null || action == null) return false;

        if (divinationEngine.CurrentPhase == DivinationPhase.CardsLocked
            || divinationEngine.CurrentPhase == DivinationPhase.Revealing
            || divinationEngine.CurrentPhase == DivinationPhase.GeneratingVerdict)
        {
            Debug.Log("[DialogUI] 已有占卜正在进行，忽略 show_spread client_action");
            return false;
        }

        SpreadDefinition spreadDef = ResolveSpreadDefinition(action);
        if (spreadDef == null)
        {
            Debug.LogWarning($"[DialogUI] show_spread 找不到牌阵: spreadKind={action.spreadKind}, cardCount={action.cardCount}");
            return false;
        }

        var plan = BuildClientActionDivinationPlan(action, spreadDef);
        divinationEngine.ApplyRuntimeDivinationPlan(plan);

        HideLoadingIndicator();
        ConvertMessageToInteractionCard(streamingMessageIndex, aiResponse, spreadDef.cardCount);
        RefreshChatAfterAIMessage(streamingMessageIndex);
        return true;
    }

    private bool TryShowRelationshipDivinationFromClientAction(int streamingMessageIndex)
    {
        if (_activeAtFriend == null) return false;

        bool canOpenLocal = CreatedFriendRelationshipDivinationLocalFlow.CanHandle(_activeAtFriend);
        bool canOpenRemote = RelationshipDivinationFlow.CanUseTwoPersonDivination(_activeAtFriend, false);
        if (!canOpenLocal && !canOpenRemote)
        {
            Debug.Log("[DialogUI] 当前 @ 好友不满足双人占卜条件，忽略 show_relationship_divination");
            return false;
        }

        HideLoadingIndicator();
        RefreshChatAfterAIMessage(streamingMessageIndex);
        PrepareTTSForCompletedAIMessage(streamingMessageIndex);
        RelationshipDivinationOverlay.StartForFriend(transform, _activeAtFriend);
        return true;
    }

    private SpreadDefinition ResolveSpreadDefinition(OracleClientActionRequest action)
    {
        if (divinationEngine == null) return null;

        string spreadKind = action?.spreadKind;
        if (!string.IsNullOrWhiteSpace(spreadKind))
        {
            var byKind = divinationEngine.GetSpreadDefinition(spreadKind);
            if (byKind != null) return byKind;
        }

        int cardCount = action != null && action.cardCount > 0 ? action.cardCount : 3;
        string fallbackKind = FindSpreadKindByCardCount(cardCount);
        return divinationEngine.GetSpreadDefinition(fallbackKind);
    }

    private DivinationPlan BuildClientActionDivinationPlan(
        OracleClientActionRequest action,
        SpreadDefinition spreadDef)
    {
        var runtimePlan = dialogSystem?.GetLastRuntimePlan()?.divinationPlan;
        if (runtimePlan != null && runtimePlan.spreadKind == spreadDef.kind)
            return runtimePlan;

        return new DivinationPlan
        {
            planId = System.Guid.NewGuid().ToString("N").Substring(0, 12),
            userId = "",
            conversationId = "dialog",
            question = FirstNonEmpty(action?.question, runtimePlan?.question, GetLatestUserMessageText()),
            scene = FirstNonEmpty(action?.scene, runtimePlan?.scene, dialogSystem?.GetLastRuntimePlan()?.scene, "general_chat"),
            spreadKind = spreadDef.kind,
            cardCount = spreadDef.cardCount,
            complexity = spreadDef.cardCount <= 1 ? "light" : spreadDef.cardCount >= 5 ? "deep" : "standard",
            positions = spreadDef.positions != null
                ? new List<SpreadPosition>(spreadDef.positions)
                : new List<SpreadPosition>(),
            reasonForSpread = FirstNonEmpty(action?.reason, runtimePlan?.reasonForSpread, "AI 判断这件事适合拆成牌位来看。"),
            professionalFrame = new List<string> { "牌是镜子，不是绝对预测", "客户端只执行 AI 明确请求的白名单牌阵动作" },
            requiresProForFullReading = spreadDef.cardCount >= 5,
            createdAt = System.DateTime.Now.ToString("o")
        };
    }

    private string FirstNonEmpty(params string[] values)
    {
        if (values == null) return "";
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return "";
    }

    private bool TryOpenRelationshipDivinationFromAIPlan(int streamingMessageIndex, string aiResponse)
    {
        if (_activeAtFriend == null || dialogSystem == null) return false;

        var runtimePlan = dialogSystem.GetLastRuntimePlan();
        if (runtimePlan == null || runtimePlan.stage != "before_draw") return false;
        if (!IsRelationshipDivinationRequest(runtimePlan, aiResponse)) return false;

        bool canOpenLocal = CreatedFriendRelationshipDivinationLocalFlow.CanHandle(_activeAtFriend);
        bool canOpenRemote = RelationshipDivinationFlow.CanUseTwoPersonDivination(_activeAtFriend, false);
        if (!canOpenLocal && !canOpenRemote)
            return false;

        HideLoadingIndicator();
        RefreshChatAfterAIMessage(streamingMessageIndex);
        PrepareTTSForCompletedAIMessage(streamingMessageIndex);
        RelationshipDivinationOverlay.StartForFriend(transform, _activeAtFriend);
        return true;
    }

    private bool IsRelationshipDivinationRequest(RuntimePlan runtimePlan, string aiResponse)
    {
        string latestUserText = GetLatestUserMessageText();
        string planQuestion = runtimePlan?.divinationPlan?.question ?? "";
        string combined = $"{latestUserText}\n{planQuestion}\n{aiResponse}";

        return System.Text.RegularExpressions.Regex.IsMatch(combined,
            @"((双人|关系|感情|喜欢|暧昧|复合|联系|好友|朋友|对方|他|她|ta|TA|我们|跟|和).*(占卜|塔罗|抽牌|牌阵|看牌|神谕|reading|tarot|spread|oracle))|((占卜|塔罗|抽牌|牌阵|看牌|神谕|reading|tarot|spread|oracle).*(双人|关系|感情|喜欢|暧昧|复合|联系|好友|朋友|对方|他|她|ta|TA|我们|跟|和))",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private string GetLatestUserMessageText()
    {
        if (dialogSystem == null) return "";

        for (int i = dialogSystem.GetMessageCount() - 1; i >= 0; i--)
        {
            var message = dialogSystem.GetMessageByIndex(i);
            if (message == null || message.roleType != DialogRoleType.User) continue;
            return message.content ?? "";
        }

        return "";
    }

    private void ApplyRuntimePlanForInteractionCard()
    {
        if (dialogSystem == null || divinationEngine == null) return;

        var runtimePlan = dialogSystem.GetLastRuntimePlan();
        var plan = runtimePlan?.divinationPlan;
        if (plan == null) return;
        if (runtimePlan.stage != "before_draw") return;

        divinationEngine.ApplyRuntimeDivinationPlan(plan);
    }

    /// <summary>
    /// 在对话中添加互动牌阵卡片消息（根据牌阵卡牌数自动选择 1/3/5 类型）
    /// </summary>
    private void AddInteractionCardToChat(string aiResponse)
    {
        // 从 DivinationEngine 获取合适的牌阵定义，优先取会话中已选中的牌阵
        var spreadDef = GetCurrentSpreadDefinition();
        int targetCardCount = spreadDef?.cardCount ?? GetCurrentSpreadCardCount();
        string spreadKind = spreadDef?.kind ?? FindSpreadKindByCardCount(targetCardCount);

        switch (targetCardCount)
        {
            case 1:
                dialogSystem.AddInteractionCard1Message(aiResponse, spreadKind);
                Debug.Log($"[DialogUI] 触发 InteractionCard1, spreadKind={spreadKind}");
                break;
            case 5:
                dialogSystem.AddInteractionCard5Message(aiResponse, spreadKind);
                Debug.Log($"[DialogUI] 触发 InteractionCard5, spreadKind={spreadKind}");
                break;
            default: // 3（默认）
                dialogSystem.AddInteractionCard3Message(aiResponse, spreadKind);
                Debug.Log($"[DialogUI] 触发 InteractionCard3, spreadKind={spreadKind}");
                break;
        }
    }

    /// <summary>
    /// 获取当前占卜会话预期的卡牌数量
    /// </summary>
    private int GetCurrentSpreadCardCount()
    {
        // 优先从当前会话中获取
        var def = GetCurrentSpreadDefinition();
        if (def != null) return def.cardCount;

        // 回退：默认 3 张牌阵
        return 3;
    }

    private SpreadDefinition GetCurrentSpreadDefinition()
    {
        if (divinationEngine?.CurrentSession == null)
            return null;

        string spreadKind = divinationEngine.CurrentSession.spreadKind;
        if (string.IsNullOrEmpty(spreadKind))
            spreadKind = divinationEngine.CurrentSession.divinationPlan?.spreadKind;
        if (string.IsNullOrEmpty(spreadKind))
            return null;

        return divinationEngine.GetSpreadDefinition(spreadKind);
    }

    /// <summary>
    /// 根据卡牌数量查找合适的牌阵 kind
    /// </summary>
    private string FindSpreadKindByCardCount(int cardCount)
    {
        if (divinationEngine?.SpreadDefinitions != null)
        {
            foreach (var sd in divinationEngine.SpreadDefinitions)
            {
                if (sd.cardCount == cardCount)
                    return sd.kind;
            }
        }

        // 回退默认值
        return cardCount switch
        {
            1 => "mirror_card",
            5 => "choice_gate",
            _ => "self_repair"
        };
    }

    // ---- WireUp 事件绑定 ----

    /// <summary>
    /// 为 ChatItem 中的 SpreadInteractionCard3 绑定事件
    /// </summary>
    public void WireUpInteractionCard3(SpreadInteractionCard3 card)
    {
        if (card == null) return;

        card.OnSelectSpreadClicked -= HandleSpreadFromCard;
        card.OnContinueAskClicked -= HandleContinueAskFromCard;
        card.OnCheckTomorrowClicked -= HandleCheckTomorrowFromCard;

        card.OnSelectSpreadClicked += HandleSpreadFromCard;
        card.OnContinueAskClicked += HandleContinueAskFromCard;
        card.OnCheckTomorrowClicked += HandleCheckTomorrowFromCard;
    }

    /// <summary>
    /// 为 ChatItem 中的 SpreadInteractionCard1 绑定事件
    /// </summary>
    public void WireUpInteractionCard1(SpreadInteractionCard1 card)
    {
        if (card == null) return;

        card.OnSelectSpreadClicked -= HandleSpreadFromCard;
        card.OnContinueAskClicked -= HandleContinueAskFromCard;

        card.OnSelectSpreadClicked += HandleSpreadFromCard;
        card.OnContinueAskClicked += HandleContinueAskFromCard;
    }

    /// <summary>
    /// 为 ChatItem 中的 SpreadInteractionCard5 绑定事件
    /// </summary>
    public void WireUpInteractionCard5(SpreadInteractionCard5 card)
    {
        if (card == null) return;

        card.OnSelectSpreadClicked -= HandleSpreadFromCard;
        card.OnContinueAskClicked -= HandleContinueAskFromCard;
        card.OnChatFirstClicked -= HandleChatFirstFromCard;

        card.OnSelectSpreadClicked += HandleSpreadFromCard;
        card.OnContinueAskClicked += HandleContinueAskFromCard;
        card.OnChatFirstClicked += HandleChatFirstFromCard;
    }

    // ---- 事件处理器 ----

    /// <summary>
    /// 牌阵内「选择牌阵」→ 展开牌阵选项列表
    /// </summary>
    private void HandleSpreadFromCard()
    {
        HandleSpreadSelection();
    }

    /// <summary>
    /// 牌阵内「继续追问」→ 激活输入框
    /// </summary>
    private void HandleContinueAskFromCard()
    {
        divinationEngine?.EnterFollowUp();
        if (uiComponent.questionInputField != null)
        {
            uiComponent.questionInputField.ActivateInputField();
        }
        ToastManager.ShowToast("请继续输入你想问的问题");
    }

    /// <summary>
    /// 牌阵内「明天再看」（仅三牌阵）→ 保存明日钩子
    /// </summary>
    private void HandleCheckTomorrowFromCard()
    {
        string triggerText = "明天再看这条线索";
        if (divinationEngine?.CurrentSession != null
            && divinationEngine.CurrentSession.lockedCards != null
            && divinationEngine.CurrentSession.lockedCards.Count > 0)
        {
            var cards = divinationEngine.CurrentSession.lockedCards;
            var names = new System.Text.StringBuilder();
            for (int i = 0; i < cards.Count && i < 5; i++)
            {
                if (i > 0) names.Append("、");
                names.Append(cards[i].cardName);
            }
            triggerText = $"回顾牌阵：{names}";
        }

        var hook = divinationEngine?.CreateTomorrowHook(triggerText);
        if (hook != null)
        {
            Debug.Log($"[DialogUI] 从牌阵保存明日钩子: hookId={hook.hookId}");
            PersistTomorrowHook(hook);
        }
        else
        {
            ToastManager.ShowToast("暂无活跃占卜，无法保存");
        }
    }

    private void PersistTomorrowHook(TomorrowHook hook)
    {
        if (hook == null)
        {
            ToastManager.ShowToast("暂无可保存线索");
            return;
        }

        UpsertTomorrowHookInLocalMemory(hook);
        AppNotificationScheduler.Instance.ScheduleTomorrowHookReminder(hook);

        FirestoreManager firestore = FirestoreManager.Instance;
        if (firestore == null || !firestore.IsInitialized)
        {
            ToastManager.ShowToast("已暂存线索，云端稍后同步");
            return;
        }

        firestore.SaveTomorrowHook(hook, success =>
        {
            ToastManager.ShowToast(success ? "已保存线索，明天见！" : "线索已暂存，云端同步失败");
        });
    }

    private void UpsertTomorrowHookInLocalMemory(TomorrowHook hook)
    {
        MemorySource source = dialogSystem?.GetMemorySource() ?? new MemorySource();
        source.tomorrowHooks ??= new List<TomorrowHook>();
        int index = source.tomorrowHooks.FindIndex(item => item != null && item.hookId == hook.hookId);
        if (index >= 0)
            source.tomorrowHooks[index] = hook;
        else
            source.tomorrowHooks.Add(hook);
        dialogSystem?.SetMemorySource(source);
    }

    /// <summary>
    /// 五牌阵「先继续聊聊」→ 占卜流程由用户自然推动
    /// </summary>
    private void HandleChatFirstFromCard()
    {
        if (uiComponent.questionInputField != null)
        {
            uiComponent.questionInputField.ActivateInputField();
        }
        ToastManager.ShowToast("可以先聊聊再决定是否抽牌～");
    }

    #endregion
}

/// <summary>
/// Moves the Dialog bottom input panel above the mobile soft keyboard without resizing it.
/// </summary>
public class MobileKeyboardInputAdapter : MonoBehaviour
{
    [SerializeField] private RectTransform targetPanel;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private float extraSpacing = 8f;
    [SerializeField] private float followSharpness = 18f;

    private Canvas rootCanvas;
    private Vector2 originalAnchoredPosition;
    private bool hasOriginalPosition;
    private float currentOffset;
    private int lastTextLength;

#if UNITY_ANDROID && !UNITY_EDITOR
    private int androidHiddenBottomInset;
#endif

    public void Bind(RectTransform panel, TMP_InputField field)
    {
        if (inputField != null)
        {
            inputField.onValueChanged.RemoveListener(OnInputValueChanged);
            inputField.onSelect.RemoveListener(OnInputSelected);
        }

        targetPanel = panel;
        inputField = field;
        rootCanvas = targetPanel != null ? targetPanel.GetComponentInParent<Canvas>() : null;

        if (targetPanel != null)
        {
            originalAnchoredPosition = targetPanel.anchoredPosition;
            hasOriginalPosition = true;
        }

        ConfigureInputField();

        if (inputField != null)
        {
            lastTextLength = inputField.text?.Length ?? 0;
            inputField.onValueChanged.AddListener(OnInputValueChanged);
            inputField.onSelect.AddListener(OnInputSelected);
        }
    }

    private void Awake()
    {
        if (targetPanel == null)
            targetPanel = transform as RectTransform;

        if (inputField == null)
            inputField = GetComponentInChildren<TMP_InputField>(true);

        Bind(targetPanel, inputField);
    }

    private void OnEnable()
    {
        if (targetPanel != null && !hasOriginalPosition)
        {
            originalAnchoredPosition = targetPanel.anchoredPosition;
            hasOriginalPosition = true;
        }
    }

    private void OnDisable()
    {
        ResetPanelPosition();
    }

    private void OnDestroy()
    {
        if (inputField != null)
        {
            inputField.onValueChanged.RemoveListener(OnInputValueChanged);
            inputField.onSelect.RemoveListener(OnInputSelected);
        }
    }

    private void LateUpdate()
    {
        if (targetPanel == null || !hasOriginalPosition)
            return;

        float targetOffset = ShouldFollowKeyboard()
            ? GetKeyboardHeightInCanvasUnits() + extraSpacing
            : 0f;

        if (targetOffset < 1f)
            targetOffset = 0f;

        float t = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
        currentOffset = Mathf.Lerp(currentOffset, targetOffset, t);

        if (Mathf.Abs(currentOffset - targetOffset) < 0.5f)
            currentOffset = targetOffset;

        targetPanel.anchoredPosition = originalAnchoredPosition + Vector2.up * currentOffset;
    }

    private void ConfigureInputField()
    {
        if (inputField == null) return;

        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.resetOnDeActivation = false;

        if (inputField.textComponent != null)
        {
            inputField.textComponent.enableWordWrapping = false;
            inputField.textComponent.overflowMode = TextOverflowModes.Masking;
        }
    }

    private bool ShouldFollowKeyboard()
    {
        if (inputField == null || !inputField.isFocused)
            return false;

#if UNITY_IOS || UNITY_ANDROID
        return true;
#else
        return false;
#endif
    }

    private float GetKeyboardHeightInCanvasUnits()
    {
        float keyboardPixels = GetKeyboardHeightPixels();
        if (keyboardPixels <= 0f)
            return 0f;

        float scaleFactor = rootCanvas != null && rootCanvas.scaleFactor > 0f
            ? rootCanvas.scaleFactor
            : 1f;

        float canvasUnits = keyboardPixels / scaleFactor;
        return Mathf.Min(canvasUnits, GetMaxAllowedOffset());
    }

    private float GetKeyboardHeightPixels()
    {
        float height = TouchScreenKeyboard.area.height;

#if UNITY_ANDROID && !UNITY_EDITOR
        height = Mathf.Max(height, GetAndroidKeyboardHeightPixels());
#endif

        float threshold = Mathf.Max(80f, Screen.height * 0.08f);
        return height >= threshold ? height : 0f;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private float GetAndroidKeyboardHeightPixels()
    {
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow"))
            using (AndroidJavaObject decorView = window.Call<AndroidJavaObject>("getDecorView"))
            using (AndroidJavaObject visibleFrame = new AndroidJavaObject("android.graphics.Rect"))
            {
                decorView.Call("getWindowVisibleDisplayFrame", visibleFrame);
                int visibleBottom = visibleFrame.Get<int>("bottom");
                int rootHeight = decorView.Call<int>("getHeight");
                int bottomInset = Mathf.Max(0, rootHeight - visibleBottom);
                int threshold = Mathf.RoundToInt(Mathf.Max(80f, Screen.height * 0.08f));

                if (bottomInset < threshold)
                {
                    androidHiddenBottomInset = Mathf.Max(androidHiddenBottomInset, bottomInset);
                    return 0f;
                }

                return Mathf.Max(0, bottomInset - androidHiddenBottomInset);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[MobileKeyboardInputAdapter] Android keyboard height unavailable: " + e.Message);
            return 0f;
        }
    }
#endif

    private float GetMaxAllowedOffset()
    {
        RectTransform parentRect = targetPanel != null ? targetPanel.parent as RectTransform : null;
        if (parentRect == null)
            return Screen.height * 0.8f;

        float panelHeight = targetPanel.rect.height;
        return Mathf.Max(0f, parentRect.rect.height - panelHeight);
    }

    private void OnInputSelected(string _)
    {
        KeepLatestTextVisible();
    }

    private void OnInputValueChanged(string _)
    {
        KeepLatestTextVisible();
    }

    public void KeepLatestTextVisible()
    {
        if (inputField == null)
            return;

        string value = inputField.text ?? string.Empty;
        int length = value.Length;
        bool textGrew = length >= lastTextLength;
        bool caretIsAtEnd = inputField.stringPosition >= Mathf.Max(0, length - 1)
            || inputField.caretPosition >= Mathf.Max(0, length - 1);

        if (inputField.isFocused && textGrew && caretIsAtEnd)
            inputField.MoveTextEnd(false);

        inputField.ForceLabelUpdate();
        lastTextLength = length;
    }

    private void ResetPanelPosition()
    {
        if (targetPanel == null || !hasOriginalPosition)
            return;

        currentOffset = 0f;
        targetPanel.anchoredPosition = originalAnchoredPosition;
    }
}
