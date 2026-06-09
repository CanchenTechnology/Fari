using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFGameFrameWork
{
    public class BindableProperty<T>
    {
        private T value;
        private event Action<T> valueChanged;

        private Dictionary<GameObject, Action> autoUnRegisterActions = new Dictionary<GameObject, Action>();
        private List<BindableProperty<T>> orSources;

        public T Value
        {
            get => value;
            set
            {
                if (!Equals(this.value, value))
                {
                    this.value = value;
                    valueChanged?.Invoke(this.value);
                }
            }
        }

        public BindableProperty() { }

        public BindableProperty(T value)
        {
            this.value = value;
        }

        /// <summary>
        /// 只注册变化通知（无初始推送）
        /// </summary>
        public BindableProperty<T> Register(Action<T> onValueChanged)
        {
            return RegisterInternal(onValueChanged, new HashSet<BindableProperty<T>>());
        }

        private BindableProperty<T> RegisterInternal(Action<T> onValueChanged, HashSet<BindableProperty<T>> visited)
        {
            if (visited.Contains(this)) return this;
            visited.Add(this);

            if (orSources != null)
            {
                foreach (var source in orSources)
                {
                    source.RegisterInternal(onValueChanged, visited);
                }
            }
            else
            {
                valueChanged += onValueChanged;
            }
            return this;
        }

        /// <summary>
        /// 注册并立即推送当前值
        /// </summary>
        public BindableProperty<T> RegisterWithInitValue(Action<T> onValueChanged)
        {
            Register(onValueChanged);
            onValueChanged?.Invoke(this.value);
            return this;
        }

        /// <summary>
        /// 注销变化通知
        /// </summary>
        public BindableProperty<T> UnRegister(Action<T> onValueChanged)
        {
            valueChanged -= onValueChanged;
            return this;
        }

        /// <summary>
        /// GameObject 销毁时自动注销绑定
        /// </summary>
        public void UnRegisterWhenGameObjectDestroyed(GameObject gameObject)
        {
            if (gameObject == null) return;

            if (orSources != null)
            {
                foreach (var source in orSources)
                {
                    if (source != null && source != this)
                    {
                        source.UnRegisterWhenGameObjectDestroyed(gameObject);
                    }
                }
                return;
            }

            if (!autoUnRegisterActions.ContainsKey(gameObject))
            {
                Action unRegisterAction = () =>
                {
                    valueChanged = null;
                    autoUnRegisterActions.Remove(gameObject);
                };

                autoUnRegisterActions.Add(gameObject, unRegisterAction);
                var helper = gameObject.GetComponent<BindablePropertyAutoUnregisterHelper>();
                if (helper == null)
                {
                    helper = gameObject.AddComponent<BindablePropertyAutoUnregisterHelper>();
                }
                helper.RegisterAction(unRegisterAction);
            }
        }


        /// <summary>
        /// Or 组合绑定（支持多个源）
        /// </summary>
        public BindableProperty<T> Or(BindableProperty<T> other)
        {
            if (other == null || other == this) return this;

            if (orSources == null)
            {
                orSources = new List<BindableProperty<T>> { this };
            }

            if (!orSources.Contains(other))
            {
                orSources.Add(other);
            }

            return this;
        }

        /// <summary>
        /// 注册无参数版本（仅通知变化）
        /// </summary>
        public BindableProperty<T> Register(Action onChanged)
        {
            return RegisterInternal(_ => onChanged?.Invoke(), new HashSet<BindableProperty<T>>());
        }

        /// <summary>
        /// 注册无参数版本（立即推送）
        /// </summary>
        public BindableProperty<T> RegisterWithInitValue(Action onChanged)
        {
            Register(onChanged);
            onChanged?.Invoke();
            return this;
        }

        private BindableProperty<T> RegisterInternal(Action onChanged, HashSet<BindableProperty<T>> visited)
        {
            if (visited.Contains(this)) return this;
            visited.Add(this);

            if (orSources != null)
            {
                foreach (var source in orSources)
                {
                    source.RegisterInternal(onChanged, visited);
                }
            }
            else
            {
                valueChanged += _ => onChanged?.Invoke();
            }
            return this;
        }
    }
}
