using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using BilVeFethet.Auth;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Profil Yöneticisi - Cloudflare ile profil ve oyuncu verilerini yönetir
    /// </summary>
    public class ProfileManager : Singleton<ProfileManager>
    {
        [Header("Yapılandırma")]
        [SerializeField] private CloudflareConfig config;

        // Cached data
        private ProfileData _currentProfile;
        private Dictionary<string, ProfileData> _cachedProfiles = new Dictionary<string, ProfileData>();

        // Events
        public event Action<ProfileData> OnProfileLoaded;
        public event Action<ProfileData> OnProfileUpdated;
        public event Action<string> OnProfileError;

        // Properties
        public ProfileData CurrentProfile => _currentProfile;
        public bool IsProfileLoaded => _currentProfile != null;
        public string DisplayName => _currentProfile?.displayName ?? "Oyuncu";
        public string AvatarUrl => _currentProfile?.avatarUrl;
        public int Level => _currentProfile?.level ?? 1;
        public int TotalTP => _currentProfile?.totalTP ?? 0;
        public int WeeklyTP => _currentProfile?.weeklyTP ?? 0;
        public int GoldCoins => _currentProfile?.goldCoins ?? 0;
        public int GameRights => _currentProfile?.gameRights ?? 5;

        private CloudflareConfig Config => config ?? CloudflareConfig.Instance;

        protected override void OnSingletonAwake()
        {
            _cachedProfiles = new Dictionary<string, ProfileData>();
        }

        private void Start()
        {
            // Auth Manager'ı dinle
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnLoginCompleted += HandleLoginCompleted;
                AuthManager.Instance.OnRegisterCompleted += HandleRegisterCompleted;
                AuthManager.Instance.OnLogoutCompleted += HandleLogout;

                // Zaten giriş yapmışsa profili yükle
                if (AuthManager.Instance.IsLoggedIn)
                {
                    _ = LoadCurrentProfileAsync();
                }
            }
        }

        private void OnDestroy()
        {
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnLoginCompleted -= HandleLoginCompleted;
                AuthManager.Instance.OnRegisterCompleted -= HandleRegisterCompleted;
                AuthManager.Instance.OnLogoutCompleted -= HandleLogout;
            }
        }

        #region Public Methods

        /// <summary>
        /// Mevcut kullanıcının profilini yükle
        /// </summary>
        public async Task<ProfileData> LoadCurrentProfileAsync()
        {
            if (!AuthManager.Instance.IsLoggedIn)
            {
                OnProfileError?.Invoke("Giriş yapılmamış");
                return null;
            }

            var response = await GetAsync<ApiResponse<ProfileData>>(Config.ProfileUrl);

            if (response == null || !response.success)
            {
                OnProfileError?.Invoke(response?.error ?? "Profil yüklenemedi");
                return null;
            }

            _currentProfile = response.data;
            CacheProfile(_currentProfile);

            // PlayerManager'ı güncelle
            UpdatePlayerManager();

            OnProfileLoaded?.Invoke(_currentProfile);
            Config?.Log($"Profile loaded: {_currentProfile.displayName}");

            return _currentProfile;
        }

        /// <summary>
        /// Başka bir oyuncunun profilini yükle
        /// </summary>
        public async Task<ProfileData> GetProfileAsync(string userId)
        {
            // Önce cache'e bak
            if (_cachedProfiles.TryGetValue(userId, out var cached))
            {
                return cached;
            }

            var url = $"{Config.ProfileUrl}/{userId}";
            var response = await GetAsync<ApiResponse<ProfileData>>(url);

            if (response == null || !response.success)
            {
                return null;
            }

            CacheProfile(response.data);
            return response.data;
        }

        /// <summary>
        /// Birden fazla oyuncunun profilini yükle (multiplayer için)
        /// </summary>
        public async Task<List<ProfileData>> GetProfilesAsync(List<string> userIds)
        {
            var profiles = new List<ProfileData>();

            foreach (var userId in userIds)
            {
                var profile = await GetProfileAsync(userId);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }

            return profiles;
        }

        /// <summary>
        /// Profili güncelle
        /// </summary>
        public async Task<bool> UpdateProfileAsync(string displayName = null, string avatarUrl = null)
        {
            var request = new ProfileUpdateRequest
            {
                displayName = displayName,
                avatarUrl = avatarUrl
            };

            var response = await PutAsync<ApiResponse<ProfileData>>(Config.ProfileUrl, request);

            if (response == null || !response.success)
            {
                OnProfileError?.Invoke(response?.error ?? "Profil güncellenemedi");
                return false;
            }

            _currentProfile = response.data;
            OnProfileUpdated?.Invoke(_currentProfile);

            return true;
        }

        /// <summary>
        /// Oyun hakkı kullan
        /// </summary>
        public async Task<bool> UseGameRightAsync()
        {
            if (_currentProfile == null || _currentProfile.gameRights <= 0)
            {
                OnProfileError?.Invoke("Oyun hakkınız yok");
                return false;
            }

            var response = await PostAsync<ApiResponse<GameRightUseResult>>($"{Config.ProfileUrl}/game-right/use", null);

            if (response == null || !response.success)
            {
                OnProfileError?.Invoke(response?.error ?? "Oyun hakkı kullanılamadı");
                return false;
            }

            _currentProfile.gameRights = response.data.remainingRights;
            return true;
        }

        /// <summary>
        /// Joker kullan
        /// </summary>
        public async Task<bool> UseJokerAsync(JokerType jokerType)
        {
            var request = new JokerUseRequest { jokerType = jokerType.ToString() };
            var response = await PostAsync<ApiResponse<JokerUseServerResult>>($"{Config.ProfileUrl}/joker/use", request);

            if (response == null || !response.success)
            {
                OnProfileError?.Invoke(response?.error ?? "Joker kullanılamadı");
                return false;
            }

            // Joker sayısını güncelle
            if (_currentProfile.jokerCounts.ContainsKey(jokerType))
            {
                _currentProfile.jokerCounts[jokerType] = response.data.remainingCount;
            }

            return true;
        }

        /// <summary>
        /// Joker sayısını al
        /// </summary>
        public int GetJokerCount(JokerType jokerType)
        {
            if (_currentProfile?.jokerCounts == null) return 0;
            return _currentProfile.jokerCounts.TryGetValue(jokerType, out var count) ? count : 0;
        }

        /// <summary>
        /// Sonraki ücretsiz oyun hakkı ne zaman
        /// </summary>
        public TimeSpan GetTimeToNextFreeGame()
        {
            if (_currentProfile == null)
                return TimeSpan.FromHours(8);

            return _currentProfile.nextFreeGameTime - DateTime.UtcNow;
        }

        /// <summary>
        /// Profil verilerini InGamePlayerData'ya dönüştür
        /// </summary>
        public InGamePlayerData ToInGamePlayerData(bool isLocalPlayer = true)
        {
            if (_currentProfile == null)
            {
                return CreateDefaultInGamePlayer(isLocalPlayer);
            }

            return new InGamePlayerData
            {
                playerId = _currentProfile.userId,
                displayName = _currentProfile.displayName,
                isLocalPlayer = isLocalPlayer,
                currentScore = 0,
                correctAnswers = 0,
                wrongAnswers = 0,
                castleHealth = 3,
                isEliminated = false,
                availableJokers = _currentProfile.jokerCounts ?? new Dictionary<JokerType, int>()
            };
        }

        /// <summary>
        /// Başka profili InGamePlayerData'ya dönüştür
        /// </summary>
        public InGamePlayerData ToInGamePlayerData(ProfileData profile, bool isLocalPlayer = false)
        {
            return new InGamePlayerData
            {
                playerId = profile.userId,
                displayName = profile.displayName,
                isLocalPlayer = isLocalPlayer,
                currentScore = 0,
                correctAnswers = 0,
                wrongAnswers = 0,
                castleHealth = 3,
                isEliminated = false,
                availableJokers = profile.jokerCounts ?? new Dictionary<JokerType, int>()
            };
        }

        #endregion

        #region Private Methods

        private InGamePlayerData CreateDefaultInGamePlayer(bool isLocalPlayer)
        {
            return new InGamePlayerData
            {
                playerId = isLocalPlayer ? "local_player" : Guid.NewGuid().ToString(),
                displayName = isLocalPlayer ? "Oyuncu" : "Rakip",
                isLocalPlayer = isLocalPlayer,
                currentScore = 0,
                correctAnswers = 0,
                wrongAnswers = 0,
                castleHealth = 3,
                isEliminated = false
            };
        }

        private void CacheProfile(ProfileData profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.userId)) return;
            _cachedProfiles[profile.userId] = profile;
        }

        private void UpdatePlayerManager()
        {
            if (_currentProfile == null || PlayerManager.Instance == null) return;

            PlayerManager.Instance.SetLocalPlayerId(_currentProfile.userId);

            // PlayerData oluştur ve cache'e ekle
            var playerData = new PlayerData
            {
                playerId = _currentProfile.userId,
                displayName = _currentProfile.displayName,
                avatarUrl = _currentProfile.avatarUrl,
                level = _currentProfile.level,
                totalTP = _currentProfile.totalTP,
                weeklyTP = _currentProfile.weeklyTP,
                goldCoins = _currentProfile.goldCoins,
                gameRights = _currentProfile.gameRights,
                jokerCounts = _currentProfile.jokerCounts,
                currentWinStreak = _currentProfile.currentWinStreak,
                currentUndefeatedStreak = _currentProfile.currentUndefeatedStreak
            };

            PlayerManager.Instance.CachePlayer(playerData);
        }

        private void HandleLoginCompleted(object sender, LoginEventArgs e)
        {
            if (e.Success)
            {
                _ = LoadCurrentProfileAsync();
            }
        }

        private void HandleRegisterCompleted(object sender, RegisterEventArgs e)
        {
            if (e.Success)
            {
                _ = LoadCurrentProfileAsync();
            }
        }

        private void HandleLogout(object sender, EventArgs e)
        {
            _currentProfile = null;
            _cachedProfiles.Clear();
        }

        #endregion

        #region HTTP Helpers

        private async Task<T> GetAsync<T>(string url) where T : class
        {
            return await SendRequestAsync<T>(url, "GET", null);
        }

        private async Task<T> PostAsync<T>(string url, object data) where T : class
        {
            return await SendRequestAsync<T>(url, "POST", data);
        }

        private async Task<T> PutAsync<T>(string url, object data) where T : class
        {
            return await SendRequestAsync<T>(url, "PUT", data);
        }

        private async Task<T> SendRequestAsync<T>(string url, string method, object data) where T : class
        {
            try
            {
                using var request = new UnityWebRequest(url, method);

                if (data != null)
                {
                    var json = JsonUtility.ToJson(data);
                    var bodyRaw = Encoding.UTF8.GetBytes(json);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Config.TimeoutSeconds;

                // Auth header ekle
                if (AuthManager.Instance != null && !string.IsNullOrEmpty(AuthManager.Instance.Token))
                {
                    request.SetRequestHeader("Authorization", $"Bearer {AuthManager.Instance.Token}");
                }

                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var responseText = request.downloadHandler.text;
                    Config?.Log($"{method} {url} -> {responseText}");
                    return JsonUtility.FromJson<T>(responseText);
                }

                Config?.LogWarning($"Request failed: {request.error}");
            }
            catch (Exception e)
            {
                Config?.LogError($"Request error: {e.Message}");
            }

            return null;
        }

        #endregion
    }

    #region Data Classes

    [Serializable]
    public class ProfileData
    {
        public string userId;
        public string email;
        public string displayName;
        public string avatarUrl;
        public int level;
        public int totalTP;
        public int weeklyTP;
        public int goldCoins;
        public int gameRights;
        public DateTime nextFreeGameTime;
        public int currentWinStreak;
        public int currentUndefeatedStreak;
        public int maxWinStreak;
        public int maxUndefeatedStreak;
        public int totalGamesPlayed;
        public int totalWins;
        public Dictionary<JokerType, int> jokerCounts;
        public Dictionary<GuardianType, int> guardianCounts;
        public PlayerSettings settings;
    }

    [Serializable]
    public class PlayerSettings
    {
        public bool soundEnabled = true;
        public bool musicEnabled = true;
        public bool notificationsEnabled = true;
        public bool vibrationEnabled = true;
        public string language = "tr";
    }

    [Serializable]
    public class ProfileUpdateRequest
    {
        public string displayName;
        public string avatarUrl;
    }

    [Serializable]
    public class JokerUseRequest
    {
        public string jokerType;
    }

    [Serializable]
    public class JokerUseServerResult
    {
        public bool success;
        public int remainingCount;
    }

    [Serializable]
    public class GameRightUseResult
    {
        public bool success;
        public int remainingRights;
        public DateTime nextFreeGameTime;
    }

    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public T data;
        public string error;
    }

    #endregion
}
