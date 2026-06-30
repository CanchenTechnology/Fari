/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/30/2026 1:27:07 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class DivinationDirectionUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;

	public Button exitBtn;
	public Image friendHead;
	public TMP_Text friendName;
	
	public TMP_Text infoText;  //在线状态

	public TMP_Dropdown directionDropDown;  //占卜方向

	public TMP_InputField userInput; //用户输入的问题

	public Button btn;  //真实好友显示“邀请”，虚拟好友显示“开始”。，注意如果输入框为空，按钮不激活
	public GameObject inviteSuccessPanel; //邀请成功界面 ,虚拟好友不显示

	public Button nextStepBtn; //邀请成功之后点击，进入下一个界面

	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    DivinationDirectionUI mWindow=(DivinationDirectionUI)target;
	    target.AddButtonClickListener(exitBtn,mWindow.OnExitButtonClick);
	    target.AddButtonClickListener(btn,mWindow.OnSubmitButtonClick);
	    target.AddButtonClickListener(nextStepBtn,mWindow.OnNextStepButtonClick);
	    if (directionDropDown != null)
	    {
	        directionDropDown.onValueChanged.RemoveListener(mWindow.OnDirectionDropDownChanged);
	        directionDropDown.onValueChanged.AddListener(mWindow.OnDirectionDropDownChanged);
	    }
	    if (userInput != null)
	    {
	        userInput.onValueChanged.RemoveListener(mWindow.OnUserInputValueChanged);
	        userInput.onValueChanged.AddListener(mWindow.OnUserInputValueChanged);
	    }
	}
}
