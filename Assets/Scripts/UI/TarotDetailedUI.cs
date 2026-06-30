/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/30/2026 9:56:27 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class TarotDetailedUI : WindowBase
{
	public TarotDetailedUIComponent uiComponent;

	private static DivinationRecordData sPendingRecord;
	private static string sPendingOwnerName = "好友";

	private DivinationRecordData currentRecord;
	private string currentOwnerName = "好友";
	private Button continueChatButton;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TarotDetailedUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("TarotDetailedUI 缺少 UI 组件绑定脚本：TarotDetailedUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		ResolveContinueChatButton();
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		currentRecord = sPendingRecord;
		currentOwnerName = ResolveOwnerName(sPendingOwnerName);
		RenderRecord();
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
	public static TarotDetailedUI Show(DivinationRecordData record, string ownerName = null)
	{
		if (record == null)
		{
			ToastManager.ShowToast("暂无占卜详情");
			return null;
		}

		sPendingRecord = record;
		sPendingOwnerName = ResolveOwnerName(ownerName);
		TarotDetailedUI window = UIModule.Instance.PopUpWindow<TarotDetailedUI>();
		window?.SetRecord(record, ownerName);
		return window;
	}

	public void SetRecord(DivinationRecordData record, string ownerName = null)
	{
		currentRecord = record;
		currentOwnerName = ResolveOwnerName(ownerName);
		if (record != null)
			sPendingRecord = record;
		if (!string.IsNullOrWhiteSpace(ownerName))
			sPendingOwnerName = ResolveOwnerName(ownerName);

		if (gameObject != null && gameObject.activeInHierarchy)
			RenderRecord();
	}

	#endregion

	#region Render
	private void RenderRecord()
	{
		if (uiComponent == null)
			return;

		if (currentRecord == null)
		{
			RenderEmptyState();
			return;
		}

		bool isRelationship = IsRelationshipRecord(currentRecord);
		SetActive(uiComponent.singlePersonTarotType, !isRelationship);
		SetActive(uiComponent.twoPersonTarotType, isRelationship);

		string typeLabel = BuildTypeLabel(currentRecord);
		string sourceLabel = BuildSourceLabel(currentRecord);
		string timeLabel = BuildDisplayDate(currentRecord);

		SetText(uiComponent.singleTypeText, typeLabel);
		SetText(uiComponent.singleSourceText, sourceLabel);
		SetText(uiComponent.singleDivinationTimeText, timeLabel);
		SetText(uiComponent.twoPersonTypeText, typeLabel);
		SetText(uiComponent.twoPersonSourceText, sourceLabel);
		SetText(uiComponent.twoPersonDivinationTimeText, timeLabel);

		RenderPeopleInfo(isRelationship);
		RenderCards(currentRecord);
		SetText(uiComponent.OverallJudgmentText, BuildOverallJudgment(currentRecord));
		RefreshContinueChatButton();
	}

	private void RenderEmptyState()
	{
		SetActive(uiComponent.singlePersonTarotType, true);
		SetActive(uiComponent.twoPersonTarotType, false);
		SetActive(uiComponent.threeCardContainer, false);
		SetActive(uiComponent.tarotCardDesList, false);
		SetText(uiComponent.singleTypeText, "占卜详情");
		SetText(uiComponent.singleSourceText, "占卜历史");
		SetText(uiComponent.singleDivinationTimeText, string.Empty);
		SetText(uiComponent.OverallJudgmentText, "暂无占卜详情");
		RefreshContinueChatButton(false);
	}

	private void RenderPeopleInfo(bool isRelationship)
	{
		if (!isRelationship)
			return;

		string userName = UserDataManager.Instance != null && !string.IsNullOrWhiteSpace(UserDataManager.Instance.UserName)
			? UserDataManager.Instance.UserName.Trim()
			: "我";

		SetText(uiComponent.userName, userName);
		SetText(uiComponent.userInfo, "当前用户");
		SetText(uiComponent.friendName, ResolveOwnerName(currentOwnerName));
		SetText(uiComponent.friendInfo, "占卜对象");
	}

	private void RenderCards(DivinationRecordData record)
	{
		List<LockedCard> cards = record?.lockedCards ?? new List<LockedCard>();
		int visibleCount = GetVisibleCardCount(record, cards.Count);
		bool hasCards = visibleCount > 0;

		SetActive(uiComponent.threeCardContainer, hasCards);
		SetActive(uiComponent.tarotCardDesList, hasCards);

		RenderCardImage(uiComponent.tarot1Image, cards, 0, visibleCount);
		RenderCardImage(uiComponent.tarot2Image, cards, 1, visibleCount);
		RenderCardImage(uiComponent.tarot3Image, cards, 2, visibleCount);

		RenderInfoItem(uiComponent.tarotItem1, cards, 0, visibleCount);
		RenderInfoItem(uiComponent.tarotItem2, cards, 1, visibleCount);
		RenderInfoItem(uiComponent.tarotItem3, cards, 2, visibleCount);
	}

	private void RenderCardImage(Image image, List<LockedCard> cards, int index, int visibleCount)
	{
		if (image == null)
			return;

		bool show = index < visibleCount && index < cards.Count;
		image.gameObject.SetActive(show);
		image.rectTransform.localRotation = Quaternion.identity;
		if (!show)
		{
			image.sprite = null;
			image.enabled = false;
			return;
		}

		LockedCard card = cards[index];
		Sprite sprite = LoadCardSprite(card.cardId);
		image.sprite = sprite;
		image.enabled = sprite != null;
		image.preserveAspect = true;
		image.rectTransform.localRotation = IsUpright(card)
			? Quaternion.identity
			: Quaternion.Euler(0f, 0f, 180f);
	}

	private void RenderInfoItem(DivinationInfoItem item, List<LockedCard> cards, int index, int visibleCount)
	{
		if (item == null)
			return;

		bool show = index < visibleCount && index < cards.Count;
		item.gameObject.SetActive(show);
		if (!show)
			return;

		LockedCard card = cards[index];
		Sprite sprite = LoadCardSprite(card.cardId);
		string cardName = BuildCardDisplayName(card);
		string description = BuildCardDescription(card, index);
		item.SetItemData(sprite, cardName, description);

		if (item.iconImage != null)
		{
			item.iconImage.enabled = sprite != null;
			item.iconImage.preserveAspect = true;
			item.iconImage.rectTransform.localRotation = IsUpright(card)
				? Quaternion.identity
				: Quaternion.Euler(0f, 0f, 180f);
		}
	}

	private void RefreshContinueChatButton(bool canContinue = true)
	{
		Button button = ResolveContinueChatButton();
		if (button == null)
			return;

		bool active = canContinue && currentRecord != null && currentRecord.lockedCards != null && currentRecord.lockedCards.Count > 0;
		button.gameObject.SetActive(active);
		button.interactable = active;

		TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
		if (label != null)
			label.text = IsRelationshipRecord(currentRecord) ? "继续和她聊聊" : "继续聊聊";
	}
	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}

	public void OnContinueChatButtonClick()
	{
		if (currentRecord == null)
		{
			ToastManager.ShowToast("暂无可继续的占卜记录");
			return;
		}

		DialogSystem.Instance?.ActivateReadingFromRecord(currentRecord, DivinationPhase.FollowUp);
		UIModule.Instance.GetWindow<NavigationUI>()?.OpenDialogUI();
		HideWindow();
		UIModule.Instance.PopUpWindow<DialogUI>();
		if (uiComponent != null)
			uiComponent.StartCoroutine(SendContinuePromptNextFrame());
	}
	#endregion

	#region Helpers
	private IEnumerator SendContinuePromptNextFrame()
	{
		yield return null;

		DialogUI dialog = UIModule.Instance.GetWindow<DialogUI>();
		if (dialog == null)
			dialog = UIModule.Instance.PopUpWindow<DialogUI>();
		if (dialog == null)
			yield break;

		string prompt = IsRelationshipRecord(currentRecord)
			? $"继续聊聊我和{ResolveOwnerName(currentOwnerName)}的这次占卜。"
			: "继续聊聊这次占卜。";
		dialog.SendMessageFromExternal(prompt);
	}

	private Button ResolveContinueChatButton()
	{
		if (continueChatButton != null)
			return continueChatButton;

		continueChatButton = uiComponent != null ? uiComponent.ContinueChatButton : null;
		if (continueChatButton == null)
			continueChatButton = FindButtonByName(transform, "ContinueChat");
		if (continueChatButton != null && uiComponent != null && uiComponent.ContinueChatButton == null)
			uiComponent.ContinueChatButton = continueChatButton;
		return continueChatButton;
	}

	private Button FindButtonByName(Transform root, string objectName)
	{
		if (root == null || string.IsNullOrWhiteSpace(objectName))
			return null;
		if (root.name == objectName)
			return root.GetComponent<Button>();

		for (int i = 0; i < root.childCount; i++)
		{
			Button result = FindButtonByName(root.GetChild(i), objectName);
			if (result != null)
				return result;
		}

		return null;
	}

	private int GetVisibleCardCount(DivinationRecordData record, int cardCount)
	{
		if (cardCount <= 0)
			return 0;
		if (IsDailyRecord(record))
			return 1;
		return Mathf.Min(3, cardCount);
	}

	private string BuildTypeLabel(DivinationRecordData record)
	{
		if (record == null)
			return "占卜详情";
		if (IsDailyRecord(record))
			return "每日占卜";
		if (IsRelationshipRecord(record))
			return "双人关系占卜";
		return FirstNonEmpty(record.SpreadLabel, "塔罗占卜");
	}

	private string BuildSourceLabel(DivinationRecordData record)
	{
		if (record == null)
			return "占卜历史";
		if (IsDailyRecord(record))
			return "今日占卜";
		if (IsRelationshipRecord(record))
			return "双人占卜";
		return FirstNonEmpty(record.SpreadLabel, "占卜历史");
	}

	private string BuildDisplayDate(DivinationRecordData record)
	{
		if (record == null)
			return string.Empty;
		if (DateTime.TryParse(record.createdAt, out DateTime createdAt))
			return $"{createdAt.Year}.{createdAt.Month}.{createdAt.Day}";
		return record.DisplayTime;
	}

	private string BuildOverallJudgment(DivinationRecordData record)
	{
		if (record == null)
			return "暂无占卜详情";

		string judge = FirstNonEmpty(record.judgeContent, record.shortVerdict, BuildDefaultJudgeText(record));
		string advice = FirstNonEmpty(record.adviceContent);
		return string.IsNullOrWhiteSpace(advice) ? judge : $"{judge}\n\n行动建议：\n{advice}";
	}

	private string BuildDefaultJudgeText(DivinationRecordData record)
	{
		List<LockedCard> cards = record?.lockedCards;
		if (cards == null || cards.Count == 0)
			return "这条记录暂时没有抽牌数据。";
		if (cards.Count >= 3)
			return $"这组牌由「{BuildCardDisplayName(cards[0])}」「{BuildCardDisplayName(cards[1])}」「{BuildCardDisplayName(cards[2])}」组成，提醒你先看清当下状态，再选择下一步行动。";
		return $"这次牌面以「{BuildCardDisplayName(cards[0])}」为核心，重点是看见此刻真正影响你的能量。";
	}

	private string BuildCardDisplayName(LockedCard card)
	{
		if (card == null)
			return "未知牌";

		TarotCard tarotData = TarotDeck.GetById(card.cardId);
		if (tarotData != null)
			return tarotData.DisplayName(IsUpright(card));

		return TarotDeck.FormatDisplayName(FirstNonEmpty(card.cardName, card.cardId, "未知牌"), IsUpright(card));
	}

	private string BuildCardDescription(LockedCard card, int index)
	{
		if (card == null)
			return string.Empty;

		string position = FirstNonEmpty(card.position, $"第{index + 1}张");
		string orientation = IsUpright(card) ? "正位" : "逆位";
		TarotCard tarotData = TarotDeck.GetById(card.cardId);
		if (tarotData == null || tarotData.keywords == null || tarotData.keywords.Count == 0)
			return $"{position} · {orientation}";

		int keywordCount = Mathf.Min(3, tarotData.keywords.Count);
		string keywords = string.Join("、", tarotData.keywords.GetRange(0, keywordCount));
		return $"{position} · {orientation} · {keywords}";
	}

	private Sprite LoadCardSprite(string cardId)
	{
		if (string.IsNullOrWhiteSpace(cardId))
			return null;
		return TarotSpriteLoader.Load(cardId);
	}

	private bool IsDailyRecord(DivinationRecordData record)
	{
		string scene = (record?.scene ?? "").ToLowerInvariant();
		string spreadKind = (record?.spreadKind ?? "").ToLowerInvariant();
		string readingId = (record?.readingId ?? "").ToLowerInvariant();
		return scene.Contains("daily")
			|| scene.Contains("today")
			|| spreadKind.Contains("daily")
			|| spreadKind.Contains("today")
			|| readingId.Contains("daily")
			|| readingId.Contains("today");
	}

	private bool IsRelationshipRecord(DivinationRecordData record)
	{
		string scene = (record?.scene ?? "").ToLowerInvariant();
		string spreadKind = (record?.spreadKind ?? "").ToLowerInvariant();
		string label = record?.SpreadLabel ?? string.Empty;
		return scene.Contains("friend")
			|| scene.Contains("relationship")
			|| spreadKind.Contains("friend")
			|| spreadKind.Contains("relationship")
			|| label.Contains("双人")
			|| label.Contains("关系");
	}

	private static bool IsUpright(LockedCard card)
	{
		return !string.Equals(card?.orientation, "reversed", StringComparison.OrdinalIgnoreCase);
	}

	private void SetText(TMP_Text text, string value)
	{
		if (text != null)
			text.text = value ?? string.Empty;
	}

	private void SetActive(GameObject target, bool active)
	{
		if (target != null)
			target.SetActive(active);
	}

	private static string ResolveOwnerName(string ownerName)
	{
		return string.IsNullOrWhiteSpace(ownerName) ? "好友" : ownerName.Trim();
	}

	private static string FirstNonEmpty(params string[] values)
	{
		if (values == null)
			return string.Empty;
		foreach (string value in values)
		{
			if (!string.IsNullOrWhiteSpace(value))
				return value.Trim();
		}
		return string.Empty;
	}
	#endregion
}
