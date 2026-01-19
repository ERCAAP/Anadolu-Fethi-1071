using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Oyun İçi Akış Yöneticisi - Oyun akışını koordine eder
    /// GameUIManager, QuestionManager ve diğer sistemler arasında köprü görevi görür
    /// </summary>
    public class InGameFlowManager : Singleton<InGameFlowManager>
    {
        [Header("Oyun Ayarları")]
        [SerializeField] private float questionAnswerDelay = 2f;
        [SerializeField] private float betweenQuestionDelay = 1.5f;
        [SerializeField] private float gameStartCountdown = 3f;
        [SerializeField] private int questionsPerRound = 4;

        // Game State
        private GameStateData currentGameState;
        private QuestionData currentQuestion;
        private bool isGameActive = false;
        private int currentRound = 0;
        private int currentQuestionIndex = 0;
        private bool isWaitingForAnswer = false;

        // Events
        public event Action OnGameFlowStarted;
        public event Action OnGameFlowEnded;
        public event Action<int> OnRoundChanged;
        public event Action<int, int> OnQuestionIndexChanged; // currentIndex, total

        // Properties
        public bool IsGameActive => isGameActive;
        public int CurrentRound => currentRound;
        public int CurrentQuestionIndex => currentQuestionIndex;
        public GameStateData CurrentGameState => currentGameState;

        protected override void Awake()
        {
            base.Awake();
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
                GameUIManager.Instance.OnTimeUp += HandleTimeUp;
                GameUIManager.Instance.OnPlayAgainClicked += HandlePlayAgain;
                GameUIManager.Instance.OnMainMenuClicked += HandleMainMenu;
            }

            // Game Events
            GameEvents.OnBotGameStarted += HandleBotGameStarted;
            GameEvents.OnGameFound += HandleGameFound;
            GameEvents.OnQuestionResultReceived += HandleQuestionResult;
            GameEvents.OnGameEnded += HandleGameEnded;
            GameEvents.OnPhaseChanged += HandlePhaseChanged;
        }

        private void UnsubscribeFromEvents()
        {
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.OnOptionSelected -= HandleOptionSelected;
                GameUIManager.Instance.OnTimeUp -= HandleTimeUp;
                GameUIManager.Instance.OnPlayAgainClicked -= HandlePlayAgain;
                GameUIManager.Instance.OnMainMenuClicked -= HandleMainMenu;
            }

            GameEvents.OnBotGameStarted -= HandleBotGameStarted;
            GameEvents.OnGameFound -= HandleGameFound;
            GameEvents.OnQuestionResultReceived -= HandleQuestionResult;
            GameEvents.OnGameEnded -= HandleGameEnded;
            GameEvents.OnPhaseChanged -= HandlePhaseChanged;
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
        /// Soru cevabını gönder
        /// </summary>
        public void SubmitAnswer(int answerIndex)
        {
            if (!isWaitingForAnswer || currentQuestion == null)
            {
                Debug.LogWarning("[InGameFlowManager] Not waiting for answer or no current question");
                return;
            }

            isWaitingForAnswer = false;

            var answerData = new PlayerAnswerData
            {
                playerId = PlayerManager.Instance?.LocalPlayerData?.playerId ?? "local",
                questionId = currentQuestion.questionId,
                selectedAnswerIndex = answerIndex,
                answerTime = Time.time
            };

            // Cevabı değerlendir
            bool isCorrect = answerIndex == currentQuestion.correctAnswerIndex;
            int points = isCorrect ? CalculatePoints() : 0;

            // Yerel oyuncu skorunu güncelle
            UpdateLocalPlayerScore(isCorrect, points);

            // Sonucu göster
            string correctAnswer = currentQuestion.options[currentQuestion.correctAnswerIndex];
            GameUIManager.Instance?.ShowAnswerResult(isCorrect, points, correctAnswer, questionAnswerDelay);

            // Event tetikle
            GameEvents.TriggerPlayerAnswered(answerData.playerId, answerIndex);

            // Bir sonraki soruya geç
            StartCoroutine(NextQuestionSequence());
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

            // Demo soru oluştur (gerçek implementasyonda sunucudan gelecek)
            var question = CreateDemoQuestion();

            currentQuestion = question;
            currentQuestionIndex++;

            Debug.Log($"[InGameFlowManager] Showing question {currentQuestionIndex}/{questionsPerRound}");

            // Soruyu UI'da göster
            GameUIManager.Instance?.ShowQuestion(question, currentQuestionIndex, questionsPerRound);
            GameEvents.TriggerQuestionReceived(question);

            isWaitingForAnswer = true;
            OnQuestionIndexChanged?.Invoke(currentQuestionIndex, questionsPerRound);

            // Zamanlayıcı başlat
            GameEvents.TriggerQuestionTimerStarted(question.timeLimit);
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
                OnRoundChanged?.Invoke(currentRound);

                // Oyun sonu kontrolü (4 tur)
                if (currentRound > 4)
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

            Debug.Log("[InGameFlowManager] Game ended");

            // Sonuçları hazırla
            var results = PrepareGameResults();

            // Event tetikle
            GameEvents.TriggerGameEnded(results);

            OnGameFlowEnded?.Invoke();
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
                questionText = "Türkiye'nin başkenti neresidir?",
                timeLimit = 15f,
                difficultyLevel = 3,
                options = new List<string> { "İstanbul", "Ankara", "İzmir", "Bursa" },
                correctAnswerIndex = 1
            },
            new QuestionData
            {
                questionId = "q2",
                questionType = QuestionType.CoktanSecmeli,
                category = QuestionCategory.Tarih,
                questionText = "Malazgirt Meydan Muharebesi hangi yılda gerçekleşmiştir?",
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
                questionText = "Suyun kimyasal formülü nedir?",
                timeLimit = 15f,
                difficultyLevel = 2,
                options = new List<string> { "CO2", "H2O", "NaCl", "O2" },
                correctAnswerIndex = 1
            },
            new QuestionData
            {
                questionId = "q4",
                questionType = QuestionType.CoktanSecmeli,
                category = QuestionCategory.Cografya,
                questionText = "Dünyanın en büyük okyanusu hangisidir?",
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
                questionText = "FIFA Dünya Kupası kaç yılda bir düzenlenir?",
                timeLimit = 15f,
                difficultyLevel = 2,
                options = new List<string> { "2 yıl", "3 yıl", "4 yıl", "5 yıl" },
                correctAnswerIndex = 2
            },
            new QuestionData
            {
                questionId = "q6",
                questionType = QuestionType.CoktanSecmeli,
                category = QuestionCategory.Sanat,
                questionText = "Mona Lisa tablosunun ressamı kimdir?",
                timeLimit = 15f,
                difficultyLevel = 4,
                options = new List<string> { "Michelangelo", "Leonardo da Vinci", "Raphael", "Van Gogh" },
                correctAnswerIndex = 1
            },
            new QuestionData
            {
                questionId = "q7",
                questionType = QuestionType.CoktanSecmeli,
                category = QuestionCategory.Teknoloji,
                questionText = "İlk iPhone hangi yılda piyasaya sürüldü?",
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
                questionText = "İstanbul'un fethi hangi tarihte gerçekleşmiştir?",
                timeLimit = 15f,
                difficultyLevel = 4,
                options = new List<string> { "29 Mayıs 1453", "6 Nisan 1453", "30 Ağustos 1071", "23 Nisan 1920" },
                correctAnswerIndex = 0
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

        private void HandleTimeUp()
        {
            if (!isWaitingForAnswer) return;

            isWaitingForAnswer = false;

            // Süre doldu - yanlış cevap olarak işle
            UpdateLocalPlayerScore(false, 0);

            string correctAnswer = currentQuestion?.options[currentQuestion.correctAnswerIndex] ?? "";
            GameUIManager.Instance?.ShowAnswerResult(false, 0, correctAnswer, questionAnswerDelay);

            StartCoroutine(NextQuestionSequence());
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
