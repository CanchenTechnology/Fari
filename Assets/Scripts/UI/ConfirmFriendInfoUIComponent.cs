/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/19/2026 11:24:40 AM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class ConfirmFriendInfoUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public Image AvatarImage;
	public TMP_Text FriendNameText;
	public Button EditNameButton;
	public TMP_Text BirthdayValueText;
	public Button EditBirthdayButton;
	public TMP_Text BirthTimeValueText;
	public Button EditBirthTimeButton;
	public TMP_Text BirthCityValueText;
	public Button EditBirthCityButton;
	public Button CreateFriendButton;
	public Button BackEditButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    ConfirmFriendInfoUI mWindow=(ConfirmFriendInfoUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(EditNameButton,mWindow.OnEditNameButtonClick);
	    target.AddButtonClickListener(EditBirthdayButton,mWindow.OnEditBirthdayButtonClick);
	    target.AddButtonClickListener(EditBirthTimeButton,mWindow.OnEditBirthTimeButtonClick);
	    target.AddButtonClickListener(EditBirthCityButton,mWindow.OnEditBirthCityButtonClick);
	    target.AddButtonClickListener(CreateFriendButton,mWindow.OnCreateFriendButtonClick);
	    target.AddButtonClickListener(BackEditButton,mWindow.OnBackEditButtonClick);
	}
}
