using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DontDestory : MonoBehaviour
{
    private static DontDestory _instance;

    public static DontDestory Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<DontDestory>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("DontDestory");
                    _instance = obj.AddComponent<DontDestory>();
                    DontDestroyOnLoad(obj);
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
