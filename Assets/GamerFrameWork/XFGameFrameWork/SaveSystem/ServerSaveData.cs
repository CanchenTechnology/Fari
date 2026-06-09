using System;
using UnityEngine.Networking;
using UnityEngine;

namespace XFGameFrameWork.SaveSystem
{
    public class ServerSaveData : ISaveData
    {
        private string _serverUrl; //服务器链接

        public ServerSaveData(string serverUrl)
        {
            _serverUrl = serverUrl;
        }

        public void SaveData<T>(string key, T data, Action<bool,object> callback = null)
        {
            string jsonData = JsonUtility.ToJson(data);
            string url = $"{_serverUrl}/save/{key}";

            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonData);

            UnityWebRequest request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(jsonBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            operation.completed += (asyncOp) =>
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Server save failed: {request.error}");
                    callback?.Invoke(false,null);
                }
                else
                {
                    callback?.Invoke(true,null);
                }
                request.Dispose();
            };
        }


        public void LoadData<T>(string key, Action<T> callback)
        {
            string url = $"{_serverUrl}/load/{key}";

            UnityWebRequest request = UnityWebRequest.Get(url);

            var operation = request.SendWebRequest();
            operation.completed += (asyncOp) =>
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Server load failed: {request.error}");
                    callback?.Invoke(default(T));
                }
                else
                {
                    T data = JsonUtility.FromJson<T>(request.downloadHandler.text);
                    callback?.Invoke(data);
                }
                request.Dispose();
            };
        }

        public void DeleteData(string key, Action<bool> callback = null)
        {
            string url = $"{_serverUrl}/delete/{key}";

            UnityWebRequest request = UnityWebRequest.Delete(url);

            var operation = request.SendWebRequest();
            operation.completed += (asyncOp) =>
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Server delete failed: {request.error}");
                    callback?.Invoke(false);
                }
                else
                {
                    callback?.Invoke(true);
                }
                request.Dispose();
            };
        }

        public bool DataExists(string key)
        {
            // 对于服务器实现，这需要同步检查，可能需要特殊处理
            // 这里简化为总是返回true，实际项目中可能需要实现异步检查
            Debug.LogWarning("Synchronous server check not implemented properly");
            return true;
        }
    }
}
