/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/16/2026 6:03:04 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
 *
 * [修复记录 2026-06-16]
 * - BugFix: SetCard() 在窗口可见时自动刷新 UI
 * - BugFix: locale 恢复使用 DailyOracleService.CurrentLocale
 * - BugFix: OnSwitchOracleButtonClick 先设占位符再请求，消除竞态
 * - BugFix: 移除 OnAwake 中与 Component.InitComponent 重复的 sortingOrder 设置
 * - Improve: 添加加载态检查(_isLoading)，防止重复请求
 * - Improve: 添加超时自动降级（5秒后无响应走 fallback）
 * - Improve: 添加 Refresh() 公共方法供外部强制刷新
 * - Improve: 完善空引用保护
 * - Improve: GetTopicText 增加降级兜底
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using System.Collections;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork;
using System.Collections.Generic;
using GamerFrameWork.OracleRuntime;
using TMPro;

public class CompleteInterpretationUI : WindowBase
{
    public CompleteInterpretationUIComponent uiComponent;

    // 当前展示的卡牌数据
    private TarotCard _currentCard;
    private bool _currentUpright;

    // 加载与错误状态
    private bool _isLoading;
    private bool _hasError;
    private Coroutine _timeoutCoroutine;
    private Coroutine _layoutRefreshCoroutine;
    private bool _isPreparingTodayDetailContent;

    // 兜底时缓存的话题列表（供 GetTopicText 使用，避免依赖 CachedInterpretation）
    private List<string> _fallbackTopics;

    #region 生命周期函数
    public override void OnAwake()
    {
        uiComponent = gameObject.GetComponent<CompleteInterpretationUIComponent>();
        if (uiComponent == null)
        {
            Debug.LogError("[CompleteInterpretationUI] 未找到 CompleteInterpretationUIComponent，请检查预制体");
            base.OnAwake();
            return;
        }
        ResolveTopicComponentRefs();
        uiComponent.InitComponent(this);
        // sortingOrder 已在 InitComponent 中设置，此处不再重复
        base.OnAwake();
    }

    public override void OnShow()
    {
        base.OnShow();
        // 防止重复请求
        if (_isLoading) return;
        PopulateCardInfo();
        RequestAIInterpretation();
    }

    public override void OnHide()
    {
        base.OnHide();
        StopTimeoutCoroutine();
        StopLayoutRefreshCoroutine();
        _isPreparingTodayDetailContent = false;
    }

    public override void OnDestroy()
    {
        StopTimeoutCoroutine();
        StopLayoutRefreshCoroutine();
        _isPreparingTodayDetailContent = false;
        base.OnDestroy();
    }
    #endregion

    #region API Function

    /// <summary>
    /// 从外部设置卡牌数据（由 ReadingCardContainer 或其他界面打开时调用）。
    /// 如果窗口已可见则自动刷新 UI。
    /// </summary>
    public void SetCard(TarotCard card, bool upright)
    {
        _currentCard = card;
        _currentUpright = upright;
        _isLoading = false;
        _hasError = false;
        StopTimeoutCoroutine();

        // 窗口已可见 → 立即刷新
        if (Visible)
        {
            PopulateCardInfo();
            RequestAIInterpretation();
        }
    }

    /// <summary>
    /// 强制刷新当前卡牌的 UI 和 AI 内容（忽略缓存）
    /// </summary>
    public void Refresh()
    {
        if (_currentCard == null) return;
        _isLoading = false;
        _hasError = false;
        StopTimeoutCoroutine();

        DailyOracleService.Instance?.ClearCache();
        PopulateCardInfo();
        RequestAIInterpretation();
    }

    /// <summary>
    /// 当前是否正在加载中
    /// </summary>
    public bool IsContentReady => !_isLoading && !_hasError && _currentCard != null;

    #endregion

    #region 初始化与数据加载

    private void PopulateCardInfo()
    {
        // 优先使用外部设置的卡牌，其次从 DailyOracleService 或 DivinationEngine 获取
        if (_currentCard == null)
        {
            var oracleService = DailyOracleService.Instance;
            if (oracleService?.CurrentCard != null)
            {
                _currentCard = oracleService.CurrentCard;
                _currentUpright = oracleService.CurrentUpright;
            }
            else if (DivinationEngine.Instance != null)
            {
                var result = DivinationEngine.Instance.DrawDailyCard();
                _currentCard = result.card;
                _currentUpright = result.upright;
            }
        }

        if (_currentCard == null)
        {
            Debug.LogWarning("[CompleteInterpretationUI] 无卡牌数据，无法初始化界面");
            return;
        }

        // 卡牌名字
        if (uiComponent.CardNameTextText != null)
            uiComponent.CardNameTextText.text = _currentCard.DisplayName(_currentUpright);

        // 卡牌图片
        if (uiComponent.CardImageImage != null)
        {
            var sprite = TarotSpriteLoader.Load(_currentCard.cardId);
            if (sprite != null)
            {
                uiComponent.CardImageImage.sprite = sprite;
                // 逆位旋转 180°
                uiComponent.CardImageImage.rectTransform.localRotation = _currentUpright
                    ? Quaternion.identity
                    : Quaternion.Euler(0, 0, 180);
            }
            else
            {
                Debug.LogWarning($"[CompleteInterpretationUI] 无法加载卡牌图片: {_currentCard.cardId}");
            }
        }

        // 设置占位符，表示等待 AI 内容
        ShowLoadingPlaceholders();
        PopulateTodayDetailSections();
    }

    /// <summary>
    /// 显示加载中的占位符
    /// </summary>
    private void ShowLoadingPlaceholders()
    {
        SetDescriptionText("...");
        SetTagTexts(new List<string> { "...", "...", "..." });
        SetMeaningText("...");
        SetActionText("...");
        SetTopicTexts(new List<string> { "...", "...", "...", "..." });
        SetSuitableText("...");
        SetNotSuitableText("...");
        SetMoodReminderText("...");
        RequestLayoutRefresh();
    }

    private void RequestAIInterpretation()
    {
        if (_currentCard == null) return;
        if (_isLoading) return; // 防止重复请求

        var oracleService = DailyOracleService.Instance;
        if (oracleService == null)
        {
            Debug.LogWarning("[CompleteInterpretationUI] DailyOracleService 未就绪，使用降级内容");
            PopulateFallback();
            return;
        }

        if (oracleService.IsCachedPreparedReadingFor(_currentCard, _currentUpright)
            && oracleService.CachedPreparedReading?.interpretationPayload != null)
        {
            PopulateFromPayload(oracleService.CachedPreparedReading.interpretationPayload);
            return;
        }

        // 如果已有缓存且是同一张牌，直接使用
        if (oracleService.IsCachedInterpretationFor(_currentCard, _currentUpright))
        {
            PopulateFromPayload(oracleService.CachedInterpretation);
            return;
        }

        // 开始加载
        _isLoading = true;
        _hasError = false;
        ShowLoadingState(true);

        // 启动超时降级协程（5 秒后无响应自动走 fallback）
        _timeoutCoroutine = uiComponent.StartCoroutine(TimeoutFallbackRoutine(5f));

        // 请求 AI 生成
        oracleService.RequestCompleteInterpretation(_currentCard, _currentUpright,
            (payload) =>
            {
                _isLoading = false;
                StopTimeoutCoroutine();
                ShowLoadingState(false);

                if (payload != null)
                {
                    PopulateFromPayload(payload);
                }
                else
                {
                    // AI 返回空 payload，走降级
                    Debug.LogWarning("[CompleteInterpretationUI] AI 返回空结果，使用降级内容");
                    _hasError = true;
                    PopulateFallback();
                }
            });
    }

    /// <summary>
    /// 超时自动降级：指定秒数后若仍未收到回调则直接走 fallback
    /// </summary>
    private IEnumerator TimeoutFallbackRoutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (_isLoading)
        {
            Debug.LogWarning($"[CompleteInterpretationUI] AI 请求超时（{seconds}s），使用降级内容");
            _isLoading = false;
            _hasError = true;
            ShowLoadingState(false);
            PopulateFallback();
        }
    }

    private void StopTimeoutCoroutine()
    {
        if (_timeoutCoroutine != null)
        {
            uiComponent.StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }
    }

    private void StopLayoutRefreshCoroutine()
    {
        if (_layoutRefreshCoroutine != null && uiComponent != null)
        {
            uiComponent.StopCoroutine(_layoutRefreshCoroutine);
            _layoutRefreshCoroutine = null;
        }
    }

    private void RequestLayoutRefresh()
    {
        if (uiComponent == null) return;

        ForceDynamicTextMeshUpdate();
        ForceLayoutRefresh();

        if (!uiComponent.gameObject.activeInHierarchy) return;

        if (_layoutRefreshCoroutine != null)
            uiComponent.StopCoroutine(_layoutRefreshCoroutine);

        _layoutRefreshCoroutine = uiComponent.StartCoroutine(RefreshLayoutNextFrame());
    }

    private IEnumerator RefreshLayoutNextFrame()
    {
        yield return null;

        ForceDynamicTextMeshUpdate();
        ForceLayoutRefresh();
        _layoutRefreshCoroutine = null;
    }

    private void ForceDynamicTextMeshUpdate()
    {
        ForceTextMeshUpdate(uiComponent.CardNameTextText);
        ForceTextMeshUpdate(uiComponent.CardDescTextText);
        ForceTextMeshUpdate(uiComponent.Tag1TextText);
        ForceTextMeshUpdate(uiComponent.Tag2TextText);
        ForceTextMeshUpdate(uiComponent.Tag3TextText);
        ForceTextMeshUpdate(uiComponent.MeaningSectionText);
        ForceTextMeshUpdate(uiComponent.ActionSectionText);
        ForceTextMeshUpdate(uiComponent.suitableText);
        ForceTextMeshUpdate(uiComponent.notSuitableText);
        ForceTextMeshUpdate(uiComponent.moodReminderText);
        ForceTextMeshUpdate(uiComponent.TopicText1Text);
        ForceTextMeshUpdate(uiComponent.TopicText2Text);
        ForceTextMeshUpdate(uiComponent.TopicText3Text);
        ForceTextMeshUpdate(uiComponent.TopicText4Text);
    }

    private void ForceTextMeshUpdate(TMP_Text text)
    {
        if (text == null) return;
        text.ForceMeshUpdate(true, true);
    }

    private void ForceLayoutRefresh()
    {
        Canvas.ForceUpdateCanvases();

        var rebuildRoots = new List<RectTransform>();
        AddLayoutChain(rebuildRoots, uiComponent.CardDescTextText?.rectTransform);
        AddLayoutChain(rebuildRoots, uiComponent.MeaningSectionText?.rectTransform);
        AddLayoutChain(rebuildRoots, uiComponent.ActionSectionText?.rectTransform);
        AddLayoutChain(rebuildRoots, uiComponent.suitableText?.rectTransform);
        AddLayoutChain(rebuildRoots, uiComponent.notSuitableText?.rectTransform);
        AddLayoutChain(rebuildRoots, uiComponent.moodReminderText?.rectTransform);
        AddLayoutChain(rebuildRoots, uiComponent.TopicText1Text?.rectTransform);
        AddLayoutChain(rebuildRoots, uiComponent.TopicText2Text?.rectTransform);
        AddLayoutChain(rebuildRoots, uiComponent.TopicText3Text?.rectTransform);
        AddLayoutChain(rebuildRoots, uiComponent.TopicText4Text?.rectTransform);

        var scrollRect = uiComponent.GetComponentInChildren<ScrollRect>(true);
        AddUniqueRect(rebuildRoots, scrollRect != null ? scrollRect.content : null);
        AddUniqueRect(rebuildRoots, transform as RectTransform);

        for (int i = 0; i < rebuildRoots.Count; i++)
        {
            if (rebuildRoots[i] != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rebuildRoots[i]);
        }

        Canvas.ForceUpdateCanvases();
    }

    private void AddLayoutChain(List<RectTransform> roots, RectTransform start)
    {
        var current = start;
        var windowRoot = transform as RectTransform;

        while (current != null)
        {
            AddUniqueRect(roots, current);

            if (current == windowRoot)
                break;

            current = current.parent as RectTransform;
        }
    }

    private void AddUniqueRect(List<RectTransform> roots, RectTransform rect)
    {
        if (rect == null || roots.Contains(rect)) return;
        roots.Add(rect);
    }

    private void PopulateFromPayload(CompleteInterpretationPayload payload)
    {
        if (payload == null) return;

        _hasError = false;
        _fallbackTopics = null; // 清除降级话题缓存

        SetDescriptionText(payload.description);
        SetTagTexts(payload.tags);
        SetMeaningText(payload.meaningAnalysis);
        SetActionText(payload.actionSuggestion);
        SetTopicTexts(payload.topics);
        PopulateTodayDetailSections();
        RequestLayoutRefresh();
    }

    /// <summary>
    /// AI 不可用 / 超时时的降级展示
    /// </summary>
    private void PopulateFallback()
    {
        if (_currentCard == null) return;

        var locale = DailyOracleService.CurrentLocale ?? "zh-CN";
        var name = _currentCard.nameZh;

        if (locale.StartsWith("en"))
        {
            var orientLabel = _currentUpright ? "Upright" : "Reversed";
            SetDescriptionText($"Today you drew {name} ({orientLabel}). Its energy is here to guide you.");
            SetTagTexts(new List<string> { "clarity", "courage", "awareness" });
            SetMeaningText($"The {name} card carries a meaningful message for today. Reflect on what resonates.");
            SetActionText("Take a quiet moment today to sit with the card's image and notice what comes up.");
            _fallbackTopics = new List<string>
            {
                $"What does {name} want me to notice today?",
                "What is the deeper message for me?",
                "How should I handle my current situation?",
                "What question should I ask myself next?"
            };
            SetTopicTexts(_fallbackTopics);
        }
        else
        {
            var orientLabel = _currentUpright ? "正位" : "逆位";
            SetDescriptionText($"今天你抽到了{orientLabel}的{name}，它的能量在今天指引着你。");
            SetTagTexts(new List<string> { "清晰", "勇气", "觉知" });
            SetMeaningText($"{name}这张牌为你今天带来了重要的讯息。静下来感受哪些部分与你产生共鸣。");
            SetActionText("今天找一个安静的片刻，凝视这张牌的图像，看看浮现出什么感受。");
            _fallbackTopics = new List<string>
            {
                $"{name}今天想提醒我什么？",
                "这件事更深层的讯息是什么？",
                "我现在该如何面对现状？",
                "我接下来该问自己什么？"
            };
            SetTopicTexts(_fallbackTopics);
        }
        PopulateTodayDetailSections();
        RequestLayoutRefresh();
    }

    #endregion

    #region UI 赋值工具方法

    private void ResolveTopicComponentRefs()
    {
        if (uiComponent == null) return;

        uiComponent.Topic1ItemButton = uiComponent.Topic1ItemButton != null
            ? uiComponent.Topic1ItemButton
            : FindComponentByObjectName<Button>("[Button]Topic1Item");
        uiComponent.Topic2ItemButton = uiComponent.Topic2ItemButton != null
            ? uiComponent.Topic2ItemButton
            : FindComponentByObjectName<Button>("[Button]Topic2Item");
        uiComponent.Topic3ItemButton = uiComponent.Topic3ItemButton != null
            ? uiComponent.Topic3ItemButton
            : FindComponentByObjectName<Button>("[Button]Topic3Item");
        uiComponent.Topic4ItemButton = uiComponent.Topic4ItemButton != null
            ? uiComponent.Topic4ItemButton
            : FindComponentByObjectName<Button>("[Button]Topic4Item");

        uiComponent.TopicText1Text = uiComponent.TopicText1Text != null
            ? uiComponent.TopicText1Text
            : FindComponentByObjectName<TMP_Text>("[Text]TopicText1");
        uiComponent.TopicText2Text = uiComponent.TopicText2Text != null
            ? uiComponent.TopicText2Text
            : FindComponentByObjectName<TMP_Text>("[Text]TopicText2");
        uiComponent.TopicText3Text = uiComponent.TopicText3Text != null
            ? uiComponent.TopicText3Text
            : FindComponentByObjectName<TMP_Text>("[Text]TopicText3");
        uiComponent.TopicText4Text = uiComponent.TopicText4Text != null
            ? uiComponent.TopicText4Text
            : FindComponentByObjectName<TMP_Text>("[Text]TopicText4");
    }

    private T FindComponentByObjectName<T>(string objectName) where T : Component
    {
        var child = FindChildRecursive(transform, objectName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null) return null;
        if (root.name == objectName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindChildRecursive(root.GetChild(i), objectName);
            if (found != null) return found;
        }

        return null;
    }

    private void SetDescriptionText(string text)
    {
        if (uiComponent.CardDescTextText != null)
            uiComponent.CardDescTextText.text = text;
    }

    private void SetTagTexts(List<string> tags)
    {
        if (uiComponent.Tag1TextText != null)
            uiComponent.Tag1TextText.text = tags != null && tags.Count > 0 ? tags[0] : "";
        if (uiComponent.Tag2TextText != null)
            uiComponent.Tag2TextText.text = tags != null && tags.Count > 1 ? tags[1] : "";
        if (uiComponent.Tag3TextText != null)
            uiComponent.Tag3TextText.text = tags != null && tags.Count > 2 ? tags[2] : "";
    }

    private void SetMeaningText(string text)
    {
        if (uiComponent.MeaningSectionText != null)
            uiComponent.MeaningSectionText.text = text;
    }

    private void SetActionText(string text)
    {
        if (uiComponent.ActionSectionText != null)
            uiComponent.ActionSectionText.text = text;
    }

    private void PopulateTodayDetailSections()
    {
        if (_currentCard == null) return;

        var content = TodayCardUI.BuildContentForCard(_currentCard, _currentUpright);
        SetTodayDetailTexts(content);
        RequestTodayDetailContentIfNeeded();
    }

    private void SetTodayDetailTexts(TodayCardDetailContent content)
    {
        if (content == null) return;

        SetSuitableText(FormatTodayList(content.dos));
        SetNotSuitableText(FormatTodayList(content.donts));
        SetMoodReminderText(content.emotionReminder);
    }

    private void SetSuitableText(string text)
    {
        if (uiComponent.suitableText != null)
            uiComponent.suitableText.text = text ?? "";
    }

    private void SetNotSuitableText(string text)
    {
        if (uiComponent.notSuitableText != null)
            uiComponent.notSuitableText.text = text ?? "";
    }

    private void SetMoodReminderText(string text)
    {
        if (uiComponent.moodReminderText != null)
            uiComponent.moodReminderText.text = text ?? "";
    }

    private string FormatTodayList(List<string> values)
    {
        if (values == null || values.Count == 0) return "";

        var lines = new List<string>();
        for (int i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (string.IsNullOrWhiteSpace(value)) continue;
            lines.Add($"{lines.Count + 1}. {value.Trim()}");
        }

        return string.Join("\n", lines);
    }

    private void RequestTodayDetailContentIfNeeded()
    {
        if (_isPreparingTodayDetailContent || _currentCard == null) return;

        var oracleService = DailyOracleService.Instance;
        if (oracleService == null) return;
        if (oracleService.IsCachedPreparedReadingFor(_currentCard, _currentUpright)) return;
        if (oracleService.IsLoading) return;

        var requestedCard = _currentCard;
        var requestedUpright = _currentUpright;
        _isPreparingTodayDetailContent = true;

        oracleService.PrepareTodayReading(requestedCard, requestedUpright, (prepared) =>
        {
            _isPreparingTodayDetailContent = false;

            if (this == null || uiComponent == null || !uiComponent.gameObject.activeInHierarchy) return;
            if (prepared == null || !prepared.IsFor(requestedCard, requestedUpright)) return;
            if (_currentCard == null || _currentCard.cardId != requestedCard.cardId || _currentUpright != requestedUpright) return;

            SetTodayDetailTexts(TodayCardUI.BuildContentForCard(_currentCard, _currentUpright));
            RequestLayoutRefresh();
        });
    }

    private void SetTopicTexts(List<string> topics)
    {
        SetTopicButton(uiComponent.TopicText1Text, topics, 0);
        SetTopicButton(uiComponent.TopicText2Text, topics, 1);
        SetTopicButton(uiComponent.TopicText3Text, topics, 2);
        SetTopicButton(uiComponent.TopicText4Text, topics, 3);
    }

    private void SetTopicButton(TMP_Text label, List<string> topics, int index)
    {
        if (label == null) return;
        var topic = (topics != null && index < topics.Count) ? topics[index] : "";
        label.text = topic;
    }

    /// <summary>
    /// 获取第 index 个话题文本（用于点击回调）
    /// 优先级: CachedInterpretation.topics → _fallbackTopics → UI 文本
    /// </summary>
    private string GetTopicText(int index)
    {
        // 1. 优先从 AI 缓存读取
        var oracleService = DailyOracleService.Instance;
        if (oracleService != null
            && oracleService.IsCachedInterpretationFor(_currentCard, _currentUpright)
            && oracleService.CachedInterpretation?.topics != null
            && index < oracleService.CachedInterpretation.topics.Count)
        {
            var topic = oracleService.CachedInterpretation.topics[index];
            if (!string.IsNullOrEmpty(topic) && topic != "...")
                return StripQuestionPrefix(topic);
        }

        // 2. 降级：使用 fallback 话题缓存
        if (_fallbackTopics != null && index < _fallbackTopics.Count)
            return StripQuestionPrefix(_fallbackTopics[index]);

        // 3. 最终兜底：直接读 UI 文本
        var uiText = index switch
        {
            0 => uiComponent.TopicText1Text?.text,
            1 => uiComponent.TopicText2Text?.text,
            2 => uiComponent.TopicText3Text?.text,
            3 => uiComponent.TopicText4Text?.text,
            _ => ""
        };
        return StripQuestionPrefix(uiText);
    }

    private static string StripQuestionPrefix(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var trimmed = text.Trim();
        if (trimmed.StartsWith("Q  ", System.StringComparison.Ordinal))
            return trimmed.Substring(3).Trim();
        if (trimmed.StartsWith("Q ", System.StringComparison.Ordinal))
            return trimmed.Substring(2).Trim();
        return trimmed;
    }

    /// <summary>
    /// 控制 Loading 状态的 UI 表现
    /// </summary>
    private void ShowLoadingState(bool isLoading)
    {
        // 加载中禁用话题按钮，避免用户在内容未就绪时点击
        SetTopicButtonsInteractable(!isLoading);
    }

    private void SetTopicButtonsInteractable(bool interactable)
    {
        if (uiComponent.Topic1ItemButton != null) uiComponent.Topic1ItemButton.interactable = interactable;
        if (uiComponent.Topic2ItemButton != null) uiComponent.Topic2ItemButton.interactable = interactable;
        if (uiComponent.Topic3ItemButton != null) uiComponent.Topic3ItemButton.interactable = interactable;
        if (uiComponent.Topic4ItemButton != null) uiComponent.Topic4ItemButton.interactable = interactable;
    }

    #endregion

    #region 跳转到对话

    private void NavigateToDialogAndSend(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        SyncTodayCardPayloadToDialogSystem();

        var navigationUI = UIModule.Instance?.GetWindow<NavigationUI>();
        if (navigationUI != null)
        {
            navigationUI.OpenDialogUI();
        }
        else
        {
            Debug.LogWarning("[CompleteInterpretationUI] NavigationUI 未找到，无法跳转到对话界面");
        }

        EventSystem.DispatchEvent(GameDataStr.CardTopicSelected, message);
        HideWindow();
    }

    private void NavigateToDialogWithCardContext()
    {
        if (_currentCard == null)
        {
            NavigateToDialogAndSend("");
            return;
        }

        var locale = DailyOracleService.CurrentLocale ?? "zh-CN";
        string cardMsg;

        if (locale.StartsWith("en"))
        {
            cardMsg = $"I just looked at today's tarot card: {_currentCard.DisplayName(_currentUpright)}. I'd like to chat more about it.";
        }
        else
        {
            cardMsg = $"我刚看完今天的塔罗牌：{_currentCard.DisplayName(_currentUpright)}。我想和你继续聊聊这张牌。";
        }

        NavigateToDialogAndSend(cardMsg);
    }

    private void SyncTodayCardPayloadToDialogSystem()
    {
        if (_currentCard == null) return;

        var oracleService = DailyOracleService.Instance;
        TodayCardPayload payload = null;
        if (oracleService != null && oracleService.IsSameCurrentCard(_currentCard, _currentUpright))
        {
            payload = oracleService.GetTodayCardPayload();
        }

        if (payload == null)
        {
            payload = new TodayCardPayload
            {
                cardId = _currentCard.cardId,
                cardName = _currentCard.nameEn,
                displayName = _currentCard.DisplayName(_currentUpright),
                nameZh = _currentCard.nameZh,
                orientation = _currentUpright ? "upright" : "reversed",
                generatedAt = System.DateTime.Now.ToString("o"),
                title = "今日塔罗"
            };
        }

        DialogSystem.Instance?.SetTodayCardPayload(payload);
    }

    #endregion

    #region UI组件事件

    public void OnExitButtonClick()
    {
        HideWindow();
    }

    public void OnTopic1ItemButtonClick()
    {
        NavigateToDialogAndSend(GetTopicText(0));
    }

    public void OnTopic2ItemButtonClick()
    {
        NavigateToDialogAndSend(GetTopicText(1));
    }

    public void OnTopic3ItemButtonClick()
    {
        NavigateToDialogAndSend(GetTopicText(2));
    }

    public void OnTopic4ItemButtonClick()
    {
        NavigateToDialogAndSend(GetTopicText(3));
    }

    /// <summary>
    /// 切换神谕 / 重新生成 — 清除缓存后重新请求 AI
    /// </summary>
    public void OnSwitchOracleButtonClick()
    {
        if (_isLoading) return; // 防止重复点击

        DailyOracleService.Instance?.ClearCache();
        _isLoading = false;
        _hasError = false;
        _fallbackTopics = null;
        StopTimeoutCoroutine();

        // 先设占位符，再请求（顺序很重要：避免异步回调先到后被占位符覆盖）
        ShowLoadingPlaceholders();
        RequestAIInterpretation();
    }

    /// <summary>
    /// 继续和她聊聊 — 携带当前卡牌上下文跳转到对话界面
    /// </summary>
    public void OnContinueChatButtonClick()
    {
        NavigateToDialogWithCardContext();
    }

    // 以下为旧的生成代码残留，保留方法名防止序列化报错
    public void OnTopic1Item_3ButtonClick() { OnTopic3ItemButtonClick(); }
    public void OnTopic1Item_2ButtonClick() { OnTopic2ItemButtonClick(); }
    public void OnTopic1Item_1ButtonClick() { OnTopic1ItemButtonClick(); }

    #endregion
}
