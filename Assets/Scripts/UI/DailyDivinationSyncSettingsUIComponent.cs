/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/19/2026 3:48:07 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using UltimateClean;
public class DailyDivinationSyncSettingsUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public Switch SwitchSwitch;
	public ToggleGroup visibilityToggleGroup;
	public Toggle VisibilityAllFriendsToggle;
	public Toggle VisibilityRealFriendsToggle;
	public Toggle MeibilityOnlyMeToggle;
	public Button SaveSettingsButton;
	
	[Header("隐私策略")]
	public Button privacyBtn;
	public GameObject privacyNoticePanel;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    DailyDivinationSyncSettingsUI mWindow=(DailyDivinationSyncSettingsUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    if (SwitchSwitch != null)
	    {
	        target.AddButtonClickListener(SwitchSwitch.GetComponent<Button>(),mWindow.OnSyncToggleButtonClick);
	    }
	    target.AddToggleClickListener(VisibilityAllFriendsToggle,mWindow.OnVisibilityAllFriendsToggleChange);
	    target.AddToggleClickListener(VisibilityRealFriendsToggle,mWindow.OnVisibilityRealFriendsToggleChange);
	    target.AddToggleClickListener(MeibilityOnlyMeToggle,mWindow.OnMeibilityOnlyMeToggleChange);
	    target.AddButtonClickListener(SaveSettingsButton,mWindow.OnSaveSettingsButtonClick);
	}
}
