using UnityEngine;
using AnadoluFethi.StateMachine;

namespace AnadoluFethi.GameStateMachine
{
    public class GameStateMachineController : MonoBehaviour
    {
        private StateMachineController _stateMachine;

        public StateMachineController StateMachine => _stateMachine;

        private void Awake()
        {
            _stateMachine = new StateMachineController();
            InitializeStates();
        }

        private void InitializeStates()
        {
            // Core flow: Boot → MainMenu → Lobby → Game → Results
            _stateMachine.AddState(new BootState(_stateMachine, this));
            _stateMachine.AddState(new MainMenuState(_stateMachine, this));
            _stateMachine.AddState(new LobbyState(_stateMachine, this));
            _stateMachine.AddState(new GameState(_stateMachine, this));
            _stateMachine.AddState(new ResultsState(_stateMachine, this));

            // Extended game states (optional)
            _stateMachine.AddState(new PreGameState(_stateMachine, this));
            _stateMachine.AddState(new ConquestState(_stateMachine, this));
            _stateMachine.AddState(new WarState(_stateMachine, this));
        }

        private void Start()
        {
            _stateMachine.SetState<BootState>();
        }

        private void Update()
        {
            _stateMachine.Update();
        }

        private void FixedUpdate()
        {
            _stateMachine.FixedUpdate();
        }

        public void ChangeState<T>() where T : IState
        {
            _stateMachine.SetState<T>();
        }
    }
}
