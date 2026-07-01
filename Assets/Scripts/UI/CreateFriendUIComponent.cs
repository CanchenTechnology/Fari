/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/18/2026 3:54:40 PM
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
	public TMP_Text titleText; 
	public Button UploadAvatarButton;
	public Image AvatarPreviewImage;
	public TMP_InputField InputInputField;
	public TMP_Text UsernameCountText;
	public Button Field_birthdayDateButton;
	public TMP_Text birthdayDateText;
	public Button Field_birthdayTimeButton;
	public TMP_Text birthdayTimeText;
	public Button Field_birthdayCountryButton;
	public TMP_Text birthdayCountryText;
	public Button SubmitButton;
	public TMP_Text SubmitButtonText;
	public TMP_Text privacyText;

	[Header("选择头像")]
	public GameObject chooseHeadPanel;

	public Button hideChooseHeadBtn;
	public Button aiGenerateBtn;
	public Button fromPhotoAlbumBtn;
	public Image headImage;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    CreateFriendUI mWindow=(CreateFriendUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(UploadAvatarButton,mWindow.OnUploadAvatarButtonClick);
	    target.AddInputFieldListener(InputInputField,mWindow.OnInputInputChange,mWindow.OnInputInputEnd);
	    target.AddButtonClickListener(Field_birthdayDateButton,mWindow.OnField_birthdayDateButtonClick);
	    target.AddButtonClickListener(Field_birthdayTimeButton,mWindow.OnField_birthdayTimeButtonClick);
	    target.AddButtonClickListener(Field_birthdayCountryButton,mWindow.OnField_birthdayCountryButtonClick);
	    target.AddButtonClickListener(SubmitButton,mWindow.OnSubmitButtonClick);
	    target.AddButtonClickListener(hideChooseHeadBtn,mWindow.OnHideChooseHeadButtonClick);
	    target.AddButtonClickListener(aiGenerateBtn,mWindow.OnAIGenerateButtonClick);
	    target.AddButtonClickListener(fromPhotoAlbumBtn,mWindow.OnFromPhotoAlbumButtonClick);
	}
}
