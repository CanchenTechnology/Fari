/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/19/2026 10:47:28 AM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using SuperScrollView;

public class UserSearchUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public Button NotificationsButton;
	public TMP_InputField SearchInputInputField;
	public Button SearchFriendsButton;

	public Button refreshBtn;

	public InviteItem item1;
	public InviteItem item2;
	public InviteItem item3;
	public LoopListView2 SearchFriendScrollViewLoopListView2;
	public GameObject mainCenterBody;
	public Transform searchCenterBody;
	public Transform recordSearchContent;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    UserSearchUI mWindow=(UserSearchUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(NotificationsButton,mWindow.OnNotificationsButtonClick);
	    target.AddInputFieldListener(SearchInputInputField,mWindow.OnSearchInputInputChange,mWindow.OnSearchInputInputEnd);
	    target.AddButtonClickListener(SearchFriendsButton,mWindow.OnSearchFriendsButtonClick);
	    target.AddButtonClickListener(refreshBtn,mWindow.OnRefreshRecommendationsButtonClick);
	}
}
