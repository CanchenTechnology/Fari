/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/21/2026 11:12:56 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Globalization;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class PersonalProfileUI : WindowBase
{
	public PersonalProfileUIComponent uiComponent;

	private const string DefaultDisplayUserName = "Luna";
	private const string EmptyDisplayText = "未填写";
	private const int MaxBioLength = 80;
	private bool isAvatarUploading;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<PersonalProfileUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("PersonalProfileUI 缺少 UI 组件绑定脚本：PersonalProfileUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		EnsureAvatarClickHandler(uiComponent?.AvatarImageImage);
		EnsureAvatarClickHandler(uiComponent?.AvatarHeadImage);
		LoadDataToUI();
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

	private void LoadDataToUI()
	{
		UserDataManager manager = UserDataManager.Instance;
		if (manager == null || uiComponent == null) return;

		SetText(uiComponent.UserNameTitleText, string.IsNullOrWhiteSpace(manager.UserName)
			? DefaultDisplayUserName
			: manager.UserName.Trim());
		SetText(uiComponent.birthdayTextText, FormatBirthdayForDisplay(manager.Birthday));
		SetText(uiComponent.birthdayTimeText, FormatTimeForDisplay(manager.BirthTime));
		SetText(uiComponent.CityTextText, FormatOptional(manager.City));

		if (uiComponent.BioInputInputField != null)
			uiComponent.BioInputInputField.text = NormalizeBio(manager.ProfileBio);

		RefreshAvatar();
	}

	private void RefreshAvatar()
	{
		UserDataManager manager = UserDataManager.Instance;
		if (manager == null) return;

		if (!string.IsNullOrWhiteSpace(manager.PhotoUrl))
		{
			if (uiComponent == null)
			{
				ApplyAvatarFallback();
				return;
			}

			uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadCurrentUserAvatarCoroutine((sprite, _) =>
			{
				if (sprite != null)
					ApplyAvatar(sprite);
				else
					ApplyAvatarFallback();
			}));
			return;
		}

		ApplyAvatarFallback();
	}

	private void ApplyAvatar(Sprite sprite)
	{
		if (sprite == null)
		{
			ApplyAvatarFallback();
			return;
		}

		ApplyAvatarToImage(uiComponent?.AvatarImageImage, sprite);
		ApplyAvatarToImage(uiComponent?.AvatarHeadImage, sprite);
	}

	private void ApplyAvatarToImage(Image target, Sprite sprite)
	{
		if (target == null || sprite == null) return;
		target.sprite = sprite;
		target.preserveAspect = true;
		target.color = Color.white;
	}

	private void ApplyAvatarFallback()
	{
		SetFallbackColor(uiComponent?.AvatarImageImage, UserDataManager.Instance?.CurrentAvatar ?? AvatarType.Moon);
		SetFallbackColor(uiComponent?.AvatarHeadImage, UserDataManager.Instance?.CurrentAvatar ?? AvatarType.Moon);
	}

	private void SetFallbackColor(Image image, AvatarType avatarType)
	{
		if (image == null) return;
		if (image.sprite != null)
		{
			image.preserveAspect = true;
			return;
		}

		image.color = avatarType == AvatarType.Moon
			? new Color(0.18f, 0.08f, 0.25f, 1f)
			: new Color(0.11f, 0.10f, 0.18f, 1f);
	}

	private void EnsureAvatarClickHandler(Image image)
	{
		if (image == null) return;

		Button button = image.GetComponent<Button>();
		if (button == null)
		{
			button = image.gameObject.AddComponent<Button>();
			button.transition = Selectable.Transition.None;
		}

		button.onClick.RemoveListener(OnSetAvatarButtonClick);
		button.onClick.AddListener(OnSetAvatarButtonClick);
	}

	private void ApplyProfileInputsToUserData()
	{
		UserDataManager manager = UserDataManager.Instance;
		if (manager == null) return;

		string bio = uiComponent?.BioInputInputField != null
			? NormalizeBio(uiComponent.BioInputInputField.text)
			: manager.ProfileBio;
		string birthday = GetNormalizedBirthdayFromUI();
		string birthTime = GetNormalizedBirthTimeFromUI();
		string city = GetValueText(uiComponent?.CityTextText);

		manager.SetProfileBio(bio);
		manager.SetBirthday(birthday);
		manager.SetBirthTime(birthTime);
		manager.SetCity(IsEmptyDisplayValue(city) ? string.Empty : city.Trim());

		string currentTitle = GetValueText(uiComponent?.UserNameTitleText);
		if (!string.IsNullOrWhiteSpace(manager.UserName))
		{
			manager.SetUserName(manager.UserName.Trim());
		}
		else if (!IsEmptyDisplayValue(currentTitle) && currentTitle != DefaultDisplayUserName)
		{
			manager.SetUserName(currentTitle.Trim());
		}
	}

	private string GetNormalizedBirthdayFromUI()
	{
		string value = GetValueText(uiComponent?.birthdayTextText);
		if (TryNormalizeBirthday(value, out string normalized))
			return normalized;
		return string.Empty;
	}

	private string GetNormalizedBirthTimeFromUI()
	{
		string value = GetValueText(uiComponent?.birthdayTimeText);
		if (TryNormalizeBirthTime(value, out string normalized))
			return normalized;
		return string.Empty;
	}

	private void SaveProfile()
	{
		ApplyProfileInputsToUserData();

		UserDataManager manager = UserDataManager.Instance;
		if (manager == null)
		{
			ShowToast("用户资料服务暂不可用");
			return;
		}

		manager.SaveData();
		FirestoreManager.Instance?.SaveUserData(success =>
		{
			if (!success)
				Debug.LogWarning("[PersonalProfileUI] 用户资料云端同步失败");
		});

		ShowToast(manager.IsProfileComplete()
			? "保存成功！"
			: "资料已保存，但信息尚未填写完整");
	}

	private string NormalizeBio(string input)
	{
		string value = string.IsNullOrWhiteSpace(input)
			? string.Empty
			: input.Trim().Replace("\r", " ").Replace("\n", " ");

		while (value.Contains("  ", StringComparison.Ordinal))
			value = value.Replace("  ", " ");

		return value.Length > MaxBioLength ? value.Substring(0, MaxBioLength) : value;
	}

	private bool TryNormalizeBirthday(string input, out string normalized)
	{
		normalized = string.Empty;
		if (IsEmptyDisplayValue(input))
			return true;

		string value = input.Trim()
			.Replace("年", "-")
			.Replace("月", "-")
			.Replace("日", "")
			.Replace("/", "-")
			.Replace(".", "-");

		string[] formats =
		{
			"yyyy-M-d",
			"yyyy-MM-dd",
			"yyyy-M-dd",
			"yyyy-MM-d"
		};

		if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
			return false;

		if (date.Date > DateTime.Today || date.Year < 1900)
			return false;

		normalized = date.ToString("yyyy-MM-dd");
		return true;
	}

	private bool TryNormalizeBirthTime(string input, out string normalized)
	{
		normalized = string.Empty;
		if (IsEmptyDisplayValue(input))
			return true;

		string value = input.Trim()
			.Replace("点", ":")
			.Replace("时", ":")
			.Replace("分", "");

		if (value.EndsWith(":", StringComparison.Ordinal))
			value += "00";

		string[] formats =
		{
			"H:m",
			"H:mm",
			"HH:m",
			"HH:mm"
		};

		if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
			return false;

		normalized = time.ToString("HH:mm");
		return true;
	}

	private string FormatBirthdayForDisplay(string value)
	{
		if (!TryNormalizeBirthday(value, out string normalized) || string.IsNullOrEmpty(normalized))
			return EmptyDisplayText;

		return DateTime.TryParseExact(normalized, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
			? date.ToString("yyyy.MM.dd")
			: normalized.Replace("-", ".");
	}

	private string FormatTimeForDisplay(string value)
	{
		return TryNormalizeBirthTime(value, out string normalized) && !string.IsNullOrEmpty(normalized)
			? normalized
			: EmptyDisplayText;
	}

	private string FormatOptional(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? EmptyDisplayText : value.Trim();
	}

	private bool IsEmptyDisplayValue(string value)
	{
		return string.IsNullOrWhiteSpace(value) || value.Trim() == EmptyDisplayText;
	}

	private string GetValueText(Text text)
	{
		return text == null ? string.Empty : text.text;
	}

	private void SetText(Text text, string value)
	{
		if (text != null)
			text.text = value ?? string.Empty;
	}

	private void ShowToast(string message)
	{
		if (!string.IsNullOrWhiteSpace(message))
			ToastManager.ShowToast(message);
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}

	public void OnBioInputInputChange(string text)
	{
	}

	public void OnBioInputInputEnd(string text)
	{
		if (uiComponent?.BioInputInputField != null)
			uiComponent.BioInputInputField.text = NormalizeBio(text);
	}

	public void OnBirthdayArrowButtonClick()
	{
		string initial = GetNormalizedBirthdayFromUI();
		if (string.IsNullOrEmpty(initial))
			initial = UserDataManager.Instance?.Birthday ?? string.Empty;

		SpinPickerUI.ShowDate(initial, value =>
		{
			if (!TryNormalizeBirthday(value, out string normalized))
			{
				ShowToast("生日格式无效");
				return;
			}

			SetText(uiComponent?.birthdayTextText, FormatBirthdayForDisplay(normalized));
		});
	}

	public void OnTimeArrowButtonClick()
	{
		string initial = GetNormalizedBirthTimeFromUI();
		if (string.IsNullOrEmpty(initial))
			initial = UserDataManager.Instance?.BirthTime ?? string.Empty;

		SpinPickerUI.ShowTime(initial, value =>
		{
			if (!TryNormalizeBirthTime(value, out string normalized))
			{
				ShowToast("出生时间格式无效");
				return;
			}

			SetText(uiComponent?.birthdayTimeText, FormatTimeForDisplay(normalized));
		});
	}

	public void OnCityArrowButtonClick()
	{
		string initial = GetValueText(uiComponent?.CityTextText);
		if (IsEmptyDisplayValue(initial))
			initial = UserDataManager.Instance?.City ?? string.Empty;

		SpinPickerUI.ShowRegion(initial, value =>
		{
			SetText(uiComponent?.CityTextText, FormatOptional(value));
		});
	}

	public void OnSetAvatarButtonClick()
	{
		if (isAvatarUploading) return;

		AvatarUploadManager uploadManager = AvatarUploadManager.Instance;
		if (uploadManager == null)
		{
			ShowToast("头像上传服务暂不可用");
			return;
		}

		isAvatarUploading = true;
		ShowToast("正在上传头像...");
		uploadManager.PickAndUploadAvatar(
			result =>
			{
				isAvatarUploading = false;
				if (result != null && result.previewSprite != null)
					ApplyAvatar(result.previewSprite);
				ShowToast("头像已保存");
			},
			error =>
			{
				isAvatarUploading = false;
				if (!string.IsNullOrEmpty(error) && error != "已取消选择头像")
				{
					Debug.LogWarning("[PersonalProfileUI] 头像上传失败: " + error);
					ShowToast(error);
				}
			});
	}

	public void OnSaveBtnButtonClick()
	{
		SaveProfile();
	}
	#endregion
}
