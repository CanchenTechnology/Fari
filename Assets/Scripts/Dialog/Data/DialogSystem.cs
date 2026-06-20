using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    InteractionCard1, //单排牌阵（单张镜像牌阵）
    InteractionCard5, //五排牌阵（五牌选择门牌阵）
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
    public string spreadKind;       // 牌阵类型（InteractionCard1/3/5 使用）
    public bool spreadCardsDrawn;   // 牌阵是否已经完成抽牌（用于聊天列表刷新后恢复状态）
    public List<TarotDrawData> spreadDrawnCards; // 已抽到的牌（InteractionCard 使用）
    public bool spreadDrawResultAddedToHistory; // 抽牌结果是否已写入 AI 上下文，避免重复追加
    public string readingId;        // 这条牌阵消息对应的占卜 ID
    public string divinationQuestion;
    public string divinationScene;
    public string divinationCreatedAt;
    public string shortVerdict;
    public string judgeContent;
    public string adviceContent;
    public List<string> followupTopics;
    public string friendName;
    public string friendContext;
    public bool ttsAudioReady;       // 是否已经为这条 AI 文本准备过语音
    public float ttsDurationSeconds; // 语音总时长，用于聊天气泡显示
    public string oraclePromptId;
    public string oracleScene;
    public string oracleStage;
    public string oracleStageReason;
    public string oracleResponseMode;
    public string oracleRiskLevel;
    public List<string> oracleRiskFlags;
}

/// <summary>
/// 聊天牌阵抽牌结果。只保存 cardId，恢复时从 TarotDeck 查完整牌面数据。
/// </summary>
[System.Serializable]
public class TarotDrawData
{
    public string cardId;
    public bool upright;
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
    private string activeFriendContext = "";

    /// <summary>当前占卜 Reading ID</summary>
    private string activeReadingId;

    /// <summary>当前占卜状态（供 DivinationEngine 同步）</summary>
    private string activeReadingState = "";

    /// <summary>当前动作类型（供 SceneRouter 路由）</summary>
    private string activeActionKind = "";

    /// <summary>最近一次 OracleRuntime 组装结果（用于输出守卫和调试）</summary>
    private AssemblyResult lastAssemblyResult;

    /// <summary>最近的 OracleRuntime prompt 调试记录</summary>
    private List<PromptRecord> promptRecords = new List<PromptRecord>();

    private const int MAX_PROMPT_RECORDS = 50;

    /// <summary>今日牌数据（供 ChatPayload 使用）</summary>
    private TodayCardPayload todayCardPayload;

    /// <summary>临时覆盖的选项列表（一次性消耗，例如牌阵选择）</summary>
    private List<string> _overriddenOptions;

    /// <summary>最近 N 轮用户发言（供 ChatPayload 使用）</summary>
    private List<string> recentUserMessages = new List<string>();

    /// <summary>最近 N 轮 AI 回复（供 ChatPayload 使用）</summary>
    private List<string> recentAssistantReplies = new List<string>();

    private const int MAX_RECENT_MESSAGES = 6;
    private const int MAX_ACTIVE_FRIEND_CONTEXT_CHARS = 5000;
    private const int MAX_FRIEND_RELATED_HISTORY = 8;
    private const int MAX_FRIEND_MEMORY_LINES = 8;
    private const int MAX_FRIEND_READING_LINES = 4;

    // 消息列表
    private List<ChatMessageData> mChatMessageList = new List<ChatMessageData>();

    // DeepSeek API 消息历史
    private List<DeepSeekAPI.Message> mApiMessageHistory = new List<DeepSeekAPI.Message>();

    // 消息ID计数器
    private int mMessageIdCounter = 0;
    private bool _cloudHistoryLoaded;
    private bool _isRestoringCloudHistory;
    private Coroutine _cloudSaveCoroutine;

    private const int MAX_SAVED_CHAT_MESSAGES = 80;
    private const int MAX_SAVED_API_MESSAGES = 120;
    private const float CLOUD_SAVE_DEBOUNCE_SECONDS = 1.2f;

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            FlushCloudDialogHistory();
    }

    private void OnApplicationQuit()
    {
        FlushCloudDialogHistory();
    }

    protected override void Awake()
    {
        base.Awake();
        if (deepSeekAPI == null)
        {
            deepSeekAPI = DeepSeekAPI.ResolveFor(gameObject);
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
        UsageStatsManager.Instance?.TrackDialogMessage();

        // 同时添加到 API 历史
        mApiMessageHistory.Add(new DeepSeekAPI.Message("user", content));

        // 追踪最近用户消息
        recentUserMessages.Add(content);
        if (recentUserMessages.Count > MAX_RECENT_MESSAGES)
            recentUserMessages.RemoveAt(0);

        SaveCloudDialogHistory();
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
        ApplyOracleRuntimeMetadata(data, lastAssemblyResult?.promptRecord);
        mChatMessageList.Add(data);

        // 同时添加到 API 历史
        mApiMessageHistory.Add(new DeepSeekAPI.Message("assistant", content));

        // 追踪最近 AI 回复
        recentAssistantReplies.Add(content);
        if (recentAssistantReplies.Count > MAX_RECENT_MESSAGES)
            recentAssistantReplies.RemoveAt(0);

        SaveCloudDialogHistory();
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

        SaveCloudDialogHistory();
        return data;
    }
    public ChatMessageData AddAtFriendMessage(string content)
    {
        return AddAtFriendMessage(content, null);
    }

    public ChatMessageData AddAtFriendMessage(string content, FriendDataManager.FriendData friend)
    {
        string friendContext = friend != null ? friend.BuildOracleContext() : "";
        string relationshipId = BuildFriendRelationshipId(friend);
        string enrichedFriendContext = friend != null
            ? BuildEnrichedFriendContext(friend, friendContext, relationshipId)
            : "";
        string messageContent = string.IsNullOrWhiteSpace(content) && friend != null
            ? $"@{friend.name}\n{friendContext}"
            : content;

         ChatMessageData data = new ChatMessageData
        {
            id = mMessageIdCounter++,
            roleType = DialogRoleType.AI,

            messageType = MsgType.AtFriend,
            content = messageContent,
            friendName = friend != null ? friend.name : "",
            friendContext = friendContext,

            divinerType = CurrentDivinerType
        };
        mChatMessageList.Add(data);

        if (friend != null)
        {
            activeRelationshipId = relationshipId;
            activeFriendContext = enrichedFriendContext;
        }

        // 同时添加到 API 历史，让塔罗师知道当前 @ 的好友档案。
        if (!string.IsNullOrWhiteSpace(messageContent))
        {
            mApiMessageHistory.Add(new DeepSeekAPI.Message("user", messageContent));
        }

        SaveCloudDialogHistory();
        return data;
    }

    public void ActivateFriendContext(FriendDataManager.FriendData friend)
    {
        ActivateFriendContext(friend, "");
    }

    public void ActivateFriendContext(FriendDataManager.FriendData friend, string extraContext)
    {
        if (friend == null)
        {
            ClearActiveFriendContext();
            return;
        }

        string friendContext = friend.BuildOracleContext();
        string relationshipId = BuildFriendRelationshipId(friend);
        activeRelationshipId = relationshipId;
        activeFriendContext = BuildEnrichedFriendContext(friend, friendContext, relationshipId, extraContext);
        SaveCloudDialogHistory();
    }

    public void ClearActiveFriendContext()
    {
        activeRelationshipId = "";
        activeFriendContext = "";
        SaveCloudDialogHistory();
    }

    private string BuildFriendRelationshipId(FriendDataManager.FriendData friend)
    {
        if (friend == null) return "";
        if (!string.IsNullOrWhiteSpace(friend.virtualFriendId))
            return $"virtual:{friend.virtualFriendId.Trim()}";
        if (!string.IsNullOrWhiteSpace(friend.firebaseUid))
            return $"firebase:{friend.firebaseUid.Trim()}";
        if (!string.IsNullOrWhiteSpace(friend.handle))
            return $"handle:{friend.handle.Trim().ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(friend.name))
            return $"name:{friend.name.Trim()}";
        return $"local:{friend.id}";
    }

    private string BuildEnrichedFriendContext(
        FriendDataManager.FriendData friend,
        string baseFriendContext,
        string relationshipId,
        string extraContext = "")
    {
        var lines = new List<string>
        {
            "【当前@好友上下文】",
            "使用方式：以下信息只作为推理依据，不要逐字复述；结合用户问题、关系状态、出生信息、历史对话和长期记忆，给出具体、温和、可执行的回复。",
            "",
            "【好友基础资料】",
            string.IsNullOrWhiteSpace(baseFriendContext) ? "暂无好友资料。" : baseFriendContext.Trim()
        };

        if (!string.IsNullOrWhiteSpace(extraContext))
        {
            AppendFriendContextSection(lines, "当前好友动态/每日牌摘要",
                new List<string> { extraContext.Trim() });
        }

        AppendFriendContextSection(lines, "相关长期关系记忆",
            BuildRelationshipMemoryLines(friend, relationshipId, MAX_FRIEND_MEMORY_LINES));
        AppendFriendContextSection(lines, "相关候选记忆",
            BuildMemoryCandidateLines(friend, relationshipId, MAX_FRIEND_MEMORY_LINES));
        AppendFriendContextSection(lines, "相关历史对话",
            BuildRelatedDialogHistoryLines(friend, relationshipId, MAX_FRIEND_RELATED_HISTORY));
        AppendFriendContextSection(lines, "相关占卜连续性",
            BuildReadingContinuityLines(friend, relationshipId, MAX_FRIEND_READING_LINES));

        return ClipForContext(string.Join("\n", lines), MAX_ACTIVE_FRIEND_CONTEXT_CHARS);
    }

    private List<string> BuildRelationshipMemoryLines(
        FriendDataManager.FriendData friend,
        string relationshipId,
        int limit)
    {
        var result = new List<string>();
        if (memorySource?.relationships == null) return result;

        foreach (var memory in memorySource.relationships)
        {
            if (memory == null || !RelationshipMemoryMatchesFriend(memory, friend, relationshipId))
                continue;

            if (!string.IsNullOrWhiteSpace(memory.displayName))
                result.Add($"关系对象：{memory.displayName}");
            if (!string.IsNullOrWhiteSpace(memory.entityType))
                result.Add($"对象类型：{memory.entityType}");
            if (!string.IsNullOrWhiteSpace(memory.consentMode))
                result.Add($"同意模式：{memory.consentMode}");
            if (memory.knownFacts != null)
            {
                foreach (var fact in memory.knownFacts)
                    if (!string.IsNullOrWhiteSpace(fact)) result.Add($"已知事实：{fact}");
            }
            if (memory.openLoops != null)
            {
                foreach (var loop in memory.openLoops)
                    if (!string.IsNullOrWhiteSpace(loop)) result.Add($"待继续观察：{loop}");
            }
            if (!string.IsNullOrWhiteSpace(memory.lastActionAdvice))
                result.Add($"上次行动建议：{memory.lastActionAdvice}");

            if (result.Count >= limit) break;
        }

        return result.Take(limit).ToList();
    }

    private List<string> BuildMemoryCandidateLines(
        FriendDataManager.FriendData friend,
        string relationshipId,
        int limit)
    {
        if (memorySource?.candidates == null) return new List<string>();

        return memorySource.candidates
            .Where(candidate => candidate != null
                && !string.IsNullOrWhiteSpace(candidate.text)
                && !string.Equals(candidate.status, "dismissed", StringComparison.OrdinalIgnoreCase)
                && MemoryCandidateMatchesFriend(candidate, friend, relationshipId))
            .OrderByDescending(candidate => candidate.confidence)
            .Take(limit)
            .Select(candidate =>
            {
                string status = string.IsNullOrWhiteSpace(candidate.status) ? "pending" : candidate.status;
                string type = string.IsNullOrWhiteSpace(candidate.type) ? "memory" : candidate.type;
                return $"[{status}/{type}] {candidate.text}";
            })
            .ToList();
    }

    private List<string> BuildRelatedDialogHistoryLines(
        FriendDataManager.FriendData friend,
        string relationshipId,
        int limit)
    {
        var result = new List<string>();
        if (friend == null || mChatMessageList == null || mChatMessageList.Count == 0)
            return result;

        int lastAtIndex = -1;
        for (int i = mChatMessageList.Count - 1; i >= 0; i--)
        {
            var message = mChatMessageList[i];
            if (message?.messageType == MsgType.AtFriend && AtFriendMessageMatches(message, friend, relationshipId))
            {
                lastAtIndex = i;
                break;
            }
        }

        if (lastAtIndex >= 0)
        {
            for (int i = lastAtIndex + 1; i < mChatMessageList.Count && result.Count < limit; i++)
            {
                var line = FormatDialogHistoryLine(mChatMessageList[i]);
                if (!string.IsNullOrWhiteSpace(line))
                    result.Add(line);
            }
        }

        for (int i = mChatMessageList.Count - 1; i >= 0 && result.Count < limit; i--)
        {
            if (i > lastAtIndex && lastAtIndex >= 0) continue;
            var message = mChatMessageList[i];
            string text = message?.content ?? "";
            if (!ContainsFriendSignal(text, friend) && !AtFriendMessageMatches(message, friend, relationshipId))
                continue;

            var line = FormatDialogHistoryLine(message);
            if (!string.IsNullOrWhiteSpace(line) && !result.Contains(line))
                result.Add(line);
        }

        return result.Take(limit).ToList();
    }

    private List<string> BuildReadingContinuityLines(
        FriendDataManager.FriendData friend,
        string relationshipId,
        int limit)
    {
        if (memorySource?.readingContinuity == null) return new List<string>();

        return memorySource.readingContinuity
            .Where(reading => reading != null && ReadingContinuityMatchesFriend(reading, friend, relationshipId))
            .OrderByDescending(reading => reading.createdAt)
            .Take(limit)
            .Select(reading =>
            {
                string title = !string.IsNullOrWhiteSpace(reading.shortVerdict)
                    ? reading.shortVerdict
                    : reading.question;
                var cards = reading.cards != null
                    ? string.Join("，", reading.cards
                        .Where(card => card != null)
                        .Select(card => $"{card.positionName ?? card.position}:{card.cardName ?? card.cardId}{(string.IsNullOrEmpty(card.orientation) ? "" : $"({card.orientation})")}"))
                    : "";

                return $"{reading.createdAt} {title}{(string.IsNullOrWhiteSpace(cards) ? "" : $" [{cards}]")}";
            })
            .ToList();
    }

    private bool RelationshipMemoryMatchesFriend(
        RelationshipMemory memory,
        FriendDataManager.FriendData friend,
        string relationshipId)
    {
        if (memory == null) return false;
        if (IsSameRelationshipKey(memory.relationshipId, friend, relationshipId)) return true;
        if (ContainsFriendSignal(memory.displayName, friend)) return true;
        if (memory.knownFacts != null && memory.knownFacts.Any(fact => ContainsFriendSignal(fact, friend))) return true;
        if (memory.openLoops != null && memory.openLoops.Any(loop => ContainsFriendSignal(loop, friend))) return true;
        return ContainsFriendSignal(memory.lastActionAdvice, friend);
    }

    private bool MemoryCandidateMatchesFriend(
        MemoryCandidate candidate,
        FriendDataManager.FriendData friend,
        string relationshipId)
    {
        if (candidate == null) return false;
        if (IsSameRelationshipKey(candidate.relationshipId, friend, relationshipId)) return true;
        return ContainsFriendSignal(candidate.text, friend);
    }

    private bool ReadingContinuityMatchesFriend(
        ReadingContinuityEntry reading,
        FriendDataManager.FriendData friend,
        string relationshipId)
    {
        if (reading == null) return false;
        if (IsSameRelationshipKey(reading.relationshipId, friend, relationshipId)) return true;
        return ContainsFriendSignal(reading.question, friend)
            || ContainsFriendSignal(reading.shortVerdict, friend);
    }

    private bool AtFriendMessageMatches(
        ChatMessageData message,
        FriendDataManager.FriendData friend,
        string relationshipId)
    {
        if (message == null || friend == null) return false;
        if (ContainsFriendSignal(message.friendName, friend)) return true;
        if (ContainsFriendSignal(message.friendContext, friend)) return true;
        return ContainsFriendSignal(message.content, friend);
    }

    private bool IsSameRelationshipKey(
        string value,
        FriendDataManager.FriendData friend,
        string relationshipId)
    {
        if (string.IsNullOrWhiteSpace(value) || friend == null) return false;

        string normalized = NormalizeContextKey(value);
        foreach (var key in BuildFriendRelationshipKeys(friend, relationshipId))
        {
            if (NormalizeContextKey(key) == normalized)
                return true;
        }

        return false;
    }

    private IEnumerable<string> BuildFriendRelationshipKeys(
        FriendDataManager.FriendData friend,
        string relationshipId)
    {
        if (!string.IsNullOrWhiteSpace(relationshipId)) yield return relationshipId;
        if (friend == null) yield break;

        yield return friend.id.ToString();
        yield return $"local:{friend.id}";

        if (!string.IsNullOrWhiteSpace(friend.virtualFriendId))
        {
            yield return friend.virtualFriendId;
            yield return $"virtual:{friend.virtualFriendId}";
        }

        if (!string.IsNullOrWhiteSpace(friend.firebaseUid))
        {
            yield return friend.firebaseUid;
            yield return $"firebase:{friend.firebaseUid}";
        }

        if (!string.IsNullOrWhiteSpace(friend.handle))
        {
            yield return friend.handle;
            yield return $"handle:{friend.handle.ToLowerInvariant()}";
        }
    }

    private bool ContainsFriendSignal(string text, FriendDataManager.FriendData friend)
    {
        if (string.IsNullOrWhiteSpace(text) || friend == null) return false;

        return ContainsText(text, friend.name)
            || ContainsText(text, friend.handle)
            || ContainsText(text, friend.firebaseUid)
            || ContainsText(text, friend.virtualFriendId);
    }

    private static bool ContainsText(string text, string value)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(value))
            return false;
        return text.IndexOf(value.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string NormalizeContextKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }

    private static string FormatDialogHistoryLine(ChatMessageData message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.content))
            return "";

        string role = message.roleType == DialogRoleType.User ? "用户" : "AI";
        string content = message.messageType == MsgType.AtFriend && !string.IsNullOrWhiteSpace(message.friendName)
            ? $"@{message.friendName}"
            : message.content;

        content = content.Replace("\r", " ").Replace("\n", " ").Trim();
        return $"{role}：{ClipForContext(content, 220)}";
    }

    private static void AppendFriendContextSection(
        List<string> lines,
        string title,
        List<string> items)
    {
        if (lines == null || items == null || items.Count == 0)
            return;

        lines.Add("");
        lines.Add($"【{title}】");
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item))
                lines.Add($"- {ClipForContext(item.Trim(), 260)}");
        }
    }

    private static string ClipForContext(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        string trimmed = value.Trim();
        if (maxLength <= 0 || trimmed.Length <= maxLength) return trimmed;
        return trimmed.Substring(0, Mathf.Max(0, maxLength - 1)).TrimEnd() + "…";
    }

    /// <summary>
    /// 添加三排牌阵互动卡片消息（AI 主动触发）
    /// </summary>
    /// <param name="content">AI 的引导文案</param>
    /// <param name="spreadKind">牌阵类型，如 "self_repair"、"relationship_tension"</param>
    public ChatMessageData AddInteractionCard3Message(string content, string spreadKind)
    {
        ChatMessageData data = new ChatMessageData
        {
            id = mMessageIdCounter++,
            roleType = DialogRoleType.AI,
            messageType = MsgType.InteractionCard3,
            content = content,
            spreadKind = spreadKind ?? "self_repair",
            options = GetTarotOptions(),
            divinerType = CurrentDivinerType
        };
        CaptureDivinationSnapshot(data);
        mChatMessageList.Add(data);

        // 同时添加到 API 历史
        mApiMessageHistory.Add(new DeepSeekAPI.Message("assistant", content));

        Debug.Log($"[DialogSystem] 添加 InteractionCard3 消息, spreadKind={spreadKind}");
        SaveCloudDialogHistory();
        return data;
    }

    /// <summary>
    /// 添加单排牌阵互动卡片消息（AI 主动触发）
    /// </summary>
    /// <param name="content">AI 的引导文案</param>
    /// <param name="spreadKind">牌阵类型，如 "single_mirror"</param>
    public ChatMessageData AddInteractionCard1Message(string content, string spreadKind)
    {
        ChatMessageData data = new ChatMessageData
        {
            id = mMessageIdCounter++,
            roleType = DialogRoleType.AI,
            messageType = MsgType.InteractionCard1,
            content = content,
            spreadKind = spreadKind ?? "single_mirror",
            options = GetTarotOptions(),
            divinerType = CurrentDivinerType
        };
        CaptureDivinationSnapshot(data);
        mChatMessageList.Add(data);

        // 同时添加到 API 历史
        mApiMessageHistory.Add(new DeepSeekAPI.Message("assistant", content));

        Debug.Log($"[DialogSystem] 添加 InteractionCard1 消息, spreadKind={spreadKind}");
        SaveCloudDialogHistory();
        return data;
    }

    /// <summary>
    /// 添加五排牌阵互动卡片消息（AI 主动触发）
    /// </summary>
    /// <param name="content">AI 的引导文案</param>
    /// <param name="spreadKind">牌阵类型，如 "five_choice_gate"</param>
    public ChatMessageData AddInteractionCard5Message(string content, string spreadKind)
    {
        ChatMessageData data = new ChatMessageData
        {
            id = mMessageIdCounter++,
            roleType = DialogRoleType.AI,
            messageType = MsgType.InteractionCard5,
            content = content,
            spreadKind = spreadKind ?? "five_choice_gate",
            options = GetTarotOptions(),
            divinerType = CurrentDivinerType
        };
        CaptureDivinationSnapshot(data);
        mChatMessageList.Add(data);

        // 同时添加到 API 历史
        mApiMessageHistory.Add(new DeepSeekAPI.Message("assistant", content));

        Debug.Log($"[DialogSystem] 添加 InteractionCard5 消息, spreadKind={spreadKind}");
        SaveCloudDialogHistory();
        return data;
    }

    public void CaptureDivinationSnapshot(ChatMessageData data)
    {
        if (data == null) return;

        var session = DivinationEngine.Instance?.CurrentSession;
        if (session == null)
        {
            if (string.IsNullOrEmpty(data.readingId))
                data.readingId = $"chat_{data.id}";
            if (string.IsNullOrEmpty(data.divinationCreatedAt))
                data.divinationCreatedAt = DateTime.Now.ToString("o");
            return;
        }

        data.readingId = FirstNonEmpty(data.readingId, session.readingId, $"chat_{data.id}");
        data.divinationQuestion = FirstNonEmpty(data.divinationQuestion, session.question);
        data.divinationScene = FirstNonEmpty(data.divinationScene, session.scene);
        data.spreadKind = FirstNonEmpty(data.spreadKind, session.spreadKind);
        data.shortVerdict = FirstNonEmpty(data.shortVerdict, session.shortVerdict);
        data.judgeContent = FirstNonEmpty(data.judgeContent, session.judgeContent);
        data.adviceContent = FirstNonEmpty(data.adviceContent, session.adviceContent);
        data.divinationCreatedAt = FirstNonEmpty(data.divinationCreatedAt, session.createdAt, DateTime.Now.ToString("o"));

        if ((data.followupTopics == null || data.followupTopics.Count == 0) && session.topics != null)
            data.followupTopics = new List<string>(session.topics);
    }

    public void RecordSpreadDrawResult(ChatMessageData data)
    {
        if (data == null || data.spreadDrawResultAddedToHistory) return;
        if (data.spreadDrawnCards == null || data.spreadDrawnCards.Count == 0) return;

        string summary = BuildSpreadDrawHistorySummary(data);
        if (string.IsNullOrWhiteSpace(summary)) return;

        mApiMessageHistory.Add(new DeepSeekAPI.Message("assistant", summary));
        recentAssistantReplies.Add(summary);
        if (recentAssistantReplies.Count > MAX_RECENT_MESSAGES)
            recentAssistantReplies.RemoveAt(0);

        data.spreadDrawResultAddedToHistory = true;
        SaveCloudDialogHistory();
    }

    public void ApplyDivinationReplyToActiveSpread(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply)) return;

        ChatMessageData target = null;
        for (int i = mChatMessageList.Count - 1; i >= 0; i--)
        {
            var message = mChatMessageList[i];
            if (message == null) continue;
            if (!IsSpreadMessage(message)) continue;

            if (!string.IsNullOrEmpty(activeReadingId) && message.readingId == activeReadingId)
            {
                target = message;
                break;
            }

            if (target == null && message.spreadCardsDrawn)
                target = message;
        }

        if (target == null) return;

        var parsed = ParseDivinationReply(reply);
        target.shortVerdict = FirstNonEmpty(parsed.shortVerdict, target.shortVerdict, reply);
        target.judgeContent = FirstNonEmpty(parsed.judgeContent, reply);
        target.adviceContent = FirstNonEmpty(parsed.adviceContent, target.adviceContent);
        if (parsed.topics.Count > 0)
            target.followupTopics = parsed.topics;

        var session = DivinationEngine.Instance?.CurrentSession;
        if (session != null && session.readingId == target.readingId)
        {
            session.shortVerdict = target.shortVerdict;
            session.judgeContent = target.judgeContent;
            session.adviceContent = target.adviceContent;
            session.topics = target.followupTopics;
        }

        CaptureDivinationSnapshot(target);
        SaveCloudDialogHistory();
        DivinationRecordFirestore.Instance?.SaveRecord(DivinationRecordBuilder.FromChatMessage(target));
    }

    public void ActivateReadingFromRecord(DivinationRecordData record, DivinationPhase phase = DivinationPhase.FollowUp)
    {
        if (record?.lockedCards == null || record.lockedCards.Count == 0) return;

        var restoredLock = new ReadingLock
        {
            readingId = record.readingId,
            readingType = record.spreadKind,
            allowedCards = record.lockedCards,
            locked = true
        };

        readingLock = restoredLock;
        activeReadingId = record.readingId;
        activeReadingState = phase == DivinationPhase.Completed ? "completed" :
            phase == DivinationPhase.CardsLocked ? "cards_locked" :
            "completed";
        activeActionKind = phase == DivinationPhase.CardsLocked ? "complete_verdict" : "dive_deeper";

        var session = new DivinationSession
        {
            readingId = record.readingId,
            question = record.question,
            scene = record.scene,
            spreadKind = record.spreadKind,
            lockedCards = record.lockedCards,
            readingLock = restoredLock,
            shortVerdict = record.shortVerdict,
            judgeContent = record.judgeContent,
            adviceContent = record.adviceContent,
            topics = record.topics,
            createdAt = string.IsNullOrEmpty(record.createdAt) ? DateTime.Now.ToString("o") : record.createdAt,
            phase = phase
        };

        DivinationEngine.Instance?.RestoreSession(session);
        SaveCloudDialogHistory();
    }

    private bool IsSpreadMessage(ChatMessageData message)
    {
        return message.messageType == MsgType.InteractionCard1
            || message.messageType == MsgType.InteractionCard3
            || message.messageType == MsgType.InteractionCard5;
    }

    private ParsedDivinationReply ParseDivinationReply(string reply)
    {
        if (TryParseStructuredDivinationReply(reply, out var structured))
            return structured;

        var parsed = new ParsedDivinationReply();
        if (string.IsNullOrWhiteSpace(reply)) return parsed;

        string currentSection = "judge";
        var judgeLines = new List<string>();
        var adviceLines = new List<string>();

        var lines = reply.Replace("\r", "\n")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        foreach (var rawLine in lines)
        {
            string line = CleanSectionPrefix(rawLine);
            string normalized = line.Replace("：", "").Replace(":", "").Trim();

            if (IsJudgeSection(normalized))
            {
                currentSection = "judge";
                continue;
            }
            if (IsAdviceSection(normalized))
            {
                currentSection = "advice";
                continue;
            }
            if (IsTopicSection(normalized))
            {
                currentSection = "topics";
                continue;
            }

            if (currentSection == "topics")
            {
                string topic = CleanTopicText(line);
                if (!string.IsNullOrEmpty(topic) && parsed.topics.Count < 4)
                    parsed.topics.Add(topic);
                continue;
            }

            if (currentSection == "advice")
                adviceLines.Add(line);
            else
                judgeLines.Add(line);
        }

        parsed.judgeContent = string.Join("\n", judgeLines).Trim();
        parsed.adviceContent = string.Join("\n", adviceLines).Trim();
        parsed.shortVerdict = BuildShortVerdict(parsed.judgeContent, reply);
        return parsed;
    }

    private bool TryParseStructuredDivinationReply(string reply, out ParsedDivinationReply parsed)
    {
        parsed = null;
        string json = ExtractJsonObject(reply);
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            var dto = JsonUtility.FromJson<StructuredDivinationReply>(json);
            if (dto == null) return false;

            parsed = new ParsedDivinationReply
            {
                displayReply = dto.displayReply ?? "",
                shortVerdict = dto.shortVerdict ?? "",
                judgeContent = dto.judgeContent ?? "",
                adviceContent = dto.adviceContent ?? "",
                topics = NormalizeTopics(dto.topics)
            };

            return !string.IsNullOrWhiteSpace(parsed.displayReply)
                || !string.IsNullOrWhiteSpace(parsed.shortVerdict)
                || !string.IsNullOrWhiteSpace(parsed.judgeContent)
                || !string.IsNullOrWhiteSpace(parsed.adviceContent)
                || parsed.topics.Count > 0;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DialogSystem] 结构化占卜 JSON 解析失败: {ex.Message}");
            return false;
        }
    }

    private string ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        string value = text.Trim();
        if (value.StartsWith("```"))
        {
            value = value.Replace("```json", "")
                .Replace("```JSON", "")
                .Replace("```", "")
                .Trim();
        }

        int start = value.IndexOf('{');
        int end = value.LastIndexOf('}');
        if (start < 0 || end <= start) return "";
        return value.Substring(start, end - start + 1);
    }

    private List<string> NormalizeTopics(List<string> rawTopics)
    {
        var topics = new List<string>();
        if (rawTopics == null) return topics;

        foreach (var rawTopic in rawTopics)
        {
            string topic = (rawTopic ?? "").Trim();
            if (string.IsNullOrEmpty(topic)) continue;
            if (!topic.EndsWith("?") && !topic.EndsWith("？"))
                topic += "？";
            topics.Add(topic);
            if (topics.Count >= 4) break;
        }

        return topics;
    }

    private bool IsJudgeSection(string line)
    {
        return line == "综合判断"
            || line == "完整解读"
            || line == "牌意解析"
            || line == "解读"
            || line == "判断";
    }

    private bool IsAdviceSection(string line)
    {
        return line == "行动建议"
            || line == "建议"
            || line == "今天可以做的一件小事"
            || line == "下一步";
    }

    private bool IsTopicSection(string line)
    {
        return line == "继续追问"
            || line == "适合继续聊的话题"
            || line == "推荐追问"
            || line == "追问话题";
    }

    private string CleanSectionPrefix(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return "";
        return line.Trim()
            .TrimStart('#', ' ', '◆', '◇', '✦', '•', '-', '*')
            .Trim();
    }

    private string CleanTopicText(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return "";
        string value = line.Trim()
            .TrimStart('Q', 'q', '：', ':', '>', ' ', '◆', '◇', '✦', '•', '-', '*');

        int dotIndex = value.IndexOf('.');
        if (dotIndex >= 0 && dotIndex < 3)
            value = value.Substring(dotIndex + 1);

        value = value.Trim();
        return value.EndsWith("?") || value.EndsWith("？") ? value : "";
    }

    private string BuildShortVerdict(string judgeContent, string fallback)
    {
        string source = FirstNonEmpty(judgeContent, fallback);
        if (string.IsNullOrWhiteSpace(source)) return "";

        var parts = System.Text.RegularExpressions.Regex.Split(source.Trim(), @"(?<=[。！？.!?])")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Take(2)
            .ToList();
        return parts.Count > 0 ? string.Join("", parts).Trim() : source.Trim();
    }

    private class ParsedDivinationReply
    {
        public string displayReply = "";
        public string shortVerdict = "";
        public string judgeContent = "";
        public string adviceContent = "";
        public List<string> topics = new List<string>();
    }

    [Serializable]
    private class StructuredDivinationReply
    {
        public string displayReply;
        public string shortVerdict;
        public string judgeContent;
        public string adviceContent;
        public List<string> topics;
    }

    private string BuildSpreadDrawHistorySummary(ChatMessageData data)
    {
        var spreadDef = DivinationEngine.Instance?.GetSpreadDefinition(data.spreadKind);
        var parts = new List<string>();
        for (int i = 0; i < data.spreadDrawnCards.Count; i++)
        {
            var draw = data.spreadDrawnCards[i];
            var card = TarotDeck.GetById(draw.cardId);
            if (card == null) continue;

            string position = spreadDef?.positions != null && i < spreadDef.positions.Count
                ? spreadDef.positions[i].label
                : $"第{i + 1}张";
            string orientation = draw.upright ? "正位" : "逆位";
            parts.Add($"{position}：{card.nameZh}（{orientation}）");
        }

        if (parts.Count == 0) return "";
        string spreadName = string.IsNullOrEmpty(spreadDef?.label) ? data.spreadKind : spreadDef.label;
        return $"[已锁定牌阵] readingId={data.readingId}，牌阵={spreadName}，抽牌结果：{string.Join("；", parts)}。";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null) return "";
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return "";
    }
    /// <summary>
    /// 获取所有消息
    /// </summary>
    public List<ChatMessageData> GetAllMessages()
    {
        return mChatMessageList;
    }

    public void LoadCloudDialogHistoryOnce(Action<bool> onComplete = null)
    {
        if (_cloudHistoryLoaded)
        {
            onComplete?.Invoke(false);
            return;
        }

        var store = DialogHistoryFirestore.Instance;
        if (store == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        store.LoadDefault(snapshot =>
        {
            if (snapshot == null || snapshot.messages == null || snapshot.messages.Count == 0)
            {
                onComplete?.Invoke(false);
                return;
            }

            if (mChatMessageList.Count > 0)
            {
                _cloudHistoryLoaded = true;
                onComplete?.Invoke(false);
                return;
            }

            RestoreDialogHistory(snapshot);
            _cloudHistoryLoaded = true;
            onComplete?.Invoke(true);
        });
    }

    public void SaveCloudDialogHistory(Action<bool> onComplete = null)
    {
        if (_isRestoringCloudHistory)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (onComplete != null)
        {
            SaveCloudDialogHistoryNow(onComplete);
            return;
        }

        if (_cloudSaveCoroutine != null)
            StopCoroutine(_cloudSaveCoroutine);
        _cloudSaveCoroutine = StartCoroutine(SaveCloudDialogHistoryDebounced());
    }

    public void FlushCloudDialogHistory(Action<bool> onComplete = null)
    {
        if (_cloudSaveCoroutine != null)
        {
            StopCoroutine(_cloudSaveCoroutine);
            _cloudSaveCoroutine = null;
        }

        SaveCloudDialogHistoryNow(onComplete);
    }

    private IEnumerator SaveCloudDialogHistoryDebounced()
    {
        yield return new WaitForSeconds(CLOUD_SAVE_DEBOUNCE_SECONDS);
        _cloudSaveCoroutine = null;
        SaveCloudDialogHistoryNow();
    }

    private void SaveCloudDialogHistoryNow(Action<bool> onComplete = null)
    {
        var store = DialogHistoryFirestore.Instance;
        if (store == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        store.SaveDefault(BuildDialogHistorySnapshot(), onComplete);
    }

    private DialogHistorySnapshot BuildDialogHistorySnapshot()
    {
        return new DialogHistorySnapshot
        {
            messages = TakeRecent(mChatMessageList, MAX_SAVED_CHAT_MESSAGES),
            apiMessages = TakeRecent(mApiMessageHistory, MAX_SAVED_API_MESSAGES),
            activeReadingId = activeReadingId ?? "",
            activeReadingState = activeReadingState ?? "",
            activeActionKind = activeActionKind ?? "",
            activeRelationshipId = activeRelationshipId ?? "",
            activeFriendContext = activeFriendContext ?? ""
        };
    }

    private void RestoreDialogHistory(DialogHistorySnapshot snapshot)
    {
        _isRestoringCloudHistory = true;

        mChatMessageList = snapshot.messages ?? new List<ChatMessageData>();
        mApiMessageHistory = snapshot.apiMessages ?? new List<DeepSeekAPI.Message>();
        activeReadingId = snapshot.activeReadingId ?? "";
        activeReadingState = snapshot.activeReadingState ?? "";
        activeActionKind = snapshot.activeActionKind ?? "";
        activeRelationshipId = snapshot.activeRelationshipId ?? "";
        activeFriendContext = snapshot.activeFriendContext ?? "";
        mStreamingMessageIndex = -1;

        mMessageIdCounter = 0;
        foreach (var message in mChatMessageList)
        {
            if (message != null)
                mMessageIdCounter = Math.Max(mMessageIdCounter, message.id + 1);
        }

        RebuildRecentMessageCaches();
        RestoreRuntimeReadingStateFromHistory();
        _isRestoringCloudHistory = false;
    }

    private void RestoreRuntimeReadingStateFromHistory()
    {
        var spreadMessage = FindLatestDrawnSpreadMessage();
        if (spreadMessage == null) return;

        var record = DivinationRecordBuilder.FromChatMessage(spreadMessage);
        if (record?.lockedCards == null || record.lockedCards.Count == 0) return;

        bool hasVerdict = !string.IsNullOrWhiteSpace(record.judgeContent)
            || !string.IsNullOrWhiteSpace(record.shortVerdict);

        var restoredLock = new ReadingLock
        {
            readingId = record.readingId,
            readingType = record.spreadKind,
            allowedCards = record.lockedCards,
            locked = true
        };

        readingLock = restoredLock;
        activeReadingId = record.readingId;
        activeReadingState = hasVerdict ? "completed" : "cards_locked";
        activeActionKind = hasVerdict ? "dive_deeper" : "complete_verdict";

        var session = new DivinationSession
        {
            readingId = record.readingId,
            question = record.question,
            scene = record.scene,
            spreadKind = record.spreadKind,
            lockedCards = record.lockedCards,
            readingLock = restoredLock,
            shortVerdict = record.shortVerdict,
            judgeContent = record.judgeContent,
            adviceContent = record.adviceContent,
            topics = record.topics,
            createdAt = record.createdAt,
            phase = hasVerdict ? DivinationPhase.Completed : DivinationPhase.CardsLocked
        };

        DivinationEngine.Instance?.RestoreSession(session);
    }

    private ChatMessageData FindLatestDrawnSpreadMessage()
    {
        for (int i = mChatMessageList.Count - 1; i >= 0; i--)
        {
            var message = mChatMessageList[i];
            if (message == null) continue;
            if (!IsSpreadMessage(message)) continue;
            if (!message.spreadCardsDrawn) continue;
            if (message.spreadDrawnCards == null || message.spreadDrawnCards.Count == 0) continue;
            return message;
        }

        return null;
    }

    private void RebuildRecentMessageCaches()
    {
        recentUserMessages.Clear();
        recentAssistantReplies.Clear();

        foreach (var message in mChatMessageList)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.content)) continue;

            if (message.roleType == DialogRoleType.User)
            {
                recentUserMessages.Add(message.content);
                if (recentUserMessages.Count > MAX_RECENT_MESSAGES)
                    recentUserMessages.RemoveAt(0);
            }
            else if (message.roleType == DialogRoleType.AI)
            {
                recentAssistantReplies.Add(message.content);
                if (recentAssistantReplies.Count > MAX_RECENT_MESSAGES)
                    recentAssistantReplies.RemoveAt(0);
            }
        }
    }

    private List<T> TakeRecent<T>(List<T> source, int maxCount)
    {
        if (source == null) return new List<T>();
        if (source.Count <= maxCount) return new List<T>(source);
        return source.GetRange(source.Count - maxCount, maxCount);
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
        if (!string.IsNullOrWhiteSpace(activeFriendContext))
        {
            messages.Add(new DeepSeekAPI.Message("system",
                "当前对话已 @ 好友。下面是仅供推理使用的好友上下文，请结合用户的问题自然回复，不要逐字复述这些资料。\n"
                + activeFriendContext));
        }
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
            rationaleQuestion = IsRationaleQuestion(userMessage),
            friendContext = activeFriendContext,
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
        lastAssemblyResult = assemblyResult;

        var messages = new List<DeepSeekAPI.Message>();

        if (assemblyResult?.messages != null)
        {
            foreach (var cm in assemblyResult.messages)
            {
                messages.Add(new DeepSeekAPI.Message(cm.role, cm.content));
            }
            AppendStructuredDivinationOutputContract(messages);
        }
        else
        {
            // 降级：如果组装失败，使用旧的简单提示词
            Debug.LogWarning("[OracleRuntime] ContextAssembler returned null, falling back to simple prompt.");
            return GetApiMessagesWithSystemPrompt();
        }

        Debug.Log($"[OracleRuntime] Assembled {messages.Count} messages for API request. "
            + $"Scene={assemblyResult.promptRecord?.scene}, Stage={assemblyResult.promptRecord?.stage}, Voice={GetOracleVoiceId()}");

        return messages;
    }

    private void AppendStructuredDivinationOutputContract(List<DeepSeekAPI.Message> messages)
    {
        if (!ShouldRequestStructuredDivinationReply()) return;

        messages.Add(new DeepSeekAPI.Message("system",
            "本轮是在完成已锁定牌阵的完整解读。请只返回严格 JSON，不要 Markdown，不要代码块，不要额外解释。"
            + "JSON 字段必须是："
            + "{\"displayReply\":\"给聊天气泡显示的自然语言回复，短、直接、具体，不超过140字\","
            + "\"shortVerdict\":\"1到2句摘要\","
            + "\"judgeContent\":\"详情页综合判断，2到5句\","
            + "\"adviceContent\":\"详情页行动建议，2到4条，可用换行分隔\","
            + "\"topics\":[\"适合继续追问的问题1？\",\"适合继续追问的问题2？\",\"适合继续追问的问题3？\"]}"));
    }

    private bool ShouldRequestStructuredDivinationReply()
    {
        if (readingLock == null) return false;
        return activeReadingState == "cards_locked"
            || activeActionKind == "complete_verdict"
            || activeActionKind == "reveal_card";
    }

    private string BuildVisibleReplyFromStructuredOutput(string output)
    {
        if (!TryParseStructuredDivinationReply(output, out var parsed))
            return output;

        string display = FirstNonEmpty(parsed.displayReply, parsed.shortVerdict, parsed.judgeContent);
        if (!string.IsNullOrWhiteSpace(parsed.adviceContent))
            display = string.IsNullOrWhiteSpace(display)
                ? parsed.adviceContent
                : $"{display}\n\n{parsed.adviceContent}";

        return string.IsNullOrWhiteSpace(display) ? output : display.Trim();
    }

    public PromptRecord GetLastPromptRecord()
    {
        return lastAssemblyResult?.promptRecord;
    }

    public IReadOnlyList<PromptRecord> GetPromptRecords()
    {
        return promptRecords;
    }

    public void RecordExternalPrompt(PromptRecord record, string modelOutput)
    {
        if (record == null) return;
        record.modelOutput = modelOutput ?? "";
        record.recordedAt = System.DateTime.UtcNow.ToString("o");
        AddPromptRecord(record);
    }

    private static bool IsRationaleQuestion(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        return message.Contains("为什么这么说")
            || message.Contains("为什么这样说")
            || message.Contains("依据是什么")
            || message.Contains("你凭什么")
            || message.ToLowerInvariant().Contains("why do you say");
    }

    private OutputGuardOptions BuildOutputGuardOptions()
    {
        var stage = lastAssemblyResult?.promptRecord?.stage;
        if (string.IsNullOrEmpty(stage))
            stage = "listen";

        var deckCardNames = new List<string>();
        foreach (var card in TarotDeck.FullDeck)
        {
            if (!string.IsNullOrEmpty(card.nameZh))
                deckCardNames.Add(card.nameZh);
            if (!string.IsNullOrEmpty(card.nameEn))
                deckCardNames.Add(card.nameEn);
            if (!string.IsNullOrEmpty(card.cardId))
                deckCardNames.Add(card.cardId);
        }

        return new OutputGuardOptions
        {
            stage = stage,
            responseMode = lastAssemblyResult?.promptRecord?.responseMode,
            locale = "zh-CN",
            responseContract = ResponseContracts.GetFor(stage),
            readingLock = readingLock,
            deckCardNames = deckCardNames
        };
    }

    private void StorePromptRecord(string modelOutput, string oracleMessageId = null)
    {
        var record = lastAssemblyResult?.promptRecord;
        if (record == null) return;

        record.modelOutput = modelOutput ?? "";
        record.oracleMessageId = oracleMessageId ?? record.oracleMessageId;
        record.recordedAt = System.DateTime.UtcNow.ToString("o");

        AddPromptRecord(record);
    }

    private void ApplyOracleRuntimeMetadata(ChatMessageData message, PromptRecord record)
    {
        if (message == null || record == null) return;

        message.oraclePromptId = record.promptId ?? "";
        message.oracleScene = record.scene ?? "";
        message.oracleStage = record.stage ?? "";
        message.oracleStageReason = record.stageReason ?? "";
        message.oracleResponseMode = record.responseMode ?? "";
        message.oracleRiskLevel = record.riskLevel ?? "";
        message.oracleRiskFlags = record.riskFlags != null
            ? new List<string>(record.riskFlags)
            : new List<string>();

        record.oracleMessageId = message.id.ToString();
    }

    private void AddPromptRecord(PromptRecord record)
    {
        if (record == null) return;

        promptRecords.Add(record);
        while (promptRecords.Count > MAX_PROMPT_RECORDS)
            promptRecords.RemoveAt(0);
    }

    private string ApplyOutputGuard(string text)
    {
        if (!useOracleRuntime) return text;
        if (TryParseStructuredDivinationReply(text, out _)) return text;

        try
        {
            var options = BuildOutputGuardOptions();
            var guardResult = OutputGuard.Check(text, options);
            if (guardResult.ok || guardResult.issues == null || guardResult.issues.Count == 0)
                return text;

            Debug.LogWarning($"[OracleRuntime] OutputGuard issues: {string.Join(", ", guardResult.issues)}");

            if (guardResult.issues.Contains("unlocked_card"))
            {
                return "我不能引用还没有锁定的牌。先把这次 readingId 和牌面确认下来，再继续解读。";
            }

            var cleaned = CleanGuardedOutput(text, guardResult.issues, options);
            return string.IsNullOrEmpty(cleaned) ? text : cleaned;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[OracleRuntime] OutputGuard check failed: {ex.Message}");
            return text;
        }
    }

    private static string CleanGuardedOutput(string text, List<string> issues, OutputGuardOptions options)
    {
        var value = text ?? "";

        if (issues.Contains("absolute_prediction"))
        {
            value = value.Replace("一定会", "更可能会")
                .Replace("肯定会", "看起来会")
                .Replace("绝对不会", "不太像会")
                .Replace("必然", "更像是")
                .Replace("注定", "像是在走向");
        }

        if (issues.Contains("third_party_mind_claim"))
        {
            value += "\n我不能断言对方心里真正怎么想，只能根据你描述的行为模式来判断。";
        }

        if (issues.Contains("therapy_claim") || issues.Contains("clinical_diagnosis") || issues.Contains("medical_claim"))
        {
            value = "这部分我不能做诊断或治疗判断。更稳妥的是：先照顾好当下安全感，如果涉及健康或风险，请找专业人士确认。";
        }

        if (issues.Contains("listen_final_verdict"))
        {
            value = "我先不急着下结论。你现在最明显的张力，是想要一个确定回应，却又怕自己继续追会更失控。";
        }

        if (issues.Contains("too_many_sentences") || issues.Contains("too_many_words"))
        {
            value = TrimToContract(value, options?.responseContract);
        }

        return value.Trim();
    }

    private static string TrimToContract(string text, ResponseContract contract)
    {
        if (string.IsNullOrEmpty(text) || contract == null) return text;

        var maxSentences = Mathf.Max(1, contract.maxSentences);
        var parts = System.Text.RegularExpressions.Regex.Split(text.Trim(), @"(?<=[。！？.!?])")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Take(maxSentences)
            .ToList();

        var trimmed = parts.Count > 0 ? string.Join("", parts) : text.Trim();
        if (contract.maxWords > 0 && trimmed.Length > contract.maxWords)
            trimmed = trimmed.Substring(0, Mathf.Min(trimmed.Length, contract.maxWords)).TrimEnd() + "…";
        return trimmed;
    }

    private void QueueMemorySummary(string userInput, string assistantReply)
    {
        if (!useOracleRuntime) return;
        if (string.IsNullOrEmpty(userInput) || string.IsNullOrEmpty(assistantReply)) return;

        // 只在有明显长期价值的回合尝试提炼记忆，避免把闲聊都塞进候选记忆。
        if (!ShouldSummarizeMemory(userInput, assistantReply)) return;

        var sourcePromptId = lastAssemblyResult?.promptRecord?.promptId;
        var summaryPayload = new ChatPayload
        {
            scene = "user_memory_summary",
            locale = "zh-CN",
            message = "请从这一轮对话中提炼长期有用的用户记忆候选，不要保存完整原话。\n"
                + $"用户：{userInput}\n"
                + $"助手：{assistantReply}",
            activeRelationshipId = activeRelationshipId,
            activeReadingId = activeReadingId,
            user = new UserPayloadProfile
            {
                preferredTone = GetOracleVoiceId(),
                locale = "zh-CN"
            }
        };

        var assembly = ContextAssembler.AssembleSceneCall(
            "user_memory_summary", summaryPayload, memorySource, GetOracleVoiceId());

        if (assembly?.messages == null || assembly.messages.Count == 0)
            return;

        var messages = new List<DeepSeekAPI.Message>();
        foreach (var cm in assembly.messages)
            messages.Add(new DeepSeekAPI.Message(cm.role, cm.content));

        deepSeekAPI.SendChatRequest(messages,
            (response) =>
            {
                if (assembly.promptRecord != null)
                {
                    assembly.promptRecord.modelOutput = response ?? "";
                    AddPromptRecord(assembly.promptRecord);
                }

                var summary = ExtractMemorySummary(response);
                if (string.IsNullOrEmpty(summary)) return;

                memorySource.candidates.Add(new MemoryCandidate
                {
                    id = System.Guid.NewGuid().ToString("N").Substring(0, 12),
                    userId = "",
                    type = "recurring_theme",
                    text = summary,
                    status = "pending",
                    confidence = 0.7f,
                    relationshipId = activeRelationshipId,
                    sourceConversationId = "",
                    sourceMessageId = sourcePromptId,
                    createdAt = System.DateTime.UtcNow.ToString("o")
                });

                TrimMemoryCandidates();
                FirestoreManager.Instance?.SaveMemorySource(memorySource);
                Debug.Log($"[OracleRuntime] Memory candidate saved: {summary}");
            },
            (error) =>
            {
                Debug.LogWarning($"[OracleRuntime] Memory summary skipped: {error}");
            });
    }

    private static bool ShouldSummarizeMemory(string userInput, string assistantReply)
    {
        var text = (userInput ?? "") + " " + (assistantReply ?? "");
        return text.Contains("关系")
            || text.Contains("喜欢")
            || text.Contains("前任")
            || text.Contains("朋友")
            || text.Contains("边界")
            || text.Contains("不要")
            || text.Contains("总是")
            || text.Contains("每次")
            || text.Contains("工作")
            || text.Contains("选择")
            || text.Contains("焦虑")
            || text.ToLowerInvariant().Contains("prefer");
    }

    private static string ExtractMemorySummary(string response)
    {
        if (string.IsNullOrEmpty(response)) return "";

        var match = System.Text.RegularExpressions.Regex.Match(
            response, "\"modelSummary\"\\s*:\\s*\"(?<value>.*?)\"",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        if (!match.Success) return "";

        var value = match.Groups["value"].Value
            .Replace("\\n", " ")
            .Replace("\\\"", "\"")
            .Trim();

        if (value.Length > 120)
            value = value.Substring(0, 120).TrimEnd() + "…";
        return value;
    }

    private void TrimMemoryCandidates()
    {
        const int maxCandidates = 20;
        if (memorySource?.candidates == null) return;
        while (memorySource.candidates.Count > maxCandidates)
            memorySource.candidates.RemoveAt(0);
    }

    /// <summary>
    /// 获取当前 MemorySource（供 DailyOracleService 等外部模块读取）
    /// </summary>
    public MemorySource GetMemorySource()
    {
        return memorySource;
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
        SaveCloudDialogHistory();
    }

    /// <summary>
    /// 设置活跃关系 ID（好友合盘场景）
    /// </summary>
    public void SetActiveRelationship(string relationshipId)
    {
        activeRelationshipId = relationshipId;
        SaveCloudDialogHistory();
    }

    public void SetActiveFriendContext(string friendContext)
    {
        activeFriendContext = friendContext ?? "";
        SaveCloudDialogHistory();
    }

    /// <summary>
    /// 设置当前占卜状态（DivinationEngine 调用）
    /// </summary>
    public void SetActiveReadingState(string state)
    {
        activeReadingState = state ?? "";
        SaveCloudDialogHistory();
    }

    /// <summary>
    /// 设置当前动作类型（DivinationEngine 调用）
    /// </summary>
    public void SetActiveActionKind(string actionKind)
    {
        activeActionKind = actionKind ?? "";
        SaveCloudDialogHistory();
    }

    /// <summary>
    /// 设置当前 Reading ID（DivinationEngine 调用）
    /// </summary>
    public void SetActiveReadingId(string readingId)
    {
        activeReadingId = readingId;
        SaveCloudDialogHistory();
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
        SaveCloudDialogHistory();
    }

    /// <summary>
    /// 发送消息到 DeepSeek API（非流式，保留向后兼容）
    /// 启用 OracleRuntime 时使用 ContextAssembler 组装结构化 Prompt
    /// </summary>
    public void SendMessageToAI(System.Action<string> onSuccess, System.Action<string> onError)
    {
        List<DeepSeekAPI.Message> messages;

        if (useOracleRuntime)
        {
            string lastUserMsg = (mApiMessageHistory.Count > 0
                && mApiMessageHistory[mApiMessageHistory.Count - 1].role == "user")
                ? mApiMessageHistory[mApiMessageHistory.Count - 1].content
                : "";
            messages = GetOracleAssembledMessages(lastUserMsg);
        }
        else
        {
            messages = GetApiMessagesWithSystemPrompt();
        }

        deepSeekAPI.SendChatRequest(messages,
            (aiResponse) =>
            {
                var guardedOutput = ApplyOutputGuard(aiResponse);
                StorePromptRecord(guardedOutput);
                QueueMemorySummary(lastAssemblyResult?.promptRecord?.userInput, guardedOutput);
                SaveCloudDialogHistory();
                onSuccess?.Invoke(guardedOutput);
            },
            onError
        );
    }

    // ---- 流式输出支持 ----

    /// <summary>当前流式消息的索引（-1 表示无活跃流式消息）</summary>
    private int mStreamingMessageIndex = -1;

    /// <summary>
    /// 发送流式消息到 DeepSeek API
    /// </summary>
    /// <param name="onChunk">每收到一个 token 的回调（delta 文本）</param>
    /// <param name="onComplete">流式完成回调（完整文本）</param>
    /// <param name="onError">错误回调</param>
    public int SendMessageToAIStream(
        System.Action<string> onChunk,
        System.Action<string> onComplete,
        System.Action<string> onError)
    {
        List<DeepSeekAPI.Message> messages;

        if (useOracleRuntime)
        {
            string lastUserMsg = (mApiMessageHistory.Count > 0
                && mApiMessageHistory[mApiMessageHistory.Count - 1].role == "user")
                ? mApiMessageHistory[mApiMessageHistory.Count - 1].content
                : "";
            messages = GetOracleAssembledMessages(lastUserMsg);
        }
        else
        {
            messages = GetApiMessagesWithSystemPrompt();
        }

        // 创建占位消息。每次请求都持有自己的索引，避免并发流式回复互相覆盖。
        int streamingMessageIndex = CreateStreamingAIMessage();

        deepSeekAPI.SendChatRequestStream(messages,
            (chunk) =>
            {
                AppendToStreamingMessage(streamingMessageIndex, chunk);
                onChunk?.Invoke(chunk);
            },
            (fullContent) =>
            {
                var guardedOutput = ApplyOutputGuard(fullContent);
                string displayOutput = BuildVisibleReplyFromStructuredOutput(guardedOutput);
                FinalizeStreamingMessage(streamingMessageIndex, displayOutput);
                StorePromptRecord(guardedOutput);
                QueueMemorySummary(lastAssemblyResult?.promptRecord?.userInput, displayOutput);
                SaveCloudDialogHistory();

                onComplete?.Invoke(guardedOutput);
            },
            (error) =>
            {
                // 移除占位消息
                if (streamingMessageIndex >= 0 && streamingMessageIndex < mChatMessageList.Count)
                {
                    mChatMessageList.RemoveAt(streamingMessageIndex);
                }
                if (mStreamingMessageIndex == streamingMessageIndex)
                    mStreamingMessageIndex = -1;
                onError?.Invoke(error);
            }
        );

        return streamingMessageIndex;
    }

    /// <summary>
    /// 创建流式占位消息（空内容）
    /// </summary>
    private int CreateStreamingAIMessage()
    {
        ChatMessageData data = new ChatMessageData
        {
            id = mMessageIdCounter++,
            roleType = DialogRoleType.AI,
            messageType = MsgType.Str,
            content = "",
            options = null,
            divinerType = CurrentDivinerType
        };
        ApplyOracleRuntimeMetadata(data, lastAssemblyResult?.promptRecord);
        mChatMessageList.Add(data);
        mStreamingMessageIndex = mChatMessageList.Count - 1;
        return mStreamingMessageIndex;
    }

    /// <summary>
    /// 向当前流式消息追加内容
    /// </summary>
    private void AppendToStreamingMessage(string chunk)
    {
        AppendToStreamingMessage(mStreamingMessageIndex, chunk);
    }

    private void AppendToStreamingMessage(int messageIndex, string chunk)
    {
        if (messageIndex < 0 || messageIndex >= mChatMessageList.Count) return;
        mChatMessageList[messageIndex].content += chunk;
    }

    /// <summary>
    /// 完成流式消息：写入 API 历史 & 最近回复
    /// </summary>
    private void FinalizeStreamingMessage(string fullContent)
    {
        FinalizeStreamingMessage(mStreamingMessageIndex, fullContent);
    }

    private void FinalizeStreamingMessage(int messageIndex, string fullContent)
    {
        if (messageIndex < 0 || messageIndex >= mChatMessageList.Count) return;

        // 确保内容完整（防止 chunk 累加不精确）
        mChatMessageList[messageIndex].content = fullContent;
        ApplyOracleRuntimeMetadata(mChatMessageList[messageIndex], lastAssemblyResult?.promptRecord);

        // 加入 API 历史
        mApiMessageHistory.Add(new DeepSeekAPI.Message("assistant", fullContent));

        // 追踪最近 AI 回复
        recentAssistantReplies.Add(fullContent);
        if (recentAssistantReplies.Count > MAX_RECENT_MESSAGES)
            recentAssistantReplies.RemoveAt(0);

        if (mStreamingMessageIndex == messageIndex)
            mStreamingMessageIndex = -1;
    }

    public void SetAIMessageTTSInfo(int messageIndex, float durationSeconds, bool audioReady)
    {
        if (messageIndex < 0 || messageIndex >= mChatMessageList.Count) return;

        var message = mChatMessageList[messageIndex];
        if (message == null) return;

        message.ttsDurationSeconds = Mathf.Max(0f, durationSeconds);
        message.ttsAudioReady = audioReady && durationSeconds > 0f;
        SaveCloudDialogHistory();
    }

    /// <summary>
    /// 获取当前流式消息在列表中的索引（-1 表示没有活跃流）
    /// </summary>
    public int GetStreamingMessageIndex()
    {
        return mStreamingMessageIndex;
    }

    /// <summary>
    /// 替换最后一条 AI 消息的选项（流式完成后调用）
    /// </summary>
    public void SetLastAIMessageOptions(List<string> options)
    {
        if (mChatMessageList.Count == 0) return;
        var last = mChatMessageList[mChatMessageList.Count - 1];
        if (last.roleType == DialogRoleType.AI)
        {
            last.options = options;
        }
    }

    public void SetAIMessageOptions(int messageIndex, List<string> options)
    {
        if (messageIndex < 0 || messageIndex >= mChatMessageList.Count) return;
        var message = mChatMessageList[messageIndex];
        if (message.roleType == DialogRoleType.AI)
        {
            message.options = options;
            SaveCloudDialogHistory();
        }
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
