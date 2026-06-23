using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class TMPDefaultFontAssetSetup
{
    private const string SourceFontPath = "Assets/GameData/Arts/Fonts/Regular.ttf";
    private const string OutputFolderPath = "Assets/GameData/Arts/Fonts/TMP";
    private const string OutputAssetPath = OutputFolderPath + "/Regular SDF.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string UguiBuilderTmpFontPathKey = "UGUIBuilder_TMPFontPath";

    [MenuItem("Tools/UI/TextMeshPro/生成默认 TMP 字体")]
    public static void CreateOrUpdateDefaultFontAsset()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            EditorUtility.DisplayDialog("生成 TMP 字体失败", $"找不到字体文件：\n{SourceFontPath}", "OK");
            return;
        }

        EnsureIncludeFontData(SourceFontPath);
        EnsureOutputFolder();

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputAssetPath);
        if (fontAsset == null)
        {
            fontAsset = CreateFontAsset(sourceFont);
            if (fontAsset == null)
            {
                EditorUtility.DisplayDialog("生成 TMP 字体失败", "TMP 无法读取字体。请确认字体导入设置里 Include Font Data 已开启。", "OK");
                return;
            }

            AssetDatabase.CreateAsset(fontAsset, OutputAssetPath);
            AddSubAssets(fontAsset);
        }
        else
        {
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            EditorUtility.SetDirty(fontAsset);
        }

        SetDefaultTmpFont(fontAsset);
        EditorPrefs.SetString(UguiBuilderTmpFontPathKey, OutputAssetPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = fontAsset;
        EditorGUIUtility.PingObject(fontAsset);

        Debug.Log($"[TMPDefaultFontAssetSetup] TMP default font asset is ready: {OutputAssetPath}", fontAsset);
        EditorUtility.DisplayDialog("TMP 字体已生成", $"已生成并设置默认 TMP 字体：\n{OutputAssetPath}", "OK");
    }

    [MenuItem("Tools/UI/TextMeshPro/生成默认 TMP 字体", true)]
    private static bool CreateOrUpdateDefaultFontAssetValidate()
    {
        return AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath) != null;
    }

    private static TMP_FontAsset CreateFontAsset(Font sourceFont)
    {
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            samplingPointSize: 90,
            atlasPadding: 9,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 2048,
            atlasHeight: 2048,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fontAsset == null)
            return null;

        fontAsset.name = "Regular SDF";
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;

        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            fontAsset.atlasTextures[0].name = "Regular Atlas";

        if (fontAsset.material != null)
            fontAsset.material.name = "Regular Atlas Material";

        return fontAsset;
    }

    private static void AddSubAssets(TMP_FontAsset fontAsset)
    {
        if (fontAsset.atlasTextures != null)
        {
            foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
            {
                if (atlasTexture != null && !AssetDatabase.Contains(atlasTexture))
                    AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            }
        }

        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        EditorUtility.SetDirty(fontAsset);
    }

    private static void SetDefaultTmpFont(TMP_FontAsset fontAsset)
    {
        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
        if (settings == null)
        {
            Debug.LogWarning($"[TMPDefaultFontAssetSetup] TMP Settings asset not found: {TmpSettingsPath}");
            return;
        }

        SerializedObject serializedSettings = new SerializedObject(settings);
        serializedSettings.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    private static void EnsureIncludeFontData(string sourceFontPath)
    {
        AssetImporter importer = AssetImporter.GetAtPath(sourceFontPath);
        if (importer == null) return;

        SerializedObject serializedImporter = new SerializedObject(importer);
        SerializedProperty includeFontData = serializedImporter.FindProperty("includeFontData");
        if (includeFontData == null)
            includeFontData = serializedImporter.FindProperty("m_IncludeFontData");

        if (includeFontData == null || includeFontData.boolValue)
            return;

        includeFontData.boolValue = true;
        serializedImporter.ApplyModifiedPropertiesWithoutUndo();
        importer.SaveAndReimport();
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolderPath))
            AssetDatabase.CreateFolder("Assets/GameData/Arts/Fonts", "TMP");
    }
}
