using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GamerFrameWork.UIFrameWork;
using TMPro;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
using TMPro;
#endif

public class NativeContactInviteRecord
{
    public string name;
    public string phone;

    public NativeContactInviteRecord(string name, string phone)
    {
        this.name = string.IsNullOrWhiteSpace(name) ? "未命名联系人" : name.Trim();
        this.phone = string.IsNullOrWhiteSpace(phone) ? string.Empty : phone.Trim();
    }
}

public static class NativeContactInviteManager
{
    private const int MaxContacts = 200;
    private const string AndroidReadContactsPermission = "android.permission.READ_CONTACTS";
#if UNITY_IOS && !UNITY_EDITOR
    private const string IosReceiverObjectName = "NativeContactInviteReceiver";

    [DllImport("__Internal")]
    private static extern void _fariPickContactForInvite(string gameObjectName, string successMethod, string cancelMethod);
#endif

    public static void OpenContactInvite(Transform anchor = null)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(AndroidReadContactsPermission))
        {
            PermissionCallbacks callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ =>
            {
                ToastManager.ShowToast("通讯录权限已开启");
                LoadAndShowContacts(anchor);
            };
            callbacks.PermissionDenied += _ =>
            {
                ToastManager.ShowToast("未获得通讯录权限，已改用短信邀请");
                FriendInviteShareUtility.OpenSmsInvite();
            };
            callbacks.PermissionDeniedAndDontAskAgain += _ =>
            {
                ToastManager.ShowToast("通讯录权限被拒绝，请在系统设置中开启");
                FriendInviteShareUtility.OpenSmsInvite();
            };

            Permission.RequestUserPermission(AndroidReadContactsPermission, callbacks);
            ToastManager.ShowToast("正在请求通讯录权限");
            return;
        }

        LoadAndShowContacts(anchor);
#elif UNITY_IOS && !UNITY_EDITOR
        NativeContactInviteCallbackReceiver.Ensure(
            contact =>
            {
                if (contact == null || string.IsNullOrWhiteSpace(contact.phone))
                {
                    ToastManager.ShowToast("未选择可邀请的联系人");
                    FriendInviteShareUtility.ShareInviteText("邀请好友");
                    return;
                }

                FriendInviteShareUtility.OpenSmsInvite(contact.phone);
            },
            () =>
            {
                ToastManager.ShowToast("已取消选择联系人");
            });
        _fariPickContactForInvite(
            IosReceiverObjectName,
            nameof(NativeContactInviteCallbackReceiver.OnNativeContactSelected),
            nameof(NativeContactInviteCallbackReceiver.OnNativeContactCancelled));
#else
        FriendInviteShareUtility.OpenSmsInvite();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void LoadAndShowContacts(Transform anchor)
    {
        List<NativeContactInviteRecord> contacts = AndroidContactReader.LoadContacts(MaxContacts);
        if (contacts.Count == 0)
        {
            ToastManager.ShowToast("没有读取到联系人，已改用短信邀请");
            FriendInviteShareUtility.OpenSmsInvite();
            return;
        }

        NativeContactInviteOverlay.Show(anchor, contacts);
    }

    private static class AndroidContactReader
    {
        public static List<NativeContactInviteRecord> LoadContacts(int maxContacts)
        {
            List<NativeContactInviteRecord> results = new List<NativeContactInviteRecord>();
            HashSet<string> seenPhones = new HashSet<string>();

            try
            {
                using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using AndroidJavaClass phoneClass = new AndroidJavaClass("android.provider.ContactsContract$CommonDataKinds$Phone");
                using AndroidJavaObject contentUri = phoneClass.GetStatic<AndroidJavaObject>("CONTENT_URI");

                string displayNameColumn = phoneClass.GetStatic<string>("DISPLAY_NAME");
                string numberColumn = phoneClass.GetStatic<string>("NUMBER");
                string[] projection = { displayNameColumn, numberColumn };

                using AndroidJavaObject cursor = resolver.Call<AndroidJavaObject>(
                    "query",
                    contentUri,
                    projection,
                    null,
                    null,
                    displayNameColumn + " ASC");

                if (cursor == null) return results;

                int nameIndex = cursor.Call<int>("getColumnIndex", displayNameColumn);
                int numberIndex = cursor.Call<int>("getColumnIndex", numberColumn);
                while (cursor.Call<bool>("moveToNext") && results.Count < maxContacts)
                {
                    string name = nameIndex >= 0 ? cursor.Call<string>("getString", nameIndex) : string.Empty;
                    string phone = numberIndex >= 0 ? cursor.Call<string>("getString", numberIndex) : string.Empty;
                    string normalizedPhone = NormalizePhone(phone);
                    if (string.IsNullOrEmpty(normalizedPhone) || seenPhones.Contains(normalizedPhone))
                        continue;

                    seenPhones.Add(normalizedPhone);
                    results.Add(new NativeContactInviteRecord(name, phone));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NativeContactInviteManager] 读取 Android 通讯录失败: {e.Message}");
            }

            return results;
        }

        private static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            string trimmed = phone.Trim();
            System.Text.StringBuilder builder = new System.Text.StringBuilder(trimmed.Length);
            foreach (char c in trimmed)
            {
                if (char.IsDigit(c) || c == '+')
                    builder.Append(c);
            }
            return builder.ToString();
        }
    }
#endif
}

public class NativeContactInviteCallbackReceiver : MonoBehaviour
{
    private static Action<NativeContactInviteRecord> selectedCallback;
    private static Action cancelledCallback;

    public static void Ensure(Action<NativeContactInviteRecord> onSelected, Action onCancelled)
    {
        selectedCallback = onSelected;
        cancelledCallback = onCancelled;

        GameObject receiver = GameObject.Find("NativeContactInviteReceiver");
        if (receiver == null)
        {
            receiver = new GameObject("NativeContactInviteReceiver");
            UnityEngine.Object.DontDestroyOnLoad(receiver);
        }

        if (receiver.GetComponent<NativeContactInviteCallbackReceiver>() == null)
            receiver.AddComponent<NativeContactInviteCallbackReceiver>();
    }

    public void OnNativeContactSelected(string payload)
    {
        NativeContactInviteRecord contact = ParseContactPayload(payload);
        selectedCallback?.Invoke(contact);
    }

    public void OnNativeContactCancelled(string _)
    {
        cancelledCallback?.Invoke();
    }

    private static NativeContactInviteRecord ParseContactPayload(string payload)
    {
        if (string.IsNullOrEmpty(payload))
            return new NativeContactInviteRecord("联系人", string.Empty);

        string[] parts = payload.Split(new[] { '|' }, 2);
        string name = parts.Length > 0 ? Uri.UnescapeDataString(parts[0]) : "联系人";
        string phone = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        return new NativeContactInviteRecord(name, phone);
    }
}

public static class NativeContactInviteOverlay
{
    public static void Show(Transform anchor, List<NativeContactInviteRecord> contacts)
    {
        Transform parent = ResolveOverlayParent(anchor);
        if (parent == null)
        {
            FriendInviteShareUtility.OpenSmsInvite();
            return;
        }

        GameObject overlay = CreateOverlay(parent);
        RectTransform root = overlay.GetComponent<RectTransform>();
        GameObject panel = CreatePanel(root);

        TMP_Text title = CreateText(panel.transform, "通讯录邀请", 28, FontStyles.Bold, new Color(1f, 0.78f, 0.45f, 1f));
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -22f);
        titleRect.sizeDelta = new Vector2(-120f, 48f);
        title.alignment = TextAlignmentOptions.Center;

        Button closeButton = CreateSmallButton(panel.transform, "×");
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-24f, -22f);
        closeButton.onClick.AddListener(() => UnityEngine.Object.Destroy(overlay));

        TMP_Text hint = CreateText(panel.transform, "选择联系人后会打开短信应用，邀请文案会自动带入。", 18, FontStyles.Normal, new Color(0.84f, 0.80f, 0.90f, 1f));
        RectTransform hintRect = hint.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0f, 1f);
        hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.pivot = new Vector2(0.5f, 1f);
        hintRect.anchoredPosition = new Vector2(0f, -72f);
        hintRect.sizeDelta = new Vector2(-60f, 42f);
        hint.alignment = TextAlignmentOptions.Center;

        ScrollRect scroll = CreateScroll(panel.transform);
        RectTransform scrollRt = scroll.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(28f, 28f);
        scrollRt.offsetMax = new Vector2(-28f, -128f);

        foreach (NativeContactInviteRecord contact in contacts ?? new List<NativeContactInviteRecord>())
        {
            CreateContactRow(scroll.content, contact, overlay);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
    }

    private static Transform ResolveOverlayParent(Transform anchor)
    {
        Canvas canvas = anchor != null ? anchor.GetComponentInParent<Canvas>() : null;
        if (canvas != null) return canvas.transform;

        try
        {
            return UIModule.Instance.UIRoot;
        }
        catch
        {
            return null;
        }
    }

    private static GameObject CreateOverlay(Transform parent)
    {
        GameObject overlay = new GameObject("NativeContactInviteOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(parent, false);
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = overlay.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.70f);
        image.raycastTarget = true;
        return overlay;
    }

    private static GameObject CreatePanel(RectTransform root)
    {
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(root, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.06f, 0.06f);
        rect.anchorMax = new Vector2(0.94f, 0.94f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.10f, 0.07f, 0.14f, 0.98f);
        image.raycastTarget = true;
        return panel;
    }

    private static ScrollRect CreateScroll(Transform parent)
    {
        GameObject scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(parent, false);
        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        RectTransform viewportRt = viewport.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 16);
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRt;
        scroll.content = contentRt;
        return scroll;
    }

    private static void CreateContactRow(Transform parent, NativeContactInviteRecord contact, GameObject overlay)
    {
        GameObject row = new GameObject("ContactRow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 74f;
        Image bg = row.GetComponent<Image>();
        bg.color = new Color(0.16f, 0.12f, 0.22f, 0.95f);

        TMP_Text name = CreateText(row.transform, contact.name, 20, FontStyles.Bold, new Color(1f, 0.84f, 0.55f, 1f));
        RectTransform nameRt = name.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0.5f);
        nameRt.anchorMax = new Vector2(1f, 0.5f);
        nameRt.pivot = new Vector2(0f, 0.5f);
        nameRt.offsetMin = new Vector2(18f, -10f);
        nameRt.offsetMax = new Vector2(-126f, 30f);
        name.alignment = TextAlignmentOptions.Left;

        TMP_Text phone = CreateText(row.transform, contact.phone, 16, FontStyles.Normal, new Color(0.82f, 0.78f, 0.88f, 1f));
        RectTransform phoneRt = phone.GetComponent<RectTransform>();
        phoneRt.anchorMin = new Vector2(0f, 0.5f);
        phoneRt.anchorMax = new Vector2(1f, 0.5f);
        phoneRt.pivot = new Vector2(0f, 0.5f);
        phoneRt.offsetMin = new Vector2(18f, -32f);
        phoneRt.offsetMax = new Vector2(-126f, 8f);
        phone.alignment = TextAlignmentOptions.Left;

        Button button = CreateSmallButton(row.transform, "邀请");
        RectTransform buttonRt = button.GetComponent<RectTransform>();
        buttonRt.anchorMin = new Vector2(1f, 0.5f);
        buttonRt.anchorMax = new Vector2(1f, 0.5f);
        buttonRt.pivot = new Vector2(1f, 0.5f);
        buttonRt.sizeDelta = new Vector2(96f, 46f);
        buttonRt.anchoredPosition = new Vector2(-14f, 0f);
        button.onClick.AddListener(() =>
        {
            UnityEngine.Object.Destroy(overlay);
            FriendInviteShareUtility.OpenSmsInvite(contact.phone);
        });
    }

    private static TMP_Text CreateText(Transform parent, string value, int size, FontStyles style, Color color)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.text = value ?? string.Empty;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, Mathf.Max(size * 2.1f, 36f));
        return text;
    }

    private static Button CreateSmallButton(Transform parent, string label)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(46f, 46f);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.34f, 0.11f, 0.52f, 1f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText(go.transform, label, label.Length > 2 ? 18 : 24, FontStyles.Bold, Color.white);
        text.alignment = TextAlignmentOptions.Center;
        RectTransform textRt = text.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        return button;
    }
}
