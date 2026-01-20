namespace AnadoluFethi.StateMachine
{
    public abstract class BaseState : IState
    {
        protected StateMachineController stateMachine;

        public BaseState(StateMachineController stateMachine)
        {
            this.stateMachine = stateMachine;
        }

        public virtual void Enter() { }
        public virtual void Update() { }
        public virtual void FixedUpdate() { }
        public virtual void Exit() { }
    }
}
