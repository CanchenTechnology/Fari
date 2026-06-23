using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TarotItem : MonoBehaviour
{
    public Image tarotImage;
    public TMP_Text tarotNameText;
    public TMP_Text tarotTagText;

    public void SetItemData(Sprite tarotSprite,string name,string tag)
    {
        if (tarotImage != null)
        {
            tarotImage.sprite = tarotSprite;
            tarotImage.preserveAspect = true;
        }
        if (tarotNameText != null)
            tarotNameText.text = name;
        if (tarotTagText != null)
            tarotTagText.text = tag;
    }

}
