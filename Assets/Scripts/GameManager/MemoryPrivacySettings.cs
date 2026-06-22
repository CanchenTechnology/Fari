using GamerFrameWork.OracleRuntime;
using UnityEngine;

public static class MemoryPrivacySettings
{
    private const string ShareAllMemoryKey = "MemoryManagement_ShareAll";
    private const string AutoTopicKey = "MemoryManagement_AutoTopic";
    private const string AutoPreferenceKey = "MemoryManagement_AutoPreference";
    private const string AutoEmotionKey = "MemoryManagement_AutoEmotion";
    private const string AutoGrowthKey = "MemoryManagement_AutoGrowth";
    private const string RequireConfirmKey = "MemoryManagement_RequireConfirm";

    public static bool ShareAllMemoryEnabled
    {
        get => PlayerPrefs.GetInt(ShareAllMemoryKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(ShareAllMemoryKey, value ? 1 : 0);
            PlayerPrefs.Save();
            SaveToCloud();
        }
    }

    public static bool AutoTopicEnabled
    {
        get => GetBool(AutoTopicKey, true);
        set => SetBool(AutoTopicKey, value);
    }

    public static bool AutoPreferenceEnabled
    {
        get => GetBool(AutoPreferenceKey, true);
        set => SetBool(AutoPreferenceKey, value);
    }

    public static bool AutoEmotionEnabled
    {
        get => GetBool(AutoEmotionKey, true);
        set => SetBool(AutoEmotionKey, value);
    }

    public static bool AutoGrowthEnabled
    {
        get => GetBool(AutoGrowthKey, false);
        set => SetBool(AutoGrowthKey, value);
    }

    public static bool RequireConfirmBeforeAdd
    {
        get => GetBool(RequireConfirmKey, true);
        set => SetBool(RequireConfirmKey, value);
    }

    public static MemorySource GetPromptMemorySource(MemorySource source)
    {
        if (!ShareAllMemoryEnabled)
            return new MemorySource();

        source ??= new MemorySource();
        var filtered = new MemorySource
        {
            stableProfile = new StableProfile
            {
                preferredName = source.stableProfile?.preferredName,
                preferredTone = AutoPreferenceEnabled ? source.stableProfile?.preferredTone : "",
                recurringThemes = AutoTopicEnabled ? Copy(source.stableProfile?.recurringThemes) : new System.Collections.Generic.List<string>(),
                doNotSay = AutoPreferenceEnabled ? Copy(source.stableProfile?.doNotSay) : new System.Collections.Generic.List<string>(),
                safetyNotes = AutoEmotionEnabled ? Copy(source.stableProfile?.safetyNotes) : new System.Collections.Generic.List<string>()
            },
            relationships = AutoEmotionEnabled ? source.relationships ?? new System.Collections.Generic.List<RelationshipMemory>() : new System.Collections.Generic.List<RelationshipMemory>(),
            readingContinuity = AutoGrowthEnabled ? source.readingContinuity ?? new System.Collections.Generic.List<ReadingContinuityEntry>() : new System.Collections.Generic.List<ReadingContinuityEntry>(),
            candidates = FilterCandidates(source.candidates),
            tomorrowHooks = AutoGrowthEnabled ? source.tomorrowHooks ?? new System.Collections.Generic.List<TomorrowHook>() : new System.Collections.Generic.List<TomorrowHook>()
        };

        return filtered;
    }

    private static System.Collections.Generic.List<MemoryCandidate> FilterCandidates(System.Collections.Generic.List<MemoryCandidate> candidates)
    {
        var result = new System.Collections.Generic.List<MemoryCandidate>();
        if (candidates == null) return result;

        foreach (var candidate in candidates)
        {
            if (candidate == null || !MemoryUiStore.IsCandidateEnabled(candidate)) continue;
            MemoryUiCategory category = MemoryUiStore.FromType(candidate.type);
            if (category == MemoryUiCategory.Topic && !AutoTopicEnabled) continue;
            if (category == MemoryUiCategory.Preference && !AutoPreferenceEnabled) continue;
            if (category == MemoryUiCategory.Emotion && !AutoEmotionEnabled) continue;
            if (category == MemoryUiCategory.Growth && !AutoGrowthEnabled) continue;
            result.Add(candidate);
        }

        return result;
    }

    private static System.Collections.Generic.List<string> Copy(System.Collections.Generic.List<string> source)
    {
        return source == null
            ? new System.Collections.Generic.List<string>()
            : new System.Collections.Generic.List<string>(source);
    }

    private static bool GetBool(string key, bool defaultValue)
    {
        return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
    }

    private static void SetBool(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
        SaveToCloud();
    }

    public static MemoryPrivacySettingsSnapshot CreateSnapshot()
    {
        return new MemoryPrivacySettingsSnapshot
        {
            shareAllMemoryEnabled = ShareAllMemoryEnabled,
            autoTopicEnabled = AutoTopicEnabled,
            autoPreferenceEnabled = AutoPreferenceEnabled,
            autoEmotionEnabled = AutoEmotionEnabled,
            autoGrowthEnabled = AutoGrowthEnabled,
            requireConfirmBeforeAdd = RequireConfirmBeforeAdd
        };
    }

    public static void ApplySnapshot(MemoryPrivacySettingsSnapshot snapshot)
    {
        if (snapshot == null) return;
        SetBoolLocal(ShareAllMemoryKey, snapshot.shareAllMemoryEnabled);
        SetBoolLocal(AutoTopicKey, snapshot.autoTopicEnabled);
        SetBoolLocal(AutoPreferenceKey, snapshot.autoPreferenceEnabled);
        SetBoolLocal(AutoEmotionKey, snapshot.autoEmotionEnabled);
        SetBoolLocal(AutoGrowthKey, snapshot.autoGrowthEnabled);
        SetBoolLocal(RequireConfirmKey, snapshot.requireConfirmBeforeAdd);
        PlayerPrefs.Save();
    }

    public static void LoadFromCloud(System.Action<bool> onComplete = null)
    {
        var firestore = FirestoreManager.Instance;
        if (firestore == null || !firestore.IsInitialized)
        {
            onComplete?.Invoke(false);
            return;
        }

        firestore.LoadMemoryPrivacySettings(onComplete);
    }

    public static void SaveToCloud(System.Action<bool> onComplete = null)
    {
        var firestore = FirestoreManager.Instance;
        if (firestore == null || !firestore.IsInitialized)
        {
            onComplete?.Invoke(false);
            return;
        }

        firestore.SaveMemoryPrivacySettings(onComplete);
    }

    private static void SetBoolLocal(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
    }
}

public class MemoryPrivacySettingsSnapshot
{
    public bool shareAllMemoryEnabled = true;
    public bool autoTopicEnabled = true;
    public bool autoPreferenceEnabled = true;
    public bool autoEmotionEnabled = true;
    public bool autoGrowthEnabled;
    public bool requireConfirmBeforeAdd = true;
}
