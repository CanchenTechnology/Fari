using System;
using System.IO;
using UnityEngine;

namespace XFGameFrameWork.SaveSystem
{
    public class JsonFileSaveData : ISaveData
    {
        private string _dataPath;

        public JsonFileSaveData()
        {
            _dataPath = GetPlatformSavePath();

            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
            }

            Debug.Log("Save Path : " + _dataPath);
        }

        /// <summary>
        /// 获取不同平台存储路径
        /// </summary>
        private string GetPlatformSavePath()
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, "../SavedData");

#elif UNITY_STANDALONE_WIN
            return Path.Combine(Application.persistentDataPath, "SavedData");

#elif UNITY_ANDROID
            return Path.Combine(Application.persistentDataPath, "SavedData");

#elif UNITY_IOS
            return Path.Combine(Application.persistentDataPath, "SavedData");

#else
            return Path.Combine(Application.persistentDataPath, "SavedData");
#endif
        }

        public void SaveData<T>(string key, T data, Action<bool,object> callback = null)
        {
            string filePath = Path.Combine(_dataPath, $"{key}.json");

            try
            {
                string json = JsonUtility.ToJson(data, true);
#if UNITY_ANDROID
                // Android 建议使用 using
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    writer.Write(json);
                }
#else
                File.WriteAllText(filePath, json);
#endif

#if UNITY_IOS
                SetNoBackupFlag(filePath);
#endif

                callback?.Invoke(true,filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON Save Failed : {e}");
                callback?.Invoke(false, filePath);
            }
        }

        public void LoadData<T>(string key, Action<T> callback)
        {
            string filePath = Path.Combine(_dataPath, $"{key}.json");

            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"JSON not found : {key}");
                callback?.Invoke(default);
                return;
            }

            try
            {
                string json;

#if UNITY_ANDROID
                using (StreamReader reader = new StreamReader(filePath))
                {
                    json = reader.ReadToEnd();
                }
#else
                json = File.ReadAllText(filePath);
#endif

                T data = JsonUtility.FromJson<T>(json);

                callback?.Invoke(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON Load Failed : {e}");
                callback?.Invoke(default);
            }
        }

        public void DeleteData(string key, Action<bool> callback = null)
        {
            string filePath = Path.Combine(_dataPath, $"{key}.json");

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                callback?.Invoke(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"JSON Delete Failed : {e}");
                callback?.Invoke(false);
            }
        }

        public bool DataExists(string key)
        {
            string filePath = Path.Combine(_dataPath, $"{key}.json");
            return File.Exists(filePath);
        }

#if UNITY_IOS
        /// <summary>
        /// iOS 防止 iCloud 备份
        /// </summary>
        private void SetNoBackupFlag(string filePath)
        {
            try
            {
                UnityEngine.iOS.Device.SetNoBackupFlag(filePath);
            }
            catch { }
        }
#endif
    }
}