/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/15/2026 10:49:55 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;

public class TodayCardUI : WindowBase
{
	public TodayCardUIComponent uiComponent;

	private DailyCardDetail _cardDetail;

	#region 生命周期函数
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TodayCardUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		_cardDetail = uiComponent.DailyCardScrollScrollRect.GetComponent<DailyCardDetail>();
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

		// 尝试从 DailyOracleService 获取 AI 生成内容
		var cachedPayload = DailyOracleService.Instance?.CachedPayload;

		if (cachedPayload != null)
		{
			// ✅ 使用 AI 生成内容
			PopulateFromAIPayload(cachedPayload, card, upright);
		}
		else
		{
			// ⚠️ 降级：没有 AI 缓存，使用本地简单模板（等 AI 完成后会刷新）
			PopulateFromFallback(card, upright);

			// 如果 DailyOracleService 可用但还没缓存，请求生成
			if (DailyOracleService.Instance != null && !DailyOracleService.Instance.IsLoading)
			{
				DailyOracleService.Instance.RequestDailyOracle(card, upright, (payload) =>
				{
					if (this != null && gameObject != null && gameObject.activeInHierarchy)
					{
						PopulateFromAIPayload(payload, card, upright);
					}
				});
			}
		}

		Debug.Log($"[TodayCardUI] 详情填充完成: {card.nameZh} ({(upright ? "正位" : "逆位")})"
			+ $"{(cachedPayload != null ? " [AI]" : " [Fallback]")}");
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

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnShareButtonClick()
	{
	}
	public void OnAskQuestion1ButtonClick()
	{
	}
	public void OnAskQuestion2ButtonClick()
	{
	}
	public void OnAskQuestion3ButtonClick()
	{
	}
	public void OnContinueChatButtonClick()
	{
		HideWindow();
		UIModule.Instance.GetWindow<NavigationUI>().OpenDialogUI();
	}
	#endregion
}
