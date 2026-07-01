import { AudioLines, Keyboard, Loader2, X } from "lucide-react";
import { useCallback, useRef, useState } from "react";
import { createPortal } from "react-dom";
import type { CSSProperties } from "react";
import type { AsrState } from "./useAsrRecorder";

export type VoiceInputMode = "text" | "voice";
export type VoiceHoldEndPayload = { cancelled: boolean; durationMs: number };

const noLongPressStyle: CSSProperties = {
  WebkitTouchCallout: "none",
  WebkitUserSelect: "none",
  userSelect: "none",
  touchAction: "none",
};

export function VoiceModeButton({
  mode,
  onToggle,
  disabled = false,
}: {
  mode: VoiceInputMode;
  onToggle: () => void;
  disabled?: boolean;
}) {
  const isVoice = mode === "voice";
  return (
    <button
      type="button"
      onClick={onToggle}
      disabled={disabled}
      className="h-11 w-11 flex-shrink-0 rounded-2xl border border-[#EBEBEB] bg-[#F7F7F7] text-[#1A1A1A] transition-all active:scale-95 disabled:opacity-40"
      aria-label={isVoice ? "切换文字输入" : "切换语音输入"}
      title={isVoice ? "切换文字输入" : "切换语音输入"}
    >
      <span className="flex h-full w-full items-center justify-center">
        {isVoice ? <Keyboard className="h-5 w-5" /> : <AudioLines className="h-5 w-5" />}
      </span>
    </button>
  );
}

export function VoiceHoldInput({
  asrState,
  partialText,
  disabled = false,
  disabledLabel = "按住 说话",
  onHoldStart,
  onHoldEnd,
}: {
  asrState: AsrState;
  partialText: string;
  disabled?: boolean;
  disabledLabel?: string;
  onHoldStart: () => void;
  onHoldEnd: (payload: VoiceHoldEndPayload) => void;
}) {
  const [pressing, setPressing] = useState(false);
  const [canceling, setCanceling] = useState(false);
  const pointerIdRef = useRef<number | null>(null);
  const startRef = useRef<{ x: number; y: number; at: number } | null>(null);
  const cancelingRef = useRef(false);

  const reset = useCallback(() => {
    pointerIdRef.current = null;
    startRef.current = null;
    cancelingRef.current = false;
    setPressing(false);
    setCanceling(false);
  }, []);

  const finish = useCallback(
    (forceCancel: boolean) => {
      const start = startRef.current;
      const durationMs = start ? Date.now() - start.at : 0;
      const cancelled = forceCancel || cancelingRef.current;
      reset();
      onHoldEnd({ cancelled, durationMs });
    },
    [onHoldEnd, reset]
  );

  const preparing = asrState === "connecting";
  const processing = asrState === "processing";
  const overlayVisible = pressing || processing;
  const transcript = partialText.trim();
  const label = disabled ? disabledLabel : preparing ? "正在准备..." : processing ? "正在整理..." : "按住 说话";
  const status = canceling ? "松手取消" : processing ? "正在整理" : transcript ? "正在转文字" : "正在听";
  const body = transcript || (processing ? "正在整理识别结果..." : "说话后会实时转成文字");

  const overlay =
    overlayVisible && typeof document !== "undefined"
      ? createPortal(
          <div
            className="fixed inset-0 z-[70]"
            style={{
              ...noLongPressStyle,
              backgroundColor: "rgba(0,0,0,0.58)",
              backdropFilter: "blur(1.5px)",
              WebkitBackdropFilter: "blur(1.5px)",
            }}
            onContextMenu={(event) => event.preventDefault()}
          >
            <div
              className="absolute left-0 right-0 px-4"
              style={{ bottom: "calc(126px + env(safe-area-inset-bottom, 0px))" }}
            >
              <div className="mx-auto w-full max-w-[360px] rounded-[14px] border border-[#E8E8E8] bg-white px-4 py-3 text-[#1A1A1A] shadow-[0_20px_60px_rgba(0,0,0,0.30)]">
                <div className="flex items-start gap-3">
                  <div className="mt-1 flex h-8 w-8 flex-shrink-0 items-end justify-center gap-0.5 rounded-[10px] bg-[#F7F7F7] text-[#777]">
                    <span className="mb-2 h-2.5 w-0.5 animate-pulse rounded-full bg-current" />
                    <span className="mb-2 h-4 w-0.5 rounded-full bg-current" />
                    <span className="mb-2 h-3 w-0.5 rounded-full bg-current" />
                    <span className="mb-2 h-5 w-0.5 rounded-full bg-current" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="text-xs font-medium leading-5 text-[#888]">{status}</div>
                    <div className="mt-1 max-h-24 min-h-[28px] overflow-y-auto whitespace-pre-wrap break-words text-[15px] leading-6">
                      {body}
                    </div>
                  </div>
                </div>
                <div className="mt-3 flex items-center justify-between border-t border-[#F0F0F0] pt-2 text-xs font-medium text-[#888]">
                  <span className={canceling ? "inline-flex items-center gap-1.5 text-[#1A1A1A]" : "inline-flex items-center gap-1.5"}>
                    <X className="h-3.5 w-3.5" />
                    上滑取消
                  </span>
                  <span>{canceling ? "松手取消" : "松手发送"}</span>
                </div>
              </div>
            </div>
          </div>,
          document.body
        )
      : null;

  return (
    <>
      <button
        type="button"
        disabled={disabled || preparing || processing}
        onContextMenu={(event) => event.preventDefault()}
        onPointerDown={(event) => {
          if (disabled || preparing || processing) return;
          event.preventDefault();
          pointerIdRef.current = event.pointerId;
          startRef.current = { x: event.clientX, y: event.clientY, at: Date.now() };
          cancelingRef.current = false;
          setCanceling(false);
          setPressing(true);
          event.currentTarget.setPointerCapture?.(event.pointerId);
          onHoldStart();
        }}
        onPointerMove={(event) => {
          const start = startRef.current;
          if (!start || pointerIdRef.current !== event.pointerId) return;
          const dx = event.clientX - start.x;
          const dy = event.clientY - start.y;
          const nextCanceling = dy < -78 || dx < -110;
          cancelingRef.current = nextCanceling;
          setCanceling(nextCanceling);
        }}
        onPointerUp={(event) => {
          if (pointerIdRef.current !== event.pointerId) return;
          event.currentTarget.releasePointerCapture?.(event.pointerId);
          finish(false);
        }}
        onPointerCancel={() => finish(true)}
        className={`h-11 flex-1 select-none rounded-2xl border text-sm font-medium transition-all active:scale-[0.99] disabled:opacity-50 ${
          pressing ? "border-[#1A1A1A] bg-[#1A1A1A] text-white" : "border-[#EBEBEB] bg-[#F7F7F7] text-[#1A1A1A]"
        }`}
        style={noLongPressStyle}
      >
        {preparing || processing ? (
          <span className="inline-flex items-center justify-center gap-2">
            <Loader2 className="h-4 w-4 animate-spin" />
            {label}
          </span>
        ) : (
          label
        )}
      </button>
      {overlay}
    </>
  );
}

