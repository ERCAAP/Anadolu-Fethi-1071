using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BilVeFethet.Data;
using BilVeFethet.Events;

namespace BilVeFethet.UI
{
    /// <summary>
    /// Sayısal Giriş Paneli - Tahmin soruları için numpad
    /// Referans tasarım: Ortada numpad, üstte giriş alanı, sağda birim göstergesi
    /// </summary>
    public class NumpadPanel : MonoBehaviour
    {
        [Header("Giriş Alanı")]
        [SerializeField] private TextMeshProUGUI inputDisplayText;
        [SerializeField] private Image inputBackgroundImage;
        [SerializeField] private TextMeshProUGUI unitText;
        [SerializeField] private TextMeshProUGUI placeholderText;

        [Header("Numpad Butonları")]
        [SerializeField] private Button[] numberButtons; // 0-9
        [SerializeField] private Button backspaceButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button negativeButton; // Negatif işareti

        [Header("Buton Stilleri")]
        [SerializeField] private Color numberButtonColor = new Color(0.25f, 0.25f, 0.35f);
        [SerializeField] private Color actionButtonColor = new Color(0.3f, 0.5f, 0.7f);
        [SerializeField] private Color submitButtonColor = new Color(0.2f, 0.7f, 0.4f);
        [SerializeField] private Color clearButtonColor = new Color(0.7f, 0.3f, 0.3f);

        [Header("Giriş Ayarları")]
        [SerializeField] private int maxDigits = 10;
        [SerializeField] private bool allowNegative = false;
        [SerializeField] private bool allowDecimal = false;

        [Header("Animasyon")]
        [SerializeField] private float buttonPressScale = 0.95f;
        [SerializeField] private float buttonPressTime = 0.1f;

        [Header("Ses")]
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField] private AudioClip submitSound;
        [SerializeField] private AudioClip errorSound;

        // Events
        public event Action<float> OnValueSubmitted;
        public event Action OnClearPressed;

        // State
        private string _currentInput = "";
        private bool _isNegative = false;
        private bool _isActive = true;
        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            SetupButtons();
        }

        private void SetupButtons()
        {
            // Sayı butonları (0-9)
            for (int i = 0; i < numberButtons?.Length && i < 10; i++)
            {
                int digit = i;
                if (numberButtons[i] != null)
                {
                    numberButtons[i].onClick.AddListener(() => OnNumberPressed(digit));
                    SetButtonColor(numberButtons[i], numberButtonColor);
                }
            }

            // Backspace
            if (backspaceButton != null)
            {
                backspaceButton.onClick.AddListener(OnBackspacePressed);
                SetButtonColor(backspaceButton, actionButtonColor);
            }

            // Clear
            if (clearButton != null)
            {
                clearButton.onClick.AddListener(OnClearButtonPressed);
                SetButtonColor(clearButton, clearButtonColor);
            }

            // Submit
            if (submitButton != null)
            {
                submitButton.onClick.AddListener(OnSubmitPressed);
                SetButtonColor(submitButton, submitButtonColor);
            }

            // Negative
            if (negativeButton != null)
            {
                negativeButton.onClick.AddListener(OnNegativePressed);
                SetButtonColor(negativeButton, actionButtonColor);
                negativeButton.gameObject.SetActive(allowNegative);
            }
        }

        #region Public Methods

        /// <summary>
        /// Paneli soru için hazırla
        /// </summary>
        public void Initialize(QuestionData question)
        {
            Clear();
            _isActive = true;

            // Birim metnini ayarla
            if (unitText != null)
            {
                unitText.text = question.valueUnit ?? "";
            }

            // Placeholder
            if (placeholderText != null)
            {
                placeholderText.text = "Tahmininizi girin...";
                placeholderText.gameObject.SetActive(true);
            }

            // Negatif izni (bazı sorular için)
            allowNegative = question.allowNegative;
            if (negativeButton != null)
                negativeButton.gameObject.SetActive(allowNegative);

            UpdateDisplay();
            SetInteractable(true);
        }

        /// <summary>
        /// Girişi temizle
        /// </summary>
        public void Clear()
        {
            _currentInput = "";
            _isNegative = false;
            UpdateDisplay();
        }

        /// <summary>
        /// Etkileşimi etkinleştir/devre dışı bırak
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            _isActive = interactable;

            foreach (var btn in numberButtons)
            {
                if (btn != null) btn.interactable = interactable;
            }

            if (backspaceButton != null) backspaceButton.interactable = interactable;
            if (clearButton != null) clearButton.interactable = interactable;
            if (submitButton != null) submitButton.interactable = interactable;
            if (negativeButton != null) negativeButton.interactable = interactable;
        }

        /// <summary>
        /// Mevcut değeri al
        /// </summary>
        public float GetCurrentValue()
        {
            if (string.IsNullOrEmpty(_currentInput))
                return 0f;

            if (float.TryParse(_currentInput, out float value))
            {
                return _isNegative ? -value : value;
            }

            return 0f;
        }

        /// <summary>
        /// Sonucu göster (doğru cevap karşılaştırması)
        /// </summary>
        public void ShowResult(float guessedValue, float correctValue, bool isCorrect)
        {
            SetInteractable(false);

            if (inputBackgroundImage != null)
            {
                inputBackgroundImage.color = isCorrect
                    ? new Color(0.2f, 0.7f, 0.3f, 0.5f)
                    : new Color(0.7f, 0.2f, 0.2f, 0.5f);
            }
        }

        #endregion

        #region Private Methods

        private void OnNumberPressed(int digit)
        {
            if (!_isActive) return;
            if (_currentInput.Length >= maxDigits) return;

            PlaySound(buttonClickSound);
            AnimateButton(numberButtons[digit]);

            // İlk rakam 0 ise ve başka rakam yoksa, 0 olarak kalsın
            if (_currentInput.Length == 0 && digit == 0)
            {
                _currentInput = "0";
            }
            // İlk rakam 0 ise ve yeni rakam geliyorsa, 0'ı kaldır
            else if (_currentInput == "0" && digit != 0)
            {
                _currentInput = digit.ToString();
            }
            else
            {
                _currentInput += digit.ToString();
            }

            UpdateDisplay();
        }

        private void OnBackspacePressed()
        {
            if (!_isActive) return;
            if (string.IsNullOrEmpty(_currentInput)) return;

            PlaySound(buttonClickSound);
            AnimateButton(backspaceButton);

            _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
            UpdateDisplay();
        }

        private void OnClearButtonPressed()
        {
            if (!_isActive) return;

            PlaySound(buttonClickSound);
            AnimateButton(clearButton);

            Clear();
            OnClearPressed?.Invoke();
        }

        private void OnNegativePressed()
        {
            if (!_isActive) return;
            if (!allowNegative) return;

            PlaySound(buttonClickSound);
            AnimateButton(negativeButton);

            _isNegative = !_isNegative;
            UpdateDisplay();
        }

        private void OnSubmitPressed()
        {
            if (!_isActive) return;

            if (string.IsNullOrEmpty(_currentInput))
            {
                PlaySound(errorSound);
                StartCoroutine(ShakeInput());
                return;
            }

            PlaySound(submitSound);
            AnimateButton(submitButton);

            float value = GetCurrentValue();
            SetInteractable(false);

            OnValueSubmitted?.Invoke(value);
        }

        private void UpdateDisplay()
        {
            if (inputDisplayText == null) return;

            if (string.IsNullOrEmpty(_currentInput))
            {
                inputDisplayText.text = "";
                if (placeholderText != null)
                    placeholderText.gameObject.SetActive(true);
            }
            else
            {
                string display = _currentInput;

                // Binlik ayırıcı ekle
                if (long.TryParse(_currentInput, out long numValue))
                {
                    display = numValue.ToString("N0");
                }

                // Negatif işareti
                if (_isNegative)
                {
                    display = "-" + display;
                }

                inputDisplayText.text = display;

                if (placeholderText != null)
                    placeholderText.gameObject.SetActive(false);
            }
        }

        private void SetButtonColor(Button button, Color color)
        {
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(color.r + 0.1f, color.g + 0.1f, color.b + 0.1f);
            colors.pressedColor = new Color(color.r - 0.1f, color.g - 0.1f, color.b - 0.1f);
            button.colors = colors;
        }

        private void AnimateButton(Button button)
        {
            if (button == null) return;

            StartCoroutine(ButtonPressAnimation(button.transform));
        }

        private IEnumerator ButtonPressAnimation(Transform buttonTransform)
        {
            Vector3 originalScale = buttonTransform.localScale;
            Vector3 pressedScale = originalScale * buttonPressScale;

            float elapsed = 0f;
            float halfTime = buttonPressTime / 2f;

            // Scale down
            while (elapsed < halfTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfTime;
                buttonTransform.localScale = Vector3.Lerp(originalScale, pressedScale, t);
                yield return null;
            }

            elapsed = 0f;

            // Scale up
            while (elapsed < halfTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfTime;
                buttonTransform.localScale = Vector3.Lerp(pressedScale, originalScale, t);
                yield return null;
            }

            buttonTransform.localScale = originalScale;
        }

        private IEnumerator ShakeInput()
        {
            if (inputDisplayText == null) yield break;

            Vector3 originalPos = inputDisplayText.transform.localPosition;
            float shakeAmount = 10f;
            float shakeDuration = 0.3f;
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float x = Mathf.Sin(elapsed * 50f) * shakeAmount * (1f - elapsed / shakeDuration);
                inputDisplayText.transform.localPosition = originalPos + new Vector3(x, 0f, 0f);
                yield return null;
            }

            inputDisplayText.transform.localPosition = originalPos;
        }

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        #endregion
    }
}
