import https from "https";
import { randomUUID } from "crypto";

const TTS_HTTP_HOST = "openspeech.bytedance.com";
const TTS_HTTP_PATH = "/api/v3/tts/unidirectional";
const TTS_RESOURCE_ID = "seed-tts-2.0";

export type TtsCallbacks = {
  onAudioChunk: (pcmBase64: string) => void;
  onEnded?: () => void;
  onError?: (message: string) => void;
};

export class TtsService {
  private appId = "";
  private accessKey = "";
  private userId: string | number = "";
  private speaker: string;
  private callbacks: TtsCallbacks | null = null;
  private textBuffer: string[] = [];
  private closed = false;
  private currentRequest: ReturnType<typeof https.request> | null = null;

  constructor(options: { speaker?: string } = {}) {
    this.speaker = options.speaker ?? process.env.VOICE_TTS_SPEAKER ?? "zh_female_vv_uranus_bigtts";
  }

  async connect(appId: string, accessKey: string, userId: string | number, callbacks: TtsCallbacks): Promise<void> {
    this.appId = appId;
    this.accessKey = accessKey;
    this.userId = userId;
    this.callbacks = callbacks;
    this.closed = false;
    this.textBuffer = [];
  }

  startSession(): void {
    this.textBuffer = [];
  }

  sendText(text: string): void {
    if (this.closed || !text.trim()) return;
    this.textBuffer.push(text);
  }

  finishSession(): void {
    if (this.closed || this.textBuffer.length === 0) {
      this.callbacks?.onEnded?.();
      return;
    }
    const text = this.textBuffer.join("").trim();
    this.textBuffer = [];
    if (!text) {
      this.callbacks?.onEnded?.();
      return;
    }
    this.sendHttpRequest(text);
  }

  close(): void {
    this.closed = true;
    this.textBuffer = [];
    try {
      this.currentRequest?.destroy();
    } catch {}
    this.currentRequest = null;
  }

  private sendHttpRequest(text: string): void {
    const body = JSON.stringify({
      user: { uid: `voice_user_${this.userId}` },
      req_params: {
        text,
        speaker: this.speaker,
        audio_params: { format: "pcm", sample_rate: 24000 },
      },
    });
    const bodyBuffer = Buffer.from(body, "utf8");
    const request = https.request(
      {
        hostname: TTS_HTTP_HOST,
        path: TTS_HTTP_PATH,
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Api-App-Id": this.appId,
          "X-Api-Access-Key": this.accessKey,
          "X-Api-Resource-Id": TTS_RESOURCE_ID,
          "X-Api-Request-Id": randomUUID(),
          "Content-Length": bodyBuffer.length,
        },
      },
      (response) => {
        if (this.closed) return;
        if (response.statusCode !== 200) {
          this.callbacks?.onError?.(`TTS HTTP error: ${response.statusCode}`);
          return;
        }

        let buffer = "";
        response.on("data", (chunk: Buffer) => {
          if (this.closed) return;
          buffer += chunk.toString("utf8");
          const lines = buffer.split("\n");
          buffer = lines.pop() ?? "";
          for (const line of lines) this.handleJsonLine(line);
        });
        response.on("end", () => {
          if (buffer.trim()) this.handleJsonLine(buffer);
          this.callbacks?.onEnded?.();
        });
        response.on("error", (error) => this.callbacks?.onError?.(error.message));
      }
    );

    request.on("error", (error) => {
      if (!this.closed) this.callbacks?.onError?.(error.message);
    });
    this.currentRequest = request;
    request.write(bodyBuffer);
    request.end();
  }

  private handleJsonLine(line: string): void {
    const trimmed = line.trim();
    if (!trimmed) return;
    try {
      const json = JSON.parse(trimmed);
      if (json.code !== undefined && json.code !== 0 && json.code !== 20000000) {
        this.callbacks?.onError?.(`TTS error: ${json.message ?? json.code}`);
        return;
      }
      if (json.data) this.callbacks?.onAudioChunk(json.data);
    } catch {
      // Ignore incomplete non-JSON chunks.
    }
  }
}

