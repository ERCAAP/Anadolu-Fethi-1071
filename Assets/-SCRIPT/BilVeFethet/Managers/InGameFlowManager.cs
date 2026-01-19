using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;
using BilVeFethet.UI;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Oyun İçi Akış Yöneticisi - Oyun akışını koordine eder
    /// GameUIManager, QuestionManager, BotAIController ve diğer sistemler arasında köprü görevi görür
    /// </summary>
    public class InGameFlowManager : Singleton<InGameFlowManager>
    {
        [Header("Oyun Ayarları")]
        [SerializeField] private float questionAnswerDelay = 2f;
        [SerializeField] private float betweenQuestionDelay = 1.5f;
        [SerializeField] private float gameStartCountdown = 3f;
        [SerializeField] private int questionsPerRound = 4;
        [SerializeField] private int totalRounds = 4;

        [Header("Puan Ayarları")]
        [SerializeField] private int basePoints = 100;
        [SerializeField] private int speedBonusMax = 50;
        [SerializeField] private float speedBonusTimeThreshold = 5f;

        // Game State
        private GameStateData currentGameState;
        private QuestionData currentQuestion;
        private bool isGameActive = false;
        private int currentRound = 0;
        private int currentQuestionIndex = 0;
        private bool isWaitingForAnswer = false;
        private float questionStartTime;

        // Answer tracking
        private Dictionary<string, PlayerAnswerData> _playerAnswers;
        private int _answeredPlayerCount;

        // Events
        public event Action OnGameFlowStarted;
        public event Action OnGameFlowEnded;
        public event Action<int> OnRoundChanged;
        public event Action<int, int> OnQuestionIndexChanged; // currentIndex, total
        public event Action<QuestionData> OnQuestionStarted;
        public event Action<List<PlayerAnswerResult>> OnQuestionEnded;

        // Properties
        public bool IsGameActive => isGameActive;
        public int CurrentRound => currentRound;
        public int CurrentQuestionIndex => currentQuestionIndex;
        public GameStateData CurrentGameState => currentGameState;
        public QuestionData CurrentQuestion => currentQuestion;

        protected override void Awake()
        {
            base.Awake();
            _playerAnswers = new Dictionary<string, PlayerAnswerData>();
        }

        private void Start()
        {
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            // UI Events
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.OnOptionSelected += HandleOptionSelected;
                GameUIManager.Instance.OnEstimationSubmitted += HandleEstimationSubmitted;
                GameUIManager.Instance.OnTimeUp += HandleTimeUp;
                GameUIManager.Instance.OnPlayAgainClicked += HandlePlayAgain;
                GameUIManager.Instance.OnMainMenuClicked += HandleMainMenu;
                GameUIManager.Instance.OnJokerUsed += HandleJokerUsed;
            }

            // Game Events
            GameEvents.OnBotGameStarted += HandleBotGameStarted;
            GameEvents.OnGameFound += HandleGameFound;
            GameEvents.OnQuestionResultReceived += HandleQuestionResult;
            GameEvents.OnGameEnded += HandleGameEnded;
            GameEvents.OnPhaseChanged += HandlePhaseChanged;
            GameEvents.OnJokerResultReceived += HandleJokerResult;

            // Bot Events
            GameEvents.OnBotAnswerSubmittedDetailed += HandleBotAnswerSubmitted;
        }

        private void UnsubscribeFromEvents()
        {
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.OnOptionSelected -= HandleOptionSelected;
                GameUIManager.Instance.OnEstimationSubmitted -= HandleEstimationSubmitted;
                GameUIManager.Instance.OnTimeUp -= HandleTimeUp;
                GameUIManager.Instance.OnPlayAgainClicked -= HandlePlayAgain;
                GameUIManager.Instance.OnMainMenuClicked -= HandleMainMenu;
                GameUIManager.Instance.OnJokerUsed -= HandleJokerUsed;
            }

            GameEvents.OnBotGameStarted -= HandleBotGameStarted;
            GameEvents.OnGameFound -= HandleGameFound;
            GameEvents.OnQuestionResultReceived -= HandleQuestionResult;
            GameEvents.OnGameEnded -= HandleGameEnded;
            GameEvents.OnPhaseChanged -= HandlePhaseChanged;
            GameEvents.OnJokerResultReceived -= HandleJokerResult;
            GameEvents.OnBotAnswerSubmittedDetailed -= HandleBotAnswerSubmitted;
        }

        /// <summary>
        /// Bot cevabı geldiğinde
        /// </summary>
        private void HandleBotAnswerSubmitted(string playerId, int answerIndex, float guessedValue, bool isCorrect, int points)
        {
            if (!isWaitingForAnswer) return;

            // Bot cevabını kaydet
            var answerData = new PlayerAnswerData
            {
                playerId = playerId,
                questionId = currentQuestion?.questionId,
                selectedAnswerIndex = answerIndex,
                guessedValue = guessedValue,
                answerTime = Time.time - questionStartTime
            };

            _playerAnswers[playerId] = answerData;
            _answeredPlayerCount++;

            Debug.Log($"[InGameFlowManager] Bot cevapladı: {playerId}, Doğru: {isCorrect}, Puan: {points}");

            // Tüm oyuncular cevapladıysa sonuçları göster
            CheckAllPlayersAnswered();
        }

        #region Public Methods

        /// <summary>
        /// Oyunu başlat
        /// </summary>
        public void StartGame(GameStateData gameState)
        {
            if (isGameActive)
            {
                Debug.LogWarning("[InGameFlowManager] Game already active");
                return;
            }

            currentGameState = gameState;
            isGameActive = true;
            currentRound = 1;
            currentQuestionIndex = 0;

            Debug.Log("[InGameFlowManager] Starting game flow...");
            OnGameFlowStarted?.Invoke();

            StartCoroutine(GameStartSequence());
        }

        /// <summary>
        /// Oyunu durdur
        /// </summary>
        public void StopGame()
        {
            isGameActive = false;
            StopAllCoroutines();
            OnGameFlowEnded?.Invoke();
            Debug.Log("[InGameFlowManager] Game flow stopped");
        }

        /// <summary>
        /// Bot oyunu başlat
        /// </summary>
        public void StartBotGame(List<InGamePlayerData> bots)
        {
            // Oyun durumu oluştur
            var gameState = new GameStateData
            {
                gameId = Guid.NewGuid().ToString(),
                currentPhase = GamePhase.Fetih,
                currentRound = 1,
                players = new List<InGamePlayerData>(bots)
            };

            // Yerel oyuncuyu ekle
            var localPlayer = new InGamePlayerData
            {
                playerId = PlayerManager.Instance?.LocalPlayerData?.playerId ?? "local",
                displayName = PlayerManager.Instance?.LocalPlayerData?.displayName ?? "Oyuncu",
                isLocalPlayer = true,
                currentScore = 0,
                correctAnswers = 0,
                wrongAnswers = 0
            };
            gameState.players.Insert(0, localPlayer);

            StartGame(gameState);
        }

        /// <summary>
        /// Çoktan seçmeli soru cevabını gönder
        /// </summary>
        public void SubmitAnswer(int answerIndex)
        {
            if (!isWaitingForAnswer || currentQuestion == null)
            {
                Debug.LogWarning("[InGameFlowManager] Not waiting for answer or no current question");
                return;
            }

            string playerId = ProfileManager.Instance?.CurrentProfile?.userId ??
                              PlayerManager.Instance?.LocalPlayerData?.playerId ?? "local";

            // Cevabı kaydet
            var answerData = new PlayerAnswerData
            {
                playerId = playerId,
                questionId = currentQuestion.questionId,
                selectedAnswerIndex = answerIndex,
                answerTime = Time.time - questionStartTime
            };

            _playerAnswers[playerId] = answerData;
            _answeredPlayerCount++;

            // Cevabı değerlendir
            bool isCorrect = answerIndex == currentQuestion.correctAnswerIndex;
            int points = isCorrect ? CalculatePointsWithSpeed(answerData.answerTime, 1) : 0;

            // Yerel oyuncu skorunu güncelle
            UpdateLocalPlayerScore(isCorrect, points);

            // Sonucu göster
            string correctAnswer = currentQuestion.options[currentQuestion.correctAnswerIndex];
            GameUIManager.Instance?.ShowAnswerResult(isCorrect, points, correctAnswer, questionAnswerDelay);

            // Event tetikle
            GameEvents.TriggerPlayerAnswered(playerId, answerIndex);

            Debug.Log($"[InGameFlowManager] Yerel oyuncu cevapladı - Doğru: {isCorrect}, Puan: {points}, Süre: {answerData.answerTime:F1}s");

            // Tüm oyuncular cevapladıysa sonuçları göster
            CheckAllPlayersAnswered();
        }

        /// <summary>
        /// Tahmin sorusu cevabını gönder
        /// </summary>
        public void SubmitEstimationAnswer(float guessedValue)
        {
            if (!isWaitingForAnswer || currentQuestion == null)
            {
                Debug.LogWarning("[InGameFlowManager] Not waiting for answer or no current question");
                return;
            }

            if (currentQuestion.questionType != QuestionType.Tahmin)
            {
                Debug.LogWarning("[InGameFlowManager] Current question is not an estimation question");
                return;
            }

            string playerId = ProfileManager.Instance?.CurrentProfile?.userId ??
                              PlayerManager.Instance?.LocalPlayerData?.playerId ?? "local";

            float correctValue = currentQuestion.correctValue;
            float tolerance = currentQuestion.tolerance;

            // Doğruluğu hesapla
            float difference = Mathf.Abs(guessedValue - correctValue);
            float accuracy = Mathf.Max(0f, 100f - (difference / correctValue * 100f));

            // Tolerans dahilinde mi kontrol et
            bool isCorrect = difference <= tolerance;

            // Cevabı kaydet
            var answerData = new PlayerAnswerData
            {
                playerId = playerId,
                questionId = currentQuestion.questionId,
                guessedValue = guessedValue,
                answerTime = Time.time - questionStartTime
            };

            _playerAnswers[playerId] = answerData;
            _answeredPlayerCount++;

            // Puan hesapla - doğruluk oranına göre
            int points = 0;
            if (isCorrect)
            {
                points = CalculatePointsWithSpeed(answerData.answerTime, 1);
                // Mükemmel cevap bonusu
                if (difference == 0)
                {
                    points = Mathf.RoundToInt(points * 1.5f);
                }
            }
            else if (accuracy >= 80f)
            {
                // Yakın cevap - kısmi puan
                points = Mathf.RoundToInt(CalculatePoints() * 0.5f);
                isCorrect = true; // Yakın cevap da doğru sayılır
            }

            // Yerel oyuncu skorunu güncelle
            UpdateLocalPlayerScore(isCorrect, points);

            // Sonucu göster
            GameUIManager.Instance?.ShowEstimationResult(
                isCorrect,
                points,
                correctValue,
                guessedValue,
                accuracy,
                currentQuestion.valueUnit,
                questionAnswerDelay
            );

            // Event tetikle
            GameEvents.TriggerPlayerAnswered(playerId, -1); // -1 = tahmin sorusu

            Debug.Log($"[InGameFlowManager] Tahmin cevabı - Tahmin: {guessedValue}, Doğru: {correctValue}, Doğruluk: {accuracy:F1}%");

            // Tüm oyuncular cevapladıysa sonuçları göster
            CheckAllPlayersAnswered();
        }

        #endregion

        #region Private Methods

        private IEnumerator GameStartSequence()
        {
            // Yükleme ekranı
            GameUIManager.Instance?.ShowLoading("Oyun Hazırlanıyor...");
            yield return new WaitForSeconds(1f);

            // Geri sayım
            GameUIManager.Instance?.ShowCountdown((int)gameStartCountdown, "Oyun Başlıyor!");
            yield return new WaitForSeconds(gameStartCountdown + 0.5f);

            // İlk soruyu göster
            RequestNextQuestion();
        }

        private void RequestNextQuestion()
        {
            if (!isGameActive) return;

            // Cevap takibini sıfırla
            _playerAnswers.Clear();
            _answeredPlayerCount = 0;

            // QuestionLoader'dan soru al (varsa), yoksa demo soru kullan
            QuestionData question = null;

            if (QuestionLoader.Instance != null && QuestionLoader.Instance.IsLoaded)
            {
                question = QuestionLoader.Instance.GetRandomQuestion();
            }

            if (question == null)
            {
                question = CreateDemoQuestion();
            }

            currentQuestion = question;
            currentQuestionIndex++;
            questionStartTime = Time.time;

            Debug.Log($"[InGameFlowManager] Showing question {currentQuestionIndex}/{questionsPerRound}");

            // Soruyu UI'da göster
            GameUIManager.Instance?.ShowQuestion(question, currentQuestionIndex, questionsPerRound);
            GameEvents.TriggerQuestionReceived(question);

            isWaitingForAnswer = true;
            OnQuestionIndexChanged?.Invoke(currentQuestionIndex, questionsPerRound);
            OnQuestionStarted?.Invoke(question);

            // Zamanlayıcı başlat
            GameEvents.TriggerQuestionTimerStarted(question.timeLimit);
        }

        /// <summary>
        /// Tüm oyuncuların cevap verip vermediğini kontrol et
        /// </summary>
        private void CheckAllPlayersAnswered()
        {
            if (currentGameState == null) return;

            int activePlayerCount = currentGameState.GetActivePlayerCount();

            // Tüm aktif oyuncular cevapladıysa
            if (_answeredPlayerCount >= activePlayerCount)
            {
                // Soru sonuçlarını hesapla ve göster
                StartCoroutine(ProcessQuestionResults());
            }
        }

        /// <summary>
        /// Soru sonuçlarını işle
        /// </summary>
        private IEnumerator ProcessQuestionResults()
        {
            isWaitingForAnswer = false;

            // Kısa bekleme (animasyonlar için)
            yield return new WaitForSeconds(0.5f);

            // Sonuçları hesapla
            var results = CalculateQuestionResults();

            // Event tetikle
            var resultData = new QuestionResultData
            {
                questionId = currentQuestion?.questionId,
                playerResults = results,
                correctAnswerIndex = currentQuestion?.correctAnswerIndex ?? 0,
                correctValue = currentQuestion?.correctValue ?? 0
            };

            GameEvents.TriggerQuestionResultReceived(resultData);
            OnQuestionEnded?.Invoke(results);

            // Sonraki soruya geç
            StartCoroutine(NextQuestionSequence());
        }

        /// <summary>
        /// Soru sonuçlarını hesapla
        /// </summary>
        private List<PlayerAnswerResult> CalculateQuestionResults()
        {
            var results = new List<PlayerAnswerResult>();
            if (currentQuestion == null || currentGameState == null) return results;

            // Cevapları sırala (hıza göre)
            var sortedAnswers = new List<KeyValuePair<string, PlayerAnswerData>>();
            foreach (var kvp in _playerAnswers)
            {
                sortedAnswers.Add(kvp);
            }
            sortedAnswers.Sort((a, b) => a.Value.answerTime.CompareTo(b.Value.answerTime));

            int rank = 0;
            foreach (var kvp in sortedAnswers)
            {
                var playerId = kvp.Key;
                var answer = kvp.Value;
                var player = currentGameState.GetPlayer(playerId);
                if (player == null) continue;

                bool isCorrect;
                float accuracy = 0f;

                if (currentQuestion.questionType == QuestionType.CoktanSecmeli)
                {
                    isCorrect = answer.selectedAnswerIndex == currentQuestion.correctAnswerIndex;
                }
                else
                {
                    // Tahmin sorusu
                    float diff = Mathf.Abs(answer.guessedValue - currentQuestion.correctValue);
                    isCorrect = diff <= currentQuestion.tolerance;
                    accuracy = Mathf.Max(0f, 100f - (diff / currentQuestion.correctValue * 100f));
                }

                // Puan hesapla
                int earnedPoints = 0;
                if (isCorrect)
                {
                    rank++;
                    earnedPoints = CalculatePointsWithSpeed(answer.answerTime, rank);
                }

                results.Add(new PlayerAnswerResult
                {
                    playerId = playerId,
                    isCorrect = isCorrect,
                    selectedAnswerIndex = answer.selectedAnswerIndex,
                    guessedValue = answer.guessedValue,
                    answerTime = answer.answerTime,
                    earnedPoints = earnedPoints,
                    rank = isCorrect ? rank : 0,
                    accuracy = accuracy
                });
            }

            return results;
        }

        /// <summary>
        /// Hız bonusu dahil puan hesapla
        /// </summary>
        private int CalculatePointsWithSpeed(float answerTime, int rank)
        {
            int points = CalculatePoints();

            // Hız bonusu (ilk X saniyede cevaplama)
            if (answerTime <= speedBonusTimeThreshold)
            {
                float speedRatio = 1f - (answerTime / speedBonusTimeThreshold);
                int speedBonus = Mathf.RoundToInt(speedRatio * speedBonusMax);
                points += speedBonus;
            }

            // Sıralama bonusu
            if (rank == 1) points += 25;
            else if (rank == 2) points += 10;

            return points;
        }

        private IEnumerator NextQuestionSequence()
        {
            yield return new WaitForSeconds(questionAnswerDelay + betweenQuestionDelay);

            if (!isGameActive) yield break;

            // Tur sonu kontrolü
            if (currentQuestionIndex >= questionsPerRound)
            {
                // Tur sonu
                currentRound++;
                currentQuestionIndex = 0;

                GameEvents.TriggerRoundEnded(currentRound - 1);
                OnRoundChanged?.Invoke(currentRound);
                GameEvents.TriggerRoundStarted(currentRound);

                Debug.Log($"[InGameFlowManager] Yeni tur başladı: {currentRound}/{totalRounds}");

                // Oyun sonu kontrolü
                if (currentRound > totalRounds)
                {
                    EndGame();
                    yield break;
                }
            }

            // Sonraki soru
            RequestNextQuestion();
        }

        private void EndGame()
        {
            isGameActive = false;
            isWaitingForAnswer = false;

            Debug.Log("[InGameFlowManager] Game ended");

            // Bot cevaplarını durdur
            BotAIController.Instance?.StopAllBotAnswers();

            // Sonuçları hazırla
            var results = PrepareGameResults();

            // UI'da oyun sonu ekranını göster
            BilVeFethet.UI.GameUIManager.Instance?.ShowGameOver(results);

            // Event tetikle
            GameEvents.TriggerGameEnded(results);
            GameEvents.TriggerPhaseChanged(GamePhase.Savas, GamePhase.GameOver);

            OnGameFlowEnded?.Invoke();

            Debug.Log($"[InGameFlowManager] Kazanan: {(results.Count > 0 ? results[0].displayName : "Yok")}");
        }

        private List<GameEndPlayerResult> PrepareGameResults()
        {
            var results = new List<GameEndPlayerResult>();

            if (currentGameState?.players == null) return results;

            // Oyuncuları skora göre sırala
            var sortedPlayers = new List<InGamePlayerData>(currentGameState.players);
            sortedPlayers.Sort((a, b) => b.currentScore.CompareTo(a.currentScore));

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var player = sortedPlayers[i];
                results.Add(new GameEndPlayerResult
                {
                    playerId = player.playerId,
                    displayName = player.displayName,
                    finalRank = i + 1,
                    finalScore = player.currentScore,
                    correctAnswers = player.correctAnswers,
                    wrongAnswers = player.wrongAnswers,
                    wasEliminated = player.isEliminated
                });
            }

            return results;
        }

        private int CalculatePoints()
        {
            if (currentQuestion == null) return 100;

            // Zorluk seviyesine göre puan
            return currentQuestion.difficultyLevel switch
            {
                <= 3 => 100,
                <= 5 => 150,
                <= 7 => 200,
                <= 9 => 250,
                _ => 300
            };
        }

        private void UpdateLocalPlayerScore(bool isCorrect, int points)
        {
            if (currentGameState == null) return;

            var localPlayer = currentGameState.GetLocalPlayer();
            if (localPlayer == null) return;

            int oldScore = localPlayer.currentScore;

            if (isCorrect)
            {
                localPlayer.currentScore += points;
                localPlayer.correctAnswers++;
            }
            else
            {
                localPlayer.wrongAnswers++;
            }

            // Event tetikle
            GameEvents.TriggerScoreChanged(localPlayer.playerId, oldScore, localPlayer.currentScore);
        }

        private List<QuestionData> demoQuestions = new List<QuestionData>
        {
            new QuestionData
            {
                questionId = "q1",
                questionType = QuestionType.CoktanSecmeli,
                category = QuestionCategory.GenelKultur,
                questionText = "Turkiye'nin baskenti neresidir?",
                timeLimit = 15f,
                difficultyLevel = 3,
                options = new List<string> { "Istanbul", "Ankara", "Izmir", "Bursa" },
                correctAnswerIndex = 1
            },
            new QuestionData
            {
                questionId = "q2",
                questionType = QuestionType.CoktanSecmeli,
                category = QuestionCategory.Tarih,
                questionText = "Malazgirt Meydan Muharebesi hangi yilda gerceklesmistir?",
                timeLimit = 15f,
                difficultyLevel = 5,
                options = new List<string> { "1071", "1453", "1299", "1176" },
                correctAnswerIndex = 0
            },
            new QuestionData
            {
                questionId = "q3",
                questionType = QuestionType.CoktanSecmeli,
                category = QuestionCategory.Bilim,
                questionText = "Suyun kimyasal formulu nedir?",
                timeLimit = 15f,
                difficultyLevel = 2,
                options = new List<string> { "CO2", "H2O", "NaCl", "O2" },
                correctAnswerIndex = 1
            },
            new QuestionData
            {
                questionId = "q4",
                questionType = QuestionType.CoktanSecmeli,
                category = QuestionCategory.GenelKultur,
                questionText = "Dunyanin en buyuk okyanusu hangisidir?",
                timeLimit = 15f,
                difficultyLevel = 3,
                options = new List<string> { "Atlantik", "Hint", "Pasifik", "Kuzey Buz" },
                correctAnswerIndex = 2
            },
            new QuestionData
            {
                questionId = "q5",
                questionType = QuestionType.CoktanSecmeli,
                category = QuestionCategory.Spor,
                questionText = "FIFA Dunya Kupasi kac yilda bir duzenlenir?",
                timeLimit = 15f,
                difficultyLevel = 2,
                options = new List<string> { "2 yil", "3 yil", "4 yil", "5 yil" },
                correctAnswerIndex = 2
            },
            new QuestionData
            {
                questionId = "q6",
                questionType = QuestionType.CoktanSecmeli,
                category = QuestionCategory.Sanat,
                questionText = "Mona Lisa tablosunun ressami kimdir?",
                timeLimit = 15f,
                difficultyLevel = 4,
                options = new List<string> { "Michelangelo", "Leonardo da Vinci", "Raphael", "Van Gogh" },
                correctAnswerIndex = 1
            },
            new QuestionData
            {
                questionId = "q7",
                questionType = QuestionType.CoktanSecmeli,
                category = QuestionCategory.Bilim,
                questionText = "Ilk iPhone hangi yilda piyasaya suruldu?",
                timeLimit = 15f,
                difficultyLevel = 5,
                options = new List<string> { "2005", "2006", "2007", "2008" },
                correctAnswerIndex = 2
            },
            new QuestionData
            {
                questionId = "q8",
                questionType = QuestionType.CoktanSecmeli,
                category = QuestionCategory.Tarih,
                questionText = "Istanbul'un fethi hangi tarihte gerceklesmistir?",
                timeLimit = 15f,
                difficultyLevel = 4,
                options = new List<string> { "29 Mayis 1453", "6 Nisan 1453", "30 Agustos 1071", "23 Nisan 1920" },
                correctAnswerIndex = 0
            },
            // Tahmin soruları
            new QuestionData
            {
                questionId = "q9",
                questionType = QuestionType.Tahmin,
                category = QuestionCategory.Tarih,
                questionText = "Osmanli Devleti hangi yil kurulmustur?",
                timeLimit = 20f,
                difficultyLevel = 5,
                correctValue = 1299f,
                tolerance = 10f,
                valueUnit = "yil"
            },
            new QuestionData
            {
                questionId = "q10",
                questionType = QuestionType.Tahmin,
                category = QuestionCategory.GenelKultur,
                questionText = "Dunyanin en yuksek dagi Everest kac metre yuksekligindedir?",
                timeLimit = 20f,
                difficultyLevel = 7,
                correctValue = 8849f,
                tolerance = 100f,
                valueUnit = "m"
            },
            new QuestionData
            {
                questionId = "q11",
                questionType = QuestionType.Tahmin,
                category = QuestionCategory.Bilim,
                questionText = "Isik saniyede yaklasik kac kilometre yol alir?",
                timeLimit = 20f,
                difficultyLevel = 6,
                correctValue = 300000f,
                tolerance = 5000f,
                valueUnit = "km"
            },
            new QuestionData
            {
                questionId = "q12",
                questionType = QuestionType.Tahmin,
                category = QuestionCategory.Tarih,
                questionText = "Turkiye Cumhuriyeti hangi yil ilan edilmistir?",
                timeLimit = 15f,
                difficultyLevel = 3,
                correctValue = 1923f,
                tolerance = 0f,
                valueUnit = "yil"
            }
        };
        private int demoQuestionIndex = 0;

        private QuestionData CreateDemoQuestion()
        {
            var question = demoQuestions[demoQuestionIndex % demoQuestions.Count];
            question.questionId = Guid.NewGuid().ToString(); // Unique ID
            demoQuestionIndex++;
            return question;
        }

        #endregion

        #region Event Handlers

        private void HandleOptionSelected(int optionIndex)
        {
            SubmitAnswer(optionIndex);
        }

        private void HandleEstimationSubmitted(float value)
        {
            SubmitEstimationAnswer(value);
        }

        private void HandleJokerUsed(JokerType jokerType)
        {
            if (currentQuestion == null) return;

            Debug.Log($"[InGameFlowManager] Joker used: {jokerType}");

            // Joker sonucunu hesapla (yerel)
            var result = CalculateJokerResult(jokerType);

            if (result != null && result.success)
            {
                // Sonucu UI'ya uygula
                switch (jokerType)
                {
                    case JokerType.Yuzde50:
                        GameUIManager.Instance?.Apply5050Result(result.eliminatedOptionIndices);
                        break;
                    case JokerType.OyuncularaSor:
                        GameUIManager.Instance?.ShowAudienceResult(result.audiencePercentages);
                        break;
                    case JokerType.Papagan:
                        GameUIManager.Instance?.ShowParrotHint(result.parrotHint, result.hintAccuracy);
                        break;
                    case JokerType.Teleskop:
                        GameUIManager.Instance?.ShowTelescopeOptions(result.telescopeOptions);
                        break;
                }

                // Event tetikle
                GameEvents.TriggerJokerResultReceived(result);
            }
        }

        private JokerUseResult CalculateJokerResult(JokerType jokerType)
        {
            if (currentQuestion == null) return null;

            var result = new JokerUseResult
            {
                jokerType = jokerType,
                success = true
            };

            switch (jokerType)
            {
                case JokerType.Yuzde50:
                    if (currentQuestion.questionType != QuestionType.CoktanSecmeli)
                    {
                        result.success = false;
                        result.errorMessage = "Bu joker sadece çoktan seçmeli sorularda kullanılabilir";
                        return result;
                    }

                    // 2 yanlış şıkkı ele
                    var wrongOptions = new List<int>();
                    for (int i = 0; i < currentQuestion.options.Count; i++)
                    {
                        if (i != currentQuestion.correctAnswerIndex)
                            wrongOptions.Add(i);
                    }

                    // Rastgele 2 tanesini seç
                    result.eliminatedOptionIndices = new List<int>();
                    while (result.eliminatedOptionIndices.Count < 2 && wrongOptions.Count > 0)
                    {
                        int randomIndex = UnityEngine.Random.Range(0, wrongOptions.Count);
                        result.eliminatedOptionIndices.Add(wrongOptions[randomIndex]);
                        wrongOptions.RemoveAt(randomIndex);
                    }
                    break;

                case JokerType.OyuncularaSor:
                    if (currentQuestion.questionType != QuestionType.CoktanSecmeli)
                    {
                        result.success = false;
                        result.errorMessage = "Bu joker sadece çoktan seçmeli sorularda kullanılabilir";
                        return result;
                    }

                    // Simüle edilmiş oyuncu yüzdeleri
                    result.audiencePercentages = new Dictionary<int, float>();
                    float remainingPercent = 100f;
                    int correctIndex = currentQuestion.correctAnswerIndex;

                    // Doğru cevaba yüksek yüzde ver
                    float correctPercent = UnityEngine.Random.Range(40f, 70f);
                    result.audiencePercentages[correctIndex] = correctPercent;
                    remainingPercent -= correctPercent;

                    // Diğer seçeneklere dağıt
                    for (int i = 0; i < currentQuestion.options.Count; i++)
                    {
                        if (i == correctIndex) continue;

                        float percent = i == currentQuestion.options.Count - 1
                            ? remainingPercent
                            : UnityEngine.Random.Range(5f, remainingPercent / 2f);
                        result.audiencePercentages[i] = percent;
                        remainingPercent -= percent;
                    }
                    break;

                case JokerType.Papagan:
                    if (currentQuestion.questionType != QuestionType.Tahmin)
                    {
                        result.success = false;
                        result.errorMessage = "Bu joker sadece tahmin sorularında kullanılabilir";
                        return result;
                    }

                    // Doğru değere yakın bir tahmin ver
                    result.hintAccuracy = UnityEngine.Random.Range(0.85f, 0.98f);
                    float errorMargin = currentQuestion.correctValue * (1f - result.hintAccuracy);
                    result.parrotHint = currentQuestion.correctValue + UnityEngine.Random.Range(-errorMargin, errorMargin);
                    break;

                case JokerType.Teleskop:
                    if (currentQuestion.questionType != QuestionType.Tahmin)
                    {
                        result.success = false;
                        result.errorMessage = "Bu joker sadece tahmin sorularında kullanılabilir";
                        return result;
                    }

                    // 4 seçenek sun (biri doğru)
                    result.telescopeOptions = new List<TelescopeOption>();
                    float correct = currentQuestion.correctValue;
                    float range = correct * 0.3f; // %30 aralık

                    // Doğru değer
                    result.telescopeOptions.Add(new TelescopeOption { optionText = correct.ToString("N0"), isCorrect = true });

                    // 3 yanlış değer
                    result.telescopeOptions.Add(new TelescopeOption { optionText = (correct - range).ToString("N0"), isCorrect = false });
                    result.telescopeOptions.Add(new TelescopeOption { optionText = (correct + range).ToString("N0"), isCorrect = false });
                    result.telescopeOptions.Add(new TelescopeOption { optionText = (correct + range * 2f).ToString("N0"), isCorrect = false });

                    // Karıştır
                    for (int i = result.telescopeOptions.Count - 1; i > 0; i--)
                    {
                        int j = UnityEngine.Random.Range(0, i + 1);
                        var temp = result.telescopeOptions[i];
                        result.telescopeOptions[i] = result.telescopeOptions[j];
                        result.telescopeOptions[j] = temp;
                    }
                    break;
            }

            return result;
        }

        private void HandleJokerResult(JokerUseResult result)
        {
            // Sunucudan gelen joker sonucu
            Debug.Log($"[InGameFlowManager] Joker result received: {result.jokerType}, Success: {result.success}");
        }

        private void HandleTimeUp()
        {
            if (!isWaitingForAnswer) return;

            string playerId = ProfileManager.Instance?.CurrentProfile?.userId ??
                              PlayerManager.Instance?.LocalPlayerData?.playerId ?? "local";

            // Yerel oyuncu henüz cevap vermediyse
            if (!_playerAnswers.ContainsKey(playerId))
            {
                // Süre doldu - cevapsız kaydet
                var answerData = new PlayerAnswerData
                {
                    playerId = playerId,
                    questionId = currentQuestion?.questionId,
                    selectedAnswerIndex = -1, // Cevap vermedi
                    guessedValue = 0,
                    answerTime = currentQuestion?.timeLimit ?? 15f
                };

                _playerAnswers[playerId] = answerData;
                _answeredPlayerCount++;

                // Süre doldu - yanlış cevap olarak işle
                UpdateLocalPlayerScore(false, 0);

                if (currentQuestion?.questionType == QuestionType.Tahmin)
                {
                    // Tahmin sorusu için
                    GameUIManager.Instance?.ShowEstimationResult(
                        false,
                        0,
                        currentQuestion.correctValue,
                        0,
                        0,
                        currentQuestion.valueUnit,
                        questionAnswerDelay
                    );
                }
                else
                {
                    // Çoktan seçmeli için
                    string correctAnswer = currentQuestion?.options[currentQuestion.correctAnswerIndex] ?? "";
                    GameUIManager.Instance?.ShowAnswerResult(false, 0, correctAnswer, questionAnswerDelay);
                }

                Debug.Log("[InGameFlowManager] Süre doldu - yerel oyuncu cevap veremedi");
            }

            // Bot cevaplarını da zorla sonlandır
            BotAIController.Instance?.StopAllBotAnswers();

            // Tüm oyuncular için sonuçları işle
            StartCoroutine(ProcessQuestionResults());
        }

        private void HandleBotGameStarted(List<InGamePlayerData> bots)
        {
            Debug.Log($"[InGameFlowManager] Bot game started with {bots.Count} bots");
            StartBotGame(bots);
        }

        private void HandleGameFound(string gameId)
        {
            Debug.Log($"[InGameFlowManager] Game found: {gameId}");
            // Multiplayer oyun başlangıcı için bekle
        }

        private void HandleQuestionResult(QuestionResultData result)
        {
            // Sunucudan gelen soru sonucu
            Debug.Log($"[InGameFlowManager] Question result received for: {result.questionId}");
        }

        private void HandleGameEnded(List<GameEndPlayerResult> results)
        {
            Debug.Log("[InGameFlowManager] Game ended event received");
            isGameActive = false;
        }

        private void HandlePhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            Debug.Log($"[InGameFlowManager] Phase changed: {oldPhase} -> {newPhase}");

            if (newPhase == GamePhase.GameOver)
            {
                EndGame();
            }
        }

        private void HandlePlayAgain()
        {
            Debug.Log("[InGameFlowManager] Play again requested");
            // Aynı modda yeniden başlat
            GameModeManager.Instance?.StartSinglePlayerGame();
        }

        private void HandleMainMenu()
        {
            Debug.Log("[InGameFlowManager] Main menu requested");
            GameModeManager.Instance?.ReturnToMainMenu();
        }

        #endregion
    }
}