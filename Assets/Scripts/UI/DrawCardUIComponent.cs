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

public class DrawCardUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;

	public Transform cardContainer;
	public GameObject cardGO;

	public Transform targetCardParent;
	public Image targetCard;
	public TMP_Text cardNameText;

	[Header("抽卡动画配置")]
	public int selectableCardCount = 12;
	public float fanWidth = 0f;
	public bool useResponsiveFanWidth = true;
	public float fanViewportWidthMultiplier = 1.42f;
	public float minFanWidth = 1120f;
	public float fanHeightOffset = 4f;
	public float fanRiseOffset = 108f;
	public float fanRotation = 18f;
	public float dealDuration = 0.42f;
	public float cardDealGap = 0.045f;
	public float selectDuration = 0.48f;
	public float flipDuration = 0.46f;
	public float resultHoldDuration = 1.5f;
	public bool infiniteScroll = true;
	public float deckCardScale = 0f;
	public float minDeckCardScale = 0.5f;
	public float maxDeckCardScale = 0.64f;
	public float maxDeckCardHeightRatio = 0.52f;
	public float maxDeckCardWidthRatio = 0.36f;
	public float selectedCardScale = 0.9f;
	public float scrollEdgePadding = 12f;
	public float minScrollRange = 0f;
	public float scrollRangeMultiplier = 1.05f;
	public bool useScrollInertia = true;
	public float scrollInertiaMultiplier = 1.45f;
	public float scrollDeceleration = 2200f;
	public float minFlickVelocity = 80f;
	public float maxFlickVelocity = 3800f;
	public float maxInertiaDuration = 1.15f;
	public float dragSensitivity = 1f;
	public float dragClickThreshold = 12f;
	public float dragPullSelectThreshold = 110f;
	public float dragPullDirectionBias = 1.15f;
	public Sprite cardBackSprite;
	public Color selectedGlowColor = new Color(1f, 0.58f, 0.12f, 0.88f);

	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    DrawCardUI mWindow=(DrawCardUI)target;
	}
}
