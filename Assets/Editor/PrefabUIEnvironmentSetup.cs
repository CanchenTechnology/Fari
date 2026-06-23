using UnityEditor;
using UnityEngine;

public static class PrefabUIEnvironmentSetup
{
    [MenuItem("Tools/UI/恢复 UI Prefab 默认预览环境")]
    public static void RestoreDefaultUIEnvironment()
    {
        EditorSettings.prefabUIEnvironment = null;
        AssetDatabase.SaveAssets();
        Debug.Log("UI Prefab 预览环境已恢复默认。重新打开 Prefab Mode 后会恢复 Unity 默认预览。");
    }
}
