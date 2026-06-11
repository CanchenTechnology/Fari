/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/11/2026 1:14:27 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class MyUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Image AvatarImage;
	public Text UsernameText;
	public Text UserSubtitleText;
	public Text tatTodayCardValueText;
	public Text StatTodaydialouNumText;
	public Text StatTodaydesText;
	public Button btn_divinationButton;
	public Button btn_myinfoButton;
	public Button memoryButton;
	public Button proButton;
	public Button accountButton;
	public Button feedbackButton;
	public Button shareButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    MyUI mWindow=(MyUI)target;
	    target.AddButtonClickListener(btn_divinationButton,mWindow.Onbtn_divinationButtonClick);
	    target.AddButtonClickListener(btn_myinfoButton,mWindow.Onbtn_myinfoButtonClick);
	    target.AddButtonClickListener(memoryButton,mWindow.OnmemoryButtonClick);
	    target.AddButtonClickListener(proButton,mWindow.OnproButtonClick);
	    target.AddButtonClickListener(accountButton,mWindow.OnaccountButtonClick);
	    target.AddButtonClickListener(feedbackButton,mWindow.OnfeedbackButtonClick);
	    target.AddButtonClickListener(shareButton,mWindow.OnshareButtonClick);
	}
}
