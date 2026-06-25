using GamerFrameWork.UIFrameWork;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TempUIComponent : MonoBehaviour
{
	public WindowLayer windowLayer = WindowLayer.Top;
	public RectTransform designRoot;
	public Button backButton;
	public Button settingButton;
	public Button addButton;
	public Button divinationHistoryButton;
	public Image avatarImage;
	public Image tankCardImage;
	public Image grimReaperCardImage;
	public Image holyGrailCardImage;
	public TMP_Text titleText;
	public TMP_Text friendNameText;
	public TMP_Text friendSubtitleText;
	public TMP_Text birthdayValueText;
	public TMP_Text timeOfBirthValueText;
	public TMP_Text placeOfBirthValueText;

	public void InitComponent(WindowBase target)
	{
		target.Canvas.sortingOrder = (int)windowLayer;
		target.Layer = windowLayer;
		TempUI window = (TempUI)target;
		target.AddButtonClickListener(backButton, window.OnBackButtonClick);
		target.AddButtonClickListener(settingButton, window.OnSettingButtonClick);
		target.AddButtonClickListener(addButton, window.OnAddButtonClick);
		target.AddButtonClickListener(divinationHistoryButton, window.OnDivinationHistoryButtonClick);
	}
}
