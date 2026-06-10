using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GamerFrameWork.UIFrameWork
{
#if UNITY_EDITOR
    /// <summary>
    /// FriendsUI Prefab 一键生成工具
    /// 结构遵循 DialogUI.prefab 模式：UIMask + UIContent + Content(TopContent + BottomContent)
    /// 文本组件使用 Legacy Text（非 TMP）
    /// 在 Unity 菜单 GameObject → GamerFrameWork → UIFrame → 生成 FriendsUI 完整界面
    /// </summary>
    public class FriendsUIPrefabGenerator
    {
        private const string PREFAB_OUTPUT_PATH = "Assets/GameData/UI";
        private const float SCREEN_WIDTH = 750f;
        private const float SCREEN_HEIGHT = 1334f;

        // 主题色（深紫色风格）
        private static readonly Color BgDark = new Color(0.08f, 0.04f, 0.16f, 1f);
        private static readonly Color BgCard = new Color(0.14f, 0.09f, 0.26f, 1f);
        private static readonly Color BgLight = new Color(0.22f, 0.15f, 0.38f, 1f);
        private static readonly Color AccentPurple = new Color(0.55f, 0.35f, 0.85f, 1f);
        private static readonly Color AccentHover = new Color(0.45f, 0.28f, 0.75f, 1f);
        private static readonly Color Gold = new Color(0.96f, 0.73f, 0.05f, 1f);
        private static readonly Color WhiteText = new Color(0.95f, 0.93f, 0.98f, 1f);
        private static readonly Color GrayText = new Color(0.60f, 0.55f, 0.68f, 1f);
        private static readonly Color BadgeRed = new Color(0.88f, 0.18f, 0.32f, 1f);
        private static readonly Color MaskBlack = new Color(0f, 0f, 0f, 0.667f);
        // 聊天背景使用纯色，不依赖外部 sprite（DialogUI 使用了外部 sprite 作为背景图）
        // 实际项目中有 chatBG sprite 时可在 Inspector 中替换

        [MenuItem("GameObject/GamerFrameWork/UIFrame/生成 FriendsUI 完整界面", false, 20)]
        static void GenerateAllPrefabs()
        {
            if (!Directory.Exists(PREFAB_OUTPUT_PATH))
            {
                Directory.CreateDirectory(PREFAB_OUTPUT_PATH);
            }
            AssetDatabase.Refresh();

            CreateFriendsUIPrefab();
            CreateFriendsGroupItemPrefab();
            CreateFriendsChildItemPrefab();
            CreateFriendsActionButtonsItemPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("完成",
                "FriendsUI 及所有 Item Prefab 已生成到:\n" + PREFAB_OUTPUT_PATH +
                "\n\n已完成自动绑定:\n" +
                "  - titleText, notificationButton\n" +
                "  - notificationBadgeText, inviteNotificationGo\n" +
                "  - viewInviteButton, friendsContextGo, contextAddButton\n" +
                "\n下一步:\n" +
                "  1. 打开 FriendsUI.prefab\n" +
                "  2. 在 ScrollArea 下放入 LoopListView2 组件\n" +
                "  3. 在 LoopListView2 的 ItemPrefabDataList 中注册:\n" +
                "     FriendsGroupItem / FriendsChildItem / FriendsActionButtonsItem\n" +
                "  4. 将 LoopListView2 拖到 FriendsUIComponent.loopListView 字段",
                "确定");
        }

        // ==================== FriendsUI.prefab（遵循 DialogUI.prefab 结构） ====================

        static void CreateFriendsUIPrefab()
        {
            // ---- 根节点：FriendsUI ----
            GameObject root = NewGO("FriendsUI");
            root.layer = 5; // UI Layer
            SetupRect(root, SCREEN_WIDTH, SCREEN_HEIGHT);

            // Canvas (ScreenSpaceCamera, same as DialogUI)
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.sortingOrder = 100;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; // DialogUI uses Overlay mode 1

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(SCREEN_WIDTH, SCREEN_HEIGHT);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            // CanvasGroup（WindowBase 通过此管理可见性）
            var rootCanvasGroup = root.AddComponent<CanvasGroup>();
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;

            // FriendsUIComponent（绑定脚本）
            var component = root.AddComponent<FriendsUIComponent>();
            component.windowLayer = WindowLayer.MainUI;

            // ---- UIMask（半透明黑色遮罩） ----
            var maskGo = NewGO("UIMask", root.transform);
            Stretch(maskGo);
            maskGo.AddComponent<CanvasRenderer>();
            var maskImg = maskGo.AddComponent<Image>();
            maskImg.color = MaskBlack;
            maskImg.raycastTarget = false; // 匹配 DialogUI 的 m_RaycastTarget: 0
            var maskCg = maskGo.AddComponent<CanvasGroup>();
            maskCg.alpha = 1f;
            maskCg.interactable = true;
            maskCg.blocksRaycasts = true;

            // ---- UIContent（内容容器） ----
            var uiContent = NewGO("UIContent", root.transform);
            Stretch(uiContent);
            uiContent.AddComponent<CanvasRenderer>();

            // ---- [Image]chatBG（背景图，参考 DialogUI 的 chatBG） ----
            var chatBg = NewGO("[Image]chatBG", uiContent.transform);
            AnchorStretch(chatBg, 0, -196.17041f, 0, 98.085205f);
            // 注意：DialogUI 使用了外部 sprite，这里用纯色背景代替
            chatBg.AddComponent<CanvasRenderer>();
            var chatBgImg = chatBg.AddComponent<Image>();
            chatBgImg.color = BgDark;
            chatBgImg.raycastTarget = false;

            component.chatBGImage = chatBgImg;

            // ---- Content ----
            var contentGo = NewGO("Content", uiContent.transform);
            Stretch(contentGo, -98.085236f, 0, 0, 0); // DialogUI 的 Content 有 bottom offset
            contentGo.AddComponent<CanvasRenderer>();
            var contentImg = contentGo.AddComponent<Image>();
            contentImg.color = Color.clear;
            contentImg.raycastTarget = false;
            contentImg.enabled = false; // 匹配 DialogUI: m_Enabled: 0

            // ---- TopContent ----
            var topContent = NewGO("TopContent", contentGo.transform);
            AnchorPinTop(topContent, 127.3352f, -63.667572f);
            topContent.AddComponent<CanvasRenderer>();
            var topImg = topContent.AddComponent<Image>();
            topImg.color = Color.clear;
            topImg.raycastTarget = false;
            topImg.enabled = false;

            // Info 区（左侧：名字 + 描述；参考 DialogUI 的 Info）
            var infoArea = NewGO("Info", topContent.transform);
            SetupRectAnchor(infoArea, new Vector2(0, 1), new Vector2(0, 1), new Vector2(212, -58), new Vector2(348.69022f, 75));

            // [Text]Title — 大标题 "朋友"
            var titleTxtGo = NewGO("[Text]Title", infoArea.transform);
            var titleRt = SetupRectAnchor(titleTxtGo, new Vector2(0, 1), new Vector2(0, 1), new Vector2(207, -23), new Vector2(239.9608f, 36.8398f));
            titleTxtGo.AddComponent<CanvasRenderer>();
            var titleTxt = titleTxtGo.AddComponent<Text>();
            titleTxt.text = "朋友";
            titleTxt.font = GetDefaultFont();
            titleTxt.fontSize = 30;
            titleTxt.color = Gold;
            titleTxt.alignment = TextAnchor.MiddleLeft;
            titleTxt.raycastTarget = false;

            // [Text]des — 副标题描述
            var desGo = NewGO("[Text]des", infoArea.transform);
            SetupRectAnchor(desGo, new Vector2(0, 1), new Vector2(0, 1), new Vector2(222, -59.27403f), new Vector2(268.2055f, 32.0434f));
            desGo.AddComponent<CanvasRenderer>();
            var desTxt = desGo.AddComponent<Text>();
            desTxt.text = "Friends关系会成为下一次占卜的上下文";
            desTxt.font = GetDefaultFont();
            desTxt.fontSize = 15;
            desTxt.color = Gold;
            desTxt.alignment = TextAnchor.MiddleLeft;
            desTxt.raycastTarget = false;

            // [Button]Notification — 鈴鐺按鈕
            var notifBtnGo = NewGO("[Button]Notification", infoArea.transform);
            var notifBtnRt = SetupRectAnchor(notifBtnGo, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-106, -57), new Vector2(163.4353f, 61.026398f));
            notifBtnGo.AddComponent<CanvasRenderer>();
            var notifBtnImg = notifBtnGo.AddComponent<Image>();
            notifBtnImg.color = AccentPurple;
            notifBtnImg.raycastTarget = true;
            var notifBtn = notifBtnGo.AddComponent<Button>();
            notifBtn.transition = Selectable.Transition.ColorTint;
            var ncb = notifBtn.colors;
            ncb.normalColor = Color.white;
            ncb.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            ncb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            ncb.selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            ncb.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f);
            ncb.fadeDuration = 0.1f;
            notifBtn.colors = ncb;
            notifBtn.targetGraphic = notifBtnImg;

            // 鈴鐺文字
            var bellTextGo = NewGO("Text (Legacy)", notifBtnGo.transform);
            Stretch(bellTextGo);
            bellTextGo.AddComponent<CanvasRenderer>();
            var bellTxt = bellTextGo.AddComponent<Text>();
            bellTxt.text = "";
            bellTxt.font = GetDefaultFont();
            bellTxt.fontSize = 20;
            bellTxt.color = Gold;
            bellTxt.alignment = TextAnchor.MiddleCenter;
            bellTxt.raycastTarget = false;

            // Badge 红点
            var badgeGo = NewGO("[Image]BadgeBg", notifBtnGo.transform);
            var badgeRt = SetupRectAnchor(badgeGo, new Vector2(0.65f, 0.65f), new Vector2(1.15f, 1.15f), Vector2.zero, Vector2.zero);
            badgeGo.AddComponent<CanvasRenderer>();
            var badgeImg = badgeGo.AddComponent<Image>();
            badgeImg.color = BadgeRed;
            badgeImg.raycastTarget = false;

            var badgeTxtGo = NewGO("Text (Legacy)", badgeGo.transform);
            Stretch(badgeTxtGo);
            badgeTxtGo.AddComponent<CanvasRenderer>();
            var badgeTxt = badgeTxtGo.AddComponent<Text>();
            badgeTxt.text = "1";
            badgeTxt.font = GetDefaultFont();
            badgeTxt.fontSize = 12;
            badgeTxt.color = Color.white;
            badgeTxt.alignment = TextAnchor.MiddleCenter;
            badgeTxt.raycastTarget = false;

            // ---- BottomContent ----
            var bottomContent = NewGO("BottomContent", contentGo.transform);
            Stretch(bottomContent);
            bottomContent.AddComponent<CanvasRenderer>();

            // ChatScrollView（外层容器，我们在这里放 LoopListView2）
            var scrollViewGo = NewGO("FriendsScrollView", bottomContent.transform);
            SetupRectAnchor(scrollViewGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10.838f, -40), new Vector2(728.3181f, 790));
            scrollViewGo.AddComponent<CanvasRenderer>();
            var scrollImg = scrollViewGo.AddComponent<Image>();
            scrollImg.color = new Color(1, 1, 1, 0.03f);
            scrollImg.raycastTarget = false;
            scrollImg.enabled = false;

            // ScrollArea 占位提示（用户自行放入 LoopListView2）
            var hintGo = NewGO("[Text]ScrollHint", scrollViewGo.transform);
            Stretch(hintGo);
            hintGo.AddComponent<CanvasRenderer>();
            var hintTxt = hintGo.AddComponent<Text>();
            hintTxt.text = "← 将 LoopListView2 放入此处\n并在 ItemPrefabDataList 中注册:\nFriendsGroupItem / FriendsChildItem / FriendsActionButtonsItem";
            hintTxt.font = GetDefaultFont();
            hintTxt.fontSize = 16;
            hintTxt.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            hintTxt.alignment = TextAnchor.MiddleCenter;
            hintTxt.raycastTarget = false;

            // QuestContent 区域 — 邀请通知 + 上下文卡片（参考 DialogUI 的 QuestContent）
            var questContent = NewGO("QuestContent", bottomContent.transform);
            SetupRectAnchor(questContent, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 220), new Vector2(750, 440));
            questContent.AddComponent<CanvasRenderer>();
            var questImg = questContent.AddComponent<Image>();
            questImg.color = Color.clear;
            questImg.raycastTarget = false;
            questImg.enabled = false;

            // ---- 邀请通知卡片 ----
            var inviteCard = NewGO("InviteNotification", questContent.transform);
            SetupRectAnchor(inviteCard, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -160), new Vector2(0, 140));
            inviteCard.layer = 5;
            inviteCard.AddComponent<CanvasRenderer>();
            var inviteCardImg = inviteCard.AddComponent<Image>();
            inviteCardImg.color = BgLight;
            inviteCardImg.raycastTarget = false;
            var inviteVlg = inviteCard.AddComponent<VerticalLayoutGroup>();
            inviteVlg.padding = new RectOffset(25, 25, 12, 12);
            inviteVlg.spacing = 4;
            inviteVlg.childAlignment = TextAnchor.UpperLeft;
            inviteVlg.childForceExpandWidth = true;
            inviteVlg.childForceExpandHeight = false;
            inviteVlg.childControlWidth = true;

            // 邀请标题
            var invTitleGo = NewGO("Text (Legacy)", inviteCard.transform);
            invTitleGo.AddComponent<CanvasRenderer>();
            var invTitleTxt = invTitleGo.AddComponent<Text>();
            invTitleTxt.text = "你收到 1 个占卜邀请";
            invTitleTxt.font = GetDefaultFont();
            invTitleTxt.fontSize = 22;
            invTitleTxt.color = WhiteText;
            invTitleTxt.alignment = TextAnchor.MiddleLeft;
            invTitleTxt.raycastTarget = false;

            // 邀请描述
            var invDescGo = NewGO("Text (Legacy)", inviteCard.transform);
            invDescGo.AddComponent<CanvasRenderer>();
            var invDescTxt = invDescGo.AddComponent<Text>();
            invDescTxt.text = "来自好友的占卜请求";
            invDescTxt.font = GetDefaultFont();
            invDescTxt.fontSize = 14;
            invDescTxt.color = GrayText;
            invDescTxt.alignment = TextAnchor.MiddleLeft;
            invDescTxt.raycastTarget = false;

            // "查看" 按钮
            var viewBtnGo = NewGO("[Button]ViewInvite", inviteCard.transform);
            SetupRectAnchor(viewBtnGo, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-100, -50), new Vector2(100, 40));
            viewBtnGo.AddComponent<CanvasRenderer>();
            var viewBtnImg = viewBtnGo.AddComponent<Image>();
            viewBtnImg.color = AccentPurple;
            viewBtnImg.raycastTarget = true;
            var viewBtn = viewBtnGo.AddComponent<Button>();
            viewBtn.transition = Selectable.Transition.ColorTint;
            viewBtn.colors = ncb;
            viewBtn.targetGraphic = viewBtnImg;
            var viewBtnTxt = NewChildText(viewBtnGo, "查看 >", 16, Color.white);

            // ---- Friends 上下文卡片 ----
            var ctxCard = NewGO("FriendsContext", questContent.transform);
            SetupRectAnchor(ctxCard, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 60), new Vector2(0, 120));
            ctxCard.AddComponent<CanvasRenderer>();
            var ctxCardImg = ctxCard.AddComponent<Image>();
            ctxCardImg.color = BgLight;
            ctxCardImg.raycastTarget = false;
            var ctxVlg = ctxCard.AddComponent<VerticalLayoutGroup>();
            ctxVlg.padding = new RectOffset(25, 25, 12, 12);
            ctxVlg.spacing = 4;
            ctxVlg.childAlignment = TextAnchor.UpperLeft;
            ctxVlg.childForceExpandWidth = true;
            ctxVlg.childForceExpandHeight = false;

            var ctxTitleGo = NewGO("Text (Legacy)", ctxCard.transform);
            ctxTitleGo.AddComponent<CanvasRenderer>();
            var ctxTitleTxt = ctxTitleGo.AddComponent<Text>();
            ctxTitleTxt.text = "Relationship Context";
            ctxTitleTxt.font = GetDefaultFont();
            ctxTitleTxt.fontSize = 22;
            ctxTitleTxt.color = WhiteText;
            ctxTitleTxt.alignment = TextAnchor.MiddleLeft;
            ctxTitleTxt.raycastTarget = false;

            var ctxDescGo = NewGO("Text (Legacy)", ctxCard.transform);
            ctxDescGo.AddComponent<CanvasRenderer>();
            var ctxDescTxt = ctxDescGo.AddComponent<Text>();
            ctxDescTxt.text = "选择好友关系作为占卜上下文";
            ctxDescTxt.font = GetDefaultFont();
            ctxDescTxt.fontSize = 14;
            ctxDescTxt.color = GrayText;
            ctxDescTxt.alignment = TextAnchor.MiddleLeft;
            ctxDescTxt.raycastTarget = false;

            // "添加" 按钮
            var ctxAddBtnGo = NewGO("[Button]ContextAdd", ctxCard.transform);
            SetupRectAnchor(ctxAddBtnGo, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-90, 10), new Vector2(80, 36));
            ctxAddBtnGo.AddComponent<CanvasRenderer>();
            var ctxAddBtnImg = ctxAddBtnGo.AddComponent<Image>();
            ctxAddBtnImg.color = AccentPurple;
            ctxAddBtnImg.raycastTarget = true;
            var ctxAddBtn = ctxAddBtnGo.AddComponent<Button>();
            ctxAddBtn.transition = Selectable.Transition.ColorTint;
            ctxAddBtn.colors = ncb;
            ctxAddBtn.targetGraphic = ctxAddBtnImg;
            NewChildText(ctxAddBtnGo, "添加", 15, Color.white);

            // ============ 绑定 FriendsUIComponent 字段 ============
            component.titleText = titleTxt;
            component.notificationButton = notifBtn;
            component.notificationBadgeText = badgeTxt;
            component.inviteNotificationGo = inviteCard;
            component.viewInviteButton = viewBtn;
            component.friendsContextGo = ctxCard;
            component.contextAddButton = ctxAddBtn;
            // loopListView 留空 — 用户在编辑器中手动拖入

            SavePrefab(root, "FriendsUI");
        }

        // ==================== FriendsGroupItem.prefab ====================

        static void CreateFriendsGroupItemPrefab()
        {
            var root = NewGO("FriendsGroupItem");
            root.layer = 5;
            SetupRect(root, 700, 60);

            var bgImg = root.AddComponent<Image>();
            bgImg.color = BgCard;
            bgImg.raycastTarget = false;

            // 透明全尺寸点击按钮（放在最底层作为第一个 sibling）
            var btnGo = NewGO("[Button]HeaderButton", root.transform);
            Stretch(btnGo);
            btnGo.AddComponent<CanvasRenderer>();
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(1, 1, 1, 0.001f);
            btnImg.raycastTarget = true;
            var headerBtn = btnGo.AddComponent<Button>();
            btnGo.transform.SetAsFirstSibling();

            // HorizontalLayout
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(30, 25, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 12;
            hlg.childControlWidth = false;

            // 分组名
            var nameGo = NewGO("[Text]GroupName", root.transform);
            nameGo.AddComponent<CanvasRenderer>();
            var nameTxt = nameGo.AddComponent<Text>();
            nameTxt.text = "已有好友";
            nameTxt.font = GetDefaultFont();
            nameTxt.fontSize = 26;
            nameTxt.color = WhiteText;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.raycastTarget = false;
            nameGo.AddComponent<LayoutElement>().preferredWidth = 170;

            // 弹性空间
            NewFlexible(root.transform, "Spacer");

            // 数字
            var countGo = NewGO("[Text]Count", root.transform);
            countGo.AddComponent<CanvasRenderer>();
            var countTxt = countGo.AddComponent<Text>();
            countTxt.text = "2";
            countTxt.font = GetDefaultFont();
            countTxt.fontSize = 22;
            countTxt.color = GrayText;
            countTxt.alignment = TextAnchor.MiddleRight;
            countTxt.raycastTarget = false;
            countGo.AddComponent<LayoutElement>().preferredWidth = 40;

            // 箭头
            var arrowGo = NewGO("Arrow", root.transform);
            SetupSize(arrowGo, 30, 30);
            arrowGo.AddComponent<CanvasRenderer>();
            var arrowTxt = arrowGo.AddComponent<Text>();
            arrowTxt.text = "▼";
            arrowTxt.font = GetDefaultFont();
            arrowTxt.fontSize = 16;
            arrowTxt.color = WhiteText;
            arrowTxt.alignment = TextAnchor.MiddleCenter;
            arrowTxt.raycastTarget = false;

            // 脚本绑定
            var script = root.AddComponent<FriendsGroupItem>();
            script.groupNameText = nameTxt;
            script.countText = countTxt;
            script.arrowTransform = arrowGo.transform;
            script.headerButton = headerBtn;

            SavePrefab(root, "FriendsGroupItem");
        }

        // ==================== FriendsChildItem.prefab ====================

        static void CreateFriendsChildItemPrefab()
        {
            var root = NewGO("FriendsChildItem");
            root.layer = 5;
            SetupRect(root, 700, 100);

            var bgImg = root.AddComponent<Image>();
            bgImg.color = BgDark;
            bgImg.raycastTarget = false;

            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(45, 20, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 16;
            hlg.childControlWidth = false;

            // 头像
            var avatarGo = NewGO("[Image]Avatar", root.transform);
            avatarGo.AddComponent<CanvasRenderer>();
            var avatarImg = avatarGo.AddComponent<Image>();
            avatarImg.color = AccentPurple;
            avatarImg.raycastTarget = false;
            SetupSize(avatarGo, 62, 62);
            avatarGo.AddComponent<LayoutElement>().preferredWidth = 62;

            // 信息区
            var infoArea = NewVLayout("InfoArea", root.transform, 60, 4, TextAnchor.MiddleLeft);
            infoArea.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 22, 22);
            infoArea.AddComponent<LayoutElement>().preferredWidth = 340;

            // 名字
            var nameGo = NewGO("[Text]Name", infoArea.transform);
            nameGo.AddComponent<CanvasRenderer>();
            var nameTxt = nameGo.AddComponent<Text>();
            nameTxt.text = "Luna";
            nameTxt.font = GetDefaultFont();
            nameTxt.fontSize = 26;
            nameTxt.color = WhiteText;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.raycastTarget = false;

            // 详情行
            var detailRow = NewHLayout("DetailRow", infoArea.transform, 24, 8, TextAnchor.MiddleLeft);

            var typeGo = NewGO("[Text]Type", detailRow.transform);
            typeGo.AddComponent<CanvasRenderer>();
            var typeTxt = typeGo.AddComponent<Text>();
            typeTxt.text = "真实好友";
            typeTxt.font = GetDefaultFont();
            typeTxt.fontSize = 15;
            typeTxt.color = GrayText;
            typeTxt.alignment = TextAnchor.MiddleLeft;
            typeTxt.raycastTarget = false;

            var dotGo = NewGO("Text (Legacy)", detailRow.transform);
            dotGo.AddComponent<CanvasRenderer>();
            var dotTxt = dotGo.AddComponent<Text>();
            dotTxt.text = "·";
            dotTxt.font = GetDefaultFont();
            dotTxt.fontSize = 15;
            dotTxt.color = GrayText;
            dotTxt.alignment = TextAnchor.MiddleLeft;
            dotTxt.raycastTarget = false;

            var cityGo = NewGO("[Text]City", detailRow.transform);
            cityGo.AddComponent<CanvasRenderer>();
            var cityTxt = cityGo.AddComponent<Text>();
            cityTxt.text = "Los Angeles";
            cityTxt.font = GetDefaultFont();
            cityTxt.fontSize = 15;
            cityTxt.color = GrayText;
            cityTxt.alignment = TextAnchor.MiddleLeft;
            cityTxt.raycastTarget = false;

            // 弹性空间
            NewFlexible(root.transform, "Spacer");

            // @ 按钮
            var atBtnGo = NewGO("[Button]AtButton", root.transform);
            SetupSize(atBtnGo, 52, 52);
            atBtnGo.AddComponent<CanvasRenderer>();
            var atBtnImg = atBtnGo.AddComponent<Image>();
            atBtnImg.color = new Color(1, 1, 1, 0.05f);
            atBtnImg.raycastTarget = true;
            var atBtn = atBtnGo.AddComponent<Button>();
            var atTxt = NewChildText(atBtnGo, "@", 32, Gold, FontStyle.Bold);
            atTxt.alignment = TextAnchor.MiddleCenter;

            // 脚本绑定
            var script = root.AddComponent<FriendsChildItem>();
            script.avatarImage = avatarImg;
            script.nameText = nameTxt;
            script.typeText = typeTxt;
            script.cityText = cityTxt;
            script.atButton = atBtn;

            SavePrefab(root, "FriendsChildItem");
        }

        // ==================== FriendsActionButtonsItem.prefab ====================

        static void CreateFriendsActionButtonsItemPrefab()
        {
            var root = NewGO("FriendsActionButtonsItem");
            root.layer = 5;
            SetupRect(root, 700, 130);

            var bgImg = root.AddComponent<Image>();
            bgImg.color = Color.clear;

            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(30, 30, 30, 30);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 24;
            hlg.childControlWidth = false;

            // 添加好友按钮
            var addBtnGo = NewGO("[Button]AddFriend", root.transform);
            SetupSize(addBtnGo, 300, 70);
            addBtnGo.AddComponent<CanvasRenderer>();
            var addBtnImg = addBtnGo.AddComponent<Image>();
            addBtnImg.color = AccentPurple;
            addBtnImg.raycastTarget = true;
            var addBtn = addBtnGo.AddComponent<Button>();
            addBtn.transition = Selectable.Transition.ColorTint;
            var cb = addBtn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            cb.selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            cb.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.5f);
            cb.fadeDuration = 0.1f;
            addBtn.colors = cb;
            addBtn.targetGraphic = addBtnImg;
            addBtnGo.AddComponent<LayoutElement>().preferredWidth = 300;
            var addTxt = NewChildText(addBtnGo, "添加好友", 22, Color.white);
            addTxt.alignment = TextAnchor.MiddleCenter;
            Stretch(addTxt.gameObject);

            // 创建好友按钮
            var createBtnGo = NewGO("[Button]CreateFriend", root.transform);
            SetupSize(createBtnGo, 300, 70);
            createBtnGo.AddComponent<CanvasRenderer>();
            var createBtnImg = createBtnGo.AddComponent<Image>();
            createBtnImg.color = new Color(0.32f, 0.20f, 0.55f, 1f);
            createBtnImg.raycastTarget = true;
            var createBtn = createBtnGo.AddComponent<Button>();
            createBtn.transition = Selectable.Transition.ColorTint;
            createBtn.colors = cb;
            createBtn.targetGraphic = createBtnImg;
            createBtnGo.AddComponent<LayoutElement>().preferredWidth = 300;
            var createTxt = NewChildText(createBtnGo, "创建好友", 22, Color.white);
            createTxt.alignment = TextAnchor.MiddleCenter;
            Stretch(createTxt.gameObject);

            // 脚本绑定
            var script = root.AddComponent<FriendsActionButtonsItem>();
            script.addFriendButton = addBtn;
            script.createFriendButton = createBtn;
            script.addFriendText = addTxt;
            script.createFriendText = createTxt;

            SavePrefab(root, "FriendsActionButtonsItem");
        }

        // ==================== 工具方法 ====================

        static GameObject NewGO(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI Layer
            if (parent) go.transform.SetParent(parent, false);
            return go;
        }

        static void SetupRect(GameObject go, float w, float h)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        static void SetupSize(GameObject go, float w, float h)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
        }

        static RectTransform SetupRectAnchor(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        /// <summary>
        /// 全屏拉伸（父容器内铺满）
        /// </summary>
        static void Stretch(GameObject go, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>
        /// 顶部锚定（用于 TopContent，遵循 DialogUI 的写法）
        /// </summary>
        static void AnchorPinTop(GameObject go, float height, float yOffset)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, yOffset);
            rt.sizeDelta = new Vector2(0, height);
        }

        /// <summary>
        /// 拉伸锚定（anchored position 模式）
        /// </summary>
        static void AnchorStretch(GameObject go, float left, float right, float top, float bottom)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(0, (top + bottom) / 2f);
            rt.sizeDelta = new Vector2(left - right, -(top + bottom));
        }

        static GameObject NewHLayout(string name, Transform parent, float height,
            float spacing, TextAnchor align)
        {
            var go = NewGO(name, parent);
            SetupSize(go, 0, height);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = align;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = spacing;
            hlg.childControlWidth = false;
            return go;
        }

        static GameObject NewVLayout(string name, Transform parent, float height,
            float spacing, TextAnchor align)
        {
            var go = NewGO(name, parent);
            SetupSize(go, 0, height);
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = align;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = spacing;
            vlg.childControlWidth = true;
            return go;
        }

        static GameObject NewFlexible(Transform parent, string name)
        {
            var go = NewGO(name, parent);
            SetupSize(go, 10, 10);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            return go;
        }

        /// <summary>
        /// 创建 Legacy Text 子节点并返回 Text 组件
        /// </summary>
        static Text NewChildText(GameObject parent, string textContent, int fontSize, Color color,
            FontStyle style = FontStyle.Normal)
        {
            var go = NewGO("Text (Legacy)", parent.transform);
            Stretch(go);
            go.AddComponent<CanvasRenderer>();
            var txt = go.AddComponent<Text>();
            txt.text = textContent;
            txt.font = GetDefaultFont();
            txt.fontSize = fontSize;
            txt.color = color;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            return txt;
        }

        /// <summary>
        /// 获取系统默认字体（Arial）
        /// </summary>
        static Font GetDefaultFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static void SavePrefab(GameObject root, string name)
        {
            string path = $"{PREFAB_OUTPUT_PATH}/{name}.prefab";
            if (File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"[FriendsUI] ✅ 已创建 Prefab: {path}");
            Object.DestroyImmediate(root);
        }
    }
#endif
}
