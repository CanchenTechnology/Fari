/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/26/2026 11:35:03 AM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using System.Collections.Generic;

public class FriendPreviewUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button backButton;
	public Button settingButton;
	public Image AvatarImage;
	public TextMeshProUGUI FriendNameTextMeshProUGUI;
	public TextMeshProUGUI infoNameTextMeshProUGUI;
	public Button AddButton;
	public TextMeshProUGUI AddLabelText;
	public TextMeshProUGUI birthdayTextTextMeshProUGUI;
	public TextMeshProUGUI birthdayTimeTextTextMeshProUGUI;
	public TextMeshProUGUI birthdayPlaceTextTextMeshProUGUI;

	[Header("占卜历史")]
	public List<DivinationItem> divinationItems;
	public Button viewHistoryBtn;
	
	public Button inviteBtn; //邀请好友一起双人占卜


	[Header("朋友设置界面")]
	public Transform friendSettingPanel;

	public Transform BtnContainer;
	public Button editFriendBtn;
	public Button deleteBtn;
	public Button exitSettingBtn;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    FriendPreviewUI mWindow=(FriendPreviewUI)target;
	    target.AddButtonClickListener(backButton,mWindow.OnbackButtonClick);
	    target.AddButtonClickListener(settingButton,mWindow.OnsettingButtonClick);
	    target.AddButtonClickListener(AddButton,mWindow.OnAddButtonClick);
	    target.AddButtonClickListener(viewHistoryBtn,mWindow.OnViewHistoryBtnClick);
	    target.AddButtonClickListener(inviteBtn,mWindow.OnInviteButtonClick);
	    target.AddButtonClickListener(editFriendBtn,mWindow.OnEditFriendButtonClick);
	    target.AddButtonClickListener(deleteBtn,mWindow.OnDeleteFriendButtonClick);
	    target.AddButtonClickListener(exitSettingBtn,mWindow.OnExitSettingButtonClick);
	}
}
