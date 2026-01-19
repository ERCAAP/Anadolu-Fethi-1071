using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;
using BilVeFethet.Auth;
using BilVeFethet.Auth.UI;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Ana Menü Manager - Ana sayfa ve navigasyon yönetimi
    /// Giriş akışı: Auth (Login/Register) -> MainMenu -> PlayMode -> Game
    /// </summary>
    public class MainMenuManager : Singleton<MainMenuManager>
    {
        [Header("Paneller")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject playModePanel;
        [SerializeField] private GameObject lobbyBrowserPanel;
        [SerializeField] private GameObject lobbyRoomPanel;
        [SerializeField] private GameObject createLobbyPanel;
        [SerializeField] private GameObject joinLobbyPanel;
        [SerializeField] private GameObject leaderboardPanel;
        [SerializeField] private GameObject profilePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private GameObject matchmakingPanel;

        [Header("Auth Panelleri")]
        [SerializeField] private GameObject authContainer;
        [SerializeField] private AuthUIManager authUIManager;
        
        [Header("Ana Menü UI")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI playerLevelText;
        [SerializeField] private TextMeshProUGUI tpText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI gameRightsText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Slider levelProgressSlider;
        
        [Header("Leaderboard Önizleme")]
        [SerializeField] private Transform leaderboardPreviewContainer;
        [SerializeField] private GameObject leaderboardEntryPrefab;
        [SerializeField] private TextMeshProUGUI playerRankText;
        [SerializeField] private TextMeshProUGUI onlinePlayersText;
        
        [Header("Matchmaking UI")]
        [SerializeField] private TextMeshProUGUI matchmakingStatusText;
        [SerializeField] private TextMeshProUGUI matchmakingPlayersText;
        [SerializeField] private Button cancelMatchmakingButton;
        [SerializeField] private GameObject matchmakingSpinner;
        
        [Header("Lobi UI")]
        [SerializeField] private TMP_InputField lobbyCodeInput;
        [SerializeField] private TMP_InputField lobbyNameInput;
        [SerializeField] private Toggle privateLobbyToggle;
        [SerializeField] private Transform lobbyPlayerListContainer;
        [SerializeField] private GameObject lobbyPlayerPrefab;
        [SerializeField] private TextMeshProUGUI lobbyCodeDisplayText;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;
        
        // State
        private MenuState currentState = MenuState.MainMenu;
        private List<GameObject> leaderboardPreviewItems = new List<GameObject>();
        private List<GameObject> lobbyPlayerItems = new List<GameObject>();
        
        public enum MenuState
        {
            MainMenu,
            PlayMode,
            LobbyBrowser,
            LobbyRoom,
            CreateLobby,
            JoinLobby,
            Leaderboard,
            Profile,
            Settings,
            Shop,
            Matchmaking
        }
        
        // Events
        public event Action<MenuState> OnMenuStateChanged;
        
        // Properties
        public MenuState CurrentState => currentState;
        
        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            SubscribeToEvents();
            CheckAuthStateAndInitialize();
        }

        /// <summary>
        /// Auth durumunu kontrol et ve UI'ı başlat
        /// </summary>
        private void CheckAuthStateAndInitialize()
        {
            // Auth durumunu kontrol et
            if (AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn)
            {
                // Giriş yapılmış - Ana menüyü göster
                ShowAuthContainer(false);
                RefreshPlayerInfo();
                RefreshLeaderboardPreview();
                ShowMainMenu();

                // Profil yükle
                _ = ProfileManager.Instance?.LoadCurrentProfileAsync();
            }
            else
            {
                // Giriş yapılmamış - Auth UI göster
                ShowAuthContainer(true);
                HideAllPanels();
            }
        }

        /// <summary>
        /// Auth container göster/gizle
        /// </summary>
        private void ShowAuthContainer(bool show)
        {
            if (authContainer != null)
                authContainer.SetActive(show);

            if (authUIManager != null && show)
                authUIManager.ShowAuthUI();
            else if (authUIManager != null)
                authUIManager.HideAuthUI();
        }

        /// <summary>
        /// Tüm panelleri gizle
        /// </summary>
        private void HideAllPanels()
        {
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(playModePanel, false);
            SetPanelActive(lobbyBrowserPanel, false);
            SetPanelActive(lobbyRoomPanel, false);
            SetPanelActive(createLobbyPanel, false);
            SetPanelActive(joinLobbyPanel, false);
            SetPanelActive(leaderboardPanel, false);
            SetPanelActive(profilePanel, false);
            SetPanelActive(settingsPanel, false);
            SetPanelActive(shopPanel, false);
            SetPanelActive(matchmakingPanel, false);
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        #region Event Subscriptions
        
        private void SubscribeToEvents()
        {
            // LeaderboardManager events
            if (LeaderboardManager.Instance != null)
            {
                LeaderboardManager.Instance.OnShortLeaderboardUpdated += UpdateLeaderboardPreview;
                LeaderboardManager.Instance.OnPlayerRankUpdated += UpdatePlayerRank;
            }
            
            // MatchmakingManager events
            if (MatchmakingManager.Instance != null)
            {
                MatchmakingManager.Instance.OnMatchmakingStateChanged += HandleMatchmakingStateChanged;
                MatchmakingManager.Instance.OnPlayersUpdated += HandlePlayersUpdated;
                MatchmakingManager.Instance.OnMatchmakingError += HandleMatchmakingError;
            }
            
            // LobbyManager events
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnLobbyCreated += HandleLobbyCreated;
                LobbyManager.Instance.OnLobbyJoined += HandleLobbyJoined;
                LobbyManager.Instance.OnLobbyUpdated += HandleLobbyUpdated;
                LobbyManager.Instance.OnLobbyLeft += HandleLobbyLeft;
                LobbyManager.Instance.OnGameStarting += HandleGameStarting;
                LobbyManager.Instance.OnLobbyError += ShowError;
            }
            
            // GameEvents
            GameEvents.OnGameFound += HandleGameFound;
            GameEvents.OnPlayerLevelUp += HandleLevelUp;

            // Auth Events
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnLoginCompleted += HandleAuthLogin;
                AuthManager.Instance.OnRegisterCompleted += HandleAuthRegister;
                AuthManager.Instance.OnLogoutCompleted += HandleAuthLogout;
            }

            // Profile Events
            if (ProfileManager.Instance != null)
            {
                ProfileManager.Instance.OnProfileLoaded += HandleProfileLoaded;
                ProfileManager.Instance.OnProfileUpdated += HandleProfileUpdated;
            }

            // AuthUIManager Events
            if (authUIManager != null)
            {
                authUIManager.OnLoginSuccess += HandleUILoginSuccess;
                authUIManager.OnRegisterSuccess += HandleUIRegisterSuccess;
            }
        }
        
        private void UnsubscribeFromEvents()
        {
            if (LeaderboardManager.Instance != null)
            {
                LeaderboardManager.Instance.OnShortLeaderboardUpdated -= UpdateLeaderboardPreview;
                LeaderboardManager.Instance.OnPlayerRankUpdated -= UpdatePlayerRank;
            }
            
            if (MatchmakingManager.Instance != null)
            {
                MatchmakingManager.Instance.OnMatchmakingStateChanged -= HandleMatchmakingStateChanged;
                MatchmakingManager.Instance.OnPlayersUpdated -= HandlePlayersUpdated;
                MatchmakingManager.Instance.OnMatchmakingError -= HandleMatchmakingError;
            }
            
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnLobbyCreated -= HandleLobbyCreated;
                LobbyManager.Instance.OnLobbyJoined -= HandleLobbyJoined;
                LobbyManager.Instance.OnLobbyUpdated -= HandleLobbyUpdated;
                LobbyManager.Instance.OnLobbyLeft -= HandleLobbyLeft;
                LobbyManager.Instance.OnGameStarting -= HandleGameStarting;
                LobbyManager.Instance.OnLobbyError -= ShowError;
            }
            
            GameEvents.OnGameFound -= HandleGameFound;
            GameEvents.OnPlayerLevelUp -= HandleLevelUp;

            // Auth Events
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnLoginCompleted -= HandleAuthLogin;
                AuthManager.Instance.OnRegisterCompleted -= HandleAuthRegister;
                AuthManager.Instance.OnLogoutCompleted -= HandleAuthLogout;
            }

            // Profile Events
            if (ProfileManager.Instance != null)
            {
                ProfileManager.Instance.OnProfileLoaded -= HandleProfileLoaded;
                ProfileManager.Instance.OnProfileUpdated -= HandleProfileUpdated;
            }

            // AuthUIManager Events
            if (authUIManager != null)
            {
                authUIManager.OnLoginSuccess -= HandleUILoginSuccess;
                authUIManager.OnRegisterSuccess -= HandleUIRegisterSuccess;
            }
        }
        
        #endregion
        
        #region Navigation Methods
        
        public void ShowMainMenu()
        {
            SetState(MenuState.MainMenu);
            RefreshPlayerInfo();
        }
        
        public void ShowPlayModePanel()
        {
            SetState(MenuState.PlayMode);
        }
        
        public void ShowLobbyBrowser()
        {
            SetState(MenuState.LobbyBrowser);
            LobbyManager.Instance?.StartLobbyListRefresh();
        }
        
        public void ShowCreateLobby()
        {
            SetState(MenuState.CreateLobby);
            if (lobbyNameInput != null)
                lobbyNameInput.text = $"{PlayerManager.Instance?.LocalPlayerData?.displayName ?? "Player"}'in Lobisi";
        }
        
        public void ShowJoinLobby()
        {
            SetState(MenuState.JoinLobby);
            if (lobbyCodeInput != null)
                lobbyCodeInput.text = "";
        }
        
        public void ShowLeaderboard()
        {
            SetState(MenuState.Leaderboard);
        }
        
        public void ShowProfile()
        {
            SetState(MenuState.Profile);
        }
        
        public void ShowSettings()
        {
            SetState(MenuState.Settings);
        }
        
        public void ShowShop()
        {
            SetState(MenuState.Shop);
        }
        
        public void GoBack()
        {
            switch (currentState)
            {
                case MenuState.PlayMode:
                case MenuState.Leaderboard:
                case MenuState.Profile:
                case MenuState.Settings:
                case MenuState.Shop:
                    ShowMainMenu();
                    break;
                case MenuState.LobbyBrowser:
                case MenuState.CreateLobby:
                case MenuState.JoinLobby:
                    LobbyManager.Instance?.StopLobbyListRefresh();
                    ShowPlayModePanel();
                    break;
                case MenuState.LobbyRoom:
                    LeaveLobby();
                    ShowPlayModePanel();
                    break;
                case MenuState.Matchmaking:
                    CancelMatchmaking();
                    ShowPlayModePanel();
                    break;
                default:
                    ShowMainMenu();
                    break;
            }
        }
        
        private void SetState(MenuState newState)
        {
            currentState = newState;
            UpdatePanelVisibility();
            OnMenuStateChanged?.Invoke(newState);
        }
        
        private void UpdatePanelVisibility()
        {
            // Tüm panelleri gizle
            SetPanelActive(mainMenuPanel, false);
            SetPanelActive(playModePanel, false);
            SetPanelActive(lobbyBrowserPanel, false);
            SetPanelActive(lobbyRoomPanel, false);
            SetPanelActive(createLobbyPanel, false);
            SetPanelActive(joinLobbyPanel, false);
            SetPanelActive(leaderboardPanel, false);
            SetPanelActive(profilePanel, false);
            SetPanelActive(settingsPanel, false);
            SetPanelActive(shopPanel, false);
            SetPanelActive(matchmakingPanel, false);
            
            // Aktif paneli göster
            switch (currentState)
            {
                case MenuState.MainMenu:
                    SetPanelActive(mainMenuPanel, true);
                    break;
                case MenuState.PlayMode:
                    SetPanelActive(playModePanel, true);
                    break;
                case MenuState.LobbyBrowser:
                    SetPanelActive(lobbyBrowserPanel, true);
                    break;
                case MenuState.LobbyRoom:
                    SetPanelActive(lobbyRoomPanel, true);
                    break;
                case MenuState.CreateLobby:
                    SetPanelActive(createLobbyPanel, true);
                    break;
                case MenuState.JoinLobby:
                    SetPanelActive(joinLobbyPanel, true);
                    break;
                case MenuState.Leaderboard:
                    SetPanelActive(leaderboardPanel, true);
                    break;
                case MenuState.Profile:
                    SetPanelActive(profilePanel, true);
                    break;
                case MenuState.Settings:
                    SetPanelActive(settingsPanel, true);
                    break;
                case MenuState.Shop:
                    SetPanelActive(shopPanel, true);
                    break;
                case MenuState.Matchmaking:
                    SetPanelActive(matchmakingPanel, true);
                    break;
            }
        }
        
        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }
        
        #endregion
        
        #region Game Mode Actions
        
        /// <summary>
        /// Hızlı oyun başlat (online matchmaking)
        /// </summary>
        public async void StartQuickMatch()
        {
            SetState(MenuState.Matchmaking);
            UpdateMatchmakingUI("Oyun aranıyor...", 0, 3);
            
            await MatchmakingManager.Instance?.QuickMatchAsync();
        }
        
        /// <summary>
        /// Botlara karşı oyna
        /// </summary>
        public void StartBotGame()
        {
            // InGameSceneManager varsa onu kullan
            if (InGameSceneManager.Instance != null)
            {
                InGameSceneManager.Instance.StartSinglePlayerGame();
            }
            else
            {
                // Fallback - eski sistemi kullan
                GameModeManager.Instance?.StartSinglePlayerGame(BotManager.BotDifficulty.Normal);
            }
        }
        
        /// <summary>
        /// Lobi oluştur
        /// </summary>
        public void CreateLobby()
        {
            string lobbyName = lobbyNameInput?.text ?? "Yeni Lobi";
            bool isPrivate = privateLobbyToggle?.isOn ?? false;

            if (isPrivate)
            {
                LobbyManager.Instance?.CreatePrivateLobby(lobbyName);
            }
            else
            {
                LobbyManager.Instance?.CreatePublicLobby(lobbyName);
            }
        }
        
        /// <summary>
        /// Lobiye kod ile katıl
        /// </summary>
        public void JoinLobbyByCode()
        {
            string code = lobbyCodeInput?.text?.ToUpper();

            if (string.IsNullOrEmpty(code))
            {
                ShowError("Lütfen lobi kodunu girin");
                return;
            }

            LobbyManager.Instance?.JoinLobbyByCode(code);
        }
        
        /// <summary>
        /// Matchmaking iptal
        /// </summary>
        public void CancelMatchmaking()
        {
            MatchmakingManager.Instance?.LeaveMatchmaking();
            ShowPlayModePanel();
        }

        /// <summary>
        /// Lobiden ayrıl
        /// </summary>
        public void LeaveLobby()
        {
            LobbyManager.Instance?.LeaveLobby();
        }

        /// <summary>
        /// Hazır ol/değil toggle
        /// </summary>
        public void ToggleReady()
        {
            bool currentReady = readyButton?.GetComponentInChildren<TextMeshProUGUI>()?.text == "Hazır Değil";
            LobbyManager.Instance?.SetPlayerReady(!currentReady);

            if (readyButton != null)
            {
                var buttonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = !currentReady ? "Hazır Değil" : "Hazır";
                }
            }
        }

        /// <summary>
        /// Oyunu başlat (host only)
        /// </summary>
        public void StartGame()
        {
            LobbyManager.Instance?.StartGame();
        }
        
        #endregion
        
        #region UI Updates
        
        private void RefreshPlayerInfo()
        {
            // Önce ProfileManager'dan dene, sonra PlayerManager
            bool useProfile = ProfileManager.Instance != null && ProfileManager.Instance.IsProfileLoaded;

            if (playerNameText != null)
            {
                playerNameText.text = useProfile
                    ? ProfileManager.Instance.DisplayName
                    : PlayerManager.Instance?.LocalPlayerData?.displayName ?? "Oyuncu";
            }

            if (playerLevelText != null)
            {
                int level = useProfile
                    ? ProfileManager.Instance.Level
                    : PlayerManager.Instance?.Level ?? 1;
                playerLevelText.text = $"Seviye {level}";
            }

            if (tpText != null)
            {
                int weeklyTP = useProfile
                    ? ProfileManager.Instance.WeeklyTP
                    : PlayerManager.Instance?.WeeklyTP ?? 0;
                tpText.text = $"{weeklyTP} TP";
            }

            if (goldText != null)
            {
                int gold = useProfile
                    ? ProfileManager.Instance.GoldCoins
                    : PlayerManager.Instance?.GoldCoins ?? 0;
                goldText.text = $"{gold}";
            }

            if (gameRightsText != null)
            {
                int rights = useProfile
                    ? ProfileManager.Instance.GameRights
                    : PlayerManager.Instance?.GameRights ?? 0;
                gameRightsText.text = rights > 0 ? $"{rights} Oyun Hakkı" : "Oyun hakkı yok";
            }

            // Level progress
            if (levelProgressSlider != null)
            {
                float progress = PlayerManager.Instance != null
                    ? PlayerManager.Instance.GetLevelProgressPercentage()
                    : 0.5f;
                levelProgressSlider.value = progress;
            }
        }
        
        private void RefreshLeaderboardPreview()
        {
            LeaderboardManager.Instance?.RefreshShortLeaderboard();
        }
        
        private void UpdateLeaderboardPreview(List<LeaderboardEntry> entries)
        {
            if (leaderboardPreviewContainer == null || leaderboardEntryPrefab == null) return;
            
            // Mevcut öğeleri temizle
            foreach (var item in leaderboardPreviewItems)
            {
                if (item != null) Destroy(item);
            }
            leaderboardPreviewItems.Clear();
            
            // Yeni öğeleri oluştur
            int count = Mathf.Min(entries.Count, 5);
            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];
                var item = Instantiate(leaderboardEntryPrefab, leaderboardPreviewContainer);
                
                // UI öğelerini güncelle (prefab yapısına göre ayarlanmalı)
                var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 3)
                {
                    texts[0].text = $"#{entry.rank}";
                    texts[1].text = entry.playerName;
                    texts[2].text = entry.score.ToString();
                }
                
                // Yerel oyuncuyu vurgula
                if (entry.isLocalPlayer)
                {
                    var bg = item.GetComponent<Image>();
                    if (bg != null) bg.color = new Color(1f, 0.9f, 0.5f, 0.3f);
                }
                
                leaderboardPreviewItems.Add(item);
            }
        }
        
        private void UpdatePlayerRank(int rank)
        {
            if (playerRankText != null)
                playerRankText.text = $"Sıralaman: #{rank}";
        }
        
        private void UpdateMatchmakingUI(string status, int currentPlayers, int requiredPlayers)
        {
            if (matchmakingStatusText != null)
                matchmakingStatusText.text = status;
                
            if (matchmakingPlayersText != null)
                matchmakingPlayersText.text = $"{currentPlayers}/{requiredPlayers} Oyuncu";
                
            if (matchmakingSpinner != null)
                matchmakingSpinner.SetActive(true);
        }
        
        private void UpdateLobbyPlayerList()
        {
            if (lobbyPlayerListContainer == null || lobbyPlayerPrefab == null) return;
            
            // Mevcut öğeleri temizle
            foreach (var item in lobbyPlayerItems)
            {
                if (item != null) Destroy(item);
            }
            lobbyPlayerItems.Clear();
            
            // Oyuncu listesini al
            var players = LobbyManager.Instance?.GetPlayerList() ?? new List<LobbyPlayerInfo>();
            
            foreach (var player in players)
            {
                var item = Instantiate(lobbyPlayerPrefab, lobbyPlayerListContainer);
                
                var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length >= 2)
                {
                    texts[0].text = player.playerName + (player.isHost ? " (Host)" : "");
                    texts[1].text = player.isReady ? "Hazır" : "Bekleniyor";
                }
                
                lobbyPlayerItems.Add(item);
            }
            
            // Start butonu (sadece host için)
            if (startGameButton != null)
            {
                startGameButton.gameObject.SetActive(LobbyManager.Instance?.IsHost ?? false);
                startGameButton.interactable = LobbyManager.Instance?.GetReadyPlayerCount() >= (LobbyManager.Instance?.PlayerCount ?? 0);
            }
        }
        
        private void ShowError(string message)
        {
            Debug.LogWarning($"[MainMenuManager] Error: {message}");
            // TODO: Toast notification veya popup göster
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleMatchmakingStateChanged(MatchmakingManager.MatchmakingState state)
        {
            switch (state)
            {
                case MatchmakingManager.MatchmakingState.SearchingQuick:
                    UpdateMatchmakingUI("Oyun aranıyor...", 0, 3);
                    break;
                case MatchmakingManager.MatchmakingState.FoundMatch:
                    UpdateMatchmakingUI("Oyun bulundu!", 1, 3);
                    break;
                case MatchmakingManager.MatchmakingState.Connecting:
                    UpdateMatchmakingUI("Bağlanıyor...", 0, 3);
                    break;
                case MatchmakingManager.MatchmakingState.Connected:
                    // Oyun sahnesine geçiş
                    break;
                case MatchmakingManager.MatchmakingState.Failed:
                    ShowPlayModePanel();
                    break;
            }
        }
        
        private void HandlePlayersUpdated(int current, int required)
        {
            UpdateMatchmakingUI($"Oyuncular bekleniyor... {current}/{required}", current, required);
        }
        
        private void HandleMatchmakingError(string error)
        {
            ShowError(error);
        }
        
        private void HandleLobbyCreated(LobbyData lobby)
        {
            SetState(MenuState.LobbyRoom);

            if (lobbyCodeDisplayText != null)
                lobbyCodeDisplayText.text = $"Kod: {lobby.lobbyCode}";

            UpdateLobbyPlayerList();
        }

        private void HandleLobbyJoined(LobbyData lobby)
        {
            SetState(MenuState.LobbyRoom);

            if (lobbyCodeDisplayText != null)
                lobbyCodeDisplayText.text = $"Kod: {lobby.lobbyCode}";

            UpdateLobbyPlayerList();
        }

        private void HandleLobbyUpdated(LobbyData lobby)
        {
            UpdateLobbyPlayerList();
        }
        
        private void HandleLobbyLeft()
        {
            ShowPlayModePanel();
        }
        
        private void HandleGameStarting()
        {
            // Oyun sahnesine geçiş
            Debug.Log("[MainMenuManager] Game is starting!");
        }
        
        private void HandleGameFound(string gameId)
        {
            Debug.Log($"[MainMenuManager] Game found: {gameId}");
            // Oyun sahnesine geçiş yapılacak
        }
        
        private void HandleLevelUp(int oldLevel, int newLevel)
        {
            RefreshPlayerInfo();
            // Level up animasyonu göster
        }

        #endregion

        #region Auth Event Handlers

        private void HandleAuthLogin(object sender, LoginEventArgs e)
        {
            if (e.Success)
            {
                ShowAuthContainer(false);
                RefreshPlayerInfo();
                RefreshLeaderboardPreview();
                ShowMainMenu();
            }
        }

        private void HandleAuthRegister(object sender, RegisterEventArgs e)
        {
            if (e.Success)
            {
                ShowAuthContainer(false);
                RefreshPlayerInfo();
                RefreshLeaderboardPreview();
                ShowMainMenu();
            }
        }

        private void HandleAuthLogout(object sender, EventArgs e)
        {
            ShowAuthContainer(true);
            HideAllPanels();
        }

        private void HandleProfileLoaded(ProfileData profile)
        {
            Debug.Log($"[MainMenuManager] Profile loaded: {profile.displayName}");
            RefreshPlayerInfo();
        }

        private void HandleProfileUpdated(ProfileData profile)
        {
            Debug.Log($"[MainMenuManager] Profile updated: {profile.displayName}");
            RefreshPlayerInfo();
        }

        private void HandleUILoginSuccess()
        {
            ShowAuthContainer(false);
            RefreshPlayerInfo();
            RefreshLeaderboardPreview();
            ShowMainMenu();
        }

        private void HandleUIRegisterSuccess()
        {
            ShowAuthContainer(false);
            RefreshPlayerInfo();
            RefreshLeaderboardPreview();
            ShowMainMenu();
        }

        /// <summary>
        /// Çıkış yap
        /// </summary>
        public async void Logout()
        {
            if (AuthManager.Instance != null)
            {
                await AuthManager.Instance.LogoutAsync();
            }
        }

        #endregion
    }
}
