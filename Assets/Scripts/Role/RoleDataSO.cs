using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "RoleDataSO", menuName = "ScriptableObjects/RoleDataSO", order = 1)]
public class RoleDataSO : ScriptableObject
{
    public string roleName;
    public string roleType;
    public string roleDescription;
    public Sprite roleImage;

    public RoleExtraData extraData;

}


[Serializable]
public class RoleExtraData
{
    public string roleName;
    public string roleDes;
    public string roleContent;

    public List<string> tags;

}
