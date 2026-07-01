# 架构解析：如何把 TimeFlow 语音能力抽象成复用模块

## 1. 两套语音能力

TimeFlow 的语音不是一个功能，而是两层能力。

### A. 语音输入

用途：替代键盘输入。

流程：

```text
按住按钮
  -> 浏览器采集麦克风
  -> AudioWorklet 转 PCM16
  -> WebSocket 发送到后端
  -> 后端转发给 ASR
  -> 前端实时显示 partial
  -> 松手后发送 audio_end
  -> 后端返回 asr_final / asr_ended
  -> 前端直接把文字作为消息发送
```

特点：

- 不进入 TTS。
- 不播放语音回复。
- 不上传语音文件。
- 不允许纯标点进入消息。
- 不需要复杂上下文，只需要把最终文字交给当前输入场景。

### B. 连续通话

用途：用户和 AI 连续语音对话。

流程：

```text
开始通话
  -> ASR 持续监听
  -> 用户自然停顿超过阈值
  -> ASR final
  -> Agent 生成回复
  -> TTS 播放回复
  -> 播放结束后自动恢复监听
  -> 用户说话时可以打断 AI
```

特点：

- 需要状态机：listening / thinking / speaking / interrupted。
- 需要把用户消息和 AI 回复写入会话。
- 需要 TTS 播放队列。
- 需要回声抑制，避免 AI 说的话被自己的麦克风识别。
- 需要 generation id，避免旧回复在被打断后继续写入。

## 2. 为什么之前会出现“AI 识别到自己说的话”

根因不是 ASR 不准，而是音频链路没有明确区分两种声音：

- 用户麦克风输入。
- 扬声器正在播放的 TTS。

如果 TTS 播放时继续上传麦克风帧，手机/电脑扬声器声音会被麦克风收到，ASR 就会把 AI 说的话当成用户说的话。

正确做法：

1. TTS 播放期间默认停止上传麦克风帧。
2. 同时监听麦克风 RMS 和 TTS RMS。
3. 只有麦克风音量显著高于 TTS 输出，且连续若干帧满足条件，才判定为用户打断。
4. 判定打断后：
   - 前端立即停止 TTS 播放。
   - 发送 `interrupt` 给后端。
   - 后端取消当前 Agent/TTS generation。
   - 开启新一轮 ASR。

## 3. 为什么会出现很多句号

常见原因：

1. 空录音或底噪触发了 ASR。
2. ASR 在结束时返回了标点占位。
3. 用户按得太短，只有无意义的结尾符号。
4. 前端没有过滤纯标点。
5. 后端没有把 empty-audio timeout 当成空结果处理。

修复原则：

- 前端录音小于 500ms 不发送。
- 前端没有检测到有效语音能量时不发送。
- 前后端都过滤纯标点。
- ASR 空音频错误不当成真正错误，而是返回空结果。

## 4. ASR 关键经验

### 结果模式必须是 full

语音助手里用户会自然停顿。如果 ASR 用“单句/增量”模式，每次返回的 text 可能只包含当前小段，最后只剩末尾一句。

推荐：

```json
{
  "request": {
    "model_name": "bigmodel",
    "result_type": "full",
    "enable_itn": true,
    "vad_segment_duration": 400
  }
}
```

### 静音阈值不能太短

过短会打断长句，过长会让用户觉得慢。

TimeFlow 经验值：

- 前端 chunk：20ms。
- ASR 采样：16kHz PCM16 mono。
- VAD 语音阈值：约 0.008。
- 自然停顿容忍：约 1200ms。

## 5. 后端抽象边界

后端语音模块只应该知道：

- 当前用户是谁。
- 当前语音 scope 是什么。
- 如何读取最近消息。
- 如何调用 Agent。
- 如何保存消息。

它不应该知道：

- 任务表结构。
- 记忆表结构。
- 页面路由。
- 具体产品文案。

所以复用模块暴露两个接口：

```ts
interface VoiceAgent {
  reply(input, events): Promise<{ text: string }>;
}

interface VoiceSessionStore {
  loadHistory(input): Promise<VoiceMessage[]>;
  appendMessage(input): Promise<void>;
}
```

任何产品只要实现这两个接口，就可以复用同一套语音链路。

## 6. 推荐状态机

### 语音输入

```text
idle
  -> connecting
  -> ready
  -> recording
  -> processing
  -> ready
```

### 连续通话

```text
idle
  -> connecting
  -> listening
  -> thinking
  -> speaking
  -> listening

speaking/thinking
  -> interrupted
  -> listening
```

## 7. 最小可复用原则

一个产品第一次接入时，不要同时接任务、记忆、多人协作。最小接入顺序：

1. ASR-only 按住说话。
2. 连续通话不带工具。
3. 连续通话写入会话历史。
4. 增加打断。
5. 增加业务 scope，例如 taskId、projectId、documentId。
6. 增加工具调用与状态展示。

