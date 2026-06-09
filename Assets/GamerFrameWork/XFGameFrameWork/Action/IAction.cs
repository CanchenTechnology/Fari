using UnityEngine;

public interface IAction
{
    bool IsFinished { get; }
    void Start();
    void Update(float deltaTime);
    void BindTo(GameObject gameObject); // 自动解绑
}

public abstract class ActionBase : IAction
{
    protected GameObject boundTarget;
    public bool IsFinished { get; protected set; }

    public void BindTo(GameObject gameObject) => boundTarget = gameObject;
    public abstract void Start();
    public abstract void Update(float deltaTime);
}
