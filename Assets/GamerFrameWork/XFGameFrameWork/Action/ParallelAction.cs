using System.Collections.Generic;

public class ParallelAction : ActionBase
{
    private List<IAction> actions = new List<IAction>();

    public ParallelAction(params IAction[] actions)
    {
        this.actions.AddRange(actions);
    }

    public ParallelAction(List<IAction> actions)
    {
        this.actions = actions;
    }

    public override void Start()
    {
        foreach (var action in actions)
        {
            action.Start();
        }
    }

    public override void Update(float deltaTime)
    {
        if (IsFinished) return;

        for (int i = actions.Count - 1; i >= 0; i--)
        {
            var action = actions[i];
            action.Update(deltaTime);
            if (action.IsFinished)
            {
                actions.RemoveAt(i);
            }
        }

        if (actions.Count == 0)
        {
            IsFinished = true;
        }
    }
}
