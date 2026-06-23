using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardSlotItem : MonoBehaviour
{
    public Image cardImage;
    public TMP_Text cardTag;

    public void SetItemData(Sprite sprite,string cardTagStr)
    {
        cardImage.sprite = sprite;
        cardTag.text = cardTagStr;
    }
}
