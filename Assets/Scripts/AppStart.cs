using System.Collections;
using System.Collections.Generic;
using GamerFrameWork.UIFrameWork;
using UnityEngine;

public class AppStart : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        UIModule.Instance.PopUpWindow<LoginUI>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
