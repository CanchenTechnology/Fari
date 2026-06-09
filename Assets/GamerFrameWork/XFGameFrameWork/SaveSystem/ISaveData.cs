using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace XFGameFrameWork.SaveSystem
{
    public interface ISaveData
    {
        void SaveData<T>(string key, T data, Action<bool,object> callback = null);
        void LoadData<T>(string key, Action<T> callback);
        void DeleteData(string key, Action<bool> callback = null);
        bool DataExists(string key);

    }
}


