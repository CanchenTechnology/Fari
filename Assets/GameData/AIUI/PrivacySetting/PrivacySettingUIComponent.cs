using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PrivacySettingUIComponent : MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.MainUI;
	public RectTransform designRoot;
	public Button backButton;
	public Button privacyNoticeButton;
	public Button saveButton;
	public Button doneButton;
	public GameObject privacyNoticeOverlay;
	public ToggleGroup visibilityToggleGroup;
	public Toggle visibilityEveryoneToggle;
	public Toggle visibilityFriendsOnlyToggle;
	public Toggle visibilityOnlyMeToggle;
	public TMP_Text titleText;

	public void InitComponent(WindowBase target)
	{
		target.Canvas.sortingOrder = (int)windowLayer;
		target.Layer = windowLayer;

		PrivacySettingUI window = (PrivacySettingUI)target;
		target.AddButtonClickListener(backButton, window.OnBackButtonClick);
		target.AddButtonClickListener(privacyNoticeButton, window.OnPrivacyNoticeButtonClick);
		target.AddButtonClickListener(saveButton, window.OnSaveButtonClick);
		target.AddButtonClickListener(doneButton, window.OnDoneButtonClick);
		target.AddToggleClickListener(visibilityEveryoneToggle, window.OnVisibilityEveryoneToggleChange);
		target.AddToggleClickListener(visibilityFriendsOnlyToggle, window.OnVisibilityFriendsOnlyToggleChange);
		target.AddToggleClickListener(visibilityOnlyMeToggle, window.OnVisibilityOnlyMeToggleChange);
	}
}
