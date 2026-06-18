using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FriendRelatedUIPrefabBuilder
{
	private static readonly Color Bg = new Color(0.006f, 0.005f, 0.018f, 1f);
	private static readonly Color Panel = new Color(0.075f, 0.062f, 0.115f, 0.94f);
	private static readonly Color PanelDeep = new Color(0.035f, 0.030f, 0.060f, 0.92f);
	private static readonly Color Field = new Color(0.130f, 0.095f, 0.170f, 0.86f);
	private static readonly Color Gold = new Color(0.88f, 0.64f, 0.33f, 1f);
	private static readonly Color SoftGold = new Color(1.00f, 0.84f, 0.55f, 1f);
	private static readonly Color TextMain = new Color(0.92f, 0.88f, 0.86f, 1f);
	private static readonly Color TextMuted = new Color(0.66f, 0.60f, 0.62f, 1f);
	private static readonly Color Purple = new Color(0.30f, 0.10f, 0.44f, 0.96f);
	private static readonly Color PurpleBright = new Color(0.74f, 0.30f, 1.00f, 1f);

	[MenuItem("Tools/UI/Build Friend Related Screens")]
	public static void BuildAll()
	{
		BuildEditFriendProfile();
		BuildDailyCardSyncSettings();
		BuildConfirmFriendInfo();
		BuildCreateFriendSuccess();
		BuildFriendDialogJump();
		BuildRelationshipTarotInvite();
		BuildRelationshipTarotReading();
		BuildRelationshipPermissionAllocation();
		BuildTarotInviteInbox();
		BuildRelationshipTarotResult();
		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("Friend related UI prefabs generated.");
	}

	[MenuItem("Tools/UI/Friend Screens/Edit Friend Profile")]
	public static void BuildEditFriendProfile()
	{
		SavePrefab("Assets/GameData/UI/Main/Friend/EditFriendProfileUI.prefab", root =>
		{
			BuildSharedBackground(root.transform, 0.72f);
			CreateCircleButton(root.transform, "Back", new Vector2(78, -86), "<", 72f, TextAnchor.MiddleCenter, true);
			CreateHeader(root.transform, "── ✦  编辑好友资料  ✦ ──", "你可以自由编辑好友的所有信息", -86f);

			GameObject top = CreatePanel("ProfileCard", root.transform, Panel);
			Rect(top, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -360), new Vector2(990, 335), new Vector2(0.5f, 0.5f));
			Frame(top.transform, new Vector2(990, 335), Gold, 1.8f);
			CreateText("AvatarLabel", top.transform, "头像", 31, SoftGold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -60), new Vector2(130, 50), FontStyle.Normal, new Vector2(0, 0.5f));
			CreateAvatar(top.transform, "Avatar", new Vector2(-285, -168), 245, PortraitA(), true);
			CreateSmallRound(top.transform, "EditAvatar", new Vector2(-180, -260), "✎", 62);
			CreateText("NameLabel", top.transform, "姓名", 30, SoftGold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(365, -60), new Vector2(160, 50), FontStyle.Normal, new Vector2(0, 0.5f));
			CreateValueBox(top.transform, new Vector2(210, -135), new Vector2(585, 84), "Nocturne Oracle", "✎", 33);
			CreateValueBox(top.transform, new Vector2(210, -245), new Vector2(585, 84), "夜色为引，真相自现 ✦", "✎", 29);

			GameObject basic = CreatePanel("BasicInfo", root.transform, PanelDeep);
			Rect(basic, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -675), new Vector2(990, 410), new Vector2(0.5f, 0.5f));
			Frame(basic.transform, new Vector2(990, 410), Gold, 1.6f);
			CreateSectionTitle(basic.transform, "✦ 基本信息", new Vector2(-405, 150));
			CreateGhostButton(basic.transform, new Vector2(370, 150), new Vector2(195, 54), "↶ 恢复默认", 24);
			CreateInfoRow(basic.transform, -15, "▣", "生日", "1993-11-23");
			CreateInfoRow(basic.transform, -115, "◷", "出生时间", "23:11");
			CreateInfoRow(basic.transform, -215, "⌖", "出生城市", "伊斯坦布尔（土耳其）");

			GameObject records = CreatePanel("Records", root.transform, PanelDeep);
			Rect(records, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1130), new Vector2(990, 460), new Vector2(0.5f, 0.5f));
			Frame(records.transform, new Vector2(990, 460), Gold, 1.6f);
			CreateSectionTitle(records.transform, "✦ 近期占卜记录", new Vector2(-390, 170));
			CreateText("More", records.transform, "查看更多  ›", 25, SoftGold, TextAnchor.MiddleRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-38, -58), new Vector2(190, 48), FontStyle.Normal, new Vector2(1, 0.5f));
			CreateRecord(records.transform, -5, "✦", "关于情感关系的未来走向", "塔罗三张牌 · 2025-05-17 21:36", "爱情");
			CreateRecord(records.transform, -126, "✦", "我的事业发展机会在哪里？", "星盘解读 · 2025-05-16 19:22", "事业");
			CreateRecord(records.transform, -247, "✦", "下个月需要注意什么？", "塔罗六张牌 · 2025-05-15 13:47", "综合");

			GameObject settings = CreatePanel("Settings", root.transform, PanelDeep);
			Rect(settings, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1525), new Vector2(990, 210), new Vector2(0.5f, 0.5f));
			Frame(settings.transform, new Vector2(990, 210), Gold, 1.6f);
			CreateSectionTitle(settings.transform, "✦ 设置", new Vector2(-425, 56));
			CreateText("SyncIcon", settings.transform, "↻", 43, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-410, -36), new Vector2(62, 62), FontStyle.Normal);
			CreateText("SyncTitle", settings.transform, "每日占卜自动同步到动态", 27, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-305, -18), new Vector2(455, 42), FontStyle.Normal, new Vector2(0, 0.5f));
			CreateText("SyncDesc", settings.transform, "开启后，Ta 的每日占卜结果将自动发布到动态", 22, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-305, -55), new Vector2(560, 34), FontStyle.Normal, new Vector2(0, 0.5f));
			CreateSwitch(settings.transform, new Vector2(380, -40), true);

			CreatePrimaryButton(root.transform, new Vector2(0, -1760), new Vector2(820, 82), "✦  保存修改  ✦");
			CreateText("LastSave", root.transform, "上次保存：刚刚", 22, TextMuted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1855), new Vector2(360, 40), FontStyle.Normal);
		});
	}

	[MenuItem("Tools/UI/Friend Screens/Daily Card Sync Settings")]
	public static void BuildDailyCardSyncSettings()
	{
		SavePrefab("Assets/GameData/UI/Main/My/DailyCardSyncSettingsUI.prefab", root =>
		{
			BuildSharedBackground(root.transform, 0.60f);
			CreateStatusBar(root.transform);
			CreateText("Back", root.transform, "<", 66, SoftGold, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(0, 1), new Vector2(58, -140), new Vector2(84, 84), FontStyle.Normal);
			CreateText("Info", root.transform, "ⓘ", 50, SoftGold, TextAnchor.MiddleCenter, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-72, -140), new Vector2(84, 84), FontStyle.Normal);
			CreateText("Title", root.transform, "每日占卜同步", 46, TextMain, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -132), new Vector2(520, 70), FontStyle.Bold);
			CreateImage("Crystal", root.transform, SpriteAt("Assets/GameData/Arts/images/asset-105.png"), new Color(1f, 1f, 1f, 0.95f), new Vector2(0, -265), new Vector2(220, 220), new Vector2(0.5f, 1), new Vector2(0.5f, 1));

			GameObject hero = CreatePanel("SyncCard", root.transform, PanelDeep);
			Rect(hero, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -505), new Vector2(1000, 260), new Vector2(0.5f, 0.5f));
			Frame(hero.transform, new Vector2(1000, 260), Gold, 1.5f);
			CreateText("SyncTitle", hero.transform, "自动同步我的每日占卜到动态", 38, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-455, 42), new Vector2(670, 58), FontStyle.Normal, new Vector2(0, 0.5f));
			CreateText("SyncDesc", hero.transform, "开启后，你的朋友可以看到你的每日占卜摘要。\n不会同步完整解读，除非你主动分享。", 29, TextMuted, TextAnchor.UpperLeft, Center(), Center(), new Vector2(-455, -48), new Vector2(700, 110), FontStyle.Normal, new Vector2(0, 0.5f));
			CreateSwitch(hero.transform, new Vector2(400, 34), true);

			CreateSeparatorTitle(root.transform, "✧ 可见范围", -740);
			GameObject range = CreatePanel("VisibleRange", root.transform, PanelDeep);
			Rect(range, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -950), new Vector2(1000, 470), new Vector2(0.5f, 0.5f));
			Frame(range.transform, new Vector2(1000, 470), Gold, 1.4f);
			CreateRadioRow(range.transform, 140, true, "♊", "所有好友", "所有好友都可以看到你的每日占卜摘要");
			CreateRadioRow(range.transform, 0, false, "♊", "仅真实好友", "只有你的真实好友可以看到");
			CreateRadioRow(range.transform, -140, false, "▣", "仅自己", "仅你自己可以看到，不会同步到动态");

			GameObject privacy = CreatePanel("Privacy", root.transform, PanelDeep);
			Rect(privacy, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1315), new Vector2(1000, 190), new Vector2(0.5f, 0.5f));
			Frame(privacy.transform, new Vector2(1000, 190), Gold, 1.4f);
			CreateText("Shield", privacy.transform, "♢", 54, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-420, 12), new Vector2(86, 86), FontStyle.Bold);
			CreateText("PrivacyTitle", privacy.transform, "隐私说明", 32, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-325, 48), new Vector2(260, 50), FontStyle.Bold, new Vector2(0, 0.5f));
			CreateText("PrivacyDesc", privacy.transform, "我们非常重视你的隐私。同步到动态的内容仅为占卜摘要，\n不会包含完整解读。你可以随时更改设置。", 27, TextMuted, TextAnchor.UpperLeft, Center(), Center(), new Vector2(-325, -28), new Vector2(760, 88), FontStyle.Normal, new Vector2(0, 0.5f));

			CreatePrimaryButton(root.transform, new Vector2(0, -1605), new Vector2(1000, 92), "保存设置");
			CreateBottomNav(root.transform, "朋友");
		});
	}

	[MenuItem("Tools/UI/Friend Screens/Confirm Friend Info")]
	public static void BuildConfirmFriendInfo()
	{
		SavePrefab("Assets/GameData/UI/Main/Friend/ConfirmFriendInfoUI.prefab", root =>
		{
			BuildSharedBackground(root.transform, 0.70f);
			CreateStatusBar(root.transform);
			CreateCircleButton(root.transform, "Back", new Vector2(84, -168), "<", 68, TextAnchor.MiddleCenter, true);
			CreateCircleButton(root.transform, "Help", new Vector2(-84, -168), "?", 52, TextAnchor.MiddleCenter, false);
			CreateHeader(root.transform, "── ✦  确认好友信息  ✦ ──", "", -168);

			GameObject card = CreatePanel("ConfirmCard", root.transform, Panel);
			Rect(card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -745), new Vector2(820, 930), new Vector2(0.5f, 0.5f));
			Frame(card.transform, new Vector2(820, 930), Gold, 1.7f);
			CreateAvatar(card.transform, "Avatar", new Vector2(0, 275), 172, PortraitB(), true);
			CreateText("Name", card.transform, "林晚星  ✎", 42, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 145), new Vector2(360, 60), FontStyle.Normal);
			GameObject info = CreatePanel("InfoRows", card.transform, new Color(0.11f, 0.09f, 0.15f, 0.70f));
			Rect(info, Center(), Center(), new Vector2(0, -95), new Vector2(730, 400), new Vector2(0.5f, 0.5f));
			Frame(info.transform, new Vector2(730, 400), new Color(Gold.r, Gold.g, Gold.b, 0.55f), 1.2f);
			CreateConfirmRow(info.transform, 118, "▣", "生日", "1996年07月18日");
			CreateConfirmRow(info.transform, 0, "◷", "出生时间", "14:35");
			CreateConfirmRow(info.transform, -118, "⌖", "出生城市", "上海市");
			CreateText("Divider", card.transform, "──────  ✦  ──────", 30, Gold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -365), new Vector2(650, 50), FontStyle.Normal);

			CreatePrimaryButton(root.transform, new Vector2(0, -1420), new Vector2(820, 105), "✦     创建好友     ✦");
			CreateGhostButton(root.transform, new Vector2(0, -1565), new Vector2(820, 92), "返回修改", 34);
			CreateText("Tip", root.transform, "──✦  这些信息将用于更准确的关系占卜与匹配。  ✦──", 25, TextMuted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1698), new Vector2(820, 50), FontStyle.Normal);
			CreateBottomNav(root.transform, "朋友");
		});
	}

	[MenuItem("Tools/UI/Friend Screens/Create Friend Success")]
	public static void BuildCreateFriendSuccess()
	{
		SavePrefab("Assets/GameData/UI/Main/Friend/CreateFriendSuccessUI.prefab", root =>
		{
			BuildSharedBackground(root.transform, 0.72f);
			CreateTopProfile(root.transform, "Nocturne Oracle", "夜色为引，真相自现 ✦");
			CreateText("Title", root.transform, "创建成功", 70, SoftGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -310), new Vector2(560, 96), FontStyle.Bold);

			GameObject card = CreatePanel("SuccessCard", root.transform, Panel);
			Rect(card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -820), new Vector2(820, 810), new Vector2(0.5f, 0.5f));
			Frame(card.transform, new Vector2(820, 810), Gold, 1.7f);
			CreateText("Star", card.transform, "✦", 54, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 328), new Vector2(80, 80), FontStyle.Bold);
			CreateAvatar(card.transform, "Avatar", new Vector2(0, 178), 250, PortraitC(), true);
			CreateSmallRound(card.transform, "Fav", new Vector2(132, 78), "★", 82);
			CreateText("FriendName", card.transform, "A.", 52, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -20), new Vector2(260, 70), FontStyle.Normal);
			CreateGhostButton(card.transform, new Vector2(0, -100), new Vector2(330, 62), "✦   已创建好友   ✦", 24);
			CreateText("Divider", card.transform, "────────  ✦  ────────", 28, Gold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -210), new Vector2(650, 40), FontStyle.Normal);
			CreateText("Desc", card.transform, "这个人已经进入你的星图。\n你可以现在向神谕师询问\n与 TA 有关的问题。", 36, TextMain, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -342), new Vector2(600, 180), FontStyle.Normal);

			CreatePrimaryButton(root.transform, new Vector2(0, -1435), new Vector2(720, 100), "＠  @ TA 进入对话");
			CreateGhostButton(root.transform, new Vector2(0, -1600), new Vector2(700, 92), "‹   返回朋友页", 34);
			CreateBottomNav(root.transform, "朋友");
		});
	}

	[MenuItem("Tools/UI/Friend Screens/Friend Dialog Jump")]
	public static void BuildFriendDialogJump()
	{
		SavePrefab("Assets/GameData/UI/Main/Friend/FriendDialogJumpUI.prefab", root =>
		{
			BuildSharedBackground(root.transform, 0.76f);
			CreateStatusBar(root.transform);
			CreateTopProfile(root.transform, "Nocturne Oracle", "夜色低语，指引真相 ✦");
			CreateText("Title", root.transform, "── ✦  跳转对话  ✦ ──", 62, SoftGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(720, 86), FontStyle.Bold);
			CreateText("Subtitle", root.transform, "与好友开启专属对话，塔罗师将结合你们的缘分与能量给出指引", 25, TextMuted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -375), new Vector2(820, 42), FontStyle.Normal);

			GameObject card = CreatePanel("JumpCard", root.transform, Panel);
			Rect(card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -935), new Vector2(820, 1090), new Vector2(0.5f, 0.5f));
			Frame(card.transform, new Vector2(820, 1090), Gold, 1.6f);
			CreateAvatar(card.transform, "Avatar", new Vector2(0, 370), 210, PortraitB(), true);
			CreateSmallRound(card.transform, "AtBadge", new Vector2(96, 295), "@", 68);
			CreateText("FriendName", card.transform, "Luna", 42, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 228), new Vector2(260, 58), FontStyle.Normal);
			CreateGhostButton(card.transform, new Vector2(0, 170), new Vector2(260, 52), "♓  双鱼座 · 月亮", 22);
			CreateGhostButton(card.transform, new Vector2(0, 65), new Vector2(460, 72), "已选择好友：   @Luna   ×", 27);
			CreateQuestionBox(card.transform, new Vector2(0, -80));
			CreateText("Hint", card.transform, "✦  塔罗师将自动结合 @Luna 的相关信息与你们的连接能量进行解读", 24, TextMuted, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -210), new Vector2(700, 48), FontStyle.Normal);
			CreateText("Inspiration", card.transform, "────  ✦  灵感问题  ✦  ────", 27, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -290), new Vector2(620, 42), FontStyle.Bold);
			CreatePromptGrid(card.transform);
			CreatePrimaryButton(card.transform, new Vector2(0, -1010), new Vector2(730, 95), "✦        进入对话        ✦");
			CreateBottomNav(root.transform, "好友");
		});
	}

	[MenuItem("Tools/UI/Friend Screens/Relationship Tarot Invite")]
	public static void BuildRelationshipTarotInvite()
	{
		SavePrefab("Assets/GameData/UI/Main/Friend/RelationshipTarotInviteUI.prefab", root =>
		{
			BuildSharedBackground(root.transform, 0.72f);
			CreateStatusBar(root.transform);
			CreateTopProfile(root.transform, "Nocturne Oracle", "夜色为引，真相自现 ✦");
			CreateText("SwitchOracle", root.transform, "切换神谕师", 24, SoftGold, TextAnchor.MiddleCenter, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-120, -205), new Vector2(180, 44), FontStyle.Normal);
			CreateText("Title", root.transform, "──✦  双人关系占卜  ✦──", 56, SoftGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -295), new Vector2(760, 82), FontStyle.Bold);
			CreateText("Subtitle", root.transform, "与你与重要之人，共启一场真心的对话", 28, TextMuted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -365), new Vector2(760, 48), FontStyle.Normal);

			GameObject note = CreatePanel("OracleNote", root.transform, PanelDeep);
			Rect(note, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -565), new Vector2(900, 250), new Vector2(0.5f, 0.5f));
			Frame(note.transform, new Vector2(900, 250), Gold, 1.5f);
			CreateText("NoteIconL", note.transform, "☾✦", 46, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-370, -10), new Vector2(90, 90), FontStyle.Bold);
			CreateText("NoteIconR", note.transform, "✦☽", 46, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(370, -10), new Vector2(90, 90), FontStyle.Bold);
			CreateText("NoteTitle", note.transform, "✦   占卜说明   ✦", 34, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 70), new Vector2(440, 50), FontStyle.Bold);
			CreateText("NoteText", note.transform, "本次为双人关系占卜，牌阵共三张。\n你和对方各自翻开属于自己的牌，\n只有你们可以看到自己可翻开的结果。", 29, TextMain, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -30), new Vector2(620, 130), FontStyle.Normal);

			GameObject spread = CreatePanel("InviteSpread", root.transform, Panel);
			Rect(spread, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1085), new Vector2(1000, 890), new Vector2(0.5f, 0.5f));
			Frame(spread.transform, new Vector2(1000, 890), Gold, 1.5f);
			CreateInviteCardColumn(spread.transform, -350, 150, "你可翻开", "你的内心与看法", "♟", new Color(0.65f, 0.28f, 0.96f, 1f));
			CreateInviteCardColumn(spread.transform, 0, 150, "共同揭示", "关系的现状与指引", "♊", SoftGold);
			CreateInviteCardColumn(spread.transform, 350, 150, "对方可翻开", "对方的内心与想法", "♟", new Color(0.65f, 0.28f, 0.96f, 1f));
			CreateLegendBar(spread.transform, new Vector2(0, -260));
			CreatePrivacyBar(spread.transform, new Vector2(0, -430), "隐私保护已开启", "结果仅对各自可见，保障你们的信任与安全");
			CreatePrimaryButton(root.transform, new Vector2(0, -1762), new Vector2(820, 102), "✦      发送占卜邀请      ▶");
			CreateText("InviteHint", root.transform, "邀请对方加入，占卜将自动开始", 25, TextMuted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1862), new Vector2(520, 40), FontStyle.Normal);
		});
	}

	[MenuItem("Tools/UI/Friend Screens/Relationship Tarot Reading")]
	public static void BuildRelationshipTarotReading()
	{
		SavePrefab("Assets/GameData/UI/Main/Friend/RelationshipTarotReadingUI.prefab", root =>
		{
			BuildSharedBackground(root.transform, 0.78f);
			CreateCircleButton(root.transform, "Back", new Vector2(96, -105), "<", 82, TextAnchor.MiddleCenter, true);
			CreateHeader(root.transform, "──✦  双人占卜  ✦──", "三张牌，洞见你们的连接与未来", -95);
			CreateText("Arcana", root.transform, "✦\n\n       ╭────────────╮\n   ✦       ✦       ✦\n       ╰────────────╯", 28, new Color(0.67f, 0.28f, 0.95f, 0.32f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -405), new Vector2(860, 360), FontStyle.Normal);
			CreateReadingCard(root.transform, -320, -620, "你", "你的影响与能量", "可翻开", true, false);
			CreateReadingCard(root.transform, 0, -620, "关系核心", "关系的核心课题", "对方可翻开", false, true);
			CreateReadingCard(root.transform, 320, -620, "对方", "对方的影响与想法", "对方可翻开", false, true);
			CreatePrimaryButton(root.transform, new Vector2(0, -1358), new Vector2(710, 94), "▣   翻开你的牌");
			CreateText("Waiting", root.transform, "⌛  等待对方翻开其牌位后，才能查看完整结果", 26, TextMuted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1455), new Vector2(720, 42), FontStyle.Normal);

			GameObject status = CreatePanel("CurrentStatus", root.transform, PanelDeep);
			Rect(status, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1650), new Vector2(920, 210), new Vector2(0.5f, 0.5f));
			Frame(status.transform, new Vector2(920, 210), new Color(Gold.r, Gold.g, Gold.b, 0.55f), 1.3f);
			CreateText("StatusTitle", status.transform, "✦   当前状态   ✦", 32, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 70), new Vector2(420, 48), FontStyle.Bold);
			CreateText("YouIcon", status.transform, "◎", 58, PurpleBright, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-330, -18), new Vector2(80, 80), FontStyle.Normal);
			CreateText("YouState", status.transform, "你\n可翻开", 28, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-225, -20), new Vector2(160, 82), FontStyle.Normal, new Vector2(0, 0.5f));
			CreateText("Dots", status.transform, "•••", 42, TextMuted, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -20), new Vector2(100, 60), FontStyle.Bold);
			CreateText("OtherIcon", status.transform, "◎", 58, TextMuted, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(235, -18), new Vector2(80, 80), FontStyle.Normal);
			CreateText("OtherState", status.transform, "对方\n未翻开", 28, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(340, -20), new Vector2(160, 82), FontStyle.Normal, new Vector2(0, 0.5f));
			CreateText("Footer", root.transform, "✦  真诚的心，才能让牌意更清晰", 25, TextMuted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1840), new Vector2(620, 42), FontStyle.Normal);
		});
	}

	[MenuItem("Tools/UI/Friend Screens/Relationship Permission Allocation")]
	public static void BuildRelationshipPermissionAllocation()
	{
		SavePrefab("Assets/GameData/UI/Main/Friend/RelationshipPermissionAllocationUI.prefab", root =>
		{
			BuildSharedBackground(root.transform, 0.74f);
			CreateStatusBar(root.transform);
			CreateTopProfile(root.transform, "Nocturne Oracle", "夜幕低语，指引真相 ☾");
			CreateText("Title", root.transform, "──✦  双人占卜 · 权限分配  ✦──", 47, SoftGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -260), new Vector2(820, 76), FontStyle.Bold);
			CreateText("Subtitle", root.transform, "真实好友占卜需要区分双方可翻开的牌位，\n双方只能翻开被分配的牌。", 27, TextMuted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -340), new Vector2(760, 90), FontStyle.Normal);
			CreatePermissionCard(root.transform, -320, -600, "1", "对方翻开", false);
			CreatePermissionCard(root.transform, 0, -600, "2", "你来翻开", true);
			CreatePermissionCard(root.transform, 320, -600, "3", "对方翻开", false);

			GameObject panel = CreatePanel("CollaborationPanel", root.transform, Panel);
			Rect(panel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1260), new Vector2(880, 650), new Vector2(0.5f, 0.5f));
			Frame(panel.transform, new Vector2(880, 650), Gold, 1.5f);
			CreateText("PanelTitle", panel.transform, "✧  协作占卜进行中  ✧", 35, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 260), new Vector2(500, 56), FontStyle.Bold);
			CreateAvatar(panel.transform, "You", new Vector2(-310, 150), 96, PortraitA(), true);
			CreateText("YouText", panel.transform, "你\n占卜者", 29, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-205, 145), new Vector2(150, 85), FontStyle.Normal, new Vector2(0, 0.5f));
			CreateText("Orb", panel.transform, "✦", 76, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 130), new Vector2(150, 100), FontStyle.Bold);
			CreateText("LunaText", panel.transform, "Luna\n你的好友", 29, TextMain, TextAnchor.MiddleRight, Center(), Center(), new Vector2(205, 145), new Vector2(180, 85), FontStyle.Normal, new Vector2(1, 0.5f));
			CreateAvatar(panel.transform, "Luna", new Vector2(325, 150), 96, PortraitB(), true);
			CreateText("Wait", panel.transform, "⌛  等待 Luna 翻开第 1 张牌", 31, TextMain, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 40), new Vector2(680, 48), FontStyle.Normal);
			CreateProgressDots(panel.transform, new Vector2(0, -80));
			CreatePrivacyBar(panel.transform, new Vector2(0, -230), "小提示", "双方只会在翻开后看到各自可见的牌面，保持神秘与尊重。");
			CreatePrimaryButton(panel.transform, new Vector2(0, -585), new Vector2(700, 88), "✦   翻开我的牌");
			CreateBottomNav(root.transform, "好友");
		});
	}

	[MenuItem("Tools/UI/Friend Screens/Tarot Invite Inbox")]
	public static void BuildTarotInviteInbox()
	{
		SavePrefab("Assets/GameData/UI/Main/Friend/TarotInviteInboxUI.prefab", root =>
		{
			BuildSharedBackground(root.transform, 0.74f);
			CreateStatusBar(root.transform);
			CreateTopProfile(root.transform, "Nocturne Oracle", "夜幕之语，直觉即真相 ✦");
			CreateText("FriendsTitle", root.transform, "朋友 Friends ✦", 50, SoftGold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -250), new Vector2(520, 68), FontStyle.Bold, new Vector2(0, 0.5f));
			CreateTab(root.transform, new Vector2(-200, -360), "占卜邀请", "收到的新邀请", "1", true);
			CreateTab(root.transform, new Vector2(170, -360), "朋友列表", "查看全部好友", "", false);

			GameObject card = CreatePanel("InviteCard", root.transform, Panel);
			Rect(card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1010), new Vector2(980, 1080), new Vector2(0.5f, 0.5f));
			Frame(card.transform, new Vector2(980, 1080), Gold, 1.5f);
			CreateText("Ribbon", card.transform, "新邀请", 30, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-415, 438), new Vector2(170, 52), FontStyle.Normal);
			CreateText("CardTitle", card.transform, "──✦  占卜邀请  ✦──", 48, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 415), new Vector2(540, 68), FontStyle.Bold);
			CreateAvatar(card.transform, "Luna", new Vector2(-250, 270), 150, PortraitB(), true);
			CreateText("Name", card.transform, "Luna", 45, SoftGold, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-70, 310), new Vector2(360, 54), FontStyle.Bold, new Vector2(0, 0.5f));
			CreateText("Handle", card.transform, "@luna_mystic  ·  好友\n5 分钟前", 28, TextMuted, TextAnchor.UpperLeft, Center(), Center(), new Vector2(-70, 242), new Vector2(360, 90), FontStyle.Normal, new Vector2(0, 0.5f));
			CreateText("Body", card.transform, "Luna 邀请你一起完成关于关系走向的三牌占卜", 31, TextMain, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 92), new Vector2(740, 56), FontStyle.Normal);
			CreateText("SubBody", card.transform, "✦ 双方将会揭示各自允许展现的牌", 26, TextMuted, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 40), new Vector2(680, 42), FontStyle.Normal);
			CreateInviteInfoRow(card.transform, -80, "占卜主题", "关系走向", "☽");
			CreateInviteInfoRow(card.transform, -195, "当前状态", "等待你的回应", "◷");
			GameObject reveal = CreatePanel("RevealInfo", card.transform, new Color(0.08f, 0.065f, 0.10f, 0.80f));
			Rect(reveal, Center(), Center(), new Vector2(0, -365), new Vector2(820, 230), new Vector2(0.5f, 0.5f));
			Frame(reveal.transform, new Vector2(820, 230), new Color(Gold.r, Gold.g, Gold.b, 0.45f), 1.2f);
			CreateText("RevealTitle", reveal.transform, "· ✦  牌面揭示信息  ✦ ·", 28, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 75), new Vector2(500, 44), FontStyle.Bold);
			CreateText("RevealMine", reveal.transform, "▣  你需要翻开的牌", 27, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-250, 18), new Vector2(360, 44), FontStyle.Normal, new Vector2(0, 0.5f));
			CreateText("RevealMineValue", reveal.transform, "第 2 张", 29, SoftGold, TextAnchor.MiddleRight, Center(), Center(), new Vector2(285, 18), new Vector2(180, 44), FontStyle.Bold, new Vector2(1, 0.5f));
			CreateText("RevealOther", reveal.transform, "▣  对方翻开的牌", 27, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-250, -55), new Vector2(360, 44), FontStyle.Normal, new Vector2(0, 0.5f));
			CreateText("RevealOtherValue", reveal.transform, "第 1 张、第 3 张", 29, SoftGold, TextAnchor.MiddleRight, Center(), Center(), new Vector2(250, -55), new Vector2(270, 44), FontStyle.Bold, new Vector2(1, 0.5f));
			CreateGhostButton(card.transform, new Vector2(-310, -510), new Vector2(250, 90), "稍后处理", 29);
			CreateGhostButton(card.transform, new Vector2(0, -510), new Vector2(250, 90), "⊗  拒绝", 29);
			CreatePrimaryButton(card.transform, new Vector2(305, -1050), new Vector2(300, 90), "✦  开始回应");
			CreateBottomNav(root.transform, "朋友");
		});
	}

	[MenuItem("Tools/UI/Friend Screens/Relationship Tarot Result")]
	public static void BuildRelationshipTarotResult()
	{
		SavePrefab("Assets/GameData/UI/Main/Friend/RelationshipTarotResultUI.prefab", root =>
		{
			BuildSharedBackground(root.transform, 0.45f);
			CreateStatusBar(root.transform);
			CreateText("Back", root.transform, "<", 64, SoftGold, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(0, 1), new Vector2(58, -130), new Vector2(80, 80), FontStyle.Normal);
			CreateText("Help", root.transform, "?", 46, SoftGold, TextAnchor.MiddleCenter, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-72, -130), new Vector2(72, 72), FontStyle.Bold);
			CreateText("Title", root.transform, "与你的共同占卜结果", 43, TextMain, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -132), new Vector2(620, 62), FontStyle.Normal);
			CreateText("Done", root.transform, "✧  占卜已完成  ✧", 31, SoftGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -215), new Vector2(420, 48), FontStyle.Bold);
			CreateText("Sub", root.transform, "你们一起揭开了关系的指引", 29, TextMain, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -265), new Vector2(520, 42), FontStyle.Normal);
			CreateResultCard(root.transform, -330, -570, "I", "你的感受", "你对这段关系充满\n好奇与期待，内心\n渴望更深的理解与\n连接。", SpriteAt("Assets/GameData/Arts/Sprites/MajorArcana/RWS_Tarot_17_Star.jpg"));
			CreateResultCard(root.transform, 0, -570, "II", "对方回应", "对方也在意你，正\n在思考如何更靠近\n你，愿意为关系付\n出行动。", SpriteAt("Assets/GameData/Arts/Sprites/MajorArcana/RWS_Tarot_18_Moon.jpg"));
			CreateResultCard(root.transform, 330, -570, "III", "关系走向", "这段关系有望稳步\n向前发展，彼此的\n真诚将带来长久的\n陪伴与成长。", SpriteAt("Assets/GameData/Arts/images/asset-080.png"));
			GameObject summary = CreatePanel("Summary", root.transform, PanelDeep);
			Rect(summary, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1228), new Vector2(900, 185), new Vector2(0.5f, 0.5f));
			Frame(summary.transform, new Vector2(900, 185), Gold, 1.2f);
			CreateText("SummaryTitle", summary.transform, "─✧  综合解读  ✧─", 34, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 45), new Vector2(500, 48), FontStyle.Bold);
			CreateText("SummaryText", summary.transform, "你们的连接充满潜力与温度。保持开放的沟通，坦诚表达感受，\n一起创造属于你们的美好未来。", 31, TextMain, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -35), new Vector2(820, 92), FontStyle.Normal);
			CreateText("NextTitle", root.transform, "✧  下一步你可以  ✧", 35, TextMain, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1365), new Vector2(520, 54), FontStyle.Normal);
			CreateActionCard(root.transform, new Vector2(-235, -1500), "☵", "继续与神谕师聊聊", "深入解析关系细节");
			CreateActionCard(root.transform, new Vector2(235, -1500), "▤", "保存到历史", "随时查看占卜记录");
			CreateText("Privacy", root.transform, "▣  本次结果仅你和对方可见，已为你们加密保存。", 24, TextMuted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1655), new Vector2(680, 40), FontStyle.Normal);
			CreateBottomNav(root.transform, "朋友");
		});
	}

	private static void SavePrefab(string path, System.Action<GameObject> build)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		GameObject root = new GameObject(Path.GetFileNameWithoutExtension(path), typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
		RectTransform rect = root.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.zero;
		rect.pivot = Vector2.zero;
		rect.sizeDelta = Vector2.zero;
		rect.localScale = Vector3.zero;

		Canvas canvas = root.GetComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceCamera;
		CanvasScaler scaler = root.GetComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1080, 1920);
		scaler.matchWidthOrHeight = 0.5f;

		build(root);
		SetLayerRecursive(root, LayerMask.NameToLayer("UI"));
		PrefabUtility.SaveAsPrefabAsset(root, path);
		Object.DestroyImmediate(root);
		Debug.Log("Generated: " + path);
	}

	private static void BuildSharedBackground(Transform parent, float veilAlpha)
	{
		Image bg = CreateImage("Background", parent, SpriteAt("Assets/GameData/Arts/flow-1-flip.png"), Color.white, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
		Stretch(bg.rectTransform);
		bg.preserveAspect = false;
		Image dark = CreateImage("DarkVeil", parent, null, new Color(Bg.r, Bg.g, Bg.b, veilAlpha), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);
		Stretch(dark.rectTransform);
		CreateText("StarMap", parent, "✦        ✧       ✦\n\n      ◜──────────────◝\n   ✧      ✦       ✧      ✦\n      ◟──────────────◞\n\n✧            ✦            ✧", 30, new Color(0.70f, 0.35f, 0.95f, 0.24f), TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 205), new Vector2(900, 520), FontStyle.Normal);
	}

	private static void CreateStatusBar(Transform parent)
	{
		CreateText("StatusTime", parent, "9:41", 31, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, -46), new Vector2(170, 46), FontStyle.Bold, new Vector2(0, 0.5f));
		CreateText("StatusIcons", parent, "▮▮▮  ◠  ▭", 29, Color.white, TextAnchor.MiddleRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -46), new Vector2(270, 46), FontStyle.Bold, new Vector2(1, 0.5f));
	}

	private static void CreateHeader(Transform parent, string title, string subtitle, float y)
	{
		CreateText("Title", parent, title, 45, SoftGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(780, 72), FontStyle.Bold);
		if (!string.IsNullOrEmpty(subtitle))
			CreateText("Subtitle", parent, subtitle, 27, SoftGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y - 64), new Vector2(700, 48), FontStyle.Normal);
	}

	private static void CreateTopProfile(Transform parent, string name, string desc)
	{
		CreateAvatar(parent, "MeAvatar", new Vector2(86, -122), 106, PortraitA(), true, new Vector2(0, 1), new Vector2(0, 1));
		CreateText("MeName", parent, name, 38, SoftGold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, -98), new Vector2(410, 48), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateText("MeDesc", parent, desc, 27, SoftGold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(190, -142), new Vector2(480, 40), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateCircleButton(parent, "Bell", new Vector2(-178, -116), "♢", 70, TextAnchor.MiddleCenter, false);
		CreateCircleButton(parent, "Add", new Vector2(-78, -116), "+", 76, TextAnchor.MiddleCenter, false);
	}

	private static void CreateSectionTitle(Transform parent, string title, Vector2 pos)
	{
		CreateText("Section_" + title, parent, title, 31, SoftGold, TextAnchor.MiddleLeft, Center(), Center(), pos, new Vector2(360, 50), FontStyle.Bold, new Vector2(0, 0.5f));
	}

	private static void CreateSeparatorTitle(Transform parent, string title, float y)
	{
		CreateText("Separator_" + title, parent, title, 29, SoftGold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(42, y), new Vector2(220, 44), FontStyle.Bold, new Vector2(0, 0.5f));
		CreateLine("SeparatorLine", parent, new Vector2(640, y), new Vector2(745, 1.6f), new Color(Gold.r, Gold.g, Gold.b, 0.42f), new Vector2(0, 1), new Vector2(0, 1));
	}

	private static void CreateInfoRow(Transform parent, float y, string icon, string label, string value)
	{
		GameObject row = CreatePanel("Row_" + label, parent, Field);
		Rect(row, Center(), Center(), new Vector2(0, y), new Vector2(930, 86), new Vector2(0.5f, 0.5f));
		Frame(row.transform, new Vector2(930, 86), new Color(Gold.r, Gold.g, Gold.b, 0.36f), 1f);
		CreateText("Icon", row.transform, icon, 36, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-400, 0), new Vector2(60, 60), FontStyle.Normal);
		CreateText("Label", row.transform, label, 27, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-318, 0), new Vector2(180, 50), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateText("Value", row.transform, value, 29, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(60, 0), new Vector2(520, 50), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateText("Arrow", row.transform, "⌄", 38, TextMuted, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(410, 0), new Vector2(50, 50), FontStyle.Normal);
	}

	private static void CreateConfirmRow(Transform parent, float y, string icon, string label, string value)
	{
		CreateText("Icon_" + label, parent, icon, 50, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-280, y), new Vector2(82, 82), FontStyle.Normal);
		CreateText("Label_" + label, parent, label, 28, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-175, y), new Vector2(180, 48), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateText("Value_" + label, parent, value, 31, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(30, y), new Vector2(360, 50), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateSmallRound(parent, "Edit_" + label, new Vector2(310, y), "✎", 52);
		if (y > -100) CreateLine("Line_" + label, parent, new Vector2(0, y - 70), new Vector2(640, 1.5f), new Color(Gold.r, Gold.g, Gold.b, 0.35f));
	}

	private static void CreateRecord(Transform parent, float y, string icon, string title, string desc, string tag)
	{
		GameObject row = CreatePanel("Record_" + tag, parent, Field);
		Rect(row, Center(), Center(), new Vector2(0, y), new Vector2(930, 106), new Vector2(0.5f, 0.5f));
		Frame(row.transform, new Vector2(930, 106), new Color(Gold.r, Gold.g, Gold.b, 0.30f), 1f);
		CreateText("Icon", row.transform, icon, 48, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-395, 0), new Vector2(82, 82), FontStyle.Bold);
		CreateText("Title", row.transform, title, 28, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-292, 18), new Vector2(470, 42), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateGhostButton(row.transform, new Vector2(205, 22), new Vector2(82, 34), tag, 18);
		CreateText("Desc", row.transform, desc, 21, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-292, -25), new Vector2(550, 34), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateText("Go", row.transform, "›", 48, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(405, 0), new Vector2(40, 60), FontStyle.Normal);
	}

	private static void CreateRadioRow(Transform parent, float y, bool selected, string icon, string title, string desc)
	{
		CreateText("Radio_" + title, parent, selected ? "◉" : "○", 58, selected ? PurpleBright : Gold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-430, y), new Vector2(70, 70), FontStyle.Normal);
		CreateText("Icon_" + title, parent, icon, 52, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-315, y), new Vector2(80, 80), FontStyle.Normal);
		CreateText("Title_" + title, parent, title, 35, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-210, y + 24), new Vector2(320, 50), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateText("Desc_" + title, parent, desc, 27, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-210, y - 24), new Vector2(620, 44), FontStyle.Normal, new Vector2(0, 0.5f));
		if (y > -130) CreateLine("Line_" + title, parent, new Vector2(60, y - 70), new Vector2(850, 1.2f), new Color(1f, 1f, 1f, 0.10f));
	}

	private static void CreateValueBox(Transform parent, Vector2 pos, Vector2 size, string value, string icon, int font)
	{
		GameObject box = CreatePanel("ValueBox_" + value, parent, Field);
		Rect(box, Center(), Center(), pos, size, new Vector2(0.5f, 0.5f));
		Frame(box.transform, size, new Color(Gold.r, Gold.g, Gold.b, 0.42f), 1.2f);
		CreateText("Value", box.transform, value, font, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-size.x * 0.5f + 32, 0), new Vector2(size.x - 95, size.y - 18), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateText("Icon", box.transform, icon, 35, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(size.x * 0.5f - 50, 0), new Vector2(58, 58), FontStyle.Normal);
	}

	private static void CreateQuestionBox(Transform parent, Vector2 pos)
	{
		GameObject box = CreatePanel("QuestionInput", parent, new Color(0.10f, 0.08f, 0.13f, 0.86f));
		Rect(box, Center(), Center(), pos, new Vector2(730, 145), new Vector2(0.5f, 0.5f));
		Frame(box.transform, new Vector2(730, 145), Gold, 1.2f);
		CreateImage("Crystal", box.transform, SpriteAt("Assets/GameData/Arts/images/asset-105.png"), Color.white, new Vector2(-305, 0), new Vector2(88, 88), Center(), Center());
		CreateText("Placeholder", box.transform, "输入你想问的问题，或从下方选择灵感", 27, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-160, 0), new Vector2(455, 56), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateSmallRound(box.transform, "Send", new Vector2(310, 0), "↑", 72);
	}

	private static void CreatePromptGrid(Transform parent)
	{
		string[] prompts =
		{
			"帮我看看她最近的状态 ›",
			"我们关系会如何发展 ›",
			"适合主动联系吗 ›",
			"她怎么看我 ›"
		};
		string[] icons = { "?", "♡", "☏", "♟" };
		for (int i = 0; i < prompts.Length; i++)
		{
			int row = i / 2;
			int col = i % 2;
			Vector2 pos = new Vector2(col == 0 ? -190 : 190, -365 - row * 130);
			GameObject cell = CreatePanel("Prompt" + i, parent, Field);
			Rect(cell, Center(), Center(), pos, new Vector2(345, 92), new Vector2(0.5f, 0.5f));
			Frame(cell.transform, new Vector2(345, 92), new Color(Gold.r, Gold.g, Gold.b, 0.48f), 1.2f);
			CreateText("Icon", cell.transform, icons[i], 34, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-120, 0), new Vector2(52, 52), FontStyle.Bold);
			CreateText("Text", cell.transform, prompts[i], 22, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-45, 0), new Vector2(245, 44), FontStyle.Normal, new Vector2(0, 0.5f));
		}
	}

	private static void CreateInviteCardColumn(Transform parent, float x, float y, string title, string desc, string badge, Color accent)
	{
		CreateTarotBack(parent, new Vector2(x, y), new Vector2(220, 330), false, false, Center(), Center());
		CreateSmallRound(parent, "Badge_" + title, new Vector2(x - 118, y + 155), badge, 72);
		GameObject label = CreatePanel("InviteLabel_" + title, parent, new Color(0.08f, 0.055f, 0.10f, 0.75f));
		Rect(label, Center(), Center(), new Vector2(x, y - 280), new Vector2(255, 135), new Vector2(0.5f, 0.5f));
		Frame(label.transform, new Vector2(255, 135), new Color(Gold.r, Gold.g, Gold.b, 0.55f), 1.1f);
		CreateText("Title", label.transform, title, 29, accent, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 32), new Vector2(210, 44), FontStyle.Normal);
		CreateText("Desc", label.transform, desc + "\n✦✦", 25, TextMain, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -30), new Vector2(215, 80), FontStyle.Normal);
	}

	private static void CreateLegendBar(Transform parent, Vector2 pos)
	{
		GameObject bar = CreatePanel("PermissionLegend", parent, new Color(0.055f, 0.045f, 0.080f, 0.82f));
		Rect(bar, Center(), Center(), pos, new Vector2(870, 130), new Vector2(0.5f, 0.5f));
		Frame(bar.transform, new Vector2(870, 130), new Color(Gold.r, Gold.g, Gold.b, 0.45f), 1f);
		CreateLegendItem(bar.transform, -285, "♟", "你可翻开", "仅你可见", PurpleBright);
		CreateLegendItem(bar.transform, 0, "♊", "共同揭示", "你与对方可见", SoftGold);
		CreateLegendItem(bar.transform, 285, "♟", "对方可翻开", "仅对方可见", PurpleBright);
		CreateLine("LegendLineL", bar.transform, new Vector2(-145, 0), new Vector2(1.2f, 80), new Color(1f, 1f, 1f, 0.18f));
		CreateLine("LegendLineR", bar.transform, new Vector2(145, 0), new Vector2(1.2f, 80), new Color(1f, 1f, 1f, 0.18f));
	}

	private static void CreateLegendItem(Transform parent, float x, string icon, string title, string desc, Color accent)
	{
		CreateSmallRound(parent, "LegendIcon_" + title, new Vector2(x - 82, 0), icon, 64);
		CreateText("LegendTitle_" + title, parent, title, 27, accent, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(x - 20, 20), new Vector2(150, 36), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateText("LegendDesc_" + title, parent, desc, 22, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(x - 20, -20), new Vector2(165, 34), FontStyle.Normal, new Vector2(0, 0.5f));
	}

	private static void CreatePrivacyBar(Transform parent, Vector2 pos, string title, string desc)
	{
		GameObject bar = CreatePanel("PrivacyBar_" + title, parent, new Color(0.08f, 0.060f, 0.105f, 0.82f));
		Rect(bar, Center(), Center(), pos, new Vector2(850, 105), new Vector2(0.5f, 0.5f));
		Frame(bar.transform, new Vector2(850, 105), new Color(Gold.r, Gold.g, Gold.b, 0.45f), 1.1f);
		CreateText("Shield", bar.transform, "▱", 48, new Color(0.70f, 0.62f, 0.72f, 0.85f), TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-360, 0), new Vector2(68, 68), FontStyle.Bold);
		CreateText("Title", bar.transform, title, 26, SoftGold, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-285, 22), new Vector2(360, 38), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateText("Desc", bar.transform, desc, 22, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-285, -18), new Vector2(650, 38), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateText("Spark", bar.transform, "✧", 33, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(370, 8), new Vector2(44, 44), FontStyle.Bold);
	}

	private static void CreateReadingCard(Transform parent, float x, float y, string owner, string title, string action, bool active, bool locked)
	{
		CreateText("Owner_" + owner, parent, owner, 34, SoftGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, y + 210), new Vector2(210, 50), FontStyle.Normal);
		CreateText("Star_" + owner, parent, "✦", 48, SoftGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, y + 150), new Vector2(80, 60), FontStyle.Bold);
		CreateTarotBack(parent, new Vector2(x, y - 50), new Vector2(245, 395), active, locked, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
		GameObject label = CreatePanel("ReadingLabel_" + owner, parent, PanelDeep);
		Rect(label, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, y - 430), new Vector2(275, 135), new Vector2(0.5f, 0.5f));
		Frame(label.transform, new Vector2(275, 135), new Color(Gold.r, Gold.g, Gold.b, 0.52f), 1f);
		CreateText("Title", label.transform, title, 26, TextMain, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 28), new Vector2(230, 42), FontStyle.Normal);
		CreateText("Action", label.transform, action, 27, active ? SoftGold : TextMain, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -28), new Vector2(220, 44), FontStyle.Bold);
	}

	private static void CreatePermissionCard(Transform parent, float x, float y, string number, string owner, bool active)
	{
		CreateTarotBack(parent, new Vector2(x, y), new Vector2(235, 380), active, false, new Vector2(0.5f, 1), new Vector2(0.5f, 1));
		CreateSmallRound(parent, "Number_" + number, new Vector2(x, y - 202), number, 58);
		CreateText("Owner_" + number, parent, owner, 31, active ? PurpleBright : TextMain, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, y - 285), new Vector2(220, 44), FontStyle.Normal);
	}

	private static void CreateProgressDots(Transform parent, Vector2 pos)
	{
		CreateLine("ProgressLine", parent, pos + new Vector2(0, 0), new Vector2(430, 2), new Color(1f, 1f, 1f, 0.32f));
		for (int i = 0; i < 3; i++)
		{
			float x = pos.x - 215 + i * 215;
			GameObject dot = CreatePanel("ProgressDot" + i, parent, i == 0 ? PurpleBright : new Color(0.10f, 0.10f, 0.13f, 0.95f));
			Rect(dot, Center(), Center(), new Vector2(x, pos.y), new Vector2(48, 48), new Vector2(0.5f, 0.5f));
			Frame(dot.transform, new Vector2(48, 48), i == 0 ? PurpleBright : TextMuted, 1.5f);
			CreateText("Label", parent, "第 " + (i + 1) + " 张牌", 22, i == 0 ? PurpleBright : TextMuted, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(x, pos.y - 58), new Vector2(150, 32), FontStyle.Normal);
		}
	}

	private static void CreateTab(Transform parent, Vector2 pos, string title, string desc, string count, bool selected)
	{
		GameObject tab = CreatePanel("Tab_" + title, parent, selected ? new Color(0.16f, 0.08f, 0.22f, 0.90f) : new Color(0.10f, 0.095f, 0.13f, 0.78f));
		Rect(tab, new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, new Vector2(330, 130), new Vector2(0.5f, 0.5f));
		Frame(tab.transform, new Vector2(330, 130), selected ? PurpleBright : new Color(Gold.r, Gold.g, Gold.b, 0.45f), 1.3f);
		CreateText("Icon", tab.transform, selected ? "♙" : "♧", 45, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-115, 12), new Vector2(62, 62), FontStyle.Bold);
		CreateText("Title", tab.transform, title, 28, SoftGold, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-40, 28), new Vector2(180, 42), FontStyle.Bold, new Vector2(0, 0.5f));
		CreateText("Desc", tab.transform, desc, 23, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-40, -25), new Vector2(210, 36), FontStyle.Normal, new Vector2(0, 0.5f));
		if (!string.IsNullOrEmpty(count))
			CreateSmallRound(tab.transform, "Count", new Vector2(125, 32), count, 42);
	}

	private static void CreateInviteInfoRow(Transform parent, float y, string title, string value, string icon)
	{
		CreateLine("InfoLine_" + title, parent, new Vector2(0, y + 58), new Vector2(820, 1.2f), new Color(1f, 1f, 1f, 0.15f));
		CreateText("Icon_" + title, parent, icon, 45, PurpleBright, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-360, y), new Vector2(60, 60), FontStyle.Bold);
		CreateText("Title_" + title, parent, title, 29, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-290, y), new Vector2(260, 48), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateText("Value_" + title, parent, value, 29, title == "当前状态" ? PurpleBright : TextMain, TextAnchor.MiddleRight, Center(), Center(), new Vector2(285, y), new Vector2(300, 48), FontStyle.Bold, new Vector2(1, 0.5f));
	}

	private static void CreateResultCard(Transform parent, float x, float y, string number, string title, string desc, Sprite sprite)
	{
		GameObject imageCard = CreatePanel("ResultCard_" + title, parent, new Color(0.05f, 0.035f, 0.08f, 0.95f));
		Rect(imageCard, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, y), new Vector2(285, 445), new Vector2(0.5f, 0.5f));
		Frame(imageCard.transform, new Vector2(285, 445), Gold, 1.7f);
		Image art = CreateImage("Art", imageCard.transform, sprite, Color.white, new Vector2(0, 20), new Vector2(252, 325), Center(), Center());
		art.preserveAspect = false;
		CreateText("Number", imageCard.transform, number, 28, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 180), new Vector2(80, 40), FontStyle.Bold);
		CreatePanel("GradientLabel", imageCard.transform, new Color(0.02f, 0.012f, 0.03f, 0.55f));
		Transform labelBg = imageCard.transform.Find("GradientLabel");
		Rect(labelBg.gameObject, Center(), Center(), new Vector2(0, -168), new Vector2(252, 86), new Vector2(0.5f, 0.5f));
		CreateText("Title", imageCard.transform, title, 32, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -168), new Vector2(230, 48), FontStyle.Bold);

		GameObject textBox = CreatePanel("ResultText_" + title, parent, PanelDeep);
		Rect(textBox, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, y - 365), new Vector2(285, 210), new Vector2(0.5f, 0.5f));
		Frame(textBox.transform, new Vector2(285, 210), new Color(Gold.r, Gold.g, Gold.b, 0.45f), 1.1f);
		CreateText("Text", textBox.transform, desc, 28, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(0, 0), new Vector2(230, 170), FontStyle.Normal);
	}

	private static void CreateActionCard(Transform parent, Vector2 pos, string icon, string title, string desc)
	{
		GameObject card = CreatePanel("Action_" + title, parent, new Color(0.11f, 0.07f, 0.18f, 0.78f));
		Rect(card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, new Vector2(420, 125), new Vector2(0.5f, 0.5f));
		Frame(card.transform, new Vector2(420, 125), new Color(Gold.r, Gold.g, Gold.b, 0.55f), 1.2f);
		CreateText("Icon", card.transform, icon, 50, TextMain, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(-150, 0), new Vector2(70, 70), FontStyle.Normal);
		CreateText("Title", card.transform, title, 29, TextMain, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-70, 25), new Vector2(270, 42), FontStyle.Normal, new Vector2(0, 0.5f));
		CreateText("Desc", card.transform, desc, 24, TextMuted, TextAnchor.MiddleLeft, Center(), Center(), new Vector2(-70, -25), new Vector2(270, 34), FontStyle.Normal, new Vector2(0, 0.5f));
	}

	private static void CreateTarotBack(Transform parent, Vector2 pos, Vector2 size, bool highlighted, bool locked, Vector2 anchorMin, Vector2 anchorMax)
	{
		GameObject card = CreatePanel("TarotBack", parent, highlighted ? new Color(0.12f, 0.07f, 0.20f, 0.98f) : new Color(0.025f, 0.020f, 0.035f, 0.98f));
		Rect(card, anchorMin, anchorMax, pos, size, new Vector2(0.5f, 0.5f));
		Frame(card.transform, size, highlighted ? PurpleBright : Gold, highlighted ? 3.5f : 1.7f);
		CreateText("Moon", card.transform, "☾", Mathf.RoundToInt(size.y * 0.10f), SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, size.y * 0.25f), new Vector2(size.x * 0.45f, 60), FontStyle.Bold);
		CreateText("Star", card.transform, "✦", Mathf.RoundToInt(size.y * 0.18f), SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, 0), new Vector2(size.x * 0.70f, 110), FontStyle.Bold);
		CreateText("BottomMoon", card.transform, "☽", Mathf.RoundToInt(size.y * 0.10f), SoftGold, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(0, -size.y * 0.25f), new Vector2(size.x * 0.45f, 60), FontStyle.Bold);
		CreateLine("InnerTop", card.transform, new Vector2(0, size.y * 0.42f), new Vector2(size.x * 0.76f, 1.2f), new Color(Gold.r, Gold.g, Gold.b, 0.45f));
		CreateLine("InnerBottom", card.transform, new Vector2(0, -size.y * 0.42f), new Vector2(size.x * 0.76f, 1.2f), new Color(Gold.r, Gold.g, Gold.b, 0.45f));
		CreateLine("InnerLeft", card.transform, new Vector2(-size.x * 0.38f, 0), new Vector2(1.2f, size.y * 0.80f), new Color(Gold.r, Gold.g, Gold.b, 0.45f));
		CreateLine("InnerRight", card.transform, new Vector2(size.x * 0.38f, 0), new Vector2(1.2f, size.y * 0.80f), new Color(Gold.r, Gold.g, Gold.b, 0.45f));
		if (locked)
			CreateSmallRound(card.transform, "Lock", new Vector2(0, 0), "▣", Mathf.Min(size.x, size.y) * 0.30f);
	}

	private static void CreateBottomNav(Transform parent, string selected)
	{
		GameObject nav = CreatePanel("BottomNavigation", parent, new Color(0.055f, 0.055f, 0.080f, 0.94f));
		Rect(nav, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 95), new Vector2(930, 150), new Vector2(0.5f, 0.5f));
		Frame(nav.transform, new Vector2(930, 150), new Color(1f, 1f, 1f, 0.10f), 1f);
		CreateNavItem(nav.transform, -360, "☾", selected == "今日神谕" ? "今日神谕" : "今日指引", selected == "今日神谕" || selected == "今日指引");
		CreateNavItem(nav.transform, -120, "▢", "对话", selected == "对话");
		CreateNavItem(nav.transform, 120, "♊", selected == "朋友" || selected == "好友" ? selected : "朋友", selected == "朋友" || selected == "好友");
		CreateNavItem(nav.transform, 360, "○", "我的", selected == "我的");
	}

	private static void CreateNavItem(Transform parent, float x, string icon, string label, bool selected)
	{
		Color color = selected ? PurpleBright : new Color(0.70f, 0.62f, 0.58f, 1f);
		CreateText("Icon_" + label, parent, icon, 45, color, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(x, 25), new Vector2(90, 52), FontStyle.Bold);
		CreateText("Label_" + label, parent, label, 25, color, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(x, -38), new Vector2(130, 36), FontStyle.Normal);
		if (selected)
			CreateText("Dot_" + label, parent, "•", 31, color, TextAnchor.MiddleCenter, Center(), Center(), new Vector2(x, -70), new Vector2(50, 24), FontStyle.Bold);
	}

	private static Button CreatePrimaryButton(Transform parent, Vector2 pos, Vector2 size, string label)
	{
		GameObject go = CreatePanel("PrimaryButton_" + label, parent, Purple);
		Rect(go, new Vector2(0.5f, 1), new Vector2(0.5f, 1), pos, size, new Vector2(0.5f, 0.5f));
		Frame(go.transform, size, SoftGold, 2f);
		CreateText("Label", go.transform, label, Mathf.RoundToInt(size.y * 0.43f), SoftGold, TextAnchor.MiddleCenter, Center(), Center(), Vector2.zero, new Vector2(size.x - 70, size.y - 18), FontStyle.Bold);
		Button button = go.AddComponent<Button>();
		button.targetGraphic = go.GetComponent<Image>();
		return button;
	}

	private static Button CreateGhostButton(Transform parent, Vector2 pos, Vector2 size, string label, int fontSize)
	{
		GameObject go = CreatePanel("GhostButton_" + label, parent, new Color(0.08f, 0.06f, 0.10f, 0.55f));
		Rect(go, Center(), Center(), pos, size, new Vector2(0.5f, 0.5f));
		Frame(go.transform, size, new Color(Gold.r, Gold.g, Gold.b, 0.50f), 1.2f);
		CreateText("Label", go.transform, label, fontSize, SoftGold, TextAnchor.MiddleCenter, Center(), Center(), Vector2.zero, new Vector2(size.x - 20, size.y - 10), FontStyle.Normal);
		Button button = go.AddComponent<Button>();
		button.targetGraphic = go.GetComponent<Image>();
		return button;
	}

	private static void CreateSwitch(Transform parent, Vector2 pos, bool on)
	{
		GameObject track = CreatePanel("Switch", parent, on ? new Color(0.35f, 0.10f, 0.55f, 1f) : new Color(0.15f, 0.13f, 0.16f, 1f));
		Rect(track, Center(), Center(), pos, new Vector2(130, 66), new Vector2(0.5f, 0.5f));
		Frame(track.transform, new Vector2(130, 66), on ? PurpleBright : Gold, 1.5f);
		GameObject knob = CreatePanel("Knob", track.transform, Color.white);
		Rect(knob, Center(), Center(), new Vector2(on ? 32 : -32, 0), new Vector2(54, 54), new Vector2(0.5f, 0.5f));
	}

	private static void CreateCircleButton(Transform parent, string name, Vector2 pos, string label, float size, TextAnchor align, bool leftAnchor)
	{
		Vector2 anchor = leftAnchor ? new Vector2(0, 1) : new Vector2(1, 1);
		GameObject go = CreatePanel("CircleButton_" + name, parent, new Color(0.02f, 0.018f, 0.04f, 0.55f));
		Rect(go, anchor, anchor, pos, new Vector2(size, size), new Vector2(0.5f, 0.5f));
		Frame(go.transform, new Vector2(size, size), Gold, 1.8f);
		CreateText("Label", go.transform, label, Mathf.RoundToInt(size * 0.58f), SoftGold, align, Center(), Center(), Vector2.zero, new Vector2(size, size), FontStyle.Bold);
	}

	private static void CreateSmallRound(Transform parent, string name, Vector2 pos, string label, float size)
	{
		GameObject go = CreatePanel(name, parent, new Color(0.18f, 0.11f, 0.26f, 0.95f));
		Rect(go, Center(), Center(), pos, new Vector2(size, size), new Vector2(0.5f, 0.5f));
		Frame(go.transform, new Vector2(size, size), Gold, 1.5f);
		CreateText("Label", go.transform, label, Mathf.RoundToInt(size * 0.44f), SoftGold, TextAnchor.MiddleCenter, Center(), Center(), Vector2.zero, new Vector2(size - 8, size - 8), FontStyle.Bold);
	}

	private static void CreateAvatar(Transform parent, string name, Vector2 pos, float size, Sprite sprite, bool framed, Vector2? anchorMin = null, Vector2? anchorMax = null)
	{
		GameObject frame = CreatePanel(name + "Frame", parent, new Color(0.02f, 0.015f, 0.03f, 0.82f));
		Vector2 aMin = anchorMin ?? Center();
		Vector2 aMax = anchorMax ?? Center();
		Rect(frame, aMin, aMax, pos, new Vector2(size, size), new Vector2(0.5f, 0.5f));
		if (framed) Frame(frame.transform, new Vector2(size, size), SoftGold, 2f);
		Image image = CreateImage(name, frame.transform, sprite, Color.white, Vector2.zero, new Vector2(size - 18, size - 18), Center(), Center());
		image.preserveAspect = true;
	}

	private static GameObject CreatePanel(string name, Transform parent, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		go.transform.SetParent(parent, false);
		go.GetComponent<Image>().color = color;
		return go;
	}

	private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		go.transform.SetParent(parent, false);
		Image image = go.GetComponent<Image>();
		image.sprite = sprite;
		image.color = color;
		Rect(go, anchorMin, anchorMax, pos, size, new Vector2(0.5f, 0.5f));
		return image;
	}

	private static Text CreateText(string name, Transform parent, string value, int fontSize, Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, FontStyle style, Vector2? pivot = null)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
		go.transform.SetParent(parent, false);
		Rect(go, anchorMin, anchorMax, anchoredPosition, size, pivot ?? new Vector2(0.5f, 0.5f));
		Text text = go.GetComponent<Text>();
		text.text = value;
		text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/GamerFrameWork/I2/Localization/Examples/Resources/ARIAL.TTF") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
		text.fontSize = fontSize;
		text.fontStyle = style;
		text.color = color;
		text.alignment = alignment;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		Shadow shadow = go.GetComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
		shadow.effectDistance = new Vector2(0, -1);
		return text;
	}

	private static void Frame(Transform parent, Vector2 size, Color color, float thickness)
	{
		CreateLine("TopBorder", parent, new Vector2(0, size.y * 0.5f), new Vector2(size.x, thickness), color);
		CreateLine("BottomBorder", parent, new Vector2(0, -size.y * 0.5f), new Vector2(size.x, thickness), color);
		CreateLine("LeftBorder", parent, new Vector2(-size.x * 0.5f, 0), new Vector2(thickness, size.y), color);
		CreateLine("RightBorder", parent, new Vector2(size.x * 0.5f, 0), new Vector2(thickness, size.y), color);
	}

	private static void CreateLine(string name, Transform parent, Vector2 position, Vector2 size, Color color)
	{
		CreateLine(name, parent, position, size, color, Center(), Center());
	}

	private static void CreateLine(string name, Transform parent, Vector2 position, Vector2 size, Color color, Vector2 anchorMin, Vector2 anchorMax)
	{
		Image line = CreateImage(name, parent, null, color, position, size, anchorMin, anchorMax);
		line.raycastTarget = false;
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.localScale = Vector3.one;
	}

	private static void Rect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
	{
		Rect(go.GetComponent<RectTransform>(), anchorMin, anchorMax, anchoredPosition, size, pivot);
	}

	private static void Rect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
	{
		rect.anchorMin = anchorMin;
		rect.anchorMax = anchorMax;
		rect.pivot = pivot;
		rect.localScale = Vector3.one;
		rect.anchoredPosition = anchoredPosition;
		rect.sizeDelta = size;
	}

	private static Vector2 Center()
	{
		return new Vector2(0.5f, 0.5f);
	}

	private static Sprite PortraitA()
	{
		return SpriteAt("Assets/GameData/Arts/witch-full.png") ?? SpriteAt("Assets/GameData/Arts/images/asset-105.png");
	}

	private static Sprite PortraitB()
	{
		return SpriteAt("Assets/GameData/Arts/astrologer_oracle_portrait.png") ?? PortraitA();
	}

	private static Sprite PortraitC()
	{
		return SpriteAt("Assets/GameData/Arts/meditation_oracle_portrait.png") ?? PortraitA();
	}

	private static Sprite SpriteAt(string path)
	{
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
	}

	private static void SetLayerRecursive(GameObject go, int layer)
	{
		if (layer >= 0) go.layer = layer;
		foreach (Transform child in go.transform)
			SetLayerRecursive(child.gameObject, layer);
	}
}
