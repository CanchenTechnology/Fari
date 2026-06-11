/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/11/2026 2:21:13 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class UnlockProUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButtonButton;
	public Text ValidityLabelText;
	public Text ValidityValueText;
	public Button ManageSubscriptionBtnButton;
	public Button RestorePurchaseBtnButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    UnlockProUI mWindow=(UnlockProUI)target;
	    target.AddButtonClickListener(BackButtonButton,mWindow.OnBackButtonButtonClick);
	    target.AddButtonClickListener(ManageSubscriptionBtnButton,mWindow.OnManageSubscriptionBtnButtonClick);
	    target.AddButtonClickListener(RestorePurchaseBtnButton,mWindow.OnRestorePurchaseBtnButtonClick);
	}
}
