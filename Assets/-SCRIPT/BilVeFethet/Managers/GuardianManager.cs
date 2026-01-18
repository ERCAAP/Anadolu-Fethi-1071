using System;
using System.Collections.Generic;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;
using UnityEngine;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Muhafız yöneticisi - galibiyet ve yenilgisizlik serilerini korur
    /// 5 muhafız: I, II, III, IV, V (sıralı kullanım gerektirir)
    /// </summary>
    public class GuardianManager : Singleton<GuardianManager>
    {
        [Header("Guardian Configuration")]
        [SerializeField] private int minWinStreakForGuardian = 2;
        [SerializeField] private int minUndefeatedStreakForGuardian = 3;

        // Guardian usage tracking
        private GuardianType? _lastUsedGuardian;
        private bool _guardianUsedThisGame;
        private int _protectedWinStreak;
        private int _protectedUndefeatedStreak;

        // Properties
        public bool GuardianUsedThisGame => _guardianUsedThisGame;
        public GuardianType? LastUsedGuardian => _lastUsedGuardian;

        private void OnEnable()
        {
            GameEvents.OnGameStarting += HandleGameStarting;
            GameEvents.OnGameEnded += HandleGameEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStarting -= HandleGameStarting;
            GameEvents.OnGameEnded -= HandleGameEnded;
        }

        #region Game Events

        private void HandleGameStarting(GameStartData data)
        {
            _guardianUsedThisGame = false;
        }

        private void HandleGameEnded(List<GameEndPlayerResult> results)
        {
            var localPlayerId = PlayerManager.Instance?.LocalPlayerId;
            var localResult = results.Find(r => r.playerId == localPlayerId);
            
            if (localResult == null) return;

            // Kaybettiyse ve muhafız kullanmadıysa
            if (localResult.finalRank == 3 && !_guardianUsedThisGame)
            {
                // Seri koruma teklifi için kontrol
                CheckGuardianOffer();
            }
        }

        #endregion

        #region Guardian Availability

        /// <summary>
        /// Muhafız kullanılabilir mi kontrol et
        /// </summary>
        public bool CanUseGuardian()
        {
            if (_guardianUsedThisGame) return false;

            // Minimum seri gereksinimi
            int winStreak = PlayerManager.Instance?.WinStreak ?? 0;
            int undefeatedStreak = PlayerManager.Instance?.UndefeatedStreak ?? 0;

            if (winStreak < minWinStreakForGuardian && undefeatedStreak < minUndefeatedStreakForGuardian)
            {
                return false;
            }

            // Sıradaki muhafız var mı?
            var nextGuardian = GetNextAvailableGuardian();
            return nextGuardian.HasValue;
        }

        /// <summary>
        /// Kullanılabilir sonraki muhafızı al
        /// </summary>
        public GuardianType? GetNextAvailableGuardian()
        {
            // Daha önce kullanıldıysa, sıradaki muhafız
            int startIndex = _lastUsedGuardian.HasValue ? (int)_lastUsedGuardian.Value : 0;

            for (int i = startIndex; i <= 5; i++)
            {
                var guardianType = (GuardianType)i;
                if (i == 0) guardianType = GuardianType.Guardian1;

                if (PlayerManager.Instance?.GetGuardianCount(guardianType) > 0)
                {
                    return guardianType;
                }
            }

            return null;
        }

        /// <summary>
        /// Belirli muhafız kullanılabilir mi?
        /// </summary>
        public bool CanUseSpecificGuardian(GuardianType guardianType)
        {
            if (_guardianUsedThisGame) return false;

            // Sıralı kullanım kontrolü
            var nextAvailable = GetNextAvailableGuardian();
            if (!nextAvailable.HasValue) return false;

            // Sadece sıradaki muhafız kullanılabilir
            return nextAvailable.Value == guardianType;
        }

        #endregion

        #region Guardian Usage

        /// <summary>
        /// Muhafız kullan
        /// </summary>
        public bool UseGuardian()
        {
            if (!CanUseGuardian()) return false;

            var guardianType = GetNextAvailableGuardian();
            if (!guardianType.HasValue) return false;

            return UseGuardian(guardianType.Value);
        }

        /// <summary>
        /// Belirli muhafızı kullan
        /// </summary>
        public bool UseGuardian(GuardianType guardianType)
        {
            if (!CanUseSpecificGuardian(guardianType)) return false;

            // Muhafızı kullan
            if (!PlayerManager.Instance.UseGuardian(guardianType)) return false;

            _guardianUsedThisGame = true;
            _lastUsedGuardian = guardianType;

            // Mevcut serileri kaydet (korunmuş olarak)
            _protectedWinStreak = PlayerManager.Instance.WinStreak;
            _protectedUndefeatedStreak = PlayerManager.Instance.UndefeatedStreak;

            Debug.Log($"[GuardianManager] Guardian {guardianType} used. Protected streaks: Win={_protectedWinStreak}, Undefeated={_protectedUndefeatedStreak}");

            return true;
        }

        /// <summary>
        /// Muhafız kullanım teklifini kontrol et
        /// </summary>
        private void CheckGuardianOffer()
        {
            if (CanUseGuardian())
            {
                // UI'a teklif göster event'i
                // OnGuardianOfferAvailable?.Invoke(GetNextAvailableGuardian().Value);
            }
        }

        #endregion

        #region Series Protection

        /// <summary>
        /// Serileri geri yükle (muhafız kullanıldıktan sonra)
        /// </summary>
        public void RestoreProtectedStreaks()
        {
            if (!_guardianUsedThisGame) return;

            // PlayerManager'daki serileri korunan değerlere geri yükle
            // Bu sunucu tarafında yapılmalı, burada sadece yerel state güncellenir
            
            Debug.Log($"[GuardianManager] Streaks restored. Win={_protectedWinStreak}, Undefeated={_protectedUndefeatedStreak}");
        }

        /// <summary>
        /// Muhafız seri korumasını sıfırla
        /// </summary>
        public void ResetGuardianProtection()
        {
            _lastUsedGuardian = null;
            _protectedWinStreak = 0;
            _protectedUndefeatedStreak = 0;
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Tüm muhafızların sayısını al
        /// </summary>
        public Dictionary<GuardianType, int> GetAllGuardianCounts()
        {
            var counts = new Dictionary<GuardianType, int>();
            
            foreach (GuardianType type in Enum.GetValues(typeof(GuardianType)))
            {
                counts[type] = PlayerManager.Instance?.GetGuardianCount(type) ?? 0;
            }

            return counts;
        }

        /// <summary>
        /// Toplam muhafız sayısı
        /// </summary>
        public int GetTotalGuardianCount()
        {
            int total = 0;
            foreach (GuardianType type in Enum.GetValues(typeof(GuardianType)))
            {
                total += PlayerManager.Instance?.GetGuardianCount(type) ?? 0;
            }
            return total;
        }

        /// <summary>
        /// Muhafız açıklamasını al
        /// </summary>
        public static string GetGuardianDescription()
        {
            return "Muhafızlar galibiyet ve yenilgisizlik serilerinizi korur. " +
                   "En az 2 galibiyet veya 3 yenilgisizlik seriniz olmalıdır. " +
                   "Muhafızlar sıralı kullanılır: önce I, sonra II, III, IV, V.";
        }

        /// <summary>
        /// Seri koruma bonusunu hesapla
        /// </summary>
        public int CalculateProtectedBonus()
        {
            int winStreakBonus = _protectedWinStreak * 100; // Her galibiyet için 100 TP
            int undefeatedBonus = _protectedUndefeatedStreak * 50; // Her yenilgisizlik için 50 TP
            
            return winStreakBonus + undefeatedBonus;
        }

        #endregion

        #region Guardian Purchase/Reward

        /// <summary>
        /// Muhafız ekle (ödül veya satın alma)
        /// </summary>
        public void AddGuardian(GuardianType guardianType, int count = 1)
        {
            var playerData = PlayerManager.Instance?.LocalPlayerData;
            if (playerData?.guardianCounts == null) return;

            if (!playerData.guardianCounts.ContainsKey(guardianType))
            {
                playerData.guardianCounts[guardianType] = 0;
            }

            playerData.guardianCounts[guardianType] += count;
        }

        #endregion
    }
}
