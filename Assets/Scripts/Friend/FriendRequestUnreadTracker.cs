using System.Collections.Generic;
using UnityEngine;

public static class FriendRequestUnreadTracker
{
    private const string SeenRequestKeyPrefix = "FriendRequestSeenIds_";
    private const int MaxSeenRequestKeys = 300;

    public static int CountUnread(IReadOnlyList<FriendDataManager.InviteData> invites)
    {
        if (invites == null || invites.Count == 0)
            return 0;

        HashSet<string> seenKeys = LoadSeenKeys();
        int count = 0;
        for (int i = 0; i < invites.Count; i++)
        {
            FriendDataManager.InviteData invite = invites[i];
            string key = BuildInviteKey(invite);
            if (!string.IsNullOrWhiteSpace(key)
                && string.IsNullOrWhiteSpace(invite?.seenAt)
                && !seenKeys.Contains(key))
            {
                count++;
            }
        }

        return count;
    }

    public static void MarkSeen(IReadOnlyList<FriendDataManager.InviteData> invites)
    {
        if (invites == null || invites.Count == 0)
            return;

        HashSet<string> seenKeys = LoadSeenKeys();
        List<string> orderedKeys = new List<string>();
        string seenAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        for (int i = 0; i < invites.Count; i++)
        {
            string key = BuildInviteKey(invites[i]);
            if (string.IsNullOrWhiteSpace(key) || orderedKeys.Contains(key))
                continue;

            invites[i].seenAt = seenAt;
            orderedKeys.Add(key);
            seenKeys.Add(key);
        }

        foreach (string key in seenKeys)
        {
            if (orderedKeys.Count >= MaxSeenRequestKeys)
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
        return $"{SeenRequestKeyPrefix}{uid.Trim()}";
    }

    private static string BuildInviteKey(FriendDataManager.InviteData invite)
    {
        if (invite == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(invite.firebaseUid))
            return invite.firebaseUid.Trim();

        return $"{invite.id}|{invite.email}|{invite.name}".Trim();
    }
}
