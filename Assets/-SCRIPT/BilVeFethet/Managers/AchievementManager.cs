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
    /// Madalya ve başarı yöneticisi
    /// 6 farklı madalya türü ve 7 derece
    /// </summary>
    public class AchievementManager : Singleton<AchievementManager>
    {
        // Current tracking
        private int _currentWinStreak;
        private int _currentUndefeatedStreak;
        private int _weeklyTowerDestroyed;
        private int _weeklyMaxTPPerGame;
        private int _weeklyTotalTP;

        // Earned medals this session
        private List<EarnedMedal> _earnedMedals;

        // Medal thresholds
        private static readonly Dictionary<MedalType, int[]> MedalThresholds = new Dictionary<MedalType, int[]>
        {
            // Yenilmez - Galibiyet serisi
            { MedalType.Yenilmez, new[] { 5, 10, 20, 40, 60, 80, 100 } },
            
            // Çok Bilmiş - Yenilgisizlik serisi
            { MedalType.CokBilmis, new[] { 5, 10, 20, 40, 60, 80, 100 } },
            
            // Kule Düşmanı - Haftalık yıkılan kule
            { MedalType.KuleDusmani, new[] { 10, 20, 50, 100, 200, 500, 1000 } },
            
            // Bilge Kağan - Tek oyunda max TP
            { MedalType.BilgeKagan, new[] { 500, 750, 1000, 1250, 1500, 1750, 2000 } },
            
            // Tecrübe Canavarı - Haftalık toplam TP
            { MedalType.TecrubeCanavari, new[] { 5000, 10000, 20000, 50000, 100000, 200000, 500000 } },
            
            // Büyük Dahi - Haftalık sıralama yüzdesi (ters - düşük yüzde daha iyi)
            { MedalType.BuyukDahi, new[] { 50, 40, 30, 20, 10, 5, 1 } }
        };

        protected override void OnSingletonAwake()
        {
            _earnedMedals = new List<EarnedMedal>();
        }

        private void OnEnable()
        {
            GameEvents.OnGameEnded += HandleGameEnded;
            GameEvents.OnTPEarned += HandleTPEarned;
            GameEvents.OnTerritoryCaptured += HandleTerritoryCaptured;
            GameEvents.OnCastleDestroyed += HandleCastleDestroyed;
        }

        private void OnDisable()
        {
            GameEvents.OnGameEnded -= HandleGameEnded;
            GameEvents.OnTPEarned -= HandleTPEarned;
            GameEvents.OnTerritoryCaptured -= HandleTerritoryCaptured;
            GameEvents.OnCastleDestroyed -= HandleCastleDestroyed;
        }

        #region Medal Calculation

        /// <summary>
        /// Belirli değer için madalya derecesini hesapla
        /// </summary>
        public MedalGrade CalculateGrade(MedalType medalType, int value)
        {
            if (!MedalThresholds.TryGetValue(medalType, out var thresholds))
                return MedalGrade.None;

            // Büyük Dahi için ters mantık (düşük yüzde daha iyi)
            if (medalType == MedalType.BuyukDahi)
            {
                for (int i = thresholds.Length - 1; i >= 0; i--)
                {
                    if (value <= thresholds[i])
                        return (MedalGrade)(i + 1);
                }
                return MedalGrade.None;
            }

            // Diğer madalyalar için normal mantık
            for (int i = thresholds.Length - 1; i >= 0; i--)
            {
                if (value >= thresholds[i])
                    return (MedalGrade)(i + 1);
            }

            return MedalGrade.None;
        }

        /// <summary>
        /// Yenilmez madalya derecesi
        /// </summary>
        public MedalGrade GetYenilmezGrade()
        {
            return CalculateGrade(MedalType.Yenilmez, _currentWinStreak);
        }

        /// <summary>
        /// Çok Bilmiş madalya derecesi
        /// </summary>
        public MedalGrade GetCokBilmisGrade()
        {
            return CalculateGrade(MedalType.CokBilmis, _currentUndefeatedStreak);
        }

        /// <summary>
        /// Kule Düşmanı madalya derecesi
        /// </summary>
        public MedalGrade GetKuleDusmaniGrade()
        {
            return CalculateGrade(MedalType.KuleDusmani, _weeklyTowerDestroyed);
        }

        /// <summary>
        /// Bilge Kağan madalya derecesi
        /// </summary>
        public MedalGrade GetBilgeKaganGrade()
        {
            return CalculateGrade(MedalType.BilgeKagan, _weeklyMaxTPPerGame);
        }

        /// <summary>
        /// Tecrübe Canavarı madalya derecesi
        /// </summary>
        public MedalGrade GetTecrubeCanavariGrade()
        {
            return CalculateGrade(MedalType.TecrubeCanavari, _weeklyTotalTP);
        }

        /// <summary>
        /// Büyük Dahi madalya derecesi
        /// </summary>
        public MedalGrade GetBuyukDahiGrade(int weeklyRankPercentile)
        {
            return CalculateGrade(MedalType.BuyukDahi, weeklyRankPercentile);
        }

        #endregion

        #region Event Handlers

        private void HandleGameEnded(List<GameEndPlayerResult> results)
        {
            var localPlayerId = PlayerManager.Instance?.LocalPlayerId;
            var localResult = results.Find(r => r.playerId == localPlayerId);
            
            if (localResult == null) return;

            // Serileri güncelle
            if (localResult.finalRank == 1)
            {
                _currentWinStreak++;
                _currentUndefeatedStreak++;
            }
            else if (localResult.finalRank == 2)
            {
                _currentWinStreak = 0;
                _currentUndefeatedStreak++;
            }
            else
            {
                // Seri bozuldu - madalya ver
                AwardStreakMedals();
                _currentWinStreak = 0;
                _currentUndefeatedStreak = 0;
            }
        }

        private void HandleTPEarned(TPCalculationResult result)
        {
            _weeklyTotalTP += result.totalTP;

            if (result.totalTP > _weeklyMaxTPPerGame)
            {
                _weeklyMaxTPPerGame = result.totalTP;
            }
        }

        private void HandleTerritoryCaptured(int territoryId, string oldOwnerId, string newOwnerId)
        {
            // Kule yıkıldıysa sayacı artır
            var territory = MapManager.Instance?.GetTerritory(territoryId);
            if (territory != null && territory.state == TerritoryState.Normal)
            {
                if (newOwnerId == PlayerManager.Instance?.LocalPlayerId)
                {
                    _weeklyTowerDestroyed++;
                }
            }
        }

        private void HandleCastleDestroyed(string playerId)
        {
            // Kale yıkan bizim mi?
            if (BattleManager.Instance?.CurrentAttackerId == PlayerManager.Instance?.LocalPlayerId)
            {
                _weeklyTowerDestroyed += 3; // Kale = 3 kule değerinde
            }
        }

        #endregion

        #region Medal Awarding

        /// <summary>
        /// Seri madalyalarını ver
        /// </summary>
        private void AwardStreakMedals()
        {
            // Yenilmez
            var yenilmezGrade = GetYenilmezGrade();
            if (yenilmezGrade != MedalGrade.None)
            {
                AwardMedal(MedalType.Yenilmez, yenilmezGrade);
            }

            // Çok Bilmiş
            var cokBilmisGrade = GetCokBilmisGrade();
            if (cokBilmisGrade != MedalGrade.None)
            {
                AwardMedal(MedalType.CokBilmis, cokBilmisGrade);
            }
        }

        /// <summary>
        /// Haftalık madalyaları ver (hafta sonu çağrılır)
        /// </summary>
        public void AwardWeeklyMedals(int weeklyRankPercentile)
        {
            // Kule Düşmanı
            var kuleDusmaniGrade = GetKuleDusmaniGrade();
            if (kuleDusmaniGrade != MedalGrade.None)
            {
                AwardMedal(MedalType.KuleDusmani, kuleDusmaniGrade);
            }

            // Bilge Kağan
            var bilgeKaganGrade = GetBilgeKaganGrade();
            if (bilgeKaganGrade != MedalGrade.None)
            {
                AwardMedal(MedalType.BilgeKagan, bilgeKaganGrade);
            }

            // Tecrübe Canavarı (min 5000 TP gerekli)
            if (_weeklyTotalTP >= 5000)
            {
                var tecrubeGrade = GetTecrubeCanavariGrade();
                if (tecrubeGrade != MedalGrade.None)
                {
                    AwardMedal(MedalType.TecrubeCanavari, tecrubeGrade);
                }
            }

            // Büyük Dahi (min 5000 TP gerekli)
            if (_weeklyTotalTP >= 5000)
            {
                var buyukDahiGrade = GetBuyukDahiGrade(weeklyRankPercentile);
                if (buyukDahiGrade != MedalGrade.None)
                {
                    AwardMedal(MedalType.BuyukDahi, buyukDahiGrade);
                }
            }

            // Haftalık sayaçları sıfırla
            ResetWeeklyCounters();
        }

        /// <summary>
        /// Madalya ver
        /// </summary>
        private void AwardMedal(MedalType type, MedalGrade grade)
        {
            var medal = new EarnedMedal
            {
                medalType = type,
                grade = grade,
                earnedDate = DateTime.UtcNow
            };

            _earnedMedals.Add(medal);
            GameEvents.TriggerMedalEarned(type, grade);

            Debug.Log($"[AchievementManager] Medal earned: {type} - Grade {(int)grade}");
        }

        /// <summary>
        /// Haftalık sayaçları sıfırla
        /// </summary>
        public void ResetWeeklyCounters()
        {
            _weeklyTowerDestroyed = 0;
            _weeklyMaxTPPerGame = 0;
            _weeklyTotalTP = 0;
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Kazanılan madalyaları al
        /// </summary>
        public List<EarnedMedal> GetEarnedMedals()
        {
            return new List<EarnedMedal>(_earnedMedals);
        }

        /// <summary>
        /// Belirli türde kazanılan madalya sayısı
        /// </summary>
        public int GetMedalCount(MedalType type)
        {
            return _earnedMedals.FindAll(m => m.medalType == type).Count;
        }

        /// <summary>
        /// Belirli türde en yüksek derece
        /// </summary>
        public MedalGrade GetHighestGrade(MedalType type)
        {
            MedalGrade highest = MedalGrade.None;
            
            foreach (var medal in _earnedMedals)
            {
                if (medal.medalType == type && medal.grade > highest)
                {
                    highest = medal.grade;
                }
            }

            return highest;
        }

        /// <summary>
        /// Mevcut galibiyet serisini al
        /// </summary>
        public int GetCurrentWinStreak() => _currentWinStreak;

        /// <summary>
        /// Mevcut yenilgisizlik serisini al
        /// </summary>
        public int GetCurrentUndefeatedStreak() => _currentUndefeatedStreak;

        /// <summary>
        /// Sonraki derece için gereken değer
        /// </summary>
        public int GetValueForNextGrade(MedalType type, MedalGrade currentGrade)
        {
            if (!MedalThresholds.TryGetValue(type, out var thresholds))
                return int.MaxValue;

            int currentIndex = (int)currentGrade;
            if (currentIndex >= thresholds.Length)
                return int.MaxValue;

            return thresholds[currentIndex];
        }

        #endregion

        #region Display Helpers

        /// <summary>
        /// Madalya adını al
        /// </summary>
        public static string GetMedalName(MedalType type)
        {
            return type switch
            {
                MedalType.Yenilmez => "Yenilmez",
                MedalType.CokBilmis => "Çok Bilmiş",
                MedalType.KuleDusmani => "Kule Düşmanı",
                MedalType.BilgeKagan => "Bilge Kağan",
                MedalType.TecrubeCanavari => "Tecrübe Canavarı",
                MedalType.BuyukDahi => "Büyük Dahi",
                _ => "Bilinmeyen"
            };
        }

        /// <summary>
        /// Madalya açıklamasını al
        /// </summary>
        public static string GetMedalDescription(MedalType type)
        {
            return type switch
            {
                MedalType.Yenilmez => "Üst üste galibiyet serisi",
                MedalType.CokBilmis => "Yenilgisizlik serisi",
                MedalType.KuleDusmani => "Haftalık yıkılan kule sayısı",
                MedalType.BilgeKagan => "Tek oyunda en yüksek TP",
                MedalType.TecrubeCanavari => "Haftalık toplam TP",
                MedalType.BuyukDahi => "Haftalık sıralama yüzdesi",
                _ => ""
            };
        }

        #endregion
    }

    /// <summary>
    /// Kazanılan madalya verisi
    /// </summary>
    [Serializable]
    public class EarnedMedal
    {
        public MedalType medalType;
        public MedalGrade grade;
        public DateTime earnedDate;
    }
}
