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
    /// Timer Bar - Alt kısımda renkli elmaslar ve oyuncu piyonları ile zamanlayıcı
    /// Referans tasarıma uygun: elmaslar sırayla yanar, piyonlar ilerler
    /// </summary>
    public class TimerBarWithPawns : MonoBehaviour
    {
        [Header("Timer Bar")]
        [SerializeField] private RectTransform timerBarContainer;
        [SerializeField] private Image timerFillImage;
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Elmaslar (Diamonds)")]
        [SerializeField] private Image[] diamondImages;
        [SerializeField] private Color diamondActiveColor = new Color(1f, 0.84f, 0f); // Altın
        [SerializeField] private Color diamondInactiveColor = new Color(0.3f, 0.3f, 0.3f);
        [SerializeField] private Color diamondWarningColor = new Color(0.9f, 0.3f, 0.3f);

        [Header("Oyuncu Piyonları")]
        [SerializeField] private RectTransform pawnsContainer;
        [SerializeField] private GameObject pawnPrefab;
        [SerializeField] private float pawnStartX = -400f;
        [SerializeField] private float pawnEndX = 400f;
        [SerializeField] private float pawnY = 30f;

        [Header("Oyuncu Renkleri")]
        [SerializeField] private Color greenPlayerColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color bluePlayerColor = new Color(0.2f, 0.4f, 0.9f);
        [SerializeField] private Color redPlayerColor = new Color(0.9f, 0.2f, 0.3f);
        [SerializeField] private Color yellowPlayerColor = new Color(0.9f, 0.8f, 0.2f);

        [Header("Animasyon")]
        [SerializeField] private float pawnMoveSpeed = 2f;
        [SerializeField] private float diamondPulseSpeed = 3f;

        // State
        private List<PawnData> _pawns = new List<PawnData>();
        private float _maxTime = 15f;
        private float _currentTime;
        private bool _isRunning;
        private Coroutine _timerCoroutine;

        private class PawnData
        {
            public string playerId;
            public RectTransform pawnTransform;
            public Image pawnImage;
            public Image borderImage;
            public float targetPosition;
            public float currentPosition;
            public bool hasAnswered;
            public PlayerColor color;
        }

        private void OnEnable()
        {
            GameEvents.OnQuestionTimerStarted += HandleTimerStarted;
            GameEvents.OnQuestionTimerExpired += HandleTimerExpired;
            GameEvents.OnPlayerAnswered += HandlePlayerAnswered;
            GameEvents.OnBotAnswerSubmittedDetailed += HandleBotAnswered;
        }

        private void OnDisable()
        {
            GameEvents.OnQuestionTimerStarted -= HandleTimerStarted;
            GameEvents.OnQuestionTimerExpired -= HandleTimerExpired;
            GameEvents.OnPlayerAnswered -= HandlePlayerAnswered;
            GameEvents.OnBotAnswerSubmittedDetailed -= HandleBotAnswered;
        }

        private void Update()
        {
            if (!_isRunning) return;

            // Piyonları hedefe doğru hareket ettir
            foreach (var pawn in _pawns)
            {
                if (pawn.pawnTransform == null) continue;

                pawn.currentPosition = Mathf.Lerp(
                    pawn.currentPosition,
                    pawn.targetPosition,
                    Time.deltaTime * pawnMoveSpeed
                );

                pawn.pawnTransform.anchoredPosition = new Vector2(
                    Mathf.Lerp(pawnStartX, pawnEndX, pawn.currentPosition),
                    pawnY
                );
            }
        }

        #region Public Methods

        /// <summary>
        /// Oyuncuları başlat
        /// </summary>
        public void Initialize(List<InGamePlayerData> players)
        {
            ClearPawns();

            foreach (var player in players)
            {
                CreatePawn(player);
            }
        }

        /// <summary>
        /// Zamanlayıcıyı başlat
        /// </summary>
        public void StartTimer(float duration)
        {
            _maxTime = duration;
            _currentTime = duration;
            _isRunning = true;

            // Piyonları sıfırla
            foreach (var pawn in _pawns)
            {
                pawn.targetPosition = 0f;
                pawn.currentPosition = 0f;
                pawn.hasAnswered = false;

                // Border rengini sıfırla
                if (pawn.borderImage != null)
                {
                    pawn.borderImage.color = Color.white;
                }
            }

            // Elmasları sıfırla
            ResetDiamonds();

            if (_timerCoroutine != null)
                StopCoroutine(_timerCoroutine);

            _timerCoroutine = StartCoroutine(TimerCoroutine());
        }

        /// <summary>
        /// Zamanlayıcıyı durdur
        /// </summary>
        public void StopTimer()
        {
            _isRunning = false;

            if (_timerCoroutine != null)
            {
                StopCoroutine(_timerCoroutine);
                _timerCoroutine = null;
            }
        }

        /// <summary>
        /// Oyuncu cevapladığında piyon pozisyonunu güncelle
        /// </summary>
        public void SetPlayerAnswered(string playerId, float answerTime, bool isCorrect)
        {
            var pawn = _pawns.Find(p => p.playerId == playerId);
            if (pawn == null) return;

            pawn.hasAnswered = true;

            // Cevap süresine göre pozisyon (hızlı = ileride)
            float normalizedTime = answerTime / _maxTime;
            pawn.targetPosition = 1f - normalizedTime; // Ters orantı - hızlı cevap = ileride

            // Doğru/yanlış göstergesi
            if (pawn.borderImage != null)
            {
                pawn.borderImage.color = isCorrect ? Color.green : Color.red;
            }
        }

        #endregion

        #region Private Methods

        private void CreatePawn(InGamePlayerData player)
        {
            if (pawnPrefab == null || pawnsContainer == null) return;

            var pawnObj = Instantiate(pawnPrefab, pawnsContainer);
            var rectTransform = pawnObj.GetComponent<RectTransform>();

            if (rectTransform == null)
            {
                rectTransform = pawnObj.AddComponent<RectTransform>();
            }

            rectTransform.anchoredPosition = new Vector2(pawnStartX, pawnY);
            rectTransform.sizeDelta = new Vector2(40f, 50f);

            // Ana image (piyon gövdesi)
            var pawnImage = pawnObj.GetComponent<Image>();
            if (pawnImage == null)
            {
                pawnImage = pawnObj.AddComponent<Image>();
            }

            // Oyuncu rengini ayarla
            Color playerColor = GetPlayerColor(player.color);
            pawnImage.color = playerColor;

            // Border (çerçeve) - child olarak
            Image borderImage = null;
            if (pawnObj.transform.childCount > 0)
            {
                var borderObj = pawnObj.transform.GetChild(0);
                borderImage = borderObj.GetComponent<Image>();
            }

            var pawnData = new PawnData
            {
                playerId = player.playerId,
                pawnTransform = rectTransform,
                pawnImage = pawnImage,
                borderImage = borderImage,
                color = player.color,
                targetPosition = 0f,
                currentPosition = 0f,
                hasAnswered = false
            };

            _pawns.Add(pawnData);
        }

        private void ClearPawns()
        {
            foreach (var pawn in _pawns)
            {
                if (pawn.pawnTransform != null)
                {
                    Destroy(pawn.pawnTransform.gameObject);
                }
            }
            _pawns.Clear();
        }

        private Color GetPlayerColor(PlayerColor color)
        {
            return color switch
            {
                PlayerColor.Yesil => greenPlayerColor,
                PlayerColor.Mavi => bluePlayerColor,
                PlayerColor.Kirmizi => redPlayerColor,
                _ => yellowPlayerColor
            };
        }

        private void ResetDiamonds()
        {
            if (diamondImages == null) return;

            foreach (var diamond in diamondImages)
            {
                if (diamond != null)
                {
                    diamond.color = diamondInactiveColor;
                }
            }
        }

        private void UpdateDiamonds(float normalizedTime)
        {
            if (diamondImages == null || diamondImages.Length == 0) return;

            // Kalan zaman oranına göre elmasları yak
            int activeDiamonds = Mathf.CeilToInt(normalizedTime * diamondImages.Length);

            for (int i = 0; i < diamondImages.Length; i++)
            {
                if (diamondImages[i] == null) continue;

                if (i < activeDiamonds)
                {
                    // Aktif elmas - uyarı durumunda kırmızı
                    if (normalizedTime <= 0.25f)
                    {
                        diamondImages[i].color = diamondWarningColor;
                    }
                    else
                    {
                        diamondImages[i].color = diamondActiveColor;
                    }
                }
                else
                {
                    diamondImages[i].color = diamondInactiveColor;
                }
            }
        }

        private IEnumerator TimerCoroutine()
        {
            while (_isRunning && _currentTime > 0)
            {
                _currentTime -= Time.deltaTime;
                float normalizedTime = _currentTime / _maxTime;

                // Timer fill güncelle
                if (timerFillImage != null)
                {
                    timerFillImage.fillAmount = normalizedTime;
                }

                // Timer text güncelle
                if (timerText != null)
                {
                    timerText.text = Mathf.CeilToInt(_currentTime).ToString();
                }

                // Elmasları güncelle
                UpdateDiamonds(normalizedTime);

                // Cevap vermemiş piyonları ilerlet (zaman geçtikçe geride kalırlar)
                foreach (var pawn in _pawns)
                {
                    if (!pawn.hasAnswered)
                    {
                        // Cevap vermeyenler sabit kalır (0 pozisyonunda)
                        pawn.targetPosition = 0f;
                    }
                }

                yield return null;
            }

            _isRunning = false;
        }

        #endregion

        #region Event Handlers

        private void HandleTimerStarted(float duration)
        {
            StartTimer(duration);
        }

        private void HandleTimerExpired()
        {
            StopTimer();

            // Cevap vermeyenleri göster
            foreach (var pawn in _pawns)
            {
                if (!pawn.hasAnswered && pawn.borderImage != null)
                {
                    pawn.borderImage.color = Color.gray;
                }
            }
        }

        private void HandlePlayerAnswered(string playerId, int answerIndex)
        {
            float answerTime = _maxTime - _currentTime;
            // Doğruluk bilgisi burada yok, sonra güncellenecek
            SetPlayerAnswered(playerId, answerTime, true); // Geçici olarak true
        }

        private void HandleBotAnswered(string playerId, int answerIndex, float guessedValue, bool isCorrect, int points)
        {
            float answerTime = _maxTime - _currentTime;
            SetPlayerAnswered(playerId, answerTime, isCorrect);
        }

        #endregion
    }
}
