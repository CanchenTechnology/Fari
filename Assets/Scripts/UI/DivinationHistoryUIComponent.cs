/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/21/2026 9:52:59 AM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class DivinationHistoryUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button BackButton;
	public ScrollRect RecordScrollContainerScrollRect;
	public TMP_Text MetaTypeValueText;
	public TMP_Text MetaTargetValueText;
	public TMP_Text MetaOracleValueText;
	public TMP_Text MetaTimeValueText;
	public TMP_Text MetaStatusValueText;
	public TMP_Text OracleTextText;
	public TMP_Text AdviceTextText;
	public TMP_Text SummaryTextText;
	public Button ContinueAskButton;
	public Button SaveToDiaryButton;
	public Button ShareResultButton;
	public Button DeleteRecordButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    DivinationHistoryUI mWindow=(DivinationHistoryUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(ContinueAskButton,mWindow.OnContinueAskButtonClick);
	    target.AddButtonClickListener(SaveToDiaryButton,mWindow.OnSaveToDiaryButtonClick);
	    target.AddButtonClickListener(ShareResultButton,mWindow.OnShareResultButtonClick);
	    target.AddButtonClickListener(DeleteRecordButton,mWindow.OnDeleteRecordButtonClick);
	}
}
