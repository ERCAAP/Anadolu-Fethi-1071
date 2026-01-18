using System;
using System.Collections.Generic;
using BilVeFethet.Enums;
using UnityEngine;

namespace BilVeFethet.Data
{
    /// <summary>
    /// Toprak (bölge) verisi - harita üzerindeki 15 alan
    /// </summary>
    [Serializable]
    public class TerritoryData
    {
        public int territoryId;              // 0-14 arası benzersiz ID
        public string territoryName;         // Bölge adı (ör: Marmara, Ege)
        public TerritoryState state;
        public string ownerId;               // Sahip oyuncu ID'si (null = boş)
        public PlayerColor ownerColor;
        public int pointValue;               // Ele geçirildiğinde kazanılacak puan (200+)
        public bool isProtected;             // Ekstra koruma jokeri aktif mi
        public int protectionTurnsLeft;      // Kalan koruma turu
        
        // Komşu topraklar (saldırı için gerekli)
        public List<int> adjacentTerritoryIds;
        
        // Kale bilgisi (sadece state == Kale ise geçerli)
        public int castleHealth;             // Kalan sağlık (3, 2, 1)
        
        // Görsel pozisyon
        public Vector2 mapPosition;          // Harita üzerindeki pozisyon

        public TerritoryData()
        {
            adjacentTerritoryIds = new List<int>();
            pointValue = 200;
            castleHealth = 3;
        }

        /// <summary>
        /// Bu toprak belirtilen topraktan saldırılabilir mi?
        /// </summary>
        public bool CanBeAttackedFrom(int attackerTerritoryId)
        {
            return adjacentTerritoryIds.Contains(attackerTerritoryId);
        }

        /// <summary>
        /// Toprak boş mu?
        /// </summary>
        public bool IsEmpty => state == TerritoryState.Bos || string.IsNullOrEmpty(ownerId);
    }

    /// <summary>
    /// Türkiye haritası verisi - 15 bölge
    /// </summary>
    [Serializable]
    public class MapData
    {
        public List<TerritoryData> territories;
        
        // Kale pozisyonları (oyun başında rastgele atanır)
        public Dictionary<PlayerColor, int> castlePositions;

        public MapData()
        {
            territories = new List<TerritoryData>();
            castlePositions = new Dictionary<PlayerColor, int>();
            InitializeDefaultMap();
        }

        /// <summary>
        /// Varsayılan Türkiye haritasını oluştur
        /// </summary>
        private void InitializeDefaultMap()
        {
            // 15 bölge tanımla (Türkiye haritası bazlı)
            var regionNames = new string[]
            {
                "Marmara",           // 0
                "Ege",               // 1
                "Akdeniz",           // 2
                "İç Anadolu",        // 3
                "Karadeniz",         // 4
                "Doğu Anadolu",      // 5
                "Güneydoğu Anadolu", // 6
                "Trakya",            // 7
                "Batı Karadeniz",    // 8
                "Orta Karadeniz",    // 9
                "Doğu Karadeniz",    // 10
                "Batı Akdeniz",      // 11
                "Orta Akdeniz",      // 12
                "Kuzey İç Anadolu",  // 13
                "Güney İç Anadolu"   // 14
            };

            // Komşuluk ilişkileri (coğrafi olarak gerçekçi)
            var adjacencies = new int[][]
            {
                new int[] { 1, 3, 4, 7, 8 },        // 0: Marmara
                new int[] { 0, 2, 3, 11 },          // 1: Ege
                new int[] { 1, 6, 11, 12, 14 },     // 2: Akdeniz
                new int[] { 0, 1, 4, 5, 6, 13, 14 },// 3: İç Anadolu
                new int[] { 0, 3, 8, 9, 10 },       // 4: Karadeniz
                new int[] { 3, 6, 10, 13 },         // 5: Doğu Anadolu
                new int[] { 2, 3, 5, 12, 14 },      // 6: Güneydoğu Anadolu
                new int[] { 0, 8 },                 // 7: Trakya
                new int[] { 0, 4, 7, 9, 13 },       // 8: Batı Karadeniz
                new int[] { 4, 8, 10, 13 },         // 9: Orta Karadeniz
                new int[] { 4, 5, 9 },              // 10: Doğu Karadeniz
                new int[] { 1, 2, 12, 14 },         // 11: Batı Akdeniz
                new int[] { 2, 6, 11, 14 },         // 12: Orta Akdeniz
                new int[] { 3, 5, 8, 9 },           // 13: Kuzey İç Anadolu
                new int[] { 2, 3, 6, 11, 12 }       // 14: Güney İç Anadolu
            };

            // Harita pozisyonları (normalize edilmiş 0-1 arası)
            var positions = new Vector2[]
            {
                new Vector2(0.25f, 0.75f),   // 0: Marmara
                new Vector2(0.15f, 0.50f),   // 1: Ege
                new Vector2(0.35f, 0.20f),   // 2: Akdeniz
                new Vector2(0.45f, 0.50f),   // 3: İç Anadolu
                new Vector2(0.55f, 0.80f),   // 4: Karadeniz
                new Vector2(0.85f, 0.55f),   // 5: Doğu Anadolu
                new Vector2(0.75f, 0.25f),   // 6: Güneydoğu Anadolu
                new Vector2(0.10f, 0.85f),   // 7: Trakya
                new Vector2(0.35f, 0.80f),   // 8: Batı Karadeniz
                new Vector2(0.55f, 0.85f),   // 9: Orta Karadeniz
                new Vector2(0.80f, 0.80f),   // 10: Doğu Karadeniz
                new Vector2(0.20f, 0.30f),   // 11: Batı Akdeniz
                new Vector2(0.50f, 0.20f),   // 12: Orta Akdeniz
                new Vector2(0.55f, 0.65f),   // 13: Kuzey İç Anadolu
                new Vector2(0.50f, 0.35f)    // 14: Güney İç Anadolu
            };

            for (int i = 0; i < 15; i++)
            {
                var territory = new TerritoryData
                {
                    territoryId = i,
                    territoryName = regionNames[i],
                    state = TerritoryState.Bos,
                    ownerId = null,
                    pointValue = 200 + (i * 10),  // Değişen puan değerleri
                    adjacentTerritoryIds = new List<int>(adjacencies[i]),
                    mapPosition = positions[i]
                };
                territories.Add(territory);
            }
        }

        /// <summary>
        /// Belirtilen ID'ye sahip toprak verisini getir
        /// </summary>
        public TerritoryData GetTerritory(int territoryId)
        {
            if (territoryId < 0 || territoryId >= territories.Count)
                return null;
            return territories[territoryId];
        }

        /// <summary>
        /// Oyuncunun sahip olduğu tüm toprakları getir
        /// </summary>
        public List<TerritoryData> GetPlayerTerritories(string playerId)
        {
            return territories.FindAll(t => t.ownerId == playerId);
        }

        /// <summary>
        /// Boş toprakları getir
        /// </summary>
        public List<TerritoryData> GetEmptyTerritories()
        {
            return territories.FindAll(t => t.IsEmpty);
        }

        /// <summary>
        /// Oyuncunun saldırabileceği hedefleri getir
        /// </summary>
        public List<TerritoryData> GetAttackableTargets(string playerId, bool hasMagicWings = false)
        {
            var result = new List<TerritoryData>();
            var playerTerritories = GetPlayerTerritories(playerId);
            
            foreach (var territory in territories)
            {
                // Kendi toprağına saldıramazsın
                if (territory.ownerId == playerId)
                    continue;
                
                // Sihirli kanatlar varsa tüm düşman topraklarına saldırabilir
                if (hasMagicWings)
                {
                    if (!territory.IsEmpty)
                        result.Add(territory);
                    continue;
                }
                
                // Normal durumda komşu kontrolü
                foreach (var playerTerritory in playerTerritories)
                {
                    if (territory.CanBeAttackedFrom(playerTerritory.territoryId))
                    {
                        result.Add(territory);
                        break;
                    }
                }
            }
            
            return result;
        }
    }

    /// <summary>
    /// Toprak güncelleme verisi - network için optimize
    /// </summary>
    [Serializable]
    public class TerritoryUpdateData
    {
        public int territoryId;
        public string newOwnerId;
        public PlayerColor newOwnerColor;
        public TerritoryState newState;
        public int newCastleHealth;
        public bool isProtected;
        public int protectionTurnsLeft;
        
        // Bandwidth optimizasyonu: değişen alanları işaretle
        public byte changeFlags;
        
        public const byte FLAG_OWNER_CHANGED = 0x01;
        public const byte FLAG_STATE_CHANGED = 0x02;
        public const byte FLAG_CASTLE_HEALTH_CHANGED = 0x04;
        public const byte FLAG_PROTECTION_CHANGED = 0x08;
    }
}
