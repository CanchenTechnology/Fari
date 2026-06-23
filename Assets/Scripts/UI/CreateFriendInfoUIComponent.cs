/*---------------------------------
 *Title:UI自动化组件生成代码生成工具
 *Author:GamerFrameWork-UIFrameWork
 *Date:6/19/2026 6:36:20 PM
 *Description:变量需要以[Text]括号加组件类型的格式进行声明，然后右键窗口物体—— 一键生成UI数据组件脚本即可
 *注意:以下文件是自动生成的，任何手动修改都会被下次生成覆盖,若手动修改后,尽量避免自动生成
---------------------------------*/
using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using SuperScrollView;
using UltimateClean;

public class CreateFriendInfoUIComponent:MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public Button BackButton;
	public Image FriendAvatarImage;
	public TMP_Text FriendNameText;
	public TMP_Text SignatureTextText;
	public TMP_Text birthdayDateText;
	public TMP_Text birthdayTimeText;
	public TMP_Text birthdayCityText;
	public TMP_Text userNameText;
	public Button MoreRecordsButton;
	public LoopListView2 OracleHistoryScrollViewLoopListView2;
	public Switch SwitchSwitch;
	public Button EditProfileButton;
	public void InitComponent(WindowBase target)
	{
	    //组件事件绑定
	    target.Canvas.sortingOrder = (int)windowLayer;
	    target.Layer = windowLayer;
	    CreateFriendInfoUI mWindow=(CreateFriendInfoUI)target;
	    target.AddButtonClickListener(BackButton,mWindow.OnBackButtonClick);
	    target.AddButtonClickListener(MoreRecordsButton,mWindow.OnMoreRecordsButtonClick);
	    target.AddButtonClickListener(EditProfileButton,mWindow.OnEditProfileButtonClick);
	    CreateFriendInfoRelationshipDivinationBinder.Bind(mWindow,this);
	    if (SwitchSwitch != null)
	    {
	        Button syncButton = SwitchSwitch.GetComponent<Button>();
	        if (syncButton != null)
	        {
	            syncButton.onClick.AddListener(mWindow.OnSyncSwitchClick);
	        }
	    }
	}
}

public static class CreateFriendInfoRelationshipDivinationBinder
{
	private const string ButtonName = "RelationshipDivinationButton";

	public static void Bind(CreateFriendInfoUI window, CreateFriendInfoUIComponent component)
	{
		if (window == null || component == null) return;

		Button existing = FindExistingButton(component);
		if (existing == null) return;
		existing.onClick.RemoveAllListeners();
		existing.gameObject.SetActive(false);
	}

	private static Button FindExistingButton(CreateFriendInfoUIComponent component)
	{
		Button[] buttons = component.GetComponentsInChildren<Button>(true);
		foreach (Button button in buttons)
		{
			if (button != null && button.gameObject.name == ButtonName)
				return button;
		}

		return null;
	}
}
