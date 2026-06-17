using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class DivinationInfoItem : MonoBehaviour
{
    public Image iconImage;
    public Text cardName;

    public Text cardDesText;  //塔罗牌的详细描述

    public void SetItemData(Sprite iconSprite,string cardNameStr,string cardDes)
    {
        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.preserveAspect = true;
        }
        if (cardName != null)
            cardName.text = cardNameStr;
        if (cardDesText != null)
            cardDesText.text = cardDes;
    }
}
