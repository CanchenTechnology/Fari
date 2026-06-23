/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/20/2026 7:18:45 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class TwoPersonDivinationWaitingFriendFlowUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button BackButton;
	public TMP_Text WaitingTitleText;
	public Image MyCardSlotImage;
	public Image FriendCardSlotImage;
	public TMP_Text FriendCardStateText;
	public Image YouStateAvatarImage;
	public Image FriendStateAvatarImage;
	public TMP_Text FriendNameText;
	public TMP_Text FriendStateTextText;
	public Button RemindFriendButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    TwoPersonDivinationWaitingFriendFlowUI mWindow=(TwoPersonDivinationWaitingFriendFlowUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(RemindFriendButton,mWindow.OnRemindFriendButtonClick);
	}
}
