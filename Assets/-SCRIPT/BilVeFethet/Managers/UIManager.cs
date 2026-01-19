using System;
using System.Collections;
using System.Collections.Generic;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// UI yoneticisi - tum ekran ve panel gecislerini yonetir
    /// Event-driven mimari ile manager'lardan bagimsiz
    /// </summary>
    public class UIManager : Singleton<UIManager>
    {
        [Header("Main Panels")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private GameObject questionPanel;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private GameObject loadingPanel;

        [Header("Question UI")]
        [SerializeField] private Text questionText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text categoryText;
        [SerializeField] private Button[] answerButtons;
        [SerializeField] private InputField estimationInput;
        [SerializeField] private GameObject multipleChoiceContainer;
        [SerializeField] private GameObject estimationContainer;

        [Header("Player Info")]
        [SerializeField] private Text[] playerNameTexts;
        [SerializeField] private Text[] playerScoreTexts;
        [SerializeField] private Image[] playerColorIndicators;

        [Header("Joker UI")]
        [SerializeField] private Button[] jokerButtons;
        [SerializeField] private GameObject jokerPanel;

        [Header("Phase UI")]
        [SerializeField] private Text phaseText;
        [SerializeField] private Text roundText;
        [SerializeField] private Text turnIndicatorText;

        [Header("Result UI")]
        [SerializeField] private Text[] resultPlayerNames;
        [SerializeField] private Text[] resultScores;
        [SerializeField] private Text[] resultRanks;
        [SerializeField] private GameObject tpBreakdownPanel;

        [Header("Animation")]
        [SerializeField] private float panelTransitionTime = 0.3f;

        // Current UI state
        private UIState _currentState;
        private QuestionData _currentQuestion;
        private bool _hasAnswered;
        private List<int> _eliminatedOptions;

        // Color definitions
        private readonly Color _greenColor = new Color(0.2f, 0.8f, 0.2f);
        private readonly Color _blueColor = new Color(0.2f, 0.4f, 0.9f);
        private readonly Color _redColor = new Color(0.9f, 0.2f, 0.2f);
        private readonly Color _disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        public enum UIState
        {
            Loading,
            Lobby,
            Fetih,
            Savas,
            Question,
            TerritorySelection,
            AttackSelection,
            Results
        }

        protected override void OnSingletonAwake()
        {
            _eliminatedOptions = new List<int>();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        #region Event Subscriptions

        private void SubscribeToEvents()
        {
            GameEvents.OnConnected += HandleConnected;
            GameEvents.OnGameStarting += HandleGameStarting;
            GameEvents.OnPhaseChanged += HandlePhaseChanged;
            GameEvents.OnQuestionReceived += HandleQuestionReceived;
            GameEvents.OnQuestionTimerUpdated += HandleTimerUpdated;
            GameEvents.OnQuestionTimerExpired += HandleTimerExpired;
            GameEvents.OnQuestionResultReceived += HandleQuestionResult;
            GameEvents.OnScoreChanged += HandleScoreChanged;
            GameEvents.OnTurnChanged += HandleTurnChanged;
            GameEvents.OnTerritorySelectionStarted += HandleTerritorySelection;
            GameEvents.OnAttackSelectionStarted += HandleAttackSelection;
            GameEvents.OnJokerResultReceived += HandleJokerResult;
            GameEvents.OnGameEnded += HandleGameEnded;
            GameEvents.OnPlayerEliminated += HandlePlayerEliminated;
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnConnected -= HandleConnected;
            GameEvents.OnGameStarting -= HandleGameStarting;
            GameEvents.OnPhaseChanged -= HandlePhaseChanged;
            GameEvents.OnQuestionReceived -= HandleQuestionReceived;
            GameEvents.OnQuestionTimerUpdated -= HandleTimerUpdated;
            GameEvents.OnQuestionTimerExpired -= HandleTimerExpired;
            GameEvents.OnQuestionResultReceived -= HandleQuestionResult;
            GameEvents.OnScoreChanged -= HandleScoreChanged;
            GameEvents.OnTurnChanged -= HandleTurnChanged;
            GameEvents.OnTerritorySelectionStarted -= HandleTerritorySelection;
            GameEvents.OnAttackSelectionStarted -= HandleAttackSelection;
            GameEvents.OnJokerResultReceived -= HandleJokerResult;
            GameEvents.OnGameEnded -= HandleGameEnded;
            GameEvents.OnPlayerEliminated -= HandlePlayerEliminated;
        }

        #endregion

        #region Panel Management

        private void SetUIState(UIState newState)
        {
            _currentState = newState;
            HideAllPanels();

            switch (newState)
            {
                case UIState.Loading:
                    ShowPanel(loadingPanel);
                    break;
                case UIState.Lobby:
                    ShowPanel(lobbyPanel);
                    break;
                case UIState.Fetih:
                case UIState.Savas:
                case UIState.TerritorySelection:
                case UIState.AttackSelection:
                    ShowPanel(gamePanel);
                    ShowPanel(mapPanel);
                    break;
                case UIState.Question:
                    ShowPanel(gamePanel);
                    ShowPanel(questionPanel);
                    break;
                case UIState.Results:
                    ShowPanel(resultsPanel);
                    break;
            }
        }

        private void HideAllPanels()
        {
            SetPanelActive(lobbyPanel, false);
            SetPanelActive(gamePanel, false);
            SetPanelActive(questionPanel, false);
            SetPanelActive(mapPanel, false);
            SetPanelActive(resultsPanel, false);
            SetPanelActive(loadingPanel, false);
        }

        private void ShowPanel(GameObject panel)
        {
            SetPanelActive(panel, true);
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        #endregion

        #region Event Handlers

        private void HandleConnected()
        {
            SetUIState(UIState.Lobby);
        }

        private void HandleGameStarting(GameStartData data)
        {
            InitializePlayerUI(data.players);
            SetUIState(UIState.Fetih);
        }

        private void HandlePhaseChanged(GamePhase oldPhase, GamePhase newPhase)
        {
            UpdatePhaseText(newPhase);

            switch (newPhase)
            {
                case GamePhase.Fetih:
                    SetUIState(UIState.Fetih);
                    break;
                case GamePhase.Savas:
                    SetUIState(UIState.Savas);
                    break;
                case GamePhase.GameOver:
                    // Results handled by OnGameEnded
                    break;
            }
        }

        private void HandleQuestionReceived(QuestionData question)
        {
            _currentQuestion = question;
            _hasAnswered = false;
            _eliminatedOptions.Clear();
            
            SetUIState(UIState.Question);
            DisplayQuestion(question);
            UpdateJokerButtons(question.questionType);
        }

        private void HandleTimerUpdated(float remaining)
        {
            UpdateTimerDisplay(remaining);
        }

        private void HandleTimerExpired()
        {
            if (!_hasAnswered)
            {
                DisableAllAnswerButtons();
            }
        }

        private void HandleQuestionResult(QuestionResultData result)
        {
            ShowCorrectAnswer(result);
            StartCoroutine(DelayedStateChange(2f));
        }

        private void HandleScoreChanged(string playerId, int oldScore, int newScore)
        {
            UpdatePlayerScore(playerId, newScore);
        }

        private void HandleTurnChanged(string playerId)
        {
            UpdateTurnIndicator(playerId);
        }

        private void HandleTerritorySelection(string playerId, int count)
        {
            if (playerId == PlayerManager.Instance?.LocalPlayerId)
            {
                SetUIState(UIState.TerritorySelection);
                ShowTerritorySelectionUI(count);
            }
        }

        private void HandleAttackSelection(string attackerId)
        {
            if (attackerId == PlayerManager.Instance?.LocalPlayerId)
            {
                SetUIState(UIState.AttackSelection);
                ShowAttackSelectionUI();
            }
        }

        private void HandleJokerResult(JokerUseResult result)
        {
            if (result.success)
            {
                ApplyJokerEffect(result);
            }
        }

        private void HandleGameEnded(List<GameEndPlayerResult> results)
        {
            SetUIState(UIState.Results);
            DisplayResults(results);
        }

        private void HandlePlayerEliminated(string playerId, int round)
        {
            MarkPlayerEliminated(playerId);
        }

        #endregion

        #region Question Display

        private void DisplayQuestion(QuestionData question)
        {
            if (questionText != null)
            {
                questionText.text = question.questionText;
            }

            if (categoryText != null)
            {
                categoryText.text = GetCategoryName(question.category);
            }

            bool isMultipleChoice = question.questionType == QuestionType.CoktanSecmeli;
            
            SetPanelActive(multipleChoiceContainer, isMultipleChoice);
            SetPanelActive(estimationContainer, !isMultipleChoice);

            if (isMultipleChoice)
            {
                DisplayMultipleChoiceOptions(question.options);
            }
            else
            {
                ClearEstimationInput();
            }
        }

        private void DisplayMultipleChoiceOptions(List<string> options)
        {
            for (int i = 0; i < answerButtons.Length; i++)
            {
                if (i < options.Count)
                {
                    answerButtons[i].gameObject.SetActive(true);
                    var buttonText = answerButtons[i].GetComponentInChildren<Text>();
                    if (buttonText != null)
                    {
                        buttonText.text = options[i];
                    }
                    answerButtons[i].interactable = true;
                    
                    int index = i;
                    answerButtons[i].onClick.RemoveAllListeners();
                    answerButtons[i].onClick.AddListener(() => OnAnswerSelected(index));
                }
                else
                {
                    answerButtons[i].gameObject.SetActive(false);
                }
            }
        }

        private void ClearEstimationInput()
        {
            if (estimationInput != null)
            {
                estimationInput.text = "";
                estimationInput.interactable = true;
            }
        }

        private void UpdateTimerDisplay(float remaining)
        {
            if (timerText != null)
            {
                timerText.text = Mathf.CeilToInt(remaining).ToString();
                
                if (remaining <= 5)
                {
                    timerText.color = _redColor;
                }
                else
                {
                    timerText.color = Color.white;
                }
            }
        }

        private void ShowCorrectAnswer(QuestionResultData result)
        {
            if (_currentQuestion?.questionType == QuestionType.CoktanSecmeli)
            {
                for (int i = 0; i < answerButtons.Length; i++)
                {
                    var colors = answerButtons[i].colors;
                    
                    if (i == result.correctAnswerIndex)
                    {
                        colors.normalColor = _greenColor;
                    }
                    else
                    {
                        colors.normalColor = _redColor;
                    }
                    
                    answerButtons[i].colors = colors;
                    answerButtons[i].interactable = false;
                }
            }
        }

        private void DisableAllAnswerButtons()
        {
            foreach (var button in answerButtons)
            {
                button.interactable = false;
            }
            
            if (estimationInput != null)
            {
                estimationInput.interactable = false;
            }
        }

        #endregion

        #region Answer Handling

        public void OnAnswerSelected(int index)
        {
            if (_hasAnswered) return;
            _hasAnswered = true;

            HighlightSelectedAnswer(index);
            QuestionManager.Instance?.SubmitMultipleChoiceAnswer(index);
        }

        public void OnEstimationSubmit()
        {
            if (_hasAnswered) return;
            
            if (float.TryParse(estimationInput.text, out float value))
            {
                _hasAnswered = true;
                QuestionManager.Instance?.SubmitEstimationAnswer(value);
            }
        }

        private void HighlightSelectedAnswer(int index)
        {
            if (index >= 0 && index < answerButtons.Length)
            {
                var colors = answerButtons[index].colors;
                colors.normalColor = _blueColor;
                answerButtons[index].colors = colors;
            }
        }

        #endregion

        #region Joker UI

        private void UpdateJokerButtons(QuestionType questionType)
        {
            var availableJokers = JokerManager.Instance?.GetAvailableJokersForQuestion(questionType);
            
            foreach (var button in jokerButtons)
            {
                button.interactable = false;
            }

            if (availableJokers == null) return;

            foreach (var jokerType in availableJokers)
            {
                int buttonIndex = GetJokerButtonIndex(jokerType);
                if (buttonIndex >= 0 && buttonIndex < jokerButtons.Length)
                {
                    jokerButtons[buttonIndex].interactable = true;
                }
            }
        }

        private int GetJokerButtonIndex(JokerType jokerType)
        {
            return jokerType switch
            {
                JokerType.Yuzde50 => 0,
                JokerType.OyuncularaSor => 1,
                JokerType.Papagan => 2,
                JokerType.Teleskop => 3,
                _ => -1
            };
        }

        public async void OnJokerButtonClicked(int jokerIndex)
        {
            var jokerType = jokerIndex switch
            {
                0 => JokerType.Yuzde50,
                1 => JokerType.OyuncularaSor,
                2 => JokerType.Papagan,
                3 => JokerType.Teleskop,
                _ => JokerType.Yuzde50
            };

            jokerButtons[jokerIndex].interactable = false;
            await JokerManager.Instance?.UseJokerAsync(jokerType);
        }

        private void ApplyJokerEffect(JokerUseResult result)
        {
            switch (result.jokerType)
            {
                case JokerType.Yuzde50:
                    EliminateOptions(result.eliminatedOptionIndices);
                    break;
                case JokerType.OyuncularaSor:
                    ShowAudiencePercentages(result.audiencePercentages);
                    break;
                case JokerType.Papagan:
                    ShowParrotHint(result.parrotHint);
                    break;
            }
        }

        private void EliminateOptions(List<int> indices)
        {
            if (indices == null) return;

            foreach (int index in indices)
            {
                if (index >= 0 && index < answerButtons.Length)
                {
                    _eliminatedOptions.Add(index);
                    answerButtons[index].interactable = false;
                    
                    var colors = answerButtons[index].colors;
                    colors.normalColor = _disabledColor;
                    answerButtons[index].colors = colors;
                }
            }
        }

        private void ShowAudiencePercentages(Dictionary<int, float> percentages)
        {
            if (percentages == null) return;

            foreach (var kvp in percentages)
            {
                if (kvp.Key >= 0 && kvp.Key < answerButtons.Length)
                {
                    var buttonText = answerButtons[kvp.Key].GetComponentInChildren<Text>();
                    if (buttonText != null)
                    {
                        buttonText.text += $" ({kvp.Value:F0}%)";
                    }
                }
            }
        }

        private void ShowParrotHint(float hint)
        {
            // Tahmin sorusu icin ipucu goster
            Debug.Log($"Papagan ipucu: {hint}");
        }

        #endregion

        #region Player UI

        private void InitializePlayerUI(List<PlayerInitData> players)
        {
            for (int i = 0; i < players.Count && i < playerNameTexts.Length; i++)
            {
                if (playerNameTexts[i] != null)
                {
                    playerNameTexts[i].text = players[i].displayName;
                }

                if (playerScoreTexts[i] != null)
                {
                    playerScoreTexts[i].text = "0";
                }

                if (playerColorIndicators[i] != null)
                {
                    playerColorIndicators[i].color = GetPlayerColor(players[i].assignedColor);
                }
            }
        }

        private void UpdatePlayerScore(string playerId, int newScore)
        {
            var players = GameManager.Instance?.GameState?.players;
            if (players == null) return;

            int index = players.FindIndex(p => p.playerId == playerId);
            if (index >= 0 && index < playerScoreTexts.Length && playerScoreTexts[index] != null)
            {
                playerScoreTexts[index].text = newScore.ToString();
            }
        }

        private void MarkPlayerEliminated(string playerId)
        {
            var players = GameManager.Instance?.GameState?.players;
            if (players == null) return;

            int index = players.FindIndex(p => p.playerId == playerId);
            if (index >= 0 && index < playerNameTexts.Length && playerNameTexts[index] != null)
            {
                playerNameTexts[index].color = _disabledColor;
            }
        }

        private Color GetPlayerColor(PlayerColor color)
        {
            return color switch
            {
                PlayerColor.Yesil => _greenColor,
                PlayerColor.Mavi => _blueColor,
                PlayerColor.Kirmizi => _redColor,
                _ => Color.white
            };
        }

        #endregion

        #region Phase UI

        private void UpdatePhaseText(GamePhase phase)
        {
            if (phaseText != null)
            {
                phaseText.text = phase switch
                {
                    GamePhase.Fetih => "FETIH ASAMASI",
                    GamePhase.Savas => "SAVAS ASAMASI",
                    GamePhase.GameOver => "OYUN BITTI",
                    _ => ""
                };
            }
        }

        private void UpdateTurnIndicator(string playerId)
        {
            var player = GameManager.Instance?.GameState?.GetPlayer(playerId);
            if (player != null && turnIndicatorText != null)
            {
                turnIndicatorText.text = $"Sira: {player.displayName}";
                turnIndicatorText.color = GetPlayerColor(player.color);
            }
        }

        #endregion

        #region Territory/Attack Selection UI

        private void ShowTerritorySelectionUI(int count)
        {
            Debug.Log($"Toprak secimi: {count} toprak secebilirsiniz");
        }

        private void ShowAttackSelectionUI()
        {
            Debug.Log("Saldiri hedefi secin");
        }

        #endregion

        #region Results UI

        private void DisplayResults(List<GameEndPlayerResult> results)
        {
            for (int i = 0; i < results.Count && i < resultPlayerNames.Length; i++)
            {
                if (resultPlayerNames[i] != null)
                {
                    resultPlayerNames[i].text = results[i].displayName;
                }

                if (resultScores[i] != null)
                {
                    resultScores[i].text = results[i].finalScore.ToString();
                }

                if (resultRanks[i] != null)
                {
                    resultRanks[i].text = $"{results[i].finalRank}.";
                }
            }
        }

        #endregion

        #region Helpers

        private string GetCategoryName(QuestionCategory category)
        {
            return category switch
            {
                QuestionCategory.Turkce => "Turkce",
                QuestionCategory.Ingilizce => "Ingilizce",
                QuestionCategory.Bilim => "Bilim",
                QuestionCategory.Sanat => "Sanat",
                QuestionCategory.Spor => "Spor",
                QuestionCategory.GenelKultur => "Genel Kultur",
                QuestionCategory.Tarih => "Tarih",
                _ => "Bilinmeyen"
            };
        }

        private IEnumerator DelayedStateChange(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            var phase = GameManager.Instance?.CurrentPhase ?? GamePhase.None;
            if (phase == GamePhase.Fetih)
            {
                SetUIState(UIState.Fetih);
            }
            else if (phase == GamePhase.Savas)
            {
                SetUIState(UIState.Savas);
            }
        }

        #endregion

        #region Loading Screen

        public void ShowLoading(string message = "Yukleniyor...")
        {
            SetUIState(UIState.Loading);
        }

        public void HideLoading()
        {
            SetPanelActive(loadingPanel, false);
        }

        #endregion
    }
}