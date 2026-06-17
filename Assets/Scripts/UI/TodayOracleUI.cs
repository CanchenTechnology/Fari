/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/9/2026 10:43:40 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;

public class TodayOracleUI : WindowBase
{
	public TodayOracleUIComponent uiComponent;

	private DivinationEngine _divinationEngine;
	private ReadingCardContainer _readingCardContainer;
	private DailyOracleService _oracleService;

	// 当前显示的牌数据（用于 DeepChat 等场景）
	private TarotCard _currentCard;
	private bool _currentUpright;
	private bool _isPreparingFlip;
	private Coroutine _prepareFlipCoroutine;
	private Coroutine _idleVideoCoroutine;
	private LoadingTextUI _loadingTextUI;

	[SerializeField] private float flipRevealDelaySeconds = 1.2f;

	#region 生命周期函数
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TodayOracleUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();

		_divinationEngine = DivinationEngine.Instance;
		if (_divinationEngine == null)
		{
			var go = new GameObject("DivinationEngine");
			_divinationEngine = go.AddComponent<DivinationEngine>();
		}

		// 获取 ReadingCardContainer 组件
		if (uiComponent.ReadingCardContainerTransform != null)
		{
			_readingCardContainer = uiComponent.ReadingCardContainerTransform.GetComponent<ReadingCardContainer>();
		}

		// 初始化 DailyOracleService
		EnsureDailyOracleService();
	}

	public override void OnShow()
	{
		base.OnShow();
		PlayIdleVideo();

		// 检查是否有缓存的 TodayOraclePayload，直接填充
		if (_oracleService != null && _oracleService.CachedPayload != null
			&& _divinationEngine?.TodayCard.HasValue == true)
		{
			var (card, upright) = _divinationEngine.TodayCard.Value;
			_currentCard = card;
			_currentUpright = upright;
			if (_oracleService.IsCachedOracleFor(card, upright))
				PopulateOracleFields(_oracleService.CachedPayload);
		}
	}

	public override void OnHide()
	{
		PauseIdleVideo();
		base.OnHide();
	}

	public override void OnDestroy()
	{
		StopPrepareFlipCoroutine();
		base.OnDestroy();
	}
	#endregion

	#region API Function

	private void PlayIdleVideo()
	{
		if (_idleVideoCoroutine != null)
			uiComponent.StopCoroutine(_idleVideoCoroutine);

		_idleVideoCoroutine = uiComponent.StartCoroutine(PlayIdleVideoRoutine());
	}

	private void PauseIdleVideo()
	{
		if (_idleVideoCoroutine != null)
		{
			uiComponent.StopCoroutine(_idleVideoCoroutine);
			_idleVideoCoroutine = null;
		}

		var videoPlayer = uiComponent?.idleVideoPlayer;
		if (videoPlayer != null && videoPlayer.isPlaying)
			videoPlayer.Pause();
	}

	private IEnumerator PlayIdleVideoRoutine()
	{
		var videoPlayer = uiComponent?.idleVideoPlayer;
		if (videoPlayer == null)
		{
			Debug.LogWarning("[TodayOracleUI] idleVideoPlayer is not assigned.");
			yield break;
		}

		if (videoPlayer.clip == null)
		{
			Debug.LogWarning("[TodayOracleUI] idleVideoPlayer has no video clip.");
			yield break;
		}

		videoPlayer.isLooping = true;

		if (!videoPlayer.isPrepared)
		{
			videoPlayer.Prepare();
			while (!videoPlayer.isPrepared)
				yield return null;
		}

		videoPlayer.time = 0;
		videoPlayer.Play();
		_idleVideoCoroutine = null;
	}

	#endregion

	#region UI组件事件
	public void OnDeepChatButtonClick()
	{
		// 先翻牌（如果还没翻）
		if (_divinationEngine != null && !_divinationEngine.TodayCard.HasValue)
		{
			_divinationEngine.DrawDailyCard();
		}

		// 把今日牌数据同步到 DialogSystem
		if (_divinationEngine?.TodayCard.HasValue == true)
		{
			var payload = _divinationEngine.GetTodayCardPayload();
			DialogSystem.Instance?.SetTodayCardPayload(payload);
			Debug.Log($"[TodayOracleUI] DeepChat 携带今日牌: {payload.displayName}");
		}

		UIModule.Instance.GetWindow<NavigationUI>().OpenDialogUI();
		UIModule.Instance.GetWindow<DialogUI>().SendTodayOracleMessage();
	}

	public void OnswitchDivinerButtonClick()
	{
		UIModule.Instance.PopUpWindow<SwitchRoleUI>();
	}

	public void OnflipCardButtonClick()
	{
		if (_isPreparingFlip) return;

		_prepareFlipCoroutine = uiComponent.StartCoroutine(PrepareAndRevealTodayCardRoutine());
	}

	private IEnumerator PrepareAndRevealTodayCardRoutine()
	{
		_isPreparingFlip = true;
		uiComponent.flipCardButton.gameObject.SetActive(false);
		uiComponent.ReadingCardContainerTransform.gameObject.SetActive(false);

		// 绘制今日牌
		if (_divinationEngine == null)
		{
			_isPreparingFlip = false;
			yield break;
		}

		var (card, upright) = _divinationEngine.DrawDailyCard();
		_currentCard = card;
		_currentUpright = upright;
		Debug.Log($"[TodayOracleUI] 翻牌准备: {card.nameZh} ({(upright ? "正位" : "逆位")})");
		ShowLoadingText(card);

		EnsureDailyOracleService();

		TodayOraclePreparedReading preparedReading = null;
		bool preparedReady = false;
		if (_oracleService != null)
		{
			_oracleService.PrepareTodayReading(card, upright, (prepared) =>
			{
				preparedReading = prepared;
				preparedReady = true;
			});
		}
		else
		{
			preparedReading = BuildLocalPreparedReading(card, upright);
			preparedReady = true;
		}

		float elapsed = 0f;
		while (elapsed < flipRevealDelaySeconds || !preparedReady)
		{
			elapsed += Time.deltaTime;
			yield return null;
		}

		if (preparedReading == null)
			preparedReading = BuildLocalPreparedReading(card, upright);

		HideLoadingText();
		RevealPreparedReading(preparedReading);
		_isPreparingFlip = false;
		_prepareFlipCoroutine = null;
	}

	private void PopulateReadingCardContainer(TarotCard card, bool upright)
	{
		// 卡牌图片
		if (_readingCardContainer.cardImage != null)
		{
			var sprite = TarotSpriteLoader.Load(card.cardId);
			if (sprite != null)
			{
				_readingCardContainer.cardImage.sprite = sprite;
				_readingCardContainer.cardImage.rectTransform.localRotation = upright
					? Quaternion.identity
					: Quaternion.Euler(0, 0, 180);
				_readingCardContainer.cardImage.preserveAspect = true;
			}
		}

		// 卡牌名称
		if (_readingCardContainer.cardNameText != null)
			_readingCardContainer.cardNameText.text = card.DisplayName(upright);

		// 标题（先用默认值，等 AI 返回后更新）
		if (_readingCardContainer.titleText != null)
			_readingCardContainer.titleText.text = $"今日神谕 · {card.nameZh}";

		// 描述：先显示简短的占位文本，等 AI 返回后更新
		if (_readingCardContainer.descriptText != null)
		{
			_readingCardContainer.descriptText.text = upright
				? $"{card.nameZh}以正位示现，关键词是{string.Join("、", card.keywords)}。"
				: $"逆位的{card.nameZh}出现，提醒你留意被忽略的面向。";
		}


		// 翻牌时预热所有今日牌相关数据，后续界面统一读取同一份缓存
		PreloadTodayReading(card, upright);
	}

	private void PopulateReadingCardContainer(TodayOraclePreparedReading preparedReading)
	{
		if (preparedReading == null || _readingCardContainer == null) return;

		if (_readingCardContainer.cardImage != null)
		{
			if (preparedReading.cardIcon != null)
			{
				_readingCardContainer.cardImage.sprite = preparedReading.cardIcon;
				_readingCardContainer.cardImage.rectTransform.localRotation = preparedReading.upright
					? Quaternion.identity
					: Quaternion.Euler(0, 0, 180);
				_readingCardContainer.cardImage.preserveAspect = true;
			}
		}

		if (_readingCardContainer.cardNameText != null)
			_readingCardContainer.cardNameText.text = preparedReading.cardDisplayName;

		if (_readingCardContainer.titleText != null)
			_readingCardContainer.titleText.text = preparedReading.oraclePayload?.title
				?? $"今日神谕 · {preparedReading.card?.nameZh}";

		if (_readingCardContainer.descriptText != null)
			_readingCardContainer.descriptText.text = preparedReading.cardDescription;
	}

	/// <summary>
	/// 翻牌时预热今日神谕和完整解读
	/// </summary>
	private void PreloadTodayReading(TarotCard card, bool upright)
	{
		EnsureDailyOracleService();

		if (_oracleService == null)
		{
			// 降级：使用本地模板
			PopulateOracleFields(BuildLocalFallback(card, upright));
			return;
		}

		// 检查是否有缓存（同一天同一张牌）
		if (_oracleService.CachedPayload != null
			&& _oracleService.IsCachedOracleFor(card, upright)
			&& !_oracleService.IsOracleLoading)
		{
			PopulateOracleFields(_oracleService.CachedPayload);
		}

		_oracleService.PreloadTodayReading(card, upright, (payload) =>
		{
			// 回到主线程更新 UI
			if (this != null && gameObject != null && gameObject.activeInHierarchy)
			{
				PopulateOracleFields(payload);
			}
		});
	}

	/// <summary>
	/// 从 TodayOraclePayload 填充 UI 的今日宜/不宜/神谕等字段
	/// </summary>
	private void PopulateOracleFields(TodayOraclePayload payload)
	{
		if (payload == null || _readingCardContainer == null) return;

		// 标题（AI 生成的可覆盖默认）
		if (_readingCardContainer.titleText != null && !string.IsNullOrEmpty(payload.title))
			_readingCardContainer.titleText.text = payload.title;

		// 描述 → 用 AI 的 detail
		if (_readingCardContainer.descriptText != null && !string.IsNullOrEmpty(payload.detail))
			_readingCardContainer.descriptText.text = payload.detail;
		
	}






	/// <summary>
	/// AI 不可用时的本地降级模板
	/// </summary>
	private TodayOraclePayload BuildLocalFallback(TarotCard card, bool upright)
	{
		var arcanaLabel = card.arcana == "major" ? "大阿卡纳" : "小阿卡纳";
		var elementLabel = MapElementZh(card.element);
		var keywords = card.keywords != null && card.keywords.Count > 0
			? string.Join("、", card.keywords)
			: "内在觉知";

		string detail;
		if (upright)
		{
			detail = $"今天抽到了{card.nameZh}（正位），这张牌属于{arcanaLabel}，由{elementLabel}能量引导。"
				+ $"它的关键词是{keywords}。正位的{card.nameZh}提醒你，有时候答案并不在外面，"
				+ $"而在你安静下来的那一刻，心底浮现的第一个声音里。";
		}
		else
		{
			detail = $"逆位的{card.nameZh}来到了你今天的牌面。这张{arcanaLabel}的牌由{elementLabel}能量守护，"
				+ $"关键词是{keywords}。逆位并不代表坏消息，而是一个温柔但坚定的提醒："
				+ $"有些被忽略的东西正在月光下浮现，请正视它。";
		}

		return new TodayOraclePayload
		{
			title = $"今日神谕 · {card.nameZh}",
			oracle = upright
				? $"{card.nameZh}的正位能量提示你，今天适合迈出第一步。"
				: $"逆位的{card.nameZh}提醒你放慢脚步，回看内心。",
			detail = detail,
			dos = new List<string>(),
			donts = new List<string>(),
			microAction = ""
		};
	}

	private TodayOraclePreparedReading BuildLocalPreparedReading(TarotCard card, bool upright)
	{
		var oraclePayload = BuildLocalFallback(card, upright);
		return new TodayOraclePreparedReading
		{
			card = card,
			upright = upright,
			cardId = card.cardId,
			cardDisplayName = card.DisplayName(upright),
			cardDescription = oraclePayload.detail,
			cardMeaning = oraclePayload.oracle,
			cardIcon = TarotSpriteLoader.Load(card.cardId),
			cardPayload = _divinationEngine?.GetTodayCardPayload(),
			oraclePayload = oraclePayload,
			interpretationPayload = null,
			preparedAt = System.DateTime.Now.ToString("o")
		};
	}

	private void RevealPreparedReading(TodayOraclePreparedReading preparedReading)
	{
		if (preparedReading == null) return;

		_currentCard = preparedReading.card;
		_currentUpright = preparedReading.upright;

		if (_readingCardContainer != null)
			PopulateReadingCardContainer(preparedReading);

		if (preparedReading.oraclePayload != null)
			PopulateOracleFields(preparedReading.oraclePayload);

		DialogSystem.Instance?.SetTodayCardPayload(
			preparedReading.cardPayload ?? _divinationEngine?.GetTodayCardPayload());

		uiComponent.ReadingCardContainerTransform.gameObject.SetActive(true);
		UIModule.Instance.PopUpWindow<OracleReadingUI>();
		Debug.Log($"[TodayOracleUI] 翻牌展示: {preparedReading.cardDisplayName}");
	}

	private void StopPrepareFlipCoroutine()
	{
		if (_prepareFlipCoroutine != null)
		{
			uiComponent.StopCoroutine(_prepareFlipCoroutine);
			_prepareFlipCoroutine = null;
		}
		HideLoadingText();
		_isPreparingFlip = false;
	}

	private void ShowLoadingText(TarotCard card)
	{
		_loadingTextUI = UIModule.Instance.PopUpWindow<LoadingTextUI>();
		if (_loadingTextUI != null)
			_loadingTextUI.SetReadingCardText(card);
	}

	private void HideLoadingText()
	{
		if (_loadingTextUI != null)
		{
			_loadingTextUI.HideWindow();
			_loadingTextUI = null;
		}
	}

	/// <summary>
	/// 元素映射为中文
	/// </summary>
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

	private void EnsureDailyOracleService()
	{
		if (_oracleService != null) return;

		_oracleService = DailyOracleService.Instance;
		if (_oracleService == null)
		{
			var go = new GameObject("DailyOracleService");
			_oracleService = go.AddComponent<DailyOracleService>();
		}
	}

	#endregion
}
