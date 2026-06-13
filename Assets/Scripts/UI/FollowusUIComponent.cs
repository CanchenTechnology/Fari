/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:2026/6/13 15:37:40
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class FollowusUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public Button OpenLink_InstaButton;
	public Button OpenLink_FBButton;
	public Button OpenLink_TwitterButton;
	public Button OpenLink_TikTokButton;
	public Button OpenLink_PinterestButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    FollowusUI mWindow=(FollowusUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(OpenLink_InstaButton,mWindow.OnOpenLink_InstaButtonClick);
	    target.AddButtonClickListener(OpenLink_FBButton,mWindow.OnOpenLink_FBButtonClick);
	    target.AddButtonClickListener(OpenLink_TwitterButton,mWindow.OnOpenLink_TwitterButtonClick);
	    target.AddButtonClickListener(OpenLink_TikTokButton,mWindow.OnOpenLink_TikTokButtonClick);
	    target.AddButtonClickListener(OpenLink_PinterestButton,mWindow.OnOpenLink_PinterestButtonClick);
	}
}
