using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace AnadoluFethi.Core
{
    /// <summary>
    /// Sahne geçişlerini tek yerden yöneten sınıf.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Ayarlar")]
        [SerializeField] private bool useFadeTransition = true;
        [SerializeField] private float fadeDuration = 0.5f;

        // Events
        public event Action OnSceneLoadStarted;
        public event Action OnSceneLoadCompleted;
        public event Action<float> OnSceneLoadProgress;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #region Sahne Yükleme - Temel

        /// <summary>
        /// Sahneyi isimle yükler.
        /// </summary>
        public void LoadScene(string sceneName)
        {
            // TODO: Sahne yükleme başladı event'i tetikle
            // TODO: Fade out animasyonu başlat
            // TODO: SceneManager.LoadScene(sceneName) çağır
            // TODO: Fade in animasyonu başlat
            // TODO: Sahne yükleme tamamlandı event'i tetikle
        }

        /// <summary>
        /// Sahneyi index ile yükler.
        /// </summary>
        public void LoadSceneByIndex(int sceneIndex)
        {
            // TODO: Index'i scene name'e çevir
            // TODO: LoadScene(string) metodunu çağır
        }

        #endregion

        #region Sahne Yükleme - Async

        /// <summary>
        /// Sahneyi asenkron olarak yükler (loading screen için).
        /// </summary>
        public void LoadSceneAsync(string sceneName)
        {
            // TODO: Coroutine başlat
            // TODO: AsyncOperation ile sahne yükle
            // TODO: Progress event'i güncelle
            // TODO: Yükleme tamamlanınca event tetikle
        }

        /// <summary>
        /// Sahneyi asenkron olarak yükler ve callback çağırır.
        /// </summary>
        public void LoadSceneAsyncWithCallback(string sceneName, Action onComplete)
        {
            // TODO: LoadSceneAsync çağır
            // TODO: Tamamlanınca onComplete callback'i çağır
        }

        #endregion

        #region Sahne Yükleme - Additive

        /// <summary>
        /// Sahneyi mevcut sahnenin üzerine ekler.
        /// </summary>
        public void LoadSceneAdditive(string sceneName)
        {
            // TODO: SceneManager.LoadScene(sceneName, LoadSceneMode.Additive)
        }

        /// <summary>
        /// Eklenen sahneyi kaldırır.
        /// </summary>
        public void UnloadScene(string sceneName)
        {
            // TODO: SceneManager.UnloadSceneAsync(sceneName)
        }

        #endregion

        #region Özel Sahneler

        /// <summary>
        /// Ana menüyü yükler.
        /// </summary>
        public void LoadMainMenu()
        {
            // TODO: Ana menü sahne adını yükle
        }

        /// <summary>
        /// Oyun sahnesini yükler.
        /// </summary>
        public void LoadGameScene()
        {
            // TODO: Oyun sahnesini yükle
        }

        /// <summary>
        /// Mevcut sahneyi yeniden yükler.
        /// </summary>
        public void ReloadCurrentScene()
        {
            // TODO: Aktif sahnenin adını al
            // TODO: LoadScene ile yeniden yükle
        }

        #endregion

        #region Geçiş Efektleri

        private void StartFadeOut()
        {
            // TODO: Ekranı karart
        }

        private void StartFadeIn()
        {
            // TODO: Ekranı aydınlat
        }

        #endregion

        #region Yardımcı Metodlar

        /// <summary>
        /// Aktif sahnenin adını döndürür.
        /// </summary>
        public string GetCurrentSceneName()
        {
            // TODO: SceneManager.GetActiveScene().name döndür
            return "";
        }

        /// <summary>
        /// Sahnenin yüklü olup olmadığını kontrol eder.
        /// </summary>
        public bool IsSceneLoaded(string sceneName)
        {
            // TODO: SceneManager ile kontrol et
            return false;
        }

        #endregion
    }
}
