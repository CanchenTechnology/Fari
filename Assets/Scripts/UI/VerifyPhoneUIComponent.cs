/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:2026/6/13 11:09:38
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class VerifyPhoneUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public Button CountryCodeButton;
	public InputField PhoneNumberInputField;
	public Button ResendCodeButton;
	public InputField VerificationCodeInputField;
	public Button VerifyButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    VerifyPhoneUI mWindow=(VerifyPhoneUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(CountryCodeButton,mWindow.OnCountryCodeButtonClick);
	    target.AddInputFieldListener(PhoneNumberInputField,mWindow.OnPhoneNumberInputChange,mWindow.OnPhoneNumberInputEnd);
	    target.AddButtonClickListener(ResendCodeButton,mWindow.OnResendCodeButtonClick);
	    target.AddInputFieldListener(VerificationCodeInputField,mWindow.OnVerificationCodeInputChange,mWindow.OnVerificationCodeInputEnd);
	    target.AddButtonClickListener(VerifyButton,mWindow.OnVerifyButtonClick);
	}
}
