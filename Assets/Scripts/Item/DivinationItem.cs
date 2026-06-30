using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DivinationItem : MonoBehaviour
{
    public Transform cardContainer;

    public Image cardImage;

    public TMP_Text cardName;

    public TMP_Text divinationDesText;

    public TMP_Text divinationSourceText;

    public TMP_Text divinationTimeText;
    public Button viewBtn;

    private readonly List<Image> runtimeCardImages = new List<Image>();

    public void SetData(
        IReadOnlyList<Sprite> cardSprites,
        string cardsName,
        string description,
        string source,
        string time)
    {
        ResolveReferences();
        SetCardSprites(cardSprites);

        if (cardName != null) cardName.text = cardsName ?? string.Empty;
        if (divinationDesText != null) divinationDesText.text = description ?? string.Empty;
        if (divinationSourceText != null) divinationSourceText.text = source ?? string.Empty;
        if (divinationTimeText != null) divinationTimeText.text = time ?? string.Empty;
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
        ClearRuntimeCardImages();
        if (cardImage == null) return;

        int count = cardSprites == null ? 0 : cardSprites.Count;
        bool hasFirstSprite = count > 0 && cardSprites[0] != null;
        cardImage.gameObject.SetActive(hasFirstSprite);
        if (hasFirstSprite)
            cardImage.sprite = cardSprites[0];

        Transform parent = cardContainer != null ? cardContainer : cardImage.transform.parent;
        if (parent == null || count <= 1) return;

        for (int i = 1; i < count; i++)
        {
            Sprite sprite = cardSprites[i];
            if (sprite == null) continue;

            Image clone = Instantiate(cardImage, parent);
            clone.name = $"CardImage_{i + 1}";
            clone.gameObject.SetActive(true);
            clone.sprite = sprite;
            runtimeCardImages.Add(clone);
        }
    }

    private void ClearRuntimeCardImages()
    {
        for (int i = 0; i < runtimeCardImages.Count; i++)
        {
            if (runtimeCardImages[i] != null)
                Destroy(runtimeCardImages[i].gameObject);
        }

        runtimeCardImages.Clear();
    }

    private void ResolveReferences()
    {
        if (cardContainer == null)
            cardContainer = transform.Find("CardContainer");
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
}
