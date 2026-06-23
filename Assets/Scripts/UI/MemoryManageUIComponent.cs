/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/21/2026 6:30:35 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class MemoryManageUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button backButton;
	public Button OpenPrivacySettingsButton;
	public TMP_Text TopicCountText;
	public Button TopicMetricButton;
	public TMP_Text PreferenceCountText;
	public Button PreferenceMetricButton;
	public TMP_Text EmotionCountText;
	public Button EmotionMetricButton;
	public TMP_Text GrowthCountText;
	public Button GrowthMetricButton;
	public Button viewAllButton;
	public ScrollRect MemoryOverviewScrollScrollRect;
	public Button ManageMemoryButton;
	public Button ClearAllMemoryButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    MemoryManageUI mWindow=(MemoryManageUI)target;
	    target.AddButtonClickListener(backButton,mWindow.OnbackButtonClick);
	    target.AddButtonClickListener(OpenPrivacySettingsButton,mWindow.OnOpenPrivacySettingsButtonClick);
	    target.AddButtonClickListener(TopicMetricButton,mWindow.OnTopicMetricButtonClick);
	    target.AddButtonClickListener(PreferenceMetricButton,mWindow.OnPreferenceMetricButtonClick);
	    target.AddButtonClickListener(EmotionMetricButton,mWindow.OnEmotionMetricButtonClick);
	    target.AddButtonClickListener(GrowthMetricButton,mWindow.OnGrowthMetricButtonClick);
	    target.AddButtonClickListener(viewAllButton,mWindow.OnviewAllButtonClick);
	    target.AddButtonClickListener(ManageMemoryButton,mWindow.OnManageMemoryButtonClick);
	    target.AddButtonClickListener(ClearAllMemoryButton,mWindow.OnClearAllMemoryButtonClick);
	}
}
