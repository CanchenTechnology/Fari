/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:2026/6/13 15:47:41
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class FeedbackUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button BackButton;
	public ToggleGroup TopTabsToggleGroup;
	public Toggle TabCommunityToggle;
	public Toggle TabChatToggle;
	public Transform CommunityViewTransform;
	public InputField SearchInputInputField;
	public Button PublishFeedbackButton;
	public ScrollRect CommunityScrollScrollRect;
	public Transform ChatViewTransform;
	public ScrollRect ChatScrollScrollRect;
	public Button TagBugButton;
	public Button TagFeatureButton;
	public Button TagRoleButton;
	public Button TagVIPButton;
	public InputField ChatInputInputField;
	public InputField ChatInputInputCaretInputField;
	public Button SendMessageButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    FeedbackUI mWindow=(FeedbackUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddToggleClickListener(TabCommunityToggle,mWindow.OnTabCommunityToggleChange);
	    target.AddToggleClickListener(TabChatToggle,mWindow.OnTabChatToggleChange);
	    target.AddInputFieldListener(SearchInputInputField,mWindow.OnSearchInputInputChange,mWindow.OnSearchInputInputEnd);
	    target.AddButtonClickListener(PublishFeedbackButton,mWindow.OnPublishFeedbackButtonClick);
	    target.AddButtonClickListener(TagBugButton,mWindow.OnTagBugButtonClick);
	    target.AddButtonClickListener(TagFeatureButton,mWindow.OnTagFeatureButtonClick);
	    target.AddButtonClickListener(TagRoleButton,mWindow.OnTagRoleButtonClick);
	    target.AddButtonClickListener(TagVIPButton,mWindow.OnTagVIPButtonClick);
	    target.AddInputFieldListener(ChatInputInputField,mWindow.OnChatInputInputChange,mWindow.OnChatInputInputEnd);
	    target.AddInputFieldListener(ChatInputInputCaretInputField,mWindow.OnChatInputInputCaretInputChange,mWindow.OnChatInputInputCaretInputEnd);
	    target.AddButtonClickListener(SendMessageButton,mWindow.OnSendMessageButtonClick);
	}
}
