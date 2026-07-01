/*---------------------------------
 * Title: UI表现层脚本自动化生成工具-不会被覆盖
 * Author: GamerFrameWork
 * Date: 6/30/2026 9:17:33 AM
 * Description: UI 表现层，该层只负责界面的交互、表现相关的更新，不允许编写任何业务逻辑代码
 * 注意: 以下文件是自动生成的，再次生成不会覆盖原有的代码，会在原有的代码上进行新增，可放心使用
---------------------------------*/
using UnityEngine.UI;
using UnityEngine;
using GamerFrameWork.OracleRuntime;
using GamerFrameWork.UIFrameWork;
using SuperScrollView;
using System;
using System.Collections.Generic;
using TMPro;
using XFGameFrameWork;

public class AllDivinationHistoryUI : WindowBase
{
	private const string HistoryItemPrefabName = "divinationItem";
	private const string MyHistoryOwnerName = "我";
	private const string MyHistoryEmptyTitle = "暂无占卜记录";
	private const string MyHistoryEmptyDescription = "完成一次占卜后会自动保存到这里。";

	public AllDivinationHistoryUIComponent uiComponent;

	private static List<AllDivinationHistoryEntry> sPendingEntries = new List<AllDivinationHistoryEntry>();
	private static string sPendingEmptyTitle = "暂无占卜记录";
	private static string sPendingEmptyDescription = "还没有可以展示的占卜记录。";
	private static string sPendingOwnerName = "好友";
	private static FriendDataManager.FriendData sPendingRelationshipFriend;
	private static readonly Dictionary<string, List<AllDivinationHistoryEntry>> sBestEntriesByOwner =
		new Dictionary<string, List<AllDivinationHistoryEntry>>();

	private readonly List<AllDivinationHistoryEntry> historyEntries = new List<AllDivinationHistoryEntry>();
	private LoopListView2 historyListView;
	private TMP_Text divinationNameDesText;
	private bool historyListInitialized;

	#region 生命周期函数
	// 调用机制与 Mono Awake 一致
	public override void OnAwake()
	{
		uiComponent = gameObject.GetComponent<AllDivinationHistoryUIComponent>();
		if (uiComponent == null)
		{
			Debug.LogError("AllDivinationHistoryUI 缺少 UI 组件绑定脚本：AllDivinationHistoryUIComponent");
			return;
		}
		uiComponent.InitComponent(this);
		this.Canvas.sortingOrder = (int)uiComponent.windowLayer;
		InitHistoryListView();
		base.OnAwake();
	}
	// 物体显示时执行
	public override void OnShow()
	{
		base.OnShow();
		ApplyPendingEntries();
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
	public static AllDivinationHistoryUI ShowLoading(
		string description = "正在同步好友公开的占卜摘要...",
		string ownerName = null,
		FriendDataManager.FriendData relationshipFriend = null)
	{
		sPendingEntries = new List<AllDivinationHistoryEntry>
		{
			AllDivinationHistoryEntry.Placeholder("正在加载", description, "占卜历史", string.Empty)
		};
		sPendingEmptyTitle = "暂无占卜记录";
		sPendingEmptyDescription = "还没有可以展示的占卜记录。";
		sPendingOwnerName = ResolveOwnerName(ownerName);
		sPendingRelationshipFriend = relationshipFriend;

		AllDivinationHistoryUI window = UIModule.Instance.PopUpWindow<AllDivinationHistoryUI>();
		window?.ApplyPendingEntries();
		return window;
	}

	public static AllDivinationHistoryUI Show(
		List<AllDivinationHistoryEntry> entries,
		string emptyTitle = "暂无占卜记录",
		string emptyDescription = "还没有可以展示的占卜记录。",
		string ownerName = null,
		FriendDataManager.FriendData relationshipFriend = null)
	{
		sPendingEntries = CloneEntries(entries);
		sPendingEmptyTitle = emptyTitle;
		sPendingEmptyDescription = emptyDescription;
		sPendingOwnerName = ResolveOwnerName(ownerName);
		sPendingRelationshipFriend = relationshipFriend;

		AllDivinationHistoryUI window = UIModule.Instance.PopUpWindow<AllDivinationHistoryUI>();
		window?.ApplyPendingEntries();
		return window;
	}

	public static AllDivinationHistoryUI ShowMyHistory()
	{
		DivinationHistoryCacheService cache = DivinationHistoryCacheService.Instance;
		List<DivinationRecordData> cachedRecords = cache.GetSnapshot();
		bool hasInitialRecords = cache.HasLoadedOnce || cachedRecords.Count > 0;
		AllDivinationHistoryUI window = hasInitialRecords
			? Show(
				ToEntries(cachedRecords, MyHistoryOwnerName),
				MyHistoryEmptyTitle,
				MyHistoryEmptyDescription,
				MyHistoryOwnerName)
			: ShowLoading("正在同步你的占卜历史...", MyHistoryOwnerName);

		cache.Refresh(false, (records, success) =>
		{
			if (window == null) return;

			window.SetEntries(
				ToEntries(records, MyHistoryOwnerName),
				MyHistoryEmptyTitle,
				MyHistoryEmptyDescription,
				MyHistoryOwnerName);

			if (!success && (records == null || records.Count == 0))
				ToastManager.ShowToast("历史服务暂不可用");
		});

		return window;
	}

	public void SetEntries(
		List<AllDivinationHistoryEntry> entries,
		string emptyTitle = "暂无占卜记录",
		string emptyDescription = "还没有可以展示的占卜记录。",
		string ownerName = null,
		FriendDataManager.FriendData relationshipFriend = null)
	{
		sPendingEntries = CloneEntries(entries);
		sPendingEmptyTitle = emptyTitle;
		sPendingEmptyDescription = emptyDescription;
		if (!string.IsNullOrWhiteSpace(ownerName))
			sPendingOwnerName = ResolveOwnerName(ownerName);
		if (relationshipFriend != null)
			sPendingRelationshipFriend = relationshipFriend;
		ApplyPendingEntries();
	}

	private void ApplyPendingEntries()
	{
		RefreshTitleText();
		List<AllDivinationHistoryEntry> entries = CloneEntries(sPendingEntries);
		entries = MergeWithCurrentEntriesIfNeeded(entries);

		historyEntries.Clear();
		if (entries != null)
		{
			foreach (AllDivinationHistoryEntry entry in entries)
			{
				if (entry != null)
					historyEntries.Add(entry);
			}
		}

		if (historyEntries.Count == 0)
		{
			historyEntries.Add(AllDivinationHistoryEntry.Placeholder(
				sPendingEmptyTitle,
				sPendingEmptyDescription,
				"占卜历史",
				string.Empty));
		}

		RefreshHistoryList();
		CacheEntriesIfNeeded();
	}

	private List<AllDivinationHistoryEntry> MergeWithCurrentEntriesIfNeeded(List<AllDivinationHistoryEntry> incomingEntries)
	{
		if (incomingEntries == null)
			incomingEntries = new List<AllDivinationHistoryEntry>();

		int incomingConcreteCount = CountConcreteEntries(incomingEntries);
		int currentConcreteCount = CountConcreteEntries(historyEntries);
		List<AllDivinationHistoryEntry> cachedEntries = GetCachedOwnerEntries();
		int cachedConcreteCount = CountConcreteEntries(cachedEntries);
		int bestKnownCount = Mathf.Max(currentConcreteCount, cachedConcreteCount);
		if (bestKnownCount == 0)
			return incomingEntries;
		if (!IsSameOwnerAsCurrentEntries())
			return incomingEntries;
		if (incomingConcreteCount >= bestKnownCount)
			return incomingEntries;

		List<AllDivinationHistoryEntry> merged = new List<AllDivinationHistoryEntry>();
		HashSet<string> addedKeys = new HashSet<string>();
		AddUniqueEntries(merged, addedKeys, incomingEntries);
		AddUniqueEntries(merged, addedKeys, historyEntries);
		AddUniqueEntries(merged, addedKeys, cachedEntries);
		SortEntriesByTimeDesc(merged);
		return merged;
	}

	private bool IsSameOwnerAsCurrentEntries()
	{
		string pendingOwnerName = ResolveOwnerName(sPendingOwnerName);
		foreach (AllDivinationHistoryEntry entry in historyEntries)
		{
			if (entry == null || IsPlaceholderEntry(entry))
				continue;
			string entryOwnerName = ResolveOwnerName(entry.ownerName);
			if (!string.Equals(entryOwnerName, pendingOwnerName, StringComparison.Ordinal))
				return false;
		}

		return true;
	}

	private List<AllDivinationHistoryEntry> GetCachedOwnerEntries()
	{
		string ownerKey = ResolveOwnerName(sPendingOwnerName);
		return sBestEntriesByOwner.TryGetValue(ownerKey, out List<AllDivinationHistoryEntry> entries)
			? CloneEntries(entries)
			: new List<AllDivinationHistoryEntry>();
	}

	private void CacheEntriesIfNeeded()
	{
		int concreteCount = CountConcreteEntries(historyEntries);
		if (concreteCount == 0)
			return;

		string ownerKey = ResolveOwnerName(sPendingOwnerName);
		int cachedCount = sBestEntriesByOwner.TryGetValue(ownerKey, out List<AllDivinationHistoryEntry> cachedEntries)
			? CountConcreteEntries(cachedEntries)
			: 0;
		if (concreteCount < cachedCount)
			return;

		sBestEntriesByOwner[ownerKey] = CloneEntries(historyEntries);
	}

	private static void AddUniqueEntries(
		List<AllDivinationHistoryEntry> target,
		HashSet<string> addedKeys,
		List<AllDivinationHistoryEntry> source)
	{
		if (target == null || addedKeys == null || source == null)
			return;

		foreach (AllDivinationHistoryEntry entry in source)
		{
			if (entry == null || IsPlaceholderEntry(entry))
				continue;

			string key = BuildEntryKey(entry);
			if (!addedKeys.Add(key))
				continue;

			target.Add(entry.Clone());
		}
	}

	private static int CountConcreteEntries(List<AllDivinationHistoryEntry> entries)
	{
		if (entries == null) return 0;

		int count = 0;
		foreach (AllDivinationHistoryEntry entry in entries)
		{
			if (entry != null && !IsPlaceholderEntry(entry))
				count++;
		}

		return count;
	}

	private static bool IsPlaceholderEntry(AllDivinationHistoryEntry entry)
	{
		if (entry == null) return true;
		if (entry.detailRecord != null) return false;
		if (entry.cardSprites != null && entry.cardSprites.Count > 0) return false;

		string title = entry.cardNames ?? string.Empty;
		return title.Contains("暂无")
			|| title.Contains("正在")
			|| title.Contains("用户未公开")
			|| title.Contains("未公开");
	}

	private static string BuildEntryKey(AllDivinationHistoryEntry entry)
	{
		if (entry == null)
			return string.Empty;
		if (!string.IsNullOrWhiteSpace(entry.detailRecord?.readingId))
			return $"record:{entry.detailRecord.readingId.Trim()}";
		if (!string.IsNullOrWhiteSpace(entry.detailRecord?.oracleId))
			return $"oracle:{entry.detailRecord.oracleId.Trim()}";

		return $"{entry.source}|{entry.time}|{entry.cardNames}|{entry.description}".Trim();
	}

	private static void SortEntriesByTimeDesc(List<AllDivinationHistoryEntry> entries)
	{
		if (entries == null || entries.Count <= 1)
			return;

		entries.Sort((left, right) => ParseEntryTime(right?.time).CompareTo(ParseEntryTime(left?.time)));
	}

	private static DateTime ParseEntryTime(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return DateTime.MinValue;

		string trimmed = value.Trim();
		string[] formats = { "yyyy.M.d", "yyyy.MM.dd", "yyyy-M-d", "yyyy-MM-dd", "yyyy/M/d", "yyyy/MM/dd", "o" };
		if (DateTime.TryParseExact(trimmed, formats, null, System.Globalization.DateTimeStyles.None, out DateTime exact))
			return exact;
		return DateTime.TryParse(trimmed, out DateTime parsed) ? parsed : DateTime.MinValue;
	}

	private void RefreshTitleText()
	{
		TMP_Text titleText = ResolveDivinationNameDesText();
		if (titleText != null)
			titleText.text = $"{ResolveOwnerName(sPendingOwnerName)}的所有占卜历史";
	}

	private TMP_Text ResolveDivinationNameDesText()
	{
		if (divinationNameDesText != null)
			return divinationNameDesText;

		divinationNameDesText = uiComponent != null ? uiComponent.divinationNameDesText : null;
		if (divinationNameDesText != null)
			return divinationNameDesText;

		divinationNameDesText = FindTextByObjectName(transform, "divinationNameDesText");
		if (divinationNameDesText == null)
			divinationNameDesText = FindTextUnderObjectName(transform, "topTitle");
		if (divinationNameDesText == null)
			divinationNameDesText = FindHistoryTitleText(transform);
		return divinationNameDesText;
	}

	private TMP_Text FindTextByObjectName(Transform root, params string[] names)
	{
		if (root == null || names == null)
			return null;

		for (int i = 0; i < names.Length; i++)
		{
			if (root.name == names[i])
				return root.GetComponent<TMP_Text>();
		}

		for (int i = 0; i < root.childCount; i++)
		{
			TMP_Text result = FindTextByObjectName(root.GetChild(i), names);
			if (result != null)
				return result;
		}

		return null;
	}

	private TMP_Text FindTextUnderObjectName(Transform root, string objectName)
	{
		Transform target = FindTransformByName(root, objectName);
		return target != null ? target.GetComponentInChildren<TMP_Text>(true) : null;
	}

	private Transform FindTransformByName(Transform root, string objectName)
	{
		if (root == null || string.IsNullOrWhiteSpace(objectName))
			return null;
		if (root.name == objectName)
			return root;

		for (int i = 0; i < root.childCount; i++)
		{
			Transform result = FindTransformByName(root.GetChild(i), objectName);
			if (result != null)
				return result;
		}

		return null;
	}

	private TMP_Text FindHistoryTitleText(Transform root)
	{
		if (root == null)
			return null;

		TMP_Text text = root.GetComponent<TMP_Text>();
		if (text != null
			&& !string.IsNullOrWhiteSpace(text.text)
			&& (text.text.Contains("All Divination History") || text.text.Contains("所有占卜历史")))
			return text;

		for (int i = 0; i < root.childCount; i++)
		{
			TMP_Text result = FindHistoryTitleText(root.GetChild(i));
			if (result != null)
				return result;
		}

		return null;
	}

	private static string ResolveOwnerName(string ownerName)
	{
		return string.IsNullOrWhiteSpace(ownerName) ? "好友" : ownerName.Trim();
	}

	private static List<AllDivinationHistoryEntry> CloneEntries(List<AllDivinationHistoryEntry> entries)
	{
		List<AllDivinationHistoryEntry> result = new List<AllDivinationHistoryEntry>();
		if (entries == null) return result;

		foreach (AllDivinationHistoryEntry entry in entries)
		{
			if (entry == null) continue;
			result.Add(entry.Clone());
		}

		return result;
	}

	private static List<AllDivinationHistoryEntry> ToEntries(List<DivinationRecordData> records, string ownerName)
	{
		List<AllDivinationHistoryEntry> entries = new List<AllDivinationHistoryEntry>();
		if (records == null) return entries;

		foreach (DivinationRecordData record in records)
		{
			AllDivinationHistoryEntry entry = ToEntry(record, ownerName);
			if (entry != null)
				entries.Add(entry);
		}

		return entries;
	}

	private static AllDivinationHistoryEntry ToEntry(DivinationRecordData record, string ownerName)
	{
		if (record == null) return null;

		List<Sprite> sprites = new List<Sprite>();
		List<string> cardNames = new List<string>();
		if (record.lockedCards != null)
		{
			foreach (LockedCard card in record.lockedCards)
			{
				if (card == null) continue;

				cardNames.Add(TarotDeck.FormatDisplayName(FirstNonEmpty(card.cardName, card.cardId, "未知牌"), card.orientation));
				Sprite sprite = LoadCardSprite(card);
				if (sprite != null)
					sprites.Add(sprite);
			}
		}

		return new AllDivinationHistoryEntry
		{
			cardSprites = sprites,
			cardNames = cardNames.Count > 0 ? string.Join(" & ", cardNames) : FirstNonEmpty(record.CardsSummary, record.SpreadLabel, "占卜记录"),
			description = FirstNonEmpty(record.shortVerdict, record.judgeContent, record.adviceContent, record.question, "已完成一次占卜。"),
			source = FirstNonEmpty(record.SpreadLabel, "占卜历史"),
			time = FormatHistoryDate(record.createdAt),
			ownerName = ownerName,
			detailRecord = record
		};
	}

	private static Sprite LoadCardSprite(LockedCard card)
	{
		string cardId = FirstNonEmpty(card?.cardId, FindTarotCardIdByName(card?.cardName));
		return string.IsNullOrWhiteSpace(cardId) ? null : TarotSpriteLoader.Load(cardId);
	}

	private static string FindTarotCardIdByName(string cardName)
	{
		string normalizedName = TarotDeck.StripOrientationSuffix(cardName);
		if (string.IsNullOrWhiteSpace(normalizedName))
			return string.Empty;

		foreach (TarotCard card in TarotDeck.FullDeck)
		{
			if (card == null) continue;
			if (string.Equals(card.nameZh, normalizedName, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(card.nameEn, normalizedName, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(card.cardId, normalizedName, StringComparison.OrdinalIgnoreCase))
				return card.cardId;
		}

		return string.Empty;
	}

	private static string FormatHistoryDate(string value)
	{
		if (string.IsNullOrWhiteSpace(value)) return string.Empty;
		if (DateTime.TryParse(value.Trim(), out DateTime parsed))
			return $"{parsed.Year}.{parsed.Month}.{parsed.Day}";
		return value.Trim();
	}

	private static string FirstNonEmpty(params string[] values)
	{
		if (values == null) return string.Empty;
		foreach (string value in values)
		{
			if (!string.IsNullOrWhiteSpace(value))
				return value.Trim();
		}

		return string.Empty;
	}

	private void InitHistoryListView()
	{
		if (historyListInitialized)
			return;

		historyListView = uiComponent != null ? uiComponent.historyScrollViewLoopListView2 : null;
		if (historyListView == null)
			historyListView = gameObject.GetComponentInChildren<LoopListView2>(true);
		if (historyListView == null)
		{
			Debug.LogError("[AllDivinationHistoryUI] historyScrollViewLoopListView2 未绑定，无法渲染占卜历史列表");
			return;
		}

		historyListView.InitListView(0, OnGetHistoryItemByIndex);
		historyListInitialized = true;
	}

	private void RefreshHistoryList()
	{
		InitHistoryListView();
		if (!historyListInitialized || historyListView == null)
			return;

		historyListView.SetListItemCount(historyEntries.Count, true);
		historyListView.RefreshAllShownItem();
	}

	private LoopListViewItem2 OnGetHistoryItemByIndex(LoopListView2 listView, int index)
	{
		if (index < 0 || index >= historyEntries.Count)
			return null;

		LoopListViewItem2 item = listView.NewListViewItem(HistoryItemPrefabName);
		if (item == null)
		{
			Debug.LogError($"[AllDivinationHistoryUI] 找不到 SuperScrollView Item 预制体: {HistoryItemPrefabName}");
			return null;
		}

		BindHistoryItem(item.gameObject, historyEntries[index]);
		return item;
	}

	private void BindHistoryItem(GameObject itemObject, AllDivinationHistoryEntry entry)
	{
		if (itemObject == null || entry == null)
			return;

		DivinationItem item = itemObject.GetComponent<DivinationItem>();
		if (item == null)
			item = itemObject.GetComponentInChildren<DivinationItem>(true);
		if (item == null)
			return;

		bool showSource = !IsPlaceholderEntry(entry);
		item.SetData(entry.cardSprites, entry.cardNames, entry.description, entry.source, entry.time, showSource);
		item.SetClickAction(entry.detailRecord != null
			? () => TarotDetailedUI.Show(entry.detailRecord, ResolveOwnerName(entry.ownerName ?? sPendingOwnerName), false, true, sPendingRelationshipFriend)
			: null);
	}

	#endregion

	#region UI组件事件
	public void OnbackButtonClick()
	{
		HideWindow();
	}
	#endregion
}

public class AllDivinationHistoryEntry
{
	public List<Sprite> cardSprites = new List<Sprite>();
	public string cardNames;
	public string description;
	public string source;
	public string time;
	public string ownerName;
	public DivinationRecordData detailRecord;

	public static AllDivinationHistoryEntry Placeholder(string title, string description, string source, string time)
	{
		return new AllDivinationHistoryEntry
		{
			cardSprites = new List<Sprite>(),
			cardNames = string.IsNullOrWhiteSpace(title) ? "暂无占卜记录" : title.Trim(),
			description = string.IsNullOrWhiteSpace(description) ? "还没有可以展示的占卜记录。" : description.Trim(),
			source = string.IsNullOrWhiteSpace(source) ? "占卜历史" : source.Trim(),
			time = time ?? string.Empty,
			detailRecord = null
		};
	}

	public AllDivinationHistoryEntry Clone()
	{
		return new AllDivinationHistoryEntry
		{
			cardSprites = cardSprites != null ? new List<Sprite>(cardSprites) : new List<Sprite>(),
			cardNames = cardNames ?? string.Empty,
			description = description ?? string.Empty,
			source = source ?? string.Empty,
			time = time ?? string.Empty,
			ownerName = ownerName ?? string.Empty,
			detailRecord = detailRecord
		};
	}
}
