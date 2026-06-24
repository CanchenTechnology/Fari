using System;
using System.Collections;
using SuperScrollView;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FriendSwipeRevealItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const string ContentRootName = "__SwipeContent";

    [Header("References")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Button deleteButton;

    [Header("Swipe")]
    [SerializeField] private float actionWidth = 180f;
    [SerializeField] private bool useDeleteButtonWidth = true;
    [SerializeField] private bool hideDeleteButtonWhenClosed = true;
    [SerializeField] private float openThreshold = 0.45f;
    [SerializeField] private float dragDecisionThreshold = 10f;
    [SerializeField] private float animationDuration = 0.14f;

    private static FriendSwipeRevealItem currentOpenItem;

    private RectTransform rootRect;
    private RectTransform deleteButtonRect;
    private LoopListView2 parentListView;
    private Action deleteRequested;
    private Coroutine animationCoroutine;

    private Vector2 dragStartPosition;
    private float dragStartOffset;
    private float currentOffset;
    private bool dragDecided;
    private bool horizontalDrag;
    private bool forwardedListBeginDrag;

    private float RevealWidth
    {
        get
        {
            if (useDeleteButtonWidth && deleteButtonRect != null && deleteButtonRect.rect.width > 0.01f)
                return Mathf.Abs(deleteButtonRect.rect.width);
            return Mathf.Max(0f, actionWidth);
        }
    }

    public bool IsOpen => currentOffset <= -RevealWidth + 0.5f;

    public static bool CloseCurrentOpen(FriendSwipeRevealItem except = null, bool immediate = false)
    {
        if (currentOpenItem == null || currentOpenItem == except)
            return false;

        FriendSwipeRevealItem item = currentOpenItem;
        if (immediate)
            item.ResetReveal(true);
        else
            item.Close();
        return true;
    }

    private void Awake()
    {
        EnsureLayout();
    }

    private void OnEnable()
    {
        EnsureLayout();
    }

    private void OnDisable()
    {
        StopAnimation();
        SetOffset(0f);
        if (currentOpenItem == this)
            currentOpenItem = null;
    }

    private void OnDestroy()
    {
        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(HandleDeleteClicked);
    }

    public void Configure(Action onDeleteRequested, bool canDelete)
    {
        EnsureLayout();
        deleteRequested = onDeleteRequested;
        if (deleteButton != null)
            deleteButton.interactable = canDelete;
        ResetReveal(true);
    }

    public void ResetReveal(bool immediate)
    {
        if (immediate)
        {
            StopAnimation();
            SetOffset(0f);
            if (currentOpenItem == this)
                currentOpenItem = null;
            return;
        }

        Close();
    }

    public void Close()
    {
        AnimateTo(0f);
        if (currentOpenItem == this)
            currentOpenItem = null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        EnsureLayout();
        CloseCurrentOpen(this);
        StopAnimation();
        dragStartPosition = eventData.position;
        dragStartOffset = currentOffset;
        dragDecided = false;
        horizontalDrag = false;
        forwardedListBeginDrag = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        Vector2 totalDelta = eventData.position - dragStartPosition;
        if (!dragDecided)
        {
            if (totalDelta.magnitude < dragDecisionThreshold)
                return;

            horizontalDrag = Mathf.Abs(totalDelta.x) > Mathf.Abs(totalDelta.y);
            dragDecided = true;

            if (!horizontalDrag)
            {
                if (currentOpenItem == this)
                    Close();
                ForwardListBeginDrag(eventData);
            }
            else if (currentOpenItem != null && currentOpenItem != this)
            {
                currentOpenItem.Close();
            }
        }

        if (!horizontalDrag)
        {
            ForwardListDrag(eventData);
            return;
        }

        float nextOffset = Mathf.Clamp(dragStartOffset + totalDelta.x, -RevealWidth, 0f);
        SetOffset(nextOffset);
        eventData.Use();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (forwardedListBeginDrag)
        {
            ForwardListEndDrag(eventData);
            return;
        }

        if (!dragDecided || !horizontalDrag)
            return;

        float revealWidth = RevealWidth;
        float openLine = -revealWidth * openThreshold;
        bool shouldOpen = currentOffset <= openLine;
        if (shouldOpen)
            Open();
        else
            Close();

        eventData.Use();
    }

    private void Open()
    {
        if (currentOpenItem != null && currentOpenItem != this)
            currentOpenItem.Close();

        currentOpenItem = this;
        AnimateTo(-RevealWidth);
    }

    private void HandleDeleteClicked()
    {
        Close();
        deleteRequested?.Invoke();
    }

    private void EnsureLayout()
    {
        if (rootRect == null)
            rootRect = transform as RectTransform;
        if (rootRect == null)
            return;

        parentListView = GetComponentInParent<LoopListView2>();
        EnsureMask();
        ResolveDeleteButton();
        EnsureContentRoot();
        BindDeleteButton();
        RefreshDeleteButtonVisibility();
    }

    private void EnsureMask()
    {
        if (GetComponent<RectMask2D>() == null)
            gameObject.AddComponent<RectMask2D>();
    }

    private void ResolveDeleteButton()
    {
        if (deleteButtonRect == null && deleteButton != null)
            deleteButtonRect = deleteButton.transform as RectTransform;

        if (deleteButtonRect != null && deleteButton == null)
            deleteButton = deleteButtonRect.GetComponent<Button>();

        if (deleteButtonRect != null && deleteButtonRect.parent == transform)
            deleteButtonRect.SetAsFirstSibling();
    }

    private void BindDeleteButton()
    {
        if (deleteButton == null)
            return;

        deleteButton.onClick.RemoveListener(HandleDeleteClicked);
        deleteButton.onClick.AddListener(HandleDeleteClicked);
    }

    private void RefreshDeleteButtonVisibility()
    {
        if (!hideDeleteButtonWhenClosed || deleteButton == null)
            return;

        bool shouldShow = currentOffset < -0.5f;
        if (deleteButton.gameObject.activeSelf != shouldShow)
            deleteButton.gameObject.SetActive(shouldShow);
    }

    private void EnsureContentRoot()
    {
        if (contentRoot == null)
        {
            Transform found = transform.Find(ContentRootName);
            if (found != null)
                contentRoot = found as RectTransform;
        }

        bool createdContentRoot = false;
        if (contentRoot == null)
        {
            GameObject contentObject = new GameObject(ContentRootName, typeof(RectTransform), typeof(CanvasRenderer));
            contentObject.layer = gameObject.layer;
            contentRoot = contentObject.GetComponent<RectTransform>();
            contentRoot.SetParent(transform, false);
            createdContentRoot = true;
            StretchToRoot(contentRoot);
            CopyRootImageToContent();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == contentRoot || child == deleteButtonRect)
                    continue;

                child.SetParent(contentRoot, true);
            }
        }

        if (contentRoot.parent == transform)
        {
            if (createdContentRoot || contentRoot.name == ContentRootName)
                StretchToRoot(contentRoot);
            contentRoot.SetAsLastSibling();
        }
    }

    private void StretchToRoot(RectTransform target)
    {
        if (target == null) return;
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
        target.pivot = rootRect.pivot;
        target.localScale = Vector3.one;
        target.localRotation = Quaternion.identity;
    }

    private void CopyRootImageToContent()
    {
        Image source = GetComponent<Image>();
        if (source == null || contentRoot == null)
            return;

        Image target = contentRoot.GetComponent<Image>();
        if (target == null)
            target = contentRoot.gameObject.AddComponent<Image>();

        target.sprite = source.sprite;
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillAmount = source.fillAmount;
        target.fillClockwise = source.fillClockwise;
        target.fillOrigin = source.fillOrigin;
        target.useSpriteMesh = source.useSpriteMesh;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.material = source.material;
        target.color = source.color;
        target.raycastTarget = source.raycastTarget;

        Button rootButton = GetComponent<Button>();
        if (rootButton != null)
            rootButton.targetGraphic = target;

        source.raycastTarget = false;
        source.enabled = false;
    }

    private void SetOffset(float offset)
    {
        currentOffset = Mathf.Clamp(offset, -RevealWidth, 0f);
        RefreshDeleteButtonVisibility();
        if (contentRoot == null)
            return;

        Vector2 anchoredPosition = contentRoot.anchoredPosition;
        anchoredPosition.x = currentOffset;
        contentRoot.anchoredPosition = anchoredPosition;
    }

    private void AnimateTo(float targetOffset)
    {
        EnsureLayout();
        StopAnimation();
        animationCoroutine = StartCoroutine(AnimateOffsetRoutine(Mathf.Clamp(targetOffset, -RevealWidth, 0f)));
    }

    private IEnumerator AnimateOffsetRoutine(float targetOffset)
    {
        float startOffset = currentOffset;
        if (Mathf.Approximately(startOffset, targetOffset))
        {
            SetOffset(targetOffset);
            animationCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            SetOffset(Mathf.Lerp(startOffset, targetOffset, t));
            yield return null;
        }

        SetOffset(targetOffset);
        animationCoroutine = null;
    }

    private void StopAnimation()
    {
        if (animationCoroutine == null)
            return;

        StopCoroutine(animationCoroutine);
        animationCoroutine = null;
    }

    private void ForwardListBeginDrag(PointerEventData eventData)
    {
        if (parentListView == null || forwardedListBeginDrag)
            return;

        forwardedListBeginDrag = true;
        parentListView.OnBeginDrag(eventData);
    }

    private void ForwardListDrag(PointerEventData eventData)
    {
        if (parentListView == null)
            return;

        if (!forwardedListBeginDrag)
            ForwardListBeginDrag(eventData);
        parentListView.OnDrag(eventData);
    }

    private void ForwardListEndDrag(PointerEventData eventData)
    {
        if (parentListView == null)
            return;

        parentListView.OnEndDrag(eventData);
        forwardedListBeginDrag = false;
    }
}
