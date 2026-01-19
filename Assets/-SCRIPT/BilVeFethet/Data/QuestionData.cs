using System;
using System.Collections.Generic;
using BilVeFethet.Enums;
using UnityEngine;

namespace BilVeFethet.Data
{
    /// <summary>
    /// Soru verisi - sunucudan gelir
    /// </summary>
    [Serializable]
    public class QuestionData
    {
        public string questionId;
        public QuestionType questionType;
        public QuestionCategory category;
        public string questionText;
        public float timeLimit;              // Saniye cinsinden süre
        public int difficultyLevel;          // 1-10 arası zorluk
        
        // Çoktan seçmeli sorular için
        public List<string> options;         // 4 seçenek
        public int correctAnswerIndex;       // Doğru cevap indeksi (0-3)
        
        // Tahmin soruları için
        public float correctValue;           // Doğru sayısal değer
        public float tolerance;              // Kabul edilebilir hata payı
        public string valueUnit;             // Birim (yıl, km, kg vb.)
        
        // Joker kullanımı sonuçları (yerel hesaplama için)
        [NonSerialized] public List<int> eliminatedOptions;  // %50 jokeri ile elenen seçenekler
        [NonSerialized] public Dictionary<int, float> audiencePercentages;  // Oyunculara sor yüzdeleri
        [NonSerialized] public float parrotHint;  // Papağan tahmini

        public QuestionData()
        {
            options = new List<string>();
            eliminatedOptions = new List<int>();
            audiencePercentages = new Dictionary<int, float>();
        }
    }

    /// <summary>
    /// Soru istek verisi - sunucuya gönderilir
    /// </summary>
    [Serializable]
    public class QuestionRequestData
    {
        public string gameId;
        public string playerId;
        public GamePhase currentPhase;
        public int roundNumber;
        public int questionIndex;
        public QuestionCategory? preferredCategory;  // Kategori seçme jokeri için
        
        // Bandwidth optimizasyonu: sadece gerekli alanlar gönderilir
        public byte requestFlags;
        
        public const byte FLAG_INCLUDE_CATEGORY = 0x01;
        public const byte FLAG_FETIH_PHASE = 0x02;
        public const byte FLAG_SAVAS_PHASE = 0x04;
    }

    /// <summary>
    /// Soru cevap sonucu - sunucudan gelir
    /// </summary>
    [Serializable]
    public class QuestionResultData
    {
        public string questionId;
        public List<PlayerAnswerResult> playerResults;
        public int correctAnswerIndex;       // Çoktan seçmeli için
        public float correctValue;           // Tahmin için
        
        // Sıralama (0: 1., 1: 2., 2: 3.)
        public List<string> playerRanking;
    }

    /// <summary>
    /// Her oyuncunun cevap sonucu
    /// </summary>
    [Serializable]
    public class PlayerAnswerResult
    {
        public string playerId;
        public bool isCorrect;
        public int selectedAnswerIndex;
        public float guessedValue;
        public float answerTime;
        public int earnedPoints;
        public int rank;                     // Bu sorudaki sıralama (1-3)
        public float accuracy;               // Tahmin soruları için doğruluk yüzdesi
    }

    /// <summary>
    /// Joker kullanım sonucu
    /// </summary>
    [Serializable]
    public class JokerUseResult
    {
        public JokerType jokerType;
        public bool success;
        public string errorMessage;
        
        // %50 jokeri sonucu
        public List<int> eliminatedOptionIndices;
        
        // Oyunculara sor sonucu
        public Dictionary<int, float> audiencePercentages;
        
        // Papağan sonucu
        public float parrotHint;
        public float hintAccuracy;           // Ne kadar doğru (0.7 - 1.0 arası)
        
        // Teleskop sonucu
        public List<TelescopeOption> telescopeOptions;
        
        // Kategori seçme sonucu
        public List<QuestionCategory> availableCategories;
    }

    /// <summary>
    /// Teleskop jokeri seçenek verisi
    /// </summary>
    [Serializable]
    public class TelescopeOption
    {
        public string optionText;
        public bool isCorrect;
    }

    /// <summary>
    /// Soru gönderme verisi - oyuncu soru önerir
    /// </summary>
    [Serializable]
    public class QuestionSubmissionData
    {
        public string playerId;
        public QuestionType questionType;
        public QuestionCategory category;
        public string questionText;
        public List<string> options;         // Çoktan seçmeli için 4 seçenek
        public int correctAnswerIndex;
        public float correctValue;           // Tahmin sorusu için
        public string valueUnit;
    }

    /// <summary>
    /// Soru bankası değerlendirme verisi
    /// </summary>
    [Serializable]
    public class QuestionReviewData
    {
        public string questionId;
        public string reviewerId;
        public bool isApproved;
        public bool isFlagged;               // Yanlış/uygunsuz işareti
        public int rating;                   // 1-5 arası değerlendirme
        public string comment;
    }
}
