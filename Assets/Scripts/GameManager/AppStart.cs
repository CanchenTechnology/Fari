using System.Collections;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using UnityEngine;

public class AppStart : MonoBehaviour
{
    private void Awake()
    {
        UIModule.Instance.Initialize(); 
    }
    // Start is called before the first frame update
    void Start()
    {
        NotificationSettingsManager settings = NotificationSettingsManager.Instance;
        AppNotificationScheduler.Instance.SyncFromSettings(settings);
        RemotePushManager.Instance.Initialize();
        AppReadinessDiagnostics.LogCurrentState("AppStart");
        UIModule.Instance.PopUpWindow<LoginUI>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
