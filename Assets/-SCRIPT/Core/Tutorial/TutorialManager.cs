using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace AnadoluFethi.Core.Tutorial
{
    public class TutorialManager : Singleton<TutorialManager>, IManager
    {
        [Header("Settings")]
        [SerializeField] private bool _autoStartFirstTutorial;
        [SerializeField] private string _saveKey = "TutorialProgress";

        [Header("References")]
        [SerializeField] private TutorialUI _tutorialUI;
        [SerializeField] private List<TutorialSequence> _tutorials = new List<TutorialSequence>();

        private readonly Dictionary<string, TutorialSequence> _tutorialMap = new Dictionary<string, TutorialSequence>();
        private readonly Dictionary<string, ITutorialCondition> _conditions = new Dictionary<string, ITutorialCondition>();
        private readonly HashSet<string> _completedTutorials = new HashSet<string>();

        private TutorialSequence _currentTutorial;
        private int _currentStepIndex;
        private Coroutine _stepCoroutine;
        private bool _isPlaying;

        public bool IsPlaying => _isPlaying;
        public TutorialSequence CurrentTutorial => _currentTutorial;
        public int CurrentStepIndex => _currentStepIndex;
        public TutorialStepData CurrentStep => _currentTutorial?.GetStep(_currentStepIndex);
        public float Progress => _currentTutorial != null ? (float)_currentStepIndex / _currentTutorial.StepCount : 0f;

        public void Initialize()
        {
            RegisterTutorials();
            LoadProgress();

            if (_autoStartFirstTutorial && _tutorials.Count > 0)
            {
                StartTutorial(_tutorials[0].TutorialId);
            }
        }

        public void Dispose()
        {
            SaveProgress();
            _tutorialMap.Clear();
            _conditions.Clear();
        }

        private void RegisterTutorials()
        {
            foreach (var tutorial in _tutorials)
            {
                if (tutorial != null && !string.IsNullOrEmpty(tutorial.TutorialId))
                {
                    _tutorialMap[tutorial.TutorialId] = tutorial;
                }
            }
        }

        public void RegisterCondition(string conditionId, ITutorialCondition condition)
        {
            _conditions[conditionId] = condition;
        }

        public void UnregisterCondition(string conditionId)
        {
            _conditions.Remove(conditionId);
        }

        public void StartTutorial(string tutorialId)
        {
            if (_isPlaying)
            {
                StopCurrentTutorial();
            }

            if (!_tutorialMap.TryGetValue(tutorialId, out var tutorial))
            {
                Debug.LogWarning($"[TutorialManager] Tutorial not found: {tutorialId}");
                return;
            }

            if (!CanStartTutorial(tutorial))
            {
                Debug.LogWarning($"[TutorialManager] Requirements not met for: {tutorialId}");
                return;
            }

            _currentTutorial = tutorial;
            _currentStepIndex = 0;
            _isPlaying = true;

            if (tutorial.PauseGame)
            {
                Time.timeScale = 0f;
            }

            TutorialEvents.TutorialStarted(tutorialId);
            _tutorialUI?.Show();

            StartStep();
        }

        private bool CanStartTutorial(TutorialSequence tutorial)
        {
            foreach (var requiredId in tutorial.RequiredTutorials)
            {
                if (!IsTutorialCompleted(requiredId))
                {
                    return false;
                }
            }
            return true;
        }

        public void StartTutorial(TutorialSequence tutorial)
        {
            if (tutorial == null) return;

            if (!_tutorialMap.ContainsKey(tutorial.TutorialId))
            {
                _tutorialMap[tutorial.TutorialId] = tutorial;
            }

            StartTutorial(tutorial.TutorialId);
        }

        private void StartStep()
        {
            if (_currentTutorial == null || _currentStepIndex >= _currentTutorial.StepCount)
            {
                CompleteTutorial();
                return;
            }

            var step = _currentTutorial.GetStep(_currentStepIndex);
            TutorialEvents.StepStarted(step, _currentStepIndex, _currentTutorial.StepCount);
            TutorialEvents.ProgressChanged(Progress);

            _stepCoroutine = StartCoroutine(ExecuteStepRoutine(step));
        }

        private IEnumerator ExecuteStepRoutine(TutorialStepData step)
        {
            if (step.delayBefore > 0)
            {
                yield return new WaitForSecondsRealtime(step.delayBefore);
            }

            _tutorialUI?.ShowStep(step);

            if (step.sfx != null)
            {
                SoundManager.Instance?.PlaySFX(step.sfx);
            }

            switch (step.completionType)
            {
                case TutorialCompletionType.Auto:
                    yield return new WaitForSecondsRealtime(step.autoCompleteDelay);
                    NextStep();
                    break;

                case TutorialCompletionType.Timer:
                    yield return new WaitForSecondsRealtime(step.autoCompleteDelay);
                    NextStep();
                    break;

                case TutorialCompletionType.Condition:
                    if (_conditions.TryGetValue(step.conditionId, out var condition))
                    {
                        condition.StartListening();
                        yield return new WaitUntil(() => condition.IsMet);
                        condition.StopListening();
                        NextStep();
                    }
                    break;

                case TutorialCompletionType.Manual:
                default:
                    break;
            }
        }

        public void NextStep()
        {
            if (!_isPlaying || _currentTutorial == null)
                return;

            var completedStep = CurrentStep;

            if (_stepCoroutine != null)
            {
                StopCoroutine(_stepCoroutine);
                _stepCoroutine = null;
            }

            TutorialEvents.StepCompleted(completedStep, _currentStepIndex, _currentTutorial.StepCount);

            _currentStepIndex++;

            if (completedStep != null && completedStep.delayAfter > 0)
            {
                StartCoroutine(DelayedNextStep(completedStep.delayAfter));
            }
            else
            {
                StartStep();
            }
        }

        private IEnumerator DelayedNextStep(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            StartStep();
        }

        public void PreviousStep()
        {
            if (!_isPlaying || _currentTutorial == null || _currentStepIndex <= 0)
                return;

            if (_stepCoroutine != null)
            {
                StopCoroutine(_stepCoroutine);
                _stepCoroutine = null;
            }

            _currentStepIndex--;
            StartStep();
        }

        public void SkipTutorial()
        {
            if (!_isPlaying || _currentTutorial == null)
                return;

            if (!_currentTutorial.CanSkip)
                return;

            var tutorialId = _currentTutorial.TutorialId;
            StopCurrentTutorial();
            TutorialEvents.TutorialSkipped(tutorialId);
        }

        private void CompleteTutorial()
        {
            if (_currentTutorial == null)
                return;

            var tutorialId = _currentTutorial.TutorialId;

            _completedTutorials.Add(tutorialId);

            if (_currentTutorial.SaveProgress)
            {
                SaveProgress();
            }

            StopCurrentTutorial();
            TutorialEvents.TutorialCompleted(tutorialId);
        }

        private void StopCurrentTutorial()
        {
            if (_stepCoroutine != null)
            {
                StopCoroutine(_stepCoroutine);
                _stepCoroutine = null;
            }

            if (_currentTutorial != null && _currentTutorial.PauseGame)
            {
                Time.timeScale = 1f;
            }

            _tutorialUI?.Hide();
            _currentTutorial = null;
            _currentStepIndex = 0;
            _isPlaying = false;
        }

        public bool IsTutorialCompleted(string tutorialId)
        {
            return _completedTutorials.Contains(tutorialId);
        }

        public void ResetTutorial(string tutorialId)
        {
            _completedTutorials.Remove(tutorialId);
            SaveProgress();
        }

        public void ResetAllTutorials()
        {
            _completedTutorials.Clear();
            SaveProgress();
        }

        private void SaveProgress()
        {
            var data = string.Join(",", _completedTutorials);
            PlayerPrefs.SetString(_saveKey, data);
            PlayerPrefs.Save();
        }

        private void LoadProgress()
        {
            var data = PlayerPrefs.GetString(_saveKey, "");
            if (string.IsNullOrEmpty(data))
                return;

            var ids = data.Split(',');
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    _completedTutorials.Add(id);
                }
            }
        }
    }
}
