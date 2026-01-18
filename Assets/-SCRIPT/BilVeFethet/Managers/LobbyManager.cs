using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Netcode;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Lobi Yöneticisi - Basitleştirilmiş lobi sistemi
    /// NetworkManager üzerinden çalışır
    /// </summary>
    public class LobbyManager : Singleton<LobbyManager>
    {
        [Header("Lobi Ayarları")]
        [SerializeField] private int maxPlayersPerLobby = 3;
        [SerializeField] private float lobbyRefreshInterval = 2f;
        
        // Lobi durumları
        public enum LobbyState
        {
            None,
            Creating,
            InLobby,
            Starting,
            Failed
        }
        
        // Events
        public event Action<LobbyState> OnLobbyStateChanged;
        public event Action<LobbyData> OnLobbyCreated;
        public event Action<LobbyData> OnLobbyJoined;
        public event Action<LobbyData> OnLobbyUpdated;
        public event Action<List<LobbyData>> OnLobbyListUpdated;
        public event Action<string> OnPlayerJoinedLobby;
        public event Action<string> OnPlayerLeftLobby;
        public event Action OnLobbyLeft;
        public event Action<string> OnLobbyError;
        public event Action OnGameStarting;
        
        // State
        private LobbyState currentState = LobbyState.None;
        private LobbyData currentLobby;
        private Coroutine lobbyRefreshCoroutine;
        private bool isHost = false;
        
        // Properties
        public LobbyState CurrentState => currentState;
        public LobbyData CurrentLobby => currentLobby;
        public bool IsHost => isHost;
        public bool IsInLobby => currentLobby != null;
        public string LobbyCode => currentLobby?.lobbyCode;
        public int PlayerCount => currentLobby?.players.Count ?? 0;
        
        protected override void Awake()
        {
            base.Awake();
        }
        
        private void Start()
        {
            // NetworkManager callback'lerini ayarla
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }
        
        private void OnDestroy()
        {
            StopAllCoroutines();
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
        
        #region Lobby Creation
        
        /// <summary>
        /// Özel lobi oluştur
        /// </summary>
        public LobbyData CreatePrivateLobby(string lobbyName)
        {
            return CreateLobbyInternal(lobbyName, true);
        }
        
        /// <summary>
        /// Genel lobi oluştur
        /// </summary>
        public LobbyData CreatePublicLobby(string lobbyName)
        {
            return CreateLobbyInternal(lobbyName, false);
        }
        
        private LobbyData CreateLobbyInternal(string lobbyName, bool isPrivate)
        {
            if (currentLobby != null)
            {
                Debug.LogWarning("[LobbyManager] Already in a lobby");
                return currentLobby;
            }
            
            try
            {
                SetState(LobbyState.Creating);
                
                string playerName = PlayerManager.Instance?.LocalPlayerData?.displayName ?? "Host";
                string playerId = AuthenticationService.Instance?.PlayerId ?? Guid.NewGuid().ToString();
                
                // Lobi verisi oluştur
                currentLobby = new LobbyData
                {
                    lobbyId = Guid.NewGuid().ToString(),
                    lobbyCode = GenerateLobbyCode(),
                    lobbyName = lobbyName,
                    hostId = playerId,
                    hostName = playerName,
                    maxPlayers = maxPlayersPerLobby,
                    isPrivate = isPrivate,
                    isGameStarted = false,
                    players = new List<LobbyPlayerInfo>()
                };
                
                // Host'u ekle
                currentLobby.players.Add(new LobbyPlayerInfo
                {
                    playerId = playerId,
                    playerName = playerName,
                    isHost = true,
                    isReady = true,
                    level = PlayerManager.Instance?.Level ?? 1
                });
                
                isHost = true;
                
                Debug.Log($"[LobbyManager] Created lobby: {currentLobby.lobbyId}, Code: {currentLobby.lobbyCode}");
                
                // NetworkManager'ı host olarak başlat
                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.StartHost();
                }
                
                lobbyRefreshCoroutine = StartCoroutine(RefreshLobbyCoroutine());
                
                SetState(LobbyState.InLobby);
                OnLobbyCreated?.Invoke(currentLobby);
                
                return currentLobby;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] Create lobby failed: {e.Message}");
                SetState(LobbyState.Failed);
                OnLobbyError?.Invoke($"Lobi oluşturma hatası: {e.Message}");
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
        
        #endregion
        
        #region Lobby Joining
        
        /// <summary>
        /// Lobi kodunu kullanarak katıl
        /// </summary>
        public bool JoinLobbyByCode(string lobbyCode)
        {
            if (currentLobby != null)
            {
                Debug.LogWarning("[LobbyManager] Already in a lobby");
                return false;
            }
            
            try
            {
                string playerName = PlayerManager.Instance?.LocalPlayerData?.displayName ?? "Player";
                string playerId = AuthenticationService.Instance?.PlayerId ?? Guid.NewGuid().ToString();
                
                // Simüle edilmiş lobi katılımı
                // Gerçek implementasyonda sunucudan lobi verisi alınacak
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
                
                Debug.Log($"[LobbyManager] Joined lobby: {currentLobby.lobbyCode}");
                
                // NetworkManager'ı client olarak başlat
                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.StartClient();
                }
                
                lobbyRefreshCoroutine = StartCoroutine(RefreshLobbyCoroutine());
                
                SetState(LobbyState.InLobby);
                OnLobbyJoined?.Invoke(currentLobby);
                
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] Join lobby by code failed: {e.Message}");
                OnLobbyError?.Invoke($"Lobi katılım hatası: {e.Message}");
                return false;
            }
        }
        
        #endregion
        
        #region Lobby Management
        
        /// <summary>
        /// Lobiden ayrıl
        /// </summary>
        public void LeaveLobby()
        {
            if (lobbyRefreshCoroutine != null)
            {
                StopCoroutine(lobbyRefreshCoroutine);
                lobbyRefreshCoroutine = null;
            }
            
            if (currentLobby == null) return;
            
            try
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }
                
                Debug.Log("[LobbyManager] Left lobby");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyManager] Leave lobby error: {e.Message}");
            }
            finally
            {
                currentLobby = null;
                isHost = false;
                SetState(LobbyState.None);
                OnLobbyLeft?.Invoke();
            }
        }
        
        /// <summary>
        /// Oyuncu hazır durumunu güncelle
        /// </summary>
        public void SetPlayerReady(bool isReady)
        {
            if (currentLobby == null) return;
            
            string playerId = AuthenticationService.Instance?.PlayerId ?? "";
            var player = currentLobby.players.Find(p => p.playerId == playerId);
            
            if (player != null)
            {
                player.isReady = isReady;
                OnLobbyUpdated?.Invoke(currentLobby);
            }
        }
        
        /// <summary>
        /// Oyunu başlat (host only)
        /// </summary>
        public void StartGame()
        {
            if (!isHost || currentLobby == null)
            {
                Debug.LogWarning("[LobbyManager] Only host can start the game");
                return;
            }
            
            // Tüm oyuncuların hazır olup olmadığını kontrol et
            int readyCount = GetReadyPlayerCount();
            if (readyCount < currentLobby.players.Count)
            {
                OnLobbyError?.Invoke("Tüm oyuncular hazır değil!");
                return;
            }
            
            SetState(LobbyState.Starting);
            currentLobby.isGameStarted = true;
            
            Debug.Log("[LobbyManager] Game starting...");
            OnGameStarting?.Invoke();
        }
        
        /// <summary>
        /// Oyuncuyu at (host only)
        /// </summary>
        public void KickPlayer(string playerId)
        {
            if (!isHost || currentLobby == null)
            {
                Debug.LogWarning("[LobbyManager] Only host can kick players");
                return;
            }
            
            var player = currentLobby.players.Find(p => p.playerId == playerId);
            if (player != null && !player.isHost)
            {
                currentLobby.players.Remove(player);
                OnPlayerLeftLobby?.Invoke(playerId);
                OnLobbyUpdated?.Invoke(currentLobby);
                Debug.Log($"[LobbyManager] Kicked player: {playerId}");
            }
        }
        
        #endregion
        
        #region Utility Methods

        /// <summary>
        /// Hazır oyuncu sayısını al
        /// </summary>
        public int GetReadyPlayerCount()
        {
            if (currentLobby == null) return 0;
            return currentLobby.players.FindAll(p => p.isReady).Count;
        }

        /// <summary>
        /// Oyuncu listesini al
        /// </summary>
        public List<LobbyPlayerInfo> GetPlayerList()
        {
            return currentLobby?.players ?? new List<LobbyPlayerInfo>();
        }

        /// <summary>
        /// Lobi listesi yenileme başlat
        /// </summary>
        public void StartLobbyListRefresh()
        {
            // Basitleştirilmiş versiyon - gerçek implementasyonda sunucudan liste çekilecek
            var demoLobbies = new List<LobbyData>();
            OnLobbyListUpdated?.Invoke(demoLobbies);
            Debug.Log("[LobbyManager] Lobby list refresh started");
        }

        /// <summary>
        /// Lobi listesi yenileme durdur
        /// </summary>
        public void StopLobbyListRefresh()
        {
            Debug.Log("[LobbyManager] Lobby list refresh stopped");
        }

        private void SetState(LobbyState newState)
        {
            if (currentState != newState)
            {
                currentState = newState;
                OnLobbyStateChanged?.Invoke(newState);
                Debug.Log($"[LobbyManager] State changed to: {newState}");
            }
        }

        #endregion
        
        #region Network Callbacks
        
        private void OnClientConnected(ulong clientId)
        {
            if (currentLobby != null)
            {
                Debug.Log($"[LobbyManager] Client connected: {clientId}");
                OnPlayerJoinedLobby?.Invoke(clientId.ToString());
                OnLobbyUpdated?.Invoke(currentLobby);
            }
        }
        
        private void OnClientDisconnected(ulong clientId)
        {
            if (currentLobby != null)
            {
                Debug.Log($"[LobbyManager] Client disconnected: {clientId}");
                OnPlayerLeftLobby?.Invoke(clientId.ToString());
                OnLobbyUpdated?.Invoke(currentLobby);
            }
        }
        
        #endregion
        
        #region Coroutines
        
        private IEnumerator RefreshLobbyCoroutine()
        {
            var waitTime = new WaitForSeconds(lobbyRefreshInterval);
            
            while (currentLobby != null)
            {
                yield return waitTime;
                
                if (currentLobby == null) yield break;
                
                OnLobbyUpdated?.Invoke(currentLobby);
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Lobi verisi
    /// </summary>
    [Serializable]
    public class LobbyData
    {
        public string lobbyId;
        public string lobbyCode;
        public string lobbyName;
        public string hostId;
        public string hostName;
        public int maxPlayers;
        public bool isPrivate;
        public bool isGameStarted;
        public List<LobbyPlayerInfo> players;
    }
    
    /// <summary>
    /// Lobi oyuncu bilgisi
    /// </summary>
    [Serializable]
    public class LobbyPlayerInfo
    {
        public string playerId;
        public string playerName;
        public bool isHost;
        public bool isReady;
        public int level;
        public int avatarId;
    }
}
