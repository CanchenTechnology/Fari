/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/19/2026 1:01:10 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class JumpToDialogUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Image FriendAvatarImage;
	public TMP_Text FriendNameText;
	public TMP_Text SelectedFriendNameText;
	public Button RemoveSelectedFriendButton;
	public TMP_InputField QuestionInputInputField;
	public Button SendQuestionButton;
	public Button EnterDialogButton;
	public Button PromptRecentStatusButton;
	public Button PromptRelationshipButton;
	public Button PromptContactButton;
	public Button PromptViewMeButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    JumpToDialogUI mWindow=(JumpToDialogUI)target;
	    target.AddButtonClickListener(RemoveSelectedFriendButton,mWindow.OnRemoveSelectedFriendButtonClick);
	    target.AddInputFieldListener(QuestionInputInputField,mWindow.OnQuestionInputInputChange,mWindow.OnQuestionInputInputEnd);
	    target.AddButtonClickListener(SendQuestionButton,mWindow.OnSendQuestionButtonClick);
	    target.AddButtonClickListener(EnterDialogButton,mWindow.OnEnterDialogButtonClick);
	    target.AddButtonClickListener(PromptRecentStatusButton,mWindow.OnPromptRecentStatusButtonClick);
	    target.AddButtonClickListener(PromptRelationshipButton,mWindow.OnPromptRelationshipButtonClick);
	    target.AddButtonClickListener(PromptContactButton,mWindow.OnPromptContactButtonClick);
	    target.AddButtonClickListener(PromptViewMeButton,mWindow.OnPromptViewMeButtonClick);
	}
}
