using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XFGameFrameWork.ActionXF
{
    public class ActionKitMonoBehaviourEvents :MonoSingleton<ActionKitMonoBehaviourEvents>
    {
        internal readonly EasyEvent OnUpdate = new EasyEvent();
        internal readonly EasyEvent OnFixedUpdate = new EasyEvent();
        internal readonly EasyEvent OnLateUpdate = new EasyEvent();
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        private void Update()
        {
            OnUpdate?.Trigger();
        }

        private void FixedUpdate()
        {
            OnFixedUpdate?.Trigger();
        }
        private void LateUpdate()
        {
            OnLateUpdate?.Trigger();
        }
    }

}

