using System;
using System.Collections.Generic;
using BilVeFethet.Enums;
using UnityEngine;

namespace BilVeFethet.Data
{
    /// <summary>
    /// Tek bir soru icin ScriptableObject
    /// Editor'da olusturulup duzenlenebilir
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuestion", menuName = "BilVeFethet/Question", order = 1)]
    public class QuestionSO : ScriptableObject
    {
        [Header("Soru Bilgileri")]
        [Tooltip("Sorunun benzersiz ID'si")]
        public string questionId;
        
        [Tooltip("Soru metni")]
        [TextArea(3, 5)]
        public string questionText;
        
        [Header("Kategori ve Zorluk")]
        [Tooltip("Soru kategorisi")]
        public QuestionCategory category;
        
        [Tooltip("Zorluk seviyesi")]
        public QuestionDifficulty difficulty;
        
        [Tooltip("Soru tipi")]
        public QuestionType questionType = QuestionType.CoktanSecmeli;
        
        [Header("Coktan Secmeli Soru")]
        [Tooltip("4 secenek")]
        public List<string> options = new List<string> { "", "", "", "" };
        
        [Tooltip("Dogru cevap indeksi (0-3)")]
        [Range(0, 3)]
        public int correctAnswerIndex;
        
        [Header("Tahmin Sorusu")]
        [Tooltip("Dogru sayisal deger")]
        public float correctValue;
        
        [Tooltip("Kabul edilebilir hata payi")]
        public float tolerance;
        
        [Tooltip("Birim (yil, km, kg vb.)")]
        public string valueUnit;
        
        [Header("Zamanlama")]
        [Tooltip("Soru icin verilen sure (saniye)")]
        public float timeLimit = 15f;

        /// <summary>
        /// ScriptableObject'i QuestionData'ya donustur
        /// </summary>
        public QuestionData ToQuestionData()
        {
            return new QuestionData
            {
                questionId = string.IsNullOrEmpty(questionId) ? Guid.NewGuid().ToString() : questionId,
                questionType = questionType,
                category = category,
                questionText = questionText,
                timeLimit = timeLimit,
                difficultyLevel = GetDifficultyLevel(),
                options = new List<string>(options),
                correctAnswerIndex = correctAnswerIndex,
                correctValue = correctValue,
                tolerance = tolerance,
                valueUnit = valueUnit
            };
        }

        /// <summary>
        /// Zorluk seviyesini sayi olarak dondur (1-10)
        /// </summary>
        public int GetDifficultyLevel()
        {
            return difficulty switch
            {
                QuestionDifficulty.Kolay => 3,
                QuestionDifficulty.Orta => 5,
                QuestionDifficulty.Zor => 8,
                _ => 5
            };
        }

        private void OnValidate()
        {
            // Otomatik ID olustur
            if (string.IsNullOrEmpty(questionId))
            {
                questionId = Guid.NewGuid().ToString().Substring(0, 8);
            }

            // Secenek sayisini 4'te tut
            while (options.Count < 4)
            {
                options.Add("");
            }
            while (options.Count > 4)
            {
                options.RemoveAt(options.Count - 1);
            }
        }
    }
}