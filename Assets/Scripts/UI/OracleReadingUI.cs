/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/16/2026 4:35:16 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;

public class OracleReadingUI : WindowBase
{
	public OracleReadingUIComponent uiComponent;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<OracleReadingUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		OracleForegroundEffects.Attach(this.Canvas, OracleForegroundEffectStyle.DailyOracle);
		PopulateTodayCard();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		OracleForegroundEffects.Detach(this.Canvas);
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
	/// 赋值今日塔罗牌图片和名字
	/// </summary>
	private void PopulateTodayCard()
	{
		if (DivinationEngine.Instance == null) return;

		var result = DivinationEngine.Instance.TodayCard.HasValue
			? DivinationEngine.Instance.TodayCard.Value
			: DivinationEngine.Instance.DrawDailyCard();
		var card = result.card;
		bool upright = result.upright;

		var preparedReading = DailyOracleService.Instance?.CachedPreparedReading;
		if (preparedReading == null || !preparedReading.IsFor(card, upright))
		{
			DailyOracleService.Instance?.PreloadTodayReading(card, upright, (payload) =>
			{
				if (this != null && gameObject != null && gameObject.activeInHierarchy)
					PopulateMessageFields(card, upright, payload);
			});
		}

		// 牌名
		if (uiComponent.CardNameTextText != null)
			uiComponent.CardNameTextText.text = preparedReading?.cardDisplayName ?? card.DisplayName(upright);

		// 牌图
		if (uiComponent.CardImageImage != null)
		{
			var sprite = preparedReading?.cardIcon ?? TarotSpriteLoader.Load(card.cardId);
			if (sprite != null)
			{
				uiComponent.CardImageImage.sprite = sprite;
				// 逆位旋转 180°
				uiComponent.CardImageImage.rectTransform.localRotation = upright
					? Quaternion.identity
					: Quaternion.Euler(0, 0, 180);
			}
		}

		PopulateMessageFields(card, upright, preparedReading?.oraclePayload);
	}

	private void PopulateMessageFields(TarotCard card, bool upright, TodayOraclePayload payload)
	{
		var title = FirstNonEmpty(payload?.title, BuildFallbackTitle(card, upright));
		var content = FirstNonEmpty(payload?.oracle, payload?.detail, BuildFallbackContent(card, upright));

		if (uiComponent.MessageTitleText != null)
			uiComponent.MessageTitleText.text = CleanMessageTitle(title);
		if (uiComponent.MessageContentText != null)
			uiComponent.MessageContentText.text = content;
	}

	private static string BuildFallbackTitle(TarotCard card, bool upright)
	{
		if (card == null) return "命运的低语";

		var keywords = card.keywords;
		if (keywords != null && keywords.Count > 0)
			return upright ? $"{keywords[0]}正在靠近" : $"看见{keywords[0]}";

		return upright ? "微光照亮前路" : "慢下来听自己";
	}

	private static string BuildFallbackContent(TarotCard card, bool upright)
	{
		if (card == null)
			return "先让自己安静下来，答案会在更清醒的地方浮现。";

		return upright
			? $"{card.nameZh}的正位能量提醒你，今天可以相信那个已经变清晰的感受。先迈出一小步，不必一次确认全部答案。"
			: $"逆位的{card.nameZh}提醒你，先别急着追问外界。真正需要被看见的线索，可能正在你的迟疑和不安里。";
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

	private static string CleanMessageTitle(string title)
	{
		if (string.IsNullOrWhiteSpace(title)) return "";

		var cleaned = title.Trim();
		if (cleaned.StartsWith("今日神谕", System.StringComparison.Ordinal)
			|| cleaned.StartsWith("今日标题", System.StringComparison.Ordinal))
		{
			var separators = new[] { "·", "：" };
			foreach (var separator in separators)
			{
				var index = cleaned.IndexOf(separator, System.StringComparison.Ordinal);
				if (index >= 0 && index + separator.Length < cleaned.Length)
					return cleaned.Substring(index + separator.Length).Trim();
			}

			cleaned = cleaned.Replace("今日神谕", "").Replace("今日标题", "").Trim();
		}

		return string.IsNullOrEmpty(cleaned) ? title.Trim() : cleaned;
	}

	#endregion

	#region UI组件事件
	public void OnSwitchOracleButtonClick()
	{
		PopulateTodayCard();
	}
	public void OnanyWhereBtnButtonClick()
	{
		HideWindow();
	}
	#endregion
}
