using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TarotItem : MonoBehaviour
{
    public Image tarotImage;
    public TMP_Text tarotNameText;
    public TMP_Text tarotdesText;

    public void SetItemData(Sprite tarotSprite,string name,string des)
    {
        if (tarotImage != null)
        {
            tarotImage.sprite = tarotSprite;
        }
        if (tarotNameText != null)
            tarotNameText.text = name;
        if (tarotdesText != null)
            tarotdesText.text = des;
    }

}
