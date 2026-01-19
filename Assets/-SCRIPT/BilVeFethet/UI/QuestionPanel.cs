using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;

namespace BilVeFethet.UI
{
    /// <summary>
    /// Soru Paneli - Referans tasarıma uygun soru gösterim paneli
    /// Sarı arka plan, üstte soru metni, ortada seçenekler/numpad
    /// </summary>
    public class QuestionPanel : MonoBehaviour
    {
        [Header("Ana Konteyner")]
        [SerializeField] private RectTransform mainContainer;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Soru Başlığı")]
        [SerializeField] private RectTransform questionHeaderContainer;
        [SerializeField] private Image questionHeaderBackground;
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI questionNumberText;
        [SerializeField] private Image categoryIcon;

        [Header("Soru Tipi Göstergesi")]
        [SerializeField] private GameObject questionTypeIndicator;
        [SerializeField] private Image questionTypeIcon;
        [SerializeField] private TextMeshProUGUI questionTypeText;

        [Header("Çoktan Seçmeli Seçenekler")]
        [SerializeField] private RectTransform optionsContainer;
        [SerializeField] private OptionButtonEnhanced[] optionButtons;

        [Header("Sayısal Giriş (Tahmin)")]
        [SerializeField] private RectTransform numpadContainer;
        [SerializeField] private NumpadPanel numpadPanel;

        [Header("Renkler")]
        [SerializeField] private Color headerBackgroundColor = new Color(0.95f, 0.85f, 0.2f); // Sarı
        [SerializeField] private Color panelBackgroundColor = new Color(0.15f, 0.15f, 0.2f);
        [SerializeField] private Color multipleChoiceColor = new Color(0.2f, 0.6f, 0.9f);
        [SerializeField] private Color estimationColor = new Color(0.9f, 0.6f, 0.2f);

        [Header("Animasyon")]
        [SerializeField] private float showAnimDuration = 0.3f;
        [SerializeField] private float optionStaggerDelay = 0.1f;

        // Events
        public event Action<int> OnOptionSelected;
        public event Action<float> OnEstimationSubmitted;

        // State
        private QuestionData _currentQuestion;
        private bool _hasAnswered;
        private int _currentQuestionNumber;
        private int _totalQuestions;

        private void Awake()
        {
            // NumpadPanel event bağlantısı
            if (numpadPanel != null)
            {
                numpadPanel.OnValueSubmitted += HandleEstimationSubmit;
            }
        }

        private void OnDestroy()
        {
            if (numpadPanel != null)
            {
                numpadPanel.OnValueSubmitted -= HandleEstimationSubmit;
            }
        }

        #region Public Methods

        /// <summary>
        /// Soruyu göster
        /// </summary>
        public void ShowQuestion(QuestionData question, int questionNumber, int totalQuestions)
        {
            _currentQuestion = question;
            _hasAnswered = false;
            _currentQuestionNumber = questionNumber;
            _totalQuestions = totalQuestions;

            // Soru başlığını ayarla
            SetupQuestionHeader(question, questionNumber, totalQuestions);

            // Soru tipine göre UI göster
            if (question.questionType == QuestionType.CoktanSecmeli)
            {
                ShowMultipleChoice(question);
            }
            else
            {
                ShowEstimation(question);
            }

            // Animasyonla göster
            StartCoroutine(ShowAnimation());
        }

        /// <summary>
        /// Cevap sonucunu göster
        /// </summary>
        public void ShowResult(bool isCorrect, int correctIndex, int selectedIndex)
        {
            _hasAnswered = true;

            // Seçenekleri güncelle
            for (int i = 0; i < optionButtons?.Length; i++)
            {
                if (optionButtons[i] == null) continue;

                if (i == correctIndex)
                {
                    optionButtons[i].ShowCorrect();
                }
                else if (i == selectedIndex && !isCorrect)
                {
                    optionButtons[i].ShowWrong();
                }

                optionButtons[i].SetInteractable(false);
            }
        }

        /// <summary>
        /// Tahmin sonucunu göster
        /// </summary>
        public void ShowEstimationResult(bool isCorrect, float guessedValue, float correctValue)
        {
            _hasAnswered = true;

            if (numpadPanel != null)
            {
                numpadPanel.ShowResult(guessedValue, correctValue, isCorrect);
            }
        }

        /// <summary>
        /// %50 joker sonucunu uygula
        /// </summary>
        public void ApplyFiftyFifty(List<int> eliminatedIndices)
        {
            if (eliminatedIndices == null || optionButtons == null) return;

            foreach (int index in eliminatedIndices)
            {
                if (index >= 0 && index < optionButtons.Length)
                {
                    optionButtons[index].SetEliminated();
                }
            }
        }

        /// <summary>
        /// Paneli gizle
        /// </summary>
        public void Hide()
        {
            StartCoroutine(HideAnimation());
        }

        /// <summary>
        /// Etkileşimi devre dışı bırak
        /// </summary>
        public void DisableInteraction()
        {
            foreach (var btn in optionButtons)
            {
                if (btn != null) btn.SetInteractable(false);
            }

            if (numpadPanel != null)
            {
                numpadPanel.SetInteractable(false);
            }
        }

        #endregion

        #region Private Methods

        private void SetupQuestionHeader(QuestionData question, int questionNumber, int totalQuestions)
        {
            // Soru metni
            if (questionText != null)
            {
                questionText.text = question.questionText;
            }

            // Kategori
            if (categoryText != null)
            {
                categoryText.text = GetCategoryDisplayName(question.category);
            }

            // Soru numarası
            if (questionNumberText != null)
            {
                questionNumberText.text = $"SORU {questionNumber}/{totalQuestions}";
            }

            // Soru tipi göstergesi
            if (questionTypeIndicator != null)
            {
                questionTypeIndicator.SetActive(true);

                if (questionTypeText != null)
                {
                    questionTypeText.text = question.questionType == QuestionType.CoktanSecmeli
                        ? "ÇOKTAN SEÇMELİ"
                        : "TAHMİN";
                }

                if (questionTypeIcon != null)
                {
                    questionTypeIcon.color = question.questionType == QuestionType.CoktanSecmeli
                        ? multipleChoiceColor
                        : estimationColor;
                }
            }

            // Başlık arka plan rengi
            if (questionHeaderBackground != null)
            {
                questionHeaderBackground.color = headerBackgroundColor;
            }
        }

        private void ShowMultipleChoice(QuestionData question)
        {
            // Containers
            if (optionsContainer != null) optionsContainer.gameObject.SetActive(true);
            if (numpadContainer != null) numpadContainer.gameObject.SetActive(false);

            // Seçenekleri ayarla
            for (int i = 0; i < optionButtons?.Length; i++)
            {
                if (i < question.options.Count)
                {
                    int index = i;
                    optionButtons[i].gameObject.SetActive(true);
                    optionButtons[i].Initialize(i, question.options[i]);
                    optionButtons[i].SetOnClick(() => HandleOptionClick(index));
                    optionButtons[i].SetInteractable(true);
                    optionButtons[i].ResetState();
                }
                else
                {
                    optionButtons[i].gameObject.SetActive(false);
                }
            }
        }

        private void ShowEstimation(QuestionData question)
        {
            // Containers
            if (optionsContainer != null) optionsContainer.gameObject.SetActive(false);
            if (numpadContainer != null) numpadContainer.gameObject.SetActive(true);

            // Numpad'i hazırla
            if (numpadPanel != null)
            {
                numpadPanel.Initialize(question);
            }
        }

        private void HandleOptionClick(int index)
        {
            if (_hasAnswered) return;

            _hasAnswered = true;

            // Seçilen şıkkı vurgula
            optionButtons[index].SetSelected();

            // Tüm butonları devre dışı bırak
            foreach (var btn in optionButtons)
            {
                if (btn != null) btn.SetInteractable(false);
            }

            OnOptionSelected?.Invoke(index);
        }

        private void HandleEstimationSubmit(float value)
        {
            if (_hasAnswered) return;

            _hasAnswered = true;
            OnEstimationSubmitted?.Invoke(value);
        }

        private string GetCategoryDisplayName(QuestionCategory category)
        {
            return category switch
            {
                QuestionCategory.Turkce => "TÜRKÇE",
                QuestionCategory.Ingilizce => "İNGİLİZCE",
                QuestionCategory.Bilim => "BİLİM",
                QuestionCategory.Sanat => "SANAT",
                QuestionCategory.Spor => "SPOR",
                QuestionCategory.GenelKultur => "GENEL KÜLTÜR",
                QuestionCategory.Tarih => "TARİH",
                _ => "GENEL"
            };
        }

        private IEnumerator ShowAnimation()
        {
            if (canvasGroup == null) yield break;

            canvasGroup.alpha = 0f;
            gameObject.SetActive(true);

            float elapsed = 0f;

            while (elapsed < showAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / showAnimDuration;

                // Ease out
                t = 1f - Mathf.Pow(1f - t, 3f);

                canvasGroup.alpha = t;

                // Scale animasyonu
                if (mainContainer != null)
                {
                    mainContainer.localScale = Vector3.Lerp(Vector3.one * 0.9f, Vector3.one, t);
                }

                yield return null;
            }

            canvasGroup.alpha = 1f;
            if (mainContainer != null)
                mainContainer.localScale = Vector3.one;

            // Seçenekleri sırayla göster
            if (_currentQuestion?.questionType == QuestionType.CoktanSecmeli)
            {
                yield return StartCoroutine(StaggerOptions());
            }
        }

        private IEnumerator StaggerOptions()
        {
            foreach (var btn in optionButtons)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy) continue;

                btn.PlayShowAnimation();
                yield return new WaitForSeconds(optionStaggerDelay);
            }
        }

        private IEnumerator HideAnimation()
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;

            while (elapsed < showAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / showAnimDuration;

                canvasGroup.alpha = 1f - t;

                yield return null;
            }

            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        #endregion
    }

    /// <summary>
    /// Geliştirilmiş seçenek butonu
    /// </summary>
    [Serializable]
    public class OptionButtonEnhanced : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI optionText;
        [SerializeField] private TextMeshProUGUI labelText; // A, B, C, D
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Renkler")]
        [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.3f);
        [SerializeField] private Color hoverColor = new Color(0.25f, 0.25f, 0.35f);
        [SerializeField] private Color selectedColor = new Color(0.3f, 0.5f, 0.8f);
        [SerializeField] private Color correctColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color wrongColor = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color eliminatedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        [Header("Animasyon")]
        [SerializeField] private float animDuration = 0.2f;

        private int _index;
        private Action _onClick;
        private bool _isEliminated;

        private static readonly string[] Labels = { "A", "B", "C", "D" };

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            button?.onClick.AddListener(() =>
            {
                if (!_isEliminated)
                    _onClick?.Invoke();
            });
        }

        public void Initialize(int index, string text)
        {
            _index = index;
            _isEliminated = false;

            if (optionText != null)
                optionText.text = text;

            if (labelText != null && index < Labels.Length)
                labelText.text = Labels[index];
        }

        public void SetOnClick(Action onClick)
        {
            _onClick = onClick;
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null)
                button.interactable = interactable && !_isEliminated;
        }

        public void ResetState()
        {
            _isEliminated = false;

            if (backgroundImage != null)
                backgroundImage.color = normalColor;

            if (borderImage != null)
                borderImage.color = Color.white;

            if (optionText != null)
                optionText.color = Color.white;
        }

        public void SetSelected()
        {
            if (backgroundImage != null)
                backgroundImage.color = selectedColor;

            StartCoroutine(PulseAnimation(selectedColor));
        }

        public void ShowCorrect()
        {
            if (backgroundImage != null)
                backgroundImage.color = correctColor;

            if (borderImage != null)
                borderImage.color = Color.green;

            StartCoroutine(PulseAnimation(correctColor));
        }

        public void ShowWrong()
        {
            if (backgroundImage != null)
                backgroundImage.color = wrongColor;

            if (borderImage != null)
                borderImage.color = Color.red;

            StartCoroutine(ShakeAnimation());
        }

        public void SetEliminated()
        {
            _isEliminated = true;

            if (backgroundImage != null)
                backgroundImage.color = eliminatedColor;

            if (optionText != null)
            {
                optionText.text = "—";
                optionText.color = new Color(0.5f, 0.5f, 0.5f);
            }

            SetInteractable(false);
        }

        public void PlayShowAnimation()
        {
            StartCoroutine(ShowAnim());
        }

        private IEnumerator ShowAnim()
        {
            if (canvasGroup == null) yield break;

            canvasGroup.alpha = 0f;

            float elapsed = 0f;

            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animDuration;
                t = 1f - Mathf.Pow(1f - t, 3f);

                canvasGroup.alpha = t;
                transform.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one, t);

                yield return null;
            }

            canvasGroup.alpha = 1f;
            transform.localScale = Vector3.one;
        }

        private IEnumerator PulseAnimation(Color color)
        {
            float duration = 0.5f;
            float elapsed = 0f;
            Color originalColor = color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = (Mathf.Sin(elapsed * Mathf.PI * 4f) + 1f) / 2f;

                if (backgroundImage != null)
                {
                    backgroundImage.color = Color.Lerp(originalColor, Color.white, t * 0.2f);
                }

                yield return null;
            }

            if (backgroundImage != null)
                backgroundImage.color = originalColor;
        }

        private IEnumerator ShakeAnimation()
        {
            float duration = 0.3f;
            float elapsed = 0f;
            float shakeAmount = 10f;
            Vector3 originalPos = transform.localPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - (elapsed / duration);
                float x = Mathf.Sin(elapsed * 40f) * shakeAmount * t;
                transform.localPosition = originalPos + new Vector3(x, 0f, 0f);
                yield return null;
            }

            transform.localPosition = originalPos;
        }
    }
}
