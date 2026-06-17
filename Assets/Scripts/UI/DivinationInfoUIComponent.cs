/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/17/2026 10:36:25 AM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class DivinationInfoUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public Button ShareButton;
	public TarotItem Card1TarotItem;
	public TarotItem Card2TarotItem;
	public TarotItem Card3TarotItem;
	public DivinationInfoItem Item1DivinationInfoItem;
	public DivinationInfoItem Item2DivinationInfoItem;
	public DivinationInfoItem Item3DivinationInfoItem;
	public Text JudgeContentText;
	public Text AdviceContentText;
	public Button Question1Button;
	public Button Question2Button;
	public Button Question3Button;
	public Button ContinueChatButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    DivinationInfoUI mWindow=(DivinationInfoUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(ShareButton,mWindow.OnShareButtonClick);
	    target.AddButtonClickListener(Question1Button,mWindow.OnQuestion1ButtonClick);
	    target.AddButtonClickListener(Question2Button,mWindow.OnQuestion2ButtonClick);
	    target.AddButtonClickListener(Question3Button,mWindow.OnQuestion3ButtonClick);
	    target.AddButtonClickListener(ContinueChatButton,mWindow.OnContinueChatButtonClick);
	}
}
