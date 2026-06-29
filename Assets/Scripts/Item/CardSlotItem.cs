using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardSlotItem : MonoBehaviour
{
    public GameObject cardSlot;
    [Header("卡牌正面")]
    public Transform cardFront;
    public Image cardImage;
    public TMP_Text cardTitleText;

    [Header("兼容旧结构")]
    public TMP_Text cardTag;

    public void SetItemData(Sprite sprite, string cardTagStr)
    {
        ShowFace(sprite, cardTagStr, true);
    }

    public RectTransform GetPoseRect()
    {
        ResolveReferences();

        if (cardFront is RectTransform frontRect)
            return frontRect;

        if (cardImage != null)
            return cardImage.rectTransform;
        return transform as RectTransform;
    }

    public void ShowBack(Sprite backSprite, string label)
    {
        ResolveReferences();

        SetCardSlotVisible(true);
        SetActive(cardFront, false);


        if (cardFront == null && cardImage != null)
        {
            cardImage.sprite = backSprite;
            cardImage.enabled = true;
            cardImage.color = backSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            cardImage.preserveAspect = true;
            cardImage.rectTransform.localScale = Vector3.one;
            cardImage.rectTransform.localRotation = Quaternion.identity;
        }

        SetText(label);
    }

    public void ShowFace(Sprite frontSprite, string title, bool upright)
    {
        ResolveReferences();

        SetCardSlotVisible(false);
        SetActive(cardFront, true);

        if (cardFront != null)
        {
            cardFront.localScale = Vector3.one;
            cardFront.localRotation = Quaternion.identity;
        }

        Image frontImage = ResolveFrontImage();
        if (frontImage != null)
        {
            frontImage.sprite = frontSprite;
            frontImage.enabled = true;
            frontImage.gameObject.SetActive(true);
            frontImage.color = Color.white;
            frontImage.preserveAspect = true;
            frontImage.rectTransform.localScale = Vector3.one;
            frontImage.rectTransform.localRotation = upright
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, 180f);
        }

        SetText(title);
    }

    public void SetCardSlotVisible(bool visible)
    {
        if (cardSlot == null || cardSlot == gameObject)
            return;

        if (cardFront != null && cardSlot == cardFront.gameObject)
            return;

        cardSlot.SetActive(visible);
    }

    public void ResolveReferences()
    {
        if (cardFront == null)
            cardFront = FindChildRecursive(transform, "CardFront", "cardFront", "Front", "front", "CardRoot");

        cardImage = ResolveFrontImage();
        if (cardImage == null)
        {
            Transform imageTransform = FindChildRecursive(transform, "CardImage", "cardImage", "CardArt", "cardArt", "Image");
            cardImage = imageTransform != null ? imageTransform.GetComponent<Image>() : GetComponentInChildren<Image>(true);
        }

        if (cardTitleText == null && cardFront != null)
            cardTitleText = cardFront.GetComponentInChildren<TMP_Text>(true);

        if (cardTag == null)
            cardTag = GetComponentInChildren<TMP_Text>(true);
    }

    private Image ResolveFrontImage()
    {
        if (cardFront != null)
        {
            if (cardImage != null
                && IsChildOf(cardImage.transform, cardFront)
                && IsUsableFaceImage(cardImage))
            {
                return cardImage;
            }

            Image namedImage = FindImageRecursive(cardFront, "CardImage", "cardImage", "CardArt", "cardArt");
            if (namedImage != null)
            {
                cardImage = namedImage;
                return cardImage;
            }

            Image fallback = FindFirstUsableImage(cardFront);
            if (fallback != null)
            {
                cardImage = fallback;
                return cardImage;
            }
        }

        if (cardImage != null)
            return cardImage;

        return null;
    }

    private static Image FindImageRecursive(Transform root, params string[] names)
    {
        Transform imageTransform = FindChildRecursive(root, names);
        return imageTransform != null ? imageTransform.GetComponent<Image>() : null;
    }

    private Image FindFirstUsableImage(Transform root)
    {
        if (root == null) return null;

        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null  || !IsUsableFaceImage(image)) continue;
            return image;
        }

        return null;
    }

    private static bool IsUsableFaceImage(Image image)
    {
        if (image == null) return false;

        string objectName = image.gameObject.name;
        if (objectName.IndexOf("Background", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (objectName.IndexOf("CardBg", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (objectName.IndexOf("Frame", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return true;
    }

    private static bool IsChildOf(Transform child, Transform parent)
    {
        if (child == null || parent == null) return false;

        Transform current = child;
        while (current != null)
        {
            if (current == parent)
                return true;
            current = current.parent;
        }

        return false;
    }

    private void SetText(string text)
    {
        if (cardTitleText != null)
        {
            cardTitleText.gameObject.SetActive(true);
            cardTitleText.text = text;
        }

        if (cardTag != null && cardTag != cardTitleText)
            cardTag.text = text;
    }

    private static void SetActive(Transform target, bool active)
    {
        if (target != null)
            target.gameObject.SetActive(active);
    }

    private static Transform FindChildRecursive(Transform root, params string[] names)
    {
        if (root == null || names == null) return null;

        for (int i = 0; i < names.Length; i++)
        {
            if (root.name == names[i])
                return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), names);
            if (result != null)
                return result;
        }

        return null;
    }
}
