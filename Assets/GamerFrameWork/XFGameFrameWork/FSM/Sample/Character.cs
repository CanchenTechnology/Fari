using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFGameFrameWork.FSM;

//**********************************************
//创建人：玖一
//功能说明：
//**********************************************
public class Character : MonoBehaviour
{
    private FSM<CharacterStateType> _fsm;
    private void Start()
    {
        _fsm = new FSM<CharacterStateType>();
        _fsm.AddState(CharacterStateType.Idle,new IdleState());
        _fsm.AddState(CharacterStateType.Move,new MoveState());
        _fsm.ChangeState(CharacterStateType.Idle);
    }
    private void Update()
    {
        _fsm.Update();
        if (Input.GetKeyDown(KeyCode.Space))
            _fsm.ChangeState(CharacterStateType.Move);

    }
}

