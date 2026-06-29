using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json.Linq;
using GamerFrameWork.OracleRuntime;

/// <summary>
/// Daily Oracle Service — 每日神谕 AI 生成服务
/// 负责：
///   1. 构建 daily_oracle 场景的 AI 请求（通过 ContextAssembler）
///   2. 调用 AI API 获取 AI 生成内容
///   3. 解析 AI 响应并填充 TodayOraclePayload
///   4. 缓存结果供 UI 层读取
/// </summary>
public class DailyOracleService : MonoBehaviour
{
    public static DailyOracleService Instance { get; private set; }

    /// <summary>当前应用语言（如 "zh-CN", "en-US"）</summary>
    public static string CurrentLocale = "zh-CN";

    /// <summary>当前缓存的 AI 生成神谕结果</summary>
    public TodayOraclePayload CachedPayload { get; private set; }

    /// <summary>缓存的完整解读结果</summary>
    public CompleteInterpretationPayload CachedInterpretation { get; private set; }

    /// <summary>当前缓存的完整预生成结果</summary>
    public TodayOraclePreparedReading CachedPreparedReading { get; private set; }

    /// <summary>当前卡牌（最近一次请求的牌）</summary>
    public TarotCard CurrentCard { get; private set; }

    /// <summary>当前卡牌是否正位</summary>
    public bool CurrentUpright { get; private set; }

    /// <summary>今日神谕是否正在请求中</summary>
    public bool IsOracleLoading { get; private set; }

    /// <summary>完整解读是否正在请求中</summary>
    public bool IsInterpretationLoading { get; private set; }

    /// <summary>是否有任意 AI 请求正在进行中（兼容旧代码）</summary>
    public bool IsLoading => IsOracleLoading || IsInterpretationLoading;

    /// <summary>生成完成事件（用于 UI 异步更新）</summary>
    public event Action<TodayOraclePayload> OnOracleGenerated;

    /// <summary>完整解读生成完成事件</summary>
    public event Action<CompleteInterpretationPayload> OnInterpretationGenerated;

    private DeepSeekAPI _deepSeekAPI;
    private string _cachedOracleCardId;
    private bool _cachedOracleUpright;
    private string _cachedInterpretationCardId;
    private bool _cachedInterpretationUpright;
    private string _cachedPreparedCardId;
    private bool _cachedPreparedUpright;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _deepSeekAPI = DeepSeekAPI.ResolveFor(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 请求 AI 生成今日神谕
    /// </summary>
    /// <param name="card">今日塔罗牌</param>
    /// <param name="upright">是否正位</param>
    /// <param name="onComplete">完成回调（null 表示只更新 CachedPayload）</param>
    public void RequestDailyOracle(TarotCard card, bool upright, Action<TodayOraclePayload> onComplete = null)
    {
        if (CachedPayload != null && IsCachedOracleFor(card, upright) && !IsOracleLoading)
        {
            onComplete?.Invoke(CachedPayload);
            return;
        }

        // 如果已在加载中，排队回调
        if (IsOracleLoading)
        {
            if (onComplete != null)
            {
                Action<TodayOraclePayload> handler = null;
                handler = (payload) =>
                {
                    OnOracleGenerated -= handler;
                    onComplete(payload);
                };
                OnOracleGenerated += handler;
            }
            return;
        }

        StartCoroutine(RequestDailyOracleRoutine(card, upright, onComplete));
    }

    public bool TryRestoreFromRecord(DailyOracleCloudRecord record, out TodayOraclePreparedReading preparedReading)
    {
        preparedReading = null;
        if (record == null || !record.HasPayload) return false;

        TarotCard card = TarotDeck.GetById(record.cardId);
        if (card == null) return false;

        bool upright = record.IsUpright;
        TodayOraclePayload payload = record.ToPayload();

        CurrentCard = card;
        CurrentUpright = upright;
        CachedPayload = payload;
        MarkOracleCache(card, upright);

        DivinationEngine.Instance?.SetTodayCardFromCloud(card, upright, record.date);

        CachedPreparedReading = BuildPreparedReading(card, upright, payload, null);
        MarkPreparedCache(card, upright);
        preparedReading = CachedPreparedReading;
        DailyOracleHistoryBridge.SaveDailyRecord(record, true);
        return true;
    }

    private System.Collections.IEnumerator RequestDailyOracleRoutine(
        TarotCard card, bool upright, Action<TodayOraclePayload> onComplete)
    {
        IsOracleLoading = true;
        CurrentCard = card;
        CurrentUpright = upright;

        bool cloudChecked = false;
        DailyOracleCloudRecord cloudRecord = null;
        var dailyOracleStore = DailyOracleFirestore.Instance;
        if (dailyOracleStore != null && dailyOracleStore.IsReady)
        {
            dailyOracleStore.LoadToday(record =>
            {
                cloudRecord = record;
                cloudChecked = true;
            });
            yield return new WaitUntil(() => cloudChecked);

            if (cloudRecord != null && cloudRecord.HasPayload)
            {
                var cloudCard = TarotDeck.GetById(cloudRecord.cardId);
                if (cloudCard != null)
                {
                    card = cloudCard;
                    upright = cloudRecord.IsUpright;
                    CurrentCard = cloudCard;
                    CurrentUpright = upright;
                    DivinationEngine.Instance?.SetTodayCardFromCloud(cloudCard, upright, cloudRecord.date);
                }

                CachedPayload = cloudRecord.ToPayload();
                MarkOracleCache(card, upright);
                DailyOracleHistoryBridge.SaveDailyRecord(cloudRecord, true);
                IsOracleLoading = false;
                Debug.Log($"[DailyOracleService] 使用云端今日神谕缓存: {cloudRecord.cardId}");
                onComplete?.Invoke(CachedPayload);
                OnOracleGenerated?.Invoke(CachedPayload);
                yield break;
            }
        }
        else
        {
            DailyOracleCloudRecord localRecord = DailyOracleFirestore.LoadTodayLocal();
            if (TryRestoreFromRecord(localRecord, out TodayOraclePreparedReading restoredReading))
            {
                CachedPayload = restoredReading.oraclePayload;
                IsOracleLoading = false;
                Debug.Log($"[DailyOracleService] 使用本地今日神谕缓存: {localRecord.cardId}");
                onComplete?.Invoke(CachedPayload);
                OnOracleGenerated?.Invoke(CachedPayload);
                yield break;
            }
        }

        // 1. 构建 ChatPayload
        var payload = BuildDailyOraclePayload(card, upright);

        // 2. 获取用于 Prompt 的 MemorySource（用户关闭共享时为空记忆源）
        var memorySource = DialogSystem.Instance?.GetMemorySourceForPrompt();

        // 3. 通过 ContextAssembler 组装 daily_oracle 场景消息
        var assemblyResult = ContextAssembler.AssembleSceneCall(
            "daily_oracle", payload, memorySource, oracleVoiceId: "tarot_reader");

        if (assemblyResult?.messages == null || assemblyResult.messages.Count == 0)
        {
            Debug.LogError("[DailyOracleService] 组装消息失败，使用降级模板");
            CachedPayload = BuildFallbackPayload(card, upright, CurrentLocale);
            MarkOracleCache(card, upright);
            DailyOracleFirestore.SaveTodayLocalPending(card, upright, CachedPayload, CurrentLocale);
            IsOracleLoading = false;
            onComplete?.Invoke(CachedPayload);
            OnOracleGenerated?.Invoke(CachedPayload);
            yield break;
        }

        // 4. 转换为 AI API Message 格式
        var apiMessages = new List<DeepSeekAPI.Message>();
        foreach (var cm in assemblyResult.messages)
        {
            apiMessages.Add(new DeepSeekAPI.Message(cm.role, cm.content));
        }

        // 5. 发送到 AI API
        string aiResponse = null;
        string errorMsg = null;
        bool completed = false;

        _deepSeekAPI.SendChatRequest(apiMessages,
            (response) =>
            {
                aiResponse = response;
                completed = true;
            },
            (error) =>
            {
                errorMsg = error;
                completed = true;
            });

        // 等待请求完成
        yield return new WaitUntil(() => completed);

        // 6. 解析 AI 响应
        if (!string.IsNullOrEmpty(aiResponse))
        {
            CachedPayload = ParseDailyOracleResponse(aiResponse, card, upright, CurrentLocale);
            MarkOracleCache(card, upright);
            DailyOracleFirestore.SaveTodayLocalPending(card, upright, CachedPayload, CurrentLocale);
            Debug.Log($"[DailyOracleService] AI 神谕生成成功: {card.nameZh}");
        }
        else
        {
            Debug.LogWarning($"[DailyOracleService] AI 请求失败: {errorMsg}，使用降级模板");
            CachedPayload = BuildFallbackPayload(card, upright, CurrentLocale);
            MarkOracleCache(card, upright);
            DailyOracleFirestore.SaveTodayLocalPending(card, upright, CachedPayload, CurrentLocale);
        }

        DialogSystem.Instance?.RecordExternalPrompt(
            assemblyResult.promptRecord,
            string.IsNullOrEmpty(aiResponse) ? CachedPayload?.detail : aiResponse);

        DailyOracleFirestore.Instance?.SaveToday(card, upright, CachedPayload, CurrentLocale);
        DailyOracleHistoryBridge.SaveToday(card, upright, CachedPayload, CurrentLocale, true);

        IsOracleLoading = false;
        onComplete?.Invoke(CachedPayload);
        OnOracleGenerated?.Invoke(CachedPayload);
    }

    /// <summary>
    /// 获取缓存的今日神谕（可能为 null）
    /// </summary>
    public TodayOraclePayload GetOraclePayload()
    {
        return CachedPayload;
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public void ClearCache()
    {
        CachedPayload = null;
        CachedInterpretation = null;
        CachedPreparedReading = null;
        _cachedOracleCardId = null;
        _cachedInterpretationCardId = null;
        _cachedPreparedCardId = null;
    }

    /// <summary>
    /// 翻牌时预热今日神谕和完整解读，保证后续 UI 读取的是同一张牌的同一份缓存。
    /// </summary>
    public void PreloadTodayReading(TarotCard card, bool upright, Action<TodayOraclePayload> onOracleReady = null,
        Action<CompleteInterpretationPayload> onInterpretationReady = null)
    {
        if (card == null) return;
        RequestDailyOracle(card, upright, onOracleReady);
        RequestCompleteInterpretation(card, upright, onInterpretationReady);
    }

    /// <summary>
    /// 翻牌准备：生成今日牌 UI 所需的完整数据包。
    /// 回调返回后，UI 再显示卡牌、打开阅读页或完整解读页。
    /// </summary>
    public void PrepareTodayReading(TarotCard card, bool upright, Action<TodayOraclePreparedReading> onReady = null)
    {
        if (card == null)
        {
            onReady?.Invoke(null);
            return;
        }

        if (IsCachedPreparedReadingFor(card, upright) && !IsLoading)
        {
            onReady?.Invoke(CachedPreparedReading);
            return;
        }

        StartCoroutine(PrepareTodayReadingRoutine(card, upright, onReady));
    }

    public bool IsSameCurrentCard(TarotCard card, bool upright)
    {
        return card != null
            && CurrentCard != null
            && CurrentCard.cardId == card.cardId
            && CurrentUpright == upright;
    }

    public bool IsCachedOracleFor(TarotCard card, bool upright)
    {
        return card != null
            && CachedPayload != null
            && _cachedOracleCardId == card.cardId
            && _cachedOracleUpright == upright;
    }

    public bool IsCachedInterpretationFor(TarotCard card, bool upright)
    {
        return card != null
            && CachedInterpretation != null
            && _cachedInterpretationCardId == card.cardId
            && _cachedInterpretationUpright == upright;
    }

    public bool IsCachedPreparedReadingFor(TarotCard card, bool upright)
    {
        return card != null
            && CachedPreparedReading != null
            && _cachedPreparedCardId == card.cardId
            && _cachedPreparedUpright == upright;
    }

    private void MarkOracleCache(TarotCard card, bool upright)
    {
        _cachedOracleCardId = card?.cardId;
        _cachedOracleUpright = upright;
    }

    private void MarkInterpretationCache(TarotCard card, bool upright)
    {
        _cachedInterpretationCardId = card?.cardId;
        _cachedInterpretationUpright = upright;
    }

    private void MarkPreparedCache(TarotCard card, bool upright)
    {
        _cachedPreparedCardId = card?.cardId;
        _cachedPreparedUpright = upright;
    }

    private System.Collections.IEnumerator PrepareTodayReadingRoutine(
        TarotCard card, bool upright, Action<TodayOraclePreparedReading> onReady)
    {
        CurrentCard = card;
        CurrentUpright = upright;

        TodayOraclePayload oraclePayload = null;
        CompleteInterpretationPayload interpretationPayload = null;
        bool oracleReady = false;
        bool interpretationReady = false;

        // 先确定今日神谕。若云端已缓存今日牌，这一步会把 card/upright 切到云端结果。
        RequestDailyOracle(card, upright, (payload) =>
        {
            oraclePayload = payload;
            oracleReady = true;
        });

        yield return new WaitUntil(() => oracleReady);

        if (CurrentCard != null && !IsSameCurrentCard(card, upright))
        {
            card = CurrentCard;
            upright = CurrentUpright;
        }

        RequestCompleteInterpretation(card, upright, (payload) =>
        {
            interpretationPayload = payload;
            interpretationReady = true;
        });

        yield return new WaitUntil(() => interpretationReady);

        CachedPreparedReading = BuildPreparedReading(card, upright, oraclePayload, interpretationPayload);
        MarkPreparedCache(card, upright);
        onReady?.Invoke(CachedPreparedReading);
    }

    private TodayOraclePreparedReading BuildPreparedReading(
        TarotCard card, bool upright, TodayOraclePayload oraclePayload,
        CompleteInterpretationPayload interpretationPayload)
    {
        var cardPayload = new TodayCardPayload
        {
            cardId = card.cardId,
            cardName = card.nameEn,
            displayName = card.DisplayName(upright),
            nameZh = card.nameZh,
            orientation = upright ? "upright" : "reversed",
            generatedAt = DateTime.Now.ToString("o"),
            oracleText = oraclePayload?.oracle,
            title = oraclePayload?.title ?? "今日塔罗"
        };

        return new TodayOraclePreparedReading
        {
            card = card,
            upright = upright,
            cardId = card.cardId,
            cardDisplayName = card.DisplayName(upright),
            cardDescription = FirstNonEmpty(oraclePayload?.detail, interpretationPayload?.description),
            cardMeaning = FirstNonEmpty(interpretationPayload?.meaningAnalysis, oraclePayload?.oracle),
            cardIcon = TarotSpriteLoader.Load(card.cardId),
            cardPayload = cardPayload,
            oraclePayload = oraclePayload,
            interpretationPayload = interpretationPayload,
            preparedAt = DateTime.Now.ToString("o")
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null) return "";
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return "";
    }

    /// <summary>
    /// 获取当前卡牌的 TodayCardPayload（用于传递给 DialogSystem）
    /// </summary>
    public TodayCardPayload GetTodayCardPayload()
    {
        if (CurrentCard == null) return null;

        TodayOraclePayload oraclePayload = null;
        if (IsCachedOracleFor(CurrentCard, CurrentUpright))
        {
            oraclePayload = CachedPayload;
        }
        else if (IsCachedPreparedReadingFor(CurrentCard, CurrentUpright))
        {
            oraclePayload = CachedPreparedReading?.oraclePayload;
        }

        return new TodayCardPayload
        {
            cardId = CurrentCard.cardId,
            cardName = CurrentCard.nameEn,
            displayName = CurrentCard.DisplayName(CurrentUpright),
            nameZh = CurrentCard.nameZh,
            orientation = CurrentUpright ? "upright" : "reversed",
            generatedAt = DateTime.Now.ToString("o"),
            oracleText = oraclePayload?.oracle,
            title = FirstNonEmpty(oraclePayload?.title, "今日塔罗")
        };
    }

    // ================================================================
    // 私有方法
    // ================================================================

    /// <summary>
    /// 构建 Daily Oracle 场景的 ChatPayload
    /// </summary>
    private ChatPayload BuildDailyOraclePayload(TarotCard card, bool upright)
    {
        var orientation = upright ? "upright" : "reversed";
        var cardSummary = card.ToSummary(upright);
        var locale = CurrentLocale;
        var ls = GetLocStrings(locale);

        // 预计算所有可能含引号的变量，避免 $"" 内部嵌套引号转义问题
        var arcanaLabel = card.arcana == "major" ? ls.MajorArcana : ls.MinorArcana;
        var orientationLabel = upright ? ls.UprightLabel : ls.ReversedLabel;
        var kwStr = string.Join(ls.KwSep, card.keywords);

        var message = $"[系统指令] 今日已抽牌：{cardSummary}。请根据以上规则，{ls.LangInstr}。"
                    + $"不要解释推理过程，直接输出符合 schema 的 JSON。牌名=\"{card.nameZh}\"，元素={card.element}，"
                    + $"类型={card.arcana}（{arcanaLabel}），"
                    + $"关键词={kwStr}。"
                    + $"方向={orientationLabel}。";

        return new ChatPayload
        {
            scene = "daily_oracle",
            actionKind = "daily_oracle",
            locale = locale,
            message = message,
            todayCard = new TodayCardPayload
            {
                cardId = card.cardId,
                cardName = card.nameEn,
                displayName = card.DisplayName(upright),
                nameZh = card.nameZh,
                orientation = orientation,
                generatedAt = DateTime.Now.ToString("o"),
                title = "今日塔罗"
            },
            activeReadingState = "no_reading",
            user = new UserPayloadProfile
            {
                preferredTone = "tarot_reader",
                locale = locale
            }
        };
    }

    /// <summary>
    /// 解析 AI 返回的每日神谕文本
    /// AI 应该按规则输出纯文本，格式如下：
    ///   {titleField}：xxx
    ///   {oracleField}：xxx
    ///   {detailField}：xxx（3-5句，叙事化解读）
    /// </summary>
    private TodayOraclePayload ParseDailyOracleResponse(string aiResponse, TarotCard card, bool upright, string locale = "zh-CN")
    {
        var ls = GetLocStrings(locale);
        var payload = new TodayOraclePayload
        {
            title = "",
            oracle = "",
            detail = "",
            dos = new List<string>(),
            donts = new List<string>(),
            microAction = ""
        };

        if (TryParseDailyOracleJson(aiResponse, payload))
        {
            NormalizeDailyOraclePayload(payload, card, upright, locale);
            return payload;
        }

        var lines = aiResponse.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        string currentSection = "";

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // 用 locale 感知的字段名检测段落标题
            if (line.Contains(ls.TitleField))
            {
                currentSection = "title";
                var val = ExtractAfter(line, ls.TitleField);
                if (!string.IsNullOrEmpty(val)) payload.title = Truncate(val, ls.TitleMax);
                continue;
            }
            if (line.Contains(ls.OracleField))
            {
                currentSection = "oracle";
                var val = ExtractAfter(line, ls.OracleField);
                if (!string.IsNullOrEmpty(val)) payload.oracle = Truncate(val, ls.OracleMax);
                continue;
            }
            if (line.Contains(ls.DetailField))
            {
                currentSection = "detail";
                var val = ExtractAfter(line, ls.DetailField);
                if (!string.IsNullOrEmpty(val)) payload.detail = val;
                continue;
            }

            // 跳过旧版字段（AI 可能仍会输出 今日宜/今日不宜/微行动 及其英文对应）
            if (line.Contains("今日宜") || line.Contains("今日不宜") || line.Contains("不宜")
                || line.Contains("微行动") || line.Contains("行动")
                || line.Contains("Dos") || line.Contains("Don") || line.Contains("Micro Action"))
            {
                currentSection = "";
                continue;
            }

            // 内容行
            switch (currentSection)
            {
                case "detail":
                    payload.detail += (string.IsNullOrEmpty(payload.detail) ? "" : " ") + line;
                    break;
                case "oracle":
                    if (!string.IsNullOrEmpty(line) && !line.StartsWith("1.") && !line.StartsWith("2.") && !line.StartsWith("3."))
                        payload.oracle += line;
                    break;
                case "title":
                    if (string.IsNullOrEmpty(payload.title))
                        payload.title = line;
                    break;
            }
        }

        NormalizeDailyOraclePayload(payload, card, upright, locale);
        return payload;
    }

    private bool TryParseDailyOracleJson(string aiResponse, TodayOraclePayload payload)
    {
        if (string.IsNullOrWhiteSpace(aiResponse) || payload == null) return false;

        string json = ExtractJsonObject(aiResponse);
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            JObject obj = JObject.Parse(json);
            payload.title = ReadString(obj, "title");
            payload.oracle = ReadString(obj, "oracle_sentence");
            payload.detail = ReadString(obj, "detail");
            payload.dos = ReadStringArray(obj, "do");
            payload.donts = ReadStringArray(obj, "dont");

            if (obj["full_reading"] is JObject fullReading)
            {
                string core = ReadString(fullReading, "core_meaning");
                string today = ReadString(fullReading, "today_meaning");
                string reminder = ReadString(fullReading, "deeper_reminder");
                payload.microAction = ReadString(fullReading, "micro_action");

                var detailParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(payload.detail)) detailParts.Add(payload.detail);
                if (!string.IsNullOrWhiteSpace(core)) detailParts.Add(core);
                if (!string.IsNullOrWhiteSpace(today)) detailParts.Add(today);
                if (!string.IsNullOrWhiteSpace(reminder)) detailParts.Add(reminder);
                payload.detail = string.Join("\n", detailParts.Where(part => !string.IsNullOrWhiteSpace(part)));
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DailyOracleService] 每日神谕 JSON 解析失败，回退旧格式解析: {ex.Message}");
            return false;
        }
    }

    private void NormalizeDailyOraclePayload(TodayOraclePayload payload, TarotCard card, bool upright, string locale)
    {
        if (payload == null) return;

        if (string.IsNullOrEmpty(payload.title))
        {
            payload.title = GetFallbackTitle(card, locale);
        }
        if (string.IsNullOrEmpty(payload.oracle))
        {
            payload.oracle = GetFallbackOracle(card, upright, locale);
        }
        if (string.IsNullOrEmpty(payload.detail))
        {
            payload.detail = BuildFallbackDetail(card, upright, locale);
        }
    }

    /// <summary>
    /// AI 不可用时的降级模板（完全本地生成）
    /// </summary>
    private TodayOraclePayload BuildFallbackPayload(TarotCard card, bool upright, string locale = "zh-CN")
    {
        var ls = GetLocStrings(locale);
        var orientationLabel = upright ? ls.UprightLabel : ls.ReversedLabel;

        return new TodayOraclePayload
        {
            title = GetFallbackTitle(card, locale),
            oracle = GetFallbackOracle(card, upright, locale),
            detail = BuildFallbackDetail(card, upright, locale),
            dos = new List<string>(),
            donts = new List<string>(),
            microAction = ""
        };
    }

    /// <summary>
    /// 本地的降级详情描述
    /// </summary>
    private string BuildFallbackDetail(TarotCard card, bool upright, string locale = "zh-CN")
    {
        var ls = GetLocStrings(locale);
        var arcanaLabel = card.arcana == "major" ? ls.MajorArcana : ls.MinorArcana;
        var elementLabel = MapElementZh(card.element);
        var keywords = card.keywords != null && card.keywords.Count > 0
            ? string.Join(ls.KwSep, card.keywords)
            : "inner awareness";

        if (locale == "en-US")
        {
            if (upright)
            {
                return $"Today you drew {card.DisplayName(upright)}, a {arcanaLabel} card guided by {elementLabel} energy. "
                    + $"Its keywords are {keywords}. The upright {card.nameZh} reminds you that sometimes the answer isn't out there — "
                    + $"it's in the first quiet voice that rises from within when you finally let yourself be still.";
            }
            else
            {
                return $"{card.DisplayName(upright)} has appeared on your daily spread. This {arcanaLabel} card is held by {elementLabel} energy, "
                    + $"with keywords {keywords}. Reversed doesn't mean bad news — it's a gentle but firm reminder: "
                    + $"something you've been ignoring is surfacing in the moonlight. Face it.";
            }
        }
        else
        {
            if (upright)
            {
                return $"今天抽到了{card.DisplayName(upright)}，这张牌属于{arcanaLabel}，由{elementLabel}能量引导。"
                    + $"它的关键词是{keywords}。正位的{card.nameZh}提醒你，有时候答案并不在外面，"
                    + $"而在你安静下来的那一刻，心底浮现的第一个声音里。";
            }
            else
            {
                return $"{card.DisplayName(upright)}来到了你今天的牌面。这张{arcanaLabel}的牌由{elementLabel}能量守护，"
                    + $"关键词是{keywords}。逆位并不代表坏消息，而是一个温柔但坚定的提醒："
                    + $"有些被忽略的东西正在月光下浮现，请正视它。";
            }
        }
    }

    /// <summary>降级标题</summary>
    private static string GetFallbackTitle(TarotCard card, string locale)
    {
        if (locale == "en-US")
            return $"Today's Oracle · {card.nameZh}";
        return $"今日神谕 · {card.nameZh}";
    }

    /// <summary>降级神谕</summary>
    private static string GetFallbackOracle(TarotCard card, bool upright, string locale)
    {
        if (locale == "en-US")
        {
            return upright
                ? $"The upright {card.nameZh} suggests today is the day to take that first step."
                : $"The reversed {card.nameZh} invites you to slow down and look inward.";
        }
        return upright
            ? $"{card.nameZh}的正位能量提示你，今天适合迈出第一步。"
            : $"逆位的{card.nameZh}提醒你放慢脚步，回看内心。";
    }

    // ================================================================
    // 多语言配置
    // ================================================================

    /// <summary>各 locale 的提示词与解析字段配置</summary>
    private struct LocStrings
    {
        public string LangInstr;        // "用中文生成" / "Generate in English"
        public string TitleField;       // "今日标题" / "Today's Title"
        public string OracleField;      // "今日神谕" / "Today's Oracle"
        public string DetailField;      // "详情解释" / "Detailed Reading"
        public string UprightLabel;     // "正位" / "Upright"
        public string ReversedLabel;    // "逆位" / "Reversed"
        public string MajorArcana;      // "大阿卡纳" / "Major Arcana"
        public string MinorArcana;      // "小阿卡纳" / "Minor Arcana"
        public string KwSep;            // "、" / ", "
        public string CharUnit;         // "字" / "chars"
        public int TitleMax;            // 8 / 20
        public int OracleMax;           // 36 / 80
        public string DetailInstr;      // 详情解释的输出要求描述
        public string OldFieldWarning;  // 提醒 AI 不要输出旧版字段

        // 完整解读字段
        public string DescField;        // "卡牌描述" / "Card Description"
        public string TagsField;        // "能量标签" / "Energy Tags"
        public string MeaningField;     // "牌义解析" / "Meaning Analysis"
        public string ActionField;      // "行动建议" / "Action Suggestion"
        public string TopicsField;      // "推荐话题" / "Suggested Topics"
        public string TopicsInstr;      // 话题输出指导
        public string DescInstr;        // 卡牌描述输出指导
        public string TagsInstr;        // 标签输出指导
        public string MeaningInstr;     // 牌义解析输出指导
        public string ActionInstr;      // 行动建议输出指导
        public int DescMaxChars;        // 描述最大长度
        public int MeaningMaxChars;     // 牌义最大长度
        public int ActionMaxChars;      // 行动建议最大长度
        public int TopicMaxChars;       // 单个话题最大长度
    }

    private static LocStrings GetLocStrings(string locale)
    {
        switch (locale)
        {
            case "en-US":
                return new LocStrings
                {
                    LangInstr = "Generate in English",
                    TitleField = "Today's Title",
                    OracleField = "Today's Oracle",
                    DetailField = "Detailed Reading",
                    UprightLabel = "Upright",
                    ReversedLabel = "Reversed",
                    MajorArcana = "Major Arcana",
                    MinorArcana = "Minor Arcana",
                    KwSep = ", ",
                    CharUnit = "chars",
                    TitleMax = 20,
                    OracleMax = 80,
                    DetailInstr = "3-5 sentences in a narrative, conversational tone — like a close friend sharing wisdom under the moonlight. Grounded in the card's upright/reversed meaning and keywords. No jargon.",
                    OldFieldWarning = "Do NOT output any legacy fields.",
                    DescField = "Card Description",
                    TagsField = "Energy Tags",
                    MeaningField = "Meaning Analysis",
                    ActionField = "Action Suggestion",
                    TopicsField = "Questions to Ask",
                    DescInstr = "A warm, narrative description of the card and its energy today, 2-4 sentences. Include the card name, orientation, and how its energy might show up in daily life. Like a gentle friend holding up a mirror.",
                    TagsInstr = "3 single words or short phrases capturing today's emotional/energetic tone, separated by your locale separator.",
                    MeaningInstr = "A deeper interpretation of the card's meaning in the context of today, 3-5 sentences. Grounded in tarot wisdom but shared conversationally, no jargon.",
                    ActionInstr = "A concrete, small action or mindset shift the user can practice today, 2-4 sentences. Practical, gentle, not prescriptive.",
                    TopicsInstr = "4 direct first-person questions the user can tap and send to the AI after this reading. Each must end with a question mark, be under 16 words, and sound like the user is asking for guidance. Avoid abstract topic labels.",
                    DescMaxChars = 300,
                    MeaningMaxChars = 400,
                    ActionMaxChars = 250,
                    TopicMaxChars = 80
                };
            default: // zh-CN and fallback
                return new LocStrings
                {
                    LangInstr = "用中文生成",
                    TitleField = "今日标题",
                    OracleField = "今日神谕",
                    DetailField = "详情解释",
                    UprightLabel = "正位",
                    ReversedLabel = "逆位",
                    MajorArcana = "大阿卡纳",
                    MinorArcana = "小阿卡纳",
                    KwSep = "、",
                    CharUnit = "字",
                    TitleMax = 8,
                    OracleMax = 36,
                    DetailInstr = "3-5句话，以叙事化、朋友般的口吻解读该牌今日对用户的启示，结合牌的正逆位含义和关键词展开，不堆砌术语，像和好朋友月下散步时的低声交谈一样自然",
                    OldFieldWarning = "注意：不要输出旧版字段。",
                    DescField = "卡牌描述",
                    TagsField = "能量标签",
                    MeaningField = "牌义解析",
                    ActionField = "行动建议",
                    TopicsField = "推荐追问",
                    DescInstr = "用温暖叙事化的口吻描述这张牌和它今天的能量，2-4句话。包含牌名、正逆位方向，以及它的能量如何出现在日常生活中。像一位温柔的朋友为你举起一面镜子。",
                    TagsInstr = "3个词语或简短短语，捕捉今天这张牌带来的情绪/能量基调，用你的分隔符分隔。",
                    MeaningInstr = "对这张牌在今日语境下的深层解读，3-5句话。基于塔罗智慧但以对话方式分享，不堆砌术语。",
                    ActionInstr = "今天可以尝试的一个具体的小行动或心态调整，2-4句话。实用、温和，不强求。",
                    TopicsInstr = "4个用户可以直接点选并发给AI的第一人称追问句。每句必须是问句，以问号结尾，控制在22字以内，像用户真的在请求指引；不要写抽象话题或陈述句。",
                    DescMaxChars = 300,
                    MeaningMaxChars = 400,
                    ActionMaxChars = 250,
                    TopicMaxChars = 80
                };
        }
    }

    // ================================================================
    // 工具方法
    // ================================================================

    /// <summary>从行文本中提取字段名之后的内容（支持中英文冒号）</summary>
    private static string ExtractAfter(string line, string fieldName)
    {
        // 尝试中文冒号
        var idx = line.IndexOf('：');
        if (idx < 0) idx = line.IndexOf(':');
        if (idx >= 0 && idx + 1 < line.Length)
            return line.Substring(idx + 1).Trim();
        return "";
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= maxLen) return text;
        return text.Substring(0, maxLen);
    }

    private static string MapElementZh(string element)
    {
        return element switch
        {
            "fire" => "火",
            "water" => "水",
            "air" => "风",
            "earth" => "土",
            "spirit" => "灵",
            _ => "元素"
        };
    }

    // ================================================================
    // 完整解读 (Complete Interpretation)
    // ================================================================

    /// <summary>
    /// 请求 AI 生成完整卡牌解读（描述、标签、牌义、行动建议、话题）
    /// </summary>
    public void RequestCompleteInterpretation(TarotCard card, bool upright,
        Action<CompleteInterpretationPayload> onComplete = null)
    {
        if (CachedInterpretation != null && IsCachedInterpretationFor(card, upright) && !IsInterpretationLoading)
        {
            onComplete?.Invoke(CachedInterpretation);
            return;
        }

        if (IsInterpretationLoading)
        {
            if (onComplete != null)
            {
                Action<CompleteInterpretationPayload> handler = null;
                handler = (payload) =>
                {
                    OnInterpretationGenerated -= handler;
                    onComplete(payload);
                };
                OnInterpretationGenerated += handler;
            }
            return;
        }

        StartCoroutine(RequestCompleteInterpretationRoutine(card, upright, onComplete));
    }

    private System.Collections.IEnumerator RequestCompleteInterpretationRoutine(
        TarotCard card, bool upright, Action<CompleteInterpretationPayload> onComplete)
    {
        IsInterpretationLoading = true;
        CurrentCard = card;
        CurrentUpright = upright;

        var payload = BuildInterpretationPayload(card, upright);
        var memorySource = DialogSystem.Instance?.GetMemorySourceForPrompt();

        var assemblyResult = ContextAssembler.AssembleSceneCall(
            "complete_interpretation", payload, memorySource, oracleVoiceId: "tarot_reader");

        if (assemblyResult?.messages == null || assemblyResult.messages.Count == 0)
        {
            Debug.LogError("[DailyOracleService] 组装解读消息失败，使用降级模板");
            CachedInterpretation = BuildFallbackInterpretation(card, upright, CurrentLocale);
            MarkInterpretationCache(card, upright);
            IsInterpretationLoading = false;
            onComplete?.Invoke(CachedInterpretation);
            OnInterpretationGenerated?.Invoke(CachedInterpretation);
            yield break;
        }

        var apiMessages = new List<DeepSeekAPI.Message>();
        foreach (var cm in assemblyResult.messages)
        {
            apiMessages.Add(new DeepSeekAPI.Message(cm.role, cm.content));
        }

        string aiResponse = null;
        string errorMsg = null;
        bool completed = false;

        _deepSeekAPI.SendChatRequest(apiMessages,
            (response) => { aiResponse = response; completed = true; },
            (error) => { errorMsg = error; completed = true; });

        yield return new WaitUntil(() => completed);

        if (!string.IsNullOrEmpty(aiResponse))
        {
            CachedInterpretation = ParseInterpretationResponse(aiResponse, card, upright, CurrentLocale);
            MarkInterpretationCache(card, upright);
            Debug.Log($"[DailyOracleService] AI 完整解读生成成功: {card.nameZh}");
        }
        else
        {
            Debug.LogWarning($"[DailyOracleService] AI 解读请求失败: {errorMsg}，使用降级模板");
            CachedInterpretation = BuildFallbackInterpretation(card, upright, CurrentLocale);
            MarkInterpretationCache(card, upright);
        }

        DialogSystem.Instance?.RecordExternalPrompt(
            assemblyResult.promptRecord,
            string.IsNullOrEmpty(aiResponse) ? CachedInterpretation?.description : aiResponse);

        IsInterpretationLoading = false;
        onComplete?.Invoke(CachedInterpretation);
        OnInterpretationGenerated?.Invoke(CachedInterpretation);
    }

    private ChatPayload BuildInterpretationPayload(TarotCard card, bool upright)
    {
        var orientation = upright ? "upright" : "reversed";
        var locale = CurrentLocale;
        var ls = GetLocStrings(locale);

        var arcanaLabel = card.arcana == "major" ? ls.MajorArcana : ls.MinorArcana;
        var orientationLabel = upright ? ls.UprightLabel : ls.ReversedLabel;
        var kwStr = string.Join(ls.KwSep, card.keywords);

        var message =
            $"[系统指令] 用户正在查看今日塔罗牌。请根据以上规则，{ls.LangInstr}。" +
            $"不要解释推理过程，直接输出符合 schema 的 JSON。" +
            $"牌名=\"{card.nameZh}\"，元素={card.element}，" +
            $"类型={card.arcana}（{arcanaLabel}），" +
            $"关键词={kwStr}。" +
            $"方向={orientationLabel}。";

        return new ChatPayload
        {
            scene = "complete_interpretation",
            actionKind = "complete_interpretation",
            locale = locale,
            message = message,
            todayCard = new TodayCardPayload
            {
                cardId = card.cardId,
                cardName = card.nameEn,
                displayName = card.DisplayName(upright),
                nameZh = card.nameZh,
                orientation = orientation,
                generatedAt = DateTime.Now.ToString("o"),
                title = "今日塔罗"
            },
            activeReadingState = "no_reading",
            user = new UserPayloadProfile
            {
                preferredTone = "tarot_reader",
                locale = locale
            }
        };
    }

    private CompleteInterpretationPayload ParseInterpretationResponse(
        string aiResponse, TarotCard card, bool upright, string locale = "zh-CN")
    {
        var ls = GetLocStrings(locale);
        var result = new CompleteInterpretationPayload
        {
            description = "",
            tags = new List<string>(),
            meaningAnalysis = "",
            actionSuggestion = "",
            topics = new List<string>()
        };

        if (TryParseInterpretationJson(aiResponse, result))
        {
            NormalizeInterpretationResult(result, card, upright, locale);
            return result;
        }

        var lines = aiResponse.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        string currentSection = "";

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // 检测段落标题
            if (line.Contains(ls.DescField))
            {
                currentSection = "desc";
                var val = ExtractAfter(line, ls.DescField);
                if (!string.IsNullOrEmpty(val)) result.description = val;
                continue;
            }
            if (line.Contains(ls.TagsField))
            {
                currentSection = "tags";
                var val = ExtractAfter(line, ls.TagsField);
                if (!string.IsNullOrEmpty(val))
                {
                    result.tags = ParseTagLine(val, ls);
                }
                continue;
            }
            if (line.Contains(ls.MeaningField))
            {
                currentSection = "meaning";
                var val = ExtractAfter(line, ls.MeaningField);
                if (!string.IsNullOrEmpty(val)) result.meaningAnalysis = val;
                continue;
            }
            if (line.Contains(ls.ActionField))
            {
                currentSection = "action";
                var val = ExtractAfter(line, ls.ActionField);
                if (!string.IsNullOrEmpty(val)) result.actionSuggestion = val;
                continue;
            }
            if (line.Contains(ls.TopicsField))
            {
                currentSection = "topics";
                var val = ExtractAfter(line, ls.TopicsField);
                if (!string.IsNullOrEmpty(val) && IsTopicLine(val))
                    result.topics.Add(CleanTopicLine(val));
                continue;
            }

            // 跳过旧版字段
            if (line.Contains("今日宜") || line.Contains("今日不宜") || line.Contains("不宜")
                || line.Contains("微行动") || line.Contains("Dos") || line.Contains("Don")
                || line.Contains("Micro Action") || line.Contains("今日标题") || line.Contains("今日神谕")
                || line.Contains("Today's Title") || line.Contains("Today's Oracle"))
            {
                currentSection = "";
                continue;
            }

            // 内容行追加
            switch (currentSection)
            {
                case "desc":
                    result.description += (string.IsNullOrEmpty(result.description) ? "" : " ") + line;
                    break;
                case "meaning":
                    result.meaningAnalysis += (string.IsNullOrEmpty(result.meaningAnalysis) ? "" : " ") + line;
                    break;
                case "action":
                    result.actionSuggestion += (string.IsNullOrEmpty(result.actionSuggestion) ? "" : " ") + line;
                    break;
                case "topics":
                    if (IsTopicLine(line))
                        result.topics.Add(CleanTopicLine(line));
                    break;
            }
        }

        NormalizeInterpretationResult(result, card, upright, locale);
        return result;
    }

    private bool TryParseInterpretationJson(string aiResponse, CompleteInterpretationPayload result)
    {
        if (string.IsNullOrWhiteSpace(aiResponse) || result == null) return false;

        string json = ExtractJsonObject(aiResponse);
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            JObject obj = JObject.Parse(json);
            result.description = ReadString(obj, "description");
            result.meaningAnalysis = ReadString(obj, "meaningAnalysis");
            result.actionSuggestion = ReadString(obj, "actionSuggestion");
            result.tags = ReadStringArray(obj, "tags");
            result.topics = ReadStringArray(obj, "topics");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DailyOracleService] 完整解读 JSON 解析失败，回退旧格式解析: {ex.Message}");
            return false;
        }
    }

    private static string ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return "";
        return text.Substring(start, end - start + 1);
    }

    private static string ReadString(JObject obj, string key)
    {
        return obj?[key]?.Type == JTokenType.String
            ? obj[key].Value<string>()?.Trim() ?? ""
            : "";
    }

    private static List<string> ReadStringArray(JObject obj, string key)
    {
        var result = new List<string>();
        JArray arr = obj?[key] as JArray;
        if (arr == null) return result;

        foreach (JToken token in arr)
        {
            string value = token.Type == JTokenType.String ? token.Value<string>()?.Trim() : "";
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value);
        }

        return result;
    }

    private void NormalizeInterpretationResult(CompleteInterpretationPayload result, TarotCard card, bool upright, string locale)
    {
        if (result == null) return;

        if (string.IsNullOrEmpty(result.description))
            result.description = BuildFallbackDescription(card, upright, locale);
        if (result.tags == null || result.tags.Count < 3)
            result.tags = GetFallbackTags(card, upright, locale);
        if (string.IsNullOrEmpty(result.meaningAnalysis))
            result.meaningAnalysis = BuildFallbackMeaning(card, upright, locale);
        if (string.IsNullOrEmpty(result.actionSuggestion))
            result.actionSuggestion = BuildFallbackAction(card, upright, locale);
        if (result.topics == null)
            result.topics = GetFallbackTopics(card, upright, locale);
        result.topics = NormalizeTopicQuestions(result.topics, card, upright, locale);

        // 确保数量正确
        while (result.tags.Count > 3) result.tags.RemoveAt(result.tags.Count - 1);
        while (result.tags.Count < 3) result.tags.Add("");
        while (result.topics.Count > 4) result.topics.RemoveAt(result.topics.Count - 1);
        while (result.topics.Count < 4) result.topics.Add("");
    }

    private CompleteInterpretationPayload BuildFallbackInterpretation(
        TarotCard card, bool upright, string locale = "zh-CN")
    {
        return new CompleteInterpretationPayload
        {
            description = BuildFallbackDescription(card, upright, locale),
            tags = GetFallbackTags(card, upright, locale),
            meaningAnalysis = BuildFallbackMeaning(card, upright, locale),
            actionSuggestion = BuildFallbackAction(card, upright, locale),
            topics = GetFallbackTopics(card, upright, locale)
        };
    }

    // ---- 降级内容生成 ----

    private string BuildFallbackDescription(TarotCard card, bool upright, string locale)
    {
        var ls = GetLocStrings(locale);
        if (locale == "en-US")
        {
            return upright
                ? $"Today you drew {card.DisplayName(upright)}. This card carries a gentle yet powerful energy — like a quiet nudge from the universe inviting you to notice something important."
                : $"{card.DisplayName(upright)} has appeared for you today. Sometimes a reversed card comes not as a warning but as an invitation to look at things from a different angle.";
        }
        return upright
            ? $"今天你抽到了{card.DisplayName(upright)}。这张牌带着一股温柔但有力的能量，像是宇宙在你耳边轻轻说：今天，请留意这件事。"
            : $"{card.DisplayName(upright)}出现在你今天的牌面。有时候，逆位的牌不是警告，而是一个邀请——邀请你换个角度看事情。";
    }

    private List<string> GetFallbackTags(TarotCard card, bool upright, string locale)
    {
        var ls = GetLocStrings(locale);
        if (locale == "en-US")
        {
            return upright
                ? new List<string> { "clarity", "courage", "new beginning" }
                : new List<string> { "reflection", "patience", "inner voice" };
        }
        return upright
            ? new List<string> { "清晰", "勇气", "新的开始" }
            : new List<string> { "反思", "耐心", "聆听内心" };
    }

    private string BuildFallbackMeaning(TarotCard card, bool upright, string locale)
    {
        var ls = GetLocStrings(locale);
        var keywords = card.keywords != null && card.keywords.Count > 0
            ? string.Join(ls.KwSep, card.keywords)
            : (locale == "en-US" ? "inner journey" : "内心探索");

        if (locale == "en-US")
        {
            return upright
                ? $"The {card.nameZh} card speaks to {keywords}. In today's context, this energy shows up as an opportunity to trust yourself more deeply. " +
                  $"When this card appears upright, it suggests the path forward is becoming clearer — even if you can't see the entire road yet. " +
                  $"Trust the first glimpse. Sometimes that's all we need to take the next step."
                : $"When {card.nameZh} appears reversed, it often points to {keywords} that have been neglected or suppressed. " +
                  $"Today might bring these hidden currents to the surface — not to overwhelm you, but to show you what needs attention. " +
                  $"Reversed cards are messengers from the shadow side, and they often carry our most important lessons.";
        }
        return upright
            ? $"{card.nameZh}这张牌的核心是{keywords}。在今天的语境里，这股能量表现为一个邀请——邀请你更信任自己的直觉。" +
              $"正位的{card.nameZh}意味着前路正在变清晰，即使你还看不到完整的地图。有时候，只需要看得到下一步就够了。" +
              $"相信你此刻感受到的第一个信号，它会带你走向对的方向。"
            : $"当{card.nameZh}以逆位出现，它往往指向那些被忽视的{keywords}。今天可能会有些隐藏的情绪浮上水面——不是来压倒你的，" +
              $"而是来提醒你：有些东西需要被看见、被承认。逆位牌是来自阴影面的信使，它们往往携带着最重要的讯息。";
    }

    private string BuildFallbackAction(TarotCard card, bool upright, string locale)
    {
        if (locale == "en-US")
        {
            return upright
                ? $"Find a quiet moment today — even just 5 minutes — to write down one thing you've been hesitating about. Then ask yourself: what's the smallest first step? Do that step today."
                : $"When a quiet moment arrives today, ask yourself: what am I avoiding? Don't try to fix it. Just name it. Naming is the first act of courage.";
        }
        return upright
            ? $"今天找一个安静的片刻——哪怕只有5分钟——写下你最近犹豫不决的一件事。然后问自己：最小的一步是什么？今天就去走那一步。"
            : $"今天安静的时候，问自己一个问题：我在回避什么？不需要急着去解决它。只是把它说出来、写下来。命名本身就是一种勇敢。";
    }

    private List<string> GetFallbackTopics(TarotCard card, bool upright, string locale)
    {
        var name = card.nameZh;
        if (locale == "en-US")
        {
            return new List<string>
            {
                $"What does {name} want me to notice today?",
                $"How does this card guide my relationship question?",
                $"What should I ask myself before acting?",
                $"How can I work with this energy now?"
            };
        }
        return new List<string>
        {
            $"{name}今天想提醒我什么？",
            $"这张牌怎么看我的关系？",
            $"我现在该先问自己什么？",
            $"我该怎么运用这股能量？"
        };
    }

    private static List<string> NormalizeTopicQuestions(List<string> topics, TarotCard card, bool upright, string locale)
    {
        var result = new List<string>();
        if (topics != null)
        {
            foreach (var raw in topics)
            {
                var topic = CleanTopicLine(raw);
                if (IsUsableQuestion(topic, locale) && !result.Contains(topic))
                    result.Add(topic);
                if (result.Count >= 4) break;
            }
        }

        foreach (var fallback in GetStaticFallbackQuestions(card, upright, locale))
        {
            if (result.Count >= 4) break;
            if (!result.Contains(fallback))
                result.Add(fallback);
        }

        return result;
    }

    private static bool IsUsableQuestion(string text, string locale)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        bool isQuestion = trimmed.EndsWith("？", StringComparison.Ordinal) || trimmed.EndsWith("?", StringComparison.Ordinal);
        if (!isQuestion) return false;
        if (trimmed.Contains("是否") && trimmed.Contains("基本")) return false;
        return locale == "en-US"
            ? trimmed.Length <= 90
            : trimmed.Length <= 28;
    }

    private static List<string> GetStaticFallbackQuestions(TarotCard card, bool upright, string locale)
    {
        var name = card?.nameZh ?? (locale == "en-US" ? "this card" : "这张牌");
        if (locale == "en-US")
        {
            return new List<string>
            {
                $"What does {name} want me to notice today?",
                "What is the deeper message for me?",
                "How should I handle my current situation?",
                "What question should I ask myself next?"
            };
        }

        return new List<string>
        {
            $"{name}今天想提醒我什么？",
            "这件事更深层的讯息是什么？",
            "我现在该如何面对现状？",
            "我接下来该问自己什么？"
        };
    }

    // ---- 解析工具 ----

    private static List<string> ParseTagLine(string line, LocStrings ls)
    {
        var tags = new List<string>();
        // 按分隔符拆分
        var parts = line.Split(new[] { ls.KwSep, "，", ",", "、" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var t = p.Trim().TrimStart('#', '·', '-').Trim();
            if (!string.IsNullOrEmpty(t) && t.Length <= 12)
                tags.Add(t);
        }
        return tags;
    }

    private static bool IsTopicLine(string line)
    {
        var t = line.Trim();
        if (string.IsNullOrEmpty(t)) return false;
        // 以数字 + 点/括号 开头，或者直接是非标签的实质内容
        return System.Text.RegularExpressions.Regex.IsMatch(t, @"^\d+[\.\)、]\s*")
            || (t.Length > 5 && !t.Contains("：") && !t.Contains(":"));
    }

    private static string CleanTopicLine(string line)
    {
        var t = line.Trim();
        // 去掉前面的编号
        t = System.Text.RegularExpressions.Regex.Replace(t, @"^\d+[\.\)、]\s*", "");
        // 去掉引号
        t = t.Trim('\"', '\"', '「', '」');
        return t.Trim();
    }

}
