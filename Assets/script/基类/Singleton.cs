using UnityEngine;


    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T _instance;
        private static object _lock = new object();

        public static T MainInstance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance = Object.FindAnyObjectByType<T>();
                    
                        if (_instance == null)
                        {
                            GameObject go = new GameObject(typeof(T).Name);
                            _instance = go.AddComponent<T>();
                        }
                    }
                }

                return _instance;
            }
        }
        

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = (T)this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                // 只移除重复组件，不销毁整个 GameObject：防止误挂在网络预制体上时连 NetworkObject 一起删掉
                Destroy(this);
            }
        }
    }

