using System;
using System.Collections;
using System.Collections.Generic;
using GamerFrameWork;
using SuperScrollView;
using UnityEngine;
using UnityEngine.UI;
using XFGameFrameWork;

public class QuickDivinationPanel : MonoBehaviour
{
    [Header("面板容器")]
    public CanvasGroup canvasGroup;

    [Header("标题区域")]
    public Text introText;              // 上方引导文案
    public Button toggleBtn;            // 收起/展开按钮
    public Text toggleBtnText;          // 按钮文字（收起⌃ / 展开⌄）

    [Header("话题标签按钮 (4个)")]
    public Button selfBtn;
    public Button loveBtn;
    public Button workBtn;
    public Button socialBtn;

    // → 扩展：占星师/冥想师有不同标签，这里保留字典方便切换
    private Dictionary<string, Button> mTopicButtons =new();
    private List<string> mTopicOrder = new();   // 保持话题显示顺序

    [Header("问题列表")]
    public LoopListView2 loopListView2;

    [Header("动画")]
    public float fadeDuration = 0.3f;
    public float expandDuration = 0.25f;

    // 内部状态
    private Coroutine mFadeCoroutine;
    private bool mIsVisible = false;

    /// <summary> 用户点击问题后的回调（传给 DialogSystem 开始占卜） </summary>
    public event Action<string> OnQuestionSelected;

    #region 生命周期

    private void Awake()
    {
        // 确保 CanvasGroup 存在
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 初始化话题按钮映射（按 key 绑定）
        mTopicButtons = new Dictionary<string, Button>
        {
            { "self",   selfBtn   },
            { "love",   loveBtn   },
            { "work",   workBtn   },
            { "social", socialBtn },
        };
        mTopicOrder = new List<string> { "self", "love", "work", "social" };

        // 列表初始化 —— 必须在 Awake 完成，因为 OnEnable 就会调 RefreshAll → RefreshQuestionList
        if (loopListView2 != null)
        {
            loopListView2.InitListView(0, OnGetItemByIndex);
        }
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
        EventSystem.AddEventListener<int>(GameDataStr.UpdateRoleInfo, OnRoleChanged);

        // 初始隐藏
        SetVisible(false, instant: true);
    }

    private void OnDestroy()
    {
        EventSystem.RemoveEventListener<int>(GameDataStr.UpdateRoleInfo, OnRoleChanged);
    }

    private void OnEnable()
    {
        RefreshAll();
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

    #endregion

    #region 话题切换

    private void OnTopicButtonClick(string topicKey)
    {
        QuickDivinationData.Instance.SwitchTopic(topicKey);
        UpdateTopicButtonStates();
        RefreshQuestionList();
    }

    #endregion

    #region LoopListView 回调

    LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
    {
        var questions = QuickDivinationData.Instance.GetCurrentQuestions();
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
        var data = QuickDivinationData.Instance;

        // 介绍文案
        if (introText != null && data.Config != null)
        {
            introText.text = data.Config.intro;
        }

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
        var config = QuickDivinationData.Instance.Config;
        if (config == null) return;

        // 先清空旧的映射，按 config 中的话题重新绑定
        // （占星师/冥想师的关键词和塔罗师不同，需要动态更新）
        mTopicButtons.Clear();
        mTopicOrder.Clear();

        var allTopics = config.topics;
        for (int i = 0; i < allTopics.Count && i < 4; i++)
        {
            var topic = allTopics[i];
            mTopicOrder.Add(topic.key);

            // 找到对应的按钮（按顺序: selfBtn=0, loveBtn=1, workBtn=2, socialBtn=3）
            Button btn = GetTopicButtonByIndex(i);
            if (btn == null) continue;

            mTopicButtons[topic.key] = btn;

            // 更新按钮文字
            var label = btn.GetComponentInChildren<Text>();
            if (label != null) label.text = $"{topic.icon} {topic.label}";
        }
    }

    private Button GetTopicButtonByIndex(int index)
    {
        return index switch
        {
            0 => selfBtn,
            1 => loveBtn,
            2 => workBtn,
            3 => socialBtn,
            _ => null,
        };
    }

    /// <summary> 更新话题按钮高亮状态 </summary>
    private void UpdateTopicButtonStates()
    {
        string current = QuickDivinationData.Instance.CurrentTopicKey;

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
            var label = kv.Value.GetComponentInChildren<Text>();
            if (label != null)
                label.color = isActive ? Color.white : new Color(0.8f, 0.8f, 0.85f);
        }
    }

    private void UpdateToggleButtonText()
    {
        if (toggleBtnText != null)
        {
            toggleBtnText.text = QuickDivinationData.Instance.IsExpanded ? "收起⌃" : "展开⌄";
        }
    }

    private void RefreshQuestionList()
    {
        if (loopListView2 == null) return;
        if (loopListView2.ScrollRect == null) return; // InitListView 尚未完成，跳过

        var questions = QuickDivinationData.Instance.GetCurrentQuestions();
        loopListView2.SetListItemCount(questions.Count);
        loopListView2.RefreshAllShownItem();
    }

    #endregion
}
