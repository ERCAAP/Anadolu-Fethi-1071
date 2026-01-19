using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using BilVeFethet.Utils;
using BilVeFethet.Managers;

namespace BilVeFethet.Auth
{
    /// <summary>
    /// Kimlik Doğrulama Yöneticisi
    /// Cloudflare Workers API ile iletişim kurar
    /// </summary>
    public class AuthManager : Singleton<AuthManager>
    {
        [Header("Yapılandırma")]
        [SerializeField] private CloudflareConfig config;

        // State
        private AuthState _currentState = AuthState.Unknown;
        private StoredAuthData _authData;
        private Coroutine _tokenRefreshCoroutine;

        // Events
        public event EventHandler<AuthStateChangedEventArgs> OnAuthStateChanged;
        public event EventHandler<LoginEventArgs> OnLoginCompleted;
        public event EventHandler<RegisterEventArgs> OnRegisterCompleted;
        public event EventHandler OnLogoutCompleted;

        // Properties
        public AuthState CurrentState => _currentState;
        public bool IsLoggedIn => _currentState == AuthState.LoggedIn;
        public string UserId => _authData?.userId;
        public string Username => _authData?.username;
        public string DisplayName => _authData?.displayName;
        public string Email => _authData?.email;
        public string Token => _authData?.token;

        private CloudflareConfig Config => config ?? CloudflareConfig.Instance;

        #region Lifecycle

        protected override void OnSingletonAwake()
        {
            LoadStoredAuth();
        }

        private void Start()
        {
            // Eğer kayıtlı token varsa doğrula
            if (_authData != null && !string.IsNullOrEmpty(_authData.token))
            {
                StartCoroutine(ValidateAndRefreshTokenCoroutine());
            }
            else
            {
                SetState(AuthState.NotLoggedIn);
            }
        }

        private void OnDestroy()
        {
            if (_tokenRefreshCoroutine != null)
            {
                StopCoroutine(_tokenRefreshCoroutine);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Yeni kullanıcı kaydı
        /// </summary>
        public async Task<RegisterEventArgs> RegisterAsync(string email, string username, string password, string displayName = null)
        {
            if (_currentState == AuthState.LoggingIn)
            {
                return new RegisterEventArgs(false, null, AuthError.Unknown, "İşlem devam ediyor");
            }

            SetState(AuthState.LoggingIn);

            var request = new RegisterRequest
            {
                email = email,
                username = username,
                password = password,
                displayName = displayName ?? username
            };

            var response = await PostAsync<ApiResponse<AuthResponseData>>(Config.AuthRegisterUrl, request);

            if (response == null)
            {
                var args = new RegisterEventArgs(false, null, AuthError.NetworkError, "Bağlantı hatası");
                SetState(AuthState.NotLoggedIn);
                OnRegisterCompleted?.Invoke(this, args);
                return args;
            }

            if (!response.success)
            {
                var error = ParseAuthError(response.error);
                var args = new RegisterEventArgs(false, null, error, response.error);
                SetState(AuthState.NotLoggedIn);
                OnRegisterCompleted?.Invoke(this, args);
                return args;
            }

            // Başarılı kayıt
            SaveAuthData(response.data);
            SetState(AuthState.LoggedIn);
            StartTokenRefreshTimer();

            // PlayerManager'ı güncelle
            UpdatePlayerManager(response.data);

            var successArgs = new RegisterEventArgs(true, response.data);
            OnRegisterCompleted?.Invoke(this, successArgs);
            return successArgs;
        }

        /// <summary>
        /// Giriş yap
        /// </summary>
        public async Task<LoginEventArgs> LoginAsync(string identifier, string password)
        {
            if (_currentState == AuthState.LoggingIn)
            {
                return new LoginEventArgs(false, null, AuthError.Unknown, "İşlem devam ediyor");
            }

            SetState(AuthState.LoggingIn);

            var request = new LoginRequest
            {
                identifier = identifier,
                password = password
            };

            var response = await PostAsync<ApiResponse<AuthResponseData>>(Config.AuthLoginUrl, request);

            if (response == null)
            {
                var args = new LoginEventArgs(false, null, AuthError.NetworkError, "Bağlantı hatası");
                SetState(AuthState.NotLoggedIn);
                OnLoginCompleted?.Invoke(this, args);
                return args;
            }

            if (!response.success)
            {
                var error = ParseAuthError(response.error);
                var args = new LoginEventArgs(false, null, error, response.error);
                SetState(AuthState.NotLoggedIn);
                OnLoginCompleted?.Invoke(this, args);
                return args;
            }

            // Başarılı giriş
            SaveAuthData(response.data);
            SetState(AuthState.LoggedIn);
            StartTokenRefreshTimer();

            // PlayerManager'ı güncelle
            UpdatePlayerManager(response.data);

            var successArgs = new LoginEventArgs(true, response.data);
            OnLoginCompleted?.Invoke(this, successArgs);
            return successArgs;
        }

        /// <summary>
        /// Çıkış yap
        /// </summary>
        public async Task LogoutAsync()
        {
            if (_authData != null && !string.IsNullOrEmpty(_authData.sessionId))
            {
                await PostAsync<ApiResponse<object>>(Config.AuthLogoutUrl, new { sessionId = _authData.sessionId });
            }

            ClearAuthData();
            SetState(AuthState.NotLoggedIn);
            OnLogoutCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Şifre sıfırlama isteği
        /// </summary>
        public async Task<bool> ForgotPasswordAsync(string email)
        {
            var request = new ForgotPasswordRequest { email = email };
            var response = await PostAsync<ApiResponse<object>>(Config.AuthForgotPasswordUrl, request);
            return response?.success ?? false;
        }

        /// <summary>
        /// Token'ı yenile
        /// </summary>
        public async Task<bool> RefreshTokenAsync()
        {
            if (_authData == null || string.IsNullOrEmpty(_authData.refreshToken))
            {
                return false;
            }

            SetState(AuthState.Refreshing);

            var request = new RefreshTokenRequest
            {
                refreshToken = _authData.refreshToken,
                sessionId = _authData.sessionId
            };

            var response = await PostAsync<ApiResponse<TokenRefreshData>>(Config.AuthRefreshUrl, request);

            if (response == null || !response.success)
            {
                // Refresh başarısız, çıkış yap
                ClearAuthData();
                SetState(AuthState.NotLoggedIn);
                return false;
            }

            // Yeni token'ı kaydet
            _authData.token = response.data.token;
            _authData.tokenExpiry = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds();
            SaveAuthToStorage();

            SetState(AuthState.LoggedIn);
            return true;
        }

        /// <summary>
        /// Profil bilgilerini al
        /// </summary>
        public async Task<ProfileData> GetProfileAsync()
        {
            var response = await GetAsync<ApiResponse<ProfileData>>(Config.ProfileUrl);
            return response?.success == true ? response.data : null;
        }

        /// <summary>
        /// Profili güncelle
        /// </summary>
        public async Task<bool> UpdateProfileAsync(string displayName = null, string avatarUrl = null)
        {
            var request = new { displayName, avatarUrl };
            var response = await PutAsync<ApiResponse<object>>(Config.ProfileUrl, request);
            return response?.success ?? false;
        }

        #endregion

        #region Private Methods

        private void SetState(AuthState newState, AuthError error = AuthError.None, string errorMessage = null)
        {
            var oldState = _currentState;
            _currentState = newState;

            if (oldState != newState)
            {
                Config?.Log($"Auth state changed: {oldState} -> {newState}");
                OnAuthStateChanged?.Invoke(this, new AuthStateChangedEventArgs(oldState, newState, error, errorMessage));
            }
        }

        private void SaveAuthData(AuthResponseData data)
        {
            _authData = new StoredAuthData
            {
                userId = data.userId,
                email = data.email,
                username = data.username,
                displayName = data.displayName,
                token = data.token,
                refreshToken = data.refreshToken,
                sessionId = data.sessionId,
                tokenExpiry = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds(),
                lastLogin = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            SaveAuthToStorage();
        }

        private void SaveAuthToStorage()
        {
            if (_authData == null) return;

            var json = JsonUtility.ToJson(_authData);
            PlayerPrefs.SetString("auth_data", json);
            PlayerPrefs.Save();

            Config?.Log("Auth data saved to storage");
        }

        private void LoadStoredAuth()
        {
            var json = PlayerPrefs.GetString("auth_data", null);

            if (string.IsNullOrEmpty(json))
            {
                _authData = null;
                return;
            }

            try
            {
                _authData = JsonUtility.FromJson<StoredAuthData>(json);
                Config?.Log($"Loaded stored auth for user: {_authData.username}");
            }
            catch (Exception e)
            {
                Config?.LogError($"Failed to load stored auth: {e.Message}");
                _authData = null;
            }
        }

        private void ClearAuthData()
        {
            _authData = null;
            PlayerPrefs.DeleteKey("auth_data");
            PlayerPrefs.Save();

            if (_tokenRefreshCoroutine != null)
            {
                StopCoroutine(_tokenRefreshCoroutine);
                _tokenRefreshCoroutine = null;
            }

            Config?.Log("Auth data cleared");
        }

        private IEnumerator ValidateAndRefreshTokenCoroutine()
        {
            SetState(AuthState.Refreshing);

            // Token süresini kontrol et
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expiresIn = _authData.tokenExpiry - now;

            if (expiresIn < Config.TokenRefreshThresholdMinutes * 60)
            {
                // Token süresi dolmak üzere, yenile
                var refreshTask = RefreshTokenAsync();
                yield return new WaitUntil(() => refreshTask.IsCompleted);

                if (!refreshTask.Result)
                {
                    yield break;
                }
            }

            // Token'ı doğrula
            var validateTask = GetAsync<ApiResponse<object>>(Config.AuthMeUrl);
            yield return new WaitUntil(() => validateTask.IsCompleted);

            if (validateTask.Result?.success == true)
            {
                SetState(AuthState.LoggedIn);
                StartTokenRefreshTimer();

                // PlayerManager'ı güncelle
                if (PlayerManager.Instance != null)
                {
                    PlayerManager.Instance.SetLocalPlayerId(_authData.userId);
                }
            }
            else
            {
                ClearAuthData();
                SetState(AuthState.NotLoggedIn);
            }
        }

        private void StartTokenRefreshTimer()
        {
            if (_tokenRefreshCoroutine != null)
            {
                StopCoroutine(_tokenRefreshCoroutine);
            }

            _tokenRefreshCoroutine = StartCoroutine(TokenRefreshTimerCoroutine());
        }

        private IEnumerator TokenRefreshTimerCoroutine()
        {
            while (IsLoggedIn && _authData != null)
            {
                // Her 30 dakikada bir token kontrolü
                yield return new WaitForSeconds(Config.TokenRefreshThresholdMinutes * 60);

                if (!IsLoggedIn) break;

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var expiresIn = _authData.tokenExpiry - now;

                if (expiresIn < Config.TokenRefreshThresholdMinutes * 60)
                {
                    var refreshTask = RefreshTokenAsync();
                    yield return new WaitUntil(() => refreshTask.IsCompleted);
                }
            }
        }

        private void UpdatePlayerManager(AuthResponseData data)
        {
            if (PlayerManager.Instance == null) return;

            PlayerManager.Instance.SetLocalPlayerId(data.userId);
            // PlayerManager diğer verileri kendi yükleyecek
        }

        private AuthError ParseAuthError(string error)
        {
            if (string.IsNullOrEmpty(error)) return AuthError.Unknown;

            error = error.ToLower();

            if (error.Contains("network") || error.Contains("bağlantı"))
                return AuthError.NetworkError;
            if (error.Contains("şifre") || error.Contains("password") || error.Contains("yanlış"))
                return AuthError.InvalidCredentials;
            if (error.Contains("bulunamadı") || error.Contains("not found"))
                return AuthError.UserNotFound;
            if (error.Contains("e-posta") && error.Contains("kullanıl"))
                return AuthError.EmailAlreadyExists;
            if (error.Contains("kullanıcı adı") && error.Contains("kullanıl"))
                return AuthError.UsernameAlreadyExists;
            if (error.Contains("güçlü") || error.Contains("weak"))
                return AuthError.WeakPassword;
            if (error.Contains("token") || error.Contains("süresi"))
                return AuthError.TokenExpired;
            if (error.Contains("yasak") || error.Contains("ban"))
                return AuthError.Banned;
            if (error.Contains("sunucu") || error.Contains("server"))
                return AuthError.ServerError;

            return AuthError.Unknown;
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
            int retries = 0;

            while (retries < Config.MaxRetries)
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
                    if (!string.IsNullOrEmpty(_authData?.token))
                    {
                        request.SetRequestHeader("Authorization", $"Bearer {_authData.token}");
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

                retries++;
                if (retries < Config.MaxRetries)
                {
                    await Task.Delay((int)(Config.RetryDelay * 1000));
                }
            }

            return null;
        }

        #endregion
    }
}
