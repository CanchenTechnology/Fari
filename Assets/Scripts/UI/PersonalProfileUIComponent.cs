/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/11/2026 1:53:34 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class PersonalProfileUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public Image AvatarImageImage;
	public Button MoonAvatarBtnButton;
	public Button PersonAvatarBtnButton;
	public InputField UserNameInputInputField;
	public InputField BirthdayInputInputField;
	public InputField TimeInputInputField;
	public InputField CityInputInputField;
	public Button SaveBtnButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    PersonalProfileUI mWindow=(PersonalProfileUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(MoonAvatarBtnButton,mWindow.OnMoonAvatarBtnButtonClick);
	    target.AddButtonClickListener(PersonAvatarBtnButton,mWindow.OnPersonAvatarBtnButtonClick);
	    target.AddInputFieldListener(UserNameInputInputField,mWindow.OnUserNameInputInputChange,mWindow.OnUserNameInputInputEnd);
	    target.AddInputFieldListener(BirthdayInputInputField,mWindow.OnBirthdayInputInputChange,mWindow.OnBirthdayInputInputEnd);
	    target.AddInputFieldListener(TimeInputInputField,mWindow.OnTimeInputInputChange,mWindow.OnTimeInputInputEnd);
	    target.AddInputFieldListener(CityInputInputField,mWindow.OnCityInputInputChange,mWindow.OnCityInputInputEnd);
	    target.AddButtonClickListener(SaveBtnButton,mWindow.OnSaveBtnButtonClick);
	}
}
