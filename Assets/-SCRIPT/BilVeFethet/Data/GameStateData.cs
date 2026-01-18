using System;
using System.Collections.Generic;
using BilVeFethet.Enums;
using UnityEngine;

namespace BilVeFethet.Data
{
    /// <summary>
    /// Oyun durumu verisi - tüm oyun state'ini tutar
    /// </summary>
    [Serializable]
    public class GameStateData
    {
        public string gameId;
        public GamePhase currentPhase;
        public FetihState fetihState;
        public SavasState savasState;
        
        // Tur bilgileri
        public int currentRound;             // Savaş aşamasında 1-4 arası
        public int currentQuestionIndex;     // Her turda 0-3 arası
        public int currentTurnPlayerIndex;   // Sırası olan oyuncu
        
        // Oyuncular
        public List<InGamePlayerData> players;
        public string currentAttackerId;
        public string currentDefenderId;
        
        // Harita durumu
        public MapData mapData;
        
        // Aktif soru
        public QuestionData currentQuestion;
        public float questionStartTime;
        public float questionEndTime;
        
        // Saldırı bilgisi
        public int attackTargetTerritoryId;
        public int attackSourceTerritoryId;
        
        // Oyun sonu bilgileri
        public bool isGameOver;
        public string winnerId;
        public List<GameEndPlayerResult> finalResults;
        
        // Zaman damgası (senkronizasyon için)
        public long serverTimestamp;
        public long lastUpdateTimestamp;

        public GameStateData()
        {
            players = new List<InGamePlayerData>();
            mapData = new MapData();
            finalResults = new List<GameEndPlayerResult>();
        }

        /// <summary>
        /// Oyuncu verisini ID'ye göre getir
        /// </summary>
        public InGamePlayerData GetPlayer(string playerId)
        {
            return players.Find(p => p.playerId == playerId);
        }

        /// <summary>
        /// Yerel oyuncuyu getir
        /// </summary>
        public InGamePlayerData GetLocalPlayer()
        {
            return players.Find(p => p.isLocalPlayer);
        }

        /// <summary>
        /// Aktif oyuncu sayısını getir (elenmemişler)
        /// </summary>
        public int GetActivePlayerCount()
        {
            return players.FindAll(p => !p.isEliminated).Count;
        }

        /// <summary>
        /// Sırası gelen oyuncuyu getir
        /// </summary>
        public InGamePlayerData GetCurrentTurnPlayer()
        {
            if (currentTurnPlayerIndex < 0 || currentTurnPlayerIndex >= players.Count)
                return null;
            return players[currentTurnPlayerIndex];
        }
    }

    /// <summary>
    /// Oyun sonu oyuncu sonucu
    /// </summary>
    [Serializable]
    public class GameEndPlayerResult
    {
        public string playerId;
        public string displayName;
        public int finalRank;                // 1, 2, veya 3
        public int finalScore;
        public TPCalculationResult tpResult;
        public int territoriesOwned;
        public int correctAnswers;
        public int wrongAnswers;
        public bool wasEliminated;
        public int eliminationRound;
    }

    /// <summary>
    /// Saldırı verisi
    /// </summary>
    [Serializable]
    public class AttackData
    {
        public string attackerId;
        public string defenderId;
        public int targetTerritoryId;
        public int sourceTerritoryId;        // Saldırının yapıldığı toprak
        public bool usedMagicWings;          // Sihirli kanatlar kullanıldı mı
        public QuestionCategory? forcedCategory;  // Kategori seçme jokeri
    }

    /// <summary>
    /// Saldırı sonuç verisi
    /// </summary>
    [Serializable]
    public class AttackResultData
    {
        public string attackerId;
        public string defenderId;
        public int targetTerritoryId;
        public AttackResult result;
        
        // Puan değişiklikleri
        public int attackerScoreChange;
        public int defenderScoreChange;
        
        // Kale hasarı (kaleye saldırıldıysa)
        public int remainingCastleHealth;
        
        // Oyuncu elenme durumu
        public bool defenderEliminated;
        public List<int> transferredTerritories;  // Devredilen topraklar
        public int transferredScore;
    }

    /// <summary>
    /// Oyun başlatma verisi - sunucudan gelir
    /// </summary>
    [Serializable]
    public class GameStartData
    {
        public string gameId;
        public long serverTime;
        public List<PlayerInitData> players;
        public MapData initialMap;
        public GameConfig gameConfig;
    }

    /// <summary>
    /// Oyuncu başlangıç verisi
    /// </summary>
    [Serializable]
    public class PlayerInitData
    {
        public string playerId;
        public string displayName;
        public string avatarUrl;
        public int level;
        public PlayerColor assignedColor;
        public int startingTerritoryId;      // Başlangıç kalesi
        public Dictionary<JokerType, int> availableJokers;
    }

    /// <summary>
    /// Oyun konfigürasyonu
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        public float questionTimeLimit = 15f;        // Soru süresi (saniye)
        public float territorySelectionTime = 10f;   // Toprak seçim süresi
        public float attackSelectionTime = 15f;      // Saldırı seçim süresi
        public int fetihQuestionCount = 4;           // Fetih aşaması soru sayısı
        public int savasRoundCount = 4;              // Savaş aşaması tur sayısı
        public int questionsPerRound = 4;            // Her turda soru sayısı
        public int baseTerritoryPoints = 200;        // Temel toprak puanı
        public int defenseBonus = 100;               // Başarılı savunma bonusu
        public int expertQuestionPoints = 10;        // Uzman sorusu puanı (beraberlik)
    }

    /// <summary>
    /// Network senkronizasyon verisi - minimum bandwidth
    /// </summary>
    [Serializable]
    public class SyncData
    {
        public long timestamp;
        public GamePhase phase;
        public int round;
        public int questionIndex;
        public int currentPlayerIndex;
        
        // Sıkıştırılmış oyuncu skorları (3 oyuncu için 12 byte)
        public int[] playerScores;
        
        // Sıkıştırılmış toprak sahipliği (15 toprak, 2 bit/toprak = 4 byte)
        public byte[] territoryOwnership;
        
        // Değişen alanları işaretle
        public ushort changeFlags;
    }
}
