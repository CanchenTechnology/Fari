export type VoiceScope = {
  scopeType?: "global" | "task" | "project" | "document" | string;
  scopeId?: string | null;
};

export type VoiceUser = {
  id: string | number;
  name?: string | null;
};

export type VoiceMessage = {
  role: "user" | "assistant" | "system";
  content: string;
  createdAt?: string;
  metadata?: Record<string, unknown>;
};

export type VoiceAgentReply = {
  text: string;
  toolResults?: unknown[];
};

export type VoiceAgentEvents = {
  onPartial?: (text: string) => void;
  onToolStart?: (payload: { toolName: string; message: string }) => void;
  onToolDone?: (result: unknown) => void;
};

export interface VoiceAgent {
  reply(
    input: {
      user: VoiceUser;
      scope: VoiceScope;
      text: string;
      history: VoiceMessage[];
      signal?: AbortSignal;
    },
    events: VoiceAgentEvents
  ): Promise<VoiceAgentReply>;
}

export interface VoiceSessionStore {
  loadHistory(input: { user: VoiceUser; scope: VoiceScope }): Promise<VoiceMessage[]>;
  appendMessage(input: { user: VoiceUser; scope: VoiceScope; message: VoiceMessage }): Promise<void>;
}

export type VoiceCredentials = {
  appId: string;
  accessKey: string;
};

export type ServerVoiceEvent =
  | { type: "session_ready" }
  | { type: "asr_partial"; text: string }
  | { type: "asr_final"; text: string }
  | { type: "asr_ended" }
  | { type: "chat_partial"; text: string }
  | { type: "chat_ended"; text: string; toolResults?: unknown[] }
  | { type: "tts_audio"; data: string }
  | { type: "tts_end" }
  | { type: "tool_start"; toolName: string; message: string }
  | { type: "tool_done"; result: unknown }
  | { type: "error"; message: string };

