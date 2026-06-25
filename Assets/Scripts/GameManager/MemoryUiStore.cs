using System;
using System.Collections.Generic;
using System.Linq;
using GamerFrameWork.OracleRuntime;
using UnityEngine;

public enum MemoryUiCategory
{
	All,
	Topic,
	Preference,
	Emotion,
	Growth
}

public enum MemoryUiItemKind
{
	Candidate,
	StablePreferredTone,
	StableRecurringTheme,
	StableDoNotSay,
	StableSafetyNote,
	RelationshipKnownFact,
	RelationshipOpenLoop,
	RelationshipAdvice,
	ReadingContinuity,
	TomorrowHook
}

public class MemoryUiItem
{
	public string Id;
	public string Text;
	public string Source;
	public string StatusLabel;
	public string DateText;
	public MemoryUiCategory Category;
	public bool Enabled = true;
	public bool PendingConfirm;
	public bool Important;
	public MemoryUiItemKind Kind;
	public int ParentIndex = -1;
	public int ItemIndex = -1;
}

public struct MemoryEditResult
{
	public string Text;
	public MemoryUiCategory Category;
	public bool Enabled;
	public bool Important;
}

public static class MemoryUiStore
{
	private const string MemoryCacheKey = "MemoryUiStore_LocalSource_v1";
	private const string PendingCloudSaveKey = "MemoryUiStore_PendingCloudSave";
	private const string PendingCloudDeleteKey = "MemoryUiStore_PendingCloudDelete";
	private static readonly MemorySource FallbackSource = new MemorySource();

	public static MemorySource Source
	{
		get
		{
			MemorySource source = DialogSystem.Instance != null
				? DialogSystem.Instance.GetMemorySource()
				: FallbackSource;

			source ??= new MemorySource();
			Normalize(source);
			if (DialogSystem.Instance != null)
				DialogSystem.Instance.SetMemorySource(source);
			return source;
		}
	}

	public static void LoadLatest(Action<MemorySource> onComplete)
	{
		MemorySource local = LoadLocalSource();
		if (HasPendingCloudDelete())
		{
			DialogSystem.Instance?.SetMemorySource(new MemorySource());
			TrySyncPendingCloudDelete(_ => onComplete?.Invoke(Source));
			return;
		}

		if (HasPendingCloudSave())
		{
			if (local != null)
				DialogSystem.Instance?.SetMemorySource(local);
			TrySyncPendingCloudSave(_ => onComplete?.Invoke(Source));
			return;
		}

		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			if (local != null)
				DialogSystem.Instance?.SetMemorySource(local);
			onComplete?.Invoke(Source);
			return;
		}

		firestore.LoadMemorySource(source =>
		{
			if (source != null)
			{
				Normalize(source);
				DialogSystem.Instance?.SetMemorySource(source);
				SaveLocalSource(source);
			}

			onComplete?.Invoke(Source);
		});
	}

	public static void SaveCurrent(Action<bool> onComplete = null)
	{
		Normalize(Source);
		DialogSystem.Instance?.SetMemorySource(Source);
		SaveLocalSource(Source);
		MarkCloudSavePending();
		MarkCloudDeleteComplete();

		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			onComplete?.Invoke(false);
			return;
		}

		firestore.SaveMemorySource(Source, success =>
		{
			if (success)
				MarkCloudSaveComplete();
			onComplete?.Invoke(success);
		});
	}

	public static void ClearAll(Action<bool> onComplete = null)
	{
		DialogSystem.Instance?.SetMemorySource(new MemorySource());
		SaveLocalSource(Source);
		MarkCloudSaveComplete();
		MarkCloudDeletePending();

		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			onComplete?.Invoke(false);
			return;
		}

		firestore.DeleteMemorySource(success =>
		{
			if (success)
				MarkCloudDeleteComplete();
			onComplete?.Invoke(success);
		});
	}

	public static bool HasPendingCloudSync()
	{
		return HasPendingCloudSave() || HasPendingCloudDelete();
	}

	public static List<MemoryUiItem> GetItems(MemoryUiCategory category = MemoryUiCategory.All, string keyword = null)
	{
		var items = BuildItems(Source);
		if (category != MemoryUiCategory.All)
			items = items.Where(item => item.Category == category).ToList();

		if (!string.IsNullOrWhiteSpace(keyword))
		{
			string needle = keyword.Trim();
			items = items.Where(item =>
				(item.Text ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
				(item.Source ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
				GetCategoryLabel(item.Category).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
		}

		return items;
	}

	public static int GetCount(MemoryUiCategory category)
	{
		return GetItems(category).Count(item => item.Enabled);
	}

	public static MemoryUiItem FindItem(string id)
	{
		if (string.IsNullOrEmpty(id)) return null;
		return BuildItems(Source).FirstOrDefault(item => item.Id == id);
	}

	public static MemoryUiItem AddManualMemory(MemoryEditResult result)
	{
		string text = NormalizeText(result.Text);
		if (string.IsNullOrEmpty(text)) return null;

		var candidate = new MemoryCandidate
		{
			id = NewId(),
			userId = UserDataManager.Instance != null ? UserDataManager.Instance.FirebaseUid : "",
			type = ToType(result.Category),
			text = text,
			status = result.Enabled ? "promoted" : "dismissed",
			confidence = 1f,
			relationshipId = "",
			sourceConversationId = "manual",
			sourceMessageId = "",
			createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
			important = result.Important
		};

		Source.candidates.Insert(0, candidate);
		return FindItem(GetCandidateUiId(candidate, 0));
	}

	public static MemoryUiItem UpdateMemory(string id, MemoryEditResult result)
	{
		MemoryUiItem item = FindItem(id);
		string text = NormalizeText(result.Text);
		if (item == null || string.IsNullOrEmpty(text)) return null;

		if (item.Kind == MemoryUiItemKind.Candidate)
		{
			MemoryCandidate candidate = FindCandidate(item);
			if (candidate == null) return null;

			candidate.text = text;
			candidate.type = ToType(result.Category);
			candidate.status = result.Enabled ? "promoted" : "dismissed";
			candidate.important = result.Important;
			if (string.IsNullOrEmpty(candidate.createdAt))
				candidate.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
			return FindItem(GetCandidateUiId(candidate, Source.candidates.IndexOf(candidate)));
		}

		RemoveItem(item);
		return AddManualMemory(result);
	}

	public static MemoryUiItem SetEnabled(string id, bool enabled)
	{
		MemoryUiItem item = FindItem(id);
		if (item == null) return null;

		if (item.Kind == MemoryUiItemKind.Candidate)
		{
			MemoryCandidate candidate = FindCandidate(item);
			if (candidate == null) return null;
			candidate.status = enabled ? "promoted" : "dismissed";
			return FindItem(GetCandidateUiId(candidate, Source.candidates.IndexOf(candidate)));
		}

		if (enabled) return item;

		RemoveItem(item);
		return AddManualMemory(new MemoryEditResult
		{
			Text = item.Text,
			Category = item.Category,
			Enabled = false,
			Important = item.Important
		});
	}

	public static MemoryUiItem SetImportant(string id, bool important)
	{
		MemoryUiItem item = FindItem(id);
		if (item == null) return null;

		if (item.Kind == MemoryUiItemKind.Candidate)
		{
			MemoryCandidate candidate = FindCandidate(item);
			if (candidate == null) return null;
			candidate.important = important;
			return FindItem(GetCandidateUiId(candidate, Source.candidates.IndexOf(candidate)));
		}

		RemoveItem(item);
		return AddManualMemory(new MemoryEditResult
		{
			Text = item.Text,
			Category = item.Category,
			Enabled = item.Enabled,
			Important = important
		});
	}

	public static bool DeleteMemory(string id)
	{
		MemoryUiItem item = FindItem(id);
		return item != null && RemoveItem(item);
	}

	public static string GetCategoryLabel(MemoryUiCategory category)
	{
		return category switch
		{
			MemoryUiCategory.Topic => "对话主题",
			MemoryUiCategory.Preference => "个人偏好",
			MemoryUiCategory.Emotion => "情感模式",
			MemoryUiCategory.Growth => "成长轨迹",
			_ => "全部"
		};
	}

	public static string ToType(MemoryUiCategory category)
	{
		return category switch
		{
			MemoryUiCategory.Preference => "preference",
			MemoryUiCategory.Emotion => "emotion",
			MemoryUiCategory.Growth => "growth",
			_ => "topic"
		};
	}

	public static MemoryUiCategory FromType(string type)
	{
		string value = (type ?? "").ToLowerInvariant();
		if (value.Contains("preference") || value.Contains("tone") || value.Contains("name") || value.Contains("do_not"))
			return MemoryUiCategory.Preference;
		if (value.Contains("emotion") || value.Contains("relationship") || value.Contains("safety"))
			return MemoryUiCategory.Emotion;
		if (value.Contains("growth") || value.Contains("reading") || value.Contains("action") || value.Contains("tomorrow"))
			return MemoryUiCategory.Growth;
		return MemoryUiCategory.Topic;
	}

	public static bool IsCandidateEnabled(MemoryCandidate candidate)
	{
		string status = (candidate?.status ?? "").ToLowerInvariant();
		return string.IsNullOrEmpty(status) || status == "promoted" || status == "accepted";
	}

	public static bool IsCandidatePending(MemoryCandidate candidate)
	{
		return string.Equals(candidate?.status, "pending", StringComparison.OrdinalIgnoreCase);
	}

	private static List<MemoryUiItem> BuildItems(MemorySource source)
	{
		Normalize(source);
		var items = new List<MemoryUiItem>();
		StableProfile profile = source.stableProfile;

		if (!string.IsNullOrWhiteSpace(profile.preferredTone))
		{
			items.Add(new MemoryUiItem
			{
				Id = "stable:preferredTone",
				Kind = MemoryUiItemKind.StablePreferredTone,
				Category = MemoryUiCategory.Preference,
				Text = profile.preferredTone,
				Source = "个人画像",
				DateText = "长期"
			});
		}

		AddStableList(items, profile.recurringThemes, MemoryUiItemKind.StableRecurringTheme, MemoryUiCategory.Topic, "stable:recurringThemes", "反复主题");
		AddStableList(items, profile.doNotSay, MemoryUiItemKind.StableDoNotSay, MemoryUiCategory.Preference, "stable:doNotSay", "表达偏好");
		AddStableList(items, profile.safetyNotes, MemoryUiItemKind.StableSafetyNote, MemoryUiCategory.Emotion, "stable:safetyNotes", "安全备注");

		for (int i = 0; i < source.relationships.Count; i++)
		{
			RelationshipMemory relationship = source.relationships[i];
			if (relationship == null) continue;
			string relName = string.IsNullOrEmpty(relationship.displayName) ? "关系记忆" : relationship.displayName;
			AddRelationshipList(items, relationship.knownFacts, MemoryUiItemKind.RelationshipKnownFact, MemoryUiCategory.Emotion, i, $"relationship:{i}:knownFacts", $"{relName} · 已知事实", relationship.lastTouchedAt);
			AddRelationshipList(items, relationship.openLoops, MemoryUiItemKind.RelationshipOpenLoop, MemoryUiCategory.Topic, i, $"relationship:{i}:openLoops", $"{relName} · 未闭合问题", relationship.lastTouchedAt);

			if (!string.IsNullOrWhiteSpace(relationship.lastActionAdvice))
			{
				items.Add(new MemoryUiItem
				{
					Id = $"relationship:{i}:advice",
					Kind = MemoryUiItemKind.RelationshipAdvice,
					ParentIndex = i,
					Category = MemoryUiCategory.Growth,
					Text = relationship.lastActionAdvice,
					Source = $"{relName} · 行动建议",
					DateText = FormatDate(relationship.lastTouchedAt)
				});
			}
		}

		for (int i = 0; i < source.readingContinuity.Count; i++)
		{
			ReadingContinuityEntry reading = source.readingContinuity[i];
			if (reading == null) continue;
			string text = !string.IsNullOrWhiteSpace(reading.shortVerdict)
				? reading.shortVerdict
				: reading.question;
			if (string.IsNullOrWhiteSpace(text)) continue;
			items.Add(new MemoryUiItem
			{
				Id = $"reading:{i}",
				Kind = MemoryUiItemKind.ReadingContinuity,
				ItemIndex = i,
				Category = MemoryUiCategory.Growth,
				Text = text,
				Source = "占卜连续性",
				DateText = FormatDate(reading.createdAt)
			});
		}

		for (int i = 0; i < source.tomorrowHooks.Count; i++)
		{
			TomorrowHook hook = source.tomorrowHooks[i];
			if (hook == null || string.IsNullOrWhiteSpace(hook.triggerText)) continue;
			items.Add(new MemoryUiItem
			{
				Id = $"tomorrow:{i}",
				Kind = MemoryUiItemKind.TomorrowHook,
				ItemIndex = i,
				Category = MemoryUiCategory.Growth,
				Text = hook.triggerText,
				Source = "明日线索",
				DateText = hook.scheduledForLocalDate
			});
		}

		for (int i = 0; i < source.candidates.Count; i++)
		{
			MemoryCandidate candidate = source.candidates[i];
			if (candidate == null || string.IsNullOrWhiteSpace(candidate.text)) continue;
			if (string.IsNullOrEmpty(candidate.id))
				candidate.id = NewId();

			bool pending = IsCandidatePending(candidate);
			bool enabled = IsCandidateEnabled(candidate);
			items.Add(new MemoryUiItem
			{
				Id = GetCandidateUiId(candidate, i),
				Kind = MemoryUiItemKind.Candidate,
				ItemIndex = i,
				Category = FromType(candidate.type),
				Text = candidate.text,
				Source = pending ? "待确认记忆" : candidate.sourceConversationId == "manual" ? "手动添加" : "对话总结",
				StatusLabel = pending ? "待确认" : enabled ? "已启用" : "已关闭",
				DateText = FormatDate(candidate.createdAt),
				Enabled = enabled,
				PendingConfirm = pending,
				Important = candidate.important
			});
		}

		return items
			.OrderByDescending(item => item.Important)
			.ThenBy(item => item.Enabled ? 0 : 1)
			.ThenByDescending(item => ParseDate(item.DateText))
			.ToList();
	}

	private static void AddStableList(List<MemoryUiItem> items, List<string> values, MemoryUiItemKind kind, MemoryUiCategory category, string idPrefix, string source)
	{
		if (values == null) return;
		for (int i = 0; i < values.Count; i++)
		{
			if (string.IsNullOrWhiteSpace(values[i])) continue;
			items.Add(new MemoryUiItem
			{
				Id = $"{idPrefix}:{i}",
				Kind = kind,
				ItemIndex = i,
				Category = category,
				Text = values[i],
				Source = source,
				DateText = "长期"
			});
		}
	}

	private static void AddRelationshipList(List<MemoryUiItem> items, List<string> values, MemoryUiItemKind kind, MemoryUiCategory category, int parentIndex, string idPrefix, string source, string date)
	{
		if (values == null) return;
		for (int i = 0; i < values.Count; i++)
		{
			if (string.IsNullOrWhiteSpace(values[i])) continue;
			items.Add(new MemoryUiItem
			{
				Id = $"{idPrefix}:{i}",
				Kind = kind,
				ParentIndex = parentIndex,
				ItemIndex = i,
				Category = category,
				Text = values[i],
				Source = source,
				DateText = FormatDate(date)
			});
		}
	}

	private static bool RemoveItem(MemoryUiItem item)
	{
		if (item == null) return false;
		MemorySource source = Source;
		switch (item.Kind)
		{
			case MemoryUiItemKind.Candidate:
				MemoryCandidate candidate = FindCandidate(item);
				return candidate != null && source.candidates.Remove(candidate);
			case MemoryUiItemKind.StablePreferredTone:
				source.stableProfile.preferredTone = "";
				return true;
			case MemoryUiItemKind.StableRecurringTheme:
				return RemoveAt(source.stableProfile.recurringThemes, item.ItemIndex);
			case MemoryUiItemKind.StableDoNotSay:
				return RemoveAt(source.stableProfile.doNotSay, item.ItemIndex);
			case MemoryUiItemKind.StableSafetyNote:
				return RemoveAt(source.stableProfile.safetyNotes, item.ItemIndex);
			case MemoryUiItemKind.RelationshipKnownFact:
				return RemoveAt(GetRelationship(item.ParentIndex)?.knownFacts, item.ItemIndex);
			case MemoryUiItemKind.RelationshipOpenLoop:
				return RemoveAt(GetRelationship(item.ParentIndex)?.openLoops, item.ItemIndex);
			case MemoryUiItemKind.RelationshipAdvice:
				RelationshipMemory relationship = GetRelationship(item.ParentIndex);
				if (relationship == null) return false;
				relationship.lastActionAdvice = "";
				return true;
			case MemoryUiItemKind.ReadingContinuity:
				return RemoveAt(source.readingContinuity, item.ItemIndex);
			case MemoryUiItemKind.TomorrowHook:
				return RemoveAt(source.tomorrowHooks, item.ItemIndex);
			default:
				return false;
		}
	}

	private static MemoryCandidate FindCandidate(MemoryUiItem item)
	{
		if (item == null) return null;
		string id = item.Id != null && item.Id.StartsWith("candidate:", StringComparison.Ordinal)
			? item.Id.Substring("candidate:".Length)
			: item.Id;
		return Source.candidates.FirstOrDefault(candidate => candidate != null && candidate.id == id);
	}

	private static RelationshipMemory GetRelationship(int index)
	{
		return index >= 0 && index < Source.relationships.Count ? Source.relationships[index] : null;
	}

	private static bool RemoveAt<T>(List<T> list, int index)
	{
		if (list == null || index < 0 || index >= list.Count) return false;
		list.RemoveAt(index);
		return true;
	}

	private static string GetCandidateUiId(MemoryCandidate candidate, int index)
	{
		if (candidate == null) return $"candidate:{index}";
		if (string.IsNullOrEmpty(candidate.id))
			candidate.id = NewId();
		return $"candidate:{candidate.id}";
	}

	private static MemorySource LoadLocalSource()
	{
		string json = PlayerPrefs.GetString(MemoryCacheKey, string.Empty);
		if (string.IsNullOrWhiteSpace(json)) return null;

		try
		{
			MemorySource source = JsonUtility.FromJson<MemorySource>(json);
			Normalize(source);
			return source;
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[MemoryUiStore] 本地记忆缓存读取失败，已重置。{ex.Message}");
			PlayerPrefs.DeleteKey(MemoryCacheKey);
			PlayerPrefs.Save();
			return null;
		}
	}

	private static void SaveLocalSource(MemorySource source)
	{
		source ??= new MemorySource();
		Normalize(source);
		PlayerPrefs.SetString(MemoryCacheKey, JsonUtility.ToJson(source));
		PlayerPrefs.Save();
	}

	private static bool HasPendingCloudSave()
	{
		return PlayerPrefs.GetInt(PendingCloudSaveKey, 0) == 1;
	}

	private static bool HasPendingCloudDelete()
	{
		return PlayerPrefs.GetInt(PendingCloudDeleteKey, 0) == 1;
	}

	private static void MarkCloudSavePending()
	{
		PlayerPrefs.SetInt(PendingCloudSaveKey, 1);
		PlayerPrefs.Save();
	}

	private static void MarkCloudSaveComplete()
	{
		PlayerPrefs.DeleteKey(PendingCloudSaveKey);
		PlayerPrefs.Save();
	}

	private static void MarkCloudDeletePending()
	{
		PlayerPrefs.SetInt(PendingCloudDeleteKey, 1);
		PlayerPrefs.Save();
	}

	private static void MarkCloudDeleteComplete()
	{
		PlayerPrefs.DeleteKey(PendingCloudDeleteKey);
		PlayerPrefs.Save();
	}

	private static void TrySyncPendingCloudSave(Action<bool> onComplete)
	{
		MemorySource source = LoadLocalSource() ?? Source;
		Normalize(source);
		DialogSystem.Instance?.SetMemorySource(source);

		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			onComplete?.Invoke(false);
			return;
		}

		firestore.SaveMemorySource(source, success =>
		{
			if (success)
				MarkCloudSaveComplete();
			onComplete?.Invoke(success);
		});
	}

	private static void TrySyncPendingCloudDelete(Action<bool> onComplete)
	{
		var firestore = FirestoreManager.Instance;
		if (firestore == null || !firestore.IsInitialized)
		{
			onComplete?.Invoke(false);
			return;
		}

		firestore.DeleteMemorySource(success =>
		{
			if (success)
				MarkCloudDeleteComplete();
			onComplete?.Invoke(success);
		});
	}

	private static string NewId()
	{
		return Guid.NewGuid().ToString("N").Substring(0, 12);
	}

	private static string NormalizeText(string text)
	{
		return string.IsNullOrWhiteSpace(text)
			? ""
			: text.Trim().Replace("\r", " ").Replace("\n", " ");
	}

	private static void Normalize(MemorySource source)
	{
		if (source == null) return;
		source.stableProfile ??= new StableProfile();
		source.stableProfile.recurringThemes ??= new List<string>();
		source.stableProfile.doNotSay ??= new List<string>();
		source.stableProfile.safetyNotes ??= new List<string>();
		source.relationships ??= new List<RelationshipMemory>();
		source.readingContinuity ??= new List<ReadingContinuityEntry>();
		source.candidates ??= new List<MemoryCandidate>();
		source.tomorrowHooks ??= new List<TomorrowHook>();
	}

	private static string FormatDate(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return "长期";
		if (DateTime.TryParse(value, out DateTime date))
			return date.ToString("yyyy.MM.dd");
		return value.Length > 10 ? value.Substring(0, 10).Replace("-", ".") : value.Replace("-", ".");
	}

	private static long ParseDate(string value)
	{
		if (DateTime.TryParse(value, out DateTime date))
			return date.Ticks;
		return 0;
	}
}
