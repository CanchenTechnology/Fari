/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/9/2026 1:02:15 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using SuperScrollView;

public class DialogUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public Image chatBGImage;
	public Button switchDivinerButton;
	public Text NameText;
	public Text desText;
	public Image headImage;
	public Button questionButton;
	public Button sendButton;
	public InputField questionInputField;

	public LoopListView2 ChatScrollViewLoopListView2;
	public RectTransform artFriendRectTransfrom;
	public Text artFriendNameText;
	public Button cancelArtButton;

	public Transform QuickDivinationPanelTransform;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    DialogUI mWindow=(DialogUI)target;
	    target.AddButtonClickListener(switchDivinerButton,mWindow.OnswitchDivinerButtonClick);
	    target.AddButtonClickListener(questionButton,mWindow.OnquestionButtonClick);
	    target.AddButtonClickListener(sendButton,mWindow.OnsendButtonClick);
	    target.AddButtonClickListener(cancelArtButton,mWindow.OncancelArtButtonClick);
	    target.AddInputFieldListener(questionInputField,mWindow.OnquestionInputChange,mWindow.OnquestionInputEnd);
	}
}
