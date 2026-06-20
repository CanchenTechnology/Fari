/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/19/2026 4:42:15 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using SuperScrollView;
using UltimateClean;

public class FriendProfileUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public Button MoreButton;
	public Image FriendAvatarImage;
	public Text FriendNameText;
	public Button AllRecordsButton;
	public LoopListView2 OracleHistoryScrollViewLoopListView2;
	public Switch SyncSwitchSwitch;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    FriendProfileUI mWindow=(FriendProfileUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(MoreButton,mWindow.OnMoreButtonClick);
	    target.AddButtonClickListener(AllRecordsButton,mWindow.OnAllRecordsButtonClick);
	    if (SyncSwitchSwitch != null)
	    {
	        Button syncButton = SyncSwitchSwitch.GetComponent<Button>();
	        if (syncButton != null)
	        {
	            syncButton.onClick.AddListener(mWindow.OnSyncSwitchClick);
	        }
	    }
	}
}
