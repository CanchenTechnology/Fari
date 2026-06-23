using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFGameFrameWork;

/// <summary>
/// 占卜历史内存缓存。只缓存到内存，不写 PlayerPrefs/本地文件。
/// </summary>
public class DivinationHistoryCacheService : MonoSingleton<DivinationHistoryCacheService>
{
    private const float ReadyTimeoutSeconds = 8f;
    private const float RefreshThrottleSeconds = 20f;
    private const int DailyOracleMergeDays = 30;

    private readonly List<DivinationRecordData> _cachedRecords = new List<DivinationRecordData>();
    private readonly List<Action<List<DivinationRecordData>, bool>> _pendingCallbacks =
        new List<Action<List<DivinationRecordData>, bool>>();

    private Coroutine _refreshRoutine;
    private float _lastRefreshAt = -999f;

    public bool HasLoadedOnce { get; private set; }
    public bool IsRefreshing => _refreshRoutine != null;
    public int CachedCount => _cachedRecords.Count;

    public event Action<List<DivinationRecordData>, bool> RecordsUpdated;

    public List<DivinationRecordData> GetSnapshot()
    {
        return new List<DivinationRecordData>(_cachedRecords);
    }

    public void Warmup(bool force = false)
    {
        Refresh(force);
    }

    public void Refresh(bool force = false, Action<List<DivinationRecordData>, bool> onComplete = null)
    {
        if (onComplete != null)
            _pendingCallbacks.Add(onComplete);

        if (_refreshRoutine != null)
            return;

        if (!force && HasLoadedOnce && Time.realtimeSinceStartup - _lastRefreshAt < RefreshThrottleSeconds)
        {
            FlushPendingCallbacks(GetSnapshot(), true);
            return;
        }

        _refreshRoutine = StartCoroutine(RefreshRoutine());
    }

    public void UpsertRecord(DivinationRecordData record)
    {
        if (record == null || string.IsNullOrEmpty(record.readingId))
            return;

        AddOrReplaceRecord(_cachedRecords, record);
        SortRecordsDescending(_cachedRecords);
        HasLoadedOnce = true;
        _lastRefreshAt = Time.realtimeSinceStartup;
        NotifyUpdated(true);
    }

    public void RemoveRecord(string readingId)
    {
        if (string.IsNullOrEmpty(readingId))
            return;

        _cachedRecords.RemoveAll(record => record == null || record.readingId == readingId);
        HasLoadedOnce = true;
        _lastRefreshAt = Time.realtimeSinceStartup;
        NotifyUpdated(true);
    }

    public void ClearMemoryCache()
    {
        if (_refreshRoutine != null)
        {
            StopCoroutine(_refreshRoutine);
            _refreshRoutine = null;
        }

        _pendingCallbacks.Clear();
        _cachedRecords.Clear();
        HasLoadedOnce = false;
        _lastRefreshAt = -999f;
        NotifyUpdated(true);
    }

    private IEnumerator RefreshRoutine()
    {
        DivinationRecordFirestore historyStore = GetRecordStore();
        float elapsed = 0f;
        while (historyStore != null && !historyStore.IsReady && elapsed < ReadyTimeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (historyStore == null || !historyStore.IsReady)
        {
            CompleteRefresh(GetSnapshot(), false, false);
            yield break;
        }

        bool recordsDone = false;
        List<DivinationRecordData> loadedRecords = null;
        historyStore.LoadAllRecords(records =>
        {
            loadedRecords = records ?? new List<DivinationRecordData>();
            recordsDone = true;
        });

        while (!recordsDone)
            yield return null;

        bool mergeDone = false;
        List<DivinationRecordData> mergedRecords = null;
        MergeRecentDailyOracleCloudRecords(loadedRecords, historyStore, records =>
        {
            mergedRecords = records ?? new List<DivinationRecordData>();
            mergeDone = true;
        });

        while (!mergeDone)
            yield return null;

        CompleteRefresh(mergedRecords, true, true);
    }

    private void CompleteRefresh(List<DivinationRecordData> records, bool success, bool updateCache)
    {
        if (updateCache)
        {
            _cachedRecords.Clear();
            if (records != null)
                _cachedRecords.AddRange(records);
            SortRecordsDescending(_cachedRecords);
            HasLoadedOnce = true;
            _lastRefreshAt = Time.realtimeSinceStartup;
        }

        _refreshRoutine = null;
        List<DivinationRecordData> snapshot = GetSnapshot();
        RecordsUpdated?.Invoke(snapshot, success);
        FlushPendingCallbacks(snapshot, success);
    }

    private void FlushPendingCallbacks(List<DivinationRecordData> records, bool success)
    {
        if (_pendingCallbacks.Count == 0)
            return;

        Action<List<DivinationRecordData>, bool>[] callbacks = _pendingCallbacks.ToArray();
        _pendingCallbacks.Clear();
        foreach (var callback in callbacks)
            callback?.Invoke(new List<DivinationRecordData>(records ?? new List<DivinationRecordData>()), success);
    }

    private void NotifyUpdated(bool success)
    {
        RecordsUpdated?.Invoke(GetSnapshot(), success);
    }

    private DivinationRecordFirestore GetRecordStore()
    {
        DivinationRecordFirestore store = DivinationRecordFirestore.Instance;
        if (store != null)
            return store;

        GameObject go = new GameObject("DivinationRecordFirestore");
        return go.AddComponent<DivinationRecordFirestore>();
    }

    private DailyOracleFirestore GetDailyOracleStore()
    {
        DailyOracleFirestore store = DailyOracleFirestore.Instance;
        if (store != null)
            return store;

        GameObject go = new GameObject("DailyOracleFirestore");
        return go.AddComponent<DailyOracleFirestore>();
    }

    private void MergeRecentDailyOracleCloudRecords(
        List<DivinationRecordData> records,
        DivinationRecordFirestore historyStore,
        Action<List<DivinationRecordData>> onComplete)
    {
        records ??= new List<DivinationRecordData>();

        DailyOracleFirestore dailyStore = GetDailyOracleStore();
        if (dailyStore == null || !dailyStore.IsReady)
        {
            SortRecordsDescending(records);
            onComplete?.Invoke(records);
            return;
        }

        dailyStore.LoadRecent(DailyOracleMergeDays, dailyRecords =>
        {
            if (dailyRecords != null)
            {
                foreach (DailyOracleCloudRecord dailyRecord in dailyRecords)
                {
                    DivinationRecordData record = DailyOracleHistoryBridge.BuildRecord(dailyRecord);
                    if (record == null || string.IsNullOrEmpty(record.readingId))
                        continue;

                    bool existed = records.Exists(item => item != null && item.readingId == record.readingId);
                    AddOrReplaceRecord(records, record);
                    if (!existed)
                        historyStore?.SaveRecord(record);
                }
            }

            SortRecordsDescending(records);
            onComplete?.Invoke(records);
        });
    }

    private static void AddOrReplaceRecord(List<DivinationRecordData> records, DivinationRecordData record)
    {
        if (records == null || record == null || string.IsNullOrEmpty(record.readingId))
            return;

        records.RemoveAll(item => item == null || item.readingId == record.readingId);
        records.Add(record);
    }

    private static void SortRecordsDescending(List<DivinationRecordData> records)
    {
        if (records == null) return;
        records.Sort((left, right) => ParseRecordTime(right).CompareTo(ParseRecordTime(left)));
    }

    private static DateTime ParseRecordTime(DivinationRecordData record)
    {
        if (record != null && DateTime.TryParse(record.createdAt, out DateTime parsed))
            return parsed;
        return DateTime.MinValue;
    }
}
