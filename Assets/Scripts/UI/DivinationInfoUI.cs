/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/17/2026 10:11:11 AM
 * Description: UI 表现层 —— 占卜解读详情页
 * 展示完整的塔罗占卜结果：卡牌、评判、建议、追问话题
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;

public class DivinationInfoUI : WindowBase
{
	public DivinationInfoUIComponent uiComponent;

	/// <summary>从其他页面传入的占卜记录（静态传递）</summary>
	public static DivinationRecordData SelectedRecord { get; set; }

	/// <summary>当前展示的记录数据</summary>
	private DivinationRecordData _currentRecord;

	/// <summary>追问话题列表（从记录或默认生成）</summary>
	private List<string> _topics = new List<string>();

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<DivinationInfoUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();

		// 加载数据
		_currentRecord = SelectedRecord;
		SelectedRecord = null;

		// 如果无传入记录，尝试从当前占卜会话获取
		if (_currentRecord == null)
		{
			_currentRecord = BuildRecordFromSession();
		}

		if (_currentRecord != null)
		{
			DialogSystem.Instance?.ActivateReadingFromRecord(_currentRecord, HasVerdict(_currentRecord)
				? DivinationPhase.Completed
				: DivinationPhase.CardsLocked);
			RenderRecord();
		}
		else
		{
			Debug.LogWarning("[DivinationInfoUI] 无占卜数据可展示");
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

	/// <summary>
	/// 设置要展示的占卜记录
	/// </summary>
	public void SetRecord(DivinationRecordData record)
	{
		_currentRecord = record;
		if (gameObject != null && gameObject.activeInHierarchy)
			RenderRecord();
	}

	#endregion

	#region 渲染逻辑

	/// <summary>
	/// 完整渲染占卜记录
	/// </summary>
	private void RenderRecord()
	{
		if (_currentRecord == null) return;
		var c = uiComponent;

		// ---- 卡牌展示（TarotItem） ----
		var cards = _currentRecord.lockedCards;
		if (cards != null && cards.Count > 0)
		{
			RenderTarotItem(c.Card1TarotItemTarotItem, cards, 0);
			RenderTarotItem(c.Card2TarotItemTarotItem, cards, 1);
			RenderTarotItem(c.Card3TarotItemTarotItem, cards, 2);
		}

		// ---- 卡牌详细信息（DivinationInfoItem） ----
		if (cards != null && cards.Count > 0)
		{
			RenderInfoItem(c.Item1DivinationInfoItemDivinationInfoItem, cards, 0);
			RenderInfoItem(c.Item2DivinationInfoItemDivinationInfoItem, cards, 1);
			RenderInfoItem(c.Item3DivinationInfoItemDivinationInfoItem, cards, 2);
		}

		// ---- 评判内容 ----
		string judgeText = FirstNonEmpty(_currentRecord.judgeContent, _currentRecord.shortVerdict);
		if (string.IsNullOrEmpty(judgeText))
			judgeText = BuildDefaultJudgeText();
		if (c.JudgeContentText != null)
		{
			c.JudgeContentText.text = string.IsNullOrEmpty(judgeText) ? "暂无评判内容" : judgeText;
		}

		// ---- 建议内容 ----
		string adviceText = _currentRecord.adviceContent ?? "";
		if (string.IsNullOrEmpty(adviceText))
			adviceText = BuildDefaultAdviceText();
		if (c.AdviceContentText != null)
		{
			c.AdviceContentText.text = string.IsNullOrEmpty(adviceText) ? "暂无建议" : adviceText;
		}

		// ---- 追问话题按钮 ----
		_topics = _currentRecord.topics ?? new List<string>();
		// 如果记录中没有话题，生成默认话题
		if (_topics.Count == 0)
		{
			_topics = GenerateDefaultTopics();
		}

		SetupTopicRow(c.Question1QuestRowItem, 0);
		SetupTopicRow(c.Question2QuestRowItem, 1);
		SetupTopicRow(c.Question3QuestRowItem, 2);

		Debug.Log($"[DivinationInfoUI] 占卜详情已渲染: {_currentRecord.readingId}");
	}

	/// <summary>
	/// 渲染单个塔罗牌项
	/// </summary>
	private void RenderTarotItem(TarotItem item, List<LockedCard> cards, int index)
	{
		if (item == null) return;

		if (index < cards.Count)
		{
			var card = cards[index];
			item.gameObject.SetActive(true);

			// 加载卡牌图片
			Sprite cardSprite = TarotSpriteLoader.Load(card.cardId);
			bool isUpright = card.orientation != "reversed";

			// 获取卡牌中文名
			string displayName;
			var tarotData = TarotDeck.GetById(card.cardId);
			if (tarotData != null)
			{
				displayName = tarotData.DisplayName(isUpright);
			}
			else
			{
				displayName = isUpright ? $"{card.cardName}（正位）" : $"{card.cardName}（逆位）";
			}

			// 设置标签（正位/逆位 + 位置名）
			string tag = isUpright ? "正位" : "逆位";
			if (!string.IsNullOrEmpty(card.position))
				tag = $"{card.position} · {tag}";

			item.SetItemData(cardSprite, displayName, tag);
		}
		else
		{
			item.gameObject.SetActive(false);
		}
	}

	/// <summary>
	/// 渲染单个卡牌详细信息项
	/// </summary>
	private void RenderInfoItem(DivinationInfoItem item, List<LockedCard> cards, int index)
	{
		if (item == null) return;

		if (index < cards.Count)
		{
			var card = cards[index];
			item.gameObject.SetActive(true);

			Sprite iconSprite = TarotSpriteLoader.Load(card.cardId);
			bool isUpright = card.orientation != "reversed";

			// 获取完整卡牌数据
			var tarotData = TarotDeck.GetById(card.cardId);
			string cardNameStr;
			string description;

			if (tarotData != null)
			{
				string position = string.IsNullOrEmpty(card.position) ? $"第{index + 1}张" : card.position;
				cardNameStr = $"{position} · {tarotData.DisplayName(isUpright)}";
				string keywords = string.Join("、", tarotData.keywords.GetRange(0, Mathf.Min(3, tarotData.keywords.Count)));
				string prompt = GetSpreadPositionPrompt(index);
				string orientationDesc = isUpright
					? "正位提醒你顺着这股能量行动。"
					: "逆位提醒你先看见阻滞、误解或内在抗拒。";
				description = string.IsNullOrEmpty(prompt)
					? $"{keywords}。{orientationDesc}"
					: $"{prompt}关键词：{keywords}。{orientationDesc}";
			}
			else
			{
				cardNameStr = card.cardName ?? "未知牌";
				description = card.orientation == "reversed" ? "逆位" : "正位";
			}

			item.SetItemData(iconSprite, cardNameStr, description);
		}
		else
		{
			item.gameObject.SetActive(false);
		}
	}

	/// <summary>
	/// 设置追问话题按钮
	/// </summary>
	private void SetupTopicRow(QuestRowItem item, int topicIndex)
	{
		if (item == null) return;

		if (topicIndex < _topics.Count)
		{
			item.gameObject.SetActive(true);
			item.SetData(_topics[topicIndex], _ => ContinueWithTopic(topicIndex));
		}
		else
		{
			item.gameObject.SetActive(false);
		}
	}

	/// <summary>
	/// 根据占卜场景生成默认追问话题
	/// </summary>
	private List<string> GenerateDefaultTopics()
	{
		var topics = new List<string>();

		string scene = _currentRecord?.scene ?? "";
		string question = _currentRecord?.question ?? "";

		if (scene.Contains("relationship") || scene.Contains("friendship"))
		{
			topics.Add("如何改善这段关系？");
			topics.Add("对方现在是什么想法？");
			topics.Add("我应该怎么做？");
		}
		else if (scene.Contains("career") || scene.Contains("work"))
		{
			topics.Add("这个选择会带来什么？");
			topics.Add("我需要注意什么？");
			topics.Add("下一步怎么走？");
		}
		else if (scene.Contains("self") || scene.Contains("daily"))
		{
			topics.Add("如何更好地理解自己？");
			topics.Add("今天的重点是什么？");
			topics.Add("有什么需要注意的？");
		}
		else
		{
			topics.Add("能再详细说说吗？");
			topics.Add("有什么建议给我？");
			topics.Add("还有什么需要注意的？");
		}

		return topics;
	}

	private DivinationRecordData BuildRecordFromSession()
	{
		return DivinationRecordBuilder.FromSession();
	}

	private string BuildDefaultJudgeText()
	{
		var cards = _currentRecord?.lockedCards;
		if (cards == null || cards.Count == 0)
			return "这次占卜暂时没有抽牌数据，请回到对话中重新抽牌。";

		if (cards.Count >= 3)
		{
			return $"这是一个由「{cards[0].cardName}」「{cards[1].cardName}」「{cards[2].cardName}」组成的转折牌面。"
				+ "第一张牌指出当下的核心状态，第二张牌揭示阻碍或需要看清的部分，第三张牌给出下一步走向。"
				+ "整体来看，答案不是立刻下结论，而是先把问题拆开，再选择更稳定的行动。";
		}

		return $"这次牌面以「{cards[0].cardName}」为核心，重点在于看见当下状态，并从一个小而确定的行动开始。";
	}

	private string BuildDefaultAdviceText()
	{
		var cards = _currentRecord?.lockedCards;
		if (cards == null || cards.Count == 0)
			return "先回到问题本身，确认你真正想知道的是什么。";

		var advice = new List<string>
		{
			"继续保持诚实地面对自己的心态，先不要急着做最终判断。",
			"多一些倾听与观察，避免因为急于确认答案而忽略细节。",
			"相信直觉，再把直觉落到一个可执行的小行动里。"
		};

		return "◆ " + string.Join("\n◆ ", advice);
	}

	private string GetSpreadPositionPrompt(int index)
	{
		if (DivinationEngine.Instance == null || string.IsNullOrEmpty(_currentRecord?.spreadKind))
			return "";

		var def = DivinationEngine.Instance.GetSpreadDefinition(_currentRecord.spreadKind);
		if (def?.positions == null || index < 0 || index >= def.positions.Count)
			return "";

		var position = def.positions[index];
		if (string.IsNullOrEmpty(position.prompt))
			return "";

		return $"{position.prompt}。";
	}

	private static string FirstNonEmpty(params string[] values)
	{
		if (values == null) return "";
		foreach (var value in values)
		{
			if (!string.IsNullOrEmpty(value))
				return value;
		}
		return "";
	}

	#endregion

	#region UI组件事件

	/// <summary>返回上一页面</summary>
	public void OnBackButtonClick()
	{
		HideWindow();
	}

	/// <summary>分享占卜结果</summary>
	public void OnShareButtonClick()
	{
		if (_currentRecord == null) return;

		string shareText = $"🔮 塔罗占卜\n"
			+ $"问题：{_currentRecord.question}\n"
			+ $"牌阵：{_currentRecord.SpreadLabel}\n"
			+ $"评判：{_currentRecord.judgeContent ?? _currentRecord.shortVerdict ?? ""}\n"
			+ $"建议：{_currentRecord.adviceContent ?? ""}";

		Debug.Log($"[DivinationInfoUI] 分享: {shareText}");

#if UNITY_IOS && !UNITY_EDITOR
		ShareToIOS(shareText);
#elif UNITY_ANDROID && !UNITY_EDITOR
		ShareToAndroid(shareText);
#else
		// Editor 模式：复制到剪贴板
		GUIUtility.systemCopyBuffer = shareText;
		Debug.Log("[DivinationInfoUI] 已复制分享内容到剪贴板");
#endif
	}

	/// <summary>追问话题 1</summary>
	public void OnQuestion1ButtonClick()
	{
		ContinueWithTopic(0);
	}

	/// <summary>追问话题 2</summary>
	public void OnQuestion2ButtonClick()
	{
		ContinueWithTopic(1);
	}

	/// <summary>追问话题 3</summary>
	public void OnQuestion3ButtonClick()
	{
		ContinueWithTopic(2);
	}

	/// <summary>继续聊天</summary>
	public void OnContinueChatButtonClick()
	{
		ContinueWithTopic(-1);
	}

	/// <summary>
	/// 使用指定话题继续对话
	/// </summary>
	private void ContinueWithTopic(int topicIndex)
	{
		if (_currentRecord == null) return;

		string topic = topicIndex >= 0 && topicIndex < _topics.Count
			? _topics[topicIndex]
			: "";

		// 恢复这条详情自己的占卜上下文，避免继续追问时串到最近一次牌阵。
		DialogSystem.Instance?.ActivateReadingFromRecord(_currentRecord, DivinationPhase.FollowUp);

		// 打开对话界面
		HideWindow();
		UIModule.Instance.PopUpWindow<DialogUI>();
		if (!string.IsNullOrEmpty(topic))
			uiComponent.StartCoroutine(SendTopicToDialogNextFrame(topic));
	}

	private IEnumerator SendTopicToDialogNextFrame(string topic)
	{
		yield return null;

		var dialog = UIModule.Instance.GetWindow<DialogUI>();
		if (dialog == null)
			dialog = UIModule.Instance.PopUpWindow<DialogUI>();

		if (dialog != null)
			dialog.SendMessageFromExternal(topic);
		else
			Debug.LogWarning($"[DivinationInfoUI] DialogUI 未找到，无法发送追问: {topic}");
	}

	private bool HasVerdict(DivinationRecordData record)
	{
		return record != null
			&& (!string.IsNullOrWhiteSpace(record.judgeContent)
				|| !string.IsNullOrWhiteSpace(record.shortVerdict)
				|| !string.IsNullOrWhiteSpace(record.adviceContent));
	}

#if UNITY_IOS && !UNITY_EDITOR
	private void ShareToIOS(string text)
	{
		// iOS 原生分享
		NativeIOSShare.ShareText(text);
	}
#endif

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
