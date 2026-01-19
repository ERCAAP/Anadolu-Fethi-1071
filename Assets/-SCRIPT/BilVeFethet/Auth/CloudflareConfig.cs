using UnityEngine;

namespace BilVeFethet.Auth
{
    /// <summary>
    /// Cloudflare API yapılandırması
    /// </summary>
    [CreateAssetMenu(fileName = "CloudflareConfig", menuName = "BilVeFethet/Cloudflare Config")]
    public class CloudflareConfig : ScriptableObject
    {
        [Header("API Ayarları")]
        [Tooltip("Cloudflare Workers API URL")]
        [SerializeField] private string apiBaseUrl = "https://bilvefethet-api.YOUR_SUBDOMAIN.workers.dev";

        [Tooltip("API Timeout (saniye)")]
        [SerializeField] private int timeoutSeconds = 30;

        [Header("Turnstile (Bot Koruması)")]
        [Tooltip("Turnstile Site Key (client-side)")]
        [SerializeField] private string turnstileSiteKey = "";

        [Header("Retry Ayarları")]
        [Tooltip("Başarısız isteklerde tekrar deneme sayısı")]
        [SerializeField] private int maxRetries = 3;

        [Tooltip("Tekrar denemeler arası bekleme (saniye)")]
        [SerializeField] private float retryDelay = 1f;

        [Header("Token Ayarları")]
        [Tooltip("Token yenileme eşiği (dakika) - bu kadar süre kala yenile")]
        [SerializeField] private int tokenRefreshThresholdMinutes = 30;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        // Properties
        public string ApiBaseUrl => apiBaseUrl;
        public int TimeoutSeconds => timeoutSeconds;
        public string TurnstileSiteKey => turnstileSiteKey;
        public int MaxRetries => maxRetries;
        public float RetryDelay => retryDelay;
        public int TokenRefreshThresholdMinutes => tokenRefreshThresholdMinutes;
        public bool EnableDebugLogs => enableDebugLogs;

        // API Endpoints
        public string AuthRegisterUrl => $"{apiBaseUrl}/auth/register";
        public string AuthLoginUrl => $"{apiBaseUrl}/auth/login";
        public string AuthLogoutUrl => $"{apiBaseUrl}/auth/logout";
        public string AuthRefreshUrl => $"{apiBaseUrl}/auth/refresh";
        public string AuthMeUrl => $"{apiBaseUrl}/auth/me";
        public string AuthForgotPasswordUrl => $"{apiBaseUrl}/auth/forgot-password";
        public string AuthResetPasswordUrl => $"{apiBaseUrl}/auth/reset-password";

        public string ProfileUrl => $"{apiBaseUrl}/profile";
        public string ProfileSettingsUrl => $"{apiBaseUrl}/profile/settings";

        public string QuestionsUrl => $"{apiBaseUrl}/questions";
        public string QuestionsRandomUrl => $"{apiBaseUrl}/questions/random";

        public string ChatUrl => $"{apiBaseUrl}/chat";
        public string FriendsUrl => $"{apiBaseUrl}/friends";
        public string LeaderboardUrl => $"{apiBaseUrl}/leaderboard";
        public string NotificationsUrl => $"{apiBaseUrl}/notifications";

        // Singleton instance
        private static CloudflareConfig _instance;
        public static CloudflareConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<CloudflareConfig>("CloudflareConfig");

                    if (_instance == null)
                    {
                        Debug.LogWarning("[CloudflareConfig] Config dosyası bulunamadı, varsayılan ayarlar kullanılıyor.");
                        _instance = CreateInstance<CloudflareConfig>();
                    }
                }
                return _instance;
            }
        }

        public void Log(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[Cloudflare] {message}");
            }
        }

        public void LogError(string message)
        {
            Debug.LogError($"[Cloudflare] {message}");
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning($"[Cloudflare] {message}");
        }
    }
}
