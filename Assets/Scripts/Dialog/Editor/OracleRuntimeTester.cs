using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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
        "", "chat_companion", "daily_oracle", "complete_interpretation",
        "chat_entry", "quick_reading", "spread_invitation",
        "card_reveal", "card_position_description", "three_card_reading",
        "followup_reading", "friend_divination_result", "user_memory_summary"
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
    private bool showPromptDiagnostics = true;
    private string promptDiagnostics = "";

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
        DrawDiagnosticsSection();

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

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("验证 Prompt 配置", GUILayout.Height(24)))
        {
            ValidatePromptConfig();
        }
        if (GUILayout.Button("运行验收测试", GUILayout.Height(24)))
        {
            RunAcceptanceTests();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDiagnosticsSection()
    {
        if (string.IsNullOrEmpty(promptDiagnostics)) return;

        EditorGUILayout.Space(8);
        showPromptDiagnostics = EditorGUILayout.Foldout(showPromptDiagnostics, "Prompt / Runtime Diagnostics", true);
        if (!showPromptDiagnostics) return;

        EditorGUILayout.TextArea(promptDiagnostics, GUILayout.MinHeight(120));
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

    private void ValidatePromptConfig()
    {
        PromptResources.ClearCache();
        List<string> lines = PromptResources.BuildDiagnostics();
        lines.AddRange(OraclePromptAssetAudit.BuildDiagnostics());
        promptDiagnostics = string.Join("\n", lines);

        List<string> issues = PromptResources.ValidateManifest();
        issues.AddRange(OraclePromptAssetAudit.FindIssues());
        if (issues.Count == 0)
            Debug.Log("[OracleRuntime Tester] Prompt manifest validation OK");
        else
            Debug.LogError("[OracleRuntime Tester] Prompt manifest issues:\n" + string.Join("\n", issues));

        Repaint();
    }

    private void RunAcceptanceTests()
    {
        var report = OracleRuntimeAcceptanceSuite.Run(oracleVoiceId, userName);
        promptDiagnostics = report.Text;
        OracleRuntimeAcceptanceSuite.LogReport(report);
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

public sealed class OracleRuntimeAcceptanceReport
{
    public int passed;
    public int failed;
    public List<string> lines = new List<string>();

    public bool Success => failed == 0;
    public string Text => string.Join("\n", lines);
}

public static class OraclePromptAssetAudit
{
    private const string ResourceAssetRoot = "Assets/Resources/prompts";
    private const string ResourcePrefix = "Assets/Resources/";

    private static readonly HashSet<string> ExemptResourcePaths = new HashSet<string>
    {
        "prompts/prompt_manifest"
    };

    private static readonly HashSet<string> PromptTextExtensions = new HashSet<string>
    {
        ".md", ".json", ".txt"
    };

    public static List<string> BuildDiagnostics()
    {
        var lines = new List<string> { "Prompt asset audit:" };
        List<string> issues = FindIssues();
        if (issues.Count == 0)
        {
            lines.Add("Prompt asset audit: OK");
            lines.Add($"Prompt assets discovered: {DiscoverPromptResourcePaths().Count}");
        }
        else
        {
            lines.AddRange(issues.Select(issue => "ASSET ISSUE: " + issue));
        }

        return lines;
    }

    public static List<string> FindIssues()
    {
        var issues = new List<string>();
        PromptManifestConfig manifest = PromptResources.GetManifest();
        List<PromptManifestEntry> entries = manifest.GetAllEntries()
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.resourcePath))
            .ToList();

        var declared = new HashSet<string>(entries.Select(entry => NormalizeResourcePath(entry.resourcePath)));
        var discovered = new HashSet<string>(DiscoverPromptResourcePaths());

        foreach (PromptManifestEntry entry in entries)
        {
            string resourcePath = NormalizeResourcePath(entry.resourcePath);
            if (!discovered.Contains(resourcePath))
                issues.Add($"Manifest resource has no matching asset: {entry.id} -> {resourcePath}");

            if (entry.kind == "schema")
                ValidateSchemaJson(entry, issues);
        }

        foreach (string resourcePath in discovered.OrderBy(path => path))
        {
            if (ExemptResourcePaths.Contains(resourcePath)) continue;
            if (!declared.Contains(resourcePath))
                issues.Add($"Prompt asset is not declared in manifest: {resourcePath}");
        }

        return issues;
    }

    private static void ValidateSchemaJson(PromptManifestEntry entry, List<string> issues)
    {
        string schemaText = PromptResources.Load(entry.resourcePath);
        if (string.IsNullOrWhiteSpace(schemaText)) return;

        try
        {
            Newtonsoft.Json.Linq.JToken.Parse(schemaText);
        }
        catch (System.Exception ex)
        {
            issues.Add($"Schema JSON parse failed: {entry.id} -> {entry.resourcePath}: {ex.Message}");
        }
    }

    private static List<string> DiscoverPromptResourcePaths()
    {
        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { ResourceAssetRoot });
        return guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsPromptTextAsset)
            .Select(ToResourcePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct()
            .OrderBy(path => path)
            .ToList();
    }

    private static bool IsPromptTextAsset(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return false;
        string extension = System.IO.Path.GetExtension(assetPath).ToLowerInvariant();
        return PromptTextExtensions.Contains(extension);
    }

    private static string ToResourcePath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith(ResourcePrefix))
            return "";

        string relative = assetPath.Substring(ResourcePrefix.Length);
        string withoutExtension = System.IO.Path.ChangeExtension(relative, null);
        return NormalizeResourcePath(withoutExtension);
    }

    private static string NormalizeResourcePath(string resourcePath)
    {
        return (resourcePath ?? "").Replace("\\", "/").Trim().Trim('/');
    }
}

public static class OracleRuntimeAcceptanceSuite
{
    [MenuItem("Tools/OracleRuntime/Run Acceptance Tests")]
    public static void RunFromMenu()
    {
        var report = Run();
        LogReport(report);
        EditorUtility.DisplayDialog(
            "OracleRuntime Acceptance Tests",
            report.Success ? $"通过：{report.passed} 项" : $"失败：{report.failed} 项，请看 Console",
            "OK");
    }

    public static void RunFromCommandLine()
    {
        var report = Run();
        LogReport(report);
        EditorApplication.Exit(report.Success ? 0 : 1);
    }

    public static OracleRuntimeAcceptanceReport Run(string oracleVoiceId = "tarot_reader", string userName = "测试用户")
    {
        PromptResources.ClearCache();

        var report = new OracleRuntimeAcceptanceReport();
        report.lines.Add("=== OracleRuntime Acceptance Tests ===");

        void Check(string label, bool condition, string detail)
        {
            if (condition)
            {
                report.passed++;
                report.lines.Add($"PASS | {label} | {detail}");
            }
            else
            {
                report.failed++;
                report.lines.Add($"FAIL | {label} | {detail}");
            }
        }

        void CheckJsonSceneSchema(string label, string sceneId, string message, params string[] requiredTokens)
        {
            AssemblyResult result = ContextAssembler.AssembleSceneCall(
                sceneId,
                BuildScenePayload(message, oracleVoiceId, userName),
                new MemorySource(),
                oracleVoiceId);

            string scenePrompt = result.messages != null && result.messages.Count > 2
                ? result.messages[2].content ?? ""
                : "";
            bool schemaAttached = HasSceneCallProtocol(result)
                && scenePrompt.Contains("## Output Schema")
                && requiredTokens.All(token => scenePrompt.Contains(token));

            Check(label,
                schemaAttached,
                schemaAttached ? $"scene={sceneId}, fields={string.Join(",", requiredTokens)}" : scenePrompt);
        }

        var manifestIssues = PromptResources.ValidateManifest();
        Check("prompt manifest", manifestIssues.Count == 0, manifestIssues.Count == 0 ? "all required prompts load" : string.Join("; ", manifestIssues));

        var assetIssues = OraclePromptAssetAudit.FindIssues();
        Check("prompt resources match manifest",
            assetIssues.Count == 0,
            assetIssues.Count == 0 ? "Resources/prompts fully declared" : string.Join("; ", assetIssues));

        string verdictSchema = PromptResources.LoadById("schemas.divination_verdict");
        Check("divination verdict schema loadable",
            verdictSchema.Contains("\"displayReply\"")
                && verdictSchema.Contains("\"shortVerdict\"")
                && verdictSchema.Contains("\"judgeContent\"")
                && verdictSchema.Contains("\"topics\""),
            string.IsNullOrWhiteSpace(verdictSchema) ? "missing schema" : verdictSchema);

        AssemblyResult normalDecision = AssembleCase("他会不会回我？", oracleVoiceId, userName, null, null);
        Check("streaming message assembly protocol",
            HasStreamingProtocol(normalDecision),
            $"messages={normalDecision.messages?.Count ?? 0}, sections={normalDecision.sections?.Count ?? 0}");
        Check("normal decision does not force tarot",
            normalDecision.promptRecord.stage != "before_draw" && normalDecision.promptRecord.stage != "card_reveal",
            $"stage={normalDecision.promptRecord.stage}, scene={normalDecision.promptRecord.scene}");

        AssemblyResult sceneCall = ContextAssembler.AssembleSceneCall(
            "daily_oracle",
            BuildScenePayload("今天适合提醒我什么？", oracleVoiceId, userName),
            new MemorySource(),
            oracleVoiceId);
        Check("scene call message assembly protocol",
            HasSceneCallProtocol(sceneCall),
            $"messages={sceneCall.messages?.Count ?? 0}, scene={sceneCall.promptRecord.scene}");
        Check("scene call attaches output schema",
            SceneCallHasDailyOracleSchema(sceneCall),
            sceneCall.messages != null && sceneCall.messages.Count > 2 ? sceneCall.messages[2].content : "missing scene prompt");

        AssemblyResult completeInterpretationSceneCall = ContextAssembler.AssembleSceneCall(
            "complete_interpretation",
            BuildScenePayload("牌名=星星，方向=正位，关键词=希望、修复、信任", oracleVoiceId, userName),
            new MemorySource(),
            oracleVoiceId);
        Check("complete interpretation scene attaches own schema",
            SceneCallHasCompleteInterpretationSchema(completeInterpretationSceneCall),
            completeInterpretationSceneCall.messages != null && completeInterpretationSceneCall.messages.Count > 2
                ? completeInterpretationSceneCall.messages[2].content
                : "missing scene prompt");

        AssemblyResult threeCardSceneCall = ContextAssembler.AssembleSceneCall(
            "three_card_reading",
            BuildScenePayload("帮我看这段关系的当下阻碍和走向", oracleVoiceId, userName),
            new MemorySource(),
            oracleVoiceId);
        Check("three-card scene attaches card schema",
            SceneCallHasThreeCardSchema(threeCardSceneCall),
            threeCardSceneCall.messages != null && threeCardSceneCall.messages.Count > 2
                ? threeCardSceneCall.messages[2].content
                : "missing scene prompt");

        AssemblyResult cardPositionSceneCall = ContextAssembler.AssembleSceneCall(
            "card_position_description",
            BuildScenePayload("牌阵：关系张力\n位置：你的位置\n卡牌：星星（正位）\n关键词：希望、修复、信任", oracleVoiceId, userName),
            new MemorySource(),
            oracleVoiceId);
        Check("card position description scene is prompt-configured",
            SceneCallHasCardPositionPrompt(cardPositionSceneCall),
            cardPositionSceneCall.messages != null && cardPositionSceneCall.messages.Count > 2
                ? cardPositionSceneCall.messages[2].content
                : "missing scene prompt");

        CheckJsonSceneSchema("chat companion scene attaches schema",
            "chat_companion",
            "我不知道该不该继续等他",
            "\"reply\"", "\"suggest_three_card\"", "\"followup_questions\"");
        CheckJsonSceneSchema("chat entry scene attaches schema",
            "chat_entry",
            "用户从今日牌入口进入对话页",
            "\"voice_text\"");
        CheckJsonSceneSchema("quick reading scene attaches schema",
            "quick_reading",
            "为用户生成快速占卜问题",
            "\"default_topic\"", "\"topics\"", "\"questions\"");
        CheckJsonSceneSchema("spread invitation scene attaches schema",
            "spread_invitation",
            "用户想看关系当下阻碍和走向",
            "\"invitation_text\"");
        CheckJsonSceneSchema("card reveal scene attaches schema",
            "card_reveal",
            "readingId=test_lock_001，当前牌=星星（正位），牌位=你的位置",
            "\"short_interpretation\"");
        CheckJsonSceneSchema("followup reading scene attaches schema",
            "followup_reading",
            "用户问：为什么你说我需要等 24 小时？",
            "\"reply\"", "\"suggested_message\"", "\"voice_text\"");
        CheckJsonSceneSchema("friend divination result scene attaches schema",
            "friend_divination_result",
            "用户与好友完成三牌关系占卜",
            "\"friend_divination\"", "\"summary\"", "\"ui\"");
        CheckJsonSceneSchema("memory summary scene attaches schema",
            "user_memory_summary",
            "总结最近一轮对话可长期使用的记忆",
            "\"memory_patch\"", "\"modelSummary\"", "\"recentThemes\"");

        AssemblyResult explicitDraw = AssembleCase("帮我抽牌看他会不会回我", oracleVoiceId, userName, null, null);
        Check("explicit tarot request enters draw flow",
            explicitDraw.promptRecord.stage == "before_draw",
            $"stage={explicitDraw.promptRecord.stage}");
        bool explicitDrawHasPlan = explicitDraw.messages.Last().content.Contains("\"divinationPlan\"")
            && explicitDraw.messages.Last().content.Contains("\"suggestedActions\"")
            && explicitDraw.messages.Last().content.Contains("\"start_spread\"");
        Check("explicit tarot request auto plans spread",
            explicitDrawHasPlan,
            explicitDraw.messages.Last().content);

        AssemblyResult timelinePlan = AssembleCase("帮我抽牌看这段关系未来七天的走向", oracleVoiceId, userName, null, null);
        bool timelineInPayload = timelinePlan.messages.Last().content.Contains("\"spreadKind\": \"timeline_thread\"");
        Check("timeline question chooses timeline spread",
            timelineInPayload,
            timelinePlan.messages.Last().content);

        var readingLock = BuildSampleReadingLock();
        AssemblyResult reveal = AssembleCase("翻开第一张", oracleVoiceId, userName, readingLock, payload =>
        {
            payload.actionKind = "reveal_card";
            payload.activeReadingId = readingLock.readingId;
            payload.activeReadingState = "cards_locked";
        });
        Check("card reveal uses reading lock",
            reveal.promptRecord.stage == "card_reveal" && reveal.promptRecord.readingId == readingLock.readingId,
            $"stage={reveal.promptRecord.stage}, reading={reveal.promptRecord.readingId}");

        AssemblyResult verdict = AssembleCase("给我完整解读", oracleVoiceId, userName, readingLock, payload =>
        {
            payload.actionKind = "complete_verdict";
            payload.activeReadingId = readingLock.readingId;
            payload.activeReadingState = "generating_verdict";
        });
        Check("complete verdict uses verdict stage",
            verdict.promptRecord.stage == "verdict" && verdict.promptRecord.readingId == readingLock.readingId,
            $"stage={verdict.promptRecord.stage}, reading={verdict.promptRecord.readingId}");

        var currentCardGuard = OutputGuard.Check(
            "星星提醒你先保持希望，但宝剑二说明对方还在回避。",
            new OutputGuardOptions
            {
                stage = "card_reveal",
                responseContract = ResponseContracts.GetFor("card_reveal"),
                readingLock = readingLock,
                currentCardId = "major_17",
                deckCardNames = new List<string> { "星星", "宝剑二", "圣杯八" }
            });
        Check("card reveal only current card",
            currentCardGuard.issues != null && currentCardGuard.issues.Contains("non_current_card"),
            currentCardGuard.issues == null ? "no issues" : string.Join(",", currentCardGuard.issues));

        var unlockedCardGuard = OutputGuard.Check(
            "这次女祭司也在提醒你别急着表态。",
            new OutputGuardOptions
            {
                stage = "card_reveal",
                responseContract = ResponseContracts.GetFor("card_reveal"),
                readingLock = readingLock,
                currentCardId = "major_17",
                deckCardNames = new List<string> { "星星", "宝剑二", "圣杯八", "女祭司" }
            });
        Check("locked reading rejects unlisted card",
            unlockedCardGuard.issues != null && unlockedCardGuard.issues.Contains("unlocked_card"),
            unlockedCardGuard.issues == null ? "no issues" : string.Join(",", unlockedCardGuard.issues));

        var thirdPartyGuard = OutputGuard.Check(
            "他肯定放不下你，只是在偷偷压住自己的感情。",
            new OutputGuardOptions
            {
                stage = "listen",
                responseContract = ResponseContracts.GetFor("listen")
            });
        Check("third-party mind claim guarded",
            thirdPartyGuard.issues != null && thirdPartyGuard.issues.Contains("third_party_mind_claim"),
            thirdPartyGuard.issues == null ? "no issues" : string.Join(",", thirdPartyGuard.issues));

        var clinicalBoundaryGuard = OutputGuard.Check(
            "你有抑郁症，我可以作为治疗师帮你治疗。",
            new OutputGuardOptions
            {
                stage = "listen",
                responseContract = ResponseContracts.GetFor("listen")
            });
        Check("clinical and therapy boundary guarded",
            clinicalBoundaryGuard.issues != null
                && clinicalBoundaryGuard.issues.Contains("clinical_diagnosis")
                && clinicalBoundaryGuard.issues.Contains("therapy_claim"),
            clinicalBoundaryGuard.issues == null ? "no issues" : string.Join(",", clinicalBoundaryGuard.issues));

        AssemblyResult rationale = AssembleCase("为什么这么说？", oracleVoiceId, userName, null, payload =>
        {
            payload.rationaleQuestion = true;
            payload.recentAssistantReplies = new List<string> { "我直说：你不是在等消息，你是在等证据。" };
        });
        bool rationaleInPayload = rationale.messages.Last().content.Contains("\"rationaleQuestion\": true");
        Check("rationale question included",
            rationaleInPayload,
            rationale.messages.Last().content);

        AssemblyResult plannedReading = AssembleCase("帮我抽牌看这段关系", oracleVoiceId, userName, null, payload =>
        {
            payload.divinationPlan = new DivinationPlan
            {
                planId = "plan_test_001",
                question = payload.message,
                scene = "relationship_anxiety",
                spreadKind = "relationship_tension",
                cardCount = 3,
                complexity = "standard",
                reasonForSpread = "关系问题需要拆开双方位置和互动张力。",
                positions = new List<SpreadPosition>
                {
                    new SpreadPosition { key = "self", label = "你的位置", prompt = "你在关系中的姿态" },
                    new SpreadPosition { key = "other", label = "对方的位置", prompt = "关系中另一方的动态模式" },
                    new SpreadPosition { key = "tension", label = "张力", prompt = "互动中的核心张力" }
                }
            };
        });
        bool planInPayload = plannedReading.messages.Last().content.Contains("\"divinationPlan\"");
        Check("divination plan included",
            planInPayload,
            plannedReading.messages.Last().content);

        AssemblyResult memoryLimit = AssembleCase("我还是很在意他有没有回我", oracleVoiceId, userName, null, payload =>
        {
            payload.activeRelationshipId = "rel_morgan";
        }, BuildHeavyMemorySource());
        Check("memory pack max 5",
            (memoryLimit.promptRecord.memoryUsed?.Count ?? 0) <= 5,
            $"memoryUsed={memoryLimit.promptRecord.memoryUsed?.Count ?? 0}");

        report.lines.Add($"=== Result: {report.passed} passed, {report.failed} failed ===");
        return report;
    }

    public static void LogReport(OracleRuntimeAcceptanceReport report)
    {
        if (report.Success)
            Debug.Log("[OracleRuntime Tester] Acceptance tests OK\n" + report.Text);
        else
            Debug.LogError("[OracleRuntime Tester] Acceptance tests failed\n" + report.Text);
    }

    private static AssemblyResult AssembleCase(
        string message,
        string oracleVoiceId,
        string userName,
        ReadingLock readingLock,
        System.Action<ChatPayload> configurePayload,
        MemorySource memorySource = null)
    {
        var payload = new ChatPayload
        {
            message = message,
            locale = "zh-CN",
            scene = "chat_companion_stream",
            user = new UserPayloadProfile
            {
                preferredName = userName,
                preferredTone = oracleVoiceId,
                locale = "zh-CN"
            },
            recentMessages = new List<string> { "最近我有点乱" },
            recentAssistantReplies = new List<string> { "我先不急着下结论。" }
        };

        configurePayload?.Invoke(payload);
        return ContextAssembler.AssembleStreamingChat(payload, memorySource ?? new MemorySource(), readingLock);
    }

    private static ChatPayload BuildScenePayload(string message, string oracleVoiceId, string userName)
    {
        return new ChatPayload
        {
            message = message,
            locale = "zh-CN",
            user = new UserPayloadProfile
            {
                preferredName = userName,
                preferredTone = oracleVoiceId,
                locale = "zh-CN"
            }
        };
    }

    private static bool HasStreamingProtocol(AssemblyResult result)
    {
        if (result?.messages == null || result.messages.Count != 6) return false;
        if (result.messages.Take(5).Any(message => message.role != "system")) return false;
        if (result.messages[5].role != "user") return false;
        if (result.sections == null || result.sections.Count != 9) return false;

        var sectionTitles = new HashSet<string>(result.sections.Select(section => section.title));
        string[] requiredSections =
        {
            "Safety Boundary", "Persona Contract", "Scene / Stage", "Therapy Lens",
            "Divination Plan", "Reading Lock", "Memory Pack", "Output Contract", "User Payload"
        };
        if (requiredSections.Any(title => !sectionTitles.Contains(title))) return false;

        string runtimeContext = result.messages[2].content ?? "";
        return runtimeContext.Contains("Oracle Runtime v1.2")
            && runtimeContext.Contains("## Persona Contract")
            && runtimeContext.Contains("## Output Contract")
            && runtimeContext.Contains("## User Payload")
            && (result.messages[5].content ?? "").Contains("\"message\"");
    }

    private static bool HasSceneCallProtocol(AssemblyResult result)
    {
        if (result?.messages == null || result.messages.Count != 5) return false;
        if (result.messages.Take(4).Any(message => message.role != "system")) return false;
        if (result.messages[4].role != "user") return false;

        return (result.messages[0].content ?? "").Contains("Nocturne Oracle")
            && (result.messages[2].content ?? "").Contains("场景")
            && (result.messages[3].content ?? "").Contains("用户记忆")
            && (result.messages[4].content ?? "").Contains("\"message\"");
    }

    private static bool SceneCallHasDailyOracleSchema(AssemblyResult result)
    {
        if (result?.messages == null || result.messages.Count <= 2) return false;
        string scenePrompt = result.messages[2].content ?? "";
        return scenePrompt.Contains("## Output Schema")
            && scenePrompt.Contains("\"oracle_sentence\"")
            && scenePrompt.Contains("\"full_reading\"")
            && scenePrompt.Contains("\"chat_prompts\"");
    }

    private static bool SceneCallHasCompleteInterpretationSchema(AssemblyResult result)
    {
        if (result?.messages == null || result.messages.Count <= 2) return false;
        string scenePrompt = result.messages[2].content ?? "";
        return scenePrompt.Contains("今日牌完整解读")
            && scenePrompt.Contains("## Output Schema")
            && scenePrompt.Contains("\"description\"")
            && scenePrompt.Contains("\"meaningAnalysis\"")
            && scenePrompt.Contains("\"actionSuggestion\"")
            && !scenePrompt.Contains("\"oracle_sentence\"");
    }

    private static bool SceneCallHasThreeCardSchema(AssemblyResult result)
    {
        if (result?.messages == null || result.messages.Count <= 2) return false;
        string scenePrompt = result.messages[2].content ?? "";
        return scenePrompt.Contains("## Output Schema")
            && scenePrompt.Contains("\"reading_type\"")
            && scenePrompt.Contains("\"summary\"")
            && scenePrompt.Contains("\"short_interpretation\"")
            && scenePrompt.Contains("\"deep_interpretation\"");
    }

    private static bool SceneCallHasCardPositionPrompt(AssemblyResult result)
    {
        if (result?.messages == null || result.messages.Count <= 2) return false;
        string scenePrompt = result.messages[2].content ?? "";
        return scenePrompt.Contains("牌位单张描述")
            && scenePrompt.Contains("只解释当前这一张牌")
            && scenePrompt.Contains("不要综合整个牌阵")
            && !scenePrompt.Contains("## Output Schema");
    }

    private static ReadingLock BuildSampleReadingLock()
    {
        return new ReadingLock
        {
            readingId = "test_lock_001",
            readingType = "relationship_tension",
            locked = true,
            allowedCards = new List<LockedCard>
            {
                new LockedCard { position = "你的位置", positionKey = "self", cardId = "major_17", cardName = "星星", orientation = "upright" },
                new LockedCard { position = "对方的位置", positionKey = "other", cardId = "swords_02", cardName = "宝剑二", orientation = "reversed" },
                new LockedCard { position = "张力", positionKey = "tension", cardId = "cups_08", cardName = "圣杯八", orientation = "upright" }
            }
        };
    }

    private static MemorySource BuildHeavyMemorySource()
    {
        return new MemorySource
        {
            stableProfile = new StableProfile
            {
                preferredName = "测试用户",
                preferredTone = "direct",
                recurringThemes = new List<string> { "等待回应", "关系边界", "反复确认", "焦虑" },
                doNotSay = new List<string> { "不要想太多", "答案在你心里" }
            },
            relationships = new List<RelationshipMemory>
            {
                new RelationshipMemory
                {
                    relationshipId = "rel_morgan",
                    displayName = "Morgan",
                    entityType = "crush",
                    consentMode = "user_scope_only",
                    knownFacts = new List<string> { "用户经常观察 Morgan 的回复节奏", "沉默会让用户觉得自己不重要" },
                    openLoops = new List<string> { "是否继续发第二条消息", "是否静音聊天" },
                    lastActionAdvice = "等 24 小时，不补第二条"
                }
            },
            candidates = new List<MemoryCandidate>
            {
                new MemoryCandidate { type = "preference", text = "用户偏好直接具体的建议。", status = "promoted", confidence = 0.9f },
                new MemoryCandidate { type = "boundary", text = "用户不喜欢空泛安慰。", status = "promoted", confidence = 0.8f },
                new MemoryCandidate { type = "open_loop", text = "明天复盘是否发了第二条消息。", status = "pending", confidence = 0.7f }
            }
        };
    }
}

public sealed class OracleRuntimeBuildPreflight : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var acceptance = OracleRuntimeAcceptanceSuite.Run();
        OracleRuntimeAcceptanceSuite.LogReport(acceptance);
        if (!acceptance.Success)
            throw new BuildFailedException("OracleRuntime acceptance tests failed before build.\n" + acceptance.Text);
    }
}
