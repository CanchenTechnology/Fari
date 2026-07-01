using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DivinationItem : MonoBehaviour
{
    private const float DefaultCardPreferredWidth = 76f;
    private const float DefaultCardPreferredHeight = 132f;

    public Transform cardContainer;

    public GameObject cardGO;
    public Image cardImage;

    public TMP_Text cardName;

    public TMP_Text divinationDesText;

    public TMP_Text divinationSourceText;

    public TMP_Text divinationTimeText;
    public Button viewBtn;

    [SerializeField] private bool showDivinationSource;

    private readonly List<GameObject> runtimeCardObjects = new List<GameObject>();

    public void SetData(
        IReadOnlyList<Sprite> cardSprites,
        string cardsName,
        string description,
        string source,
        string time,
        bool? showSourceOverride = null)
    {
        ResolveReferences();
        SetCardSprites(cardSprites);

        if (cardName != null) cardName.text = cardsName ?? string.Empty;
        if (divinationDesText != null) divinationDesText.text = description ?? string.Empty;
        SetDivinationSource(source, showSourceOverride);
        if (divinationTimeText != null) divinationTimeText.text = time ?? string.Empty;

        RebuildLayout();
    }

    public void Clear()
    {
        SetData(null, string.Empty, string.Empty, string.Empty, string.Empty);
        SetClickAction(null);
    }

    public void SetClickAction(Action onClick)
    {
        ResolveReferences();
        if (viewBtn == null)
            return;

        viewBtn.onClick.RemoveAllListeners();
        viewBtn.interactable = onClick != null;
        if (onClick != null)
            viewBtn.onClick.AddListener(() => onClick());
    }

    private void SetCardSprites(IReadOnlyList<Sprite> cardSprites)
    {
        ClearRuntimeCardObjects();

        GameObject template = ResolveCardTemplate();
        if (template == null) return;

        Image templateImage = ResolveCardImage(template);

        int count = cardSprites == null ? 0 : cardSprites.Count;
        int firstSpriteIndex = -1;
        for (int i = 0; i < count; i++)
        {
            if (cardSprites[i] != null)
            {
                firstSpriteIndex = i;
                break;
            }
        }

        bool hasFirstSprite = firstSpriteIndex >= 0;
        template.SetActive(hasFirstSprite);
        if (hasFirstSprite)
        {
            if (templateImage != null)
                templateImage.sprite = cardSprites[firstSpriteIndex];
            EnsureCardLayout(template, templateImage);
        }
        else
        {
            if (templateImage != null)
                templateImage.sprite = null;
        }

        Transform parent = cardContainer != null ? cardContainer : template.transform.parent;
        if (parent == null || !hasFirstSprite) return;

        int renderedCount = 1;
        for (int i = firstSpriteIndex + 1; i < count; i++)
        {
            Sprite sprite = cardSprites[i];
            if (sprite == null) continue;

            GameObject clone = Instantiate(template, parent);
            clone.name = $"{template.name}_{renderedCount + 1}";
            clone.SetActive(true);

            Image cloneImage = ResolveCardImage(clone);
            if (cloneImage != null)
                cloneImage.sprite = sprite;
            EnsureCardLayout(clone, cloneImage);
            runtimeCardObjects.Add(clone);
            renderedCount++;
        }
    }

    private void ClearRuntimeCardObjects()
    {
        for (int i = 0; i < runtimeCardObjects.Count; i++)
        {
            GameObject cardObject = runtimeCardObjects[i];
            if (cardObject != null)
            {
                cardObject.SetActive(false);
                Destroy(cardObject);
            }
        }

        runtimeCardObjects.Clear();
    }

    private GameObject ResolveCardTemplate()
    {
        if (cardGO != null)
            return cardGO;

        if (cardImage != null)
            return cardImage.gameObject;

        Transform cardGoTransform = FindTransformByName(transform, "CardGo", "cardGO", "CardGO", "card", "Card");
        if (cardGoTransform != null)
        {
            cardGO = cardGoTransform.gameObject;
            return cardGO;
        }

        return null;
    }

    private Image ResolveCardImage(GameObject cardObject)
    {
        if (cardObject == null) return null;
        if (cardImage != null && cardImage.transform.IsChildOf(cardObject.transform))
            return cardImage;

        Image image = FindImageByName(cardObject.transform, "CardImage", "cardImage");
        if (image == null)
            image = cardObject.GetComponentInChildren<Image>(true);
        return image;
    }

    private void EnsureCardLayout(GameObject cardObject, Image image)
    {
        if (cardObject == null) return;

        LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = cardObject.AddComponent<LayoutElement>();

        if (layoutElement.preferredWidth <= 0f)
            layoutElement.preferredWidth = DefaultCardPreferredWidth;
        if (layoutElement.preferredHeight <= 0f)
            layoutElement.preferredHeight = DefaultCardPreferredHeight;

        if (image != null)
            image.preserveAspect = true;
    }

    private void RebuildLayout()
    {
        Canvas.ForceUpdateCanvases();
        if (cardContainer is RectTransform cardContainerRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardContainerRect);
        if (transform is RectTransform selfRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(selfRect);
    }

    private void SetDivinationSource(string source, bool? showSourceOverride)
    {
        if (divinationSourceText == null) return;

        bool shouldShow = showSourceOverride ?? showDivinationSource;
        bool hasSource = !string.IsNullOrWhiteSpace(source);
        divinationSourceText.text = shouldShow && hasSource ? source : string.Empty;
        divinationSourceText.gameObject.SetActive(shouldShow && hasSource);
    }

    private void ResolveReferences()
    {
        if (cardContainer == null)
            cardContainer = transform.Find("CardContainer");
        if (cardGO == null)
            cardGO = ResolveCardTemplate();
        if (cardImage == null)
            cardImage = FindImageByName(transform, "CardImage", "cardImage");
        if (cardName == null)
            cardName = FindTextByName(transform, "cardName", "CardName");
        if (divinationDesText == null)
            divinationDesText = FindTextByName(transform, "cardDes", "divinationDes", "divinationDescription");
        if (divinationSourceText == null)
            divinationSourceText = FindTextByName(transform, "divinationSource", "DivinationSource");
        if (divinationTimeText == null)
            divinationTimeText = FindTextByName(transform, "divinationTime", "DivinationTime");
        if (viewBtn == null)
            viewBtn = GetComponent<Button>();
        if (viewBtn == null)
            viewBtn = GetComponentInChildren<Button>(true);
    }

    private TMP_Text FindTextByName(Transform root, params string[] names)
    {
        if (root == null || names == null) return null;
        for (int i = 0; i < names.Length; i++)
        {
            if (root.name == names[i])
                return root.GetComponent<TMP_Text>();
        }

        for (int i = 0; i < root.childCount; i++)
        {
            TMP_Text result = FindTextByName(root.GetChild(i), names);
            if (result != null) return result;
        }

        return null;
    }

    private Image FindImageByName(Transform root, params string[] names)
    {
        if (root == null || names == null) return null;
        for (int i = 0; i < names.Length; i++)
        {
            if (root.name == names[i])
                return root.GetComponent<Image>();
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Image result = FindImageByName(root.GetChild(i), names);
            if (result != null) return result;
        }

        return null;
    }

    private Transform FindTransformByName(Transform root, params string[] names)
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
            if (result != null) return result;
        }

        return null;
    }
}
