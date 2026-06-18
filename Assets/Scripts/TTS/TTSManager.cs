using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Firebase.Auth;
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

    [Tooltip("单次请求最大文本长度，需与 Cloud Function 限制保持一致")]
    public int maxRequestTextLength = 1200;

    private const string TTS_API_URL = "https://us-central1-fari-app-b2fd2.cloudfunctions.net/ttsSynthesize";
    private const string VOLCANO_TTS_V3_API_URL = "https://openspeech.bytedance.com/api/v3/tts/unidirectional";
    private const string CACHE_DIR = "TTSCache";

    #endregion

    #region Editor 直连调试配置

    [Header("火山 TTS 2.0 / Editor 调试")]
    [Tooltip("Editor 下优先使用下面的火山参数直连 TTS。正式包不生效，正式包始终走 Firebase Functions。")]
    public bool useEditorDirectVolcanoTTS = true;

    [Tooltip("火山控制台 API Key。建议留空并在本机环境变量 VOLC_TTS_API_KEY 配置，避免写入场景资源。")]
    public string editorVolcanoApiKey = "";

    [Tooltip("V3 TTS 资源 ID。TTS 2.0 使用 seed-tts-2.0。正式包会把这个值传给 Firebase Functions。")]
    public string editorVolcanoResourceId = "seed-tts-2.0";

    [Tooltip("V3 TTS 发音人。正式包会把这个值传给 Firebase Functions。TTS 2.0 需要填对应的 bigtts 音色。")]
    public string editorVolcanoSpeaker = "zh_female_vv_uranus_bigtts";

    [Tooltip("V3 TTS 采样率。")]
    public int editorVolcanoSampleRate = 24000;

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

    public bool HasEditorDirectVolcanoConfig()
    {
#if UNITY_EDITOR
        return ShouldUseEditorDirectVolcanoTTS();
#else
        return false;
#endif
    }

    public static TTSManager ResolveFor(GameObject owner)
    {
#if UNITY_EDITOR
        TTSManager[] managers = FindObjectsOfType<TTSManager>();
        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null && managers[i].HasEditorDirectVolcanoConfig())
            {
                return managers[i];
            }
        }
#endif
        if (Instance != null)
        {
            return Instance;
        }

        TTSManager local = owner != null ? owner.GetComponent<TTSManager>() : null;
        if (local != null)
        {
            return local;
        }

        if (owner != null)
        {
            return owner.AddComponent<TTSManager>();
        }

        var go = new GameObject("TTSManager");
        return go.AddComponent<TTSManager>();
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

        if (maxRequestTextLength > 0 && text.Length > maxRequestTextLength)
        {
            onError?.Invoke($"文本过长，请先分段后再合成（当前 {text.Length} 字，限制 {maxRequestTextLength} 字）");
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
        string requestJson = BuildRequestJson(text);

#if UNITY_EDITOR
        if (ShouldUseEditorDirectVolcanoTTS())
        {
            yield return RequestEditorDirectVolcanoTTS(text, textHash, diskPath, onComplete, onError);
            yield break;
        }
#endif

        string idToken = null;
        yield return GetFirebaseIdToken(
            token => idToken = token,
            error =>
            {
                IsSynthesizing = false;
                _currentTextHash = null;
                Debug.LogError($"[TTSManager] Firebase token error: {error}");
                onError?.Invoke(error);
                OnSynthesisError?.Invoke(text, error);
            });
        if (string.IsNullOrEmpty(idToken))
            yield break;
        if (_currentTextHash != textHash)
            yield break;

        using (UnityWebRequest request = new UnityWebRequest(TTS_API_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {idToken}");

            yield return request.SendWebRequest();
            if (_currentTextHash != textHash)
                yield break;

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
                if (_currentTextHash != textHash)
                    return;

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

    private string BuildRequestJson(string text)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        sb.Append($"\"text\":\"{EscapeJson(text)}\",");
        sb.Append($"\"voiceType\":\"{EscapeJson(voiceType)}\",");
        sb.Append($"\"resourceId\":\"{EscapeJson(GetVolcanoResourceId())}\",");
        sb.Append($"\"speaker\":\"{EscapeJson(GetVolcanoSpeaker())}\",");
        sb.Append($"\"encoding\":\"{EscapeJson(encoding)}\",");
        sb.Append($"\"speedRatio\":{speedRatio:F1},");
        sb.Append($"\"volumeRatio\":{volumeRatio:F1}");
        sb.Append("}");
        return sb.ToString();
    }

    private string GetVolcanoResourceId()
    {
        return string.IsNullOrWhiteSpace(editorVolcanoResourceId)
            ? "seed-tts-2.0"
            : editorVolcanoResourceId.Trim();
    }

    private string GetVolcanoSpeaker()
    {
        return string.IsNullOrWhiteSpace(editorVolcanoSpeaker)
            ? "zh_female_vv_uranus_bigtts"
            : editorVolcanoSpeaker.Trim();
    }

#if UNITY_EDITOR
    private bool ShouldUseEditorDirectVolcanoTTS()
    {
        return useEditorDirectVolcanoTTS
            && !string.IsNullOrWhiteSpace(GetEditorVolcanoApiKey())
            && !string.IsNullOrWhiteSpace(GetVolcanoResourceId());
    }

    private string GetEditorVolcanoApiKey()
    {
        if (!string.IsNullOrWhiteSpace(editorVolcanoApiKey))
            return editorVolcanoApiKey.Trim();

        string envApiKey = Environment.GetEnvironmentVariable("VOLC_TTS_API_KEY");
        return string.IsNullOrWhiteSpace(envApiKey) ? string.Empty : envApiKey.Trim();
    }

    private IEnumerator RequestEditorDirectVolcanoTTS(string text, string textHash, string diskPath,
        Action<AudioClip> onComplete, Action<string> onError)
    {
        if (ShouldUseEditorDirectVolcanoTTS())
        {
            yield return RequestEditorDirectVolcanoTTSV3(text, textHash, diskPath, onComplete, onError);
            yield break;
        }

        string errorMsg = "Editor 火山 TTS 未配置 API Key 或 ResourceId";
        IsSynthesizing = false;
        _currentTextHash = null;
        Debug.LogError($"[TTSManager] {errorMsg}");
        onError?.Invoke(errorMsg);
        OnSynthesisError?.Invoke(text, errorMsg);
    }

    private IEnumerator RequestEditorDirectVolcanoTTSV3(string text, string textHash, string diskPath,
        Action<AudioClip> onComplete, Action<string> onError)
    {
        string requestJson = BuildVolcanoV3RequestJson(text);

        using (UnityWebRequest request = new UnityWebRequest(VOLCANO_TTS_V3_API_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Api-Key", GetEditorVolcanoApiKey());
            request.SetRequestHeader("X-Api-Resource-Id", GetVolcanoResourceId());
            request.SetRequestHeader("X-Api-Request-Id", Guid.NewGuid().ToString());

            yield return request.SendWebRequest();
            if (_currentTextHash != textHash)
                yield break;

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.ProtocolError)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                IsSynthesizing = false;
                _currentTextHash = null;

                string errorMsg = BuildVolcanoV3ErrorMessage(request.error, request.downloadHandler.text);
                Debug.LogError($"[TTSManager] {errorMsg}");
                onError?.Invoke(errorMsg);
                OnSynthesisError?.Invoke(text, errorMsg);
                yield break;
            }

            string responseText = request.downloadHandler.text;
            byte[] audioData = ParseVolcanoV3AudioFromChunkedResponse(responseText);

            if (audioData == null || audioData.Length == 0)
            {
                IsSynthesizing = false;
                _currentTextHash = null;

                string errorMsg = $"Editor 火山 TTS V3 响应解析失败: {responseText.Substring(0, Math.Min(responseText.Length, 200))}";
                Debug.LogError($"[TTSManager] {errorMsg}");
                onError?.Invoke(errorMsg);
                OnSynthesisError?.Invoke(text, errorMsg);
                yield break;
            }

            if (enableDiskCache && !string.IsNullOrEmpty(diskPath))
            {
                SaveToDisk(diskPath, audioData, textHash);
            }

            yield return LoadAudioClipFromBytes(audioData, textHash, (clip) =>
            {
                if (_currentTextHash != textHash)
                    return;

                IsSynthesizing = false;
                _currentTextHash = null;

                if (clip != null)
                {
                    _clipCache[textHash] = clip;
                    Debug.Log($"[TTSManager] Editor 火山 TTS V3 合成完成: {text.Substring(0, Math.Min(text.Length, 20))}... -> {clip.length:F1}s");
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

    private string BuildVolcanoV3RequestJson(string text)
    {
        string speaker = GetVolcanoSpeaker();
        string format = NormalizeV3AudioFormat(encoding);
        int speechRate = RatioToVolcanoRate(speedRatio);
        int loudnessRate = RatioToVolcanoRate(volumeRatio);

        StringBuilder sb = new StringBuilder();
        sb.Append("{");
        sb.Append("\"user\":{");
        sb.Append("\"uid\":\"unity-editor\"");
        sb.Append("},");
        sb.Append("\"namespace\":\"BidirectionalTTS\",");
        sb.Append("\"req_params\":{");
        sb.Append($"\"text\":\"{EscapeJson(text)}\",");
        sb.Append($"\"speaker\":\"{EscapeJson(speaker)}\",");
        sb.Append("\"audio_params\":{");
        sb.Append($"\"format\":\"{EscapeJson(format)}\",");
        sb.Append($"\"sample_rate\":{Mathf.Max(8000, editorVolcanoSampleRate)},");
        sb.Append($"\"speech_rate\":{speechRate},");
        sb.Append($"\"loudness_rate\":{loudnessRate}");
        sb.Append("}");
        sb.Append("}");
        sb.Append("}");
        return sb.ToString();
    }

    private string BuildVolcanoV3ErrorMessage(string requestError, string responseText)
    {
        string resourceId = GetVolcanoResourceId();
        string speaker = GetVolcanoSpeaker();
        string message = $"Editor 火山 TTS V3 错误: {requestError}, ResourceId={resourceId}, Speaker={speaker}, 响应: {responseText}";

        if (!string.IsNullOrEmpty(responseText) && responseText.Contains("requested resource not granted"))
        {
            message += "。请在火山控制台确认这个 API Key 已开通并授权对应的 X-Api-Resource-Id；TTS 2.0 通常是 seed-tts-2.0。";
        }

        return message;
    }
#endif

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
            string audioBase64 = resp != null && !string.IsNullOrEmpty(resp.audioBase64)
                ? resp.audioBase64
                : resp?.data;

            if (resp == null || (string.IsNullOrEmpty(audioBase64) && resp.code != 3000))
            {
                Debug.LogError($"[TTSManager] API 返回错误 code={resp?.code}, message={resp?.message}, error={resp?.error}");
                return null;
            }

            if (string.IsNullOrEmpty(audioBase64))
            {
                Debug.LogError("[TTSManager] API 返回的音频数据为空");
                return null;
            }

            return Convert.FromBase64String(audioBase64);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TTSManager] 解析响应失败: {e.Message}");
            return null;
        }
    }

    private byte[] ParseVolcanoV3AudioFromChunkedResponse(string responseText)
    {
        try
        {
            if (string.IsNullOrEmpty(responseText))
                return null;

            List<byte> audioBytes = new List<byte>();
            MatchCollection matches = Regex.Matches(responseText, "\"data\"\\s*:\\s*\"(?<data>(?:\\\\.|[^\"])*)\"");
            foreach (Match match in matches)
            {
                string encoded = match.Groups["data"].Value;
                if (string.IsNullOrEmpty(encoded))
                    continue;

                encoded = encoded.Replace("\\/", "/")
                    .Replace("\\n", "")
                    .Replace("\\r", "")
                    .Replace("\\t", "");

                byte[] chunk = Convert.FromBase64String(encoded);
                audioBytes.AddRange(chunk);
            }

            if (audioBytes.Count > 0)
                return audioBytes.ToArray();

            return ParseAudioFromResponse(responseText);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TTSManager] 解析 V3 响应失败: {e.Message}");
            return null;
        }
    }

    private static string NormalizeV3AudioFormat(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "mp3";

        string lower = value.ToLowerInvariant();
        if (lower == "ogg")
            return "ogg_opus";
        if (lower == "wav")
            return "mp3";
        return lower;
    }

    private static int RatioToVolcanoRate(float ratio)
    {
        if (ratio >= 1f)
            return Mathf.RoundToInt(Mathf.Clamp01(ratio - 1f) * 100f);

        return Mathf.RoundToInt(Mathf.Clamp(ratio - 1f, -0.5f, 0f) * 100f);
    }

#pragma warning disable 0649
    [Serializable]
    private class TTSResponse
    {
        public int code;
        public string message;
        public string audioBase64;
        public string data;   // base64 编码的音频
        public string reqid;
        public string sequence;
        public string addition;
        public string error;
    }
#pragma warning restore 0649

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

    private string GetTextHash(string text)
    {
        using (MD5 md5 = MD5.Create())
        {
            string cacheKey = string.Join("|",
                text ?? "",
                voiceType ?? "",
                encoding ?? "",
                speedRatio.ToString("F2"),
                volumeRatio.ToString("F2"),
                "v2");
            byte[] inputBytes = Encoding.UTF8.GetBytes(cacheKey);
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

    private IEnumerator GetFirebaseIdToken(Action<string> onToken, Action<string> onError)
    {
        var user = FirebaseAuth.DefaultInstance?.CurrentUser;
        if (user == null)
        {
            onError?.Invoke("用户未登录，无法调用 TTS 服务");
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
}
