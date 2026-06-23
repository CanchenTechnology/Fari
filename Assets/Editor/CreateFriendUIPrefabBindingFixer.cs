using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CreateFriendUIPrefabBindingFixer
{
	private const string PrefabPath = "Assets/GameData/UI/Main/Friend/CreateFriendUI.prefab";

	[MenuItem("Tools/UI/Fix CreateFriendUI Bindings")]
	public static void FixCreateFriendUIBindings()
	{
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
		if (prefab == null)
		{
			Debug.LogError("CreateFriendUI prefab not found: " + PrefabPath);
			return;
		}

		GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
		if (instance == null)
		{
			Debug.LogError("Failed to instantiate CreateFriendUI prefab: " + PrefabPath);
			return;
		}

		CreateFriendUIComponent component = instance.GetComponent<CreateFriendUIComponent>();
		if (component == null)
		{
			Debug.LogError("CreateFriendUIComponent not found on prefab: " + PrefabPath);
			Object.DestroyImmediate(instance);
			return;
		}

		component.BackButton = FindComponent<Button>(instance.transform, "[Button]Back");
		component.UploadAvatarButton = FindComponent<Button>(instance.transform, "[Button]UploadAvatar");
		component.AvatarPreviewImage = FindComponent<Image>(instance.transform, "[Image]AvatarPreview");
		component.InputInputField = FindComponent<TMP_InputField>(instance.transform, "[InputField]UserNameInput");
		component.UsernameCountText = FindComponent<TMP_Text>(instance.transform, "[Text]UsernameCount");
		component.Field_birthdayDateButton = FindComponent<Button>(instance.transform, "[Button]Field_birthdayDate");
		component.birthdayDateText = FindComponent<TMP_Text>(instance.transform, "[Text]birthdayDate");
		component.Field_birthdayTimeButton = FindComponent<Button>(instance.transform, "[Button]Field_birthdayTime");
		component.birthdayTimeText = FindComponent<TMP_Text>(instance.transform, "[Text]birthdayTime");
		component.Field_birthdayCountryButton = FindComponent<Button>(instance.transform, "[Button]Field_birthdayCountry");
		component.birthdayCountryText = FindComponent<TMP_Text>(instance.transform, "[Text]birthdayCountry");
		component.SubmitButton = FindComponent<Button>(instance.transform, "[Button]Submit");

		ValidateBinding(component.BackButton, nameof(component.BackButton));
		ValidateBinding(component.UploadAvatarButton, nameof(component.UploadAvatarButton));
		ValidateBinding(component.AvatarPreviewImage, nameof(component.AvatarPreviewImage));
		ValidateBinding(component.InputInputField, nameof(component.InputInputField));
		ValidateBinding(component.UsernameCountText, nameof(component.UsernameCountText));
		ValidateBinding(component.Field_birthdayDateButton, nameof(component.Field_birthdayDateButton));
		ValidateBinding(component.birthdayDateText, nameof(component.birthdayDateText));
		ValidateBinding(component.Field_birthdayTimeButton, nameof(component.Field_birthdayTimeButton));
		ValidateBinding(component.birthdayTimeText, nameof(component.birthdayTimeText));
		ValidateBinding(component.Field_birthdayCountryButton, nameof(component.Field_birthdayCountryButton));
		ValidateBinding(component.birthdayCountryText, nameof(component.birthdayCountryText));
		ValidateBinding(component.SubmitButton, nameof(component.SubmitButton));

		PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
		Object.DestroyImmediate(instance);
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("CreateFriendUI bindings fixed: " + PrefabPath);
	}

	private static T FindComponent<T>(Transform root, string objectName) where T : Component
	{
		Transform child = FindDeepChild(root, objectName);
		return child != null ? child.GetComponent<T>() : null;
	}

	private static Transform FindDeepChild(Transform root, string objectName)
	{
		if (root.name == objectName) return root;
		foreach (Transform child in root)
		{
			Transform found = FindDeepChild(child, objectName);
			if (found != null) return found;
		}
		return null;
	}

	private static void ValidateBinding(Object value, string fieldName)
	{
		if (value == null)
		{
			Debug.LogWarning("CreateFriendUI binding missing: " + fieldName);
		}
	}
}
