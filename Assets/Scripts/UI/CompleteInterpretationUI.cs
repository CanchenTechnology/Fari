/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/16/2026 6:03:04 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork;
using System.Collections.Generic;

public class CompleteInterpretationUI : WindowBase
{
    public CompleteInterpretationUIComponent uiComponent;

    // 当前展示的卡牌数据
    private TarotCard _currentCard;
    private bool _currentUpright;

    #region 生命周期函数
    // 调用机制与 Mono Awake 一致
    public override void OnAwake()
    {
        uiComponent = gameObject.GetComponent<CompleteInterpretationUIComponent>();
        uiComponent.InitComponent(this);
        this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
        base.OnAwake();
    }
    // 物体显示时执行
    public override void OnShow()
    {
        base.OnShow();
        PopulateCardInfo();
        RequestAIInterpretation();
    }
    // 物体隐藏时执行
    public override void OnHide()
    {
        base.OnHide();
    }
    // 物体销毁时执行
    public override void OnDestroy()
    {
        base.OnDestroy();
    }
    #endregion

    #region API Function

    /// <summary>
    /// 从外部设置卡牌数据（由 ReadingCardContainer 或其他界面打开时调用）
    /// </summary>
    public void SetCard(TarotCard card, bool upright)
    {
        _currentCard = card;
        _currentUpright = upright;
    }

    #endregion

    #region 初始化与数据加载

    /// <summary>
    /// 填充卡牌图片和名字（立刻显示，无需等待 AI）
    /// </summary>
    private void PopulateCardInfo()
    {
        // 优先使用外部设置的卡牌，其次从 DailyOracleService 或 DivinationEngine 获取
        if (_currentCard == null)
        {
            var oracleService = DailyOracleService.Instance;
            // if (oracleService?.CurrentCard != null)
            // {
            //     _currentCard = oracleService.CurrentCard;
            //     _currentUpright = oracleService.CurrentUpright;
            // }
            // else if (DivinationEngine.Instance != null)
            // {
            //     var result = DivinationEngine.Instance.DrawDailyCard();
            //     _currentCard = result.card;
            //     _currentUpright = result.upright;
            // }
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
                uiComponent.CardImageImage.preserveAspect = true;
                // 逆位旋转 180°
                uiComponent.CardImageImage.rectTransform.localRotation = _currentUpright
                    ? Quaternion.identity
                    : Quaternion.Euler(0, 0, 180);
            }
        }

        // 清空描述（等待 AI）
        SetDescriptionText("...");
        SetTagTexts(new List<string> { "...", "...", "..." });
        SetMeaningText("...");
        SetActionText("...");
        SetTopicTexts(new List<string> { "...", "...", "...", "..." });
    }

    /// <summary>
    /// 请求 AI 生成完整解读内容
    /// </summary>
    private void RequestAIInterpretation()
    {
        if (_currentCard == null) return;

        var oracleService = DailyOracleService.Instance;
        if (oracleService == null)
        {
            Debug.LogWarning("[CompleteInterpretationUI] DailyOracleService 未就绪，使用降级内容");
            PopulateFallback();
            return;
        }

        // // 如果已有缓存且是同一张牌，直接使用
        // if (oracleService.CachedInterpretation != null
        //     && oracleService.CurrentCard == _currentCard
        //     && oracleService.CurrentUpright == _currentUpright)
        // {
        //     PopulateFromPayload(oracleService.CachedInterpretation);
        //     return;
        // }

        // // 请求 AI 生成
        // oracleService.RequestCompleteInterpretation(_currentCard, _currentUpright,
        //     (payload) =>
        //     {
        //         // 回调在主线程（UnityWebRequest 保证）
        //         PopulateFromPayload(payload);
        //     });
    }

    /// <summary>
    /// 将 AI 返回的 CompleteInterpretationPayload 赋值到 UI
    /// </summary>
    // private void PopulateFromPayload(CompleteInterpretationPayload payload)
    // {
    //     if (payload == null) return;

    //     SetDescriptionText(payload.description);
    //     SetTagTexts(payload.tags);
    //     SetMeaningText(payload.meaningAnalysis);
    //     SetActionText(payload.actionSuggestion);
    //     SetTopicTexts(payload.topics);
    // }

    /// <summary>
    /// AI 不可用时的降级展示
    /// </summary>
    private void PopulateFallback()
    {
        if (_currentCard == null) return;
        var locale ="";// DailyOracleService.CurrentLocale;
        var name = _currentCard.nameZh;

        if (locale == "en-US")
        {
            SetDescriptionText($"Today you drew {name} ({(_currentUpright ? "Upright" : "Reversed")}). Its energy is here to guide you.");
            SetTagTexts(new List<string> { "clarity", "courage", "awareness" });
            SetMeaningText($"The {name} card carries a meaningful message for today. Reflect on what resonates.");
            SetActionText("Take a quiet moment today to sit with the card's image and notice what comes up.");
            SetTopicTexts(new List<string>
            {
                $"Tell me more about {name}",
                "How does this card relate to love?",
                "What should I focus on at work?",
                "What energy is around me this week?"
            });
        }
        else
        {
            SetDescriptionText($"今天你抽到了{(_currentUpright ? "正位" : "逆位")}的{name}，它的能量在今天指引着你。");
            SetTagTexts(new List<string> { "清晰", "勇气", "觉知" });
            SetMeaningText($"{name}这张牌为你今天带来了重要的讯息。静下来感受哪些部分与你产生共鸣。");
            SetActionText("今天找一个安静的片刻，凝视这张牌的图像，看看浮现出什么感受。");
            SetTopicTexts(new List<string>
            {
                $"我想更了解{name}",
                "这张牌和感情有什么关系？",
                "今天工作上我要注意什么？",
                "这周我的整体能量怎样？"
            });
        }
    }

    #endregion

    #region UI 赋值工具方法

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

    private void SetTopicTexts(List<string> topics)
    {
        SetTopicButton(uiComponent.TopicText1Text, topics, 0);
        SetTopicButton(uiComponent.TopicText2Text, topics, 1);
        SetTopicButton(uiComponent.TopicText3Text, topics, 2);
        SetTopicButton(uiComponent.TopicText4Text, topics, 3);
    }

    private void SetTopicButton(Text label, List<string> topics, int index)
    {
        if (label == null) return;
        label.text = (topics != null && index < topics.Count) ? topics[index] : "";
    }

    /// <summary>
    /// 获取第 index 个话题文本（用于点击回调）
    /// </summary>
    private string GetTopicText(int index)
    {
        // var oracleService = DailyOracleService.Instance;
        // if (oracleService?.CachedInterpretation?.topics != null
        //     && index < oracleService.CachedInterpretation.topics.Count)
        // {
        //     return oracleService.CachedInterpretation.topics[index];
        // }

        // 降级：直接读 UI 文本
        return index switch
        {
            0 => uiComponent.TopicText1Text?.text,
            1 => uiComponent.TopicText2Text?.text,
            2 => uiComponent.TopicText3Text?.text,
            3 => uiComponent.TopicText4Text?.text,
            _ => ""
        };
    }

    #endregion

    #region 跳转到对话

    /// <summary>
    /// 跳转到对话界面并自动发送指定消息
    /// </summary>
    private void NavigateToDialogAndSend(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        // 1. 切换到对话界面（通过 NavigationUI 的导航 Toggle）
        var navigationUI = UIModule.Instance.GetWindow<NavigationUI>();
        if (navigationUI != null)
        {
            navigationUI.OpenDialogUI();
        }

        // 2. 发送消息（通过 EventSystem 通知 DialogUI）
        EventSystem.DispatchEvent(GameDataStr.CardTopicSelected, message);

        // 3. 隐藏当前界面
        HideWindow();
    }

    /// <summary>
    /// 发送今日塔罗牌信息并跳转到对话
    /// </summary>
    private void NavigateToDialogWithCardContext()
    {
        if (_currentCard == null)
        {
            NavigateToDialogAndSend("");
            return;
        }

        var locale = "";//;DailyOracleService.CurrentLocale;
        string cardMsg;

        if (locale == "en-US")
        {
            var orientLabel = _currentUpright ? "Upright" : "Reversed";
            cardMsg = $"I just looked at today's tarot card: {_currentCard.nameZh} ({orientLabel}). I'd like to chat more about it.";
        }
        else
        {
            var orientLabel = _currentUpright ? "正位" : "逆位";
            cardMsg = $"我刚看完今天的塔罗牌：{_currentCard.nameZh}（{orientLabel}）。我想和你继续聊聊这张牌。";
        }

        NavigateToDialogAndSend(cardMsg);
    }

    #endregion

    #region UI组件事件

    // 话题按钮 1
    public void OnTopic1ItemButtonClick()
    {
        NavigateToDialogAndSend(GetTopicText(0));
    }

    // 话题按钮 2
    public void OnTopic2ItemButtonClick()
    {
        NavigateToDialogAndSend(GetTopicText(1));
    }

    // 话题按钮 3
    public void OnTopic3ItemButtonClick()
    {
        NavigateToDialogAndSend(GetTopicText(2));
    }

    // 话题按钮 4
    public void OnTopic4ItemButtonClick()
    {
        NavigateToDialogAndSend(GetTopicText(3));
    }

    // 切换神谕 / 重新生成
    public void OnSwitchOracleButtonClick()
    {
        // 清除缓存，重新请求 AI
        DailyOracleService.Instance?.ClearCache();
        RequestAIInterpretation();
        SetDescriptionText("...");
        SetTagTexts(new List<string> { "...", "...", "..." });
        SetMeaningText("...");
        SetActionText("...");
        SetTopicTexts(new List<string> { "...", "...", "...", "..." });
    }

    // 继续和她聊聊
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
