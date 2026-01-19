using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Utils;
using UnityEngine;
using UnityEngine.Networking;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Soru yukleme yoneticisi
    /// JSON dosyasindan veya sunucudan sorulari yukler
    /// Otomatik olarak olusturulur - sahneye eklemeye gerek yok
    /// </summary>
    public class QuestionLoader : Singleton<QuestionLoader>
    {
        [Header("Veri Kaynaklari")]
        [Tooltip("ScriptableObject soru veritabani")]
        [SerializeField] private QuestionDatabaseSO questionDatabase;

        [Tooltip("JSON soru dosyasi (Resources klasorunde)")]
        [SerializeField] private string jsonFileName = "questions";

        [Tooltip("Sunucu URL (bos ise lokal kullanilir)")]
        [SerializeField] private string serverUrl = "";

        [Header("Ayarlar")]
        [Tooltip("Baslangicta otomatik yukle")]
        [SerializeField] private bool loadOnStart = true;

        [Tooltip("Oncelik sirasi: 0=ScriptableObject, 1=JSON, 2=Server")]
        [SerializeField] private int loadPriority = 1;

        // Yuklenen sorular
        private List<QuestionData> _loadedQuestions = new List<QuestionData>();
        private Dictionary<QuestionCategory, List<QuestionData>> _questionsByCategory = new Dictionary<QuestionCategory, List<QuestionData>>();
        private Dictionary<QuestionDifficulty, List<QuestionData>> _questionsByDifficulty = new Dictionary<QuestionDifficulty, List<QuestionData>>();

        // Kullanilmis sorular (tekrar sorulmamasi icin)
        private HashSet<string> _usedQuestionIds = new HashSet<string>();

        // State
        private bool _isLoaded = false;
        private bool _isLoading = false;

        // Properties
        public bool IsLoaded => _isLoaded;
        public bool IsLoading => _isLoading;
        public int QuestionCount => _loadedQuestions.Count;

        // Events
        public event Action OnQuestionsLoaded;
        public event Action<string> OnLoadError;

        /// <summary>
        /// Oyun basladiginda otomatik olarak QuestionLoader olustur
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreateInstance()
        {
            // HasInstance property'sini kullanarak kontrol et (private _instance'a erişilemez)
            if (!HasInstance)
            {
                var go = new GameObject("QuestionLoader");
                go.AddComponent<QuestionLoader>();
                DontDestroyOnLoad(go);
                Debug.Log("[QuestionLoader] Otomatik olarak olusturuldu");
            }
        }

        protected override void OnSingletonAwake()
        {
            InitializeDictionaries();
        }

        private void Start()
        {
            if (loadOnStart)
            {
                LoadQuestions();
            }
        }

        private void InitializeDictionaries()
        {
            foreach (QuestionCategory category in Enum.GetValues(typeof(QuestionCategory)))
            {
                _questionsByCategory[category] = new List<QuestionData>();
            }

            foreach (QuestionDifficulty difficulty in Enum.GetValues(typeof(QuestionDifficulty)))
            {
                _questionsByDifficulty[difficulty] = new List<QuestionData>();
            }
        }

        #region Public Methods

        /// <summary>
        /// Sorulari yukle
        /// </summary>
        public void LoadQuestions()
        {
            if (_isLoading) return;
            StartCoroutine(LoadQuestionsCoroutine());
        }

        /// <summary>
        /// Sorulari asenkron yukle
        /// </summary>
        public async Task LoadQuestionsAsync()
        {
            if (_isLoading) return;

            _isLoading = true;

            try
            {
                switch (loadPriority)
                {
                    case 0:
                        LoadFromScriptableObject();
                        break;
                    case 1:
                        LoadFromJson();
                        break;
                    case 2:
                        await LoadFromServerAsync();
                        break;
                }

                OrganizeQuestions();
                _isLoaded = _loadedQuestions.Count > 0;

                if (_isLoaded)
                {
                    Debug.Log($"[QuestionLoader] {_loadedQuestions.Count} soru yuklendi");
                    OnQuestionsLoaded?.Invoke();
                }
                else
                {
                    Debug.LogWarning("[QuestionLoader] Hic soru yuklenemedi");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestionLoader] Yukleme hatasi: {e.Message}");
                OnLoadError?.Invoke(e.Message);
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// Rastgele soru al
        /// </summary>
        public QuestionData GetRandomQuestion(QuestionCategory? category = null, QuestionDifficulty? difficulty = null)
        {
            // Eger henuz yuklenmemisse, bekle veya bos dondur
            if (!_isLoaded && !_isLoading)
            {
                LoadQuestions();
            }

            if (_loadedQuestions.Count == 0)
            {
                Debug.LogWarning("[QuestionLoader] Yuklenmis soru yok!");
                return null;
            }

            List<QuestionData> pool;

            if (category.HasValue && difficulty.HasValue)
            {
                pool = _loadedQuestions.Where(q =>
                    q.category == category.Value &&
                    GetDifficulty(q.difficultyLevel) == difficulty.Value &&
                    !_usedQuestionIds.Contains(q.questionId)).ToList();
            }
            else if (category.HasValue)
            {
                pool = _questionsByCategory.TryGetValue(category.Value, out var catList)
                    ? catList.Where(q => !_usedQuestionIds.Contains(q.questionId)).ToList()
                    : new List<QuestionData>();
            }
            else if (difficulty.HasValue)
            {
                pool = _questionsByDifficulty.TryGetValue(difficulty.Value, out var diffList)
                    ? diffList.Where(q => !_usedQuestionIds.Contains(q.questionId)).ToList()
                    : new List<QuestionData>();
            }
            else
            {
                pool = _loadedQuestions
                    .Where(q => !_usedQuestionIds.Contains(q.questionId)).ToList();
            }

            // Eger havuz bos ise kullanilmis sorulari sifirla
            if (pool.Count == 0)
            {
                ResetUsedQuestions();
                // Tekrar dene ama sonsuz donguyu onle
                pool = _loadedQuestions.ToList();
                if (category.HasValue)
                {
                    pool = pool.Where(q => q.category == category.Value).ToList();
                }
                if (difficulty.HasValue)
                {
                    pool = pool.Where(q => GetDifficulty(q.difficultyLevel) == difficulty.Value).ToList();
                }
            }

            if (pool.Count == 0)
            {
                Debug.LogWarning($"[QuestionLoader] Uygun soru bulunamadi! Kategori: {category}, Zorluk: {difficulty}");
                return null;
            }

            var question = pool[UnityEngine.Random.Range(0, pool.Count)];
            _usedQuestionIds.Add(question.questionId);

            return question;
        }

        /// <summary>
        /// Belirli sayida rastgele soru al
        /// </summary>
        public List<QuestionData> GetRandomQuestions(int count, QuestionCategory? category = null, QuestionDifficulty? difficulty = null)
        {
            var questions = new List<QuestionData>();

            for (int i = 0; i < count; i++)
            {
                var question = GetRandomQuestion(category, difficulty);
                if (question != null)
                {
                    questions.Add(question);
                }
            }

            return questions;
        }

        /// <summary>
        /// Kullanilmis soru listesini sifirla
        /// </summary>
        public void ResetUsedQuestions()
        {
            _usedQuestionIds.Clear();
            Debug.Log("[QuestionLoader] Kullanilmis sorular sifirlandi");
        }

        /// <summary>
        /// Kategoriye gore soru sayisi
        /// </summary>
        public int GetQuestionCount(QuestionCategory category)
        {
            return _questionsByCategory.TryGetValue(category, out var list) ? list.Count : 0;
        }

        /// <summary>
        /// Zorluğa gore soru sayisi
        /// </summary>
        public int GetQuestionCount(QuestionDifficulty difficulty)
        {
            return _questionsByDifficulty.TryGetValue(difficulty, out var list) ? list.Count : 0;
        }

        /// <summary>
        /// Tum kategorilerin soru sayisini dondur
        /// </summary>
        public Dictionary<QuestionCategory, int> GetAllCategoryCounts()
        {
            var counts = new Dictionary<QuestionCategory, int>();
            foreach (var kvp in _questionsByCategory)
            {
                counts[kvp.Key] = kvp.Value.Count;
            }
            return counts;
        }

        /// <summary>
        /// Tum zorluklarin soru sayisini dondur
        /// </summary>
        public Dictionary<QuestionDifficulty, int> GetAllDifficultyCounts()
        {
            var counts = new Dictionary<QuestionDifficulty, int>();
            foreach (var kvp in _questionsByDifficulty)
            {
                counts[kvp.Key] = kvp.Value.Count;
            }
            return counts;
        }

        #endregion

        #region Private Loading Methods

        private IEnumerator LoadQuestionsCoroutine()
        {
            _isLoading = true;

            switch (loadPriority)
            {
                case 0:
                    LoadFromScriptableObject();
                    break;
                case 1:
                    LoadFromJson();
                    break;
                case 2:
                    yield return StartCoroutine(LoadFromServerCoroutine());
                    break;
            }

            OrganizeQuestions();
            _isLoaded = _loadedQuestions.Count > 0;
            _isLoading = false;

            if (_isLoaded)
            {
                Debug.Log($"[QuestionLoader] {_loadedQuestions.Count} soru yuklendi");
                LogQuestionStats();
                OnQuestionsLoaded?.Invoke();
            }
            else
            {
                Debug.LogWarning("[QuestionLoader] Hic soru yuklenemedi!");
            }
        }

        private void LoadFromScriptableObject()
        {
            if (questionDatabase == null)
            {
                Debug.LogWarning("[QuestionLoader] QuestionDatabase atanmamis");
                return;
            }

            var allQuestions = questionDatabase.GetAllQuestions();
            foreach (var questionSO in allQuestions)
            {
                if (questionSO != null)
                {
                    _loadedQuestions.Add(questionSO.ToQuestionData());
                }
            }
        }

        private void LoadFromJson()
        {
            try
            {
                var jsonAsset = Resources.Load<TextAsset>(jsonFileName);
                if (jsonAsset == null)
                {
                    Debug.LogWarning($"[QuestionLoader] JSON dosyasi bulunamadi: {jsonFileName}");
                    return;
                }

                var container = JsonUtility.FromJson<QuestionJsonContainer>(jsonAsset.text);
                if (container?.questions != null)
                {
                    _loadedQuestions.AddRange(container.questions);
                    Debug.Log($"[QuestionLoader] JSON'dan {container.questions.Count} soru yuklendi");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestionLoader] JSON parse hatasi: {e.Message}");
            }
        }

        private IEnumerator LoadFromServerCoroutine()
        {
            if (string.IsNullOrEmpty(serverUrl))
            {
                Debug.LogWarning("[QuestionLoader] Sunucu URL'si bos, JSON'dan yukleniyor...");
                LoadFromJson();
                yield break;
            }

            Debug.Log($"[QuestionLoader] Sunucudan yukleniyor: {serverUrl}");

            using (var request = UnityWebRequest.Get(serverUrl))
            {
                request.timeout = 10; // 10 saniye timeout
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var container = JsonUtility.FromJson<QuestionJsonContainer>(request.downloadHandler.text);
                        if (container?.questions != null)
                        {
                            _loadedQuestions.AddRange(container.questions);
                            Debug.Log($"[QuestionLoader] Sunucudan {container.questions.Count} soru yuklendi");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[QuestionLoader] Sunucu verisi parse hatasi: {e.Message}");
                        OnLoadError?.Invoke(e.Message);
                        // Fallback: JSON'dan yukle
                        LoadFromJson();
                    }
                }
                else
                {
                    Debug.LogWarning($"[QuestionLoader] Sunucu hatasi: {request.error}, JSON'dan yukleniyor...");
                    OnLoadError?.Invoke(request.error);
                    // Fallback: JSON'dan yukle
                    LoadFromJson();
                }
            }
        }

        private async Task LoadFromServerAsync()
        {
            if (string.IsNullOrEmpty(serverUrl))
            {
                Debug.LogWarning("[QuestionLoader] Sunucu URL'si bos");
                LoadFromJson();
                return;
            }

            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(LoadFromServerCoroutineAsync(tcs));
            await tcs.Task;
        }

        private IEnumerator LoadFromServerCoroutineAsync(TaskCompletionSource<bool> tcs)
        {
            yield return StartCoroutine(LoadFromServerCoroutine());
            tcs.SetResult(true);
        }

        private void OrganizeQuestions()
        {
            // Onceki verileri temizle
            foreach (var list in _questionsByCategory.Values)
            {
                list.Clear();
            }
            foreach (var list in _questionsByDifficulty.Values)
            {
                list.Clear();
            }

            // Kategori ve zorluga gore ayir
            foreach (var question in _loadedQuestions)
            {
                if (_questionsByCategory.TryGetValue(question.category, out var categoryList))
                {
                    categoryList.Add(question);
                }

                var difficulty = GetDifficulty(question.difficultyLevel);
                if (_questionsByDifficulty.TryGetValue(difficulty, out var difficultyList))
                {
                    difficultyList.Add(question);
                }
            }
        }

        private QuestionDifficulty GetDifficulty(int level)
        {
            if (level <= 3) return QuestionDifficulty.Kolay;
            if (level <= 6) return QuestionDifficulty.Orta;
            return QuestionDifficulty.Zor;
        }

        private void LogQuestionStats()
        {
            Debug.Log("=== Soru Istatistikleri ===");
            Debug.Log($"Toplam: {_loadedQuestions.Count}");
            foreach (var kvp in _questionsByCategory)
            {
                if (kvp.Value.Count > 0)
                {
                    Debug.Log($"  {kvp.Key}: {kvp.Value.Count}");
                }
            }
            Debug.Log("Zorluk dagilimi:");
            foreach (var kvp in _questionsByDifficulty)
            {
                if (kvp.Value.Count > 0)
                {
                    Debug.Log($"  {kvp.Key}: {kvp.Value.Count}");
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// JSON soru container'i
    /// </summary>
    [Serializable]
    public class QuestionJsonContainer
    {
        public List<QuestionData> questions;
    }
}
