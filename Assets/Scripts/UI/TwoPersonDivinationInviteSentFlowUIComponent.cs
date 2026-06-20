/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/20/2026 7:28:15 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class TwoPersonDivinationInviteSentFlowUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button BackButton;
	public Text WaitingAcceptTitleText;
	public Text InviteCountdownText;
	public Image MyCardImage;
	public Button CancelInviteButton;
	public Button CopyInviteLinkButton;
	public Button FlipMyCardButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    TwoPersonDivinationInviteSentFlowUI mWindow=(TwoPersonDivinationInviteSentFlowUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(CancelInviteButton,mWindow.OnCancelInviteButtonClick);
	    target.AddButtonClickListener(CopyInviteLinkButton,mWindow.OnCopyInviteLinkButtonClick);
	    target.AddButtonClickListener(FlipMyCardButton,mWindow.OnFlipMyCardButtonClick);
	}
}
