/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/30/2026 3:50:37 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Video;

public class DrawMyCardUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button backButton;
	public VideoPlayer idleVideoPlayer;
	

	[Header("牌阵")]
	public Transform cardContainer;
	public GameObject cardGO;

	[Header("开牌界面")]
	public GameObject openCardPanel;

	public Button cancelBtn; //退出开牌界面

	public Button openBtn;//翻牌，执行翻牌动画

	public Button nextBtn;  //当翻完牌之后再显示这个按钮，然后点击这个按钮，就会跳转到占卜结果界面

	public GameObject cardSlot;

	public GameObject cardBack; 
	
	public GameObject cardFront; //牌正面
	
	public Image cardImage;
	public TMP_Text cardTitle;

	[Header("抽卡动画配置")]
	[Header("视觉资源")]
	[Tooltip("牌背图。未配置时会使用模板 Card 或 cardBack 上当前的图片。")]
	public Sprite cardBackSprite;

	[Header("洗牌入场")]
	[Tooltip("牌组打散到桌面上的时长。")]
	public float shuffleScatterDuration = 1.6f;
	[Tooltip("打散后收拢成牌堆的时长。")]
	public float shuffleGatherDuration = 0.83f;
	[Tooltip("收拢后从左到右展开成扇形的单张牌时长。")]
	public float shuffleFanOutDuration = 0.45f;
	[Tooltip("扇形展开时每张牌之间的延迟。")]
	public float shuffleFanOutGap = 0.055f;

	[Header("牌扇布局")]
	[Tooltip("牌扇里显示的可选牌数量。")]
	public int selectableCardCount = 12;
	[Tooltip("牌扇总宽度。填 0 时按当前容器宽度自动计算。")]
	public float fanWidth = 0f;
	[Tooltip("自动牌扇宽度相对容器宽度的倍率，值越大牌组越容易延伸到屏幕外。")]
	public float fanViewportWidthMultiplier = 1.42f;
	[Tooltip("自动牌扇最小宽度。")]
	public float minFanWidth = 1120f;
	[Tooltip("整组牌在容器内的基础纵向偏移。")]
	public float fanHeightOffset = 0f;
	[Tooltip("中间牌上拱高度。")]
	public float fanRiseOffset = 108f;
	[Tooltip("两侧牌最大倾斜角度。")]
	public float fanRotation = 18f;
	[Tooltip("牌扇中卡牌固定缩放。填 0 时根据容器自动计算。")]
	public float deckCardScale = 0f;
	[Tooltip("自动计算卡牌缩放时允许的最小值。")]
	public float minDeckCardScale = 0.5f;
	[Tooltip("自动计算卡牌缩放时允许的最大值。")]
	public float maxDeckCardScale = 0.64f;
	[Tooltip("自动计算卡牌缩放时，卡牌最大高度占容器高度的比例。")]
	public float maxDeckCardHeightRatio = 0.56f;
	[Tooltip("自动计算卡牌缩放时，卡牌最大宽度占容器宽度的比例。")]
	public float maxDeckCardWidthRatio = 0.36f;

	[Header("交互")]
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
	[Tooltip("是否启用横向拖动后的惯性滚动。")]
	public bool useScrollInertia = true;
	[Tooltip("惯性滚动速度倍率。")]
	public float scrollInertiaMultiplier = 1.35f;
	[Tooltip("惯性滚动减速度。")]
	public float scrollDeceleration = 2200f;
	[Tooltip("触发惯性滚动所需的最小横向速度。")]
	public float minFlickVelocity = 80f;
	[Tooltip("惯性滚动允许的最大横向速度。")]
	public float maxFlickVelocity = 3800f;
	[Tooltip("惯性滚动最长持续时间。")]
	public float maxInertiaDuration = 1.1f;

	[Header("选牌与翻牌")]
	[Tooltip("选中牌飞到 cardSlot 并放大的时长。")]
	public float selectDuration = 0.9f;
	[Tooltip("选中后其他牌淡出的时长。")]
	public float otherCardFadeDuration = 0.32f;
	[Tooltip("OpenCardPanel 淡入淡出的时长。")]
	public float openPanelFadeDuration = 0.22f;
	[Tooltip("点击打开后，翻牌前牌背抖动的时长。")]
	public float shakeDuration = 0.68f;
	[Tooltip("翻牌前牌背左右抖动的位移强度。")]
	public float shakePosition = 12f;
	[Tooltip("翻牌前牌背旋转抖动的角度。")]
	public float shakeRotation = 6f;
	[Tooltip("牌背翻到正面的总时长。")]
	public float flipDuration = 0.92f;
	[Tooltip("牌正面完整显示后，结束动画前额外停留的时长。")]
	public float frontRevealHoldDuration = 0.65f;



	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    DrawMyCardUI mWindow=(DrawMyCardUI)target;
	    if (backButton != null) target.AddButtonClickListener(backButton,mWindow.OnbackButtonClick);
	    if (cancelBtn != null) target.AddButtonClickListener(cancelBtn,mWindow.OnCancelBtnClick);
	    if (openBtn != null) target.AddButtonClickListener(openBtn,mWindow.OnOpenBtnClick);
	    if (nextBtn != null) target.AddButtonClickListener(nextBtn,mWindow.OnNextBtnClick);
	}
}
