using System;
using System.Collections.Generic;
using BilVeFethet.Enums;
using UnityEngine;

namespace BilVeFethet.Data
{
    /// <summary>
    /// Oyuncu temel bilgileri - sunucudan gelen
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public string playerId;
        public string displayName;
        public string avatarUrl;
        public int level;
        public int totalTP;
        public int weeklyTP;
        public int goldCoins;
        public int gameRights;         // Kalan oyun hakkı
        public DateTime nextFreeGameTime;
        
        // İstatistikler
        public int totalGamesPlayed;
        public int totalWins;
        public int currentWinStreak;
        public int currentUndefeatedStreak;
        public int maxWinStreak;
        public int maxUndefeatedStreak;
        
        // Sosyal
        public string cityId;
        public string cityName;
        public List<string> friendIds;
        
        // Jokerler
        public Dictionary<JokerType, int> jokerCounts;
        
        // Muhafızlar
        public Dictionary<GuardianType, int> guardianCounts;
    }

    /// <summary>
    /// Oyun içi oyuncu durumu - maç sırasında kullanılır
    /// </summary>
    [Serializable]
    public class InGamePlayerData
    {
        public string playerId;
        public string displayName;
        public PlayerColor color;
        public int currentScore;
        public int castleHealth;       // Kale sağlığı (başlangıç: 3)
        public bool isEliminated;
        public bool isLocalPlayer;
        
        // Sahip olunan topraklar (territory ID listesi)
        public List<int> ownedTerritories;
        
        // Kale pozisyonu
        public int castleTerritoryId;
        
        // Bu oyunda kullanılabilir jokerler
        public Dictionary<JokerType, int> availableJokers;
        
        // Bu maçtaki istatistikler
        public int correctAnswers;
        public int wrongAnswers;
        public int territoriesCaptured;
        public int territoriesLost;
        public int towerDestroyed;

        public InGamePlayerData()
        {
            ownedTerritories = new List<int>();
            availableJokers = new Dictionary<JokerType, int>();
            castleHealth = 3;
        }

        /// <summary>
        /// Jokerin kullanılabilir olup olmadığını kontrol et
        /// </summary>
        public bool CanUseJoker(JokerType jokerType, int playerLevel)
        {
            // Seviye kontrolü
            int requiredLevel = GetRequiredLevelForJoker(jokerType);
            if (playerLevel < requiredLevel) return false;
            
            // Joker sayısı kontrolü
            return availableJokers.TryGetValue(jokerType, out int count) && count > 0;
        }

        public static int GetRequiredLevelForJoker(JokerType jokerType)
        {
            return jokerType switch
            {
                JokerType.Yuzde50 => 2,
                JokerType.Teleskop => 3,
                JokerType.SihirliKanatlar => 4,
                JokerType.OyuncularaSor => 6,
                JokerType.KategoriSecme => 7,
                JokerType.EkstraKoruma => 8,
                JokerType.Papagan => 9,
                _ => 99
            };
        }
    }

    /// <summary>
    /// Oyuncu cevap verisi - sunucuya gönderilir
    /// </summary>
    [Serializable]
    public class PlayerAnswerData
    {
        public string playerId;
        public string questionId;
        public int selectedAnswerIndex;    // Çoktan seçmeli için
        public float guessedValue;         // Tahmin sorusu için
        public float answerTime;           // Cevaplama süresi (saniye)
        public List<JokerType> usedJokers; // Kullanılan jokerler

        public PlayerAnswerData()
        {
            usedJokers = new List<JokerType>();
        }
    }

    /// <summary>
    /// Oyuncu sıralama verisi
    /// </summary>
    [Serializable]
    public class PlayerRankingData
    {
        public string playerId;
        public string displayName;
        public string avatarUrl;
        public int rank;
        public int totalTP;
        public int level;
        public string cityName;
    }

    /// <summary>
    /// TP hesaplama sonucu
    /// </summary>
    [Serializable]
    public class TPCalculationResult
    {
        public int baseTP;                    // Temel TP (1: 300, 2: 200, 3: 100)
        public int scorePercentageBonus;      // Puan yüzdesi bonusu
        public int opponentBonus;             // Rakip seviye bonusu
        public int winStreakBonus;            // Galibiyet serisi bonusu
        public int undefeatedBonus;           // Yenilgisizlik bonusu
        public int totalTP => baseTP + scorePercentageBonus + opponentBonus + winStreakBonus + undefeatedBonus;
    }
}
