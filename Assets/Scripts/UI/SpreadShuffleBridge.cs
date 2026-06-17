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

    /// <summary>
    /// 洗牌完成时调用（由 TarorSingleSpreadShuffleUI 触发）
    /// </summary>
    public static void NotifyComplete(List<(TarotCard card, bool upright)> drawnCards)
    {
        ShuffleCompleted?.Invoke(drawnCards);
    }

    /// <summary>
    /// 清理桥接状态（在 InteractionCard 被销毁 / 面板重置时调用）
    /// </summary>
    public static void Clear()
    {
        PendingSpread = null;
        PendingMessageData = null;
        ShuffleCompleted = null;
    }
}
