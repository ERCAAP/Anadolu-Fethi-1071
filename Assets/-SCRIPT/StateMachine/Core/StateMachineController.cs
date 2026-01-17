using UnityEngine;
using System;
using System.Collections.Generic;

namespace AnadoluFethi.StateMachine
{
    public class StateMachineController
    {
        private IState _currentState;
        private Dictionary<Type, IState> _states = new Dictionary<Type, IState>();

        public IState CurrentState => _currentState;

        public void AddState(IState state)
        {
            var type = state.GetType();
            if (!_states.ContainsKey(type))
            {
                _states.Add(type, state);
            }
        }

        public void SetState<T>() where T : IState
        {
            var type = typeof(T);
            if (_states.TryGetValue(type, out IState newState))
            {
                ChangeState(newState);
            }
            else
            {
                Debug.LogWarning($"State {type.Name} not found in StateMachine.");
            }
        }

        public void SetState(IState newState)
        {
            ChangeState(newState);
        }

        private void ChangeState(IState newState)
        {
            _currentState?.Exit();
            _currentState = newState;
            _currentState?.Enter();
        }

        public void Update()
        {
            _currentState?.Update();
        }

        public void FixedUpdate()
        {
            _currentState?.FixedUpdate();
        }

        public bool IsCurrentState<T>() where T : IState
        {
            return _currentState != null && _currentState.GetType() == typeof(T);
        }
    }
}
