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
    /// Skor ve TP hesaplama yöneticisi
    /// Tüm puan hesaplamaları merkezi olarak buradan yapılır
    /// </summary>
    public class ScoreManager : Singleton<ScoreManager>
    {
        [Header("Score Configuration")]
        [SerializeField] private int baseFirstPlaceTP = 300;
        [SerializeField] private int baseSecondPlaceTP = 200;
        [SerializeField] private int baseThirdPlaceTP = 100;
        [SerializeField] private int maxWinStreakBonus = 1000;
        [SerializeField] private int maxUndefeatedBonus = 500;
        [SerializeField] private int winStreakBonusPerWin = 100;
        [SerializeField] private int undefeatedBonusPerGame = 50;

        // Current game scores
        private Dictionary<string, int> _playerScores;
        private Dictionary<string, int> _playerCorrectAnswers;
        private Dictionary<string, int> _playerWrongAnswers;
        private int _totalGamePoints;

        protected override void OnSingletonAwake()
        {
            _playerScores = new Dictionary<string, int>();
            _playerCorrectAnswers = new Dictionary<string, int>();
            _playerWrongAnswers = new Dictionary<string, int>();
        }

        private void OnEnable()
        {
            GameEvents.OnGameStarting += HandleGameStarting;
            GameEvents.OnQuestionResultReceived += HandleQuestionResult;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStarting -= HandleGameStarting;
            GameEvents.OnQuestionResultReceived -= HandleQuestionResult;
        }

        #region Score Management

        /// <summary>
        /// Oyun başladığında skorları sıfırla
        /// </summary>
        private void HandleGameStarting(GameStartData data)
        {
            ResetScores();

            foreach (var player in data.players)
            {
                _playerScores[player.playerId] = 0;
                _playerCorrectAnswers[player.playerId] = 0;
                _playerWrongAnswers[player.playerId] = 0;
            }
        }

        /// <summary>
        /// Oyuncu skorunu al
        /// </summary>
        public int GetScore(string playerId)
        {
            return _playerScores.TryGetValue(playerId, out var score) ? score : 0;
        }

        /// <summary>
        /// Tüm skorları al
        /// </summary>
        public Dictionary<string, int> GetAllScores()
        {
            return new Dictionary<string, int>(_playerScores);
        }

        /// <summary>
        /// Puan ekle
        /// </summary>
        public void AddScore(string playerId, int points)
        {
            if (string.IsNullOrEmpty(playerId)) return;

            if (!_playerScores.ContainsKey(playerId))
            {
                _playerScores[playerId] = 0;
            }

            int oldScore = _playerScores[playerId];
            _playerScores[playerId] += points;
            _totalGamePoints += points;

            // InGamePlayer'ı da güncelle
            var gameState = GameManager.Instance?.GameState;
            var player = gameState?.GetPlayer(playerId);
            if (player != null)
            {
                player.currentScore = _playerScores[playerId];
            }

            GameEvents.TriggerScoreChanged(playerId, oldScore, _playerScores[playerId]);
        }

        /// <summary>
        /// Puan çıkar
        /// </summary>
        public void SubtractScore(string playerId, int points)
        {
            AddScore(playerId, -points);
        }

        /// <summary>
        /// Doğru cevap kaydet
        /// </summary>
        public void RecordCorrectAnswer(string playerId)
        {
            if (!_playerCorrectAnswers.ContainsKey(playerId))
            {
                _playerCorrectAnswers[playerId] = 0;
            }
            _playerCorrectAnswers[playerId]++;

            var gameState = GameManager.Instance?.GameState;
            var player = gameState?.GetPlayer(playerId);
            if (player != null)
            {
                player.correctAnswers++;
            }
        }

        /// <summary>
        /// Yanlış cevap kaydet
        /// </summary>
        public void RecordWrongAnswer(string playerId)
        {
            if (!_playerWrongAnswers.ContainsKey(playerId))
            {
                _playerWrongAnswers[playerId] = 0;
            }
            _playerWrongAnswers[playerId]++;

            var gameState = GameManager.Instance?.GameState;
            var player = gameState?.GetPlayer(playerId);
            if (player != null)
            {
                player.wrongAnswers++;
            }
        }

        /// <summary>
        /// Skorları sıfırla
        /// </summary>
        public void ResetScores()
        {
            _playerScores.Clear();
            _playerCorrectAnswers.Clear();
            _playerWrongAnswers.Clear();
            _totalGamePoints = 0;
        }

        #endregion

        #region Question Result Handling

        private void HandleQuestionResult(QuestionResultData result)
        {
            foreach (var playerResult in result.playerResults)
            {
                if (playerResult.earnedPoints > 0)
                {
                    AddScore(playerResult.playerId, playerResult.earnedPoints);
                }

                if (playerResult.isCorrect)
                {
                    RecordCorrectAnswer(playerResult.playerId);
                }
                else
                {
                    RecordWrongAnswer(playerResult.playerId);
                }
            }
        }

        #endregion

        #region TP Calculation

        /// <summary>
        /// Oyun sonu TP hesapla
        /// </summary>
        public TPCalculationResult CalculateTP(int finalRank, float scorePercentage, 
            int opponentAverageLevel, int winStreak, int undefeatedStreak)
        {
            var result = new TPCalculationResult();

            // 1. Temel TP (sıralamaya göre)
            result.baseTP = finalRank switch
            {
                1 => baseFirstPlaceTP,
                2 => baseSecondPlaceTP,
                3 => baseThirdPlaceTP,
                _ => 0
            };

            // 2. Puan Yüzdesi Bonusu
            // Temel TP'nin puan yüzdesi kadarı
            result.scorePercentageBonus = Mathf.RoundToInt(result.baseTP * scorePercentage);

            // 3. Rakip Seviye Bonusu
            // Rakiplerin ortalama seviyesi oranında temel TP'den bonus
            result.opponentBonus = Mathf.RoundToInt(result.baseTP * (opponentAverageLevel / 100f));

            // 4. Galibiyet Serisi Bonusu (sadece 1. için)
            if (finalRank == 1)
            {
                result.winStreakBonus = Mathf.Min(winStreak * winStreakBonusPerWin, maxWinStreakBonus);
            }

            // 5. Yenilgisizlik Serisi Bonusu (1. ve 2. için)
            if (finalRank <= 2)
            {
                result.undefeatedBonus = Mathf.Min(undefeatedStreak * undefeatedBonusPerGame, maxUndefeatedBonus);
            }

            return result;
        }

        /// <summary>
        /// Oyuncu için TP hesapla
        /// </summary>
        public TPCalculationResult CalculateTPForPlayer(string playerId, int finalRank, List<InGamePlayerData> allPlayers)
        {
            // Puan yüzdesi hesapla
            int playerScore = GetScore(playerId);
            float scorePercentage = _totalGamePoints > 0 ? (float)playerScore / _totalGamePoints : 0;

            // Rakip ortalama seviyesi
            int totalOpponentLevel = 0;
            int opponentCount = 0;
            
            foreach (var player in allPlayers)
            {
                if (player.playerId != playerId)
                {
                    var playerData = PlayerManager.Instance?.GetCachedPlayer(player.playerId);
                    if (playerData != null)
                    {
                        totalOpponentLevel += playerData.level;
                        opponentCount++;
                    }
                }
            }
            
            int averageOpponentLevel = opponentCount > 0 ? totalOpponentLevel / opponentCount : 10;

            // Seri bilgileri
            int winStreak = PlayerManager.Instance?.WinStreak ?? 0;
            int undefeatedStreak = PlayerManager.Instance?.UndefeatedStreak ?? 0;

            var result = CalculateTP(finalRank, scorePercentage, averageOpponentLevel, winStreak, undefeatedStreak);

            // Event tetikle
            if (playerId == PlayerManager.Instance?.LocalPlayerId)
            {
                GameEvents.TriggerTPEarned(result);
            }

            return result;
        }

        #endregion

        #region Score Calculations

        /// <summary>
        /// Toprak ele geçirme puanı hesapla
        /// </summary>
        public int CalculateTerritoryPoints(int baseTerritoryValue, bool isCastle)
        {
            if (isCastle)
            {
                // Kale yıkıldığında tüm toprakların puanı devredilir
                // Bu ayrı hesaplanır
                return 0;
            }

            return baseTerritoryValue;
        }

        /// <summary>
        /// Savunma başarı puanı
        /// </summary>
        public int GetDefenseSuccessPoints()
        {
            return GameManager.Instance?.Config.defenseBonus ?? 100;
        }

        /// <summary>
        /// Soru hızı bonusu hesapla
        /// </summary>
        public int CalculateSpeedBonus(float answerTime, float maxTime)
        {
            if (answerTime >= maxTime) return 0;

            // Kalan süre yüzdesi kadar bonus (max 50 puan)
            float remainingPercentage = 1 - (answerTime / maxTime);
            return Mathf.RoundToInt(50 * remainingPercentage);
        }

        /// <summary>
        /// Tahmin sorusu için doğruluk puanı hesapla
        /// </summary>
        public int CalculateEstimationPoints(float guessedValue, float correctValue, float tolerance)
        {
            float difference = Mathf.Abs(guessedValue - correctValue);
            float accuracy = 1 - Mathf.Min(difference / correctValue, 1);

            if (accuracy >= 0.95f) return 100;  // Çok yakın
            if (accuracy >= 0.9f) return 80;
            if (accuracy >= 0.8f) return 60;
            if (accuracy >= 0.7f) return 40;
            if (accuracy >= 0.5f) return 20;
            return 0;
        }

        #endregion

        #region Ranking Helpers

        /// <summary>
        /// Oyuncuları puana göre sırala
        /// </summary>
        public List<string> GetPlayerRanking()
        {
            var sortedPlayers = new List<KeyValuePair<string, int>>(_playerScores);
            sortedPlayers.Sort((a, b) => b.Value.CompareTo(a.Value));

            var ranking = new List<string>();
            foreach (var pair in sortedPlayers)
            {
                ranking.Add(pair.Key);
            }

            return ranking;
        }

        /// <summary>
        /// Oyuncunun sırasını al
        /// </summary>
        public int GetPlayerRank(string playerId)
        {
            var ranking = GetPlayerRanking();
            return ranking.IndexOf(playerId) + 1;
        }

        /// <summary>
        /// Lider oyuncuyu al
        /// </summary>
        public string GetLeadingPlayer()
        {
            var ranking = GetPlayerRanking();
            return ranking.Count > 0 ? ranking[0] : null;
        }

        /// <summary>
        /// Puan farkını al
        /// </summary>
        public int GetScoreDifference(string playerId1, string playerId2)
        {
            int score1 = GetScore(playerId1);
            int score2 = GetScore(playerId2);
            return score1 - score2;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Doğru cevap sayısı
        /// </summary>
        public int GetCorrectAnswers(string playerId)
        {
            return _playerCorrectAnswers.TryGetValue(playerId, out var count) ? count : 0;
        }

        /// <summary>
        /// Yanlış cevap sayısı
        /// </summary>
        public int GetWrongAnswers(string playerId)
        {
            return _playerWrongAnswers.TryGetValue(playerId, out var count) ? count : 0;
        }

        /// <summary>
        /// Doğru cevap yüzdesi
        /// </summary>
        public float GetAccuracyPercentage(string playerId)
        {
            int correct = GetCorrectAnswers(playerId);
            int wrong = GetWrongAnswers(playerId);
            int total = correct + wrong;

            return total > 0 ? (float)correct / total : 0;
        }

        #endregion
    }
}
