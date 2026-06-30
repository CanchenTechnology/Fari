/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/30/2026 3:07:20 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class TwoPersonDivinationUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button backButton;
	public Button settingButton;

	[Header("朋友信息")]
	public Image friendHead;
	public TMP_Text friendName;
	public TMP_Text infoText;
	public TMP_Text divinationType; //根据玩家选择的占卜类型
	public TMP_Text timeType; //占卜时间

	public TMP_Text friendNameInCard;

	public CardSlotItem cardSlotitem1;
	public CardSlotItem cardSlotitem2;
	public CardSlotItem cardSlotitem3;
	public Button flipBtn;

	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    TwoPersonDivinationUI mWindow=(TwoPersonDivinationUI)target;
	    target.AddButtonClickListener(backButton,mWindow.OnbackButtonClick);
	    target.AddButtonClickListener(settingButton,mWindow.OnsettingButtonClick);
	    target.AddButtonClickListener(flipBtn,mWindow.OnFlipButtonClick);
	}
}
