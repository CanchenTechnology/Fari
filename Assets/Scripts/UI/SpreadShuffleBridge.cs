using System;
using System.Collections.Generic;
using GamerFrameWork;

/// <summary>
/// SpreadInteractionCard 和 TarorSingleSpreadShuffleUI 之间的通信桥梁
/// 
/// 工作流程：
/// 1. InteractionCard 点击「开始抽牌」→ 设置 PendingSpread + 订阅 ShuffleCompleted
/// 2. 打开 TarorSingleSpreadShuffleUI
/// 3. ShuffleUI 读取 PendingSpread 显示对应数量卡槽
/// 4. 用户点击「开始洗牌」→ 抽牌 + 动画 → NotifyComplete
/// 5. InteractionCard 的回调被触发，更新面板状态
/// </summary>
public static class SpreadShuffleBridge
{
    /// <summary>待展示的牌阵定义（从 InteractionCard 传入）</summary>
    public static SpreadDefinition PendingSpread;

    /// <summary>触发洗牌的聊天消息（用于把抽牌结果写回具体消息）</summary>
    public static ChatMessageData PendingMessageData;

    /// <summary>洗牌完成事件（InteractionCard 订阅）</summary>
    public static event Action<List<(TarotCard card, bool upright)>> ShuffleCompleted;

    private static int _pendingDialogRevealMessageId = -1;
    private static string _pendingDialogRevealReadingId;

    /// <summary>
    /// 洗牌完成时调用（由 TarorSingleSpreadShuffleUI 触发）
    /// </summary>
    public static void NotifyComplete(List<(TarotCard card, bool upright)> drawnCards)
    {
        ShuffleCompleted?.Invoke(drawnCards);
    }

    /// <summary>
    /// 标记刚完成抽牌的对话消息。对话卡片恢复这条消息时，应播放一次翻牌动画。
    /// </summary>
    public static void MarkPendingDialogReveal(ChatMessageData message)
    {
        if (message == null) return;

        _pendingDialogRevealMessageId = message.id;
        _pendingDialogRevealReadingId = message.readingId;
    }

    /// <summary>
    /// 如果当前恢复的是刚抽完的消息，则消费标记并返回 true。
    /// </summary>
    public static bool ConsumePendingDialogReveal(ChatMessageData message)
    {
        if (message == null) return false;

        bool sameMessage = _pendingDialogRevealMessageId >= 0 && message.id == _pendingDialogRevealMessageId;
        bool sameReading = !string.IsNullOrEmpty(_pendingDialogRevealReadingId)
            && message.readingId == _pendingDialogRevealReadingId;
        if (!sameMessage && !sameReading)
            return false;

        _pendingDialogRevealMessageId = -1;
        _pendingDialogRevealReadingId = null;
        return true;
    }

    /// <summary>
    /// 清理桥接状态（在 InteractionCard 被销毁 / 面板重置时调用）
    /// </summary>
    public static void Clear()
    {
        PendingSpread = null;
        PendingMessageData = null;
        ShuffleCompleted = null;
        _pendingDialogRevealMessageId = -1;
        _pendingDialogRevealReadingId = null;
    }
}
