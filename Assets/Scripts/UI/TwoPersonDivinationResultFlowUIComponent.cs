/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/20/2026 10:32:32 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class TwoPersonDivinationResultFlowUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button BackButton;
	public Image MyCardImage;
	public Image FriendCardImage;
	public Image relationCardImage;
	public TMP_Text Card1DescText;
	public TMP_Text Card2DescText;
	public TMP_Text Card3DescText;
	public TMP_Text SummaryDescText;
	public Button ContinueChatButton;
	public Button SaveHistoryButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    TwoPersonDivinationResultFlowUI mWindow=(TwoPersonDivinationResultFlowUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(ContinueChatButton,mWindow.OnContinueChatButtonClick);
	    target.AddButtonClickListener(SaveHistoryButton,mWindow.OnSaveHistoryButtonClick);
	}
}
