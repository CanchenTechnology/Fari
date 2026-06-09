using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

//**********************************************
//创建人：玖一
//功能说明：
//**********************************************
namespace XFGameFrameWork.ResLoader
{
    public interface IResourceLoader
    {
        T Load<T>(string path) where T : UnityEngine.Object;
        Task<T> LoadAsync<T>(string path) where T : UnityEngine.Object;
        Task<IList<T>> LoadAllAsync<T>(string path) where T : UnityEngine.Object; // 新增
        void Unload(string path);
    }

}


