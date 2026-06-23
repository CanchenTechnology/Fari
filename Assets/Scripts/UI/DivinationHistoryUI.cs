/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/21/2026 9:52:47 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System;
using System.Collections.Generic;
using GamerFrameWork.OracleRuntime;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using UnityEngine.UI;
using TMPro;

public class DivinationHistoryUI : WindowBase
{
	public DivinationHistoryUIComponent uiComponent;

	/// <summary>从历史列表传入的记录。</summary>
	public static DivinationRecordData SelectedRecord { get; set; }

	private DivinationRecordData _currentRecord;
	private DivinationInfoItem[] _cardItems = Array.Empty<DivinationInfoItem>();
	private bool _isDeleting;
	private string _pendingDeleteReadingId;
	private float _deleteConfirmDeadline;
	private const float DeleteConfirmSeconds = 8f;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<DivinationHistoryUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("DivinationHistoryUI 缺少 UI 组件绑定脚本：DivinationHistoryUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();

		CacheCardItems();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();

		if (_cardItems == null || _cardItems.Length == 0)
			CacheCardItems();

		if (_currentRecord == null)
			_currentRecord = SelectedRecord ?? HistoryUI.SelectedRecord;

		if (_currentRecord != null)
		{
			RenderRecord();
		}
		else
		{
			Debug.LogWarning("[DivinationHistoryUI] 没有可展示的占卜记录");
			RenderEmptyState("暂无占卜记录");
		}
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

	/// <summary>设置当前要展示的历史记录。</summary>
	public void SetRecord(DivinationRecordData record)
	{
		_currentRecord = record;
		SelectedRecord = record;
		HistoryUI.SelectedRecord = record;

		if (gameObject != null && gameObject.activeInHierarchy)
			RenderRecord();
	}

	#endregion

	#region 渲染逻辑

	private void RenderRecord()
	{
		if (_currentRecord == null)
		{
			RenderEmptyState("暂无占卜记录");
			return;
		}

		SetText(uiComponent.MetaTypeValueText, BuildTypeLabel(_currentRecord));
		SetText(uiComponent.MetaTargetValueText, BuildTargetLabel(_currentRecord));
		SetText(uiComponent.MetaOracleValueText, BuildOracleLabel(_currentRecord));
		SetText(uiComponent.MetaTimeValueText, BuildDetailTime(_currentRecord));
		SetText(uiComponent.MetaStatusValueText, IsRecordComplete(_currentRecord) ? "已完成" : "进行中");

		RenderCards(_currentRecord);

		SetText(uiComponent.OracleTextText, FirstNonEmpty(_currentRecord.judgeContent, _currentRecord.shortVerdict, BuildDefaultJudgeText(_currentRecord)));
		SetText(uiComponent.AdviceTextText, FirstNonEmpty(_currentRecord.adviceContent, BuildDefaultAdviceText(_currentRecord)));
		SetText(uiComponent.SummaryTextText, BuildSummaryText(_currentRecord));

		SetButtonLabel(uiComponent.SaveToDiaryButton, "保存到日记");

		if (uiComponent.RecordScrollContainerScrollRect != null)
			uiComponent.RecordScrollContainerScrollRect.verticalNormalizedPosition = 1f;

		Debug.Log($"[DivinationHistoryUI] 已渲染占卜记录: {_currentRecord.readingId}");
	}

	private void RenderEmptyState(string message)
	{
		SetText(uiComponent.MetaTypeValueText, "");
		SetText(uiComponent.MetaTargetValueText, "");
		SetText(uiComponent.MetaOracleValueText, "");
		SetText(uiComponent.MetaTimeValueText, "");
		SetText(uiComponent.MetaStatusValueText, "");
		SetText(uiComponent.OracleTextText, message);
		SetText(uiComponent.AdviceTextText, "");
		SetText(uiComponent.SummaryTextText, "");
		HideAllCards();
	}

	private void CacheCardItems()
	{
		_cardItems = gameObject.GetComponentsInChildren<DivinationInfoItem>(true);
		if (_cardItems == null)
		{
			_cardItems = Array.Empty<DivinationInfoItem>();
			return;
		}

		Array.Sort(_cardItems, (left, right) => GetCardItemOrder(left).CompareTo(GetCardItemOrder(right)));
	}

	private int GetCardItemOrder(DivinationInfoItem item)
	{
		if (item == null) return int.MaxValue;

		string itemName = item.gameObject.name;
		if (itemName == "Card") return 0;
		if (itemName.EndsWith("_1", StringComparison.Ordinal)) return 1;
		if (itemName.EndsWith("_2", StringComparison.Ordinal)) return 2;

		return item.transform.GetSiblingIndex();
	}

	private void RenderCards(DivinationRecordData record)
	{
		if (_cardItems == null || _cardItems.Length == 0)
			CacheCardItems();

		List<LockedCard> cards = record?.lockedCards;
		if (cards == null || cards.Count == 0)
		{
			HideAllCards();
			return;
		}

		int visibleCount = GetVisibleCardCount(record, cards.Count);
		for (int i = 0; i < _cardItems.Length; i++)
		{
			DivinationInfoItem item = _cardItems[i];
			if (item == null) continue;

			if (i < visibleCount && i < cards.Count)
				RenderCardItem(item, cards[i], i);
			else
				item.gameObject.SetActive(false);
		}
	}

	private int GetVisibleCardCount(DivinationRecordData record, int cardCount)
	{
		if (cardCount <= 0) return 0;
		if (IsDailyRecord(record)) return 1;
		if (IsRelationshipRecord(record)) return Mathf.Min(3, cardCount);

		return Mathf.Min(cardCount, _cardItems?.Length ?? cardCount);
	}

	private void RenderCardItem(DivinationInfoItem item, LockedCard card, int index)
	{
		item.gameObject.SetActive(true);

		Sprite iconSprite = TarotSpriteLoader.Load(card.cardId);
		bool isUpright = card.orientation != "reversed";
		string cardName = BuildCardDisplayName(card, isUpright);
		string description = BuildCardDescription(card, index, isUpright);

		item.SetItemData(iconSprite, cardName, description);

		if (item.iconImage != null)
		{
			item.iconImage.enabled = iconSprite != null;
			item.iconImage.rectTransform.localRotation = isUpright
				? Quaternion.identity
				: Quaternion.Euler(0f, 0f, 180f);
		}
	}

	private void HideAllCards()
	{
		if (_cardItems == null) return;
		foreach (DivinationInfoItem item in _cardItems)
		{
			if (item != null)
				item.gameObject.SetActive(false);
		}
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnContinueAskButtonClick()
	{
		if (_currentRecord == null)
		{
			ToastManager.ShowToast("暂无可追问的占卜记录");
			return;
		}

		DialogSystem.Instance?.ActivateReadingFromRecord(_currentRecord, DivinationPhase.FollowUp);
		RestoreEngineSession(_currentRecord);

		HideWindow();
		UIModule.Instance.PopUpWindow<DialogUI>();
	}
	public void OnSaveToDiaryButtonClick()
	{
		if (_currentRecord == null)
		{
			ToastManager.ShowToast("暂无可保存的占卜记录");
			return;
		}

		DivinationRecordFirestore firestore = GetRecordStore();
		if (firestore != null)
		{
			firestore.SaveRecord(_currentRecord, success =>
			{
				ToastManager.ShowToast(success ? "已保存到历史" : "保存失败，请稍后再试");
				if (!success)
					Debug.LogWarning("[DivinationHistoryUI] 云端保存失败");
			});
		}
		else
		{
			ToastManager.ShowToast("历史服务暂不可用");
		}
	}
	public void OnShareResultButtonClick()
	{
		if (_currentRecord == null) return;

		string shareText = BuildShareText(_currentRecord);
#if UNITY_IOS && !UNITY_EDITOR
		NativeIOSShare.ShareText(shareText);
#elif UNITY_ANDROID && !UNITY_EDITOR
		ShareToAndroid(shareText);
#else
		GUIUtility.systemCopyBuffer = shareText;
		ToastManager.ShowToast("分享内容已复制");
#endif
	}
	public void OnDeleteRecordButtonClick()
	{
		if (_currentRecord == null || _isDeleting) return;

		string readingId = _currentRecord.readingId;
		if (string.IsNullOrWhiteSpace(readingId))
		{
			ToastManager.ShowToast("这条记录缺少 ID，无法删除");
			return;
		}

		if (_pendingDeleteReadingId != readingId || Time.time > _deleteConfirmDeadline)
		{
			_pendingDeleteReadingId = readingId;
			_deleteConfirmDeadline = Time.time + DeleteConfirmSeconds;
			ToastManager.ShowToast("再次点击删除这条占卜记录");
			return;
		}

		_isDeleting = true;
		_pendingDeleteReadingId = null;

		DivinationRecordFirestore firestore = GetRecordStore();
		if (firestore == null)
		{
			_isDeleting = false;
			ToastManager.ShowToast("历史服务暂不可用");
			return;
		}

		firestore.DeleteRecord(readingId, success =>
		{
			_isDeleting = false;

			if (success)
			{
				ToastManager.ShowToast("占卜记录已删除");
				if (HistoryUI.SelectedRecord?.readingId == readingId)
					HistoryUI.SelectedRecord = null;
				if (SelectedRecord?.readingId == readingId)
					SelectedRecord = null;

				UIModule.Instance.GetWindow<HistoryUI>()?.RefreshList();
				HideWindow();
			}
			else
			{
				ToastManager.ShowToast("删除失败，请稍后再试");
			}
		});
	}
	#endregion

	#region 文案构建

	private string BuildTypeLabel(DivinationRecordData record)
	{
		if (record == null) return "";
		if (IsDailyRecord(record)) return "今日占卜";
		if (IsRelationshipRecord(record)) return "双人占卜";

		string label = record.SpreadLabel;
		return string.IsNullOrWhiteSpace(label) ? "塔罗占卜" : label;
	}

	private string BuildTargetLabel(DivinationRecordData record)
	{
		if (record == null) return "";
		if (IsDailyRecord(record)) return "自己";
		if (IsRelationshipRecord(record)) return ExtractRelationshipTarget(record);
		return "自己";
	}

	private string ExtractRelationshipTarget(DivinationRecordData record)
	{
		string verdict = FirstNonEmpty(record.shortVerdict, record.judgeContent);
		int start = verdict.IndexOf('：');
		int end = verdict.IndexOf('，');
		if (start >= 0 && end > start)
		{
			string names = verdict.Substring(start + 1, end - start - 1).Trim();
			if (!string.IsNullOrWhiteSpace(names))
				return names;
		}

		return "关系对象";
	}

	private string BuildOracleLabel(DivinationRecordData record)
	{
		string oracleId = (record?.oracleId ?? "").ToLowerInvariant();
		if (oracleId.Contains("astrology")) return "占星师";
		if (oracleId.Contains("sage") || oracleId.Contains("meditation")) return "冥想引导";
		if (oracleId.Contains("tarot")) return "塔罗师";
		if (!string.IsNullOrWhiteSpace(record?.oracleId)) return record.oracleId;
		return "塔罗师";
	}

	private string BuildDetailTime(DivinationRecordData record)
	{
		if (record == null) return "";
		if (DateTime.TryParse(record.createdAt, out DateTime createdAt))
			return createdAt.ToString("yyyy.MM.dd HH:mm");
		return record.DisplayTime;
	}

	private string BuildCardDisplayName(LockedCard card, bool isUpright)
	{
		if (card == null) return "未知牌";

		TarotCard tarotData = TarotDeck.GetById(card.cardId);
		if (tarotData != null)
			return tarotData.DisplayName(isUpright);

		string name = string.IsNullOrWhiteSpace(card.cardName) ? "未知牌" : card.cardName;
		return $"{name}（{(isUpright ? "正位" : "逆位")}）";
	}

	private string BuildCardDescription(LockedCard card, int index, bool isUpright)
	{
		if (card == null) return "";

		string position = FirstNonEmpty(card.position, $"第{index + 1}张");
		string orientation = isUpright ? "正位" : "逆位";
		TarotCard tarotData = TarotDeck.GetById(card.cardId);

		if (tarotData == null || tarotData.keywords == null || tarotData.keywords.Count == 0)
			return $"{position} · {orientation}";

		int keywordCount = Mathf.Min(3, tarotData.keywords.Count);
		string keywords = string.Join("、", tarotData.keywords.GetRange(0, keywordCount));
		return $"{position} · {orientation} · {keywords}";
	}

	private string BuildDefaultJudgeText(DivinationRecordData record)
	{
		List<LockedCard> cards = record?.lockedCards;
		if (cards == null || cards.Count == 0)
			return "这条记录暂时没有抽牌数据。";

		if (IsDailyRecord(record))
			return $"今日牌「{cards[0].cardName}」提醒你先看见当下的能量，再选择一个小而确定的行动。";

		if (cards.Count >= 3)
		{
			return $"这次牌面由「{cards[0].cardName}」「{cards[1].cardName}」「{cards[2].cardName}」组成。"
				+ "第一张牌指出当下状态，第二张牌揭示阻碍，第三张牌给出下一步的方向。";
		}

		return $"这次牌面以「{cards[0].cardName}」为核心，重点是看清此刻真正影响你的因素。";
	}

	private string BuildDefaultAdviceText(DivinationRecordData record)
	{
		if (record?.lockedCards == null || record.lockedCards.Count == 0)
			return "先回到问题本身，确认你真正想知道的是什么。";

		return "1. 先观察行动，不要追问解释。\n2. 今晚不要反复翻看旧对话。\n3. 三天后再回来记录变化。";
	}

	private string BuildSummaryText(DivinationRecordData record)
	{
		if (record == null) return "";

		string question = FirstNonEmpty(record.question, "这次占卜");
		string cards = record.CardsSummary;
		string verdict = FirstNonEmpty(record.shortVerdict, record.judgeContent);

		if (!string.IsNullOrWhiteSpace(verdict))
			return $"你围绕「{question}」进行了占卜。{verdict}";

		if (!string.IsNullOrWhiteSpace(cards))
			return $"你围绕「{question}」抽到了：{cards}。";

		return $"你围绕「{question}」留下了这条占卜记录。";
	}

	private string BuildShareText(DivinationRecordData record)
	{
		string text = $"{BuildTypeLabel(record)}\n";
		text += $"问题：{FirstNonEmpty(record.question, "未命名占卜")}\n";
		text += $"时间：{BuildDetailTime(record)}\n";

		if (record.lockedCards != null && record.lockedCards.Count > 0)
		{
			text += "抽到的牌：";
			int count = GetVisibleCardCount(record, record.lockedCards.Count);
			for (int i = 0; i < count; i++)
			{
				LockedCard card = record.lockedCards[i];
				bool isUpright = card.orientation != "reversed";
				text += $"\n{i + 1}. {FirstNonEmpty(card.position, $"第{i + 1}张")}：{BuildCardDisplayName(card, isUpright)}";
			}
		}

		string verdict = FirstNonEmpty(record.judgeContent, record.shortVerdict);
		if (!string.IsNullOrWhiteSpace(verdict))
			text += $"\n\n神谕判词：{verdict}";

		if (!string.IsNullOrWhiteSpace(record.adviceContent))
			text += $"\n\n行动建议：{record.adviceContent}";

		text += "\n\n-- Moonly";
		return text;
	}

	private bool IsDailyRecord(DivinationRecordData record)
	{
		string scene = (record?.scene ?? "").ToLowerInvariant();
		string spreadKind = (record?.spreadKind ?? "").ToLowerInvariant();
		string readingId = (record?.readingId ?? "").ToLowerInvariant();
		string label = record?.SpreadLabel ?? "";

		return scene.Contains("daily")
			|| scene.Contains("today")
			|| spreadKind.Contains("daily")
			|| spreadKind.Contains("today")
			|| readingId.Contains("daily")
			|| readingId.Contains("today")
			|| label.Contains("今日");
	}

	private bool IsRelationshipRecord(DivinationRecordData record)
	{
		string scene = (record?.scene ?? "").ToLowerInvariant();
		string spreadKind = (record?.spreadKind ?? "").ToLowerInvariant();
		string label = record?.SpreadLabel ?? "";

		return scene.Contains("friend")
			|| scene.Contains("relationship")
			|| spreadKind.Contains("friend")
			|| spreadKind.Contains("relationship")
			|| label.Contains("双人")
			|| label.Contains("关系");
	}

	private bool IsRecordComplete(DivinationRecordData record)
	{
		if (record == null) return false;
		if (!string.IsNullOrWhiteSpace(record.shortVerdict)
			|| !string.IsNullOrWhiteSpace(record.judgeContent)
			|| !string.IsNullOrWhiteSpace(record.adviceContent))
			return true;
		return record.lockedCards != null && record.lockedCards.Count > 0;
	}

	private static string FirstNonEmpty(params string[] values)
	{
		if (values == null) return "";
		foreach (string value in values)
		{
			if (!string.IsNullOrWhiteSpace(value))
				return value.Trim();
		}
		return "";
	}

	#endregion

	#region 辅助方法

	private void RestoreEngineSession(DivinationRecordData record)
	{
		if (record == null || DivinationEngine.Instance == null) return;

		ReadingLock readingLock = null;
		if (record.lockedCards != null && record.lockedCards.Count > 0)
		{
			readingLock = new ReadingLock
			{
				readingId = record.readingId,
				readingType = record.spreadKind,
				allowedCards = record.lockedCards,
				locked = true
			};
		}

		DivinationEngine.Instance.RestoreSession(new DivinationSession
		{
			readingId = record.readingId,
			question = record.question,
			scene = record.scene,
			spreadKind = record.spreadKind,
			lockedCards = record.lockedCards,
			readingLock = readingLock,
			shortVerdict = record.shortVerdict,
			judgeContent = record.judgeContent,
			adviceContent = record.adviceContent,
			topics = record.topics,
			phase = DivinationPhase.FollowUp,
			createdAt = FirstNonEmpty(record.createdAt, DateTime.Now.ToString("o"))
		});
	}

	private DivinationRecordFirestore GetRecordStore()
	{
		DivinationRecordFirestore firestore = DivinationRecordFirestore.Instance;
		if (firestore != null)
			return firestore;

		GameObject go = new GameObject("DivinationRecordFirestore");
		return go.AddComponent<DivinationRecordFirestore>();
	}

	private void SetText(TMP_Text text, string value)
	{
		if (text != null)
			text.text = value ?? "";
	}

	private void SetButtonLabel(Button button, string label)
	{
		if (button == null) return;
		button.interactable = true;
		TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
		if (text != null)
			text.text = label ?? "";
	}

	private void LateUpdate()
	{
		if (uiComponent?.SaveToDiaryButton != null && !uiComponent.SaveToDiaryButton.interactable)
			uiComponent.SaveToDiaryButton.interactable = true;
	}

#if UNITY_ANDROID && !UNITY_EDITOR
	private void ShareToAndroid(string text)
	{
		using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
		using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
		using (var intentClass = new AndroidJavaClass("android.content.Intent"))
		using (var intent = new AndroidJavaObject("android.content.Intent"))
		{
			intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
			intent.Call<AndroidJavaObject>("setType", "text/plain");
			intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
			currentActivity.Call("startActivity",
				intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "分享占卜结果"));
		}
	}
#endif

	#endregion
}
