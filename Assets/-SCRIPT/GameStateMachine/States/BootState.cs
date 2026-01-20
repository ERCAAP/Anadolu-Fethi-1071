using UnityEngine;
using AnadoluFethi.StateMachine;

namespace AnadoluFethi.GameStateMachine
{
    public class BootState : BaseState
    {
        private readonly GameStateMachineController _controller;

        public BootState(StateMachineController stateMachine, GameStateMachineController controller)
            : base(stateMachine)
        {
            _controller = controller;
        }

        public override void Enter()
        {
            Debug.Log("[BootState] Initializing game...");
            // Initialize game systems here (save data, settings, etc.)

            // Transition to MainMenu when ready
            _controller.ChangeState<MainMenuState>();
        }

        public override void Update()
        {
        }

        public override void FixedUpdate()
        {
        }

        public override void Exit()
        {
            Debug.Log("[BootState] Boot complete.");
        }
    }
}
