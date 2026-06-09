using UnityEngine;
namespace XFGameFrameWork
{
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T m_instance;
        private static readonly object m_lock = new object();

        public static T Instance
        {
            get
            {
                if (m_instance == null)
                {
                    lock (m_lock)
                    {
                        if (m_instance == null)
                        {
                            // 查找场景中是否已有实例
                            m_instance = FindObjectOfType<T>();
                            if (m_instance == null)
                            {
                                // 创建新的 GameObject，并挂载组件
                                GameObject singletonObj = new GameObject(typeof(T).Name);
                                m_instance = singletonObj.AddComponent<T>();
                                DontDestroyOnLoad(singletonObj); // 如果你想它跨场景不销毁
                            }
                        }
                    }
                }
                return m_instance;
            }
        }

        protected virtual void Awake()
        {
            if (m_instance == null)
            {
                m_instance = this as T;
                DontDestroyOnLoad(gameObject); // 可选：防止被销毁
            }
            else if (m_instance != this)
            {
                Destroy(gameObject); // 避免重复单例
            }
        }
    }
}

