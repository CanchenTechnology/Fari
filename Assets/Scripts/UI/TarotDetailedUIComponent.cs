/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/30/2026 9:57:30 AM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class TarotDetailedUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;

	[Header("单人占卜")]
    public GameObject singlePersonTarotType;  //单人占卜时候显示,三牌占卜时显示
	public TMP_Text singleTypeText;
	public TMP_Text singleSourceText; //来源：每日占卜，三牌占卜
	public TMP_Text singleDivinationTimeText;// 占卜时间

	[Header("双人占卜")]

	public GameObject twoPersonTarotType;//双人占卜时候显示
	public TMP_Text twoPersonTypeText;
	public TMP_Text twoPersonSourceText; //来源：每日占卜，三牌占卜
	public TMP_Text twoPersonDivinationTimeText;// 占卜时间
	//用户信息
	public Image userImage;
	public TMP_Text userName;
	public TMP_Text userInfo;
	//朋友信息
	public Image friendImage;
	public TMP_Text friendName;
	public TMP_Text friendInfo;

	[Header("三牌")]
	public GameObject threeCardContainer; //三排占卜，双人占卜显示
	public Image tarot1Image;
	public Image tarot2Image;
	public Image tarot3Image;

	public DivinationInfoItem tarotItem1;
	public DivinationInfoItem tarotItem2;
	public DivinationInfoItem tarotItem3;

	[Header("三牌详细")]
	public GameObject tarotCardDesList; //三排占卜，双人占卜显示

	public Button BackButton;
	public Button ContinueChatButton;
	public TMP_Text OverallJudgmentText;  //综合评价

	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    TarotDetailedUI mWindow=(TarotDetailedUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(ContinueChatButton,mWindow.OnContinueChatButtonClick);
	}
}
