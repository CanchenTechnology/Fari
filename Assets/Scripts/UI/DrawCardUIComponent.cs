/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/26/2026 1:52:21 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Video;

public class DrawCardUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;

	public Transform cardContainer;
	public GameObject cardGO;

	public Transform targetCardParent;
	public Image targetCard;
	public Transform targetCardBack;
	public Transform targetCardFront;
	public Image targetCardFrontImage;
	public TMP_Text cardNameText;
	public VideoPlayer backgroundVideoPlayer;

	public Transform cardFrontParent;
	public Button exitBtn;

	[Header("抽卡动画配置")]
	[Header("牌组布局")]
	[Tooltip("本次抽卡界面生成的可选牌数量。")]
	public int selectableCardCount = 12;
	[Tooltip("牌扇总宽度。填 0 时使用响应式宽度和最小宽度自动计算。")]
	public float fanWidth = 0f;
	[Tooltip("是否根据当前容器宽度自动扩展牌扇宽度。")]
	public bool useResponsiveFanWidth = true;
	[Tooltip("响应式牌扇宽度倍率，值越大，牌组左右越容易超出屏幕。")]
	public float fanViewportWidthMultiplier = 1.42f;
	[Tooltip("牌扇最小宽度，防止窄屏或容器较小时牌挤在一起。")]
	public float minFanWidth = 1120f;
	[Tooltip("整组牌在容器内的基础纵向偏移，值越大越靠上。")]
	public float fanHeightOffset = 4f;
	[Tooltip("牌扇中间牌的抬高高度，值越大弧度越明显。")]
	public float fanRiseOffset = 108f;
	[Tooltip("牌扇两侧最大旋转角度，中心附近会逐渐接近 0。")]
	public float fanRotation = 18f;

	[Header("动画节奏")]
	[Tooltip("发牌入场时每张牌移动到目标位置的时长。")]
	public float dealDuration = 0.42f;
	[Tooltip("连续发牌之间的间隔时间，值越大牌出现得越分散。")]
	public float cardDealGap = 0.045f;
	[Tooltip("选中牌飞向结果位置的移动时长。")]
	public float selectDuration = 0.48f;
	[Tooltip("选中牌飞到中心并放大的时长。")]
	public float centerRevealDuration = 0.68f;
	[Tooltip("中心展示牌的最大缩放。")]
	public float centerRevealMaxScale = 1.15f;
	[Tooltip("牌背翻开前，中心展示牌轻微抖动的时长。")]
	public float centerRevealShakeDuration = 0.42f;
	[Tooltip("牌背翻开前，中心展示牌轻微左右位移抖动的强度。")]
	public float centerRevealShakePosition = 10f;
	[Tooltip("牌背翻开前，中心展示牌轻微旋转抖动的角度。")]
	public float centerRevealShakeRotation = 5f;
	[Tooltip("选中牌翻转到正面的总时长。")]
	public float flipDuration = 0.46f;
	[Tooltip("当前流程不再自动关闭结果页；翻牌完成后会等待 exitBtn 点击返回。该值保留给旧流程或备用停留设置。")]
	public float resultHoldDuration = 1.5f;

	[Header("卡牌尺寸")]
	[Tooltip("开启后横向拖动牌组会循环滚动，牌会从另一侧补回来。")]
	public bool infiniteScroll = true;
	[Tooltip("牌组中卡牌的固定缩放。填 0 时根据容器和限制比例自动计算。")]
	public float deckCardScale = 0f;
	[Tooltip("自动计算卡牌缩放时允许的最小缩放。")]
	public float minDeckCardScale = 0.5f;
	[Tooltip("自动计算卡牌缩放时允许的最大缩放。")]
	public float maxDeckCardScale = 0.64f;
	[Tooltip("自动计算卡牌缩放时，卡牌最大高度占容器高度的比例。")]
	public float maxDeckCardHeightRatio = 0.52f;
	[Tooltip("自动计算卡牌缩放时，卡牌最大宽度占容器宽度的比例。")]
	public float maxDeckCardWidthRatio = 0.36f;
	[Tooltip("抽中的牌飞向结果位置前使用的选中态缩放下限。")]
	public float selectedCardScale = 0.9f;

	[Header("横向滚动")]
	[Tooltip("有限滚动模式下，牌组左右边缘额外保留的可拖动留白。无限滚动模式下基本不生效。")]
	public float scrollEdgePadding = 12f;
	[Tooltip("有限滚动模式下的最小可滚动范围。无限滚动模式下不生效。")]
	public float minScrollRange = 0f;
	[Tooltip("有限滚动模式下，基于内容超出宽度计算出的滚动范围倍率。")]
	public float scrollRangeMultiplier = 1.05f;
	[Tooltip("松手后是否根据拖动速度继续滑动一段距离。")]
	public bool useScrollInertia = true;
	[Tooltip("惯性滚动速度倍率，值越大甩动越远。")]
	public float scrollInertiaMultiplier = 1.45f;
	[Tooltip("惯性滚动减速度，值越大越快停下来。")]
	public float scrollDeceleration = 2200f;
	[Tooltip("触发惯性滚动所需的最小横向速度。")]
	public float minFlickVelocity = 80f;
	[Tooltip("惯性滚动允许的最大横向速度，防止一次甩太远。")]
	public float maxFlickVelocity = 3800f;
	[Tooltip("惯性滚动最长持续时间。")]
	public float maxInertiaDuration = 1.15f;

	[Header("拖拽抽牌")]
	[Tooltip("手指或鼠标横向拖动的灵敏度倍率。")]
	public float dragSensitivity = 1f;
	[Tooltip("拖动距离超过该值后，不再当作普通点击。")]
	public float dragClickThreshold = 12f;
	[Tooltip("从牌上向上拖动超过该距离后，松手会确认抽中这张牌。")]
	public float dragPullSelectThreshold = 110f;
	[Tooltip("判断向上抽牌时，纵向距离需要相对横向距离占优的倍率。")]
	public float dragPullDirectionBias = 1.15f;
	[Tooltip("双击卡牌抽牌的最大间隔；单击会等待该时长后触发，避免和双击冲突。")]
	public float doubleClickSelectInterval = 0.24f;

	[Header("视觉资源")]
	[Tooltip("牌背图。未配置时会使用模板 Card 上当前的图片。")]
	public Sprite cardBackSprite;
	[Tooltip("选中牌描边和高光使用的颜色。")]
	public Color selectedGlowColor = new Color(1f, 0.58f, 0.12f, 0.88f);

	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    DrawCardUI mWindow=(DrawCardUI)target;
	}
}
