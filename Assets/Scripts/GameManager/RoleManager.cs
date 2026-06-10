using System.Collections.Generic;
using GamerFrameWork;
using XFGameFrameWork;

public class RoleManager : MonoSingleton<RoleManager>
{
    public List<RoleDataSO> roleDataList = new List<RoleDataSO>();

    private Dictionary<int, RoleDataSO> roleDict = new Dictionary<int, RoleDataSO>();

    private int currentRoleId = 0;

    protected void Awake()
    {
        base.Awake();
        InitRoleDict();
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

            EventSystem.DispatchEvent(GameDataStr.UpdateRoleInfo, roleId);
        }
    }

    public RoleDataSO GetRoleDataById(int roleId)
    {
        roleDict.TryGetValue(roleId, out var role);
        return role;
    }
}