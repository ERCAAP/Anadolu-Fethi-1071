namespace AnadoluFethi.StateMachine.Editor
{
    public static class SMTemplates
    {
        public const string ControllerTemplate = @"using UnityEngine;
using {ROOT_NAMESPACE}.StateMachine;

namespace {ROOT_NAMESPACE}.{SM_NAME}
{
    public class {SM_NAME}Controller : MonoBehaviour
    {
        private StateMachineController _stateMachine;

{STATE_FIELDS}

        private void Awake()
        {
            InitializeStateMachine();
        }

        private void InitializeStateMachine()
        {
            _stateMachine = new StateMachineController();

{STATE_INITIALIZATIONS}

{STATE_REGISTRATIONS}

            _stateMachine.SetState<{INITIAL_STATE}>();
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

        public bool IsCurrentState<T>() where T : IState
        {
            return _stateMachine.IsCurrentState<T>();
        }
    }
}
";

        public const string StateTemplate = @"using UnityEngine;
using {ROOT_NAMESPACE}.StateMachine;

namespace {ROOT_NAMESPACE}.{SM_NAME}
{
    public class {STATE_NAME} : BaseState
    {
        private readonly {SM_NAME}Controller _controller;

        public {STATE_NAME}(StateMachineController stateMachine, {SM_NAME}Controller controller)
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
";

        public const string StateFieldTemplate = "        private {STATE_NAME} {FIELD_NAME};";
        public const string StateInitTemplate = "            {FIELD_NAME} = new {STATE_NAME}(_stateMachine, this);";
        public const string StateRegisterTemplate = "            _stateMachine.AddState({FIELD_NAME});";
    }
}
