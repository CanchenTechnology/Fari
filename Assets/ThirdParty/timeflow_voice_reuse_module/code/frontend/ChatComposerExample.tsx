import { useState } from "react";
import { Phone, Send } from "lucide-react";
import { useAsrRecorder } from "./useAsrRecorder";
import { VoiceHoldInput, VoiceModeButton, type VoiceInputMode } from "./VoiceHoldInput";

export function ChatComposerExample({
  userId,
  disabled = false,
  inCall = false,
  onSend,
  onStartCall,
}: {
  userId: string | number;
  disabled?: boolean;
  inCall?: boolean;
  onSend: (text: string) => Promise<void> | void;
  onStartCall: () => void;
}) {
  const [mode, setMode] = useState<VoiceInputMode>("text");
  const [text, setText] = useState("");
  const asr = useAsrRecorder({
    userId,
    onTranscript: async (transcript) => {
      const clean = transcript.trim();
      if (!clean) return;
      await onSend(clean);
    },
    onError: (message) => window.alert(message),
  });

  const sendText = async () => {
    const clean = text.trim();
    if (!clean) return;
    setText("");
    await onSend(clean);
  };

  return (
    <div className="flex items-end gap-2 border-t border-[#EEE] bg-white px-3 py-2">
      <VoiceModeButton
        mode={mode}
        onToggle={() => {
          setMode((value) => (value === "voice" ? "text" : "voice"));
          if (mode === "voice") asr.disconnect();
          else asr.connect();
        }}
        disabled={disabled || inCall}
      />

      {mode === "voice" ? (
        <VoiceHoldInput
          asrState={asr.asrState}
          partialText={asr.partialText}
          disabled={disabled || inCall}
          disabledLabel={inCall ? "通话中" : "按住 说话"}
          onHoldStart={async () => {
            if (asr.asrState === "idle") asr.connect();
            window.setTimeout(() => asr.startRecording(), 0);
          }}
          onHoldEnd={({ cancelled }) => {
            if (cancelled) asr.cancelRecording();
            else asr.stopRecording();
          }}
        />
      ) : (
        <textarea
          value={text}
          onChange={(event) => setText(event.target.value)}
          rows={1}
          placeholder="说点什么..."
          className="min-h-11 flex-1 resize-none rounded-2xl border border-[#EBEBEB] bg-[#F7F7F7] px-4 py-3 text-sm outline-none"
        />
      )}

      <button
        type="button"
        onClick={mode === "text" && text.trim() ? sendText : onStartCall}
        disabled={disabled}
        className="flex h-11 w-11 flex-shrink-0 items-center justify-center rounded-2xl border border-[#EBEBEB] bg-[#F7F7F7]"
      >
        {mode === "text" && text.trim() ? <Send className="h-5 w-5" /> : <Phone className="h-5 w-5" />}
      </button>
    </div>
  );
}

