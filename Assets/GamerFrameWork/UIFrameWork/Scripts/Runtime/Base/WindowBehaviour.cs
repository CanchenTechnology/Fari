using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GamerFrameWork.UIFrameWork
{
    /// <summary>
    /// WindowBehaviour:是所有界面都必须继承的最基础最顶层的类,主要负责声明周期以及基础属性的声明
    /// </summary>
    public abstract class WindowBehaviour
    {
        public GameObject gameObject { get; set; }//当前窗口物体
        public Transform transform { get; set; }
        public Canvas Canvas { get; set; }
        public WindowLayer Layer { get; set; }//窗口层级
        public string Name { get; set; }//窗口名称
        public bool Visible { get; set; }//是否在显示
        public bool IsUpdate { get; protected set; }//是否开启Update渲染帧更新
        public bool PopStack { get; set; }//是否通过堆栈系统弹出的弹窗
        /// <summary>
        /// 全屏窗口标志(在窗口Awake接口中进行设置,智能显隐开启后当全屏弹窗弹出时,被遮挡的窗口都会通过伪隐藏隐藏掉,从而提升性能)
        /// </summary>
        public bool FullScreenWindow { get; set; }
        public Action<WindowBase> PopStackListener { get; set; }
        /// <summary>
        /// 只会在物体创建时执行一次,与Mono中Awake调用时机和次数保持一致
        /// </summary>
        public virtual void OnAwake() { }
        /// <summary>
        /// 在物体显示时执行一次，与Mono中OnEnable一致
        /// </summary>
        public virtual void OnShow() { }
        /// <summary>
        /// 渲染帧更新接口(需在Awake中把Update字段设置为True,对应窗口才会开启OnUpdate回调,防止性能滥用)
        /// </summary>
        public virtual void OnUpdate() { }
        /// <summary>
        /// 在物体隐藏时执行一次,与Mono中OnDisable一致
        /// </summary>
        public virtual void OnHide() { }
        /// <summary>
        /// 在当前界面被摧毁时调用一次
        /// </summary>
        public virtual void OnDestroy() { }
        /// <summary>
        /// 设置物体的可见性
        /// </summary>
        /// <param name="isVisable"></param>
        public virtual void SetVisible(bool isVisable) { }

    }
}
