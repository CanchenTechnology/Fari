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
using UnityEngine.Video;
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
	private Coroutine _restoreTodayCoroutine;
	private LoadingTextUI _loadingTextUI;
	private RenderTexture _idleVideoTextureInstance;
	private RenderTexture _sharedIdleVideoTexture;
	private int _tomorrowHookRequestId;
	private int _todayStateRequestId;
	private int _idleVideoPlayRequestId;
	private bool _isRestoringToday;
	private bool _idleVideoHasStarted;

	[SerializeField] private float flipRevealDelaySeconds = 1.2f;

	#region 生命周期函数
	public override void OnAwake()
	{
		uiComponent = ResolveUiComponent();
		uiComponent.InitComponent(this);
		ResolveTodayStateContentReferences();
		ConfigureIdleVideoPlayer();
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
			if (_readingCardContainer != null)
				_readingCardContainer.RefreshButtonBindings();
		}

		// 初始化 DailyOracleService
		EnsureDailyOracleService();
	}

	public override void OnShow()
	{
		base.OnShow();
		RefreshReadingCardContainerActions();
		PlayIdleVideo();
		LoadDueTomorrowHooks();
		RefreshTodayOracleState();
	}

	public override void OnHide()
	{
		_tomorrowHookRequestId++;
		_todayStateRequestId++;
		StopRestoreTodayCoroutine();
		PauseIdleVideo();
		base.OnHide();
	}

	public override void OnDestroy()
	{
		_todayStateRequestId++;
		StopRestoreTodayCoroutine();
		StopPrepareFlipCoroutine();
		ReleaseIdleVideoTextureInstance();
		base.OnDestroy();
	}
	#endregion

	#region API Function

	private TodayOracleUIComponent ResolveUiComponent()
	{
		TodayOracleUIComponent[] components = gameObject.GetComponents<TodayOracleUIComponent>();
		if (components == null || components.Length == 0)
			return null;

		foreach (TodayOracleUIComponent component in components)
		{
			if (component != null
				&& component.idleVideoPlayer != null
				&& component.flipCardButton != null
				&& component.ReadingCardContainerTransform != null)
			{
				return component;
			}
		}

		return components[0];
	}

	private void ResolveTodayStateContentReferences()
	{
		if (uiComponent == null) return;

		if (uiComponent.filpContent == null)
			uiComponent.filpContent = FindTransformByName(transform,
				"[Transform]filpContent",
				"filpContent",
				"FlipContent",
				"UndrawnContent",
				"NotDrawnContent",
				"TodayUndrawnContent",
				"未抽卡");
		if (uiComponent.filpedContent == null)
			uiComponent.filpedContent = FindTransformByName(transform,
				"[Transform]filpedContent",
				"filpedContent",
				"FlippedContent",
				"DrawnContent",
				"TodayDrawnContent",
				"已抽卡");

		if (uiComponent.filpContent == null && uiComponent.flipCardButton != null)
			uiComponent.filpContent = uiComponent.flipCardButton.transform;
		if (uiComponent.filpedContent == null && uiComponent.ReadingCardContainerTransform != null)
			uiComponent.filpedContent = uiComponent.ReadingCardContainerTransform;
	}

	private void ConfigureIdleVideoPlayer()
	{
		VideoPlayer videoPlayer = uiComponent?.idleVideoPlayer;
		if (videoPlayer == null) return;

		EnsureIdleVideoTextureInstance(videoPlayer);
		videoPlayer.playOnAwake = false;
		videoPlayer.waitForFirstFrame = true;
		videoPlayer.skipOnDrop = false;
		videoPlayer.isLooping = true;
	}

	private void EnsureIdleVideoTextureInstance(VideoPlayer videoPlayer)
	{
		if (videoPlayer == null || _idleVideoTextureInstance != null)
			return;

		RenderTexture sourceTexture = videoPlayer.targetTexture;
		if (sourceTexture == null)
			return;

		_sharedIdleVideoTexture = sourceTexture;
		var descriptor = sourceTexture.descriptor;
		_idleVideoTextureInstance = new RenderTexture(descriptor)
		{
			name = $"{sourceTexture.name}_TodayOracleRuntime",
			filterMode = sourceTexture.filterMode,
			wrapMode = sourceTexture.wrapMode,
			anisoLevel = sourceTexture.anisoLevel,
		};
		_idleVideoTextureInstance.Create();

		videoPlayer.targetTexture = _idleVideoTextureInstance;
		ApplyIdleVideoTextureToRawImages(_sharedIdleVideoTexture, _idleVideoTextureInstance);
	}

	private void ApplyIdleVideoTextureToRawImages(Texture fromTexture, Texture toTexture)
	{
		if (fromTexture == null || toTexture == null)
			return;

		RawImage[] rawImages = gameObject.GetComponentsInChildren<RawImage>(true);
		if (rawImages == null)
			return;

		foreach (RawImage rawImage in rawImages)
		{
			if (rawImage != null && rawImage.texture == fromTexture)
				rawImage.texture = toTexture;
		}
	}

	private void ReleaseIdleVideoTextureInstance()
	{
		if (_idleVideoTextureInstance == null)
			return;

		if (uiComponent?.idleVideoPlayer != null && uiComponent.idleVideoPlayer.targetTexture == _idleVideoTextureInstance)
			uiComponent.idleVideoPlayer.targetTexture = _sharedIdleVideoTexture;

		ApplyIdleVideoTextureToRawImages(_idleVideoTextureInstance, _sharedIdleVideoTexture);
		_idleVideoTextureInstance.Release();
		UnityEngine.Object.Destroy(_idleVideoTextureInstance);
		_idleVideoTextureInstance = null;
		_sharedIdleVideoTexture = null;
	}

	private void PlayIdleVideo()
	{
		VideoPlayer videoPlayer = uiComponent?.idleVideoPlayer;
		if (videoPlayer == null)
		{
			Debug.LogWarning("[TodayOracleUI] idleVideoPlayer is not assigned.");
			return;
		}

		ConfigureIdleVideoPlayer();

		if (videoPlayer.isPlaying || _idleVideoCoroutine != null)
			return;

		_idleVideoCoroutine = uiComponent.StartCoroutine(PlayIdleVideoRoutine(++_idleVideoPlayRequestId));
	}

	private void PauseIdleVideo()
	{
		_idleVideoPlayRequestId++;
		if (_idleVideoCoroutine != null)
		{
			uiComponent.StopCoroutine(_idleVideoCoroutine);
			_idleVideoCoroutine = null;
		}

		var videoPlayer = uiComponent?.idleVideoPlayer;
		if (videoPlayer != null && videoPlayer.isPlaying)
			videoPlayer.Pause();
	}

	private IEnumerator PlayIdleVideoRoutine(int requestId)
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

		ConfigureIdleVideoPlayer();

		if (!videoPlayer.isPrepared)
		{
			videoPlayer.Prepare();
			while (!videoPlayer.isPrepared)
			{
				if (requestId != _idleVideoPlayRequestId)
				{
					_idleVideoCoroutine = null;
					yield break;
				}
				yield return null;
			}
		}

		if (requestId != _idleVideoPlayRequestId)
		{
			_idleVideoCoroutine = null;
			yield break;
		}

		if (!_idleVideoHasStarted || (videoPlayer.length > 0 && videoPlayer.time >= videoPlayer.length - 0.05f))
		{
			videoPlayer.time = 0;
			_idleVideoHasStarted = true;
		}

		videoPlayer.Play();
		_idleVideoCoroutine = null;
	}

	private void LoadDueTomorrowHooks()
	{
		FirestoreManager firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
			return;

		int requestId = ++_tomorrowHookRequestId;
		firestore.LoadDueTomorrowHooks(hooks =>
		{
			if (requestId != _tomorrowHookRequestId || !gameObject.activeInHierarchy)
				return;
			if (hooks == null || hooks.Count == 0)
				return;

			MergeTomorrowHooksToMemory(hooks);
			AppNotificationScheduler.Instance.NotifyTomorrowHookCount(hooks.Count);
			ToastManager.ShowToast(hooks.Count == 1
				? "昨天保存的线索可以回看了"
				: $"有 {hooks.Count} 条明日线索可以回看");
		});
	}

	private void MergeTomorrowHooksToMemory(List<TomorrowHook> hooks)
	{
		DialogSystem dialog = DialogSystem.Instance;
		if (dialog == null || hooks == null || hooks.Count == 0) return;

		MemorySource source = dialog.GetMemorySource() ?? new MemorySource();
		source.tomorrowHooks ??= new List<TomorrowHook>();
		foreach (TomorrowHook hook in hooks)
		{
			if (hook == null || string.IsNullOrWhiteSpace(hook.hookId)) continue;
			int index = source.tomorrowHooks.FindIndex(item => item != null && item.hookId == hook.hookId);
			if (index >= 0)
				source.tomorrowHooks[index] = hook;
			else
				source.tomorrowHooks.Add(hook);
		}

		dialog.SetMemorySource(source);
	}

	#endregion

	#region UI组件事件
		public void OnDeepChatButtonClick()
		{
			// 先翻牌（如果还没翻）
			if (_divinationEngine != null && !_divinationEngine.TodayCard.HasValue)
			{
				if (!MembershipGate.CanUse(MembershipFeature.DailyOracle)) return;
				_divinationEngine.DrawDailyCard();
			}

		// 把今日牌数据同步到 DialogSystem
		if (_divinationEngine?.TodayCard.HasValue == true)
		{
			var payload = BuildTodayCardPayloadForDialog();
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
			if (_isPreparingFlip || _isRestoringToday) return;
			if (TryRestoreSavedToday(DailyOracleFirestore.LoadTodayLocal()))
				return;
			if (TryRevealConsumedTodayFallback(false))
				return;

			if (_divinationEngine != null && !_divinationEngine.TodayCard.HasValue
				&& !MembershipGate.CanUse(MembershipFeature.DailyOracle))
			{
				return;
			}

			StartDrawCardAnimation();
		}

	private IEnumerator PrepareAndRevealTodayCardRoutine()
	{
		if (_divinationEngine == null)
		{
			_isPreparingFlip = false;
			yield break;
		}

		var (card, upright) = _divinationEngine.DrawDailyCard();
		_currentCard = card;
		_currentUpright = upright;
		Debug.Log($"[TodayOracleUI] 翻牌准备: {card.nameZh} ({(upright ? "正位" : "逆位")})");
		DailyOracleFirestore.SaveTodayLocalPending(card, upright, BuildLocalFallback(card, upright), DailyOracleService.CurrentLocale);

		yield return PrepareAndRevealTodayCardRoutine(card, upright, flipRevealDelaySeconds);
	}

	private IEnumerator PrepareAndRevealTodayCardRoutine(TarotCard card, bool upright, float minimumRevealDelaySeconds)
	{
		_isPreparingFlip = true;
		ApplyPreparingState();

		if (card == null)
		{
			_isPreparingFlip = false;
			_prepareFlipCoroutine = null;
			yield break;
		}

		_currentCard = card;
		_currentUpright = upright;
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
		float minDelay = Mathf.Max(0f, minimumRevealDelaySeconds);
		while (elapsed < minDelay || !preparedReady)
		{
			elapsed += Time.deltaTime;
			yield return null;
		}

		if (preparedReading == null)
			preparedReading = BuildLocalPreparedReading(card, upright);

		HideLoadingText();
		RevealPreparedReading(preparedReading, true);
		_isPreparingFlip = false;
		_prepareFlipCoroutine = null;
	}

	public void StartDrawCardAnimation()
	{
		_isPreparingFlip = true;
		ApplyPreparingState();

		DrawCardUI.Prepare(DrawDailyCardForAnimation, OnDrawCardAnimationCompleted, OnDrawCardAnimationCanceled);
		DrawCardUI drawWindow = UIModule.Instance.PopUpWindow<DrawCardUI>();
		if (drawWindow == null)
		{
			Debug.LogWarning("[TodayOracleUI] DrawCardUI 打开失败，回退到直接翻牌流程");
			DrawCardUI.Prepare(null, null, null);
			_prepareFlipCoroutine = uiComponent.StartCoroutine(PrepareAndRevealTodayCardRoutine());
		}
	}

	private (TarotCard card, bool upright) DrawDailyCardForAnimation()
	{
		if (_divinationEngine == null)
		{
			GameObject engineObject = new GameObject("DivinationEngine");
			_divinationEngine = engineObject.AddComponent<DivinationEngine>();
		}

		var result = _divinationEngine.DrawDailyCard();
		_currentCard = result.card;
		_currentUpright = result.upright;

		if (result.card != null)
		{
			DailyOracleFirestore.SaveTodayLocalPending(result.card, result.upright,
				BuildLocalFallback(result.card, result.upright), DailyOracleService.CurrentLocale);
			Debug.Log($"[TodayOracleUI] 抽卡动画锁定今日牌: {result.card.nameZh} ({(result.upright ? "正位" : "逆位")})");
		}

		return result;
	}

	private void OnDrawCardAnimationCompleted(TarotCard card, bool upright)
	{
		if (card == null)
		{
			OnDrawCardAnimationCanceled();
			return;
		}

		_prepareFlipCoroutine = uiComponent.StartCoroutine(PrepareAndRevealTodayCardRoutine(card, upright, 0.15f));
	}

	private void OnDrawCardAnimationCanceled()
	{
		_isPreparingFlip = false;
		_prepareFlipCoroutine = null;
		HideLoadingText();
		ApplyUndrawnState();
	}

	private void PopulateReadingCardContainer(TarotCard card, bool upright)
	{
		if (_readingCardContainer == null || card == null) return;

		_readingCardContainer.SetCard(card, upright);

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
			_readingCardContainer.titleText.text = BuildReadingTitle(card, null);

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
		_readingCardContainer.SetCard(preparedReading.card, preparedReading.upright);

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
			_readingCardContainer.titleText.text = BuildReadingTitle(preparedReading.card, preparedReading.oraclePayload);

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
			_readingCardContainer.titleText.text = CleanTodayOracleTitle(payload.title, _currentCard?.nameZh);

		// 描述 → 用 AI 的 detail
		if (_readingCardContainer.descriptText != null && !string.IsNullOrEmpty(payload.detail))
			_readingCardContainer.descriptText.text = payload.detail;
		
	}

	private static string BuildReadingTitle(TarotCard card, TodayOraclePayload payload)
	{
		return CleanTodayOracleTitle(payload?.title, card != null ? card.nameZh : string.Empty);
	}

	private static string CleanTodayOracleTitle(string title, string fallback)
	{
		string cleaned = FirstNonEmpty(title, fallback).Trim();
		if (string.IsNullOrEmpty(cleaned))
			return string.Empty;

		string[] prefixes =
		{
			"今日神谕 ·",
			"今日神谕·",
			"今日神谕：",
			"今日神谕:",
			"今日标题：",
			"今日标题:"
		};

		foreach (string prefix in prefixes)
		{
			if (!cleaned.StartsWith(prefix, System.StringComparison.Ordinal))
				continue;

			cleaned = cleaned.Substring(prefix.Length).Trim();
			break;
		}

		return string.IsNullOrEmpty(cleaned) ? FirstNonEmpty(fallback, title).Trim() : cleaned;
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
			title = card.nameZh,
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
			cardPayload = BuildTodayCardPayloadForDialog(card, upright, oraclePayload),
			oraclePayload = oraclePayload,
			interpretationPayload = null,
			preparedAt = System.DateTime.Now.ToString("o")
		};
	}

	private void RevealPreparedReading(TodayOraclePreparedReading preparedReading, bool openReadingWindow)
	{
		if (preparedReading == null) return;

		_currentCard = preparedReading.card;
		_currentUpright = preparedReading.upright;

		if (_readingCardContainer != null)
			PopulateReadingCardContainer(preparedReading);

		if (preparedReading.oraclePayload != null)
			PopulateOracleFields(preparedReading.oraclePayload);

		DialogSystem.Instance?.SetTodayCardPayload(
			BuildTodayCardPayloadForDialog(preparedReading.card, preparedReading.upright, preparedReading.oraclePayload)
			?? preparedReading.cardPayload
			?? _divinationEngine?.GetTodayCardPayload());

		ApplyDrawnState();
		DailyOracleHistoryBridge.SavePreparedReading(preparedReading, true);
		if (openReadingWindow)
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

	private void StopRestoreTodayCoroutine()
	{
		if (_restoreTodayCoroutine != null)
		{
			uiComponent.StopCoroutine(_restoreTodayCoroutine);
			_restoreTodayCoroutine = null;
		}
		_isRestoringToday = false;
	}

	private void RefreshTodayOracleState()
	{
		int requestId = ++_todayStateRequestId;
		StopRestoreTodayCoroutine();
		_restoreTodayCoroutine = uiComponent.StartCoroutine(RestoreTodayOracleStateRoutine(requestId));
	}

	private IEnumerator RestoreTodayOracleStateRoutine(int requestId)
	{
		_isRestoringToday = true;
		ApplyPreparingState();

		if (TryRestoreSavedToday(DailyOracleFirestore.LoadTodayLocal())
			|| TryRevealInMemoryToday()
			|| TryRevealCurrentTodayFallback())
		{
			FinishRestoreTodayRoutine();
			yield break;
		}

		DailyOracleCloudRecord cloudRecord = null;
		DailyOracleFirestore store = DailyOracleFirestore.Instance;
		if (store != null)
		{
			float elapsed = 0f;
			while (!store.IsReady && elapsed < 2f)
			{
				if (requestId != _todayStateRequestId || !gameObject.activeInHierarchy)
					yield break;

				elapsed += Time.deltaTime;
				yield return null;
			}

			bool loaded = false;
			store.LoadToday(record =>
			{
				cloudRecord = record;
				loaded = true;
			});

			float loadElapsed = 0f;
			while (!loaded && loadElapsed < 5f)
			{
				if (requestId != _todayStateRequestId || !gameObject.activeInHierarchy)
					yield break;

				loadElapsed += Time.deltaTime;
				yield return null;
			}
		}

		if (requestId != _todayStateRequestId || !gameObject.activeInHierarchy)
			yield break;

		if (!TryRestoreSavedToday(cloudRecord) && !TryRevealConsumedTodayFallback(false))
			ApplyUndrawnState();

		FinishRestoreTodayRoutine();
	}

	private void FinishRestoreTodayRoutine()
	{
		_isRestoringToday = false;
		_restoreTodayCoroutine = null;
	}

	private bool TryRevealInMemoryToday()
	{
		if (_divinationEngine?.TodayCard.HasValue != true)
			return false;

		var (card, upright) = _divinationEngine.TodayCard.Value;
		EnsureDailyOracleService();

		if (_oracleService != null && _oracleService.IsCachedPreparedReadingFor(card, upright))
		{
			RevealPreparedReading(_oracleService.CachedPreparedReading, false);
			return true;
		}

		if (_oracleService != null && _oracleService.IsCachedOracleFor(card, upright))
		{
			RevealPreparedReading(BuildPreparedReadingFromPayload(card, upright, _oracleService.CachedPayload), false);
			return true;
		}

		return false;
	}

	private bool TryRevealCurrentTodayFallback()
	{
		if (_divinationEngine?.TodayCard.HasValue != true)
			return false;

		var (card, upright) = _divinationEngine.TodayCard.Value;
		DailyOracleFirestore.SaveTodayLocalPending(card, upright, BuildLocalFallback(card, upright), DailyOracleService.CurrentLocale);
		RevealPreparedReading(BuildLocalPreparedReading(card, upright), false);
		return true;
	}

	private bool TryRevealConsumedTodayFallback(bool openReadingWindow)
	{
		if (!HasUsedDailyOracleToday() || _divinationEngine == null)
			return false;

		var (card, upright) = _divinationEngine.TodayCard.HasValue
			? _divinationEngine.TodayCard.Value
			: _divinationEngine.DrawDailyCard();

		DailyOracleFirestore.SaveTodayLocalPending(card, upright, BuildLocalFallback(card, upright), DailyOracleService.CurrentLocale);
		RevealPreparedReading(BuildLocalPreparedReading(card, upright), openReadingWindow);
		Debug.LogWarning($"[TodayOracleUI] 今日牌已用过但缓存缺失，已用本地模板重建: {card.nameZh} ({(upright ? "正位" : "逆位")})");
		return true;
	}

	private bool TryRestoreSavedToday(DailyOracleCloudRecord record)
	{
		EnsureDailyOracleService();
		if (_oracleService == null || !_oracleService.TryRestoreFromRecord(record, out TodayOraclePreparedReading preparedReading))
			return false;

		RevealPreparedReading(preparedReading, false);
		return true;
	}

	private TodayOraclePreparedReading BuildPreparedReadingFromPayload(TarotCard card, bool upright, TodayOraclePayload payload)
	{
		TodayOraclePayload fallback = BuildLocalFallback(card, upright);
		TodayOraclePayload safePayload = payload ?? fallback;
		return new TodayOraclePreparedReading
		{
			card = card,
			upright = upright,
			cardId = card.cardId,
			cardDisplayName = card.DisplayName(upright),
			cardDescription = FirstNonEmpty(safePayload.detail, fallback.detail),
			cardMeaning = FirstNonEmpty(safePayload.oracle, fallback.oracle),
			cardIcon = TarotSpriteLoader.Load(card.cardId),
			cardPayload = BuildTodayCardPayloadForDialog(card, upright, safePayload),
			oraclePayload = safePayload,
			interpretationPayload = null,
			preparedAt = System.DateTime.Now.ToString("o")
		};
	}

	private void ApplyUndrawnState()
	{
		ApplyTodayCardState(false);
	}

	private void ApplyDrawnState()
	{
		ApplyTodayCardState(true);
		RefreshReadingCardContainerActions();
	}

	private void ApplyPreparingState()
	{
		SetUndrawnContentVisible(false);
		SetDrawnContentVisible(false);
		SetFlipButtonVisible(false);
		SetReadingContainerVisible(false);
	}

	private void ApplyTodayCardState(bool hasDrawnCard)
	{
		SetUndrawnContentVisible(!hasDrawnCard);
		SetDrawnContentVisible(hasDrawnCard);
		SetFlipButtonVisible(!hasDrawnCard);
		SetReadingContainerVisible(hasDrawnCard);
	}

	private void RefreshReadingCardContainerActions()
	{
		if (_readingCardContainer == null) return;
		_readingCardContainer.RefreshButtonBindings();
		if (_currentCard != null)
			_readingCardContainer.SetCard(_currentCard, _currentUpright);
	}

	private bool HasUsedDailyOracleToday()
	{
		return UsageStatsManager.Instance != null
			&& UsageStatsManager.Instance.HasUsedDailyOracleToday();
	}

	private void SetFlipButtonVisible(bool visible)
	{
		if (uiComponent?.flipCardButton != null)
			uiComponent.flipCardButton.gameObject.SetActive(visible);
	}

	private void SetReadingContainerVisible(bool visible)
	{
		if (uiComponent?.ReadingCardContainerTransform != null)
			uiComponent.ReadingCardContainerTransform.gameObject.SetActive(visible);
	}

	private void SetUndrawnContentVisible(bool visible)
	{
		if (uiComponent?.filpContent != null)
			uiComponent.filpContent.gameObject.SetActive(visible);
	}

	private void SetDrawnContentVisible(bool visible)
	{
		if (uiComponent?.filpedContent != null)
			uiComponent.filpedContent.gameObject.SetActive(visible);
	}

	private static Transform FindTransformByName(Transform root, params string[] names)
	{
		if (root == null || names == null) return null;
		for (int i = 0; i < names.Length; i++)
		{
			if (root.name == names[i])
				return root;
		}

		for (int i = 0; i < root.childCount; i++)
		{
			Transform result = FindTransformByName(root.GetChild(i), names);
			if (result != null)
				return result;
		}

		return null;
	}

	private static string FirstNonEmpty(params string[] values)
	{
		if (values == null) return "";
		foreach (string value in values)
		{
			if (!string.IsNullOrWhiteSpace(value))
				return value;
		}
		return "";
	}

	private TodayCardPayload BuildTodayCardPayloadForDialog()
	{
		if (_currentCard != null)
			return BuildTodayCardPayloadForDialog(_currentCard, _currentUpright, GetCachedOraclePayloadFor(_currentCard, _currentUpright));

		if (_divinationEngine?.TodayCard.HasValue == true)
		{
			var (card, upright) = _divinationEngine.TodayCard.Value;
			return BuildTodayCardPayloadForDialog(card, upright, GetCachedOraclePayloadFor(card, upright));
		}

		return null;
	}

	private TodayOraclePayload GetCachedOraclePayloadFor(TarotCard card, bool upright)
	{
		EnsureDailyOracleService();
		if (_oracleService == null || card == null) return null;

		if (_oracleService.IsCachedOracleFor(card, upright))
			return _oracleService.CachedPayload;
		if (_oracleService.IsCachedPreparedReadingFor(card, upright))
			return _oracleService.CachedPreparedReading?.oraclePayload;

		return null;
	}

	private TodayCardPayload BuildTodayCardPayloadForDialog(TarotCard card, bool upright, TodayOraclePayload oraclePayload)
	{
		if (card == null) return null;

		EnsureDailyOracleService();
		if (_oracleService != null && _oracleService.IsSameCurrentCard(card, upright))
		{
			TodayCardPayload servicePayload = _oracleService.GetTodayCardPayload();
			if (servicePayload != null)
				return servicePayload;
		}

		TodayCardPayload payload = null;
		string orientation = upright ? "upright" : "reversed";
		TodayCardPayload enginePayload = _divinationEngine?.GetTodayCardPayload();
		if (enginePayload != null && enginePayload.cardId == card.cardId && enginePayload.orientation == orientation)
		{
			payload = enginePayload;
		}

		if (payload == null)
		{
			payload = new TodayCardPayload
			{
				cardId = card.cardId,
				cardName = card.nameEn,
				displayName = card.DisplayName(upright),
				nameZh = card.nameZh,
				orientation = orientation,
				generatedAt = System.DateTime.Now.ToString("o"),
				title = "今日塔罗"
			};
		}

		payload.oracleText = FirstNonEmpty(payload.oracleText, oraclePayload?.oracle);
		payload.title = FirstNonEmpty(payload.title, oraclePayload?.title, "今日塔罗");
		return payload;
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
