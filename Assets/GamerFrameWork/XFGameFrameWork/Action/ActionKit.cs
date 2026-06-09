using System;

namespace XFGameFrameWork.ActionXF
{
    public static partial class ActionKit
    {
        public static ActionSequence Sequence() => ActionSequence.Create();

        public static IAction DelayFrame(int frameCount, Action onComplete)
        {
            return new DelayFrameAction(frameCount, onComplete);
        }
        public static EasyEvent OnUpdate => ActionKitMonoBehaviourEvents.Instance.OnUpdate;
    }
}
