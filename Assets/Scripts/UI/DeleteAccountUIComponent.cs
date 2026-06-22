/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/21/2026 2:09:55 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class DeleteAccountUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button BackButton;
	public Toggle confirmToggle;
	public Button DeleteAccountButton;
	public Button CancelButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    DeleteAccountUI mWindow=(DeleteAccountUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddToggleClickListener(confirmToggle,mWindow.OnconfirmToggleChange);
	    target.AddButtonClickListener(DeleteAccountButton,mWindow.OnDeleteAccountButtonClick);
	    target.AddButtonClickListener(CancelButton,mWindow.OnCancelButtonClick);
	}
}
