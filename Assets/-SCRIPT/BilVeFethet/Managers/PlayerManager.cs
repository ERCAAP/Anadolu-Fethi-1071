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
    /// Oyuncu yöneticisi - yerel ve uzak oyuncu verilerini yönetir
    /// </summary>
    public class PlayerManager : Singleton<PlayerManager>
    {
        [Header("Local Player")]
        [SerializeField] private string localPlayerId;

        // Cached player data
        private PlayerData _localPlayerData;
        private Dictionary<string, PlayerData> _cachedPlayers;

        // Statistics tracking
        private int _sessionWinStreak;
        private int _sessionUndefeatedStreak;
        private int _gamesPlayedThisSession;

        // Properties
        public string LocalPlayerId => localPlayerId;
        public PlayerData LocalPlayerData => _localPlayerData;
        public int Level => _localPlayerData?.level ?? 1;
        public int TotalTP => _localPlayerData?.totalTP ?? 0;
        public int WeeklyTP => _localPlayerData?.weeklyTP ?? 0;
        public int GoldCoins => _localPlayerData?.goldCoins ?? 0;
        public int GameRights => _localPlayerData?.gameRights ?? 0;
        public int WinStreak => _sessionWinStreak;
        public int UndefeatedStreak => _sessionUndefeatedStreak;

        protected override void OnSingletonAwake()
        {
            _cachedPlayers = new Dictionary<string, PlayerData>();
        }

        private void OnEnable()
        {
            GameEvents.OnGameEnded += HandleGameEnded;
            GameEvents.OnTPEarned += HandleTPEarned;
            GameEvents.OnPlayerLevelUp += HandleLevelUp;
        }

        private void OnDisable()
        {
            GameEvents.OnGameEnded -= HandleGameEnded;
            GameEvents.OnTPEarned -= HandleTPEarned;
            GameEvents.OnPlayerLevelUp -= HandleLevelUp;
        }

        #region Initialization

        /// <summary>
        /// Yerel oyuncu ID'sini ayarla
        /// </summary>
        public void SetLocalPlayerId(string playerId)
        {
            localPlayerId = playerId;
        }

        /// <summary>
        /// Oyuncu verilerini yükle
        /// </summary>
        public async Task<bool> LoadPlayerDataAsync()
        {
            if (string.IsNullOrEmpty(localPlayerId))
            {
                Debug.LogError("[PlayerManager] Local player ID not set!");
                return false;
            }

            try
            {
                _localPlayerData = await NetworkManager.Instance.GetPlayerDataAsync(localPlayerId);
                
                if (_localPlayerData != null)
                {
                    // Seri bilgilerini yükle
                    _sessionWinStreak = _localPlayerData.currentWinStreak;
                    _sessionUndefeatedStreak = _localPlayerData.currentUndefeatedStreak;
                    
                    CachePlayer(_localPlayerData);
                    Debug.Log($"[PlayerManager] Player data loaded. Level: {_localPlayerData.level}, TP: {_localPlayerData.totalTP}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerManager] Failed to load player data: {ex.Message}");
            }

            return false;
        }

        #endregion

        #region Player Data Access

        /// <summary>
        /// Oyuncu verisini cache'e ekle
        /// </summary>
        public void CachePlayer(PlayerData player)
        {
            if (player == null || string.IsNullOrEmpty(player.playerId)) return;
            _cachedPlayers[player.playerId] = player;
        }

        /// <summary>
        /// Cache'den oyuncu verisi al
        /// </summary>
        public PlayerData GetCachedPlayer(string playerId)
        {
            return _cachedPlayers.TryGetValue(playerId, out var player) ? player : null;
        }

        /// <summary>
        /// Oyuncu verisini async olarak al (cache + network)
        /// </summary>
        public async Task<PlayerData> GetPlayerDataAsync(string playerId)
        {
            // Önce cache'e bak
            if (_cachedPlayers.TryGetValue(playerId, out var cached))
            {
                return cached;
            }

            // Network'ten al
            var player = await NetworkManager.Instance.GetPlayerDataAsync(playerId);
            if (player != null)
            {
                CachePlayer(player);
            }

            return player;
        }

        /// <summary>
        /// Yerel oyuncu mu kontrol et
        /// </summary>
        public bool IsLocalPlayer(string playerId)
        {
            return playerId == localPlayerId;
        }

        #endregion

        #region Joker Management

        /// <summary>
        /// Joker sayısını al
        /// </summary>
        public int GetJokerCount(JokerType jokerType)
        {
            if (_localPlayerData?.jokerCounts == null) return 0;
            return _localPlayerData.jokerCounts.TryGetValue(jokerType, out var count) ? count : 0;
        }

        /// <summary>
        /// Jokerin kullanılabilir olup olmadığını kontrol et
        /// </summary>
        public bool CanUseJoker(JokerType jokerType)
        {
            int requiredLevel = InGamePlayerData.GetRequiredLevelForJoker(jokerType);
            if (Level < requiredLevel) return false;
            return GetJokerCount(jokerType) > 0;
        }

        /// <summary>
        /// Joker kullan (sayıyı azalt)
        /// </summary>
        public bool UseJoker(JokerType jokerType)
        {
            if (!CanUseJoker(jokerType)) return false;

            _localPlayerData.jokerCounts[jokerType]--;
            return true;
        }

        /// <summary>
        /// Joker ekle
        /// </summary>
        public void AddJoker(JokerType jokerType, int count = 1)
        {
            if (_localPlayerData?.jokerCounts == null) return;

            if (!_localPlayerData.jokerCounts.ContainsKey(jokerType))
            {
                _localPlayerData.jokerCounts[jokerType] = 0;
            }

            _localPlayerData.jokerCounts[jokerType] += count;
        }

        #endregion

        #region Guardian Management

        /// <summary>
        /// Muhafız sayısını al
        /// </summary>
        public int GetGuardianCount(GuardianType guardianType)
        {
            if (_localPlayerData?.guardianCounts == null) return 0;
            return _localPlayerData.guardianCounts.TryGetValue(guardianType, out var count) ? count : 0;
        }

        /// <summary>
        /// Muhafız kullanılabilir mi
        /// </summary>
        public bool CanUseGuardian(GuardianType guardianType)
        {
            // Sıralı kullanım gerekli - önce Guardian1, sonra Guardian2...
            for (int i = 1; i < (int)guardianType; i++)
            {
                var prevGuardian = (GuardianType)i;
                if (GetGuardianCount(prevGuardian) > 0)
                {
                    return false; // Önce daha düşük muhafız kullanılmalı
                }
            }

            return GetGuardianCount(guardianType) > 0;
        }

        /// <summary>
        /// Muhafız kullan
        /// </summary>
        public bool UseGuardian(GuardianType guardianType)
        {
            if (!CanUseGuardian(guardianType)) return false;

            _localPlayerData.guardianCounts[guardianType]--;
            return true;
        }

        #endregion

        #region Game Rights

        /// <summary>
        /// Oyun hakkı kullan
        /// </summary>
        public bool UseGameRight()
        {
            if (_localPlayerData == null || _localPlayerData.gameRights <= 0)
                return false;

            _localPlayerData.gameRights--;
            return true;
        }

        /// <summary>
        /// Oyun hakkı ekle
        /// </summary>
        public void AddGameRight(int count = 1)
        {
            if (_localPlayerData == null) return;
            _localPlayerData.gameRights += count;
        }

        /// <summary>
        /// Sonraki ücretsiz oyun hakkı ne zaman
        /// </summary>
        public TimeSpan GetTimeToNextFreeGame()
        {
            if (_localPlayerData == null)
                return TimeSpan.FromHours(8);

            return _localPlayerData.nextFreeGameTime - DateTime.UtcNow;
        }

        /// <summary>
        /// Oyun hakkı dolu mu
        /// </summary>
        public bool HasFullGameRights()
        {
            return GameRights >= 5; // Başlangıç maksimum 5
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Oyun oynadı
        /// </summary>
        public void RecordGamePlayed()
        {
            _gamesPlayedThisSession++;
            _localPlayerData.totalGamesPlayed++;
        }

        /// <summary>
        /// Oyun kazandı
        /// </summary>
        public void RecordWin()
        {
            _sessionWinStreak++;
            _sessionUndefeatedStreak++;
            _localPlayerData.totalWins++;
            _localPlayerData.currentWinStreak = _sessionWinStreak;
            _localPlayerData.currentUndefeatedStreak = _sessionUndefeatedStreak;

            if (_sessionWinStreak > _localPlayerData.maxWinStreak)
            {
                _localPlayerData.maxWinStreak = _sessionWinStreak;
            }

            if (_sessionUndefeatedStreak > _localPlayerData.maxUndefeatedStreak)
            {
                _localPlayerData.maxUndefeatedStreak = _sessionUndefeatedStreak;
            }
        }

        /// <summary>
        /// Oyun kaybetti
        /// </summary>
        public void RecordLoss()
        {
            _sessionWinStreak = 0;
            _sessionUndefeatedStreak = 0;
            _localPlayerData.currentWinStreak = 0;
            _localPlayerData.currentUndefeatedStreak = 0;
        }

        /// <summary>
        /// İkinci veya üçüncü oldu (yenilgisizlik devam eder)
        /// </summary>
        public void RecordNotFirst(bool isSecond)
        {
            _sessionWinStreak = 0;
            _localPlayerData.currentWinStreak = 0;
            
            // Yenilgisizlik devam eder (sadece 3. olmak = kaybetmek)
            if (!isSecond)
            {
                _sessionUndefeatedStreak = 0;
                _localPlayerData.currentUndefeatedStreak = 0;
            }
            else
            {
                _sessionUndefeatedStreak++;
                _localPlayerData.currentUndefeatedStreak = _sessionUndefeatedStreak;
                
                if (_sessionUndefeatedStreak > _localPlayerData.maxUndefeatedStreak)
                {
                    _localPlayerData.maxUndefeatedStreak = _sessionUndefeatedStreak;
                }
            }
        }

        #endregion

        #region Level Calculation

        /// <summary>
        /// Seviye atlama için gereken TP
        /// </summary>
        public int GetTPForNextLevel()
        {
            return GetTPRequiredForLevel(Level + 1);
        }

        /// <summary>
        /// Belirli bir seviye için gereken toplam TP
        /// </summary>
        public static int GetTPRequiredForLevel(int level)
        {
            // Exponential growth formula
            return (int)(1000 * Math.Pow(1.5, level - 1));
        }

        /// <summary>
        /// Mevcut seviye ilerleme yüzdesi
        /// </summary>
        public float GetLevelProgressPercentage()
        {
            int currentLevelTP = GetTPRequiredForLevel(Level);
            int nextLevelTP = GetTPRequiredForLevel(Level + 1);
            int tpInCurrentLevel = TotalTP - currentLevelTP;
            int tpNeeded = nextLevelTP - currentLevelTP;

            return (float)tpInCurrentLevel / tpNeeded;
        }

        /// <summary>
        /// Seviye hesapla
        /// </summary>
        private void CheckLevelUp(int addedTP)
        {
            int oldLevel = Level;
            int newLevel = CalculateLevelFromTP(TotalTP);

            if (newLevel > oldLevel)
            {
                _localPlayerData.level = newLevel;
                GameEvents.TriggerPlayerLevelUp(oldLevel, newLevel);
                
                // Seviye atlayınca oyun hakları yenilenir
                _localPlayerData.gameRights = 5;
            }
        }

        /// <summary>
        /// TP'den seviye hesapla
        /// </summary>
        public static int CalculateLevelFromTP(int totalTP)
        {
            int level = 1;
            while (GetTPRequiredForLevel(level + 1) <= totalTP)
            {
                level++;
            }
            return level;
        }

        #endregion

        #region Event Handlers

        private void HandleGameEnded(List<GameEndPlayerResult> results)
        {
            var localResult = results.Find(r => r.playerId == localPlayerId);
            if (localResult == null) return;

            RecordGamePlayed();

            switch (localResult.finalRank)
            {
                case 1:
                    RecordWin();
                    break;
                case 2:
                    RecordNotFirst(true);
                    break;
                case 3:
                    RecordLoss();
                    break;
            }
        }

        private void HandleTPEarned(TPCalculationResult result)
        {
            if (_localPlayerData == null) return;

            int oldTP = _localPlayerData.totalTP;
            _localPlayerData.totalTP += result.totalTP;
            _localPlayerData.weeklyTP += result.totalTP;

            CheckLevelUp(result.totalTP);
        }

        private void HandleLevelUp(int oldLevel, int newLevel)
        {
            Debug.Log($"[PlayerManager] Level up! {oldLevel} -> {newLevel}");
            
            // Seviye atlama ödülleri burada verilebilir
        }

        #endregion

        /// <summary>
        /// Altın ekle
        /// </summary>
        public void AddGold(int amount)
        {
            if (_localPlayerData == null) return;
            _localPlayerData.goldCoins += amount;
        }

        /// <summary>
        /// Altın harca
        /// </summary>
        public bool SpendGold(int amount)
        {
            if (_localPlayerData == null || _localPlayerData.goldCoins < amount)
                return false;

            _localPlayerData.goldCoins -= amount;
            return true;
        }
    }
}
