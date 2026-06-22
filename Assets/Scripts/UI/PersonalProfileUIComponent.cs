/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/21/2026 11:13:06 AM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class PersonalProfileUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button BackButton;
	public Image AvatarImageImage;
	public Text UserNameTitleText;
	public InputField BioInputInputField;
	public Button BirthdayArrowButton;
	public Text birthdayTextText;
	public Text birthdayTimeText;
	public Button TimeArrowButton;
	public Text CityTextText;
	public Button CityArrowButton;
	public Image AvatarHeadImage;
	public Button SetAvatarButton;
	public Button SaveBtnButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    PersonalProfileUI mWindow=(PersonalProfileUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddInputFieldListener(BioInputInputField,mWindow.OnBioInputInputChange,mWindow.OnBioInputInputEnd);
	    target.AddButtonClickListener(BirthdayArrowButton,mWindow.OnBirthdayArrowButtonClick);
	    target.AddButtonClickListener(TimeArrowButton,mWindow.OnTimeArrowButtonClick);
	    target.AddButtonClickListener(CityArrowButton,mWindow.OnCityArrowButtonClick);
	    target.AddButtonClickListener(SetAvatarButton,mWindow.OnSetAvatarButtonClick);
	    target.AddButtonClickListener(SaveBtnButton,mWindow.OnSaveBtnButtonClick);
	}
}
