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


public class DialogUI : WindowBase
{
    public DialogUIComponent uiComponent;

    private LoopListView2 chatListView;

    private string userItemPrefabName = "ChatRootRight";
    private string aiItemPrefabName = "MessageItem";

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

    private TTSManager ttsManager;
    private ChatItem _currentTTSItem; // 当前正在播放语音的 ChatItem
    private Coroutine _ttsPlaybackCoroutine;
    private Coroutine _cloudDialogLoadCoroutine;
    private readonly Queue<System.Action> _pendingAIRequests = new Queue<System.Action>();
    private bool _forceNextAIMessageAsThreeCardSpread;
    private const float CLOUD_DIALOG_LOAD_RETRY_SECONDS = 8f;
    private const float CLOUD_DIALOG_LOAD_RETRY_INTERVAL = 0.5f;

    #region 生命周期函数
    // 调用机制与 Mono Awake 一致
    public override void OnAwake()
    {
        uiComponent = gameObject.GetComponent<DialogUIComponent>();
        uiComponent.InitComponent(this);
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

        // 订阅事件
        EventSystem.AddEvent(GameDataStr.RefreshChatUI, OnRefreshChatUI);
        EventSystem.AddEventListener<string>(GameDataStr.QuickQuestionSelected, OnQuickQuestionSelected);
        EventSystem.AddEventListener<string>(GameDataStr.CardTopicSelected, OnCardTopicSelected);

        UpdateDivinerInfo();
    }
    // 物体显示时执行
    public override void OnShow()
    {
        base.OnShow();
        LoadCloudDialogState();
    }
    // 物体隐藏时执行
    public override void OnHide()
    {
        base.OnHide();
    }
    // 物体销毁时执行
    public override void OnDestroy()
    {
        EventSystem.RemoveEvent(GameDataStr.RefreshChatUI, OnRefreshChatUI);
        EventSystem.RemoveEventListener<string>(GameDataStr.QuickQuestionSelected, OnQuickQuestionSelected);
        EventSystem.RemoveEventListener<string>(GameDataStr.CardTopicSelected, OnCardTopicSelected);
        base.OnDestroy();
    }
    #endregion

    #region 初始化

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
        bool historyCompleted = false;
        bool historyRequestInFlight = false;
        float elapsed = 0f;

        while (elapsed <= CLOUD_DIALOG_LOAD_RETRY_SECONDS)
        {
            if (!memoryRequested && FirestoreManager.Instance != null && FirestoreManager.Instance.IsInitialized)
            {
                memoryRequested = true;
                FirestoreManager.Instance.LoadMemorySource(source =>
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
                    }
                    else if (attemptedCloud)
                    {
                        historyCompleted = true;
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
    }

    /// <summary>
    /// 初始化 TTS 管理器
    /// </summary>
    private void InitTTSManager()
    {
        if (!enableTTS) return;

        ttsManager = TTSManager.ResolveFor(gameObject);
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

        // 调用流式 API
        streamingMessageIndex = dialogSystem.SendMessageToAIStream(
            // ---- onChunk: 每收到一个 token ----
            (chunk) =>
            {
                if (mIsFirstChunk)
                {
                    if (_forceNextAIMessageAsThreeCardSpread && ShouldTriggerInteractionCard())
                    {
                        ConvertMessageToInteractionCard(streamingMessageIndex, "", 3);
                        _forceNextAIMessageAsThreeCardSpread = false;
                    }

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
                HideLoadingIndicator();

                // ---- 占卜完成检测：如果处于 CardsLocked 阶段（AI 已完成解读），自动保存到 Firestore ----
                if (divinationEngine != null
                    && divinationEngine.CurrentSession != null
                    && divinationEngine.CurrentPhase == DivinationPhase.CardsLocked)
                {
                    divinationEngine.CompleteDivination(fullContent);
                    dialogSystem.ApplyDivinationReplyToActiveSpread(fullContent);
                }

                // ---- 检查是否需要展示 InteractionCard ----
                if (ShouldTriggerInteractionCard())
                {
                    ConvertMessageToInteractionCard(streamingMessageIndex, fullContent);
                    _forceNextAIMessageAsThreeCardSpread = false;
                }
                else
                {
                    // 普通 AI 回复：设置选项按钮
                    List<string> options = dialogSystem.GetCurrentDivinerOptions();
                    dialogSystem.SetAIMessageOptions(streamingMessageIndex, options);
                }

                // 最终完整刷新
                int msgCount = dialogSystem.GetMessageCount();
                Debug.Log($"[DialogUI] 流式完成，最终消息数: {msgCount}");
                chatListView.SetListItemCount(msgCount, false);
                chatListView.MovePanelToItemIndex(msgCount - 1, 0);
                chatListView.RefreshAllShownItem();
                ProcessNextQueuedAIRequest();
            },
            // ---- onError: 流式出错 ----
            (error) =>
            {
                HideLoadingIndicator();
                _forceNextAIMessageAsThreeCardSpread = false;

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
        int targetCardCount = forcedCardCount > 0 ? forcedCardCount : GetCurrentSpreadCardCount();
        string spreadKind = FindSpreadKindByCardCount(targetCardCount);
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
        //todo:塔罗牌数据
        //添加     
        dialogSystem.AddTodayDivinationMessage("");
        UpdateChatScrollView();
    }
    public void SendAtFriendsMessage()
    {
        SendAtFriendsMessage(null);
    }

    public void SendAtFriendsMessage(FriendDataManager.FriendData friend)
    {
        dialogSystem.AddAtFriendMessage("", friend);
        UpdateChatScrollView();
        Debug.Log(friend != null ? $"关联好友：{friend.name}" : "关联好友");
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
         // 更新列表 - 移除 RefreshAllShownItem，让 ScrollView 自动处理
        int msgCount = dialogSystem.GetMessageCount();
        Debug.Log($"发送用户消息后，当前消息数量：{msgCount}");
        chatListView.SetListItemCount(msgCount, false);

        // 滚动到最后一条
        chatListView.MovePanelToItemIndex(
            msgCount - 1,
            0
        );
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

        string inputText = uiComponent.questionInputField.text;
        if (string.IsNullOrEmpty(inputText))
        {
            ToastManager.ShowToast("请写下你想问的问题。");
            return;
        }

        if (mIsLoading)
        {
            SendUserMessage(inputText);
            uiComponent.questionInputField.text = "";
            ToastManager.ShowToast("AI正在回复，已为你排队发送。");
            return;
        }
        Debug.Log($"发送信息：{inputText}");
        // 发送消息
        SendUserMessage(inputText);

        // 清空输入框
        uiComponent.questionInputField.text = "";
    }

    /// <summary>
    /// 输入框内容变化
    /// </summary>
    public void OnquestionInputChange(string text)
    {
    }

    /// <summary>
    /// 输入框结束编辑
    /// </summary>
    public void OnquestionInputEnd(string text)
    {

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
        chatListView.SetListItemCount(msgCount, false);
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

        // 启动占卜引擎
        if (divinationEngine != null)
        {
            var session = divinationEngine.StartQuickDivination(question);
            Debug.Log($"[DialogUI] 占卜已启动 [{session.readingId}], phase={session.phase}");

            // 如果携带今日牌，同步到 DialogSystem
            if (divinationEngine.TodayCard.HasValue)
            {
                dialogSystem.SetTodayCardPayload(divinationEngine.GetTodayCardPayload());
            }
        }

        // 添加用户消息
        dialogSystem.AddUserMessage(question);
        _forceNextAIMessageAsThreeCardSpread = true;

        UpdateChatScrollView();

        // 发送到 AI（此时 DialogSystem 已携带 readingState/actionKing，OracleRuntime 会走 plan_spread scene）
        SendMessageToAI();
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
            ToastManager.ShowToast("已保存线索，明天见！");
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
        if (divinationEngine != null)
        {
            divinationEngine.StartQuickDivination("请分析这段关系的周期和走向");
        }
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

        string text = msgData.content;

        // 停止之前的播放
        StopTTSPlayback();
        ConfigureTTSVoice(msgData.divinerType);

        // 标记当前播放项
        _currentTTSItem = item;
        item.ShowTTSLoading(true);

        Debug.Log($"[DialogUI] TTS 请求播放: {text.Substring(0, Mathf.Min(text.Length, 30))}...");

        _ttsPlaybackCoroutine = uiComponent.StartCoroutine(SpeakAndPlayTextSegments(item, text));
    }

    /// <summary>
    /// 按服务端限制拆分长文本，逐段合成并播放。
    /// </summary>
    private System.Collections.IEnumerator SpeakAndPlayTextSegments(ChatItem item, string text)
    {
        var segments = SplitTTSText(text, Mathf.Max(100, ttsSegmentMaxLength));
        if (segments.Count == 0)
        {
            item.ShowTTSLoading(false);
            _currentTTSItem = null;
            yield break;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            if (_currentTTSItem != item)
                yield break;

            AudioClip clip = null;
            string synthError = null;
            bool done = false;

            item.ShowTTSLoading(true);
            ttsManager.Speak(segments[i],
                (result) =>
                {
                    clip = result;
                    done = true;
                },
                (error) =>
                {
                    synthError = error;
                    done = true;
                });

            yield return new WaitUntil(() => done || _currentTTSItem != item);
            if (_currentTTSItem != item)
                yield break;

            item.ShowTTSLoading(false);

            if (!string.IsNullOrEmpty(synthError) || clip == null)
            {
                Debug.LogError($"[DialogUI] TTS 合成失败: {synthError ?? "AudioClip 为空"}");
                ToastManager.ShowToast("语音生成失败");
                _currentTTSItem = null;
                yield break;
            }

            if (AudioManager.Instance == null)
            {
                Debug.LogWarning("[DialogUI] AudioManager.Instance 不存在");
                ToastManager.ShowToast("音频管理器未就绪");
                _currentTTSItem = null;
                yield break;
            }

            AudioManager.Instance.PlayVoice(clip, interrupt: true);
            Debug.Log($"[DialogUI] TTS 播放第 {i + 1}/{segments.Count} 段, 时长={clip.length:F1}s");

            yield return new WaitForSeconds(clip.length + 0.08f);
        }

        if (_currentTTSItem == item)
            _currentTTSItem = null;
        _ttsPlaybackCoroutine = null;
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
    private void OnRegenerateVoiceClick()
    {
        Debug.Log("重新生成声音按钮点击");
        if (ttsManager == null)
        {
            ToastManager.ShowToast("TTS 服务未就绪，请在 TTSManager 中配置 API 密钥");
            return;
        }
        ToastManager.ShowToast("正在重新生成语音...");
        // TODO: 如果需要重新生成（换音色），调用 ttsManager.ClearCache() 后重新播放
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

    /// <summary>
    /// 在对话中添加互动牌阵卡片消息（根据牌阵卡牌数自动选择 1/3/5 类型）
    /// </summary>
    private void AddInteractionCardToChat(string aiResponse)
    {
        // 从 DivinationEngine 获取合适的牌阵定义，优先取会话中已选中的牌阵
        int targetCardCount = GetCurrentSpreadCardCount();

        // 查找对应卡牌数的牌阵
        string spreadKind = FindSpreadKindByCardCount(targetCardCount);

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
        if (divinationEngine?.CurrentSession != null
            && !string.IsNullOrEmpty(divinationEngine.CurrentSession.spreadKind))
        {
            var def = divinationEngine.GetSpreadDefinition(divinationEngine.CurrentSession.spreadKind);
            if (def != null) return def.cardCount;
        }

        // 回退：默认 3 张牌阵
        return 3;
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
            1 => "single_mirror",
            5 => "five_choice_gate",
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
            ToastManager.ShowToast("已保存线索，明天见！");
        }
        else
        {
            ToastManager.ShowToast("暂无活跃占卜，无法保存");
        }
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
