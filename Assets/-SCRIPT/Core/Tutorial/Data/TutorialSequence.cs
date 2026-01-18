using UnityEngine;
using System.Collections.Generic;

namespace AnadoluFethi.Core.Tutorial
{
    [CreateAssetMenu(fileName = "NewTutorial", menuName = "Anadolu Fethi/Tutorial/Tutorial Sequence")]
    public class TutorialSequence : ScriptableObject
    {
        [Header("Identification")]
        [SerializeField] private string _tutorialId;
        [SerializeField] private string _displayName;
        [TextArea(2, 4)]
        [SerializeField] private string _description;

        [Header("Settings")]
        [SerializeField] private bool _canSkip = true;
        [SerializeField] private bool _pauseGame = false;
        [SerializeField] private bool _saveProgress = true;
        [SerializeField] private int _priority;

        [Header("Steps")]
        [SerializeField] private List<TutorialStepData> _steps = new List<TutorialStepData>();

        [Header("Requirements")]
        [SerializeField] private List<string> _requiredTutorials = new List<string>();

        public string TutorialId => _tutorialId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public bool CanSkip => _canSkip;
        public bool PauseGame => _pauseGame;
        public bool SaveProgress => _saveProgress;
        public int Priority => _priority;
        public IReadOnlyList<TutorialStepData> Steps => _steps;
        public IReadOnlyList<string> RequiredTutorials => _requiredTutorials;
        public int StepCount => _steps.Count;

        public TutorialStepData GetStep(int index)
        {
            if (index < 0 || index >= _steps.Count)
                return null;

            return _steps[index];
        }

        public void AddStep(TutorialStepData step)
        {
            _steps.Add(step);
        }

        public void RemoveStep(int index)
        {
            if (index >= 0 && index < _steps.Count)
            {
                _steps.RemoveAt(index);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_tutorialId))
            {
                _tutorialId = name;
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                if (string.IsNullOrEmpty(_steps[i].stepId))
                {
                    _steps[i].stepId = $"{_tutorialId}_Step_{i}";
                }
            }
        }
#endif
    }
}
