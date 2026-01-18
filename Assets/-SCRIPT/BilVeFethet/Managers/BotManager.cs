using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Yapay zeka bot sistemi - Single player modunda botları yönetir
    /// </summary>
    public class BotManager : Singleton<BotManager>
    {
        [Header("Bot Ayarları")]
        [SerializeField] private float minAnswerDelay = 1.5f;
        [SerializeField] private float maxAnswerDelay = 8f;
        [SerializeField] private float territorySelectDelay = 1f;
        
        // Bot zorluk seviyeleri
        public enum BotDifficulty
        {
            Kolay,      // %40-50 doğru cevap
            Normal,     // %55-65 doğru cevap
            Zor,        // %70-80 doğru cevap
            Uzman       // %85-95 doğru cevap
        }
        
        [System.Serializable]
        public class BotPlayer
        {
            public string playerId;
            public string displayName;
            public PlayerColor color;
            public BotDifficulty difficulty;
            public int level;
            public int avatarId;
            
            // Bot davranış parametreleri
            public float correctAnswerChance;
            public float jokerUseChance;
            public float estimationAccuracy; // 0-1 arası, tahmin sorularında sapma oranı
            
            // In-game state
            public InGamePlayerData inGameData;
            
            public BotPlayer(string id, string name, PlayerColor color, BotDifficulty difficulty)
            {
                this.playerId = id;
                this.displayName = name;
                this.color = color;
                this.difficulty = difficulty;
                this.level = UnityEngine.Random.Range(1, 50);
                this.avatarId = UnityEngine.Random.Range(1, 20);
                
                SetDifficultyParams();
                InitInGameData();
            }
            
            private void SetDifficultyParams()
            {
                switch (difficulty)
                {
                    case BotDifficulty.Kolay:
                        correctAnswerChance = UnityEngine.Random.Range(0.40f, 0.50f);
                        jokerUseChance = 0.1f;
                        estimationAccuracy = 0.3f;
                        break;
                    case BotDifficulty.Normal:
                        correctAnswerChance = UnityEngine.Random.Range(0.55f, 0.65f);
                        jokerUseChance = 0.25f;
                        estimationAccuracy = 0.5f;
                        break;
                    case BotDifficulty.Zor:
                        correctAnswerChance = UnityEngine.Random.Range(0.70f, 0.80f);
                        jokerUseChance = 0.4f;
                        estimationAccuracy = 0.7f;
                        break;
                    case BotDifficulty.Uzman:
                        correctAnswerChance = UnityEngine.Random.Range(0.85f, 0.95f);
                        jokerUseChance = 0.6f;
                        estimationAccuracy = 0.9f;
                        break;
                }
            }
            
            private void InitInGameData()
            {
                inGameData = new InGamePlayerData
                {
                    playerId = playerId,
                    color = color,
                    currentScore = 0,
                    correctAnswers = 0,
                    wrongAnswers = 0,
                    ownedTerritories = new List<int>(),
                    isEliminated = false,
                    usedJokersThisGame = new List<JokerType>()
                };
            }
        }
        
        // Bot oyuncuları
        private List<BotPlayer> activeBots = new List<BotPlayer>();
        private bool isBotGameActive = false;
        
        // Bot isimleri havuzu
        private readonly string[] botNames = new string[]
        {
            "Fatih", "Mehmet", "Ayşe", "Zeynep", "Ali", "Mustafa",
            "Elif", "Hakan", "Selin", "Burak", "Deniz", "Ceren",
            "Emre", "Gökhan", "Merve", "Serkan", "Ebru", "Tolga",
            "Nazlı", "Onur", "Pınar", "Cem", "Sibel", "Kaan"
        };
        
        protected override void Awake()
        {
            base.Awake();
            SubscribeToEvents();
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        private void SubscribeToEvents()
        {
            GameEvents.OnQuestionReceived += HandleQuestionForBots;
            GameEvents.OnTerritorySelectionStarted += HandleTerritorySelectionForBots;
            GameEvents.OnAttackPhaseStarted += HandleAttackPhaseForBots;
            GameEvents.OnGameEnded += HandleGameEnded;
        }
        
        private void UnsubscribeFromEvents()
        {
            GameEvents.OnQuestionReceived -= HandleQuestionForBots;
            GameEvents.OnTerritorySelectionStarted -= HandleTerritorySelectionForBots;
            GameEvents.OnAttackPhaseStarted -= HandleAttackPhaseForBots;
            GameEvents.OnGameEnded -= HandleGameEnded;
        }
        
        #region Public Methods
        
        /// <summary>
        /// Bot oyunu başlat
        /// </summary>
        public void StartBotGame(BotDifficulty difficulty)
        {
            activeBots.Clear();
            isBotGameActive = true;
            
            // 2 bot oluştur (toplam 3 oyuncu için)
            PlayerColor[] botColors = { PlayerColor.Yesil, PlayerColor.Mavi };
            
            for (int i = 0; i < 2; i++)
            {
                string botId = $"bot_{Guid.NewGuid().ToString().Substring(0, 8)}";
                string botName = GetRandomBotName();
                
                var bot = new BotPlayer(botId, botName, botColors[i], difficulty);
                activeBots.Add(bot);
            }
            
            Debug.Log($"[BotManager] Bot oyunu başlatıldı. Zorluk: {difficulty}");
            
            // Oyun başlatma event'i tetikle
            GameEvents.TriggerBotGameStarted(activeBots.Select(b => b.inGameData).ToList());
        }
        
        /// <summary>
        /// Karma zorlukta bot oyunu başlat
        /// </summary>
        public void StartMixedBotGame()
        {
            activeBots.Clear();
            isBotGameActive = true;
            
            PlayerColor[] botColors = { PlayerColor.Yesil, PlayerColor.Mavi };
            BotDifficulty[] difficulties = { BotDifficulty.Normal, BotDifficulty.Zor };
            
            for (int i = 0; i < 2; i++)
            {
                string botId = $"bot_{Guid.NewGuid().ToString().Substring(0, 8)}";
                string botName = GetRandomBotName();
                
                var bot = new BotPlayer(botId, botName, botColors[i], difficulties[i]);
                activeBots.Add(bot);
            }
            
            Debug.Log("[BotManager] Karma zorluk bot oyunu başlatıldı");
            GameEvents.TriggerBotGameStarted(activeBots.Select(b => b.inGameData).ToList());
        }
        
        /// <summary>
        /// Bot oyununu sonlandır
        /// </summary>
        public void EndBotGame()
        {
            isBotGameActive = false;
            activeBots.Clear();
            StopAllCoroutines();
        }
        
        /// <summary>
        /// Belirli bir bot'un verisini al
        /// </summary>
        public BotPlayer GetBot(string playerId)
        {
            return activeBots.Find(b => b.playerId == playerId);
        }
        
        /// <summary>
        /// Tüm botları al
        /// </summary>
        public List<BotPlayer> GetAllBots()
        {
            return new List<BotPlayer>(activeBots);
        }
        
        /// <summary>
        /// Oyuncu ID'sinin bot olup olmadığını kontrol et
        /// </summary>
        public bool IsBot(string playerId)
        {
            return activeBots.Any(b => b.playerId == playerId);
        }
        
        #endregion
        
        #region Bot AI Logic
        
        private void HandleQuestionForBots(QuestionData question)
        {
            if (!isBotGameActive) return;
            
            foreach (var bot in activeBots)
            {
                if (!bot.inGameData.isEliminated)
                {
                    StartCoroutine(BotAnswerQuestion(bot, question));
                }
            }
        }
        
        private IEnumerator BotAnswerQuestion(BotPlayer bot, QuestionData question)
        {
            // Rastgele gecikme
            float delay = UnityEngine.Random.Range(minAnswerDelay, maxAnswerDelay);
            yield return new WaitForSeconds(delay);
            
            if (question.type == QuestionType.MultipleChoice)
            {
                // Çoktan seçmeli soru
                int selectedAnswer = GetBotMultipleChoiceAnswer(bot, question);
                
                // Joker kullanımı değerlendirmesi
                bool useJoker = ShouldBotUseJoker(bot, question);
                JokerType? jokerUsed = null;
                
                if (useJoker)
                {
                    jokerUsed = SelectBotJoker(bot, question);
                    if (jokerUsed.HasValue)
                    {
                        GameEvents.TriggerJokerUsed(bot.playerId, jokerUsed.Value);
                        bot.inGameData.usedJokersThisGame.Add(jokerUsed.Value);
                        
                        // Joker etkisine göre cevabı güncelle
                        if (jokerUsed.Value == JokerType.CiftSans)
                        {
                            // İkinci şans - iki cevap seç
                        }
                        else if (jokerUsed.Value == JokerType.YariYariya)
                        {
                            // Yarı yarıya - doğru cevap şansı artar
                            selectedAnswer = GetBotAnswerAfterFiftyFifty(bot, question);
                        }
                    }
                }
                
                var answerData = new PlayerAnswerData
                {
                    playerId = bot.playerId,
                    questionId = question.questionId,
                    selectedAnswerIndex = selectedAnswer,
                    answerTime = delay,
                    jokerUsed = jokerUsed
                };
                
                GameEvents.TriggerBotAnswerSubmitted(answerData);
            }
            else
            {
                // Tahmin sorusu
                int estimation = GetBotEstimationAnswer(bot, question);
                
                var answerData = new PlayerAnswerData
                {
                    playerId = bot.playerId,
                    questionId = question.questionId,
                    estimationAnswer = estimation,
                    answerTime = delay
                };
                
                GameEvents.TriggerBotAnswerSubmitted(answerData);
            }
        }
        
        private int GetBotMultipleChoiceAnswer(BotPlayer bot, QuestionData question)
        {
            bool willAnswerCorrectly = UnityEngine.Random.value < bot.correctAnswerChance;
            
            if (willAnswerCorrectly)
            {
                return question.correctAnswerIndex;
            }
            else
            {
                // Yanlış cevaplardan birini seç
                List<int> wrongAnswers = new List<int>();
                for (int i = 0; i < question.options.Length; i++)
                {
                    if (i != question.correctAnswerIndex)
                        wrongAnswers.Add(i);
                }
                return wrongAnswers[UnityEngine.Random.Range(0, wrongAnswers.Count)];
            }
        }
        
        private int GetBotAnswerAfterFiftyFifty(BotPlayer bot, QuestionData question)
        {
            // Yarı yarıya sonrası %70 doğru cevap şansı
            bool willAnswerCorrectly = UnityEngine.Random.value < 0.7f;
            
            if (willAnswerCorrectly)
            {
                return question.correctAnswerIndex;
            }
            else
            {
                // Kalan yanlış şık
                for (int i = 0; i < question.options.Length; i++)
                {
                    if (i != question.correctAnswerIndex)
                        return i;
                }
                return 0;
            }
        }
        
        private int GetBotEstimationAnswer(BotPlayer bot, QuestionData question)
        {
            int correctAnswer = question.correctEstimationAnswer;
            
            // Sapma hesapla - accuracy yüksekse sapma az olur
            float maxDeviation = correctAnswer * (1f - bot.estimationAccuracy);
            float deviation = UnityEngine.Random.Range(-maxDeviation, maxDeviation);
            
            return Mathf.Max(0, Mathf.RoundToInt(correctAnswer + deviation));
        }
        
        private bool ShouldBotUseJoker(BotPlayer bot, QuestionData question)
        {
            // Zaten bu oyunda çok joker kullandıysa kullanma
            if (bot.inGameData.usedJokersThisGame.Count >= 3)
                return false;
            
            return UnityEngine.Random.value < bot.jokerUseChance;
        }
        
        private JokerType? SelectBotJoker(BotPlayer bot, QuestionData question)
        {
            // Kullanılabilir jokerler
            List<JokerType> availableJokers = new List<JokerType>();
            
            // Çoktan seçmeli sorularda kullanılabilecek jokerler
            if (question.type == QuestionType.MultipleChoice)
            {
                if (!bot.inGameData.usedJokersThisGame.Contains(JokerType.YariYariya))
                    availableJokers.Add(JokerType.YariYariya);
                if (!bot.inGameData.usedJokersThisGame.Contains(JokerType.CiftSans))
                    availableJokers.Add(JokerType.CiftSans);
                if (!bot.inGameData.usedJokersThisGame.Contains(JokerType.SoruDegistir))
                    availableJokers.Add(JokerType.SoruDegistir);
            }
            
            // Tahmin sorularında
            if (question.type == QuestionType.Estimation)
            {
                if (!bot.inGameData.usedJokersThisGame.Contains(JokerType.DogruCevap))
                    availableJokers.Add(JokerType.DogruCevap);
            }
            
            if (availableJokers.Count == 0)
                return null;
            
            return availableJokers[UnityEngine.Random.Range(0, availableJokers.Count)];
        }
        
        #endregion
        
        #region Territory Selection AI
        
        private void HandleTerritorySelectionForBots(string currentPlayerId, List<int> availableTerritories)
        {
            if (!isBotGameActive) return;
            
            var bot = GetBot(currentPlayerId);
            if (bot != null)
            {
                StartCoroutine(BotSelectTerritory(bot, availableTerritories));
            }
        }
        
        private IEnumerator BotSelectTerritory(BotPlayer bot, List<int> availableTerritories)
        {
            yield return new WaitForSeconds(territorySelectDelay);
            
            if (availableTerritories.Count == 0) yield break;
            
            int selectedTerritory;
            
            // Strateji: Sahip olunan topraklara komşu olan toprakları tercih et
            List<int> adjacentTerritories = new List<int>();
            var mapData = new MapData();
            
            foreach (int territory in availableTerritories)
            {
                var territoryData = mapData.GetTerritory(territory);
                if (territoryData != null)
                {
                    foreach (int ownedTerritory in bot.inGameData.ownedTerritories)
                    {
                        if (territoryData.adjacentTerritories.Contains(ownedTerritory))
                        {
                            adjacentTerritories.Add(territory);
                            break;
                        }
                    }
                }
            }
            
            if (adjacentTerritories.Count > 0 && UnityEngine.Random.value > 0.3f)
            {
                // Komşu toprak seç
                selectedTerritory = adjacentTerritories[UnityEngine.Random.Range(0, adjacentTerritories.Count)];
            }
            else
            {
                // Rastgele seç
                selectedTerritory = availableTerritories[UnityEngine.Random.Range(0, availableTerritories.Count)];
            }
            
            GameEvents.TriggerBotTerritorySelected(bot.playerId, selectedTerritory);
        }
        
        #endregion
        
        #region Attack Phase AI
        
        private void HandleAttackPhaseForBots(string currentPlayerId, List<int> attackableTargets)
        {
            if (!isBotGameActive) return;
            
            var bot = GetBot(currentPlayerId);
            if (bot != null)
            {
                StartCoroutine(BotSelectAttackTarget(bot, attackableTargets));
            }
        }
        
        private IEnumerator BotSelectAttackTarget(BotPlayer bot, List<int> attackableTargets)
        {
            yield return new WaitForSeconds(territorySelectDelay);
            
            if (attackableTargets.Count == 0) yield break;
            
            // Strateji: Hasarlı kaleleri veya zayıf oyuncuları hedef al
            int selectedTarget = attackableTargets[UnityEngine.Random.Range(0, attackableTargets.Count)];
            
            // Sihirli Kanatlar joker kullanımı değerlendirmesi
            bool useMagicWings = false;
            if (!bot.inGameData.usedJokersThisGame.Contains(JokerType.SihirliKanatlar))
            {
                if (UnityEngine.Random.value < bot.jokerUseChance * 0.5f)
                {
                    useMagicWings = true;
                    bot.inGameData.usedJokersThisGame.Add(JokerType.SihirliKanatlar);
                    GameEvents.TriggerJokerUsed(bot.playerId, JokerType.SihirliKanatlar);
                }
            }
            
            GameEvents.TriggerBotAttackTargetSelected(bot.playerId, selectedTarget, useMagicWings);
        }
        
        #endregion
        
        #region Utility Methods
        
        private string GetRandomBotName()
        {
            List<string> availableNames = new List<string>(botNames);
            
            // Zaten kullanılan isimleri çıkar
            foreach (var bot in activeBots)
            {
                availableNames.Remove(bot.displayName);
            }
            
            if (availableNames.Count == 0)
                return $"Bot_{UnityEngine.Random.Range(1000, 9999)}";
            
            return availableNames[UnityEngine.Random.Range(0, availableNames.Count)];
        }
        
        private void HandleGameEnded(List<GameEndPlayerResult> results)
        {
            EndBotGame();
        }
        
        #endregion
    }
}
