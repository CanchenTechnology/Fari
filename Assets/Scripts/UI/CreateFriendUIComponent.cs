/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:2026/6/13 12:28:34
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class CreateFriendUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public Button NotificationsButton;
	public ScrollRect FormScrollContainerScrollRect;
	public Button Avatar1Button;
	public Button Avatar2Button;
	public Button Avatar3Button;
	public Button Avatar4Button;
	public Button UploadAvatarButton;
	public InputField UsernameInputField;
	public InputField BirthdayInputField;
	public InputField BirthTimeInputField;
	public InputField CityInputField;
	public Button SubmitButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    CreateFriendUI mWindow=(CreateFriendUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(NotificationsButton,mWindow.OnNotificationsButtonClick);
	    target.AddButtonClickListener(Avatar1Button,mWindow.OnAvatar1ButtonClick);
	    target.AddButtonClickListener(Avatar2Button,mWindow.OnAvatar2ButtonClick);
	    target.AddButtonClickListener(Avatar3Button,mWindow.OnAvatar3ButtonClick);
	    target.AddButtonClickListener(Avatar4Button,mWindow.OnAvatar4ButtonClick);
	    target.AddButtonClickListener(UploadAvatarButton,mWindow.OnUploadAvatarButtonClick);
	    target.AddInputFieldListener(UsernameInputField,mWindow.OnUsernameInputChange,mWindow.OnUsernameInputEnd);
	    target.AddInputFieldListener(BirthdayInputField,mWindow.OnBirthdayInputChange,mWindow.OnBirthdayInputEnd);
	    target.AddInputFieldListener(BirthTimeInputField,mWindow.OnBirthTimeInputChange,mWindow.OnBirthTimeInputEnd);
	    target.AddInputFieldListener(CityInputField,mWindow.OnCityInputChange,mWindow.OnCityInputEnd);
	    target.AddButtonClickListener(SubmitButton,mWindow.OnSubmitButtonClick);
	}
}
