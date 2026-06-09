using System.Collections.Generic;
using System;
using UnityEngine;
using XFGameFrameWork.SaveSystem;
namespace XFGameFrameWork.SaveSystem
{
    public class SaveSystemManager : MonoBehaviour
    {
        public enum StorageType
        {
            PlayerPrefs,
            JsonFile,
            Server
        }

        private static SaveSystemManager _instance;
        public static SaveSystemManager Instance => _instance;

        private Dictionary<StorageType, ISaveData> _dataServices;

        [SerializeField] private StorageType _defaultStorageType = StorageType.PlayerPrefs;
        [SerializeField] private string _serverUrl = "http://yourserver.com/api";

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeDataServices();
        }

        private void InitializeDataServices()
        {
            _dataServices = new Dictionary<StorageType, ISaveData>
        {
            { StorageType.PlayerPrefs, new PlayerPrefsSaveData() },
            { StorageType.JsonFile, new JsonFileSaveData() },
            { StorageType.Server, new ServerSaveData(_serverUrl) }
        };
        }
        /// <summary>
        /// 保存数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">
        /// PlayerPrefs:是键
        /// JsonFile:是文件名
        /// Server:是文件夹名字
        /// </param>
        /// <param name="data"></param>
        /// <param name="storageType"></param>
        /// <param name="callback"></param>
        public void SaveData<T>(string key, T data, StorageType? storageType = null, Action<bool,object> callback = null)
        {
            var type = storageType ?? _defaultStorageType;
            if (_dataServices.TryGetValue(type, out var service))
            {
                service.SaveData(key, data, callback);
            }
            else
            {
                Debug.LogError($"No data service found for type: {type}");
                callback?.Invoke(false, null);
            }
        }

        public void LoadData<T>(string key, Action<T> callback, StorageType? storageType = null)
        {
            var type = storageType ?? _defaultStorageType;
            if (_dataServices.TryGetValue(type, out var service))
            {
                service.LoadData(key, callback);
            }
            else
            {
                Debug.LogError($"No data service found for type: {type}");
                callback?.Invoke(default(T));
            }
        }

        public void DeleteData(string key, StorageType? storageType = null, Action<bool> callback = null)
        {
            var type = storageType ?? _defaultStorageType;
            if (_dataServices.TryGetValue(type, out var service))
            {
                service.DeleteData(key, callback);
            }
            else
            {
                Debug.LogError($"No data service found for type: {type}");
                callback?.Invoke(false);
            }
        }

        public bool DataExists(string key, StorageType? storageType = null)
        {
            var type = storageType ?? _defaultStorageType;
            if (_dataServices.TryGetValue(type, out var service))
            {
                return service.DataExists(key);
            }

            Debug.LogError($"No data service found for type: {type}");
            return false;
        }

        // 高级功能：数据迁移
        public void MigrateData<T>(string key, StorageType from, StorageType to, Action<bool> callback)
        {
            LoadData<T>(key, (data) =>
            {
                if (data != null)
                {
                    SaveData(key, data, to, (success, obj) =>
                    {
                        if (success)
                        {
                            DeleteData(key, from, callback);
                        }
                        else
                        {
                            callback?.Invoke(false);
                        }
                    });
                }
                else
                {
                    callback?.Invoke(false);
                }
            }, from);
        }
    }
}
