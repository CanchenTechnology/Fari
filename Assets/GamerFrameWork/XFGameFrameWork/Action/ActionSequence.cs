using System;
using System.Collections;
using UnityEngine;
namespace XFGameFrameWork.ActionXF
{
    public class ActionSequence
    {
        private SequenceAction sequence = new SequenceAction();

        public static ActionSequence Create() => new ActionSequence();

        public ActionSequence Lerp(float from, float to, float duration, Action<float> onUpdate, Action onComplete = null, Func<float, float> easing = null)
        {
            sequence.Add(new LerpAction(from, to, duration, onUpdate, onComplete, easing));
            return this;
        }

        public ActionSequence Delay(float duration, Action onComplete = null)
        {
            sequence.Add(new DelayAction(duration, onComplete));
            return this;
        }

        public ActionSequence Parallel(params IAction[] actions)
        {
            sequence.Add(new ParallelAction(actions));
            return this;
        }
        public ActionSequence Parallel(Action<ActionSequence> builder)
        {
            var nested = new ActionSequence();
            builder.Invoke(nested);
            sequence.Add(new ParallelAction(nested.sequence.GetActions()));
            return this;
        }

        public ActionSequence Callback(Action action)
        {
            sequence.Add(new CallbackAction(action));
            return this;
        }

        public ActionSequence ThenSequence(Action<ActionSequence> sequenceBuilder)
        {
            var nested = new ActionSequence();
            sequenceBuilder.Invoke(nested);
            sequence.Add(nested.sequence); // 加入内部的序列动作
            return this;
        }
        public ActionSequence Append(ActionSequence nested)
        {
            sequence.Add(nested.sequence);
            return this;
        }

        public void Start(MonoBehaviour runner, GameObject bindTo = null, Action onComplete = null)
        {
            if (bindTo != null)
                sequence.BindTo(bindTo);

            sequence.Start(); // 先启动序列
            runner.StartCoroutine(UpdateRoutine(onComplete)); // 再开启更新
        }

        private IEnumerator UpdateRoutine(Action onComplete)
        {
            while (!sequence.IsFinished)
            {
                sequence.Update(Time.deltaTime);
                yield return null;
            }

            onComplete?.Invoke(); // 动作结束后调用
        }

    }

}
