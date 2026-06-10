using UnityEngine;
using UnityEditor;
using System.IO;

namespace UGUIBuilder
{
    /// <summary>
    /// UGUI Builder 编辑器窗口 —— 拖入 JSON、选择 Sprite 文件夹和输出路径，一键生成 UGUI Prefab。
    /// 菜单位置: Tools → UGUI Builder → Open Window
    /// </summary>
    public class UGUIBuilderWindow : EditorWindow
    {
        private TextAsset jsonFile;
        private string spriteFolder = "Assets/Art/UI/Sprites";
        private string prefabOutputPath = "Assets/UI/Generated/NewScreen.prefab";
        private Font selectedFont;

        private Vector2 scrollPos;
        private string statusMsg = "";
        private MessageType statusType = MessageType.None;

        private bool foldoutJSON = true;
        private bool foldoutPaths = true;
        private bool foldoutBuild = true;

        [MenuItem("Tools/UGUI Builder/Open Window", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<UGUIBuilderWindow>("UGUI Builder");
            window.minSize = new Vector2(420, 460);
            window.Show();
        }

        private void OnEnable()
        {
            // 从 EditorPrefs 恢复上次的路径
            spriteFolder = EditorPrefs.GetString("UGUIBuilder_SpriteFolder", "Assets/Art/UI/Sprites");
            prefabOutputPath = EditorPrefs.GetString("UGUIBuilder_PrefabPath", "Assets/UI/Generated/NewScreen.prefab");

            // 恢复上次选择的字体
            string savedFontPath = EditorPrefs.GetString("UGUIBuilder_FontPath", "");
            if (!string.IsNullOrEmpty(savedFontPath))
            {
                selectedFont = AssetDatabase.LoadAssetAtPath<Font>(savedFontPath);
            }
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawHeader();
            DrawJSONSection();
            DrawPathsSection();
            DrawBuildSection();
            DrawStatusArea();
            DrawQuickActions();

            EditorGUILayout.EndScrollView();
        }

        // ============================================================
        // 绘制区域
        // ============================================================

        private void DrawHeader()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("UGUI Prefab Builder", new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            });
            GUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "JSON → Canvas → UIMask + UIContent → Prefab\n" +
                "与 NavigationUI.prefab 结构一致：Canvas 下包含 UIMask（遮罩）和 UIContent（内容容器）",
                MessageType.Info);
            GUILayout.Space(10);
        }

        private void DrawJSONSection()
        {
            foldoutJSON = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutJSON, "1. 布局 JSON");
            if (foldoutJSON)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                jsonFile = (TextAsset)EditorGUILayout.ObjectField(
                    new GUIContent("JSON 文件", "拖入布局 JSON 文件（.json / .txt）"),
                    jsonFile, typeof(TextAsset), false);

                // 清除按钮
                if (jsonFile != null && GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18)))
                {
                    jsonFile = null;
                }
                EditorGUILayout.EndHorizontal();

                if (jsonFile != null)
                {
                    string jsonPath = AssetDatabase.GetAssetPath(jsonFile);
                    EditorGUILayout.LabelField("  路径: " + jsonPath, EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(5);
        }

        private void DrawPathsSection()
        {
            foldoutPaths = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutPaths, "2. 资源与输出路径");
            if (foldoutPaths)
            {
                EditorGUI.indentLevel++;

                // Sprite 文件夹
                EditorGUILayout.BeginHorizontal();
                spriteFolder = EditorGUILayout.TextField(
                    new GUIContent("Sprite 文件夹", "存放切图的文件夹（Assets 相对路径）"),
                    spriteFolder);
                if (GUILayout.Button("浏览...", GUILayout.Width(60)))
                {
                    string picked = PickFolder("选择 Sprite 文件夹", spriteFolder);
                    if (!string.IsNullOrEmpty(picked)) spriteFolder = picked;
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);

                // 字体选择
                EditorGUILayout.BeginHorizontal();
                selectedFont = (Font)EditorGUILayout.ObjectField(
                    new GUIContent("默认字体", "用于所有 Text 元素的字体。留空则使用 JSON 中指定的字体路径"),
                    selectedFont, typeof(Font), false);
                if (selectedFont != null && GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18)))
                {
                    selectedFont = null;
                }
                EditorGUILayout.EndHorizontal();

                if (selectedFont != null)
                {
                    string fontPath = AssetDatabase.GetAssetPath(selectedFont);
                    EditorGUILayout.LabelField("  路径: " + fontPath, EditorStyles.miniLabel);
                    EditorGUILayout.HelpBox("窗口选择的字体会覆盖 JSON 中的 defaultFont 设置。", MessageType.None);
                }

                GUILayout.Space(4);

                // 输出路径
                EditorGUILayout.BeginHorizontal();
                prefabOutputPath = EditorGUILayout.TextField(
                    new GUIContent("Prefab 输出", "生成 Prefab 的保存路径（Assets 相对路径）"),
                    prefabOutputPath);
                if (GUILayout.Button("另存为...", GUILayout.Width(60)))
                {
                    string picked = SaveFilePanel("保存 Prefab", prefabOutputPath);
                    if (!string.IsNullOrEmpty(picked)) prefabOutputPath = picked;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(5);
        }

        private void DrawBuildSection()
        {
            foldoutBuild = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutBuild, "3. 生成");
            if (foldoutBuild)
            {
                EditorGUI.indentLevel++;

                bool canBuild = jsonFile != null
                    && !string.IsNullOrEmpty(spriteFolder)
                    && !string.IsNullOrEmpty(prefabOutputPath);

                GUI.enabled = canBuild;
                GUI.backgroundColor = canBuild
                    ? new Color(0.35f, 0.75f, 0.35f)
                    : Color.gray;

                if (GUILayout.Button("▶  生成 UGUI Prefab", GUILayout.Height(40)))
                {
                    Build();
                }

                GUI.backgroundColor = Color.white;
                GUI.enabled = true;

                if (!canBuild)
                {
                    string hint = "";
                    if (jsonFile == null) hint = "请先拖入 JSON 布局文件";
                    else if (string.IsNullOrEmpty(spriteFolder)) hint = "请设置 Sprite 文件夹";
                    else if (string.IsNullOrEmpty(prefabOutputPath)) hint = "请设置 Prefab 输出路径";
                    EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(5);
        }

        private void DrawStatusArea()
        {
            if (!string.IsNullOrEmpty(statusMsg))
            {
                EditorGUILayout.HelpBox(statusMsg, statusType);
            }
        }

        private void DrawQuickActions()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("快捷操作", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("生成示例 JSON", GUILayout.Height(24)))
            {
                GenerateSampleJSON();
            }
            if (GUILayout.Button("定位输出目录", GUILayout.Height(24)))
            {
                string dir = Path.GetDirectoryName(prefabOutputPath);
                var obj = AssetDatabase.LoadAssetAtPath<Object>(dir);
                if (obj != null) EditorGUIUtility.PingObject(obj);
            }
            if (GUILayout.Button("清空状态", GUILayout.Height(24)))
            {
                statusMsg = "";
            }
            EditorGUILayout.EndHorizontal();
        }

        // ============================================================
        // 核心逻辑
        // ============================================================

        private void Build()
        {
            if (jsonFile == null)
            {
                SetStatus("❌ 请先拖入 JSON 布局文件", MessageType.Error);
                return;
            }

            string jsonPath = AssetDatabase.GetAssetPath(jsonFile);
            if (string.IsNullOrEmpty(jsonPath))
            {
                SetStatus("❌ 无法获取 JSON 文件路径，请重新拖入", MessageType.Error);
                return;
            }

            // 保存路径到 EditorPrefs
            EditorPrefs.SetString("UGUIBuilder_SpriteFolder", spriteFolder);
            EditorPrefs.SetString("UGUIBuilder_PrefabPath", prefabOutputPath);

            // 保存字体路径
            if (selectedFont != null)
            {
                string fontPath = AssetDatabase.GetAssetPath(selectedFont);
                EditorPrefs.SetString("UGUIBuilder_FontPath", fontPath);
            }
            else
            {
                EditorPrefs.SetString("UGUIBuilder_FontPath", "");
            }

            try
            {
                UGUIBuilderTool.BuildUGUIFromJSON(jsonPath, spriteFolder, prefabOutputPath, selectedFont);

                SetStatus($"✅ Prefab 生成成功: {prefabOutputPath}", MessageType.Info);

                // 定位生成的 Prefab
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabOutputPath);
                if (prefab != null)
                {
                    EditorGUIUtility.PingObject(prefab);
                    Selection.activeObject = prefab;
                }

                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                SetStatus($"❌ 生成失败: {e.Message}", MessageType.Error);
                Debug.LogException(e);
            }
        }

        private void SetStatus(string msg, MessageType type)
        {
            statusMsg = msg;
            statusType = type;
            Repaint();
        }

        // ============================================================
        // 文件选择工具
        // ============================================================

        private string PickFolder(string title, string fallback)
        {
            string startPath = "Assets";
            if (!string.IsNullOrEmpty(fallback))
            {
                string full = Path.Combine(Application.dataPath, fallback.Replace("Assets/", ""));
                if (Directory.Exists(full)) startPath = full;
            }

            string picked = EditorUtility.OpenFolderPanel(title, startPath, "");
            if (string.IsNullOrEmpty(picked)) return null;

            return ToAssetsPath(picked);
        }

        private string SaveFilePanel(string title, string fallback)
        {
            string dir = "Assets";
            string file = "NewScreen";
            if (!string.IsNullOrEmpty(fallback))
            {
                dir = Path.GetDirectoryName(fallback) ?? "Assets";
                file = Path.GetFileNameWithoutExtension(fallback) ?? "NewScreen";
            }

            string fullDir = Path.Combine(Application.dataPath, dir.Replace("Assets/", ""));
            string picked = EditorUtility.SaveFilePanel(title, fullDir, file, "prefab");
            if (string.IsNullOrEmpty(picked)) return null;

            return ToAssetsPath(picked);
        }

        private string ToAssetsPath(string absolutePath)
        {
            if (absolutePath.StartsWith(Application.dataPath))
                return "Assets" + absolutePath.Substring(Application.dataPath.Length);
            return absolutePath;
        }

        // ============================================================
        // 示例 JSON
        // ============================================================

        private void GenerateSampleJSON()
        {
            string picked = EditorUtility.SaveFilePanel("保存示例 JSON", Application.dataPath, "SampleLayout", "json");
            if (string.IsNullOrEmpty(picked)) return;

            string json = @"{
  ""screenName"": ""NavigationUI"",
  ""resolution"": { ""x"": 750, ""y"": 1334 },
  ""matchWidthOrHeight"": 0.5,
  ""includeMask"": true,
  ""maskColor"": { ""r"": 0, ""g"": 0, ""b"": 0, ""a"": 0.67 },
  ""defaultFont"": """",
  ""elements"": [
    {
      ""name"": ""Bottom"",
      ""type"": ""Panel"",
      ""color"": { ""r"": 0, ""g"": 0, ""b"": 0, ""a"": 1 },
      ""raycastTarget"": false,
      ""anchorMin"": { ""x"": 0, ""y"": 0 },
      ""anchorMax"": { ""x"": 1, ""y"": 0 },
      ""position"": { ""x"": 0, ""y"": 104.21 },
      ""size"": { ""x"": 0, ""y"": 208.42 },
      ""children"": [
        {
          ""name"": ""ToggleGroup"",
          ""type"": ""ToggleGroup"",
          ""spacing"": 0,
          ""paddingLeft"": 69,
          ""paddingRight"": 0,
          ""paddingTop"": 30,
          ""paddingBottom"": 0,
          ""allowSwitchOff"": false,
          ""anchorMin"": { ""x"": 0, ""y"": 0 },
          ""anchorMax"": { ""x"": 1, ""y"": 1 },
          ""position"": { ""x"": 8.15, ""y"": 14.41 },
          ""size"": { ""x"": -16.3, ""y"": -28.81 },
          ""children"": [
            {
              ""name"": ""todayOracle"",
              ""type"": ""Toggle"",
              ""size"": { ""x"": 75, ""y"": 75 },
              ""backgroundSprite"": ""bg_oracle"",
              ""checkmarkSprite"": ""check_oracle"",
              ""text"": ""今日神谕"",
              ""fontSize"": 20,
              ""isOn"": true,
              ""outlineColor"": { ""r"": 0.903, ""g"": 0.005, ""b"": 1, ""a"": 0.5 },
              ""outlineDistance"": { ""x"": 1, ""y"": -1 }
            },
            {
              ""name"": ""dialogue"",
              ""type"": ""Toggle"",
              ""size"": { ""x"": 75, ""y"": 75 },
              ""backgroundSprite"": ""bg_dialogue"",
              ""checkmarkSprite"": ""check_dialogue"",
              ""text"": ""对话"",
              ""fontSize"": 20,
              ""isOn"": false,
              ""outlineColor"": { ""r"": 0.903, ""g"": 0.005, ""b"": 1, ""a"": 0.5 },
              ""outlineDistance"": { ""x"": 1, ""y"": -1 }
            },
            {
              ""name"": ""friend"",
              ""type"": ""Toggle"",
              ""size"": { ""x"": 75, ""y"": 75 },
              ""backgroundSprite"": ""bg_friend"",
              ""checkmarkSprite"": ""check_friend"",
              ""text"": ""一起玩"",
              ""fontSize"": 20,
              ""isOn"": false,
              ""outlineColor"": { ""r"": 0.903, ""g"": 0.005, ""b"": 1, ""a"": 0.5 },
              ""outlineDistance"": { ""x"": 1, ""y"": -1 }
            },
            {
              ""name"": ""my"",
              ""type"": ""Toggle"",
              ""size"": { ""x"": 75, ""y"": 75 },
              ""backgroundSprite"": ""bg_my"",
              ""checkmarkSprite"": ""check_my"",
              ""text"": ""我的"",
              ""fontSize"": 20,
              ""isOn"": false,
              ""outlineColor"": { ""r"": 0.903, ""g"": 0.005, ""b"": 1, ""a"": 0.5 },
              ""outlineDistance"": { ""x"": 1, ""y"": -1 }
            }
          ]
        }
      ]
    }
  ]
}";

            string writePath = ToAssetsPath(picked);
            File.WriteAllText(picked, json);
            AssetDatabase.Refresh();

            // 自动关联
            jsonFile = AssetDatabase.LoadAssetAtPath<TextAsset>(writePath);
            SetStatus($"✅ 示例 JSON 已生成: {writePath}", MessageType.Info);
        }
    }
}
