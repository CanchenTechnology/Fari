/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/30/2026 3:50:29 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using GamerFrameWork.UIFrameWork;
using UnityEngine.Video;

public class DrawMyCardUI : WindowBase
{
	private const string DeckCardNamePrefix = "DrawMyCardDeck_";
	private const string FlyingCardName = "DrawMyCardFlyingCard";
	private const float DefaultShuffleScatterDuration = 1.6f;
	private const float DefaultShuffleGatherDuration = 0.83f;
	private static RelationshipDivinationRecord sPendingRecord;
	private static FriendDataManager.FriendData sPendingFriend;

	public DrawMyCardUIComponent uiComponent;

	private readonly List<Image> _deckImages = new List<Image>();
	private readonly List<int> _deckSiblingOrder = new List<int>();

	private RectTransform _rootRect;
	private RectTransform _cardContainer;
	private Image _cardTemplateImage;
	private RectTransform _cardSlotRect;
	private RectTransform _cardBackRect;
	private RectTransform _cardFrontRect;
	private CanvasGroup _openPanelGroup;
	private Image _flyingCard;
	private Sequence _flowSequence;
	private Sequence _deckShuffleSequence;
	private Tween _deckInertiaTween;
	private Tween _clickSuppressTween;
	private Coroutine _singleClickCoroutine;
	private Coroutine _longPressCoroutine;
	private Coroutine _idleVideoCoroutine;
	private RenderTexture _idleVideoTextureInstance;
	private RenderTexture _sharedIdleVideoTexture;

	private (TarotCard card, bool upright) _drawResult;
	private RelationshipDivinationRecord currentRecord;
	private FriendDataManager.FriendData currentFriend;
	private bool _hasDrawResult;
	private bool _revealSubmitted;
	private bool _isChoosingCard;
	private bool _isAnimatingCard;
	private bool _isPanelShown;
	private bool _hasOpenedCard;
	private bool _pointerActive;
	private bool _dragMoved;
	private bool _dragStartedFromCard;
	private bool _isPullingCard;
	private bool _suppressPointerClick;
	private int _activeDragCardIndex = -1;
	private int _pendingClickDeckIndex = -1;
	private float _lastDeckClickTime = -100f;
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
	private Vector2 _lastDeckViewportSize;
	private int _idleVideoPlayRequestId;
	private bool _idleVideoHasStarted;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<DrawMyCardUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("DrawMyCardUI 缺少 UI 组件绑定脚本：DrawMyCardUIComponent");
			return;
		}

		uiComponent.InitComponent(this);
		ResolveReferences();
		ResolveIdleVideoReference();
		ConfigureIdleVideoPlayer();
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}

	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		currentRecord = sPendingRecord;
		currentFriend = sPendingFriend;
		PlayIdleVideo();
		ResetDrawView();
	}

	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
		KillFlow();
		ClearRuntimeDeckCards();
		SetOpenPanelVisible(false, true);
		PauseIdleVideo();
	}

	// 物体销毁时执行
	public override void OnDestroy()
	{
		KillFlow();
		ClearRuntimeDeckCards();
		ReleaseIdleVideoTextureInstance();
		base.OnDestroy();
	}

	private void LateUpdate()
	{
		if (!_isChoosingCard || _isAnimatingCard || _isPullingCard || _deckImages.Count <= 0)
			return;

		RefreshDeckLayoutIfViewportChanged();
		RefreshDeckSiblingOrder();
	}
	#endregion

	#region API Function
	public static DrawMyCardUI Show(RelationshipDivinationRecord record, FriendDataManager.FriendData friend = null)
	{
		if (record == null)
		{
			ToastManager.ShowToast("双人占卜记录不存在");
			return null;
		}

		sPendingRecord = record;
		sPendingFriend = friend;
		DrawMyCardUI window = UIModule.Instance.PopUpWindow<DrawMyCardUI>();
		window?.SetRecord(record, friend);
		return window;
	}

	public void SetRecord(RelationshipDivinationRecord record, FriendDataManager.FriendData friend = null)
	{
		if (record == null)
			return;

		sPendingRecord = record;
		sPendingFriend = friend;
		currentRecord = record;
		currentFriend = friend;
		if (gameObject != null && gameObject.activeInHierarchy)
			ResetDrawView();
	}

	#endregion

	#region UI组件事件
	public void OnbackButtonClick()
	{
		if (_isAnimatingCard || _revealSubmitted) return;
		HideWindow();
	}

	public void OnCancelBtnClick()
	{
		if (_isAnimatingCard || _revealSubmitted) return;

		_hasDrawResult = false;
		_revealSubmitted = false;
		_hasOpenedCard = false;
		_isPanelShown = false;
		SetNextButtonState(false, false);
		SetOpenPanelVisible(false, true);
		PrepareSlotBack();
		RestoreDeckAfterCancel();
	}

	public void OnOpenBtnClick()
	{
		if (_isAnimatingCard || !_isPanelShown || _hasOpenedCard)
			return;

		EnsureDrawResult();
		PlayOpenCardAnimation();
	}

	public void OnNextBtnClick()
	{
		if (_isAnimatingCard || _revealSubmitted || currentRecord == null)
			return;

		if (!_hasOpenedCard)
			return;

		if (currentRecord.IsCompleted || currentRecord.isLocalOnly)
		{
			HideWindow();
			RelationshipDivinationFlow.OpenResult(currentRecord);
			return;
		}

		if (RelationshipDivinationFlow.IsMyCardRevealed(currentRecord))
		{
			HideWindow();
			TwoPersonDivinationUI.Show(currentRecord, currentFriend);
			return;
		}

		SubmitRelationshipRevealIfNeeded();
	}
	#endregion

	private void ResetDrawView()
	{
		ResolveReferences();
		KillFlow();
		ClearRuntimeDeckCards();

		_hasDrawResult = false;
		_revealSubmitted = false;
		_isChoosingCard = false;
		_isAnimatingCard = false;
		_isPanelShown = false;
		_hasOpenedCard = false;
		_deckOffsetX = 0f;
		_scrollVelocityX = 0f;
		_lastDeckViewportSize = Vector2.zero;

		SetOpenPanelVisible(false, true);
		SetNextButtonState(false, false);
		PrepareSlotBack();

		if (_cardContainer != null)
			_cardContainer.gameObject.SetActive(true);

		BuildDeckFan();
		PlayDeckShuffleIntro();
	}

	private void ResolveReferences()
	{
		_rootRect = transform as RectTransform;
		_cardContainer = uiComponent != null ? uiComponent.cardContainer as RectTransform : null;
		_cardTemplateImage = uiComponent != null && uiComponent.cardGO != null
			? uiComponent.cardGO.GetComponent<Image>()
			: null;
		_cardSlotRect = uiComponent != null && uiComponent.cardSlot != null
			? uiComponent.cardSlot.transform as RectTransform
			: null;
		_cardBackRect = uiComponent != null && uiComponent.cardBack != null
			? uiComponent.cardBack.transform as RectTransform
			: null;
		_cardFrontRect = uiComponent != null && uiComponent.cardFront != null
			? uiComponent.cardFront.transform as RectTransform
			: null;

		if (uiComponent != null && uiComponent.cardGO != null)
			uiComponent.cardGO.SetActive(false);

		Sprite backSprite = ResolveCardBackSprite();
		Image templateImage = _cardTemplateImage;
		if (templateImage != null && backSprite != null)
			templateImage.sprite = backSprite;

		Image backImage = uiComponent != null && uiComponent.cardBack != null
			? uiComponent.cardBack.GetComponent<Image>()
			: null;
		if (backImage != null && backSprite != null)
			backImage.sprite = backSprite;
	}

	private void ResolveIdleVideoReference()
	{
		if (uiComponent == null || uiComponent.idleVideoPlayer != null)
			return;

		uiComponent.idleVideoPlayer = transform.Find("video")?.GetComponent<VideoPlayer>();
		if (uiComponent.idleVideoPlayer == null)
			uiComponent.idleVideoPlayer = gameObject.GetComponentInChildren<VideoPlayer>(true);
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
		RenderTextureDescriptor descriptor = sourceTexture.descriptor;
		_idleVideoTextureInstance = new RenderTexture(descriptor)
		{
			name = $"{sourceTexture.name}_DrawMyCardRuntime",
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
			Debug.LogWarning("[DrawMyCardUI] idleVideoPlayer is not assigned.");
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

		VideoPlayer videoPlayer = uiComponent?.idleVideoPlayer;
		if (videoPlayer != null && videoPlayer.isPlaying)
			videoPlayer.Pause();
	}

	private IEnumerator PlayIdleVideoRoutine(int requestId)
	{
		VideoPlayer videoPlayer = uiComponent?.idleVideoPlayer;
		if (videoPlayer == null)
		{
			Debug.LogWarning("[DrawMyCardUI] idleVideoPlayer is not assigned.");
			yield break;
		}

		if (videoPlayer.clip == null)
		{
			Debug.LogWarning("[DrawMyCardUI] idleVideoPlayer has no video clip.");
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

	private void BuildDeckFan()
	{
		if (_cardContainer == null || _cardTemplateImage == null)
			return;

		ResolveDeckLayoutMetrics();
		int count = Mathf.Max(3, uiComponent.selectableCardCount);
		Sprite backSprite = ResolveCardBackSprite();

		for (int i = 0; i < count; i++)
		{
			GameObject cardObject = UnityEngine.Object.Instantiate(_cardTemplateImage.gameObject, _cardContainer);
			cardObject.name = $"{DeckCardNamePrefix}{i + 1}";
			cardObject.SetActive(true);

			Image image = cardObject.GetComponent<Image>();
			if (image == null)
				image = cardObject.AddComponent<Image>();

			image.sprite = backSprite;
			image.color = Color.white;
			image.raycastTarget = true;
			DisableCardLocalSorting(cardObject);

			RectTransform cardRect = image.rectTransform;
			cardRect.anchorMin = new Vector2(0.5f, 0.5f);
			cardRect.anchorMax = new Vector2(0.5f, 0.5f);
			cardRect.pivot = new Vector2(0.5f, 0.5f);

			BindDeckCardInteractions(cardObject, i);
			_deckImages.Add(image);
		}

		PlaceDeckAtStackPose();
		SetDeckCardsInteractable(false);
	}

	private void PlayDeckShuffleIntro()
	{
		StopDeckShuffleSequence();
		StopDeckInertia();
		StopLongPressSelection();
		StopPendingSingleClick();

		if (_deckImages.Count == 0)
		{
			FinishDeckShuffleIntro();
			return;
		}

		_deckOffsetX = 0f;
		_isChoosingCard = false;
		_isAnimatingCard = true;
		SetDeckCardsInteractable(false);
		PlayDeckScatterAnimation(() => PlayDeckGatherAnimation(PlayDeckFanOutFromStack));
	}

	private void PlayDeckScatterAnimation(Action onComplete)
	{
		StopDeckShuffleSequence();
		ResolveDeckLayoutMetrics();

		_deckShuffleSequence = DOTween.Sequence();
		float scatterDuration = Mathf.Max(0.58f, uiComponent.shuffleScatterDuration > 0f
			? uiComponent.shuffleScatterDuration
			: DefaultShuffleScatterDuration);

		for (int i = 0; i < _deckImages.Count; i++)
		{
			Image image = _deckImages[i];
			if (image == null) continue;

			RectTransform rect = image.rectTransform;
			DOTween.Kill(image);
			DOTween.Kill(rect);

			float delay = Mathf.Lerp(0f, 0.18f, Hash01(i * 23 + 5));
			float duration = Mathf.Max(0.58f, scatterDuration - delay - Mathf.Lerp(0.04f, 0.16f, Hash01(i * 19 + 7)));

			_deckShuffleSequence.Insert(delay, rect.DOAnchorPos(GetDeckChaosPose(i, _deckImages.Count), duration)
				.SetEase(Ease.InOutCubic));
			_deckShuffleSequence.Insert(delay, rect.DORotate(new Vector3(0f, 0f, GetDeckChaosRotation(i)), duration)
				.SetEase(Ease.InOutCubic));
			_deckShuffleSequence.Insert(delay, rect.DOScale(Vector3.one * _deckCardScale, duration)
				.SetEase(Ease.InOutCubic));
		}

		_deckShuffleSequence.AppendInterval(Mathf.Max(0f, scatterDuration - _deckShuffleSequence.Duration()));
		_deckShuffleSequence.OnComplete(() =>
		{
			_deckShuffleSequence = null;
			onComplete?.Invoke();
		});
	}

	private void PlayDeckGatherAnimation(Action onComplete)
	{
		StopDeckShuffleSequence();
		ResolveDeckLayoutMetrics();

		_deckShuffleSequence = DOTween.Sequence();
		float gatherDuration = Mathf.Max(0.28f, uiComponent.shuffleGatherDuration > 0f
			? uiComponent.shuffleGatherDuration
			: DefaultShuffleGatherDuration);

		for (int i = 0; i < _deckImages.Count; i++)
		{
			Image image = _deckImages[i];
			if (image == null) continue;

			RectTransform rect = image.rectTransform;
			DOTween.Kill(rect);
			_deckShuffleSequence.Insert(0f, rect.DOAnchorPos(GetDeckShuffleStackPose(i, _deckImages.Count), gatherDuration)
				.SetEase(Ease.OutBack, 0.95f));
			_deckShuffleSequence.Insert(0f, rect.DORotate(new Vector3(0f, 0f, GetDeckShuffleStackRotation(i)), gatherDuration)
				.SetEase(Ease.OutBack, 0.95f));
			_deckShuffleSequence.Insert(0f, rect.DOScale(Vector3.one * _deckCardScale, gatherDuration)
				.SetEase(Ease.OutBack, 0.95f));
		}

		_deckShuffleSequence.OnComplete(() =>
		{
			_deckShuffleSequence = null;
			onComplete?.Invoke();
		});
	}

	private void PlayDeckFanOutFromStack()
	{
		StopDeckShuffleSequence();
		ResolveDeckLayoutMetrics();

		_deckShuffleSequence = DOTween.Sequence();
		float fanOutDuration = Mathf.Max(0.32f, uiComponent.shuffleFanOutDuration);
		float fanOutGap = Mathf.Max(0.035f, uiComponent.shuffleFanOutGap);
		List<int> fanOutOrder = BuildLeftToRightDeckFanOutOrder();

		for (int orderIndex = 0; orderIndex < fanOutOrder.Count; orderIndex++)
		{
			int deckIndex = fanOutOrder[orderIndex];
			Image image = _deckImages[deckIndex];
			if (image == null) continue;

			RectTransform rect = image.rectTransform;
			DOTween.Kill(image);
			DOTween.Kill(rect);

			Vector2 sourcePosition = rect.anchoredPosition;
			Vector2 targetPosition = GetDeckFanPosition(deckIndex);
			float targetRotation = GetDeckFanRotation(deckIndex);
			float lift = Mathf.Clamp(_viewportHeight * 0.03f, 28f, 58f);
			Vector2 stagingPosition = Vector2.Lerp(sourcePosition, targetPosition, 0.54f) + new Vector2(0f, lift);
			Vector3 targetScale = Vector3.one * _deckCardScale;

			float startTime = orderIndex * fanOutGap;
			float liftDuration = fanOutDuration * 0.42f;
			float settleDuration = fanOutDuration - liftDuration;
			Image capturedImage = image;
			Sequence cardSequence = DOTween.Sequence();
			cardSequence.Append(rect.DOAnchorPos(stagingPosition, liftDuration).SetEase(Ease.OutCubic));
			cardSequence.Append(rect.DOAnchorPos(targetPosition, settleDuration).SetEase(Ease.OutQuart));

			_deckShuffleSequence.InsertCallback(startTime, () => BringDeckFanOutCardToTop(capturedImage));
			_deckShuffleSequence.Insert(startTime, image.DOFade(1f, fanOutDuration * 0.45f));
			_deckShuffleSequence.Insert(startTime, cardSequence);
			_deckShuffleSequence.Insert(startTime, rect.DORotate(new Vector3(0f, 0f, targetRotation), fanOutDuration)
				.SetEase(Ease.InOutCubic));
			_deckShuffleSequence.Insert(startTime, rect.DOScale(targetScale * 1.025f, liftDuration).SetEase(Ease.OutSine));
			_deckShuffleSequence.Insert(startTime + liftDuration, rect.DOScale(targetScale, settleDuration).SetEase(Ease.OutCubic));
		}

		_deckShuffleSequence.OnComplete(FinishDeckShuffleIntro);
	}

	private void FinishDeckShuffleIntro()
	{
		_deckShuffleSequence = null;
		_isChoosingCard = _deckImages.Count > 0;
		_isAnimatingCard = false;
		_lastDeckViewportSize = GetDeckViewportSize();
		RefreshDeckSiblingOrder();
		SetDeckCardsInteractable(_isChoosingCard);
	}

	private void PlaceDeckAtStackPose()
	{
		ResolveDeckLayoutMetrics();

		for (int i = 0; i < _deckImages.Count; i++)
		{
			Image image = _deckImages[i];
			if (image == null) continue;

			RectTransform rect = image.rectTransform;
			DOTween.Kill(image);
			DOTween.Kill(rect);
			image.enabled = true;
			image.color = Color.white;
			image.raycastTarget = false;
			rect.anchoredPosition = GetDeckShuffleStackPose(i, _deckImages.Count);
			rect.localRotation = Quaternion.Euler(0f, 0f, GetDeckShuffleStackRotation(i));
			rect.localScale = Vector3.one * _deckCardScale;
		}

		RefreshDeckSiblingOrder();
	}

	private List<int> BuildLeftToRightDeckFanOutOrder()
	{
		List<int> order = new List<int>(_deckImages.Count);
		for (int i = 0; i < _deckImages.Count; i++)
		{
			if (_deckImages[i] != null)
				order.Add(i);
		}

		order.Sort((left, right) =>
		{
			float leftX = GetDeckFanPosition(left).x;
			float rightX = GetDeckFanPosition(right).x;
			int xCompare = leftX.CompareTo(rightX);
			return xCompare != 0 ? xCompare : left.CompareTo(right);
		});

		return order;
	}

	private void BringDeckFanOutCardToTop(Image image)
	{
		if (image == null) return;
		image.rectTransform.SetAsLastSibling();
	}

	private Vector2 GetDeckShuffleAnchor()
	{
		return new Vector2(0f, Mathf.Clamp(_viewportHeight * 0.1f, 58f, 128f));
	}

	private Vector2 GetDeckShuffleStackPose(int index, int count)
	{
		Vector2 anchor = GetDeckShuffleAnchor();
		float centeredIndex = index - (count - 1) * 0.5f;
		float x = anchor.x + centeredIndex * 0.34f + Mathf.Sin(index * 0.61f) * 2.2f;
		float y = anchor.y - centeredIndex * 0.5f + (index % 7) * 0.55f;
		return new Vector2(x, y);
	}

	private float GetDeckShuffleStackRotation(int index)
	{
		return Mathf.Sin(index * 0.7f) * 1.05f;
	}

	private Vector2 GetDeckChaosPose(int index, int count)
	{
		Vector2 anchor = GetDeckShuffleAnchor();
		float maxRadius = Mathf.Clamp(_viewportWidth * 0.23f, 132f, 220f);
		float radius = Mathf.Lerp(44f, maxRadius, Hash01(index * 17 + 3));
		float angle = (index * 137.507f + Hash01(index * 31 + 9) * 48f) * Mathf.Deg2Rad;
		float x = Mathf.Cos(angle) * radius;
		float y = Mathf.Sin(angle) * radius * 0.68f + Mathf.Sin(index * 0.33f) * 18f;
		return anchor + new Vector2(x, y);
	}

	private float GetDeckChaosRotation(int index)
	{
		return Mathf.Lerp(-34f, 34f, Hash01(index * 29 + 11));
	}

	private static float Hash01(int seed)
	{
		return Mathf.Repeat(Mathf.Sin(seed * 12.9898f) * 43758.5453f, 1f);
	}

	private void BindDeckCardInteractions(GameObject cardObject, int index)
	{
		if (cardObject == null) return;

		EventTrigger trigger = cardObject.GetComponent<EventTrigger>();
		if (trigger == null)
			trigger = cardObject.AddComponent<EventTrigger>();
		if (trigger.triggers == null)
			trigger.triggers = new List<EventTrigger.Entry>();

		trigger.triggers.Clear();
		AddEventTriggerEntry(trigger, EventTriggerType.PointerDown, data => OnDeckPointerDown(data, index));
		AddEventTriggerEntry(trigger, EventTriggerType.PointerClick, data => OnDeckCardPointerClick(data, index));
		AddEventTriggerEntry(trigger, EventTriggerType.BeginDrag, OnDeckBeginDrag);
		AddEventTriggerEntry(trigger, EventTriggerType.Drag, OnDeckDrag);
		AddEventTriggerEntry(trigger, EventTriggerType.EndDrag, OnDeckEndDrag);
		AddEventTriggerEntry(trigger, EventTriggerType.PointerUp, OnDeckPointerUp);
	}

	private static void AddEventTriggerEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
	{
		var entry = new EventTrigger.Entry { eventID = type };
		entry.callback.AddListener(callback);
		trigger.triggers.Add(entry);
	}

	private void OnDeckPointerDown(BaseEventData eventData, int cardIndex)
	{
		if (!_isChoosingCard || _isAnimatingCard || _isPanelShown) return;
		if (!TryGetDeckLocalPoint(eventData, out Vector2 localPoint)) return;

		StopDeckInertia();
		StopLongPressSelection();
		_clickSuppressTween?.Kill(false);

		_pointerActive = true;
		_activeDragCardIndex = cardIndex;
		_dragStartedFromCard = cardIndex >= 0;
		_dragMoved = false;
		_suppressPointerClick = false;
		_isPullingCard = false;
		_activePullY = 0f;
		_dragStartLocalX = localPoint.x;
		_dragStartLocalY = localPoint.y;
		_dragStartOffsetX = _deckOffsetX;
		_lastScrollSampleX = localPoint.x;
		_lastScrollSampleTime = Time.unscaledTime;
		_scrollVelocityX = 0f;

		if (_dragStartedFromCard)
			_longPressCoroutine = uiComponent.StartCoroutine(LongPressSelectRoutine(cardIndex));
	}

	private void OnDeckBeginDrag(BaseEventData eventData)
	{
		if (!_isChoosingCard || _isAnimatingCard) return;
	}

	private void OnDeckDrag(BaseEventData eventData)
	{
		if (!_pointerActive || !_isChoosingCard || _isAnimatingCard || _isPanelShown) return;
		if (!TryGetDeckLocalPoint(eventData, out Vector2 localPoint)) return;

		float deltaX = localPoint.x - _dragStartLocalX;
		float deltaY = localPoint.y - _dragStartLocalY;
		float clickThreshold = Mathf.Max(1f, uiComponent.dragClickThreshold);

		if (!_dragMoved && new Vector2(deltaX, deltaY).magnitude >= clickThreshold)
		{
			_dragMoved = true;
			StopLongPressSelection();
			StopPendingSingleClick();
		}

		if (_dragStartedFromCard && _activeDragCardIndex >= 0)
		{
			float directionBias = Mathf.Max(0.1f, uiComponent.dragPullDirectionBias);
			if (!_isPullingCard
				&& deltaY > clickThreshold
				&& deltaY > Mathf.Abs(deltaX) * directionBias)
			{
				_isPullingCard = true;
			}
		}

		if (_isPullingCard)
		{
			_activePullY = Mathf.Max(0f, deltaY);
			PreviewPulledDeckCard(_activeDragCardIndex, deltaX, deltaY);
			return;
		}

		float sensitivity = Mathf.Max(0.01f, uiComponent.dragSensitivity);
		_deckOffsetX = NormalizeDeckOffset(_dragStartOffsetX + deltaX * sensitivity);

		float now = Time.unscaledTime;
		float deltaTime = Mathf.Max(0.001f, now - _lastScrollSampleTime);
		_scrollVelocityX = (localPoint.x - _lastScrollSampleX) / deltaTime;
		_lastScrollSampleX = localPoint.x;
		_lastScrollSampleTime = now;

		ApplyDeckLayout(false);
	}

	private void OnDeckEndDrag(BaseEventData eventData)
	{
		ReleaseDeckPointer();
	}

	private void OnDeckPointerUp(BaseEventData eventData)
	{
		ReleaseDeckPointer();
	}

	private void OnDeckCardPointerClick(BaseEventData eventData, int index)
	{
		if (!_isChoosingCard || _isAnimatingCard || _isPanelShown) return;
		if (_suppressPointerClick || _dragMoved) return;

		float now = Time.unscaledTime;
		float interval = Mathf.Clamp(uiComponent.doubleClickSelectInterval, 0.05f, 0.6f);
		bool doubleClick = eventData is PointerEventData pointerData && pointerData.clickCount >= 2;
		doubleClick = doubleClick || (_pendingClickDeckIndex == index && now - _lastDeckClickTime <= interval);

		if (doubleClick)
		{
			StopPendingSingleClick();
			SelectDeckCard(index);
			return;
		}

		StopPendingSingleClick();
		_pendingClickDeckIndex = index;
		_lastDeckClickTime = now;
		_singleClickCoroutine = uiComponent.StartCoroutine(SingleClickSelectRoutine(index, interval));
	}

	private IEnumerator SingleClickSelectRoutine(int index, float delay)
	{
		yield return new WaitForSecondsRealtime(delay);
		_singleClickCoroutine = null;
		_pendingClickDeckIndex = -1;

		if (!_isChoosingCard || _isAnimatingCard || _isPanelShown) yield break;
		if (_suppressPointerClick || _dragMoved) yield break;
		SelectDeckCard(index);
	}

	private IEnumerator LongPressSelectRoutine(int index)
	{
		yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, uiComponent.longPressSelectDuration));
		_longPressCoroutine = null;

		if (!_isChoosingCard || _isAnimatingCard || _isPanelShown) yield break;
		if (_dragMoved || _activeDragCardIndex != index) yield break;

		_suppressPointerClick = true;
		SelectDeckCard(index);
	}

	private void ReleaseDeckPointer()
	{
		if (!_pointerActive) return;

		_pointerActive = false;
		StopLongPressSelection();

		if (_isPullingCard)
		{
			int releasedIndex = _activeDragCardIndex;
			bool shouldSelect = _activePullY >= Mathf.Max(1f, uiComponent.dragPullSelectThreshold);
			_isPullingCard = false;
			_activePullY = 0f;

			if (shouldSelect)
				SelectDeckCard(releasedIndex);
			else
				SnapPulledDeckCardBack(releasedIndex);

			SuppressPointerClickBriefly();
			return;
		}

		if (_dragMoved)
		{
			SuppressPointerClickBriefly();
			StartDeckInertia();
		}

		_dragStartedFromCard = false;
		_activeDragCardIndex = -1;
	}

	private bool TryGetDeckLocalPoint(BaseEventData eventData, out Vector2 localPoint)
	{
		localPoint = Vector2.zero;
		if (_cardContainer == null || eventData is not PointerEventData pointerData)
			return false;

		return RectTransformUtility.ScreenPointToLocalPointInRectangle(
			_cardContainer,
			pointerData.position,
			pointerData.pressEventCamera,
			out localPoint);
	}

	private void SelectDeckCard(int index)
	{
		if (!_isChoosingCard || _isAnimatingCard || _isPanelShown) return;
		if (index < 0 || index >= _deckImages.Count) return;

		StopLongPressSelection();
		StopPendingSingleClick();
		StopDeckInertia();
		EnsureDrawResult();

		_isChoosingCard = false;
		_isAnimatingCard = true;
		_dragMoved = true;
		_suppressPointerClick = true;
		SetDeckCardsInteractable(false);

		PlaySelectedCardToSlot(index);
	}

	private void PlaySelectedCardToSlot(int deckIndex)
	{
		Image sourceImage = deckIndex >= 0 && deckIndex < _deckImages.Count ? _deckImages[deckIndex] : null;
		if (sourceImage == null || _cardSlotRect == null || _rootRect == null)
		{
			ShowOpenPanelAfterSelection(null);
			return;
		}

		Canvas.ForceUpdateCanvases();
		RectTransform sourceRect = sourceImage.rectTransform;
		_flyingCard = UnityEngine.Object.Instantiate(sourceImage, _rootRect);
		_flyingCard.name = FlyingCardName;
		_flyingCard.raycastTarget = false;
		_flyingCard.sprite = ResolveCardBackSprite();
		_flyingCard.color = Color.white;

		RectTransform flyRect = _flyingCard.rectTransform;
		CopyRectWorldPose(sourceRect, flyRect, _rootRect);
		flyRect.SetAsLastSibling();
		DisableCardLocalSorting(_flyingCard.gameObject);

		SetImageAlpha(sourceImage, 0f);
		FadeDeckExcept(sourceImage, 0f, Mathf.Max(0f, uiComponent.otherCardFadeDuration));

		ResolveRectTargetPose(_cardSlotRect, _rootRect, sourceRect, out Vector2 targetPosition, out float targetRotationZ, out float targetScale);

		_flowSequence?.Kill(false);
		_flowSequence = DOTween.Sequence();
		float selectDuration = Mathf.Max(0.12f, uiComponent.selectDuration);
		_flowSequence.Append(flyRect.DOAnchorPos(targetPosition, selectDuration).SetEase(Ease.InOutCubic));
		_flowSequence.Join(flyRect.DORotate(new Vector3(0f, 0f, targetRotationZ), selectDuration).SetEase(Ease.InOutCubic));
		_flowSequence.Join(flyRect.DOScale(Vector3.one * targetScale, selectDuration).SetEase(Ease.OutCubic));
		_flowSequence.AppendCallback(() => ShowOpenPanelAfterSelection(_flyingCard));
		_flowSequence.AppendInterval(Mathf.Max(0f, uiComponent.openPanelFadeDuration));
		_flowSequence.OnComplete(() =>
		{
			if (_flyingCard != null)
				DestroyDeckObject(_flyingCard.gameObject);
			_flyingCard = null;
			_isAnimatingCard = false;
			_isPanelShown = true;
			SetOpenButtonsInteractable(true, true);
			_flowSequence = null;
		});
	}

	private void ShowOpenPanelAfterSelection(Image flyingCard)
	{
		PrepareSlotBack();
		SetOpenButtonsInteractable(false, false);
		SetNextButtonState(false, false);
		SetOpenPanelVisible(true, false);

		if (flyingCard != null)
		{
			flyingCard.transform.SetAsLastSibling();
			flyingCard.raycastTarget = false;
		}
	}

	private void PlayOpenCardAnimation()
	{
		if (_cardSlotRect == null)
			return;

		_isAnimatingCard = true;
		SetOpenButtonsInteractable(false, false);
		PrepareSlotBack();

		Vector2 originalPosition = _cardSlotRect.anchoredPosition;
		Vector3 originalScale = _cardSlotRect.localScale;
		float halfFlipDuration = Mathf.Max(0.08f, uiComponent.flipDuration * 0.5f);

		_flowSequence?.Kill(false);
		_flowSequence = DOTween.Sequence();
		AppendSlotShake(_flowSequence, originalPosition, originalScale);
		_flowSequence.Append(_cardSlotRect.DORotate(new Vector3(0f, 88f, 0f), halfFlipDuration).SetEase(Ease.InCubic));
		_flowSequence.AppendCallback(() =>
		{
			ShowSlotFront();
			_cardSlotRect.localRotation = Quaternion.Euler(0f, 88f, 0f);
		});
		_flowSequence.Append(_cardSlotRect.DORotate(Vector3.zero, halfFlipDuration).SetEase(Ease.OutCubic));
		if (uiComponent.frontRevealHoldDuration > 0f)
			_flowSequence.AppendInterval(uiComponent.frontRevealHoldDuration);
		_flowSequence.OnComplete(() =>
		{
			_cardSlotRect.anchoredPosition = originalPosition;
			_cardSlotRect.localRotation = Quaternion.identity;
			_cardSlotRect.localScale = originalScale;
			_hasOpenedCard = true;
			_isAnimatingCard = false;
			SetOpenButtonsInteractable(false, true);
			_flowSequence = null;
			SetNextButtonState(true, false, "同步中...");
			SubmitRelationshipRevealIfNeeded();
		});
	}

	private void AppendSlotShake(Sequence sequence, Vector2 originalPosition, Vector3 originalScale)
	{
		if (sequence == null || _cardSlotRect == null || uiComponent == null)
			return;

		float duration = Mathf.Max(0f, uiComponent.shakeDuration);
		float positionStrength = Mathf.Max(0f, uiComponent.shakePosition);
		float rotationStrength = Mathf.Max(0f, uiComponent.shakeRotation);
		if (duration <= 0f || (positionStrength <= 0f && rotationStrength <= 0f))
			return;

		float[] offsets = { 1f, -0.9f, 0.78f, -0.66f, 0.54f, -0.42f, 0.3f, -0.18f, 0.1f, 0f };
		float stepDuration = Mathf.Max(0.018f, duration / offsets.Length);
		for (int i = 0; i < offsets.Length; i++)
		{
			float offset = offsets[i];
			sequence.Append(_cardSlotRect.DOAnchorPos(originalPosition + new Vector2(positionStrength * offset, 0f), stepDuration).SetEase(Ease.InOutSine));
			sequence.Join(_cardSlotRect.DORotate(new Vector3(0f, 0f, rotationStrength * offset), stepDuration).SetEase(Ease.InOutSine));
			sequence.Join(_cardSlotRect.DOScale(originalScale, stepDuration).SetEase(Ease.InOutSine));
		}
	}

	private void PrepareSlotBack()
	{
		if (uiComponent == null) return;

		if (uiComponent.cardSlot != null)
		{
			uiComponent.cardSlot.SetActive(true);
			if (_cardSlotRect != null)
			{
				_cardSlotRect.localRotation = Quaternion.identity;
				_cardSlotRect.localScale = Vector3.one;
			}
		}

		if (uiComponent.cardBack != null)
		{
			uiComponent.cardBack.SetActive(true);
			Image backImage = uiComponent.cardBack.GetComponent<Image>();
			Sprite backSprite = ResolveCardBackSprite();
			if (backImage != null && backSprite != null)
				backImage.sprite = backSprite;
		}

		if (uiComponent.cardFront != null)
			uiComponent.cardFront.SetActive(false);

		if (uiComponent.cardTitle != null && !_hasOpenedCard)
			uiComponent.cardTitle.text = "";

		if (uiComponent.cardImage != null)
			uiComponent.cardImage.rectTransform.localRotation = Quaternion.identity;
	}

	private void ShowSlotFront()
	{
		if (!_hasDrawResult)
			EnsureDrawResult();

		if (uiComponent.cardBack != null)
			uiComponent.cardBack.SetActive(false);
		if (uiComponent.cardFront != null)
			uiComponent.cardFront.SetActive(true);

		if (uiComponent.cardImage != null)
		{
			uiComponent.cardImage.sprite = LoadCardSprite(_drawResult.card);
			uiComponent.cardImage.color = Color.white;
			uiComponent.cardImage.rectTransform.localRotation = _drawResult.upright
				? Quaternion.identity
				: Quaternion.Euler(0f, 0f, 180f);
		}

		if (uiComponent.cardTitle != null)
			uiComponent.cardTitle.text = FormatCardName(_drawResult.card, _drawResult.upright);
	}

	private void RestoreDeckAfterCancel()
	{
		KillFlow();
		StopFlyingCard();

		for (int i = 0; i < _deckImages.Count; i++)
		{
			Image image = _deckImages[i];
			if (image == null) continue;

			DOTween.Kill(image);
			DOTween.Kill(image.rectTransform);
			image.gameObject.SetActive(true);
			image.sprite = ResolveCardBackSprite();
			image.color = Color.white;
		}

		_isChoosingCard = _deckImages.Count > 0;
		_isAnimatingCard = false;
		_pointerActive = false;
		_dragStartedFromCard = false;
		_activeDragCardIndex = -1;
		_isPullingCard = false;
		_activePullY = 0f;
		_dragMoved = false;
		_suppressPointerClick = false;
		SetDeckCardsInteractable(_isChoosingCard);
		ApplyDeckLayout(true);
	}

	private void StopFlyingCard()
	{
		if (_flyingCard == null) return;
		DestroyDeckObject(_flyingCard.gameObject);
		_flyingCard = null;
	}

	private void ApplyDeckLayout(bool animated)
	{
		if (_deckImages.Count == 0)
			return;

		ResolveDeckLayoutMetrics();
		_lastDeckViewportSize = GetDeckViewportSize();

		for (int i = 0; i < _deckImages.Count; i++)
		{
			Image image = _deckImages[i];
			if (image == null) continue;
			if (_isPullingCard && i == _activeDragCardIndex) continue;

			RectTransform rect = image.rectTransform;
			Vector2 targetPosition = GetDeckFanPosition(i);
			float targetRotation = GetDeckFanRotation(i);
			Vector3 targetScale = Vector3.one * _deckCardScale;

			DOTween.Kill(rect);
			if (animated)
			{
				rect.DOAnchorPos(targetPosition, 0.18f)
					.SetEase(Ease.OutCubic)
					.OnUpdate(RefreshDeckSiblingOrder)
					.OnComplete(RefreshDeckSiblingOrder);
				rect.DORotate(new Vector3(0f, 0f, targetRotation), 0.18f).SetEase(Ease.OutCubic);
				rect.DOScale(targetScale, 0.18f).SetEase(Ease.OutCubic);
			}
			else
			{
				rect.anchoredPosition = targetPosition;
				rect.localRotation = Quaternion.Euler(0f, 0f, targetRotation);
				rect.localScale = targetScale;
			}
		}

		RefreshDeckSiblingOrder();
	}

	private void PreviewPulledDeckCard(int index, float deltaX, float deltaY)
	{
		if (index < 0 || index >= _deckImages.Count) return;

		Image image = _deckImages[index];
		if (image == null) return;

		RectTransform rect = image.rectTransform;
		DOTween.Kill(rect);

		float pullY = Mathf.Max(0f, deltaY);
		float pullX = Mathf.Clamp(deltaX * 0.24f, -54f, 54f);
		float strength = Mathf.Clamp01(pullY / Mathf.Max(1f, uiComponent.dragPullSelectThreshold));
		rect.anchoredPosition = GetDeckFanPosition(index) + new Vector2(pullX, pullY);
		rect.localScale = Vector3.one * (_deckCardScale * (1f + 0.1f * strength));
		rect.localRotation = Quaternion.Euler(0f, 0f, GetDeckFanRotation(index) + pullX * 0.05f);
		rect.SetAsLastSibling();
	}

	private void SnapPulledDeckCardBack(int index)
	{
		if (index < 0 || index >= _deckImages.Count) return;

		Image image = _deckImages[index];
		if (image == null) return;

		RectTransform rect = image.rectTransform;
		DOTween.Kill(rect);
		Sequence sequence = DOTween.Sequence();
		sequence.Join(rect.DOAnchorPos(GetDeckFanPosition(index), 0.18f).SetEase(Ease.OutCubic));
		sequence.Join(rect.DORotate(new Vector3(0f, 0f, GetDeckFanRotation(index)), 0.18f).SetEase(Ease.OutCubic));
		sequence.Join(rect.DOScale(Vector3.one * _deckCardScale, 0.18f).SetEase(Ease.OutCubic));
		sequence.OnComplete(() =>
		{
			ApplyDeckLayout(false);
			SetDeckCardsInteractable(_isChoosingCard && !_isAnimatingCard);
		});
	}

	private Vector2 GetDeckFanPosition(int index)
	{
		ResolveDeckLayoutMetrics();
		int count = Mathf.Max(1, _deckImages.Count);
		float spacing = count > 1 ? _fanWidth / (count - 1) : _fanWidth;
		float cycleWidth = Mathf.Max(1f, _fanWidth + spacing);
		float x = -_fanWidth * 0.5f + spacing * index + _deckOffsetX;
		x = Mathf.Repeat(x + cycleWidth * 0.5f, cycleWidth) - cycleWidth * 0.5f;

		float normalized = Mathf.Clamp(x / Mathf.Max(1f, _fanWidth * 0.5f), -1f, 1f);
		float y = uiComponent.fanHeightOffset + uiComponent.fanRiseOffset * (1f - normalized * normalized);
		return new Vector2(x, y);
	}

	private float GetDeckFanRotation(int index)
	{
		Vector2 position = GetDeckFanPosition(index);
		float normalized = Mathf.Clamp(position.x / Mathf.Max(1f, _fanWidth * 0.5f), -1f, 1f);
		return -normalized * uiComponent.fanRotation;
	}

	private void ResolveDeckLayoutMetrics()
	{
		RectTransform viewportRoot = _cardContainer;
		if (viewportRoot == null) return;

		Rect rect = viewportRoot.rect;
		_viewportWidth = Mathf.Max(1f, rect.width);
		_viewportHeight = Mathf.Max(1f, rect.height);
		_fanWidth = uiComponent.fanWidth > 0f
			? uiComponent.fanWidth
			: Mathf.Max(uiComponent.minFanWidth, _viewportWidth * Mathf.Max(1f, uiComponent.fanViewportWidthMultiplier));
		_deckCardScale = ResolveDeckCardScale();
	}

	private float ResolveDeckCardScale()
	{
		if (uiComponent.deckCardScale > 0f)
			return uiComponent.deckCardScale;

		float sourceWidth = ResolveDeckCardSourceWidth();
		float sourceHeight = ResolveDeckCardSourceHeight();
		float widthScale = (_viewportWidth * Mathf.Clamp01(uiComponent.maxDeckCardWidthRatio)) / Mathf.Max(1f, sourceWidth);
		float heightScale = (_viewportHeight * Mathf.Clamp01(uiComponent.maxDeckCardHeightRatio)) / Mathf.Max(1f, sourceHeight);
		return Mathf.Clamp(Mathf.Min(widthScale, heightScale), uiComponent.minDeckCardScale, uiComponent.maxDeckCardScale);
	}

	private float ResolveDeckCardSourceWidth()
	{
		RectTransform templateRect = _cardTemplateImage != null ? _cardTemplateImage.rectTransform : null;
		return templateRect != null ? Mathf.Max(1f, templateRect.rect.width) : 285f;
	}

	private float ResolveDeckCardSourceHeight()
	{
		RectTransform templateRect = _cardTemplateImage != null ? _cardTemplateImage.rectTransform : null;
		return templateRect != null ? Mathf.Max(1f, templateRect.rect.height) : 487f;
	}

	private float NormalizeDeckOffset(float offset)
	{
		ResolveDeckLayoutMetrics();
		int count = Mathf.Max(1, _deckImages.Count);
		float spacing = count > 1 ? _fanWidth / (count - 1) : _fanWidth;
		float cycleWidth = Mathf.Max(1f, _fanWidth + spacing);
		return Mathf.Repeat(offset + cycleWidth * 0.5f, cycleWidth) - cycleWidth * 0.5f;
	}

	private void RefreshDeckLayoutIfViewportChanged()
	{
		Vector2 viewportSize = GetDeckViewportSize();
		if (viewportSize.x <= 1f || viewportSize.y <= 1f)
			return;

		if (Mathf.Abs(viewportSize.x - _lastDeckViewportSize.x) <= 0.5f
			&& Mathf.Abs(viewportSize.y - _lastDeckViewportSize.y) <= 0.5f)
			return;

		ApplyDeckLayout(false);
	}

	private Vector2 GetDeckViewportSize()
	{
		return _cardContainer != null ? _cardContainer.rect.size : Vector2.zero;
	}

	private void RefreshDeckSiblingOrder()
	{
		if (_deckImages.Count <= 1) return;

		_deckSiblingOrder.Clear();
		for (int i = 0; i < _deckImages.Count; i++)
		{
			if (_deckImages[i] != null)
				_deckSiblingOrder.Add(i);
		}

		_deckSiblingOrder.Sort(CompareDeckSiblingOrder);
		for (int i = 0; i < _deckSiblingOrder.Count; i++)
		{
			Image image = _deckImages[_deckSiblingOrder[i]];
			if (image != null)
				image.rectTransform.SetSiblingIndex(i);
		}

		if (_isPullingCard && _activeDragCardIndex >= 0 && _activeDragCardIndex < _deckImages.Count)
		{
			Image activeImage = _deckImages[_activeDragCardIndex];
			if (activeImage != null)
				activeImage.rectTransform.SetAsLastSibling();
		}
	}

	private int CompareDeckSiblingOrder(int leftIndex, int rightIndex)
	{
		Vector2 leftPosition = GetDeckSiblingPosition(leftIndex);
		Vector2 rightPosition = GetDeckSiblingPosition(rightIndex);

		int xCompare = Mathf.RoundToInt(leftPosition.x * 100f)
			.CompareTo(Mathf.RoundToInt(rightPosition.x * 100f));
		if (xCompare != 0)
			return xCompare;

		int yCompare = Mathf.RoundToInt(leftPosition.y * 100f)
			.CompareTo(Mathf.RoundToInt(rightPosition.y * 100f));
		if (yCompare != 0)
			return yCompare;

		return leftIndex.CompareTo(rightIndex);
	}

	private Vector2 GetDeckSiblingPosition(int index)
	{
		if (index >= 0 && index < _deckImages.Count)
		{
			Image image = _deckImages[index];
			if (image != null)
				return image.rectTransform.anchoredPosition;
		}

		return GetDeckFanPosition(index);
	}

	private void StartDeckInertia()
	{
		if (!uiComponent.useScrollInertia || !_isChoosingCard || _isAnimatingCard)
			return;

		float velocity = _scrollVelocityX * Mathf.Max(0f, uiComponent.scrollInertiaMultiplier);
		velocity = Mathf.Clamp(velocity, -Mathf.Abs(uiComponent.maxFlickVelocity), Mathf.Abs(uiComponent.maxFlickVelocity));
		if (Mathf.Abs(velocity) < Mathf.Max(0f, uiComponent.minFlickVelocity))
			return;

		float deceleration = Mathf.Max(1f, uiComponent.scrollDeceleration);
		float duration = Mathf.Min(Mathf.Abs(velocity) / deceleration, Mathf.Max(0.05f, uiComponent.maxInertiaDuration));
		float distance = velocity * duration * 0.5f;
		float targetOffset = NormalizeDeckOffset(_deckOffsetX + distance);

		_deckInertiaTween?.Kill(false);
		_deckInertiaTween = DOTween.To(
				() => _deckOffsetX,
				value =>
				{
					_deckOffsetX = NormalizeDeckOffset(value);
					ApplyDeckLayout(false);
				},
				targetOffset,
				duration)
			.SetEase(Ease.OutCubic)
			.OnComplete(() =>
			{
				_deckOffsetX = NormalizeDeckOffset(targetOffset);
				ApplyDeckLayout(false);
				_deckInertiaTween = null;
			});
	}

	private void StopDeckInertia()
	{
		_deckInertiaTween?.Kill(false);
		_deckInertiaTween = null;
	}

	private void StopDeckShuffleSequence()
	{
		_deckShuffleSequence?.Kill(false);
		_deckShuffleSequence = null;
	}

	private void SetDeckCardsInteractable(bool interactable)
	{
		for (int i = 0; i < _deckImages.Count; i++)
		{
			Image image = _deckImages[i];
			if (image != null)
				image.raycastTarget = interactable && image.enabled && image.gameObject.activeInHierarchy;
		}
	}

	private void FadeDeckExcept(Image excludedImage, float alpha, float duration)
	{
		for (int i = 0; i < _deckImages.Count; i++)
		{
			Image image = _deckImages[i];
			if (image == null || image == excludedImage) continue;

			DOTween.Kill(image);
			if (duration <= 0f)
				SetImageAlpha(image, alpha);
			else
				image.DOFade(alpha, duration).SetEase(Ease.OutCubic);
		}
	}

	private void ClearRuntimeDeckCards()
	{
		StopDeckShuffleSequence();
		StopDeckInertia();
		StopLongPressSelection();
		StopPendingSingleClick();
		_clickSuppressTween?.Kill(false);
		_clickSuppressTween = null;
		_flowSequence?.Kill(false);
		_flowSequence = null;
		StopFlyingCard();

		for (int i = 0; i < _deckImages.Count; i++)
		{
			Image image = _deckImages[i];
			if (image != null)
				DestroyDeckObject(image.gameObject);
		}
		_deckImages.Clear();
		_deckSiblingOrder.Clear();
		ClearGeneratedDeckChildrenUnder(_cardContainer);

		if (uiComponent != null && uiComponent.cardGO != null)
			uiComponent.cardGO.SetActive(false);
	}

	private void ClearGeneratedDeckChildrenUnder(Transform root)
	{
		if (root == null) return;

		for (int i = root.childCount - 1; i >= 0; i--)
		{
			Transform child = root.GetChild(i);
			if (child == null) continue;
			if (_cardTemplateImage != null && child == _cardTemplateImage.transform) continue;
			if (IsTrackedDeckChild(child)) continue;

			if (!string.IsNullOrEmpty(child.name) && child.name.StartsWith(DeckCardNamePrefix, StringComparison.Ordinal))
				DestroyDeckObject(child.gameObject);
		}
	}

	private bool IsTrackedDeckChild(Transform child)
	{
		for (int i = 0; i < _deckImages.Count; i++)
		{
			Image image = _deckImages[i];
			if (image != null && image.transform == child)
				return true;
		}

		return false;
	}

	private void SetOpenPanelVisible(bool visible, bool immediate)
	{
		if (uiComponent == null || uiComponent.openCardPanel == null)
			return;

		CanvasGroup group = EnsureOpenPanelCanvasGroup();
		DOTween.Kill(group);

		if (visible)
		{
			uiComponent.openCardPanel.SetActive(true);
			uiComponent.openCardPanel.transform.SetAsLastSibling();

			if (group != null)
			{
				group.interactable = true;
				group.blocksRaycasts = true;
				if (immediate)
					group.alpha = 1f;
				else
				{
					group.alpha = 0f;
					group.DOFade(1f, Mathf.Max(0f, uiComponent.openPanelFadeDuration)).SetEase(Ease.OutCubic);
				}
			}
			return;
		}

		if (group == null)
		{
			uiComponent.openCardPanel.SetActive(false);
			return;
		}

		group.interactable = false;
		group.blocksRaycasts = false;
		if (immediate)
		{
			group.alpha = 0f;
			uiComponent.openCardPanel.SetActive(false);
			return;
		}

		group.DOFade(0f, Mathf.Max(0f, uiComponent.openPanelFadeDuration))
			.SetEase(Ease.OutCubic)
			.OnComplete(() =>
			{
				if (uiComponent != null && uiComponent.openCardPanel != null)
					uiComponent.openCardPanel.SetActive(false);
			});
	}

	private CanvasGroup EnsureOpenPanelCanvasGroup()
	{
		if (_openPanelGroup != null)
			return _openPanelGroup;

		if (uiComponent == null || uiComponent.openCardPanel == null)
			return null;

		_openPanelGroup = uiComponent.openCardPanel.GetComponent<CanvasGroup>();
		if (_openPanelGroup == null)
			_openPanelGroup = uiComponent.openCardPanel.AddComponent<CanvasGroup>();
		return _openPanelGroup;
	}

	private void SetOpenButtonsInteractable(bool openInteractable, bool cancelInteractable)
	{
		if (uiComponent == null) return;
		if (uiComponent.openBtn != null) uiComponent.openBtn.interactable = openInteractable;
		if (uiComponent.cancelBtn != null) uiComponent.cancelBtn.interactable = cancelInteractable;
	}

	private void SetNextButtonState(bool visible, bool interactable, string label = "下一步")
	{
		if (uiComponent == null)
			return;

		bool hasNextButton = uiComponent.nextBtn != null;
		SetOpenButtonsVisible(!visible || !hasNextButton);
		if (!hasNextButton)
			return;

		uiComponent.nextBtn.gameObject.SetActive(visible);
		uiComponent.nextBtn.interactable = visible && interactable;
		RelationshipDivinationFlow.SetButtonText(uiComponent.nextBtn, label);
	}

	private void SetOpenButtonsVisible(bool visible)
	{
		if (uiComponent == null)
			return;

		if (uiComponent.openBtn != null)
			uiComponent.openBtn.gameObject.SetActive(visible);
		if (uiComponent.cancelBtn != null)
			uiComponent.cancelBtn.gameObject.SetActive(visible);
	}

	private void RefreshNextButtonForRecord()
	{
		if (!_hasOpenedCard || currentRecord == null)
		{
			SetNextButtonState(false, false);
			return;
		}

		if (_revealSubmitted)
		{
			SetNextButtonState(true, false, "同步中...");
			return;
		}

		if (currentRecord.IsCompleted || currentRecord.isLocalOnly)
		{
			SetNextButtonState(true, true, "查看占卜结果");
			return;
		}

		if (RelationshipDivinationFlow.IsMyCardRevealed(currentRecord))
		{
			SetNextButtonState(true, true, "返回双人占卜");
			return;
		}

		SetNextButtonState(true, true, "重试同步");
	}

	private void EnsureDrawResult()
	{
		if (_hasDrawResult) return;

		RelationshipDivinationCard relationshipCard = RelationshipDivinationFlow.GetMyPrivateCard(currentRecord);
		if (relationshipCard != null)
		{
			TarotCard tarotCard = TarotDeck.GetById(relationshipCard.cardId);
			_drawResult = (tarotCard, relationshipCard.IsUpright);
			_hasDrawResult = tarotCard != null;
			if (!_hasDrawResult)
				Debug.LogWarning($"[DrawMyCardUI] 关系占卜牌数据不存在: {relationshipCard.cardId}");
			return;
		}

		_drawResult = TarotDeck.DrawOne();
		_hasDrawResult = _drawResult.card != null;
		if (!_hasDrawResult)
			Debug.LogWarning("[DrawMyCardUI] 抽牌失败：TarotDeck.DrawOne 返回空牌。");
	}

	private void SubmitRelationshipRevealIfNeeded()
	{
		if (currentRecord == null || _revealSubmitted)
			return;

		if (currentRecord.isLocalOnly || currentRecord.IsCompleted)
		{
			RefreshNextButtonForRecord();
			return;
		}

		if (!currentRecord.CanCurrentUserReveal(RelationshipDivinationFlow.GetCurrentUid()))
		{
			RefreshNextButtonForRecord();
			return;
		}

		RelationshipDivinationFirestore service = RelationshipDivinationFlow.GetOrCreateService();
		if (service == null)
		{
			ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
			SetOpenButtonsInteractable(false, true);
			RefreshNextButtonForRecord();
			return;
		}

		_revealSubmitted = true;
		SetOpenButtonsInteractable(false, false);
		RefreshNextButtonForRecord();
		service.RevealMyCard(currentRecord, updated =>
		{
			_revealSubmitted = false;
			if (updated == null)
			{
				SetOpenButtonsInteractable(false, true);
				RefreshNextButtonForRecord();
				return;
			}

			currentRecord = updated;
			sPendingRecord = updated;
			RelationshipDivinationFlow.UpdateCurrentRecordState(updated, currentFriend);
			SetOpenButtonsInteractable(false, false);
			RefreshNextButtonForRecord();
		}, false);
	}

	private Sprite ResolveCardBackSprite()
	{
		if (uiComponent == null)
			return null;
		if (uiComponent.cardBackSprite != null)
			return uiComponent.cardBackSprite;
		if (_cardTemplateImage != null && _cardTemplateImage.sprite != null)
			return _cardTemplateImage.sprite;

		Image backImage = uiComponent.cardBack != null ? uiComponent.cardBack.GetComponent<Image>() : null;
		return backImage != null ? backImage.sprite : null;
	}

	private Sprite LoadCardSprite(TarotCard card)
	{
		if (card == null || string.IsNullOrEmpty(card.cardId))
			return ResolveCardBackSprite();

		Sprite sprite = TarotSpriteLoader.Load(card.cardId);
		return sprite != null ? sprite : ResolveCardBackSprite();
	}

	private static string FormatCardName(TarotCard card, bool upright)
	{
		return card != null ? card.DisplayName(upright) : TarotDeck.FormatDisplayName("未知牌", upright);
	}

	private void StopPendingSingleClick()
	{
		if (_singleClickCoroutine != null)
			uiComponent.StopCoroutine(_singleClickCoroutine);

		_singleClickCoroutine = null;
		_pendingClickDeckIndex = -1;
	}

	private void StopLongPressSelection()
	{
		if (_longPressCoroutine != null)
			uiComponent.StopCoroutine(_longPressCoroutine);

		_longPressCoroutine = null;
	}

	private void SuppressPointerClickBriefly()
	{
		_suppressPointerClick = true;
		_clickSuppressTween?.Kill(false);
		_clickSuppressTween = DOVirtual.DelayedCall(0.18f, () =>
		{
			_suppressPointerClick = false;
			_dragMoved = false;
			_clickSuppressTween = null;
			SetDeckCardsInteractable(_isChoosingCard && !_isAnimatingCard);
		});
	}

	private void KillFlow()
	{
		_flowSequence?.Kill(false);
		_flowSequence = null;
		StopDeckShuffleSequence();
		StopDeckInertia();
		StopPendingSingleClick();
		StopLongPressSelection();
		_clickSuppressTween?.Kill(false);
		_clickSuppressTween = null;

		for (int i = 0; i < _deckImages.Count; i++)
		{
			Image image = _deckImages[i];
			if (image == null) continue;
			DOTween.Kill(image);
			DOTween.Kill(image.rectTransform);
		}

		if (_cardSlotRect != null)
			DOTween.Kill(_cardSlotRect);
		if (_openPanelGroup != null)
			DOTween.Kill(_openPanelGroup);
		if (_flyingCard != null)
			DOTween.Kill(_flyingCard.rectTransform);
	}

	private static void CopyRectWorldPose(RectTransform sourceRect, RectTransform targetRect, RectTransform targetParent)
	{
		if (sourceRect == null || targetRect == null || targetParent == null) return;

		targetRect.anchorMin = new Vector2(0.5f, 0.5f);
		targetRect.anchorMax = new Vector2(0.5f, 0.5f);
		targetRect.pivot = new Vector2(0.5f, 0.5f);
		targetRect.sizeDelta = sourceRect.rect.size;

		Vector3 worldCenter = sourceRect.TransformPoint(sourceRect.rect.center);
		Vector3 localCenter = targetParent.InverseTransformPoint(worldCenter);
		targetRect.anchoredPosition = new Vector2(localCenter.x, localCenter.y);
		targetRect.localRotation = Quaternion.Inverse(targetParent.rotation) * sourceRect.rotation;

		Vector3 sourceScale = sourceRect.lossyScale;
		Vector3 parentScale = targetParent.lossyScale;
		targetRect.localScale = new Vector3(
			SafeDivide(sourceScale.x, parentScale.x),
			SafeDivide(sourceScale.y, parentScale.y),
			1f);
	}

	private static void ResolveRectTargetPose(RectTransform targetRect,
		RectTransform motionParent,
		RectTransform sourceRect,
		out Vector2 targetPosition,
		out float targetRotationZ,
		out float targetScale)
	{
		targetPosition = Vector2.zero;
		targetRotationZ = 0f;
		targetScale = 1f;

		if (targetRect == null || motionParent == null || sourceRect == null)
			return;

		Vector3 worldCenter = targetRect.TransformPoint(targetRect.rect.center);
		Vector3 localCenter = motionParent.InverseTransformPoint(worldCenter);
		targetPosition = new Vector2(localCenter.x, localCenter.y);

		Quaternion localRotation = Quaternion.Inverse(motionParent.rotation) * targetRect.rotation;
		targetRotationZ = localRotation.eulerAngles.z;

		Vector3[] worldCorners = new Vector3[4];
		targetRect.GetWorldCorners(worldCorners);
		Vector3 localBottomLeft = motionParent.InverseTransformPoint(worldCorners[0]);
		Vector3 localTopLeft = motionParent.InverseTransformPoint(worldCorners[1]);
		Vector3 localBottomRight = motionParent.InverseTransformPoint(worldCorners[3]);

		float targetWidth = Vector3.Distance(localBottomLeft, localBottomRight);
		float targetHeight = Vector3.Distance(localBottomLeft, localTopLeft);
		float sourceWidth = Mathf.Max(1f, sourceRect.rect.width);
		float sourceHeight = Mathf.Max(1f, sourceRect.rect.height);
		targetScale = Mathf.Max(0.01f, Mathf.Min(targetWidth / sourceWidth, targetHeight / sourceHeight));
	}

	private static float SafeDivide(float value, float divisor)
	{
		return Mathf.Abs(divisor) <= 0.0001f ? value : value / divisor;
	}

	private static void SetImageAlpha(Image image, float alpha)
	{
		if (image == null) return;
		Color color = image.color;
		color.a = alpha;
		image.color = color;
	}

	private static void DisableCardLocalSorting(GameObject cardObject)
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
}
