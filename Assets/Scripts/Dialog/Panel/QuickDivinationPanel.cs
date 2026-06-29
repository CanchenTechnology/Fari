using System;
using System.Collections;
using System.Collections.Generic;
using GamerFrameWork;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;
using XFGameFrameWork;
using TMPro;

public class QuickDivinationPanel : MonoBehaviour
{
    private const string ThreeCardQuickQuestion = "给我做个三张牌的占卜";
    private const string SelectedTopicDropdownTextColor = "#FE8E54";

    public class QuickQuestionRequest
    {
        public string topicKey;
        public string topicLabel;
        public string oracleId;
        public CharacterType characterType;
        public int requiredCount;
        public bool isExpanded;
    }

    /// <summary>
    /// 外部 AI/规则生成问题入口。
    /// 返回的问题会优先显示；返回 null 或空列表时走 Inspector/Firebase/fallback。
    /// </summary>
    public static Func<QuickQuestionRequest, List<string>> QuestionProvider;

    [Header("面板容器")]
    public CanvasGroup canvasGroup;

    public Button exitBtn;

    [Header("标题区域")]
    public Button toggleBtn;            // 收起/展开按钮
    public TMP_Text toggleBtnText;          // 按钮文字（收起 / 展开）
    public RectTransform toggleArrowIcon;

    [Header("话题标签按钮 (4个)")]
    public TMP_Dropdown topicDropdown;

    // → 扩展：占星师/冥想师有不同标签，这里保留字典方便切换
    private Dictionary<string, Button> mTopicButtons =new();
    private List<string> mTopicOrder = new();   // 保持话题显示顺序

    [Header("问题列表")]
    public LoopListView2 loopListView2;
    [Tooltip("可在 Inspector 直接配置快速问题。留空时使用 QuickDivinationData 的默认/Firebase 配置。key 需要和话题按钮一致。")]
    public List<QuickTopic> inspectorTopics = new List<QuickTopic>();

    [Header("展开/收起数量")]
    public int collapsedQuestionCount = 3;
    public int expandedQuestionCount = 5;
    public int scrollableQuestionCount = 8;

    [Header("Content 自动高度")]
    public RectTransform contentRoot;
    public RectTransform questionListRect;
    public float questionItemHeight = 70f;
    public float questionListPadding = 10f;
    public float questionListTopPadding = 6f;
    public float questionListBottomPadding = 18f;
    public float minPanelHeight = 0f;
    public float minQuestionListHeight = 0f;
    [Tooltip("展开后的面板最大高度。小于等于 0 时保持 prefab 初始高度，避免压住输入框和底部导航。")]
    public float maxPanelHeight = 0f;
    [Tooltip("展开后的问题列表最大高度。小于等于 0 时根据 maxPanelHeight 自动计算，超出后列表内部滚动。")]
    public float maxQuestionListHeight = 0f;

    [Header("分区自动布局")]
    public bool autoLayoutSections = true;
    public RectTransform headerRoot;
    public RectTransform topicRoot;
    public float layoutTopPadding = 8f;
    public float headerTopicSpacing = 4f;
    public float headerLayoutHeight = 0f;
    public float topicListSpacing = 8f;
    public float layoutBottomPadding = 24f;
    [Tooltip("勾选后面板底部保持贴近输入框，收起时顶部下移，避免列表和输入框之间留大空位。")]
    public bool keepPanelBottomFixed = true;
    public RectTransform chatBoundsRect;
    public bool clampInsideChatBounds = true;
    public float chatBoundsPadding = 0f;
    public RectTransform followTargetRect;
    public bool followTargetWhenVisible = true;
    public float followTargetSpacing = 20f;

    [Header("动画")]
    public float fadeDuration = 0.3f;
    public float expandDuration = 0.25f;

    // 内部状态
    private Coroutine mFadeCoroutine;
    private Coroutine mResizeCoroutine;
    private bool mIsVisible = false;
    private float mCollapsedPanelHeight;
    private float mCollapsedListHeight;
    private float mListTopInset;
    private float mPanelBottomPadding;
    private string mInspectorCurrentTopicKey;
    private readonly Dictionary<string, List<string>> mGeneratedQuestions = new Dictionary<string, List<string>>();
    private readonly Dictionary<string, string> mTopicDropdownLabels = new Dictionary<string, string>();
    private readonly Vector3[] mWorldCorners = new Vector3[4];
    private readonly Vector3[] mFollowTargetWorldCorners = new Vector3[4];
    private bool mSuppressDropdownCallback;

    /// <summary> 用户点击问题后的回调（传给 DialogSystem 开始占卜） </summary>
    public event Action<string> OnQuestionSelected;
    public bool IsVisible => mIsVisible;

    #region 生命周期

    private void Awake()
    {
        // 确保 CanvasGroup 存在
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();


        mTopicOrder = new List<string> { "self", "love", "work", "social" };

        // 列表初始化 —— 必须在 Awake 完成，因为 OnEnable 就会调 RefreshAll → RefreshQuestionList
        if (loopListView2 != null)
        {
            loopListView2.InitListView(0, OnGetItemByIndex);
        }

        if (contentRoot == null)
            contentRoot = transform as RectTransform;
        if (questionListRect == null && loopListView2 != null)
            questionListRect = loopListView2.GetComponent<RectTransform>();

        ResolveExitButton();
        ResolveTopicDropdown();
        BindExitButton();
        BindTopicDropdown();
        CacheCollapsedHeights();
    }

    private void Start()
    {
        // 为每个话题按钮绑定点击
        foreach (var kv in mTopicButtons)
        {
            if (kv.Value == null) continue;
            string topicKey = kv.Key;  // 闭包捕获
            kv.Value.onClick.RemoveAllListeners();
            kv.Value.onClick.AddListener(() => OnTopicButtonClick(topicKey));
        }

        // 展开/收起按钮
        if (toggleBtn != null)
        {
            toggleBtn.onClick.RemoveAllListeners();
            toggleBtn.onClick.AddListener(OnToggleClick);
        }

        // 订阅角色切换事件 → 刷新题库
        BindTopicDropdown();
        EventSystem.AddEventListener<int>(GameDataStr.UpdateRoleInfo, OnRoleChanged);

        // 初始隐藏
        SetVisible(false, instant: true);
    }

    private void OnDestroy()
    {
        Canvas.willRenderCanvases -= OnWillRenderCanvases;
        EventSystem.RemoveEventListener<int>(GameDataStr.UpdateRoleInfo, OnRoleChanged);
        if (topicDropdown != null)
            topicDropdown.onValueChanged.RemoveListener(OnTopicDropdownValueChanged);
        if (exitBtn != null)
            exitBtn.onClick.RemoveListener(OnExitButtonClick);

        if (mResizeCoroutine != null)
        {
            StopCoroutine(mResizeCoroutine);
            mResizeCoroutine = null;
        }
    }

    private void OnEnable()
    {
        Canvas.willRenderCanvases -= OnWillRenderCanvases;
        Canvas.willRenderCanvases += OnWillRenderCanvases;
        RefreshAll();
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= OnWillRenderCanvases;
    }

    private void OnWillRenderCanvases()
    {
        if (mIsVisible)
            AlignAboveFollowTarget();
    }

    #endregion

    #region 显示/隐藏

    public void ShowPanel()
    {
        SetVisible(true);
        RefreshAll();
    }

    public void HidePanel()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible, bool instant = false)
    {
        mIsVisible = visible;

        if (mFadeCoroutine != null)
            StopCoroutine(mFadeCoroutine);

        if (instant)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        else
        {
            mFadeCoroutine = StartCoroutine(FadeCoroutine(visible));
        }
    }

    private IEnumerator FadeCoroutine(bool show)
    {
        float startAlpha = canvasGroup.alpha;
        float targetAlpha = show ? 1f : 0f;
        float elapsed = 0f;

        canvasGroup.interactable = show;
        canvasGroup.blocksRaycasts = show;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    #endregion

    #region 角色切换

    /// <summary> 响应神谕师切换，重新加载题库 </summary>
    private void OnRoleChanged(int newRoleId)
    {
        Debug.Log($"[QuickDivinationPanel] 角色切换为: {(CharacterType)newRoleId}");
        var data = QuickDivinationData.Instance;
        data.RefreshForCharacter((CharacterType)newRoleId);
        RefreshAll();
    }

    #endregion

    #region 展开/收起

    public void SetQuestionDisplayCounts(int collapsedCount, int expandedCount)
    {
        collapsedQuestionCount = Mathf.Max(0, collapsedCount);
        expandedQuestionCount = Mathf.Max(collapsedQuestionCount, expandedCount);
        scrollableQuestionCount = Mathf.Max(scrollableQuestionCount, expandedQuestionCount + 1);
        RefreshQuestionList();
        UpdateToggleButtonText();
    }

    public void SetGeneratedQuestions(string topicKey, List<string> questions)
    {
        if (string.IsNullOrEmpty(topicKey)) return;

        if (questions == null || questions.Count == 0)
            mGeneratedQuestions.Remove(topicKey);
        else
            mGeneratedQuestions[topicKey] = SanitizeQuestions(questions);

        UpdateToggleButtonText();
        RefreshQuestionList();
    }

    public void SetGeneratedTopics(List<QuickTopic> topics)
    {
        mGeneratedQuestions.Clear();
        if (topics != null)
        {
            foreach (var topic in topics)
            {
                if (topic == null || string.IsNullOrEmpty(topic.key)) continue;
                mGeneratedQuestions[topic.key] = SanitizeQuestions(topic.questions);
            }
        }

        RefreshAll();
    }

    public void ClearGeneratedQuestions()
    {
        mGeneratedQuestions.Clear();
        UpdateToggleButtonText();
        RefreshQuestionList();
    }

    private void ExpandContent()
    {
        QuickDivinationData.Instance.IsExpanded = true;
        RefreshAll();
    }

    private void HideContent()
    {
        QuickDivinationData.Instance.IsExpanded = false;
        RefreshAll();
    }

    private void OnToggleClick()
    {
        var data = QuickDivinationData.Instance;
        if (data.IsExpanded)
            HideContent();
        else
            ExpandContent();
    }

    private void OnExitButtonClick()
    {
        HidePanel();
    }

    #endregion

    #region 话题切换

    private void OnTopicButtonClick(string topicKey)
    {
        if (UseInspectorTopics())
            mInspectorCurrentTopicKey = topicKey;
        else
            QuickDivinationData.Instance.SwitchTopic(topicKey);

        UpdateTopicButtonStates();
        SyncTopicDropdownSelection();
        UpdateToggleButtonText();
        RefreshQuestionList(true);
    }

    private void OnTopicDropdownValueChanged(int optionIndex)
    {
        if (mSuppressDropdownCallback)
            return;

        if (optionIndex < 0 || optionIndex >= mTopicOrder.Count)
            return;

        OnTopicButtonClick(mTopicOrder[optionIndex]);
    }

    #endregion

    #region LoopListView 回调

    LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
    {
        var questions = GetListQuestions();
        if (index < 0 || index >= questions.Count)
        {
            return null;
        }

        LoopListViewItem2 item = listView.NewListViewItem("QuestionItem");
        QuestionItem itemScript = item.GetComponent<QuestionItem>();

        // 只初始化一次
        if (!item.IsInitHandlerCalled)
        {
            item.IsInitHandlerCalled = true;
            itemScript.Init();
        }

        string question = questions[index];
        itemScript.SetItemData(question, () => OnQuestionClick(question));
        itemScript.RefreshLayout();
        listView.OnItemSizeChanged(item.ItemIndex);

        return item;
    }

    #endregion

    #region 问题点击 → 触发占卜

    private void OnQuestionClick(string question)
    {
        Debug.Log($"[QuickDivinationPanel] 选中问题: {question}");

        // 1. 通知外部订阅者
        OnQuestionSelected?.Invoke(question);

        // 2. 通过事件系统通知 DialogUI 处理消息
        EventSystem.DispatchEvent(GameDataStr.QuickQuestionSelected, question);

        // 3. 关闭面板
        HidePanel();
    }

    #endregion

    #region UI 刷新

    /// <summary> 全量刷新 </summary>
    private void RefreshAll()
    {
        // 话题按钮状态
        UpdateTopicButtonLabels();
        UpdateTopicButtonStates();

        // 收起/展开按钮文字
        UpdateToggleButtonText();

        // 问题列表
        RefreshQuestionList();
    }

    /// <summary> 根据云端 config 动态更新按钮文字 </summary>
    private void UpdateTopicButtonLabels()
    {
        var allTopics = GetSourceTopics();
        if (allTopics == null || allTopics.Count == 0)
        {
            mTopicButtons.Clear();
            mTopicOrder.Clear();
            RefreshTopicDropdownOptions(allTopics);
            return;
        }
        EnsureCurrentTopicKey(allTopics);

        // 先清空旧的映射，按 config 中的话题重新绑定
        // （占星师/冥想师的关键词和塔罗师不同，需要动态更新）
        mTopicButtons.Clear();
        mTopicOrder.Clear();

       
        RefreshTopicDropdownOptions(allTopics);
    }



    /// <summary> 更新话题按钮高亮状态 </summary>
    private void UpdateTopicButtonStates()
    {
        string current = GetCurrentTopicKey();

        foreach (var kv in mTopicButtons)
        {
            if (kv.Value == null) continue;

            bool isActive = (kv.Key == current);
            var colors = kv.Value.colors;

            // 选中态：绿色底 + 白色字；未选中：透明底
            colors.normalColor = isActive
                ? new Color(0.18f, 0.55f, 0.34f, 1f)   // 墨绿色（选中）
                : new Color(0.12f, 0.12f, 0.14f, 0.6f); // 暗灰（未选中）

            kv.Value.colors = colors;

            // 子文字颜色
            var label = kv.Value.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.color = isActive ? Color.white : new Color(0.8f, 0.8f, 0.85f);
        }

        SyncTopicDropdownSelection();
    }

    private void ResolveTopicDropdown()
    {
        if (topicDropdown == null)
            topicDropdown = GetComponentInChildren<TMP_Dropdown>(true);
    }

    private void ResolveExitButton()
    {
        if (exitBtn != null)
            return;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button != null && string.Equals(button.name, "exitBtn", StringComparison.OrdinalIgnoreCase))
            {
                exitBtn = button;
                return;
            }
        }
    }

    private void BindExitButton()
    {
        ResolveExitButton();
        if (exitBtn == null)
            return;

        exitBtn.onClick.RemoveListener(OnExitButtonClick);
        exitBtn.onClick.AddListener(OnExitButtonClick);
    }

    private void BindTopicDropdown()
    {
        ResolveTopicDropdown();
        if (topicDropdown == null)
            return;

        topicDropdown.onValueChanged.RemoveListener(OnTopicDropdownValueChanged);
        topicDropdown.onValueChanged.AddListener(OnTopicDropdownValueChanged);
        EnableTopicDropdownRichText();
    }

    private void RefreshTopicDropdownOptions(List<QuickTopic> topics)
    {
        ResolveTopicDropdown();
        if (topicDropdown == null)
            return;

        mSuppressDropdownCallback = true;
        topicDropdown.ClearOptions();
        mTopicOrder.Clear();
        mTopicDropdownLabels.Clear();

        if (topics != null && topics.Count > 0)
        {
            List<string> labels = new List<string>(topics.Count);
            foreach (QuickTopic topic in topics)
            {
                if (topic == null || string.IsNullOrEmpty(topic.key))
                    continue;

                mTopicOrder.Add(topic.key);
                string label = GetTopicDropdownLabel(topic);
                mTopicDropdownLabels[topic.key] = label;
                labels.Add(label);
            }

            topicDropdown.AddOptions(labels);
        }

        mSuppressDropdownCallback = false;
        SyncTopicDropdownSelection();
    }

    private void SyncTopicDropdownSelection()
    {
        ResolveTopicDropdown();
        if (topicDropdown == null || mTopicOrder.Count == 0)
            return;

        int index = Mathf.Max(0, mTopicOrder.IndexOf(GetCurrentTopicKey()));
        mSuppressDropdownCallback = true;
        topicDropdown.SetValueWithoutNotify(index);
        ApplyTopicDropdownSelectedTextColor();
        topicDropdown.RefreshShownValue();
        mSuppressDropdownCallback = false;
    }

    private void EnableTopicDropdownRichText()
    {
        if (topicDropdown == null)
            return;

        if (topicDropdown.captionText != null)
            topicDropdown.captionText.richText = true;
        if (topicDropdown.itemText != null)
            topicDropdown.itemText.richText = true;
    }

    private void ApplyTopicDropdownSelectedTextColor()
    {
        ResolveTopicDropdown();
        if (topicDropdown == null)
            return;

        EnableTopicDropdownRichText();

        string currentKey = GetCurrentTopicKey();
        int count = Mathf.Min(topicDropdown.options.Count, mTopicOrder.Count);
        for (int i = 0; i < count; i++)
        {
            string key = mTopicOrder[i];
            string label = mTopicDropdownLabels.TryGetValue(key, out string cachedLabel)
                ? cachedLabel
                : StripTopicDropdownColor(topicDropdown.options[i].text);

            topicDropdown.options[i].text = key == currentKey
                ? $"<color={SelectedTopicDropdownTextColor}>{label}</color>"
                : label;
        }
    }

    private static string StripTopicDropdownColor(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace($"<color={SelectedTopicDropdownTextColor}>", string.Empty)
            .Replace("</color>", string.Empty);
    }

    private static string GetTopicDropdownLabel(QuickTopic topic)
    {
        if (topic == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(topic.label))
            return topic.label.Trim();

        return topic.key ?? string.Empty;
    }

    private void UpdateToggleButtonText()
    {
        int visibleLimit = GetVisibleQuestionCapacity();
        int totalCount = GetListQuestions().Count;
        int visibleCount = Mathf.Min(visibleLimit, totalCount);
        bool isExpanded = QuickDivinationData.Instance.IsExpanded;

        if (toggleBtnText != null)
        {
            toggleBtnText.text = isExpanded
                ? "收起"
                : "展开";
        }

        UpdateToggleArrowIcon(isExpanded);

        if (toggleBtn != null)
            toggleBtn.gameObject.SetActive(totalCount > visibleCount || isExpanded);
    }

    private void UpdateToggleArrowIcon(bool isExpanded)
    {
        ResolveToggleArrowIcon();
        if (toggleArrowIcon == null)
            return;

        toggleArrowIcon.localRotation = Quaternion.Euler(0f, 0f, isExpanded ? 180f : 0f);
    }

    private void ResolveToggleArrowIcon()
    {
        if (toggleArrowIcon != null || toggleBtn == null)
            return;

        Transform directIcon = toggleBtn.transform.Find("icon");
        if (directIcon != null)
        {
            toggleArrowIcon = directIcon as RectTransform;
            return;
        }

        RectTransform[] children = toggleBtn.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform child in children)
        {
            if (child != null && string.Equals(child.name, "icon", StringComparison.OrdinalIgnoreCase))
            {
                toggleArrowIcon = child;
                return;
            }
        }
    }

    private void RefreshQuestionList(bool resetScrollPosition = false)
    {
        if (loopListView2 == null) return;
        if (loopListView2.ScrollRect == null) return; // InitListView 尚未完成，跳过

        var questions = GetListQuestions();
        ApplyContentHeight(questions);
        ConfigureQuestionListScroll(questions);
        loopListView2.SetListItemCount(questions.Count, resetScrollPosition);
        if (resetScrollPosition && questions.Count > 0)
            loopListView2.MovePanelToItemIndex(0, 0);
        loopListView2.RefreshAllShownItem();
    }

    private List<string> GetListQuestions()
    {
        return GetCurrentSourceQuestions(ResolveQuestionDataCount());
    }

    private int GetVisibleQuestionCapacity()
    {
        return QuickDivinationData.Instance.IsExpanded
            ? Mathf.Max(1, expandedQuestionCount)
            : collapsedQuestionCount;
    }

    private int ResolveQuestionDataCount()
    {
        int visibleCount = Mathf.Max(collapsedQuestionCount, expandedQuestionCount);
        int scrollCount = Mathf.Max(visibleCount + 1, scrollableQuestionCount);
        return Mathf.Max(0, scrollCount);
    }

    private List<string> GetCurrentSourceQuestions(int requiredCount)
    {
        var topic = GetCurrentSourceTopic();
        if (IsThreeCardTopic(topic))
            return new List<string> { ThreeCardQuickQuestion };

        var generated = GetGeneratedQuestions(topic, requiredCount);
        if (generated.Count > 0)
            return generated;

        if (topic?.questions != null && topic.questions.Count > 0)
            return EnsureEnoughQuestions(topic.questions, topic, requiredCount);

        return EnsureEnoughQuestions(QuickDivinationData.Instance.GetCurrentQuestions(), topic, requiredCount);
    }

    private bool IsThreeCardTopic(QuickTopic topic)
    {
        if (topic == null) return false;

        string key = topic.key ?? "";
        string label = topic.label ?? "";
        string value = $"{key} {label}".ToLowerInvariant();

        return value.Contains("three")
            || value.Contains("3")
            || value.Contains("三牌")
            || value.Contains("三排")
            || value.Contains("三张");
    }

    private List<string> GetGeneratedQuestions(QuickTopic topic, int requiredCount)
    {
        string topicKey = topic?.key ?? GetCurrentTopicKey();
        if (!string.IsNullOrEmpty(topicKey)
            && mGeneratedQuestions.TryGetValue(topicKey, out var cached)
            && cached != null
            && cached.Count > 0)
        {
            return EnsureEnoughQuestions(cached, topic, requiredCount);
        }

        if (QuestionProvider == null) return new List<string>();

        var request = new QuickQuestionRequest
        {
            topicKey = topicKey,
            topicLabel = topic?.label ?? topicKey,
            oracleId = GetCurrentOracleId(),
            characterType = RoleManager.Instance != null
                ? RoleManager.Instance.characterType
                : CharacterType.TarotReader,
            requiredCount = requiredCount,
            isExpanded = QuickDivinationData.Instance.IsExpanded
        };

        List<string> provided;
        try
        {
            provided = SanitizeQuestions(QuestionProvider.Invoke(request));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[QuickDivinationPanel] 生成快速问题失败，使用默认问题: {e.Message}");
            return new List<string>();
        }

        if (provided.Count == 0)
            return new List<string>();

        if (!string.IsNullOrEmpty(topicKey))
            mGeneratedQuestions[topicKey] = provided;

        return EnsureEnoughQuestions(provided, topic, requiredCount);
    }

    private List<string> EnsureEnoughQuestions(List<string> source, QuickTopic topic, int requiredCount)
    {
        var result = SanitizeQuestions(source);
        var fallback = BuildFallbackQuestions(topic);

        for (int i = 0; result.Count < requiredCount && i < fallback.Count; i++)
        {
            if (!result.Contains(fallback[i]))
                result.Add(fallback[i]);
        }

        return result;
    }

    private static List<string> SanitizeQuestions(List<string> questions)
    {
        var result = new List<string>();
        if (questions == null) return result;

        foreach (var question in questions)
        {
            if (string.IsNullOrWhiteSpace(question)) continue;
            var value = question.Trim();
            if (!result.Contains(value))
                result.Add(value);
        }

        return result;
    }

    private List<string> BuildFallbackQuestions(QuickTopic topic)
    {
        var label = topic?.label ?? "这个主题";
        return new List<string>
        {
            $"我现在最需要看清的{label}问题是什么？",
            $"这件事背后真正影响我的是什么？",
            $"我接下来可以先做哪一个小行动？",
            $"我需要放下哪种反复消耗自己的想法？",
            $"如果换一个角度看，这件事在提醒我什么？",
            $"我该如何更稳定地面对这个局面？",
            $"这件事未来一段时间可能怎样发展？",
            $"我现在最不该忽略的信号是什么？"
        };
    }

    private QuickTopic GetCurrentSourceTopic()
    {
        var topics = GetSourceTopics();
        EnsureCurrentTopicKey(topics);

        string current = GetCurrentTopicKey();
        var topic = topics.Find(t => t.key == current);
        if (topic != null) return topic;

        if (topics.Count > 0)
        {
            if (UseInspectorTopics())
                mInspectorCurrentTopicKey = topics[0].key;
            else
                QuickDivinationData.Instance.SwitchTopic(topics[0].key);
            return topics[0];
        }

        return null;
    }

    private List<QuickTopic> GetSourceTopics()
    {
        if (UseInspectorTopics())
            return inspectorTopics;

        return QuickDivinationData.Instance.Config?.topics ?? new List<QuickTopic>();
    }

    private bool UseInspectorTopics()
    {
        return inspectorTopics != null && inspectorTopics.Count > 0;
    }

    private string GetCurrentTopicKey()
    {
        return UseInspectorTopics()
            ? mInspectorCurrentTopicKey
            : QuickDivinationData.Instance.CurrentTopicKey;
    }

    private void EnsureCurrentTopicKey(List<QuickTopic> topics)
    {
        if (!UseInspectorTopics() || topics == null || topics.Count == 0)
            return;

        if (string.IsNullOrEmpty(mInspectorCurrentTopicKey)
            || !topics.Exists(t => t.key == mInspectorCurrentTopicKey))
        {
            mInspectorCurrentTopicKey = topics[0].key;
        }
    }

    private void ApplyContentHeight(List<string> visibleQuestions)
    {
        ResolveLayoutRects();

        if (questionListRect == null && loopListView2 != null)
            questionListRect = loopListView2.GetComponent<RectTransform>();
        if (contentRoot == null)
            contentRoot = transform as RectTransform;

        ArrangeStaticSections();

        float panelLimit = ResolveMaxPanelHeight();
        float listLimit = ResolveMaxQuestionListHeight(panelLimit);
        float targetListHeight = ResolveWholeItemListHeight(visibleQuestions, listLimit);

        float targetPanelHeight = Mathf.Max(minPanelHeight, mListTopInset + targetListHeight + mPanelBottomPadding);
        if (contentRoot != null && questionListRect != null)
        {
            targetPanelHeight = Mathf.Min(targetPanelHeight, panelLimit);
        }

        if (mResizeCoroutine != null)
            StopCoroutine(mResizeCoroutine);

        if (expandDuration <= 0f || !gameObject.activeInHierarchy)
        {
            SetContentHeights(targetPanelHeight, targetListHeight);
            return;
        }

        mResizeCoroutine = StartCoroutine(ResizeCoroutine(targetPanelHeight, targetListHeight));
    }

    private IEnumerator ResizeCoroutine(float targetPanelHeight, float targetListHeight)
    {
        float startPanelHeight = contentRoot != null ? contentRoot.sizeDelta.y : targetPanelHeight;
        float startListHeight = questionListRect != null ? questionListRect.sizeDelta.y : targetListHeight;
        float elapsed = 0f;

        while (elapsed < expandDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / expandDuration);
            SetContentHeights(
                Mathf.Lerp(startPanelHeight, targetPanelHeight, t),
                Mathf.Lerp(startListHeight, targetListHeight, t));
            yield return null;
        }

        SetContentHeights(targetPanelHeight, targetListHeight);
        mResizeCoroutine = null;
    }

    private void SetContentHeights(float panelHeight, float listHeight)
    {
        float oldPanelHeight = contentRoot != null ? contentRoot.sizeDelta.y : panelHeight;

        if (questionListRect != null)
        {
            questionListRect.anchorMin = new Vector2(0f, 1f);
            questionListRect.anchorMax = new Vector2(1f, 1f);
            questionListRect.pivot = new Vector2(0.5f, 1f);

            var anchoredPosition = questionListRect.anchoredPosition;
            anchoredPosition.y = -mListTopInset;
            questionListRect.anchoredPosition = anchoredPosition;

            var size = questionListRect.sizeDelta;
            size.y = listHeight;
            questionListRect.sizeDelta = size;
        }

        if (contentRoot != null)
        {
            var size = contentRoot.sizeDelta;
            size.y = panelHeight;
            contentRoot.sizeDelta = size;

            // 默认保持底部不动，让收起态贴近输入框；必要时可改成顶部固定。
            if (!AlignAboveFollowTarget(false))
            {
                var anchoredPosition = contentRoot.anchoredPosition;
                float deltaHeight = panelHeight - oldPanelHeight;
                anchoredPosition.y += keepPanelBottomFixed
                    ? deltaHeight * contentRoot.pivot.y
                    : -deltaHeight * (1f - contentRoot.pivot.y);
                contentRoot.anchoredPosition = anchoredPosition;
            }
        }

        if (loopListView2 != null)
            loopListView2.UpdateAllShownItemSnapData();

        KeepPanelInsideChatBounds();
    }

    private float ResolveMaxPanelHeight()
    {
        float boundsLimit = ResolveChatBoundsHeightLimit();
        float followLimit = ResolveFollowTargetHeightLimit();
        float preferredHeight = maxPanelHeight > 0f
            ? Mathf.Max(mCollapsedPanelHeight, maxPanelHeight)
            : mCollapsedPanelHeight;

        float resolvedHeight = preferredHeight;
        if (boundsLimit > 0f)
            resolvedHeight = Mathf.Min(resolvedHeight, boundsLimit);
        if (followLimit > 0f)
            resolvedHeight = Mathf.Min(resolvedHeight, followLimit);

        if (maxPanelHeight > 0f)
            return Mathf.Max(minPanelHeight, resolvedHeight);

        return Mathf.Max(minPanelHeight, resolvedHeight);
    }

    private float ResolveMaxQuestionListHeight(float panelLimit)
    {
        if (maxQuestionListHeight > 0f)
            return Mathf.Max(minQuestionListHeight, maxQuestionListHeight);

        return Mathf.Max(minQuestionListHeight, panelLimit - mListTopInset - mPanelBottomPadding);
    }

    private float ResolveWholeItemListHeight(List<string> questions, float listLimit)
    {
        float safeLimit = Mathf.Max(0f, listLimit);
        if (questions == null || questions.Count == 0)
            return Mathf.Min(GetQuestionListVerticalPadding(), safeLimit);

        int maxItems = Mathf.Min(questions.Count, Mathf.Max(0, GetVisibleQuestionCapacity()));
        if (maxItems <= 0)
            return 0f;

        float preferredHeight = EstimateQuestionListHeight(questions, maxItems);
        if (preferredHeight <= safeLimit + 0.5f)
            return Mathf.Max(0f, preferredHeight);

        float itemPadding = GetQuestionItemPadding();
        float fittedHeight = GetQuestionListVerticalPadding();
        int fittedCount = 0;

        for (int i = 0; i < maxItems; i++)
        {
            float itemHeight = EstimateQuestionItemHeight(questions[i]);
            float nextHeight = fittedHeight
                + (fittedCount > 0 ? itemPadding : 0f)
                + itemHeight;

            if (nextHeight > safeLimit + 0.5f)
                break;

            fittedHeight = nextHeight;
            fittedCount++;
        }

        if (fittedCount > 0)
            return Mathf.Max(0f, fittedHeight);

        return Mathf.Min(safeLimit, EstimateQuestionListHeight(questions, 1));
    }

    private void ConfigureQuestionListScroll(List<string> questions)
    {
        if (loopListView2 == null || loopListView2.ScrollRect == null)
            return;

        ScrollRect scrollRect = loopListView2.ScrollRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = questions != null && questions.Count > 0;
        if (scrollRect.movementType == ScrollRect.MovementType.Unrestricted)
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
    }

    private float EstimateQuestionListHeight(List<string> visibleQuestions, int maxItems = int.MaxValue)
    {
        if (visibleQuestions == null || visibleQuestions.Count == 0)
            return GetQuestionListVerticalPadding();

        float itemPadding = GetQuestionItemPadding();
        float totalHeight = GetQuestionListVerticalPadding();
        int count = Mathf.Min(visibleQuestions.Count, Mathf.Max(0, maxItems));
        for (int i = 0; i < count; i++)
        {
            totalHeight += EstimateQuestionItemHeight(visibleQuestions[i]);
        }

        totalHeight += itemPadding * Mathf.Max(0, count - 1);
        return totalHeight;
    }

    private float EstimateQuestionItemHeight(string question)
    {
        return QuestionItem.EstimatePreferredHeight(
            question,
            questionListRect,
            questionItemHeight);
    }

    private float GetQuestionListTopPadding()
    {
        return questionListTopPadding > 0f
            ? questionListTopPadding
            : Mathf.Max(0f, questionListPadding * 0.5f);
    }

    private float GetQuestionListBottomPadding()
    {
        return questionListBottomPadding > 0f
            ? questionListBottomPadding
            : Mathf.Max(0f, questionListPadding);
    }

    private float GetQuestionListVerticalPadding()
    {
        return GetQuestionListTopPadding() + GetQuestionListBottomPadding();
    }

    private float GetQuestionItemPadding()
    {
        if (loopListView2 == null)
            return 0f;

        LoopListViewItem2 shownItem = loopListView2.GetShownItemByItemIndex(0);
        if (shownItem != null)
            return Mathf.Max(0f, shownItem.Padding);

        return GetConfiguredQuestionItemPadding();
    }

    private float GetConfiguredQuestionItemPadding()
    {
        if (loopListView2 == null || loopListView2.ItemPrefabDataList == null)
            return 0f;

        foreach (ItemPrefabConfData data in loopListView2.ItemPrefabDataList)
        {
            if (data == null || data.mItemPrefab == null)
                continue;

            if (data.mItemPrefab.name == "QuestionItem")
                return Mathf.Max(0f, data.mPadding);
        }

        return loopListView2.ItemPrefabDataList.Count > 0
            ? Mathf.Max(0f, loopListView2.ItemPrefabDataList[0].mPadding)
            : 0f;
    }

    private float ResolveChatBoundsHeightLimit()
    {
        ResolveChatBoundsRect();
        if (!clampInsideChatBounds || chatBoundsRect == null)
            return 0f;

        return Mathf.Max(0f, chatBoundsRect.rect.height - Mathf.Max(0f, chatBoundsPadding) * 2f);
    }

    private float ResolveFollowTargetHeightLimit()
    {
        if (!followTargetWhenVisible || contentRoot == null)
            return 0f;

        ResolveFollowTargetRect();
        if (followTargetRect == null)
            return 0f;

        RectTransform parentRect = contentRoot.parent as RectTransform;
        if (parentRect == null)
            return 0f;

        followTargetRect.GetWorldCorners(mFollowTargetWorldCorners);
        float targetTopY = float.MinValue;
        for (int i = 0; i < mFollowTargetWorldCorners.Length; i++)
        {
            float localY = parentRect.InverseTransformPoint(mFollowTargetWorldCorners[i]).y;
            if (localY > targetTopY)
                targetTopY = localY;
        }

        float boundsTopY = parentRect.rect.yMax;
        ResolveChatBoundsRect();
        if (chatBoundsRect != null)
        {
            chatBoundsRect.GetWorldCorners(mWorldCorners);
            boundsTopY = float.MinValue;
            for (int i = 0; i < mWorldCorners.Length; i++)
            {
                float localY = parentRect.InverseTransformPoint(mWorldCorners[i]).y;
                if (localY > boundsTopY)
                    boundsTopY = localY;
            }
        }

        float padding = clampInsideChatBounds ? Mathf.Max(0f, chatBoundsPadding) : 0f;
        return Mathf.Max(0f, boundsTopY - padding - targetTopY - Mathf.Max(0f, followTargetSpacing));
    }

    private void ResolveChatBoundsRect()
    {
        if (chatBoundsRect == null && contentRoot != null)
            chatBoundsRect = contentRoot.parent as RectTransform;
    }

    private void ResolveFollowTargetRect()
    {
        if (followTargetRect != null)
            return;

        ResolveChatBoundsRect();
        RectTransform searchRoot = chatBoundsRect != null
            ? chatBoundsRect
            : contentRoot != null ? contentRoot.parent as RectTransform : null;

        followTargetRect = FindChildRectTransform(searchRoot, "FooterInputPanel");
        if (followTargetRect == null)
            followTargetRect = FindChildRectTransform(searchRoot, "InputBg");
    }

    private bool AlignAboveFollowTarget(bool clampToBounds = true)
    {
        if (!followTargetWhenVisible || contentRoot == null)
            return false;

        ResolveFollowTargetRect();
        if (followTargetRect == null)
            return false;

        RectTransform parentRect = contentRoot.parent as RectTransform;
        if (parentRect == null)
            return false;

        followTargetRect.GetWorldCorners(mFollowTargetWorldCorners);
        float targetTopY = float.MinValue;
        for (int i = 0; i < mFollowTargetWorldCorners.Length; i++)
        {
            float localY = parentRect.InverseTransformPoint(mFollowTargetWorldCorners[i]).y;
            if (localY > targetTopY)
                targetTopY = localY;
        }

        contentRoot.GetWorldCorners(mWorldCorners);
        float panelBottomY = float.MaxValue;
        for (int i = 0; i < mWorldCorners.Length; i++)
        {
            float localY = parentRect.InverseTransformPoint(mWorldCorners[i]).y;
            if (localY < panelBottomY)
                panelBottomY = localY;
        }

        float deltaY = targetTopY + Mathf.Max(0f, followTargetSpacing) - panelBottomY;
        if (Mathf.Abs(deltaY) > 0.01f)
        {
            var anchoredPosition = contentRoot.anchoredPosition;
            anchoredPosition.y += deltaY;
            contentRoot.anchoredPosition = anchoredPosition;
        }

        if (clampToBounds)
            KeepPanelInsideChatBounds();

        return true;
    }

    private void KeepPanelInsideChatBounds()
    {
        if (!clampInsideChatBounds || contentRoot == null)
            return;

        ResolveChatBoundsRect();
        if (chatBoundsRect == null)
            return;

        Rect rect = chatBoundsRect.rect;
        float padding = Mathf.Max(0f, chatBoundsPadding);
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        contentRoot.GetWorldCorners(mWorldCorners);
        for (int i = 0; i < mWorldCorners.Length; i++)
        {
            Vector3 localPoint = chatBoundsRect.InverseTransformPoint(mWorldCorners[i]);
            minX = Mathf.Min(minX, localPoint.x);
            maxX = Mathf.Max(maxX, localPoint.x);
            minY = Mathf.Min(minY, localPoint.y);
            maxY = Mathf.Max(maxY, localPoint.y);
        }

        Vector2 offset = Vector2.zero;

        if (minX < rect.xMin + padding)
            offset.x += rect.xMin + padding - minX;
        if (maxX > rect.xMax - padding)
            offset.x -= maxX - (rect.xMax - padding);
        if (minY < rect.yMin + padding)
            offset.y += rect.yMin + padding - minY;
        if (maxY > rect.yMax - padding)
            offset.y -= maxY - (rect.yMax - padding);

        if (offset.sqrMagnitude > 0.01f)
            contentRoot.anchoredPosition += offset;
    }

    private static RectTransform FindChildRectTransform(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        if (root.name == childName)
            return root as RectTransform;

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform child = FindChildRectTransform(root.GetChild(i), childName);
            if (child != null)
                return child;
        }

        return null;
    }

    private void ResolveLayoutRects()
    {
        if (loopListView2 != null && questionListRect == null)
            questionListRect = loopListView2.GetComponent<RectTransform>();

        if (contentRoot == null)
        {
            var rect = transform as RectTransform;
            contentRoot = rect;
        }

        ResolveTopicDropdown();

        if (headerRoot == null && toggleBtn != null)
        {
            var toggleTransform = toggleBtn.transform as RectTransform;
            headerRoot = toggleTransform?.parent as RectTransform;
        }


        if (topicRoot == null && topicDropdown != null)
            topicRoot = topicDropdown.transform as RectTransform;

        ResolveChatBoundsRect();
        ResolveFollowTargetRect();

        if (mCollapsedPanelHeight <= 0f || mCollapsedListHeight <= 0f)
            CacheCollapsedHeights();
    }

    private void CacheCollapsedHeights()
    {
        if (contentRoot != null)
            mCollapsedPanelHeight = Mathf.Max(minPanelHeight, contentRoot.sizeDelta.y);
        if (questionListRect != null)
            mCollapsedListHeight = Mathf.Max(minQuestionListHeight, questionListRect.sizeDelta.y);

        if (contentRoot != null && questionListRect != null)
        {
            if (autoLayoutSections)
                ArrangeStaticSections();
            else
            {
                var rootRect = contentRoot.rect;
                float listTop = GetChildLocalMaxY(questionListRect, contentRoot);
                float listBottom = GetChildLocalMinY(questionListRect, contentRoot);

                mListTopInset = Mathf.Max(0f, rootRect.yMax - listTop);
                mPanelBottomPadding = Mathf.Max(0f, listBottom - rootRect.yMin);
            }
        }
    }

    private void ArrangeStaticSections()
    {
        if (!autoLayoutSections || contentRoot == null) return;

        ResolveHeaderAndTopicRoots();

        float cursor = Mathf.Max(0f, layoutTopPadding);

        if (headerRoot != null)
        {
            PinSectionToTop(headerRoot, cursor);
            float layoutHeight = headerLayoutHeight > 0f ? headerLayoutHeight : headerRoot.rect.height;
            cursor += Mathf.Max(0f, layoutHeight) + headerTopicSpacing;
        }

        if (topicRoot != null)
        {
            PinSectionToTop(topicRoot, cursor);
            cursor += Mathf.Max(0f, topicRoot.rect.height) + Mathf.Max(0f, topicListSpacing);
        }

        mListTopInset = cursor;
        mPanelBottomPadding = Mathf.Max(0f, layoutBottomPadding);
    }

    private void ResolveHeaderAndTopicRoots()
    {
        ResolveTopicDropdown();

        if (headerRoot == null && toggleBtn != null)
        {
            var toggleTransform = toggleBtn.transform as RectTransform;
            headerRoot = toggleTransform?.parent as RectTransform;
        }


        if (topicRoot == null && topicDropdown != null)
            topicRoot = topicDropdown.transform as RectTransform;
    }

    private void PinSectionToTop(RectTransform section, float topInset)
    {
        if (section == null) return;

        section.anchorMin = new Vector2(0f, 1f);
        section.anchorMax = new Vector2(1f, 1f);
        section.pivot = new Vector2(0.5f, 1f);

        var anchoredPosition = section.anchoredPosition;
        anchoredPosition.y = -topInset;
        section.anchoredPosition = anchoredPosition;
    }

    private float GetChildLocalMaxY(RectTransform child, RectTransform parent)
    {
        child.GetWorldCorners(mWorldCorners);
        float maxY = float.MinValue;
        for (int i = 0; i < mWorldCorners.Length; i++)
        {
            float localY = parent.InverseTransformPoint(mWorldCorners[i]).y;
            if (localY > maxY) maxY = localY;
        }
        return maxY;
    }

    private float GetChildLocalMinY(RectTransform child, RectTransform parent)
    {
        child.GetWorldCorners(mWorldCorners);
        float minY = float.MaxValue;
        for (int i = 0; i < mWorldCorners.Length; i++)
        {
            float localY = parent.InverseTransformPoint(mWorldCorners[i]).y;
            if (localY < minY) minY = localY;
        }
        return minY;
    }

    private string GetCurrentOracleId()
    {
        if (RoleManager.Instance == null) return "tarot";

        return RoleManager.Instance.characterType switch
        {
            CharacterType.Astrologer => "astrology",
            CharacterType.Meditator => "sage",
            _ => "tarot",
        };
    }

    #endregion
}
