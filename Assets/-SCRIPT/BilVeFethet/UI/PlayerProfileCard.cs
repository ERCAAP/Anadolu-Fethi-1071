using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;

namespace BilVeFethet.UI
{
    /// <summary>
    /// Oyuncu Profil Kartı - Sol/sağ tarafta gösterilen oyuncu bilgileri
    /// Referans tasarım: Avatar, isim, skor, joker ikonları
    /// </summary>
    public class PlayerProfileCard : MonoBehaviour
    {
        [Header("Ana Bileşenler")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private Image avatarBorderImage;
        [SerializeField] private Image cardBackground;
        [SerializeField] private RectTransform cardContainer;

        [Header("Metin Alanları")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI rankText;

        [Header("Joker Göstergeleri")]
        [SerializeField] private JokerIndicator[] jokerIndicators;

        [Header("Durum Göstergeleri")]
        [SerializeField] private GameObject answerIndicator;
        [SerializeField] private Image answerIndicatorImage;
        [SerializeField] private GameObject eliminatedOverlay;
        [SerializeField] private GameObject currentTurnIndicator;
        [SerializeField] private Image glowEffect;

        [Header("Animasyon")]
        [SerializeField] private float scoreAnimDuration = 0.5f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float glowIntensity = 0.3f;

        [Header("Renkler")]
        [SerializeField] private Color greenColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color blueColor = new Color(0.2f, 0.5f, 0.9f);
        [SerializeField] private Color redColor = new Color(0.9f, 0.2f, 0.3f);
        [SerializeField] private Color yellowColor = new Color(0.9f, 0.8f, 0.2f);
        [SerializeField] private Color correctColor = new Color(0.3f, 0.9f, 0.4f);
        [SerializeField] private Color wrongColor = new Color(0.9f, 0.3f, 0.3f);
        [SerializeField] private Color eliminatedColor = new Color(0.3f, 0.3f, 0.3f, 0.7f);

        [Header("Pozisyon")]
        [SerializeField] private bool isLeftSide = true; // Sol taraf = yerel oyuncu

        // State
        private InGamePlayerData _playerData;
        private int _currentDisplayScore;
        private Coroutine _scoreAnimCoroutine;
        private Coroutine _pulseCoroutine;

        private void OnEnable()
        {
            GameEvents.OnScoreChanged += HandleScoreChanged;
            GameEvents.OnPlayerAnswered += HandlePlayerAnswered;
            GameEvents.OnPlayerEliminated += HandlePlayerEliminated;
            GameEvents.OnTurnChanged += HandleTurnChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnScoreChanged -= HandleScoreChanged;
            GameEvents.OnPlayerAnswered -= HandlePlayerAnswered;
            GameEvents.OnPlayerEliminated -= HandlePlayerEliminated;
            GameEvents.OnTurnChanged -= HandleTurnChanged;
        }

        #region Public Methods

        /// <summary>
        /// Kartı oyuncu verisiyle başlat
        /// </summary>
        public void Initialize(InGamePlayerData player)
        {
            _playerData = player;
            _currentDisplayScore = player.currentScore;

            // İsim
            if (nameText != null)
                nameText.text = player.displayName;

            // Skor
            if (scoreText != null)
                scoreText.text = FormatScore(player.currentScore);

            // Seviye
            if (levelText != null)
            {
                int level = 1;
                if (player.isLocalPlayer && Managers.ProfileManager.Instance != null)
                {
                    level = Managers.ProfileManager.Instance.Level;
                }
                levelText.text = $"Lv.{level}";
            }

            // Oyuncu rengini ayarla
            SetPlayerColor(player.color);

            // Jokerları güncelle
            UpdateJokerIndicators(player.availableJokers);

            // Durum göstergelerini sıfırla
            HideAnswerIndicator();
            SetEliminated(player.isEliminated);
            SetCurrentTurn(false);
        }

        /// <summary>
        /// Skoru güncelle
        /// </summary>
        public void UpdateScore(int newScore, bool animate = true)
        {
            if (animate)
            {
                AnimateScore(_currentDisplayScore, newScore);
            }
            else
            {
                _currentDisplayScore = newScore;
                if (scoreText != null)
                    scoreText.text = FormatScore(newScore);
            }
        }

        /// <summary>
        /// Cevap göstergesini göster
        /// </summary>
        public void ShowAnswerIndicator(bool isCorrect)
        {
            if (answerIndicator != null)
            {
                answerIndicator.SetActive(true);
            }

            if (answerIndicatorImage != null)
            {
                answerIndicatorImage.color = isCorrect ? correctColor : wrongColor;
            }

            // Kart glow efekti
            if (glowEffect != null)
            {
                glowEffect.gameObject.SetActive(true);
                glowEffect.color = new Color(
                    isCorrect ? correctColor.r : wrongColor.r,
                    isCorrect ? correctColor.g : wrongColor.g,
                    isCorrect ? correctColor.b : wrongColor.b,
                    glowIntensity
                );
            }

            // Pulse animasyonu
            if (_pulseCoroutine != null)
                StopCoroutine(_pulseCoroutine);

            _pulseCoroutine = StartCoroutine(PulseEffect(isCorrect ? correctColor : wrongColor, 1.5f));
        }

        /// <summary>
        /// Cevap göstergesini gizle
        /// </summary>
        public void HideAnswerIndicator()
        {
            if (answerIndicator != null)
                answerIndicator.SetActive(false);

            if (glowEffect != null)
                glowEffect.gameObject.SetActive(false);

            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
                _pulseCoroutine = null;
            }
        }

        /// <summary>
        /// Elenmiş olarak işaretle
        /// </summary>
        public void SetEliminated(bool eliminated)
        {
            if (eliminatedOverlay != null)
                eliminatedOverlay.SetActive(eliminated);

            if (eliminated && cardBackground != null)
            {
                cardBackground.color = eliminatedColor;
            }
        }

        /// <summary>
        /// Sıradaki oyuncu olarak işaretle
        /// </summary>
        public void SetCurrentTurn(bool isTurn)
        {
            if (currentTurnIndicator != null)
                currentTurnIndicator.SetActive(isTurn);
        }

        /// <summary>
        /// Joker kullanıldı
        /// </summary>
        public void UseJoker(JokerType jokerType)
        {
            foreach (var indicator in jokerIndicators)
            {
                if (indicator != null && indicator.JokerType == jokerType)
                {
                    indicator.DecreaseCount();
                    break;
                }
            }
        }

        /// <summary>
        /// Player ID kontrolü
        /// </summary>
        public bool IsPlayer(string playerId)
        {
            return _playerData != null && _playerData.playerId == playerId;
        }

        #endregion

        #region Private Methods

        private void SetPlayerColor(PlayerColor color)
        {
            Color playerColor = GetColorFromEnum(color);

            if (avatarBorderImage != null)
                avatarBorderImage.color = playerColor;

            // Kartın kenar rengini de ayarla
            if (cardBackground != null)
            {
                // Hafif bir renk tonu
                cardBackground.color = new Color(
                    playerColor.r * 0.3f,
                    playerColor.g * 0.3f,
                    playerColor.b * 0.3f,
                    0.9f
                );
            }
        }

        private Color GetColorFromEnum(PlayerColor color)
        {
            return color switch
            {
                PlayerColor.Yesil => greenColor,
                PlayerColor.Mavi => blueColor,
                PlayerColor.Kirmizi => redColor,
                _ => yellowColor
            };
        }

        private string FormatScore(int score)
        {
            return $"{score:N0}";
        }

        private void UpdateJokerIndicators(Dictionary<JokerType, int> jokers)
        {
            if (jokerIndicators == null || jokers == null) return;

            foreach (var indicator in jokerIndicators)
            {
                if (indicator == null) continue;

                if (jokers.TryGetValue(indicator.JokerType, out int count))
                {
                    indicator.SetCount(count);
                }
                else
                {
                    indicator.SetCount(0);
                }
            }
        }

        private void AnimateScore(int from, int to)
        {
            if (_scoreAnimCoroutine != null)
                StopCoroutine(_scoreAnimCoroutine);

            _scoreAnimCoroutine = StartCoroutine(ScoreAnimCoroutine(from, to));
        }

        private IEnumerator ScoreAnimCoroutine(int from, int to)
        {
            float elapsed = 0f;

            while (elapsed < scoreAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / scoreAnimDuration;

                // Ease out cubic
                t = 1f - Mathf.Pow(1f - t, 3f);

                _currentDisplayScore = Mathf.RoundToInt(Mathf.Lerp(from, to, t));

                if (scoreText != null)
                    scoreText.text = FormatScore(_currentDisplayScore);

                yield return null;
            }

            _currentDisplayScore = to;
            if (scoreText != null)
                scoreText.text = FormatScore(to);
        }

        private IEnumerator PulseEffect(Color color, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = (Mathf.Sin(elapsed * pulseSpeed * Mathf.PI * 2f) + 1f) / 2f;

                if (glowEffect != null)
                {
                    glowEffect.color = new Color(color.r, color.g, color.b, t * glowIntensity);
                }

                yield return null;
            }

            if (glowEffect != null)
                glowEffect.gameObject.SetActive(false);
        }

        #endregion

        #region Event Handlers

        private void HandleScoreChanged(string playerId, int oldScore, int newScore)
        {
            if (!IsPlayer(playerId)) return;

            UpdateScore(newScore, true);

            // Puan popup göster
            int diff = newScore - oldScore;
            if (diff != 0)
            {
                ShowAnswerIndicator(diff > 0);
            }
        }

        private void HandlePlayerAnswered(string playerId, int answerIndex)
        {
            if (!IsPlayer(playerId)) return;

            // Cevap verildiğini göster (doğru/yanlış henüz belli değil)
            if (answerIndicator != null)
                answerIndicator.SetActive(true);

            if (answerIndicatorImage != null)
                answerIndicatorImage.color = yellowColor; // Bekleme rengi
        }

        private void HandlePlayerEliminated(string playerId, int round)
        {
            if (!IsPlayer(playerId)) return;

            SetEliminated(true);
            if (_playerData != null)
                _playerData.isEliminated = true;
        }

        private void HandleTurnChanged(string playerId)
        {
            SetCurrentTurn(IsPlayer(playerId));
        }

        #endregion
    }

    /// <summary>
    /// Joker göstergesi - Küçük ikon ve sayı
    /// </summary>
    [Serializable]
    public class JokerIndicator : MonoBehaviour
    {
        [SerializeField] private JokerType jokerType;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private GameObject usedOverlay;

        private int _count;

        public JokerType JokerType => jokerType;
        public int Count => _count;

        public void SetCount(int count)
        {
            _count = count;

            if (countText != null)
                countText.text = count.ToString();

            // 0 ise gri göster
            if (iconImage != null)
            {
                iconImage.color = count > 0 ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            }

            if (usedOverlay != null)
                usedOverlay.SetActive(count <= 0);
        }

        public void DecreaseCount()
        {
            SetCount(Mathf.Max(0, _count - 1));
        }
    }
}
