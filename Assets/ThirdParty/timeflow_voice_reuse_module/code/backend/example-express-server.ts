import express from "express";
import http from "http";
import WebSocket, { WebSocketServer } from "ws";
import { createVoiceWsHandler } from "./voice-ws-handler";
import type { VoiceAgent, VoiceMessage, VoiceSessionStore } from "./types";

const app = express();
const server = http.createServer(app);
const wss = new WebSocketServer({ noServer: true });

const memory = new Map<string, VoiceMessage[]>();

function key(userId: string | number, scopeType?: string, scopeId?: string | null): string {
  return `${userId}:${scopeType ?? "global"}:${scopeId ?? ""}`;
}

const store: VoiceSessionStore = {
  async loadHistory({ user, scope }) {
    return memory.get(key(user.id, scope.scopeType, scope.scopeId)) ?? [];
  },
  async appendMessage({ user, scope, message }) {
    const k = key(user.id, scope.scopeType, scope.scopeId);
    memory.set(k, [...(memory.get(k) ?? []), message]);
  },
};

const agent: VoiceAgent = {
  async reply(input, events) {
    events.onPartial?.("我先理解一下你的意思。");
    // Replace this with your real LLM or Agent call.
    return {
      text: `你刚才说的是：${input.text}。我会基于当前上下文继续处理。`,
      toolResults: [],
    };
  },
};

const voiceHandler = createVoiceWsHandler({
  credentials: {
    appId: process.env.VOICE_APP_ID ?? "",
    accessKey: process.env.VOICE_ACCESS_KEY ?? "",
  },
  resolveUser: async (_req, url) => {
    const userId = url.searchParams.get("userId") ?? "demo";
    return { id: userId, name: `User ${userId}` };
  },
  agent,
  store,
});

server.on("upgrade", (req, socket, head) => {
  if (!req.url?.startsWith("/api/voice/ws")) {
    socket.destroy();
    return;
  }
  wss.handleUpgrade(req, socket, head, (ws: WebSocket) => {
    voiceHandler(ws, req).catch((error) => {
      ws.send(JSON.stringify({ type: "error", message: error instanceof Error ? error.message : "voice handler failed" }));
      ws.close();
    });
  });
});

app.get("/health", (_req, res) => res.json({ ok: true }));

server.listen(3000, () => {
  console.log("Voice example server listening on http://localhost:3000");
});

