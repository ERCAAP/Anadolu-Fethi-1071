using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Managers;
using BilVeFethet.Utils;

namespace BilVeFethet.UI
{
    /// <summary>
    /// Ana Oyun UI Yöneticisi
    /// Soru paneli, oyuncu profilleri, zamanlayıcı ve tüm oyun UI'ını yönetir
    /// </summary>
    public class GameUIManager : Singleton<GameUIManager>
    {
        [Header("Ana Paneller")]
        [SerializeField] private GameObject questionPanel;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private GameObject countdownPanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private GameObject gameOverPanel;

        [Header("Soru Paneli - Ortak")]
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI questionNumberText;
        [SerializeField] private Image categoryIcon;

        [Header("Çoktan Seçmeli UI")]
        [SerializeField] private GameObject multipleChoiceContainer;
        [SerializeField] private OptionButton[] optionButtons;

        [Header("Sayısal Giriş UI (Tahmin)")]
        [SerializeField] private GameObject numericInputContainer;
        [SerializeField] private TMP_InputField numericInputField;
        [SerializeField] private TextMeshProUGUI numericUnitText;
        [SerializeField] private Button[] numpadButtons; // 0-9 butonları
        [SerializeField] private Button numpadBackspaceButton;
        [SerializeField] private Button numpadSubmitButton;

        [Header("Zamanlayıcı")]
        [SerializeField] private Slider timerSlider;
        [SerializeField] private Image timerFillImage;
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Color timerNormalColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color timerWarningColor = new Color(0.9f, 0.7f, 0.2f);
        [SerializeField] private Color timerDangerColor = new Color(0.9f, 0.2f, 0.2f);

        [Header("Sol Oyuncu Profili (Yerel Oyuncu)")]
        [SerializeField] private PlayerGameCard localPlayerCard;

        [Header("Sağ Oyuncu Profilleri (Rakipler)")]
        [SerializeField] private PlayerGameCard[] opponentCards;

        [Header("Joker Butonları")]
        [SerializeField] private JokerButton[] jokerButtons;

        [Header("Cevap Sonucu")]
        [SerializeField] private GameObject answerResultPopup;
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private TextMeshProUGUI resultMessageText;
        [SerializeField] private TextMeshProUGUI resultPointsText;
        [SerializeField] private Image resultBackgroundImage;
        [SerializeField] private Color correctAnswerColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color wrongAnswerColor = new Color(0.9f, 0.2f, 0.2f);

        [Header("Yükleme ve Geri Sayım")]
        [SerializeField] private TextMeshProUGUI loadingText;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private TextMeshProUGUI countdownSubText;

        [Header("Oyun Sonu")]
        [SerializeField] private TextMeshProUGUI gameOverTitleText;
        [SerializeField] private Transform rankingContainer;
        [SerializeField] private GameOverPlayerCard[] gameOverPlayerCards;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Joker Sonuç Panelleri")]
        [SerializeField] private GameObject audienceResultPanel;
        [SerializeField] private AudienceBar[] audienceBars;
        [SerializeField] private GameObject parrotHintPanel;
        [SerializeField] private TextMeshProUGUI parrotHintText;
        [SerializeField] private GameObject telescopePanel;
        [SerializeField] private Button[] telescopeOptionButtons;

        [Header("Ses Efektleri")]
        [SerializeField] private AudioClip correctSound;
        [SerializeField] private AudioClip wrongSound;
        [SerializeField] private AudioClip tickSound;
        [SerializeField] private AudioClip timeUpSound;
        [SerializeField] private AudioClip buttonClickSound;

        // Events
        public event Action<int> OnOptionSelected;
        public event Action<float> OnEstimationSubmitted;
        public event Action OnTimeUp;
        public event Action OnPlayAgainClicked;
        public event Action OnMainMenuClicked;
        public event Action<JokerType> OnJokerUsed;

        // State
        private QuestionData _currentQuestion;
        private List<InGamePlayerData> _players;
        private float _currentTime;
        private float _maxTime;
        private bool _isTimerRunning;
        private bool _hasAnswered;
        private Coroutine _timerCoroutine;
        private AudioSource _audioSource;
        private string _numericInput = "";

        protected override void OnSingletonAwake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void Start()
        {
            FindUIReferences();
            SetupButtons();
            HideAllPanels();
        }

        /// <summary>
        /// UI referanslarını otomatik bul (eğer atanmamışsa)
        /// </summary>
        private void FindUIReferences()
        {
            // GameCanvas'ı bul
            var gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas == null) return;

            // Ana Paneller
            if (questionPanel == null)
                questionPanel = gameCanvas.transform.Find("QuestionPanel")?.gameObject;
            if (loadingPanel == null)
                loadingPanel = gameCanvas.transform.Find("LoadingPanel")?.gameObject;
            if (countdownPanel == null)
                countdownPanel = gameCanvas.transform.Find("CountdownPanel")?.gameObject;
            if (resultPanel == null)
                resultPanel = gameCanvas.transform.Find("AnswerResultPanel")?.gameObject;
            if (gameOverPanel == null)
                gameOverPanel = gameCanvas.transform.Find("GameEndPanel")?.gameObject;

            // QuestionPanel altındaki elemanlar
            if (questionPanel != null)
            {
                var questionContainer = questionPanel.transform.Find("QuestionContainer");
                if (questionContainer != null)
                {
                    if (questionText == null)
                        questionText = questionContainer.Find("QuestionText")?.GetComponent<TextMeshProUGUI>();
                    if (categoryText == null)
                        categoryText = questionContainer.Find("CategoryText")?.GetComponent<TextMeshProUGUI>();
                }

                // OptionsContainer ve optionButtons
                var optionsContainer = questionPanel.transform.Find("OptionsContainer");
                if (optionsContainer != null && (optionButtons == null || optionButtons.Length == 0))
                {
                    var optionList = new List<OptionButton>();
                    foreach (Transform child in optionsContainer)
                    {
                        var optBtn = child.GetComponent<OptionButton>();
                        if (optBtn != null)
                            optionList.Add(optBtn);
                    }
                    if (optionList.Count > 0)
                        optionButtons = optionList.ToArray();
                }
            }

            // CountdownPanel altındaki elemanlar
            if (countdownPanel != null)
            {
                if (countdownText == null)
                    countdownText = countdownPanel.transform.Find("CountdownText")?.GetComponent<TextMeshProUGUI>();
                if (countdownSubText == null)
                    countdownSubText = countdownPanel.transform.Find("CountdownMessageText")?.GetComponent<TextMeshProUGUI>();
            }

            // LoadingPanel altındaki elemanlar
            if (loadingPanel != null)
            {
                if (loadingText == null)
                    loadingText = loadingPanel.transform.Find("LoadingText")?.GetComponent<TextMeshProUGUI>();
            }

            // AnswerResultPanel
            if (resultPanel != null && answerResultPopup == null)
            {
                answerResultPopup = resultPanel;
            }

            Debug.Log("[GameUIManager] UI referansları otomatik bulundu");
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void SetupButtons()
        {
            // Çoktan seçmeli butonları
            for (int i = 0; i < optionButtons?.Length; i++)
            {
                int index = i;
                optionButtons[i]?.SetOnClick(() => HandleOptionClick(index));
            }

            // Numpad butonları
            for (int i = 0; i < numpadButtons?.Length; i++)
            {
                int digit = i;
                numpadButtons[i]?.onClick.AddListener(() => HandleNumpadClick(digit));
            }

            numpadBackspaceButton?.onClick.AddListener(HandleNumpadBackspace);
            numpadSubmitButton?.onClick.AddListener(HandleNumpadSubmit);

            // Joker butonları
            for (int i = 0; i < jokerButtons?.Length; i++)
            {
                int index = i;
                jokerButtons[i]?.SetOnClick(() => HandleJokerClick(index));
            }

            // Oyun sonu butonları
            playAgainButton?.onClick.AddListener(() => OnPlayAgainClicked?.Invoke());
            mainMenuButton?.onClick.AddListener(() => OnMainMenuClicked?.Invoke());
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnQuestionTimerStarted += HandleTimerStarted;
            GameEvents.OnScoreChanged += HandleScoreChanged;
            GameEvents.OnPlayerEliminated += HandlePlayerEliminated;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnQuestionTimerStarted -= HandleTimerStarted;
            GameEvents.OnScoreChanged -= HandleScoreChanged;
            GameEvents.OnPlayerEliminated -= HandlePlayerEliminated;
        }

        #region Public Methods

        /// <summary>
        /// Oyuncuları güncelle
        /// </summary>
        public void UpdatePlayers(List<InGamePlayerData> players)
        {
            _players = players;

            // Yerel oyuncuyu bul
            var localPlayer = players.Find(p => p.isLocalPlayer);
            if (localPlayer != null && localPlayerCard != null)
            {
                localPlayerCard.Initialize(localPlayer);
            }

            // Rakipleri ayarla
            var opponents = players.FindAll(p => !p.isLocalPlayer);
            for (int i = 0; i < opponentCards?.Length; i++)
            {
                if (i < opponents.Count)
                {
                    opponentCards[i].gameObject.SetActive(true);
                    opponentCards[i].Initialize(opponents[i]);
                }
                else
                {
                    opponentCards[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Soru göster
        /// </summary>
        public void ShowQuestion(QuestionData question, int questionIndex, int totalQuestions)
        {
            _currentQuestion = question;
            _hasAnswered = false;
            _numericInput = "";

            HideAllPanels();
            questionPanel.SetActive(true);

            // Soru metni
            if (questionText != null)
                questionText.text = question.questionText;

            // Kategori
            if (categoryText != null)
                categoryText.text = GetCategoryName(question.category);

            // Soru numarası
            if (questionNumberText != null)
                questionNumberText.text = $"Soru {questionIndex}/{totalQuestions}";

            // Soru tipine göre UI göster
            if (question.questionType == QuestionType.CoktanSecmeli)
            {
                ShowMultipleChoiceUI(question);
            }
            else
            {
                ShowNumericInputUI(question);
            }

            // Jokerları güncelle
            UpdateJokerButtons(question.questionType);
        }

        /// <summary>
        /// Çoktan seçmeli UI göster
        /// </summary>
        private void ShowMultipleChoiceUI(QuestionData question)
        {
            if (multipleChoiceContainer != null)
                multipleChoiceContainer.SetActive(true);
            if (numericInputContainer != null)
                numericInputContainer.SetActive(false);

            for (int i = 0; i < optionButtons?.Length; i++)
            {
                if (i < question.options.Count)
                {
                    optionButtons[i].gameObject.SetActive(true);
                    optionButtons[i].SetOption(i, question.options[i]);
                    optionButtons[i].SetInteractable(true);
                    optionButtons[i].ResetState();
                }
                else
                {
                    optionButtons[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Sayısal giriş UI göster
        /// </summary>
        private void ShowNumericInputUI(QuestionData question)
        {
            if (multipleChoiceContainer != null)
                multipleChoiceContainer.SetActive(false);
            if (numericInputContainer != null)
                numericInputContainer.SetActive(true);

            if (numericInputField != null)
            {
                numericInputField.text = "";
                numericInputField.interactable = true;
            }

            if (numericUnitText != null)
                numericUnitText.text = question.valueUnit ?? "";

            _numericInput = "";
        }

        /// <summary>
        /// Cevap sonucunu göster
        /// </summary>
        public void ShowAnswerResult(bool isCorrect, int points, string correctAnswer, float displayDuration)
        {
            _hasAnswered = true;
            StopTimer();

            // Doğru cevabı işaretle
            if (_currentQuestion?.questionType == QuestionType.CoktanSecmeli)
            {
                for (int i = 0; i < optionButtons?.Length; i++)
                {
                    if (i == _currentQuestion.correctAnswerIndex)
                    {
                        optionButtons[i].ShowCorrect();
                    }
                    optionButtons[i].SetInteractable(false);
                }
            }

            // Popup göster
            StartCoroutine(ShowResultPopup(isCorrect, points, isCorrect ? "DOĞRU!" : "YANLIŞ!",
                isCorrect ? $"+{points} TP" : $"Doğru cevap: {correctAnswer}", displayDuration));

            // Ses çal
            PlaySound(isCorrect ? correctSound : wrongSound);
        }

        /// <summary>
        /// Tahmin sonucunu göster
        /// </summary>
        public void ShowEstimationResult(bool isCorrect, int points, float correctValue, float guessedValue,
            float accuracy, string unit, float displayDuration)
        {
            _hasAnswered = true;
            StopTimer();

            string message;
            if (isCorrect)
            {
                message = accuracy >= 95f
                    ? $"Mükemmel! Doğru cevap: {correctValue:N0} {unit}"
                    : $"Yakın! Cevabınız: {guessedValue:N0}, Doğru: {correctValue:N0} {unit}";
            }
            else
            {
                message = $"Cevabınız: {guessedValue:N0}, Doğru: {correctValue:N0} {unit}";
            }

            StartCoroutine(ShowResultPopup(isCorrect, points, isCorrect ? "DOĞRU!" : "YANLIŞ!", message, displayDuration));
            PlaySound(isCorrect ? correctSound : wrongSound);
        }

        /// <summary>
        /// Yükleme ekranı göster
        /// </summary>
        public void ShowLoading(string message)
        {
            HideAllPanels();
            if (loadingPanel != null)
                loadingPanel.SetActive(true);
            if (loadingText != null)
                loadingText.text = message;
        }

        /// <summary>
        /// Geri sayım göster
        /// </summary>
        public void ShowCountdown(int seconds, string message)
        {
            HideAllPanels();
            if (countdownPanel != null)
                countdownPanel.SetActive(true);
            if (countdownSubText != null)
                countdownSubText.text = message;

            StartCoroutine(CountdownCoroutine(seconds));
        }

        /// <summary>
        /// Oyun sonu ekranı göster
        /// </summary>
        public void ShowGameOver(List<GameEndPlayerResult> results)
        {
            HideAllPanels();
            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);

            // Kazananı belirle
            var winner = results.Find(r => r.finalRank == 1);
            if (gameOverTitleText != null)
            {
                bool isLocalWinner = winner != null &&
                    winner.playerId == (ProfileManager.Instance?.CurrentProfile?.userId ?? "local");
                gameOverTitleText.text = isLocalWinner ? "TEBRİKLER!\nKAZANDINIZ!" : "OYUN BİTTİ";
            }

            // Sıralamayı göster
            for (int i = 0; i < gameOverPlayerCards?.Length && i < results.Count; i++)
            {
                gameOverPlayerCards[i].gameObject.SetActive(true);
                gameOverPlayerCards[i].SetResult(results[i]);
            }
        }

        /// <summary>
        /// %50 joker sonucunu uygula
        /// </summary>
        public void Apply5050Result(List<int> eliminatedIndices)
        {
            if (eliminatedIndices == null) return;

            foreach (int index in eliminatedIndices)
            {
                if (index >= 0 && index < optionButtons?.Length)
                {
                    optionButtons[index].SetEliminated();
                }
            }
        }

        /// <summary>
        /// Oyunculara sor sonucunu göster
        /// </summary>
        public void ShowAudienceResult(Dictionary<int, float> percentages)
        {
            if (audienceResultPanel != null)
                audienceResultPanel.SetActive(true);

            if (audienceBars == null || percentages == null) return;

            foreach (var kvp in percentages)
            {
                if (kvp.Key >= 0 && kvp.Key < audienceBars.Length)
                {
                    audienceBars[kvp.Key].SetPercentage(kvp.Value);
                }
            }

            StartCoroutine(HideAfterDelay(audienceResultPanel, 5f));
        }

        /// <summary>
        /// Papağan ipucunu göster
        /// </summary>
        public void ShowParrotHint(float hint, float accuracy)
        {
            if (parrotHintPanel != null)
                parrotHintPanel.SetActive(true);

            if (parrotHintText != null)
            {
                int accuracyPercent = Mathf.RoundToInt(accuracy * 100f);
                parrotHintText.text = $"Papağan tahmini: {hint:N0}\n(%{accuracyPercent} doğruluk)";
            }

            StartCoroutine(HideAfterDelay(parrotHintPanel, 5f));
        }

        /// <summary>
        /// Teleskop seçeneklerini göster
        /// </summary>
        public void ShowTelescopeOptions(List<TelescopeOption> options)
        {
            if (telescopePanel != null)
                telescopePanel.SetActive(true);

            // Sayısal girişi kapat, teleskop seçeneklerini göster
            if (numericInputContainer != null)
                numericInputContainer.SetActive(false);

            for (int i = 0; i < telescopeOptionButtons?.Length && i < options?.Count; i++)
            {
                int index = i;
                var option = options[i];

                telescopeOptionButtons[i].gameObject.SetActive(true);
                telescopeOptionButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = option.optionText;
                telescopeOptionButtons[i].onClick.RemoveAllListeners();
                telescopeOptionButtons[i].onClick.AddListener(() =>
                {
                    float value = float.Parse(option.optionText.Replace(",", "").Replace(".", ""));
                    OnEstimationSubmitted?.Invoke(value);
                    telescopePanel.SetActive(false);
                });
            }
        }

        #endregion

        #region Timer

        private void HandleTimerStarted(float duration)
        {
            StartTimer(duration);
        }

        private void StartTimer(float duration)
        {
            _maxTime = duration;
            _currentTime = duration;
            _isTimerRunning = true;

            if (_timerCoroutine != null)
                StopCoroutine(_timerCoroutine);

            _timerCoroutine = StartCoroutine(TimerCoroutine());
        }

        private void StopTimer()
        {
            _isTimerRunning = false;
            if (_timerCoroutine != null)
            {
                StopCoroutine(_timerCoroutine);
                _timerCoroutine = null;
            }
        }

        private IEnumerator TimerCoroutine()
        {
            while (_isTimerRunning && _currentTime > 0)
            {
                _currentTime -= Time.deltaTime;
                UpdateTimerUI();

                // Son 5 saniyede tik sesi
                if (_currentTime <= 5f && _currentTime > 0)
                {
                    int currentSecond = Mathf.CeilToInt(_currentTime);
                    int previousSecond = Mathf.CeilToInt(_currentTime + Time.deltaTime);
                    if (currentSecond != previousSecond)
                    {
                        PlaySound(tickSound);
                    }
                }

                yield return null;
            }

            if (_isTimerRunning && !_hasAnswered)
            {
                _isTimerRunning = false;
                PlaySound(timeUpSound);
                OnTimeUp?.Invoke();
            }
        }

        private void UpdateTimerUI()
        {
            float normalized = _currentTime / _maxTime;

            if (timerSlider != null)
                timerSlider.value = normalized;

            if (timerText != null)
                timerText.text = Mathf.CeilToInt(_currentTime).ToString();

            // Renk değişimi
            if (timerFillImage != null)
            {
                if (normalized > 0.5f)
                    timerFillImage.color = timerNormalColor;
                else if (normalized > 0.25f)
                    timerFillImage.color = timerWarningColor;
                else
                    timerFillImage.color = timerDangerColor;
            }
        }

        #endregion

        #region Input Handlers

        private void HandleOptionClick(int optionIndex)
        {
            if (_hasAnswered || !_isTimerRunning) return;

            PlaySound(buttonClickSound);

            // Seçilen şıkkı işaretle
            optionButtons[optionIndex].SetSelected();

            // Tüm butonları devre dışı bırak
            foreach (var btn in optionButtons)
            {
                btn.SetInteractable(false);
            }

            OnOptionSelected?.Invoke(optionIndex);
        }

        private void HandleNumpadClick(int digit)
        {
            if (_hasAnswered || !_isTimerRunning) return;
            if (_numericInput.Length >= 10) return; // Max 10 karakter

            PlaySound(buttonClickSound);

            _numericInput += digit.ToString();
            UpdateNumericDisplay();
        }

        private void HandleNumpadBackspace()
        {
            if (_hasAnswered || !_isTimerRunning) return;
            if (string.IsNullOrEmpty(_numericInput)) return;

            PlaySound(buttonClickSound);

            _numericInput = _numericInput.Substring(0, _numericInput.Length - 1);
            UpdateNumericDisplay();
        }

        private void HandleNumpadSubmit()
        {
            if (_hasAnswered || !_isTimerRunning) return;
            if (string.IsNullOrEmpty(_numericInput)) return;

            PlaySound(buttonClickSound);

            if (float.TryParse(_numericInput, out float value))
            {
                OnEstimationSubmitted?.Invoke(value);
            }
        }

        private void UpdateNumericDisplay()
        {
            if (numericInputField != null)
                numericInputField.text = _numericInput;
        }

        private void HandleJokerClick(int jokerIndex)
        {
            if (_hasAnswered || !_isTimerRunning) return;
            if (jokerIndex < 0 || jokerIndex >= jokerButtons?.Length) return;

            var jokerButton = jokerButtons[jokerIndex];
            if (jokerButton == null || !jokerButton.IsAvailable) return;

            PlaySound(buttonClickSound);

            JokerType jokerType = jokerButton.JokerType;
            jokerButton.Use();

            OnJokerUsed?.Invoke(jokerType);
        }

        #endregion

        #region Helper Methods

        private void HideAllPanels()
        {
            // Unity'nin "fake null" davranışı nedeniyle explicit null check kullanıyoruz
            if (questionPanel != null) questionPanel.SetActive(false);
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (countdownPanel != null) countdownPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (answerResultPopup != null) answerResultPopup.SetActive(false);
            if (audienceResultPanel != null) audienceResultPanel.SetActive(false);
            if (parrotHintPanel != null) parrotHintPanel.SetActive(false);
            if (telescopePanel != null) telescopePanel.SetActive(false);
        }

        private void UpdateJokerButtons(QuestionType questionType)
        {
            if (jokerButtons == null) return;

            var localPlayer = _players?.Find(p => p.isLocalPlayer);
            if (localPlayer == null) return;

            foreach (var jokerBtn in jokerButtons)
            {
                if (jokerBtn == null) continue;

                // Joker sayısını kontrol et
                int count = 0;
                if (localPlayer.availableJokers != null &&
                    localPlayer.availableJokers.TryGetValue(jokerBtn.JokerType, out count))
                {
                    jokerBtn.SetCount(count);
                }
                else
                {
                    jokerBtn.SetCount(0);
                }

                // Soru tipine göre joker uygunluğu
                bool isCompatible = IsJokerCompatible(jokerBtn.JokerType, questionType);
                jokerBtn.SetCompatible(isCompatible);
            }
        }

        private bool IsJokerCompatible(JokerType jokerType, QuestionType questionType)
        {
            return jokerType switch
            {
                JokerType.Yuzde50 => questionType == QuestionType.CoktanSecmeli,
                JokerType.OyuncularaSor => questionType == QuestionType.CoktanSecmeli,
                JokerType.Papagan => questionType == QuestionType.Tahmin,
                JokerType.Teleskop => questionType == QuestionType.Tahmin,
                _ => true
            };
        }

        private string GetCategoryName(QuestionCategory category)
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

        private void PlaySound(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        private IEnumerator ShowResultPopup(bool isCorrect, int points, string title, string message, float duration)
        {
            if (answerResultPopup != null)
                answerResultPopup.SetActive(true);

            if (resultTitleText != null)
                resultTitleText.text = title;

            if (resultMessageText != null)
                resultMessageText.text = message;

            if (resultPointsText != null)
                resultPointsText.text = points > 0 ? $"+{points} TP" : "";

            if (resultBackgroundImage != null)
                resultBackgroundImage.color = isCorrect ? correctAnswerColor : wrongAnswerColor;

            yield return new WaitForSeconds(duration);

            if (answerResultPopup != null)
                answerResultPopup.SetActive(false);
        }

        private IEnumerator CountdownCoroutine(int seconds)
        {
            for (int i = seconds; i > 0; i--)
            {
                if (countdownText != null)
                    countdownText.text = i.ToString();

                PlaySound(tickSound);
                yield return new WaitForSeconds(1f);
            }

            if (countdownText != null)
                countdownText.text = "BAŞLA!";

            yield return new WaitForSeconds(0.5f);

            if (countdownPanel != null)
                countdownPanel.SetActive(false);
        }

        private IEnumerator HideAfterDelay(GameObject panel, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (panel != null)
                panel.SetActive(false);
        }

        #endregion

        #region Event Handlers

        private void HandleScoreChanged(string playerId, int oldScore, int newScore)
        {
            // Yerel oyuncu
            var localPlayer = _players?.Find(p => p.isLocalPlayer);
            if (localPlayer != null && localPlayer.playerId == playerId)
            {
                localPlayerCard?.UpdateScore(newScore);
            }

            // Rakipler
            for (int i = 0; i < opponentCards?.Length; i++)
            {
                var opponent = _players?.Find(p => !p.isLocalPlayer && p.playerId == playerId);
                if (opponent != null)
                {
                    opponentCards[i]?.UpdateScore(newScore);
                }
            }
        }

        private void HandlePlayerEliminated(string playerId, int round)
        {
            // UI güncelle
            for (int i = 0; i < opponentCards?.Length; i++)
            {
                opponentCards[i]?.CheckEliminated(playerId);
            }
        }

        #endregion
    }

    #region UI Component Classes

    /// <summary>
    /// Seçenek butonu
    /// </summary>
    [Serializable]
    public class OptionButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI optionText;
        [SerializeField] private TextMeshProUGUI labelText; // A, B, C, D
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image borderImage;

        [Header("Renkler")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(0.3f, 0.6f, 0.9f);
        [SerializeField] private Color correctColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color wrongColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color eliminatedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        private int _index;
        private Action _onClick;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            button?.onClick.AddListener(() => _onClick?.Invoke());
        }

        public void SetOnClick(Action onClick)
        {
            _onClick = onClick;
        }

        public void SetOption(int index, string text)
        {
            _index = index;
            if (optionText != null)
                optionText.text = text;
            if (labelText != null)
                labelText.text = ((char)('A' + index)).ToString();
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        public void ResetState()
        {
            if (backgroundImage != null)
                backgroundImage.color = normalColor;
        }

        public void SetSelected()
        {
            if (backgroundImage != null)
                backgroundImage.color = selectedColor;
        }

        public void ShowCorrect()
        {
            if (backgroundImage != null)
                backgroundImage.color = correctColor;
        }

        public void ShowWrong()
        {
            if (backgroundImage != null)
                backgroundImage.color = wrongColor;
        }

        public void SetEliminated()
        {
            if (backgroundImage != null)
                backgroundImage.color = eliminatedColor;
            SetInteractable(false);

            if (optionText != null)
                optionText.text = "—";
        }
    }

    /// <summary>
    /// Oyuncu oyun kartı (sol/sağ profil)
    /// </summary>
    [Serializable]
    public class PlayerGameCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject eliminatedOverlay;
        [SerializeField] private GameObject answerIndicator;

        [Header("Renkler")]
        [SerializeField] private Color greenColor = new Color(0.2f, 0.7f, 0.3f);
        [SerializeField] private Color blueColor = new Color(0.2f, 0.4f, 0.9f);
        [SerializeField] private Color redColor = new Color(0.9f, 0.2f, 0.3f);

        private InGamePlayerData _playerData;

        public void Initialize(InGamePlayerData player)
        {
            _playerData = player;

            if (nameText != null)
                nameText.text = player.displayName;

            if (scoreText != null)
                scoreText.text = player.currentScore.ToString();

            // Renk ayarla
            Color playerColor = player.color switch
            {
                PlayerColor.Yesil => greenColor,
                PlayerColor.Mavi => blueColor,
                PlayerColor.Kirmizi => redColor,
                _ => blueColor
            };

            if (borderImage != null)
                borderImage.color = playerColor;

            if (eliminatedOverlay != null)
                eliminatedOverlay.SetActive(player.isEliminated);

            if (answerIndicator != null)
                answerIndicator.SetActive(false);
        }

        public void UpdateScore(int score)
        {
            if (scoreText != null)
                scoreText.text = score.ToString();
        }

        public void ShowAnswered(bool isCorrect)
        {
            if (answerIndicator != null)
            {
                answerIndicator.SetActive(true);
                var image = answerIndicator.GetComponent<Image>();
                if (image != null)
                    image.color = isCorrect ? Color.green : Color.red;
            }
        }

        public void HideAnswered()
        {
            if (answerIndicator != null)
                answerIndicator.SetActive(false);
        }

        public void CheckEliminated(string playerId)
        {
            if (_playerData != null && _playerData.playerId == playerId)
            {
                if (eliminatedOverlay != null)
                    eliminatedOverlay.SetActive(true);
            }
        }
    }

    /// <summary>
    /// Joker butonu
    /// </summary>
    [Serializable]
    public class JokerButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private JokerType jokerType;

        private int _count;
        private bool _isCompatible = true;
        private bool _isUsed = false;
        private Action _onClick;

        public JokerType JokerType => jokerType;
        public bool IsAvailable => _count > 0 && _isCompatible && !_isUsed;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            button?.onClick.AddListener(() => _onClick?.Invoke());
        }

        public void SetOnClick(Action onClick)
        {
            _onClick = onClick;
        }

        public void SetCount(int count)
        {
            _count = count;
            if (countText != null)
                countText.text = count.ToString();

            UpdateState();
        }

        public void SetCompatible(bool compatible)
        {
            _isCompatible = compatible;
            UpdateState();
        }

        public void Use()
        {
            _isUsed = true;
            _count--;
            if (countText != null)
                countText.text = _count.ToString();
            UpdateState();
        }

        public void Reset()
        {
            _isUsed = false;
            UpdateState();
        }

        private void UpdateState()
        {
            bool available = IsAvailable;

            if (button != null)
                button.interactable = available;

            if (lockedOverlay != null)
                lockedOverlay.SetActive(!available);

            if (iconImage != null)
                iconImage.color = available ? Color.white : new Color(0.5f, 0.5f, 0.5f);
        }
    }

    /// <summary>
    /// Oyunculara sor bar grafiği
    /// </summary>
    [Serializable]
    public class AudienceBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI percentText;
        [SerializeField] private TextMeshProUGUI optionText;

        public void SetPercentage(float percent)
        {
            if (fillImage != null)
                fillImage.fillAmount = percent / 100f;

            if (percentText != null)
                percentText.text = $"%{Mathf.RoundToInt(percent)}";
        }

        public void SetOption(string option)
        {
            if (optionText != null)
                optionText.text = option;
        }
    }

    /// <summary>
    /// Oyun sonu oyuncu kartı
    /// </summary>
    [Serializable]
    public class GameOverPlayerCard : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI correctText;
        [SerializeField] private TextMeshProUGUI tpGainedText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject crownIcon;

        [Header("Sıralama Renkleri")]
        [SerializeField] private Color firstPlaceColor = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color secondPlaceColor = new Color(0.75f, 0.75f, 0.75f);
        [SerializeField] private Color thirdPlaceColor = new Color(0.8f, 0.5f, 0.2f);

        public void SetResult(GameEndPlayerResult result)
        {
            if (rankText != null)
                rankText.text = $"{result.finalRank}.";

            if (nameText != null)
                nameText.text = result.displayName;

            if (scoreText != null)
                scoreText.text = $"{result.finalScore} Puan";

            if (correctText != null)
                correctText.text = $"{result.correctAnswers}/{result.correctAnswers + result.wrongAnswers} Doğru";

            if (tpGainedText != null && result.tpResult != null)
                tpGainedText.text = $"+{result.tpResult.totalTP} TP";

            // Taç ikonu
            if (crownIcon != null)
                crownIcon.SetActive(result.finalRank == 1);

            // Arka plan rengi
            if (backgroundImage != null)
            {
                backgroundImage.color = result.finalRank switch
                {
                    1 => firstPlaceColor,
                    2 => secondPlaceColor,
                    3 => thirdPlaceColor,
                    _ => Color.white
                };
            }
        }
    }

    #endregion
}
