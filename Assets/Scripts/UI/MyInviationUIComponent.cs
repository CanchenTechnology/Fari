/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/30/2026 5:25:31 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class MyInviationUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button backButton;
	public Button initiateButton;
	public Button receivedButton;
	public LoopListView2 invitationListView;
	public UserInvitationItem itemTemplate;
	public Transform itemContentRoot;
	public TMP_Text emptyText;

	public void InitComponent(WindowBase target)
	{
		ResolveReferences();
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    MyInviationUI mWindow=(MyInviationUI)target;
	    if (backButton != null) target.AddButtonClickListener(backButton,mWindow.OnbackButtonClick);
	    if (initiateButton != null) target.AddButtonClickListener(initiateButton,mWindow.OnInitiateButtonClick);
	    if (receivedButton != null) target.AddButtonClickListener(receivedButton,mWindow.OnReceivedButtonClick);
	}

	public void ResolveReferences()
	{
		if (backButton == null) backButton = FindButtonByName("backButton", "BackButton", "[Button]Back", "[Button]back");
		if (initiateButton == null) initiateButton = FindButtonByName("InitiateBtn", "InitiateButton", "[Button]Initiate");
		if (receivedButton == null) receivedButton = FindButtonByName("ReceivedBtn", "ReceivedButton", "[Button]Received");
		if (invitationListView == null) invitationListView = GetComponentInChildren<LoopListView2>(true);
		if (itemTemplate == null) itemTemplate = GetComponentInChildren<UserInvitationItem>(true);
		if (itemContentRoot == null && itemTemplate != null) itemContentRoot = itemTemplate.transform.parent;
		if (itemContentRoot == null) itemContentRoot = FindTransformByName("Content");
		if (emptyText == null) emptyText = FindTextByName("EmptyText", "NoInvitationText", "NoDataText", "EmptyStateText");
	}

	private Button FindButtonByName(params string[] names)
	{
		Button[] buttons = GetComponentsInChildren<Button>(true);
		foreach (string targetName in names)
		{
			foreach (Button button in buttons)
			{
				if (button != null && button.name == targetName)
					return button;
			}
		}
		return null;
	}

	private Transform FindTransformByName(string targetName)
	{
		Transform[] transforms = GetComponentsInChildren<Transform>(true);
		foreach (Transform child in transforms)
		{
			if (child != null && child.name == targetName)
				return child;
		}
		return null;
	}

	private TMP_Text FindTextByName(params string[] names)
	{
		TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
		foreach (string targetName in names)
		{
			foreach (TMP_Text text in texts)
			{
				if (text != null && text.name == targetName)
					return text;
			}
		}
		return null;
	}
}
