# WebSocket 协议

默认入口：

```text
/api/voice/ws?userId=123
/api/voice/ws?userId=123&mode=asr
/api/voice/ws?userId=123&scopeType=task&scopeId=abc
```

实际产品中建议不要直接信任 `userId`，应由 Cookie/JWT/session 解码得到用户。

## ASR-only 模式

### 客户端发送

```json
{ "type": "start_asr" }
```

之后发送二进制 PCM frame：

```text
PCM 16kHz, int16, mono, little-endian, 20ms per frame
```

松手：

```json
{ "type": "audio_end" }
```

取消：

```json
{ "type": "finish_session" }
```

### 服务端发送

```json
{ "type": "session_ready" }
{ "type": "asr_partial", "text": "明天去" }
{ "type": "asr_final", "text": "明天去杭州见客户" }
{ "type": "asr_ended" }
{ "type": "error", "message": "..." }
```

## 连续通话模式

### 客户端发送

二进制 PCM frame 同 ASR-only。

用户打断：

```json
{
  "type": "interrupt",
  "source": "client_barge_in",
  "micRms": 0.11,
  "ttsRms": 0.02
}
```

挂断：

```json
{ "type": "finish_session" }
```

### 服务端发送

```json
{ "type": "session_ready" }
{ "type": "asr_partial", "text": "帮我安排" }
{ "type": "asr_final", "text": "帮我安排明天的会议" }
{ "type": "asr_ended" }
{ "type": "chat_partial", "text": "我先看一下你的日程。" }
{ "type": "tool_start", "toolName": "calendar.search", "message": "正在查看日程" }
{ "type": "tool_done", "result": { "message": "已找到 2 个空档" } }
{ "type": "tts_audio", "data": "base64_pcm" }
{ "type": "tts_end" }
{ "type": "chat_ended", "text": "明天 10 点比较合适。", "toolResults": [] }
{ "type": "error", "message": "..." }
```

## 事件顺序建议

ASR-only：

```text
session_ready
start_asr
pcm...
asr_partial*
audio_end
asr_final?
asr_ended
```

连续通话：

```text
session_ready
pcm...
asr_partial*
asr_final
asr_ended
chat_partial*
tts_audio*
tts_end
chat_ended
```

打断：

```text
speaking
client interrupt
server cancels active generation
server sends tts_end
client returns to listening
```

