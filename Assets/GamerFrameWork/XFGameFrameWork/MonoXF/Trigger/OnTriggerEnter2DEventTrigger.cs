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
    public class OnTriggerEnter2DEventTrigger : MonoBehaviour
    {
        public EasyEvent<Collider2D> OnTriggerEnter2DEvent = new EasyEvent<Collider2D>();
        private void OnTriggerEnter2D(Collider2D other)
        {
            OnTriggerEnter2DEvent.Trigger(other);
        }

    }
    public static class OnTriggerEnter2DEventTriggerExtension
    {
        public static IUnRegister OnTriggerEnter2DEvent<T>(this T self, Action<Collider2D> onTriggerEnter2D)
            where T : Component
        {
            return self.GetOrAddComponent<OnTriggerEnter2DEventTrigger>().OnTriggerEnter2DEvent
                .Register(onTriggerEnter2D);
        }

        public static IUnRegister OnTriggerEnter2DEvent(this GameObject self, Action<Collider2D> onTriggerEnter2D)
        {
            return self.GetOrAddComponent<OnTriggerEnter2DEventTrigger>().OnTriggerEnter2DEvent
                .Register(onTriggerEnter2D);
        }
    }
}


