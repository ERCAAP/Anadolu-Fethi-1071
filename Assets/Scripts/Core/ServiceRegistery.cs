using UnityEngine;
using System;
using System.Collections.Generic;

namespace AnadoluFethi.Core
{
    /// <summary>
    /// Tüm Manager'ları tek noktadan erişilebilir yapan Service Locator.
    /// </summary>
    public class ServiceRegistry : MonoBehaviour
    {
        public static ServiceRegistry Instance { get; private set; }

        // Generic service dictionary
        private Dictionary<Type, object> services = new Dictionary<Type, object>();

        #region Manager Referansları (Inspector'dan atanabilir)

        [Header("Core Managers")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private AudioManager audioManager;

        [Header("Gameplay Managers")]
        [SerializeField] private QuestionManager questionManager;
        [SerializeField] private PlayerManager playerManager;
        [SerializeField] private MapManager mapManager;
        [SerializeField] private JokerManager jokerManager;

        [Header("System Managers")]
        [SerializeField] private SaveManager saveManager;

        #endregion

        #region Kolay Erişim Property'leri

        public static GameManager Game => Instance?.gameManager;
        public static UIManager UI => Instance?.uiManager;
        public static AudioManager Audio => Instance?.audioManager;
        public static QuestionManager Question => Instance?.questionManager;
        public static PlayerManager Player => Instance?.playerManager;
        public static MapManager Map => Instance?.mapManager;
        public static JokerManager Joker => Instance?.jokerManager;
        public static SaveManager Save => Instance?.saveManager;

        #endregion

        private void Awake()
        {
            InitializeSingleton();
            RegisterAllServices();
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

        #region Service Registration

        private void RegisterAllServices()
        {
            // TODO: Inspector'dan atanan manager'ları kaydet
            // TODO: Her manager null değilse Register<T> ile kaydet
        }

        /// <summary>
        /// Service'i registry'ye kaydet.
        /// </summary>
        public void Register<T>(T service) where T : class
        {
            // TODO: Type key olarak service'i dictionary'ye ekle
            // TODO: Zaten varsa uyarı ver ve üzerine yaz
        }

        /// <summary>
        /// Service'i registry'den kaldır.
        /// </summary>
        public void Unregister<T>() where T : class
        {
            // TODO: Type key ile dictionary'den kaldır
        }

        #endregion

        #region Service Retrieval

        /// <summary>
        /// Service'i registry'den al.
        /// </summary>
        public T Get<T>() where T : class
        {
            // TODO: Type key ile dictionary'den al
            // TODO: Bulunamazsa null döndür ve uyarı ver
            return null;
        }

        /// <summary>
        /// Service'in kayıtlı olup olmadığını kontrol et.
        /// </summary>
        public bool Has<T>() where T : class
        {
            // TODO: Dictionary'de var mı kontrol et
            return false;
        }

        /// <summary>
        /// Service'i almayı dene, başarılıysa true döndür.
        /// </summary>
        public bool TryGet<T>(out T service) where T : class
        {
            // TODO: Dictionary'den almayı dene
            // TODO: Başarılıysa service'i ata ve true döndür
            service = null;
            return false;
        }

        #endregion

        #region Initialization Check

        /// <summary>
        /// Tüm kritik servislerin hazır olup olmadığını kontrol et.
        /// </summary>
        public bool AreAllServicesReady()
        {
            // TODO: Kritik manager'ların null olmadığını kontrol et
            // TODO: Her birinin Initialize edildiğini kontrol et
            return false;
        }

        #endregion

        private void OnDestroy()
        {
            // TODO: Tüm servisleri temizle
            // TODO: Instance'ı null yap
        }
    }

    #region Manager Base Class

    /// <summary>
    /// Tüm Manager'ların türediği base class.
    /// Her Manager kendi Instance'ına sahip olur.
    /// </summary>
    public abstract class ManagerBase<T> : MonoBehaviour where T : ManagerBase<T>
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = (T)this;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

    #endregion

    #region Manager Placeholder Classes (Daha sonra kendi dosyalarına taşınacak)

    // Bu class'lar şimdilik placeholder olarak burada.
    // Her birini kendi dosyasına taşıyacağız.

    public class GameManager : ManagerBase<GameManager>
    {
        // TODO: Oyun akışını yönet
    }

    public class UIManager : ManagerBase<UIManager>
    {
        // TODO: UI sistemini yönet
    }

    public class AudioManager : ManagerBase<AudioManager>
    {
        // TODO: Ses sistemini yönet
    }

    public class QuestionManager : ManagerBase<QuestionManager>
    {
        // TODO: Soru sistemini yönet
    }

    public class PlayerManager : ManagerBase<PlayerManager>
    {
        // TODO: Oyuncu yönetimi
    }

    public class MapManager : ManagerBase<MapManager>
    {
        // TODO: Harita yönetimi
    }

    public class JokerManager : ManagerBase<JokerManager>
    {
        // TODO: Joker sistemi
    }

    public class SaveManager : ManagerBase<SaveManager>
    {
        // TODO: Kayıt sistemi
    }

    #endregion
}
