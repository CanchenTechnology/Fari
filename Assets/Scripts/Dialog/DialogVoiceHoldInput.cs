using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Press-and-hold voice input for DialogUI.
/// Short tap falls through to the normal Button click; a longer hold records and transcribes speech.
/// </summary>
public class DialogVoiceHoldInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Bindings")]
    [SerializeField] private Button holdButton;
    [SerializeField] private TMP_InputField previewInputField;
    [SerializeField] private VoiceAsrClient asrClient;

    [Header("Recording")]
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private int maxRecordingSeconds = 15;
    [SerializeField] private float holdActivationDelay = 0.28f;
    [SerializeField] private float minRecordSeconds = 0.45f;
    [SerializeField] private float minRms = 0.006f;
    [SerializeField] private bool cancelOnPointerExit;

    public event Action RecordingStarted;
    public event Action RecordingStopped;
    public event Action<string> TranscriptFinal;
    public event Action<string> Error;
    public event Action<string> StatusChanged;

    public bool IsRecording { get; private set; }
    public bool IsTranscribing => asrClient != null && asrClient.IsTranscribing;

    private bool pointerDown;
    private bool suppressNextClick;
    private bool placeholderCaptured;
    private string originalPlaceholder = "";
    private AudioClip recordingClip;
    private Coroutine activationCoroutine;
    private Coroutine maxRecordingCoroutine;
    private float recordingStartedAt;
    private string microphoneDevice;

    public void Configure(Button button, TMP_InputField inputField, VoiceAsrClient client)
    {
        holdButton = button != null ? button : holdButton;
        previewInputField = inputField != null ? inputField : previewInputField;
        asrClient = client != null ? client : asrClient;
        CapturePlaceholder();
        SetIdleStatus();
    }

    public bool ConsumePendingClickSuppression()
    {
        if (!suppressNextClick) return false;
        suppressNextClick = false;
        return true;
    }

    public void CancelRecording()
    {
        StopActivationCoroutine();

        if (!IsRecording)
        {
            pointerDown = false;
            SetIdleStatus();
            return;
        }

        pointerDown = false;
        StopMaxRecordingCoroutine();
        EndMicrophone();
        IsRecording = false;
        recordingClip = null;
        RecordingStopped?.Invoke();
        SetStatus("已取消语音输入");
        SetIdleStatus();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanBeginHold()) return;

        pointerDown = true;
        StopActivationCoroutine();
        activationCoroutine = StartCoroutine(ActivateAfterDelay());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerDown = false;

        if (activationCoroutine != null)
        {
            StopActivationCoroutine();
            return;
        }

        if (!IsRecording) return;

        suppressNextClick = true;
        StopRecordingAndTranscribe();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!cancelOnPointerExit) return;
        if (IsRecording) CancelRecording();
    }

    private bool CanBeginHold()
    {
        if (!isActiveAndEnabled) return false;
        if (IsRecording || IsTranscribing) return false;
        if (holdButton != null && !holdButton.interactable) return false;
        return true;
    }

    private IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, holdActivationDelay));
        activationCoroutine = null;

        if (!pointerDown || !CanBeginHold())
            yield break;

        suppressNextClick = true;
        yield return StartCoroutine(StartRecording());
    }

    private IEnumerator StartRecording()
    {
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
        recordingClip = Microphone.Start(microphoneDevice, false, Mathf.Max(1, maxRecordingSeconds), sampleRate);
        if (recordingClip == null)
        {
            RaiseError("麦克风启动失败");
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + 1.5f;
        while (Microphone.GetPosition(microphoneDevice) <= 0 && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (Microphone.GetPosition(microphoneDevice) <= 0)
        {
            EndMicrophone();
            recordingClip = null;
            RaiseError("麦克风没有返回声音");
            yield break;
        }

        recordingStartedAt = Time.realtimeSinceStartup;
        IsRecording = true;
        RecordingStarted?.Invoke();
        SetStatus("正在聆听，松开发送");

        StopMaxRecordingCoroutine();
        maxRecordingCoroutine = StartCoroutine(AutoStopAtMaxDuration());
    }

    private IEnumerator AutoStopAtMaxDuration()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(1, maxRecordingSeconds));
        if (IsRecording)
        {
            pointerDown = false;
            StopRecordingAndTranscribe();
        }
    }

    private void StopRecordingAndTranscribe()
    {
        StopActivationCoroutine();
        StopMaxRecordingCoroutine();

        int position = recordingClip != null ? Microphone.GetPosition(microphoneDevice) : 0;
        AudioClip clip = recordingClip;
        EndMicrophone();

        float duration = Time.realtimeSinceStartup - recordingStartedAt;
        IsRecording = false;
        recordingClip = null;
        RecordingStopped?.Invoke();

        if (clip == null || position <= 0 || duration < minRecordSeconds)
        {
            RaiseError("说话时间太短");
            return;
        }

        byte[] pcm16 = DialogVoiceAudioUtility.AudioClipToPcm16(clip, position, out float rms);
        if (pcm16 == null || pcm16.Length == 0)
        {
            RaiseError("没有录到有效声音");
            return;
        }

        if (rms < minRms)
        {
            RaiseError("没有检测到有效声音");
            return;
        }

        if (asrClient == null)
            asrClient = GetComponentInParent<VoiceAsrClient>();
        if (asrClient == null)
            asrClient = gameObject.AddComponent<VoiceAsrClient>();

        SetStatus("正在识别...");
        asrClient.TranscribePcm16(pcm16, sampleRate,
            transcript =>
            {
                string clean = DialogVoiceAudioUtility.CleanTranscript(transcript);
                if (string.IsNullOrWhiteSpace(clean))
                {
                    RaiseError("没有识别到内容");
                    return;
                }

                SetIdleStatus();
                TranscriptFinal?.Invoke(clean);
            },
            message =>
            {
                RaiseError(message);
            });
    }

    private void RaiseError(string message)
    {
        SetIdleStatus();
        Error?.Invoke(string.IsNullOrWhiteSpace(message) ? "语音输入失败" : message);
    }

    private void SetStatus(string text)
    {
        StatusChanged?.Invoke(text);
        SetPlaceholder(text);
    }

    private void SetIdleStatus()
    {
        SetPlaceholder(originalPlaceholder);
        StatusChanged?.Invoke("");
    }

    private void CapturePlaceholder()
    {
        if (placeholderCaptured) return;
        TMP_Text placeholder = previewInputField != null ? previewInputField.placeholder as TMP_Text : null;
        originalPlaceholder = placeholder != null ? placeholder.text : "";
        placeholderCaptured = true;
    }

    private void SetPlaceholder(string text)
    {
        CapturePlaceholder();
        TMP_Text placeholder = previewInputField != null ? previewInputField.placeholder as TMP_Text : null;
        if (placeholder != null)
            placeholder.text = text ?? "";
    }

    private void StopActivationCoroutine()
    {
        if (activationCoroutine == null) return;
        StopCoroutine(activationCoroutine);
        activationCoroutine = null;
    }

    private void StopMaxRecordingCoroutine()
    {
        if (maxRecordingCoroutine == null) return;
        StopCoroutine(maxRecordingCoroutine);
        maxRecordingCoroutine = null;
    }

    private void EndMicrophone()
    {
        if (!string.IsNullOrEmpty(microphoneDevice) && Microphone.IsRecording(microphoneDevice))
            Microphone.End(microphoneDevice);
    }

    private void OnDisable()
    {
        CancelRecording();
    }
}
