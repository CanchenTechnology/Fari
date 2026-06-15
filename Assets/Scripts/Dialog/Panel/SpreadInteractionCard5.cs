using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpreadInteractionCard5 : MonoBehaviour
{
    [Header("标题")]
    public Text cardTitle;
    public Text cardSubtitle1;
    public Text cardSubtitle2;

    [Header("抽牌")]
    public Button drawCardBtn;
    public Text drawCardBtnText;           // 按钮文字（可选），默认"开始抽牌"

    [Header("先继续聊聊")]
    public Button chatFirstBtn;

    [Header("卡牌槽位")]
    public Image cardSlot1Image;
    public Image cardSlot2Image;
    public Image cardSlot3Image;
    public Image cardSlot4Image;
    public Image cardSlot5Image;

    [Header("槽位标签")]
    public Text slot1Label;                
    public Text slot2Label;                
    public Text slot3Label;  


    [Header("卡牌背面（占位图）")]
    public Sprite cardBackSprite;

    [Header("操作按钮")]
    public Button selectSpreadBtn;         // 「为这个问题选择牌阵」
    public Button continueAskBtn;          // 「继续追问」
    public Button checkTomorrowBtn;        // 「明天再看这条线索」
}
