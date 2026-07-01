# 接入指南

## 1. 前端接入

### 放置 AudioWorklet

把 `code/frontend/pcm-processor.js` 放到静态目录根路径：

```text
public/pcm-processor.js
```

浏览器端通过：

```ts
await audioContext.audioWorklet.addModule("/pcm-processor.js");
```

### 接入按住说话

```tsx
const asr = useAsrRecorder({
  userId,
  onTranscript: async (text) => {
    if (!text.trim()) return;
    await sendMessage(text);
  },
  onError: toast.error,
});

<VoiceHoldInput
  asrState={asr.asrState}
  partialText={asr.partialText}
  onHoldStart={() => {
    if (asr.asrState === "idle") asr.connect();
    asr.startRecording();
  }}
  onHoldEnd={({ cancelled }) => {
    if (cancelled) asr.cancelRecording();
    else asr.stopRecording();
  }}
/>
```

注意：在 iOS Safari 上，麦克风权限必须由用户手势触发，所以 `startRecording` 要放在 pointer/touch handler 之后。

### 接入连续通话

```tsx
const call = useVoiceCall({
  userId,
  scope: { scopeType: "task", scopeId: taskId },
  onUserText: addUserBubble,
  onAssistantText: updateAssistantBubble,
  onAssistantDone: finalizeAssistantBubble,
  onError: toast.error,
});

<button onClick={call.startCall}>开始通话</button>
<button onClick={call.hangUp}>挂断</button>
```

## 2. 后端接入

### 创建 handler

```ts
const voiceHandler = createVoiceWsHandler({
  credentials: {
    appId: process.env.VOICE_APP_ID!,
    accessKey: process.env.VOICE_ACCESS_KEY!,
  },
  resolveUser: async (req, url) => {
    return { id: Number(url.searchParams.get("userId")), name: "User" };
  },
  agent,
  store,
});
```

### 接入 WebSocket server

```ts
server.on("upgrade", (req, socket, head) => {
  if (!req.url?.startsWith("/api/voice/ws")) return;
  wss.handleUpgrade(req, socket, head, (ws) => {
    voiceHandler(ws, req);
  });
});
```

## 3. 实现 Agent

最小版：

```ts
const agent: VoiceAgent = {
  async reply(input, events) {
    events.onPartial?.("我先处理一下。");
    const text = await callYourModel(input.text, input.history);
    return { text };
  },
};
```

## 4. 实现存储

最小版内存存储：

```ts
const messages = new Map<string, VoiceMessage[]>();

const store: VoiceSessionStore = {
  async loadHistory({ userId, scope }) {
    return messages.get(`${userId}:${scope?.scopeType}:${scope?.scopeId}`) ?? [];
  },
  async appendMessage({ userId, scope, message }) {
    const key = `${userId}:${scope?.scopeType}:${scope?.scopeId}`;
    messages.set(key, [...(messages.get(key) ?? []), message]);
  },
};
```

生产环境应替换为数据库存储。

## 5. 迁移到 App

如果是 WebView App，可以保留 Web Audio + WebSocket，但要确保：

- iOS/Android 请求麦克风权限。
- WebView 允许 `getUserMedia`。
- HTTPS/WSS。
- 后台切换时挂断通话或暂停录音。

如果是原生 App，建议保留同一套 WebSocket 协议：

- 原生端采集 PCM16 16kHz mono。
- 按协议发送二进制 frame。
- 播放服务端返回的 24kHz PCM TTS。
- 复用同一套后端 `voice-ws-handler.ts`。

