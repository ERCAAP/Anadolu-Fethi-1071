using System;
using System.Collections.Generic;
using BilVeFethet.Data;
using BilVeFethet.Enums;

namespace BilVeFethet.Events
{
    /// <summary>
    /// Merkezi event sistemi - tüm manager'lar arası iletişim
    /// Observer pattern ile decoupled mimari sağlar
    /// </summary>
    public static class GameEvents
    {
        // ===== BAĞLANTI EVENTLERI =====
        
        /// <summary>Sunucuya bağlandı</summary>
        public static event Action OnConnected;
        
        /// <summary>Sunucu bağlantısı kesildi</summary>
        public static event Action<string> OnDisconnected;  // reason
        
        /// <summary>Bağlantı hatası</summary>
        public static event Action<string, int> OnConnectionError;  // message, errorCode
        
        /// <summary>Yeniden bağlanma deneniyor</summary>
        public static event Action<int> OnReconnecting;  // attempt number
        
        // ===== LOBBY EVENTLERI =====
        
        /// <summary>Oyun aranıyor</summary>
        public static event Action OnSearchingGame;
        
        /// <summary>Oyun bulundu</summary>
        public static event Action<string> OnGameFound;  // gameId
        
        /// <summary>Oyuncu lobiye katıldı</summary>
        public static event Action<InGamePlayerData> OnPlayerJoined;
        
        /// <summary>Oyuncu lobiden ayrıldı</summary>
        public static event Action<string> OnPlayerLeft;  // playerId
        
        /// <summary>Oyun başlıyor</summary>
        public static event Action<GameStartData> OnGameStarting;
        
        // ===== OYUN AŞAMA EVENTLERI =====
        
        /// <summary>Oyun aşaması değişti</summary>
        public static event Action<GamePhase, GamePhase> OnPhaseChanged;  // oldPhase, newPhase
        
        /// <summary>Fetih aşaması durumu değişti</summary>
        public static event Action<FetihState> OnFetihStateChanged;
        
        /// <summary>Savaş aşaması durumu değişti</summary>
        public static event Action<SavasState> OnSavasStateChanged;
        
        /// <summary>Yeni tur başladı</summary>
        public static event Action<int> OnRoundStarted;  // round number
        
        /// <summary>Tur sona erdi</summary>
        public static event Action<int> OnRoundEnded;  // round number
        
        /// <summary>Sıra değişti</summary>
        public static event Action<string> OnTurnChanged;  // playerId
        
        // ===== SORU EVENTLERI =====
        
        /// <summary>Yeni soru alındı</summary>
        public static event Action<QuestionData> OnQuestionReceived;
        
        /// <summary>Soru süresi başladı</summary>
        public static event Action<float> OnQuestionTimerStarted;  // time limit
        
        /// <summary>Soru süresi güncellendi</summary>
        public static event Action<float> OnQuestionTimerUpdated;  // remaining time
        
        /// <summary>Soru süresi doldu</summary>
        public static event Action OnQuestionTimerExpired;
        
        /// <summary>Oyuncu cevap verdi</summary>
        public static event Action<string, int> OnPlayerAnswered;  // playerId, answerIndex
        
        /// <summary>Soru sonuçları alındı</summary>
        public static event Action<QuestionResultData> OnQuestionResultReceived;
        
        /// <summary>Joker kullanıldı</summary>
        public static event Action<string, JokerType> OnJokerUsed;  // playerId, jokerType
        
        /// <summary>Joker sonucu alındı</summary>
        public static event Action<JokerUseResult> OnJokerResultReceived;
        
        // ===== HARİTA VE TOPRAK EVENTLERI =====
        
        /// <summary>Toprak ele geçirildi</summary>
        public static event Action<int, string, string> OnTerritoryCaptured;  // territoryId, oldOwnerId, newOwnerId
        
        /// <summary>Toprak seçim modu başladı</summary>
        public static event Action<string, int> OnTerritorySelectionStarted;  // playerId, selectionCount
        
        /// <summary>Toprak seçildi</summary>
        public static event Action<string, int> OnTerritorySelected;  // playerId, territoryId
        
        /// <summary>Harita güncellendi</summary>
        public static event Action<TerritoryUpdateData> OnTerritoryUpdated;
        
        /// <summary>Kale hasar aldı</summary>
        public static event Action<string, int> OnCastleDamaged;  // playerId, remainingHealth
        
        /// <summary>Kale yıkıldı</summary>
        public static event Action<string> OnCastleDestroyed;  // playerId
        
        // ===== SALDIRI EVENTLERI =====
        
        /// <summary>Saldırı hedefi seçim başladı</summary>
        public static event Action<string> OnAttackSelectionStarted;  // attackerId
        
        /// <summary>Saldırı başladı</summary>
        public static event Action<AttackData> OnAttackStarted;
        
        /// <summary>Saldırı sonuçlandı</summary>
        public static event Action<AttackResultData> OnAttackResolved;
        
        /// <summary>Savunma başarılı</summary>
        public static event Action<string, int> OnDefenseSuccessful;  // defenderId, territoryId
        
        // ===== SKOR EVENTLERI =====
        
        /// <summary>Oyuncu puanı değişti</summary>
        public static event Action<string, int, int> OnScoreChanged;  // playerId, oldScore, newScore
        
        /// <summary>TP kazanıldı</summary>
        public static event Action<TPCalculationResult> OnTPEarned;
        
        // ===== OYUNCU EVENTLERI =====
        
        /// <summary>Oyuncu elendi</summary>
        public static event Action<string, int> OnPlayerEliminated;  // playerId, eliminationRound
        
        /// <summary>Oyuncu seviye atladı</summary>
        public static event Action<int, int> OnPlayerLevelUp;  // oldLevel, newLevel
        
        // ===== OYUN SONU EVENTLERI =====
        
        /// <summary>Oyun sona erdi</summary>
        public static event Action<List<GameEndPlayerResult>> OnGameEnded;
        
        /// <summary>Beraberlik - uzman sorusu</summary>
        public static event Action OnTiebreaker;
        
        /// <summary>Madalya kazanıldı</summary>
        public static event Action<MedalType, MedalGrade> OnMedalEarned;
        
        /// <summary>Sıralama güncellendi</summary>
        public static event Action<RankingType, int> OnRankingUpdated;  // type, new rank
        
        // ===== BOT EVENTLERI =====

        /// <summary>Bot oyunu başladı</summary>
        public static event Action<List<InGamePlayerData>> OnBotGameStarted;

        /// <summary>Bot cevap verdi</summary>
        public static event Action<PlayerAnswerData> OnBotAnswerSubmitted;

        /// <summary>Bot toprak seçti</summary>
        public static event Action<string, int> OnBotTerritorySelected;  // botId, territoryId

        /// <summary>Bot saldırı hedefi seçti</summary>
        public static event Action<string, int, bool> OnBotAttackTargetSelected;  // botId, targetTerritoryId, useMagicWings

        /// <summary>Saldırı aşaması başladı (botlar için genişletilmiş)</summary>
        public static event Action<string, List<int>> OnAttackPhaseStarted;  // currentPlayerId, attackableTargets

        // ===== NETWORK EVENTLERI =====

        /// <summary>Senkronizasyon gerekli</summary>
        public static event Action OnSyncRequired;
        
        /// <summary>Senkronizasyon tamamlandı</summary>
        public static event Action<SyncData> OnSyncCompleted;
        
        /// <summary>Ping güncellendi</summary>
        public static event Action<int> OnPingUpdated;  // ping ms
        
        // ===== TRIGGER METODLARI =====
        
        // Bağlantı
        public static void TriggerConnected() => OnConnected?.Invoke();
        public static void TriggerDisconnected(string reason) => OnDisconnected?.Invoke(reason);
        public static void TriggerConnectionError(string message, int code) => OnConnectionError?.Invoke(message, code);
        public static void TriggerReconnecting(int attempt) => OnReconnecting?.Invoke(attempt);
        
        // Lobby
        public static void TriggerSearchingGame() => OnSearchingGame?.Invoke();
        public static void TriggerGameFound(string gameId) => OnGameFound?.Invoke(gameId);
        public static void TriggerPlayerJoined(InGamePlayerData player) => OnPlayerJoined?.Invoke(player);
        public static void TriggerPlayerLeft(string playerId) => OnPlayerLeft?.Invoke(playerId);
        public static void TriggerGameStarting(GameStartData data) => OnGameStarting?.Invoke(data);
        
        // Aşama
        public static void TriggerPhaseChanged(GamePhase oldPhase, GamePhase newPhase) => OnPhaseChanged?.Invoke(oldPhase, newPhase);
        public static void TriggerFetihStateChanged(FetihState state) => OnFetihStateChanged?.Invoke(state);
        public static void TriggerSavasStateChanged(SavasState state) => OnSavasStateChanged?.Invoke(state);
        public static void TriggerRoundStarted(int round) => OnRoundStarted?.Invoke(round);
        public static void TriggerRoundEnded(int round) => OnRoundEnded?.Invoke(round);
        public static void TriggerTurnChanged(string playerId) => OnTurnChanged?.Invoke(playerId);
        
        // Soru
        public static void TriggerQuestionReceived(QuestionData question) => OnQuestionReceived?.Invoke(question);
        public static void TriggerQuestionTimerStarted(float time) => OnQuestionTimerStarted?.Invoke(time);
        public static void TriggerQuestionTimerUpdated(float remaining) => OnQuestionTimerUpdated?.Invoke(remaining);
        public static void TriggerQuestionTimerExpired() => OnQuestionTimerExpired?.Invoke();
        public static void TriggerPlayerAnswered(string playerId, int answerIndex) => OnPlayerAnswered?.Invoke(playerId, answerIndex);
        public static void TriggerQuestionResultReceived(QuestionResultData result) => OnQuestionResultReceived?.Invoke(result);
        public static void TriggerJokerUsed(string playerId, JokerType joker) => OnJokerUsed?.Invoke(playerId, joker);
        public static void TriggerJokerResultReceived(JokerUseResult result) => OnJokerResultReceived?.Invoke(result);
        
        // Harita
        public static void TriggerTerritoryCaptured(int territoryId, string oldOwner, string newOwner) => OnTerritoryCaptured?.Invoke(territoryId, oldOwner, newOwner);
        public static void TriggerTerritorySelectionStarted(string playerId, int count) => OnTerritorySelectionStarted?.Invoke(playerId, count);
        public static void TriggerTerritorySelected(string playerId, int territoryId) => OnTerritorySelected?.Invoke(playerId, territoryId);
        public static void TriggerTerritoryUpdated(TerritoryUpdateData data) => OnTerritoryUpdated?.Invoke(data);
        public static void TriggerCastleDamaged(string playerId, int health) => OnCastleDamaged?.Invoke(playerId, health);
        public static void TriggerCastleDestroyed(string playerId) => OnCastleDestroyed?.Invoke(playerId);
        
        // Saldırı
        public static void TriggerAttackSelectionStarted(string attackerId) => OnAttackSelectionStarted?.Invoke(attackerId);
        public static void TriggerAttackStarted(AttackData data) => OnAttackStarted?.Invoke(data);
        public static void TriggerAttackResolved(AttackResultData data) => OnAttackResolved?.Invoke(data);
        public static void TriggerDefenseSuccessful(string defenderId, int territoryId) => OnDefenseSuccessful?.Invoke(defenderId, territoryId);
        
        // Skor
        public static void TriggerScoreChanged(string playerId, int oldScore, int newScore) => OnScoreChanged?.Invoke(playerId, oldScore, newScore);
        public static void TriggerTPEarned(TPCalculationResult result) => OnTPEarned?.Invoke(result);
        
        // Oyuncu
        public static void TriggerPlayerEliminated(string playerId, int round) => OnPlayerEliminated?.Invoke(playerId, round);
        public static void TriggerPlayerLevelUp(int oldLevel, int newLevel) => OnPlayerLevelUp?.Invoke(oldLevel, newLevel);
        
        // Oyun sonu
        public static void TriggerGameEnded(List<GameEndPlayerResult> results) => OnGameEnded?.Invoke(results);
        public static void TriggerTiebreaker() => OnTiebreaker?.Invoke();
        public static void TriggerMedalEarned(MedalType type, MedalGrade grade) => OnMedalEarned?.Invoke(type, grade);
        public static void TriggerRankingUpdated(RankingType type, int rank) => OnRankingUpdated?.Invoke(type, rank);
        
        // Network
        public static void TriggerSyncRequired() => OnSyncRequired?.Invoke();
        public static void TriggerSyncCompleted(SyncData data) => OnSyncCompleted?.Invoke(data);
        public static void TriggerPingUpdated(int ping) => OnPingUpdated?.Invoke(ping);

        // Bot
        public static void TriggerBotGameStarted(List<InGamePlayerData> bots) => OnBotGameStarted?.Invoke(bots);
        public static void TriggerBotAnswerSubmitted(PlayerAnswerData answer) => OnBotAnswerSubmitted?.Invoke(answer);
        public static void TriggerBotTerritorySelected(string botId, int territoryId) => OnBotTerritorySelected?.Invoke(botId, territoryId);
        public static void TriggerBotAttackTargetSelected(string botId, int targetId, bool useMagicWings) => OnBotAttackTargetSelected?.Invoke(botId, targetId, useMagicWings);
        public static void TriggerAttackPhaseStarted(string playerId, List<int> targets) => OnAttackPhaseStarted?.Invoke(playerId, targets);

        /// <summary>
        /// Tüm event listener'ları temizle (scene geçişlerinde çağır)
        /// </summary>
        public static void ClearAllListeners()
        {
            OnConnected = null;
            OnDisconnected = null;
            OnConnectionError = null;
            OnReconnecting = null;
            OnSearchingGame = null;
            OnGameFound = null;
            OnPlayerJoined = null;
            OnPlayerLeft = null;
            OnGameStarting = null;
            OnPhaseChanged = null;
            OnFetihStateChanged = null;
            OnSavasStateChanged = null;
            OnRoundStarted = null;
            OnRoundEnded = null;
            OnTurnChanged = null;
            OnQuestionReceived = null;
            OnQuestionTimerStarted = null;
            OnQuestionTimerUpdated = null;
            OnQuestionTimerExpired = null;
            OnPlayerAnswered = null;
            OnQuestionResultReceived = null;
            OnJokerUsed = null;
            OnJokerResultReceived = null;
            OnTerritoryCaptured = null;
            OnTerritorySelectionStarted = null;
            OnTerritorySelected = null;
            OnTerritoryUpdated = null;
            OnCastleDamaged = null;
            OnCastleDestroyed = null;
            OnAttackSelectionStarted = null;
            OnAttackStarted = null;
            OnAttackResolved = null;
            OnDefenseSuccessful = null;
            OnScoreChanged = null;
            OnTPEarned = null;
            OnPlayerEliminated = null;
            OnPlayerLevelUp = null;
            OnGameEnded = null;
            OnTiebreaker = null;
            OnMedalEarned = null;
            OnRankingUpdated = null;
            OnSyncRequired = null;
            OnSyncCompleted = null;
            OnPingUpdated = null;
            OnBotGameStarted = null;
            OnBotAnswerSubmitted = null;
            OnBotTerritorySelected = null;
            OnBotAttackTargetSelected = null;
            OnAttackPhaseStarted = null;
        }
    }
}
