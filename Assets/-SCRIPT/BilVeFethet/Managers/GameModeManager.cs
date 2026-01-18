using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Oyun Modu Manager - Oyun modlarını ve başlatma işlemlerini yönetir
    /// </summary>
    public class GameModeManager : Singleton<GameModeManager>
    {
        [Header("Sahne Ayarları")]
        [SerializeField] private string gameSceneName = "GameScene";
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        
        [Header("Oyun Ayarları")]
        [SerializeField] private int requiredPlayers = 3;
        [SerializeField] private float gameStartDelay = 3f;
        
        // Oyun modları
        public enum GameMode
        {
            None,
            SinglePlayer,       // Botlara karşı
            QuickMatch,         // Hızlı eşleşme
            CustomLobby,        // Özel lobi
            Tournament,         // Turnuva (gelecek)
            Practice            // Pratik modu
        }
        
        // Events
        public event Action<GameMode> OnGameModeSelected;
        public event Action OnGameStarting;
        public event Action OnGameStarted;
        public event Action OnReturnToMainMenu;
        public event Action<string> OnGameModeError;
        
        // State
        private GameMode currentMode = GameMode.None;
        private BotManager.BotDifficulty selectedDifficulty = BotManager.BotDifficulty.Normal;
        private bool isGameInProgress = false;
        
        // Properties
        public GameMode CurrentMode => currentMode;
        public bool IsGameInProgress => isGameInProgress;
        public BotManager.BotDifficulty SelectedDifficulty => selectedDifficulty;
        
        protected override void Awake()
        {
            base.Awake();
        }
        
        private void Start()
        {
            SubscribeToEvents();
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        private void SubscribeToEvents()
        {
            GameEvents.OnGameFound += HandleGameFound;
            GameEvents.OnGameEnded += HandleGameEnded;
            GameEvents.OnBotGameStarted += HandleBotGameStarted;
        }
        
        private void UnsubscribeFromEvents()
        {
            GameEvents.OnGameFound -= HandleGameFound;
            GameEvents.OnGameEnded -= HandleGameEnded;
            GameEvents.OnBotGameStarted -= HandleBotGameStarted;
        }
        
        #region Public Methods - Oyun Modları
        
        /// <summary>
        /// Tek oyunculu oyun başlat (botlara karşı)
        /// </summary>
        public void StartSinglePlayerGame(BotManager.BotDifficulty difficulty = BotManager.BotDifficulty.Normal)
        {
            if (isGameInProgress)
            {
                OnGameModeError?.Invoke("Zaten bir oyun devam ediyor!");
                return;
            }
            
            currentMode = GameMode.SinglePlayer;
            selectedDifficulty = difficulty;
            
            Debug.Log($"[GameModeManager] Starting single player game with difficulty: {difficulty}");
            OnGameModeSelected?.Invoke(currentMode);
            
            StartCoroutine(StartSinglePlayerGameCoroutine());
        }
        
        /// <summary>
        /// Hızlı eşleşme başlat
        /// </summary>
        public async void StartQuickMatch()
        {
            if (isGameInProgress)
            {
                OnGameModeError?.Invoke("Zaten bir oyun devam ediyor!");
                return;
            }
            
            currentMode = GameMode.QuickMatch;
            Debug.Log("[GameModeManager] Starting quick match...");
            OnGameModeSelected?.Invoke(currentMode);
            
            await MatchmakingManager.Instance?.QuickMatchAsync();
        }
        
        /// <summary>
        /// Özel lobi ile oyun başlat
        /// </summary>
        public void StartCustomLobbyGame()
        {
            if (isGameInProgress)
            {
                OnGameModeError?.Invoke("Zaten bir oyun devam ediyor!");
                return;
            }
            
            currentMode = GameMode.CustomLobby;
            Debug.Log("[GameModeManager] Starting custom lobby game...");
            OnGameModeSelected?.Invoke(currentMode);
        }
        
        /// <summary>
        /// Pratik modu başlat (sınırsız sorulara cevap ver)
        /// </summary>
        public void StartPracticeMode()
        {
            if (isGameInProgress)
            {
                OnGameModeError?.Invoke("Zaten bir oyun devam ediyor!");
                return;
            }
            
            currentMode = GameMode.Practice;
            Debug.Log("[GameModeManager] Starting practice mode...");
            OnGameModeSelected?.Invoke(currentMode);
            
            StartCoroutine(StartPracticeModeCoroutine());
        }
        
        /// <summary>
        /// Mevcut oyunu sonlandır
        /// </summary>
        public void EndCurrentGame()
        {
            if (!isGameInProgress) return;
            
            Debug.Log("[GameModeManager] Ending current game...");
            
            // Botları temizle
            BotManager.Instance?.EndBotGame();
            
            // Matchmaking'den çık
            MatchmakingManager.Instance?.LeaveMatchmaking();

            // Lobiden çık
            LobbyManager.Instance?.LeaveLobby();
            
            isGameInProgress = false;
            currentMode = GameMode.None;
            
            OnReturnToMainMenu?.Invoke();
        }
        
        /// <summary>
        /// Ana menüye dön
        /// </summary>
        public void ReturnToMainMenu()
        {
            EndCurrentGame();
            
            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }
        
        #endregion
        
        #region Difficulty Selection
        
        /// <summary>
        /// Bot zorluğunu ayarla
        /// </summary>
        public void SetBotDifficulty(BotManager.BotDifficulty difficulty)
        {
            selectedDifficulty = difficulty;
            Debug.Log($"[GameModeManager] Bot difficulty set to: {difficulty}");
        }
        
        /// <summary>
        /// Kolay mod
        /// </summary>
        public void SetDifficultyEasy()
        {
            SetBotDifficulty(BotManager.BotDifficulty.Kolay);
        }
        
        /// <summary>
        /// Normal mod
        /// </summary>
        public void SetDifficultyNormal()
        {
            SetBotDifficulty(BotManager.BotDifficulty.Normal);
        }
        
        /// <summary>
        /// Zor mod
        /// </summary>
        public void SetDifficultyHard()
        {
            SetBotDifficulty(BotManager.BotDifficulty.Zor);
        }
        
        /// <summary>
        /// Uzman mod
        /// </summary>
        public void SetDifficultyExpert()
        {
            SetBotDifficulty(BotManager.BotDifficulty.Uzman);
        }
        
        #endregion
        
        #region Game State Queries
        
        /// <summary>
        /// Tek oyunculu mod mu?
        /// </summary>
        public bool IsSinglePlayer()
        {
            return currentMode == GameMode.SinglePlayer || currentMode == GameMode.Practice;
        }
        
        /// <summary>
        /// Çok oyunculu mod mu?
        /// </summary>
        public bool IsMultiplayer()
        {
            return currentMode == GameMode.QuickMatch || currentMode == GameMode.CustomLobby || currentMode == GameMode.Tournament;
        }
        
        /// <summary>
        /// Oyun hakkı kontrolü
        /// </summary>
        public bool HasGameRights()
        {
            // Pratik modunda oyun hakkı gerekmez
            if (currentMode == GameMode.Practice)
                return true;
                
            return (PlayerManager.Instance?.GameRights ?? 0) > 0;
        }
        
        /// <summary>
        /// Mevcut modun açıklamasını al
        /// </summary>
        public string GetModeDescription()
        {
            return currentMode switch
            {
                GameMode.SinglePlayer => $"Tek Oyunculu ({selectedDifficulty})",
                GameMode.QuickMatch => "Hızlı Eşleşme",
                GameMode.CustomLobby => "Özel Lobi",
                GameMode.Tournament => "Turnuva",
                GameMode.Practice => "Pratik Modu",
                _ => "Bilinmeyen"
            };
        }
        
        #endregion
        
        #region Internal Methods
        
        private IEnumerator StartSinglePlayerGameCoroutine()
        {
            OnGameStarting?.Invoke();
            
            yield return new WaitForSeconds(0.5f);
            
            // Bot oyununu başlat
            if (selectedDifficulty == BotManager.BotDifficulty.Normal || selectedDifficulty == BotManager.BotDifficulty.Zor)
            {
                BotManager.Instance?.StartMixedBotGame();
            }
            else
            {
                BotManager.Instance?.StartBotGame(selectedDifficulty);
            }
        }
        
        private IEnumerator StartPracticeModeCoroutine()
        {
            OnGameStarting?.Invoke();
            
            yield return new WaitForSeconds(0.5f);
            
            isGameInProgress = true;
            OnGameStarted?.Invoke();
            
            Debug.Log("[GameModeManager] Practice mode started!");
            
            // Pratik modunda sadece soru cevaplama olacak
            // QuestionManager üzerinden sorular gelecek
        }
        
        private IEnumerator LoadGameSceneCoroutine()
        {
            // Yükleme ekranı göster
            OnGameStarting?.Invoke();
            
            yield return new WaitForSeconds(gameStartDelay);
            
            // Oyun sahnesini yükle
            if (!string.IsNullOrEmpty(gameSceneName))
            {
                var asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
                asyncLoad.allowSceneActivation = false;
                
                while (asyncLoad.progress < 0.9f)
                {
                    yield return null;
                }
                
                asyncLoad.allowSceneActivation = true;
                
                yield return new WaitUntil(() => asyncLoad.isDone);
            }
            
            isGameInProgress = true;
            OnGameStarted?.Invoke();
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleGameFound(string gameId)
        {
            Debug.Log($"[GameModeManager] Game found: {gameId}");
            
            if (currentMode == GameMode.QuickMatch || currentMode == GameMode.CustomLobby)
            {
                StartCoroutine(LoadGameSceneCoroutine());
            }
        }
        
        private void HandleBotGameStarted(List<InGamePlayerData> bots)
        {
            Debug.Log($"[GameModeManager] Bot game started with {bots.Count} bots");
            
            isGameInProgress = true;
            
            // Oyun sahnesini yükle
            StartCoroutine(LoadGameSceneCoroutine());
        }
        
        private void HandleGameEnded(List<GameEndPlayerResult> results)
        {
            Debug.Log("[GameModeManager] Game ended");
            
            isGameInProgress = false;
            
            // İstatistikleri göster, sonra ana menüye dön seçeneği sun
            // UI tarafında gösterilecek
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Aktif oyuncu sayısını al
        /// </summary>
        public int GetActivePlayerCount()
        {
            if (!isGameInProgress) return 0;
            
            int count = 1; // Yerel oyuncu
            
            // Botlar
            var bots = BotManager.Instance?.GetAllBots();
            if (bots != null)
                count += bots.Count;
            
            // Multiplayer oyuncuları
            if (IsMultiplayer())
            {
                count = LobbyManager.Instance?.PlayerCount ?? count;
            }
            
            return count;
        }
        
        /// <summary>
        /// Oyun süresi (saniye)
        /// </summary>
        public float GetGameDuration()
        {
            // GameManager'dan alınacak
            return 0f;
        }
        
        #endregion
    }
}
