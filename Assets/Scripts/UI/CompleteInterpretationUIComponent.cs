/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/16/2026 6:13:20 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
 *
 * [修复记录 2026-06-16]
 * - Add: LoadingOverlay GameObject（AI 请求时显示加载遮罩）
 * - BugFix: InitComponent 添加空引用保护
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class CompleteInterpretationUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Button exitButton;
	public Button SwitchOracleButton;
	public Image CardImageImage;
	public TMP_Text CardDescTextText;
	public TMP_Text CardNameTextText;
	public TMP_Text Tag1TextText;
	public TMP_Text Tag2TextText;
	public TMP_Text Tag3TextText;
	public TMP_Text MeaningSectionText;
	public TMP_Text ActionSectionText;
	public Button Topic1ItemButton;
	public TMP_Text TopicText1Text;
	public Button Topic2ItemButton;
	public TMP_Text TopicText2Text;
	public Button Topic3ItemButton;
	public TMP_Text TopicText3Text;
	public Button Topic4ItemButton;
	public TMP_Text TopicText4Text;
	public Button ContinueChatButton;

	public TMP_Text suitableText;
	public TMP_Text notSuitableText;

	public TMP_Text moodReminderText;


	public void InitComponent(WindowBase target)
	{
		//空引用保护
		if (target == null)
		{
			Debug.LogError("[CompleteInterpretationUIComponent] InitComponent: target is null");
			return;
		}

		//组件事件绑定
		target.Canvas.sortingOrder = (int)windowLayer;
		target.Layer = windowLayer;
		CompleteInterpretationUI mWindow=(CompleteInterpretationUI)target;
		target.AddButtonClickListener(exitButton,mWindow.OnExitButtonClick);
		target.AddButtonClickListener(SwitchOracleButton,mWindow.OnSwitchOracleButtonClick);
		target.AddButtonClickListener(Topic1ItemButton,mWindow.OnTopic1ItemButtonClick);
		target.AddButtonClickListener(Topic2ItemButton,mWindow.OnTopic2ItemButtonClick);
		target.AddButtonClickListener(Topic3ItemButton,mWindow.OnTopic3ItemButtonClick);
		target.AddButtonClickListener(Topic4ItemButton,mWindow.OnTopic4ItemButtonClick);
		target.AddButtonClickListener(ContinueChatButton,mWindow.OnContinueChatButtonClick);
	}
}
