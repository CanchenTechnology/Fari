/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:2025/11/26 10:24:59
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class SelectWindowUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button CancelButton;
	public Text CancelText;
	public Button OKButton;
	public Text OKText;
	public Text ContentText;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    SelectWindow mWindow=(SelectWindow)target;
	    target.AddButtonClickListener(CancelButton,mWindow.OnCancelButtonClick);
	    target.AddButtonClickListener(OKButton,mWindow.OnOKButtonClick);
	}
}
