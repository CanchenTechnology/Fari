using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// DeepSeek API 客户端
/// 用于与 DeepSeek AI 服务通信
/// </summary>
public class DeepSeekAPI : MonoBehaviour
{
    private const string API_URL = "https://api.deepseek.com/chat/completions";
    private const string API_KEY = "sk-71d2099e083448928be76e01964012ec";
    private const string MODEL = "deepseek-v4-pro";

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;

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
    /// 发送对话请求到 DeepSeek API（非流式）
    /// </summary>
    /// <param name="messages">对话历史消息列表</param>
    /// <param name="onSuccess">成功回调</param>
    /// <param name="onError">错误回调</param>
    public void SendChatRequest(List<Message> messages, Action<string> onSuccess, Action<string> onError)
    {
        StartCoroutine(SendChatRequestCoroutine(messages, onSuccess, onError));
    }

    private IEnumerator SendChatRequestCoroutine(List<Message> messages, Action<string> onSuccess, Action<string> onError)
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
        sb.Append("}");

        jsonBody = sb.ToString();

        using (UnityWebRequest request = new UnityWebRequest(API_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + API_KEY);

            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError("DeepSeek API Error: " + request.error);
                Debug.LogError("Response: " + request.downloadHandler.text);
                onError?.Invoke(request.error);
            }
            else
            {
                string responseText = request.downloadHandler.text;
                Debug.Log("DeepSeek API Response: " + responseText);

                try
                {
                    ResponseBody response = JsonUtility.FromJson<ResponseBody>(responseText);
                    if (response.choices != null && response.choices.Count > 0)
                    {
                        string content = response.choices[0].message.content;
                        onSuccess?.Invoke(content);
                    }
                    else
                    {
                        onError?.Invoke("No response content");
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
    /// 发送流式对话请求到 DeepSeek API
    /// </summary>
    /// <param name="messages">对话历史消息列表</param>
    /// <param name="onChunk">每收到一个 token 的回调</param>
    /// <param name="onComplete">流式完成时的回调（传入完整文本）</param>
    /// <param name="onError">错误回调</param>
    public void SendChatRequestStream(List<Message> messages,
        Action<string> onChunk, Action<string> onComplete, Action<string> onError)
    {
        StartCoroutine(SendChatRequestStreamCoroutine(messages, onChunk, onComplete, onError));
    }

    private IEnumerator SendChatRequestStreamCoroutine(List<Message> messages,
        Action<string> onChunk, Action<string> onComplete, Action<string> onError)
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
        sb.Append("}");

        string jsonBody = sb.ToString();

        using (UnityWebRequest request = new UnityWebRequest(API_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);

            var handler = new StreamingDownloadHandler(onChunk, onComplete, onError);
            request.downloadHandler = handler;

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + API_KEY);

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
                    Debug.LogError("DeepSeek Stream Error: " + request.error);
                    onError?.Invoke(request.error);
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
        private StringBuilder fullContent = new StringBuilder();
        private bool _hasCompleted;

        public bool HasCompleted => _hasCompleted;

        public StreamingDownloadHandler(Action<string> onChunk,
            Action<string> onComplete, Action<string> onError)
        {
            this.onChunk = onChunk;
            this.onComplete = onComplete;
            this.onError = onError;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0) return false;

            string text = Encoding.UTF8.GetString(data, 0, dataLength);
            textBuffer.Append(text);
            ProcessLines();
            return true;
        }

        protected override void CompleteContent()
        {
            // 处理缓冲区中可能残留的最后一行
            if (textBuffer.Length > 0)
            {
                textBuffer.Append('\n');
                ProcessLines();
            }
        }

        private void ProcessLines()
        {
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
                        _hasCompleted = true;
                        onComplete?.Invoke(fullContent.ToString());
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
                                _hasCompleted = true;
                                onComplete?.Invoke(fullContent.ToString());
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
    }

    #endregion

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
}
