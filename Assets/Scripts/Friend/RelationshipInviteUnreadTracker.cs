using System.Collections.Generic;
using UnityEngine;

public static class RelationshipInviteUnreadTracker
{
    private const string SeenInviteKeyPrefix = "RelationshipInviteSeenIds_";
    private const int MaxSeenInviteKeys = 300;

    public static int CountUnread(IReadOnlyList<RelationshipDivinationRecord> records)
    {
        if (records == null || records.Count == 0)
            return 0;

        HashSet<string> seenKeys = LoadSeenKeys();
        int count = 0;
        for (int i = 0; i < records.Count; i++)
        {
            RelationshipDivinationRecord record = records[i];
            string key = BuildRecordKey(record);
            if (!string.IsNullOrWhiteSpace(key)
                && string.IsNullOrWhiteSpace(record?.receiverSeenAt)
                && !seenKeys.Contains(key))
            {
                count++;
            }
        }

        return count;
    }

    public static void MarkSeen(IReadOnlyList<RelationshipDivinationRecord> records)
    {
        if (records == null || records.Count == 0)
            return;

        HashSet<string> seenKeys = LoadSeenKeys();
        List<string> orderedKeys = new List<string>();

        for (int i = 0; i < records.Count; i++)
        {
            string key = BuildRecordKey(records[i]);
            if (string.IsNullOrWhiteSpace(key) || orderedKeys.Contains(key))
                continue;

            records[i].receiverSeenAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            orderedKeys.Add(key);
            seenKeys.Add(key);
        }

        foreach (string key in seenKeys)
        {
            if (orderedKeys.Count >= MaxSeenInviteKeys)
                break;
            if (!orderedKeys.Contains(key))
                orderedKeys.Add(key);
        }

        PlayerPrefs.SetString(GetStorageKey(), string.Join("\n", orderedKeys));
        PlayerPrefs.Save();
    }

    private static HashSet<string> LoadSeenKeys()
    {
        HashSet<string> keys = new HashSet<string>();
        string raw = PlayerPrefs.GetString(GetStorageKey(), string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return keys;

        string[] values = raw.Split('\n');
        for (int i = 0; i < values.Length; i++)
        {
            string key = values[i]?.Trim();
            if (!string.IsNullOrWhiteSpace(key))
                keys.Add(key);
        }

        return keys;
    }

    private static string GetStorageKey()
    {
        string uid = RelationshipDivinationFlow.GetCurrentUid();
        if (string.IsNullOrWhiteSpace(uid))
            uid = "anonymous";
        return $"{SeenInviteKeyPrefix}{uid.Trim()}";
    }

    private static string BuildRecordKey(RelationshipDivinationRecord record)
    {
        if (record == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(record.readingId))
            return record.readingId.Trim();

        return $"{record.initiatorUid}|{record.receiverUid}|{record.createdAt}|{record.question}".Trim();
    }
}
