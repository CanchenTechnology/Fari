using System.Collections;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using UnityEngine;
using XFGameFrameWork;

public class GameManager : MonoSingleton<GameManager>
{
    public bool isRegister;

    private void Update() {
        if(Input.GetKeyDown(KeyCode.T))
        {
            UIModule.Instance.GetWindow<TodayOracleUI>().StartDrawCardAnimation();
        }
        
    }
    
}
