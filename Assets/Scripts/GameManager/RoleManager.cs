using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFGameFrameWork;
using GamerFrameWork;
using System;
/// <summary>
/// 角色数据管理器
/// </summary>
public class RoleManager : MonoSingleton<RoleManager>
{
    public List<RoleDataSO> roleDataList = new List<RoleDataSO>();

    private int currentRoleIndex = 0; //当前角色下标，默认为0

    private void Start()
    {
    }

    public RoleDataSO GetCurrentRoleData()
    {
        if (roleDataList != null && roleDataList.Count > 0)
        {
            return roleDataList[currentRoleIndex];
        }
        return null;
    }
    public void ChangeRole(int index)
    {
        if (index >= 0 && index < roleDataList.Count)
        {
            currentRoleIndex = index;
            //这里可以添加其他需要更新角色信息的逻辑
            EventSystem.DispatchEvent(GameDataStr.UpdateRoleInfo, currentRoleIndex);
        }
    }
    public RoleDataSO GetRoleDataByIndex(int index)
    {
        if (index >= 0 && index < roleDataList.Count)
        {
            return roleDataList[index];
        }
        return null;
    }


}
