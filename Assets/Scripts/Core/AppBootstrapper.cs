using UnityEngine;

namespace AnadoluFethi.Core
{
    /// <summary>
    /// Oyunu başlatan tek giriş noktası.
    /// Tüm servisleri ve sistemleri başlatır.
    /// </summary>
    public class AppBootstrapper : MonoBehaviour
    {
        public static AppBootstrapper Instance { get; private set; }

        [Header("Ayarlar")]
        [SerializeField] private bool showDebugLogs = true;

        private void Awake()
        {
            InitializeSingleton();
            InitializeGame();
        }

        private void InitializeSingleton()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void InitializeGame()
        {
            // Başlatma sırası önemli!
            LoadSaveData();
            InitializeServices();
            StartGame();
        }

        #region Save Yükleme

        private void LoadSaveData()
        {
            // TODO: Kayıtlı oyun verilerini yükle
            // TODO: PlayerPrefs veya JSON dosyasından veri oku
            // TODO: Eğer kayıt yoksa varsayılan değerleri ata
            // TODO: Yükleme başarısız olursa hata yönetimi yap
        }

        #endregion

        #region Servisleri Başlatma

        private void InitializeServices()
        {
            // TODO: AudioManager'ı başlat
            // TODO: UIManager'ı başlat
            // TODO: GameStateManager'ı başlat
            // TODO: QuestionManager'ı başlat
            // TODO: JokerManager'ı başlat
            // TODO: MapManager'ı başlat
            // TODO: PlayerManager'ı başlat
        }

        private void InitializeAudioService()
        {
            // TODO: Ses sistemini başlat
            // TODO: Ses ayarlarını yükle
        }

        private void InitializeUIService()
        {
            // TODO: UI sistemini başlat
            // TODO: Ana menüyü hazırla
        }

        private void InitializeGameStateService()
        {
            // TODO: Oyun durumu yöneticisini başlat
            // TODO: Başlangıç durumunu ayarla
        }

        #endregion

        #region Oyun Başlatma

        private void StartGame()
        {
            // TODO: Tüm servisler hazır olduğunda oyunu başlat
            // TODO: Splash screen veya ana menüye geçiş yap
        }

        #endregion

        #region Yardımcı Metodlar

        private void Log(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[AppBootstrapper] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[AppBootstrapper] {message}");
        }

        #endregion

        private void OnApplicationQuit()
        {
            // TODO: Oyun kapanırken verileri kaydet
            // TODO: Servisleri temizle
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // TODO: Mobil cihazlarda arka plana alındığında kaydet
        }
    }
}
