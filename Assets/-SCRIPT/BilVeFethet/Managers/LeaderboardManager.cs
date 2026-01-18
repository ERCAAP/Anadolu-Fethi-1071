using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Leaderboard Manager - Sıralama tablosu yönetimi
    /// Ana sayfada gösterilecek liderlik tablosu
    /// </summary>
    public class LeaderboardManager : Singleton<LeaderboardManager>
    {
        [Header("Leaderboard Ayarları")]
        [SerializeField] private int shortListCount = 5;
        [SerializeField] private int longListCount = 100;
        [SerializeField] private float cacheExpirationTime = 300f; // 5 dakika
        
        // Events
        public event Action<List<LeaderboardEntry>> OnShortLeaderboardUpdated;
        public event Action<List<LeaderboardEntry>> OnLongLeaderboardUpdated;
        public event Action<List<LeaderboardEntry>> OnFriendsLeaderboardUpdated;
        public event Action<List<LeaderboardEntry>> OnCityLeaderboardUpdated;
        public event Action<int> OnPlayerRankUpdated; // Oyuncunun sıralaması
        public event Action<string> OnLeaderboardError;
        
        // Cached data
        private Dictionary<LeaderboardType, List<LeaderboardEntry>> cachedLeaderboards;
        private Dictionary<LeaderboardType, DateTime> cacheTimestamps;
        private int? cachedPlayerRank;
        
        // Leaderboard türleri
        public enum LeaderboardType
        {
            WeeklyShort,    // Kısa haftalık (ana sayfa)
            WeeklyLong,     // Uzun haftalık
            AllTimeShort,   // Kısa tüm zamanlar
            AllTimeLong,    // Uzun tüm zamanlar
            Friends,        // Arkadaş sıralaması
            City            // Şehir sıralaması
        }
        
        // Properties
        public int? PlayerRank => cachedPlayerRank;
        
        protected override void Awake()
        {
            base.Awake();
            cachedLeaderboards = new Dictionary<LeaderboardType, List<LeaderboardEntry>>();
            cacheTimestamps = new Dictionary<LeaderboardType, DateTime>();
        }
        
        private void Start()
        {
            // İlk yüklemede kısa listeyi al
            RefreshShortLeaderboard();
        }
        
        #region Public Methods
        
        /// <summary>
        /// Kısa sıralama tablosunu al (ana sayfa için)
        /// </summary>
        public async Task<List<LeaderboardEntry>> GetShortLeaderboardAsync(bool forceRefresh = false)
        {
            return await GetLeaderboardAsync(LeaderboardType.WeeklyShort, forceRefresh);
        }
        
        /// <summary>
        /// Uzun sıralama tablosunu al
        /// </summary>
        public async Task<List<LeaderboardEntry>> GetLongLeaderboardAsync(bool forceRefresh = false)
        {
            return await GetLeaderboardAsync(LeaderboardType.WeeklyLong, forceRefresh);
        }
        
        /// <summary>
        /// Arkadaş sıralamasını al
        /// </summary>
        public async Task<List<LeaderboardEntry>> GetFriendsLeaderboardAsync(bool forceRefresh = false)
        {
            return await GetLeaderboardAsync(LeaderboardType.Friends, forceRefresh);
        }
        
        /// <summary>
        /// Şehir sıralamasını al
        /// </summary>
        public async Task<List<LeaderboardEntry>> GetCityLeaderboardAsync(bool forceRefresh = false)
        {
            return await GetLeaderboardAsync(LeaderboardType.City, forceRefresh);
        }
        
        /// <summary>
        /// Tüm zamanların sıralamasını al
        /// </summary>
        public async Task<List<LeaderboardEntry>> GetAllTimeLeaderboardAsync(bool forceRefresh = false)
        {
            return await GetLeaderboardAsync(LeaderboardType.AllTimeLong, forceRefresh);
        }
        
        /// <summary>
        /// Kısa tabloyu yenile (event trigger ile)
        /// </summary>
        public void RefreshShortLeaderboard()
        {
            StartCoroutine(RefreshShortLeaderboardCoroutine());
        }
        
        /// <summary>
        /// Tüm önbelleği temizle
        /// </summary>
        public void ClearCache()
        {
            cachedLeaderboards.Clear();
            cacheTimestamps.Clear();
            cachedPlayerRank = null;
        }
        
        /// <summary>
        /// Belirli bir leaderboard önbelleğini temizle
        /// </summary>
        public void ClearCache(LeaderboardType type)
        {
            cachedLeaderboards.Remove(type);
            cacheTimestamps.Remove(type);
        }
        
        #endregion
        
        #region Internal Methods
        
        private async Task<List<LeaderboardEntry>> GetLeaderboardAsync(LeaderboardType type, bool forceRefresh)
        {
            // Cache kontrolü
            if (!forceRefresh && IsCacheValid(type))
            {
                return cachedLeaderboards[type];
            }
            
            // API'den veri al
            try
            {
                var entries = await FetchLeaderboardFromServer(type);
                
                // Cache'e kaydet
                cachedLeaderboards[type] = entries;
                cacheTimestamps[type] = DateTime.Now;
                
                // Event tetikle
                TriggerLeaderboardEvent(type, entries);
                
                return entries;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LeaderboardManager] Failed to fetch leaderboard: {e.Message}");
                OnLeaderboardError?.Invoke($"Sıralama yüklenemedi: {e.Message}");
                
                // Cache varsa döndür
                if (cachedLeaderboards.TryGetValue(type, out var cached))
                {
                    return cached;
                }
                
                return new List<LeaderboardEntry>();
            }
        }
        
        private bool IsCacheValid(LeaderboardType type)
        {
            if (!cachedLeaderboards.ContainsKey(type) || !cacheTimestamps.ContainsKey(type))
                return false;
                
            var elapsed = (DateTime.Now - cacheTimestamps[type]).TotalSeconds;
            return elapsed < cacheExpirationTime;
        }
        
        private void TriggerLeaderboardEvent(LeaderboardType type, List<LeaderboardEntry> entries)
        {
            switch (type)
            {
                case LeaderboardType.WeeklyShort:
                case LeaderboardType.AllTimeShort:
                    OnShortLeaderboardUpdated?.Invoke(entries);
                    break;
                case LeaderboardType.WeeklyLong:
                case LeaderboardType.AllTimeLong:
                    OnLongLeaderboardUpdated?.Invoke(entries);
                    break;
                case LeaderboardType.Friends:
                    OnFriendsLeaderboardUpdated?.Invoke(entries);
                    break;
                case LeaderboardType.City:
                    OnCityLeaderboardUpdated?.Invoke(entries);
                    break;
            }
        }
        
        private IEnumerator RefreshShortLeaderboardCoroutine()
        {
            var task = GetShortLeaderboardAsync(true);
            
            while (!task.IsCompleted)
            {
                yield return null;
            }
        }
        
        #endregion
        
        #region Server Communication (Simulated)
        
        /// <summary>
        /// Sunucudan sıralama verilerini al
        /// Gerçek implementasyonda NetworkManager kullanılacak
        /// </summary>
        private async Task<List<LeaderboardEntry>> FetchLeaderboardFromServer(LeaderboardType type)
        {
            // Simüle edilmiş gecikme
            await Task.Delay(500);
            
            int count = type switch
            {
                LeaderboardType.WeeklyShort or LeaderboardType.AllTimeShort => shortListCount,
                _ => longListCount
            };
            
            // Simüle edilmiş veri oluştur
            var entries = new List<LeaderboardEntry>();
            string localPlayerId = PlayerManager.Instance?.LocalPlayerId ?? "";
            
            for (int i = 0; i < count; i++)
            {
                var entry = new LeaderboardEntry
                {
                    rank = i + 1,
                    playerId = i == 7 ? localPlayerId : $"player_{i}",
                    playerName = GetRandomPlayerName(i),
                    avatarId = UnityEngine.Random.Range(1, 20),
                    score = 10000 - (i * 100) + UnityEngine.Random.Range(-50, 50),
                    level = UnityEngine.Random.Range(1, 50),
                    isLocalPlayer = i == 7 // 8. sırada yerel oyuncu
                };
                
                if (type == LeaderboardType.City)
                {
                    entry.cityName = GetRandomCityName();
                }
                
                entries.Add(entry);
            }
            
            // Oyuncunun sıralamasını güncelle
            var localEntry = entries.Find(e => e.isLocalPlayer);
            if (localEntry != null)
            {
                cachedPlayerRank = localEntry.rank;
                OnPlayerRankUpdated?.Invoke(localEntry.rank);
            }
            
            return entries;
        }
        
        private string GetRandomPlayerName(int index)
        {
            string[] names = { "Fatih", "Mehmet", "Ayşe", "Ali", "Zeynep", "Mustafa", "Elif", "Hakan", 
                              "Selin", "Burak", "Deniz", "Ceren", "Emre", "Gökhan", "Merve" };
            return names[index % names.Length] + UnityEngine.Random.Range(1, 999);
        }
        
        private string GetRandomCityName()
        {
            string[] cities = { "İstanbul", "Ankara", "İzmir", "Bursa", "Antalya", "Adana", "Konya", "Gaziantep" };
            return cities[UnityEngine.Random.Range(0, cities.Length)];
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Oyuncunun sıralamasını güncelle
        /// Oyun sonunda çağrılır
        /// </summary>
        public void UpdatePlayerRank(int newRank)
        {
            cachedPlayerRank = newRank;
            OnPlayerRankUpdated?.Invoke(newRank);
            
            // Önbelleği temizle (güncel veri için)
            ClearCache(LeaderboardType.WeeklyShort);
            ClearCache(LeaderboardType.WeeklyLong);
        }
        
        /// <summary>
        /// Çevrimiçi oyuncu sayısını al (simüle)
        /// </summary>
        public int GetOnlinePlayerCount()
        {
            // Gerçek implementasyonda sunucudan alınacak
            return UnityEngine.Random.Range(1000, 5000);
        }
        
        /// <summary>
        /// Haftalık sıralama bitiş tarihini al
        /// </summary>
        public DateTime GetWeeklyResetTime()
        {
            // Çarşamba sabahı 06:00
            var now = DateTime.Now;
            int daysUntilWednesday = ((int)DayOfWeek.Wednesday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilWednesday == 0 && now.Hour >= 6)
            {
                daysUntilWednesday = 7;
            }
            
            return now.Date.AddDays(daysUntilWednesday).AddHours(6);
        }
        
        /// <summary>
        /// Haftalık sıralamaya kalan süreyi al
        /// </summary>
        public TimeSpan GetTimeUntilWeeklyReset()
        {
            return GetWeeklyResetTime() - DateTime.Now;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Leaderboard entry data
    /// </summary>
    [Serializable]
    public class LeaderboardEntry
    {
        public int rank;
        public string playerId;
        public string playerName;
        public int avatarId;
        public int score;
        public int level;
        public string cityName;
        public bool isLocalPlayer;
        public int weeklyWins;
        public int weeklyGames;
        
        // Değişim göstergesi (önceki sıralamaya göre)
        public int rankChange; // +: yükseldi, -: düştü, 0: değişmedi
    }
}
