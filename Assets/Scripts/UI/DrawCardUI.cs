/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/26/2026 1:52:11 PM
 * Description: 每日塔罗抽卡动画界面
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

public class DrawCardUI : WindowBase
{
	private const float DeckClipHorizontalPadding = 24f;
	private const float DeckVisibleSlotCount = 7f;
	private const float DeckCardSpacing = 12f;
	private const int VisualDeckCardCount = 12;
	private const int ShuffleDeckCardCount = 52;
	private const float PostShuffleFanCardSpacingRatio = 0.62f;
	private const int PostShuffleFanVisualCardCount = 34;
	private const float PostShuffleFanMinViewportMultiplier = 1.08f;
	private const float PostShuffleFanMaxViewportMultiplier = 1.55f;
	private const float PostShuffleFanBaseHeightRatio = -0.16f;
	private const float PostShuffleFanRiseRatio = 0.085f;
	private const float PostShuffleFanMaxRotation = 10.5f;
	private const float DeckIntroDuration = 0.38f;
	private const float DeckChaosDuration = 1.6f;
	private const float DeckWeaveDuration = 2.15f;
	private const float DeckTripleCutDuration = 3f;
	private const float DeckGatherDuration = 0.83f;

	public DrawCardUIComponent uiComponent;

	public static Func<(TarotCard card, bool upright)> PendingDrawProvider;
	public static Action<TarotCard, bool> PendingDrawCompleted;
	public static Action PendingDrawCanceled;

	private readonly List<Image> _cardImages = new List<Image>();
	private readonly List<Image> _shuffleVisualCards = new List<Image>();
	private readonly List<Image> _shuffleExtraImages = new List<Image>();
	private readonly List<Vector2> _fanPositions = new List<Vector2>();
	private readonly List<Image> _sparkImages = new List<Image>();

	private RectTransform _contentRoot;
	private RectTransform _cardContainer;
	private RectTransform _deckClipRoot;
	private Image _cardTemplateImage;
	private TMP_Text _titleText;
	private TMP_Text _desText;
	private Sequence _flowSequence;
	private Tween _clickSuppressTween;
	private Tween _pullBackTween;
	private Tween _inertiaTween;
	private Tween _tapHintTween;
	private Coroutine _singleClickCoroutine;
	private Sprite _cachedCardBackSprite;
	private Coroutine _backgroundVideoCoroutine;
	private RenderTexture _backgroundVideoTextureInstance;
	private RenderTexture _sharedBackgroundVideoTexture;

	private bool _isChoosing;
	private bool _isAnimating;
	private bool _finishedOrCanceled;
	private bool _isDraggingDeck;
	private bool _dragMoved;
	private bool _suppressPointerClick;
	private bool _dragStartedFromCard;
	private bool _isScrollingDeck;
	private bool _isPullingCard;
	private bool _isInertiaScrolling;
	private bool _backgroundVideoHasStarted;
	private bool _isWaitingForExit;
	private bool _isWaitingForShuffleStart;

	private int _activeDragCardIndex = -1;
	private int _backgroundVideoPlayRequestId;
	private float _dragStartLocalX;
	private float _dragStartLocalY;
	private float _dragStartOffsetX;
	private float _deckOffsetX;
	private float _activePullY;
	private float _lastScrollSampleX;
	private float _lastScrollSampleTime;
	private float _scrollVelocityX;
	private float _viewportWidth;
	private float _viewportHeight;
	private float _fanWidth;
	private float _deckCardScale = 1f;
	private float _selectedCardScale = 0.9f;
	private float _minDeckOffsetX;
	private float _maxDeckOffsetX;
	private Vector2 _lastDeckViewportSize;
	private int _pendingClickCardIndex = -1;
	private float _lastCardClickTime = -100f;
	private TarotCard _completedCard;
	private bool _completedUpright = true;

	public static void Prepare(Func<(TarotCard card, bool upright)> drawProvider,
		Action<TarotCard, bool> onCompleted,
		Action onCanceled = null)
	{
		PendingDrawProvider = drawProvider;
		PendingDrawCompleted = onCompleted;
		PendingDrawCanceled = onCanceled;
	}

	#region 生命周期函数
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<DrawCardUIComponent>();
		if (uiComponent == null)
		{
			uiComponent = gameObject.AddComponent<DrawCardUIComponent>();
			Debug.LogWarning("[DrawCardUI] Prefab 未挂 DrawCardUIComponent，运行时已自动补齐。");
		}

		uiComponent.InitComponent(this);
		ConfigureDeckSettings();
		ResolveBackgroundVideoReference();
		ConfigureBackgroundVideoPlayer();
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}

	public override void OnShow()
	{
		base.OnShow();
		PlayBackgroundVideo();
		PlayDrawEntrance();
	}

	public override void OnHide()
	{
		if (!_finishedOrCanceled)
			CancelDraw(false);

		KillFlow();
		ClearRuntimeCards();
		PauseBackgroundVideo();
		base.OnHide();
	}

	public override void OnDestroy()
	{
		if (!_finishedOrCanceled)
			CancelDraw(false);

		KillFlow();
		ClearRuntimeCards();
		ReleaseBackgroundVideoTextureInstance();
		base.OnDestroy();
	}

	private void LateUpdate()
	{
		if (!_isChoosing || _isAnimating || _cardImages.Count <= 1)
			return;

		RefreshDeckLayoutIfViewportChanged();
		SanitizeDeckVisuals();
		if (_shuffleExtraImages.Count > 0)
			RestorePostShuffleFanSiblingOrder();
		else
			RestoreCardSiblingOrder();
	}
	#endregion

	#region API Function
	public void RestartDrawAnimation()
	{
		PlayDrawEntrance();
	}
	#endregion

	#region UI组件事件
	private void OnMaskClick()
	{
		if (_isWaitingForShuffleStart && !_isAnimating)
		{
			StartShuffleFromIntro();
			return;
		}

		if (_isWaitingForExit) return;
		if (_isAnimating || _suppressPointerClick) return;
		CancelDraw(true);
	}

	private void OnExitButtonClicked()
	{
		if (!_isWaitingForExit || _finishedOrCanceled) return;
		if (_completedCard == null) return;

		CompleteDraw(_completedCard, _completedUpright);
	}

	private void OnCardClicked(int index)
	{
		if (!_isChoosing || _isAnimating) return;
		if (_suppressPointerClick || _dragMoved || _isInertiaScrolling) return;

		SelectCardByIndex(index);
	}

	private void OnCardPointerClick(int index, BaseEventData eventData)
	{
		if (_isWaitingForShuffleStart && !_isAnimating)
		{
			StartShuffleFromIntro();
			return;
		}

		if (!_isChoosing || _isAnimating) return;
		if (_suppressPointerClick || _dragMoved || _isInertiaScrolling) return;

		float now = Time.unscaledTime;
		float doubleClickInterval = Mathf.Clamp(uiComponent.doubleClickSelectInterval, 0.05f, 0.6f);
		bool doubleClick = eventData is PointerEventData pointerData && pointerData.clickCount >= 2;
		doubleClick = doubleClick || (_pendingClickCardIndex == index && now - _lastCardClickTime <= doubleClickInterval);

		if (doubleClick)
		{
			StopPendingSingleClick();
			SelectCardByIndex(index);
			return;
		}

		StopPendingSingleClick();
		_pendingClickCardIndex = index;
		_lastCardClickTime = now;
		_singleClickCoroutine = uiComponent.StartCoroutine(SingleClickSelectRoutine(index, doubleClickInterval));
	}

	private IEnumerator SingleClickSelectRoutine(int index, float delay)
	{
		yield return new WaitForSecondsRealtime(delay);
		_singleClickCoroutine = null;
		_pendingClickCardIndex = -1;

		if (!_isChoosing || _isAnimating) yield break;
		if (_suppressPointerClick || _dragMoved || _isInertiaScrolling) yield break;
		SelectCardByIndex(index);
	}

	private void StopPendingSingleClick()
	{
		if (_singleClickCoroutine != null && uiComponent != null)
		{
			uiComponent.StopCoroutine(_singleClickCoroutine);
			_singleClickCoroutine = null;
		}
		else
		{
			_singleClickCoroutine = null;
		}

		_pendingClickCardIndex = -1;
	}
	#endregion

	private void PlayDrawEntrance()
	{
		KillFlow();
		ResetRuntimeState();
		ResolveRuntimeReferences();
		BuildCardFan();
		BuildShuffleVisualDeck();
		ResetTexts();
		ResetTargetCard();

		PlayDeckIntro();
	}

	private void PlayDeckIntro()
	{
		_isAnimating = true;
		_flowSequence = DOTween.Sequence();
		IList<Image> shuffleCards = GetShuffleCards();

		for (int i = 0; i < shuffleCards.Count; i++)
		{
			Image image = shuffleCards[i];
			if (image == null) continue;

			RectTransform rect = image.rectTransform;
			Vector2 targetPosition = GetStackPose(i, shuffleCards.Count);
			float targetRotation = GetStackRotation(i);
			image.enabled = true;
			image.raycastTarget = false;
			Color startColor = image.color;
			startColor.a = 0f;
			image.color = startColor;

			rect.anchoredPosition = targetPosition + new Vector2(0f, -24f);
			rect.localRotation = Quaternion.identity;
			rect.localScale = Vector3.one * (_deckCardScale * 0.94f);

			float startTime = i * 0.004f;
			_flowSequence.Insert(startTime, image.DOFade(1f, DeckIntroDuration * 0.75f));
			_flowSequence.Insert(startTime, rect.DOAnchorPos(targetPosition, DeckIntroDuration).SetEase(Ease.OutCubic));
			_flowSequence.Insert(startTime, rect.DORotate(new Vector3(0f, 0f, targetRotation), DeckIntroDuration).SetEase(Ease.OutCubic));
			_flowSequence.Insert(startTime, rect.DOScale(Vector3.one * _deckCardScale, DeckIntroDuration).SetEase(Ease.OutCubic));
		}

		RestoreShuffleCardSiblingOrder();

		if (_desText != null)
		{
			_desText.gameObject.SetActive(true);
			_desText.text = "轻触牌堆以洗牌";
			_desText.alpha = 0f;
			_flowSequence.Insert(0.12f, _desText.DOFade(1f, 0.25f));
		}

		_flowSequence.OnComplete(() =>
		{
			_isAnimating = false;
			_isWaitingForShuffleStart = true;
			SetIntroCardsInteractable(true);
			StartTapHintPulse();
			_flowSequence = null;
		});
	}

	private void StartShuffleFromIntro()
	{
		if (!_isWaitingForShuffleStart || _isAnimating) return;

		_isWaitingForShuffleStart = false;
		_isAnimating = true;
		StopTapHintPulse();
		SetIntroCardsInteractable(false);
		SetPromptText("让牌在你的问题里重新排序", 1f);
		PlayChaosAnimation(() => PlayWeaveAnimation(() => PlayTripleCutAnimation(() =>
			PlayGatherAnimation(PlayFanOutFromCurrentPose))));
	}

	private void PlayChaosAnimation(Action onComplete)
	{
		_flowSequence?.Kill(false);
		_flowSequence = DOTween.Sequence();
		IList<Image> shuffleCards = GetShuffleCards();

		for (int i = 0; i < shuffleCards.Count; i++)
		{
			Image image = shuffleCards[i];
			if (image == null) continue;

			RectTransform rect = image.rectTransform;
			float delay = Mathf.Lerp(0f, 0.18f, Hash01(i * 23 + 5));
			float duration = Mathf.Max(0.58f, DeckChaosDuration - delay - Mathf.Lerp(0.04f, 0.16f, Hash01(i * 19 + 7)));
			Vector2 targetPosition = GetChaosPose(i, shuffleCards.Count);
			float targetRotation = GetChaosRotation(i);

			_flowSequence.Insert(delay, rect.DOAnchorPos(targetPosition, duration)
				.SetEase(Ease.InOutCubic));
			_flowSequence.Insert(delay, rect.DORotate(new Vector3(0f, 0f, targetRotation), duration)
				.SetEase(Ease.InOutCubic));
			_flowSequence.Insert(delay, rect.DOScale(Vector3.one * _deckCardScale, duration)
				.SetEase(Ease.InOutCubic));
		}

		_flowSequence.AppendInterval(Mathf.Max(0f, DeckChaosDuration - _flowSequence.Duration()));
		_flowSequence.OnComplete(() =>
		{
			RestoreShuffleCardSiblingOrder();
			_flowSequence = null;
			onComplete?.Invoke();
		});
	}

	private void PlayWeaveAnimation(Action onComplete)
	{
		_flowSequence?.Kill(false);
		_flowSequence = DOTween.Sequence();
		IList<Image> shuffleCards = GetShuffleCards();
		RestoreWeaveCardSiblingOrder(shuffleCards);
		int half = Mathf.Max(1, (shuffleCards.Count + 1) / 2);
		float firstLiftTime = DeckWeaveDuration * 0.2f;
		float centerTime = DeckWeaveDuration * 0.5f;
		float secondLiftTime = DeckWeaveDuration * 0.75f;

		for (int i = 0; i < shuffleCards.Count; i++)
		{
			Image image = shuffleCards[i];
			if (image == null) continue;

			RectTransform rect = image.rectTransform;
			bool topPile = i < half;
			float phaseDelay = (i % 3) * 0.045f + i * 0.0008f;
			Vector2 centerPose = GetWeavePose(i, shuffleCards.Count, 0f);
			Vector2 firstLiftPose = GetWeavePose(i, shuffleCards.Count, topPile ? 1f : -1f);
			Vector2 secondLiftPose = GetWeavePose(i, shuffleCards.Count, topPile ? -1f : 1f);
			float splitRotation = topPile ? -7f : 7f;

			_flowSequence.Insert(phaseDelay, rect.DOAnchorPos(firstLiftPose, firstLiftTime)
				.SetEase(Ease.InOutSine));
			_flowSequence.Insert(phaseDelay, rect.DORotate(new Vector3(0f, 0f, splitRotation), firstLiftTime)
				.SetEase(Ease.InOutSine));

			_flowSequence.Insert(phaseDelay + firstLiftTime, rect.DOAnchorPos(centerPose, centerTime - firstLiftTime)
				.SetEase(Ease.InOutSine));
			_flowSequence.Insert(phaseDelay + firstLiftTime, rect.DORotate(Vector3.zero, centerTime - firstLiftTime)
				.SetEase(Ease.InOutSine));
			_flowSequence.Insert(phaseDelay + centerTime, rect.DOAnchorPos(secondLiftPose, secondLiftTime - centerTime)
				.SetEase(Ease.InOutSine));
			_flowSequence.Insert(phaseDelay + centerTime, rect.DORotate(new Vector3(0f, 0f, -splitRotation), secondLiftTime - centerTime)
				.SetEase(Ease.InOutSine));
			_flowSequence.Insert(phaseDelay + secondLiftTime, rect.DOAnchorPos(centerPose, DeckWeaveDuration - secondLiftTime)
				.SetEase(Ease.InOutSine));
			_flowSequence.Insert(phaseDelay + secondLiftTime, rect.DORotate(Vector3.zero, DeckWeaveDuration - secondLiftTime)
				.SetEase(Ease.InOutSine));
		}

		_flowSequence.AppendInterval(Mathf.Max(0f, DeckWeaveDuration - _flowSequence.Duration()));
		_flowSequence.OnComplete(() =>
		{
			RestoreShuffleCardSiblingOrder();
			_flowSequence = null;
			onComplete?.Invoke();
		});
	}

	private void PlayTripleCutAnimation(Action onComplete)
	{
		_flowSequence?.Kill(false);
		_flowSequence = DOTween.Sequence();
		IList<Image> shuffleCards = GetShuffleCards();
		int firstCut = Mathf.Clamp(Mathf.RoundToInt(shuffleCards.Count * 0.32f), 1, shuffleCards.Count - 2);
		int secondCut = Mathf.Clamp(Mathf.RoundToInt(shuffleCards.Count * 0.68f), firstCut + 1, shuffleCards.Count - 1);
		float firstStageTime = DeckTripleCutDuration * 0.35f;
		float secondStageTime = DeckTripleCutDuration * 0.65f;

		for (int i = 0; i < shuffleCards.Count; i++)
		{
			Image image = shuffleCards[i];
			if (image == null) continue;

			RectTransform rect = image.rectTransform;
			int pile = GetTriplePile(i, firstCut, secondCut);
			float delay = i * 0.01f;

			_flowSequence.Insert(delay,
				rect.DOAnchorPos(GetTripleCutPose(i, shuffleCards.Count, pile, 0), firstStageTime)
					.SetEase(Ease.InOutCubic));
			_flowSequence.Insert(delay,
				rect.DORotate(new Vector3(0f, 0f, pile == 0 ? -8f : pile == 1 ? -2f : 8f), firstStageTime)
					.SetEase(Ease.InOutCubic));

			_flowSequence.Insert(delay + firstStageTime,
				rect.DOAnchorPos(GetTripleCutPose(i, shuffleCards.Count, pile, 1), secondStageTime - firstStageTime)
					.SetEase(Ease.InOutCubic));
			_flowSequence.Insert(delay + firstStageTime,
				rect.DORotate(new Vector3(0f, 0f, pile == 0 ? -5f : pile == 1 ? -7f : 5f), secondStageTime - firstStageTime)
					.SetEase(Ease.InOutCubic));

			_flowSequence.Insert(delay + secondStageTime,
				rect.DOAnchorPos(GetTripleCutPose(i, shuffleCards.Count, pile, 2), DeckTripleCutDuration - secondStageTime)
					.SetEase(Ease.InOutCubic));
			_flowSequence.Insert(delay + secondStageTime,
				rect.DORotate(new Vector3(0f, 0f, pile < 2 ? -3f : 2f), DeckTripleCutDuration - secondStageTime)
					.SetEase(Ease.InOutCubic));
		}

		_flowSequence.AppendInterval(Mathf.Max(0f, DeckTripleCutDuration - _flowSequence.Duration()));
		_flowSequence.OnComplete(() =>
		{
			RestoreShuffleCardSiblingOrder();
			_flowSequence = null;
			onComplete?.Invoke();
		});
	}

	private void PlayGatherAnimation(Action onComplete)
	{
		_flowSequence?.Kill(false);
		_flowSequence = DOTween.Sequence();
		IList<Image> shuffleCards = GetShuffleCards();

		for (int i = 0; i < shuffleCards.Count; i++)
		{
			Image image = shuffleCards[i];
			if (image == null) continue;

			RectTransform rect = image.rectTransform;
			_flowSequence.Insert(0f, rect.DOAnchorPos(GetStackPose(i, shuffleCards.Count), DeckGatherDuration)
				.SetEase(Ease.OutBack, 0.95f));
			_flowSequence.Insert(0f, rect.DORotate(new Vector3(0f, 0f, GetStackRotation(i)), DeckGatherDuration)
				.SetEase(Ease.OutBack, 0.95f));
			_flowSequence.Insert(0f, rect.DOScale(Vector3.one * _deckCardScale, DeckGatherDuration)
				.SetEase(Ease.OutBack, 0.95f));
		}

		_flowSequence.OnComplete(() =>
		{
			RestoreShuffleCardSiblingOrder();
			_flowSequence = null;
			onComplete?.Invoke();
		});
	}

	private void PlayFanOutFromCurrentPose()
	{
		SetPromptText("凭直觉选择一个", 1f);
		_flowSequence?.Kill(false);
		_flowSequence = DOTween.Sequence();
		float dealDuration = Mathf.Max(0.1f, uiComponent.dealDuration);
		float dealGap = Mathf.Max(0f, uiComponent.cardDealGap);
		int visualCardCount = GetPostShuffleFanVisualCount();

		AnimatePostShuffleFanBackground(visualCardCount, dealDuration);

		for (int i = 0; i < _cardImages.Count; i++)
		{
			Image image = _cardImages[i];
			if (image == null) continue;

			RectTransform rect = image.rectTransform;
			Vector2 targetPosition = GetCurrentFanPosition(i);
			float targetRotation = GetCurrentFanRotation(i);
			bool visibleInViewport = IsDeckCardInsideViewport(targetPosition, _deckCardScale);
			SetDeckCardViewportVisible(image, visibleInViewport);

			float startTime = i * dealGap;
			_flowSequence.Insert(startTime, image.DOFade(1f, dealDuration * 0.55f));
			_flowSequence.Insert(startTime, rect.DOAnchorPos(targetPosition, dealDuration).SetEase(Ease.OutBack, 1.15f));
			_flowSequence.Insert(startTime, rect.DORotate(new Vector3(0f, 0f, targetRotation), dealDuration).SetEase(Ease.OutCubic));
			_flowSequence.Insert(startTime, rect.DOScale(Vector3.one * _deckCardScale, dealDuration).SetEase(Ease.OutBack, 1.05f));
		}

		RestorePostShuffleFanSiblingOrder();

		_flowSequence.OnComplete(() =>
		{
			RestorePostShuffleFanSiblingOrder();
			_isChoosing = true;
			_isAnimating = false;
			SetCardsInteractable(true);
			_flowSequence = null;
		});
	}

	private void AnimatePostShuffleFanBackground(int visualCardCount, float dealDuration)
	{
		if (_flowSequence == null || _shuffleExtraImages.Count == 0 || visualCardCount <= _cardImages.Count)
			return;

		bool[] selectableSlots = BuildSelectableFanSlots(visualCardCount);
		int extraIndex = 0;

		for (int slot = 0; slot < visualCardCount && extraIndex < _shuffleExtraImages.Count; slot++)
		{
			if (selectableSlots[slot])
				continue;

			Image image = _shuffleExtraImages[extraIndex++];
			if (image == null) continue;

			RectTransform rect = image.rectTransform;
			Vector2 targetPosition = GetFanPositionByVisualSlot(slot, visualCardCount);
			float targetRotation = GetFanRotationByVisualSlot(slot, visualCardCount);
			image.enabled = true;
			image.raycastTarget = false;

			float startTime = slot * 0.008f;
			_flowSequence.Insert(startTime, image.DOFade(0.92f, dealDuration * 0.45f));
			_flowSequence.Insert(startTime, rect.DOAnchorPos(targetPosition, dealDuration).SetEase(Ease.OutBack, 1.04f));
			_flowSequence.Insert(startTime, rect.DORotate(new Vector3(0f, 0f, targetRotation), dealDuration).SetEase(Ease.OutCubic));
			_flowSequence.Insert(startTime, rect.DOScale(Vector3.one * _deckCardScale, dealDuration).SetEase(Ease.OutBack, 1.02f));
		}

		for (int i = extraIndex; i < _shuffleExtraImages.Count; i++)
		{
			Image image = _shuffleExtraImages[i];
			if (image == null) continue;
			image.enabled = false;
			image.raycastTarget = false;
		}
	}

	private bool[] BuildSelectableFanSlots(int visualCardCount)
	{
		bool[] slots = new bool[Mathf.Max(0, visualCardCount)];
		for (int i = 0; i < _cardImages.Count; i++)
		{
			int slot = GetSelectableFanVisualSlot(i, visualCardCount);
			if (slot >= 0 && slot < slots.Length)
				slots[slot] = true;
		}

		return slots;
	}

	private void ResetRuntimeState()
	{
		_finishedOrCanceled = false;
		_isChoosing = false;
		_isAnimating = false;
		_isDraggingDeck = false;
		_dragMoved = false;
		_suppressPointerClick = false;
		_pendingClickCardIndex = -1;
		_dragStartedFromCard = false;
		_isScrollingDeck = false;
		_isPullingCard = false;
		_isInertiaScrolling = false;
		_isWaitingForExit = false;
		_activeDragCardIndex = -1;
		_deckOffsetX = 0f;
		_activePullY = 0f;
		_lastScrollSampleX = 0f;
		_lastScrollSampleTime = 0f;
		_scrollVelocityX = 0f;
		_deckCardScale = 1f;
		_selectedCardScale = 0.9f;
		_minDeckOffsetX = 0f;
		_maxDeckOffsetX = 0f;
		_lastDeckViewportSize = Vector2.zero;
		_cachedCardBackSprite = null;
		_completedCard = null;
		_completedUpright = true;
		_isWaitingForShuffleStart = false;
	}

	private Vector2 GetStackPose(int index, int count)
	{
		Vector2 anchor = GetShuffleAnchor();
		float centeredIndex = index - (count - 1) * 0.5f;
		float x = anchor.x + centeredIndex * 0.34f + Mathf.Sin(index * 0.61f) * 2.2f;
		float y = anchor.y - centeredIndex * 0.5f + (index % 7) * 0.55f;
		return new Vector2(x, y);
	}

	private float GetStackRotation(int index)
	{
		return Mathf.Sin(index * 0.7f) * 1.05f;
	}

	private Vector2 GetChaosPose(int index, int count)
	{
		Vector2 anchor = GetShuffleAnchor();
		float maxRadius = Mathf.Clamp(_viewportWidth * 0.23f, 132f, 220f);
		float radius = Mathf.Lerp(44f, maxRadius, Hash01(index * 17 + 3));
		float angle = (index * 137.507f + Hash01(index * 31 + 9) * 48f) * Mathf.Deg2Rad;
		float x = Mathf.Cos(angle) * radius;
		float y = Mathf.Sin(angle) * radius * 0.68f + Mathf.Sin(index * 0.33f) * 18f;
		return anchor + new Vector2(x, y);
	}

	private float GetChaosRotation(int index)
	{
		return Mathf.Lerp(-34f, 34f, Hash01(index * 29 + 11));
	}

	private Vector2 GetWeavePose(int index, int count, float direction)
	{
		Vector2 anchor = GetShuffleAnchor();
		int half = Mathf.Max(1, (count + 1) / 2);
		bool topPile = index < half;
		int pileIndex = topPile ? index : index - half;
		int pileCount = topPile ? half : Mathf.Max(1, count - half);
		float centeredPileIndex = pileIndex - (pileCount - 1) * 0.5f;

		if (Mathf.Approximately(direction, 0f))
		{
			int orderIndex = topPile ? pileIndex * 2 : pileIndex * 2 + 1;
			float centeredOrder = orderIndex - (count - 1) * 0.5f;
			float seamX = Mathf.Clamp(ResolveCardWidth() * _deckCardScale * 0.035f, 7f, 13f);
			float seamY = Mathf.Clamp(ResolveCardHeight() * _deckCardScale * 0.006f, 2.4f, 4.8f);
			float x = (topPile ? -seamX : seamX) + Mathf.Sin(orderIndex * 0.72f) * 2.2f;
			float y = -centeredOrder * seamY;
			return anchor + new Vector2(x, y);
		}

		float amplitude = Mathf.Clamp(ResolveCardHeight() * _deckCardScale * 0.2f, 78f, 128f);
		float pileOffsetX = topPile ? -34f : 34f;
		float xOffset = pileOffsetX + centeredPileIndex * 1.35f + Mathf.Sin(index * 0.42f) * 10f;
		float yOffset = direction * amplitude - centeredPileIndex * 1.8f;
		return anchor + new Vector2(xOffset, yOffset);
	}

	private int GetTriplePile(int index, int firstCut, int secondCut)
	{
		if (index < firstCut) return 0;
		return index < secondCut ? 1 : 2;
	}

	private Vector2 GetTripleCutPose(int index, int count, int pile, int stage)
	{
		Vector2 stackPose = GetStackPose(index, count);
		float offsetX = Mathf.Clamp(ResolveCardWidth() * _deckCardScale * 0.88f, 160f, 260f);
		float liftY = Mathf.Clamp(ResolveCardHeight() * _deckCardScale * 0.08f, 34f, 54f);
		float jitter = (index % 7) * 1.8f;
		float x = 0f;
		float y = -jitter;

		if (stage == 0)
		{
			x = pile == 0 ? -offsetX : pile == 1 ? 0f : offsetX;
			y += pile == 0 ? liftY : pile == 1 ? liftY * 0.5f : 0f;
		}
		else if (stage == 1)
		{
			x = pile == 0 ? -offsetX * 0.55f : pile == 1 ? -offsetX * 0.12f : offsetX * 0.78f;
			y += pile == 1 ? liftY : pile == 0 ? liftY * 0.55f : 0f;
		}
		else
		{
			x = (pile - 1) * 24f;
			y += pile < 2 ? liftY * 0.45f : 0f;
		}

		return stackPose + new Vector2(x, y);
	}

	private static float Hash01(int seed)
	{
		return Mathf.Repeat(Mathf.Sin(seed * 12.9898f) * 43758.5453f, 1f);
	}

	private Vector2 GetSplitPose(int index, int count, int cycleOffset = 0)
	{
		Vector2 anchor = GetShuffleAnchor();
		int half = Mathf.Max(1, (count + 1) / 2);
		bool leftPile = index < half;
		int pileIndex = leftPile ? index : index - half;
		int pileCount = leftPile ? half : Mathf.Max(1, count - half);
		float centeredPileIndex = pileIndex - (pileCount - 1) * 0.5f;
		float handOffset = Mathf.Clamp(_viewportWidth * 0.26f, 176f, 292f);
		float cycleShift = cycleOffset % 2 == 0 ? 0f : 18f;
		float pileCenterOffset = handOffset * 0.5f;
		float edgeSpacingX = leftPile ? 2.05f : 1.45f;
		float edgeSpacingY = Mathf.Clamp(ResolveCardHeight() * _deckCardScale * 0.014f, 5.2f, 8.2f);
		float x = anchor.x + (leftPile ? -pileCenterOffset + cycleShift : pileCenterOffset - cycleShift)
			+ centeredPileIndex * edgeSpacingX;
		float y = anchor.y - centeredPileIndex * edgeSpacingY + (leftPile ? 22f : -22f);
		return new Vector2(x, y);
	}

	private float GetSplitRotation(int index, int count, int cycleOffset = 0)
	{
		int half = Mathf.Max(1, (count + 1) / 2);
		bool leftPile = index < half;
		float baseRotation = leftPile ? -14f : 7f;
		float cycleTilt = cycleOffset % 2 == 0 ? 0f : (leftPile ? 2.5f : -2f);
		return baseRotation + cycleTilt + Mathf.Sin(index * 0.7f) * 1.2f;
	}

	private Vector2 GetRifflePose(int index, int count, int cycleIndex)
	{
		Vector2 anchor = GetShuffleAnchor();
		int half = Mathf.Max(1, (count + 1) / 2);
		bool leftPile = index < half;
		int pileIndex = leftPile ? index : index - half;
		int orderIndex = leftPile ? pileIndex * 2 : pileIndex * 2 + 1;
		float centeredOrder = orderIndex - (count - 1) * 0.5f;
		float x = anchor.x + Mathf.Sin((orderIndex + cycleIndex) * 0.85f) * 18f + (leftPile ? -28f : 28f);
		float y = anchor.y - centeredOrder * 4.25f + Mathf.Sin((pileIndex + cycleIndex) * 1.4f) * 9f;
		return new Vector2(x, y);
	}

	private float GetRiffleRotation(int index, int count, int cycleIndex)
	{
		int half = Mathf.Max(1, (count + 1) / 2);
		bool leftPile = index < half;
		return (leftPile ? -7.5f : 5f) + Mathf.Sin((index + cycleIndex) * 0.9f) * 2.4f;
	}

	private Vector2 GetShuffleAnchor()
	{
		return new Vector2(0f, GetStackCenterY());
	}

	private float GetStackCenterY()
	{
		return Mathf.Clamp(_viewportHeight * 0.1f, 58f, 128f);
	}

	private void SetPromptText(string text, float alpha)
	{
		if (_desText == null) return;

		DOTween.Kill(_desText);
		_desText.gameObject.SetActive(true);
		_desText.text = text;
		_desText.alpha = alpha;
	}

	private void StartTapHintPulse()
	{
		if (_desText == null) return;

		StopTapHintPulse();
		_desText.alpha = 1f;
		_tapHintTween = _desText.DOFade(0.65f, 0.72f)
			.SetEase(Ease.InOutSine)
			.SetLoops(-1, LoopType.Yoyo);
	}

	private void StopTapHintPulse()
	{
		_tapHintTween?.Kill(false);
		_tapHintTween = null;
		if (_desText != null)
			_desText.alpha = 1f;
	}

	private void SetIntroCardsInteractable(bool interactable)
	{
		for (int i = 0; i < _cardImages.Count; i++)
		{
			Image image = _cardImages[i];
			if (image == null) continue;

			image.raycastTarget = interactable;
			Button button = image.GetComponent<Button>();
			if (button != null)
				button.interactable = interactable;
		}
	}

	private void SelectCardByIndex(int index)
	{
		if (!_isChoosing || _isAnimating) return;
		if (index < 0 || index >= _cardImages.Count) return;

		StopDeckInertia();
		_pullBackTween?.Kill(false);
		_pullBackTween = null;
		_isChoosing = false;
		_isAnimating = true;
		_isDraggingDeck = false;
		_dragMoved = true;
		_suppressPointerClick = true;
		_activeDragCardIndex = index;
		_isScrollingDeck = false;
		_isPullingCard = false;
		SetCardsInteractable(false);

		var draw = ResolveDrawResult();
		PlayRevealAnimation(index, draw.card, draw.upright);
	}

	private void PlayRevealAnimation(int selectedIndex, TarotCard card, bool upright)
	{
		Image selectedImage = _cardImages[selectedIndex];
		RectTransform selectedRect = selectedImage.rectTransform;
		selectedImage.enabled = true;
		selectedImage.raycastTarget = false;
		DetachSelectedCardFromDeckClip(selectedRect);
		selectedRect.SetAsLastSibling();
		SetDeckCardSortingOrder(selectedImage, _cardImages.Count + 20);

		Outline outline = EnsureSelectedOutline(selectedImage);
		outline.effectColor = WithAlpha(uiComponent.selectedGlowColor, 0f);
		ResolveSelectedCardTargetPose(selectedRect, out Vector2 targetPosition, out float targetRotationZ, out float targetScale);

		_flowSequence = DOTween.Sequence();
		FadeOutShuffleExtraCards(_flowSequence, 0.28f);
		float selectDuration = Mathf.Max(0.12f, uiComponent.selectDuration);
		float flipHalfDuration = Mathf.Max(0.08f, uiComponent.flipDuration * 0.5f);
		float reverseRotateDuration = Mathf.Max(0.08f, uiComponent.reverseRotateDuration);
		float liftDuration = Mathf.Min(0.18f, selectDuration * 0.38f);
		Vector2 liftPosition = selectedRect.anchoredPosition
			+ new Vector2(0f, Mathf.Clamp(_viewportHeight * 0.12f, 80f, 170f));

		for (int i = 0; i < _cardImages.Count; i++)
		{
			Image image = _cardImages[i];
			if (image == null || image == selectedImage) continue;

			RectTransform rect = image.rectTransform;
			float side = i < selectedIndex ? -1f : 1f;
			Vector2 exitPosition = GetCurrentFanPosition(i) + new Vector2(side * 220f, -260f);

			_flowSequence.Insert(0f, rect.DOAnchorPos(exitPosition, 0.32f).SetEase(Ease.InCubic));
			_flowSequence.Insert(0f, rect.DOScale(Vector3.one * (_deckCardScale * 0.78f), 0.32f).SetEase(Ease.InCubic));
			_flowSequence.Insert(0f, image.DOFade(0f, 0.22f));
		}

		_flowSequence.Insert(0f, selectedRect.DOAnchorPos(liftPosition, liftDuration).SetEase(Ease.OutCubic));
		_flowSequence.Insert(liftDuration * 0.65f, selectedRect.DOAnchorPos(targetPosition, selectDuration).SetEase(Ease.InOutCubic));
		_flowSequence.Insert(liftDuration * 0.65f, selectedRect.DORotate(new Vector3(0f, 0f, targetRotationZ), selectDuration).SetEase(Ease.InOutCubic));
		_flowSequence.Insert(liftDuration * 0.65f, selectedRect.DOScale(Vector3.one * targetScale, selectDuration).SetEase(Ease.OutBack, 0.82f));
		_flowSequence.Insert(0.02f, DOTween.To(
				() => outline.effectColor,
				value => outline.effectColor = value,
				uiComponent.selectedGlowColor,
				0.28f)
			.SetTarget(outline));

		_flowSequence.AppendCallback(() => PlaySparkBurst(selectedRect));
		_flowSequence.AppendInterval(0.08f);
		_flowSequence.Append(selectedRect.DORotate(new Vector3(0f, 88f, targetRotationZ), flipHalfDuration).SetEase(Ease.InCubic));
		_flowSequence.AppendCallback(() => RevealFrontCard(selectedImage, card, upright, targetRotationZ));
		_flowSequence.Append(selectedRect.DORotate(new Vector3(0f, 0f, targetRotationZ), flipHalfDuration).SetEase(Ease.OutCubic));
		if (!upright)
			_flowSequence.Append(selectedRect.DORotate(new Vector3(0f, 0f, targetRotationZ + 180f), reverseRotateDuration).SetEase(Ease.InOutCubic));
		_flowSequence.AppendCallback(() => CommitTargetCardResult(selectedImage, card, upright));
		RectTransform punchRect = ResolveTargetCardPoseRect();
		if (punchRect != null)
			_flowSequence.Append(punchRect.DOPunchScale(Vector3.one * 0.035f, 0.3f, 7, 0.65f));
		_flowSequence.OnComplete(() => EnterResultWaitingState(card, upright));
	}

	private void DetachSelectedCardFromDeckClip(RectTransform selectedRect)
	{
		if (selectedRect == null || _cardContainer == null || _deckClipRoot == null)
			return;
		if (!selectedRect.IsChildOf(_deckClipRoot))
			return;

		selectedRect.SetParent(_cardContainer, true);
	}

	private void RevealFrontCard(Image selectedImage, TarotCard card, bool upright, float targetRotationZ)
	{
		if (selectedImage == null) return;

		Sprite frontSprite = LoadCardSprite(card);
		selectedImage.sprite = frontSprite != null ? frontSprite : ResolveCardBackSprite();
		selectedImage.color = Color.white;
		selectedImage.preserveAspect = true;
		selectedImage.rectTransform.localRotation = Quaternion.Euler(0f, 88f, targetRotationZ);

		if (_desText != null && card != null)
		{
			_desText.DOFade(0f, 0.12f).OnComplete(() =>
			{
				if (_desText == null) return;
				_desText.text = card.DisplayName(upright);
				_desText.DOFade(1f, 0.2f);
			});
		}
	}

	private void ResolveSelectedCardTargetPose(RectTransform selectedRect,
		out Vector2 targetPosition,
		out float targetRotationZ,
		out float targetScale)
	{
		targetPosition = Vector2.zero;
		targetRotationZ = 0f;
		targetScale = _selectedCardScale;

		RectTransform targetRect = ResolveTargetCardPoseRect();
		if (targetRect == null || selectedRect == null || _cardContainer == null)
			return;

		Vector3 worldCenter = targetRect.TransformPoint(targetRect.rect.center);
		Vector3 localCenter = _cardContainer.InverseTransformPoint(worldCenter);
		targetPosition = new Vector2(localCenter.x, localCenter.y);

		Quaternion localRotation = Quaternion.Inverse(_cardContainer.rotation) * targetRect.rotation;
		targetRotationZ = localRotation.eulerAngles.z;

		Vector3[] worldCorners = new Vector3[4];
		targetRect.GetWorldCorners(worldCorners);
		Vector3 localBottomLeft = _cardContainer.InverseTransformPoint(worldCorners[0]);
		Vector3 localTopLeft = _cardContainer.InverseTransformPoint(worldCorners[1]);
		Vector3 localBottomRight = _cardContainer.InverseTransformPoint(worldCorners[3]);

		float targetWidth = Vector3.Distance(localBottomLeft, localBottomRight);
		float targetHeight = Vector3.Distance(localBottomLeft, localTopLeft);
		float sourceWidth = Mathf.Max(1f, selectedRect.rect.width);
		float sourceHeight = Mathf.Max(1f, selectedRect.rect.height);
		float widthScale = targetWidth / sourceWidth;
		float heightScale = targetHeight / sourceHeight;
		targetScale = Mathf.Max(0.01f, Mathf.Min(widthScale, heightScale));
	}

	private void CommitTargetCardResult(Image selectedImage, TarotCard card, bool upright)
	{
		if (card == null) return;

		Sprite frontSprite = LoadCardSprite(card);
		if (uiComponent.targetCard != null)
		{
			uiComponent.targetCard.sprite = ResolveCardBackSprite();
			uiComponent.targetCard.color = Color.white;
			uiComponent.targetCard.preserveAspect = true;
			uiComponent.targetCard.rectTransform.localRotation = Quaternion.identity;
		}

		if (uiComponent.targetCardFrontImage != null)
		{
			uiComponent.targetCardFrontImage.sprite = frontSprite != null ? frontSprite : ResolveCardBackSprite();
			uiComponent.targetCardFrontImage.color = Color.white;
			uiComponent.targetCardFrontImage.preserveAspect = true;
		}

		if (uiComponent.cardNameText != null)
		{
			uiComponent.cardNameText.gameObject.SetActive(true);
			uiComponent.cardNameText.text = card.DisplayName(upright);
			uiComponent.cardNameText.alpha = 1f;
		}

		SetTargetCardVisible(true);
		SetTargetCardResultRotation(upright);

		if (selectedImage != null)
			selectedImage.gameObject.SetActive(false);
	}

	private void EnterResultWaitingState(TarotCard card, bool upright)
	{
		_isAnimating = false;
		_isChoosing = false;
		_isWaitingForExit = true;
		_completedCard = card;
		_completedUpright = upright;
		_flowSequence = null;

		SetExitButtonInteractable(true);
	}

	private void CompleteDraw(TarotCard card, bool upright)
	{
		_finishedOrCanceled = true;
		_isAnimating = false;
		_isWaitingForExit = false;
		_flowSequence = null;

		Action<TarotCard, bool> completed = PendingDrawCompleted;
		ClearPendingCallbacks();

		UIModule.Instance.HideWindow<DrawCardUI>();
		completed?.Invoke(card, upright);
	}

	private void CancelDraw(bool hideWindow)
	{
		_finishedOrCanceled = true;
		_isChoosing = false;
		_isAnimating = false;
		_isWaitingForExit = false;
		KillFlow();

		Action canceled = PendingDrawCanceled;
		ClearPendingCallbacks();

		if (hideWindow)
			UIModule.Instance.HideWindow<DrawCardUI>();

		canceled?.Invoke();
	}

	private void ResolveRuntimeReferences()
	{
		ConfigureDeckSettings();
		_contentRoot = transform.Find("UIContent") as RectTransform;
		_cardContainer = uiComponent.cardContainer as RectTransform;
		if (_cardContainer == null)
			_cardContainer = transform.Find("UIContent/Bottom/cardTransform") as RectTransform;

		if (uiComponent.cardGO == null && _cardContainer != null)
		{
			Transform cardTransform = _cardContainer.Find("Card");
			if (cardTransform != null)
				uiComponent.cardGO = cardTransform.gameObject;
		}

		_cardTemplateImage = uiComponent.cardGO != null
			? uiComponent.cardGO.GetComponent<Image>()
			: null;

		_deckClipRoot = null;
		ClearGeneratedDeckChildren();

		_titleText = transform.Find("UIContent/Top/title")?.GetComponent<TMP_Text>();
		_desText = transform.Find("UIContent/Top/des")?.GetComponent<TMP_Text>();
		ResolveTargetCardFaceReferences();
		ResolveExitButtonReferences();
		BindExitButton();

		GameObject mask = ResolveMaskObject();
		if (mask != null)
		{
			BindMaskClick(mask);
			BindDeckDragSurface(mask);
		}

		if (_cardContainer != null)
			BindDeckDragSurface(_cardContainer.gameObject);

		if (_titleText != null)
			_titleText.text = "每日塔罗牌";
	}

	private void ResolveBackgroundVideoReference()
	{
		if (uiComponent == null || uiComponent.backgroundVideoPlayer != null)
			return;

		uiComponent.backgroundVideoPlayer = transform.Find("video")?.GetComponent<VideoPlayer>();
	}

	private void ConfigureDeckClipRoot()
	{
		_deckClipRoot = null;
		if (_cardContainer == null) return;

		const string clipRootName = "DeckClipRoot";
		Transform existing = _cardContainer.Find(clipRootName);
		if (existing == null)
		{
			GameObject clipObject = new GameObject(clipRootName, typeof(RectTransform));
			clipObject.layer = _cardContainer.gameObject.layer;
			existing = clipObject.transform;
			existing.SetParent(_cardContainer, false);
		}

		_deckClipRoot = existing as RectTransform;
		if (_deckClipRoot == null) return;

		_deckClipRoot.anchorMin = Vector2.zero;
		_deckClipRoot.anchorMax = Vector2.one;
		_deckClipRoot.pivot = new Vector2(0.5f, 0.5f);
		_deckClipRoot.anchoredPosition = Vector2.zero;
		_deckClipRoot.sizeDelta = new Vector2(-DeckClipHorizontalPadding * 2f, 0f);
		_deckClipRoot.localRotation = Quaternion.identity;
		_deckClipRoot.localScale = Vector3.one;
		_deckClipRoot.SetAsLastSibling();

		RectMask2D clipMask = _deckClipRoot.GetComponent<RectMask2D>();
		if (clipMask == null)
			clipMask = _deckClipRoot.gameObject.AddComponent<RectMask2D>();
		clipMask.enabled = true;

		Image raycastSurface = _deckClipRoot.GetComponent<Image>();
		if (raycastSurface == null)
			raycastSurface = _deckClipRoot.gameObject.AddComponent<Image>();
		raycastSurface.enabled = true;
		raycastSurface.color = Color.clear;
		raycastSurface.raycastTarget = true;
	}

	private RectTransform GetDeckViewportRoot()
	{
		return _cardContainer;
	}

	private void ConfigureDeckSettings()
	{
		if (uiComponent == null) return;

		uiComponent.selectableCardCount = Mathf.Max(uiComponent.selectableCardCount, VisualDeckCardCount);
		uiComponent.fanWidth = 0f;
		uiComponent.useResponsiveFanWidth = true;
		uiComponent.fanViewportWidthMultiplier = Mathf.Max(uiComponent.fanViewportWidthMultiplier, 1.42f);
		uiComponent.minFanWidth = Mathf.Max(uiComponent.minFanWidth, 1120f);
		uiComponent.fanRiseOffset = uiComponent.fanRiseOffset <= 0f ? 108f : uiComponent.fanRiseOffset;
		uiComponent.fanRotation = uiComponent.fanRotation <= 0f ? 18f : uiComponent.fanRotation;
		uiComponent.infiniteScroll = true;
		uiComponent.deckCardScale = 0f;
		uiComponent.minDeckCardScale = Mathf.Max(uiComponent.minDeckCardScale, 0.5f);
		uiComponent.maxDeckCardScale = Mathf.Max(uiComponent.maxDeckCardScale, 0.64f);
	}

	private void ResolveTargetCardFaceReferences()
	{
		if (uiComponent == null) return;

		Transform content = transform.Find("UIContent");
		if (uiComponent.targetCardBack == null)
			uiComponent.targetCardBack = content != null ? content.Find("CardBack") : FindChildRecursive(transform, "CardBack");
		if (uiComponent.targetCardFront == null)
			uiComponent.targetCardFront = content != null ? content.Find("CardFront") : FindChildRecursive(transform, "CardFront");

		if (uiComponent.targetCard == null && uiComponent.targetCardBack != null)
			uiComponent.targetCard = uiComponent.targetCardBack.GetComponentInChildren<Image>(true);

		if (uiComponent.targetCardFrontImage == null && uiComponent.targetCardFront != null)
		{
			Transform cardImage = uiComponent.targetCardFront.Find("CardRoot/CardImage");
			uiComponent.targetCardFrontImage = cardImage != null
				? cardImage.GetComponent<Image>()
				: FindImageChild(uiComponent.targetCardFront, "CardImage");
		}

		if (uiComponent.cardNameText == null)
		{
			if (uiComponent.targetCardFront != null)
				uiComponent.cardNameText = uiComponent.targetCardFront.GetComponentInChildren<TMP_Text>(true);
			else if (uiComponent.targetCardBack != null)
				uiComponent.cardNameText = uiComponent.targetCardBack.GetComponentInChildren<TMP_Text>(true);
			else if (uiComponent.targetCard != null)
				uiComponent.cardNameText = uiComponent.targetCard.GetComponentInChildren<TMP_Text>(true);
		}
	}

	private void ResolveExitButtonReferences()
	{
		if (uiComponent == null) return;

		if (uiComponent.cardFrontParent == null)
		{
			Transform cardParent = FindChildRecursive(transform, "CardParent");
			if (cardParent != null)
				uiComponent.cardFrontParent = cardParent;
		}

		if (uiComponent.exitBtn == null)
		{
			Transform exitButtonTransform = FindChildRecursive(transform, "exitBtn");
			if (exitButtonTransform != null)
				uiComponent.exitBtn = exitButtonTransform.GetComponent<Button>();
		}
	}

	private void BindExitButton()
	{
		if (uiComponent?.exitBtn == null) return;

		uiComponent.exitBtn.onClick.RemoveListener(OnExitButtonClicked);
		uiComponent.exitBtn.onClick.AddListener(OnExitButtonClicked);
	}

	private static Transform FindChildRecursive(Transform root, string childName)
	{
		if (root == null || string.IsNullOrEmpty(childName))
			return null;

		for (int i = 0; i < root.childCount; i++)
		{
			Transform child = root.GetChild(i);
			if (child.name == childName)
				return child;

			Transform match = FindChildRecursive(child, childName);
			if (match != null)
				return match;
		}

		return null;
	}

	private static Image FindImageChild(Transform root, string childName)
	{
		Transform child = FindChildRecursive(root, childName);
		return child != null ? child.GetComponent<Image>() : null;
	}

	private RectTransform ResolveTargetCardPoseRect()
	{
		if (uiComponent.targetCardFront != null)
			return uiComponent.targetCardFront as RectTransform;
		if (uiComponent.targetCardBack != null)
			return uiComponent.targetCardBack as RectTransform;
		return uiComponent.targetCard != null ? uiComponent.targetCard.rectTransform : null;
	}

	private void ConfigureBackgroundVideoPlayer()
	{
		ResolveBackgroundVideoReference();

		VideoPlayer videoPlayer = uiComponent?.backgroundVideoPlayer;
		if (videoPlayer == null) return;

		EnsureBackgroundVideoTextureInstance(videoPlayer);
		videoPlayer.playOnAwake = false;
		videoPlayer.waitForFirstFrame = true;
		videoPlayer.skipOnDrop = false;
		videoPlayer.isLooping = true;
	}

	private void EnsureBackgroundVideoTextureInstance(VideoPlayer videoPlayer)
	{
		if (videoPlayer == null || _backgroundVideoTextureInstance != null)
			return;

		RenderTexture sourceTexture = videoPlayer.targetTexture;
		if (sourceTexture == null)
			return;

		_sharedBackgroundVideoTexture = sourceTexture;
		RenderTextureDescriptor descriptor = sourceTexture.descriptor;
		_backgroundVideoTextureInstance = new RenderTexture(descriptor)
		{
			name = $"{sourceTexture.name}_DrawCardRuntime",
			filterMode = sourceTexture.filterMode,
			wrapMode = sourceTexture.wrapMode,
			anisoLevel = sourceTexture.anisoLevel,
		};
		_backgroundVideoTextureInstance.Create();

		videoPlayer.targetTexture = _backgroundVideoTextureInstance;
		ApplyBackgroundVideoTextureToRawImages(_sharedBackgroundVideoTexture, _backgroundVideoTextureInstance);
	}

	private void ApplyBackgroundVideoTextureToRawImages(Texture fromTexture, Texture toTexture)
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

	private void PlayBackgroundVideo()
	{
		VideoPlayer videoPlayer = uiComponent?.backgroundVideoPlayer;
		if (videoPlayer == null)
			return;

		ConfigureBackgroundVideoPlayer();
		if (videoPlayer.isPlaying || _backgroundVideoCoroutine != null)
			return;

		_backgroundVideoCoroutine = uiComponent.StartCoroutine(PlayBackgroundVideoRoutine(++_backgroundVideoPlayRequestId));
	}

	private IEnumerator PlayBackgroundVideoRoutine(int requestId)
	{
		VideoPlayer videoPlayer = uiComponent?.backgroundVideoPlayer;
		if (videoPlayer == null || videoPlayer.clip == null)
		{
			_backgroundVideoCoroutine = null;
			yield break;
		}

		if (!videoPlayer.isPrepared)
		{
			videoPlayer.Prepare();
			while (!videoPlayer.isPrepared)
			{
				if (requestId != _backgroundVideoPlayRequestId)
				{
					_backgroundVideoCoroutine = null;
					yield break;
				}

				yield return null;
			}
		}

		if (requestId != _backgroundVideoPlayRequestId)
		{
			_backgroundVideoCoroutine = null;
			yield break;
		}

		if (!_backgroundVideoHasStarted || (videoPlayer.length > 0 && videoPlayer.time >= videoPlayer.length - 0.05f))
		{
			videoPlayer.time = 0;
			_backgroundVideoHasStarted = true;
		}

		videoPlayer.Play();
		_backgroundVideoCoroutine = null;
	}

	private void PauseBackgroundVideo()
	{
		_backgroundVideoPlayRequestId++;
		if (_backgroundVideoCoroutine != null)
		{
			uiComponent.StopCoroutine(_backgroundVideoCoroutine);
			_backgroundVideoCoroutine = null;
		}

		VideoPlayer videoPlayer = uiComponent?.backgroundVideoPlayer;
		if (videoPlayer != null && videoPlayer.isPlaying)
			videoPlayer.Pause();
	}

	private void ReleaseBackgroundVideoTextureInstance()
	{
		PauseBackgroundVideo();

		if (_backgroundVideoTextureInstance == null)
			return;

		if (uiComponent?.backgroundVideoPlayer != null
			&& uiComponent.backgroundVideoPlayer.targetTexture == _backgroundVideoTextureInstance)
		{
			uiComponent.backgroundVideoPlayer.targetTexture = _sharedBackgroundVideoTexture;
		}

		ApplyBackgroundVideoTextureToRawImages(_backgroundVideoTextureInstance, _sharedBackgroundVideoTexture);
		_backgroundVideoTextureInstance.Release();
		UnityEngine.Object.Destroy(_backgroundVideoTextureInstance);
		_backgroundVideoTextureInstance = null;
		_sharedBackgroundVideoTexture = null;
	}

	private GameObject ResolveMaskObject()
	{
		Transform activeMask = transform.Find("UIMask");
		if (activeMask != null && activeMask.gameObject.activeInHierarchy)
			return activeMask.gameObject;

		Transform anyMask = transform.Find("UIMask");
		return anyMask != null ? anyMask.gameObject : null;
	}

	private void BindMaskClick(GameObject maskObject)
	{
		if (maskObject == null) return;

		Button maskButton = maskObject.GetComponent<Button>();
		if (maskButton == null)
			maskButton = maskObject.AddComponent<Button>();

		maskButton.transition = Selectable.Transition.None;
		maskButton.onClick.RemoveListener(OnMaskClick);
		maskButton.onClick.AddListener(OnMaskClick);
	}

	private void BindDeckDragSurface(GameObject surfaceObject)
	{
		if (surfaceObject == null) return;

		EventTrigger trigger = surfaceObject.GetComponent<EventTrigger>();
		if (trigger == null)
			trigger = surfaceObject.AddComponent<EventTrigger>();
		if (trigger.triggers == null)
			trigger.triggers = new List<EventTrigger.Entry>();

		RemoveEventTriggerEntry(trigger, EventTriggerType.PointerDown);
		RemoveEventTriggerEntry(trigger, EventTriggerType.BeginDrag);
		RemoveEventTriggerEntry(trigger, EventTriggerType.Drag);
		RemoveEventTriggerEntry(trigger, EventTriggerType.EndDrag);

		AddEventTriggerEntry(trigger, EventTriggerType.PointerDown, OnDeckPointerDown);
		AddEventTriggerEntry(trigger, EventTriggerType.BeginDrag, OnDeckBeginDrag);
		AddEventTriggerEntry(trigger, EventTriggerType.Drag, OnDeckDrag);
		AddEventTriggerEntry(trigger, EventTriggerType.EndDrag, OnDeckEndDrag);
	}

	private void BindCardDrag(GameObject cardObject, int index)
	{
		if (cardObject == null) return;

		EventTrigger trigger = cardObject.GetComponent<EventTrigger>();
		if (trigger == null)
			trigger = cardObject.AddComponent<EventTrigger>();
		if (trigger.triggers == null)
			trigger.triggers = new List<EventTrigger.Entry>();

		RemoveEventTriggerEntry(trigger, EventTriggerType.PointerDown);
		RemoveEventTriggerEntry(trigger, EventTriggerType.PointerClick);
		RemoveEventTriggerEntry(trigger, EventTriggerType.BeginDrag);
		RemoveEventTriggerEntry(trigger, EventTriggerType.Drag);
		RemoveEventTriggerEntry(trigger, EventTriggerType.EndDrag);

		AddEventTriggerEntry(trigger, EventTriggerType.PointerDown, eventData => OnCardPointerDown(index, eventData));
		AddEventTriggerEntry(trigger, EventTriggerType.PointerClick, eventData => OnCardPointerClick(index, eventData));
		AddEventTriggerEntry(trigger, EventTriggerType.BeginDrag, eventData => OnCardBeginDrag(index, eventData));
		AddEventTriggerEntry(trigger, EventTriggerType.Drag, eventData => OnCardDrag(index, eventData));
		AddEventTriggerEntry(trigger, EventTriggerType.EndDrag, OnCardEndDrag);
	}

	private static void AddEventTriggerEntry(EventTrigger trigger, EventTriggerType eventType,
		UnityEngine.Events.UnityAction<BaseEventData> callback)
	{
		EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
		entry.callback.AddListener(callback);
		trigger.triggers.Add(entry);
	}

	private static void RemoveEventTriggerEntry(EventTrigger trigger, EventTriggerType eventType)
	{
		trigger.triggers.RemoveAll(entry => entry.eventID == eventType);
	}

	private void OnDeckPointerDown(BaseEventData eventData)
	{
		if (!_isChoosing || _isAnimating) return;

		StopDeckInertia();
		StopPendingSingleClick();
		_dragMoved = false;
		_suppressPointerClick = false;
		_activeDragCardIndex = -1;
		_dragStartedFromCard = false;
		_isScrollingDeck = false;
		_isPullingCard = false;
		_clickSuppressTween?.Kill(false);
		_clickSuppressTween = null;
	}

	private void OnDeckBeginDrag(BaseEventData eventData)
	{
		if (!_isChoosing || _isAnimating) return;

		StopPendingSingleClick();
		PointerEventData pointerData = eventData as PointerEventData;
		if (pointerData == null) return;
		if (!TryGetContainerLocalPoint(pointerData, out Vector2 localPoint)) return;

		_dragStartLocalX = localPoint.x;
		_dragStartLocalY = localPoint.y;
		_dragStartOffsetX = _deckOffsetX;
		_dragMoved = false;
		_dragStartedFromCard = false;
		_isDraggingDeck = true;
		BeginScrollVelocitySample(localPoint.x);
	}

	private void OnDeckDrag(BaseEventData eventData)
	{
		if (!_isDraggingDeck || !_isChoosing || _isAnimating) return;

		PointerEventData pointerData = eventData as PointerEventData;
		if (pointerData == null) return;
		if (!TryGetContainerLocalPoint(pointerData, out Vector2 localPoint)) return;

		UpdateScrollVelocitySample(localPoint.x);
		float delta = (localPoint.x - _dragStartLocalX) * Mathf.Max(0.1f, uiComponent.dragSensitivity);
		if (!_dragMoved && Mathf.Abs(delta) >= Mathf.Max(1f, uiComponent.dragClickThreshold))
		{
			_dragMoved = true;
			_suppressPointerClick = true;
			_isScrollingDeck = true;
			SetCardsInteractable(false);
		}

		if (!_dragMoved) return;

		_deckOffsetX = NormalizeDeckOffset(_dragStartOffsetX + delta);
		ApplyDeckOffset(false);
	}

	private void OnDeckEndDrag(BaseEventData eventData)
	{
		if (!_isDraggingDeck) return;

		_isDraggingDeck = false;
		_isScrollingDeck = false;

		if (_dragMoved)
			StartDeckInertiaOrRelease();
		else
			SetCardsInteractable(_isChoosing && !_isAnimating);
	}

	private void OnCardPointerDown(int index, BaseEventData eventData)
	{
		if (!_isChoosing || _isAnimating) return;

		StopDeckInertia();
		StopPendingSingleClick();
		_pullBackTween?.Kill(false);
		_pullBackTween = null;
		_activeDragCardIndex = index;
		_dragStartedFromCard = true;
		_dragMoved = false;
		_isScrollingDeck = false;
		_isPullingCard = false;
		_suppressPointerClick = false;
		_activePullY = 0f;
		_clickSuppressTween?.Kill(false);
		_clickSuppressTween = null;
	}

	private void OnCardBeginDrag(int index, BaseEventData eventData)
	{
		if (!_isChoosing || _isAnimating) return;

		StopPendingSingleClick();
		PointerEventData pointerData = eventData as PointerEventData;
		if (pointerData == null) return;
		if (!TryGetContainerLocalPoint(pointerData, out Vector2 localPoint)) return;

		_activeDragCardIndex = index;
		_dragStartedFromCard = true;
		_dragStartLocalX = localPoint.x;
		_dragStartLocalY = localPoint.y;
		_dragStartOffsetX = _deckOffsetX;
		_dragMoved = false;
		_activePullY = 0f;
		_isDraggingDeck = true;
		BeginScrollVelocitySample(localPoint.x);
	}

	private void OnCardDrag(int index, BaseEventData eventData)
	{
		if (!_isChoosing || _isAnimating || !_dragStartedFromCard) return;
		if (_activeDragCardIndex != index) return;

		PointerEventData pointerData = eventData as PointerEventData;
		if (pointerData == null) return;
		if (!TryGetContainerLocalPoint(pointerData, out Vector2 localPoint)) return;

		UpdateScrollVelocitySample(localPoint.x);
		float deltaX = (localPoint.x - _dragStartLocalX) * Mathf.Max(0.1f, uiComponent.dragSensitivity);
		float deltaY = localPoint.y - _dragStartLocalY;
		float absX = Mathf.Abs(deltaX);
		float absY = Mathf.Abs(deltaY);
		float dragThreshold = Mathf.Max(1f, uiComponent.dragClickThreshold);
		float directionBias = Mathf.Max(0.1f, uiComponent.dragPullDirectionBias);

		if (!_dragMoved)
		{
			if (absX >= dragThreshold && absX > absY)
			{
				_dragMoved = true;
				_isScrollingDeck = true;
				_suppressPointerClick = true;
				SetCardsInteractable(false);
			}
			else if (deltaY >= dragThreshold && deltaY >= absX * directionBias)
			{
				_dragMoved = true;
				_isPullingCard = true;
				_suppressPointerClick = true;
				SetCardsInteractable(false);
				BringCardToFront(index);
			}
		}

		if (!_dragMoved) return;

		if (_isScrollingDeck)
		{
			_deckOffsetX = NormalizeDeckOffset(_dragStartOffsetX + deltaX);
			ApplyDeckOffset(false);
		}
		else if (_isPullingCard)
		{
			_activePullY = Mathf.Max(0f, deltaY);
			PreviewPulledCard(index, deltaX, deltaY);
		}
	}

	private void OnCardEndDrag(BaseEventData eventData)
	{
		if (!_dragStartedFromCard) return;

		int releasedCardIndex = _activeDragCardIndex;
		_dragStartedFromCard = false;
		_isDraggingDeck = false;
		_activeDragCardIndex = -1;

		if (_isAnimating) return;

		if (_isPullingCard)
		{
			bool shouldSelect = _activePullY >= Mathf.Max(1f, uiComponent.dragPullSelectThreshold);
			_isPullingCard = false;
			_activePullY = 0f;

			if (shouldSelect)
			{
				SelectCardByIndex(releasedCardIndex);
			}
			else
			{
				SnapPulledCardBack(releasedCardIndex);
				SuppressPointerClickBriefly();
			}
		}
		else if (_dragMoved)
		{
			bool wasScrollingDeck = _isScrollingDeck;
			_isScrollingDeck = false;
			if (wasScrollingDeck)
				StartDeckInertiaOrRelease();
			else
				SuppressPointerClickBriefly();
		}
		else
		{
			_suppressPointerClick = false;
			SetCardsInteractable(_isChoosing && !_isAnimating);
		}
	}

	private void BeginScrollVelocitySample(float localX)
	{
		_lastScrollSampleX = localX;
		_lastScrollSampleTime = Time.unscaledTime;
		_scrollVelocityX = 0f;
	}

	private void UpdateScrollVelocitySample(float localX)
	{
		float now = Time.unscaledTime;
		float deltaTime = now - _lastScrollSampleTime;
		if (deltaTime > 0.0001f)
		{
			float instantVelocity = (localX - _lastScrollSampleX)
				* Mathf.Max(0.1f, uiComponent.dragSensitivity)
				/ deltaTime;
			_scrollVelocityX = Mathf.Lerp(_scrollVelocityX, instantVelocity, Mathf.Clamp01(deltaTime * 18f));
		}

		_lastScrollSampleX = localX;
		_lastScrollSampleTime = now;
	}

	private void StartDeckInertiaOrRelease()
	{
		if (!uiComponent.useScrollInertia || !_isChoosing || _isAnimating)
		{
			SuppressPointerClickBriefly();
			return;
		}

		float minVelocity = Mathf.Max(0f, uiComponent.minFlickVelocity);
		float velocity = _scrollVelocityX * Mathf.Max(0f, uiComponent.scrollInertiaMultiplier);
		float maxVelocity = Mathf.Max(minVelocity, uiComponent.maxFlickVelocity);
		velocity = Mathf.Clamp(velocity, -maxVelocity, maxVelocity);

		if (!uiComponent.infiniteScroll)
		{
			bool pushingLeftEdge = _deckOffsetX <= _minDeckOffsetX && velocity < 0f;
			bool pushingRightEdge = _deckOffsetX >= _maxDeckOffsetX && velocity > 0f;
			if (pushingLeftEdge || pushingRightEdge)
				velocity = 0f;
		}

		float absVelocity = Mathf.Abs(velocity);
		if (absVelocity < minVelocity)
		{
			SuppressPointerClickBriefly();
			return;
		}

		float deceleration = Mathf.Max(100f, uiComponent.scrollDeceleration);
		float maxDuration = Mathf.Max(0.05f, uiComponent.maxInertiaDuration);
		float duration = Mathf.Min(maxDuration, absVelocity / deceleration);
		float distance = Mathf.Sign(velocity) * (absVelocity * duration - 0.5f * deceleration * duration * duration);
		float unclampedTarget = _deckOffsetX + distance;
		float target = uiComponent.infiniteScroll
			? unclampedTarget
			: Mathf.Clamp(unclampedTarget, _minDeckOffsetX, _maxDeckOffsetX);
		float actualDistance = Mathf.Abs(target - _deckOffsetX);

		if (actualDistance < 0.5f)
		{
			SuppressPointerClickBriefly();
			return;
		}

		if (!uiComponent.infiniteScroll && !Mathf.Approximately(target, unclampedTarget))
		{
			duration = Mathf.Min(duration, Mathf.Clamp(actualDistance / Mathf.Max(absVelocity * 0.65f, 1f), 0.12f, 0.42f));
		}

		duration = Mathf.Clamp(duration, 0.12f, maxDuration);
		_clickSuppressTween?.Kill(false);
		_clickSuppressTween = null;
		_isInertiaScrolling = true;
		_suppressPointerClick = true;
		SetCardsInteractable(false);

		_inertiaTween?.Kill(false);
		_inertiaTween = DOVirtual.Float(_deckOffsetX, target, duration, value =>
			{
				_deckOffsetX = NormalizeDeckOffset(value);
				ApplyDeckOffset(false);
			})
			.SetEase(Ease.OutCubic)
			.SetUpdate(true)
			.OnComplete(() =>
			{
				_deckOffsetX = NormalizeDeckOffset(target);
				ApplyDeckOffset(false);
				_isInertiaScrolling = false;
				_scrollVelocityX = 0f;
				_inertiaTween = null;
				SuppressPointerClickBriefly();
			});
	}

	private void StopDeckInertia()
	{
		_inertiaTween?.Kill(false);
		_inertiaTween = null;
		_isInertiaScrolling = false;
		_scrollVelocityX = 0f;
	}

	private bool TryGetContainerLocalPoint(PointerEventData pointerData, out Vector2 localPoint)
	{
		localPoint = Vector2.zero;
		RectTransform viewportRoot = GetDeckViewportRoot();
		if (viewportRoot == null || pointerData == null) return false;

		return RectTransformUtility.ScreenPointToLocalPointInRectangle(
			viewportRoot,
			pointerData.position,
			pointerData.pressEventCamera,
			out localPoint);
	}

	private void BuildCardFan()
	{
		ClearRuntimeCards();
		if (_cardContainer == null || _cardTemplateImage == null)
		{
			Debug.LogWarning("[DrawCardUI] 缺少 UIContent/Bottom/cardTransform/Card，无法构建抽卡动画");
			return;
		}

		int cardCount = GetVisualDeckCardCount();
		Sprite backSprite = ResolveCardBackSprite();
		CalculateFanLayout(cardCount);
		Transform deckParent = _cardContainer != null ? _cardContainer : _cardTemplateImage.transform.parent;

		for (int i = 0; i < cardCount; i++)
		{
			Image image = UnityEngine.Object.Instantiate(_cardTemplateImage, deckParent);

			image.name = $"DrawCard_{i + 1}";
			image.gameObject.SetActive(true);
			image.sprite = backSprite;
			image.color = backSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.72f);
			image.preserveAspect = true;
			image.raycastTarget = true;
			EnsureDeckCardCanvas(image.gameObject);

			Outline outline = image.GetComponent<Outline>();
			if (outline != null)
				outline.effectColor = WithAlpha(uiComponent.selectedGlowColor, 0f);

			RectTransform rect = image.rectTransform;
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.localScale = Vector3.one * _deckCardScale;

			Button button = image.GetComponent<Button>();
			if (button == null)
				button = image.gameObject.AddComponent<Button>();
			button.transition = Selectable.Transition.None;
			button.onClick.RemoveAllListeners();
			int capturedIndex = i;
			BindCardDrag(image.gameObject, capturedIndex);

			_cardImages.Add(image);
		}

		_cardTemplateImage.gameObject.SetActive(false);

		SetCardsInteractable(false);
	}

	private void BuildShuffleVisualDeck()
	{
		ClearShuffleExtraCards();
		_shuffleVisualCards.Clear();

		for (int i = 0; i < _cardImages.Count; i++)
		{
			if (_cardImages[i] != null)
				_shuffleVisualCards.Add(_cardImages[i]);
		}

		if (_cardTemplateImage == null || _shuffleVisualCards.Count <= 0)
			return;

		int totalCardCount = Mathf.Max(_shuffleVisualCards.Count, ShuffleDeckCardCount);
		Sprite backSprite = ResolveCardBackSprite();
		Transform deckParent = _cardContainer != null ? _cardContainer : _cardTemplateImage.transform.parent;

		for (int i = _shuffleVisualCards.Count; i < totalCardCount; i++)
		{
			Image image = CreateLightweightShuffleCard(deckParent, backSprite, i);

			RectTransform rect = image.rectTransform;
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.localScale = Vector3.one * _deckCardScale;

			_shuffleExtraImages.Add(image);
			_shuffleVisualCards.Add(image);
		}
	}

	private Image CreateLightweightShuffleCard(Transform parent, Sprite backSprite, int index)
	{
		GameObject cardObject = new GameObject(
			$"ShuffleDeckCard_{index + 1}",
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(Image));
		cardObject.transform.SetParent(parent, false);

		Image image = cardObject.GetComponent<Image>();
		image.sprite = backSprite;
		image.color = backSprite != null ? WithAlpha(Color.white, 0f) : new Color(1f, 1f, 1f, 0f);
		image.preserveAspect = true;
		image.raycastTarget = false;
		image.type = _cardTemplateImage.type;
		image.fillCenter = _cardTemplateImage.fillCenter;
		image.pixelsPerUnitMultiplier = _cardTemplateImage.pixelsPerUnitMultiplier;
		image.material = _cardTemplateImage.material;
		image.maskable = true;

		RectTransform rect = image.rectTransform;
		RectTransform templateRect = _cardTemplateImage.rectTransform;
		rect.sizeDelta = templateRect.sizeDelta;
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.localScale = Vector3.one * _deckCardScale;

		return image;
	}

	private void ClearShuffleExtraCards()
	{
		for (int i = 0; i < _shuffleExtraImages.Count; i++)
		{
			Image image = _shuffleExtraImages[i];
			if (image == null) continue;
			DestroyDeckObject(image.gameObject);
		}

		_shuffleExtraImages.Clear();
		_shuffleVisualCards.Clear();
	}

	private IList<Image> GetShuffleCards()
	{
		return _shuffleVisualCards.Count > 0 ? _shuffleVisualCards : _cardImages;
	}

	private void CalculateFanLayout(int cardCount)
	{
		_fanPositions.Clear();
		_viewportWidth = ResolveViewportWidth();
		_viewportHeight = ResolveViewportHeight();
		ResolveCardScales();

		int visualCardCount = Mathf.Max(cardCount, GetPostShuffleFanVisualCount());
		_fanWidth = ResolvePostShuffleFanWidth(visualCardCount);

		for (int i = 0; i < cardCount; i++)
		{
			int visualSlot = GetSelectableFanVisualSlot(i, visualCardCount);
			_fanPositions.Add(GetFanPositionByVisualSlot(visualSlot, visualCardCount));
		}

		CalculateDeckScrollBounds();
	}

	private int GetVisualDeckCardCount()
	{
		return Mathf.Max(3, uiComponent.selectableCardCount);
	}

	private int GetPostShuffleFanVisualCount()
	{
		return Mathf.Max(_cardImages.Count, PostShuffleFanVisualCardCount);
	}

	private int GetSelectableFanVisualSlot(int selectableIndex, int visualCardCount)
	{
		if (_cardImages.Count <= 1 || visualCardCount <= 1)
			return Mathf.Clamp(selectableIndex, 0, Mathf.Max(0, visualCardCount - 1));

		float normalized = selectableIndex / (float)(_cardImages.Count - 1);
		return Mathf.Clamp(Mathf.RoundToInt(normalized * (visualCardCount - 1)), 0, visualCardCount - 1);
	}

	private Vector2 GetFanPositionByVisualSlot(int visualSlot, int visualCardCount)
	{
		float t = ResolveFanLayoutT(visualSlot, visualCardCount);
		float x = Mathf.Lerp(-_fanWidth * 0.5f, _fanWidth * 0.5f, t);
		return new Vector2(x, GetFanY(x));
	}

	private float GetFanRotationByVisualSlot(int visualSlot, int visualCardCount)
	{
		float t = ResolveFanLayoutT(visualSlot, visualCardCount);
		float x = Mathf.Lerp(-_fanWidth * 0.5f, _fanWidth * 0.5f, t);
		return GetFanRotation(x);
	}

	private float ResolveFanLayoutT(int index, int cardCount)
	{
		if (cardCount <= 1)
			return 0.5f;

		if (!uiComponent.infiniteScroll)
			return index / (float)(cardCount - 1);

		return index / (float)cardCount;
	}

	private void ResolveCardScales()
	{
		float cardWidth = Mathf.Max(1f, ResolveCardWidth());
		float cardHeight = Mathf.Max(1f, ResolveCardHeight());
		float explicitScale = uiComponent.deckCardScale;

		if (explicitScale > 0f)
		{
			_deckCardScale = explicitScale;
		}
		else
		{
			float heightScale = (_viewportHeight * Mathf.Clamp(uiComponent.maxDeckCardHeightRatio, 0.2f, 1f)) / cardHeight;
			float widthScale = (_viewportWidth * Mathf.Clamp(uiComponent.maxDeckCardWidthRatio, 0.15f, 1f)) / cardWidth;
			_deckCardScale = Mathf.Min(heightScale, widthScale);
		}

		float minScale = Mathf.Max(0.1f, uiComponent.minDeckCardScale);
		float maxScale = Mathf.Max(minScale, uiComponent.maxDeckCardScale);
		_deckCardScale = Mathf.Clamp(_deckCardScale, minScale, maxScale);
		_selectedCardScale = Mathf.Clamp(uiComponent.selectedCardScale, _deckCardScale, 1.1f);
	}

	private float ResolveFittedDeckCardScale(float cardWidth, float cardHeight)
	{
		float availableWidth = Mathf.Max(1f, _viewportWidth - DeckCardSpacing * (DeckVisibleSlotCount - 1f));
		float widthScale = (availableWidth / DeckVisibleSlotCount) / Mathf.Max(1f, cardWidth);
		float heightScale = (_viewportHeight * 0.56f) / Mathf.Max(1f, cardHeight);
		return Mathf.Max(0.1f, Mathf.Min(widthScale, heightScale));
	}

	private float ResolvePostShuffleFanWidth(int cardCount)
	{
		float cardVisualWidth = ResolveCardWidth() * _deckCardScale;
		float desiredSpacing = cardVisualWidth * PostShuffleFanCardSpacingRatio;
		float visibleSpan = desiredSpacing * Mathf.Max(1, cardCount - 1);
		float layoutSpan = uiComponent.infiniteScroll && cardCount > 1
			? Mathf.Max(0.1f, (cardCount - 1) / (float)cardCount)
			: 1f;
		float cardBasedWidth = visibleSpan / layoutSpan;
		float minWidth = _viewportWidth * PostShuffleFanMinViewportMultiplier;
		float maxWidth = _viewportWidth * PostShuffleFanMaxViewportMultiplier;

		return Mathf.Clamp(cardBasedWidth, minWidth, Mathf.Max(minWidth, maxWidth));
	}

	private void CalculateDeckScrollBounds()
	{
		if (uiComponent.infiniteScroll)
		{
			_minDeckOffsetX = float.NegativeInfinity;
			_maxDeckOffsetX = float.PositiveInfinity;
			_deckOffsetX = NormalizeDeckOffset(_deckOffsetX);
			return;
		}

		float contentWidth = _fanWidth + ResolveCardWidth() * _deckCardScale;
		float overflow = Mathf.Max(0f, (contentWidth - _viewportWidth) * 0.5f + uiComponent.scrollEdgePadding);
		float range = Mathf.Max(uiComponent.minScrollRange, overflow * Mathf.Max(0f, uiComponent.scrollRangeMultiplier));
		_minDeckOffsetX = -range;
		_maxDeckOffsetX = range;
		_deckOffsetX = Mathf.Clamp(_deckOffsetX, _minDeckOffsetX, _maxDeckOffsetX);
	}

	private float ResolveViewportWidth()
	{
		RectTransform viewportRoot = GetDeckViewportRoot();
		if (viewportRoot != null && viewportRoot.rect.width > 1f)
			return viewportRoot.rect.width;

		if (_contentRoot != null && _contentRoot.rect.width > 1f)
			return _contentRoot.rect.width;

		return 1080f;
	}

	private float ResolveViewportHeight()
	{
		RectTransform viewportRoot = GetDeckViewportRoot();
		if (viewportRoot != null && viewportRoot.rect.height > 1f)
			return viewportRoot.rect.height;

		if (_contentRoot != null && _contentRoot.rect.height > 1f)
			return _contentRoot.rect.height;

		return 1920f;
	}

	private float ResolveCardWidth()
	{
		if (_cardTemplateImage != null && _cardTemplateImage.rectTransform.rect.width > 1f)
			return _cardTemplateImage.rectTransform.rect.width;

		return 424f;
	}

	private float ResolveCardHeight()
	{
		if (_cardTemplateImage != null && _cardTemplateImage.rectTransform.rect.height > 1f)
			return _cardTemplateImage.rectTransform.rect.height;

		return 753f;
	}

	private Vector2 GetCurrentFanPosition(int index)
	{
		if (index < 0 || index >= _fanPositions.Count)
			return Vector2.zero;

		if (!uiComponent.infiniteScroll)
			return _fanPositions[index] + new Vector2(_deckOffsetX, 0f);

		float x = GetCurrentFanX(index);
		return new Vector2(x, GetFanY(x));
	}

	private float GetCurrentFanRotation(int index)
	{
		if (index < 0 || index >= _fanPositions.Count)
			return 0f;

		if (!uiComponent.infiniteScroll)
			return GetFanRotation(_fanPositions[index].x);

		return GetFanRotation(GetCurrentFanX(index));
	}

	private float GetCurrentFanX(int index)
	{
		float x = _fanPositions[index].x + _deckOffsetX;
		return uiComponent.infiniteScroll ? WrapFanX(x) : x;
	}

	private float WrapFanX(float x)
	{
		if (_fanWidth <= 1f) return x;

		float halfWidth = _fanWidth * 0.5f;
		return Mathf.Repeat(x + halfWidth, _fanWidth) - halfWidth;
	}

	private float NormalizeDeckOffset(float offset)
	{
		if (!uiComponent.infiniteScroll)
			return Mathf.Clamp(offset, _minDeckOffsetX, _maxDeckOffsetX);

		if (_fanWidth <= 1f)
			return offset;

		float halfWidth = _fanWidth * 0.5f;
		return Mathf.Repeat(offset + halfWidth, _fanWidth) - halfWidth;
	}

	private float GetFanY(float x)
	{
		float halfWidth = Mathf.Max(1f, _fanWidth * 0.5f);
		float normalized = Mathf.Clamp(x / halfWidth, -1f, 1f);
		float baseY = Mathf.Clamp(_viewportHeight * PostShuffleFanBaseHeightRatio, -155f, -70f);
		float rise = Mathf.Clamp(_viewportHeight * PostShuffleFanRiseRatio, 68f, 112f);
		return baseY + rise * (1f - normalized * normalized);
	}

	private float GetFanRotation(float x)
	{
		float halfWidth = Mathf.Max(1f, _fanWidth * 0.5f);
		float normalized = Mathf.Clamp(x / halfWidth, -1f, 1f);
		return -normalized * PostShuffleFanMaxRotation;
	}

	private float GetDeckSpacing()
	{
		return ResolveCardWidth() * _deckCardScale + DeckCardSpacing;
	}

	private void ApplyDeckOffset(bool animated)
	{
		_lastDeckViewportSize = GetDeckViewportSize();

		const float duration = 0.12f;
		for (int i = 0; i < _cardImages.Count; i++)
		{
			Image image = _cardImages[i];
			if (image == null) continue;

			RectTransform rect = image.rectTransform;
			Vector2 targetPosition = GetCurrentFanPosition(i);
			Quaternion targetRotation = Quaternion.Euler(0f, 0f, GetCurrentFanRotation(i));
			bool visibleInViewport = IsDeckCardInsideViewport(targetPosition, _deckCardScale);
			DOTween.Kill(rect);
			SetDeckCardViewportVisible(image, visibleInViewport);

			if (animated)
			{
				rect.DOAnchorPos(targetPosition, duration).SetEase(Ease.OutCubic);
				rect.DORotateQuaternion(targetRotation, duration).SetEase(Ease.OutCubic);
			}
			else
			{
				rect.anchoredPosition = targetPosition;
				rect.localRotation = targetRotation;
				rect.localScale = Vector3.one * _deckCardScale;
			}
		}

		RestoreCardSiblingOrder();
	}

	private void RefreshDeckLayoutIfViewportChanged()
	{
		Vector2 viewportSize = GetDeckViewportSize();
		if (viewportSize.x <= 1f || viewportSize.y <= 1f)
			return;

		if (Mathf.Abs(viewportSize.x - _lastDeckViewportSize.x) <= 0.5f
			&& Mathf.Abs(viewportSize.y - _lastDeckViewportSize.y) <= 0.5f)
			return;

		CalculateFanLayout(_cardImages.Count);
		ApplyDeckOffset(false);
	}

	private Vector2 GetDeckViewportSize()
	{
		RectTransform viewportRoot = GetDeckViewportRoot();
		return viewportRoot != null ? viewportRoot.rect.size : Vector2.zero;
	}

	private bool IsDeckCardInsideViewport(Vector2 anchoredPosition, float scale)
	{
		return true;
	}

	private void SetDeckCardViewportVisible(Image image, bool visible)
	{
		if (image == null) return;

		image.enabled = visible;
		image.raycastTarget = visible && _isChoosing && !_isAnimating;
	}

	private void BringCardToFront(int index)
	{
		if (index < 0 || index >= _cardImages.Count) return;
		Image image = _cardImages[index];
		if (image == null) return;

		image.rectTransform.SetAsLastSibling();
		SetDeckCardSortingOrder(image, _cardImages.Count + 20);
	}

	private void RestoreCardSiblingOrder()
	{
		List<Image> orderedCards = new List<Image>(_cardImages.Count);
		for (int i = 0; i < _cardImages.Count; i++)
		{
			Image image = _cardImages[i];
			if (image == null) continue;
			orderedCards.Add(image);
		}

		orderedCards.Sort(CompareCardSiblingOrder);

		for (int i = 0; i < orderedCards.Count; i++)
		{
			orderedCards[i].rectTransform.SetSiblingIndex(i);
			SetDeckCardSortingOrder(orderedCards[i], i);
		}
	}

	private void RestorePostShuffleFanSiblingOrder()
	{
		int siblingIndex = 0;
		for (int i = 0; i < _shuffleExtraImages.Count; i++)
		{
			Image image = _shuffleExtraImages[i];
			if (image == null || !image.enabled) continue;
			image.rectTransform.SetSiblingIndex(siblingIndex++);
			SetDeckCardSortingOrder(image, siblingIndex);
		}

		for (int i = 0; i < _cardImages.Count; i++)
		{
			Image image = _cardImages[i];
			if (image == null) continue;
			image.rectTransform.SetSiblingIndex(siblingIndex++);
			SetDeckCardSortingOrder(image, siblingIndex + 20);
		}
	}

	private void FadeOutShuffleExtraCards(Sequence sequence, float duration)
	{
		if (sequence == null || _shuffleExtraImages.Count == 0) return;

		for (int i = 0; i < _shuffleExtraImages.Count; i++)
		{
			Image image = _shuffleExtraImages[i];
			if (image == null || !image.enabled) continue;

			RectTransform rect = image.rectTransform;
			image.raycastTarget = false;
			DOTween.Kill(image);
			DOTween.Kill(rect);

			sequence.Insert(0f, image.DOFade(0f, duration * 0.75f));
			sequence.Insert(0f, rect.DOScale(Vector3.one * (_deckCardScale * 0.86f), duration).SetEase(Ease.InCubic));
		}

		sequence.InsertCallback(duration, ClearShuffleExtraCards);
	}

	private void RestoreShuffleCardSiblingOrder()
	{
		IList<Image> shuffleCards = GetShuffleCards();
		List<Image> orderedCards = new List<Image>(shuffleCards.Count);
		for (int i = 0; i < shuffleCards.Count; i++)
		{
			Image image = shuffleCards[i];
			if (image == null) continue;
			orderedCards.Add(image);
		}

		orderedCards.Sort(CompareCardSiblingOrder);

		for (int i = 0; i < orderedCards.Count; i++)
		{
			orderedCards[i].rectTransform.SetSiblingIndex(i);
			SetDeckCardSortingOrder(orderedCards[i], i);
		}
	}

	private void RestoreWeaveCardSiblingOrder(IList<Image> shuffleCards)
	{
		if (shuffleCards == null || shuffleCards.Count == 0) return;

		List<Image> orderedCards = new List<Image>(shuffleCards.Count);
		int half = Mathf.Max(1, (shuffleCards.Count + 1) / 2);
		int maxPileCount = Mathf.Max(half, shuffleCards.Count - half);

		for (int i = 0; i < maxPileCount; i++)
		{
			if (i < half && shuffleCards[i] != null)
				orderedCards.Add(shuffleCards[i]);

			int bottomIndex = half + i;
			if (bottomIndex < shuffleCards.Count && shuffleCards[bottomIndex] != null)
				orderedCards.Add(shuffleCards[bottomIndex]);
		}

		for (int i = 0; i < orderedCards.Count; i++)
		{
			orderedCards[i].rectTransform.SetSiblingIndex(i);
			SetDeckCardSortingOrder(orderedCards[i], i);
		}
	}

	private int CompareCardSiblingOrder(Image left, Image right)
	{
		if (left == right)
			return 0;
		if (left == null)
			return -1;
		if (right == null)
			return 1;

		Vector2 leftPosition = left.rectTransform.anchoredPosition;
		Vector2 rightPosition = right.rectTransform.anchoredPosition;

		int xCompare = Mathf.RoundToInt(leftPosition.x * 100f)
			.CompareTo(Mathf.RoundToInt(rightPosition.x * 100f));
		if (xCompare != 0)
			return xCompare;

		int yCompare = Mathf.RoundToInt(leftPosition.y * 100f)
			.CompareTo(Mathf.RoundToInt(rightPosition.y * 100f));
		if (yCompare != 0)
			return yCompare;

		return string.CompareOrdinal(left.name, right.name);
	}

	private void EnsureDeckCardCanvas(GameObject cardObject)
	{
		if (cardObject == null) return;

		Canvas sortingCanvas = cardObject.GetComponent<Canvas>();
		if (sortingCanvas != null)
		{
			sortingCanvas.overrideSorting = false;
			sortingCanvas.sortingOrder = 0;
			sortingCanvas.enabled = false;
		}

		GraphicRaycaster raycaster = cardObject.GetComponent<GraphicRaycaster>();
		if (raycaster != null)
			raycaster.enabled = false;

		MaskableGraphic[] graphics = cardObject.GetComponentsInChildren<MaskableGraphic>(true);
		for (int i = 0; i < graphics.Length; i++)
		{
			if (graphics[i] != null)
				graphics[i].maskable = true;
		}
	}

	private static void SetChildGraphicsRaycastTarget(GameObject root, bool raycastTarget)
	{
		if (root == null) return;

		Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
		for (int i = 0; i < graphics.Length; i++)
		{
			if (graphics[i] != null)
				graphics[i].raycastTarget = raycastTarget;
		}
	}

	private void SetDeckCardSortingOrder(Image image, int visualOrder)
	{
		if (image == null) return;

		Canvas sortingCanvas = image.GetComponent<Canvas>();
		if (sortingCanvas != null)
		{
			sortingCanvas.overrideSorting = false;
			sortingCanvas.sortingOrder = 0;
			sortingCanvas.enabled = false;
		}
	}

	private void PreviewPulledCard(int index, float deltaX, float deltaY)
	{
		if (index < 0 || index >= _cardImages.Count) return;

		Image image = _cardImages[index];
		if (image == null) return;

		RectTransform rect = image.rectTransform;
		DOTween.Kill(rect);

		float pullY = Mathf.Max(0f, deltaY);
		float pullX = Mathf.Clamp(deltaX * 0.18f, -80f, 80f);
		float strength = Mathf.Clamp01(pullY / Mathf.Max(1f, uiComponent.dragPullSelectThreshold));
		float rotation = Mathf.Lerp(GetCurrentFanRotation(index), 0f, strength * 0.9f);
		float scale = _deckCardScale * (1f + strength * 0.08f);

		rect.anchoredPosition = GetCurrentFanPosition(index) + new Vector2(pullX, pullY);
		rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
		rect.localScale = Vector3.one * scale;
	}

	private void SnapPulledCardBack(int index)
	{
		if (index < 0 || index >= _cardImages.Count)
		{
			SetCardsInteractable(_isChoosing && !_isAnimating);
			return;
		}

		Image image = _cardImages[index];
		if (image == null)
		{
			SetCardsInteractable(_isChoosing && !_isAnimating);
			return;
		}

		RectTransform rect = image.rectTransform;
		DOTween.Kill(rect);

		Sequence sequence = DOTween.Sequence();
		sequence.Join(rect.DOAnchorPos(GetCurrentFanPosition(index), 0.18f).SetEase(Ease.OutCubic));
		sequence.Join(rect.DORotateQuaternion(Quaternion.Euler(0f, 0f, GetCurrentFanRotation(index)), 0.18f).SetEase(Ease.OutCubic));
		sequence.Join(rect.DOScale(Vector3.one * _deckCardScale, 0.18f).SetEase(Ease.OutCubic));
		sequence.OnComplete(() =>
		{
			RestoreCardSiblingOrder();
			ApplyDeckOffset(false);
			_pullBackTween = null;
			SetCardsInteractable(_isChoosing && !_isAnimating);
		});
		_pullBackTween = sequence;
	}

	private void ResetTexts()
	{
		if (_desText != null)
		{
			_desText.gameObject.SetActive(true);
			_desText.text = "凭直觉选择一个";
			_desText.alpha = 0f;
		}
	}

	private void ResetTargetCard()
	{
		SetTargetCardVisible(false);
		SetExitButtonInteractable(false);

		if (uiComponent.targetCard != null)
		{
			uiComponent.targetCard.gameObject.SetActive(true);
			uiComponent.targetCard.sprite = ResolveCardBackSprite();
			uiComponent.targetCard.color = Color.white;
			uiComponent.targetCard.rectTransform.localRotation = Quaternion.identity;
		}

		if (uiComponent.targetCardFrontImage != null)
		{
			uiComponent.targetCardFrontImage.sprite = null;
			uiComponent.targetCardFrontImage.color = Color.white;
			uiComponent.targetCardFrontImage.rectTransform.localRotation = Quaternion.identity;
		}

		SetTargetCardResultRotation(true);

		if (uiComponent.cardNameText != null)
		{
			uiComponent.cardNameText.text = string.Empty;
			uiComponent.cardNameText.alpha = 0f;
		}
	}

	private void SetTargetCardVisible(bool visible)
	{
		if (uiComponent.cardFrontParent != null)
			uiComponent.cardFrontParent.gameObject.SetActive(visible);

		if (uiComponent.targetCardBack != null || uiComponent.targetCardFront != null)
		{
			if (uiComponent.targetCardBack != null)
				uiComponent.targetCardBack.gameObject.SetActive(false);
			if (uiComponent.targetCardFront != null)
				uiComponent.targetCardFront.gameObject.SetActive(visible);
			return;
		}

		if (uiComponent.targetCardParent != null)
		{
			uiComponent.targetCardParent.gameObject.SetActive(visible);
			return;
		}

		if (uiComponent.targetCard != null)
			uiComponent.targetCard.gameObject.SetActive(visible);
	}

	private void SetExitButtonInteractable(bool interactable)
	{
		if (uiComponent?.exitBtn == null) return;

		uiComponent.exitBtn.interactable = interactable;
	}

	private void SetTargetCardResultRotation(bool upright)
	{
		Quaternion rotation = Quaternion.Euler(0f, 0f, upright ? 0f : 180f);
		if (uiComponent.targetCardFront != null)
			uiComponent.targetCardFront.localRotation = Quaternion.identity;

		if (uiComponent.targetCardFrontImage != null)
			uiComponent.targetCardFrontImage.rectTransform.localRotation = rotation;
		else if (uiComponent.targetCard != null)
			uiComponent.targetCard.rectTransform.localRotation = rotation;
		else if (uiComponent.targetCardFront != null)
			uiComponent.targetCardFront.localRotation = rotation;

		if (uiComponent.targetCardBack != null)
			uiComponent.targetCardBack.localRotation = Quaternion.identity;
	}

	private void SetCardsInteractable(bool interactable)
	{
		for (int i = 0; i < _cardImages.Count; i++)
		{
			Image image = _cardImages[i];
			if (image == null) continue;
			image.raycastTarget = interactable && image.enabled;

			Button button = image.GetComponent<Button>();
			if (button != null)
				button.interactable = interactable;
		}
	}

	private void ClearRuntimeCards()
	{
		StopPendingSingleClick();
		ClearShuffleExtraCards();
		for (int i = 0; i < _cardImages.Count; i++)
		{
			Image image = _cardImages[i];
			if (image == null) continue;
			DestroyDeckObject(image.gameObject);
		}

		_cardImages.Clear();
		ClearGeneratedDeckChildren();
		_fanPositions.Clear();
		ClearSparks();

		if (_cardTemplateImage != null)
			_cardTemplateImage.gameObject.SetActive(false);
	}

	private void ClearGeneratedDeckChildren()
	{
		ClearGeneratedDeckChildrenUnder(_deckClipRoot);
		ClearGeneratedDeckChildrenUnder(_cardContainer);
	}

	private void SanitizeDeckVisuals()
	{
		bool deckChanged = TrimTrackedDeckImages();
		ClearGeneratedDeckChildren();
		if (deckChanged && !_isPullingCard && _cardImages.Count > 0)
		{
			CalculateFanLayout(_cardImages.Count);
			ApplyDeckOffset(false);
		}
	}

	private bool TrimTrackedDeckImages()
	{
		bool changed = false;
		Transform deckParent = GetDeckViewportRoot();
		int maxRuntimeCards = GetVisualDeckCardCount();
		for (int i = _cardImages.Count - 1; i >= 0; i--)
		{
			Image image = _cardImages[i];
			if (image == null)
			{
				_cardImages.RemoveAt(i);
				changed = true;
				continue;
			}

			if (i >= maxRuntimeCards)
			{
				DestroyDeckObject(image.gameObject);
				_cardImages.RemoveAt(i);
				changed = true;
				continue;
			}

			EnsureDeckCardCanvas(image.gameObject);
			if (!_isPullingCard && deckParent != null && image.transform.parent != deckParent)
			{
				image.transform.SetParent(deckParent, false);
				changed = true;
			}
		}

		return changed;
	}

	private void ClearGeneratedDeckChildrenUnder(Transform root)
	{
		if (root == null) return;

		for (int i = root.childCount - 1; i >= 0; i--)
		{
			Transform child = root.GetChild(i);
			if (child == null) continue;
			if (_deckClipRoot != null && child == _deckClipRoot) continue;
			if (_cardTemplateImage != null && child == _cardTemplateImage.transform) continue;

			if (ShouldClearDeckChild(child))
			{
				DestroyDeckObject(child.gameObject);
				continue;
			}

			ClearGeneratedDeckChildrenUnder(child);
		}
	}

	private bool ShouldClearDeckChild(Transform child)
	{
		if (child == null) return false;
		if (_deckClipRoot != null && child == _deckClipRoot) return false;
		if (_cardTemplateImage != null && child == _cardTemplateImage.transform) return false;
		if (IsTrackedDeckChild(child)) return false;

		if (IsGeneratedDeckCardName(child.name))
			return true;

		if (child.name.StartsWith("DeckClipRoot", StringComparison.Ordinal))
			return true;

		if (child.GetComponent<Image>() == null)
			return false;

		return child.name.StartsWith("Card", StringComparison.Ordinal)
			|| child.GetComponent<Button>() != null
			|| child.GetComponent<EventTrigger>() != null;
	}

	private bool IsTrackedDeckChild(Transform child)
	{
		for (int i = 0; i < _cardImages.Count; i++)
		{
			Image image = _cardImages[i];
			if (image != null && image.transform == child)
				return true;
		}

		for (int i = 0; i < _shuffleExtraImages.Count; i++)
		{
			Image image = _shuffleExtraImages[i];
			if (image != null && image.transform == child)
				return true;
		}

		return false;
	}

	private static bool IsGeneratedDeckCardName(string objectName)
	{
		return !string.IsNullOrEmpty(objectName)
			&& (objectName.StartsWith("SpreadDeckCard_", StringComparison.Ordinal)
				|| objectName.StartsWith("DrawCard_", StringComparison.Ordinal)
				|| objectName.StartsWith("ShuffleDeckCard_", StringComparison.Ordinal)
				|| objectName.StartsWith("Card(Clone)", StringComparison.Ordinal));
	}

	private static void DestroyDeckObject(GameObject target)
	{
		if (target == null) return;

		target.SetActive(false);
		if (Application.isPlaying)
			UnityEngine.Object.Destroy(target);
		else
			UnityEngine.Object.DestroyImmediate(target);
	}

	private void PlaySparkBurst(RectTransform centerRect)
	{
		if (_cardContainer == null || centerRect == null) return;

		ClearSparks();
		const int sparkCount = 12;
		for (int i = 0; i < sparkCount; i++)
		{
			GameObject sparkObject = new GameObject($"draw_spark_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			sparkObject.transform.SetParent(_cardContainer, false);
			sparkObject.transform.SetAsLastSibling();

			Image spark = sparkObject.GetComponent<Image>();
			spark.color = new Color(1f, 0.67f, 0.22f, 0.9f);
			spark.raycastTarget = false;

			RectTransform rect = spark.rectTransform;
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(12f, 12f);
			rect.anchoredPosition = centerRect.anchoredPosition;
			rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
			rect.localScale = Vector3.one * UnityEngine.Random.Range(0.6f, 1.15f);

			float angle = i * Mathf.PI * 2f / sparkCount + UnityEngine.Random.Range(-0.18f, 0.18f);
			float distance = UnityEngine.Random.Range(120f, 230f);
			Vector2 target = rect.anchoredPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

			Sequence sparkSequence = DOTween.Sequence().SetTarget(sparkObject);
			sparkSequence.Insert(0f, rect.DOAnchorPos(target, 0.55f).SetEase(Ease.OutCubic));
			sparkSequence.Insert(0f, rect.DOScale(0f, 0.55f).SetEase(Ease.InCubic));
			sparkSequence.Insert(0.08f, spark.DOFade(0f, 0.42f));
			sparkSequence.OnComplete(() =>
			{
				if (sparkObject != null)
					UnityEngine.Object.Destroy(sparkObject);
			});

			_sparkImages.Add(spark);
		}
	}

	private void ClearSparks()
	{
		for (int i = 0; i < _sparkImages.Count; i++)
		{
			Image spark = _sparkImages[i];
			if (spark == null) continue;
			DOTween.Kill(spark.gameObject);
			UnityEngine.Object.Destroy(spark.gameObject);
		}

		_sparkImages.Clear();
	}

	private Outline EnsureSelectedOutline(Image image)
	{
		Outline outline = image.GetComponent<Outline>();
		if (outline == null)
			outline = image.gameObject.AddComponent<Outline>();

		outline.effectDistance = new Vector2(6f, -6f);
		outline.useGraphicAlpha = false;
		return outline;
	}

	private (TarotCard card, bool upright) ResolveDrawResult()
	{
		try
		{
			if (PendingDrawProvider != null)
			{
				var result = PendingDrawProvider.Invoke();
				if (result.card != null)
					return result;
			}
		}
		catch (Exception exception)
		{
			Debug.LogWarning($"[DrawCardUI] 抽牌数据提供器执行失败，使用本地随机牌: {exception.Message}");
		}

		if (DivinationEngine.Instance != null)
			return DivinationEngine.Instance.DrawDailyCard();

		return TarotDeck.DrawOne();
	}

	private Sprite ResolveCardBackSprite()
	{
		if (_cachedCardBackSprite != null) return _cachedCardBackSprite;
		Sprite preferred = null;
		if (uiComponent.cardBackSprite != null)
			preferred = uiComponent.cardBackSprite;
		else if (_cardTemplateImage != null)
			preferred = _cardTemplateImage.sprite;

		return _cachedCardBackSprite = CardBackSpriteUtility.ResolveOpaqueBack(preferred);
	}

	private Sprite LoadCardSprite(TarotCard card)
	{
		if (card == null || string.IsNullOrEmpty(card.cardId))
			return ResolveCardBackSprite();

		Sprite sprite = TarotSpriteLoader.Load(card.cardId);
		if (sprite != null) return sprite;

		sprite = Resources.Load<Sprite>($"TarotCards/{card.cardId}");
		return sprite != null ? sprite : ResolveCardBackSprite();
	}

	private void SuppressPointerClickBriefly()
	{
		_suppressPointerClick = true;
		_clickSuppressTween?.Kill(false);
		_clickSuppressTween = DOVirtual.DelayedCall(0.18f, () =>
		{
			_suppressPointerClick = false;
			_dragMoved = false;
			SetCardsInteractable(_isChoosing && !_isAnimating);
			_clickSuppressTween = null;
		});
	}

	private void KillFlow()
	{
		_flowSequence?.Kill(false);
		_flowSequence = null;
		StopPendingSingleClick();
		_clickSuppressTween?.Kill(false);
		_clickSuppressTween = null;
		_pullBackTween?.Kill(false);
		_pullBackTween = null;
		StopTapHintPulse();
		StopDeckInertia();

		IList<Image> cardsToKill = GetShuffleCards();
		for (int i = 0; i < cardsToKill.Count; i++)
		{
			Image image = cardsToKill[i];
			if (image == null) continue;
			DOTween.Kill(image);
			DOTween.Kill(image.rectTransform);

			Outline outline = image.GetComponent<Outline>();
			if (outline != null)
				DOTween.Kill(outline);
		}

		if (_desText != null)
			DOTween.Kill(_desText);

		ClearSparks();
	}

	private static Color WithAlpha(Color color, float alpha)
	{
		color.a = alpha;
		return color;
	}

	private static void ClearPendingCallbacks()
	{
		PendingDrawProvider = null;
		PendingDrawCompleted = null;
		PendingDrawCanceled = null;
	}
}

internal static class CardBackSpriteUtility
{
	private const int TextureWidth = 256;
	private const int TextureHeight = 438;
	private static Sprite _fallbackSprite;

	public static Sprite ResolveOpaqueBack(Sprite preferred)
	{
		return IsKnownOpaqueBack(preferred) ? preferred : GetFallbackSprite(preferred);
	}

	private static bool IsKnownOpaqueBack(Sprite sprite)
	{
		if (sprite == null) return false;

		return ContainsKnownOpaqueName(sprite.name)
			|| (sprite.texture != null && ContainsKnownOpaqueName(sprite.texture.name));
	}

	private static bool ContainsKnownOpaqueName(string value)
	{
		return !string.IsNullOrEmpty(value)
			&& value.IndexOf("icon_card_background", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static Sprite GetFallbackSprite(Sprite preferred)
	{
		if (_fallbackSprite != null) return _fallbackSprite;

		Texture2D texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false)
		{
			name = "runtime_opaque_card_back",
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp
		};

		Color32 background = new Color32(31, 31, 35, 255);
		Color32 inner = new Color32(39, 28, 28, 255);
		Color32 border = new Color32(254, 142, 84, 255);
		Color32 accent = new Color32(254, 142, 84, 255);
		Color32[] pixels = new Color32[TextureWidth * TextureHeight];

		for (int y = 0; y < TextureHeight; y++)
		{
			for (int x = 0; x < TextureWidth; x++)
			{
				pixels[y * TextureWidth + x] = IsInsideRoundedRect(x, y, 8, TextureWidth - 9, 8, TextureHeight - 9, 18)
					? inner
					: background;
			}
		}

		DrawRoundedRectOutline(pixels, 14, TextureWidth - 15, 14, TextureHeight - 15, 16, 4, border);
		DrawFourPointStar(pixels, TextureWidth / 2, TextureHeight / 2, 26, 10, accent);
		DrawFourPointStar(pixels, TextureWidth / 2 - 32, TextureHeight / 2 + 4, 10, 4, accent);
		DrawFourPointStar(pixels, TextureWidth / 2 + 32, TextureHeight / 2 - 4, 10, 4, accent);

		texture.SetPixels32(pixels);
		texture.Apply(false, true);

		float pixelsPerUnit = preferred != null ? preferred.pixelsPerUnit : 100f;
		_fallbackSprite = Sprite.Create(
			texture,
			new Rect(0f, 0f, TextureWidth, TextureHeight),
			new Vector2(0.5f, 0.5f),
			pixelsPerUnit,
			0,
			SpriteMeshType.FullRect);
		_fallbackSprite.name = "runtime_opaque_card_back";
		return _fallbackSprite;
	}

	private static bool IsInsideRoundedRect(int x, int y, int left, int right, int bottom, int top, int radius)
	{
		if (x < left || x > right || y < bottom || y > top)
			return false;

		int cx = x < left + radius ? left + radius : x > right - radius ? right - radius : x;
		int cy = y < bottom + radius ? bottom + radius : y > top - radius ? top - radius : y;
		int dx = x - cx;
		int dy = y - cy;
		return dx * dx + dy * dy <= radius * radius;
	}

	private static void DrawRoundedRectOutline(Color32[] pixels, int left, int right, int bottom, int top, int radius, int thickness, Color32 color)
	{
		for (int y = bottom; y <= top; y++)
		{
			for (int x = left; x <= right; x++)
			{
				bool outer = IsInsideRoundedRect(x, y, left, right, bottom, top, radius);
				bool inner = IsInsideRoundedRect(x, y, left + thickness, right - thickness, bottom + thickness, top - thickness, Mathf.Max(1, radius - thickness));
				if (outer && !inner)
					pixels[y * TextureWidth + x] = color;
			}
		}
	}

	private static void DrawFourPointStar(Color32[] pixels, int centerX, int centerY, int outerRadius, int innerRadius, Color32 color)
	{
		for (int y = centerY - outerRadius; y <= centerY + outerRadius; y++)
		{
			if (y < 0 || y >= TextureHeight) continue;

			for (int x = centerX - outerRadius; x <= centerX + outerRadius; x++)
			{
				if (x < 0 || x >= TextureWidth) continue;

				int dx = Math.Abs(x - centerX);
				int dy = Math.Abs(y - centerY);
				float limit = outerRadius - dy * (outerRadius - innerRadius) / Mathf.Max(1f, outerRadius);
				if (dx + dy <= limit)
					pixels[y * TextureWidth + x] = color;
			}
		}
	}
}
