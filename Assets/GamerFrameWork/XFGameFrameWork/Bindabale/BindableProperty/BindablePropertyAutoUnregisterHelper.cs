using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//**********************************************
//创建人：玖一
//功能说明：
//**********************************************
namespace XFGameFrameWork
{
    public class BindablePropertyAutoUnregisterHelper : MonoBehaviour
    {
        public List<Action> onDestroyActions = new List<Action>();
        public void RegisterAction(Action action)
        {
            onDestroyActions.Add(action);
        }
        private void OnDestroy()
        {
            foreach (var action in onDestroyActions)
            {
                action?.Invoke();
            }
            onDestroyActions.Clear();
        }
    }
}


