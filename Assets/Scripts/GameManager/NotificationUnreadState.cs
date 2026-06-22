using System;
using UnityEngine;
using UnityEngine.UI;

public static class NotificationUnreadState
{
    private const string UnreadCountKey = "NotificationUnread_Count";
    private const string LastSourceKey = "NotificationUnread_LastSource";

    public static event Action Changed;

    public static int UnreadCount => PlayerPrefs.GetInt(UnreadCountKey, 0);
    public static bool HasUnread => UnreadCount > 0;

    public static void MarkUnread(string source)
    {
        int count = Mathf.Clamp(UnreadCount + 1, 1, 99);
        PlayerPrefs.SetInt(UnreadCountKey, count);
        PlayerPrefs.SetString(LastSourceKey, source ?? string.Empty);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void ClearUnread()
    {
        if (!HasUnread) return;

        PlayerPrefs.DeleteKey(UnreadCountKey);
        PlayerPrefs.DeleteKey(LastSourceKey);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}

public static class NotificationUnreadBadge
{
    private const string BadgeName = "NotificationUnreadBadge";

    public static void Attach(Button button)
    {
        if (button == null) return;

        NotificationUnreadBadgeBinder binder = button.GetComponent<NotificationUnreadBadgeBinder>();
        if (binder == null)
            binder = button.gameObject.AddComponent<NotificationUnreadBadgeBinder>();

        binder.TargetButton = button;
        Refresh(button);
    }

    public static void Refresh(Button button)
    {
        if (button == null) return;
        GameObject badge = EnsureBadge(button.transform);
        badge.SetActive(NotificationUnreadState.HasUnread);
    }

    private static GameObject EnsureBadge(Transform parent)
    {
        Transform existing = parent.Find(BadgeName);
        if (existing != null) return existing.gameObject;

        GameObject badge = new GameObject(BadgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        badge.transform.SetParent(parent, false);

        RectTransform rect = badge.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(18f, 18f);
        rect.anchoredPosition = new Vector2(-8f, -8f);

        Image image = badge.GetComponent<Image>();
        image.color = new Color(1f, 0.18f, 0.24f, 1f);
        image.raycastTarget = false;

        return badge;
    }
}

public class NotificationUnreadBadgeBinder : MonoBehaviour
{
    public Button TargetButton { get; set; }

    private void OnEnable()
    {
        NotificationUnreadState.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        NotificationUnreadState.Changed -= Refresh;
    }

    private void Refresh()
    {
        NotificationUnreadBadge.Refresh(TargetButton != null ? TargetButton : GetComponent<Button>());
    }
}
