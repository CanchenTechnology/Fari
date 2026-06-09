using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

//**********************************************
//创建人：玖一
//功能说明：MVC.AutoRegisterAll();
//**********************************************
namespace XFGameFrameWork.MVC
{
    public static class MVC
    {
        public static Dictionary<string, IModel> models = new Dictionary<string, IModel>();
        public static Dictionary<string, IView> views = new Dictionary<string, IView>();
        public static Dictionary<string, IController> controllers = new Dictionary<string, IController>();


        /// <summary>
        /// 注册模型(数据)层
        /// </summary>
        /// <param name="name"></param>
        /// <param name="model"></param>
        public static void RegisterModel(IModel model)
        {
            var key = model.GetType().Name;
            if (!models.ContainsKey(key))
            {
                models.Add(key, model);
                model.Init();
            }
        }
        /// <summary>
        /// 注册视图(UI)层
        /// </summary>
        /// <param name="name"></param>
        /// <param name="view"></param>
        public static void RegisterView(IView view)
        {
            var key = view.GetType().Name;
            if (!views.ContainsKey(key))
            {
                Debug.Log($"注册View：{key}");
                views.Add(key, view);
                view.Init();
            }
        }

        /// <summary>
        /// 注册控制层
        /// </summary>
        /// <param name="name"></param>
        /// <param name="controller"></param>
        public static void RegisterController(IController controller)
        {
            var key = controller.GetType().Name;
            if (!controllers.ContainsKey(key))
            {
                controllers.Add(key, controller);
                controller.Init();
            }
        }
        public static T GetModel<T>() where T : class, IModel
        {
            var name = typeof(T).Name;
            return models.TryGetValue(name, out var model) ? model as T : null;
        }
        public static T GetView<T>() where T : class, IView
        {
            var name = typeof(T).Name;
            return views.TryGetValue(name, out var view) ? view as T : null;
        }
        public static T GetController<T>() where T : class, IController
        {
            var name = typeof(T).Name;
            return controllers.TryGetValue(name, out var controller) ? controller as T : null;
        }
        public static void SendEvent(string controllerName, string eventName, object data = null)
        {
            if (controllers.TryGetValue(controllerName, out var controller))
            {
                controller.Execute(eventName, data);
            }
            else
            {
                Debug.LogWarning($"未注册 Controller: {controllerName}");
            }
        }


    }



}