/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/11/2026 1:53:22 PM
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

	// 头像精灵引用（需要在 Inspector 中配置或通过 Resource 加载）
	public Sprite moonAvatarSprite;
	public Sprite personAvatarSprite;
	private bool isAvatarUploading;
	private InputField _bioInputField;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<PersonalProfileUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		EnsureAvatarUploadClick();
		EnsureBioInputField();
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

	#region 数据加载与刷新

	/// <summary>
	/// 从 UserDataManager 加载数据到界面
	/// </summary>
	private void LoadDataToUI()
	{
		var manager = UserDataManager.Instance;

		// 加载文本数据
		uiComponent.UserNameInputInputField.text = manager.UserName;
		uiComponent.BirthdayInputInputField.text = manager.Birthday;
		uiComponent.TimeInputInputField.text = manager.BirthTime;
		uiComponent.CityInputInputField.text = manager.City;
		if (_bioInputField != null)
			_bioInputField.text = manager.ProfileBio;

		// 加载头像
		RefreshAvatar();
	}

	/// <summary>
	/// 刷新头像显示
	/// </summary>
	private void RefreshAvatar()
	{
		var manager = UserDataManager.Instance;
		Sprite sprite = null;

		if (!string.IsNullOrEmpty(manager.PhotoUrl) && GameManager.Instance != null)
		{
			GameManager.Instance.StartCoroutine(GoogleUserInfoHelper.LoadAndCacheAvatarCoroutine(
				manager.PhotoUrl,
				loadedSprite =>
				{
					if (loadedSprite != null && uiComponent.AvatarImageImage != null)
					{
						uiComponent.AvatarImageImage.sprite = loadedSprite;
						uiComponent.AvatarImageImage.preserveAspect = true;
					}
				}));
		}

		switch (manager.CurrentAvatar)
		{
			case AvatarType.Moon:
				sprite = moonAvatarSprite;
				break;
			case AvatarType.Person:
				sprite = personAvatarSprite;
				break;
		}

		if (sprite != null)
		{
			uiComponent.AvatarImageImage.sprite = sprite;
			uiComponent.AvatarImageImage.preserveAspect = true;
		}
	}

	private void EnsureAvatarUploadClick()
	{
		if (uiComponent == null || uiComponent.AvatarImageImage == null) return;

		Button button = uiComponent.AvatarImageImage.GetComponent<Button>();
		if (button == null)
		{
			button = uiComponent.AvatarImageImage.gameObject.AddComponent<Button>();
			button.transition = Selectable.Transition.None;
		}

		button.onClick.RemoveListener(OnAvatarImageClick);
		button.onClick.AddListener(OnAvatarImageClick);
	}

	private void EnsureBioInputField()
	{
		if (_bioInputField != null) return;

		_bioInputField = FindRuntimeBioInput();
		if (_bioInputField != null)
		{
			BindBioInput();
			return;
		}

		if (uiComponent == null || uiComponent.SaveBtnButton == null) return;
		Transform infoRoot = uiComponent.SaveBtnButton.transform.parent;
		if (infoRoot == null) return;

		GameObject row = new GameObject("RuntimeBioRow", typeof(RectTransform));
		row.transform.SetParent(infoRoot, false);
		var rowRect = row.GetComponent<RectTransform>();
		rowRect.anchorMin = new Vector2(0f, 0f);
		rowRect.anchorMax = new Vector2(0f, 0f);
		rowRect.pivot = new Vector2(0.5f, 0.5f);
		rowRect.sizeDelta = new Vector2(864f, 165.517f);
		row.transform.SetSiblingIndex(uiComponent.SaveBtnButton.transform.GetSiblingIndex());

		Text label = CreateRuntimeText(row.transform, "BioLabel", "个人简介", 30, FontStyle.Normal, new Color(0.9f, 0.84f, 1f, 1f));
		var labelRect = label.GetComponent<RectTransform>();
		labelRect.anchorMin = new Vector2(0f, 1f);
		labelRect.anchorMax = new Vector2(1f, 1f);
		labelRect.pivot = new Vector2(0f, 1f);
		labelRect.anchoredPosition = new Vector2(0f, 0f);
		labelRect.sizeDelta = new Vector2(0f, 56f);
		label.alignment = TextAnchor.MiddleLeft;

		GameObject inputGo = new GameObject("[InputField]BioInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
		inputGo.transform.SetParent(row.transform, false);
		var inputRect = inputGo.GetComponent<RectTransform>();
		inputRect.anchorMin = new Vector2(0.5f, 0.5f);
		inputRect.anchorMax = new Vector2(0.5f, 0.5f);
		inputRect.pivot = new Vector2(0.5f, 0.5f);
		inputRect.anchoredPosition = new Vector2(0f, -32.384f);
		inputRect.sizeDelta = new Vector2(864f, 100.75f);

		Image inputBg = inputGo.GetComponent<Image>();
		inputBg.color = new Color(0f, 0f, 0f, 1f);

		Text text = CreateRuntimeText(inputGo.transform, "Text", string.Empty, 28, FontStyle.Normal, Color.white);
		var textRect = text.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = new Vector2(28f, 8f);
		textRect.offsetMax = new Vector2(-28f, -8f);
		text.alignment = TextAnchor.MiddleLeft;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;

		Text placeholder = CreateRuntimeText(inputGo.transform, "Placeholder", "写一句给自己的简介或签名", 28, FontStyle.Italic, new Color(0.58f, 0.54f, 0.66f, 1f));
		var placeholderRect = placeholder.GetComponent<RectTransform>();
		placeholderRect.anchorMin = Vector2.zero;
		placeholderRect.anchorMax = Vector2.one;
		placeholderRect.offsetMin = new Vector2(28f, 8f);
		placeholderRect.offsetMax = new Vector2(-28f, -8f);
		placeholder.alignment = TextAnchor.MiddleLeft;
		placeholder.horizontalOverflow = HorizontalWrapMode.Wrap;

		_bioInputField = inputGo.GetComponent<InputField>();
		_bioInputField.targetGraphic = inputBg;
		_bioInputField.textComponent = text;
		_bioInputField.placeholder = placeholder;
		_bioInputField.characterLimit = 80;
		_bioInputField.lineType = InputField.LineType.SingleLine;
		BindBioInput();
	}

	private InputField FindRuntimeBioInput()
	{
		foreach (InputField input in gameObject.GetComponentsInChildren<InputField>(true))
		{
			if (input != null && input.gameObject.name.Contains("Bio"))
				return input;
		}

		return null;
	}

	private Text CreateRuntimeText(Transform parent, string name, string content, int fontSize, FontStyle style, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
		go.transform.SetParent(parent, false);
		Text text = go.GetComponent<Text>();
		text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		text.fontSize = fontSize;
		text.fontStyle = style;
		text.color = color;
		text.text = content ?? string.Empty;
		text.raycastTarget = false;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Truncate;
		return text;
	}

	private void BindBioInput()
	{
		if (_bioInputField == null) return;
		_bioInputField.onEndEdit.RemoveListener(OnBioInputEnd);
		_bioInputField.onEndEdit.AddListener(OnBioInputEnd);
	}

	#endregion

	#region API Function

	private bool TryApplyProfileInputs(out string validationMessage)
	{
		validationMessage = string.Empty;

		string userName = uiComponent.UserNameInputInputField != null
			? uiComponent.UserNameInputInputField.text.Trim()
			: UserDataManager.Instance.UserName;
		string birthday = uiComponent.BirthdayInputInputField != null
			? uiComponent.BirthdayInputInputField.text.Trim()
			: UserDataManager.Instance.Birthday;
		string birthTime = uiComponent.TimeInputInputField != null
			? uiComponent.TimeInputInputField.text.Trim()
			: UserDataManager.Instance.BirthTime;
		string city = uiComponent.CityInputInputField != null
			? uiComponent.CityInputInputField.text.Trim()
			: UserDataManager.Instance.City;
		string bio = _bioInputField != null
			? NormalizeBio(_bioInputField.text)
			: UserDataManager.Instance.ProfileBio;

		if (!TryNormalizeBirthday(birthday, out string normalizedBirthday))
		{
			validationMessage = "生日格式请填写为 1998-08-08";
			return false;
		}

		if (!TryNormalizeBirthTime(birthTime, out string normalizedBirthTime))
		{
			validationMessage = "出生时间格式请填写为 08:30";
			return false;
		}

		if (uiComponent.UserNameInputInputField != null)
			uiComponent.UserNameInputInputField.text = userName;
		if (uiComponent.BirthdayInputInputField != null)
			uiComponent.BirthdayInputInputField.text = normalizedBirthday;
		if (uiComponent.TimeInputInputField != null)
			uiComponent.TimeInputInputField.text = normalizedBirthTime;
		if (uiComponent.CityInputInputField != null)
			uiComponent.CityInputInputField.text = city;
		if (_bioInputField != null)
			_bioInputField.text = bio;

		UserDataManager.Instance.SetUserName(userName);
		UserDataManager.Instance.SetBirthday(normalizedBirthday);
		UserDataManager.Instance.SetBirthTime(normalizedBirthTime);
		UserDataManager.Instance.SetCity(city);
		UserDataManager.Instance.SetProfileBio(bio);
		return true;
	}

	private string NormalizeBio(string input)
	{
		string value = string.IsNullOrWhiteSpace(input)
			? string.Empty
			: input.Trim().Replace("\r", " ").Replace("\n", " ");

		while (value.Contains("  ", StringComparison.Ordinal))
			value = value.Replace("  ", " ");

		return value.Length > 80 ? value.Substring(0, 80) : value;
	}

	private bool TryNormalizeBirthday(string input, out string normalized)
	{
		normalized = string.Empty;
		if (string.IsNullOrWhiteSpace(input))
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
		if (string.IsNullOrWhiteSpace(input))
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

	private void NormalizeBirthdayField(bool showToast)
	{
		if (uiComponent.BirthdayInputInputField == null) return;

		string value = uiComponent.BirthdayInputInputField.text;
		if (TryNormalizeBirthday(value, out string normalized))
		{
			uiComponent.BirthdayInputInputField.text = normalized;
			return;
		}

		if (showToast)
			ToastManager.ShowToast("生日格式请填写为 1998-08-08");
	}

	private void NormalizeBirthTimeField(bool showToast)
	{
		if (uiComponent.TimeInputInputField == null) return;

		string value = uiComponent.TimeInputInputField.text;
		if (TryNormalizeBirthTime(value, out string normalized))
		{
			uiComponent.TimeInputInputField.text = normalized;
			return;
		}

		if (showToast)
			ToastManager.ShowToast("出生时间格式请填写为 08:30");
	}

	private void TrimTextField(InputField field)
	{
		if (field != null)
			field.text = field.text.Trim();
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		// 返回上一界面
		HideWindow();
		
	}
	public void OnMoonAvatarBtnButtonClick()
	{
		// 切换到月亮头像
		UserDataManager.Instance.SetAvatarType(AvatarType.Moon);
		RefreshAvatar();
	}
	public void OnPersonAvatarBtnButtonClick()
	{
		// 切换到人物头像
		UserDataManager.Instance.SetAvatarType(AvatarType.Person);
		RefreshAvatar();
	}
	public void OnAvatarImageClick()
	{
		if (isAvatarUploading) return;

		isAvatarUploading = true;
		ToastManager.ShowToast("正在上传头像...");
		AvatarUploadManager.Instance.PickAndUploadAvatar(
			result =>
			{
				isAvatarUploading = false;
				if (result != null && result.previewSprite != null && uiComponent.AvatarImageImage != null)
				{
					uiComponent.AvatarImageImage.sprite = result.previewSprite;
					uiComponent.AvatarImageImage.preserveAspect = true;
				}
				ToastManager.ShowToast("头像已保存");
			},
			error =>
			{
				isAvatarUploading = false;
				if (!string.IsNullOrEmpty(error) && error != "已取消选择头像")
				{
					Debug.LogWarning("[PersonalProfileUI] 头像上传失败: " + error);
					ToastManager.ShowToast(error);
				}
			});
	}
	public void OnUserNameInputInputChange(string text)
	{
	}
	public void OnUserNameInputInputEnd(string text)
	{
		TrimTextField(uiComponent.UserNameInputInputField);
	}
	public void OnBirthdayInputInputChange(string text)
	{
	}
	public void OnBirthdayInputInputEnd(string text)
	{
		NormalizeBirthdayField(true);
	}
	public void OnTimeInputInputChange(string text)
	{
	}
	public void OnTimeInputInputEnd(string text)
	{
		NormalizeBirthTimeField(true);
	}
	public void OnCityInputInputChange(string text)
	{
	}
	public void OnCityInputInputEnd(string text)
	{
		TrimTextField(uiComponent.CityInputInputField);
	}
	private void OnBioInputEnd(string text)
	{
		if (_bioInputField != null)
			_bioInputField.text = NormalizeBio(text);
	}
	public void OnSaveBtnButtonClick()
	{
		if (!TryApplyProfileInputs(out string validationMessage))
		{
			ToastManager.ShowToast(validationMessage);
			return;
		}

		// 保存数据到本地
		UserDataManager.Instance.SaveData();
		FirestoreManager.Instance?.SaveUserData(success =>
		{
			if (!success)
				Debug.LogWarning("[PersonalProfileUI] 用户资料云端同步失败");
		});

		// 可选：校验数据完整性
		if (UserDataManager.Instance.IsProfileComplete())
		{
			ToastManager.ShowToast("保存成功！");
		}
		else
		{
			ToastManager.ShowToast("资料已保存，但信息尚未填写完整");
		}
	}
	#endregion
}
