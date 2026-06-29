using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DivinationInfoItem : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text cardName;

    public TMP_Text cardDesText;  //塔罗牌的详细描述

    public void SetItemData(Sprite iconSprite,string cardNameStr,string cardDes)
    {
        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
        }
        if (cardName != null)
            cardName.text = cardNameStr;
        if (cardDesText != null)
            cardDesText.text = cardDes;
    }
}
