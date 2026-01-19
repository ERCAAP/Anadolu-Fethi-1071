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
    /// Bot AI Kontrol - Bot oyuncuların cevaplarını simüle eder
    /// </summary>
    public class BotAIController : Singleton<BotAIController>
    {
        [Header("Bot Ayarları")]
        [SerializeField] private float minAnswerTime = 2f;
        [SerializeField] private float maxAnswerTime = 10f;
        [SerializeField] private float baseCorrectChance = 0.6f; // %60 doğru cevap şansı

        [Header("Zorluk Ayarları")]
        [SerializeField] private float easyBotCorrectChance = 0.4f;
        [SerializeField] private float normalBotCorrectChance = 0.6f;
        [SerializeField] private float hardBotCorrectChance = 0.8f;

        // Active bots
        private List<InGamePlayerData> _activeBots;
        private Dictionary<string, Coroutine> _botAnswerCoroutines;
        private QuestionData _currentQuestion;
        private bool _isQuestionActive;

        protected override void OnSingletonAwake()
        {
            _activeBots = new List<InGamePlayerData>();
            _botAnswerCoroutines = new Dictionary<string, Coroutine>();
        }

        private void OnEnable()
        {
            GameEvents.OnBotGameStarted += HandleBotGameStarted;
            GameEvents.OnQuestionReceived += HandleQuestionReceived;
            GameEvents.OnQuestionResultReceived += HandleQuestionResult;
        }

        private void OnDisable()
        {
            GameEvents.OnBotGameStarted -= HandleBotGameStarted;
            GameEvents.OnQuestionReceived -= HandleQuestionReceived;
            GameEvents.OnQuestionResultReceived -= HandleQuestionResult;
        }

        #region Public Methods

        /// <summary>
        /// Botları ayarla
        /// </summary>
        public void SetBots(List<InGamePlayerData> bots)
        {
            _activeBots = bots.FindAll(b => !b.isLocalPlayer);
            Debug.Log($"[BotAIController] {_activeBots.Count} bot ayarlandı");
        }

        /// <summary>
        /// Tüm bot cevaplarını durdur
        /// </summary>
        public void StopAllBotAnswers()
        {
            foreach (var coroutine in _botAnswerCoroutines.Values)
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
            }
            _botAnswerCoroutines.Clear();
            _isQuestionActive = false;
        }

        /// <summary>
        /// Bot cevap sonuçlarını al (soru sonunda)
        /// </summary>
        public List<BotAnswerResult> GetBotResults()
        {
            var results = new List<BotAnswerResult>();

            foreach (var bot in _activeBots)
            {
                if (bot.isEliminated) continue;

                var result = new BotAnswerResult
                {
                    playerId = bot.playerId,
                    displayName = bot.displayName,
                    answerTime = UnityEngine.Random.Range(minAnswerTime, maxAnswerTime)
                };

                if (_currentQuestion != null)
                {
                    // Doğru cevap şansını hesapla
                    float correctChance = CalculateCorrectChance(bot, _currentQuestion);
                    result.isCorrect = UnityEngine.Random.value < correctChance;

                    if (_currentQuestion.questionType == QuestionType.CoktanSecmeli)
                    {
                        result.selectedAnswerIndex = result.isCorrect
                            ? _currentQuestion.correctAnswerIndex
                            : GetRandomWrongAnswer(_currentQuestion.correctAnswerIndex, _currentQuestion.options.Count);
                    }
                    else
                    {
                        // Tahmin sorusu
                        result.guessedValue = CalculateBotGuess(_currentQuestion, result.isCorrect);
                    }

                    // Puan hesapla
                    result.earnedPoints = result.isCorrect ? CalculatePoints(_currentQuestion) : 0;
                }

                results.Add(result);
            }

            return results;
        }

        #endregion

        #region Private Methods

        private void HandleBotGameStarted(List<InGamePlayerData> bots)
        {
            SetBots(bots);
        }

        private void HandleQuestionReceived(QuestionData question)
        {
            _currentQuestion = question;
            _isQuestionActive = true;

            // Her bot için cevap zamanlaması başlat
            foreach (var bot in _activeBots)
            {
                if (bot.isEliminated) continue;

                var coroutine = StartCoroutine(BotAnswerCoroutine(bot, question));
                _botAnswerCoroutines[bot.playerId] = coroutine;
            }
        }

        private void HandleQuestionResult(QuestionResultData result)
        {
            _isQuestionActive = false;
            StopAllBotAnswers();
        }

        private IEnumerator BotAnswerCoroutine(InGamePlayerData bot, QuestionData question)
        {
            // Rastgele bekleme süresi
            float waitTime = UnityEngine.Random.Range(minAnswerTime, Mathf.Min(maxAnswerTime, question.timeLimit - 1f));
            yield return new WaitForSeconds(waitTime);

            if (!_isQuestionActive) yield break;

            // Doğru cevap şansını hesapla
            float correctChance = CalculateCorrectChance(bot, question);
            bool isCorrect = UnityEngine.Random.value < correctChance;

            int answerIndex = -1;
            float guessedValue = 0;

            if (question.questionType == QuestionType.CoktanSecmeli)
            {
                answerIndex = isCorrect
                    ? question.correctAnswerIndex
                    : GetRandomWrongAnswer(question.correctAnswerIndex, question.options.Count);
            }
            else
            {
                guessedValue = CalculateBotGuess(question, isCorrect);
            }

            // Puan hesapla
            int points = isCorrect ? CalculatePoints(question) : 0;

            // Bot skorunu güncelle
            int oldScore = bot.currentScore;
            if (isCorrect)
            {
                bot.currentScore += points;
                bot.correctAnswers++;
            }
            else
            {
                bot.wrongAnswers++;
            }

            // Event tetikle
            GameEvents.TriggerPlayerAnswered(bot.playerId, answerIndex);
            GameEvents.TriggerScoreChanged(bot.playerId, oldScore, bot.currentScore);

            // Bot cevap event'i
            GameEvents.TriggerBotAnswerSubmitted(bot.playerId, answerIndex, guessedValue, isCorrect, points);

            Debug.Log($"[BotAI] {bot.displayName} cevapladı - " +
                $"Doğru: {isCorrect}, Puan: {points}, Süre: {waitTime:F1}s");
        }

        private float CalculateCorrectChance(InGamePlayerData bot, QuestionData question)
        {
            // Temel şans
            float chance = normalBotCorrectChance;

            // Zorluk seviyesine göre düşür
            chance -= (question.difficultyLevel - 5) * 0.05f;

            // Bot performansına göre ayarla (momentum)
            if (bot.correctAnswers > bot.wrongAnswers)
            {
                chance += 0.05f;
            }
            else if (bot.wrongAnswers > bot.correctAnswers * 2)
            {
                chance -= 0.1f;
            }

            return Mathf.Clamp(chance, 0.2f, 0.9f);
        }

        private int GetRandomWrongAnswer(int correctIndex, int optionCount)
        {
            int wrongIndex;
            do
            {
                wrongIndex = UnityEngine.Random.Range(0, optionCount);
            } while (wrongIndex == correctIndex);

            return wrongIndex;
        }

        private float CalculateBotGuess(QuestionData question, bool shouldBeCorrect)
        {
            float correctValue = question.correctValue;
            float tolerance = question.tolerance;

            if (shouldBeCorrect)
            {
                // Tolerans içinde bir değer
                return correctValue + UnityEngine.Random.Range(-tolerance, tolerance);
            }
            else
            {
                // Tolerans dışında bir değer
                float error = tolerance + UnityEngine.Random.Range(tolerance * 0.5f, tolerance * 3f);
                return UnityEngine.Random.value > 0.5f ? correctValue + error : correctValue - error;
            }
        }

        private int CalculatePoints(QuestionData question)
        {
            return question.difficultyLevel switch
            {
                <= 3 => 100,
                <= 5 => 150,
                <= 7 => 200,
                <= 9 => 250,
                _ => 300
            };
        }

        #endregion
    }

    /// <summary>
    /// Bot cevap sonucu
    /// </summary>
    [Serializable]
    public class BotAnswerResult
    {
        public string playerId;
        public string displayName;
        public bool isCorrect;
        public int selectedAnswerIndex;
        public float guessedValue;
        public float answerTime;
        public int earnedPoints;
    }
}
