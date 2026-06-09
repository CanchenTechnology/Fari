using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GamerFrameWork
{
    /// <summary>
    /// UI事件派发中心
    /// 由逻辑层调用，UI层接收
    /// 代替直接交互，进行解耦
    /// </summary>
    public class EventSystem
    {
        /// <summary>
        /// 委托事件
        /// </summary>
        /// <param name="data"></param>
        public delegate void EventHandler(object data);
        /// <summary>
        /// 事件派发注册字典
        /// </summary>
        private static Dictionary<string, List<EventHandler>> mEventDic = new Dictionary<string, List<EventHandler>>();

        /// <summary>
        /// 注册事件
        /// </summary>
        /// <param name="eventType"></param>
        /// <param name="eventHandler"></param>
        public static void AddEvent(string eventName, EventHandler eventHandler)
        {
            if (!mEventDic.ContainsKey(eventName))
            {
                mEventDic.Add(eventName, new List<EventHandler>());
            }
            if (!mEventDic[eventName].Contains(eventHandler))
            {
                mEventDic[eventName].Add(eventHandler);
            }
        }
        /// <summary>
        /// 移除事件
        /// </summary>
        /// <param name="eventType"></param>
        /// <param name="eventHandler"></param>
        public static void RemoveEvent(string eventName, EventHandler eventHandler)
        {
            if (mEventDic.ContainsKey(eventName))
            {
                if (mEventDic[eventName].Contains(eventHandler))
                {
                    mEventDic[eventName].Remove(eventHandler);
                }
            }
        }
        /// <summary>
        /// 分发事件
        /// </summary>
        /// <param name="eventType"></param>
        /// <param name="data"></param>
        public static void DispatchEvent(string eventName, object data = null)
        {
            List<EventHandler> eventList = null;
            if (mEventDic.ContainsKey(eventName))
            {
                eventList = mEventDic[eventName];
            }
            for (int i = 0; i < eventList.Count; i++)
            {
                eventList[i]?.Invoke(data);
            }
        }
    }
}

