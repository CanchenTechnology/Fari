// 定义一个可序列化的数据类
using UnityEngine;
using System;
using XFGameFrameWork.SaveSystem;
[Serializable]
public class PlayerData
{
    public string playerName;
    public int level;
    public float experience;
    public DateTime lastPlayed;
}

public class ExampleUsage : MonoBehaviour
{
    private void Start()
    {
        // 保存数据到PlayerPrefs
        var data = new PlayerData
        {
            playerName = "TestPlayer",
            level = 5,
            experience = 1250.5f,
            lastPlayed = DateTime.Now
        };

        SaveSystemManager.Instance.SaveData("player_data", data, SaveSystemManager.StorageType.PlayerPrefs, (success,obj) =>
        {
            if (success)
            {
                Debug.Log("Data saved to PlayerPrefs successfully");

                // 从PlayerPrefs加载数据
                SaveSystemManager.Instance.LoadData<PlayerData>("player_data", (loadedData) =>
                {
                    if (loadedData != null)
                    {
                        Debug.Log($"Loaded player: {loadedData.playerName}, Level: {loadedData.level}");

                        // 迁移数据到JSON文件
                        SaveSystemManager.Instance.MigrateData<PlayerData>("player_data",
                            SaveSystemManager.StorageType.PlayerPrefs,
                            SaveSystemManager.StorageType.JsonFile,
                            (migrateSuccess) =>
                            {
                                if (migrateSuccess)
                                {
                                    Debug.Log("Data migrated to JSON successfully");
                                }
                            });
                    }
                }, SaveSystemManager.StorageType.PlayerPrefs);
            }
        });

        // 保存数据到服务器
        SaveSystemManager.Instance.SaveData("player_server_data", data, SaveSystemManager.StorageType.Server, (success, obj) =>
        {
            if (success)
            {
                Debug.Log($"Data saved to server successfully:{(string)obj}");
            }
        });
        SaveSystemManager.Instance.SaveData("Player_Data", data, SaveSystemManager.StorageType.JsonFile, (success, obj) =>
        {
            if (success)
            {
                
            }
        });
    }
}