# 对话中由 AI 触发牌阵的客户端动作方案

## 目标

对话页不再靠 UI 自己猜“什么时候该弹牌阵”。  
AI 先根据用户反馈、上下文、@好友、当前 reading 状态判断是否需要牌阵；只有当 AI 明确输出客户端动作时，Unity 才展示对应牌阵 UI。

这样可以避免：

- 普通聊天突然弹牌阵。
- 用户只是问建议时，被强行进入抽牌。
- 已经锁定 reading 的追问阶段又开新牌阵。
- @好友时靠正则硬猜双人占卜。

## 客户端动作格式

AI 如果要请求客户端打开 UI，需要在回复末尾追加一个隐藏块：

```text
<client_action>{"action":"show_spread","spreadKind":"self_repair","cardCount":3,"question":"用户真正要看的问题","reason":"为什么这个牌阵适合"}</client_action>
```

这个隐藏块不会显示在聊天气泡里。`DialogSystem` 会在流式完成后解析它，并把它从可见回复中剥离。

## 支持的动作

| action | 作用 | 触发条件 |
| --- | --- | --- |
| `show_spread` | 在聊天里显示 1/3/5 牌阵交互卡 | 用户明确要求占卜/抽牌/牌阵，或 AI 判断问题确实需要牌阵拆解 |
| `start_spread` | 同 `show_spread`，兼容 runtime suggestedActions | 同上 |
| `show_relationship_divination` | 打开 @好友的双人关系占卜 | 已 @ 好友，且用户表达想和该好友做关系占卜/抽牌 |

## show_spread 参数

```json
{
  "action": "show_spread",
  "spreadKind": "relationship_tension",
  "cardCount": 3,
  "question": "我和他现在的关系卡在哪里？",
  "reason": "这个问题需要拆成当下、阻碍和下一步行动来看。"
}
```

字段说明：

- `spreadKind`：优先使用的牌阵类型。找不到时按 `cardCount` 回退。
- `cardCount`：支持 `1 / 3 / 5`，分别映射到现有互动卡 UI。
- `question`：本次牌阵真正要看的问题，会写入 `DivinationPlan`。
- `reason`：为什么适合该牌阵，会写入 runtime plan。

## 客户端执行链路

1. `ContextAssembler.BuildChatTurnInstruction()` 告诉 AI 可用的隐藏动作格式。
2. `DialogSystem.SendMessageToAIStream()` 收到完整回复后解析 `<client_action>`。
3. `DialogSystem` 清理可见文本，只把普通回复显示在聊天气泡。
4. `DialogUI.TryExecuteClientAction()` 读取动作请求。
5. `show_spread/start_spread` 会创建或应用 `DivinationPlan`，再把当前 AI 消息转换成对应 `InteractionCard1/3/5`。
6. `show_relationship_divination` 会检查当前是否已 @ 好友，以及本地/云端关系占卜是否可用，通过后打开双人占卜流程。

## 安全与边界

- 客户端只执行白名单动作。
- 普通聊天不输出 `client_action`。
- safety/crisis 话题不触发牌阵。
- 已经有锁定中的 reading 时，不再开新牌阵。
- 好友双人占卜必须有当前 @ 好友上下文。
- AI 不能声称知道第三方真实想法，只能说“牌面显示的关系动态”。

## 后续扩展

新增 UI API 时，只需要：

1. 在 `OracleClientActionRequest` 加必要字段。
2. 在 `DialogUI.TryExecuteClientAction()` 加一个 action 分支。
3. 在 `ContextAssembler.BuildChatTurnInstruction()` 的 allowed actions 中补说明。
4. 如果需要持久化，再把字段写进聊天历史结构。

