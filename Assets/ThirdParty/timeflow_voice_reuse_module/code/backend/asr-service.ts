import WebSocket from "ws";
import { randomUUID } from "crypto";
import { gzipSync, gunzipSync } from "zlib";

const ASR_WS_URL = "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async";
const ASR_RESOURCE_ID = "volc.seedasr.sauc.duration";

const PROTO_VERSION = 0x01;
const HEADER_SIZE = 0x01;
const MSG_TYPE_FULL_CLIENT = 0b0001;
const MSG_TYPE_AUDIO_CLIENT = 0b0010;
const MSG_TYPE_FULL_SERVER = 0b1001;
const MSG_TYPE_ERROR = 0b1111;
const FLAG_NONE = 0b0000;
const FLAG_LAST = 0b0010;
const FLAG_SEQ_NEG = 0b0011;
const SERIAL_JSON = 0b0001;
const SERIAL_NONE = 0b0000;
const COMPRESS_GZIP = 0b0001;
const COMPRESS_NONE = 0b0000;

export type AsrCallbacks = {
  onReady?: () => void;
  onPartial?: (text: string) => void;
  onFinal: (text: string) => void;
  onEnded?: () => void;
  onError?: (message: string) => void;
};

function buildHeader(msgType: number, flags: number, serial: number, compress: number): Buffer {
  return Buffer.from([
    (PROTO_VERSION << 4) | HEADER_SIZE,
    (msgType << 4) | flags,
    (serial << 4) | compress,
    0x00,
  ]);
}

function buildFullClientRequest(config: object): Buffer {
  const header = buildHeader(MSG_TYPE_FULL_CLIENT, FLAG_NONE, SERIAL_JSON, COMPRESS_GZIP);
  const payload = gzipSync(Buffer.from(JSON.stringify(config), "utf8"));
  const size = Buffer.allocUnsafe(4);
  size.writeUInt32BE(payload.length, 0);
  return Buffer.concat([header, size, payload]);
}

function buildAudioRequest(pcm: Buffer, isLast = false): Buffer {
  const header = buildHeader(MSG_TYPE_AUDIO_CLIENT, isLast ? FLAG_LAST : FLAG_NONE, SERIAL_NONE, COMPRESS_NONE);
  const size = Buffer.allocUnsafe(4);
  size.writeUInt32BE(pcm.length, 0);
  return Buffer.concat([header, size, pcm]);
}

function parseServerResponse(data: Buffer): {
  msgType: number;
  flags: number;
  sequence: number | null;
  payload?: any;
  errorCode?: number;
  errorMsg?: string;
  isLast: boolean;
} | null {
  if (data.length < 4) return null;

  const msgType = (data[1]! >> 4) & 0x0f;
  const flags = data[1]! & 0x0f;
  const compress = data[2]! & 0x0f;
  let offset = 4;
  let sequence: number | null = null;

  if (flags & 0b0001) {
    if (data.length >= offset + 4) {
      sequence = data.readInt32BE(offset);
      offset += 4;
    }
  }

  const isLast = flags === FLAG_SEQ_NEG || (flags & FLAG_LAST) !== 0 || (sequence !== null && sequence < 0);

  if (msgType === MSG_TYPE_ERROR) {
    let errorCode: number | undefined;
    let errorMsg: string | undefined;
    if (data.length >= offset + 4) {
      errorCode = data.readUInt32BE(offset);
      offset += 4;
    }
    if (data.length >= offset + 4) {
      const msgSize = data.readUInt32BE(offset);
      offset += 4;
      if (data.length >= offset + msgSize) errorMsg = data.slice(offset, offset + msgSize).toString("utf8");
    }
    return { msgType, flags, sequence, errorCode, errorMsg, isLast: true };
  }

  let payload: any = null;
  if (data.length >= offset + 4) {
    const payloadSize = data.readUInt32BE(offset);
    offset += 4;
    if (payloadSize > 0 && data.length >= offset + payloadSize) {
      const raw = data.slice(offset, offset + payloadSize);
      try {
        const decoded = compress === COMPRESS_GZIP ? gunzipSync(raw) : raw;
        payload = JSON.parse(decoded.toString("utf8"));
      } catch {
        payload = raw.toString("utf8");
      }
    }
  }

  return { msgType, flags, sequence, payload, isLast };
}

export class AsrService {
  private ws: WebSocket | null = null;
  private callbacks: AsrCallbacks | null = null;
  private configSent = false;
  private audioQueue: Buffer[] = [];
  private lastText = "";
  private finalTextFired = false;
  private endedFired = false;
  private finishCalled = false;
  private closed = false;
  private suppressCloseCallbacks = false;
  private speechDetected = false;
  private silenceTimer: ReturnType<typeof setTimeout> | null = null;

  private readonly silenceTimeoutMs: number;
  private readonly vadSpeechThreshold: number;

  constructor(options: { silenceTimeoutMs?: number; vadSpeechThreshold?: number } = {}) {
    this.silenceTimeoutMs = options.silenceTimeoutMs ?? 1200;
    this.vadSpeechThreshold = options.vadSpeechThreshold ?? 0.008;
  }

  connect(appId: string, accessKey: string, callbacks: AsrCallbacks): void {
    this.callbacks = callbacks;
    this.ws = new WebSocket(ASR_WS_URL, {
      headers: {
        "X-Api-App-Key": appId,
        "X-Api-Access-Key": accessKey,
        "X-Api-Resource-Id": ASR_RESOURCE_ID,
        "X-Api-Connect-Id": randomUUID(),
      },
    });

    this.ws.on("open", () => {
      const config = {
        user: { uid: "voice_user", appid: appId },
        audio: { format: "pcm", rate: 16000, bits: 16, channel: 1, encoding: "raw" },
        request: {
          model_name: "bigmodel",
          result_type: "full",
          enable_itn: true,
          show_utterances: false,
          vad_segment_duration: 400,
        },
      };
      this.ws?.send(buildFullClientRequest(config));
      this.configSent = true;
      for (const chunk of this.audioQueue) this.ws?.send(buildAudioRequest(chunk, false));
      this.audioQueue = [];
      this.callbacks?.onReady?.();
    });

    this.ws.on("message", (data: Buffer) => {
      const frame = parseServerResponse(data);
      if (!frame) return;

      if (frame.msgType === MSG_TYPE_ERROR) {
        const message = frame.errorMsg || `ASR error code: ${frame.errorCode}`;
        if (this.finishCalled && (this.lastText.trim() || this.finalTextFired)) return;
        this.callbacks?.onError?.(message);
        return;
      }

      if (frame.msgType !== MSG_TYPE_FULL_SERVER || !frame.payload) return;
      const text = frame.payload?.result?.text ?? "";

      if (text && text !== this.lastText) {
        this.lastText = text;
        if (frame.isLast) {
          this.finalTextFired = true;
          this.callbacks?.onFinal(text);
        } else {
          this.callbacks?.onPartial?.(text);
        }
      }

      if (frame.isLast) {
        if (!this.finalTextFired && this.lastText.trim()) {
          this.finalTextFired = true;
          this.callbacks?.onFinal(this.lastText);
        }
        this.fireEndedOnce();
      }
    });

    this.ws.on("error", (error) => {
      if (!this.closed && !this.suppressCloseCallbacks) this.callbacks?.onError?.(error.message);
    });

    this.ws.on("close", () => {
      this.configSent = false;
      this.speechDetected = false;
      if (this.suppressCloseCallbacks) return;
      if (!this.finalTextFired && this.lastText.trim()) {
        this.finalTextFired = true;
        this.callbacks?.onFinal(this.lastText);
      }
      this.fireEndedOnce();
    });
  }

  sendAudioChunk(pcm: Buffer): void {
    if (this.closed || this.finishCalled) return;
    if (!this.configSent) {
      this.audioQueue.push(pcm);
      return;
    }
    this.ws?.send(buildAudioRequest(pcm, false));
    const rms = this.computeRms(pcm);
    if (rms >= this.vadSpeechThreshold) {
      this.speechDetected = true;
      this.resetSilenceTimer();
      return;
    }
    if (this.speechDetected && this.silenceTimer === null) {
      this.silenceTimer = setTimeout(() => {
        this.silenceTimer = null;
        if (!this.closed && !this.finishCalled && this.configSent) this.finishSession();
      }, this.silenceTimeoutMs);
    }
  }

  finishSession(): void {
    if (!this.configSent || this.closed || this.finishCalled) return;
    this.finishCalled = true;
    this.resetSilenceTimer();
    this.ws?.send(buildAudioRequest(Buffer.alloc(0), true));
  }

  close(options: { suppressCallbacks?: boolean } = { suppressCallbacks: true }): void {
    this.closed = true;
    this.suppressCloseCallbacks = options.suppressCallbacks !== false;
    this.audioQueue = [];
    this.resetSilenceTimer();
    try {
      this.ws?.close();
    } catch {}
    this.ws = null;
  }

  private fireEndedOnce(): void {
    if (this.endedFired) return;
    this.endedFired = true;
    this.callbacks?.onEnded?.();
  }

  private resetSilenceTimer(): void {
    if (this.silenceTimer) clearTimeout(this.silenceTimer);
    this.silenceTimer = null;
  }

  private computeRms(pcm: Buffer): number {
    if (pcm.length < 2) return 0;
    let sum = 0;
    const samples = pcm.length >> 1;
    for (let i = 0; i < pcm.length - 1; i += 2) {
      const sample = pcm.readInt16LE(i) / 32768;
      sum += sample * sample;
    }
    return Math.sqrt(sum / Math.max(1, samples));
  }
}

