using UnityEngine;
using System;

namespace AnadoluFethi.Core
{
    public enum GameState
    {
        None,
        Initializing,
        MainMenu,
        Loading,
        Playing,
        Paused,
        GameOver,
        Victory
    }

    public class GameManager : Singleton<GameManager>, IManager
    {
        [Header("Settings")]
        [SerializeField] private GameState _initialState = GameState.Initializing;

        private GameState _currentState;
        private GameState _previousState;

        public GameState CurrentState => _currentState;
        public GameState PreviousState => _previousState;
        public bool IsPaused => _currentState == GameState.Paused;
        public bool IsPlaying => _currentState == GameState.Playing;

        public event Action<GameState, GameState> OnStateChanged;

        public void Initialize()
        {
            SetState(_initialState);
        }

        public void Dispose() { }

        public void SetState(GameState newState)
        {
            if (_currentState == newState)
                return;

            _previousState = _currentState;
            _currentState = newState;

            HandleStateChange();
            OnStateChanged?.Invoke(_previousState, _currentState);
        }

        private void HandleStateChange()
        {
            Time.timeScale = _currentState == GameState.Paused ? 0f : 1f;
        }

        public void StartGame()
        {
            SetState(GameState.Playing);
        }

        public void PauseGame()
        {
            if (_currentState == GameState.Playing)
            {
                SetState(GameState.Paused);
            }
        }

        public void ResumeGame()
        {
            if (_currentState == GameState.Paused)
            {
                SetState(GameState.Playing);
            }
        }

        public void TogglePause()
        {
            if (_currentState == GameState.Playing)
                PauseGame();
            else if (_currentState == GameState.Paused)
                ResumeGame();
        }

        public void GameOver()
        {
            SetState(GameState.GameOver);
        }

        public void Victory()
        {
            SetState(GameState.Victory);
        }

        public void ReturnToMainMenu()
        {
            SetState(GameState.MainMenu);
        }
    }
}
