import { useCallback, useEffect, useRef, useState } from "react";

export type VoiceCallState = "idle" | "connecting" | "listening" | "thinking" | "speaking" | "error";

export type VoiceScope = {
  scopeType?: "global" | "task" | "project" | "document" | string;
  scopeId?: string | null;
};

export type UseVoiceCallOptions = {
  userId: string | number;
  endpoint?: string;
  scope?: VoiceScope;
  onUserText?: (text: string) => void;
  onAssistantPartial?: (text: string) => void;
  onAssistantDone?: (text: string, toolResults?: unknown[]) => void;
  onError?: (message: string) => void;
};

const PUNCTUATION_ONLY_RE = /^[\s.,!?;:，。！？、；：“”"'‘’（）()【】[\]{}《》<>…·~\-—_]+$/;

const TTS_ECHO_MIN_MIC_RMS = 0.045;
const TTS_ECHO_LOW_TTS_RMS = 0.018;
const TTS_ECHO_RATIO = 1.75;
const TTS_ECHO_MARGIN = 0.014;
const TTS_BARGE_IN_CONSECUTIVE_FRAMES = 6;
const TTS_BARGE_IN_COOLDOWN_MS = 1200;
const TTS_BARGE_IN_UPLOAD_MS = 2600;
const TTS_ECHO_TAIL_GUARD_MS = 650;

function cleanText(text: string | undefined | null): string {
  const normalized = (text ?? "").replace(/\s+/g, " ").trim();
  if (!normalized) return "";
  return PUNCTUATION_ONLY_RE.test(normalized) ? "" : normalized;
}

function pcmRms(buffer: ArrayBuffer): number {
  if (buffer.byteLength < 2) return 0;
  const samples = new Int16Array(buffer);
  let sum = 0;
  for (let i = 0; i < samples.length; i += 1) {
    const sample = (samples[i] ?? 0) / 32768;
    sum += sample * sample;
  }
  return Math.sqrt(sum / Math.max(1, samples.length));
}

function looksLikeHumanBargeIn(micRms: number, ttsRms: number): boolean {
  if (micRms < TTS_ECHO_MIN_MIC_RMS) return false;
  if (ttsRms < TTS_ECHO_LOW_TTS_RMS) return true;
  return micRms > Math.max(TTS_ECHO_MIN_MIC_RMS, ttsRms * TTS_ECHO_RATIO + TTS_ECHO_MARGIN);
}

function buildWsUrl(endpoint: string, userId: string | number, scope?: VoiceScope): string {
  const params = new URLSearchParams({ userId: String(userId) });
  if (scope?.scopeType) params.set("scopeType", scope.scopeType);
  if (scope?.scopeId) params.set("scopeId", scope.scopeId);
  if (/^wss?:\/\//.test(endpoint)) return `${endpoint}?${params.toString()}`;
  const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
  return `${protocol}//${window.location.host}${endpoint}?${params.toString()}`;
}

function base64PcmToAudioBuffer(audioContext: AudioContext, base64: string, sampleRate = 24000): AudioBuffer {
  const binary = atob(base64);
  const pcm = new Int16Array(binary.length / 2);
  for (let i = 0; i < pcm.length; i += 1) {
    const lo = binary.charCodeAt(i * 2);
    const hi = binary.charCodeAt(i * 2 + 1);
    const value = (hi << 8) | lo;
    pcm[i] = value >= 0x8000 ? value - 0x10000 : value;
  }
  const audioBuffer = audioContext.createBuffer(1, pcm.length, sampleRate);
  const channel = audioBuffer.getChannelData(0);
  for (let i = 0; i < pcm.length; i += 1) channel[i] = (pcm[i] ?? 0) / 32768;
  return audioBuffer;
}

export function useVoiceCall({
  userId,
  endpoint = "/api/voice/ws",
  scope,
  onUserText,
  onAssistantPartial,
  onAssistantDone,
  onError,
}: UseVoiceCallOptions) {
  const [state, setState] = useState<VoiceCallState>("idle");
  const [asrText, setAsrText] = useState("");
  const [assistantText, setAssistantText] = useState("");

  const wsRef = useRef<WebSocket | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const workletRef = useRef<AudioWorkletNode | null>(null);
  const sourceRef = useRef<MediaStreamAudioSourceNode | null>(null);
  const streamRef = useRef<MediaStream | null>(null);

  const ttsContextRef = useRef<AudioContext | null>(null);
  const ttsAnalyserRef = useRef<AnalyserNode | null>(null);
  const ttsSourceRef = useRef<AudioBufferSourceNode | null>(null);
  const ttsQueueRef = useRef<AudioBuffer[]>([]);
  const ttsPlayingRef = useRef(false);
  const ttsEndedRef = useRef(false);
  const pendingDoneRef = useRef<{ text: string; toolResults?: unknown[] } | null>(null);

  const bargeInFramesRef = useRef(0);
  const uploadUntilRef = useRef(0);
  const lastBargeInAtRef = useRef(0);
  const echoGuardUntilRef = useRef(0);

  const getTtsContext = useCallback(() => {
    if (!ttsContextRef.current || ttsContextRef.current.state === "closed") {
      ttsContextRef.current = new AudioContext({ sampleRate: 24000 });
      const analyser = ttsContextRef.current.createAnalyser();
      analyser.fftSize = 256;
      ttsAnalyserRef.current = analyser;
    }
    if (ttsContextRef.current.state === "suspended") ttsContextRef.current.resume().catch(() => {});
    return ttsContextRef.current;
  }, []);

  const getTtsRms = useCallback(() => {
    const analyser = ttsAnalyserRef.current;
    if (!analyser) return 0;
    const data = new Uint8Array(analyser.frequencyBinCount);
    analyser.getByteTimeDomainData(data);
    let sum = 0;
    for (const value of data) {
      const normalized = (value - 128) / 128;
      sum += normalized * normalized;
    }
    return Math.sqrt(sum / Math.max(1, data.length));
  }, []);

  const stopTtsPlayback = useCallback(() => {
    try {
      ttsSourceRef.current?.stop();
    } catch {}
    ttsSourceRef.current = null;
    ttsQueueRef.current = [];
    ttsPlayingRef.current = false;
    ttsEndedRef.current = true;
    echoGuardUntilRef.current = Date.now() + TTS_ECHO_TAIL_GUARD_MS;
  }, []);

  const playNextTts = useCallback(() => {
    const context = getTtsContext();
    if (ttsPlayingRef.current) return;
    const next = ttsQueueRef.current.shift();
    if (!next) {
      if (ttsEndedRef.current) {
        setState("listening");
        const done = pendingDoneRef.current;
        pendingDoneRef.current = null;
        if (done) onAssistantDone?.(done.text, done.toolResults);
      }
      return;
    }

    const source = context.createBufferSource();
    source.buffer = next;
    const analyser = ttsAnalyserRef.current;
    if (analyser) {
      source.connect(analyser);
      analyser.connect(context.destination);
    } else {
      source.connect(context.destination);
    }

    ttsPlayingRef.current = true;
    ttsSourceRef.current = source;
    source.onended = () => {
      if (ttsSourceRef.current === source) ttsSourceRef.current = null;
      ttsPlayingRef.current = false;
      playNextTts();
    };
    source.start();
  }, [getTtsContext, onAssistantDone]);

  const cleanupMic = useCallback(() => {
    workletRef.current?.disconnect();
    workletRef.current?.port.close();
    workletRef.current = null;
    sourceRef.current?.disconnect();
    sourceRef.current = null;
    streamRef.current?.getTracks().forEach((track) => track.stop());
    streamRef.current = null;
    if (audioContextRef.current && audioContextRef.current.state !== "closed") {
      audioContextRef.current.close().catch(() => {});
    }
    audioContextRef.current = null;
  }, []);

  const startMic = useCallback(async () => {
    const ws = wsRef.current;
    if (!ws || ws.readyState !== WebSocket.OPEN) return;

    const stream = await navigator.mediaDevices.getUserMedia({
      audio: {
        channelCount: 1,
        sampleRate: 16000,
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
      },
    });
    streamRef.current = stream;
    const audioContext = new AudioContext({ sampleRate: 16000 });
    audioContextRef.current = audioContext;
    await audioContext.audioWorklet.addModule("/pcm-processor.js");
    const worklet = new AudioWorkletNode(audioContext, "pcm-processor");
    workletRef.current = worklet;

    worklet.port.onmessage = (event: MessageEvent<ArrayBuffer>) => {
      const socket = wsRef.current;
      if (!socket || socket.readyState !== WebSocket.OPEN) return;

      const now = Date.now();
      const micRms = pcmRms(event.data);
      const ttsRms = getTtsRms();
      const ttsActive = ttsPlayingRef.current || state === "speaking" || now < echoGuardUntilRef.current;

      if (ttsActive && now > uploadUntilRef.current) {
        if (now - lastBargeInAtRef.current > TTS_BARGE_IN_COOLDOWN_MS && looksLikeHumanBargeIn(micRms, ttsRms)) {
          bargeInFramesRef.current += 1;
        } else {
          bargeInFramesRef.current = 0;
        }
        if (bargeInFramesRef.current >= TTS_BARGE_IN_CONSECUTIVE_FRAMES) {
          lastBargeInAtRef.current = now;
          uploadUntilRef.current = now + TTS_BARGE_IN_UPLOAD_MS;
          bargeInFramesRef.current = 0;
          stopTtsPlayback();
          socket.send(JSON.stringify({ type: "interrupt", source: "client_barge_in", micRms, ttsRms }));
          setState("listening");
          socket.send(event.data);
        }
        return;
      }

      socket.send(event.data);
    };

    const source = audioContext.createMediaStreamSource(stream);
    sourceRef.current = source;
    source.connect(worklet);
    worklet.connect(audioContext.destination);
  }, [getTtsRms, state, stopTtsPlayback]);

  const startCall = useCallback(async () => {
    if (state !== "idle" && state !== "error") return;
    setState("connecting");
    setAsrText("");
    setAssistantText("");

    const ws = new WebSocket(buildWsUrl(endpoint, userId, scope));
    wsRef.current = ws;

    ws.onmessage = async (event) => {
      try {
        const msg = JSON.parse(String(event.data)) as { type: string; text?: string; data?: string; message?: string; toolResults?: unknown[] };
        if (msg.type === "session_ready") {
          await startMic();
          setState("listening");
        }
        if (msg.type === "asr_partial") setAsrText(cleanText(msg.text));
        if (msg.type === "asr_final") {
          const text = cleanText(msg.text);
          setAsrText(text);
          if (text) onUserText?.(text);
        }
        if (msg.type === "asr_ended") setState("thinking");
        if (msg.type === "chat_partial") {
          const text = msg.text ?? "";
          setAssistantText(text);
          onAssistantPartial?.(text);
        }
        if (msg.type === "tts_audio" && msg.data) {
          ttsEndedRef.current = false;
          setState("speaking");
          const buffer = base64PcmToAudioBuffer(getTtsContext(), msg.data);
          ttsQueueRef.current.push(buffer);
          playNextTts();
        }
        if (msg.type === "tts_end") {
          ttsEndedRef.current = true;
          playNextTts();
        }
        if (msg.type === "chat_ended") {
          pendingDoneRef.current = { text: msg.text ?? "", toolResults: msg.toolResults };
          if (!ttsPlayingRef.current && ttsQueueRef.current.length === 0 && ttsEndedRef.current) {
            const done = pendingDoneRef.current;
            pendingDoneRef.current = null;
            if (done) onAssistantDone?.(done.text, done.toolResults);
            setState("listening");
          }
        }
        if (msg.type === "error") {
          setState("error");
          onError?.(msg.message ?? "语音通话错误");
        }
      } catch {
        // Ignore non-JSON frames.
      }
    };

    ws.onerror = () => {
      setState("error");
      onError?.("语音通话连接失败");
    };

    ws.onclose = () => {
      cleanupMic();
      stopTtsPlayback();
      if (wsRef.current === ws) wsRef.current = null;
      setState("idle");
    };
  }, [state, endpoint, userId, scope, startMic, cleanupMic, stopTtsPlayback, getTtsContext, playNextTts, onUserText, onAssistantPartial, onAssistantDone, onError]);

  const hangUp = useCallback(() => {
    try {
      wsRef.current?.send(JSON.stringify({ type: "finish_session" }));
      wsRef.current?.close();
    } catch {}
    wsRef.current = null;
    cleanupMic();
    stopTtsPlayback();
    setState("idle");
  }, [cleanupMic, stopTtsPlayback]);

  useEffect(() => () => hangUp(), [hangUp]);

  return {
    state,
    asrText,
    assistantText,
    startCall,
    hangUp,
    isInCall: state !== "idle" && state !== "error",
  };
}

