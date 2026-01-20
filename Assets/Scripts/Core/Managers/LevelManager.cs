using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

namespace AnadoluFethi.Core
{
    public class LevelManager : Singleton<LevelManager>, IManager
    {
        [Header("Settings")]
        [SerializeField] private string _mainMenuScene = "MainMenu";
        [SerializeField] private float _minLoadingTime = 0.5f;

        private int _currentLevelIndex;
        private bool _isLoading;

        public int CurrentLevelIndex => _currentLevelIndex;
        public string CurrentSceneName => SceneManager.GetActiveScene().name;
        public bool IsLoading => _isLoading;

        public event Action OnLoadStarted;
        public event Action<float> OnLoadProgress;
        public event Action OnLoadCompleted;

        public void Initialize()
        {
            _currentLevelIndex = 0;
        }

        public void Dispose() { }

        public void LoadScene(string sceneName, Action onComplete = null)
        {
            if (_isLoading)
                return;

            StartCoroutine(LoadSceneAsync(sceneName, onComplete));
        }

        public void LoadScene(int sceneIndex, Action onComplete = null)
        {
            if (_isLoading)
                return;

            StartCoroutine(LoadSceneByIndexAsync(sceneIndex, onComplete));
        }

        public void LoadLevel(int levelIndex, Action onComplete = null)
        {
            _currentLevelIndex = levelIndex;
            LoadScene($"Level_{levelIndex}", onComplete);
        }

        public void LoadNextLevel(Action onComplete = null)
        {
            LoadLevel(_currentLevelIndex + 1, onComplete);
        }

        public void ReloadCurrentScene(Action onComplete = null)
        {
            LoadScene(CurrentSceneName, onComplete);
        }

        public void LoadMainMenu(Action onComplete = null)
        {
            LoadScene(_mainMenuScene, onComplete);
        }

        private IEnumerator LoadSceneAsync(string sceneName, Action onComplete)
        {
            _isLoading = true;
            OnLoadStarted?.Invoke();

            float startTime = Time.realtimeSinceStartup;
            var operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                OnLoadProgress?.Invoke(operation.progress);
                yield return null;
            }

            float elapsed = Time.realtimeSinceStartup - startTime;
            if (elapsed < _minLoadingTime)
            {
                yield return new WaitForSecondsRealtime(_minLoadingTime - elapsed);
            }

            OnLoadProgress?.Invoke(1f);
            operation.allowSceneActivation = true;

            yield return operation;

            _isLoading = false;
            OnLoadCompleted?.Invoke();
            onComplete?.Invoke();
        }

        private IEnumerator LoadSceneByIndexAsync(int sceneIndex, Action onComplete)
        {
            string sceneName = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
            yield return LoadSceneAsync(sceneName, onComplete);
        }
    }
}
