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
    /// Oyun State Yöneticisi - Tüm oyun durumunu merkezi olarak yönetir
    /// State Machine pattern ile oyun akışını kontrol eder
    /// </summary>
    public class GameStateManager : Singleton<GameStateManager>
    {
        [Header("Oyun Ayarları")]
        [SerializeField] private GameConfig gameConfig;

        // Current State
        private GameStateData _currentState;
        private GamePhase _currentPhase = GamePhase.None;
        private FetihState _currentFetihState = FetihState.WaitingQuestion;
        private SavasState _currentSavasState = SavasState.SelectingTarget;

        // Events
        public event Action<GameStateData> OnStateChanged;
        public event Action<GamePhase> OnPhaseEntered;
        public event Action<GamePhase> OnPhaseExited;

        // Properties
        public GameStateData CurrentState => _currentState;
        public GamePhase CurrentPhase => _currentPhase;
        public FetihState CurrentFetihState => _currentFetihState;
        public SavasState CurrentSavasState => _currentSavasState;
        public bool IsGameActive => _currentPhase != GamePhase.None && _currentPhase != GamePhase.GameOver;
        public GameConfig Config => gameConfig ?? new GameConfig();

        protected override void OnSingletonAwake()
        {
            if (gameConfig == null)
                gameConfig = new GameConfig();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnGameStarting += HandleGameStarting;
            GameEvents.OnQuestionResultReceived += HandleQuestionResult;
            GameEvents.OnPlayerEliminated += HandlePlayerEliminated;
            GameEvents.OnAttackResolved += HandleAttackResolved;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnGameStarting -= HandleGameStarting;
            GameEvents.OnQuestionResultReceived -= HandleQuestionResult;
            GameEvents.OnPlayerEliminated -= HandlePlayerEliminated;
            GameEvents.OnAttackResolved -= HandleAttackResolved;
        }

        #region Public Methods

        /// <summary>
        /// Yeni oyun başlat
        /// </summary>
        public void InitializeGame(GameStateData state)
        {
            _currentState = state;
            _currentPhase = GamePhase.None;

            Debug.Log($"[GameStateManager] Oyun başlatıldı - ID: {state.gameId}, Oyuncu sayısı: {state.players.Count}");

            OnStateChanged?.Invoke(_currentState);
        }

        /// <summary>
        /// Oyun fazını değiştir
        /// </summary>
        public void ChangePhase(GamePhase newPhase)
        {
            if (_currentPhase == newPhase) return;

            GamePhase oldPhase = _currentPhase;

            // Eski fazdan çık
            OnPhaseExited?.Invoke(oldPhase);
            ExitPhase(oldPhase);

            // Yeni faza gir
            _currentPhase = newPhase;
            _currentState.currentPhase = newPhase;

            EnterPhase(newPhase);
            OnPhaseEntered?.Invoke(newPhase);

            // Event tetikle
            GameEvents.TriggerPhaseChanged(oldPhase, newPhase);

            Debug.Log($"[GameStateManager] Faz değişti: {oldPhase} -> {newPhase}");
        }

        /// <summary>
        /// Fetih alt durumunu değiştir
        /// </summary>
        public void ChangeFetihState(FetihState newState)
        {
            if (_currentFetihState == newState) return;

            _currentFetihState = newState;
            _currentState.fetihState = newState;

            GameEvents.TriggerFetihStateChanged(newState);
            Debug.Log($"[GameStateManager] Fetih durumu: {newState}");
        }

        /// <summary>
        /// Savaş alt durumunu değiştir
        /// </summary>
        public void ChangeSavasState(SavasState newState)
        {
            if (_currentSavasState == newState) return;

            _currentSavasState = newState;
            _currentState.savasState = newState;

            GameEvents.TriggerSavasStateChanged(newState);
            Debug.Log($"[GameStateManager] Savaş durumu: {newState}");
        }

        /// <summary>
        /// Sıradaki oyuncuya geç
        /// </summary>
        public void NextTurn()
        {
            if (_currentState == null || _currentState.players == null) return;

            int activeCount = _currentState.GetActivePlayerCount();
            if (activeCount <= 1)
            {
                // Tek oyuncu kaldı - oyun bitti
                EndGame();
                return;
            }

            // Sıradaki aktif oyuncuyu bul
            int nextIndex = _currentState.currentTurnPlayerIndex;
            int attempts = 0;

            do
            {
                nextIndex = (nextIndex + 1) % _currentState.players.Count;
                attempts++;

                if (attempts > _currentState.players.Count)
                {
                    Debug.LogError("[GameStateManager] Aktif oyuncu bulunamadı!");
                    return;
                }
            } while (_currentState.players[nextIndex].isEliminated);

            _currentState.currentTurnPlayerIndex = nextIndex;
            string playerId = _currentState.players[nextIndex].playerId;

            GameEvents.TriggerTurnChanged(playerId);
            Debug.Log($"[GameStateManager] Sıra değişti: {_currentState.players[nextIndex].displayName}");
        }

        /// <summary>
        /// Yeni tur başlat
        /// </summary>
        public void StartNewRound()
        {
            _currentState.currentRound++;
            _currentState.currentQuestionIndex = 0;

            GameEvents.TriggerRoundStarted(_currentState.currentRound);
            Debug.Log($"[GameStateManager] Yeni tur başladı: {_currentState.currentRound}");
        }

        /// <summary>
        /// Turu bitir
        /// </summary>
        public void EndRound()
        {
            GameEvents.TriggerRoundEnded(_currentState.currentRound);

            // Maksimum tur kontrolü
            if (_currentState.currentRound >= Config.savasRoundCount)
            {
                EndGame();
            }
        }

        /// <summary>
        /// Oyuncu skorunu güncelle
        /// </summary>
        public void UpdatePlayerScore(string playerId, int scoreDelta)
        {
            var player = _currentState.GetPlayer(playerId);
            if (player == null) return;

            int oldScore = player.currentScore;
            player.currentScore += scoreDelta;

            GameEvents.TriggerScoreChanged(playerId, oldScore, player.currentScore);
        }

        /// <summary>
        /// Oyuncuyu elen
        /// </summary>
        public void EliminatePlayer(string playerId)
        {
            var player = _currentState.GetPlayer(playerId);
            if (player == null || player.isEliminated) return;

            player.isEliminated = true;

            GameEvents.TriggerPlayerEliminated(playerId, _currentState.currentRound);
            Debug.Log($"[GameStateManager] Oyuncu elendi: {player.displayName}");

            // Kalan oyuncu sayısını kontrol et
            if (_currentState.GetActivePlayerCount() <= 1)
            {
                EndGame();
            }
        }

        /// <summary>
        /// Oyunu bitir
        /// </summary>
        public void EndGame()
        {
            ChangePhase(GamePhase.GameOver);

            // Sonuçları hazırla
            var results = PrepareGameResults();

            _currentState.isGameOver = true;
            _currentState.finalResults = results;

            // Kazananı belirle
            if (results.Count > 0)
            {
                _currentState.winnerId = results[0].playerId;
            }

            GameEvents.TriggerGameEnded(results);
            Debug.Log("[GameStateManager] Oyun bitti!");
        }

        /// <summary>
        /// Mevcut soru indeksini artır
        /// </summary>
        public void IncrementQuestionIndex()
        {
            _currentState.currentQuestionIndex++;
        }

        /// <summary>
        /// Aktif soru sayısına ulaşıldı mı?
        /// </summary>
        public bool IsRoundComplete()
        {
            return _currentState.currentQuestionIndex >= Config.questionsPerRound;
        }

        #endregion

        #region Private Methods

        private void EnterPhase(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Lobby:
                    // Lobby başlangıç işlemleri
                    break;

                case GamePhase.Fetih:
                    _currentFetihState = FetihState.WaitingQuestion;
                    _currentState.fetihState = FetihState.WaitingQuestion;
                    _currentState.currentRound = 0;
                    _currentState.currentQuestionIndex = 0;
                    break;

                case GamePhase.Savas:
                    _currentSavasState = SavasState.SelectingTarget;
                    _currentState.savasState = SavasState.SelectingTarget;
                    _currentState.currentRound = 0;
                    _currentState.currentQuestionIndex = 0;
                    _currentState.currentTurnPlayerIndex = 0;
                    break;

                case GamePhase.GameOver:
                    // Oyun sonu işlemleri
                    break;
            }
        }

        private void ExitPhase(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Lobby:
                    // Lobby temizlik
                    break;

                case GamePhase.Fetih:
                    // Fetih sonuç hesaplama
                    break;

                case GamePhase.Savas:
                    // Savaş sonuç hesaplama
                    break;
            }
        }

        private List<GameEndPlayerResult> PrepareGameResults()
        {
            var results = new List<GameEndPlayerResult>();
            if (_currentState?.players == null) return results;

            // Oyuncuları skora göre sırala
            var sortedPlayers = new List<InGamePlayerData>(_currentState.players);
            sortedPlayers.Sort((a, b) =>
            {
                // Önce elenme durumuna göre (elenmemişler önce)
                if (a.isEliminated != b.isEliminated)
                    return a.isEliminated ? 1 : -1;

                // Sonra skora göre
                return b.currentScore.CompareTo(a.currentScore);
            });

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var player = sortedPlayers[i];

                // TP hesapla
                var tpResult = CalculateTP(player, i + 1, sortedPlayers);

                results.Add(new GameEndPlayerResult
                {
                    playerId = player.playerId,
                    displayName = player.displayName,
                    finalRank = i + 1,
                    finalScore = player.currentScore,
                    correctAnswers = player.correctAnswers,
                    wrongAnswers = player.wrongAnswers,
                    wasEliminated = player.isEliminated,
                    tpResult = tpResult
                });
            }

            return results;
        }

        private TPCalculationResult CalculateTP(InGamePlayerData player, int rank, List<InGamePlayerData> allPlayers)
        {
            var result = new TPCalculationResult();

            // Sıraya göre taban TP
            result.baseTP = rank switch
            {
                1 => 300, // 1. sıra
                2 => 200, // 2. sıra
                3 => 100, // 3. sıra
                _ => 0
            };

            // Puan yüzdesi bonusu
            int maxScore = 0;
            foreach (var p in allPlayers)
            {
                if (p.currentScore > maxScore) maxScore = p.currentScore;
            }

            if (maxScore > 0)
            {
                float scorePercent = (float)player.currentScore / maxScore;
                result.scorePercentageBonus = Mathf.RoundToInt(scorePercent * 100);
            }

            // Rakip seviyesi bonusu (basitleştirilmiş)
            result.opponentBonus = allPlayers.Count > 1 ? 20 : 0;

            // Win streak ve undefeated bonusları PlayerManager'dan alınabilir
            // Şimdilik 0 olarak bırakıyoruz - bunlar sunucudan gelecek
            result.winStreakBonus = 0;
            result.undefeatedBonus = 0;

            return result;
        }

        #endregion

        #region Event Handlers

        private void HandleGameStarting(GameStartData data)
        {
            // GameStartData'dan GameStateData oluştur
            var state = new GameStateData
            {
                gameId = data.gameId,
                currentPhase = GamePhase.Fetih,
                players = new List<InGamePlayerData>()
            };

            foreach (var playerInit in data.players)
            {
                state.players.Add(new InGamePlayerData
                {
                    playerId = playerInit.playerId,
                    displayName = playerInit.displayName,
                    color = playerInit.assignedColor,
                    availableJokers = playerInit.availableJokers
                });
            }

            InitializeGame(state);
            ChangePhase(GamePhase.Fetih);
        }

        private void HandleQuestionResult(QuestionResultData result)
        {
            // Soru sonuçlarını işle
            if (result?.playerResults == null) return;

            foreach (var playerResult in result.playerResults)
            {
                var player = _currentState.GetPlayer(playerResult.playerId);
                if (player == null) continue;

                // Skorları güncelle
                int oldScore = player.currentScore;
                player.currentScore += playerResult.earnedPoints;

                if (playerResult.isCorrect)
                    player.correctAnswers++;
                else
                    player.wrongAnswers++;

                GameEvents.TriggerScoreChanged(playerResult.playerId, oldScore, player.currentScore);
            }
        }

        private void HandlePlayerEliminated(string playerId, int round)
        {
            var player = _currentState.GetPlayer(playerId);
            if (player != null)
            {
                player.isEliminated = true;
            }
        }

        private void HandleAttackResolved(AttackResultData data)
        {
            // Saldırı sonuçlarını işle
            if (data.defenderEliminated)
            {
                EliminatePlayer(data.defenderId);
            }
        }

        #endregion
    }
}
