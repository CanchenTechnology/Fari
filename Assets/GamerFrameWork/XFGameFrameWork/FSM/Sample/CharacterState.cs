using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFGameFrameWork.FSM;

//**********************************************
//创建人：玖一
//功能说明：
//**********************************************
public enum CharacterStateType
{
    Idle,
    Move,
    Attack,
    Dead,
}
public class IdleState : IState
{
    public void OnEnter()
    {
        Debug.Log("Idle进入待机状态");
    }

    public void OnExit()
    {
        Debug.Log("Idle离开待机状态");
    }

    public void OnFixedUpdate()
    {
        Debug.Log("Idle待机中...");
    }

    public void OnUpdate()
    {
        Debug.Log("Idle待机中...");
    }
}
public class MoveState : IState
{
    public void OnEnter()
    {
        Debug.Log("Move进入待机状态");
    }

    public void OnExit()
    {
        Debug.Log("Move离开待机状态");
    }

    public void OnFixedUpdate()
    {
        Debug.Log("Move待机中...");
    }

    public void OnUpdate()
    {
        Debug.Log("Move待机中...");
    }
}

