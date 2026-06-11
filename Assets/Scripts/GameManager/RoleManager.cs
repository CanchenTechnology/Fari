using System.Collections.Generic;
using GamerFrameWork;
using UnityEngine;
using XFGameFrameWork;

public enum CharacterType
{
    TarotReader = 0,//塔罗师
    Astrologer = 1, //占星师
    Meditator = 2,//冥想师

}
public class RoleManager : MonoSingleton<RoleManager>
{
    private const string KEY_CURRENT_ROLE_ID = "RoleData_CurrentRoleId";

    public List<RoleDataSO> roleDataList = new List<RoleDataSO>();

    private Dictionary<int, RoleDataSO> roleDict = new Dictionary<int, RoleDataSO>();

    public CharacterType characterType; //需要初始化
    private int currentRoleId = 0;

    protected override void Awake()
    {
        base.Awake();
        InitRoleDict();
        LoadRoleData();
    }

    private void InitRoleDict()
    {
        roleDict.Clear();

        foreach (var role in roleDataList)
        {
            if (role != null && !roleDict.ContainsKey(role.roleId))
            {
                roleDict.Add(role.roleId, role);
            }
        }
    }

    public RoleDataSO GetCurrentRoleData()
    {
        return GetRoleDataById(currentRoleId);
    }

    public void ChangeRole(int roleId)
    {
        if (roleDict.ContainsKey(roleId))
        {
            currentRoleId = roleId;
            characterType = (CharacterType)roleId;
            SaveRoleData();
            EventSystem.DispatchEvent(GameDataStr.UpdateRoleInfo, roleId);
        }
    }

    public RoleDataSO GetRoleDataById(int roleId)
    {
        roleDict.TryGetValue(roleId, out var role);
        return role;
    }

    #region 本地存储
    /// <summary>
    /// 保存当前角色ID到本地
    /// </summary>
    public void SaveRoleData()
    {
        PlayerPrefs.SetInt(KEY_CURRENT_ROLE_ID, currentRoleId);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 从本地读取角色ID，若本地无记录则保持默认值0
    /// </summary>
    private void LoadRoleData()
    {
        if (PlayerPrefs.HasKey(KEY_CURRENT_ROLE_ID))
        {
            int savedRoleId = PlayerPrefs.GetInt(KEY_CURRENT_ROLE_ID);
            if (roleDict.ContainsKey(savedRoleId))
            {
                currentRoleId = savedRoleId;
                characterType = (CharacterType)savedRoleId;
            }
        }
    }
    #endregion
}