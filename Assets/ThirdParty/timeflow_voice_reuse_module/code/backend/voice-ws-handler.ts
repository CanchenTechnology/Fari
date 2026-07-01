import WebSocket from "ws";
import type { IncomingMessage } from "http";
import { AsrService } from "./asr-service";
import { TtsService } from "./tts-service";
import type {
  ServerVoiceEvent,
  VoiceAgent,
  VoiceCredentials,
  VoiceScope,
  VoiceSessionStore,
  VoiceUser,
} from "./types";

const PUNCTUATION_ONLY_RE = /^[\s.,!?;:，。！？、；：“”"'‘’（）()【】[\]{}《》<>…·~\-—_]+$/;
const EMPTY_AUDIO_ERROR_RE = /Timeout waiting next packet|waiting next packet timeout|session has ended/i;

function cleanText(text: string | undefined | null): string {
  const normalized = (text ?? "").replace(/\s+/g, " ").trim();
  if (!normalized) return "";
  return PUNCTUATION_ONLY_RE.test(normalized) ? "" : normalized;
}

function send(ws: WebSocket, event: ServerVoiceEvent): void {
  if (ws.readyState !== WebSocket.OPEN) return;
  ws.send(JSON.stringify(event));
}

function getUrl(req: IncomingMessage): URL {
  return new URL(req.url ?? "/", `http://${req.headers.host ?? "localhost"}`);
}

function getScope(url: URL): VoiceScope {
  return {
    scopeType: url.searchParams.get("scopeType") ?? "global",
    scopeId: url.searchParams.get("scopeId"),
  };
}

export type CreateVoiceWsHandlerOptions = {
  credentials: VoiceCredentials;
  resolveUser: (req: IncomingMessage, url: URL) => Promise<VoiceUser>;
  agent: VoiceAgent;
  store: VoiceSessionStore;
  createAsr?: () => AsrService;
  createTts?: () => TtsService;
};

export function createVoiceWsHandler(options: CreateVoiceWsHandlerOptions) {
  return async function handleVoiceWs(ws: WebSocket, req: IncomingMessage): Promise<void> {
    const url = getUrl(req);
    const user = await options.resolveUser(req, url);
    const mode = url.searchParams.get("mode");
    const scope = getScope(url);

    if (!options.credentials.appId || !options.credentials.accessKey) {
      send(ws, { type: "error", message: "Voice credentials are missing" });
      ws.close();
      return;
    }

    if (mode === "asr") {
      handleAsrOnly(ws, user, options.credentials, options.createAsr);
      return;
    }

    handleVoiceCall(ws, user, scope, options);
  };
}

function handleAsrOnly(
  ws: WebSocket,
  user: VoiceUser,
  credentials: VoiceCredentials,
  createAsr: (() => AsrService) | undefined
): void {
  let asr: AsrService | null = null;
  let closed = false;
  let acceptingAudio = false;
  let finishing = false;
  let lastRecognizedText = "";

  const cleanup = () => {
    asr?.close();
    asr = null;
  };

  const begin = () => {
    acceptingAudio = true;
    finishing = false;
    lastRecognizedText = "";
    cleanup();
  };

  const ensureAsr = () => {
    if (closed || !acceptingAudio) return null;
    if (asr) return asr;

    const instance = createAsr?.() ?? new AsrService();
    asr = instance;
    instance.connect(credentials.appId, credentials.accessKey, {
      onPartial: (text) => {
        const clean = cleanText(text);
        if (clean) {
          lastRecognizedText = clean;
          send(ws, { type: "asr_partial", text: clean });
        }
      },
      onFinal: (text) => {
        const clean = cleanText(text);
        if (clean) {
          lastRecognizedText = clean;
          send(ws, { type: "asr_final", text: clean });
        }
      },
      onEnded: () => {
        acceptingAudio = false;
        finishing = false;
        send(ws, { type: "asr_ended" });
        cleanup();
      },
      onError: (message) => {
        acceptingAudio = false;
        finishing = false;
        if (EMPTY_AUDIO_ERROR_RE.test(message) && !lastRecognizedText) {
          send(ws, { type: "asr_ended" });
        } else {
          send(ws, { type: "error", message });
        }
        cleanup();
      },
    });
    return instance;
  };

  send(ws, { type: "session_ready" });

  ws.on("message", (data: Buffer | string) => {
    if (typeof data === "string") {
      try {
        const msg = JSON.parse(data) as { type?: string };
        if (msg.type === "start_asr") begin();
        if (msg.type === "audio_end" && !finishing) {
          finishing = true;
          acceptingAudio = false;
          asr ? asr.finishSession() : send(ws, { type: "asr_ended" });
        }
        if (msg.type === "finish_session") {
          acceptingAudio = false;
          finishing = false;
          cleanup();
          send(ws, { type: "asr_ended" });
        }
      } catch {
        // Ignore invalid control JSON.
      }
      return;
    }

    const instance = ensureAsr();
    if (instance) instance.sendAudioChunk(Buffer.from(data));
  });

  ws.on("close", () => {
    closed = true;
    cleanup();
  });
}

function handleVoiceCall(
  ws: WebSocket,
  user: VoiceUser,
  scope: VoiceScope,
  options: CreateVoiceWsHandlerOptions
): void {
  let asr: AsrService | null = null;
  let tts: TtsService | null = null;
  let closed = false;
  let processing = false;
  let lastUserText = "";
  let generation = 0;
  const abortedGenerations = new Set<number>();
  let activeAbort: AbortController | null = null;

  const cleanupAsr = () => {
    asr?.close();
    asr = null;
  };

  const cleanupTts = () => {
    tts?.close();
    tts = null;
  };

  const interrupt = (source: string) => {
    const currentGeneration = generation;
    abortedGenerations.add(currentGeneration);
    activeAbort?.abort();
    activeAbort = null;
    cleanupTts();
    send(ws, { type: "tts_end" });
    processing = false;
    if (!closed) startAsr();
    console.log(`[voice] interrupted by ${source}`);
  };

  const startAsr = () => {
    if (closed || processing || asr) return;
    const instance = options.createAsr?.() ?? new AsrService();
    asr = instance;
    lastUserText = "";
    instance.connect(options.credentials.appId, options.credentials.accessKey, {
      onPartial: (text) => {
        const clean = cleanText(text);
        if (!clean) return;
        lastUserText = clean;
        send(ws, { type: "asr_partial", text: clean });
      },
      onFinal: (text) => {
        const clean = cleanText(text);
        if (!clean) return;
        lastUserText = clean;
        send(ws, { type: "asr_final", text: clean });
      },
      onEnded: () => {
        const clean = cleanText(lastUserText);
        cleanupAsr();
        send(ws, { type: "asr_ended" });
        if (clean) processUserText(clean).catch((error) => {
          send(ws, { type: "error", message: error instanceof Error ? error.message : "Voice processing failed" });
          processing = false;
          if (!closed) startAsr();
        });
        else if (!closed) startAsr();
      },
      onError: (message) => {
        cleanupAsr();
        if (EMPTY_AUDIO_ERROR_RE.test(message)) {
          if (!closed) startAsr();
          return;
        }
        send(ws, { type: "error", message });
        if (!closed) startAsr();
      },
    });
  };

  const processUserText = async (text: string) => {
    if (closed) return;
    processing = true;
    cleanupAsr();

    const currentGeneration = ++generation;
    const abort = new AbortController();
    activeAbort = abort;

    await options.store.appendMessage({
      user,
      scope,
      message: { role: "user", content: text, createdAt: new Date().toISOString(), metadata: { inputMode: "voice" } },
    });
    const history = await options.store.loadHistory({ user, scope });

    tts = options.createTts?.() ?? new TtsService();
    await tts.connect(options.credentials.appId, options.credentials.accessKey, user.id, {
      onAudioChunk: (data) => {
        if (!abortedGenerations.has(currentGeneration)) send(ws, { type: "tts_audio", data });
      },
      onEnded: () => {
        if (!abortedGenerations.has(currentGeneration)) send(ws, { type: "tts_end" });
      },
      onError: (message) => send(ws, { type: "error", message }),
    });
    tts.startSession();

    const reply = await options.agent.reply(
      { user, scope, text, history, signal: abort.signal },
      {
        onPartial: (partial) => {
          if (!abortedGenerations.has(currentGeneration)) send(ws, { type: "chat_partial", text: partial });
        },
        onToolStart: (payload) => {
          if (!abortedGenerations.has(currentGeneration)) send(ws, { type: "tool_start", ...payload });
        },
        onToolDone: (result) => {
          if (!abortedGenerations.has(currentGeneration)) send(ws, { type: "tool_done", result });
        },
      }
    );

    if (abortedGenerations.has(currentGeneration) || abort.signal.aborted || closed) return;

    const answer = cleanText(reply.text) || "我明白了。";
    await options.store.appendMessage({
      user,
      scope,
      message: { role: "assistant", content: answer, createdAt: new Date().toISOString(), metadata: { inputMode: "voice" } },
    });

    send(ws, { type: "chat_partial", text: answer });
    tts.sendText(answer);
    tts.finishSession();
    send(ws, { type: "chat_ended", text: answer, toolResults: reply.toolResults });
    processing = false;
  };

  send(ws, { type: "session_ready" });
  startAsr();

  ws.on("message", (data: Buffer | string) => {
    if (typeof data === "string") {
      try {
        const msg = JSON.parse(data) as { type?: string; source?: string };
        if (msg.type === "interrupt") interrupt(msg.source ?? "client");
        if (msg.type === "finish_session") ws.close();
      } catch {
        // Ignore invalid control JSON.
      }
      return;
    }

    if (!asr && !processing && !closed) startAsr();
    asr?.sendAudioChunk(Buffer.from(data));
  });

  ws.on("close", () => {
    closed = true;
    activeAbort?.abort();
    cleanupAsr();
    cleanupTts();
  });
}

