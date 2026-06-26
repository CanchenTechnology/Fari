using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

[InitializeOnLoad]
public static class BluuNextTmpFontAssetSetup
{
    private const string SourceFontPath = "Assets/GameData/Arts/Fonts/Bluu-Next-Cyrillic-2.otf";
    private const string OutputFolderPath = "Assets/GameData/Arts/Fonts/TMP";
    private const string OutputAssetPath = OutputFolderPath + "/Bluu Next Cyrillic 2 SDF.asset";

    static BluuNextTmpFontAssetSetup()
    {
        EditorApplication.delayCall += CreateMissingFontAssetOnReload;
    }

    [MenuItem("Tools/UI/TextMeshPro/生成 Bluu Next Cyrillic TMP 字体")]
    public static void CreateOrUpdateFontAsset()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[BluuNextTmpFontAssetSetup] Source font not found: {SourceFontPath}");
            return;
        }

        EnsureIncludeFontData(SourceFontPath);
        EnsureOutputFolder();

        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputAssetPath);
        if (fontAsset == null)
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 2048,
                atlasHeight: 2048,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError("[BluuNextTmpFontAssetSetup] TMP failed to create font asset.");
                return;
            }

            fontAsset.name = "Bluu Next Cyrillic 2 SDF";
            RenameSubAssets(fontAsset);
            AssetDatabase.CreateAsset(fontAsset, OutputAssetPath);
            AddSubAssets(fontAsset);
        }
        else
        {
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            RenameSubAssets(fontAsset);
            EditorUtility.SetDirty(fontAsset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BluuNextTmpFontAssetSetup] TMP font asset is ready: {OutputAssetPath}", fontAsset);
    }

    private static void CreateMissingFontAssetOnReload()
    {
        EditorApplication.delayCall -= CreateMissingFontAssetOnReload;
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputAssetPath) != null)
            return;

        CreateOrUpdateFontAsset();
    }

    private static void RenameSubAssets(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null) return;
        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            fontAsset.atlasTextures[0].name = "Bluu Next Cyrillic 2 Atlas";
        if (fontAsset.material != null)
            fontAsset.material.name = "Bluu Next Cyrillic 2 Atlas Material";
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
