using System;
using System.Collections;
using System.Collections.Generic;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;
using UnityEngine;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Tur yöneticisi - sıra ve tur geçişlerini yönetir
    /// </summary>
    public class TurnManager : Singleton<TurnManager>
    {
        [Header("Turn Configuration")]
        [SerializeField] private float turnTransitionDelay = 1f;

        // Turn order tracking
        private List<string> _turnOrder;
        private int _currentTurnIndex;
        private int _currentRound;
        private int _totalRounds = 4;
        private int _questionsPerRound = 4;
        private int _currentQuestionInRound;

        // Turn state
        private bool _isTurnInProgress;
        private string _currentPlayerId;
        private Coroutine _turnTransitionCoroutine;

        // Properties
        public int CurrentRound => _currentRound;
        public int CurrentQuestionInRound => _currentQuestionInRound;
        public string CurrentPlayerId => _currentPlayerId;
        public bool IsTurnInProgress => _isTurnInProgress;
        public bool IsLocalPlayerTurn => _currentPlayerId == PlayerManager.Instance?.LocalPlayerId;

        protected override void OnSingletonAwake()
        {
            _turnOrder = new List<string>();
        }

        private void OnEnable()
        {
            GameEvents.OnGameStarting += HandleGameStarting;
            GameEvents.OnPhaseChanged += HandlePhaseChanged;
            GameEvents.OnQuestionResultReceived += HandleQuestionResult;
            GameEvents.OnAttackResolved += HandleAttackResolved;
            GameEvents.OnPlayerEliminated += HandlePlayerEliminated;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStarting -= HandleGameStarting;
            GameEvents.OnPhaseChanged -= HandlePhaseChanged;
            GameEvents.OnQuestionResultReceived -= HandleQuestionResult;
            GameEvents.OnAttackResolved -= HandleAttackResolved;
            GameEvents.OnPlayerEliminated -= HandlePlayerEliminated;
        }

        #region Initialization

        private void HandleGameStarting(GameStartData data)
        {
            _turnOrder.Clear();
            _currentRound = 0;
            _currentTurnIndex = 0;
            _currentQuestionInRound = 0;
            _isTurnInProgress = false;

            // Başlangıç sırasını oluştur
            foreach (var player in data.players)
            {
                _turnOrder.Add(player.playerId);
            }
        }

        private void HandlePhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            if (newPhase == GamePhase.Savas)
            {
                StartSavasPhase();
            }
        }

        #endregion

        #region Savas Phase Turn Order

        /// <summary>
        /// Savaş aşamasını başlat
        /// </summary>
        private void StartSavasPhase()
        {
            _currentRound = 1;
            _currentQuestionInRound = 0;
            
            SetTurnOrderForRound(_currentRound);
            _currentTurnIndex = 0;
            
            StartTurn(_turnOrder[_currentTurnIndex]);
            GameEvents.TriggerRoundStarted(_currentRound);
        }

        /// <summary>
        /// Belirli tur için sıralamayı ayarla
        /// </summary>
        private void SetTurnOrderForRound(int round)
        {
            var gameState = GameManager.Instance?.GameState;
            if (gameState == null) return;

            var activePlayers = gameState.players.FindAll(p => !p.isEliminated);

            if (round <= 3)
            {
                // Tur 1-3: Sabit sıralama
                var colorOrders = new PlayerColor[][]
                {
                    new[] { PlayerColor.Kirmizi, PlayerColor.Yesil, PlayerColor.Mavi },  // Tur 1
                    new[] { PlayerColor.Yesil, PlayerColor.Mavi, PlayerColor.Kirmizi },  // Tur 2
                    new[] { PlayerColor.Mavi, PlayerColor.Kirmizi, PlayerColor.Yesil }   // Tur 3
                };

                var order = colorOrders[round - 1];
                _turnOrder.Clear();

                foreach (var color in order)
                {
                    var player = activePlayers.Find(p => p.color == color);
                    if (player != null)
                    {
                        _turnOrder.Add(player.playerId);
                    }
                }
            }
            else
            {
                // Tur 4: Puana göre (en yüksek ilk)
                activePlayers.Sort((a, b) => b.currentScore.CompareTo(a.currentScore));
                
                _turnOrder.Clear();
                foreach (var player in activePlayers)
                {
                    _turnOrder.Add(player.playerId);
                }
            }
        }

        #endregion

        #region Turn Management

        /// <summary>
        /// Turu başlat
        /// </summary>
        public void StartTurn(string playerId)
        {
            _currentPlayerId = playerId;
            _isTurnInProgress = true;

            GameEvents.TriggerTurnChanged(playerId);

            // Saldırı seçim modu başlat
            GameEvents.TriggerAttackSelectionStarted(playerId);
        }

        /// <summary>
        /// Mevcut turu bitir ve sonrakine geç
        /// </summary>
        public void EndCurrentTurn()
        {
            _isTurnInProgress = false;

            if (_turnTransitionCoroutine != null)
            {
                StopCoroutine(_turnTransitionCoroutine);
            }

            _turnTransitionCoroutine = StartCoroutine(TurnTransitionCoroutine());
        }

        /// <summary>
        /// Tur geçiş rutini
        /// </summary>
        private IEnumerator TurnTransitionCoroutine()
        {
            yield return new WaitForSeconds(turnTransitionDelay);

            _currentQuestionInRound++;

            // Bu turdaki tüm sorular bitti mi?
            if (_currentQuestionInRound >= _questionsPerRound)
            {
                EndCurrentRound();
                yield break;
            }

            // Sonraki oyuncuya geç
            AdvanceToNextPlayer();
        }

        /// <summary>
        /// Sonraki oyuncuya geç
        /// </summary>
        private void AdvanceToNextPlayer()
        {
            int startIndex = _currentTurnIndex;

            do
            {
                _currentTurnIndex = (_currentTurnIndex + 1) % _turnOrder.Count;

                var player = GameManager.Instance?.GameState?.GetPlayer(_turnOrder[_currentTurnIndex]);
                if (player != null && !player.isEliminated)
                {
                    StartTurn(_turnOrder[_currentTurnIndex]);
                    return;
                }
            }
            while (_currentTurnIndex != startIndex);

            // Tüm oyuncular elendi
            GameEvents.TriggerPhaseChanged(GamePhase.Savas, GamePhase.GameOver);
        }

        /// <summary>
        /// Mevcut turu bitir
        /// </summary>
        private void EndCurrentRound()
        {
            GameEvents.TriggerRoundEnded(_currentRound);

            _currentRound++;

            // Tüm turlar bitti mi?
            if (_currentRound > _totalRounds)
            {
                GameEvents.TriggerPhaseChanged(GamePhase.Savas, GamePhase.GameOver);
                return;
            }

            // Sonraki turu başlat
            _currentQuestionInRound = 0;
            SetTurnOrderForRound(_currentRound);
            _currentTurnIndex = 0;

            GameEvents.TriggerRoundStarted(_currentRound);
            StartTurn(_turnOrder[_currentTurnIndex]);
        }

        #endregion

        #region Event Handlers

        private void HandleQuestionResult(QuestionResultData result)
        {
            // Fetih aşamasında tur yönetimi GameManager'da
            if (GameManager.Instance?.CurrentPhase != GamePhase.Savas) return;
        }

        private void HandleAttackResolved(AttackResultData result)
        {
            if (GameManager.Instance?.CurrentPhase != GamePhase.Savas) return;
            
            EndCurrentTurn();
        }

        private void HandlePlayerEliminated(string playerId, int round)
        {
            // Elenen oyuncuyu sıradan çıkar
            _turnOrder.Remove(playerId);

            // Sadece 1 oyuncu kaldıysa oyun biter
            var activePlayers = GameManager.Instance?.GameState?.GetActivePlayerCount() ?? 0;
            if (activePlayers <= 1)
            {
                GameEvents.TriggerPhaseChanged(GamePhase.Savas, GamePhase.GameOver);
            }
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Sıradaki oyuncuyu al
        /// </summary>
        public string GetNextPlayer()
        {
            int nextIndex = (_currentTurnIndex + 1) % _turnOrder.Count;
            
            for (int i = 0; i < _turnOrder.Count; i++)
            {
                var playerId = _turnOrder[nextIndex];
                var player = GameManager.Instance?.GameState?.GetPlayer(playerId);
                
                if (player != null && !player.isEliminated)
                {
                    return playerId;
                }

                nextIndex = (nextIndex + 1) % _turnOrder.Count;
            }

            return null;
        }

        /// <summary>
        /// Oyuncunun bu turda sırası geldi mi?
        /// </summary>
        public bool HasPlayerPlayedThisRound(string playerId)
        {
            int playerIndex = _turnOrder.IndexOf(playerId);
            return playerIndex >= 0 && playerIndex < _currentTurnIndex;
        }

        /// <summary>
        /// Kalan tur sayısı
        /// </summary>
        public int GetRemainingRounds()
        {
            return _totalRounds - _currentRound;
        }

        /// <summary>
        /// Kalan soru sayısı (bu turda)
        /// </summary>
        public int GetRemainingQuestionsInRound()
        {
            return _questionsPerRound - _currentQuestionInRound;
        }

        /// <summary>
        /// Tur sırasını al
        /// </summary>
        public List<string> GetTurnOrder()
        {
            return new List<string>(_turnOrder);
        }

        /// <summary>
        /// Oyuncunun tur sırasındaki pozisyonu
        /// </summary>
        public int GetPlayerTurnPosition(string playerId)
        {
            return _turnOrder.IndexOf(playerId);
        }

        #endregion

        #region Pause/Resume

        /// <summary>
        /// Tur sistemini duraklat
        /// </summary>
        public void PauseTurns()
        {
            if (_turnTransitionCoroutine != null)
            {
                StopCoroutine(_turnTransitionCoroutine);
            }
            _isTurnInProgress = false;
        }

        /// <summary>
        /// Tur sistemini devam ettir
        /// </summary>
        public void ResumeTurns()
        {
            if (!_isTurnInProgress && !string.IsNullOrEmpty(_currentPlayerId))
            {
                StartTurn(_currentPlayerId);
            }
        }

        #endregion
    }
}
