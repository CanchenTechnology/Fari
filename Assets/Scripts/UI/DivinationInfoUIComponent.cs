/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/18/2026 11:56:46 AM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class DivinationInfoUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public TarotItem Card1TarotItemTarotItem;
	public TarotItem Card2TarotItemTarotItem;
	public TarotItem Card3TarotItemTarotItem;
	public DivinationInfoItem Item1DivinationInfoItemDivinationInfoItem;
	public DivinationInfoItem Item2DivinationInfoItemDivinationInfoItem;
	public DivinationInfoItem Item3DivinationInfoItemDivinationInfoItem;
	public Text JudgeContentText;
	public Text AdviceContentText;
	public QuestRowItem Question1QuestRowItem;
	public QuestRowItem Question2QuestRowItem;
	public QuestRowItem Question3QuestRowItem;
	public Button ContinueChatButton;
	public Button BackButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    DivinationInfoUI mWindow=(DivinationInfoUI)target;
	    target.AddButtonClickListener(ContinueChatButton,mWindow.OnContinueChatButtonClick);
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	}
}
