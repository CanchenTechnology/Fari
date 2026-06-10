using UnityEngine;
using UnityEditor;
using System.IO;
using TMPro;

namespace UGUIBuilder
{
    /// <summary>
    /// UGUI Builder 编辑器窗口 —— 拖入 JSON、选择 Sprite 文件夹和输出路径，一键生成 UGUI Prefab。
    /// 菜单位置: Tools → UGUI Builder → Open Window
    /// </summary>
    public class UGUIBuilderWindow : EditorWindow
    {
        private const string PREF_SPRITE_FOLDER = "UGUIBuilder_SpriteFolder";
        private const string PREF_PREFAB_PATH  = "UGUIBuilder_PrefabPath";
        private const string PREF_FONT_PATH    = "UGUIBuilder_FontPath";
        private const string PREF_TMP_FONT_PATH= "UGUIBuilder_TMPFontPath";

        private TextAsset jsonFile;
        private string spriteFolder = "Assets/Art/UI/Sprites";
        private string prefabOutputPath = "Assets/UI/Generated/NewScreen.prefab";
        private Font selectedFont;
        private TMP_FontAsset tmpSelectedFont;

        private Vector2 scrollPos;
        private string statusMsg = "";
        private MessageType statusType = MessageType.None;

        // 校验与预览
        private System.Collections.Generic.List<string> validationMessages = new System.Collections.Generic.List<string>();
        private Vector2 validationScrollPos;
        private GameObject previewRoot;

        private bool foldoutJSON = true;
        private bool foldoutPaths = true;
        private bool foldoutBuild = true;

        private GUIStyle headerStyle;

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
            spriteFolder = EditorPrefs.GetString(PREF_SPRITE_FOLDER, "Assets/Art/UI/Sprites");
            prefabOutputPath = EditorPrefs.GetString(PREF_PREFAB_PATH, "Assets/UI/Generated/NewScreen.prefab");

            // 缓存 GUIStyle，避免 OnGUI 中每帧创建
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

            // 恢复上次选择的字体
            string savedFontPath = EditorPrefs.GetString(PREF_FONT_PATH, "");
            if (!string.IsNullOrEmpty(savedFontPath))
            {
                selectedFont = AssetDatabase.LoadAssetAtPath<Font>(savedFontPath);
            }

            // 恢复 TMP 字体
            string savedTMPFontPath = EditorPrefs.GetString(PREF_TMP_FONT_PATH, "");
            if (!string.IsNullOrEmpty(savedTMPFontPath))
            {
                tmpSelectedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(savedTMPFontPath);
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
            EditorGUILayout.LabelField("UGUI Prefab Builder", headerStyle);
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

                // TMP 字体选择
                EditorGUILayout.BeginHorizontal();
                tmpSelectedFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
                    new GUIContent("TMP 字体", "TextMeshPro 字体（优先于 Legacy 字体）"),
                    tmpSelectedFont, typeof(TMP_FontAsset), false);
                if (tmpSelectedFont != null && GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18)))
                {
                    tmpSelectedFont = null;
                }
                EditorGUILayout.EndHorizontal();

                if (tmpSelectedFont != null)
                {
                    string tmpFontPath = AssetDatabase.GetAssetPath(tmpSelectedFont);
                    EditorGUILayout.LabelField("  TMP 路径: " + tmpFontPath, EditorStyles.miniLabel);
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
            foldoutBuild = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutBuild, "3. 校验 · 预览 · 生成");
            if (foldoutBuild)
            {
                EditorGUI.indentLevel++;

                // ---- 校验按钮 ----
                GUI.enabled = jsonFile != null;
                GUI.backgroundColor = jsonFile != null ? new Color(0.45f, 0.6f, 0.85f) : Color.gray;
                if (GUILayout.Button("🔍 校验 JSON", GUILayout.Height(28)))
                {
                    RunValidation();
                }
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;

                // ---- 校验结果面板 ----
                if (validationMessages.Count > 0)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    validationScrollPos = EditorGUILayout.BeginScrollView(validationScrollPos, GUILayout.MaxHeight(140));
                    foreach (var msg in validationMessages)
                    {
                        if (msg.StartsWith("[ERROR]"))
                        {
                            GUI.color = new Color(0.95f, 0.3f, 0.25f);
                            EditorGUILayout.LabelField("  ✕ " + msg.Substring(8), EditorStyles.miniLabel);
                        }
                        else if (msg.StartsWith("[WARN]"))
                        {
                            GUI.color = new Color(0.95f, 0.75f, 0.1f);
                            EditorGUILayout.LabelField("  ⚠ " + msg.Substring(7), EditorStyles.miniLabel);
                        }
                        else
                        {
                            GUI.color = Color.gray;
                            EditorGUILayout.LabelField("  " + msg.Substring(7), EditorStyles.miniLabel);
                        }
                    }
                    GUI.color = Color.white;
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndVertical();
                }

                GUILayout.Space(6);

                // ---- 预览 / 清除按钮 ----
                EditorGUILayout.BeginHorizontal();
                bool canPreview = jsonFile != null && !string.IsNullOrEmpty(spriteFolder);
                GUI.enabled = canPreview && previewRoot == null;
                if (GUILayout.Button("👁 预览到场景", GUILayout.Height(30)))
                {
                    RunPreview();
                }

                GUI.enabled = previewRoot != null;
                GUI.backgroundColor = previewRoot != null ? new Color(0.85f, 0.4f, 0.4f) : Color.gray;
                if (GUILayout.Button("✕ 清除预览", GUILayout.Height(30)))
                {
                    ClearPreviewInScene();
                }
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(6);

                // ---- 生成按钮 ----
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
            EditorPrefs.SetString(PREF_SPRITE_FOLDER, spriteFolder);
            EditorPrefs.SetString(PREF_PREFAB_PATH, prefabOutputPath);

            // 保存字体路径
            if (selectedFont != null)
            {
                string fontPath = AssetDatabase.GetAssetPath(selectedFont);
                EditorPrefs.SetString(PREF_FONT_PATH, fontPath);
            }
            else
            {
                EditorPrefs.SetString(PREF_FONT_PATH, "");
            }

            // 保存 TMP 字体路径
            if (tmpSelectedFont != null)
            {
                string tmpFontPath = AssetDatabase.GetAssetPath(tmpSelectedFont);
                EditorPrefs.SetString(PREF_TMP_FONT_PATH, tmpFontPath);
            }
            else
            {
                EditorPrefs.SetString(PREF_TMP_FONT_PATH, "");
            }

            try
            {
                UGUIBuilderTool.BuildUGUIFromJSON(jsonPath, spriteFolder, prefabOutputPath, selectedFont, tmpSelectedFont);

                SetStatus($"✅ Prefab 生成成功: {prefabOutputPath}", MessageType.Info);

                // 定位生成的 Prefab
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabOutputPath);
                if (prefab != null)
                {
                    EditorGUIUtility.PingObject(prefab);
                    Selection.activeObject = prefab;
                }
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
  ""screenName"": ""SampleLayout"",
  ""resolution"": { ""x"": 750, ""y"": 1334 },
  ""matchWidthOrHeight"": 0.5,
  ""includeMask"": false,
  ""defaultFont"": """",
  ""elements"": [
    {
      ""name"": ""FullBg"",
      ""type"": ""Panel"",
      ""color"": { ""r"": 0.08, ""g"": 0.08, ""b"": 0.12, ""a"": 1 },
      ""anchorMin"": { ""x"": 0, ""y"": 0 },
      ""anchorMax"": { ""x"": 1, ""y"": 1 },
      ""size"": { ""x"": 0, ""y"": 0 },
      ""children"": [
        {
          ""name"": ""Title"",
          ""type"": ""Text"",
          ""text"": ""示例页面"",
          ""fontSize"": 32,
          ""fontStyle"": ""Bold"",
          ""textAlignment"": ""Center"",
          ""textColor"": { ""r"": 0.91, ""g"": 0.91, ""b"": 0.94, ""a"": 1 },
          ""position"": { ""x"": 0, ""y"": 200 }
        },
        {
          ""name"": ""DescText"",
          ""type"": ""Text"",
          ""text"": ""涵盖 Image / Text / Button / InputField / ScrollRect / ToggleGroup"",
          ""fontSize"": 16,
          ""textAlignment"": ""Center"",
          ""textColor"": { ""r"": 0.6, ""g"": 0.6, ""b"": 0.69, ""a"": 1 },
          ""position"": { ""x"": 0, ""y"": 150 }
        },
        {
          ""name"": ""FilledBar"",
          ""type"": ""Image"",
          ""imageType"": ""Filled"",
          ""fillMethod"": ""Horizontal"",
          ""fillAmount"": 0.65,
          ""color"": { ""r"": 0.27, ""g"": 0.55, ""b"": 0.91, ""a"": 1 },
          ""size"": { ""x"": 500, ""y"": 24 },
          ""position"": { ""x"": 0, ""y"": 80 }
        },
        {
          ""name"": ""InputArea"",
          ""type"": ""InputField"",
          ""placeholderText"": ""随便写点什么..."",
          ""contentType"": ""Standard"",
          ""fontSize"": 20,
          ""sprite"": ""input_bg"",
          ""size"": { ""x"": 600, ""y"": 56 },
          ""position"": { ""x"": 0, ""y"": 0 }
        },
        {
          ""name"": ""CardScroll"",
          ""type"": ""ScrollRect"",
          ""verticalScroll"": true,
          ""horizontalScroll"": false,
          ""layoutType"": ""Vertical"",
          ""spacing"": 10,
          ""paddingTop"": 12,
          ""paddingBottom"": 12,
          ""paddingLeft"": 20,
          ""paddingRight"": 20,
          ""color"": { ""r"": 0.12, ""g"": 0.12, ""b"": 0.18, ""a"": 1 },
          ""size"": { ""x"": 680, ""y"": 400 },
          ""position"": { ""x"": 0, ""y"": -280 },
          ""children"": [
            {
              ""name"": ""Card1"",
              ""type"": ""Panel"",
              ""size"": { ""x"": 640, ""y"": 100 },
              ""color"": { ""r"": 0.15, ""g"": 0.15, ""b"": 0.25, ""a"": 1 }
            },
            {
              ""name"": ""Card2"",
              ""type"": ""Panel"",
              ""size"": { ""x"": 640, ""y"": 100 },
              ""color"": { ""r"": 0.15, ""g"": 0.15, ""b"": 0.25, ""a"": 1 }
            },
            {
              ""name"": ""Card3"",
              ""type"": ""Panel"",
              ""size"": { ""x"": 640, ""y"": 100 },
              ""color"": { ""r"": 0.15, ""g"": 0.15, ""b"": 0.25, ""a"": 1 }
            },
            {
              ""name"": ""Card4"",
              ""type"": ""Panel"",
              ""size"": { ""x"": 640, ""y"": 100 },
              ""color"": { ""r"": 0.15, ""g"": 0.15, ""b"": 0.25, ""a"": 1 }
            }
          ]
        },
        {
          ""name"": ""BottomBar"",
          ""type"": ""ToggleGroup"",
          ""allowSwitchOff"": false,
          ""spacing"": 40,
          ""paddingTop"": 12,
          ""anchorMin"": { ""x"": 0, ""y"": 0 },
          ""anchorMax"": { ""x"": 1, ""y"": 0 },
          ""size"": { ""x"": 0, ""y"": 90 },
          ""position"": { ""x"": 0, ""y"": 0 },
          ""color"": { ""r"": 0.06, ""g"": 0.06, ""b"": 0.1, ""a"": 1 },
          ""children"": [
            {
              ""name"": ""Tab1"",
              ""type"": ""Toggle"",
              ""text"": ""首页"",
              ""fontSize"": 14,
              ""isOn"": true
            },
            {
              ""name"": ""Tab2"",
              ""type"": ""Toggle"",
              ""text"": ""发现"",
              ""fontSize"": 14,
              ""isOn"": false
            },
            {
              ""name"": ""Tab3"",
              ""type"": ""Toggle"",
              ""text"": ""我的"",
              ""fontSize"": 14,
              ""isOn"": false
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

        // ============================================================
        // 校验 & 预览
        // ============================================================

        private void RunValidation()
        {
            if (jsonFile == null) return;

            string jsonPath = AssetDatabase.GetAssetPath(jsonFile);
            string text = File.ReadAllText(jsonPath);
            UGUIBuilderTool.LayoutRoot layout;
            try
            {
                layout = JsonUtility.FromJson<UGUIBuilderTool.LayoutRoot>(text);
            }
            catch (System.Exception e)
            {
                validationMessages = new System.Collections.Generic.List<string>
                    { $"[ERROR] JSON 解析失败: {e.Message}" };
                Repaint();
                return;
            }

            var result = UGUIBuilderTool.ValidateLayout(layout);
            validationMessages = result.messages;
            Repaint();
        }

        private void RunPreview()
        {
            // 先清除旧预览
            if (previewRoot != null)
            {
                UGUIBuilderTool.ClearPreview(previewRoot);
                previewRoot = null;
            }

            string jsonPath = AssetDatabase.GetAssetPath(jsonFile);
            EditorPrefs.SetString(PREF_SPRITE_FOLDER, spriteFolder);
            EditorPrefs.SetString(PREF_FONT_PATH,
                selectedFont != null ? AssetDatabase.GetAssetPath(selectedFont) : "");
            EditorPrefs.SetString(PREF_TMP_FONT_PATH,
                tmpSelectedFont != null ? AssetDatabase.GetAssetPath(tmpSelectedFont) : "");

            try
            {
                previewRoot = UGUIBuilderTool.BuildPreviewFromJSON(jsonPath, spriteFolder, selectedFont, tmpSelectedFont);
                if (previewRoot != null)
                {
                    Selection.activeObject = previewRoot;
                    EditorGUIUtility.PingObject(previewRoot);
                    SetStatus($"✅ 预览已生成: {previewRoot.name}", MessageType.Info);
                }
            }
            catch (System.Exception e)
            {
                SetStatus($"❌ 预览失败: {e.Message}", MessageType.Error);
                Debug.LogException(e);
            }
        }

        private void ClearPreviewInScene()
        {
            if (previewRoot == null) return;
            UGUIBuilderTool.ClearPreview(previewRoot);
            previewRoot = null;
            SetStatus("预览已清除", MessageType.Info);
        }
    }
}
