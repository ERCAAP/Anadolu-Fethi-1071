using System;

namespace BilVeFethet.Enums
{
    /// <summary>
    /// Oyunun ana aşamaları
    /// </summary>
    public enum GamePhase
    {
        None = 0,
        Lobby = 1,           // Oyuncu bekleme
        Fetih = 2,           // Fetih aşaması - Toprak ele geçirme
        Savas = 3,           // Savaş aşaması
        GameOver = 4         // Oyun sonu
    }

    /// <summary>
    /// Fetih aşamasındaki alt durumlar
    /// </summary>
    public enum FetihState
    {
        WaitingQuestion = 0,     // Soru bekleniyor
        AnsweringQuestion = 1,   // Soru cevaplanıyor
        SelectingTerritory = 2,  // Toprak seçiliyor
        TerritoryAssigned = 3    // Toprak atandı
    }

    /// <summary>
    /// Savaş aşamasındaki alt durumlar
    /// </summary>
    public enum SavasState
    {
        SelectingTarget = 0,     // Saldırı hedefi seçiliyor
        WaitingQuestion = 1,     // Soru bekleniyor
        AnsweringQuestion = 2,   // Soru cevaplanıyor
        ResolvingBattle = 3,     // Savaş sonucu hesaplanıyor
        TurnEnded = 4            // Tur sona erdi
    }

    /// <summary>
    /// Oyuncu renkleri
    /// </summary>
    public enum PlayerColor
    {
        Yesil = 0,   // Yeşil
        Mavi = 1,    // Mavi
        Kirmizi = 2  // Kırmızı
    }

    /// <summary>
    /// Toprak durumları
    /// </summary>
    public enum TerritoryState
    {
        Bos = 0,         // Boş - fethedilmemiş
        Normal = 1,      // Normal toprak
        Kale = 2,        // Ana kale (3 kuleli)
        Korunmali = 3    // Ekstra koruma jokeri ile korunan
    }

    /// <summary>
    /// Soru tipleri
    /// </summary>
    public enum QuestionType
    {
        CoktanSecmeli = 0,   // 4 seçenekli çoktan seçmeli
        Tahmin = 1           // Sayısal tahmin sorusu
    }

    /// <summary>
    /// Soru zorluk seviyeleri
    /// </summary>
    public enum QuestionDifficulty
    {
        Kolay = 0,   // Kolay sorular
        Orta = 1,    // Orta zorlukta sorular
        Zor = 2      // Zor sorular
    }

    /// <summary>
    /// Soru kategorileri
    /// </summary>
    public enum QuestionCategory
    {
        Turkce = 0,          // Türkçe dil bilgisi
        Ingilizce = 1,       // İngilizce
        Bilim = 2,           // Bilim ve doğa
        Sanat = 3,           // Sanat ve edebiyat
        Spor = 4,            // Spor
        GenelKultur = 5,     // Genel kültür
        Tarih = 6            // Tarih
    }

    /// <summary>
    /// Joker tipleri
    /// </summary>
    public enum JokerType
    {
        Yuzde50 = 0,           // %50 - 2 yanlış şık elenir (Seviye 2)
        OyuncularaSor = 1,      // Oyuncuların cevaplarını görme (Seviye 6)
        Papagan = 2,            // Tahmin sorularında yardım (Seviye 9)
        Teleskop = 3,           // 4 seçenek sunar (Seviye 3)
        SihirliKanatlar = 4,    // Uzun mesafe saldırı (Seviye 4)
        EkstraKoruma = 5,       // Kale/kule koruma (Seviye 8)
        KategoriSecme = 6       // Kategori seçme (Seviye 7)
    }

    /// <summary>
    /// Madalya tipleri
    /// </summary>
    public enum MedalType
    {
        Yenilmez = 0,          // Üst üste galibiyet serisi
        CokBilmis = 1,         // Yenilgisizlik serisi
        KuleDusmani = 2,       // Haftalık yıkılan kule sayısı
        BilgeKagan = 3,        // Tek oyunda max TP
        TecrubeCanavari = 4,   // Haftalık toplam TP
        BuyukDahi = 5          // Haftalık sıralama yüzdesi
    }

    /// <summary>
    /// Madalya dereceleri (1-7)
    /// </summary>
    public enum MedalGrade
    {
        None = 0,
        Grade1 = 1,
        Grade2 = 2,
        Grade3 = 3,
        Grade4 = 4,
        Grade5 = 5,
        Grade6 = 6,
        Grade7 = 7
    }

    /// <summary>
    /// Muhafız tipleri
    /// </summary>
    public enum GuardianType
    {
        Guardian1 = 1,
        Guardian2 = 2,
        Guardian3 = 3,
        Guardian4 = 4,
        Guardian5 = 5
    }

    /// <summary>
    /// Sıralama türleri
    /// </summary>
    public enum RankingType
    {
        Bireysel = 0,    // Kişisel sıralama
        Haftalik = 1,    // Haftalık sıralama
        Arkadaslar = 2,  // Arkadaş sıralaması
        Sehir = 3        // Şehir sıralaması
    }

    /// <summary>
    /// Network mesaj tipleri - düşük bandwidth için optimize
    /// </summary>
    public enum NetworkMessageType : byte
    {
        // Lobby
        JoinGame = 1,
        LeaveGame = 2,
        GameStart = 3,
        
        // Soru
        QuestionRequest = 10,
        QuestionResponse = 11,
        AnswerSubmit = 12,
        AnswerResult = 13,
        
        // Oyun durumu
        PhaseChange = 20,
        TurnChange = 21,
        TerritoryUpdate = 22,
        ScoreUpdate = 23,
        
        // Saldırı
        AttackRequest = 30,
        AttackResult = 31,
        DefenseResult = 32,
        
        // Joker
        JokerUse = 40,
        JokerResult = 41,
        
        // Oyun sonu
        GameEnd = 50,
        RankingUpdate = 51,
        
        // Senkronizasyon
        SyncRequest = 60,
        SyncResponse = 61,
        Heartbeat = 62,
        
        // Hata
        Error = 255
    }

    /// <summary>
    /// Bonus TP tipleri
    /// </summary>
    public enum BonusTPType
    {
        PuanYuzdesi = 0,       // Oyundaki puan yüzdesi
        Rakip = 1,             // Rakip seviye bonusu
        GalibiyetSerisi = 2,   // Üst üste galibiyet
        YenilgisizlikSerisi = 3 // Yenilgisizlik serisi
    }

    /// <summary>
    /// Saldırı sonuçları
    /// </summary>
    public enum AttackResult
    {
        Success = 0,           // Saldırı başarılı - toprak ele geçirildi
        Failed = 1,            // Saldırı başarısız - savunma kazandı
        CastleHit = 2,         // Kaleye hasar verildi (3 haktan biri)
        CastleDestroyed = 3,   // Kale yıkıldı - oyuncu elendi
        InvalidTarget = 4      // Geçersiz hedef
    }
}