using UnityEngine;
using AnadoluFethi.StateMachine;

namespace AnadoluFethi.GameStateMachine
{
    public class GameState : BaseState
    {
        private readonly GameStateMachineController _controller;

        public GameState(StateMachineController stateMachine, GameStateMachineController controller)
            : base(stateMachine)
        {
            _controller = controller;
        }

        public override void Enter()
        {
            Debug.Log("[GameState] Game started.");
        }

        public override void Update()
        {
        }

        public override void FixedUpdate()
        {
        }

        public override void Exit()
        {
            Debug.Log("[GameState] Game ended.");
        }
    }
}
