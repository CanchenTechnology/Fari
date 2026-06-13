using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFGameFrameWork;

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

            //messageType = MsgType.Str,
            content = content,
            options = options,
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
    /// 清空对话历史
    /// </summary>
    public void ClearChatHistory()
    {
        mChatMessageList.Clear();
        mApiMessageHistory.Clear();
        mMessageIdCounter = 0;
    }

    /// <summary>
    /// 发送消息到 DeepSeek API
    /// </summary>
    public void SendMessageToAI(System.Action<string> onSuccess, System.Action<string> onError)
    {
        List<DeepSeekAPI.Message> messages = GetApiMessagesWithSystemPrompt();
        deepSeekAPI.SendChatRequest(messages, onSuccess, onError);
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
    /// 获取当前占卜师的默认选项
    /// </summary>
    public List<string> GetCurrentDivinerOptions()
    {
        return CurrentDivinerType == DivinerType.Tarot ? GetTarotOptions() : GetAstrologyOptions();
    }
}
