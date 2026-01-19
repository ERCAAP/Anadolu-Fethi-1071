using System;
using System.Collections.Generic;
using System.Linq;
using BilVeFethet.Enums;
using UnityEngine;

namespace BilVeFethet.Data
{
    /// <summary>
    /// Tum sorulari iceren veritabani ScriptableObject
    /// Kategorilere ve zorluk seviyelerine gore sorulari tutar
    /// </summary>
    [CreateAssetMenu(fileName = "QuestionDatabase", menuName = "BilVeFethet/Question Database", order = 0)]
    public class QuestionDatabaseSO : ScriptableObject
    {
        [Header("Soru Listeleri")]
        [Tooltip("Turkce kategorisindeki sorular")]
        public List<QuestionSO> turkceQuestions = new List<QuestionSO>();
        
        [Tooltip("Ingilizce kategorisindeki sorular")]
        public List<QuestionSO> ingilizceQuestions = new List<QuestionSO>();
        
        [Tooltip("Bilim kategorisindeki sorular")]
        public List<QuestionSO> bilimQuestions = new List<QuestionSO>();
        
        [Tooltip("Sanat kategorisindeki sorular")]
        public List<QuestionSO> sanatQuestions = new List<QuestionSO>();
        
        [Tooltip("Spor kategorisindeki sorular")]
        public List<QuestionSO> sporQuestions = new List<QuestionSO>();
        
        [Tooltip("Genel Kultur kategorisindeki sorular")]
        public List<QuestionSO> genelKulturQuestions = new List<QuestionSO>();
        
        [Tooltip("Tarih kategorisindeki sorular")]
        public List<QuestionSO> tarihQuestions = new List<QuestionSO>();

        /// <summary>
        /// Kategoriye gore soru listesi dondur
        /// </summary>
        public List<QuestionSO> GetQuestionsByCategory(QuestionCategory category)
        {
            return category switch
            {
                QuestionCategory.Turkce => turkceQuestions,
                QuestionCategory.Ingilizce => ingilizceQuestions,
                QuestionCategory.Bilim => bilimQuestions,
                QuestionCategory.Sanat => sanatQuestions,
                QuestionCategory.Spor => sporQuestions,
                QuestionCategory.GenelKultur => genelKulturQuestions,
                QuestionCategory.Tarih => tarihQuestions,
                _ => new List<QuestionSO>()
            };
        }

        /// <summary>
        /// Kategori ve zorluğa gore sorulari filtrele
        /// </summary>
        public List<QuestionSO> GetQuestions(QuestionCategory category, QuestionDifficulty? difficulty = null)
        {
            var questions = GetQuestionsByCategory(category);
            
            if (difficulty.HasValue)
            {
                questions = questions.Where(q => q.difficulty == difficulty.Value).ToList();
            }
            
            return questions;
        }

        /// <summary>
        /// Rastgele bir soru dondur
        /// </summary>
        public QuestionSO GetRandomQuestion(QuestionCategory? category = null, QuestionDifficulty? difficulty = null)
        {
            List<QuestionSO> pool;
            
            if (category.HasValue)
            {
                pool = GetQuestions(category.Value, difficulty);
            }
            else
            {
                pool = GetAllQuestions();
                if (difficulty.HasValue)
                {
                    pool = pool.Where(q => q.difficulty == difficulty.Value).ToList();
                }
            }
            
            if (pool.Count == 0) return null;
            
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        /// <summary>
        /// Tum sorulari dondur
        /// </summary>
        public List<QuestionSO> GetAllQuestions()
        {
            var allQuestions = new List<QuestionSO>();
            allQuestions.AddRange(turkceQuestions);
            allQuestions.AddRange(ingilizceQuestions);
            allQuestions.AddRange(bilimQuestions);
            allQuestions.AddRange(sanatQuestions);
            allQuestions.AddRange(sporQuestions);
            allQuestions.AddRange(genelKulturQuestions);
            allQuestions.AddRange(tarihQuestions);
            return allQuestions;
        }

        /// <summary>
        /// Toplam soru sayisini dondur
        /// </summary>
        public int GetTotalQuestionCount()
        {
            return turkceQuestions.Count + ingilizceQuestions.Count + bilimQuestions.Count +
                   sanatQuestions.Count + sporQuestions.Count + genelKulturQuestions.Count +
                   tarihQuestions.Count;
        }

        /// <summary>
        /// Kategoriye gore soru sayisini dondur
        /// </summary>
        public int GetQuestionCountByCategory(QuestionCategory category)
        {
            return GetQuestionsByCategory(category).Count;
        }

        /// <summary>
        /// Zorluğa gore soru sayisini dondur
        /// </summary>
        public int GetQuestionCountByDifficulty(QuestionDifficulty difficulty)
        {
            return GetAllQuestions().Count(q => q.difficulty == difficulty);
        }

        /// <summary>
        /// Veritabanini dogrula
        /// </summary>
        public List<string> ValidateDatabase()
        {
            var errors = new List<string>();
            var allQuestions = GetAllQuestions();
            var ids = new HashSet<string>();

            foreach (var question in allQuestions)
            {
                if (question == null)
                {
                    errors.Add("Null soru referansi bulundu");
                    continue;
                }

                if (string.IsNullOrEmpty(question.questionText))
                {
                    errors.Add($"Soru metni bos: {question.name}");
                }

                if (question.questionType == QuestionType.CoktanSecmeli)
                {
                    if (question.options.Count != 4)
                    {
                        errors.Add($"Secenek sayisi 4 degil: {question.name}");
                    }
                    
                    if (question.options.Any(string.IsNullOrEmpty))
                    {
                        errors.Add($"Bos secenek var: {question.name}");
                    }
                }

                if (!ids.Add(question.questionId))
                {
                    errors.Add($"Tekrarlanan ID: {question.questionId}");
                }
            }

            return errors;
        }

#if UNITY_EDITOR
        [ContextMenu("Validate Database")]
        private void ValidateInEditor()
        {
            var errors = ValidateDatabase();
            if (errors.Count == 0)
            {
                Debug.Log($"Veritabani gecerli! Toplam {GetTotalQuestionCount()} soru.");
            }
            else
            {
                foreach (var error in errors)
                {
                    Debug.LogError(error);
                }
            }
        }

        [ContextMenu("Log Statistics")]
        private void LogStatistics()
        {
            Debug.Log($"=== Soru Veritabani Istatistikleri ===");
            Debug.Log($"Toplam: {GetTotalQuestionCount()} soru");
            Debug.Log($"---");
            Debug.Log($"Turkce: {turkceQuestions.Count}");
            Debug.Log($"Ingilizce: {ingilizceQuestions.Count}");
            Debug.Log($"Bilim: {bilimQuestions.Count}");
            Debug.Log($"Sanat: {sanatQuestions.Count}");
            Debug.Log($"Spor: {sporQuestions.Count}");
            Debug.Log($"Genel Kultur: {genelKulturQuestions.Count}");
            Debug.Log($"Tarih: {tarihQuestions.Count}");
            Debug.Log($"---");
            Debug.Log($"Kolay: {GetQuestionCountByDifficulty(QuestionDifficulty.Kolay)}");
            Debug.Log($"Orta: {GetQuestionCountByDifficulty(QuestionDifficulty.Orta)}");
            Debug.Log($"Zor: {GetQuestionCountByDifficulty(QuestionDifficulty.Zor)}");
        }
#endif
    }
}