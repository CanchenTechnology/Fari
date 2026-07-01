# TimeFlow Voice Reuse Module

这是从 TimeFlow 的语音输入与通话能力中抽象出来的复用包。目标不是复制 TimeFlow 的任务业务，而是沉淀一套可迁移到其他产品的“语音交互模块”。

## 这个模块解决什么

1. 按住说话转文字并发送
   - 用户按住按钮开始录音。
   - 前端实时显示 ASR partial。
   - 松手后发送最终文字，不发送语音文件。
   - 过滤空音频、纯标点、过短录音。

2. 连续语音通话
   - 用户说话后，ASR 识别成文字。
   - Agent 读取上下文生成回复。
   - TTS 播放 AI 回复。
   - 播放时如果用户继续说话，可以打断 AI。
   - 避免把 AI 自己播放的声音再次识别成用户输入。

## 核心结构

```text
frontend
  pcm-processor.js        Browser AudioWorklet, Float32 -> PCM16.
  useAsrRecorder.ts       ASR-only hook for hold-to-talk text input.
  VoiceHoldInput.tsx      Notion-minimal press/hold UI.
  useVoiceCall.ts         Continuous call hook with TTS playback and echo gate.
  ChatComposerExample.tsx Example composer.

backend
  asr-service.ts          Volcengine/Doubao streaming ASR client.
  tts-service.ts          Volcengine/Doubao streaming TTS client.
  voice-ws-handler.ts     Generic WebSocket protocol handler.
  types.ts                Product integration interfaces.
  example-express-server.ts Minimal Express/ws wiring.

docs
  architecture.md         Detailed mechanism and lessons.
  protocol.md             WebSocket message protocol.
  integration-guide.md    How another product should integrate it.
  qa-checklist.md         Full test checklist.

prompts
  reuse-implementation-prompt.md Prompt for other engineers/agents.
  voice-agent-system-prompt.md   Prompt for voice Agent behavior.
```

## 最重要的经验

- ASR 使用“全量结果”模式。增量/单句模式容易在用户自然停顿时只保留最后一句。
- 语音输入和语音通话是两种不同入口：前者只产出文字，后者是持续 Agent 回合。
- 通话必须有打断状态机。否则用户无法自然接话，也容易把旧回复混入新一轮。
- TTS 播放时默认不要上传麦克风音频，只有检测到真实用户插话时才打断并上传。
- 纯标点、短录音、无语音能量都不能进入对话，否则会出现很多“。”。
- 后端不要直接依赖业务表。通过 `VoiceAgent` 和 `VoiceSessionStore` 两个接口接入产品自己的业务。

## 快速接入

1. 前端复制：
   - `code/frontend/pcm-processor.js` 放到 Web 静态目录根路径，例如 `/public/pcm-processor.js`。
   - 复制 `useAsrRecorder.ts`、`VoiceHoldInput.tsx`、`useVoiceCall.ts`。

2. 后端复制：
   - 复制 `code/backend/asr-service.ts`、`tts-service.ts`、`voice-ws-handler.ts`、`types.ts`。
   - 按 `example-express-server.ts` 接入 `/api/voice/ws`。

3. 配置环境变量：

```bash
VOICE_APP_ID=your_volc_app_id
VOICE_ACCESS_KEY=your_volc_access_key
VOICE_TTS_SPEAKER=zh_female_vv_uranus_bigtts
```

4. 接入自己的 Agent：

```ts
const agent: VoiceAgent = {
  async reply(input, events) {
    events.onPartial?.("我先看一下。");
    const text = await yourLLM(input.text, input.history);
    return { text };
  },
};
```

## 交付边界

这个包不包含 TimeFlow 的密钥、任务数据库、记忆系统、多人协作和部署配置。它提供的是可复用的语音模块骨架与迁移纪律。

