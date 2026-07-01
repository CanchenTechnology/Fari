using System;
using System.Collections;
using System.Text;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Unity client for the dialog ASR Cloud Function.
/// Sends PCM16 16kHz mono audio and receives the final transcript text.
/// </summary>
public class VoiceAsrClient : MonoBehaviour
{
    private const string VOICE_ASR_URL = "https://us-central1-fari-app-b2fd2.cloudfunctions.net/voiceAsrTranscribe";
    private const string READINESS_URL = "https://us-central1-fari-app-b2fd2.cloudfunctions.net/readinessStatus";

    [Header("ASR Backend")]
    [Tooltip("Firebase Function endpoint for ASR transcription.")]
    public string voiceAsrUrl = VOICE_ASR_URL;
    [Tooltip("Public backend readiness endpoint used before starting a voice call.")]
    public string readinessUrl = READINESS_URL;

    [Tooltip("Editor only: allow FirebaseAuthManager REST token fallback when available.")]
    public bool useEditorRestToken = true;

    public bool IsTranscribing { get; private set; }
    public bool IsCheckingReadiness { get; private set; }

    [Serializable]
    private class AsrRequestBody
    {
        public string audioBase64;
        public int sampleRate = 16000;
        public string format = "pcm_s16le";
        public string source = "unity_dialog_hold";
        public string requestId;
    }

    [Serializable]
    private class AsrResponseBody
    {
        public string text = "";
        public string error = "";
        public string code = "";
        public string membershipStatus = "";
        public string requestId = "";
    }

    [Serializable]
    private class ReadinessResponseBody
    {
        public ReadinessFunctions functions = new ReadinessFunctions();
    }

    [Serializable]
    private class ReadinessFunctions
    {
        public ReadinessFunctionStatus voiceAsrTranscribe = null;
    }

    [Serializable]
    private class ReadinessFunctionStatus
    {
        public bool configured = false;
        public string[] missingSecrets = Array.Empty<string>();
    }

    public void CheckVoiceCallReadiness(Action<bool, string> onComplete)
    {
        if (!isActiveAndEnabled)
        {
            onComplete?.Invoke(false, "语音识别组件未启用");
            return;
        }

        if (IsCheckingReadiness)
        {
            onComplete?.Invoke(false, "正在检查语音服务，请稍候");
            return;
        }

        StartCoroutine(CheckVoiceCallReadinessCoroutine(onComplete));
    }

    public void TranscribePcm16(byte[] pcm16, int sampleRate, Action<string> onSuccess, Action<string> onError)
    {
        if (!isActiveAndEnabled)
        {
            onError?.Invoke("语音识别组件未启用");
            return;
        }

        StartCoroutine(TranscribePcm16Coroutine(pcm16, sampleRate, onSuccess, onError));
    }

    private IEnumerator CheckVoiceCallReadinessCoroutine(Action<bool, string> onComplete)
    {
        IsCheckingReadiness = true;

        using (UnityWebRequest request = UnityWebRequest.Get(readinessUrl))
        {
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool failed = request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError;
#else
            bool failed = request.isNetworkError || request.isHttpError;
#endif

            IsCheckingReadiness = false;

            if (failed)
            {
                Debug.LogWarning($"[VoiceAsrClient] readiness check failed, allowing direct ASR attempt: {request.error}");
                onComplete?.Invoke(true, "");
                yield break;
            }

            try
            {
                ReadinessResponseBody readiness = JsonUtility.FromJson<ReadinessResponseBody>(request.downloadHandler.text);
                ReadinessFunctionStatus asr = readiness?.functions?.voiceAsrTranscribe;
                if (asr != null && !asr.configured)
                {
                    string message = BuildReadinessMissingMessage(asr);
                    if (!string.IsNullOrEmpty(message))
                    {
                        onComplete?.Invoke(false, message);
                        yield break;
                    }
                }

                onComplete?.Invoke(true, "");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VoiceAsrClient] readiness response parse failed, allowing direct ASR attempt: {e.Message}");
                onComplete?.Invoke(true, "");
            }
        }
    }

    private IEnumerator TranscribePcm16Coroutine(byte[] pcm16, int sampleRate, Action<string> onSuccess, Action<string> onError)
    {
        if (pcm16 == null || pcm16.Length == 0)
        {
            onError?.Invoke("没有录到有效声音");
            yield break;
        }

        if (IsTranscribing)
        {
            onError?.Invoke("语音识别正在进行中");
            yield break;
        }

        IsTranscribing = true;

        string idToken = null;
        string tokenError = null;
        yield return GetFirebaseIdToken(
            token => idToken = token,
            error => tokenError = error);

        if (string.IsNullOrEmpty(idToken))
        {
            IsTranscribing = false;
            onError?.Invoke(string.IsNullOrWhiteSpace(tokenError) ? "请先登录后使用语音" : tokenError);
            yield break;
        }

        AsrRequestBody body = new AsrRequestBody
        {
            audioBase64 = Convert.ToBase64String(pcm16),
            sampleRate = sampleRate,
            requestId = Guid.NewGuid().ToString("N")
        };

        string jsonBody = JsonUtility.ToJson(body);
        using (UnityWebRequest request = new UnityWebRequest(voiceAsrUrl, "POST"))
        {
            byte[] raw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(raw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + idToken);

            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool failed = request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError;
#else
            bool failed = request.isNetworkError || request.isHttpError;
#endif

            IsTranscribing = false;

            if (failed)
            {
                onError?.Invoke(BuildBackendErrorMessage(request.responseCode, request.error, request.downloadHandler.text));
                yield break;
            }

            try
            {
                AsrResponseBody response = JsonUtility.FromJson<AsrResponseBody>(request.downloadHandler.text);
                if (!string.IsNullOrWhiteSpace(response?.error))
                {
                    onError?.Invoke(response.error);
                    yield break;
                }

                onSuccess?.Invoke(response?.text ?? "");
            }
            catch (Exception e)
            {
                onError?.Invoke("语音识别响应解析失败: " + e.Message);
            }
        }
    }

    private IEnumerator GetFirebaseIdToken(Action<string> onToken, Action<string> onError)
    {
#if UNITY_EDITOR
        if (useEditorRestToken
            && FirebaseAuthManager.Instance != null
            && FirebaseAuthManager.Instance.TryGetEditorRestIdToken(out string editorRestToken))
        {
            onToken?.Invoke(editorRestToken);
            yield break;
        }
#endif

        FirebaseUser user = null;
        try
        {
            user = FirebaseAuth.DefaultInstance?.CurrentUser;
        }
        catch (Exception e)
        {
            onError?.Invoke("Firebase Auth 初始化失败: " + e.Message);
            yield break;
        }

        if (user == null)
        {
            onError?.Invoke("请先登录后使用语音");
            yield break;
        }

        var task = user.TokenAsync(false);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled)
        {
            onError?.Invoke(task.Exception?.InnerException?.Message ?? "获取 Firebase Token 失败");
            yield break;
        }

        onToken?.Invoke(task.Result);
    }

    private string BuildReadinessMissingMessage(ReadinessFunctionStatus status)
    {
        if (status?.missingSecrets != null && status.missingSecrets.Length > 0)
        {
            string joined = string.Join(", ", status.missingSecrets);
            if (joined.Contains("VOICE_APP_ID") || joined.Contains("VOICE_ACCESS_KEY"))
                return "语音识别服务未配置，请先设置 VOICE_APP_ID 和 VOICE_ACCESS_KEY";
        }

        return "";
    }

    private string BuildBackendErrorMessage(long statusCode, string requestError, string responseText)
    {
        string backendError = "";
        string backendCode = "";
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            try
            {
                AsrResponseBody response = JsonUtility.FromJson<AsrResponseBody>(responseText);
                backendError = response?.error ?? "";
                backendCode = response?.code ?? "";
            }
            catch
            {
                // Keep raw transport error below.
            }
        }

        if (statusCode == 429)
        {
            return string.IsNullOrWhiteSpace(backendError) ? "今日语音识别额度已用完" : backendError;
        }

        if (backendCode == "voice-asr-missing-secret")
        {
            return "语音识别服务未配置，请先设置 VOICE_APP_ID 和 VOICE_ACCESS_KEY";
        }

        if (!string.IsNullOrWhiteSpace(backendError))
        {
            return string.IsNullOrWhiteSpace(backendCode) ? backendError : $"{backendError} ({backendCode})";
        }

        if (!string.IsNullOrWhiteSpace(requestError)) return requestError;
        return statusCode > 0 ? $"语音识别失败（HTTP {statusCode}）" : "语音识别失败";
    }
}
