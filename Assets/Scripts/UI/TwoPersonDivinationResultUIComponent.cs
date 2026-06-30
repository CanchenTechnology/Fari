/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/30/2026 4:23:36 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class TwoPersonDivinationResultUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button BackButton;

	public Button ContinueChatButton;

	[Header("朋友详细信息")]
	public Image friendHead;
	public TMP_Text friendName;
	public TMP_Text divinationType;
	public TMP_Text divinationTime;
	[Header("塔罗牌")]
	public Image yourTarotImage;
	public Image resultTarotImage;
	public Image herTarotImage;
	public TMP_Text drawCardFriendName1;  //也就是朋友名字
	[Header("塔罗牌详细信息")]
	public TMP_Text drawCardFriendName2;  //也就是朋友名字
	public DivinationInfoItem tarotItem1;  //根据双人占卜提出的问题进行分析每张牌的含义
	public DivinationInfoItem tarotItem2;
	public DivinationInfoItem tarotItem3;

	[Header("综合评价")]
	public TMP_Text OverallJudgmentText;//根据双人占卜提出的问题，以及抽到到的三张牌，进行综合评价
	[Header("行动建议")]
	public TMP_Text ActionSectionText;//根据双人占卜提出的问题，以及抽到到的三张牌，进行行动建议

	[Header("可能感兴趣的事情")] //根据双人占卜提出的问题，猜一下玩家可能会提的问题，或者最有可能会接着问的问题
	public QuestRowItem topicItem1;
	public QuestRowItem topicItem2;
	public QuestRowItem topicItem3;
	public QuestRowItem topicItem4;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    TwoPersonDivinationResultUI mWindow=(TwoPersonDivinationResultUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(ContinueChatButton,mWindow.OnContinueChatButtonClick);
	}
}
