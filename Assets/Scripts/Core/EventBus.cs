using System;
using System.Collections.Generic;

namespace AnadoluFethi.Core
{
    /// <summary>
    /// Oyun içi event sistemi - Publisher/Subscriber pattern.
    /// Sistemler arası gevşek bağlı (loosely coupled) iletişim sağlar.
    /// </summary>
    public static class EventBus
    {
        // Event dictionary - her event tipi için subscriber listesi
        private static Dictionary<Type, List<Delegate>> eventSubscribers = new Dictionary<Type, List<Delegate>>();

        #region Subscribe / Unsubscribe

        /// <summary>
        /// Event'e abone ol.
        /// </summary>
        public static void Subscribe<T>(Action<T> callback) where T : IGameEvent
        {
            // TODO: Event tipini al
            // TODO: Dictionary'de yoksa yeni liste oluştur
            // TODO: Callback'i listeye ekle
        }

        /// <summary>
        /// Event aboneliğini iptal et.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> callback) where T : IGameEvent
        {
            // TODO: Event tipini al
            // TODO: Dictionary'de varsa callback'i listeden çıkar
        }

        #endregion

        #region Publish

        /// <summary>
        /// Event'i yayınla - tüm subscriber'lar bilgilendirilir.
        /// </summary>
        public static void Publish<T>(T gameEvent) where T : IGameEvent
        {
            // TODO: Event tipini al
            // TODO: Dictionary'de varsa tüm callback'leri çağır
            // TODO: Her callback'e gameEvent parametresini gönder
        }

        #endregion

        #region Yardımcı Metodlar

        /// <summary>
        /// Tüm abonelikleri temizle.
        /// </summary>
        public static void Clear()
        {
            // TODO: Dictionary'yi temizle
        }

        /// <summary>
        /// Belirli bir event tipinin aboneliklerini temizle.
        /// </summary>
        public static void Clear<T>() where T : IGameEvent
        {
            // TODO: Belirli event tipinin listesini temizle
        }

        #endregion
    }

    #region Event Interface

    /// <summary>
    /// Tüm game event'lerin implement etmesi gereken interface.
    /// </summary>
    public interface IGameEvent { }

    #endregion

    #region Game State Events

    /// <summary>
    /// Oyun durumu değiştiğinde tetiklenir.
    /// </summary>
    public struct StateChangedEvent : IGameEvent
    {
        public string PreviousState;
        public string NewState;
    }

    #endregion

    #region Question Events

    /// <summary>
    /// Soru başladığında tetiklenir.
    /// </summary>
    public struct QuestionStartedEvent : IGameEvent
    {
        public int QuestionIndex;
        public string QuestionText;
        // TODO: Question data eklenebilir
    }

    /// <summary>
    /// Soru cevaplandığında tetiklenir.
    /// </summary>
    public struct QuestionAnsweredEvent : IGameEvent
    {
        public int QuestionIndex;
        public int SelectedAnswerIndex;
        public bool IsCorrect;
    }

    /// <summary>
    /// Soru süresi dolduğunda tetiklenir.
    /// </summary>
    public struct QuestionTimeoutEvent : IGameEvent
    {
        public int QuestionIndex;
    }

    #endregion

    #region Player Events

    /// <summary>
    /// Sıradaki oyuncu değiştiğinde tetiklenir.
    /// </summary>
    public struct PlayerTurnChangedEvent : IGameEvent
    {
        public int PreviousPlayerIndex;
        public int CurrentPlayerIndex;
    }

    /// <summary>
    /// Oyuncu puan kazandığında tetiklenir.
    /// </summary>
    public struct PlayerScoreChangedEvent : IGameEvent
    {
        public int PlayerIndex;
        public int OldScore;
        public int NewScore;
    }

    /// <summary>
    /// Oyuncu toprak kazandığında tetiklenir.
    /// </summary>
    public struct TerritoryConqueredEvent : IGameEvent
    {
        public int PlayerIndex;
        public string TerritoryId;
    }

    #endregion

    #region Joker Events

    /// <summary>
    /// Joker kullanıldığında tetiklenir.
    /// </summary>
    public struct JokerUsedEvent : IGameEvent
    {
        public int PlayerIndex;
        public string JokerType;
    }

    #endregion

    #region UI Events

    /// <summary>
    /// Panel açıldığında/kapandığında tetiklenir.
    /// </summary>
    public struct UIPanelChangedEvent : IGameEvent
    {
        public string PanelName;
        public bool IsOpened;
    }

    #endregion

    #region Audio Events

    /// <summary>
    /// Ses çalınması gerektiğinde tetiklenir.
    /// </summary>
    public struct PlaySoundEvent : IGameEvent
    {
        public string SoundName;
        public float Volume;
    }

    /// <summary>
    /// Müzik değiştiğinde tetiklenir.
    /// </summary>
    public struct MusicChangedEvent : IGameEvent
    {
        public string MusicName;
        public bool FadeIn;
    }

    #endregion

    #region Game Flow Events

    /// <summary>
    /// Oyun başladığında tetiklenir.
    /// </summary>
    public struct GameStartedEvent : IGameEvent
    {
        public int PlayerCount;
    }

    /// <summary>
    /// Oyun bittiğinde tetiklenir.
    /// </summary>
    public struct GameEndedEvent : IGameEvent
    {
        public int WinnerPlayerIndex;
        public string WinReason;
    }

    /// <summary>
    /// Oyun duraklatıldığında/devam ettiğinde tetiklenir.
    /// </summary>
    public struct GamePausedEvent : IGameEvent
    {
        public bool IsPaused;
    }

    #endregion
}
