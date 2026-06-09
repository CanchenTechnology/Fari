using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//**********************************************
//创建人：玖一
//功能说明：标记自动注册MVC,需要再类名前添加[AutoRegister("PlayerModel")]
//**********************************************
namespace XFGameFrameWork.MVC
{
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoRegisterAttribute : Attribute
    {
        public string Name;
        public AutoRegisterAttribute(string name)
        {
            Name = name;
        }
    }
}


