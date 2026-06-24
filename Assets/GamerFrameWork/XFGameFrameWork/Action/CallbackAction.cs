using System;


namespace XFGameFrameWork.ActionXF
{
    public class CallbackAction : ActionBase
    {
        private Action action;

        public CallbackAction(Action action)
        {
            this.action = action;
        }

        public override void Start()
        {
            if (boundTarget == null)
            {
                IsFinished = true;
                return;
            }

            action?.Invoke();
            IsFinished = true;
        }

        public override void Update(float deltaTime)
        {
            // 无需处理，每帧立即完成
        }
    }
}

