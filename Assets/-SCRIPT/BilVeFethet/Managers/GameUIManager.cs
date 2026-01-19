using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Oyun İçi UI Manager - Tüm oyun içi UI elementlerini yönetir
    /// </summary>
    public class GameUIManager : Singleton<GameUIManager>
    {
        [Header("Ana Paneller")]
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private GameObject countdownPanel;
        [SerializeField] private GameObject questionPanel;
        [SerializeField] private GameObject answerResultPanel;
        [SerializeField] private GameObject gameEndPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject loadingPanel;
        
        [Header("Soru Paneli")]
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI categoryText;
        [SerializeField] private TextMeshProUGUI questionNumberText;
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private TextMeshProUGUI pointsText;
        [SerializeField] private Transform optionsContainer;
        [SerializeField] private GameObject optionButtonPrefab;

        [Header("Tahmin Sorusu UI")]
        [SerializeField] private GameObject estimationContainer;
        [SerializeField] private TMP_InputField estimationInputField;
        [SerializeField] private Button estimationSubmitButton;
        [SerializeField] private TextMeshProUGUI estimationUnitText;
        [SerializeField] private TextMeshProUGUI estimationHintText;
        [SerializeField] private Slider estimationSlider;
        [SerializeField] private TextMeshProUGUI estimationSliderValueText;

        [Header("Joker Butonları")]
        [SerializeField] private GameObject jokerContainer;
        [SerializeField] private Button joker5050Button;
        [SerializeField] private Button jokerAudienceButton;
        [SerializeField] private Button jokerParrotButton;
        [SerializeField] private Button jokerTelescopeButton;
        [SerializeField] private TextMeshProUGUI joker5050CountText;
        [SerializeField] private TextMeshProUGUI jokerAudienceCountText;
        [SerializeField] private TextMeshProUGUI jokerParrotCountText;
        [SerializeField] private TextMeshProUGUI jokerTelescopeCountText;
        [SerializeField] private GameObject audienceResultPanel;
        [SerializeField] private Image[] audienceBarImages;
        [SerializeField] private TextMeshProUGUI[] audiencePercentTexts;
        
        [Header("Zamanlayıcı")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Image timerFillImage;
        [SerializeField] private GameObject timerContainer;
        [SerializeField] private float questionTime = 30f;
        
        [Header("Oyuncu Göstergeleri")]
        [SerializeField] private Transform playersContainer;
        [SerializeField] private GameObject playerIndicatorPrefab;
        [SerializeField] private GameObject localPlayerIndicator;
        
        [Header("Skor Göstergesi")]
        [SerializeField] private TextMeshProUGUI localScoreText;
        [SerializeField] private TextMeshProUGUI localCorrectText;
        [SerializeField] private GameObject scorePopupPrefab;
        
        [Header("Cevap Sonucu")]
        [SerializeField] private Image answerResultIcon;
        [SerializeField] private TextMeshProUGUI answerResultText;
        [SerializeField] private TextMeshProUGUI answerPointsText;
        [SerializeField] private TextMeshProUGUI correctAnswerText;
        [SerializeField] private Color correctColor = new Color(0.3f, 0.9f, 0.4f);
        [SerializeField] private Color wrongColor = new Color(0.9f, 0.3f, 0.3f);
        [SerializeField] private Color timeoutColor = new Color(0.9f, 0.7f, 0.2f);
        
        [Header("Geri Sayım")]
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private TextMeshProUGUI countdownMessageText;
        
        [Header("Oyun Sonu")]
        [SerializeField] private TextMeshProUGUI gameEndTitleText;
        [SerializeField] private TextMeshProUGUI winnerText;
        [SerializeField] private Transform resultsContainer;
        [SerializeField] private GameObject playerResultPrefab;
        [SerializeField] private TextMeshProUGUI yourScoreText;
        [SerializeField] private TextMeshProUGUI yourCorrectText;
        [SerializeField] private TextMeshProUGUI xpGainedText;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button mainMenuButton;
        
        [Header("Yükleme")]
        [SerializeField] private TextMeshProUGUI loadingText;
        [SerializeField] private Image loadingProgressImage;
        [SerializeField] private TextMeshProUGUI tipText;
        
        // Events
        public event Action<int> OnOptionSelected;
        public event Action<float> OnEstimationSubmitted;
        public event Action OnTimeUp;
        public event Action OnPlayAgainClicked;
        public event Action OnMainMenuClicked;
        public event Action OnPauseClicked;
        public event Action OnResumeClicked;
        public event Action<JokerType> OnJokerUsed;
        
        // State
        private List<GameObject> optionButtons = new List<GameObject>();
        private List<GameObject> playerIndicators = new List<GameObject>();
        private List<GameObject> resultCards = new List<GameObject>();
        private Coroutine timerCoroutine;
        private float currentTime;
        private bool isAnswered = false;
        private int selectedOptionIndex = -1;
        private int currentQuestionIndex = 0;
        private List<string> lastQuestionOptions;
        private QuestionData currentQuestion;
        private List<int> eliminatedOptions = new List<int>();
        private float submittedEstimation = 0f;
        
        // Properties
        public bool IsAnswered => isAnswered;
        public int SelectedOption => selectedOptionIndex;
        
        private string[] tips = new string[]
        {
            "Hızlı cevap vererek daha fazla puan kazanabilirsiniz!",
            "Yanlış cevap puan kaybettirmez, her zaman tahmin edin!",
            "Günlük giriş yaparak bonus oyun hakkı kazanın!",
            "Arkadaşlarınızla oynayarak daha eğlenceli vakit geçirin!",
            "Sıralamada yükselerek özel ödüller kazanın!",
            "Her sorunun zorluğuna göre puan değeri değişir.",
            "Turnuvalara katılarak büyük ödüller kazanabilirsiniz!"
        };
        
        protected override void Awake()
        {
            base.Awake();
        }
        
        private void Start()
        {
            SubscribeToEvents();
            SetupJokerButtons();
            SetupEstimationUI();
            HideAllPanels();
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        private void SubscribeToEvents()
        {
            GameEvents.OnQuestionReceived += HandleQuestionReceived;
            GameEvents.OnQuestionResultReceived += HandleQuestionResultReceived;
            GameEvents.OnGameEnded += HandleGameEnded;
            GameEvents.OnScoreChanged += HandleScoreChanged;
            GameEvents.OnGameStarting += HandleGameStarting;
            GameEvents.OnQuestionTimerStarted += HandleQuestionTimerStarted;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnQuestionReceived -= HandleQuestionReceived;
            GameEvents.OnQuestionResultReceived -= HandleQuestionResultReceived;
            GameEvents.OnGameEnded -= HandleGameEnded;
            GameEvents.OnScoreChanged -= HandleScoreChanged;
            GameEvents.OnGameStarting -= HandleGameStarting;
            GameEvents.OnQuestionTimerStarted -= HandleQuestionTimerStarted;
        }
        
        #region Public Methods
        
        /// <summary>
        /// Yükleme ekranını göster
        /// </summary>
        public void ShowLoading(string message = "Yükleniyor...")
        {
            HideAllPanels();
            SetPanelActive(loadingPanel, true);
            
            if (loadingText != null)
                loadingText.text = message;
                
            if (tipText != null)
                tipText.text = tips[UnityEngine.Random.Range(0, tips.Length)];
                
            if (loadingProgressImage != null)
                loadingProgressImage.fillAmount = 0f;
        }
        
        /// <summary>
        /// Yükleme ilerlemesini güncelle
        /// </summary>
        public void UpdateLoadingProgress(float progress)
        {
            if (loadingProgressImage != null)
                loadingProgressImage.fillAmount = progress;
        }
        
        /// <summary>
        /// Geri sayımı göster
        /// </summary>
        public void ShowCountdown(int seconds, string message = "Oyun Başlıyor!")
        {
            HideAllPanels();
            SetPanelActive(countdownPanel, true);
            StartCoroutine(CountdownCoroutine(seconds, message));
        }
        
        /// <summary>
        /// Oyun panelini göster
        /// </summary>
        public void ShowGamePanel()
        {
            HideAllPanels();
            SetPanelActive(gamePanel, true);
            SetPanelActive(questionPanel, true);
        }
        
        /// <summary>
        /// Soruyu göster
        /// </summary>
        public void ShowQuestion(QuestionData question, int questionNumber, int totalQuestions)
        {
            isAnswered = false;
            selectedOptionIndex = -1;
            submittedEstimation = 0f;
            eliminatedOptions.Clear();
            currentQuestion = question;

            ShowGamePanel();

            // Soru metni
            if (questionText != null)
                questionText.text = question.questionText;

            // Kategori
            if (categoryText != null)
                categoryText.text = GetCategoryDisplayName(question.category);

            // Soru numarası
            if (questionNumberText != null)
                questionNumberText.text = $"Soru {questionNumber}/{totalQuestions}";

            // Zorluk (1-10 arası seviye)
            if (difficultyText != null)
            {
                string difficultyStr = question.difficultyLevel switch
                {
                    <= 3 => "Kolay",
                    <= 6 => "Orta",
                    <= 8 => "Zor",
                    _ => "Uzman"
                };
                difficultyText.text = difficultyStr;
                difficultyText.color = GetDifficultyColor(question.difficultyLevel);
            }

            // Puan (zorluk seviyesine göre hesaplanır)
            int questionPoints = CalculateQuestionPoints(question.difficultyLevel);
            if (pointsText != null)
                pointsText.text = $"+{questionPoints} TP";

            // Soru tipine göre UI göster
            bool isMultipleChoice = question.questionType == QuestionType.CoktanSecmeli;

            // Çoktan seçmeli container
            if (optionsContainer != null)
                optionsContainer.gameObject.SetActive(isMultipleChoice);

            // Tahmin container
            SetPanelActive(estimationContainer, !isMultipleChoice);

            // Joker paneli - soru tipine göre uygun jokerler
            UpdateJokerButtons(question.questionType);

            // Audience result panelini gizle
            SetPanelActive(audienceResultPanel, false);

            if (isMultipleChoice)
            {
                // Seçenekleri kaydet ve oluştur
                lastQuestionOptions = question.options;
                CreateOptions(question.options);
            }
            else
            {
                // Tahmin sorusu UI ayarla
                SetupEstimationForQuestion(question);
            }

            // Zamanlayıcıyı başlat
            StartTimer(question.timeLimit > 0 ? question.timeLimit : questionTime);
        }

        /// <summary>
        /// Tahmin sorusu için UI ayarla
        /// </summary>
        private void SetupEstimationForQuestion(QuestionData question)
        {
            if (estimationInputField != null)
            {
                estimationInputField.text = "";
                estimationInputField.interactable = true;
                estimationInputField.contentType = TMP_InputField.ContentType.DecimalNumber;
            }

            if (estimationUnitText != null)
                estimationUnitText.text = !string.IsNullOrEmpty(question.valueUnit) ? question.valueUnit : "";

            if (estimationHintText != null)
            {
                // İpucu göster (örn: "Yıl olarak giriniz")
                string hint = question.valueUnit switch
                {
                    "yil" or "yıl" => "Yıl olarak giriniz",
                    "km" => "Kilometre olarak giriniz",
                    "kg" => "Kilogram olarak giriniz",
                    "m" => "Metre olarak giriniz",
                    _ => "Sayısal değer giriniz"
                };
                estimationHintText.text = hint;
            }

            if (estimationSubmitButton != null)
                estimationSubmitButton.interactable = true;

            // Slider varsa ayarla
            if (estimationSlider != null)
            {
                estimationSlider.gameObject.SetActive(false); // Varsayılan olarak gizli
            }
        }
        
        /// <summary>
        /// Cevap sonucunu göster
        /// </summary>
        public void ShowAnswerResult(bool isCorrect, int points, string correctAnswer, float displayTime = 2f)
        {
            SetPanelActive(answerResultPanel, true);

            // Çoktan seçmeli cevapları vurgula
            if (currentQuestion != null && currentQuestion.questionType == QuestionType.CoktanSecmeli)
            {
                HighlightCorrectAndWrongAnswers(currentQuestion.correctAnswerIndex);
            }

            if (isCorrect)
            {
                if (answerResultIcon != null)
                    answerResultIcon.color = correctColor;
                if (answerResultText != null)
                    answerResultText.text = "DOĞRU!";
                if (answerPointsText != null)
                    answerPointsText.text = $"+{points} TP";

                // Skor popup göster
                ShowScorePopup(points, true);

                // Başarı sesi ve animasyon
                PlayCorrectAnswerAnimation();
            }
            else if (selectedOptionIndex == -1 && submittedEstimation == 0f) // Süre doldu
            {
                if (answerResultIcon != null)
                    answerResultIcon.color = timeoutColor;
                if (answerResultText != null)
                    answerResultText.text = "SÜRE DOLDU!";
                if (answerPointsText != null)
                    answerPointsText.text = "0 TP";

                // Timeout animasyonu
                PlayTimeoutAnimation();
            }
            else
            {
                if (answerResultIcon != null)
                    answerResultIcon.color = wrongColor;
                if (answerResultText != null)
                    answerResultText.text = "YANLIŞ!";
                if (answerPointsText != null)
                    answerPointsText.text = "0 TP";

                // Yanlış cevap animasyonu
                PlayWrongAnswerAnimation();
            }

            if (correctAnswerText != null)
                correctAnswerText.text = $"Doğru Cevap: {correctAnswer}";

            StartCoroutine(HideAnswerResultAfterDelay(displayTime));
        }

        /// <summary>
        /// Tahmin sorusu cevap sonucunu göster
        /// </summary>
        public void ShowEstimationResult(bool isCorrect, int points, float correctValue, float guessedValue, float accuracy, string unit, float displayTime = 2f)
        {
            SetPanelActive(answerResultPanel, true);

            if (isCorrect)
            {
                if (answerResultIcon != null)
                    answerResultIcon.color = correctColor;
                if (answerResultText != null)
                    answerResultText.text = accuracy >= 100f ? "MÜKEMMEL!" : "DOĞRU!";
                if (answerPointsText != null)
                    answerPointsText.text = $"+{points} TP";

                ShowScorePopup(points, true);
                PlayCorrectAnswerAnimation();
            }
            else if (submittedEstimation == 0f) // Süre doldu
            {
                if (answerResultIcon != null)
                    answerResultIcon.color = timeoutColor;
                if (answerResultText != null)
                    answerResultText.text = "SÜRE DOLDU!";
                if (answerPointsText != null)
                    answerPointsText.text = "0 TP";

                PlayTimeoutAnimation();
            }
            else
            {
                if (answerResultIcon != null)
                    answerResultIcon.color = wrongColor;
                if (answerResultText != null)
                    answerResultText.text = $"YANLIŞ! (%{accuracy:F0} doğruluk)";
                if (answerPointsText != null)
                    answerPointsText.text = "0 TP";

                PlayWrongAnswerAnimation();
            }

            if (correctAnswerText != null)
            {
                string unitStr = !string.IsNullOrEmpty(unit) ? $" {unit}" : "";
                correctAnswerText.text = $"Doğru Cevap: {correctValue:N0}{unitStr}\nSenin Tahminin: {guessedValue:N0}{unitStr}";
            }

            StartCoroutine(HideAnswerResultAfterDelay(displayTime));
        }

        /// <summary>
        /// Doğru ve yanlış cevapları vurgula
        /// </summary>
        private void HighlightCorrectAndWrongAnswers(int correctIndex)
        {
            for (int i = 0; i < optionButtons.Count; i++)
            {
                var buttonImage = optionButtons[i].GetComponent<Image>();
                if (buttonImage == null) continue;

                if (i == correctIndex)
                {
                    // Doğru cevap - yeşil
                    buttonImage.color = correctColor;
                    StartCoroutine(PulseAnimation(optionButtons[i].transform));
                }
                else if (i == selectedOptionIndex)
                {
                    // Seçilen yanlış cevap - kırmızı
                    buttonImage.color = wrongColor;
                    StartCoroutine(ShakeAnimation(optionButtons[i].transform));
                }
                else if (eliminatedOptions.Contains(i))
                {
                    // %50 jokeri ile elenen - gri
                    buttonImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                }
                else
                {
                    // Diğer seçenekler - hafif soluk
                    buttonImage.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
                }
            }
        }
        
        /// <summary>
        /// Oyun sonu ekranını göster
        /// </summary>
        public void ShowGameEnd(List<GameEndPlayerResult> results, int localPlayerScore, int correctAnswers, int totalQuestions, int xpGained)
        {
            HideAllPanels();
            SetPanelActive(gameEndPanel, true);
            
            // Kazananı bul
            if (results.Count > 0)
            {
                var winner = results[0];

                if (gameEndTitleText != null)
                    gameEndTitleText.text = "OYUN BİTTİ!";

                if (winnerText != null)
                {
                    bool isLocalPlayerWinner = winner.playerId == (PlayerManager.Instance?.LocalPlayerData?.playerId ?? "");
                    winnerText.text = isLocalPlayerWinner ? "TEBRİKLER! KAZANDINIZ!" : $"Kazanan: {winner.displayName}";
                    winnerText.color = isLocalPlayerWinner ? correctColor : Color.white;
                }
            }
            
            // Sonuç kartlarını oluştur
            CreateResultCards(results);
            
            // Yerel oyuncu istatistikleri
            if (yourScoreText != null)
                yourScoreText.text = $"Puanınız: {localPlayerScore} TP";
                
            if (yourCorrectText != null)
                yourCorrectText.text = $"Doğru Cevap: {correctAnswers}/{totalQuestions}";
                
            if (xpGainedText != null)
                xpGainedText.text = $"+{xpGained} XP Kazandınız!";
        }
        
        /// <summary>
        /// Oyuncuları güncelle
        /// </summary>
        public void UpdatePlayers(List<InGamePlayerData> players)
        {
            ClearPlayerIndicators();

            if (playersContainer == null || playerIndicatorPrefab == null) return;

            foreach (var player in players)
            {
                var indicator = Instantiate(playerIndicatorPrefab, playersContainer);

                var texts = indicator.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 3)
                {
                    texts[0].text = player.displayName; // Name
                    texts[1].text = $"{player.currentScore} TP"; // Score
                    texts[2].text = player.isEliminated ? "✗" : "..."; // Status
                }

                // Yerel oyuncuyu vurgula
                if (player.isLocalPlayer)
                {
                    var bg = indicator.GetComponent<Image>();
                    if (bg != null) bg.color = new Color(0.3f, 0.5f, 0.7f, 0.8f);
                }

                playerIndicators.Add(indicator);
            }
        }
        
        /// <summary>
        /// Yerel oyuncu skorunu güncelle
        /// </summary>
        public void UpdateLocalScore(int score, int correctAnswers)
        {
            if (localScoreText != null)
                localScoreText.text = $"{score} TP";
                
            if (localCorrectText != null)
                localCorrectText.text = $"{correctAnswers} Doğru";
        }
        
        /// <summary>
        /// Oyuncu cevap durumunu güncelle
        /// </summary>
        public void UpdatePlayerAnswerStatus(string playerId, bool hasAnswered, bool isCorrect)
        {
            // Player indicator'ları güncelle
            foreach (var indicator in playerIndicators)
            {
                // ID kontrolü yapılabilir
            }
        }
        
        /// <summary>
        /// Pause menüsünü göster
        /// </summary>
        public void ShowPauseMenu()
        {
            SetPanelActive(pausePanel, true);
            Time.timeScale = 0f;
        }
        
        /// <summary>
        /// Pause menüsünü gizle
        /// </summary>
        public void HidePauseMenu()
        {
            SetPanelActive(pausePanel, false);
            Time.timeScale = 1f;
        }
        
        #endregion
        
        #region Private Methods
        
        private void HideAllPanels()
        {
            SetPanelActive(gamePanel, false);
            SetPanelActive(countdownPanel, false);
            SetPanelActive(questionPanel, false);
            SetPanelActive(answerResultPanel, false);
            SetPanelActive(gameEndPanel, false);
            SetPanelActive(pausePanel, false);
            SetPanelActive(loadingPanel, false);
        }
        
        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }
        
        private void CreateOptions(List<string> options)
        {
            // Mevcut seçenekleri temizle
            foreach (var btn in optionButtons)
            {
                if (btn != null) Destroy(btn);
            }
            optionButtons.Clear();
            
            if (optionsContainer == null || optionButtonPrefab == null) return;
            
            string[] letters = { "A", "B", "C", "D" };
            
            for (int i = 0; i < options.Count && i < 4; i++)
            {
                var optionGo = Instantiate(optionButtonPrefab, optionsContainer);
                
                // Letter ve Text ayarla
                var texts = optionGo.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = letters[i];
                    texts[1].text = options[i];
                }
                else if (texts.Length >= 1)
                {
                    texts[0].text = $"{letters[i]}. {options[i]}";
                }
                
                // Button click event
                var button = optionGo.GetComponent<Button>();
                if (button != null)
                {
                    int index = i;
                    button.onClick.AddListener(() => SelectOption(index));
                }
                
                optionButtons.Add(optionGo);
            }
        }
        
        private void SelectOption(int index)
        {
            if (isAnswered) return;

            isAnswered = true;
            selectedOptionIndex = index;

            // Zamanlayıcıyı durdur
            StopTimer();

            // Seçilen butonu vurgula ve animasyon uygula
            if (index >= 0 && index < optionButtons.Count)
            {
                var selectedBtn = optionButtons[index].GetComponent<Image>();
                if (selectedBtn != null)
                    selectedBtn.color = new Color(0.3f, 0.5f, 0.9f);

                // Seçim animasyonu
                StartCoroutine(OptionSelectAnimation(optionButtons[index].transform));
            }

            // Diğer butonları devre dışı bırak ve soluk göster
            for (int i = 0; i < optionButtons.Count; i++)
            {
                var btn = optionButtons[i];
                var button = btn.GetComponent<Button>();
                if (button != null) button.interactable = false;

                // Seçilmeyen butonları soluk göster
                if (i != index && !eliminatedOptions.Contains(i))
                {
                    var image = btn.GetComponent<Image>();
                    if (image != null)
                        image.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);
                }
            }

            // Event'i tetikle
            OnOptionSelected?.Invoke(index);
        }
        
        private void StartTimer(float duration)
        {
            StopTimer();
            currentTime = duration;
            timerCoroutine = StartCoroutine(TimerCoroutine(duration));
        }
        
        private void StopTimer()
        {
            if (timerCoroutine != null)
            {
                StopCoroutine(timerCoroutine);
                timerCoroutine = null;
            }
        }
        
        private IEnumerator TimerCoroutine(float duration)
        {
            currentTime = duration;
            
            while (currentTime > 0)
            {
                // Timer UI güncelle
                if (timerText != null)
                    timerText.text = Mathf.CeilToInt(currentTime).ToString();
                    
                if (timerFillImage != null)
                    timerFillImage.fillAmount = currentTime / duration;
                    
                // Renk değişimi (son 10 saniye)
                if (currentTime <= 10f && timerText != null)
                {
                    timerText.color = currentTime <= 5f ? wrongColor : timeoutColor;
                }
                
                currentTime -= Time.deltaTime;
                yield return null;
            }
            
            // Süre doldu
            if (!isAnswered)
            {
                isAnswered = true;
                selectedOptionIndex = -1;
                OnTimeUp?.Invoke();
            }
        }
        
        private IEnumerator CountdownCoroutine(int seconds, string message)
        {
            if (countdownMessageText != null)
                countdownMessageText.text = message;
                
            for (int i = seconds; i > 0; i--)
            {
                if (countdownText != null)
                {
                    countdownText.text = i.ToString();
                    // Animasyon efekti
                    countdownText.transform.localScale = Vector3.one * 1.5f;
                    
                    float elapsed = 0f;
                    while (elapsed < 1f)
                    {
                        elapsed += Time.deltaTime;
                        float t = elapsed / 1f;
                        countdownText.transform.localScale = Vector3.Lerp(Vector3.one * 1.5f, Vector3.one, t);
                        yield return null;
                    }
                }
                else
                {
                    yield return new WaitForSeconds(1f);
                }
            }
            
            if (countdownText != null)
                countdownText.text = "BAŞLA!";
                
            yield return new WaitForSeconds(0.5f);
            
            SetPanelActive(countdownPanel, false);
        }
        
        private IEnumerator HideAnswerResultAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            SetPanelActive(answerResultPanel, false);
        }
        
        private void ShowScorePopup(int points, bool isPositive)
        {
            if (scorePopupPrefab == null || localPlayerIndicator == null) return;
            
            var popup = Instantiate(scorePopupPrefab, localPlayerIndicator.transform);
            var text = popup.GetComponentInChildren<TextMeshProUGUI>();
            
            if (text != null)
            {
                text.text = isPositive ? $"+{points}" : $"{points}";
                text.color = isPositive ? correctColor : wrongColor;
            }
            
            // Animasyon ve yok etme
            StartCoroutine(AnimateAndDestroyPopup(popup));
        }
        
        private IEnumerator AnimateAndDestroyPopup(GameObject popup)
        {
            float duration = 1.5f;
            float elapsed = 0f;
            Vector3 startPos = popup.transform.localPosition;
            Vector3 endPos = startPos + Vector3.up * 100f;
            
            var canvasGroup = popup.GetComponent<CanvasGroup>();
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                popup.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
                
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f - t;
                    
                yield return null;
            }
            
            Destroy(popup);
        }
        
        private void CreateResultCards(List<GameEndPlayerResult> results)
        {
            // Mevcut kartları temizle
            foreach (var card in resultCards)
            {
                if (card != null) Destroy(card);
            }
            resultCards.Clear();

            if (resultsContainer == null || playerResultPrefab == null) return;

            for (int i = 0; i < results.Count && i < 3; i++)
            {
                var result = results[i];
                var card = Instantiate(playerResultPrefab, resultsContainer);

                var texts = card.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 4)
                {
                    texts[0].text = result.finalRank.ToString(); // Position
                    texts[1].text = result.displayName; // Name
                    texts[2].text = $"{result.finalScore} TP"; // Score
                    int totalAnswers = result.correctAnswers + result.wrongAnswers;
                    texts[3].text = $"{result.correctAnswers}/{totalAnswers}"; // Correct
                }

                // 1. sıra için özel renk
                if (i == 0)
                {
                    var bg = card.GetComponent<Image>();
                    if (bg != null) bg.color = new Color(1f, 0.85f, 0.2f, 0.3f);
                }

                resultCards.Add(card);
            }
        }
        
        private void ClearPlayerIndicators()
        {
            foreach (var indicator in playerIndicators)
            {
                if (indicator != null) Destroy(indicator);
            }
            playerIndicators.Clear();
        }
        
        #endregion
        
        #region Button Callbacks
        
        public void OnPlayAgainButtonClicked()
        {
            OnPlayAgainClicked?.Invoke();
        }
        
        public void OnMainMenuButtonClicked()
        {
            OnMainMenuClicked?.Invoke();
        }
        
        public void OnPauseButtonClicked()
        {
            OnPauseClicked?.Invoke();
            ShowPauseMenu();
        }
        
        public void OnResumeButtonClicked()
        {
            OnResumeClicked?.Invoke();
            HidePauseMenu();
        }
        
        #endregion
        
        #region Event Handlers

        private void HandleQuestionReceived(QuestionData question)
        {
            // Soru numarası GameStateData'dan alınmalı
            int questionNumber = currentQuestionIndex + 1;
            int totalQuestions = 4; // Her turda 4 soru
            ShowQuestion(question, questionNumber, totalQuestions);
            currentQuestionIndex++;
        }

        private void HandleQuestionResultReceived(QuestionResultData result)
        {
            // Yerel oyuncunun sonucunu bul
            string localPlayerId = PlayerManager.Instance?.LocalPlayerData?.playerId ?? "";
            var localResult = result.playerResults?.Find(p => p.playerId == localPlayerId);

            if (localResult != null)
            {
                string correctAnswer = "";
                if (result.correctAnswerIndex >= 0 && lastQuestionOptions != null && result.correctAnswerIndex < lastQuestionOptions.Count)
                {
                    correctAnswer = lastQuestionOptions[result.correctAnswerIndex];
                }

                ShowAnswerResult(localResult.isCorrect, localResult.earnedPoints, correctAnswer);
            }
        }

        private void HandleGameEnded(List<GameEndPlayerResult> results)
        {
            int localScore = 0;
            int correctAnswers = 0;
            int totalQuestions = 10;

            string localPlayerId = PlayerManager.Instance?.LocalPlayerData?.playerId ?? "";
            foreach (var result in results)
            {
                if (result.playerId == localPlayerId)
                {
                    localScore = result.finalScore;
                    correctAnswers = result.correctAnswers;
                    totalQuestions = result.correctAnswers + result.wrongAnswers;
                    break;
                }
            }

            int xpGained = Mathf.RoundToInt(localScore * 0.1f) + (correctAnswers * 5);
            ShowGameEnd(results, localScore, correctAnswers, totalQuestions, xpGained);
        }

        private void HandleScoreChanged(string playerId, int oldScore, int newScore)
        {
            // Oyuncu skorunu güncelle
            if (playerId == PlayerManager.Instance?.LocalPlayerData?.playerId)
            {
                if (localScoreText != null)
                    localScoreText.text = $"{newScore} TP";
            }
        }

        private void HandleGameStarting(GameStartData data)
        {
            ShowCountdown(3, "Oyun Başlıyor!");
            currentQuestionIndex = 0;
        }

        private void HandleQuestionTimerStarted(float timeLimit)
        {
            StartTimer(timeLimit);
        }

        #endregion

        #region Helper Methods

        private int CalculateQuestionPoints(int difficultyLevel)
        {
            // Zorluk seviyesine göre puan hesaplama
            return difficultyLevel switch
            {
                <= 3 => 100,
                <= 5 => 150,
                <= 7 => 200,
                <= 9 => 250,
                _ => 300
            };
        }

        private string GetCategoryDisplayName(QuestionCategory category)
        {
            return category switch
            {
                QuestionCategory.Turkce => "Türkçe",
                QuestionCategory.Ingilizce => "İngilizce",
                QuestionCategory.Bilim => "Bilim",
                QuestionCategory.Sanat => "Sanat",
                QuestionCategory.Spor => "Spor",
                QuestionCategory.GenelKultur => "Genel Kültür",
                QuestionCategory.Tarih => "Tarih",
                _ => "Bilinmeyen"
            };
        }

        private Color GetDifficultyColor(int difficultyLevel)
        {
            return difficultyLevel switch
            {
                <= 3 => new Color(0.3f, 0.9f, 0.4f), // Yeşil - Kolay
                <= 6 => new Color(0.9f, 0.8f, 0.2f), // Sarı - Orta
                <= 8 => new Color(0.9f, 0.5f, 0.2f), // Turuncu - Zor
                _ => new Color(0.9f, 0.2f, 0.2f)     // Kırmızı - Uzman
            };
        }

        #endregion

        #region Setup Methods

        private void SetupJokerButtons()
        {
            if (joker5050Button != null)
                joker5050Button.onClick.AddListener(() => UseJoker(JokerType.Yuzde50));

            if (jokerAudienceButton != null)
                jokerAudienceButton.onClick.AddListener(() => UseJoker(JokerType.OyuncularaSor));

            if (jokerParrotButton != null)
                jokerParrotButton.onClick.AddListener(() => UseJoker(JokerType.Papagan));

            if (jokerTelescopeButton != null)
                jokerTelescopeButton.onClick.AddListener(() => UseJoker(JokerType.Teleskop));
        }

        private void SetupEstimationUI()
        {
            if (estimationSubmitButton != null)
                estimationSubmitButton.onClick.AddListener(SubmitEstimation);

            if (estimationInputField != null)
            {
                estimationInputField.onEndEdit.AddListener(OnEstimationInputEndEdit);
            }

            if (estimationSlider != null)
            {
                estimationSlider.onValueChanged.AddListener(OnEstimationSliderChanged);
            }
        }

        private void UpdateJokerButtons(QuestionType questionType)
        {
            if (jokerContainer != null)
                jokerContainer.SetActive(true);

            // %50 jokeri - sadece çoktan seçmeli
            if (joker5050Button != null)
            {
                joker5050Button.gameObject.SetActive(questionType == QuestionType.CoktanSecmeli);
                // TODO: Joker kullanım hakkı kontrolü
            }

            // Oyunculara sor - sadece çoktan seçmeli
            if (jokerAudienceButton != null)
            {
                jokerAudienceButton.gameObject.SetActive(questionType == QuestionType.CoktanSecmeli);
            }

            // Papağan - sadece tahmin soruları
            if (jokerParrotButton != null)
            {
                jokerParrotButton.gameObject.SetActive(questionType == QuestionType.Tahmin);
            }

            // Teleskop - sadece tahmin soruları
            if (jokerTelescopeButton != null)
            {
                jokerTelescopeButton.gameObject.SetActive(questionType == QuestionType.Tahmin);
            }
        }

        #endregion

        #region Joker Methods

        private void UseJoker(JokerType jokerType)
        {
            if (isAnswered) return;

            OnJokerUsed?.Invoke(jokerType);
            GameEvents.TriggerJokerUsed(PlayerManager.Instance?.LocalPlayerData?.playerId ?? "", jokerType);
        }

        /// <summary>
        /// %50 joker sonucunu uygula
        /// </summary>
        public void Apply5050Result(List<int> eliminatedIndices)
        {
            if (eliminatedIndices == null) return;

            eliminatedOptions.AddRange(eliminatedIndices);

            foreach (int index in eliminatedIndices)
            {
                if (index >= 0 && index < optionButtons.Count)
                {
                    var button = optionButtons[index].GetComponent<Button>();
                    var image = optionButtons[index].GetComponent<Image>();

                    if (button != null)
                        button.interactable = false;

                    if (image != null)
                        image.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

                    // X işareti göster
                    var texts = optionButtons[index].GetComponentsInChildren<TextMeshProUGUI>();
                    if (texts.Length > 0)
                        texts[0].text = "✗";
                }
            }

            if (joker5050Button != null)
                joker5050Button.interactable = false;
        }

        /// <summary>
        /// Oyunculara sor joker sonucunu göster
        /// </summary>
        public void ShowAudienceResult(Dictionary<int, float> percentages)
        {
            if (percentages == null) return;

            SetPanelActive(audienceResultPanel, true);

            foreach (var kvp in percentages)
            {
                if (kvp.Key >= 0 && kvp.Key < audienceBarImages.Length && audienceBarImages[kvp.Key] != null)
                {
                    audienceBarImages[kvp.Key].fillAmount = kvp.Value / 100f;
                }

                if (kvp.Key >= 0 && kvp.Key < audiencePercentTexts.Length && audiencePercentTexts[kvp.Key] != null)
                {
                    audiencePercentTexts[kvp.Key].text = $"%{kvp.Value:F0}";
                }

                // Butona da yüzde ekle
                if (kvp.Key >= 0 && kvp.Key < optionButtons.Count)
                {
                    var texts = optionButtons[kvp.Key].GetComponentsInChildren<TextMeshProUGUI>();
                    if (texts.Length >= 2)
                        texts[1].text += $" (%{kvp.Value:F0})";
                }
            }

            if (jokerAudienceButton != null)
                jokerAudienceButton.interactable = false;
        }

        /// <summary>
        /// Papağan joker sonucunu göster
        /// </summary>
        public void ShowParrotHint(float hint, float accuracy)
        {
            if (estimationHintText != null)
            {
                string accuracyStr = accuracy >= 0.95f ? "çok yakın" : accuracy >= 0.85f ? "yakın" : "tahmini";
                estimationHintText.text = $"Papağan tahmini ({accuracyStr}): {hint:N0}";
                estimationHintText.color = correctColor;
            }

            if (jokerParrotButton != null)
                jokerParrotButton.interactable = false;
        }

        /// <summary>
        /// Teleskop joker sonucunu göster
        /// </summary>
        public void ShowTelescopeOptions(List<TelescopeOption> options)
        {
            if (options == null || options.Count == 0) return;

            // Mevcut input'u temizle ve dropdown/slider göster
            if (estimationInputField != null)
                estimationInputField.gameObject.SetActive(false);

            if (estimationSlider != null)
            {
                estimationSlider.gameObject.SetActive(true);

                // Slider değerlerini ayarla
                float minVal = float.MaxValue;
                float maxVal = float.MinValue;

                foreach (var opt in options)
                {
                    if (float.TryParse(opt.optionText, out float val))
                    {
                        minVal = Mathf.Min(minVal, val);
                        maxVal = Mathf.Max(maxVal, val);
                    }
                }

                estimationSlider.minValue = minVal;
                estimationSlider.maxValue = maxVal;
                estimationSlider.value = (minVal + maxVal) / 2f;
            }

            if (jokerTelescopeButton != null)
                jokerTelescopeButton.interactable = false;
        }

        #endregion

        #region Estimation Input

        private void SubmitEstimation()
        {
            if (isAnswered) return;

            float value = 0f;

            if (estimationSlider != null && estimationSlider.gameObject.activeSelf)
            {
                value = estimationSlider.value;
            }
            else if (estimationInputField != null && float.TryParse(estimationInputField.text, out float inputValue))
            {
                value = inputValue;
            }
            else
            {
                // Geçersiz giriş - uyarı göster
                if (estimationHintText != null)
                {
                    estimationHintText.text = "Geçerli bir sayı giriniz!";
                    estimationHintText.color = wrongColor;
                }
                return;
            }

            isAnswered = true;
            submittedEstimation = value;

            // Input'u devre dışı bırak
            if (estimationInputField != null)
                estimationInputField.interactable = false;

            if (estimationSubmitButton != null)
                estimationSubmitButton.interactable = false;

            if (estimationSlider != null)
                estimationSlider.interactable = false;

            // Zamanlayıcıyı durdur
            StopTimer();

            // Event'i tetikle
            OnEstimationSubmitted?.Invoke(value);
        }

        private void OnEstimationInputEndEdit(string value)
        {
            // Enter tuşuna basıldığında gönder
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SubmitEstimation();
            }
        }

        private void OnEstimationSliderChanged(float value)
        {
            if (estimationSliderValueText != null)
            {
                estimationSliderValueText.text = value.ToString("N0");
            }
        }

        #endregion

        #region Animations

        private void PlayCorrectAnswerAnimation()
        {
            if (answerResultPanel != null)
            {
                StartCoroutine(PulseAnimation(answerResultPanel.transform));
            }
        }

        private void PlayWrongAnswerAnimation()
        {
            if (answerResultPanel != null)
            {
                StartCoroutine(ShakeAnimation(answerResultPanel.transform));
            }
        }

        private void PlayTimeoutAnimation()
        {
            if (answerResultPanel != null)
            {
                StartCoroutine(FadeInAnimation(answerResultPanel.GetComponent<CanvasGroup>()));
            }
        }

        private IEnumerator PulseAnimation(Transform target)
        {
            if (target == null) yield break;

            Vector3 originalScale = target.localScale;
            float duration = 0.3f;
            float elapsed = 0f;

            // Büyüt
            while (elapsed < duration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration / 2f);
                target.localScale = Vector3.Lerp(originalScale, originalScale * 1.1f, t);
                yield return null;
            }

            elapsed = 0f;
            // Küçült
            while (elapsed < duration / 2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration / 2f);
                target.localScale = Vector3.Lerp(originalScale * 1.1f, originalScale, t);
                yield return null;
            }

            target.localScale = originalScale;
        }

        private IEnumerator ShakeAnimation(Transform target)
        {
            if (target == null) yield break;

            Vector3 originalPosition = target.localPosition;
            float duration = 0.3f;
            float elapsed = 0f;
            float shakeAmount = 10f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float x = Mathf.Sin(elapsed * 50f) * shakeAmount * (1f - elapsed / duration);
                target.localPosition = originalPosition + new Vector3(x, 0, 0);
                yield return null;
            }

            target.localPosition = originalPosition;
        }

        private IEnumerator FadeInAnimation(CanvasGroup canvasGroup)
        {
            if (canvasGroup == null) yield break;

            float duration = 0.3f;
            float elapsed = 0f;

            canvasGroup.alpha = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = elapsed / duration;
                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        private IEnumerator OptionSelectAnimation(Transform target)
        {
            if (target == null) yield break;

            Vector3 originalScale = target.localScale;

            // Hızlı küçült
            target.localScale = originalScale * 0.9f;
            yield return new WaitForSeconds(0.05f);

            // Normal boyuta dön
            float duration = 0.15f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                target.localScale = Vector3.Lerp(originalScale * 0.9f, originalScale, t);
                yield return null;
            }

            target.localScale = originalScale;
        }

        #endregion
    }
}
