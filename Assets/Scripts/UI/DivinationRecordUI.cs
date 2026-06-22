/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 12:56:29
 * Description: UI 表现层 —— 占卜记录详情页
 * 展示单次占卜的完整信息（问题、牌阵、卡牌、解读）
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;

public class DivinationRecordUI : WindowBase
{
	public DivinationRecordUIComponent uiComponent;

	/// <summary>当前展示的占卜记录</summary>
	private DivinationRecordData _currentRecord;

	/// <summary>ScrollRect Content 容器</summary>
	private RectTransform _contentRect;

	/// <summary>动态创建的 UI 元素（用于清理）</summary>
	private List<GameObject> _dynamicElements = new List<GameObject>();

	/// <summary>是否正在删除中（防重复点击）</summary>
	private bool _isDeleting = false;
	private string _pendingDeleteReadingId;
	private float _deleteConfirmDeadline;
	private const float DELETE_CONFIRM_SECONDS = 8f;
	private bool _hasLoggedSaveButtonAutoEnable;

	[Header("详情页样式配置")]
	public float cardItemHeight = 60f;             // 每张牌的显示高度
	public Color bgPanelColor = new Color(0.12f, 0.09f, 0.18f, 1f);
	public Color cardBgColor = new Color(0.18f, 0.14f, 0.26f, 1f);
	public Color titleColor = new Color(0.9f, 0.88f, 0.95f, 1f);
	public Color bodyColor = new Color(0.75f, 0.72f, 0.85f, 1f);
	public Color cardNameColor = new Color(0.9f, 0.75f, 0.55f, 1f); // 金色
	public Color uprightColor = new Color(0.55f, 0.85f, 0.65f, 1f); // 正位绿
	public Color reversedColor = new Color(0.85f, 0.55f, 0.65f, 1f); // 逆位红

	#region 生命周期函数

	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<DivinationRecordUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();

		if (uiComponent.RecordScrollContainerScrollRect != null)
			_contentRect = uiComponent.RecordScrollContainerScrollRect.content;
	}

	public override void OnShow()
	{
		base.OnShow();
		EnsureSaveToDiaryButtonInteractable();
		SetSaveToDiaryButtonState(true, "保存到历史");

		// 从 HistoryUI 获取选中的记录
		_currentRecord = HistoryUI.SelectedRecord;

		if (_currentRecord == null)
		{
			Debug.LogWarning("[DivinationRecordUI] 没有选中的记录，尝试从 Firestore 加载最后一条");
			TryLoadLatest();
		}
		else
		{
			RenderRecord();
		}
	}

	public override void OnHide()
	{
		base.OnHide();
	}

	public override void OnDestroy()
	{
		ClearDynamicElements();
		base.OnDestroy();
	}

	#endregion

	#region API Function

	/// <summary>
	/// 设置当前要展示的记录（外部调用，如推送通知打开）
	/// </summary>
	public void SetRecord(DivinationRecordData record)
	{
		_currentRecord = record;
		if (gameObject != null && gameObject.activeInHierarchy)
			RenderRecord();
	}

	/// <summary>
	/// 尝试加载最近一条记录
	/// </summary>
	private void TryLoadLatest()
	{
		var firestore = DivinationRecordFirestore.Instance;
		if (firestore == null)
		{
			var go = new GameObject("DivinationRecordFirestore");
			firestore = go.AddComponent<DivinationRecordFirestore>();
		}

		if (firestore == null)
		{
			RenderEmptyState("历史服务暂不可用");
			return;
		}

		firestore.LoadAllRecords(records =>
		{
			if (records != null && records.Count > 0)
			{
				_currentRecord = records[0];
				RenderRecord();
				return;
			}

			RenderEmptyState("暂无占卜记录");
		});
	}

	/// <summary>
	/// 渲染占卜记录详情
	/// </summary>
	private void RenderRecord()
	{
		if (_currentRecord == null || _contentRect == null) return;

		ClearDynamicElements();

		float currentY = 16f;
		float contentWidth = _contentRect.rect.width;
		float textWidth = contentWidth - 40f;

		// ===== 1. 牌阵名称（顶部居中） =====
		GameObject spreadHeader = CreateTextElement(
			"SpreadHeader", _contentRect,
			_currentRecord.SpreadLabel,
			fontSize: 24, fontStyle: FontStyle.Bold, color: titleColor,
			width: textWidth, height: 32f,
			yOffset: currentY, alignment: TextAnchor.MiddleCenter);
		_dynamicElements.Add(spreadHeader);
		currentY += 44f;

		// ===== 2. 时间标签 =====
		GameObject timeLabel = CreateTextElement(
			"TimeLabel", _contentRect,
			_currentRecord.DisplayTime,
			fontSize: 14, color: bodyColor,
			width: textWidth, height: 22f,
			yOffset: currentY, alignment: TextAnchor.MiddleCenter);
		_dynamicElements.Add(timeLabel);
		currentY += 32f;

		// ===== 3. 问题标题 =====
		GameObject questionTitle = CreateTextElement(
			"QuestionTitle", _contentRect,
			"你的问题",
			fontSize: 14, fontStyle: FontStyle.Bold, color: new Color(0.75f, 0.55f, 0.9f, 1f),
			width: textWidth, height: 22f,
			yOffset: currentY, alignment: TextAnchor.MiddleLeft);
		_dynamicElements.Add(questionTitle);
		currentY += 26f;

		GameObject questionText = CreateTextElement(
			"QuestionText", _contentRect,
			_currentRecord.question ?? "",
			fontSize: 18, color: titleColor,
			width: textWidth, height: 50f,
			yOffset: currentY, alignment: TextAnchor.MiddleLeft);
		_dynamicElements.Add(questionText);
		currentY += 62f;

		// ===== 4. 卡牌区域 =====
		GameObject cardsTitle = CreateTextElement(
			"CardsTitle", _contentRect,
			"抽到的牌",
			fontSize: 14, fontStyle: FontStyle.Bold, color: new Color(0.75f, 0.55f, 0.9f, 1f),
			width: textWidth, height: 22f,
			yOffset: currentY, alignment: TextAnchor.MiddleLeft);
		_dynamicElements.Add(cardsTitle);
		currentY += 30f;

		if (_currentRecord.lockedCards != null)
		{
			foreach (var card in _currentRecord.lockedCards)
			{
				GameObject cardItem = CreateCardItem(card, textWidth, currentY);
				if (cardItem != null)
				{
					_dynamicElements.Add(cardItem);
					currentY += cardItemHeight + 8f;
				}
			}
		}
		currentY += 8f;

		// ===== 5. AI 解读 =====
		if (!string.IsNullOrEmpty(_currentRecord.shortVerdict))
		{
			GameObject verdictTitle = CreateTextElement(
				"VerdictTitle", _contentRect,
				"解读",
				fontSize: 14, fontStyle: FontStyle.Bold, color: new Color(0.75f, 0.55f, 0.9f, 1f),
				width: textWidth, height: 22f,
				yOffset: currentY, alignment: TextAnchor.MiddleLeft);
			_dynamicElements.Add(verdictTitle);
			currentY += 28f;

			// 计算解读文本所需高度
			float verdictHeight = Mathf.Max(EstimateTextHeight(_currentRecord.shortVerdict, 16, textWidth - 32f), 80f);
			GameObject verdictText = CreateTextElement(
				"VerdictText", _contentRect,
				_currentRecord.shortVerdict,
				fontSize: 16, color: bodyColor,
				width: textWidth - 16f, height: verdictHeight,
				yOffset: currentY, alignment: TextAnchor.UpperLeft, paddingX: 12f, paddingY: 8f,
				bgColor: cardBgColor);
			_dynamicElements.Add(verdictText);
			currentY += verdictHeight + 20f;
		}

		currentY = AddTextSection("综合判断", _currentRecord.judgeContent, textWidth, currentY);
		currentY = AddTextSection("行动建议", _currentRecord.adviceContent, textWidth, currentY);
		currentY = AddTextSection("继续追问", BuildTopicsText(_currentRecord.topics), textWidth, currentY);

		// ===== 6. 更新 Content 总高度 =====
		_contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, currentY + 100f);

		// 重置滚动到顶部
		if (uiComponent.RecordScrollContainerScrollRect != null)
			uiComponent.RecordScrollContainerScrollRect.verticalNormalizedPosition = 1f;
	}

	private void RenderEmptyState(string message)
	{
		if (_contentRect == null)
		{
			ToastManager.ShowToast(message);
			return;
		}

		ClearDynamicElements();
		float textWidth = _contentRect.rect.width - 40f;
		if (textWidth <= 0f)
			textWidth = 520f;

		GameObject title = CreateTextElement(
			"EmptyTitle", _contentRect,
			"占卜记录",
			fontSize: 24, fontStyle: FontStyle.Bold, color: titleColor,
			width: textWidth, height: 36f,
			yOffset: 24f, alignment: TextAnchor.MiddleCenter);
		_dynamicElements.Add(title);

		GameObject body = CreateTextElement(
			"EmptyBody", _contentRect,
			string.IsNullOrWhiteSpace(message) ? "暂无可展示的占卜记录。" : message,
			fontSize: 16, color: bodyColor,
			width: textWidth, height: 80f,
			yOffset: 76f, alignment: TextAnchor.MiddleCenter,
			paddingX: 16f, paddingY: 8f,
			bgColor: cardBgColor);
		_dynamicElements.Add(body);

		_contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 220f);
		if (uiComponent.RecordScrollContainerScrollRect != null)
			uiComponent.RecordScrollContainerScrollRect.verticalNormalizedPosition = 1f;
	}

	/// <summary>
	/// 创建单个卡牌信息的 UI 条目
	/// 显示：位置名 | 卡牌名 | 正/逆位
	/// </summary>
	private GameObject CreateCardItem(LockedCard card, float textWidth, float yOffset)
	{
		if (_contentRect == null) return null;

		GameObject go = new GameObject($"CardItem_{card.positionKey}", typeof(RectTransform));
		go.transform.SetParent(_contentRect, false);
		go.transform.localScale = Vector3.one;

		RectTransform rt = go.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.anchoredPosition = new Vector2(0f, -yOffset);
		rt.sizeDelta = new Vector2(0f, cardItemHeight);

		// 卡片背景
		Image bg = go.AddComponent<Image>();
		bg.color = cardBgColor;

		// 左侧位置标签
		GameObject posGo = new GameObject("PositionLabel", typeof(RectTransform));
		posGo.transform.SetParent(go.transform, false);
		RectTransform pRt = posGo.GetComponent<RectTransform>();
		pRt.anchorMin = new Vector2(0f, 1f);
		pRt.anchorMax = new Vector2(0.3f, 1f);
		pRt.pivot = new Vector2(0f, 1f);
		pRt.anchoredPosition = new Vector2(16f, -14f);
		pRt.sizeDelta = new Vector2(0f, 36f);
		Text posText = posGo.AddComponent<Text>();
		posText.text = card.position ?? "";
		posText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		posText.fontSize = 14;
		posText.fontStyle = FontStyle.Bold;
		posText.color = titleColor;
		posText.alignment = TextAnchor.MiddleLeft;

		// 中间卡牌名
		GameObject nameGo = new GameObject("CardName", typeof(RectTransform));
		nameGo.transform.SetParent(go.transform, false);
		RectTransform nRt = nameGo.GetComponent<RectTransform>();
		nRt.anchorMin = new Vector2(0.3f, 1f);
		nRt.anchorMax = new Vector2(0.7f, 1f);
		nRt.pivot = new Vector2(0f, 1f);
		nRt.anchoredPosition = new Vector2(0f, -14f);
		nRt.sizeDelta = new Vector2(0f, 36f);
		Text nameText = nameGo.AddComponent<Text>();
		nameText.text = card.cardName ?? "";
		nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		nameText.fontSize = 16;
		nameText.color = cardNameColor;
		nameText.alignment = TextAnchor.MiddleLeft;

		// 右侧正/逆位标签
		GameObject orientGo = new GameObject("Orientation", typeof(RectTransform));
		orientGo.transform.SetParent(go.transform, false);
		RectTransform oRt = orientGo.GetComponent<RectTransform>();
		oRt.anchorMin = new Vector2(0.7f, 1f);
		oRt.anchorMax = new Vector2(1f, 1f);
		oRt.pivot = new Vector2(1f, 1f);
		oRt.anchoredPosition = new Vector2(-12f, -14f);
		oRt.sizeDelta = new Vector2(50f, 36f);
		Text orientText = orientGo.AddComponent<Text>();
		bool isUpright = card.orientation == "upright";
		orientText.text = isUpright ? "正位 ↑" : "逆位 ↓";
		orientText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		orientText.fontSize = 14;
		orientText.fontStyle = FontStyle.Bold;
		orientText.color = isUpright ? uprightColor : reversedColor;
		orientText.alignment = TextAnchor.MiddleRight;

		return go;
	}

	private float AddTextSection(string title, string body, float textWidth, float currentY)
	{
		if (string.IsNullOrWhiteSpace(body)) return currentY;

		GameObject sectionTitle = CreateTextElement(
			title + "Title", _contentRect,
			title,
			fontSize: 14, fontStyle: FontStyle.Bold, color: new Color(0.75f, 0.55f, 0.9f, 1f),
			width: textWidth, height: 22f,
			yOffset: currentY, alignment: TextAnchor.MiddleLeft);
		_dynamicElements.Add(sectionTitle);
		currentY += 28f;

		float sectionHeight = Mathf.Max(EstimateTextHeight(body, 16, textWidth - 32f), 70f);
		GameObject sectionText = CreateTextElement(
			title + "Text", _contentRect,
			body,
			fontSize: 16, color: bodyColor,
			width: textWidth - 16f, height: sectionHeight,
			yOffset: currentY, alignment: TextAnchor.UpperLeft, paddingX: 12f, paddingY: 8f,
			bgColor: cardBgColor);
		_dynamicElements.Add(sectionText);
		currentY += sectionHeight + 20f;
		return currentY;
	}

	private string BuildTopicsText(List<string> topics)
	{
		if (topics == null || topics.Count == 0) return string.Empty;

		var lines = new List<string>();
		for (int i = 0; i < topics.Count; i++)
		{
			if (string.IsNullOrWhiteSpace(topics[i])) continue;
			lines.Add($"{i + 1}. {topics[i]}");
		}
		return string.Join("\n", lines);
	}

	#endregion

	#region UI组件事件

	/// <summary>返回上一个窗口</summary>
	public void OnBackButtonClick()
	{
		HideWindow();
	}

	/// <summary>继续询问 → 打开对话界面</summary>
	public void OnContinueAskButtonClick()
	{
		if (_currentRecord == null) return;

		Debug.Log($"[DivinationRecordUI] 继续追问: {_currentRecord.readingId}");

		DialogSystem.Instance?.ActivateReadingFromRecord(_currentRecord, DivinationPhase.FollowUp);

		// 恢复占卜会话上下文
		if (DivinationEngine.Instance != null)
		{
			var session = new DivinationSession
			{
				readingId = _currentRecord.readingId,
				question = _currentRecord.question,
				scene = _currentRecord.scene,
				spreadKind = _currentRecord.spreadKind,
				lockedCards = _currentRecord.lockedCards,
				shortVerdict = _currentRecord.shortVerdict,
				judgeContent = _currentRecord.judgeContent,
				adviceContent = _currentRecord.adviceContent,
				topics = _currentRecord.topics,
				phase = DivinationPhase.FollowUp,
				createdAt = System.DateTime.Now.ToString("o")
			};

			DivinationEngine.Instance.RestoreSession(session);
		}

		// 打开对话界面
		HideWindow();
		UIModule.Instance.PopUpWindow<DialogUI>();
	}

	/// <summary>保存到日记</summary>
	public void OnSaveToDiaryButtonClick()
	{
		if (_currentRecord == null)
		{
			SetSaveToDiaryButtonState(true, "保存到历史");
			return;
		}

		Debug.Log($"[DivinationRecordUI] 保存到日记: {_currentRecord.readingId}");

		var firestore = DivinationRecordFirestore.Instance;
		if (firestore == null)
		{
			var go = new GameObject("DivinationRecordFirestore");
			firestore = go.AddComponent<DivinationRecordFirestore>();
		}

		if (firestore != null)
		{
			firestore.SaveRecord(_currentRecord, success =>
			{
				if (success)
				{
					ToastManager.ShowToast("已保存到历史");
					Debug.Log("[DivinationRecordUI] 已保存到云端历史");
				}
				else
				{
					ToastManager.ShowToast("保存失败，请稍后再试");
					Debug.LogWarning("[DivinationRecordUI] 云端保存失败");
				}
				SetSaveToDiaryButtonState(true, "保存到历史");
			});
		}
		else
		{
			ToastManager.ShowToast("历史服务暂不可用");
			SetSaveToDiaryButtonState(true, "保存到历史");
			Debug.LogWarning("[DivinationRecordUI] 历史服务未就绪，未保存记录");
		}
	}

	/// <summary>分享占卜结果</summary>
	public void OnShareResultButtonClick()
	{
		if (_currentRecord == null) return;

		Debug.Log($"[DivinationRecordUI] 分享结果: {_currentRecord.readingId}");

#if UNITY_IOS && !UNITY_EDITOR
		ShareToNativeIOS();
#elif UNITY_ANDROID && !UNITY_EDITOR
		ShareToNativeAndroid();
#else
		// Editor: 复制到剪贴板
		string shareText = BuildShareText();
		GUIUtility.systemCopyBuffer = shareText;
		Debug.Log("[DivinationRecordUI] Editor 模式：已复制分享文本到剪贴板");
		ToastManager.ShowToast("分享内容已复制");
#endif
	}

	/// <summary>删除记录</summary>
	public void OnDeleteRecordButtonClick()
	{
		if (_currentRecord == null || _isDeleting) return;

		_isDeleting = true;

		string readingId = _currentRecord.readingId;
		if (_pendingDeleteReadingId != readingId || Time.time > _deleteConfirmDeadline)
		{
			_isDeleting = false;
			_pendingDeleteReadingId = readingId;
			_deleteConfirmDeadline = Time.time + DELETE_CONFIRM_SECONDS;
			ToastManager.ShowToast("再次点击删除这条占卜记录");
			return;
		}

		_pendingDeleteReadingId = null;

		Debug.Log($"[DivinationRecordUI] 删除记录: {readingId}");

		var firestore = DivinationRecordFirestore.Instance;
		if (firestore == null)
		{
			var go = new GameObject("DivinationRecordFirestore");
			firestore = go.AddComponent<DivinationRecordFirestore>();
		}

		if (firestore != null)
		{
			firestore.DeleteRecord(readingId, success =>
			{
				_isDeleting = false;

				if (success)
				{
					Debug.Log($"[DivinationRecordUI] 已删除记录: {readingId}");
					ToastManager.ShowToast("占卜记录已删除");
					// 清除选中状态
					HistoryUI.SelectedRecord = null;
					UIModule.Instance.GetWindow<HistoryUI>()?.RefreshList();
					// 返回上一页
					HideWindow();
				}
				else
				{
					Debug.LogError($"[DivinationRecordUI] 删除失败: {readingId}");
					ToastManager.ShowToast("删除失败，请稍后再试");
				}
			});
		}
		else
		{
			_isDeleting = false;
			Debug.LogWarning("[DivinationRecordUI] Firestore 未就绪，无法删除");
			ToastManager.ShowToast("历史服务未就绪");
		}
	}

	#endregion

	#region 分享功能

	private void LateUpdate()
	{
		EnsureSaveToDiaryButtonInteractable();
	}

	/// <summary>
	/// 构建分享文本
	/// </summary>
	private string BuildShareText()
	{
		if (_currentRecord == null) return "";

		string text = $"🔮 {_currentRecord.SpreadLabel}\n\n";
		text += $"问题：{_currentRecord.question}\n\n";
		text += "抽到的牌：";

		if (_currentRecord.lockedCards != null)
		{
			foreach (var card in _currentRecord.lockedCards)
			{
				bool isUpright = card.orientation == "upright";
				text += $"\n  {card.position}：{card.cardName}（{(isUpright ? "正位" : "逆位")}）";
			}
		}

		if (!string.IsNullOrEmpty(_currentRecord.shortVerdict))
		{
			text += $"\n\n解读：{_currentRecord.shortVerdict}";
		}

		text += "\n\n—— Moonly 塔罗占卜";

		return text;
	}

	private void SetSaveToDiaryButtonState(bool interactable, string label)
	{
		if (uiComponent?.SaveToDiaryButton == null)
			return;

		uiComponent.SaveToDiaryButton.interactable = interactable;
		Text buttonText = uiComponent.SaveToDiaryButton.GetComponentInChildren<Text>(true);
		if (buttonText != null)
			buttonText.text = label ?? string.Empty;
	}

	private void EnsureSaveToDiaryButtonInteractable()
	{
		if (uiComponent?.SaveToDiaryButton == null)
			return;

		if (!uiComponent.SaveToDiaryButton.interactable)
		{
			uiComponent.SaveToDiaryButton.interactable = true;
			Text buttonText = uiComponent.SaveToDiaryButton.GetComponentInChildren<Text>(true);
			if (buttonText != null)
				buttonText.text = "保存到历史";

			if (!_hasLoggedSaveButtonAutoEnable)
			{
				_hasLoggedSaveButtonAutoEnable = true;
				Debug.LogWarning("[DivinationRecordUI] SaveToDiaryButton 被置为不可交互，已自动恢复。");
			}
		}
	}

#if UNITY_IOS && !UNITY_EDITOR
	private void ShareToNativeIOS()
	{
		string shareText = BuildShareText();
		NativeIOSShare.ShareText(shareText);
	}
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
	private void ShareToNativeAndroid()
	{
		string shareText = BuildShareText();
		using (AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent"))
		using (AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent"))
		{
			intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
			intentObject.Call<AndroidJavaObject>("setType", "text/plain");
			intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), shareText);

			AndroidJavaClass unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			AndroidJavaObject currentActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity");
			currentActivity.Call("startActivity", intentClass.CallStatic<AndroidJavaObject>("createChooser", intentObject, "分享占卜结果"));
		}
	}
#endif

	#endregion

	#region UI 辅助方法

	/// <summary>
	/// 创建文本元素
	/// </summary>
	private GameObject CreateTextElement(
		string name, Transform parent,
		string content,
		int fontSize = 16, FontStyle fontStyle = FontStyle.Normal, Color? color = null,
		float width = 300f, float height = 30f, float yOffset = 0f,
		TextAnchor alignment = TextAnchor.MiddleLeft,
		float paddingX = 20f, float paddingY = 0f, Color? bgColor = null)
	{
		GameObject go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false);
		go.transform.localScale = Vector3.one;

		RectTransform rt = go.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.anchoredPosition = new Vector2(0f, -yOffset);
		rt.sizeDelta = new Vector2(0f, height);

		// 可选背景
		if (bgColor.HasValue)
		{
			Image img = go.AddComponent<Image>();
			img.color = bgColor.Value;
		}

		// 文本
		GameObject textGo = new GameObject("Text", typeof(RectTransform));
		textGo.transform.SetParent(go.transform, false);
		RectTransform textRt = textGo.GetComponent<RectTransform>();
		textRt.anchorMin = Vector2.zero;
		textRt.anchorMax = Vector2.one;
		textRt.offsetMin = new Vector2(paddingX, paddingY);
		textRt.offsetMax = new Vector2(-paddingX - 4f, -paddingY - 2f);

		Text text = textGo.AddComponent<Text>();
		text.text = content ?? "";
		text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		text.fontSize = fontSize;
		text.fontStyle = fontStyle;
		text.color = color ?? Color.white;
		text.alignment = alignment;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Overflow;

		return go;
	}

	/// <summary>
	/// 估算文本高度（粗略算法）
	/// </summary>
	private float EstimateTextHeight(string text, int fontSize, float maxWidth)
	{
		if (string.IsNullOrEmpty(text)) return 40f;
		float charsPerLine = maxWidth / (fontSize * 0.6f);
		float lines = Mathf.Ceil(text.Length / charsPerLine);
		return Mathf.Max(lines * (fontSize * 1.6f), 40f);
	}

	/// <summary>
	/// 清理动态创建的元素
	/// </summary>
	private void ClearDynamicElements()
	{
		foreach (var elem in _dynamicElements)
		{
			if (elem != null)
				GameObject.Destroy(elem);
		}
		_dynamicElements.Clear();
	}

	#endregion
}
