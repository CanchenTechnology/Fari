/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/30/2026 4:23:26 PM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;

public class TwoPersonDivinationResultUI : WindowBase
{
	private static RelationshipDivinationRecord sPendingRecord;
	private static FriendDataManager.FriendData sPendingFriend;

	public TwoPersonDivinationResultUIComponent uiComponent;
	private RelationshipDivinationRecord currentRecord;
	private FriendDataManager.FriendData currentFriend;
	private DivinationRecordData currentRecordData;
	private int avatarRequestVersion;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<TwoPersonDivinationResultUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("TwoPersonDivinationResultUI 缺少 UI 组件绑定脚本：TwoPersonDivinationResultUIComponent");
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
		currentRecord = sPendingRecord ?? RelationshipDivinationFlow.CurrentRecord;
		currentFriend = sPendingFriend ?? RelationshipDivinationFlow.CurrentFriend;
		currentRecordData = currentRecord != null ? RelationshipDivinationFlow.BuildDivinationRecord(currentRecord) : null;
		avatarRequestVersion++;
		if (currentRecordData != null)
			DialogSystem.Instance?.ActivateReadingFromRecord(currentRecordData, DivinationPhase.Completed);
		Render();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		avatarRequestVersion++;
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		base.OnDestroy();
	}
	#endregion

	#region API Function
	public static TwoPersonDivinationResultUI Show(RelationshipDivinationRecord record, FriendDataManager.FriendData friend = null)
	{
		if (record == null)
		{
			ToastManager.ShowToast("占卜结果不存在");
			return null;
		}

		sPendingRecord = record;
		sPendingFriend = friend;
		TwoPersonDivinationResultUI window = UIModule.Instance.PopUpWindow<TwoPersonDivinationResultUI>();
		window?.SetRecord(record, friend);
		return window;
	}

	public void SetRecord(RelationshipDivinationRecord record, FriendDataManager.FriendData friend = null)
	{
		if (record == null)
			return;

		sPendingRecord = record;
		sPendingFriend = friend;
		currentRecord = record;
		currentFriend = friend ?? currentFriend ?? RelationshipDivinationFlow.CurrentFriend;
		currentRecordData = RelationshipDivinationFlow.BuildDivinationRecord(record);
		if (gameObject != null && gameObject.activeInHierarchy)
			Render();
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnContinueChatButtonClick()
	{
		ContinueWithTopic(string.Empty);
	}
	#endregion

	private void Render()
	{
		if (uiComponent == null)
			return;

		if (currentRecord == null)
		{
			ToastManager.ShowToast("占卜结果不存在");
			return;
		}

		currentRecordData ??= RelationshipDivinationFlow.BuildDivinationRecord(currentRecord);
		RenderFriendInfo();
		RenderMeta();
		RenderCards();
		RenderTexts();
		RenderTopics();
	}

	private void RenderFriendInfo()
	{
		string uid = RelationshipDivinationFlow.GetCurrentUid();
		string friendName = currentFriend != null
			? RelationshipDivinationFlow.GetFriendName(currentFriend)
			: RelationshipDivinationFlow.GetOtherName(currentRecord, uid);

		SetText(uiComponent.friendName, friendName);
		SetText(uiComponent.drawCardFriendName1, friendName);
		SetText(uiComponent.drawCardFriendName2, friendName);

		Sprite avatar = currentFriend != null
			? FriendAvatarImageUtility.ResolveFriendAvatar(currentFriend, uiComponent.friendHead)
			: null;
		FriendAvatarImageUtility.ApplyAvatar(uiComponent.friendHead, avatar);
		LoadRemoteAvatarIfNeeded();
	}

	private void RenderMeta()
	{
		SetText(uiComponent.divinationType, ExtractDirectionLabel(currentRecord?.question));
		SetText(uiComponent.divinationTime, FormatDisplayTime(currentRecord?.completedAt, currentRecord?.updatedAt, currentRecord?.createdAt));
	}

	private void RenderCards()
	{
		RelationshipDivinationCard myCard = RelationshipDivinationFlow.GetMyPrivateCard(currentRecord);
		RelationshipDivinationCard sharedCard = currentRecord?.SharedCard;
		RelationshipDivinationCard friendCard = RelationshipDivinationFlow.GetFriendPrivateCard(currentRecord);

		ApplyCardImage(uiComponent.yourTarotImage, myCard);
		ApplyCardImage(uiComponent.resultTarotImage, sharedCard);
		ApplyCardImage(uiComponent.herTarotImage, friendCard);

		SetInfoItem(uiComponent.tarotItem1, myCard, "你的牌");
		SetInfoItem(uiComponent.tarotItem2, sharedCard, "共同牌");
		SetInfoItem(uiComponent.tarotItem3, friendCard, BuildFriendCardLabel());
	}

	private void RenderTexts()
	{
		RelationshipDivinationCard myCard = RelationshipDivinationFlow.GetMyPrivateCard(currentRecord);
		RelationshipDivinationCard sharedCard = currentRecord?.SharedCard;
		RelationshipDivinationCard friendCard = RelationshipDivinationFlow.GetFriendPrivateCard(currentRecord);

		SetText(uiComponent.OverallJudgmentText, FirstNonEmpty(
			currentRecordData?.judgeContent,
			currentRecordData?.shortVerdict,
			BuildOverallJudgment(myCard, sharedCard, friendCard)));
		SetText(uiComponent.ActionSectionText, FirstNonEmpty(
			currentRecordData?.adviceContent,
			BuildActionAdvice(sharedCard)));
	}

	private void RenderTopics()
	{
		List<string> topics = BuildTopics();
		QuestRowItem[] items =
		{
			uiComponent.topicItem1,
			uiComponent.topicItem2,
			uiComponent.topicItem3,
			uiComponent.topicItem4
		};

		for (int i = 0; i < items.Length; i++)
		{
			QuestRowItem item = items[i];
			if (item == null) continue;

			bool visible = i < topics.Count;
			item.gameObject.SetActive(visible);
			if (visible)
			{
				string topic = topics[i];
				item.SetData(topic, ContinueWithTopic);
			}
		}
	}

	private void ApplyCardImage(Image target, RelationshipDivinationCard card)
	{
		if (target == null)
			return;

		Sprite sprite = RelationshipDivinationFlow.LoadCardSprite(card);
		target.enabled = sprite != null;
		if (sprite != null)
		{
			target.sprite = sprite;
		}
		target.rectTransform.localRotation = card != null && !card.IsUpright
			? Quaternion.Euler(0f, 0f, 180f)
			: Quaternion.identity;
	}

	private void SetInfoItem(DivinationInfoItem item, RelationshipDivinationCard card, string fallbackPosition)
	{
		if (item == null)
			return;

		if (card == null)
		{
			item.gameObject.SetActive(false);
			return;
		}

		item.gameObject.SetActive(true);
		Sprite sprite = RelationshipDivinationFlow.LoadCardSprite(card);
		item.SetItemData(sprite, card.DisplayName, BuildCardDescription(card, fallbackPosition));
		if (item.iconImage != null)
		{
			item.iconImage.enabled = sprite != null;
			item.iconImage.rectTransform.localRotation = card.IsUpright
				? Quaternion.identity
				: Quaternion.Euler(0f, 0f, 180f);
		}
	}

	private string BuildCardDescription(RelationshipDivinationCard card, string fallbackPosition)
	{
		if (card == null)
			return string.Empty;

		TarotCard tarot = TarotDeck.GetById(card.cardId);
		string position = FirstNonEmpty(card.position, fallbackPosition);
		string keywords = tarot != null && tarot.keywords != null && tarot.keywords.Count > 0
			? string.Join("、", tarot.keywords)
			: "关系、选择、回应";
		string direction = card.IsUpright ? "顺着牌面的能量表达真实感受" : "先看见被压住的担心，再决定如何回应";
		return $"「{card.DisplayName}」落在「{position}」位置，关键词是{keywords}。这张牌提醒你{direction}。";
	}

	private string BuildOverallJudgment(RelationshipDivinationCard myCard, RelationshipDivinationCard sharedCard, RelationshipDivinationCard friendCard)
	{
		string friendName = currentFriend != null
			? RelationshipDivinationFlow.GetFriendName(currentFriend)
			: RelationshipDivinationFlow.GetOtherName(currentRecord, RelationshipDivinationFlow.GetCurrentUid());
		return $"这次双人占卜围绕「{FirstNonEmpty(ExtractDirectionLabel(currentRecord?.question), "关系趋势")}」展开。"
			+ $"你的牌「{FormatCardName(myCard)}」、共同牌「{FormatCardName(sharedCard)}」和 {friendName} 的牌「{FormatCardName(friendCard)}」"
			+ "一起显示：这段关系需要在真实表达和耐心倾听之间找到新的平衡。";
	}

	private string BuildActionAdvice(RelationshipDivinationCard sharedCard)
	{
		string sharedName = FormatCardName(sharedCard);
		return $"行动建议：先从一次轻量、明确的沟通开始。围绕共同牌「{sharedName}」给出的提醒，"
			+ "把感受说清楚，把期待说小一点，让对方有空间回应。";
	}

	private List<string> BuildTopics()
	{
		List<string> topics = new List<string>();
		if (currentRecordData?.topics != null)
		{
			foreach (string topic in currentRecordData.topics)
			{
				if (!string.IsNullOrWhiteSpace(topic))
					topics.Add(topic.Trim());
				if (topics.Count >= 4)
					return topics;
			}
		}

		string friendName = currentFriend != null
			? RelationshipDivinationFlow.GetFriendName(currentFriend)
			: RelationshipDivinationFlow.GetOtherName(currentRecord, RelationshipDivinationFlow.GetCurrentUid());
		topics.Add($"我和{friendName}下一步最适合怎么沟通？");
		topics.Add("这段关系里我最需要看见什么？");
		topics.Add("对方现在可能更在意什么？");
		topics.Add("如果继续推进，最稳的行动是什么？");
		return topics;
	}

	private void ContinueWithTopic(string topic)
	{
		if (currentRecordData == null)
		{
			ToastManager.ShowToast("暂无可继续解读的双人占卜结果");
			return;
		}

		DialogSystem.Instance?.ActivateReadingFromRecord(currentRecordData, DivinationPhase.FollowUp);
		HideWindow();
		UIModule.Instance.PopUpWindow<DialogUI>();
		if (!string.IsNullOrWhiteSpace(topic))
			uiComponent.StartCoroutine(SendTopicToDialogNextFrame(topic));
	}

	private IEnumerator SendTopicToDialogNextFrame(string topic)
	{
		yield return null;

		DialogUI dialog = UIModule.Instance.GetWindow<DialogUI>();
		if (dialog == null)
			dialog = UIModule.Instance.PopUpWindow<DialogUI>();

		if (dialog != null)
			dialog.SendMessageFromExternal(topic);
	}

	private void LoadRemoteAvatarIfNeeded()
	{
		if (currentFriend == null
			|| currentFriend.headSprite != null
			|| string.IsNullOrWhiteSpace(currentFriend.photoUrl)
			|| uiComponent == null)
			return;

		int requestId = ++avatarRequestVersion;
		FriendDataManager.FriendData friend = currentFriend;
		uiComponent.StartCoroutine(FriendAvatarImageUtility.LoadUserSpriteFromUrlCoroutine(currentFriend.name, currentFriend.photoUrl, sprite =>
		{
			if (requestId != avatarRequestVersion || currentFriend != friend || sprite == null)
				return;

			currentFriend.headSprite = sprite;
			FriendAvatarImageUtility.ApplyAvatar(uiComponent.friendHead, sprite);
		}));
	}

	private string BuildFriendCardLabel()
	{
		string friendName = currentFriend != null
			? RelationshipDivinationFlow.GetFriendName(currentFriend)
			: RelationshipDivinationFlow.GetOtherName(currentRecord, RelationshipDivinationFlow.GetCurrentUid());
		return $"{friendName}的牌";
	}

	private string ExtractDirectionLabel(string question)
	{
		if (!string.IsNullOrWhiteSpace(question))
		{
			int start = question.IndexOf('「');
			int end = question.IndexOf('」');
			if (start >= 0 && end > start)
				return question.Substring(start + 1, end - start - 1).Trim();
		}

		return "双人占卜";
	}

	private string FormatDisplayTime(params string[] values)
	{
		foreach (string value in values)
		{
			if (string.IsNullOrWhiteSpace(value))
				continue;
			if (DateTime.TryParse(value, out DateTime parsed))
				return $"{parsed.Year}.{parsed.Month}.{parsed.Day}";
			return value.Trim();
		}

		return DateTime.Now.ToString("yyyy.M.d");
	}

	private string FormatCardName(RelationshipDivinationCard card)
	{
		return card != null ? card.DisplayName : "未知牌";
	}

	private void SetText(TMPro.TMP_Text text, string value)
	{
		if (text != null)
			text.text = value ?? string.Empty;
	}

	private string FirstNonEmpty(params string[] values)
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
}
