using System.Collections.Generic;
using System;
using UnityEngine;

public static class Ease
{
    public static float Linear(float t) => t;
    public static float InQuad(float t) => t * t;
    public static float OutQuad(float t) => t * (2 - t);
    public static float InOutQuad(float t) => t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;
    // 扩展更多
}

public class LerpAction : ActionBase
{
    private float from, to, duration, elapsed;
    private Action<float> onUpdate;
    private Action onComplete;
    private Func<float, float> easing;

    public LerpAction(float from, float to, float duration, Action<float> onUpdate, Action onComplete = null, Func<float, float> easing = null)
    {
        this.from = from;
        this.to = to;
        this.duration = duration;
        this.onUpdate = onUpdate;
        this.onComplete = onComplete;
        this.easing = easing ?? Ease.Linear;
    }

    public override void Start() => elapsed = 0f;

    public override void Update(float deltaTime)
    {
        if (IsFinished) return;
        elapsed += deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float value = Mathf.Lerp(from, to, easing(t));
        onUpdate?.Invoke(value);

        if (elapsed >= duration)
        {
            onComplete?.Invoke();
            IsFinished = true;
        }
    }
}

public class DelayAction : ActionBase
{
    private float duration, elapsed;
    private Action onComplete;

    public DelayAction(float duration, Action onComplete = null)
    {
        this.duration = duration;
        this.onComplete = onComplete;
    }

    public override void Start() => elapsed = 0f;

    public override void Update(float deltaTime)
    {
        if (IsFinished)
        {
            return;
        } 
        elapsed += deltaTime;
        if (elapsed >= duration)
        {
            onComplete?.Invoke(); // 触发回调
            IsFinished = true;
        }
    }
}


public class SequenceAction : ActionBase
{
    private List<IAction> actions = new List<IAction>();
    private int currentIndex = 0;
    private IAction current;

    public SequenceAction Add(IAction action)
    {
        actions.Add(action);
        return this;
    }

    public override void Start()
    {
        currentIndex = 0;
        NextAction();
    }

    public override void Update(float deltaTime)
    {
        if (IsFinished || current == null) return;

        current.Update(deltaTime);
        if (current.IsFinished)
            NextAction();
    }

    private void NextAction()
    {
       
        if (currentIndex < actions.Count)
        {
            current = actions[currentIndex++];

            current.Start();
        }
        else
        {
            current = null;
            IsFinished = true;
        }
    }

    // ✅ 供 Parallel(ActionSequence) 使用
    public List<IAction> GetActions() => actions;
}

