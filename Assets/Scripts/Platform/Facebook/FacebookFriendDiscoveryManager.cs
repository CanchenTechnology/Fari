using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using GamerFrameWork.UIFrameWork;
using TMPro;

public static class FacebookFriendDiscoveryManager
{
    private const int MaxFacebookFriends = 100;
    private const int MaxAppFriendResults = 30;

    public static void DiscoverAndShow(Transform anchor = null)
    {
        if (FacebookSignInHelper.Instance == null || !FacebookSignInHelper.Instance.IsSDKAvailable())
        {
            ToastManager.ShowToast("Facebook SDK 未就绪，已改用分享邀请");
            FriendInviteShareUtility.ShareFacebookInvite();
            return;
        }

        string accessToken = GetCurrentAccessToken();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            RequestFriendDiscoveryAccess(anchor, false);
            return;
        }

        FacebookFriendDiscoveryRunner.Run(anchor, accessToken, false);
    }

    private static void RequestFriendDiscoveryAccess(Transform anchor, bool retriedAfterGraphFailure)
    {
        ToastManager.ShowToast("正在请求 Facebook 好友权限");
        FacebookSignInHelper.Instance.RequestFriendDiscoveryAccess(
            accessToken =>
            {
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    ToastManager.ShowToast("Facebook 授权未返回有效凭证，已改用分享邀请");
                    FriendInviteShareUtility.ShareFacebookInvite();
                    return;
                }

                FacebookFriendDiscoveryRunner.Run(anchor, accessToken, retriedAfterGraphFailure);
            },
            error =>
            {
                Debug.LogWarning($"[FacebookFriendDiscoveryManager] Facebook 好友权限授权失败: {error}");
                ToastManager.ShowToast("Facebook 好友授权失败，已改用分享邀请");
                FriendInviteShareUtility.ShareFacebookInvite();
            });
    }

    private static string GetCurrentAccessToken()
    {
        try
        {
            Type accessTokenType = Type.GetType("Facebook.Unity.AccessToken, Facebook.Unity");
            object accessToken = accessTokenType?.GetProperty("CurrentAccessToken")?.GetValue(null);
            return accessToken?.GetType().GetProperty("TokenString")?.GetValue(accessToken) as string ?? string.Empty;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FacebookFriendDiscoveryManager] 读取 Facebook AccessToken 失败: {e.Message}");
            return string.Empty;
        }
    }

    private class FacebookFriendDiscoveryRunner : MonoBehaviour
    {
        private static FacebookFriendDiscoveryRunner instance;

        public static void Run(Transform anchor, string accessToken, bool retriedPermissionRequest)
        {
            if (instance == null)
            {
                GameObject go = new GameObject("FacebookFriendDiscoveryRunner");
                UnityEngine.Object.DontDestroyOnLoad(go);
                instance = go.AddComponent<FacebookFriendDiscoveryRunner>();
            }

            instance.StartCoroutine(instance.DiscoverCoroutine(anchor, accessToken, retriedPermissionRequest));
        }

        private IEnumerator DiscoverCoroutine(Transform anchor, string accessToken, bool retriedPermissionRequest)
        {
            ToastManager.ShowToast("正在发现 Facebook 好友");
            string encodedToken = Uri.EscapeDataString(accessToken);
            string url = $"https://graph.facebook.com/me/friends?fields=id,name&limit={MaxFacebookFriends}&access_token={encodedToken}";

            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[FacebookFriendDiscoveryManager] Graph API 失败: {request.error}, body={request.downloadHandler?.text}");
                if (!retriedPermissionRequest && IsPermissionError(request.downloadHandler?.text))
                {
                    FacebookFriendDiscoveryManager.RequestFriendDiscoveryAccess(anchor, true);
                    yield break;
                }

                ToastManager.ShowToast("Facebook 好友发现失败，已改用分享邀请");
                FriendInviteShareUtility.ShareFacebookInvite();
                yield break;
            }

            List<string> facebookIds = ParseFacebookFriendIds(request.downloadHandler.text);
            if (facebookIds.Count == 0)
            {
                ToastManager.ShowToast("还没有发现已授权本应用的 Facebook 好友");
                FriendInviteShareUtility.ShareFacebookInvite();
                yield break;
            }

            if (FirestoreManager.Instance == null || !FirestoreManager.Instance.IsInitialized)
            {
                ToastManager.ShowToast("好友服务未就绪，已改用分享邀请");
                FriendInviteShareUtility.ShareFacebookInvite();
                yield break;
            }

            FirestoreManager.Instance.FindPublicProfilesByFacebookIds(facebookIds, MaxAppFriendResults, appFriends =>
            {
                if (appFriends == null || appFriends.Count == 0)
                {
                    ToastManager.ShowToast("Facebook 好友中还没有可添加的 Moonly 用户");
                    FriendInviteShareUtility.ShareFacebookInvite();
                    return;
                }

                FacebookFriendDiscoveryOverlay.Show(anchor, appFriends);
            });
        }

        private static List<string> ParseFacebookFriendIds(string json)
        {
            List<string> ids = new List<string>();
            if (string.IsNullOrEmpty(json)) return ids;

            try
            {
                JObject root = JObject.Parse(json);
                JArray data = root["data"] as JArray;
                if (data == null) return ids;

                foreach (JToken item in data)
                {
                    string id = item.Value<string>("id");
                    if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
                        ids.Add(id.Trim());
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FacebookFriendDiscoveryManager] 解析 Facebook 好友失败: {e.Message}");
            }

            return ids;
        }

        private static bool IsPermissionError(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;
            string text = body.ToLowerInvariant();
            return text.Contains("permission")
                || text.Contains("permissions")
                || text.Contains("oauth")
                || text.Contains("access token");
        }
    }
}

public static class FacebookFriendDiscoveryOverlay
{
    public static void Show(Transform anchor, List<FirestoreManager.UserSearchResult> users)
    {
        Transform parent = ResolveOverlayParent(anchor);
        if (parent == null)
        {
            FriendInviteShareUtility.ShareFacebookInvite();
            return;
        }

        GameObject overlay = CreateOverlay(parent);
        RectTransform root = overlay.GetComponent<RectTransform>();
        GameObject panel = CreatePanel(root);

        TMP_Text title = CreateText(panel.transform, "Facebook 好友", 28, FontStyles.Bold, new Color(1f, 0.78f, 0.45f, 1f));
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

        TMP_Text hint = CreateText(panel.transform, "这些好友已授权本应用，可以直接发送 Firebase 好友请求。", 18, FontStyles.Normal, new Color(0.84f, 0.80f, 0.90f, 1f));
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

        foreach (FirestoreManager.UserSearchResult user in users ?? new List<FirestoreManager.UserSearchResult>())
            CreateUserRow(scroll.content, user);

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
        GameObject overlay = new GameObject("FacebookFriendDiscoveryOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
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

    private static void CreateUserRow(Transform parent, FirestoreManager.UserSearchResult user)
    {
        GameObject row = new GameObject("FacebookFriendRow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 76f;
        row.GetComponent<Image>().color = new Color(0.16f, 0.12f, 0.22f, 0.95f);

        TMP_Text name = CreateText(row.transform, string.IsNullOrWhiteSpace(user.displayName) ? "未命名用户" : user.displayName, 20, FontStyles.Bold, new Color(1f, 0.84f, 0.55f, 1f));
        RectTransform nameRt = name.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0.5f);
        nameRt.anchorMax = new Vector2(1f, 0.5f);
        nameRt.pivot = new Vector2(0f, 0.5f);
        nameRt.offsetMin = new Vector2(18f, -8f);
        nameRt.offsetMax = new Vector2(-132f, 32f);
        name.alignment = TextAlignmentOptions.Left;

        TMP_Text info = CreateText(row.transform, user.Info, 16, FontStyles.Normal, new Color(0.82f, 0.78f, 0.88f, 1f));
        RectTransform infoRt = info.GetComponent<RectTransform>();
        infoRt.anchorMin = new Vector2(0f, 0.5f);
        infoRt.anchorMax = new Vector2(1f, 0.5f);
        infoRt.pivot = new Vector2(0f, 0.5f);
        infoRt.offsetMin = new Vector2(18f, -32f);
        infoRt.offsetMax = new Vector2(-132f, 8f);
        info.alignment = TextAlignmentOptions.Left;

        Button button = CreateSmallButton(row.transform, GetButtonLabel(user));
        RectTransform buttonRt = button.GetComponent<RectTransform>();
        buttonRt.anchorMin = new Vector2(1f, 0.5f);
        buttonRt.anchorMax = new Vector2(1f, 0.5f);
        buttonRt.pivot = new Vector2(1f, 0.5f);
        buttonRt.sizeDelta = new Vector2(104f, 46f);
        buttonRt.anchoredPosition = new Vector2(-14f, 0f);
        button.interactable = CanInvite(user);
        button.onClick.AddListener(() =>
        {
            if (!CanInvite(user)) return;
            ToastManager.ShowToast($"正在发送给 {user.displayName}");
            FirestoreManager.Instance.SendFriendRequest(user, success =>
            {
                ToastManager.ShowToast(success ? "已发送好友请求" : "发送失败，请稍后再试");
                if (success)
                {
                    SetButtonText(button, "已发送");
                    button.interactable = false;
                }
            });
        });
    }

    private static string GetButtonLabel(FirestoreManager.UserSearchResult user)
    {
        if (user == null) return "添加";
        if (user.isSelf) return "自己";
        if (FriendDataManager.Instance != null && FriendDataManager.Instance.IsUserBlocked(user.uid)) return "已屏蔽";
        if (FriendDataManager.Instance != null && FriendDataManager.Instance.FindRealFriendByFirebaseUid(user.uid) != null) return "已添加";
        return "添加";
    }

    private static bool CanInvite(FirestoreManager.UserSearchResult user)
    {
        if (user == null || string.IsNullOrEmpty(user.uid) || user.isSelf) return false;
        if (FriendDataManager.Instance != null && FriendDataManager.Instance.IsUserBlocked(user.uid)) return false;
        if (FriendDataManager.Instance != null && FriendDataManager.Instance.FindRealFriendByFirebaseUid(user.uid) != null) return false;
        return FirestoreManager.Instance != null && FirestoreManager.Instance.IsInitialized;
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
        return text;
    }

    private static Button CreateSmallButton(Transform parent, string label)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.34f, 0.11f, 0.52f, 1f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText(go.transform, label, label.Length > 2 ? 17 : 24, FontStyles.Bold, Color.white);
        text.alignment = TextAlignmentOptions.Center;
        RectTransform textRt = text.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        return button;
    }

    private static void SetButtonText(Button button, string value)
    {
        TMP_Text text = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
        if (text != null) text.text = value;
    }
}
