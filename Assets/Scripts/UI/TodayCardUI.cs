/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/15/2026 10:49:55 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork;
using GamerFrameWork.OracleRuntime;
using TMPro;

public class TodayCardDetailContent
{
	public string description;
	public string uprightMeaning;
	public string reversedMeaning;
	public List<string> dos = new List<string>();
	public List<string> donts = new List<string>();
	public string todayState;
	public string emotionReminder;
	public string actionSuggestion;
	public List<string> followupQuestions = new List<string>();
}

public class TodayCardUI : WindowBase
{
	public TodayCardUIComponent uiComponent;

	private DailyCardDetail _cardDetail;
	private TodayCardDetailContent _contentOverride;
	private readonly List<string> _followupQuestions = new List<string>();

	public static Func<TarotCard, bool, TodayCardDetailContent> ContentRuleProvider;

	#region 生命周期函数
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TodayCardUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		_cardDetail = uiComponent.DailyCardScrollScrollRect.GetComponent<DailyCardDetail>();
		BindQuestionButtons();
	}

	public override void OnShow()
	{
		base.OnShow();
		PopulateCardDetail();
	}

	public override void OnHide()
	{
		base.OnHide();
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region 数据填充

	public void SetContentOverride(TodayCardDetailContent content)
	{
		_contentOverride = content;
	}

	private void PopulateCardDetail()
	{
		if (_cardDetail == null)
		{
			Debug.LogWarning("[TodayCardUI] DailyCardDetail 组件未找到，跳过填充");
			return;
		}

		if (DivinationEngine.Instance?.TodayCard.HasValue != true)
		{
			Debug.LogWarning("[TodayCardUI] TodayCard 为空，跳过填充");
			return;
		}

		var (card, upright) = DivinationEngine.Instance.TodayCard.Value;

		// 卡牌图片
		if (_cardDetail.cardImage != null)
		{
			var sprite = TarotSpriteLoader.Load(card.cardId);
			if (sprite != null)
			{
				_cardDetail.cardImage.sprite = sprite;
				_cardDetail.cardImage.preserveAspect = true;
				_cardDetail.cardImage.rectTransform.localRotation = upright
					? Quaternion.identity
					: Quaternion.Euler(0, 0, 180);
			}
		}

		// 卡牌名称
		if (_cardDetail.cardNameText != null)
			_cardDetail.cardNameText.text = card.DisplayName(upright);

		var content = BuildContent(card, upright);
		ApplyContent(content, card, upright);
		RequestContentIfNeeded(card, upright);

		Debug.Log($"[TodayCardUI] 详情填充完成: {card.nameZh} ({(upright ? "正位" : "逆位")})"
			+ $"{(HasAIContent(card, upright) ? " [AI]" : " [Fallback]")}");
	}

	/// <summary>
	/// 从 AI 生成的 TodayOraclePayload 填充所有字段
	/// </summary>
	private void PopulateFromAIPayload(TodayOraclePayload payload, TarotCard card, bool upright)
	{
		if (payload == null) return;

		// 牌面描述 → AI 的 detail
		if (_cardDetail.descriptionText != null && !string.IsNullOrEmpty(payload.detail))
			_cardDetail.descriptionText.text = payload.detail;

		// 神谕（正位牌义区域）
		if (_cardDetail.uprightMeaningText != null && !string.IsNullOrEmpty(payload.oracle))
			_cardDetail.uprightMeaningText.text = payload.oracle;

		// 逆位释义（用详情的前半作为逆位解读提示）
		if (_cardDetail.reversedMeaningText != null)
			_cardDetail.reversedMeaningText.text = upright
				? $"{card.nameZh}今日以正向能量示现。注意：不要因为牌面美好就放松觉察。"
				: $"{card.nameZh}以逆位示现，提醒你回看被忽略的面向。";

		// 今日适宜（逐条）
		if (payload.dos != null)
		{
			if (_cardDetail.uprightMeaningText1 != null && payload.dos.Count > 0)
				_cardDetail.uprightMeaningText1.text = $"1. {payload.dos[0]}";
			if (_cardDetail.uprightMeaningText2 != null && payload.dos.Count > 1)
				_cardDetail.uprightMeaningText2.text = $"2. {payload.dos[1]}";
			if (_cardDetail.uprightMeaningText3 != null && payload.dos.Count > 2)
				_cardDetail.uprightMeaningText3.text = $"3. {payload.dos[2]}";
		}

		// 今日不宜（逐条）
		if (payload.donts != null)
		{
			if (_cardDetail.reversedMeaningText1 != null && payload.donts.Count > 0)
				_cardDetail.reversedMeaningText1.text = $"1. {payload.donts[0]}";
			if (_cardDetail.reversedMeaningText2 != null && payload.donts.Count > 1)
				_cardDetail.reversedMeaningText2.text = $"2. {payload.donts[1]}";
			if (_cardDetail.reversedMeaningText3 != null && payload.donts.Count > 2)
				_cardDetail.reversedMeaningText3.text = $"3. {payload.donts[2]}";
		}

		// 今日状态映射 → AI 的 microAction
		if (_cardDetail.todayStateText != null && !string.IsNullOrEmpty(payload.microAction))
			_cardDetail.todayStateText.text = payload.microAction;

		// 情绪提醒（从 dos[0] 派生）
		if (_cardDetail.emotionText != null && payload.dos != null && payload.dos.Count > 0)
			_cardDetail.emotionText.text = $"今日核心提醒：{payload.dos[0]}。";

		// 行动建议（从 dos[1] 或 microAction 派生）
		if (_cardDetail.actionSuggestionText != null)
		{
			_cardDetail.actionSuggestionText.text = !string.IsNullOrEmpty(payload.microAction)
				? payload.microAction
				: (payload.dos != null && payload.dos.Count > 1 ? payload.dos[1] : "跟随直觉行动");
		}
	}

	private TodayCardDetailContent BuildContent(TarotCard card, bool upright)
	{
		if (_contentOverride != null)
			return MergeWithFallback(_contentOverride, BuildFallbackContent(card, upright));

		var ruleContent = ContentRuleProvider?.Invoke(card, upright);
		if (ruleContent != null)
			return MergeWithFallback(ruleContent, BuildFallbackContent(card, upright));

		var oracleService = DailyOracleService.Instance;
		var preparedReading = oracleService?.CachedPreparedReading;
		var oraclePayload = preparedReading != null && preparedReading.IsFor(card, upright)
			? preparedReading.oraclePayload
			: oracleService != null && oracleService.IsCachedOracleFor(card, upright)
				? oracleService.CachedPayload
				: null;

		var interpretationPayload = preparedReading != null && preparedReading.IsFor(card, upright)
			? preparedReading.interpretationPayload
			: oracleService != null && oracleService.IsCachedInterpretationFor(card, upright)
				? oracleService.CachedInterpretation
				: null;

		return MergeWithFallback(
			BuildContentFromPayloads(oraclePayload, interpretationPayload, card, upright),
			BuildFallbackContent(card, upright));
	}

	private TodayCardDetailContent BuildContentFromPayloads(TodayOraclePayload oraclePayload,
		CompleteInterpretationPayload interpretationPayload, TarotCard card, bool upright)
	{
		var content = new TodayCardDetailContent();

		content.description = FirstNonEmpty(interpretationPayload?.description, oraclePayload?.detail);
		content.uprightMeaning = FirstNonEmpty(interpretationPayload?.meaningAnalysis, oraclePayload?.oracle);
		content.reversedMeaning = upright
			? $"{card.nameZh}今日以正向能量示现。也提醒你别因为顺利就忽略内心的真实感受。"
			: $"{card.nameZh}以逆位示现，提醒你回看被忽略的面向。";

		if (oraclePayload?.dos != null)
			content.dos.AddRange(oraclePayload.dos);
		if (oraclePayload?.donts != null)
			content.donts.AddRange(oraclePayload.donts);

		content.todayState = FirstNonEmpty(oraclePayload?.microAction, interpretationPayload?.actionSuggestion);
		content.emotionReminder = !string.IsNullOrEmpty(oraclePayload?.oracle)
			? $"今日核心提醒：{oraclePayload.oracle}"
			: "";
		content.actionSuggestion = FirstNonEmpty(interpretationPayload?.actionSuggestion, oraclePayload?.microAction);

		if (interpretationPayload?.topics != null)
		{
			foreach (var topic in interpretationPayload.topics)
			{
				if (!string.IsNullOrWhiteSpace(topic))
					content.followupQuestions.Add(topic);
			}
		}

		return content;
	}

	private TodayCardDetailContent BuildFallbackContent(TarotCard card, bool upright)
	{
		var kw = card.keywords ?? new List<string>();
		var content = new TodayCardDetailContent
		{
			description = BuildFallbackDescription(card, upright),
			uprightMeaning = BuildFallbackUpright(card),
			reversedMeaning = BuildFallbackReversed(card),
			todayState = upright
				? "今天适合把注意力放回你真正想推进的事情上。"
				: "今天先别急着推进，先看清哪里正在消耗你。",
			emotionReminder = kw.Count > 0 ? $"今日核心提醒：留意「{kw[0]}」带来的感受。" : "今日核心提醒：先照顾好自己的状态。",
			actionSuggestion = upright ? "选一件最小但确定的事开始行动。" : "先暂停一下，把真实感受写下来。"
		};

		content.dos.Add(kw.Count > 0 ? $"靠近「{kw[0]}」相关的选择" : "做一个让自己安定的小决定");
		content.dos.Add(kw.Count > 1 ? $"观察「{kw[1]}」在今天如何出现" : $"留意{MapElementZh(card.element)}带来的直觉");
		content.dos.Add("给自己留出一段不被打扰的时间");

		content.donts.Add(upright ? "不要因为顺利就忽略细节" : "不要急着否定自己的感受");
		content.donts.Add("不要用别人的节奏催促自己");
		content.donts.Add("不要把一个瞬间当成最终答案");

		content.followupQuestions.Add($"这张{card.nameZh}今天最想提醒我什么？");
		content.followupQuestions.Add(upright ? "我现在可以迈出的最小一步是什么？" : "我现在最该先看清的阻碍是什么？");
		content.followupQuestions.Add($"这张牌和我的关系/工作状态有什么关联？");

		return content;
	}

	private TodayCardDetailContent MergeWithFallback(TodayCardDetailContent primary, TodayCardDetailContent fallback)
	{
		if (primary == null) return fallback;
		if (fallback == null) return primary;

		primary.description = FirstNonEmpty(primary.description, fallback.description);
		primary.uprightMeaning = FirstNonEmpty(primary.uprightMeaning, fallback.uprightMeaning);
		primary.reversedMeaning = FirstNonEmpty(primary.reversedMeaning, fallback.reversedMeaning);
		primary.todayState = FirstNonEmpty(primary.todayState, fallback.todayState);
		primary.emotionReminder = FirstNonEmpty(primary.emotionReminder, fallback.emotionReminder);
		primary.actionSuggestion = FirstNonEmpty(primary.actionSuggestion, fallback.actionSuggestion);
		FillList(primary.dos, fallback.dos, 3);
		FillList(primary.donts, fallback.donts, 3);
		FillList(primary.followupQuestions, fallback.followupQuestions, 3);
		return primary;
	}

	private void ApplyContent(TodayCardDetailContent content, TarotCard card, bool upright)
	{
		if (content == null) return;

		if (_cardDetail.descriptionText != null)
			_cardDetail.descriptionText.text = content.description;
		if (_cardDetail.uprightMeaningText != null)
			_cardDetail.uprightMeaningText.text = content.uprightMeaning;
		if (_cardDetail.reversedMeaningText != null)
			_cardDetail.reversedMeaningText.text = content.reversedMeaning;

		SetText(_cardDetail.uprightMeaningText1, GetListValue(content.dos, 0, true));
		SetText(_cardDetail.uprightMeaningText2, GetListValue(content.dos, 1, true));
		SetText(_cardDetail.uprightMeaningText3, GetListValue(content.dos, 2, true));
		SetText(_cardDetail.reversedMeaningText1, GetListValue(content.donts, 0, true));
		SetText(_cardDetail.reversedMeaningText2, GetListValue(content.donts, 1, true));
		SetText(_cardDetail.reversedMeaningText3, GetListValue(content.donts, 2, true));
		SetText(_cardDetail.todayStateText, content.todayState);
		SetText(_cardDetail.emotionText, content.emotionReminder);
		SetText(_cardDetail.actionSuggestionText, content.actionSuggestion);

		SetFollowupQuestions(content.followupQuestions);
	}

	private void RequestContentIfNeeded(TarotCard card, bool upright)
	{
		var oracleService = DailyOracleService.Instance;
		if (oracleService == null) return;
		if (oracleService.IsCachedPreparedReadingFor(card, upright)) return;
		if (oracleService.IsLoading) return;

		oracleService.PrepareTodayReading(card, upright, (prepared) =>
		{
			if (this == null || gameObject == null || !gameObject.activeInHierarchy) return;
			if (prepared == null || !prepared.IsFor(card, upright)) return;
			ApplyContent(BuildContent(card, upright), card, upright);
		});
	}

	private bool HasAIContent(TarotCard card, bool upright)
	{
		var oracleService = DailyOracleService.Instance;
		return oracleService != null
			&& (oracleService.IsCachedPreparedReadingFor(card, upright)
				|| oracleService.IsCachedOracleFor(card, upright)
				|| oracleService.IsCachedInterpretationFor(card, upright));
	}

	/// <summary>
	/// 降级模板（AI 不可用或正在生成中时使用）
	/// </summary>
	private void PopulateFromFallback(TarotCard card, bool upright)
	{
		// 牌面描述
		if (_cardDetail.descriptionText != null)
			_cardDetail.descriptionText.text = BuildFallbackDescription(card, upright);

		// 正位释义
		if (_cardDetail.uprightMeaningText != null)
			_cardDetail.uprightMeaningText.text = BuildFallbackUpright(card);

		// 逆位释义
		if (_cardDetail.reversedMeaningText != null)
			_cardDetail.reversedMeaningText.text = BuildFallbackReversed(card);

		// 使用关键词填充宜/不宜区域作为降级
		var kw = card.keywords ?? new List<string>();
		if (_cardDetail.uprightMeaningText1 != null)
			_cardDetail.uprightMeaningText1.text = kw.Count > 0 ? $"关键词：{kw[0]}" : "";
		if (_cardDetail.uprightMeaningText2 != null)
			_cardDetail.uprightMeaningText2.text = kw.Count > 1 ? $"元素：{MapElementZh(card.element)}" : "";
		if (_cardDetail.uprightMeaningText3 != null)
			_cardDetail.uprightMeaningText3.text = kw.Count > 2 ? $"牌组：{MapArcanaZh(card.arcana)}" : "";
		if (_cardDetail.reversedMeaningText1 != null) _cardDetail.reversedMeaningText1.text = "";
		if (_cardDetail.reversedMeaningText2 != null) _cardDetail.reversedMeaningText2.text = "";
		if (_cardDetail.reversedMeaningText3 != null) _cardDetail.reversedMeaningText3.text = "";
	}

	#endregion

	#region 降级静态模板

	private static string BuildFallbackDescription(TarotCard card, bool upright)
	{
		var mood = upright ? "正向" : "警示";
		return $"今天抽到了{card.nameZh}（{mood}），这张牌属于{MapArcanaZh(card.arcana)}，"
			+ $"由{MapElementZh(card.element)}能量引导。请跟随牌面的指引，"
			+ $"让今天的每一步都更有觉知。";
	}

	private static string BuildFallbackUpright(TarotCard card)
	{
		var keywords = card.keywords != null && card.keywords.Count > 0
			? string.Join("、", card.keywords)
			: card.nameZh;
		return $"正位的{card.nameZh}代表{keywords}。这是一张充满正向能量的牌，提醒你相信自己的内在智慧，"
			+ $"勇敢迈出第一步。今天适合开启新计划、表达真实想法、拥抱未知的可能性。";
	}

	private static string BuildFallbackReversed(TarotCard card)
	{
		return $"逆位的{card.nameZh}提醒你注意内在的抗拒与逃避。可能有些事情被你忽略了，"
			+ $"或是你正在回避某个重要的决定。不妨停下来审视内心，找到真正的阻碍所在。";
	}

	private static string MapElementZh(string element)
	{
		return element switch
		{
			"fire" => "火元素",
			"water" => "水元素",
			"air" => "风元素",
			"earth" => "土元素",
			"spirit" => "灵性",
			_ => element
		};
	}

	private static string MapArcanaZh(string arcana)
	{
		return arcana == "major" ? "大阿卡纳" : "小阿卡纳";
	}

	private static string FirstNonEmpty(params string[] values)
	{
		if (values == null) return "";
		foreach (var value in values)
		{
			if (!string.IsNullOrWhiteSpace(value))
				return value;
		}
		return "";
	}

	private static void FillList(List<string> target, List<string> fallback, int count)
	{
		if (target == null) return;
		if (fallback == null) fallback = new List<string>();

		for (int i = target.Count - 1; i >= 0; i--)
		{
			if (string.IsNullOrWhiteSpace(target[i]))
				target.RemoveAt(i);
		}

		for (int i = 0; target.Count < count && i < fallback.Count; i++)
		{
			if (!string.IsNullOrWhiteSpace(fallback[i]))
				target.Add(fallback[i]);
		}
	}

	private static string GetListValue(List<string> values, int index, bool withNumber)
	{
		if (values == null || index < 0 || index >= values.Count) return "";
		var value = values[index] ?? "";
		return withNumber && !string.IsNullOrEmpty(value) ? $"{index + 1}. {value}" : value;
	}

	private static void SetText(TMP_Text text, string value)
	{
		if (text != null)
			text.text = value ?? "";
	}

	#endregion

	#region UI组件事件
	private void BindQuestionButtons()
	{
		BindQuestionButton(uiComponent.AskQuestion1Button, 0);
		BindQuestionButton(uiComponent.AskQuestion2Button, 1);
		BindQuestionButton(uiComponent.AskQuestion3Button, 2);
	}

	private void BindQuestionButton(Button button, int index)
	{
		if (button == null) return;
		if (FindQuestRowItem(button) != null)
			return;

		button.onClick.RemoveAllListeners();
		button.onClick.AddListener(() => SendFollowupQuestion(GetDisplayedQuestion(button, index)));
	}

	private void SetFollowupQuestions(List<string> questions)
	{
		_followupQuestions.Clear();
		if (questions != null)
		{
			foreach (var question in questions)
			{
				if (!string.IsNullOrWhiteSpace(question))
					_followupQuestions.Add(question);
			}
		}

		SetQuestionButtonText(uiComponent.AskQuestion1Button, 0);
		SetQuestionButtonText(uiComponent.AskQuestion2Button, 1);
		SetQuestionButtonText(uiComponent.AskQuestion3Button, 2);
	}

	private void SetQuestionButtonText(Button button, int index)
	{
		if (button == null) return;
		var question = index >= 0 && index < _followupQuestions.Count ? _followupQuestions[index] : "";

		var questRowItem = FindQuestRowItem(button);

		if (questRowItem != null)
		{
			button.onClick.RemoveAllListeners();
			questRowItem.SetData(question, SendFollowupQuestion);
		}
		else
		{
			var text = button.GetComponentInChildren<TMP_Text>(true);
			if (text != null)
				text.text = question;

			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(() => SendFollowupQuestion(GetDisplayedQuestion(button, index)));
		}

		button.gameObject.SetActive(!string.IsNullOrEmpty(question));
	}

	private QuestRowItem FindQuestRowItem(Button button)
	{
		if (button == null) return null;
		var questRowItem = button.GetComponent<QuestRowItem>();
		if (questRowItem != null) return questRowItem;

		questRowItem = button.GetComponentInChildren<QuestRowItem>(true);
		if (questRowItem != null) return questRowItem;

		return button.GetComponentInParent<QuestRowItem>(true);
	}

	private string GetDisplayedQuestion(Button button, int index)
	{
		var questRowItem = FindQuestRowItem(button);
		if (questRowItem != null && !string.IsNullOrWhiteSpace(questRowItem.Question))
			return questRowItem.Question;

		var text = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
		if (text != null && !string.IsNullOrWhiteSpace(text.text))
			return text.text;

		return index >= 0 && index < _followupQuestions.Count ? _followupQuestions[index] : "";
	}

	private void SendFollowupQuestion(int index)
	{
		if (index < 0 || index >= _followupQuestions.Count) return;
		SendFollowupQuestion(_followupQuestions[index]);
	}

	private void SendFollowupQuestion(string question)
	{
		if (string.IsNullOrWhiteSpace(question)) return;

		SyncTodayCardPayloadToDialogSystem();

		var navigationUI = UIModule.Instance?.GetWindow<NavigationUI>();
		if (navigationUI != null)
			navigationUI.OpenDialogUI();
		else
			Debug.LogWarning("[TodayCardUI] NavigationUI 未找到，无法跳转到对话界面");

		EventSystem.DispatchEvent(GameDataStr.CardTopicSelected, question);
		HideWindow();
	}

	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnShareButtonClick()
	{
		if (DivinationEngine.Instance?.TodayCard.HasValue != true)
		{
			ToastManager.ShowToast("暂无今日神谕可分享");
			return;
		}

		var (card, upright) = DivinationEngine.Instance.TodayCard.Value;
		TodayCardDetailContent content = BuildContent(card, upright);
		string shareText = BuildShareText(card, upright, content);
		FriendInviteShareUtility.ShareText(shareText, "分享今日神谕", "今日神谕已复制");
	}
	public void OnAskQuestion1ButtonClick()
	{
		SendFollowupQuestion(0);
	}
	public void OnAskQuestion2ButtonClick()
	{
		SendFollowupQuestion(1);
	}
	public void OnAskQuestion3ButtonClick()
	{
		SendFollowupQuestion(2);
	}
	public void OnContinueChatButtonClick()
	{
		SyncTodayCardPayloadToDialogSystem();
		HideWindow();
		UIModule.Instance.GetWindow<NavigationUI>()?.OpenDialogUI();
		UIModule.Instance.GetWindow<DialogUI>()?.SendTodayOracleMessage();
	}

	private string BuildShareText(TarotCard card, bool upright, TodayCardDetailContent content)
	{
		StringBuilder builder = new StringBuilder();
		builder.AppendLine("FariApp 今日神谕");
		builder.AppendLine($"今日牌：{card.DisplayName(upright)}");

		if (!string.IsNullOrWhiteSpace(content?.uprightMeaning))
			builder.AppendLine($"神谕：{content.uprightMeaning}");
		if (!string.IsNullOrWhiteSpace(content?.todayState))
			builder.AppendLine($"今日状态：{content.todayState}");
		if (!string.IsNullOrWhiteSpace(content?.actionSuggestion))
			builder.AppendLine($"行动建议：{content.actionSuggestion}");

		if (content?.dos != null && content.dos.Count > 0)
			builder.AppendLine($"适宜：{content.dos[0]}");
		if (content?.donts != null && content.donts.Count > 0)
			builder.AppendLine($"不宜：{content.donts[0]}");

		return builder.ToString().Trim();
	}

	private void SyncTodayCardPayloadToDialogSystem()
	{
		TodayCardPayload payload = BuildTodayCardPayloadForDialog();
		if (payload != null)
			DialogSystem.Instance?.SetTodayCardPayload(payload);
	}

	private TodayCardPayload BuildTodayCardPayloadForDialog()
	{
		if (DivinationEngine.Instance?.TodayCard.HasValue != true)
			return null;

		var (card, upright) = DivinationEngine.Instance.TodayCard.Value;
		TodayCardPayload payload = DivinationEngine.Instance.GetTodayCardPayload() ?? new TodayCardPayload
		{
			cardId = card.cardId,
			cardName = card.nameEn,
			displayName = card.DisplayName(upright),
			nameZh = card.nameZh,
			orientation = upright ? "upright" : "reversed",
			generatedAt = DateTime.Now.ToString("o"),
			title = "今日塔罗"
		};

		TodayCardDetailContent content = BuildContent(card, upright);
		payload.oracleText = FirstNonEmpty(payload.oracleText, content?.uprightMeaning, content?.todayState, content?.actionSuggestion);
		payload.title = FirstNonEmpty(payload.title, "今日塔罗");
		return payload;
	}
	#endregion
}
