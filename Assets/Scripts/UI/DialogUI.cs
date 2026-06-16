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

    private DialogSystem dialogSystem;
    private DivinationEngine divinationEngine;

    #region 生命周期函数
    // 调用机制与 Mono Awake 一致
    public override void OnAwake()
    {
        uiComponent = gameObject.GetComponent<DialogUIComponent>();
        uiComponent.InitComponent(this);
        this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
        base.OnAwake();

        dialogSystem = DialogSystem.Instance;

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

    /// <summary>
    /// 初始化对话框系统
    /// </summary>
    private void InitDialogSystem()
    {
        if (dialogSystem == null)
        {

        }
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

        return item;
    }

    #endregion

    #region 发送和接收消息

    /// <summary>
    /// 发送用户消息
    /// </summary>
    private void SendUserMessage(string content)
    {
        // 添加用户消息到数据层
        dialogSystem.AddUserMessage(content);

        UpdateChatScrollView();

        // 发送消息到AI
        SendMessageToAI();
    }

    /// <summary>
    /// 发送消息到AI
    /// </summary>
    private void SendMessageToAI()
    {
        if (dialogSystem == null) return;
        Debug.Log("发送消息到AI");
        // 显示加载中提示
        ShowLoadingIndicator();

        // 调用DeepSeek API
        dialogSystem.SendMessageToAI(
            (aiResponse) =>
                {
                    // 隐藏加载中提示
                    HideLoadingIndicator();

                    // ---- 检查是否需要展示 InteractionCard（1/3/5 牌阵）----
                    if (ShouldTriggerInteractionCard())
                    {
                        AddInteractionCardToChat(aiResponse);
                    }
                    else
                    {
                        // 普通 AI 回复
                        List<string> options = dialogSystem.GetCurrentDivinerOptions();
                        dialogSystem.AddAIMessage(aiResponse, options);
                    }

                    // 更新列表
                    if (chatListView != null)
                    {
                        int msgCount = dialogSystem.GetMessageCount();
                        Debug.Log($"接收AI消息后，当前消息数量：{msgCount}");
                        chatListView.SetListItemCount(msgCount, false);

                        chatListView.MovePanelToItemIndex(
                         msgCount - 1,
                         0
                         );
                    }
                },
            (error) =>
            {
                // 隐藏加载中提示
                HideLoadingIndicator();

                Debug.LogError("AI响应错误: " + error);
                ToastManager.ShowToast("AI响应失败，请稍后重试。");
            }
        );
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
        dialogSystem.AddAtFriendMessage("");
        UpdateChatScrollView();
        Debug.Log("关联好友");
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
            Debug.LogWarning("[DialogUI] AI 正在回复中，消息已进入队列");
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
            ToastManager.ShowToast("请等待AI回复完成。");
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
    /// 重新生成声音按钮点击回调
    /// </summary>
    private void OnRegenerateVoiceClick()
    {
        Debug.Log("重新生成声音按钮点击");
        ToastManager.ShowToast("正在重新生成语音...");
        // TODO: 实现语音合成功能
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
