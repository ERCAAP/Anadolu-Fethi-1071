using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Matchmaking Manager - Basitleştirilmiş eşleşme sistemi
    /// NetworkManager üzerinden çalışır
    /// </summary>
    public class MatchmakingManager : Singleton<MatchmakingManager>
    {
        [Header("Matchmaking Ayarları")]
        [SerializeField] private int maxPlayersPerGame = 3;
        [SerializeField] private float matchmakingTimeout = 60f;
        [SerializeField] private float quickMatchTimeout = 30f;
        
        // Matchmaking durumları
        public enum MatchmakingState
        {
            Idle,
            Initializing,
            SearchingQuick,
            SearchingNormal,
            FoundMatch,
            Connecting,
            Connected,
            Failed
        }
        
        // Events
        public event Action<MatchmakingState> OnMatchmakingStateChanged;
        public event Action<LobbyData> OnLobbyFound;
        public event Action<string> OnMatchmakingError;
        public event Action<int, int> OnPlayersUpdated; // currentPlayers, requiredPlayers
        
        // State
        private MatchmakingState currentState = MatchmakingState.Idle;
        private LobbyData currentLobby;
        private Coroutine matchmakingCoroutine;
        private bool isHost = false;
        
        public MatchmakingState CurrentState => currentState;
        public bool IsHost => isHost;
        public LobbyData CurrentLobby => currentLobby;
        
        protected override void Awake()
        {
            base.Awake();
        }
        
        private async void Start()
        {
            await InitializeUnityServices();
        }
        
        private void OnDestroy()
        {
            StopAllCoroutines();
            LeaveMatchmaking();
        }
        
        #region Unity Services Initialization
        
        /// <summary>
        /// Unity Gaming Services'i başlat
        /// </summary>
        public async Task<bool> InitializeUnityServices()
        {
            try
            {
                SetState(MatchmakingState.Initializing);
                
                // Unity Services'i başlat
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }
                
                // Anonim giriş yap
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"[Matchmaking] Signed in as: {AuthenticationService.Instance.PlayerId}");
                }
                
                SetState(MatchmakingState.Idle);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Matchmaking] Unity Services initialization failed: {e.Message}");
                SetState(MatchmakingState.Failed);
                OnMatchmakingError?.Invoke($"Servis başlatma hatası: {e.Message}");
                return false;
            }
        }
        
        #endregion
        
        #region Quick Match
        
        /// <summary>
        /// Hızlı eşleşme - En kısa sürede oyun bul
        /// </summary>
        public async Task QuickMatchAsync()
        {
            if (currentState != MatchmakingState.Idle)
            {
                Debug.LogWarning("[Matchmaking] Already in matchmaking");
                return;
            }
            
            SetState(MatchmakingState.SearchingQuick);
            GameEvents.TriggerSearchingGame();
            
            try
            {
                // Simüle edilmiş arama - gerçekte sunucu sorgusu yapılacak
                await Task.Delay(2000);
                
                // Lobi bulunamadı, yeni lobi oluştur
                await CreateLobbyAsync("QuickMatch");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Matchmaking] Quick match failed: {e.Message}");
                SetState(MatchmakingState.Failed);
                OnMatchmakingError?.Invoke($"Hızlı eşleşme hatası: {e.Message}");
            }
        }
        
        #endregion
        
        #region Lobby Management
        
        /// <summary>
        /// Yeni lobi oluştur
        /// </summary>
        public async Task<LobbyData> CreateLobbyAsync(string lobbyName, bool isPrivate = false)
        {
            try
            {
                string playerName = PlayerManager.Instance?.LocalPlayerData?.displayName ?? "Host";
                string playerId = AuthenticationService.Instance?.PlayerId ?? Guid.NewGuid().ToString();
                
                currentLobby = new LobbyData
                {
                    lobbyId = Guid.NewGuid().ToString(),
                    lobbyCode = GenerateLobbyCode(),
                    lobbyName = lobbyName,
                    hostId = playerId,
                    hostName = playerName,
                    maxPlayers = maxPlayersPerGame,
                    isPrivate = isPrivate,
                    isGameStarted = false,
                    players = new List<LobbyPlayerInfo>()
                };
                
                currentLobby.players.Add(new LobbyPlayerInfo
                {
                    playerId = playerId,
                    playerName = playerName,
                    isHost = true,
                    isReady = true,
                    level = PlayerManager.Instance?.Level ?? 1
                });
                
                isHost = true;
                
                Debug.Log($"[Matchmaking] Created lobby: {currentLobby.lobbyId}, Code: {currentLobby.lobbyCode}");
                
                // NetworkManager'ı host olarak başlat
                StartHost();
                
                // Oyuncu bekleme coroutine'i başlat
                matchmakingCoroutine = StartCoroutine(WaitForPlayers());
                
                OnLobbyFound?.Invoke(currentLobby);
                SetState(MatchmakingState.FoundMatch);
                
                return currentLobby;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Matchmaking] Create lobby failed: {e.Message}");
                SetState(MatchmakingState.Failed);
                OnMatchmakingError?.Invoke($"Lobi oluşturma hatası: {e.Message}");
                return null;
            }
        }
        
        private string GenerateLobbyCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            char[] code = new char[6];
            for (int i = 0; i < 6; i++)
            {
                code[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
            }
            return new string(code);
        }
        
        /// <summary>
        /// Lobi kodunu kullanarak katıl
        /// </summary>
        public async Task JoinLobbyByCodeAsync(string lobbyCode)
        {
            try
            {
                SetState(MatchmakingState.Connecting);
                
                string playerName = PlayerManager.Instance?.LocalPlayerData?.displayName ?? "Player";
                string playerId = AuthenticationService.Instance?.PlayerId ?? Guid.NewGuid().ToString();
                
                currentLobby = new LobbyData
                {
                    lobbyCode = lobbyCode.ToUpper(),
                    lobbyId = Guid.NewGuid().ToString(),
                    players = new List<LobbyPlayerInfo>()
                };
                
                currentLobby.players.Add(new LobbyPlayerInfo
                {
                    playerId = playerId,
                    playerName = playerName,
                    isHost = false,
                    isReady = false,
                    level = PlayerManager.Instance?.Level ?? 1
                });
                
                isHost = false;
                
                // Client olarak bağlan
                StartClient();
                
                Debug.Log($"[Matchmaking] Joined lobby by code: {currentLobby.lobbyCode}");
                
                OnLobbyFound?.Invoke(currentLobby);
                SetState(MatchmakingState.Connected);
                GameEvents.TriggerGameFound(currentLobby.lobbyId);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Matchmaking] Join by code failed: {e.Message}");
                SetState(MatchmakingState.Failed);
                OnMatchmakingError?.Invoke($"Lobi kodu geçersiz: {e.Message}");
            }
        }
        
        /// <summary>
        /// Matchmaking'den ayrıl
        /// </summary>
        public void LeaveMatchmaking()
        {
            if (matchmakingCoroutine != null)
            {
                StopCoroutine(matchmakingCoroutine);
                matchmakingCoroutine = null;
            }
            
            // NetworkManager'ı durdur
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            
            currentLobby = null;
            isHost = false;
            SetState(MatchmakingState.Idle);
        }
        
        #endregion
        
        #region Network Management
        
        /// <summary>
        /// Host olarak başlat
        /// </summary>
        private void StartHost()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.StartHost();
                Debug.Log("[Matchmaking] Started as host");
            }
        }
        
        /// <summary>
        /// Client olarak bağlan
        /// </summary>
        private void StartClient()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.StartClient();
                Debug.Log("[Matchmaking] Started as client");
            }
        }
        
        #endregion
        
        #region Coroutines
        
        /// <summary>
        /// Oyuncu bekleme - Yeterli oyuncu gelene kadar bekle
        /// </summary>
        private IEnumerator WaitForPlayers()
        {
            float elapsed = 0f;
            var pollInterval = new WaitForSeconds(2f);
            
            while (currentLobby != null && elapsed < matchmakingTimeout)
            {
                if (currentLobby != null)
                {
                    int currentPlayers = currentLobby.players.Count;
                    OnPlayersUpdated?.Invoke(currentPlayers, maxPlayersPerGame);
                    
                    // Yeterli oyuncu varsa oyunu başlat
                    if (currentPlayers >= maxPlayersPerGame)
                    {
                        SetState(MatchmakingState.Connected);
                        GameEvents.TriggerGameFound(currentLobby.lobbyId);
                        yield break;
                    }
                }
                
                yield return pollInterval;
                elapsed += 2f;
            }
            
            // Timeout - Botlarla başla
            if (currentLobby != null && currentLobby.players.Count < maxPlayersPerGame)
            {
                Debug.Log("[Matchmaking] Timeout - Not enough players, starting with bots");
                int botsNeeded = maxPlayersPerGame - currentLobby.players.Count;
                StartGameWithBots(botsNeeded);
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        private void SetState(MatchmakingState newState)
        {
            if (currentState != newState)
            {
                currentState = newState;
                OnMatchmakingStateChanged?.Invoke(newState);
                Debug.Log($"[Matchmaking] State changed to: {newState}");
            }
        }
        
        private void StartGameWithBots(int botCount)
        {
            // BotManager ile botları ekle
            if (botCount == 2)
            {
                BotManager.Instance?.StartMixedBotGame();
            }
            else if (botCount == 1)
            {
                BotManager.Instance?.StartBotGame(BotManager.BotDifficulty.Normal);
            }
            
            SetState(MatchmakingState.Connected);
            GameEvents.TriggerGameFound(currentLobby?.lobbyId ?? "local_game");
        }
        
        /// <summary>
        /// Mevcut lobi kodunu al
        /// </summary>
        public string GetLobbyCode()
        {
            return currentLobby?.lobbyCode;
        }
        
        /// <summary>
        /// Mevcut oyuncu sayısını al
        /// </summary>
        public int GetCurrentPlayerCount()
        {
            return currentLobby?.players.Count ?? 0;
        }
        
        #endregion
    }
}
