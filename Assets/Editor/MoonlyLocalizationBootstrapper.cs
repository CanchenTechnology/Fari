using System;
using System.Collections.Generic;
using System.IO;
using I2.Loc;
using TMPro;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UI;

public static class MoonlyLocalizationBootstrapper
{
    private const string RunMarkerPath = "Library/MoonlyLocalizationBootstrapper.run";
    private const string LanguageSourcePath = "Assets/Resources/I2Languages.asset";
    private const string Chinese = "Chinese (Simplified)";
    private const string English = "English";
    private const string Spanish = "Spanish";

    private static readonly string[] PrefabPaths =
    {
        "Assets/GameData/UI/Main/My/MyUI.prefab",
        "Assets/GameData/UI/Main/NavigationUI.prefab",
        "Assets/GameData/UI/Main/TodayDivination/TodayOracleUI.prefab"
    };

    private static readonly LocalizationEntry[] Entries =
    {
        new("UI/Common/TodayOracle", "今日神谕", "Today Oracle", "Oráculo de hoy"),
        new("UI/Common/Dialogue", "对话", "Dialogue", "Diálogo"),
        new("UI/Common/PlayTogether", "一起玩", "Play Together", "Jugar juntos"),
        new("UI/Common/My", "我的", "My", "Mi perfil"),

        new("UI/My/AccountInfo", "账户信息", "Account Info", "Información de cuenta"),
        new("UI/My/MostRecent", "最近一次", "Most Recent", "Más reciente"),
        new("UI/My/DashboardDescription", "统一查看个人、朋友与进行中的占卜记录", "View personal, friend, and active reading records in one place", "Consulta en un solo lugar tus registros personales, de amigos y lecturas en curso"),
        new("UI/My/UserSubtitle", "探索内在，连接宇宙的指引。", "Explore within and connect with cosmic guidance.", "Explora tu interior y conecta con la guía del universo."),
        new("UI/My/TodayReading", "今日占卜", "Today's Reading", "Lectura de hoy"),
        new("UI/My/Notifications", "通知设置", "Notifications", "Notificaciones"),
        new("UI/My/Logout", "退出登陆", "Log Out", "Cerrar sesión"),
        new("UI/My/FollowUs", "关注我们", "Follow Us", "Síguenos"),
        new("UI/My/CustomSettings", "自定义设置", "Custom Settings", "Configuración personalizada"),
        new("UI/My/TodayDialogue", "今日对话", "Today's Dialogue", "Diálogo de hoy"),
        new("UI/My/Feedback", "反馈意见", "Feedback", "Comentarios"),
        new("UI/My/UnlockAllFeatures", "解锁所有功能", "Unlock All Features", "Desbloquear todas las funciones"),
        new("UI/My/ReadingHistory", "占卜历史", "Reading History", "Historial de lecturas"),

        new("UI/TodayOracle/ViewFullReading", "查看完整解读", "View Full Reading", "Ver lectura completa"),
        new("UI/TodayOracle/SwitchDiviner", "切换神谕师", "Switch Oracle Guide", "Cambiar guía del oráculo"),
        new("UI/TodayOracle/DeepChat", "深入聊聊占卜结果", "Talk More About the Reading", "Hablar más sobre la lectura"),
        new("UI/TodayOracle/Subtitle", "让塔罗的指引，照亮你的此刻", "Let tarot's guidance illuminate this moment", "Que la guía del tarot ilumine este momento"),
        new("UI/TodayOracle/NocturneOracle", "Nocturne Oracle", "Nocturne Oracle", "Nocturne Oracle"),
        new("UI/TodayOracle/FlipTodayCard", "翻开今日卡牌", "Reveal Today's Card", "Revelar la carta de hoy")
    };

    [DidReloadScripts]
    private static void RunOnceWhenRequested()
    {
        if (!File.Exists(RunMarkerPath))
            return;

        File.Delete(RunMarkerPath);
        BootstrapLocalizedPrefabs();
    }

    [MenuItem("Tools/Moonly/Localization/Bootstrap Localized Prefabs")]
    public static void BootstrapLocalizedPrefabs()
    {
        LanguageSourceAsset asset = AssetDatabase.LoadAssetAtPath<LanguageSourceAsset>(LanguageSourcePath);
        if (asset == null)
        {
            Debug.LogError($"[MoonlyLocalization] Missing language source asset at {LanguageSourcePath}");
            return;
        }

        LanguageSourceData source = asset.SourceData;
        source.owner = asset;
        ConfigureLanguageSource(source);

        Dictionary<string, string> termByText = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (LocalizationEntry entry in Entries)
        {
            EnsureTerm(source, entry);
            termByText[entry.Chinese] = entry.Term;
        }

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        int localizedTextCount = 0;
        foreach (string prefabPath in PrefabPaths)
            localizedTextCount += LocalizePrefab(prefabPath, termByText);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MoonlyLocalization] Configured {Entries.Length} terms and {localizedTextCount} Localize components across {PrefabPaths.Length} prefabs.");
    }

    private static void ConfigureLanguageSource(LanguageSourceData source)
    {
        EnsureLanguage(source, Chinese, "zh-CN");
        EnsureLanguage(source, English, "en");
        EnsureLanguage(source, Spanish, "es");

        source.CaseInsensitiveTerms = false;
        source.OnMissingTranslation = LanguageSourceData.MissingTranslationAction.Fallback;
        source.IgnoreDeviceLanguage = false;
        source._AllowUnloadingLanguages = LanguageSourceData.eAllowUnloadLanguages.Never;

        source.GoogleLiveSyncIsUptoDate = true;
        source.GoogleUpdateFrequency = LanguageSourceData.eGoogleUpdateFrequency.Weekly;
        source.GoogleInEditorCheckFrequency = LanguageSourceData.eGoogleUpdateFrequency.Daily;
        source.GoogleUpdateSynchronization = LanguageSourceData.eGoogleUpdateSynchronization.OnSceneLoaded;
        source.GoogleUpdateDelay = 0f;
    }

    private static void EnsureLanguage(LanguageSourceData source, string name, string code)
    {
        int index = source.GetLanguageIndex(name, false, false);
        if (index < 0)
        {
            source.AddLanguage(name, code);
            index = source.GetLanguageIndex(name, false, false);
        }

        if (index >= 0)
        {
            source.mLanguages[index].Name = name;
            source.mLanguages[index].Code = code;
            source.mLanguages[index].Flags = 0;
        }
    }

    private static void EnsureTerm(LanguageSourceData source, LocalizationEntry entry)
    {
        TermData term = source.AddTerm(entry.Term, eTermType.Text, false);
        int languageCount = source.mLanguages.Count;
        Array.Resize(ref term.Languages, languageCount);
        Array.Resize(ref term.Flags, languageCount);

        SetTranslation(source, term, Chinese, entry.Chinese);
        SetTranslation(source, term, English, entry.English);
        SetTranslation(source, term, Spanish, entry.Spanish);
        term.Description = entry.Chinese;
    }

    private static void SetTranslation(LanguageSourceData source, TermData term, string language, string value)
    {
        int index = source.GetLanguageIndex(language, false, false);
        if (index < 0)
            return;

        term.Languages[index] = value;
        term.Flags[index] = 0;
    }

    private static int LocalizePrefab(string prefabPath, IReadOnlyDictionary<string, string> termByText)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        int count = 0;
        try
        {
            TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in tmpTexts)
                count += TryLocalizeText(text.gameObject, text.text, termByText);

            Text[] uiTexts = root.GetComponentsInChildren<Text>(true);
            foreach (Text text in uiTexts)
                count += TryLocalizeText(text.gameObject, text.text, termByText);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return count;
    }

    private static int TryLocalizeText(GameObject target, string text, IReadOnlyDictionary<string, string> termByText)
    {
        if (string.IsNullOrWhiteSpace(text) || !termByText.TryGetValue(text, out string term))
            return 0;

        Localize localize = target.GetComponent<Localize>();
        if (localize == null)
            localize = target.AddComponent<Localize>();

        localize.mTerm = term;
        localize.mTermSecondary = "-";
        localize.LocalizeOnAwake = true;
        localize.AllowLocalizedParameters = true;
        localize.AllowParameters = true;
        localize.CorrectAlignmentForRTL = true;
        localize.mLocalizeTargetName = null;
        localize.FindTarget();
        EditorUtility.SetDirty(target);
        return 1;
    }

    private readonly struct LocalizationEntry
    {
        public readonly string Term;
        public readonly string Chinese;
        public readonly string English;
        public readonly string Spanish;

        public LocalizationEntry(string term, string chinese, string english, string spanish)
        {
            Term = term;
            Chinese = chinese;
            English = english;
            Spanish = spanish;
        }
    }
}
