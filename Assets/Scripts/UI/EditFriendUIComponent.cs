/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/19/2026 6:16:37 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using SuperScrollView;
using UltimateClean;

public class EditFriendUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button BackButton;
	public Image FriendAvatarImage;
	public Button EditAvatarButton;
	public TMP_InputField FriendNameInputField;
	public TMP_InputField FriendSignatureInputField;
	public Button ResetDefaultButton;
	public TMP_Text BirthdayTextText;
	public Button BirthDateButton;
	public TMP_Text BirthTimeTextText;
	public Button BirthTimeButton;
	public TMP_Text BirthCityTextText;
	public Button BirthCityButton;
	public Button MoreRecordsButton;
	public LoopListView2 OracleHistoryScrollViewLoopListView2;
	public Switch SwitchSwitch;
	public Button SaveChangesButton;
	public TMP_Text LastSavedTextText;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    EditFriendUI mWindow=(EditFriendUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(EditAvatarButton,mWindow.OnEditAvatarButtonClick);
	    target.AddInputFieldListener(FriendNameInputField,mWindow.OnFriendNameInputChange,mWindow.OnFriendNameInputEnd);
	    target.AddInputFieldListener(FriendSignatureInputField,mWindow.OnFriendSignatureInputChange,mWindow.OnFriendSignatureInputEnd);
	    target.AddButtonClickListener(ResetDefaultButton,mWindow.OnResetDefaultButtonClick);
	    target.AddButtonClickListener(BirthDateButton,mWindow.OnBirthDateButtonClick);
	    target.AddButtonClickListener(BirthTimeButton,mWindow.OnBirthTimeButtonClick);
	    target.AddButtonClickListener(BirthCityButton,mWindow.OnBirthCityButtonClick);
	    target.AddButtonClickListener(MoreRecordsButton,mWindow.OnMoreRecordsButtonClick);
	    target.AddButtonClickListener(SaveChangesButton,mWindow.OnSaveChangesButtonClick);
	    if (SwitchSwitch != null)
	    {
	        Button syncButton = SwitchSwitch.GetComponent<Button>();
	        if (syncButton != null)
	        {
	            syncButton.onClick.AddListener(mWindow.OnSyncSwitchClick);
	        }
	    }
	}
}
