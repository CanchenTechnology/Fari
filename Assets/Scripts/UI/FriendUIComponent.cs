/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/11/2026 11:54:12 AM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class FriendUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button NotionButton;
	public Transform InviteBannerTransform;
	public Button AddRelationButton;
	public Text ExistingCountText;
	public Button ExpandRealFriendButton;
	public Transform RealFriendContentTransform;
	public Text CreatedCountText;
	public Button ExpandVirtualFriendButton;
	public Transform VirtualFriendContentTransform;
	public Button AddFriendButton;
	public Button CreateFriendButton;

	public GameObject invitePrefab;
	public GameObject friendPrefab;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    FriendUI mWindow=(FriendUI)target;
	    target.AddButtonClickListener(NotionButton,mWindow.OnNotionButtonClick);
	    target.AddButtonClickListener(AddRelationButton,mWindow.OnAddRelationButtonClick);
	    target.AddButtonClickListener(ExpandRealFriendButton,mWindow.OnExpandRealFriendButtonClick);
	    target.AddButtonClickListener(ExpandVirtualFriendButton,mWindow.OnExpandVirtualFriendButtonClick);
	    target.AddButtonClickListener(AddFriendButton,mWindow.OnAddFriendButtonClick);
	    target.AddButtonClickListener(CreateFriendButton,mWindow.OnCreateFriendButtonClick);
	}
}
