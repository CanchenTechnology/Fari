using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 存放游戏资源路径
/// </summary>
public class GameDataStr
{
    //资源路径
    public static readonly string RoleDataPath = "RoleDataSO"; //角色数据路径




    //事件
    public static readonly string UpdateRoleInfo = "UpdateRoleInfo"; //更新角色信息事件
    public static readonly string RefreshChatUI = "RefreshChatUI"; //刷新聊天UI事件
    public static readonly string QuickQuestionSelected = "QuickQuestionSelected"; //快速占卜问题选中事件
    public static readonly string CardTopicSelected = "CardTopicSelected"; //卡牌话题选中事件（从解读页面跳转）
}
