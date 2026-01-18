using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Utils;
using UnityEngine;
using UnityEngine.Networking;

namespace BilVeFethet.Network
{
    /// <summary>
    /// Ana network manager - sunucu iletişimini yönetir
    /// REST API + WebSocket hybrid yaklaşım
    /// Bandwidth optimizasyonu için delta senkronizasyon
    /// </summary>
    public class NetworkManager : Singleton<NetworkManager>
    {
        [Header("Server Configuration")]
        [SerializeField] private string baseUrl = "https://api.bilvefethet.com";
        [SerializeField] private string websocketUrl = "wss://ws.bilvefethet.com";
        [SerializeField] private float heartbeatInterval = 5f;
        [SerializeField] private float syncInterval = 1f;
        [SerializeField] private int maxRetryAttempts = 3;
        [SerializeField] private float retryDelay = 2f;

        // Connection state
        private bool _isConnected;
        private bool _isReconnecting;
        private int _currentRetryAttempt;
        private string _authToken;
        private string _sessionId;
        private string _currentGameId;
        
        // Ping tracking
        private float _lastPingTime;
        private int _currentPing;
        
        // Message queue for bandwidth optimization
        private Queue<NetworkMessage> _outgoingQueue;
        private float _lastSyncTime;
        private long _lastServerTimestamp;
        
        // WebSocket (placeholder - gerçek implementasyon için NativeWebSocket veya benzeri kullanılabilir)
        private bool _wsConnected;
        
        public bool IsConnected => _isConnected;
        public int CurrentPing => _currentPing;
        public string CurrentGameId => _currentGameId;

        protected override void OnSingletonAwake()
        {
            _outgoingQueue = new Queue<NetworkMessage>();
        }

        private void Start()
        {
            StartCoroutine(HeartbeatRoutine());
            StartCoroutine(SyncRoutine());
        }

        #region Connection Management

        /// <summary>
        /// Sunucuya bağlan ve kimlik doğrula
        /// </summary>
        public async Task<bool> ConnectAsync(string userId, string authToken)
        {
            _authToken = authToken;
            
            try
            {
                // REST API ile auth
                var authResult = await AuthenticateAsync(userId, authToken);
                if (!authResult.success)
                {
                    GameEvents.TriggerConnectionError(authResult.error, authResult.errorCode);
                    return false;
                }

                _sessionId = authResult.sessionId;
                
                // WebSocket bağlantısı
                await ConnectWebSocketAsync();
                
                _isConnected = true;
                GameEvents.TriggerConnected();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Connection failed: {ex.Message}");
                GameEvents.TriggerConnectionError(ex.Message, -1);
                return false;
            }
        }

        /// <summary>
        /// Bağlantıyı kes
        /// </summary>
        public void Disconnect()
        {
            _isConnected = false;
            _wsConnected = false;
            DisconnectWebSocket();
            GameEvents.TriggerDisconnected("Manual disconnect");
        }

        /// <summary>
        /// Yeniden bağlanma girişimi
        /// </summary>
        private IEnumerator ReconnectRoutine()
        {
            if (_isReconnecting) yield break;
            
            _isReconnecting = true;
            _currentRetryAttempt = 0;

            while (_currentRetryAttempt < maxRetryAttempts && !_isConnected)
            {
                _currentRetryAttempt++;
                GameEvents.TriggerReconnecting(_currentRetryAttempt);
                
                yield return new WaitForSeconds(retryDelay);
                
                // Yeniden bağlanmayı dene
                var task = ConnectWebSocketAsync();
                yield return new WaitUntil(() => task.IsCompleted);
                
                if (_wsConnected)
                {
                    _isConnected = true;
                    GameEvents.TriggerConnected();
                    break;
                }
            }

            if (!_isConnected)
            {
                GameEvents.TriggerConnectionError("Max retry attempts reached", 408);
            }

            _isReconnecting = false;
        }

        #endregion

        #region REST API Calls

        /// <summary>
        /// Kimlik doğrulama
        /// </summary>
        private async Task<AuthResult> AuthenticateAsync(string userId, string authToken)
        {
            var data = new Dictionary<string, string>
            {
                { "userId", userId },
                { "token", authToken }
            };

            var result = await PostAsync<AuthResult>("/auth/login", data);
            return result;
        }

        /// <summary>
        /// Oyun ara ve katıl
        /// </summary>
        public async Task<MatchmakingResult> FindGameAsync()
        {
            GameEvents.TriggerSearchingGame();
            
            var result = await PostAsync<MatchmakingResult>("/game/find", new Dictionary<string, string>
            {
                { "sessionId", _sessionId }
            });

            if (result.success)
            {
                _currentGameId = result.gameId;
                GameEvents.TriggerGameFound(result.gameId);
            }

            return result;
        }

        /// <summary>
        /// Soru iste - bandwidth optimize
        /// </summary>
        public async Task<QuestionData> RequestQuestionAsync(QuestionRequestData request)
        {
            // Sadece gerekli alanları gönder
            var compactRequest = new Dictionary<string, object>
            {
                { "gid", _currentGameId },
                { "pid", request.playerId },
                { "ph", (byte)request.currentPhase },
                { "r", request.roundNumber },
                { "qi", request.questionIndex }
            };

            if (request.preferredCategory.HasValue)
            {
                compactRequest["cat"] = (byte)request.preferredCategory.Value;
            }

            var result = await PostAsync<QuestionResponse>("/game/question", compactRequest);
            
            if (result.success)
            {
                GameEvents.TriggerQuestionReceived(result.question);
                return result.question;
            }

            return null;
        }

        /// <summary>
        /// Cevap gönder - minimal payload
        /// </summary>
        public async Task<QuestionResultData> SubmitAnswerAsync(PlayerAnswerData answer)
        {
            // Bandwidth optimizasyonu: sadece gerekli alanlar
            var compactAnswer = new Dictionary<string, object>
            {
                { "gid", _currentGameId },
                { "qid", answer.questionId },
                { "a", answer.selectedAnswerIndex },
                { "t", Mathf.RoundToInt(answer.answerTime * 100) } // 2 decimal precision, integer olarak
            };

            // Tahmin sorusu ise
            if (answer.guessedValue != 0)
            {
                compactAnswer["g"] = answer.guessedValue;
            }

            // Joker kullanıldıysa
            if (answer.usedJokers != null && answer.usedJokers.Count > 0)
            {
                compactAnswer["j"] = answer.usedJokers.ConvertAll(j => (byte)j);
            }

            var result = await PostAsync<QuestionResultResponse>("/game/answer", compactAnswer);
            
            if (result.success)
            {
                GameEvents.TriggerQuestionResultReceived(result.result);
                return result.result;
            }

            return null;
        }

        /// <summary>
        /// Saldırı gönder
        /// </summary>
        public async Task<AttackResultData> SubmitAttackAsync(AttackData attack)
        {
            var compactAttack = new Dictionary<string, object>
            {
                { "gid", _currentGameId },
                { "tid", attack.targetTerritoryId },
                { "sid", attack.sourceTerritoryId }
            };

            if (attack.usedMagicWings)
            {
                compactAttack["mw"] = true;
            }

            if (attack.forcedCategory.HasValue)
            {
                compactAttack["cat"] = (byte)attack.forcedCategory.Value;
            }

            var result = await PostAsync<AttackResponse>("/game/attack", compactAttack);
            
            if (result.success)
            {
                GameEvents.TriggerAttackResolved(result.result);
                return result.result;
            }

            return null;
        }

        /// <summary>
        /// Joker kullan
        /// </summary>
        public async Task<JokerUseResult> UseJokerAsync(JokerType jokerType, string questionId)
        {
            var request = new Dictionary<string, object>
            {
                { "gid", _currentGameId },
                { "jt", (byte)jokerType },
                { "qid", questionId }
            };

            var result = await PostAsync<JokerResponse>("/game/joker", request);
            
            if (result.success)
            {
                GameEvents.TriggerJokerResultReceived(result.result);
                return result.result;
            }

            return null;
        }

        /// <summary>
        /// Toprak seç
        /// </summary>
        public async Task<bool> SelectTerritoryAsync(int territoryId)
        {
            var request = new Dictionary<string, object>
            {
                { "gid", _currentGameId },
                { "tid", territoryId }
            };

            var result = await PostAsync<TerritorySelectResponse>("/game/territory/select", request);
            
            if (result.success && result.territoryUpdate != null)
            {
                GameEvents.TriggerTerritoryUpdated(result.territoryUpdate);
            }

            return result.success;
        }

        /// <summary>
        /// Oyuncu verilerini al
        /// </summary>
        public async Task<PlayerData> GetPlayerDataAsync(string playerId)
        {
            var result = await GetAsync<PlayerDataResponse>($"/player/{playerId}");
            return result.success ? result.player : null;
        }

        /// <summary>
        /// Sıralama al
        /// </summary>
        public async Task<List<PlayerRankingData>> GetRankingAsync(RankingType type, int offset = 0, int limit = 20)
        {
            var url = $"/ranking/{type.ToString().ToLower()}?offset={offset}&limit={limit}";
            var result = await GetAsync<RankingResponse>(url);
            return result.success ? result.rankings : new List<PlayerRankingData>();
        }

        #endregion

        #region WebSocket Communication

        private async Task ConnectWebSocketAsync()
        {
            // Gerçek implementasyonda NativeWebSocket veya benzeri kullanılır
            // Placeholder
            await Task.Delay(100);
            _wsConnected = true;
        }

        private void DisconnectWebSocket()
        {
            _wsConnected = false;
        }

        /// <summary>
        /// WebSocket mesajı gönder - queue ile batching
        /// </summary>
        public void SendWebSocketMessage(NetworkMessageType type, object data)
        {
            if (!_wsConnected) return;

            var message = new NetworkMessage
            {
                type = type,
                data = data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _outgoingQueue.Enqueue(message);
        }

        /// <summary>
        /// WebSocket mesajlarını işle
        /// </summary>
        private void ProcessIncomingMessage(string jsonMessage)
        {
            try
            {
                var message = JsonUtility.FromJson<NetworkMessage>(jsonMessage);
                
                switch (message.type)
                {
                    case NetworkMessageType.QuestionResponse:
                        var question = JsonUtility.FromJson<QuestionData>(message.data.ToString());
                        GameEvents.TriggerQuestionReceived(question);
                        break;
                        
                    case NetworkMessageType.AnswerResult:
                        var result = JsonUtility.FromJson<QuestionResultData>(message.data.ToString());
                        GameEvents.TriggerQuestionResultReceived(result);
                        break;
                        
                    case NetworkMessageType.PhaseChange:
                        var phaseData = JsonUtility.FromJson<PhaseChangeData>(message.data.ToString());
                        GameEvents.TriggerPhaseChanged(phaseData.oldPhase, phaseData.newPhase);
                        break;
                        
                    case NetworkMessageType.TerritoryUpdate:
                        var territoryUpdate = JsonUtility.FromJson<TerritoryUpdateData>(message.data.ToString());
                        GameEvents.TriggerTerritoryUpdated(territoryUpdate);
                        break;
                        
                    case NetworkMessageType.ScoreUpdate:
                        var scoreData = JsonUtility.FromJson<ScoreUpdateData>(message.data.ToString());
                        GameEvents.TriggerScoreChanged(scoreData.playerId, scoreData.oldScore, scoreData.newScore);
                        break;
                        
                    case NetworkMessageType.GameEnd:
                        var endData = JsonUtility.FromJson<GameEndData>(message.data.ToString());
                        GameEvents.TriggerGameEnded(endData.results);
                        break;
                        
                    case NetworkMessageType.SyncResponse:
                        var syncData = JsonUtility.FromJson<SyncData>(message.data.ToString());
                        _lastServerTimestamp = syncData.timestamp;
                        GameEvents.TriggerSyncCompleted(syncData);
                        break;
                        
                    case NetworkMessageType.Heartbeat:
                        _currentPing = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastPingTime);
                        GameEvents.TriggerPingUpdated(_currentPing);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Failed to process message: {ex.Message}");
            }
        }

        #endregion

        #region HTTP Helpers

        private async Task<T> GetAsync<T>(string endpoint) where T : BaseResponse, new()
        {
            using var request = UnityWebRequest.Get(baseUrl + endpoint);
            request.SetRequestHeader("Authorization", $"Bearer {_authToken}");
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                return JsonUtility.FromJson<T>(request.downloadHandler.text);
            }

            return new T { success = false, error = request.error };
        }

        private async Task<T> PostAsync<T>(string endpoint, object data) where T : BaseResponse, new()
        {
            var json = JsonUtility.ToJson(data);
            var bytes = Encoding.UTF8.GetBytes(json);

            using var request = new UnityWebRequest(baseUrl + endpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {_authToken}");
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                return JsonUtility.FromJson<T>(request.downloadHandler.text);
            }

            return new T { success = false, error = request.error };
        }

        #endregion

        #region Coroutines

        private IEnumerator HeartbeatRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(heartbeatInterval);
                
                if (_wsConnected)
                {
                    _lastPingTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    SendWebSocketMessage(NetworkMessageType.Heartbeat, null);
                }
            }
        }

        private IEnumerator SyncRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(syncInterval);
                
                // Queue'daki mesajları batch olarak gönder
                if (_outgoingQueue.Count > 0 && _wsConnected)
                {
                    var batch = new List<NetworkMessage>();
                    while (_outgoingQueue.Count > 0 && batch.Count < 10)
                    {
                        batch.Add(_outgoingQueue.Dequeue());
                    }
                    
                    // Batch gönder (gerçek implementasyonda)
                    // SendBatch(batch);
                }
            }
        }

        #endregion
    }

    #region Network Data Classes

    [Serializable]
    public class NetworkMessage
    {
        public NetworkMessageType type;
        public object data;
        public long timestamp;
    }

    [Serializable]
    public class BaseResponse
    {
        public bool success;
        public string error;
        public int errorCode;
    }

    [Serializable]
    public class AuthResult : BaseResponse
    {
        public string sessionId;
        public PlayerData player;
    }

    [Serializable]
    public class MatchmakingResult : BaseResponse
    {
        public string gameId;
        public int estimatedWaitTime;
    }

    [Serializable]
    public class QuestionResponse : BaseResponse
    {
        public QuestionData question;
    }

    [Serializable]
    public class QuestionResultResponse : BaseResponse
    {
        public QuestionResultData result;
    }

    [Serializable]
    public class AttackResponse : BaseResponse
    {
        public AttackResultData result;
    }

    [Serializable]
    public class JokerResponse : BaseResponse
    {
        public JokerUseResult result;
    }

    [Serializable]
    public class TerritorySelectResponse : BaseResponse
    {
        public TerritoryUpdateData territoryUpdate;
    }

    [Serializable]
    public class PlayerDataResponse : BaseResponse
    {
        public PlayerData player;
    }

    [Serializable]
    public class RankingResponse : BaseResponse
    {
        public List<PlayerRankingData> rankings;
    }

    [Serializable]
    public class PhaseChangeData
    {
        public GamePhase oldPhase;
        public GamePhase newPhase;
    }

    [Serializable]
    public class ScoreUpdateData
    {
        public string playerId;
        public int oldScore;
        public int newScore;
    }

    [Serializable]
    public class GameEndData
    {
        public List<GameEndPlayerResult> results;
    }

    #endregion
}
