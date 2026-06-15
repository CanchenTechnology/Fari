/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/15/2026 10:50:12 AM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class TodayCardUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public Button ShareButton;
	public ScrollRect DailyCardScrollScrollRect;
	public Button AskQuestion1Button;
	public Button AskQuestion2Button;
	public Button AskQuestion3Button;
	public Button ContinueChatButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    TodayCardUI mWindow=(TodayCardUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(ShareButton,mWindow.OnShareButtonClick);
	    target.AddButtonClickListener(AskQuestion1Button,mWindow.OnAskQuestion1ButtonClick);
	    target.AddButtonClickListener(AskQuestion2Button,mWindow.OnAskQuestion2ButtonClick);
	    target.AddButtonClickListener(AskQuestion3Button,mWindow.OnAskQuestion3ButtonClick);
	    target.AddButtonClickListener(ContinueChatButton,mWindow.OnContinueChatButtonClick);
	}
}
