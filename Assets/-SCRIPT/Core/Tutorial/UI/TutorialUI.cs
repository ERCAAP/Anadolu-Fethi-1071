using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace AnadoluFethi.Core.Tutorial
{
    public class TutorialUI : MonoBehaviour
    {
        [Header("Main Container")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private GameObject _container;

        [Header("Content")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private Image _iconImage;
        [SerializeField] private GameObject _iconContainer;

        [Header("Progress")]
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TextMeshProUGUI _progressText;

        [Header("Buttons")]
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _previousButton;
        [SerializeField] private Button _skipButton;

        [Header("Pointer")]
        [SerializeField] private TutorialPointer _pointer;

        [Header("Highlight")]
        [SerializeField] private TutorialHighlight _highlight;

        [Header("Settings")]
        [SerializeField] private float _fadeDuration = 0.3f;
        [SerializeField] private bool _useFadeAnimation = true;

        private TutorialStepData _currentStep;
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            SetupButtons();
            Hide();
        }

        private void SetupButtons()
        {
            _nextButton?.onClick.AddListener(OnNextClicked);
            _previousButton?.onClick.AddListener(OnPreviousClicked);
            _skipButton?.onClick.AddListener(OnSkipClicked);
        }

        public void Show()
        {
            _container.SetActive(true);

            if (_canvasGroup != null)
            {
                if (_useFadeAnimation && _fadeDuration > 0)
                {
                    StartFade(1f, () =>
                    {
                        _canvasGroup.interactable = true;
                        _canvasGroup.blocksRaycasts = true;
                    });
                }
                else
                {
                    _canvasGroup.alpha = 1f;
                    _canvasGroup.interactable = true;
                    _canvasGroup.blocksRaycasts = true;
                }
            }
        }

        public void Hide()
        {
            _pointer?.Hide();
            _highlight?.Hide();

            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;

                if (_useFadeAnimation && _fadeDuration > 0)
                {
                    StartFade(0f, () => _container.SetActive(false));
                }
                else
                {
                    _canvasGroup.alpha = 0f;
                    _container.SetActive(false);
                }
            }
            else
            {
                _container.SetActive(false);
            }
        }

        private void StartFade(float targetAlpha, System.Action onComplete = null)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, onComplete));
        }

        private IEnumerator FadeRoutine(float targetAlpha, System.Action onComplete)
        {
            float startAlpha = _canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / _fadeDuration);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
            onComplete?.Invoke();
            _fadeCoroutine = null;
        }

        public void ShowStep(TutorialStepData step)
        {
            _currentStep = step;

            UpdateContent(step);
            UpdateProgress();
            UpdateButtons(step);
            UpdatePointer(step);
            UpdateHighlight(step);
        }

        private void UpdateContent(TutorialStepData step)
        {
            if (_titleText != null)
            {
                _titleText.text = step.title;
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = step.description;
            }

            if (_iconImage != null && _iconContainer != null)
            {
                bool hasIcon = step.icon != null;
                _iconContainer.SetActive(hasIcon);

                if (hasIcon)
                {
                    _iconImage.sprite = step.icon;
                }
            }
        }

        private void UpdateProgress()
        {
            var manager = TutorialManager.Instance;
            if (manager?.CurrentTutorial == null)
                return;

            float progress = manager.Progress;
            int current = manager.CurrentStepIndex + 1;
            int total = manager.CurrentTutorial.StepCount;

            if (_progressSlider != null)
            {
                _progressSlider.value = progress;
            }

            if (_progressText != null)
            {
                _progressText.text = $"{current} / {total}";
            }
        }

        private void UpdateButtons(TutorialStepData step)
        {
            var manager = TutorialManager.Instance;
            if (manager?.CurrentTutorial == null)
                return;

            bool isManual = step.completionType == TutorialCompletionType.Manual;
            bool isFirstStep = manager.CurrentStepIndex == 0;
            bool canSkip = manager.CurrentTutorial.CanSkip;

            if (_nextButton != null)
            {
                _nextButton.gameObject.SetActive(isManual);
            }

            if (_previousButton != null)
            {
                _previousButton.gameObject.SetActive(isManual && !isFirstStep);
            }

            if (_skipButton != null)
            {
                _skipButton.gameObject.SetActive(canSkip);
            }
        }

        private void UpdatePointer(TutorialStepData step)
        {
            if (_pointer == null)
                return;

            if (step.pointerDirection == TutorialPointerDirection.None)
            {
                _pointer.Hide();
                return;
            }

            Transform target = FindTarget(step);
            if (target != null)
            {
                _pointer.Show(target, step.pointerDirection, step.pointerOffset);
            }
            else if (step.useCustomPosition)
            {
                _pointer.ShowAtPosition(step.customPosition, step.pointerDirection);
            }
            else
            {
                _pointer.Hide();
            }
        }

        private void UpdateHighlight(TutorialStepData step)
        {
            if (_highlight == null)
                return;

            if (!step.highlightTarget)
            {
                _highlight.Hide();
                return;
            }

            Transform target = FindTarget(step);
            if (target != null)
            {
                _highlight.Show(target, step.allowTargetInteraction);
            }
            else
            {
                _highlight.Hide();
            }
        }

        private Transform FindTarget(TutorialStepData step)
        {
            if (!string.IsNullOrEmpty(step.targetObjectName))
            {
                var obj = GameObject.Find(step.targetObjectName);
                if (obj != null)
                    return obj.transform;
            }

            if (!string.IsNullOrEmpty(step.targetObjectTag))
            {
                var obj = GameObject.FindWithTag(step.targetObjectTag);
                if (obj != null)
                    return obj.transform;
            }

            return null;
        }

        private void OnNextClicked()
        {
            TutorialManager.Instance?.NextStep();
        }

        private void OnPreviousClicked()
        {
            TutorialManager.Instance?.PreviousStep();
        }

        private void OnSkipClicked()
        {
            TutorialManager.Instance?.SkipTutorial();
        }

        private void OnDestroy()
        {
            _nextButton?.onClick.RemoveListener(OnNextClicked);
            _previousButton?.onClick.RemoveListener(OnPreviousClicked);
            _skipButton?.onClick.RemoveListener(OnSkipClicked);
        }
    }
}
