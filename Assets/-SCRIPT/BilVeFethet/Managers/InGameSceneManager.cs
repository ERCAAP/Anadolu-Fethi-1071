using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;
using BilVeFethet.UI;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Oyun İçi Sahne Yöneticisi
    /// Tüm oyun akışını koordine eder: Profil -> Lobby -> Oyun -> Sonuç
    /// </summary>
    public class InGameSceneManager : Singleton<InGameSceneManager>
    {
        [Header("Sahne İsimleri")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string gameScene = "Game";
        [SerializeField] private string lobbyScene = "Lobby";

        [Header("UI Referansları")]
        [SerializeField] private PlayerTopBar playerTopBar;
        [SerializeField] private GameObject loadingScreen;
        [SerializeField] private UnityEngine.UI.Slider loadingProgressBar;
        [SerializeField] private TMPro.TextMeshProUGUI loadingText;

        [Header("Oyun Ayarları")]
        [SerializeField] private int maxPlayers = 3;
        [SerializeField] private int questionsPerRound = 4;
        [SerializeField] private int totalRounds = 4;
        [SerializeField] private float matchmakingTimeout = 30f;

        // Game State
        private GameMode _currentGameMode;
        private List<InGamePlayerData> _players;
        private bool _isGameStarted = false;
        private bool _isInitialized = false;

        // Events
        public event Action OnGameSceneReady;
        public event Action<List<InGamePlayerData>> OnPlayersReady;
        public event Action<GameEndResult> OnGameCompleted;

        // Properties
        public List<InGamePlayerData> Players => _players;
        public bool IsGameStarted => _isGameStarted;
        public GameMode CurrentGameMode => _currentGameMode;

        protected override void OnSingletonAwake()
        {
            _players = new List<InGamePlayerData>();
        }

        private void Start()
        {
            InitializeScene();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            GameEvents.OnGameEnded += HandleGameEnded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            GameEvents.OnGameEnded -= HandleGameEnded;
        }

        #region Public Methods

        /// <summary>
        /// Tek oyunculu oyun başlat (bot'larla)
        /// </summary>
        public void StartSinglePlayerGame()
        {
            _currentGameMode = GameMode.SinglePlayer;
            StartCoroutine(StartSinglePlayerFlow());
        }

        /// <summary>
        /// Çok oyunculu oyun başlat (matchmaking)
        /// </summary>
        public void StartMultiplayerGame()
        {
            _currentGameMode = GameMode.Multiplayer;
            StartCoroutine(StartMultiplayerFlow());
        }

        /// <summary>
        /// Arkadaşlarla oyun başlat
        /// </summary>
        public void StartFriendGame(List<string> friendIds)
        {
            _currentGameMode = GameMode.FriendMatch;
            StartCoroutine(StartFriendGameFlow(friendIds));
        }

        /// <summary>
        /// Ana menüye dön
        /// </summary>
        public void ReturnToMainMenu()
        {
            CleanupGame();
            SceneManager.LoadScene(mainMenuScene);
        }

        /// <summary>
        /// Oyunu yeniden başlat (aynı mod)
        /// </summary>
        public void RestartGame()
        {
            CleanupGame();

            switch (_currentGameMode)
            {
                case GameMode.SinglePlayer:
                    StartSinglePlayerGame();
                    break;
                case GameMode.Multiplayer:
                    StartMultiplayerGame();
                    break;
                case GameMode.FriendMatch:
                    // TODO: Arkadaş listesini sakla ve tekrar başlat
                    StartSinglePlayerGame();
                    break;
            }
        }

        #endregion

        #region Game Flow Coroutines

        private IEnumerator StartSinglePlayerFlow()
        {
            ShowLoading("Oyun Hazırlanıyor...");

            // 1. Profil kontrolü (opsiyonel - offline da çalışabilir)
            yield return StartCoroutine(EnsureProfileLoaded());

            UpdateLoadingProgress(0.2f);

            // 2. Oyun hakkı kontrolü (tek oyunculu modda offline çalışabilir)
            // Not: Profil yüklü değilse veya offline modda oyun hakkı kontrolü yapılmaz
            bool hasGameRights = true;
            if (ProfileManager.Instance != null && ProfileManager.Instance.IsProfileLoaded)
            {
                hasGameRights = ProfileManager.Instance.GameRights > 0;
                if (!hasGameRights)
                {
                    Debug.LogWarning("[InGameSceneManager] No game rights, but allowing single player game");
                    // Tek oyunculu modda oyun hakkı olmasa da devam et (demo amaçlı)
                }
            }

            // 3. Oyun hakkı kullan (sadece profil yüklüyse ve online ise)
            // Not: Offline/demo modda bu adımı atlıyoruz
            if (ProfileManager.Instance != null && ProfileManager.Instance.IsProfileLoaded && hasGameRights)
            {
                // Sunucu çağrısı başarısız olsa bile devam et
                yield return StartCoroutine(UseGameRightCoroutine());
            }

            UpdateLoadingProgress(0.4f);

            // 4. Bot'ları oluştur
            _players = CreateBotPlayers();

            // 5. Yerel oyuncuyu ekle
            var localPlayer = CreateLocalPlayer();
            _players.Insert(0, localPlayer);

            UpdateLoadingProgress(0.6f);

            // 6. Soruları yükle
            yield return StartCoroutine(LoadQuestions());

            UpdateLoadingProgress(0.8f);

            // 7. UI'ı başlat
            InitializeGameUI();

            UpdateLoadingProgress(1f);
            yield return new WaitForSeconds(0.5f);

            HideLoading();

            // 8. Oyunu başlat
            StartGameWithPlayers();
        }

        private IEnumerator StartMultiplayerFlow()
        {
            ShowLoading("Rakip Aranıyor...");

            // 1. Profil kontrolü
            yield return StartCoroutine(EnsureProfileLoaded());

            UpdateLoadingProgress(0.2f);

            // 2. Oyun hakkı kontrolü ve kullanımı
            if (ProfileManager.Instance != null)
            {
                if (ProfileManager.Instance.GameRights <= 0)
                {
                    ShowError("Oyun hakkınız kalmadı!");
                    yield break;
                }
                yield return StartCoroutine(UseGameRightCoroutine());
            }

            UpdateLoadingProgress(0.3f);

            // 3. Matchmaking başlat
            var matchmakingTask = MatchmakingManager.Instance?.QuickMatchAsync();
            float startTime = Time.time;

            while (matchmakingTask != null && !matchmakingTask.IsCompleted)
            {
                float elapsed = Time.time - startTime;
                float progress = Mathf.Min(0.3f + (elapsed / matchmakingTimeout) * 0.5f, 0.8f);
                UpdateLoadingProgress(progress);

                if (elapsed > matchmakingTimeout)
                {
                    ShowError("Rakip bulunamadı. Tekrar deneyin.");
                    MatchmakingManager.Instance?.LeaveMatchmaking();
                    yield break;
                }

                yield return null;
            }

            // 4. Match bulundu - oyuncuları al
            if (MatchmakingManager.Instance?.CurrentLobby != null)
            {
                _players = new List<InGamePlayerData>();

                foreach (var playerData in MatchmakingManager.Instance.CurrentLobby.players)
                {
                    var inGamePlayer = new InGamePlayerData
                    {
                        playerId = playerData.playerId,
                        displayName = playerData.playerName,
                        isLocalPlayer = playerData.playerId == ProfileManager.Instance?.CurrentProfile?.userId
                    };
                    _players.Add(inGamePlayer);
                }
            }
            else
            {
                // Matchmaking başarısız - bot'larla oyna
                _players = CreateBotPlayers();
                var localPlayer = CreateLocalPlayer();
                _players.Insert(0, localPlayer);
            }

            UpdateLoadingProgress(0.9f);

            // 5. Soruları yükle
            yield return StartCoroutine(LoadQuestions());

            // 6. UI başlat
            InitializeGameUI();

            UpdateLoadingProgress(1f);
            yield return new WaitForSeconds(0.5f);

            HideLoading();

            // 7. Oyunu başlat
            StartGameWithPlayers();
        }

        private IEnumerator StartFriendGameFlow(List<string> friendIds)
        {
            ShowLoading("Arkadaşlar Bekleniyor...");

            // 1. Profil kontrolü
            yield return StartCoroutine(EnsureProfileLoaded());

            // 2. Arkadaş profillerini yükle
            var profiles = new List<ProfileData>();
            foreach (var friendId in friendIds)
            {
                var profile = ProfileManager.Instance?.GetProfileAsync(friendId);
                if (profile != null)
                {
                    yield return new WaitUntil(() => profile.IsCompleted);
                    if (profile.Result != null)
                    {
                        profiles.Add(profile.Result);
                    }
                }
            }

            // 3. Oyuncuları oluştur
            _players = new List<InGamePlayerData>();

            // Yerel oyuncu
            var localPlayer = CreateLocalPlayer();
            _players.Add(localPlayer);

            // Arkadaşlar
            foreach (var profile in profiles)
            {
                _players.Add(ProfileManager.Instance.ToInGamePlayerData(profile, false));
            }

            // Eksik varsa bot ekle
            while (_players.Count < maxPlayers)
            {
                _players.Add(CreateBotPlayer($"Bot {_players.Count}"));
            }

            // 4. Soruları yükle
            yield return StartCoroutine(LoadQuestions());

            // 5. UI başlat
            InitializeGameUI();

            HideLoading();

            // 6. Oyunu başlat
            StartGameWithPlayers();
        }

        #endregion

        #region Helper Methods

        private void InitializeScene()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            // PlayerTopBar bul (eğer atanmamışsa)
            if (playerTopBar == null)
            {
                playerTopBar = FindFirstObjectByType<PlayerTopBar>();
            }

            // GameCanvas'tan UI referanslarını bul
            FindUIReferences();
        }

        /// <summary>
        /// UI referanslarını otomatik bul
        /// </summary>
        private void FindUIReferences()
        {
            var gameCanvas = GameObject.Find("GameCanvas");
            if (gameCanvas == null) return;

            // LoadingPanel ve alt elemanları
            if (loadingScreen == null)
            {
                var loadingPanel = gameCanvas.transform.Find("LoadingPanel");
                if (loadingPanel != null)
                {
                    loadingScreen = loadingPanel.gameObject;

                    if (loadingText == null)
                        loadingText = loadingPanel.Find("LoadingText")?.GetComponent<TMPro.TextMeshProUGUI>();

                    if (loadingProgressBar == null)
                    {
                        var progressImage = loadingPanel.Find("LoadingProgressImage");
                        if (progressImage != null)
                        {
                            loadingProgressBar = progressImage.GetComponent<UnityEngine.UI.Slider>();
                            // Slider yoksa Image'dan oluşturmayı deneme - sadece slider varsa kullan
                        }
                    }
                }
            }

            Debug.Log("[InGameSceneManager] UI referansları otomatik bulundu");
        }

        private IEnumerator EnsureProfileLoaded()
        {
            if (ProfileManager.Instance == null)
            {
                Debug.LogWarning("[InGameSceneManager] ProfileManager not found");
                yield break;
            }

            if (!ProfileManager.Instance.IsProfileLoaded)
            {
                var task = ProfileManager.Instance.LoadCurrentProfileAsync();
                yield return new WaitUntil(() => task.IsCompleted);
            }
        }

        private IEnumerator UseGameRightCoroutine()
        {
            if (ProfileManager.Instance == null) yield break;

            // Timeout ile bekle - sunucu cevap vermezse devam et
            var task = ProfileManager.Instance.UseGameRightAsync();
            float startTime = Time.time;
            float timeout = 5f; // 5 saniye timeout

            while (!task.IsCompleted && Time.time - startTime < timeout)
            {
                yield return null;
            }

            if (!task.IsCompleted)
            {
                Debug.LogWarning("[InGameSceneManager] Game right request timed out, continuing anyway");
            }
            else if (!task.Result)
            {
                Debug.LogWarning("[InGameSceneManager] Failed to use game right, but continuing with game");
            }
        }

        private IEnumerator LoadQuestions()
        {
            // QuestionLoader kullan
            if (QuestionLoader.Instance != null && !QuestionLoader.Instance.IsLoaded)
            {
                var task = QuestionLoader.Instance.LoadQuestionsAsync();
                yield return new WaitUntil(() => task.IsCompleted);
            }
        }

        private InGamePlayerData CreateLocalPlayer()
        {
            if (ProfileManager.Instance != null && ProfileManager.Instance.IsProfileLoaded)
            {
                return ProfileManager.Instance.ToInGamePlayerData(true);
            }

            // Fallback - profil yüklenemezse
            return new InGamePlayerData
            {
                playerId = "local_player",
                displayName = "Oyuncu",
                isLocalPlayer = true,
                currentScore = 0,
                correctAnswers = 0,
                wrongAnswers = 0,
                castleHealth = 3
            };
        }

        private List<InGamePlayerData> CreateBotPlayers()
        {
            var bots = new List<InGamePlayerData>();

            string[] botNames = { "Fatih", "Selim", "Süleyman", "Murat", "Mehmet", "Osman" };
            PlayerColor[] colors = { PlayerColor.Mavi, PlayerColor.Kirmizi, PlayerColor.Yesil };

            for (int i = 0; i < maxPlayers - 1; i++)
            {
                var bot = CreateBotPlayer(botNames[i % botNames.Length]);
                bot.color = colors[(i + 1) % colors.Length];
                bots.Add(bot);
            }

            return bots;
        }

        private InGamePlayerData CreateBotPlayer(string name)
        {
            return new InGamePlayerData
            {
                playerId = $"bot_{Guid.NewGuid().ToString().Substring(0, 8)}",
                displayName = name,
                isLocalPlayer = false,
                currentScore = 0,
                correctAnswers = 0,
                wrongAnswers = 0,
                castleHealth = 3
            };
        }

        private void InitializeGameUI()
        {
            // Player Top Bar'ı başlat
            if (playerTopBar != null && _players != null)
            {
                playerTopBar.Initialize(_players);
                playerTopBar.UpdateRound(1, totalRounds);
                playerTopBar.UpdatePhase(GamePhase.Fetih);
            }

            // GameUIManager'ı güncelle
            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.UpdatePlayers(_players);
            }

            OnPlayersReady?.Invoke(_players);
        }

        private void StartGameWithPlayers()
        {
            _isGameStarted = true;

            // GameStateData oluştur
            var gameState = new GameStateData
            {
                gameId = Guid.NewGuid().ToString(),
                currentPhase = GamePhase.Fetih,
                currentRound = 1,
                players = _players
            };

            // InGameFlowManager'a oyunu başlat
            if (InGameFlowManager.Instance != null)
            {
                InGameFlowManager.Instance.StartGame(gameState);
            }
            else
            {
                Debug.LogError("[InGameSceneManager] InGameFlowManager not found!");
            }

            // Event trigger
            GameEvents.TriggerBotGameStarted(_players.FindAll(p => !p.isLocalPlayer));
            OnGameSceneReady?.Invoke();
        }

        private void CleanupGame()
        {
            _isGameStarted = false;
            _players?.Clear();

            if (InGameFlowManager.Instance != null)
            {
                InGameFlowManager.Instance.StopGame();
            }
        }

        #endregion

        #region UI Helpers

        private void ShowLoading(string message)
        {
            if (loadingScreen != null)
                loadingScreen.SetActive(true);

            if (loadingText != null)
                loadingText.text = message;

            if (loadingProgressBar != null)
                loadingProgressBar.value = 0f;
        }

        private void UpdateLoadingProgress(float progress)
        {
            if (loadingProgressBar != null)
                loadingProgressBar.value = progress;
        }

        private void HideLoading()
        {
            if (loadingScreen != null)
                loadingScreen.SetActive(false);
        }

        private void ShowError(string message)
        {
            HideLoading();
            Debug.LogError($"[InGameSceneManager] {message}");

            // TODO: Error popup göster
        }

        #endregion

        #region Event Handlers

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == gameScene)
            {
                InitializeScene();
            }
        }

        private void HandleGameEnded(List<GameEndPlayerResult> results)
        {
            _isGameStarted = false;

            // Sonuçları hazırla
            var result = new GameEndResult
            {
                results = results,
                gameMode = _currentGameMode,
                totalRounds = totalRounds
            };

            // Yerel oyuncu sonucu
            var localResult = results.Find(r => r.playerId == ProfileManager.Instance?.CurrentProfile?.userId);
            if (localResult != null)
            {
                result.localPlayerRank = localResult.finalRank;
                result.localPlayerScore = localResult.finalScore;
                result.localPlayerCorrect = localResult.correctAnswers;
                result.localPlayerWrong = localResult.wrongAnswers;
            }

            OnGameCompleted?.Invoke(result);
        }

        #endregion
    }

    #region Data Classes

    public enum GameMode
    {
        SinglePlayer,
        Multiplayer,
        FriendMatch,
        Tournament
    }

    [Serializable]
    public class GameEndResult
    {
        public List<GameEndPlayerResult> results;
        public GameMode gameMode;
        public int totalRounds;
        public int localPlayerRank;
        public int localPlayerScore;
        public int localPlayerCorrect;
        public int localPlayerWrong;

        public bool IsLocalPlayerWinner => localPlayerRank == 1;
    }

    #endregion
}
