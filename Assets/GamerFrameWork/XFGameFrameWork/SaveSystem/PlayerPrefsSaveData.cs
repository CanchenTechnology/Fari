using System;
using UnityEngine;
namespace XFGameFrameWork.SaveSystem
{
    public class PlayerPrefsSaveData : ISaveData
    {
        public void SaveData<T>(string key, T data, Action<bool,object> callback = null)
        {
            try
            {
                string jsonData = JsonUtility.ToJson(data);
                PlayerPrefs.SetString(key, jsonData);
                PlayerPrefs.Save();
                callback?.Invoke(true,null);
            }
            catch (Exception e)
            {
                Debug.LogError($"PlayerPrefs save failed: {e.Message}");
                callback?.Invoke(false,null);
            }
        }

        public void LoadData<T>(string key, Action<T> callback)
        {
            if (PlayerPrefs.HasKey(key))
            {
                string jsonData = PlayerPrefs.GetString(key);
                T data = JsonUtility.FromJson<T>(jsonData);
                callback?.Invoke(data);
            }
            else
            {
                Debug.LogWarning($"No data found for key: {key}");
                callback?.Invoke(default(T));
            }
        }

        public void DeleteData(string key, Action<bool> callback = null)
        {
            try
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                callback?.Invoke(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"PlayerPrefs delete failed: {e.Message}");
                callback?.Invoke(false);
            }
        }

        public bool DataExists(string key)
        {
            return PlayerPrefs.HasKey(key);
        }
    }
}
