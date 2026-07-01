using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DialogVoiceCallController : MonoBehaviour
{
    public enum VoiceCallState
    {
        Idle,
        Starting,
        Listening,
        Capturing,
        Recognizing,
        Thinking,
        Speaking,
        Error
    }

    [Header("Capture")]
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private int microphoneBufferSeconds = 60;
    [SerializeField] private float speechStartRms = 0.018f;
    [SerializeField] private float silenceRms = 0.010f;
    [SerializeField] private float speechStartSeconds = 0.12f;
    [SerializeField] private float endSilenceSeconds = 1.05f;
    [SerializeField] private float minUtteranceSeconds = 0.55f;
    [SerializeField] private float maxUtteranceSeconds = 14f;
    [SerializeField] private float preRollSeconds = 0.28f;

    [Header("Barge In")]
    [SerializeField] private float bargeInRms = 0.075f;
    [SerializeField] private float bargeInSeconds = 0.18f;

    public event Action<VoiceCallState, string> StateChanged;
    public event Action<string> Error;

    public VoiceCallState State { get; private set; } = VoiceCallState.Idle;
    public bool IsActive => State != VoiceCallState.Idle && State != VoiceCallState.Error;

    private DialogUI dialogUI;
    private VoiceAsrClient asrClient;
    private AudioClip microphoneClip;
    private string microphoneDevice;
    private int lastMicPosition;
    private bool stopRequested;
    private bool waitingForAsr;
    private readonly List<float> captureSamples = new List<float>(16000 * 16);
    private readonly List<float> preRollSamples = new List<float>(16000);
    private float speechAccumulatedSeconds;
    private float bargeInAccumulatedSeconds;
    private float lastVoiceRealtime;

    public void Configure(DialogUI owner, VoiceAsrClient client)
    {
        dialogUI = owner != null ? owner : dialogUI;
        asrClient = client != null ? client : asrClient;
    }

    public void ToggleCall()
    {
        if (IsActive)
            StopCall();
        else
            StartCall();
    }

    public void StartCall()
    {
        if (IsActive || State == VoiceCallState.Starting)
            return;

        stopRequested = false;
        StartCoroutine(StartCallRoutine());
    }

    public void StopCall()
    {
        stopRequested = true;
        StopMicrophone();
        captureSamples.Clear();
        preRollSamples.Clear();
        waitingForAsr = false;
        dialogUI?.StopVoiceCallSpeech();
        SetState(VoiceCallState.Idle, "通话已结束");
    }

    private IEnumerator StartCallRoutine()
    {
        SetState(VoiceCallState.Starting, "正在启动通话");

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                RaiseError("没有麦克风权限");
                yield break;
            }
        }

        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            RaiseError("没有可用麦克风");
            yield break;
        }

        microphoneDevice = Microphone.devices[0];
        microphoneClip = Microphone.Start(microphoneDevice, true, Mathf.Max(5, microphoneBufferSeconds), sampleRate);
        if (microphoneClip == null)
        {
            RaiseError("麦克风启动失败");
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + 1.5f;
        while (Microphone.GetPosition(microphoneDevice) <= 0 && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (Microphone.GetPosition(microphoneDevice) <= 0)
        {
            StopMicrophone();
            RaiseError("麦克风没有返回声音");
            yield break;
        }

        lastMicPosition = Microphone.GetPosition(microphoneDevice);
        speechAccumulatedSeconds = 0f;
        bargeInAccumulatedSeconds = 0f;
        captureSamples.Clear();
        preRollSamples.Clear();
        SetState(VoiceCallState.Listening, "正在聆听");
    }

    private void Update()
    {
        if (!IsActive || microphoneClip == null || string.IsNullOrEmpty(microphoneDevice))
            return;

        float[] samples = ReadNewMicrophoneSamples();
        if (samples == null || samples.Length == 0)
            return;

        float rms = DialogVoiceAudioUtility.ComputeRms(samples);
        float duration = samples.Length / (float)Mathf.Max(1, sampleRate);

        switch (State)
        {
            case VoiceCallState.Listening:
                HandleListeningSamples(samples, rms, duration);
                break;
            case VoiceCallState.Capturing:
                HandleCapturingSamples(samples, rms);
                break;
            case VoiceCallState.Speaking:
                HandleSpeakingSamples(samples, rms, duration);
                break;
        }
    }

    private void HandleListeningSamples(float[] samples, float rms, float duration)
    {
        AppendPreRoll(samples);
        if (rms >= speechStartRms)
        {
            speechAccumulatedSeconds += duration;
            if (speechAccumulatedSeconds >= speechStartSeconds)
                BeginCaptureFromPreRoll();
        }
        else
        {
            speechAccumulatedSeconds = 0f;
        }
    }

    private void HandleCapturingSamples(float[] samples, float rms)
    {
        captureSamples.AddRange(samples);

        if (rms >= silenceRms)
            lastVoiceRealtime = Time.realtimeSinceStartup;

        float utteranceSeconds = captureSamples.Count / (float)Mathf.Max(1, sampleRate);
        bool reachedSilence = Time.realtimeSinceStartup - lastVoiceRealtime >= endSilenceSeconds;
        bool reachedMax = utteranceSeconds >= maxUtteranceSeconds;
        if (reachedSilence || reachedMax)
            FinishCapture();
    }

    private void HandleSpeakingSamples(float[] samples, float rms, float duration)
    {
        if (!dialogUI.IsVoiceCallSpeechPlaying())
        {
            SetState(VoiceCallState.Listening, "正在聆听");
            return;
        }

        if (rms >= bargeInRms)
        {
            bargeInAccumulatedSeconds += duration;
            if (bargeInAccumulatedSeconds >= bargeInSeconds)
            {
                dialogUI.StopVoiceCallSpeech();
                BeginCapture(samples);
            }
        }
        else
        {
            bargeInAccumulatedSeconds = 0f;
        }
    }

    private void BeginCaptureFromPreRoll()
    {
        float[] seed = preRollSamples.ToArray();
        BeginCapture(seed);
    }

    private void BeginCapture(float[] seed)
    {
        if (waitingForAsr || State == VoiceCallState.Recognizing || State == VoiceCallState.Thinking)
            return;

        captureSamples.Clear();
        if (seed != null && seed.Length > 0)
            captureSamples.AddRange(seed);

        preRollSamples.Clear();
        speechAccumulatedSeconds = 0f;
        bargeInAccumulatedSeconds = 0f;
        lastVoiceRealtime = Time.realtimeSinceStartup;
        SetState(VoiceCallState.Capturing, "正在聆听");
    }

    private void FinishCapture()
    {
        if (waitingForAsr)
            return;

        float utteranceSeconds = captureSamples.Count / (float)Mathf.Max(1, sampleRate);
        if (utteranceSeconds < minUtteranceSeconds)
        {
            captureSamples.Clear();
            SetState(VoiceCallState.Listening, "正在聆听");
            return;
        }

        byte[] pcm16 = DialogVoiceAudioUtility.FloatSamplesToPcm16(captureSamples, out float rms);
        captureSamples.Clear();
        if (pcm16 == null || pcm16.Length == 0 || rms < silenceRms)
        {
            SetState(VoiceCallState.Listening, "正在聆听");
            return;
        }

        if (asrClient == null)
            asrClient = GetComponent<VoiceAsrClient>();
        if (asrClient == null)
            asrClient = gameObject.AddComponent<VoiceAsrClient>();

        waitingForAsr = true;
        SetState(VoiceCallState.Recognizing, "正在识别");
        asrClient.TranscribePcm16(pcm16, sampleRate,
            transcript =>
            {
                waitingForAsr = false;
                HandleTranscript(transcript);
            },
            message =>
            {
                waitingForAsr = false;
                RaiseRecoverableError(message);
            });
    }

    private void HandleTranscript(string transcript)
    {
        if (stopRequested || !IsActive) return;

        string clean = DialogVoiceAudioUtility.CleanTranscript(transcript);
        if (string.IsNullOrWhiteSpace(clean))
        {
            SetState(VoiceCallState.Listening, "正在聆听");
            return;
        }

        SetState(VoiceCallState.Thinking, "正在思考");
        bool sent = dialogUI != null && dialogUI.SendVoiceCallMessage(clean,
            (messageIndex, fullText) =>
            {
                if (stopRequested || !IsActive) return;
                PlayAssistantReply(messageIndex, fullText);
            },
            error =>
            {
                if (stopRequested || !IsActive) return;
                RaiseRecoverableError(error);
            });

        if (!sent)
            SetState(VoiceCallState.Listening, "正在聆听");
    }

    private void PlayAssistantReply(int messageIndex, string fullText)
    {
        if (stopRequested || !IsActive) return;

        if (string.IsNullOrWhiteSpace(fullText))
        {
            SetState(VoiceCallState.Listening, "正在聆听");
            return;
        }

        SetState(VoiceCallState.Speaking, "正在回应");
        bool started = dialogUI != null && dialogUI.PlayVoiceCallResponse(messageIndex,
            () =>
            {
                if (stopRequested || !IsActive) return;
                SetState(VoiceCallState.Listening, "正在聆听");
            },
            error =>
            {
                if (stopRequested || !IsActive) return;
                RaiseRecoverableError(error);
            });

        if (!started)
            SetState(VoiceCallState.Listening, "正在聆听");
    }

    private float[] ReadNewMicrophoneSamples()
    {
        if (microphoneClip == null || string.IsNullOrEmpty(microphoneDevice))
            return null;

        int position = Microphone.GetPosition(microphoneDevice);
        if (position < 0 || position == lastMicPosition)
            return null;

        int totalFrames = microphoneClip.samples;
        float[] result;
        if (position > lastMicPosition)
        {
            result = ReadMonoSegment(lastMicPosition, position - lastMicPosition);
        }
        else
        {
            float[] tail = ReadMonoSegment(lastMicPosition, totalFrames - lastMicPosition);
            float[] head = ReadMonoSegment(0, position);
            result = new float[(tail?.Length ?? 0) + (head?.Length ?? 0)];
            if (tail != null) Array.Copy(tail, 0, result, 0, tail.Length);
            if (head != null) Array.Copy(head, 0, result, tail?.Length ?? 0, head.Length);
        }

        lastMicPosition = position;
        return result;
    }

    private float[] ReadMonoSegment(int startFrame, int frameCount)
    {
        if (microphoneClip == null || frameCount <= 0)
            return null;

        int channels = Mathf.Max(1, microphoneClip.channels);
        float[] raw = new float[frameCount * channels];
        microphoneClip.GetData(raw, startFrame);
        if (channels == 1)
            return raw;

        float[] mono = new float[frameCount];
        for (int frame = 0; frame < frameCount; frame++)
        {
            float sum = 0f;
            int baseIndex = frame * channels;
            for (int channel = 0; channel < channels; channel++)
                sum += raw[baseIndex + channel];
            mono[frame] = sum / channels;
        }
        return mono;
    }

    private void AppendPreRoll(float[] samples)
    {
        if (samples == null || samples.Length == 0)
            return;

        preRollSamples.AddRange(samples);
        int maxSamples = Mathf.RoundToInt(preRollSeconds * sampleRate);
        if (preRollSamples.Count > maxSamples)
            preRollSamples.RemoveRange(0, preRollSamples.Count - maxSamples);
    }

    private void StopMicrophone()
    {
        if (!string.IsNullOrEmpty(microphoneDevice) && Microphone.IsRecording(microphoneDevice))
            Microphone.End(microphoneDevice);

        microphoneClip = null;
        microphoneDevice = null;
        lastMicPosition = 0;
    }

    private void RaiseRecoverableError(string message)
    {
        Error?.Invoke(string.IsNullOrWhiteSpace(message) ? "语音通话失败" : message);
        SetState(VoiceCallState.Listening, "正在聆听");
    }

    private void RaiseError(string message)
    {
        StopMicrophone();
        Error?.Invoke(string.IsNullOrWhiteSpace(message) ? "语音通话失败" : message);
        SetState(VoiceCallState.Error, message);
    }

    private void SetState(VoiceCallState state, string status)
    {
        if (State == state && string.IsNullOrEmpty(status))
            return;

        State = state;
        StateChanged?.Invoke(State, status ?? "");
        Debug.Log($"[DialogVoiceCall] {State}: {status}");
    }

    private void OnDisable()
    {
        if (IsActive)
            StopCall();
    }
}
