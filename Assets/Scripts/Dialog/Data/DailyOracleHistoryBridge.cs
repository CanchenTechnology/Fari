using System;
using System.Collections;
using System.Collections.Generic;
using GamerFrameWork.OracleRuntime;
using UnityEngine;

/// <summary>
/// Bridges the daily oracle cache into the common divination history store.
/// </summary>
public static class DailyOracleHistoryBridge
{
    private const string DailyScene = "daily_oracle";
    private const string DailySpreadKind = "daily_oracle";
    private const float SaveWhenReadyTimeout = 10f;

    public static DivinationRecordData SavePreparedReading(TodayOraclePreparedReading preparedReading, bool saveCloud)
    {
        DivinationRecordData record = BuildRecord(preparedReading);
        return SaveRecord(record, saveCloud);
    }

    public static DivinationRecordData SaveToday(TarotCard card, bool upright, TodayOraclePayload payload, string locale, bool saveCloud)
    {
        DivinationRecordData record = BuildRecord(card, upright, payload, locale, DateTime.Now.ToString("o"));
        return SaveRecord(record, saveCloud);
    }

    public static DivinationRecordData SaveDailyRecord(DailyOracleCloudRecord dailyRecord, bool saveCloud)
    {
        DivinationRecordData record = BuildRecord(dailyRecord);
        return SaveRecord(record, saveCloud);
    }

    public static DivinationRecordData SyncTodayLocalToHistory(bool saveCloud = false)
    {
        Debug.LogWarning("[DailyOracleHistoryBridge] 历史记录不再从本地缓存同步，请直接保存到 Firebase。");
        return null;
    }

    public static List<DivinationRecordData> SyncRecentLocalToHistory(int daysBack = 30, bool saveCloud = false)
    {
        Debug.LogWarning("[DailyOracleHistoryBridge] 历史记录不再从本地缓存同步，请直接保存到 Firebase。");
        return new List<DivinationRecordData>();
    }

    public static DivinationRecordData BuildRecord(TodayOraclePreparedReading preparedReading)
    {
        if (preparedReading == null) return null;

        TarotCard card = preparedReading.card;
        if (card == null && !string.IsNullOrWhiteSpace(preparedReading.cardId))
            card = TarotDeck.GetById(preparedReading.cardId);

        TodayOraclePayload payload = preparedReading.oraclePayload ?? new TodayOraclePayload
        {
            title = "今日神谕",
            oracle = preparedReading.cardMeaning ?? "",
            detail = preparedReading.cardDescription ?? "",
            dos = new List<string>(),
            donts = new List<string>(),
            microAction = ""
        };

        return BuildRecord(card, preparedReading.upright, payload, DailyOracleService.CurrentLocale, preparedReading.preparedAt);
    }

    public static DivinationRecordData BuildRecord(DailyOracleCloudRecord dailyRecord)
    {
        if (dailyRecord == null || !dailyRecord.HasPayload)
            return null;

        TarotCard card = TarotDeck.GetById(dailyRecord.cardId);
        TodayOraclePayload payload = dailyRecord.ToPayload();
        string createdAt = FirstNonEmpty(dailyRecord.createdAtLocal, BuildDateCreatedAt(dailyRecord.date));
        string cardName = FirstNonEmpty(dailyRecord.cardName, card?.nameZh, "今日牌");
        bool upright = dailyRecord.orientation != "reversed";

        return BuildRecord(
            card,
            upright,
            payload,
            dailyRecord.locale,
            createdAt,
            dailyRecord.date,
            dailyRecord.cardId,
            cardName,
            FirstNonEmpty(dailyRecord.oracleId, GetCurrentOracleId()));
    }

    public static DivinationRecordData BuildRecord(
        TarotCard card,
        bool upright,
        TodayOraclePayload payload,
        string locale,
        string createdAt,
        string date = null,
        string cardIdOverride = null,
        string cardNameOverride = null,
        string oracleIdOverride = null)
    {
        if (card == null && string.IsNullOrWhiteSpace(cardIdOverride))
            return null;

        string safeDate = FirstNonEmpty(date, ExtractDate(createdAt), DateTime.Now.ToString("yyyy-MM-dd"));
        string cardId = FirstNonEmpty(cardIdOverride, card?.cardId);
        string cardName = FirstNonEmpty(cardNameOverride, card?.nameZh, "今日牌");
        TodayOraclePayload safePayload = payload ?? new TodayOraclePayload();

        return new DivinationRecordData
        {
            readingId = BuildReadingId(safeDate),
            question = $"今日神谕 · {safeDate}",
            scene = DailyScene,
            spreadKind = DailySpreadKind,
            lockedCards = new List<LockedCard>
            {
                new LockedCard
                {
                    positionKey = "daily",
                    position = "今日牌",
                    cardId = cardId,
                    cardName = cardName,
                    orientation = upright ? "upright" : "reversed"
                }
            },
            shortVerdict = FirstNonEmpty(safePayload.title, safePayload.oracle, $"今日神谕 · {cardName}"),
            judgeContent = FirstNonEmpty(safePayload.oracle, safePayload.title, $"今日牌是{cardName}。"),
            adviceContent = BuildAdviceText(safePayload),
            topics = BuildTopics(cardName),
            oracleId = FirstNonEmpty(oracleIdOverride, GetCurrentOracleId()),
            createdAt = FirstNonEmpty(createdAt, BuildDateCreatedAt(safeDate), DateTime.Now.ToString("o"))
        };
    }

    private static DivinationRecordData SaveRecord(DivinationRecordData record, bool saveCloud)
    {
        if (record == null) return null;

        if (!saveCloud)
        {
            return record;
        }

        DivinationRecordFirestore store = GetRecordStore();
        if (store != null)
        {
#if UNITY_EDITOR
            if (Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser == null)
            {
                DivinationRecordFirestore.SaveRecordLocal(record);
                return record;
            }
#endif

            if (store.IsReady && Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser != null)
                SaveToFirestore(store, record);
            else
                store.StartCoroutine(SaveToFirestoreWhenReady(store, record));
        }
        else
        {
            Debug.LogWarning($"[DailyOracleHistoryBridge] 历史服务不可用，未保存每日神谕历史: {record.readingId}");
        }

        return record;
    }

    private static DivinationRecordFirestore GetRecordStore()
    {
        DivinationRecordFirestore store = DivinationRecordFirestore.Instance;
        if (store != null)
            return store;

        GameObject go = new GameObject("DivinationRecordFirestore");
        return go.AddComponent<DivinationRecordFirestore>();
    }

    private static IEnumerator SaveToFirestoreWhenReady(DivinationRecordFirestore store, DivinationRecordData record)
    {
        float elapsed = 0f;
        while (store != null
            && (!store.IsReady || Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser == null)
            && elapsed < SaveWhenReadyTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (store == null || !store.IsReady || Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser == null)
        {
            Debug.LogWarning($"[DailyOracleHistoryBridge] 历史服务暂未就绪，未保存每日神谕历史: {record?.readingId}");
            yield break;
        }

        SaveToFirestore(store, record);
    }

    private static void SaveToFirestore(DivinationRecordFirestore store, DivinationRecordData record)
    {
        if (store == null || record == null) return;
        store.SaveRecord(record, success =>
        {
            if (!success)
                Debug.LogWarning($"[DailyOracleHistoryBridge] 每日神谕历史云端保存失败: {record.readingId}");
        });
    }

    private static string BuildReadingId(string date)
    {
        string safeDate = string.IsNullOrWhiteSpace(date) ? DateTime.Now.ToString("yyyy-MM-dd") : date;
        return "daily_oracle_" + safeDate.Replace("-", "_");
    }

    private static string BuildAdviceText(TodayOraclePayload payload)
    {
        if (payload == null) return "";

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(payload.detail))
            parts.Add(payload.detail.Trim());
        if (payload.dos != null && payload.dos.Count > 0)
            parts.Add("今日宜：" + string.Join("、", payload.dos));
        if (payload.donts != null && payload.donts.Count > 0)
            parts.Add("今日不宜：" + string.Join("、", payload.donts));
        if (!string.IsNullOrWhiteSpace(payload.microAction))
            parts.Add("微行动：" + payload.microAction.Trim());

        return string.Join("\n", parts);
    }

    private static List<string> BuildTopics(string cardName)
    {
        return new List<string>
        {
            $"今天的「{cardName}」还想提醒我什么？",
            "我今天最该注意的行动是什么？",
            "这张牌和我最近的状态有什么关系？"
        };
    }

    private static string BuildDateCreatedAt(string date)
    {
        if (DateTime.TryParse(date, out DateTime parsed))
            return parsed.ToString("yyyy-MM-dd") + " 09:00:00";
        return DateTime.Now.ToString("o");
    }

    private static string ExtractDate(string createdAt)
    {
        if (DateTime.TryParse(createdAt, out DateTime parsed))
            return parsed.ToString("yyyy-MM-dd");
        return "";
    }

    private static string GetCurrentOracleId()
    {
        if (RoleManager.Instance == null) return "tarot";
        return RoleManager.Instance.characterType switch
        {
            CharacterType.Astrologer => "astrology",
            CharacterType.Meditator => "sage",
            _ => "tarot",
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null) return "";
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return "";
    }
}
