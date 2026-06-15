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
        /// 泛型委托事件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        public delegate void EventHandler<T>(T data);

        /// <summary>
        /// 事件派发注册字典（非泛型）
        /// </summary>
        private static Dictionary<string, List<EventHandler>> mEventDic = new Dictionary<string, List<EventHandler>>();

        /// <summary>
        /// 泛型事件派发注册字典：eventName -> Type -> List&lt;EventHandler&lt;T&gt;&gt;
        /// </summary>
        private static Dictionary<string, Dictionary<System.Type, object>> mGenericEventDic = new Dictionary<string, Dictionary<System.Type, object>>();

        #region 非泛型方法

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
            if (mEventDic.TryGetValue(eventName, out var sourceList))
            {
                eventList = new List<EventHandler>(sourceList);
            }
            if (eventList == null) return;
            for (int i = 0; i < eventList.Count; i++)
            {
                eventList[i]?.Invoke(data);
            }
        }

        #endregion

        #region 泛型方法

        /// <summary>
        /// 注册泛型事件
        /// </summary>
        public static void AddEventListener<T>(string eventName, EventHandler<T> handler)
        {
            if (!mGenericEventDic.TryGetValue(eventName, out var typeDic))
            {
                typeDic = new Dictionary<System.Type, object>();
                mGenericEventDic.Add(eventName, typeDic);
            }
            if (!typeDic.TryGetValue(typeof(T), out var listObj))
            {
                listObj = new List<EventHandler<T>>();
                typeDic.Add(typeof(T), listObj);
            }
            var list = (List<EventHandler<T>>)listObj;
            if (!list.Contains(handler))
            {
                list.Add(handler);
            }
        }

        /// <summary>
        /// 移除泛型事件
        /// </summary>
        public static void RemoveEventListener<T>(string eventName, EventHandler<T> handler)
        {
            if (mGenericEventDic.TryGetValue(eventName, out var typeDic))
            {
                if (typeDic.TryGetValue(typeof(T), out var listObj))
                {
                    var list = (List<EventHandler<T>>)listObj;
                    list.Remove(handler);
                }
            }
        }

        /// <summary>
        /// 分发泛型事件
        /// </summary>
        public static void DispatchEvent<T>(string eventName, T data)
        {
            if (mGenericEventDic.TryGetValue(eventName, out var typeDic))
            {
                if (typeDic.TryGetValue(typeof(T), out var listObj))
                {
                    var sourceList = (List<EventHandler<T>>)listObj;
                    var eventList = new List<EventHandler<T>>(sourceList);
                    for (int i = 0; i < eventList.Count; i++)
                    {
                        eventList[i]?.Invoke(data);
                    }
                }
            }
        }

        #endregion
    }
}

