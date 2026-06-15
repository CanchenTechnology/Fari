using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFGameFrameWork;
using GamerFrameWork.OracleRuntime;

/// <summary>
/// 占卜师类型
/// </summary>
public enum DivinerType
{
    Tarot,      // 塔罗师
    Astrology,  // 占星师
}

/// <summary>
/// 消息类型
/// </summary>
public enum DialogRoleType
{
    User,       // 用户消息
    AI,         // AI回复
}
public enum MsgType
{
    Str,  //普通消息
    PopupWindow,// 弹窗
    Picture,
    Voice,
    AtFriend,//@好友
    DailyCard, //今日塔罗牌

    InteractionCard3, //三排牌阵
}

/// <summary>
/// 对话消息数据
/// </summary>
[System.Serializable]
public class ChatMessageData
{
    public int id;
    public DialogRoleType roleType;
    public MsgType messageType;
    public string content;
    public List<string> options; // AI回复的选项按钮文本
    public DivinerType divinerType; // 当messageType为AI时使用
}

/// <summary>
/// 对话系统
/// 管理对话数据、占卜师类型、消息历史等
/// </summary>
public class DialogSystem : MonoSingleton<DialogSystem>
{

    [Header("当前占卜师类型")]
    public DivinerType CurrentDivinerType = DivinerType.Tarot;

    [Header("占卜师配置")]
    public string TarotDivinerName = "塔罗师";
    public string AstrologyDivinerName = "占星师";
    public string UserName = "小夜";

    [Header("头像资源名称")]
    public string TarotHeadIcon = "TarotHead";
    public string AstrologyHeadIcon = "AstrologyHead";
    public string UserHeadIcon = "UserHead";

    [Header("DeepSeek API")]
    public DeepSeekAPI deepSeekAPI;

    [Header("系统提示词")]
    [TextArea(3, 10)]
    public string TarotSystemPrompt = "你是一位神秘的塔罗师，说话风格神秘而优雅，善于用塔罗牌的意象来回答问题。你的回复应该包含塔罗牌相关的隐喻和建议。回复 concise 一些，在200字以内。";

    [TextArea(3, 10)]
    public string AstrologySystemPrompt = "你是一位专业的占星师，说话风格温柔而知性，善于用星象和星座来回答问题。你的回复应该包含星象相关的分析和建议。回复 concise 一些，在200字以内。";

    [Header("Oracle Runtime 集成")]
    [Tooltip("启用后使用 ContextAssembler 结构化 Prompt，替代简单系统提示词")]
    public bool useOracleRuntime = true;

    /// <summary>Oracle Runtime 用户记忆数据源</summary>
    private MemorySource memorySource = new MemorySource();

    /// <summary>当前活跃的占卜阅读锁（防止追问时重新抽牌）</summary>
    private ReadingLock readingLock;

    /// <summary>当前活跃的关系 ID（好友合盘场景）</summary>
    private string activeRelationshipId;

    /// <summary>当前占卜 Reading ID</summary>
    private string activeReadingId;

    /// <summary>当前占卜状态（供 DivinationEngine 同步）</summary>
    private string activeReadingState = "";

    /// <summary>当前动作类型（供 SceneRouter 路由）</summary>
    private string activeActionKind = "";

    /// <summary>今日牌数据（供 ChatPayload 使用）</summary>
    private TodayCardPayload todayCardPayload;

    /// <summary>临时覆盖的选项列表（一次性消耗，例如牌阵选择）</summary>
    private List<string> _overriddenOptions;

    /// <summary>最近 N 轮用户发言（供 ChatPayload 使用）</summary>
    private List<string> recentUserMessages = new List<string>();

    /// <summary>最近 N 轮 AI 回复（供 ChatPayload 使用）</summary>
    private List<string> recentAssistantReplies = new List<string>();

    private const int MAX_RECENT_MESSAGES = 6;

    // 消息列表
    private List<ChatMessageData> mChatMessageList = new List<ChatMessageData>();

    // DeepSeek API 消息历史
    private List<DeepSeekAPI.Message> mApiMessageHistory = new List<DeepSeekAPI.Message>();

    // 消息ID计数器
    private int mMessageIdCounter = 0;

    protected override void Awake()
    {
        base.Awake();
        if (deepSeekAPI == null)
        {
            deepSeekAPI = gameObject.GetComponent<DeepSeekAPI>();
            if (deepSeekAPI == null)
            {
                deepSeekAPI = gameObject.AddComponent<DeepSeekAPI>();
            }
        }
    }

    /// <summary>
    /// 切换占卜师类型
    /// </summary>
    public void SwitchDivinerType()
    {
        CurrentDivinerType = (CurrentDivinerType == DivinerType.Tarot) ? DivinerType.Astrology : DivinerType.Tarot;
        Debug.Log("切换占卜师为: " + GetCurrentDivinerName());
    }

    /// <summary>
    /// 获取当前占卜师名称
    /// </summary>
    public string GetCurrentDivinerName()
    {
        return CurrentDivinerType == DivinerType.Tarot ? TarotDivinerName : AstrologyDivinerName;
    }

    /// <summary>
    /// 获取当前占卜师头像
    /// </summary>
    public string GetCurrentDivinerHeadIcon()
    {
        return CurrentDivinerType == DivinerType.Tarot ? TarotHeadIcon : AstrologyHeadIcon;
    }

    /// <summary>
    /// 获取当前系统提示词
    /// </summary>
    public string GetCurrentSystemPrompt()
    {
        return CurrentDivinerType == DivinerType.Tarot ? TarotSystemPrompt : AstrologySystemPrompt;
    }

    /// <summary>
    /// 添加用户消息
    /// </summary>
    public ChatMessageData AddUserMessage(string content)
    {
        ChatMessageData data = new ChatMessageData
        {
            id = mMessageIdCounter++,
            roleType = DialogRoleType.User,
            messageType = MsgType.Str,
            content = content,
            options = null,
            divinerType = CurrentDivinerType
        };
        mChatMessageList.Add(data);

        // 同时添加到 API 历史
        mApiMessageHistory.Add(new DeepSeekAPI.Message("user", content));

        // 追踪最近用户消息
        recentUserMessages.Add(content);
        if (recentUserMessages.Count > MAX_RECENT_MESSAGES)
            recentUserMessages.RemoveAt(0);

        return data;
    }

    /// <summary>
    /// 添加AI回复消息
    /// </summary>
    public ChatMessageData AddAIMessage(string content, List<string> options = null)
    {
        ChatMessageData data = new ChatMessageData
        {
            id = mMessageIdCounter++,
            roleType = DialogRoleType.AI,

            messageType = MsgType.Str,
            content = content,
            options = options,
            divinerType = CurrentDivinerType
        };
        mChatMessageList.Add(data);

        // 同时添加到 API 历史
        mApiMessageHistory.Add(new DeepSeekAPI.Message("assistant", content));

        // 追踪最近 AI 回复
        recentAssistantReplies.Add(content);
        if (recentAssistantReplies.Count > MAX_RECENT_MESSAGES)
            recentAssistantReplies.RemoveAt(0);

        return data;
    }

    public ChatMessageData AddTodayDivinationMessage(string content)
    {
         ChatMessageData data = new ChatMessageData
        {
            id = mMessageIdCounter++,
            roleType = DialogRoleType.AI,

            messageType = MsgType.DailyCard,
            content = content,

            divinerType = CurrentDivinerType
        };
        mChatMessageList.Add(data);

        // 同时添加到 API 历史
        mApiMessageHistory.Add(new DeepSeekAPI.Message("assistant", content));

        return data;
    }
    public ChatMessageData AddAtFriendMessage(string content)
    {
         ChatMessageData data = new ChatMessageData
        {
            id = mMessageIdCounter++,
            roleType = DialogRoleType.AI,

            messageType = MsgType.AtFriend,
            content = content,

            divinerType = CurrentDivinerType
        };
        mChatMessageList.Add(data);

        // 同时添加到 API 历史
        mApiMessageHistory.Add(new DeepSeekAPI.Message("assistant", content));

        return data;
    }
    /// <summary>
    /// 获取所有消息
    /// </summary>
    public List<ChatMessageData> GetAllMessages()
    {
        return mChatMessageList;
    }

    /// <summary>
    /// 获取消息数量
    /// </summary>
    public int GetMessageCount()
    {
        return mChatMessageList.Count;
    }

    /// <summary>
    /// 获取指定索引的消息
    /// </summary>
    public ChatMessageData GetMessageByIndex(int index)
    {
        if (index < 0 || index >= mChatMessageList.Count)
            return null;
        return mChatMessageList[index];
    }

    /// <summary>
    /// 获取用于 API 请求的消息历史（包含系统提示词）
    /// </summary>
    public List<DeepSeekAPI.Message> GetApiMessagesWithSystemPrompt()
    {
        List<DeepSeekAPI.Message> messages = new List<DeepSeekAPI.Message>();
        messages.Add(new DeepSeekAPI.Message("system", GetCurrentSystemPrompt()));
        messages.AddRange(mApiMessageHistory);
        return messages;
    }

    /// <summary>
    /// 构建 ChatPayload 供 ContextAssembler 使用
    /// </summary>
    private ChatPayload BuildChatPayload(string userMessage)
    {
        var payload = new ChatPayload
        {
            scene = "chat_companion_stream",
            locale = "zh-CN",
            message = userMessage ?? "",
            activeRelationshipId = activeRelationshipId ?? "",
            activeReadingId = activeReadingId ?? "",
            activeReadingState = string.IsNullOrEmpty(activeReadingState)
                ? (readingLock != null ? "cards_locked" : "")
                : activeReadingState,
            isReturningFromHook = false,
            actionKind = activeActionKind ?? "",
            todayCard = todayCardPayload,
            conversationSummary = "",
            lastOracleReply = recentAssistantReplies.Count > 0
                ? recentAssistantReplies[recentAssistantReplies.Count - 1]
                : "",
            rationaleQuestion = false,
            friendContext = "",
            memoryUsed = new List<string>(),
            recentMessages = new List<string>(recentUserMessages),
            recentAssistantReplies = new List<string>(recentAssistantReplies),

            user = new UserPayloadProfile
            {
                userId = "",
                preferredName = memorySource.stableProfile?.preferredName ?? UserName,
                preferredTone = GetOracleVoiceId(),
                locale = "zh-CN",
                activeRelationships = string.IsNullOrEmpty(activeRelationshipId)
                    ? null
                    : new List<string> { activeRelationshipId },
                recentThemes = memorySource.stableProfile?.recurringThemes
            }
        };

        return payload;
    }

    /// <summary>
    /// 使用 OracleRuntime ContextAssembler 构建消息列表
    /// 替换旧的简单 system prompt，使用 fari 原版的 5 段结构化提示词 + 对话历史
    /// </summary>
    private List<DeepSeekAPI.Message> GetOracleAssembledMessages(string userMessage)
    {
        // 构建 ChatPayload
        var payload = BuildChatPayload(userMessage);

        // 调用 ContextAssembler 生成 6 条消息
        // [0..4] = system, [5] = user payload
        var assemblyResult = ContextAssembler.AssembleStreamingChat(
            payload, memorySource, readingLock);

        var messages = new List<DeepSeekAPI.Message>();

        if (assemblyResult?.messages != null)
        {
            foreach (var cm in assemblyResult.messages)
            {
                messages.Add(new DeepSeekAPI.Message(cm.role, cm.content));
            }
        }
        else
        {
            // 降级：如果组装失败，使用旧的简单提示词
            Debug.LogWarning("[OracleRuntime] ContextAssembler returned null, falling back to simple prompt.");
            return GetApiMessagesWithSystemPrompt();
        }

        // 追加最近 N 轮对话历史（不含当前用户消息，因为已在 payload 中）
        // 取 mApiMessageHistory 中最近几轮，跳过最后一个（即当前用户消息）
        int historyCount = mApiMessageHistory.Count;
        int skipLast = (historyCount > 0 && mApiMessageHistory[historyCount - 1].role == "user") ? 1 : 0;
        int maxHistory = 6; // 最多追加 3 轮对话
        int startIdx = Mathf.Max(0, historyCount - skipLast - maxHistory);
        for (int i = startIdx; i < historyCount - skipLast; i++)
        {
            messages.Add(mApiMessageHistory[i]);
        }

        Debug.Log($"[OracleRuntime] Assembled {messages.Count} messages for API request. "
            + $"Scene={payload.scene}, Stage=auto-detected, Voice={GetOracleVoiceId()}");

        return messages;
    }

    /// <summary>
    /// 设置用户记忆（供外部模块 Firebase 加载后调用）
    /// </summary>
    public void SetMemorySource(MemorySource source)
    {
        memorySource = source ?? new MemorySource();
    }

    /// <summary>
    /// 设置当前 ReadingLock（抽牌后调用，防止追问时重新抽牌）
    /// </summary>
    public void SetReadingLock(ReadingLock rl)
    {
        readingLock = rl;
        activeReadingId = rl?.readingId;
    }

    /// <summary>
    /// 设置活跃关系 ID（好友合盘场景）
    /// </summary>
    public void SetActiveRelationship(string relationshipId)
    {
        activeRelationshipId = relationshipId;
    }

    /// <summary>
    /// 设置当前占卜状态（DivinationEngine 调用）
    /// </summary>
    public void SetActiveReadingState(string state)
    {
        activeReadingState = state ?? "";
    }

    /// <summary>
    /// 设置当前动作类型（DivinationEngine 调用）
    /// </summary>
    public void SetActiveActionKind(string actionKind)
    {
        activeActionKind = actionKind ?? "";
    }

    /// <summary>
    /// 设置当前 Reading ID（DivinationEngine 调用）
    /// </summary>
    public void SetActiveReadingId(string readingId)
    {
        activeReadingId = readingId;
    }

    /// <summary>
    /// 设置今日牌数据（TodayOracleUI / DivinationEngine 调用）
    /// </summary>
    public void SetTodayCardPayload(TodayCardPayload payload)
    {
        todayCardPayload = payload;
    }

    /// <summary>
    /// 清除 ReadingLock
    /// </summary>
    public void ClearReadingLock()
    {
        readingLock = null;
        activeReadingId = null;
    }

    /// <summary>
    /// 清空对话历史
    /// </summary>
    public void ClearChatHistory()
    {
        mChatMessageList.Clear();
        mApiMessageHistory.Clear();
        recentUserMessages.Clear();
        recentAssistantReplies.Clear();
        mMessageIdCounter = 0;
    }

    /// <summary>
    /// 发送消息到 DeepSeek API
    /// 启用 OracleRuntime 时使用 ContextAssembler 组装结构化 Prompt
    /// </summary>
    public void SendMessageToAI(System.Action<string> onSuccess, System.Action<string> onError)
    {
        List<DeepSeekAPI.Message> messages;

        if (useOracleRuntime)
        {
            // 使用 ContextAssembler 构建 5 段结构化 system prompt + 对话历史
            string lastUserMsg = (mApiMessageHistory.Count > 0
                && mApiMessageHistory[mApiMessageHistory.Count - 1].role == "user")
                ? mApiMessageHistory[mApiMessageHistory.Count - 1].content
                : "";
            messages = GetOracleAssembledMessages(lastUserMsg);
        }
        else
        {
            // 降级：使用旧版简单 system prompt
            messages = GetApiMessagesWithSystemPrompt();
        }

        deepSeekAPI.SendChatRequest(messages,
            (aiResponse) =>
            {
                // OracleRuntime 输出校验（非阻断，仅日志）
                if (useOracleRuntime)
                {
                    try
                    {
                        var guardResult = OutputGuard.Check(aiResponse);
                        if (!guardResult.ok && guardResult.issues?.Count > 0)
                        {
                            Debug.LogWarning($"[OracleRuntime] OutputGuard issues: {string.Join(", ", guardResult.issues)}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[OracleRuntime] OutputGuard check failed: {ex.Message}");
                    }
                }
                onSuccess?.Invoke(aiResponse);
            },
            onError
        );
    }

    /// <summary>
    /// 获取塔罗师回复的默认选项
    /// </summary>
    public List<string> GetTarotOptions()
    {
        return new List<string>
        {
            "为这个问题选牌阵",
            "继续追问",
            "明天再看这条线索"
        };
    }

    /// <summary>
    /// 获取占星师回复的默认选项
    /// </summary>
    public List<string> GetAstrologyOptions()
    {
        return new List<string>
        {
            "看这段关系的周期",
            "分析今日星象",
            "看下一周趋势",
            "保存明日回看"
        };
    }

    /// <summary>
    /// DivinerType → oracleVoiceId 映射
    /// </summary>
    private string GetOracleVoiceId()
    {
        return CurrentDivinerType == DivinerType.Tarot ? "tarot_reader" : "astrologer";
    }

    /// <summary>
    /// 获取当前占卜师的默认选项
    /// </summary>
    public List<string> GetCurrentDivinerOptions()
    {
        // 如果有临时覆盖的选项（如牌阵选择），优先使用
        if (_overriddenOptions != null && _overriddenOptions.Count > 0)
        {
            var temp = _overriddenOptions;
            _overriddenOptions = null; // 一次性消耗
            return temp;
        }
        return CurrentDivinerType == DivinerType.Tarot ? GetTarotOptions() : GetAstrologyOptions();
    }

    /// <summary>
    /// 临时覆盖占卜师选项（如展示牌阵选择）
    /// </summary>
    public void SetDivinerOptions(List<string> options)
    {
        _overriddenOptions = options;
    }

    /// <summary>
    /// 获取指定索引消息的摘要片段
    /// </summary>
    public string GetMessageSnippet(int index, int maxLen)
    {
        if (index < 0 || index >= mChatMessageList.Count) return "";
        var msg = mChatMessageList[index];
        if (string.IsNullOrEmpty(msg.content)) return "";
        return msg.content.Length <= maxLen ? msg.content : msg.content.Substring(0, maxLen) + "...";
    }
}
