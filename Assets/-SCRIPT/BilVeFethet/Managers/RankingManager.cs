using System;
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
    /// Sıralama yöneticisi - tüm sıralama türlerini yönetir
    /// Haftalık (Çarşambadan Çarşambaya) ve genel sıralamalar
    /// </summary>
    public class RankingManager : Singleton<RankingManager>
    {
        [Header("Ranking Configuration")]
        [SerializeField] private int rankingPageSize = 20;
        [SerializeField] private float cacheExpireTime = 300f; // 5 dakika

        // Cached rankings
        private Dictionary<RankingType, List<PlayerRankingData>> _cachedRankings;
        private Dictionary<RankingType, DateTime> _cacheTimestamps;
        private Dictionary<RankingType, int> _localPlayerRanks;

        // Weekly data
        private DateTime _weekStartDate;
        private DateTime _weekEndDate;
        private int _weeklyTP;

        // Properties
        public int LocalWeeklyRank => _localPlayerRanks.TryGetValue(RankingType.Haftalik, out var rank) ? rank : 0;
        public int LocalOverallRank => _localPlayerRanks.TryGetValue(RankingType.Bireysel, out var rank) ? rank : 0;
        public DateTime WeekEndDate => _weekEndDate;

        protected override void OnSingletonAwake()
        {
            _cachedRankings = new Dictionary<RankingType, List<PlayerRankingData>>();
            _cacheTimestamps = new Dictionary<RankingType, DateTime>();
            _localPlayerRanks = new Dictionary<RankingType, int>();
        }

        private void OnEnable()
        {
            GameEvents.OnGameEnded += HandleGameEnded;
            GameEvents.OnTPEarned += HandleTPEarned;
        }

        private void OnDisable()
        {
            GameEvents.OnGameEnded -= HandleGameEnded;
            GameEvents.OnTPEarned -= HandleTPEarned;
        }

        #region Ranking Queries

        /// <summary>
        /// Sıralama verilerini al (cache veya network)
        /// </summary>
        public async Task<List<PlayerRankingData>> GetRankingAsync(RankingType type, int offset = 0, bool forceRefresh = false)
        {
            // Cache kontrol
            if (!forceRefresh && IsCacheValid(type))
            {
                return _cachedRankings[type];
            }

            // Network'ten al
            var rankings = await NetworkManager.Instance.GetRankingAsync(type, offset, rankingPageSize);
            
            if (rankings != null && rankings.Count > 0)
            {
                CacheRankings(type, rankings);
                UpdateLocalPlayerRank(type, rankings);
            }

            return rankings ?? new List<PlayerRankingData>();
        }

        /// <summary>
        /// Yerel oyuncunun sıralamasını al
        /// </summary>
        public async Task<int> GetLocalPlayerRankAsync(RankingType type)
        {
            if (_localPlayerRanks.TryGetValue(type, out var cachedRank) && cachedRank > 0)
            {
                return cachedRank;
            }

            // Sıralama verilerini çek, yerel oyuncuyu bul
            await GetRankingAsync(type);
            return _localPlayerRanks.TryGetValue(type, out var rank) ? rank : 0;
        }

        /// <summary>
        /// Çevre sıralamasını al (yerel oyuncu + 4 üst/alt)
        /// </summary>
        public async Task<List<PlayerRankingData>> GetSurroundingRankingAsync(RankingType type)
        {
            int localRank = await GetLocalPlayerRankAsync(type);
            if (localRank <= 0) return new List<PlayerRankingData>();

            int offset = Math.Max(0, localRank - 5);
            return await GetRankingAsync(type, offset);
        }

        #endregion

        #region Cache Management

        private bool IsCacheValid(RankingType type)
        {
            if (!_cacheTimestamps.TryGetValue(type, out var timestamp))
                return false;

            return (DateTime.UtcNow - timestamp).TotalSeconds < cacheExpireTime;
        }

        private void CacheRankings(RankingType type, List<PlayerRankingData> rankings)
        {
            _cachedRankings[type] = rankings;
            _cacheTimestamps[type] = DateTime.UtcNow;
        }

        private void UpdateLocalPlayerRank(RankingType type, List<PlayerRankingData> rankings)
        {
            var localPlayerId = PlayerManager.Instance?.LocalPlayerId;
            var localPlayer = rankings.Find(r => r.playerId == localPlayerId);
            
            if (localPlayer != null)
            {
                _localPlayerRanks[type] = localPlayer.rank;
                GameEvents.TriggerRankingUpdated(type, localPlayer.rank);
            }
        }

        /// <summary>
        /// Cache'i temizle
        /// </summary>
        public void ClearCache()
        {
            _cachedRankings.Clear();
            _cacheTimestamps.Clear();
        }

        /// <summary>
        /// Belirli türün cache'ini temizle
        /// </summary>
        public void ClearCache(RankingType type)
        {
            _cachedRankings.Remove(type);
            _cacheTimestamps.Remove(type);
        }

        #endregion

        #region Weekly Calculations

        /// <summary>
        /// Haftalık sıralama yüzdesini hesapla
        /// </summary>
        public float CalculateWeeklyPercentile()
        {
            int rank = LocalWeeklyRank;
            if (rank <= 0) return 0;

            // Yüzdelik hesaplama (tahmini toplam oyuncu sayısına göre)
            // Gerçek implementasyonda sunucudan total player count alınmalı
            int estimatedTotalPlayers = 100000;
            return (1 - ((float)rank / estimatedTotalPlayers)) * 100;
        }

        /// <summary>
        /// Haftalık bitiş süresini al
        /// </summary>
        public TimeSpan GetTimeToWeekEnd()
        {
            return _weekEndDate - DateTime.UtcNow;
        }

        /// <summary>
        /// Yeni hafta mı kontrol et
        /// </summary>
        public bool IsNewWeek()
        {
            return DateTime.UtcNow >= _weekEndDate;
        }

        /// <summary>
        /// Haftalık tarihleri güncelle
        /// </summary>
        public void UpdateWeekDates(DateTime startDate, DateTime endDate)
        {
            _weekStartDate = startDate;
            _weekEndDate = endDate;
        }

        #endregion

        #region Percentile Helpers

        /// <summary>
        /// Yüzdelik dilimine göre madalya derecesi
        /// </summary>
        public MedalGrade GetBuyukDahiGradeFromPercentile(float percentile)
        {
            if (percentile >= 99) return MedalGrade.Grade7;
            if (percentile >= 95) return MedalGrade.Grade6;
            if (percentile >= 90) return MedalGrade.Grade5;
            if (percentile >= 80) return MedalGrade.Grade4;
            if (percentile >= 70) return MedalGrade.Grade3;
            if (percentile >= 60) return MedalGrade.Grade2;
            if (percentile >= 50) return MedalGrade.Grade1;
            return MedalGrade.None;
        }

        #endregion

        #region Friend Rankings

        /// <summary>
        /// Arkadaş sıralamasını al
        /// </summary>
        public async Task<List<PlayerRankingData>> GetFriendRankingAsync()
        {
            var friendIds = PlayerManager.Instance?.LocalPlayerData?.friendIds;
            if (friendIds == null || friendIds.Count < 5) return new List<PlayerRankingData>();

            return await GetRankingAsync(RankingType.Arkadaslar);
        }

        /// <summary>
        /// Arkadaş sıralaması için minimum arkadaş sayısı
        /// </summary>
        public int MinFriendsForRanking => 5;

        /// <summary>
        /// Arkadaş sıralaması açık mı?
        /// </summary>
        public bool IsFriendRankingAvailable()
        {
            var friendCount = PlayerManager.Instance?.LocalPlayerData?.friendIds?.Count ?? 0;
            return friendCount >= MinFriendsForRanking;
        }

        #endregion

        #region City Rankings

        /// <summary>
        /// Şehir sıralamasını al
        /// </summary>
        public async Task<List<PlayerRankingData>> GetCityRankingAsync()
        {
            return await GetRankingAsync(RankingType.Sehir);
        }

        #endregion

        #region Event Handlers

        private void HandleGameEnded(List<GameEndPlayerResult> results)
        {
            // Sıralama cache'ini geçersiz kıl
            ClearCache();
        }

        private void HandleTPEarned(TPCalculationResult result)
        {
            _weeklyTP += result.totalTP;
            ClearCache(RankingType.Haftalik);
        }

        #endregion

        #region Leaderboard Display Helpers

        /// <summary>
        /// Sıralama gösterim metni (ör: "Top 5%")
        /// </summary>
        public string GetPercentileDisplayText(float percentile)
        {
            if (percentile >= 99) return "Top 1%";
            if (percentile >= 95) return "Top 5%";
            if (percentile >= 90) return "Top 10%";
            if (percentile >= 80) return "Top 20%";
            if (percentile >= 70) return "Top 30%";
            if (percentile >= 60) return "Top 40%";
            if (percentile >= 50) return "Top 50%";
            return $"Top {100 - Mathf.FloorToInt(percentile)}%";
        }

        /// <summary>
        /// Sıralama değişimi simgesi
        /// </summary>
        public string GetRankChangeSymbol(int previousRank, int currentRank)
        {
            if (currentRank < previousRank) return "↑";
            if (currentRank > previousRank) return "↓";
            return "-";
        }

        #endregion
    }
}
