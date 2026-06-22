using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class FolderTextFontReplacerWindow : EditorWindow
{
    private const string PrefKeyFolder = "Moonly.FolderTextFontReplacer.Folder";
    private const string PrefKeyFont = "Moonly.FolderTextFontReplacer.Font";

    private DefaultAsset targetFolder;
    private Font targetFont;
    private bool includePrefabs = true;
    private bool includeScenes;
    private Vector2 scroll;
    private ReplaceSummary lastSummary;

    [MenuItem("Tools/UI/批量替换文件夹 Text 字体")]
    public static void Open()
    {
        GetWindow<FolderTextFontReplacerWindow>("Text 字体批量替换");
    }

    private void OnEnable()
    {
        string folderPath = EditorPrefs.GetString(PrefKeyFolder, "");
        if (!string.IsNullOrWhiteSpace(folderPath))
            targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);

        string fontPath = EditorPrefs.GetString(PrefKeyFont, "");
        if (string.IsNullOrWhiteSpace(fontPath))
            fontPath = EditorPrefs.GetString("Moonly.FolderTextFontReplacer.LegacyFont", "");
        if (!string.IsNullOrWhiteSpace(fontPath))
            targetFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
    }

    private void OnDisable()
    {
        EditorPrefs.SetString(PrefKeyFolder, GetFolderPath());
        EditorPrefs.SetString(PrefKeyFont, targetFont == null ? "" : AssetDatabase.GetAssetPath(targetFont));
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("拖入要处理的 Project 文件夹", EditorStyles.boldLabel);
        DrawDropArea();

        EditorGUI.BeginChangeCheck();
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("目标文件夹", targetFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
            ValidateFolderObject();

        string folderPath = GetFolderPath();
        if (string.IsNullOrWhiteSpace(folderPath))
            EditorGUILayout.HelpBox("请拖入 Assets 下的文件夹。工具会递归扫描该文件夹。", MessageType.Info);
        else
            EditorGUILayout.HelpBox($"当前文件夹：{folderPath}", MessageType.None);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("要替换成的字体", EditorStyles.boldLabel);
        targetFont = (Font)EditorGUILayout.ObjectField("Text Font", targetFont, typeof(Font), false);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("扫描范围", EditorStyles.boldLabel);
        includePrefabs = EditorGUILayout.ToggleLeft("处理 Prefab 文件", includePrefabs);
        includeScenes = EditorGUILayout.ToggleLeft("处理 Scene 文件（会打开并保存场景）", includeScenes);

        EditorGUILayout.Space(12);
        using (new EditorGUI.DisabledScope(!CanRun()))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("预览数量", GUILayout.Height(32)))
                Run(dryRun: true);
            if (GUILayout.Button("执行替换", GUILayout.Height(32)))
                Run(dryRun: false);
            EditorGUILayout.EndHorizontal();
        }

        if (!CanRun())
            EditorGUILayout.HelpBox("需要选择目标文件夹和 Font。工具只会替换 UnityEngine.UI.Text 组件。", MessageType.Warning);

        DrawSummary();

        EditorGUILayout.EndScrollView();
    }

    private void DrawDropArea()
    {
        Rect rect = GUILayoutUtility.GetRect(0, 58, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "把 Assets 里的文件夹拖到这里", EditorStyles.helpBox);

        Event current = Event.current;
        if (!rect.Contains(current.mousePosition)) return;
        if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform) return;

        string folderPath = DragAndDrop.paths
            .Select(NormalizeFolderPath)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

        DragAndDrop.visualMode = string.IsNullOrWhiteSpace(folderPath)
            ? DragAndDropVisualMode.Rejected
            : DragAndDropVisualMode.Copy;

        if (current.type == EventType.DragPerform && !string.IsNullOrWhiteSpace(folderPath))
        {
            DragAndDrop.AcceptDrag();
            targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
            GUI.changed = true;
        }

        current.Use();
    }

    private void DrawSummary()
    {
        if (lastSummary == null) return;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField(lastSummary.dryRun ? "预览结果" : "执行结果", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"扫描 Prefab：{lastSummary.prefabCount}\n"
            + $"扫描 Scene：{lastSummary.sceneCount}\n"
            + $"会/已替换 Text 组件：{lastSummary.textChanged}\n"
            + $"会/已修改资源：{lastSummary.changedAssetPaths.Count}",
            lastSummary.warningMessages.Count > 0 ? MessageType.Warning : MessageType.Info);

        foreach (string warning in lastSummary.warningMessages)
            EditorGUILayout.HelpBox(warning, MessageType.Warning);

        if (lastSummary.changedAssetPaths.Count == 0) return;

        EditorGUILayout.LabelField("受影响资源", EditorStyles.boldLabel);
        foreach (string path in lastSummary.changedAssetPaths.Take(80))
            EditorGUILayout.LabelField(path);
        if (lastSummary.changedAssetPaths.Count > 80)
            EditorGUILayout.LabelField($"... 还有 {lastSummary.changedAssetPaths.Count - 80} 个");
    }

    private bool CanRun()
    {
        if (string.IsNullOrWhiteSpace(GetFolderPath())) return false;
        if (!includePrefabs && !includeScenes) return false;
        return targetFont != null;
    }

    private void Run(bool dryRun)
    {
        string folderPath = GetFolderPath();
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        if (!dryRun && includeScenes && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        lastSummary = new ReplaceSummary { dryRun = dryRun };
        string[] searchFolders = { folderPath };

        try
        {
            if (includePrefabs)
                ProcessPrefabs(searchFolders, dryRun, lastSummary);
            if (includeScenes)
                ProcessScenes(searchFolders, dryRun, lastSummary);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (!dryRun)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log(
            $"[TextFontReplacer] {(dryRun ? "Preview" : "Applied")} "
            + $"Text={lastSummary.textChanged}, Assets={lastSummary.changedAssetPaths.Count}");
    }

    private void ProcessPrefabs(string[] searchFolders, bool dryRun, ReplaceSummary summary)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", searchFolders);
        summary.prefabCount = prefabGuids.Length;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            EditorUtility.DisplayProgressBar("替换 Text 字体", path, prefabGuids.Length == 0 ? 1f : i / (float)prefabGuids.Length);

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                bool changed = ReplaceInGameObjects(new[] { root }, dryRun, summary);

                if (changed)
                {
                    summary.changedAssetPaths.Add(path);
                    if (!dryRun)
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            catch (Exception ex)
            {
                summary.warningMessages.Add($"{path}: {ex.Message}");
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private void ProcessScenes(string[] searchFolders, bool dryRun, ReplaceSummary summary)
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:SceneAsset", searchFolders);
        summary.sceneCount = sceneGuids.Length;

        for (int i = 0; i < sceneGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
            EditorUtility.DisplayProgressBar("替换 Text 字体", path, sceneGuids.Length == 0 ? 1f : i / (float)sceneGuids.Length);

            Scene scene = default;
            bool wasLoaded = TryGetLoadedScene(path, out scene);
            try
            {
                if (!wasLoaded)
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                bool changed = ReplaceInGameObjects(scene.GetRootGameObjects(), dryRun, summary);
                if (changed)
                {
                    summary.changedAssetPaths.Add(path);
                    if (!dryRun)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
            }
            catch (Exception ex)
            {
                summary.warningMessages.Add($"{path}: {ex.Message}");
            }
            finally
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private bool ReplaceInGameObjects(IEnumerable<GameObject> roots, bool dryRun, ReplaceSummary summary)
    {
        bool changed = false;

        foreach (GameObject root in roots)
        {
            if (root == null) continue;

            if (targetFont != null)
            {
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                {
                    if (text.font == targetFont) continue;
                    summary.textChanged++;
                    changed = true;
                    if (!dryRun)
                    {
                        Undo.RecordObject(text, "Replace Text Font");
                        text.font = targetFont;
                        EditorUtility.SetDirty(text);
                    }
                }
            }
        }

        return changed;
    }

    private void ValidateFolderObject()
    {
        string path = GetFolderPath();
        if (!string.IsNullOrWhiteSpace(path)) return;
        targetFolder = null;
    }

    private string GetFolderPath()
    {
        if (targetFolder == null) return "";
        return NormalizeFolderPath(AssetDatabase.GetAssetPath(targetFolder));
    }

    private static string NormalizeFolderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        path = path.Replace("\\", "/");

        string dataPath = Application.dataPath.Replace("\\", "/");
        if (path.Equals(dataPath, StringComparison.OrdinalIgnoreCase))
            path = "Assets";
        else if (path.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
            path = "Assets" + path.Substring(dataPath.Length);

        return path.StartsWith("Assets", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(path)
            ? path
            : "";
    }

    private static bool TryGetLoadedScene(string path, out Scene scene)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            scene = SceneManager.GetSceneAt(i);
            if (scene.path == path)
                return true;
        }

        scene = default;
        return false;
    }

    private sealed class ReplaceSummary
    {
        public bool dryRun;
        public int prefabCount;
        public int sceneCount;
        public int textChanged;
        public readonly List<string> changedAssetPaths = new List<string>();
        public readonly List<string> warningMessages = new List<string>();
    }
}
