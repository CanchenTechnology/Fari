/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/17/2026 5:49:14 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class TarorSingleSpreadShuffleUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public Text TitleTextText;
	public Text SubtitleTextText;
	public CardSlotItem CardSlotItem1CardSlotItem;
	public CardSlotItem CardSlotItem2CardSlotItem;
	public CardSlotItem CardSlotItem3CardSlotItem;
	public CardSlotItem CardSlotItem4CardSlotItem;
	public CardSlotItem CardSlotItem5CardSlotItem;
	public Text cardTitleText;
	public Text InstructionTextText;
	public Button StartShuffleButton;

	[Header("洗牌配置")]
    public float shuffleCycleDuration = 0.08f;   // 每张牌快速切换的间隔
    public int shuffleCycles = 3;                 // 洗牌动画循环次数
    public float flipDuration = 0.4f;             // 单张翻牌持续时间
    public float cardRevealGap = 0.25f;           // 两张牌之间揭示间隔
    public Sprite cardBackSprite;                 // 卡牌背面图（可在 Inspector 覆盖）

	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    TarorSingleSpreadShuffleUI mWindow=(TarorSingleSpreadShuffleUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(StartShuffleButton,mWindow.OnStartShuffleButtonClick);
	}
}
