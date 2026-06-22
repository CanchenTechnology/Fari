/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/21/2026 7:04:29 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class MemoryManageListUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button BackButton;
	public InputField MemorySearchInputInputField;
	public ToggleGroup CategoryTabsToggleGroup;
	public Toggle TabAllToggle;
	public Toggle TabTopicToggle;
	public Toggle TabPreferenceToggle;
	public Toggle TabEmotionToggle;
	public Toggle TabGrowthToggle;
	public ScrollRect MemoryListScrollScrollRect;
	public Button AddMemoryButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    MemoryManageListUI mWindow=(MemoryManageListUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddInputFieldListener(MemorySearchInputInputField,mWindow.OnMemorySearchInputInputChange,mWindow.OnMemorySearchInputInputEnd);
	  
	    target.AddToggleClickListener(TabAllToggle,mWindow.OnTabAllToggleChange);
	    target.AddToggleClickListener(TabTopicToggle,mWindow.OnTabTopicToggleChange);
	    target.AddToggleClickListener(TabPreferenceToggle,mWindow.OnTabPreferenceToggleChange);
	    target.AddToggleClickListener(TabEmotionToggle,mWindow.OnTabEmotionToggleChange);
	    target.AddToggleClickListener(TabGrowthToggle,mWindow.OnTabGrowthToggleChange);
	    target.AddButtonClickListener(AddMemoryButton,mWindow.OnAddMemoryButtonClick);
	}
}
