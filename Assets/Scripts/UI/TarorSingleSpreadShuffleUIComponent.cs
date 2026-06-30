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
	public TMP_Text TitleTextText;
	public TMP_Text SubtitleTextText;
	public CardSlotItem CardSlotItem1CardSlotItem;
	public CardSlotItem CardSlotItem2CardSlotItem;
	public CardSlotItem CardSlotItem3CardSlotItem;

	public TMP_Text cardTitleText;
	public TMP_Text InstructionTextText;

	public Button hideBtn;

	public Image maskUI;
	[Header("卡牌动画")]
	public Transform cardContainer;
	public GameObject cardImageGo;
	public float shuffleCycleDuration = 0.08f;
	public int shuffleCycles = 3;
	public float flipDuration = 0.62f;
	[Tooltip("选中牌飞到屏幕中心并放大的时长。")]
	public float centerRevealDuration = 0.68f;
	[Tooltip("屏幕中心展示牌的最大缩放。")]
	public float centerRevealMaxScale = 1.15f;
	[Tooltip("中心展示遮罩淡入或淡出的时长。")]
	public float centerRevealMaskFadeDuration = 0.18f;
	[Tooltip("牌背翻开前，中心展示牌轻微抖动的时长。")]
	public float centerRevealShakeDuration = 0.42f;
	[Tooltip("牌背翻开前，中心展示牌轻微左右位移抖动的强度。")]
	public float centerRevealShakePosition = 10f;
	[Tooltip("牌背翻开前，中心展示牌轻微旋转抖动的角度。")]
	public float centerRevealShakeRotation = 5f;
	[Tooltip("逆位牌翻到正面后额外反转的时长。")]
	public float reverseRotateDuration = 0.36f;
	public float cardRevealGap = 0.45f;
	public Sprite cardBackSprite;


	[Header("牌扇抽卡")]
	[Tooltip("牌扇里显示的可选牌数量。")]
	public int selectableCardCount = 12;
	[Tooltip("牌扇宽度。填 0 时按容器宽度自动计算。")]
	public float fanWidth = 0f;
	[Tooltip("牌扇宽度相对容器宽度的倍率，值越大越容易延伸到屏幕外。")]
	public float fanViewportWidthMultiplier = 1.42f;
	[Tooltip("牌扇最小宽度。")]
	public float minFanWidth = 1120f;
	[Tooltip("牌扇在容器内的纵向偏移。")]
	public float fanHeightOffset = 0f;
	[Tooltip("中间牌的上拱高度。")]
	public float fanRiseOffset = 108f;
	[Tooltip("两侧牌的最大倾斜角度。")]
	public float fanRotation = 18f;
	[Tooltip("牌扇中卡牌的固定缩放。填 0 时自动计算。")]
	public float deckCardScale = 0f;
	public float minDeckCardScale = 0.5f;
	public float maxDeckCardScale = 0.64f;
	[Tooltip("抽出第一张牌后，牌扇向下收拢的距离。填 0 时保持原位。")]
	public float drawnFanLowerOffset = 0f;
	[Tooltip("抽出第一张牌后，剩余牌扇的缩放倍率。")]
	public float drawnFanScaleMultiplier = 0.88f;
	[Tooltip("横向拖动牌扇的灵敏度。")]
	public float dragSensitivity = 1f;
	[Tooltip("拖动距离超过该值后，不再视为点击。")]
	public float dragClickThreshold = 12f;
	[Tooltip("向上拖动超过该距离后松手会抽出这张牌。")]
	public float dragPullSelectThreshold = 110f;
	[Tooltip("向上抽牌时纵向距离需要相对横向距离占优的倍率。")]
	public float dragPullDirectionBias = 1.15f;
	[Tooltip("长按卡牌达到该时长后直接抽牌。")]
	public float longPressSelectDuration = 0.45f;
	[Tooltip("双击卡牌抽牌的最大间隔；单击会等待该时长后触发，避免和双击冲突。")]
	public float doubleClickSelectInterval = 0.24f;
	[Tooltip("选中牌飞到目标卡槽的时长。")]
	public float selectDuration = 0.66f;
	[Tooltip("是否启用横向拖动后的惯性滚动。")]
	public bool useScrollInertia = true;
	public float scrollInertiaMultiplier = 1.35f;
	public float scrollDeceleration = 2200f;
	public float minFlickVelocity = 80f;
	public float maxFlickVelocity = 3800f;
	public float maxInertiaDuration = 1.1f;


	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    TarorSingleSpreadShuffleUI mWindow=(TarorSingleSpreadShuffleUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	}
}
