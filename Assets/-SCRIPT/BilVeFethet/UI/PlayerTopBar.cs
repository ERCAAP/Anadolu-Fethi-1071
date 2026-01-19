using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Managers;

namespace BilVeFethet.UI
{
    /// <summary>
    /// Oyun içi üst bar - 3 oyuncunun bilgilerini gösterir
    /// </summary>
    public class PlayerTopBar : MonoBehaviour
    {
        [Header("Oyuncu Kartları")]
        [SerializeField] private PlayerTopBarCard[] playerCards;

        [Header("Tur Göstergesi")]
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI turnIndicatorText;

        [Header("Animasyon")]
        [SerializeField] private float scoreAnimationDuration = 0.5f;
        [SerializeField] private float highlightDuration = 2f;

        // Cached data
        private List<InGamePlayerData> _players;
        private string _currentTurnPlayerId;

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnScoreChanged += HandleScoreChanged;
            GameEvents.OnTurnChanged += HandleTurnChanged;
            GameEvents.OnPhaseChanged += HandlePhaseChanged;
            GameEvents.OnPlayerEliminated += HandlePlayerEliminated;
            GameEvents.OnPlayerAnswered += HandlePlayerAnswered;

            // InGameFlowManager events
            if (InGameFlowManager.Instance != null)
            {
                InGameFlowManager.Instance.OnRoundChanged += HandleRoundChanged;
                InGameFlowManager.Instance.OnQuestionIndexChanged += HandleQuestionIndexChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnScoreChanged -= HandleScoreChanged;
            GameEvents.OnTurnChanged -= HandleTurnChanged;
            GameEvents.OnPhaseChanged -= HandlePhaseChanged;
            GameEvents.OnPlayerEliminated -= HandlePlayerEliminated;
            GameEvents.OnPlayerAnswered -= HandlePlayerAnswered;

            if (InGameFlowManager.Instance != null)
            {
                InGameFlowManager.Instance.OnRoundChanged -= HandleRoundChanged;
                InGameFlowManager.Instance.OnQuestionIndexChanged -= HandleQuestionIndexChanged;
            }
        }

        #region Public Methods

        /// <summary>
        /// Oyuncuları başlat
        /// </summary>
        public void Initialize(List<InGamePlayerData> players)
        {
            _players = players;

            for (int i = 0; i < playerCards.Length; i++)
            {
                if (i < players.Count)
                {
                    playerCards[i].gameObject.SetActive(true);
                    playerCards[i].Initialize(players[i]);
                }
                else
                {
                    playerCards[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Oyuncuları güncelle
        /// </summary>
        public void UpdatePlayers(List<InGamePlayerData> players)
        {
            _players = players;

            for (int i = 0; i < playerCards.Length && i < players.Count; i++)
            {
                playerCards[i].UpdateData(players[i]);
            }
        }

        /// <summary>
        /// Turu güncelle
        /// </summary>
        public void UpdateRound(int round, int totalRounds = 4)
        {
            if (roundText != null)
                roundText.text = $"Tur {round}/{totalRounds}";
        }

        /// <summary>
        /// Fazı güncelle
        /// </summary>
        public void UpdatePhase(GamePhase phase)
        {
            if (phaseText != null)
            {
                phaseText.text = phase switch
                {
                    GamePhase.Fetih => "FETİH AŞAMASI",
                    GamePhase.Savas => "SAVAŞ AŞAMASI",
                    GamePhase.GameOver => "OYUN BİTTİ",
                    _ => ""
                };
            }
        }

        /// <summary>
        /// Sıradaki oyuncuyu göster
        /// </summary>
        public void SetCurrentTurn(string playerId)
        {
            _currentTurnPlayerId = playerId;

            for (int i = 0; i < playerCards.Length && i < _players?.Count; i++)
            {
                bool isCurrentTurn = _players[i].playerId == playerId;
                playerCards[i].SetTurnIndicator(isCurrentTurn);
            }

            // Turn indicator text
            var player = _players?.Find(p => p.playerId == playerId);
            if (player != null && turnIndicatorText != null)
            {
                turnIndicatorText.text = $"Sıra: {player.displayName}";
            }
        }

        /// <summary>
        /// Oyuncu skorunu animasyonlu güncelle
        /// </summary>
        public void AnimateScoreChange(string playerId, int oldScore, int newScore)
        {
            for (int i = 0; i < playerCards.Length && i < _players?.Count; i++)
            {
                if (_players[i].playerId == playerId)
                {
                    playerCards[i].AnimateScore(oldScore, newScore, scoreAnimationDuration);

                    // Puan değişimi popup
                    int diff = newScore - oldScore;
                    if (diff != 0)
                    {
                        playerCards[i].ShowScorePopup(diff);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Oyuncuyu vurgula (cevap verdiğinde)
        /// </summary>
        public void HighlightPlayer(string playerId, bool isCorrect)
        {
            for (int i = 0; i < playerCards.Length && i < _players?.Count; i++)
            {
                if (_players[i].playerId == playerId)
                {
                    playerCards[i].Highlight(isCorrect, highlightDuration);
                    break;
                }
            }
        }

        /// <summary>
        /// Oyuncuyu elenmiş olarak işaretle
        /// </summary>
        public void MarkEliminated(string playerId)
        {
            for (int i = 0; i < playerCards.Length && i < _players?.Count; i++)
            {
                if (_players[i].playerId == playerId)
                {
                    playerCards[i].SetEliminated(true);
                    break;
                }
            }
        }

        #endregion

        #region Event Handlers

        private void HandleScoreChanged(string playerId, int oldScore, int newScore)
        {
            AnimateScoreChange(playerId, oldScore, newScore);

            // Player data'yı güncelle
            var player = _players?.Find(p => p.playerId == playerId);
            if (player != null)
            {
                player.currentScore = newScore;
            }
        }

        private void HandleTurnChanged(string playerId)
        {
            SetCurrentTurn(playerId);
        }

        private void HandlePhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            UpdatePhase(newPhase);
        }

        private void HandlePlayerEliminated(string playerId, int round)
        {
            MarkEliminated(playerId);

            var player = _players?.Find(p => p.playerId == playerId);
            if (player != null)
            {
                player.isEliminated = true;
            }
        }

        private void HandlePlayerAnswered(string playerId, int answerIndex)
        {
            for (int i = 0; i < playerCards.Length && i < _players?.Count; i++)
            {
                if (_players[i].playerId == playerId)
                {
                    playerCards[i].SetAnswerStatus(true);
                    break;
                }
            }
        }

        private void HandleRoundChanged(int round)
        {
            UpdateRound(round);
        }

        private void HandleQuestionIndexChanged(int current, int total)
        {
            // Her yeni soruda cevap durumlarını sıfırla
            foreach (var card in playerCards)
            {
                card.SetAnswerStatus(false);
            }
        }

        #endregion
    }

    /// <summary>
    /// Tek oyuncu kartı
    /// </summary>
    [Serializable]
    public class PlayerTopBarCard : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image turnIndicatorImage;
        [SerializeField] private Image answerStatusImage;
        [SerializeField] private GameObject eliminatedOverlay;
        [SerializeField] private Transform scorePopupAnchor;

        [Header("Score Popup")]
        [SerializeField] private GameObject scorePopupPrefab;

        [Header("Renkler")]
        [SerializeField] private Color localPlayerColor = new Color(0.2f, 0.6f, 0.9f, 0.8f);
        [SerializeField] private Color otherPlayerColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        [SerializeField] private Color currentTurnColor = new Color(0.9f, 0.8f, 0.2f);
        [SerializeField] private Color correctAnswerColor = new Color(0.3f, 0.9f, 0.4f);
        [SerializeField] private Color wrongAnswerColor = new Color(0.9f, 0.3f, 0.3f);
        [SerializeField] private Color eliminatedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        // Cached data
        private InGamePlayerData _playerData;
        private Coroutine _highlightCoroutine;

        /// <summary>
        /// Kartı başlat
        /// </summary>
        public void Initialize(InGamePlayerData player)
        {
            _playerData = player;

            if (nameText != null)
                nameText.text = player.displayName;

            if (scoreText != null)
                scoreText.text = $"{player.currentScore} TP";

            if (levelText != null)
            {
                // PlayerManager'dan level al
                int level = 1;
                if (player.isLocalPlayer && ProfileManager.Instance != null)
                {
                    level = ProfileManager.Instance.Level;
                }
                levelText.text = $"Lv.{level}";
            }

            // Arka plan rengi
            if (backgroundImage != null)
            {
                backgroundImage.color = player.isLocalPlayer ? localPlayerColor : otherPlayerColor;
            }

            // Turn indicator gizle
            SetTurnIndicator(false);

            // Answer status gizle
            SetAnswerStatus(false);

            // Eliminated overlay gizle
            SetEliminated(false);
        }

        /// <summary>
        /// Verileri güncelle
        /// </summary>
        public void UpdateData(InGamePlayerData player)
        {
            _playerData = player;

            if (scoreText != null)
                scoreText.text = $"{player.currentScore} TP";

            if (player.isEliminated)
                SetEliminated(true);
        }

        /// <summary>
        /// Sıra göstergesini ayarla
        /// </summary>
        public void SetTurnIndicator(bool isCurrentTurn)
        {
            if (turnIndicatorImage != null)
            {
                turnIndicatorImage.gameObject.SetActive(isCurrentTurn);
                turnIndicatorImage.color = currentTurnColor;
            }

            // Arka planı da vurgula
            if (isCurrentTurn && backgroundImage != null)
            {
                backgroundImage.color = new Color(
                    backgroundImage.color.r + 0.1f,
                    backgroundImage.color.g + 0.1f,
                    backgroundImage.color.b + 0.1f,
                    backgroundImage.color.a
                );
            }
        }

        /// <summary>
        /// Cevap durumunu ayarla
        /// </summary>
        public void SetAnswerStatus(bool hasAnswered)
        {
            if (answerStatusImage != null)
            {
                answerStatusImage.gameObject.SetActive(hasAnswered);
                answerStatusImage.color = Color.yellow; // Bekleme rengi
            }
        }

        /// <summary>
        /// Elenmiş olarak işaretle
        /// </summary>
        public void SetEliminated(bool eliminated)
        {
            if (eliminatedOverlay != null)
                eliminatedOverlay.SetActive(eliminated);

            if (eliminated && backgroundImage != null)
                backgroundImage.color = eliminatedColor;
        }

        /// <summary>
        /// Skoru animasyonlu güncelle
        /// </summary>
        public void AnimateScore(int fromScore, int toScore, float duration)
        {
            StartCoroutine(AnimateScoreCoroutine(fromScore, toScore, duration));
        }

        /// <summary>
        /// Skor popup göster
        /// </summary>
        public void ShowScorePopup(int amount)
        {
            if (scorePopupPrefab == null) return;

            var anchor = scorePopupAnchor != null ? scorePopupAnchor : transform;
            var popup = Instantiate(scorePopupPrefab, anchor);

            var text = popup.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = amount > 0 ? $"+{amount}" : $"{amount}";
                text.color = amount > 0 ? correctAnswerColor : wrongAnswerColor;
            }

            StartCoroutine(AnimatePopup(popup));
        }

        /// <summary>
        /// Kartı vurgula
        /// </summary>
        public void Highlight(bool isCorrect, float duration)
        {
            if (_highlightCoroutine != null)
                StopCoroutine(_highlightCoroutine);

            _highlightCoroutine = StartCoroutine(HighlightCoroutine(isCorrect, duration));
        }

        #region Coroutines

        private IEnumerator AnimateScoreCoroutine(int from, int to, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                int current = Mathf.RoundToInt(Mathf.Lerp(from, to, t));

                if (scoreText != null)
                    scoreText.text = $"{current} TP";

                yield return null;
            }

            if (scoreText != null)
                scoreText.text = $"{to} TP";
        }

        private IEnumerator AnimatePopup(GameObject popup)
        {
            float duration = 1.5f;
            float elapsed = 0f;
            Vector3 startPos = popup.transform.localPosition;
            Vector3 endPos = startPos + Vector3.up * 50f;

            var canvasGroup = popup.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = popup.AddComponent<CanvasGroup>();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                popup.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
                canvasGroup.alpha = 1f - (t * t); // Ease out

                yield return null;
            }

            Destroy(popup);
        }

        private IEnumerator HighlightCoroutine(bool isCorrect, float duration)
        {
            Color highlightColor = isCorrect ? correctAnswerColor : wrongAnswerColor;
            Color originalColor = _playerData.isLocalPlayer ? localPlayerColor : otherPlayerColor;

            if (backgroundImage != null)
            {
                backgroundImage.color = highlightColor;

                // Pulse effect
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = (Mathf.Sin(elapsed * 10f) + 1f) / 2f;
                    backgroundImage.color = Color.Lerp(highlightColor, originalColor, t * 0.3f);
                    yield return null;
                }

                backgroundImage.color = _playerData.isEliminated ? eliminatedColor : originalColor;
            }

            // Answer status güncelle
            if (answerStatusImage != null)
            {
                answerStatusImage.color = isCorrect ? correctAnswerColor : wrongAnswerColor;
            }
        }

        #endregion
    }
}
