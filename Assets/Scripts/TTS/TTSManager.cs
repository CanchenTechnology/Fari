using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 火山引擎 TTS (文本转语音) 管理器
/// 支持流式合成 + 本地缓存 + AudioManager 集成
/// </summary>
public class TTSManager : MonoBehaviour
{
    public static TTSManager Instance { get; private set; }

    #region 火山引擎 API 配置

    [Header("火山引擎 TTS 配置")]
    [Tooltip("火山引擎 AppID")]
    public string appId = "";

    [Tooltip("火山引擎 Access Token")]
    public string accessToken = "";

    [Tooltip("音色类型: zh_female_qingxin / zh_male_qingrun / zh_female_shuangkuaidv2 等")]
    public string voiceType = "zh_female_qingxin";

    [Tooltip("语速比例 (0.5 ~ 2.0)")]
    [Range(0.5f, 2.0f)]
    public float speedRatio = 1.0f;

    [Tooltip("音量比例 (0.5 ~ 2.0)")]
    [Range(0.5f, 2.0f)]
    public float volumeRatio = 1.0f;

    [Tooltip("音频编码: mp3 / wav / ogg")]
    public string encoding = "mp3";

    private const string TTS_API_URL = "https://openspeech.bytedance.com/api/v1/tts";
    private const string CLUSTER = "volcano_tts";
    private const string CACHE_DIR = "TTSCache";

    #endregion

    #region 缓存

    /// <summary>文本 MD5 → AudioClip 缓存（内存）</summary>
    private Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();

    /// <summary>文本 MD5 → 文件路径缓存（磁盘）</summary>
    private HashSet<string> _diskCacheSet = new HashSet<string>();

    /// <summary>是否启用磁盘缓存</summary>
    public bool enableDiskCache = true;

    #endregion

    #region 状态

    /// <summary>是否正在合成</summary>
    public bool IsSynthesizing { get; private set; }

    /// <summary>当前正在合成的请求 ID（用于取消）</summary>
    private string _currentTextHash;

    /// <summary>TTS 合成完成事件</summary>
    public event Action<string, AudioClip> OnSynthesisComplete;

    /// <summary>TTS 合成失败事件</summary>
    public event Action<string, string> OnSynthesisError;

    #endregion

    // ---- 生命周期 ----

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 加载磁盘缓存索引
        if (enableDiskCache)
            LoadDiskCacheIndex();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ---- 公开接口 ----

    /// <summary>
    /// 将文本转为语音（自动缓存，优先返回缓存）
    /// </summary>
    /// <param name="text">要合成语音的文本</param>
    /// <param name="onComplete">完成回调 (text, audioClip)</param>
    /// <param name="onError">错误回调 (text, errorMessage)</param>
    public void Speak(string text, Action<AudioClip> onComplete = null, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            onError?.Invoke("文本为空");
            return;
        }

        string hash = GetTextHash(text);

        // 1. 内存缓存命中
        if (_clipCache.TryGetValue(hash, out AudioClip cachedClip) && cachedClip != null)
        {
            Debug.Log($"[TTSManager] 内存缓存命中: {text.Substring(0, Math.Min(text.Length, 20))}...");
            onComplete?.Invoke(cachedClip);
            return;
        }

        // 2. 磁盘缓存命中
        string diskPath = GetDiskCachePath(hash);
        if (enableDiskCache && _diskCacheSet.Contains(hash))
        {
            StartCoroutine(LoadFromDisk(diskPath, text, onComplete, onError));
            return;
        }

        // 3. 请求火山引擎 TTS
        StartCoroutine(RequestTTSSynthesis(text, hash, diskPath, onComplete, onError));
    }

    /// <summary>
    /// 直接通过 AudioManager 播放文本语音
    /// </summary>
    public void PlayText(string text, bool interrupt = true)
    {
        Speak(text,
            (clip) =>
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayVoice(clip, interrupt);
                }
                else
                {
                    Debug.LogWarning("[TTSManager] AudioManager.Instance 不存在，无法播放");
                }
            },
            (error) =>
            {
                Debug.LogError($"[TTSManager] TTS 合成失败: {error}");
            });
    }

    /// <summary>
    /// 停止正在进行的 TTS 合成
    /// </summary>
    public void CancelSynthesis()
    {
        _currentTextHash = null;
        StopAllCoroutines();
        IsSynthesizing = false;
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public void ClearCache()
    {
        _clipCache.Clear();
        _diskCacheSet.Clear();

        string cacheDir = System.IO.Path.Combine(Application.persistentDataPath, CACHE_DIR);
        if (System.IO.Directory.Exists(cacheDir))
        {
            System.IO.Directory.Delete(cacheDir, true);
        }
        Debug.Log("[TTSManager] 缓存已清除");
    }

    // ---- 核心 API 请求 ----

    private IEnumerator RequestTTSSynthesis(string text, string textHash, string diskPath,
        Action<AudioClip> onComplete, Action<string> onError)
    {
        IsSynthesizing = true;
        _currentTextHash = textHash;

        Debug.Log($"[TTSManager] 请求 TTS: {text.Substring(0, Math.Min(text.Length, 30))}...");

        // 构建请求 JSON
        string reqId = Guid.NewGuid().ToString();
        string requestJson = BuildRequestJson(text, reqId);

        using (UnityWebRequest request = new UnityWebRequest(TTS_API_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer;{accessToken}");

            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                IsSynthesizing = false;
                _currentTextHash = null;

                string errorMsg = $"TTS API 错误: {request.error}, 响应: {request.downloadHandler.text}";
                Debug.LogError($"[TTSManager] {errorMsg}");
                onError?.Invoke(errorMsg);
                OnSynthesisError?.Invoke(text, errorMsg);
                yield break;
            }

            // 解析响应 JSON，获取音频数据
            string responseText = request.downloadHandler.text;
            byte[] audioData = ParseAudioFromResponse(responseText);

            if (audioData == null || audioData.Length == 0)
            {
                IsSynthesizing = false;
                _currentTextHash = null;

                string errorMsg = $"TTS 响应解析失败: {responseText.Substring(0, Math.Min(responseText.Length, 200))}";
                Debug.LogError($"[TTSManager] {errorMsg}");
                onError?.Invoke(errorMsg);
                OnSynthesisError?.Invoke(text, errorMsg);
                yield break;
            }

            // 保存到磁盘缓存
            if (enableDiskCache && !string.IsNullOrEmpty(diskPath))
            {
                SaveToDisk(diskPath, audioData, textHash);
            }

            // 转换为 AudioClip（通过加载 mp3/wav 文件）
            yield return LoadAudioClipFromBytes(audioData, textHash, (clip) =>
            {
                IsSynthesizing = false;
                _currentTextHash = null;

                if (clip != null)
                {
                    // 存入内存缓存
                    _clipCache[textHash] = clip;
                    Debug.Log($"[TTSManager] TTS 合成完成: {text.Substring(0, Math.Min(text.Length, 20))}... → {clip.length:F1}s");
                    onComplete?.Invoke(clip);
                    OnSynthesisComplete?.Invoke(text, clip);
                }
                else
                {
                    string err = "AudioClip 创建失败";
                    onError?.Invoke(err);
                    OnSynthesisError?.Invoke(text, err);
                }
            });
        }
    }

    // ---- 请求构建 ----

    private string BuildRequestJson(string text, string reqId)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{");

        // app 配置
        sb.Append("\"app\":{");
        sb.Append($"\"appid\":\"{EscapeJson(appId)}\",");
        sb.Append($"\"token\":\"{EscapeJson(accessToken)}\",");
        sb.Append($"\"cluster\":\"{CLUSTER}\"");
        sb.Append("},");

        // user 配置
        sb.Append("\"user\":{");
        sb.Append($"\"uid\":\"moonly_user\"");
        sb.Append("},");

        // audio 配置
        sb.Append("\"audio\":{");
        sb.Append($"\"voice_type\":\"{EscapeJson(voiceType)}\",");
        sb.Append($"\"encoding\":\"{encoding}\",");
        sb.Append($"\"speed_ratio\":{speedRatio:F1},");
        sb.Append($"\"volume_ratio\":{volumeRatio:F1}");
        sb.Append("},");

        // request 配置
        sb.Append("\"request\":{");
        sb.Append($"\"reqid\":\"{EscapeJson(reqId)}\",");
        sb.Append($"\"text\":\"{EscapeJson(text)}\",");
        sb.Append("\"text_type\":\"plain\",");
        sb.Append("\"operation\":\"query\"");
        sb.Append("}");

        sb.Append("}");
        return sb.ToString();
    }

    // ---- 响应解析 ----

    /// <summary>
    /// 从火山引擎 TTS 响应中提取音频数据
    /// 响应格式: { "code": 3000, "message": "Success", "data": "base64encodedaudio..." }
    /// </summary>
    private byte[] ParseAudioFromResponse(string responseJson)
    {
        try
        {
            // 手动解析 JSON（避免依赖 Newtonsoft.Json）
            var resp = JsonUtility.FromJson<TTSResponse>(responseJson);
            if (resp == null || resp.code != 3000)
            {
                Debug.LogError($"[TTSManager] API 返回错误 code={resp?.code}, message={resp?.message}");
                return null;
            }

            if (string.IsNullOrEmpty(resp.data))
            {
                Debug.LogError("[TTSManager] API 返回的音频数据为空");
                return null;
            }

            return Convert.FromBase64String(resp.data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TTSManager] 解析响应失败: {e.Message}");
            return null;
        }
    }

    [Serializable]
    private class TTSResponse
    {
        public int code;
        public string message;
        public string data;   // base64 编码的音频
        public string reqid;
        public string sequence;
        public string addition;
    }

    // ---- 音频加载 ----

    /// <summary>
    /// 从字节数组加载 AudioClip（mp3/wav）
    /// Unity 原生支持通过文件路径加载 mp3/wav，需要先写入临时文件
    /// </summary>
    private IEnumerator LoadAudioClipFromBytes(byte[] audioData, string textHash, Action<AudioClip> callback)
    {
        string extension = encoding.ToLower() switch
        {
            "wav" => "wav",
            "ogg" => "ogg",
            _ => "mp3"
        };

        string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, $"tts_{textHash}.{extension}");

        try
        {
            System.IO.File.WriteAllBytes(tempPath, audioData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TTSManager] 写入临时文件失败: {e.Message}");
            callback?.Invoke(null);
            yield break;
        }

        // 使用 UnityWebRequest 加载音频文件（支持 AudioType 自动检测）
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
            "file://" + tempPath,
            extension == "wav" ? AudioType.WAV : (extension == "ogg" ? AudioType.OGGVORBIS : AudioType.MPEG)))
        {
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError($"[TTSManager] 加载音频失败: {request.error}");
                callback?.Invoke(null);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip != null)
            {
                clip.name = $"TTS_{textHash.Substring(0, 8)}";
            }
            callback?.Invoke(clip);
        }

        // 清理临时文件
        try
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
        catch { /* 忽略清理错误 */ }
    }

    // ---- 磁盘缓存 ----

    private string GetDiskCachePath(string textHash)
    {
        return System.IO.Path.Combine(Application.persistentDataPath, CACHE_DIR, $"{textHash}.{encoding}");
    }

    private void LoadDiskCacheIndex()
    {
        string cacheDir = System.IO.Path.Combine(Application.persistentDataPath, CACHE_DIR);
        if (!System.IO.Directory.Exists(cacheDir)) return;

        try
        {
            var files = System.IO.Directory.GetFiles(cacheDir, $"*.{encoding}");
            foreach (var file in files)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                _diskCacheSet.Add(name);
            }
            Debug.Log($"[TTSManager] 磁盘缓存索引加载完成: {_diskCacheSet.Count} 条");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TTSManager] 加载磁盘缓存索引失败: {e.Message}");
        }
    }

    private void SaveToDisk(string filePath, byte[] audioData, string textHash)
    {
        try
        {
            string dir = System.IO.Path.GetDirectoryName(filePath);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            System.IO.File.WriteAllBytes(filePath, audioData);
            _diskCacheSet.Add(textHash);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TTSManager] 保存磁盘缓存失败: {e.Message}");
        }
    }

    private IEnumerator LoadFromDisk(string filePath, string text,
        Action<AudioClip> onComplete, Action<string> onError)
    {
        string extension = encoding.ToLower() switch
        {
            "wav" => "wav",
            "ogg" => "ogg",
            _ => "mp3"
        };

        AudioType audioType = extension == "wav" ? AudioType.WAV
            : (extension == "ogg" ? AudioType.OGGVORBIS : AudioType.MPEG);

        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
            "file://" + filePath, audioType))
        {
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogWarning($"[TTSManager] 磁盘缓存读取失败: {request.error}，将重新请求 API");

                // 移除损坏的缓存索引
                string textHash = GetTextHash(text);
                _diskCacheSet.Remove(textHash);

                // 回退到 API 请求
                string diskPath = GetDiskCachePath(textHash);
                StartCoroutine(RequestTTSSynthesis(text, textHash, diskPath, onComplete, onError));
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip != null)
            {
                string textHash = GetTextHash(text);
                clip.name = $"TTS_{textHash.Substring(0, 8)}";
                _clipCache[textHash] = clip; // 存入内存缓存
                Debug.Log($"[TTSManager] 磁盘缓存命中: {text.Substring(0, Math.Min(text.Length, 20))}...");
                onComplete?.Invoke(clip);
            }
            else
            {
                onError?.Invoke("磁盘缓存加载失败");
            }
        }
    }

    // ---- 工具方法 ----

    private static string GetTextHash(string text)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(text + "_v1"); // 加版本号避免音色变更后缓存不更新
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
                sb.Append(hashBytes[i].ToString("x2"));
            return sb.ToString();
        }
    }

    private static string EscapeJson(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }
}
