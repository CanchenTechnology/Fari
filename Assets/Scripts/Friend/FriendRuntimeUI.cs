using System;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using UnityEngine;
using UnityEngine.UI;

public class FriendRuntimeAction
{
    public string label;
    public Action callback;
    public bool destructive;

    public FriendRuntimeAction(string label, Action callback, bool destructive = false)
    {
        this.label = label;
        this.callback = callback;
        this.destructive = destructive;
    }
}

public static class FriendRuntimeDialog
{
    public static void ShowConfirm(Transform anchor, string title, string message, string confirmText, Action onConfirm)
    {
        ShowActionSheet(
            anchor,
            title,
            message,
            new FriendRuntimeAction(confirmText, onConfirm, true),
            new FriendRuntimeAction("取消", null));
    }

    public static void ShowActionSheet(Transform anchor, string title, string message, params FriendRuntimeAction[] actions)
    {
        Transform parent = ResolveOverlayParent(anchor);
        GameObject overlay = CreateOverlay(parent, "FriendRuntimeActionSheet");
        RectTransform root = overlay.GetComponent<RectTransform>();

        GameObject panel = CreatePanel(root, new Vector2(520f, 0f));
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 24);
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Text titleText = CreateText(panel.transform, title, 28, FontStyle.Bold, new Color(1f, 0.78f, 0.45f, 1f));
        titleText.alignment = TextAnchor.MiddleCenter;
        if (!string.IsNullOrWhiteSpace(message))
        {
            Text messageText = CreateText(panel.transform, message, 18, FontStyle.Normal, new Color(0.86f, 0.82f, 0.92f, 1f));
            messageText.alignment = TextAnchor.MiddleCenter;
        }

        if (actions != null)
        {
            foreach (FriendRuntimeAction action in actions)
            {
                if (action == null) continue;
                Button button = CreateButton(panel.transform, action.label, action.destructive);
                button.onClick.AddListener(() =>
                {
                    UnityEngine.Object.Destroy(overlay);
                    action.callback?.Invoke();
                });
            }
        }

        FitPanelHeight(panel.GetComponent<RectTransform>(), 110f + (actions?.Length ?? 0) * 58f + (string.IsNullOrWhiteSpace(message) ? 0f : 52f));
    }

    private static Transform ResolveOverlayParent(Transform anchor)
    {
        Canvas canvas = anchor != null ? anchor.GetComponentInParent<Canvas>() : null;
        return canvas != null ? canvas.transform : anchor;
    }

    private static GameObject CreateOverlay(Transform parent, string name)
    {
        GameObject overlay = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(parent, false);
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.62f);
        dim.raycastTarget = true;

        Button blocker = overlay.AddComponent<Button>();
        blocker.transition = Selectable.Transition.None;
        blocker.targetGraphic = dim;
        blocker.onClick.AddListener(() => UnityEngine.Object.Destroy(overlay));
        return overlay;
    }

    private static GameObject CreatePanel(RectTransform root, Vector2 size)
    {
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(root, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.12f, 0.08f, 0.16f, 0.98f);
        image.raycastTarget = true;
        return panel;
    }

    private static void FitPanelHeight(RectTransform panel, float height)
    {
        if (panel == null) return;
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Clamp(height, 240f, 640f));
    }

    private static Text CreateText(Transform parent, string value, int size, FontStyle style, Color color)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.text = value ?? string.Empty;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, Mathf.Max(size * 2.2f, 42f));
        return text;
    }

    private static Button CreateButton(Transform parent, string label, bool destructive)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 48f);

        Image image = go.GetComponent<Image>();
        image.color = destructive ? new Color(0.48f, 0.08f, 0.16f, 1f) : new Color(0.30f, 0.10f, 0.48f, 1f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(go.transform, label, 20, FontStyle.Bold, Color.white);
        text.alignment = TextAnchor.MiddleCenter;
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return button;
    }
}

public static class FriendOracleHistoryOverlay
{
    public static void Show(Transform anchor, string title, List<FriendProfileHistoryEntry> entries, string emptyText)
    {
        Transform parent = ResolveOverlayParent(anchor);
        GameObject overlay = CreateOverlay(parent);
        RectTransform root = overlay.GetComponent<RectTransform>();

        GameObject panel = CreatePanel(root);
        RectTransform panelRect = panel.GetComponent<RectTransform>();

        Text titleText = CreateText(panel.transform, title, 28, FontStyle.Bold, new Color(1f, 0.78f, 0.45f, 1f));
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -22f);
        titleRect.sizeDelta = new Vector2(-120f, 48f);
        titleText.alignment = TextAnchor.MiddleCenter;

        Button closeButton = CreateSmallButton(panel.transform, "×");
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-24f, -22f);
        closeButton.onClick.AddListener(() => UnityEngine.Object.Destroy(overlay));

        ScrollRect scrollRect = CreateScroll(panel.transform);
        RectTransform scrollRt = scrollRect.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(28f, 28f);
        scrollRt.offsetMax = new Vector2(-28f, -88f);

        List<FriendProfileHistoryEntry> safeEntries = entries ?? new List<FriendProfileHistoryEntry>();
        if (safeEntries.Count == 0)
        {
            CreateHistoryRow(scrollRect.content, FriendProfileHistoryEntry.Empty(emptyText, "完成更多占卜后，这里会继续累积记录。"));
        }
        else
        {
            foreach (FriendProfileHistoryEntry entry in safeEntries)
                CreateHistoryRow(scrollRect.content, entry);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        panelRect.SetAsLastSibling();
    }

    private static Transform ResolveOverlayParent(Transform anchor)
    {
        Canvas canvas = anchor != null ? anchor.GetComponentInParent<Canvas>() : null;
        return canvas != null ? canvas.transform : anchor;
    }

    private static GameObject CreateOverlay(Transform parent)
    {
        GameObject overlay = new GameObject("FriendOracleHistoryOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
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
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRt;
        scroll.content = contentRt;
        return scroll;
    }

    private static void CreateHistoryRow(Transform parent, FriendProfileHistoryEntry entry)
    {
        GameObject row = new GameObject("HistoryRow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        row.transform.SetParent(parent, false);
        RectTransform rect = row.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 132f);
        Image bg = row.GetComponent<Image>();
        bg.color = new Color(0.18f, 0.13f, 0.22f, 0.94f);

        VerticalLayoutGroup layout = row.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 12, 12);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        CreateText(row.transform, $"{entry.title}    {entry.date}", 19, FontStyle.Bold, new Color(1f, 0.82f, 0.50f, 1f));
        CreateText(row.transform, entry.content, 17, FontStyle.Normal, new Color(0.88f, 0.84f, 0.94f, 1f));
    }

    private static Button CreateSmallButton(Transform parent, string label)
    {
        GameObject go = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(54f, 54f);
        go.GetComponent<Image>().color = new Color(0.25f, 0.15f, 0.34f, 1f);

        Button button = go.GetComponent<Button>();
        Text text = CreateText(go.transform, label, 26, FontStyle.Bold, Color.white);
        text.alignment = TextAnchor.MiddleCenter;
        RectTransform textRt = text.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        return button;
    }

    private static Text CreateText(Transform parent, string value, int size, FontStyle style, Color color)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.text = value ?? string.Empty;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, Mathf.Max(size * 2.3f, 42f));
        return text;
    }
}

public static class RelationshipDivinationOverlay
{
    public static void StartForFriend(Transform anchor, FriendDataManager.FriendData friend)
    {
        if (friend == null)
        {
            ToastManager.ShowToast("好友资料不完整");
            return;
        }

        RelationshipDivinationFirestore service = RelationshipDivinationFirestore.Instance;
        if (service == null)
        {
            ToastManager.ShowToast("关系占卜服务初始化中，请稍后再试");
            return;
        }

        string friendName = string.IsNullOrWhiteSpace(friend.name) ? "好友" : friend.name.Trim();
        string question = $"我和 {friendName} 的关系接下来会如何发展？";
        ToastManager.ShowToast(friend.isVirtual ? "正在生成关系占卜" : $"正在邀请 {friendName} 进行关系占卜");
        service.CreateInvite(friend, question, record =>
        {
            if (record == null) return;
            Show(anchor, record, friend);
        });
    }

    public static void Show(Transform anchor, RelationshipDivinationRecord record, FriendDataManager.FriendData friend = null)
    {
        if (record == null) return;

        Transform parent = ResolveOverlayParent(anchor);
        GameObject overlay = CreateOverlay(parent);
        RectTransform root = overlay.GetComponent<RectTransform>();

        GameObject panel = CreatePanel(root);
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 26, 26);
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        string currentUid = GetCurrentUid();
        string otherName = GetOtherName(record, currentUid);

        Text title = CreateText(panel.transform, "双人关系占卜", 30, FontStyle.Bold, new Color(1f, 0.78f, 0.45f, 1f), 46f);
        title.alignment = TextAnchor.MiddleCenter;
        Text subtitle = CreateText(panel.transform, $"与 {otherName} 的三张关系牌", 20, FontStyle.Normal, new Color(0.86f, 0.82f, 0.92f, 1f), 34f);
        subtitle.alignment = TextAnchor.MiddleCenter;

        CreateText(panel.transform, record.GetStatusText(currentUid), 18, FontStyle.Normal, new Color(0.74f, 0.70f, 0.84f, 1f), 52f);
        CreateText(panel.transform, "隐私提示：你的私牌只对你可见；对方私牌只对对方可见；共同牌在双方完成后共同揭示。", 16, FontStyle.Normal, new Color(0.62f, 0.56f, 0.72f, 1f), 52f);

        CreateCardRow(panel.transform, record.InitiatorCard, record, currentUid);
        CreateCardRow(panel.transform, record.SharedCard, record, currentUid);
        CreateCardRow(panel.transform, record.ReceiverCard, record, currentUid);

        if (record.CanCurrentUserReveal(currentUid))
        {
            Button revealButton = CreateButton(panel.transform, record.IsCurrentUserReceiver(currentUid) ? "加入并翻开我的牌" : "翻开我的牌", false);
            revealButton.onClick.AddListener(() =>
            {
                RelationshipDivinationFirestore.Instance.RevealMyCard(record, updated =>
                {
                    UnityEngine.Object.Destroy(overlay);
                    if (updated != null) Show(anchor, updated, friend);
                });
            });
        }

        if (record.IsCompleted || record.isLocalOnly)
        {
            Button dialogButton = CreateButton(panel.transform, "进入对话解读", false);
            dialogButton.onClick.AddListener(() =>
            {
                UnityEngine.Object.Destroy(overlay);
                OpenDialogWithContext(record, friend);
            });
        }

        if (!record.isLocalOnly && !record.IsCompleted && !record.IsCancelled)
        {
            Button cancelButton = CreateButton(panel.transform, "取消邀请", true);
            cancelButton.onClick.AddListener(() =>
            {
                RelationshipDivinationFirestore.Instance.CancelInvite(record, success =>
                {
                    ToastManager.ShowToast(success ? "已取消关系占卜邀请" : "取消失败，请稍后再试");
                    UnityEngine.Object.Destroy(overlay);
                });
            });
        }

        Button closeButton = CreateButton(panel.transform, "关闭", false);
        closeButton.onClick.AddListener(() => UnityEngine.Object.Destroy(overlay));

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    }

    private static void CreateCardRow(Transform parent, RelationshipDivinationCard card, RelationshipDivinationRecord record, string currentUid)
    {
        if (card == null) return;

        GameObject row = new GameObject(card.positionKey, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        Image bg = row.GetComponent<Image>();
        bg.color = new Color(0.18f, 0.12f, 0.22f, 0.96f);

        VerticalLayoutGroup layout = row.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 10, 10);
        layout.spacing = 5f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        LayoutElement element = row.GetComponent<LayoutElement>();
        element.minHeight = 104f;
        element.flexibleWidth = 1f;

        bool visible = IsCardVisibleToCurrentUser(card, record, currentUid);
        string title = visible ? card.DisplayName : "牌面暂未揭示";
        string visibility = GetVisibilityText(card, record, currentUid);
        CreateText(row.transform, card.position, 19, FontStyle.Bold, new Color(1f, 0.82f, 0.50f, 1f), 28f);
        CreateText(row.transform, $"{title} · {visibility}", 17, FontStyle.Normal, visible ? Color.white : new Color(0.70f, 0.66f, 0.78f, 1f), 34f);
        CreateText(row.transform, BuildCardHint(card, visible), 15, FontStyle.Normal, new Color(0.70f, 0.64f, 0.78f, 1f), 28f);
    }

    private static bool IsCardVisibleToCurrentUser(RelationshipDivinationCard card, RelationshipDivinationRecord record, string currentUid)
    {
        if (card == null || record == null) return false;
        if (record.isLocalOnly) return true;
        if (card.visibleTo == "both") return record.IsCompleted;
        if (card.visibleTo == "initiator")
            return record.IsCurrentUserInitiator(currentUid) && record.initiatorRevealed;
        if (card.visibleTo == "receiver")
            return record.IsCurrentUserReceiver(currentUid) && record.receiverRevealed;
        return false;
    }

    private static string GetVisibilityText(RelationshipDivinationCard card, RelationshipDivinationRecord record, string currentUid)
    {
        if (card.visibleTo == "both") return "双方可见";
        if (card.visibleTo == "initiator")
            return record.IsCurrentUserInitiator(currentUid) || record.isLocalOnly ? "仅你可见" : "对方私牌";
        if (card.visibleTo == "receiver")
            return record.IsCurrentUserReceiver(currentUid) || record.isLocalOnly ? "仅你可见" : "对方私牌";
        return "关系牌";
    }

    private static string BuildCardHint(RelationshipDivinationCard card, bool visible)
    {
        if (visible)
        {
            if (card.visibleTo == "both")
                return "这张牌代表你们之间共同可讨论的关系现状与指引。";
            return "这张牌只描述你的主观感受、期待和可行动的部分。";
        }

        if (card.visibleTo == "both")
            return "等待双方完成翻牌后，共同牌会同时开放。";
        return "为了保护隐私，对方的私牌不会在你的视图中直接展示。";
    }

    private static void OpenDialogWithContext(RelationshipDivinationRecord record, FriendDataManager.FriendData friend)
    {
        FriendDataManager.FriendData targetFriend = friend ?? FindFriendForRecord(record);
        if (targetFriend == null)
        {
            targetFriend = new FriendDataManager.FriendData
            {
                firebaseUid = GetOtherUid(record, GetCurrentUid()),
                name = GetOtherName(record, GetCurrentUid()),
                info = "真实好友",
                relationship = "好友",
                isVirtual = false
            };
        }

        UIModule.Instance.GetWindow<NavigationUI>()?.OpenDialogUI();
        DialogUI dialog = UIModule.Instance.GetWindow<DialogUI>();
        dialog?.SendAtFriendsMessage(targetFriend, BuildDialogContext(record));
        ToastManager.ShowToast("已带入双人关系占卜上下文");
    }

    private static string BuildDialogContext(RelationshipDivinationRecord record)
    {
        string currentUid = GetCurrentUid();
        List<string> lines = new List<string>
        {
            "【双人关系占卜上下文】",
            $"问题：{record.question}",
            $"发起者：{record.initiatorName}",
            $"受邀者：{record.receiverName}",
            $"状态：{record.GetStatusText(currentUid)}",
            "隐私规则：只讨论当前用户可见的私牌；不要断言对方私牌内容。"
        };

        AddVisibleCardLine(lines, record.InitiatorCard, record, currentUid);
        AddVisibleCardLine(lines, record.SharedCard, record, currentUid);
        AddVisibleCardLine(lines, record.ReceiverCard, record, currentUid);
        return string.Join("\n", lines);
    }

    private static void AddVisibleCardLine(List<string> lines, RelationshipDivinationCard card, RelationshipDivinationRecord record, string currentUid)
    {
        if (card == null) return;
        if (IsCardVisibleToCurrentUser(card, record, currentUid))
            lines.Add($"{card.position}：{card.DisplayName}");
        else
            lines.Add($"{card.position}：未公开或不可见");
    }

    private static FriendDataManager.FriendData FindFriendForRecord(RelationshipDivinationRecord record)
    {
        if (record == null || FriendDataManager.Instance == null) return null;
        return FriendDataManager.Instance.FindRealFriendByFirebaseUid(GetOtherUid(record, GetCurrentUid()));
    }

    private static string GetOtherUid(RelationshipDivinationRecord record, string currentUid)
    {
        if (record == null) return "";
        return record.IsCurrentUserInitiator(currentUid) ? record.receiverUid : record.initiatorUid;
    }

    private static string GetOtherName(RelationshipDivinationRecord record, string currentUid)
    {
        if (record == null) return "好友";
        if (record.isLocalOnly) return string.IsNullOrWhiteSpace(record.receiverName) ? "创建好友" : record.receiverName;
        return record.IsCurrentUserInitiator(currentUid)
            ? (string.IsNullOrWhiteSpace(record.receiverName) ? "好友" : record.receiverName)
            : (string.IsNullOrWhiteSpace(record.initiatorName) ? "好友" : record.initiatorName);
    }

    private static string GetCurrentUid()
    {
        if (UserDataManager.Instance != null && !string.IsNullOrWhiteSpace(UserDataManager.Instance.FirebaseUid))
            return UserDataManager.Instance.FirebaseUid;
        return Firebase.Auth.FirebaseAuth.DefaultInstance?.CurrentUser?.UserId ?? "";
    }

    private static Transform ResolveOverlayParent(Transform anchor)
    {
        Canvas canvas = anchor != null ? anchor.GetComponentInParent<Canvas>() : null;
        return canvas != null ? canvas.transform : anchor;
    }

    private static GameObject CreateOverlay(Transform parent)
    {
        GameObject overlay = new GameObject("RelationshipDivinationOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(parent, false);
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = overlay.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.72f);
        image.raycastTarget = true;
        return overlay;
    }

    private static GameObject CreatePanel(RectTransform root)
    {
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ContentSizeFitter));
        panel.transform.SetParent(root, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 0.08f);
        rect.anchorMax = new Vector2(0.92f, 0.92f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.10f, 0.07f, 0.14f, 0.99f);
        image.raycastTarget = true;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        return panel;
    }

    private static Text CreateText(Transform parent, string value, int size, FontStyle style, Color color, float minHeight)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.text = value ?? string.Empty;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.alignment = TextAnchor.MiddleLeft;
        LayoutElement element = go.GetComponent<LayoutElement>();
        element.minHeight = minHeight;
        element.flexibleWidth = 1f;
        return text;
    }

    private static Button CreateButton(Transform parent, string label, bool destructive)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = destructive ? new Color(0.48f, 0.08f, 0.16f, 1f) : new Color(0.30f, 0.10f, 0.48f, 1f);

        LayoutElement element = go.GetComponent<LayoutElement>();
        element.minHeight = 52f;
        element.flexibleWidth = 1f;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(go.transform, label, 20, FontStyle.Bold, Color.white, 52f);
        text.alignment = TextAnchor.MiddleCenter;
        RectTransform textRt = text.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        return button;
    }
}
