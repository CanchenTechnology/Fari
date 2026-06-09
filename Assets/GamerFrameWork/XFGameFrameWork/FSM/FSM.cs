using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFGameFrameWork.FSM
{
    public interface IState
    {
        void OnEnter();
        void OnUpdate();
        void OnFixedUpdate();
        void OnExit();
    }

    /// <summary>
    /// 可链式配置的状态类，实现 IState 接口
    /// </summary>
    public class State : IState
    {
        private Action _onEnter;
        private Action _onUpdate;
        private Action _onFixedUpdate;
        private Action _onExit;

        public State OnEnter(Action action) { _onEnter = action; return this; }
        public State OnUpdate(Action action) { _onUpdate = action; return this; }
        public State OnFixedUpdate(Action action) { _onFixedUpdate = action; return this; }
        public State OnExit(Action action) { _onExit = action; return this; }

        public virtual void OnEnter() => _onEnter?.Invoke();
        public virtual void OnUpdate() => _onUpdate?.Invoke();
        public virtual void OnFixedUpdate() => _onFixedUpdate?.Invoke();
        public virtual void OnExit() => _onExit?.Invoke();
    }

    /// <summary>
    /// 泛型状态机类，可用于任意 enum 作为 Key
    /// </summary>
    public class FSM<T>
    {
        private Dictionary<T, IState> _states = new Dictionary<T, IState>();
        private IState _currentState;
        private T _currentKey;

        public int FrameCountOfCurrentState { get; private set; }

        /// <summary>
        /// 链式添加状态（自动创建并返回 State 实例）
        /// </summary>
        public State AddState(T key)
        {
            var state = new State();
            _states[key] = state;
            return state;
        }

        /// <summary>
        /// 添加自定义状态（例如继承 State 的自定义子类）
        /// </summary>
        public void AddState(T key, IState state)
        {
            _states[key] = state;
        }

        /// <summary>
        /// 切换状态
        /// </summary>
        public void ChangeState(T newKey)
        {
            if (_currentState != null && EqualityComparer<T>.Default.Equals(_currentKey, newKey))
                return;

            _currentState?.OnExit();

            if (_states.TryGetValue(newKey, out var newState))
            {
                _currentState = newState;
                _currentKey = newKey;
                FrameCountOfCurrentState = 0;
                _currentState.OnEnter();
            }
            else
            {
                Debug.LogWarning($"FSM: 无法找到状态：{newKey}");
            }
        }

        public void Update()
        {
            _currentState?.OnUpdate();
            FrameCountOfCurrentState++;
        }

        public void FixedUpdate()
        {
            _currentState?.OnFixedUpdate();
        }

        public T CurrentKey => _currentKey;
    }
}
