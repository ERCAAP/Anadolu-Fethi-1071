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
    /// Joker yöneticisi - tüm joker işlemlerini yönetir
    /// Seviye gereksinimlerini kontrol eder
    /// </summary>
    public class JokerManager : Singleton<JokerManager>
    {
        [Header("Joker Settings")]
        [SerializeField] private int protectionDuration = 2; // Ekstra koruma tur sayısı

        // In-game joker usage tracking
        private Dictionary<JokerType, int> _usedJokersThisGame;
        private List<JokerType> _usedJokersThisQuestion;

        // Joker descriptions (UI için)
        private static readonly Dictionary<JokerType, JokerInfo> JokerInfos = new Dictionary<JokerType, JokerInfo>
        {
            { JokerType.Yuzde50, new JokerInfo(2, "%50", "İki yanlış cevabı eler") },
            { JokerType.Teleskop, new JokerInfo(3, "Teleskop", "4 seçenek sunar") },
            { JokerType.SihirliKanatlar, new JokerInfo(4, "Sihirli Kanatlar", "Uzak topraklara saldır") },
            { JokerType.OyuncularaSor, new JokerInfo(6, "Oyunculara Sor", "Oyuncu oranlarını göster") },
            { JokerType.KategoriSecme, new JokerInfo(7, "Kategori Seçme", "Soru kategorisini seç") },
            { JokerType.EkstraKoruma, new JokerInfo(8, "Ekstra Koruma", "Kale veya kuleyi koru") },
            { JokerType.Papagan, new JokerInfo(9, "Papağan", "Tahmin sorularında ipucu") }
        };

        protected override void OnSingletonAwake()
        {
            _usedJokersThisGame = new Dictionary<JokerType, int>();
            _usedJokersThisQuestion = new List<JokerType>();
        }

        private void OnEnable()
        {
            GameEvents.OnGameStarting += HandleGameStarting;
            GameEvents.OnQuestionReceived += HandleQuestionReceived;
            GameEvents.OnJokerResultReceived += HandleJokerResult;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStarting -= HandleGameStarting;
            GameEvents.OnQuestionReceived -= HandleQuestionReceived;
            GameEvents.OnJokerResultReceived -= HandleJokerResult;
        }

        #region Initialization

        private void HandleGameStarting(GameStartData data)
        {
            _usedJokersThisGame.Clear();
            _usedJokersThisQuestion.Clear();
        }

        private void HandleQuestionReceived(QuestionData question)
        {
            _usedJokersThisQuestion.Clear();
        }

        #endregion

        #region Joker Queries

        /// <summary>
        /// Joker var mı?
        /// </summary>
        public bool HasJoker(JokerType jokerType)
        {
            return PlayerManager.Instance?.GetJokerCount(jokerType) > 0;
        }

        /// <summary>
        /// Joker kullanılabilir mi?
        /// </summary>
        public bool CanUseJoker(JokerType jokerType)
        {
            // Seviye kontrolü
            int playerLevel = PlayerManager.Instance?.Level ?? 1;
            int requiredLevel = GetRequiredLevel(jokerType);
            
            if (playerLevel < requiredLevel) return false;
            
            // Joker sayısı kontrolü
            return HasJoker(jokerType);
        }

        /// <summary>
        /// Bu soru için joker kullanılabilir mi?
        /// </summary>
        public bool CanUseJokerForQuestion(JokerType jokerType, QuestionType questionType)
        {
            if (!CanUseJoker(jokerType)) return false;
            if (_usedJokersThisQuestion.Contains(jokerType)) return false;

            return jokerType switch
            {
                JokerType.Yuzde50 => questionType == QuestionType.CoktanSecmeli,
                JokerType.OyuncularaSor => questionType == QuestionType.CoktanSecmeli,
                JokerType.Teleskop => questionType == QuestionType.CoktanSecmeli,
                JokerType.Papagan => questionType == QuestionType.Tahmin,
                _ => true
            };
        }

        /// <summary>
        /// Gerekli seviyeyi al
        /// </summary>
        public static int GetRequiredLevel(JokerType jokerType)
        {
            return JokerInfos.TryGetValue(jokerType, out var info) ? info.requiredLevel : 99;
        }

        /// <summary>
        /// Joker bilgisini al
        /// </summary>
        public static JokerInfo GetJokerInfo(JokerType jokerType)
        {
            return JokerInfos.TryGetValue(jokerType, out var info) ? info : null;
        }

        /// <summary>
        /// Kullanılabilir jokerleri al
        /// </summary>
        public List<JokerType> GetAvailableJokers()
        {
            var available = new List<JokerType>();
            
            foreach (JokerType joker in Enum.GetValues(typeof(JokerType)))
            {
                if (CanUseJoker(joker))
                {
                    available.Add(joker);
                }
            }

            return available;
        }

        /// <summary>
        /// Soru için kullanılabilir jokerleri al
        /// </summary>
        public List<JokerType> GetAvailableJokersForQuestion(QuestionType questionType)
        {
            var available = new List<JokerType>();
            
            foreach (JokerType joker in Enum.GetValues(typeof(JokerType)))
            {
                if (CanUseJokerForQuestion(joker, questionType))
                {
                    available.Add(joker);
                }
            }

            return available;
        }

        #endregion

        #region Joker Usage

        /// <summary>
        /// Joker kullan
        /// </summary>
        public bool UseJoker(JokerType jokerType)
        {
            if (!CanUseJoker(jokerType)) return false;

            bool used = PlayerManager.Instance?.UseJoker(jokerType) ?? false;
            
            if (used)
            {
                TrackJokerUsage(jokerType);
                GameEvents.TriggerJokerUsed(PlayerManager.Instance?.LocalPlayerId, jokerType);
            }

            return used;
        }

        /// <summary>
        /// Soru için joker kullan (async - sunucu sonucu bekle)
        /// </summary>
        public async Task<JokerUseResult> UseJokerAsync(JokerType jokerType)
        {
            if (!CanUseJoker(jokerType)) return null;

            var questionId = QuestionManager.Instance?.CurrentQuestion?.questionId;
            if (string.IsNullOrEmpty(questionId)) return null;

            bool used = PlayerManager.Instance?.UseJoker(jokerType) ?? false;
            if (!used) return null;

            TrackJokerUsage(jokerType);
            _usedJokersThisQuestion.Add(jokerType);

            var result = await NetworkManager.Instance.UseJokerAsync(jokerType, questionId);
            return result;
        }

        /// <summary>
        /// Joker kullanımını takip et
        /// </summary>
        private void TrackJokerUsage(JokerType jokerType)
        {
            if (!_usedJokersThisGame.ContainsKey(jokerType))
            {
                _usedJokersThisGame[jokerType] = 0;
            }
            _usedJokersThisGame[jokerType]++;
        }

        /// <summary>
        /// Joker sonucunu işle
        /// </summary>
        private void HandleJokerResult(JokerUseResult result)
        {
            if (!result.success)
            {
                Debug.LogWarning($"[JokerManager] Joker failed: {result.errorMessage}");
            }
        }

        #endregion

        #region Special Joker Actions

        /// <summary>
        /// Ekstra koruma jokeri kullan
        /// </summary>
        public bool UseExtraProtection(int territoryId)
        {
            if (!CanUseJoker(JokerType.EkstraKoruma)) return false;

            var territory = MapManager.Instance?.GetTerritory(territoryId);
            if (territory == null) return false;

            // Sadece kendi toprağına uygulayabilir
            if (territory.ownerId != PlayerManager.Instance?.LocalPlayerId) return false;

            if (UseJoker(JokerType.EkstraKoruma))
            {
                MapManager.Instance?.ProtectTerritory(territoryId, protectionDuration);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Kategori seçme jokeri kullan
        /// </summary>
        public async Task<List<QuestionCategory>> UseCategorySelection()
        {
            if (!CanUseJoker(JokerType.KategoriSecme)) return null;

            if (!UseJoker(JokerType.KategoriSecme)) return null;

            // Sunucudan mevcut kategorileri al
            var questionId = QuestionManager.Instance?.CurrentQuestion?.questionId ?? "";
            var result = await NetworkManager.Instance.UseJokerAsync(JokerType.KategoriSecme, questionId);

            return result?.availableCategories;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Bu oyunda kullanılan joker sayısı
        /// </summary>
        public int GetUsedJokerCount(JokerType jokerType)
        {
            return _usedJokersThisGame.TryGetValue(jokerType, out var count) ? count : 0;
        }

        /// <summary>
        /// Bu oyunda toplam kullanılan joker sayısı
        /// </summary>
        public int GetTotalUsedJokers()
        {
            int total = 0;
            foreach (var count in _usedJokersThisGame.Values)
            {
                total += count;
            }
            return total;
        }

        #endregion
    }

    /// <summary>
    /// Joker bilgi yapısı
    /// </summary>
    [Serializable]
    public class JokerInfo
    {
        public int requiredLevel;
        public string displayName;
        public string description;

        public JokerInfo(int level, string name, string desc)
        {
            requiredLevel = level;
            displayName = name;
            description = desc;
        }
    }
}
