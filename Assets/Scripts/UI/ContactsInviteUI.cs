/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 12:07:45
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class ContactsInviteUI : WindowBase
{
	public ContactsInviteUIComponent uiComponent;
	private Button[] contactInviteButtons;

	private readonly MockContact[] mockContacts =
	{
		new MockContact("Alice", "138 **** 8888"),
		new MockContact("Bob", "139 **** 6666"),
		new MockContact("Cindy", "137 **** 1234"),
		new MockContact("David", "136 **** 4321"),
		new MockContact("Emma", "138 **** 1010"),
		new MockContact("Frank", "137 **** 5678")
	};

	private struct MockContact
	{
		public string name;
		public string phone;

		public MockContact(string name, string phone)
		{
			this.name = name;
			this.phone = phone;
		}
	}

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<ContactsInviteUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		BindContactInviteButtons();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnNotificationsButtonClick()
	{
		UIModule.Instance.PopUpWindow<NotionUI>();
	}
	public void OnBottomNavToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnTabOracleToggleChange(bool state, Toggle toggle)
	{
		if (!state) return;
		HideWindow();
		UIModule.Instance.PopUpWindow<TodayOracleUI>();
	}
	public void OnTabChatToggleChange(bool state, Toggle toggle)
	{
		if (!state) return;
		HideWindow();
		UIModule.Instance.PopUpWindow<DialogUI>();
	}
	public void OnTabFriendsToggleChange(bool state, Toggle toggle)
	{
		if (!state) return;
		HideWindow();
		NavigationUI navigation = UIModule.Instance.PopUpWindow<NavigationUI>();
		if (navigation != null)
		{
			navigation.OpenFriendUI();
			return;
		}
		UIModule.Instance.PopUpWindow<FriendUI>();
	}
	public void OnTabProfileToggleChange(bool state, Toggle toggle)
	{
		if (!state) return;
		HideWindow();
		UIModule.Instance.PopUpWindow<MyUI>();
	}

	private void BindContactInviteButtons()
	{
		var buttons = gameObject.GetComponentsInChildren<Button>(true);
		var list = new System.Collections.Generic.List<Button>();

		foreach (var button in buttons)
		{
			if (button == null || button == uiComponent.BackButton || button == uiComponent.NotificationsButton)
			{
				continue;
			}

			string buttonName = button.gameObject.name.ToLowerInvariant();
			if (buttonName.Contains("invite") || buttonName.Contains("contact"))
			{
				list.Add(button);
			}
		}

		contactInviteButtons = list.ToArray();
		for (int i = 0; i < contactInviteButtons.Length && i < mockContacts.Length; i++)
		{
			int index = i;
			contactInviteButtons[i].onClick.RemoveAllListeners();
			contactInviteButtons[i].onClick.AddListener(() => InviteMockContact(index));
		}
	}

	private void InviteMockContact(int index)
	{
		if (index < 0 || index >= mockContacts.Length) return;

		var contact = mockContacts[index];
		Debug.Log($"[ContactsInviteUI] 准备邀请通讯录联系人：{contact.name} {contact.phone}");
		FriendInviteShareUtility.OpenSmsInvite();
	}
	#endregion
}
