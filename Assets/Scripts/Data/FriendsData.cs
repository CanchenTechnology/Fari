using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个好友数据
/// </summary>
[System.Serializable]
public class FriendData
{
    public string friendId;
    public string friendName;
    public Sprite friendAvatar;
    public string friendType;
    public string friendCity;
    public bool isRealFriend;
}

/// <summary>
/// 好友分组数据（如：已有好友、创建的好友）
/// </summary>
[System.Serializable]
public class FriendGroupData
{
    public string groupName;
    public bool isExpanded = true;
    public List<FriendData> friends = new List<FriendData>();
}
