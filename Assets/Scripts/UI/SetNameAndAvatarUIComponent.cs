/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:2026/6/13 11:13:48
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class SetNameAndAvatarUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public ToggleGroup AvatarSelectionToggleGroup;
	public Toggle AvatarOption1Toggle;
	public Toggle AvatarOption2Toggle;
	public Toggle AvatarOption3Toggle;
	public InputField NameInputField;
	public Button ContinueButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    SetNameAndAvatarUI mWindow=(SetNameAndAvatarUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddToggleClickListener(AvatarOption1Toggle,mWindow.OnAvatarOption1ToggleChange);
	    target.AddToggleClickListener(AvatarOption2Toggle,mWindow.OnAvatarOption2ToggleChange);
	    target.AddToggleClickListener(AvatarOption3Toggle,mWindow.OnAvatarOption3ToggleChange);
	    target.AddInputFieldListener(NameInputField,mWindow.OnNameInputChange,mWindow.OnNameInputEnd);
	    target.AddButtonClickListener(ContinueButton,mWindow.OnContinueButtonClick);
	}
}
