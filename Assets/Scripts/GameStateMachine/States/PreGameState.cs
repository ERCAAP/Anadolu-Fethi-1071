using UnityEngine;
using AnadoluFethi.StateMachine;

namespace AnadoluFethi.GameStateMachine
{
    public class PreGameState : BaseState
    {
        private readonly GameStateMachineController _controller;

        public PreGameState(StateMachineController stateMachine, GameStateMachineController controller)
            : base(stateMachine)
        {
            _controller = controller;
        }

        public override void Enter()
        {
            // Called when entering this state
        }

        public override void Update()
        {
            // Called every frame while in this state
        }

        public override void FixedUpdate()
        {
            // Called every fixed update while in this state
        }

        public override void Exit()
        {
            // Called when exiting this state
        }
    }
}
