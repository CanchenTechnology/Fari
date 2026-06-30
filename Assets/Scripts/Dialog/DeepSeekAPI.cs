using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// AI API 客户端
/// 用于与阿里云 DashScope 兼容接口通信
/// </summary>
public class DeepSeekAPI : MonoBehaviour
{
    private const string AI_CHAT_URL = "https://us-central1-fari-app-b2fd2.cloudfunctions.net/aiChat";
    private const string AI_CHAT_STREAM_URL = "https://us-central1-fari-app-b2fd2.cloudfunctions.net/aiChatStream";
    private const string DEEPSEEK_CHAT_URL = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";
    private const string MODEL = "deepseek-v4-pro";

    [Header("Editor 调试")]
    [Tooltip("Editor 下优先使用下面的调试 Key 直连阿里云 DashScope。正式包不生效，正式包始终走 Firebase Functions。")]
    public bool useEditorDirectDeepSeek = false;

    [Tooltip("仅用于 Unity Editor 调试的阿里云 DashScope API Key。请使用单独的调试 Key，不要提交真实生产 Key。")]
    public string editorDeepSeekApiKey = "";

    [Tooltip("Editor 下没有真实 Firebase 用户时，自动使用游客登录拿到 Token，再调用正式的 Cloud Functions。")]
    public bool editorAutoAnonymousSignIn = true;

    [Tooltip("Editor 后端不可用时是否返回本地模拟回复。正常联调建议关闭，否则会把真实错误伪装成成功回复。正式包不生效。")]
    public bool useEditorMockWhenBackendUnavailable = false;

    public bool HasEditorDirectDeepSeekConfig()
    {
#if UNITY_EDITOR
        return ShouldUseEditorDirectDeepSeek();
#else
        return false;
#endif
    }

    public static DeepSeekAPI ResolveFor(GameObject owner)
    {
        DeepSeekAPI local = owner != null ? owner.GetComponent<DeepSeekAPI>() : null;
#if UNITY_EDITOR
        DeepSeekAPI[] apis = FindObjectsOfType<DeepSeekAPI>();
        for (int i = 0; i < apis.Length; i++)
        {
            if (apis[i] != null && apis[i].HasEditorDirectDeepSeekConfig())
            {
                return apis[i];
            }
        }
#endif
        if (local != null)
        {
            return local;
        }

        return owner != null ? owner.AddComponent<DeepSeekAPI>() : null;
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;

        public Message()
        {
        }

        public Message(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    [System.Serializable]
    public class RequestBody
    {
        public string model;
        public List<Message> messages;
        public float temperature = 0.7f;
        public int max_tokens = 2000;
    }

    [System.Serializable]
    public class Choice
    {
        public Message message;
        public string finish_reason;
        public int index;
    }

    [System.Serializable]
    public class ResponseBody
    {
        public string id;
        public string object_type;
        public int created;
        public string model;
        public List<Choice> choices;
        public Usage usage;
    }

    [System.Serializable]
    public class Usage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
    }

    [System.Serializable]
    private class BackendAIResponse
    {
        public string content = "";
        public string error = "";
        public string code = "";
    }

    // ---- 流式响应解析 ----

    [System.Serializable]
    public class StreamChunk
    {
        public string id;
        public string model;
        public List<StreamChoice> choices;
    }

    [System.Serializable]
    public class StreamChoice
    {
        public StreamDelta delta;
        public string finish_reason;
        public int index;
    }

    [System.Serializable]
    public class StreamDelta
    {
        public string content;
        public string role;
    }

    /// <summary>
    /// 发送对话请求到阿里云 DashScope API（非流式）
    /// </summary>
    /// <param name="messages">对话历史消息列表</param>
    /// <param name="onSuccess">成功回调</param>
    /// <param name="onError">错误回调</param>
    public void SendChatRequest(
        List<Message> messages,
        Action<string> onSuccess,
        Action<string> onError,
        bool notifyOnComplete = false,
        string clientRequestId = null)
    {
        StartCoroutine(SendChatRequestCoroutine(messages, onSuccess, onError, notifyOnComplete, clientRequestId));
    }

    private IEnumerator SendChatRequestCoroutine(
        List<Message> messages,
        Action<string> onSuccess,
        Action<string> onError,
        bool notifyOnComplete,
        string clientRequestId)
    {
        RequestBody requestBody = new RequestBody
        {
            model = MODEL,
            messages = messages
        };

        string jsonBody = JsonUtility.ToJson(requestBody);
        // JsonUtility 不支持 List，需要手动替换
        StringBuilder sb = new StringBuilder();
        sb.Append("{\"model\":\"").Append(MODEL).Append("\",");
        sb.Append("\"messages\":[");
        for (int i = 0; i < messages.Count; i++)
        {
            sb.Append("{\"role\":\"").Append(messages[i].role).Append("\",");
            sb.Append("\"content\":\"").Append(EscapeJsonString(messages[i].content)).Append("\"");
            sb.Append("}");
            if (i < messages.Count - 1)
            {
                sb.Append(",");
            }
        }
        sb.Append("],");
        sb.Append("\"temperature\":0.7,");
        sb.Append("\"max_tokens\":2000");
        AppendNotificationOptions(sb, notifyOnComplete, clientRequestId);
        sb.Append("}");

        jsonBody = sb.ToString();

#if UNITY_EDITOR
        if (ShouldUseEditorDirectDeepSeek())
        {
            yield return SendEditorDirectChatRequest(jsonBody, messages, onSuccess, onError);
            yield break;
        }
#endif

        string idToken = null;
        string tokenError = null;
        yield return GetFirebaseIdToken(
            token => idToken = token,
            error => tokenError = error);
        if (string.IsNullOrEmpty(idToken))
        {
            if (TryCompleteWithEditorMock(messages, onSuccess, tokenError))
                yield break;

            Debug.LogError("[DeepSeekAPI] Firebase token error: " + tokenError);
            onError?.Invoke(tokenError);
            yield break;
        }

        using (UnityWebRequest request = new UnityWebRequest(AI_CHAT_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + idToken);

            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                string errorMessage = BuildBackendErrorMessage(request.responseCode, request.error, request.downloadHandler.text);
                Debug.LogError("DashScope API Error: " + errorMessage);
                Debug.LogError("Response: " + request.downloadHandler.text);
                if (TryCompleteWithEditorMock(messages, onSuccess, errorMessage))
                    yield break;
                onError?.Invoke(errorMessage);
            }
            else
            {
                string responseText = request.downloadHandler.text;
                Debug.Log("DashScope API Response: " + responseText);

                try
                {
                    BackendAIResponse response = JsonUtility.FromJson<BackendAIResponse>(responseText);
                    if (!string.IsNullOrEmpty(response?.content))
                    {
                        onSuccess?.Invoke(response.content);
                    }
                    else
                    {
                        onError?.Invoke(response?.error ?? "No response content");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Parse Error: " + e.Message);
                    onError?.Invoke("Parse error: " + e.Message);
                }
            }
        }
    }

    #region 流式请求

    /// <summary>
    /// 发送流式对话请求到阿里云 DashScope API
    /// </summary>
    /// <param name="messages">对话历史消息列表</param>
    /// <param name="onChunk">每收到一个 token 的回调</param>
    /// <param name="onComplete">流式完成时的回调（传入完整文本）</param>
    /// <param name="onError">错误回调</param>
    public void SendChatRequestStream(
        List<Message> messages,
        Action<string> onChunk,
        Action<string> onComplete,
        Action<string> onError,
        bool notifyOnComplete = false,
        string clientRequestId = null)
    {
        StartCoroutine(SendChatRequestStreamCoroutine(messages, onChunk, onComplete, onError, notifyOnComplete, clientRequestId));
    }

    private IEnumerator SendChatRequestStreamCoroutine(List<Message> messages,
        Action<string> onChunk, Action<string> onComplete, Action<string> onError, bool notifyOnComplete, string clientRequestId)
    {
        // 构建请求 JSON（比非流式多 "stream": true）
        StringBuilder sb = new StringBuilder();
        sb.Append("{\"model\":\"").Append(MODEL).Append("\",");
        sb.Append("\"messages\":[");
        for (int i = 0; i < messages.Count; i++)
        {
            sb.Append("{\"role\":\"").Append(messages[i].role).Append("\",");
            sb.Append("\"content\":\"").Append(EscapeJsonString(messages[i].content)).Append("\"");
            sb.Append("}");
            if (i < messages.Count - 1) sb.Append(",");
        }
        sb.Append("],");
        sb.Append("\"temperature\":0.7,");
        sb.Append("\"max_tokens\":2000,");
        sb.Append("\"stream\":true");
        AppendNotificationOptions(sb, notifyOnComplete, clientRequestId);
        sb.Append("}");

        string jsonBody = sb.ToString();

#if UNITY_EDITOR
        if (ShouldUseEditorDirectDeepSeek())
        {
            yield return SendEditorDirectChatRequestStream(jsonBody, messages, onChunk, onComplete, onError);
            yield break;
        }
#endif

        string idToken = null;
        string tokenError = null;
        yield return GetFirebaseIdToken(
            token => idToken = token,
            error => tokenError = error);
        if (string.IsNullOrEmpty(idToken))
        {
            if (TryCompleteStreamWithEditorMock(messages, onChunk, onComplete, tokenError))
                yield break;

            Debug.LogError("[DeepSeekAPI] Firebase token error: " + tokenError);
            onError?.Invoke(tokenError);
            yield break;
        }

        using (UnityWebRequest request = new UnityWebRequest(AI_CHAT_STREAM_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);

            var handler = new StreamingDownloadHandler(onChunk, onComplete, onError);
            request.downloadHandler = handler;

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + idToken);

            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                // 如果 Handler 还没触发过错误回调才报错
                if (!handler.HasCompleted)
                {
                    string errorMessage = BuildBackendErrorMessage(request.responseCode, request.error, handler.ResponseText);
                    Debug.LogError("DashScope Stream Error: " + errorMessage);
                    if (!string.IsNullOrWhiteSpace(handler.ResponseText))
                    {
                        Debug.LogError("Response: " + handler.ResponseText);
                    }
                    if (TryCompleteStreamWithEditorMock(messages, onChunk, onComplete, errorMessage))
                        yield break;
                    onError?.Invoke(errorMessage);
                }
            }
        }
    }

    /// <summary>
    /// 流式下载处理器 — 逐行解析 SSE 事件
    /// </summary>
    private class StreamingDownloadHandler : DownloadHandlerScript
    {
        private Action<string> onChunk;
        private Action<string> onComplete;
        private Action<string> onError;
        private StringBuilder textBuffer = new StringBuilder();
        private StringBuilder rawResponse = new StringBuilder();
        private StringBuilder fullContent = new StringBuilder();
        private bool _hasCompleted;

        public bool HasCompleted => _hasCompleted;
        public string ResponseText => rawResponse.ToString();

        public StreamingDownloadHandler(Action<string> onChunk,
            Action<string> onComplete, Action<string> onError)
        {
            this.onChunk = onChunk;
            this.onComplete = onComplete;
            this.onError = onError;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (_hasCompleted) return true;
            if (data == null || dataLength <= 0) return false;

            string text = Encoding.UTF8.GetString(data, 0, dataLength);
            rawResponse.Append(text);
            textBuffer.Append(text);
            ProcessLines();
            return true;
        }

        protected override void CompleteContent()
        {
            if (_hasCompleted) return;

            // 处理缓冲区中可能残留的最后一行
            if (textBuffer.Length > 0)
            {
                textBuffer.Append('\n');
                ProcessLines();
            }
        }

        private void ProcessLines()
        {
            if (_hasCompleted) return;

            string buffer = textBuffer.ToString();
            int newlineIndex;
            while ((newlineIndex = buffer.IndexOf('\n')) >= 0)
            {
                string line = buffer.Substring(0, newlineIndex).Trim();
                buffer = buffer.Substring(newlineIndex + 1);

                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("data: "))
                {
                    string data = line.Substring(6).Trim();
                    if (data == "[DONE]")
                    {
                        CompleteOnce();
                        return;
                    }

                    try
                    {
                        var chunk = JsonUtility.FromJson<StreamChunk>(data);
                        if (chunk?.choices != null && chunk.choices.Count > 0)
                        {
                            string content = chunk.choices[0].delta?.content;
                            if (!string.IsNullOrEmpty(content))
                            {
                                fullContent.Append(content);
                                onChunk?.Invoke(content);
                            }

                            // finish_reason 出现时也触发完成
                            if (!string.IsNullOrEmpty(chunk.choices[0].finish_reason)
                                && chunk.choices[0].finish_reason != "null")
                            {
                                CompleteOnce();
                                return;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[DeepSeekAPI] SSE parse error: " + e.Message
                            + " | raw: " + data.Substring(0, Math.Min(data.Length, 80)));
                    }
                }
            }
            textBuffer.Clear();
            textBuffer.Append(buffer);
        }

        private void CompleteOnce()
        {
            if (_hasCompleted) return;

            _hasCompleted = true;
            textBuffer.Clear();
            onComplete?.Invoke(fullContent.ToString());
        }
    }

    #endregion

    private void AppendNotificationOptions(StringBuilder sb, bool notifyOnComplete, string clientRequestId)
    {
        if (!notifyOnComplete || sb == null)
            return;

        sb.Append(",\"notifyOnComplete\":true");
        sb.Append(",\"notificationType\":\"dialogue_reply\"");
        sb.Append(",\"notificationTitle\":\"神谕师回复了你\"");
        sb.Append(",\"clientRequestId\":\"")
            .Append(string.IsNullOrWhiteSpace(clientRequestId) ? Guid.NewGuid().ToString("N") : EscapeJsonString(clientRequestId))
            .Append("\"");
    }

#if UNITY_EDITOR
    private bool ShouldUseEditorDirectDeepSeek()
    {
        return useEditorDirectDeepSeek && !string.IsNullOrWhiteSpace(editorDeepSeekApiKey);
    }

    private IEnumerator SignInAnonymouslyForEditor(Action<FirebaseUser> onSuccess, Action<string> onError)
    {
        FirebaseAuth auth = null;
        try
        {
            auth = FirebaseAuth.DefaultInstance;
        }
        catch (Exception e)
        {
            onError?.Invoke("Firebase Auth 初始化失败: " + e.Message);
            yield break;
        }

        if (auth.CurrentUser != null)
        {
            onSuccess?.Invoke(auth.CurrentUser);
            yield break;
        }

        Debug.Log("[DeepSeekAPI] Editor 未检测到 Firebase 用户，正在自动游客登录后调用 AI 后端。");

        FirebaseAuthManager authManager = null;
        try
        {
            authManager = FirebaseAuthManager.Instance;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DeepSeekAPI] 获取 FirebaseAuthManager 失败，将直接使用 Firebase Auth 游客登录: " + e.Message);
        }

        if (authManager != null)
        {
            float initDeadline = Time.realtimeSinceStartup + 10f;
            while (!authManager.IsFirebaseInitialized && Time.realtimeSinceStartup < initDeadline)
            {
                yield return null;
            }

            if (auth.CurrentUser != null)
            {
                onSuccess?.Invoke(auth.CurrentUser);
                yield break;
            }

            if (!authManager.IsFirebaseInitialized)
            {
                onError?.Invoke("Firebase 初始化未完成，无法自动游客登录");
                yield break;
            }

            float loggingDeadline = Time.realtimeSinceStartup + 10f;
            while (authManager.IsLoggingIn && Time.realtimeSinceStartup < loggingDeadline)
            {
                yield return null;
            }

            if (auth.CurrentUser != null)
            {
                onSuccess?.Invoke(auth.CurrentUser);
                yield break;
            }

            if (authManager.IsLoggingIn)
            {
                onError?.Invoke("Firebase 正在登录中，暂时无法自动游客登录");
                yield break;
            }

            bool finished = false;
            string signInError = null;
            FirebaseUser signedInUser = null;

            Action<AuthProvider, FirebaseUser> handleSuccess = (provider, user) =>
            {
                if (provider != AuthProvider.Anonymous) return;
                signedInUser = user ?? auth.CurrentUser;
                finished = true;
            };
            Action<AuthProvider, string> handleFailed = (provider, error) =>
            {
                if (provider != AuthProvider.Anonymous) return;
                signInError = error;
                finished = true;
            };

            authManager.OnLoginSuccess += handleSuccess;
            authManager.OnLoginFailed += handleFailed;
            authManager.SignInAnonymously();

            float signInDeadline = Time.realtimeSinceStartup + 15f;
            while (!finished && Time.realtimeSinceStartup < signInDeadline)
            {
                if (auth.CurrentUser != null)
                {
                    signedInUser = auth.CurrentUser;
                    finished = true;
                    break;
                }

                yield return null;
            }

            authManager.OnLoginSuccess -= handleSuccess;
            authManager.OnLoginFailed -= handleFailed;

            if (signedInUser != null)
            {
                onSuccess?.Invoke(signedInUser);
                yield break;
            }

            onError?.Invoke(string.IsNullOrWhiteSpace(signInError)
                ? "Editor 自动游客登录超时"
                : signInError);
            yield break;
        }

        var task = auth.SignInAnonymouslyAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted || task.IsCanceled)
        {
            onError?.Invoke(task.Exception?.InnerException?.Message ?? "Editor 自动游客登录失败");
            yield break;
        }

        onSuccess?.Invoke(task.Result.User);
    }

    private IEnumerator SendEditorDirectChatRequest(string jsonBody, List<Message> messages,
        Action<string> onSuccess, Action<string> onError)
    {
        using (UnityWebRequest request = new UnityWebRequest(DEEPSEEK_CHAT_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + editorDeepSeekApiKey.Trim());

            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError("[DeepSeekAPI] Editor DashScope API Error: " + request.error);
                Debug.LogError("Response: " + request.downloadHandler.text);
                if (TryCompleteWithEditorMock(messages, onSuccess, request.error))
                    yield break;
                onError?.Invoke(request.error);
                yield break;
            }

            try
            {
                ResponseBody response = JsonUtility.FromJson<ResponseBody>(request.downloadHandler.text);
                string content = response?.choices != null && response.choices.Count > 0
                    ? response.choices[0].message?.content
                    : null;

                if (!string.IsNullOrEmpty(content))
                {
                    onSuccess?.Invoke(content);
                }
                else
                {
                    onError?.Invoke("Editor DashScope response has no content");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[DeepSeekAPI] Editor DashScope parse error: " + e.Message);
                onError?.Invoke("Parse error: " + e.Message);
            }
        }
    }

    private IEnumerator SendEditorDirectChatRequestStream(string jsonBody, List<Message> messages,
        Action<string> onChunk, Action<string> onComplete, Action<string> onError)
    {
        using (UnityWebRequest request = new UnityWebRequest(DEEPSEEK_CHAT_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);

            var handler = new StreamingDownloadHandler(onChunk, onComplete, onError);
            request.downloadHandler = handler;

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + editorDeepSeekApiKey.Trim());

            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                if (!handler.HasCompleted)
                {
                    Debug.LogError("[DeepSeekAPI] Editor DashScope Stream Error: " + request.error);
                    Debug.LogError("Response: " + request.downloadHandler.text);
                    if (TryCompleteStreamWithEditorMock(messages, onChunk, onComplete, request.error))
                        yield break;
                    onError?.Invoke(request.error);
                }
            }
        }
    }
#endif

    /// <summary>
    /// 把后端/DashScope 的 HTTP 错误转成可读提示。
    /// </summary>
    private string BuildBackendErrorMessage(long statusCode, string requestError, string responseText)
    {
        string backendError = "";
        string backendCode = "";
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            try
            {
                BackendAIResponse response = JsonUtility.FromJson<BackendAIResponse>(responseText);
                backendError = response?.error ?? "";
                backendCode = response?.code ?? "";
            }
            catch
            {
                // Non-JSON bodies are still useful as raw debug context below.
            }
        }

        if (statusCode == 402)
        {
            return string.IsNullOrWhiteSpace(backendError)
                ? "DashScope 额度不足（HTTP 402）。请在阿里云控制台确认额度，或在后台添加/切换到可用备用 Key。"
                : $"DashScope 额度不足（HTTP 402）：{backendError}";
        }

        if (statusCode == 401 || statusCode == 403)
        {
            return string.IsNullOrWhiteSpace(backendError)
                ? $"DashScope 密钥无效或无权限（HTTP {statusCode}）。请在后台“模型额度”里替换 Key。"
                : $"DashScope 密钥无效或无权限（HTTP {statusCode}）：{backendError}";
        }

        if (statusCode == 429)
        {
            return string.IsNullOrWhiteSpace(backendError)
                ? "DashScope 请求受限（HTTP 429）。请稍后重试，或切换备用 Key。"
                : $"DashScope 请求受限（HTTP 429）：{backendError}";
        }

        if (!string.IsNullOrWhiteSpace(backendError))
        {
            return string.IsNullOrWhiteSpace(backendCode)
                ? backendError
                : $"{backendError} ({backendCode})";
        }

        if (!string.IsNullOrWhiteSpace(requestError)) return requestError;
        return statusCode > 0 ? $"AI 服务请求失败（HTTP {statusCode}）" : "AI 服务请求失败";
    }

    /// <summary>
    /// 转义 JSON 字符串中的特殊字符
    /// </summary>
    private string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;

        StringBuilder sb = new StringBuilder();
        foreach (char c in str)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private IEnumerator GetFirebaseIdToken(Action<string> onToken, Action<string> onError)
    {
#if UNITY_EDITOR
        if (FirebaseAuthManager.Instance != null
            && FirebaseAuthManager.Instance.TryGetEditorRestIdToken(out string editorRestToken))
        {
            onToken?.Invoke(editorRestToken);
            yield break;
        }
#endif

        FirebaseUser user = null;
        string editorSignInError = null;

        try
        {
            user = FirebaseAuth.DefaultInstance?.CurrentUser;
        }
        catch (Exception e)
        {
            onError?.Invoke("Firebase Auth 初始化失败: " + e.Message);
            yield break;
        }

#if UNITY_EDITOR
        if (user == null && editorAutoAnonymousSignIn)
        {
            yield return SignInAnonymouslyForEditor(
                signedInUser => user = signedInUser,
                error => editorSignInError = error);
        }
#endif

        if (user == null)
        {
            onError?.Invoke(string.IsNullOrWhiteSpace(editorSignInError)
                ? "用户未登录，无法调用 AI 服务"
                : editorSignInError);
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

    private bool TryCompleteWithEditorMock(List<Message> messages, Action<string> onSuccess, string reason)
    {
#if UNITY_EDITOR
        if (IsPaymentRequiredError(reason)) return false;
        if (!useEditorMockWhenBackendUnavailable) return false;

        string content = BuildEditorMockResponse(messages, reason);
        Debug.LogWarning("[DeepSeekAPI] Editor 使用本地模拟 AI 回复: " + reason);
        onSuccess?.Invoke(content);
        return true;
#else
        return false;
#endif
    }

    private bool TryCompleteStreamWithEditorMock(List<Message> messages,
        Action<string> onChunk, Action<string> onComplete, string reason)
    {
#if UNITY_EDITOR
        if (IsPaymentRequiredError(reason)) return false;
        if (!useEditorMockWhenBackendUnavailable) return false;

        string content = BuildEditorMockResponse(messages, reason);
        Debug.LogWarning("[DeepSeekAPI] Editor 使用本地模拟流式 AI 回复: " + reason);
        onChunk?.Invoke(content);
        onComplete?.Invoke(content);
        return true;
#else
        return false;
#endif
    }

    private bool IsPaymentRequiredError(string reason)
    {
        return !string.IsNullOrEmpty(reason)
            && (reason.IndexOf("402", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("Payment Required", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private string BuildEditorMockResponse(List<Message> messages, string reason)
    {
        string lastUserMessage = "";
        if (messages != null)
        {
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i]?.role == "user")
                {
                    lastUserMessage = messages[i].content;
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(lastUserMessage)
            && (lastUserMessage.Contains("今日标题") || lastUserMessage.Contains("今日神谕") || lastUserMessage.Contains("详情解释")))
        {
            return "今日标题：微光靠近\n今日神谕：先相信你心里最安静的那个答案。\n详情解释：这是一条 Editor 调试回复，因为当前没有 Firebase 登录或后端 Functions 尚未部署。你可以继续测试 UI 流程；正式环境会调用 Cloud Functions 里的真实 AI 服务。";
        }

        if (!string.IsNullOrEmpty(lastUserMessage)
            && (lastUserMessage.Contains("卡牌描述") || lastUserMessage.Contains("牌义解析") || lastUserMessage.Contains("推荐追问")))
        {
            return "卡牌描述：这是一条 Editor 调试解读，用来保证本地界面流程可以继续跑通。\n能量标签：调试、确认、前进\n牌义解析：当前没有 Firebase 登录或后端 Functions 尚未部署，所以这里不会调用真实 AI。你可以先验证翻牌、完整解读、按钮跳转和布局表现。\n行动建议：先完成本地 UI 与交互检查，等 Firebase Functions 部署后再切回真实服务验证内容质量。\n推荐追问：\n1. 这张牌今天想提醒我什么？\n2. 我现在该如何面对现状？\n3. 这件事更深层的讯息是什么？\n4. 我接下来该问自己什么？";
        }

        return string.IsNullOrEmpty(lastUserMessage)
            ? "这是 Editor 调试回复：当前没有 Firebase 登录或后端 Functions 尚未部署。正式环境会调用 Cloud Functions 的 AI 代理。"
            : $"这是 Editor 调试回复：我收到了你的问题“{lastUserMessage}”。当前没有 Firebase 登录或后端 Functions 尚未部署，所以先返回本地模拟内容，方便你测试对话流程。";
    }
}
