# 可复制给其他开发者或 AI Agent 的实现提示词

你需要把一个 Web 产品接入“语音输入 + 连续语音通话”能力。请严格按以下架构实现，不要把业务逻辑和语音协议耦合在一起。

## 目标

实现两种语音入口：

1. 按住说话转文字并发送：
   - 用户按住按钮开始录音。
   - 实时显示识别文字。
   - 松手后直接发送最终文字。
   - 不发送语音文件。
   - 不进入 TTS。

2. 连续语音通话：
   - 用户说话后 ASR 转文字。
   - Agent 基于当前上下文回复。
   - TTS 播放 AI 回复。
   - 播放期间用户说话可打断。
   - 避免 AI 自己的 TTS 被麦克风识别成用户输入。

## 技术要求

- 前端音频格式：PCM 16kHz, int16, mono, little-endian。
- 使用 AudioWorklet 把浏览器 Float32 PCM 转为 Int16 PCM。
- 每 20ms 发送一个 PCM frame。
- WebSocket 路径建议为 `/api/voice/ws`。
- `mode=asr` 只做 ASR-only。
- 默认模式做 ASR + Agent + TTS。
- ASR 结果必须过滤空文本和纯标点。
- 录音小于 500ms 不发送。
- ASR 上游必须使用全量识别结果模式，避免自然停顿丢前文。
- ASR 静音结束阈值建议 1200ms。
- TTS 播放期间默认禁止上传麦克风帧，只有检测到真实用户打断才上传。

## 后端抽象

语音模块只能依赖两个业务接口：

```ts
interface VoiceAgent {
  reply(input, events): Promise<{ text: string }>;
}

interface VoiceSessionStore {
  loadHistory(input): Promise<VoiceMessage[]>;
  appendMessage(input): Promise<void>;
}
```

不要在语音模块里直接写具体业务表、任务表、项目表。业务 scope 通过 `{ scopeType, scopeId }` 传入。

## 状态机

按住说话：

```text
idle -> connecting -> ready -> recording -> processing -> ready
```

连续通话：

```text
idle -> connecting -> listening -> thinking -> speaking -> listening
speaking/thinking -> interrupted -> listening
```

## 验收

必须测试：

- 空录音不发送。
- 纯标点不发送。
- 长句中自然停顿不丢前文。
- AI 播放时不会识别自己。
- 用户说话能打断 AI。
- 打断后旧回复不会污染新回合。
- 挂断后麦克风和 TTS 都停止。

