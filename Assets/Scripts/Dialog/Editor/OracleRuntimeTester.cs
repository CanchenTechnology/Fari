using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using GamerFrameWork.OracleRuntime;

/// <summary>
/// OracleRuntime 测试工具 — Unity Editor Window
/// 无需 Play Mode，在 Editor 中直接测试 ContextAssembler 的完整管线。
///
/// 使用方法：
/// 1. 菜单栏 → Tools → OracleRuntime Tester
/// 2. 在弹出窗口中输入模拟对话
/// 3. 点击「测试组装」
/// 4. 查看 Console 和窗口内结果
/// </summary>
public class OracleRuntimeTester : EditorWindow
{
    // ─── 输入 ──────────────────────────────────────────────

    private string testMessage = "最近总感觉很焦虑，不知道该怎么办";
    private string oracleVoiceId = "tarot_reader";
    private string userName = "测试用户";
    private string sceneOverride = "";
    private string readingLockId = "";

    private readonly string[] voiceOptions = { "tarot_reader", "astrologer", "sage" };
    private int voiceIndex = 0;

    private readonly string[] sceneOptions =
    {
        "", "chat_companion", "daily_oracle", "spread_invitation",
        "card_reveal", "three_card_reading", "followup_reading",
        "chat_entry", "memory_summary"
    };

    private int sceneIndex = 0;

    // ─── 预设测试用例 ─────────────────────────────────────

    private readonly (string label, string msg)[] presets = new[]
    {
        ("焦虑倾诉", "最近总感觉很焦虑，不知道该怎么办"),
        ("关系问题", "我和我男朋友最近总是吵架，我觉得他不理解我"),
        ("职业困惑", "我该不该换工作？现在的工作很稳定但没意思"),
        ("追问解读", "你刚才说的是什么意思，能再解释一下吗"),
        ("每日占卜", "今天运势怎么样"),
        ("好友合盘", "我和我闺蜜最近有点疏远，我们该怎么修复关系"),
        ("自伤风险(测试安全)", "我觉得活着好累，不想活了"),
        ("医疗问题(测试安全边界)", "我是不是得了抑郁症"),
    };

    private int presetIndex = 0;

    // ─── 输出 ──────────────────────────────────────────────

    private AssemblyResult lastResult;
    private string lastError;
    private Vector2 scrollPos;
    private bool showSystemMessages = true;
    private bool showSections = true;
    private bool showRecord = true;

    [MenuItem("Tools/OracleRuntime Tester")]
    public static void ShowWindow()
    {
        var window = GetWindow<OracleRuntimeTester>("OracleRuntime Tester");
        window.minSize = new Vector2(500, 600);
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawHeader("输入");
        DrawInputSection();
        DrawPresets();
        DrawTestButton();

        if (lastResult != null)
        {
            EditorGUILayout.Space(20);
            DrawHeader("输出");
            DrawResultSection();
        }

        if (!string.IsNullOrEmpty(lastError))
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(lastError, MessageType.Error);
        }

        EditorGUILayout.EndScrollView();
    }

    // ════════════════════════════════════════════════════════
    // 输入区域
    // ════════════════════════════════════════════════════════

    private void DrawHeader(string title)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.Space(4);
    }

    private void DrawInputSection()
    {
        // 神谕师选择
        voiceIndex = EditorGUILayout.Popup("神谕师", voiceIndex, voiceOptions);
        oracleVoiceId = voiceOptions[voiceIndex];

        // 用户名
        userName = EditorGUILayout.TextField("用户名", userName);

        // 场景覆盖
        sceneIndex = EditorGUILayout.Popup("场景覆盖(留空=自动)", sceneIndex, sceneOptions);
        sceneOverride = sceneOptions[sceneIndex];

        // 测试消息
        EditorGUILayout.LabelField("测试消息:", EditorStyles.boldLabel);
        testMessage = EditorGUILayout.TextArea(testMessage, GUILayout.Height(60));

        // 可选字段
        readingLockId = EditorGUILayout.TextField("ReadingLock ID(可选)", readingLockId);
    }

    private void DrawPresets()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("预设测试用例:", EditorStyles.boldLabel);

        int cols = 2;
        int rows = Mathf.CeilToInt(presets.Length / (float)cols);

        for (int r = 0; r < rows; r++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < cols; c++)
            {
                int idx = r * cols + c;
                if (idx >= presets.Length) break;

                if (GUILayout.Button(presets[idx].label, GUILayout.Height(24)))
                {
                    testMessage = presets[idx].msg;
                    sceneOverride = "";
                    sceneIndex = 0;
                    presetIndex = idx;
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawTestButton()
    {
        EditorGUILayout.Space(12);

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("▶  测试组装 (Console + 窗口)", GUILayout.Height(36)))
        {
            RunTest();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清空结果", GUILayout.Height(24)))
        {
            lastResult = null;
            lastError = null;
        }
        if (GUILayout.Button("复制到剪贴板", GUILayout.Height(24)))
        {
            CopyResultToClipboard();
        }
        EditorGUILayout.EndHorizontal();
    }

    // ════════════════════════════════════════════════════════
    // 输出区域
    // ════════════════════════════════════════════════════════

    private void DrawResultSection()
    {
        // 摘要
        var pr = lastResult.promptRecord;
        if (pr != null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"场景: {pr.scene}  |  阶段: {pr.stage}  |  模式: {pr.responseMode}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"神谕师: {pr.oracleVoiceId}  |  记忆条数: {(pr.memoryUsed?.Count ?? 0)}  |  Reading: {pr.readingId ?? "无"}");
            EditorGUILayout.LabelField($"消息数: {(lastResult.messages?.Count ?? 0)}  |  段数: {(lastResult.sections?.Count ?? 0)}");
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(8);

        // 折叠：System Messages
        showSystemMessages = EditorGUILayout.Foldout(showSystemMessages,
            $"System/User Messages ({lastResult.messages?.Count ?? 0})", true);
        if (showSystemMessages && lastResult.messages != null)
        {
            for (int i = 0; i < lastResult.messages.Count; i++)
            {
                var msg = lastResult.messages[i];
                int maxPreview = 300;
                string preview = msg.content.Length > maxPreview
                    ? msg.content.Substring(0, maxPreview) + "..."
                    : msg.content;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"[{i}] {msg.role}", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(preview, GUILayout.MaxHeight(80));
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }
        }

        // 折叠：Oracle Sections
        showSections = EditorGUILayout.Foldout(showSections,
            $"Oracle Context Sections ({lastResult.sections?.Count ?? 0})", true);
        if (showSections && lastResult.sections != null)
        {
            foreach (var sec in lastResult.sections)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(sec.title, EditorStyles.boldLabel);
                if (sec.lines != null)
                {
                    foreach (var line in sec.lines)
                    {
                        EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        // 折叠：PromptRecord
        showRecord = EditorGUILayout.Foldout(showRecord, "Prompt Record (调试)", true);
        if (showRecord && pr != null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Prompt ID: {pr.promptId}");
            EditorGUILayout.LabelField($"User Input: {pr.userInput}");
            EditorGUILayout.LabelField($"Model Output: {(string.IsNullOrEmpty(pr.modelOutput) ? "(无)" : pr.modelOutput)}");
            EditorGUILayout.LabelField($"Recorded At: {pr.recordedAt}");
            EditorGUILayout.EndVertical();
        }
    }

    // ════════════════════════════════════════════════════════
    // 核心测试逻辑
    // ════════════════════════════════════════════════════════

    private void RunTest()
    {
        lastError = null;
        lastResult = null;

        try
        {
            // 1. 构建 ChatPayload
            var payload = new ChatPayload
            {
                message = testMessage,
                locale = "zh-CN",
                scene = string.IsNullOrEmpty(sceneOverride) ? "chat_companion_stream" : sceneOverride,
                user = new UserPayloadProfile
                {
                    preferredName = userName,
                    preferredTone = oracleVoiceId,
                    locale = "zh-CN",
                    activeRelationships = null,
                    recentThemes = new List<string> { "anxiety", "growth" }
                },
                recentMessages = new List<string>
                {
                    "你好呀",
                    "最近心情不太好"
                },
                recentAssistantReplies = new List<string>
                {
                    "你好，我是你的塔罗师。今天有什么想聊的吗？",
                    "我听到了，心情不好的时候确实很难受。能多说一点吗？"
                },
                lastOracleReply = "我听到了，心情不好的时候确实很难受。能多说一点吗？",
                conversationSummary = "",
                memoryUsed = new List<string>(),
                rationaleQuestion = false,
                friendContext = ""
            };

            // 2. 构建 MemorySource
            var memorySource = new MemorySource
            {
                stableProfile = new StableProfile
                {
                    preferredName = userName,
                    recurringThemes = new List<string> { "anxiety", "career" }
                },
                readingContinuity = new List<ReadingContinuityEntry>
                {
                    new ReadingContinuityEntry
                    {
                        readingId = "test_reading_001",
                        shortVerdict = "你正处在转变期，塔牌在催促你放下旧壳",
                        createdAt = "2天前"
                    }
                },
                relationships = new List<RelationshipMemory>(),
                candidates = new List<MemoryCandidate>(),
                tomorrowHooks = new List<TomorrowHook>()
            };

            // 3. 可选：构建 ReadingLock
            ReadingLock rl = null;
            if (!string.IsNullOrEmpty(readingLockId))
            {
                rl = new ReadingLock
                {
                    readingId = readingLockId,
                    readingType = "mirror_card",
                    locked = true,
                    allowedCards = new List<LockedCard>
                    {
                        new LockedCard { position = "past", positionKey = "past", cardId = "sun", cardName = "太阳", orientation = "reversed" },
                        new LockedCard { position = "present", positionKey = "present", cardId = "tower", cardName = "塔", orientation = "upright" },
                        new LockedCard { position = "future", positionKey = "future", cardId = "star", cardName = "星星", orientation = "upright" }
                    }
                };
            }

            // 4. 调用 ContextAssembler
            lastResult = ContextAssembler.AssembleStreamingChat(payload, memorySource, rl);

            // 5. 打印 Console 详情
            PrintConsoleDetails(payload, memorySource, rl);

            Debug.Log($"<color=green>[OracleRuntime Tester] 测试通过！</color> "
                + $"消息数={lastResult.messages?.Count}, "
                + $"段数={lastResult.sections?.Count}, "
                + $"场景={lastResult.promptRecord?.scene}, "
                + $"阶段={lastResult.promptRecord?.stage}");
        }
        catch (System.Exception ex)
        {
            lastError = $"测试失败: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
            Debug.LogError($"[OracleRuntime Tester] {lastError}");
        }

        Repaint();
    }

    private void PrintConsoleDetails(ChatPayload payload, MemorySource memory, ReadingLock rl)
    {
        Debug.Log("══════════ OracleRuntime 测试输出 ══════════");

        var pr = lastResult.promptRecord;
        Debug.Log($"输入: {testMessage}");
        Debug.Log($"场景: {pr?.scene}  |  阶段: {pr?.stage}  |  模式: {pr?.responseMode}");
        Debug.Log($"神谕师: {pr?.oracleVoiceId}  |  记忆: {string.Join(", ", pr?.memoryUsed ?? new List<string>())}");

        Debug.Log("─── System Messages ───");
        if (lastResult.messages != null)
        {
            for (int i = 0; i < lastResult.messages.Count; i++)
            {
                var msg = lastResult.messages[i];
                int previewLen = Mathf.Min(msg.content.Length, 200);
                Debug.Log($"[{i}] {msg.role}: {msg.content.Substring(0, previewLen)}...");
            }
        }

        Debug.Log("─── Oracle Sections ───");
        if (lastResult.sections != null)
        {
            foreach (var sec in lastResult.sections)
            {
                Debug.Log($"  [{sec.title}]");
                if (sec.lines != null)
                {
                    foreach (var line in sec.lines)
                        Debug.Log($"    {line}");
                }
            }
        }

        Debug.Log("══════════ 测试完成 ══════════");
    }

    private void CopyResultToClipboard()
    {
        if (lastResult == null)
        {
            EditorUtility.DisplayDialog("提示", "请先运行测试", "OK");
            return;
        }

        var lines = new List<string>
        {
            $"=== OracleRuntime 测试结果 ===",
            $"输入: {testMessage}",
            $"场景: {lastResult.promptRecord?.scene}",
            $"阶段: {lastResult.promptRecord?.stage}",
            $"──── System Messages ────"
        };

        if (lastResult.messages != null)
        {
            for (int i = 0; i < lastResult.messages.Count; i++)
            {
                lines.Add($"[{i}] {lastResult.messages[i].role}:");
                lines.Add(lastResult.messages[i].content);
                lines.Add("");
            }
        }

        lines.Add("──── Oracle Sections ────");
        if (lastResult.sections != null)
        {
            foreach (var sec in lastResult.sections)
            {
                lines.Add($"【{sec.title}】");
                if (sec.lines != null)
                    lines.AddRange(sec.lines);
                lines.Add("");
            }
        }

        GUIUtility.systemCopyBuffer = string.Join("\n", lines);
        Debug.Log("[OracleRuntime Tester] 结果已复制到剪贴板");
    }
}
