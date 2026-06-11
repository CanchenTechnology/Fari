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

    /// <summary>
    /// 发送对话请求到 DeepSeek API
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
