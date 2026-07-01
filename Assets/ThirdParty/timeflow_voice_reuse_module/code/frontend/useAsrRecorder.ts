import { useCallback, useEffect, useRef, useState } from "react";

export type AsrState = "idle" | "connecting" | "ready" | "recording" | "processing" | "error";

export type UseAsrRecorderOptions = {
  userId: string | number;
  endpoint?: string;
  onTranscript?: (text: string) => void;
  onPartial?: (text: string) => void;
  onError?: (message: string) => void;
};

const PUNCTUATION_ONLY_RE = /^[\s.,!?;:，。！？、；：“”"'‘’（）()【】[\]{}《》<>…·~\-—_]+$/;
const EMPTY_AUDIO_ERROR_RE = /Timeout waiting next packet|waiting next packet timeout|session has ended/i;
const CLIENT_VAD_SPEECH_THRESHOLD = 0.006;
const MIN_RECORDING_MS = 500;
const PROCESSING_TIMEOUT_MS = 12000;

function cleanText(text: string | undefined | null): string {
  const normalized = (text ?? "").replace(/\s+/g, " ").trim();
  if (!normalized) return "";
  return PUNCTUATION_ONLY_RE.test(normalized) ? "" : normalized;
}

function pcmRms(buffer: ArrayBuffer): number {
  if (buffer.byteLength < 2) return 0;
  const view = new DataView(buffer);
  const samples = Math.floor(buffer.byteLength / 2);
  let sum = 0;
  for (let i = 0; i < samples; i += 1) {
    const sample = view.getInt16(i * 2, true) / 32768;
    sum += sample * sample;
  }
  return Math.sqrt(sum / samples);
}

function buildWsUrl(endpoint: string, userId: string | number): string {
  if (/^wss?:\/\//.test(endpoint)) return `${endpoint}?userId=${userId}&mode=asr`;
  const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
  return `${protocol}//${window.location.host}${endpoint}?userId=${userId}&mode=asr`;
}

export function useAsrRecorder({
  userId,
  endpoint = "/api/voice/ws",
  onTranscript,
  onPartial,
  onError,
}: UseAsrRecorderOptions) {
  const [asrState, setAsrState] = useState<AsrState>("idle");
  const [partialText, setPartialText] = useState("");

  const wsRef = useRef<WebSocket | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const workletRef = useRef<AudioWorkletNode | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const sourceRef = useRef<MediaStreamAudioSourceNode | null>(null);
  const finalTextRef = useRef("");
  const speechDetectedRef = useRef(false);
  const startedAtRef = useRef(0);
  const processingTimerRef = useRef<number | null>(null);

  const clearProcessingTimer = useCallback(() => {
    if (processingTimerRef.current) {
      window.clearTimeout(processingTimerRef.current);
      processingTimerRef.current = null;
    }
  }, []);

  const cleanupAudio = useCallback(() => {
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

  const connect = useCallback(() => {
    if (wsRef.current?.readyState === WebSocket.OPEN) {
      setAsrState("ready");
      return;
    }

    setAsrState("connecting");
    setPartialText("");
    finalTextRef.current = "";

    const ws = new WebSocket(buildWsUrl(endpoint, userId));
    wsRef.current = ws;

    ws.onmessage = (event) => {
      try {
        const msg = JSON.parse(String(event.data)) as { type: string; text?: string; message?: string };
        if (msg.type === "session_ready") setAsrState("ready");
        if (msg.type === "asr_partial") {
          const text = cleanText(msg.text);
          setPartialText(text);
          onPartial?.(text);
        }
        if (msg.type === "asr_final") {
          const text = cleanText(msg.text);
          finalTextRef.current = text;
          setPartialText(text);
        }
        if (msg.type === "asr_ended") {
          const text = cleanText(finalTextRef.current);
          clearProcessingTimer();
          cleanupAudio();
          setPartialText("");
          setAsrState("ready");
          finalTextRef.current = "";
          speechDetectedRef.current = false;
          onTranscript?.(text);
        }
        if (msg.type === "error") {
          clearProcessingTimer();
          cleanupAudio();
          if (EMPTY_AUDIO_ERROR_RE.test(msg.message ?? "") && !cleanText(finalTextRef.current)) {
            setPartialText("");
            setAsrState("idle");
            speechDetectedRef.current = false;
            onTranscript?.("");
            return;
          }
          setAsrState("error");
          onError?.(msg.message ?? "语音识别错误");
        }
      } catch {
        // Ignore non-JSON frames.
      }
    };

    ws.onerror = () => {
      cleanupAudio();
      setAsrState("error");
      onError?.("语音识别连接失败");
    };

    ws.onclose = () => {
      if (wsRef.current === ws) {
        wsRef.current = null;
        setAsrState("idle");
      }
    };
  }, [endpoint, userId, cleanupAudio, clearProcessingTimer, onTranscript, onPartial, onError]);

  const disconnect = useCallback(() => {
    clearProcessingTimer();
    cleanupAudio();
    if (wsRef.current?.readyState === WebSocket.OPEN) {
      wsRef.current.send(JSON.stringify({ type: "finish_session" }));
      wsRef.current.close();
    }
    wsRef.current = null;
    setAsrState("idle");
    setPartialText("");
    finalTextRef.current = "";
    speechDetectedRef.current = false;
  }, [cleanupAudio, clearProcessingTimer]);

  const startRecording = useCallback(async () => {
    if (asrState !== "ready" || wsRef.current?.readyState !== WebSocket.OPEN) return;

    try {
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

      wsRef.current.send(JSON.stringify({ type: "start_asr" }));

      const audioContext = new AudioContext({ sampleRate: 16000 });
      audioContextRef.current = audioContext;
      await audioContext.audioWorklet.addModule("/pcm-processor.js");

      const worklet = new AudioWorkletNode(audioContext, "pcm-processor");
      workletRef.current = worklet;
      worklet.port.onmessage = (event: MessageEvent<ArrayBuffer>) => {
        const ws = wsRef.current;
        if (!ws || ws.readyState !== WebSocket.OPEN) return;
        if (pcmRms(event.data) >= CLIENT_VAD_SPEECH_THRESHOLD) speechDetectedRef.current = true;
        ws.send(event.data);
      };

      const source = audioContext.createMediaStreamSource(stream);
      sourceRef.current = source;
      source.connect(worklet);
      worklet.connect(audioContext.destination);

      startedAtRef.current = Date.now();
      finalTextRef.current = "";
      speechDetectedRef.current = false;
      setPartialText("");
      setAsrState("recording");
    } catch (error) {
      cleanupAudio();
      setAsrState("error");
      onError?.(error instanceof Error ? error.message : "无法访问麦克风");
    }
  }, [asrState, cleanupAudio, onError]);

  const stopRecording = useCallback(() => {
    if (asrState !== "recording") return;

    const duration = Date.now() - startedAtRef.current;
    if (duration < MIN_RECORDING_MS || !speechDetectedRef.current) {
      disconnect();
      onTranscript?.("");
      return;
    }

    wsRef.current?.send(JSON.stringify({ type: "audio_end" }));
    cleanupAudio();
    setAsrState("processing");
    clearProcessingTimer();
    processingTimerRef.current = window.setTimeout(() => {
      disconnect();
      onTranscript?.("");
    }, PROCESSING_TIMEOUT_MS);
  }, [asrState, cleanupAudio, clearProcessingTimer, disconnect, onTranscript]);

  const cancelRecording = useCallback(() => {
    wsRef.current?.send(JSON.stringify({ type: "finish_session" }));
    disconnect();
  }, [disconnect]);

  useEffect(() => () => disconnect(), [disconnect]);

  return {
    asrState,
    partialText,
    connect,
    disconnect,
    startRecording,
    stopRecording,
    cancelRecording,
  };
}

