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

public class MemoryManagementUI : WindowBase
{
	public MemoryManagementUIComponent uiComponent;
	private readonly List<GameObject> _renderedItems = new List<GameObject>();
	private RectTransform _contentRect;
	private const string KEY_SHARE_ALL_MEMORY = "MemoryManagement_ShareAll";

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
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		if (uiComponent.ShareAllToggle != null)
			uiComponent.ShareAllToggle.isOn = PlayerPrefs.GetInt(KEY_SHARE_ALL_MEMORY, 1) == 1;
		LoadCloudMemoryThenRender();
	}
	// 物体隐藏时执行
	public override void OnHide()
	{
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
		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			RenderMemoryList();
			return;
		}

		firestore.LoadMemorySource(source =>
		{
			if (source != null)
				DialogSystem.Instance?.SetMemorySource(source);
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
			string status = string.IsNullOrEmpty(candidate.status) ? "pending" : candidate.status;
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
		AddText(text, 24, FontStyle.Bold, new Color(0.95f, 0.86f, 1f, 1f), ref y, 34f);
	}

	private void AddSection(string title, List<string> lines, ref float y)
	{
		AddText(title, 19, FontStyle.Bold, new Color(0.86f, 0.7f, 1f, 1f), ref y, 28f);
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
		AddText(text, 15, FontStyle.Normal, new Color(0.86f, 0.84f, 0.92f, 1f), ref y, 44f);
	}

	private void AddText(string text, int fontSize, FontStyle style, Color color, ref float y, float height)
	{
		var go = new GameObject("MemoryText", typeof(RectTransform));
		go.transform.SetParent(_contentRect, false);
		var rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = new Vector2(20f, -y);
		rect.sizeDelta = new Vector2(-40f, height);

		var label = go.AddComponent<Text>();
		label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		label.fontSize = fontSize;
		label.fontStyle = style;
		label.color = color;
		label.alignment = TextAnchor.UpperLeft;
		label.horizontalOverflow = HorizontalWrapMode.Wrap;
		label.verticalOverflow = VerticalWrapMode.Truncate;
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

	#endregion

	#region UI组件事件
	public void OnBackButtonClick()
	{
		HideWindow();
	}
	public void OnShareAllToggleChange(bool state, Toggle toggle)
	{
		PlayerPrefs.SetInt(KEY_SHARE_ALL_MEMORY, state ? 1 : 0);
		PlayerPrefs.Save();
	}
	public void OnSyncMemoryButtonClick()
	{
		var firestore = FirestoreManager.Instance;
		var source = DialogSystem.Instance?.GetMemorySource();
		if (firestore != null && firestore.IsInitialized)
		{
			firestore.SaveMemorySource(source, success =>
			{
				LoadCloudMemoryThenRender();
				ToastManager.ShowToast(success ? "记忆已同步" : "记忆同步失败");
			});
		}
		else
		{
			RenderMemoryList();
			ToastManager.ShowToast("记忆已刷新");
		}
	}
	public void OnClearAllButtonClick()
	{
		DialogSystem.Instance?.SetMemorySource(new MemorySource());
		var firestore = FirestoreManager.Instance;
		if (firestore != null && firestore.IsInitialized)
		{
			firestore.DeleteMemorySource(success =>
			{
				RenderMemoryList();
				ToastManager.ShowToast(success ? "AI 记忆已清空" : "本地记忆已清空，云端删除失败");
			});
		}
		else
		{
			RenderMemoryList();
			ToastManager.ShowToast("本地 AI 记忆已清空");
		}
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
