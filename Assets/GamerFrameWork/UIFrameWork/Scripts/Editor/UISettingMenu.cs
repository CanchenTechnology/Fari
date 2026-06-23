using UnityEditor;
using UnityEngine;

namespace GamerFrameWork.UIFrameWork
{
    public class UISettingMenu : EditorWindow
    {
        private UnityEditor.Editor settingEditor;
        private Vector2 scrollPosition;

        [MenuItem("GamerFrameWork/UIFrameWork/UI Setting", false, 2)]
        public static void ShowAssetBundleWindow()
        {
            UISettingMenu window = GetWindow<UISettingMenu>("UI Setting");
            window.minSize = new Vector2(760f, 480f);
            window.position = GetCenteredWindowRect(985f, 612f);
            window.Show();
        }

        private static Rect GetCenteredWindowRect(float width, float height)
        {
            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            return new Rect(
                mainWindow.x + (mainWindow.width - width) * 0.5f,
                mainWindow.y + (mainWindow.height - height) * 0.5f,
                width,
                height);
        }

        private void OnEnable()
        {
            RebuildEditor();
        }

        private void OnDisable()
        {
            SaveSetting();
            DestroySettingEditor();
        }

        private void OnGUI()
        {
            UISetting setting = UISetting.Instance;
            if (setting == null)
            {
                EditorGUILayout.HelpBox(
                    "未找到 UISetting.asset，请确认路径：Assets/GamerFrameWork/UIFrameWork/Config/UISetting.asset",
                    MessageType.Warning);
                return;
            }

            if (settingEditor == null || settingEditor.target != setting)
            {
                RebuildEditor();
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("UIFrameWork Setting", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Select Asset", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    Selection.activeObject = setting;
                    EditorGUIUtility.PingObject(setting);
                }

                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                {
                    SaveSetting();
                }
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            settingEditor?.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        private void RebuildEditor()
        {
            DestroySettingEditor();

            UISetting setting = UISetting.Instance;
            if (setting != null)
            {
                settingEditor = UnityEditor.Editor.CreateEditor(setting);
            }
        }

        private void SaveSetting()
        {
            UISetting.Instance?.Save();
        }

        private void DestroySettingEditor()
        {
            if (settingEditor != null)
            {
                DestroyImmediate(settingEditor);
                settingEditor = null;
            }
        }
    }
}
