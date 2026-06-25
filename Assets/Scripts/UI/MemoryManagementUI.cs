/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 2026/6/13 15:10:46
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.UIFrameWork;
using GamerFrameWork.OracleRuntime;
using System.Collections.Generic;
using TMPro;

public class MemoryManagementUI : WindowBase
{
	public MemoryManagementUIComponent uiComponent;
	private readonly List<GameObject> _renderedItems = new List<GameObject>();
	private RectTransform _contentRect;
	private const float CLEAR_CONFIRM_SECONDS = 8f;
	private TMP_Text _clearTitleText;
	private TMP_Text _clearDescText;
	private string _clearTitleDefault;
	private string _clearDescDefault;
	private bool _clearConfirmArmed;
	private bool _isClearing;
	private float _clearConfirmDeadline;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<MemoryManagementUIComponent>();
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		base.OnAwake();
		_contentRect = uiComponent.MemoryScrollContainerScrollRect != null
			? uiComponent.MemoryScrollContainerScrollRect.content
			: null;
		BindClearTexts();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		BindClearTexts();
		ResetClearConfirmation();
		if (uiComponent.ShareAllToggle != null)
			uiComponent.ShareAllToggle.isOn = MemoryPrivacySettings.ShareAllMemoryEnabled;
		LoadCloudMemoryThenRender();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
		ResetClearConfirmation();
		base.OnHide();
	}
	// 物体销毁时执行
	public override void OnDestroy()
	{
		ClearRenderedItems();
		base.OnDestroy();
	}
	#endregion

	#region API Function
	private void RenderMemoryList()
	{
		ClearRenderedItems();
		if (_contentRect == null) return;

		var dialogSystem = DialogSystem.Instance;
		var source = dialogSystem?.GetMemorySource();
		var promptRecords = dialogSystem?.GetPromptRecords();

		float y = 16f;
		AddHeader("AI 记忆", ref y);

		if (source == null)
		{
			AddParagraph("当前还没有可读取的记忆源。和神谕师多聊几轮后，这里会出现偏好、关系线索和待确认记忆。", ref y);
			SetContentHeight(y + 40f);
			return;
		}

		AddSection("个人偏好", BuildStableProfileLines(source.stableProfile), ref y);
		AddSection("关系记忆", BuildRelationshipLines(source.relationships), ref y);
		AddSection("占卜连续性", BuildReadingContinuityLines(source.readingContinuity), ref y);
		AddSection("候选记忆", BuildCandidateLines(source.candidates), ref y);
		AddSection("明日线索", BuildTomorrowHookLines(source.tomorrowHooks), ref y);
		AddSection("最近 Prompt", BuildPromptRecordLines(promptRecords), ref y);

		SetContentHeight(y + 40f);
	}

	private void LoadCloudMemoryThenRender()
	{
		MemoryUiStore.LoadLatest(_ =>
		{
			RenderMemoryList();
		});
	}

	private List<string> BuildStableProfileLines(StableProfile profile)
	{
		var lines = new List<string>();
		if (profile == null) return lines;

		if (!string.IsNullOrEmpty(profile.preferredName))
			lines.Add($"称呼：{profile.preferredName}");
		if (!string.IsNullOrEmpty(profile.preferredTone))
			lines.Add($"偏好语气：{profile.preferredTone}");
		AddListLines(lines, "反复主题", profile.recurringThemes);
		AddListLines(lines, "避免表达", profile.doNotSay);
		AddListLines(lines, "安全备注", profile.safetyNotes);
		return lines;
	}

	private List<string> BuildRelationshipLines(List<RelationshipMemory> relationships)
	{
		var lines = new List<string>();
		if (relationships == null) return lines;
		foreach (var rel in relationships)
		{
			if (rel == null) continue;
			string name = string.IsNullOrEmpty(rel.displayName) ? rel.relationshipId : rel.displayName;
			lines.Add($"{name} · 提及 {rel.mentionCount30d} 次");
			AddListLines(lines, "已知事实", rel.knownFacts);
			AddListLines(lines, "未闭合问题", rel.openLoops);
			if (!string.IsNullOrEmpty(rel.lastActionAdvice))
				lines.Add($"建议：{rel.lastActionAdvice}");
		}
		return lines;
	}

	private List<string> BuildReadingContinuityLines(List<ReadingContinuityEntry> readings)
	{
		var lines = new List<string>();
		if (readings == null) return lines;
		foreach (var reading in readings)
		{
			if (reading == null) continue;
			string question = string.IsNullOrEmpty(reading.question) ? "未命名占卜" : reading.question;
			lines.Add($"{question} · {reading.shortVerdict}");
		}
		return lines;
	}

	private List<string> BuildCandidateLines(List<MemoryCandidate> candidates)
	{
		var lines = new List<string>();
		if (candidates == null) return lines;
		foreach (var candidate in candidates)
		{
			if (candidate == null || string.IsNullOrEmpty(candidate.text)) continue;
			string status = string.IsNullOrEmpty(candidate.status) ? "promoted" : candidate.status;
			lines.Add($"[{status}] {candidate.text}");
		}
		return lines;
	}

	private List<string> BuildTomorrowHookLines(List<TomorrowHook> hooks)
	{
		var lines = new List<string>();
		if (hooks == null) return lines;
		foreach (var hook in hooks)
		{
			if (hook == null) continue;
			string text = string.IsNullOrEmpty(hook.triggerText) ? hook.hookType : hook.triggerText;
			lines.Add($"{text} · {hook.scheduledForLocalDate}");
		}
		return lines;
	}

	private List<string> BuildPromptRecordLines(IReadOnlyList<PromptRecord> records)
	{
		var lines = new List<string>();
		if (records == null) return lines;

		int start = Mathf.Max(0, records.Count - 8);
		for (int i = records.Count - 1; i >= start; i--)
		{
			var record = records[i];
			if (record == null) continue;
			string scene = string.IsNullOrEmpty(record.scene) ? "chat" : record.scene;
			string input = string.IsNullOrEmpty(record.userInput) ? "(外部场景)" : record.userInput;
			lines.Add($"{scene} · {input}");
		}
		return lines;
	}

	private void AddListLines(List<string> lines, string label, List<string> values)
	{
		if (values == null || values.Count == 0) return;
		lines.Add($"{label}：{string.Join(" / ", values)}");
	}

	private void AddHeader(string text, ref float y)
	{
		AddText(text, 24, FontStyles.Bold, new Color(0.95f, 0.86f, 1f, 1f), ref y, 34f);
	}

	private void AddSection(string title, List<string> lines, ref float y)
	{
		AddText(title, 19, FontStyles.Bold, new Color(0.86f, 0.7f, 1f, 1f), ref y, 28f);
		if (lines == null || lines.Count == 0)
		{
			AddParagraph("暂无记录", ref y);
			return;
		}

		foreach (var line in lines)
			AddParagraph(line, ref y);
		y += 8f;
	}

	private void AddParagraph(string text, ref float y)
	{
		AddText(text, 15, FontStyles.Normal, new Color(0.86f, 0.84f, 0.92f, 1f), ref y, 44f);
	}

	private void AddText(string text, int fontSize, FontStyles style, Color color, ref float y, float height)
	{
		var go = new GameObject("MemoryText", typeof(RectTransform));
		go.transform.SetParent(_contentRect, false);
		var rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = new Vector2(20f, -y);
		rect.sizeDelta = new Vector2(-40f, height);

		var label = go.AddComponent<TextMeshProUGUI>();
		label.font = TMP_Settings.defaultFontAsset;
		label.fontSize = fontSize;
		label.fontStyle = style;
		label.color = color;
		label.alignment = TextAlignmentOptions.TopLeft;
		label.enableWordWrapping = true;
		label.overflowMode = TextOverflowModes.Truncate;
		label.text = text ?? "";

		_renderedItems.Add(go);
		y += height;
	}

	private void SetContentHeight(float height)
	{
		if (_contentRect != null)
			_contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(height, 400f));
	}

	private void ClearRenderedItems()
	{
		foreach (var item in _renderedItems)
		{
			if (item != null)
				GameObject.Destroy(item);
		}
		_renderedItems.Clear();
	}

	private void BindClearTexts()
	{
		_clearTitleText = _clearTitleText != null ? _clearTitleText : FindTextByObjectName("ClearTitleText");
		_clearDescText = _clearDescText != null ? _clearDescText : FindTextByObjectName("ClearDescText");

		if (_clearTitleText != null && string.IsNullOrEmpty(_clearTitleDefault))
			_clearTitleDefault = _clearTitleText.text;
		if (_clearDescText != null && string.IsNullOrEmpty(_clearDescDefault))
			_clearDescDefault = _clearDescText.text;
	}

	private TMP_Text FindTextByObjectName(string objectName)
	{
		if (string.IsNullOrEmpty(objectName))
			return null;

		TMP_Text[] texts = gameObject.GetComponentsInChildren<TMP_Text>(true);
		foreach (TMP_Text text in texts)
		{
			if (text != null && text.gameObject.name == objectName)
				return text;
		}

		return null;
	}

	private void ArmClearConfirmation()
	{
		_clearConfirmArmed = true;
		_clearConfirmDeadline = Time.time + CLEAR_CONFIRM_SECONDS;
		SetClearTexts("再次点击确认清除", "这会清空本地和云端 AI 记忆，8 秒内再次点击才会执行。");
		ToastManager.ShowToast("再次点击清除全部 AI 记忆");
	}

	private void ResetClearConfirmation(bool restoreText = true)
	{
		_clearConfirmArmed = false;
		_clearConfirmDeadline = 0f;
		if (restoreText)
			SetClearTexts(_clearTitleDefault, _clearDescDefault);
	}

	private void SetClearTexts(string title, string desc)
	{
		BindClearTexts();
		if (_clearTitleText != null && !string.IsNullOrEmpty(title))
			_clearTitleText.text = title;
		if (_clearDescText != null && !string.IsNullOrEmpty(desc))
			_clearDescText.text = desc;
	}

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnShareAllToggleChange(bool state, Toggle toggle)
	{
		MemoryPrivacySettings.ShareAllMemoryEnabled = state;
		ToastManager.ShowToast(state ? "AI 记忆共享已开启" : "AI 记忆共享已关闭");
	}
	public void OnSyncMemoryButtonClick()
	{
		ResetClearConfirmation();

		MemoryUiStore.SaveCurrent(success =>
		{
			LoadCloudMemoryThenRender();
			ToastManager.ShowToast(success ? "记忆已同步" : "已保存到本地，云端稍后同步");
		});
	}
	public void OnClearAllButtonClick()
	{
		if (_isClearing)
			return;

		if (!_clearConfirmArmed || Time.time > _clearConfirmDeadline)
		{
			ArmClearConfirmation();
			return;
		}

		_isClearing = true;
		ResetClearConfirmation(false);
		SetClearTexts("正在清除记忆", "正在清空本地和云端 AI 记忆，请稍候。");

		MemoryUiStore.ClearAll(success =>
		{
			_isClearing = false;
			ResetClearConfirmation();
			RenderMemoryList();
			ToastManager.ShowToast(success ? "AI 记忆已清空" : "本地 AI 记忆已清空，云端稍后同步");
		});
	}
	public void OnBottomNavToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnTabOracleToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnTabChatToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnTabFriendsToggleChange(bool state, Toggle toggle)
	{
	}
	public void OnTabProfileToggleChange(bool state, Toggle toggle)
	{
	}
	#endregion
}
