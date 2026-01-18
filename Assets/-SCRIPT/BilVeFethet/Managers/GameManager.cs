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
    /// Ana oyun yöneticisi - tüm oyun akışını koordine eder
    /// State machine pattern ile aşama yönetimi
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        [Header("Game Configuration")]
        [SerializeField] private GameConfig gameConfig;

        // Current game state
        private GameStateData _gameState;
        private bool _isInitialized;

        // State machine
        private GamePhase _currentPhase;
        private FetihState _fetihState;
        private SavasState _savasState;

        // Turn tracking
        private int _currentRound;
        private int _currentQuestionIndex;
        private int _fetihQuestionCount;
        private int _territoriesAssignedThisRound;

        // Properties
        public GameStateData GameState => _gameState;
        public GamePhase CurrentPhase => _currentPhase;
        public FetihState FetihState => _fetihState;
        public SavasState SavasState => _savasState;
        public int CurrentRound => _currentRound;
        public bool IsLocalPlayerTurn => 
            _gameState?.GetCurrentTurnPlayer()?.isLocalPlayer ?? false;
        public GameConfig Config => gameConfig ?? new GameConfig();

        protected override void OnSingletonAwake()
        {
            gameConfig = gameConfig ?? new GameConfig();
            _gameState = new GameStateData();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        #region Event Subscriptions

        private void SubscribeToEvents()
        {
            GameEvents.OnGameStarting += HandleGameStarting;
            GameEvents.OnQuestionResultReceived += HandleQuestionResult;
            GameEvents.OnAttackResolved += HandleAttackResolved;
            GameEvents.OnSyncCompleted += HandleSyncCompleted;
            GameEvents.OnPlayerEliminated += HandlePlayerEliminated;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnGameStarting -= HandleGameStarting;
            GameEvents.OnQuestionResultReceived -= HandleQuestionResult;
            GameEvents.OnAttackResolved -= HandleAttackResolved;
            GameEvents.OnSyncCompleted -= HandleSyncCompleted;
            GameEvents.OnPlayerEliminated -= HandlePlayerEliminated;
        }

        #endregion

        #region Game Initialization

        private void HandleGameStarting(GameStartData startData)
        {
            InitializeGame(startData);
        }

        public void InitializeGame(GameStartData startData)
        {
            _gameState = new GameStateData
            {
                gameId = startData.gameId,
                mapData = startData.initialMap,
                serverTimestamp = startData.serverTime
            };

            foreach (var playerInit in startData.players)
            {
                var inGamePlayer = new InGamePlayerData
                {
                    playerId = playerInit.playerId,
                    displayName = playerInit.displayName,
                    color = playerInit.assignedColor,
                    castleTerritoryId = playerInit.startingTerritoryId,
                    availableJokers = playerInit.availableJokers ?? new Dictionary<JokerType, int>(),
                    isLocalPlayer = playerInit.playerId == PlayerManager.Instance?.LocalPlayerId
                };

                inGamePlayer.ownedTerritories.Add(playerInit.startingTerritoryId);
                
                var territory = _gameState.mapData.GetTerritory(playerInit.startingTerritoryId);
                if (territory != null)
                {
                    territory.state = TerritoryState.Kale;
                    territory.ownerId = playerInit.playerId;
                    territory.ownerColor = playerInit.assignedColor;
                }

                _gameState.players.Add(inGamePlayer);
            }

            _isInitialized = true;
            SetPhase(GamePhase.Fetih);
            
            Debug.Log($"[GameManager] Game initialized. ID: {startData.gameId}, Players: {_gameState.players.Count}");
        }

        #endregion

        #region Phase Management

        public void SetPhase(GamePhase newPhase)
        {
            if (_currentPhase == newPhase) return;

            var oldPhase = _currentPhase;
            _currentPhase = newPhase;
            _gameState.currentPhase = newPhase;

            Debug.Log($"[GameManager] Phase changed: {oldPhase} -> {newPhase}");
            GameEvents.TriggerPhaseChanged(oldPhase, newPhase);

            switch (newPhase)
            {
                case GamePhase.Fetih:
                    StartFetihPhase();
                    break;
                case GamePhase.Savas:
                    StartSavasPhase();
                    break;
                case GamePhase.GameOver:
                    HandleGameOver();
                    break;
            }
        }

        private void StartFetihPhase()
        {
            _fetihQuestionCount = 0;
            _currentQuestionIndex = 0;
            SetFetihState(FetihState.WaitingQuestion);
            RequestNextFetihQuestionAsync();
        }

        private void StartSavasPhase()
        {
            _currentRound = 1;
            _currentQuestionIndex = 0;
            _gameState.currentRound = 1;
            SetInitialTurnOrder();
            SetSavasState(SavasState.SelectingTarget);
            GameEvents.TriggerRoundStarted(_currentRound);
        }

        public void SetFetihState(FetihState newState)
        {
            _fetihState = newState;
            _gameState.fetihState = newState;
            GameEvents.TriggerFetihStateChanged(newState);
        }

        public void SetSavasState(SavasState newState)
        {
            _savasState = newState;
            _gameState.savasState = newState;
            GameEvents.TriggerSavasStateChanged(newState);
        }

        #endregion

        #region Fetih Phase Logic

        private async void RequestNextFetihQuestionAsync()
        {
            await Task.Delay(500);

            if (_fetihQuestionCount >= gameConfig.fetihQuestionCount)
            {
                var emptyTerritories = _gameState.mapData.GetEmptyTerritories();
                if (emptyTerritories.Count == 0 || _fetihQuestionCount >= gameConfig.fetihQuestionCount)
                {
                    SetPhase(GamePhase.Savas);
                    return;
                }
            }

            SetFetihState(FetihState.WaitingQuestion);
            
            var request = new QuestionRequestData
            {
                gameId = _gameState.gameId,
                playerId = PlayerManager.Instance?.LocalPlayerId,
                currentPhase = GamePhase.Fetih,
                roundNumber = 0,
                questionIndex = _fetihQuestionCount
            };

            var question = await NetworkManager.Instance.RequestQuestionAsync(request);
            
            if (question != null)
            {
                _gameState.currentQuestion = question;
                _fetihQuestionCount++;
                SetFetihState(FetihState.AnsweringQuestion);
            }
        }

        private void ProcessFetihQuestionResult(QuestionResultData result)
        {
            _territoriesAssignedThisRound = 0;
            
            if (result.playerRanking.Count >= 1)
            {
                var firstPlaceId = result.playerRanking[0];
                GameEvents.TriggerTerritorySelectionStarted(firstPlaceId, 2);
                
                if (firstPlaceId == PlayerManager.Instance?.LocalPlayerId)
                {
                    SetFetihState(FetihState.SelectingTerritory);
                }
            }
        }

        public void OnTerritorySelected(string playerId, int territoryId)
        {
            var player = _gameState.GetPlayer(playerId);
            var territory = _gameState.mapData.GetTerritory(territoryId);
            
            if (player == null || territory == null || !territory.IsEmpty)
                return;

            territory.ownerId = playerId;
            territory.ownerColor = player.color;
            territory.state = TerritoryState.Normal;
            player.ownedTerritories.Add(territoryId);

            GameEvents.TriggerTerritoryCaptured(territoryId, null, playerId);
            GameEvents.TriggerTerritorySelected(playerId, territoryId);

            _territoriesAssignedThisRound++;

            if (_territoriesAssignedThisRound >= 3)
            {
                RequestNextFetihQuestionAsync();
            }
        }

        #endregion

        #region Savas Phase Logic

        private void SetInitialTurnOrder()
        {
            var turnOrders = new PlayerColor[][]
            {
                new[] { PlayerColor.Kirmizi, PlayerColor.Yesil, PlayerColor.Mavi },
                new[] { PlayerColor.Yesil, PlayerColor.Mavi, PlayerColor.Kirmizi },
                new[] { PlayerColor.Mavi, PlayerColor.Kirmizi, PlayerColor.Yesil }
            };

            if (_currentRound <= 3)
            {
                var order = turnOrders[_currentRound - 1];
                var firstPlayer = _gameState.players.Find(p => p.color == order[0] && !p.isEliminated);
                
                if (firstPlayer != null)
                {
                    _gameState.currentTurnPlayerIndex = _gameState.players.IndexOf(firstPlayer);
                    GameEvents.TriggerTurnChanged(firstPlayer.playerId);
                }
            }
            else
            {
                var activePlayers = _gameState.players.FindAll(p => !p.isEliminated);
                activePlayers.Sort((a, b) => b.currentScore.CompareTo(a.currentScore));
                
                if (activePlayers.Count > 0)
                {
                    _gameState.currentTurnPlayerIndex = _gameState.players.IndexOf(activePlayers[0]);
                    GameEvents.TriggerTurnChanged(activePlayers[0].playerId);
                }
            }
        }

        public void InitiateAttack(int targetTerritoryId, int sourceTerritoryId, bool useMagicWings = false)
        {
            var attacker = _gameState.GetCurrentTurnPlayer();
            if (attacker == null || !attacker.isLocalPlayer) return;

            var targetTerritory = _gameState.mapData.GetTerritory(targetTerritoryId);
            if (targetTerritory == null || targetTerritory.IsEmpty) return;

            _gameState.attackTargetTerritoryId = targetTerritoryId;
            _gameState.attackSourceTerritoryId = sourceTerritoryId;
            _gameState.currentAttackerId = attacker.playerId;
            _gameState.currentDefenderId = targetTerritory.ownerId;

            var attackData = new AttackData
            {
                attackerId = attacker.playerId,
                defenderId = targetTerritory.ownerId,
                targetTerritoryId = targetTerritoryId,
                sourceTerritoryId = sourceTerritoryId,
                usedMagicWings = useMagicWings
            };

            GameEvents.TriggerAttackStarted(attackData);
            SetSavasState(SavasState.WaitingQuestion);
            RequestBattleQuestionAsync();
        }

        private async void RequestBattleQuestionAsync()
        {
            await Task.Delay(300);

            var request = new QuestionRequestData
            {
                gameId = _gameState.gameId,
                playerId = PlayerManager.Instance?.LocalPlayerId,
                currentPhase = GamePhase.Savas,
                roundNumber = _currentRound,
                questionIndex = _currentQuestionIndex
            };

            var question = await NetworkManager.Instance.RequestQuestionAsync(request);
            
            if (question != null)
            {
                _gameState.currentQuestion = question;
                SetSavasState(SavasState.AnsweringQuestion);
            }
        }

        private void HandleAttackResolved(AttackResultData result)
        {
            var attacker = _gameState.GetPlayer(result.attackerId);
            var defender = _gameState.GetPlayer(result.defenderId);
            var territory = _gameState.mapData.GetTerritory(_gameState.attackTargetTerritoryId);

            if (attacker == null || defender == null || territory == null) return;

            switch (result.result)
            {
                case AttackResult.Success:
                    TransferTerritory(territory, attacker, defender);
                    break;

                case AttackResult.Failed:
                    ScoreManager.Instance?.AddScore(defender.playerId, gameConfig.defenseBonus);
                    GameEvents.TriggerDefenseSuccessful(defender.playerId, territory.territoryId);
                    break;

                case AttackResult.CastleHit:
                    defender.castleHealth = result.remainingCastleHealth;
                    GameEvents.TriggerCastleDamaged(defender.playerId, result.remainingCastleHealth);
                    break;

                case AttackResult.CastleDestroyed:
                    EliminatePlayer(defender, attacker, result);
                    break;
            }

            SetSavasState(SavasState.ResolvingBattle);
            StartCoroutine(ProceedToNextTurn());
        }

        private void TransferTerritory(TerritoryData territory, InGamePlayerData newOwner, InGamePlayerData oldOwner)
        {
            var oldOwnerId = territory.ownerId;
            
            oldOwner.ownedTerritories.Remove(territory.territoryId);
            newOwner.ownedTerritories.Add(territory.territoryId);
            
            territory.ownerId = newOwner.playerId;
            territory.ownerColor = newOwner.color;
            
            ScoreManager.Instance?.AddScore(newOwner.playerId, territory.pointValue);
            GameEvents.TriggerTerritoryCaptured(territory.territoryId, oldOwnerId, newOwner.playerId);
            
            newOwner.territoriesCaptured++;
            oldOwner.territoriesLost++;
        }

        private void EliminatePlayer(InGamePlayerData eliminated, InGamePlayerData eliminator, AttackResultData result)
        {
            eliminated.isEliminated = true;
            
            foreach (var territoryId in result.transferredTerritories)
            {
                var territory = _gameState.mapData.GetTerritory(territoryId);
                if (territory != null)
                {
                    territory.ownerId = eliminator.playerId;
                    territory.ownerColor = eliminator.color;
                    eliminator.ownedTerritories.Add(territoryId);
                }
            }
            eliminated.ownedTerritories.Clear();
            
            ScoreManager.Instance?.AddScore(eliminator.playerId, result.transferredScore);
            
            GameEvents.TriggerCastleDestroyed(eliminated.playerId);
            GameEvents.TriggerPlayerEliminated(eliminated.playerId, _currentRound);

            if (_gameState.GetActivePlayerCount() <= 1)
            {
                SetPhase(GamePhase.GameOver);
            }
        }

        private IEnumerator ProceedToNextTurn()
        {
            yield return new WaitForSeconds(1f);

            _currentQuestionIndex++;
            
            if (_currentQuestionIndex >= gameConfig.questionsPerRound)
            {
                _currentQuestionIndex = 0;
                _currentRound++;
                _gameState.currentRound = _currentRound;
                
                GameEvents.TriggerRoundEnded(_currentRound - 1);
                
                if (_currentRound > gameConfig.savasRoundCount)
                {
                    SetPhase(GamePhase.GameOver);
                    yield break;
                }
                
                GameEvents.TriggerRoundStarted(_currentRound);
                SetInitialTurnOrder();
            }
            else
            {
                AdvanceToNextPlayer();
            }

            SetSavasState(SavasState.SelectingTarget);
        }

        private void AdvanceToNextPlayer()
        {
            var currentIndex = _gameState.currentTurnPlayerIndex;
            var startIndex = currentIndex;
            
            do
            {
                currentIndex = (currentIndex + 1) % _gameState.players.Count;
                
                if (!_gameState.players[currentIndex].isEliminated)
                {
                    _gameState.currentTurnPlayerIndex = currentIndex;
                    GameEvents.TriggerTurnChanged(_gameState.players[currentIndex].playerId);
                    return;
                }
            }
            while (currentIndex != startIndex);
        }

        #endregion

        #region Event Handlers

        private void HandleQuestionResult(QuestionResultData result)
        {
            if (_currentPhase == GamePhase.Fetih)
            {
                ProcessFetihQuestionResult(result);
            }
        }

        private void HandleSyncCompleted(SyncData sync)
        {
            _gameState.serverTimestamp = sync.timestamp;
            _gameState.currentPhase = sync.phase;
            _gameState.currentRound = sync.round;
            _gameState.currentQuestionIndex = sync.questionIndex;
            _gameState.currentTurnPlayerIndex = sync.currentPlayerIndex;

            for (int i = 0; i < _gameState.players.Count && i < sync.playerScores.Length; i++)
            {
                _gameState.players[i].currentScore = sync.playerScores[i];
            }
        }

        private void HandlePlayerEliminated(string playerId, int round)
        {
            var player = _gameState.GetPlayer(playerId);
            if (player != null)
            {
                player.isEliminated = true;
            }
        }

        #endregion

        #region Game Over

        private void HandleGameOver()
        {
            var results = CalculateFinalResults();
            
            if (results.Count >= 2 && results[0].finalScore == results[1].finalScore)
            {
                StartTiebreaker();
                return;
            }

            _gameState.finalResults = results;
            _gameState.isGameOver = true;
            _gameState.winnerId = results[0].playerId;

            GameEvents.TriggerGameEnded(results);
        }

        private List<GameEndPlayerResult> CalculateFinalResults()
        {
            var results = new List<GameEndPlayerResult>();

            foreach (var player in _gameState.players)
            {
                var result = new GameEndPlayerResult
                {
                    playerId = player.playerId,
                    displayName = player.displayName,
                    finalScore = player.currentScore,
                    territoriesOwned = player.ownedTerritories.Count,
                    correctAnswers = player.correctAnswers,
                    wrongAnswers = player.wrongAnswers,
                    wasEliminated = player.isEliminated
                };

                results.Add(result);
            }

            results.Sort((a, b) => b.finalScore.CompareTo(a.finalScore));

            for (int i = 0; i < results.Count; i++)
            {
                results[i].finalRank = i + 1;
            }

            return results;
        }

        private void StartTiebreaker()
        {
            GameEvents.TriggerTiebreaker();
        }

        #endregion

        public void ResetGame()
        {
            _gameState = new GameStateData();
            _currentPhase = GamePhase.None;
            _fetihState = FetihState.WaitingQuestion;
            _savasState = SavasState.SelectingTarget;
            _currentRound = 0;
            _currentQuestionIndex = 0;
            _isInitialized = false;
        }
    }
}
