using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Network;
using BilVeFethet.Utils;
using UnityEngine;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Soru yöneticisi - soru akışını, zamanlayıcıyı ve cevap işlemlerini yönetir
    /// </summary>
    public class QuestionManager : Singleton<QuestionManager>
    {
        [Header("Timer Configuration")]
        [SerializeField] private float defaultQuestionTime = 15f;
        [SerializeField] private float estimationQuestionTime = 20f;
        [SerializeField] private float warningTime = 5f;

        // Current question state
        private QuestionData _currentQuestion;
        private float _timeRemaining;
        private float _questionStartTime;
        private bool _isTimerRunning;
        private bool _hasAnswered;
        private Coroutine _timerCoroutine;

        // Answer tracking
        private PlayerAnswerData _currentAnswer;
        private List<JokerType> _usedJokersThisQuestion;

        // Cache for joker results
        private JokerUseResult _lastJokerResult;

        // Properties
        public QuestionData CurrentQuestion => _currentQuestion;
        public float TimeRemaining => _timeRemaining;
        public bool IsTimerRunning => _isTimerRunning;
        public bool HasAnswered => _hasAnswered;
        public List<int> EliminatedOptions => _currentQuestion?.eliminatedOptions;

        protected override void OnSingletonAwake()
        {
            _usedJokersThisQuestion = new List<JokerType>();
        }

        private void OnEnable()
        {
            GameEvents.OnQuestionReceived += HandleQuestionReceived;
            GameEvents.OnQuestionResultReceived += HandleQuestionResult;
            GameEvents.OnJokerResultReceived += HandleJokerResult;
        }

        private void OnDisable()
        {
            GameEvents.OnQuestionReceived -= HandleQuestionReceived;
            GameEvents.OnQuestionResultReceived -= HandleQuestionResult;
            GameEvents.OnJokerResultReceived -= HandleJokerResult;
        }

        #region Question Handling

        /// <summary>
        /// Yeni soru geldiğinde
        /// </summary>
        private void HandleQuestionReceived(QuestionData question)
        {
            _currentQuestion = question;
            _hasAnswered = false;
            _usedJokersThisQuestion.Clear();
            _lastJokerResult = null;

            // Answer data hazırla
            _currentAnswer = new PlayerAnswerData
            {
                playerId = PlayerManager.Instance?.LocalPlayerId,
                questionId = question.questionId,
                usedJokers = new List<JokerType>()
            };

            // Zamanlayıcıyı başlat
            float timeLimit = question.questionType == QuestionType.Tahmin 
                ? estimationQuestionTime 
                : (question.timeLimit > 0 ? question.timeLimit : defaultQuestionTime);

            StartTimer(timeLimit);
        }

        /// <summary>
        /// Soru sonucu geldiğinde
        /// </summary>
        private void HandleQuestionResult(QuestionResultData result)
        {
            StopTimer();
            _currentQuestion = null;
        }

        #endregion

        #region Timer Management

        /// <summary>
        /// Zamanlayıcıyı başlat
        /// </summary>
        private void StartTimer(float duration)
        {
            StopTimer();
            _timerCoroutine = StartCoroutine(TimerCoroutine(duration));
        }

        /// <summary>
        /// Zamanlayıcıyı durdur
        /// </summary>
        private void StopTimer()
        {
            if (_timerCoroutine != null)
            {
                StopCoroutine(_timerCoroutine);
                _timerCoroutine = null;
            }
            _isTimerRunning = false;
        }

        /// <summary>
        /// Zamanlayıcı coroutine
        /// </summary>
        private IEnumerator TimerCoroutine(float duration)
        {
            _timeRemaining = duration;
            _questionStartTime = Time.time;
            _isTimerRunning = true;

            GameEvents.TriggerQuestionTimerStarted(duration);

            while (_timeRemaining > 0 && !_hasAnswered)
            {
                yield return null;
                _timeRemaining -= Time.deltaTime;

                // Her 0.5 saniyede bir güncelleme
                if (Mathf.FloorToInt(_timeRemaining * 2) != Mathf.FloorToInt((_timeRemaining + Time.deltaTime) * 2))
                {
                    GameEvents.TriggerQuestionTimerUpdated(_timeRemaining);
                }
            }

            _isTimerRunning = false;

            if (!_hasAnswered)
            {
                GameEvents.TriggerQuestionTimerExpired();
                
                // Süre doldu, boş cevap gönder
                SubmitAnswer(-1, 0);
            }
        }

        #endregion

        #region Answer Submission

        /// <summary>
        /// Çoktan seçmeli cevap gönder
        /// </summary>
        public void SubmitMultipleChoiceAnswer(int selectedIndex)
        {
            if (_hasAnswered || _currentQuestion == null) return;
            if (_currentQuestion.questionType != QuestionType.CoktanSecmeli) return;

            SubmitAnswer(selectedIndex, 0);
        }

        /// <summary>
        /// Tahmin cevabı gönder
        /// </summary>
        public void SubmitEstimationAnswer(float guessedValue)
        {
            if (_hasAnswered || _currentQuestion == null) return;
            if (_currentQuestion.questionType != QuestionType.Tahmin) return;

            SubmitAnswer(-1, guessedValue);
        }

        /// <summary>
        /// Cevap gönder
        /// </summary>
        private async void SubmitAnswer(int selectedIndex, float guessedValue)
        {
            _hasAnswered = true;
            float answerTime = Time.time - _questionStartTime;

            _currentAnswer.selectedAnswerIndex = selectedIndex;
            _currentAnswer.guessedValue = guessedValue;
            _currentAnswer.answerTime = answerTime;
            _currentAnswer.usedJokers = new List<JokerType>(_usedJokersThisQuestion);

            StopTimer();

            // Diğer oyunculara bildir
            GameEvents.TriggerPlayerAnswered(_currentAnswer.playerId, selectedIndex);

            // Sunucuya gönder
            await NetworkManager.Instance.SubmitAnswerAsync(_currentAnswer);
        }

        #endregion

        #region Joker Usage

        /// <summary>
        /// Joker kullan
        /// </summary>
        public async Task<bool> UseJokerAsync(JokerType jokerType)
        {
            if (_currentQuestion == null || _hasAnswered) return false;

            // Bu soruda zaten kullanıldı mı?
            if (_usedJokersThisQuestion.Contains(jokerType)) return false;

            // Joker kullanılabilir mi?
            if (!PlayerManager.Instance.CanUseJoker(jokerType)) return false;

            // Soru tipine uygun mu?
            if (!IsJokerValidForQuestion(jokerType, _currentQuestion.questionType)) return false;

            // Jokeri kullan
            if (!PlayerManager.Instance.UseJoker(jokerType)) return false;

            _usedJokersThisQuestion.Add(jokerType);
            GameEvents.TriggerJokerUsed(_currentAnswer.playerId, jokerType);

            // Sunucudan joker sonucunu al
            var result = await NetworkManager.Instance.UseJokerAsync(jokerType, _currentQuestion.questionId);
            
            return result != null && result.success;
        }

        /// <summary>
        /// Joker soru tipine uygun mu?
        /// </summary>
        private bool IsJokerValidForQuestion(JokerType jokerType, QuestionType questionType)
        {
            return jokerType switch
            {
                // Sadece çoktan seçmeli için
                JokerType.Yuzde50 => questionType == QuestionType.CoktanSecmeli,
                JokerType.OyuncularaSor => questionType == QuestionType.CoktanSecmeli,
                JokerType.Teleskop => questionType == QuestionType.CoktanSecmeli,
                
                // Sadece tahmin için
                JokerType.Papagan => questionType == QuestionType.Tahmin,
                
                // Her iki tip için (saldırı/strateji jokerleri)
                JokerType.SihirliKanatlar => true,
                JokerType.EkstraKoruma => true,
                JokerType.KategoriSecme => true,
                
                _ => false
            };
        }

        /// <summary>
        /// Joker sonucunu işle
        /// </summary>
        private void HandleJokerResult(JokerUseResult result)
        {
            _lastJokerResult = result;

            if (!result.success)
            {
                Debug.LogWarning($"[QuestionManager] Joker failed: {result.errorMessage}");
                return;
            }

            switch (result.jokerType)
            {
                case JokerType.Yuzde50:
                    // Elenen seçenekleri kaydet
                    if (_currentQuestion != null && result.eliminatedOptionIndices != null)
                    {
                        _currentQuestion.eliminatedOptions = result.eliminatedOptionIndices;
                    }
                    break;

                case JokerType.OyuncularaSor:
                    // Oyuncu yüzdelerini kaydet
                    if (_currentQuestion != null && result.audiencePercentages != null)
                    {
                        _currentQuestion.audiencePercentages = result.audiencePercentages;
                    }
                    break;

                case JokerType.Papagan:
                    // Papağan ipucunu kaydet
                    if (_currentQuestion != null)
                    {
                        _currentQuestion.parrotHint = result.parrotHint;
                    }
                    break;
            }
        }

        #endregion

        #region Question Helpers

        /// <summary>
        /// Seçenek kullanılabilir mi (elenmemiş mi)?
        /// </summary>
        public bool IsOptionAvailable(int optionIndex)
        {
            if (_currentQuestion?.eliminatedOptions == null) return true;
            return !_currentQuestion.eliminatedOptions.Contains(optionIndex);
        }

        /// <summary>
        /// Oyuncu yüzdesi al
        /// </summary>
        public float GetAudiencePercentage(int optionIndex)
        {
            if (_currentQuestion?.audiencePercentages == null) return 0;
            return _currentQuestion.audiencePercentages.TryGetValue(optionIndex, out var percentage) 
                ? percentage : 0;
        }

        /// <summary>
        /// Papağan ipucunu al
        /// </summary>
        public float GetParrotHint()
        {
            return _currentQuestion?.parrotHint ?? 0;
        }

        /// <summary>
        /// Kalan kullanılabilir jokerler
        /// </summary>
        public List<JokerType> GetAvailableJokers()
        {
            var available = new List<JokerType>();
            
            foreach (JokerType jokerType in Enum.GetValues(typeof(JokerType)))
            {
                if (CanUseJokerNow(jokerType))
                {
                    available.Add(jokerType);
                }
            }

            return available;
        }

        /// <summary>
        /// Şu an joker kullanılabilir mi?
        /// </summary>
        public bool CanUseJokerNow(JokerType jokerType)
        {
            if (_currentQuestion == null || _hasAnswered) return false;
            if (_usedJokersThisQuestion.Contains(jokerType)) return false;
            if (!PlayerManager.Instance.CanUseJoker(jokerType)) return false;
            if (!IsJokerValidForQuestion(jokerType, _currentQuestion.questionType)) return false;
            return true;
        }

        /// <summary>
        /// Cevap süresi yüzdesini al
        /// </summary>
        public float GetAnswerTimePercentage()
        {
            if (_currentQuestion == null) return 0;
            
            float maxTime = _currentQuestion.timeLimit > 0 
                ? _currentQuestion.timeLimit 
                : defaultQuestionTime;
                
            return 1 - (_timeRemaining / maxTime);
        }

        #endregion

        #region Question Request

        /// <summary>
        /// Soru iste (belirli kategori ile)
        /// </summary>
        public async Task<QuestionData> RequestQuestionWithCategoryAsync(QuestionCategory category)
        {
            var request = new QuestionRequestData
            {
                gameId = NetworkManager.Instance.CurrentGameId,
                playerId = PlayerManager.Instance?.LocalPlayerId,
                currentPhase = GameManager.Instance.CurrentPhase,
                roundNumber = GameManager.Instance.CurrentRound,
                preferredCategory = category
            };

            return await NetworkManager.Instance.RequestQuestionAsync(request);
        }

        #endregion
    }
}
