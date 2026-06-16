using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CardSlotItem : MonoBehaviour
{
    public Image cardImage;
    public Text cardTag;

    public void SetItemData(Sprite sprite,string cardTagStr)
    {
        cardImage.sprite = sprite;
        cardTag.text = cardTagStr;
    }
}
