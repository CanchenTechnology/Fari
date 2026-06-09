using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace XFGameFrameWork.Pool
{
    public interface IPooable
    {
        void OnSpawn();//从池中取出时调用
        void OnRecycle();//回收进池时调用
    }
}

