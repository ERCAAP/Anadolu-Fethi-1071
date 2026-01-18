using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace AnadoluFethi.Core.Network.UnityServices
{
    /// <summary>
    /// Unity Relay Service wrapper for P2P connections
    /// Currently running in offline mode - install Unity Relay and Transport packages to enable online features
    /// </summary>
    public class RelayServiceWrapper : MonoBehaviour
    {
        #region Constants
        private const int MAX_CONNECTIONS = 3;
        #endregion

        #region Events
        public event Action<string> OnRelayCreated;
        public event Action OnRelayJoined;
        public event Action<int> OnClientConnected;
        public event Action<int> OnClientDisconnected;
        public event Action<int, byte[]> OnDataReceived;
        public event Action<string> OnError;
        #endregion

        #region Properties
        public bool IsHost { get; private set; }
        public bool IsConnected { get; private set; }
        public string JoinCode { get; private set; }
        public int LocalPlayerIndex { get; private set; }
        public int ConnectedPlayerCount => _connectedClients.Count + (IsHost ? 1 : 0);
        #endregion

        #region Private Fields
        private bool _isInitialized;
        private List<int> _connectedClients = new List<int>();
        private Queue<QueuedMessage> _messageQueue = new Queue<QueuedMessage>();
        #endregion

        #region Unity Lifecycle
        private void Update()
        {
            if (!_isInitialized) return;

            // Process queued messages in offline mode
            ProcessMessageQueue();
        }

        private void OnDestroy()
        {
            Disconnect();
        }
        #endregion

        #region Public Methods - Host
        /// <summary>
        /// Create a relay allocation (host)
        /// </summary>
        public async Task<string> CreateRelayAsync()
        {
            // Offline mode
            JoinCode = GenerateOfflineCode();
            IsHost = true;
            IsConnected = true;
            LocalPlayerIndex = 0;
            _isInitialized = true;

            Debug.Log($"[RelayServiceWrapper] Created offline relay. Join code: {JoinCode}");
            OnRelayCreated?.Invoke(JoinCode);
            await Task.CompletedTask;
            return JoinCode;
        }
        #endregion

        #region Public Methods - Client
        /// <summary>
        /// Join a relay allocation (client)
        /// </summary>
        public async Task<bool> JoinRelayAsync(string joinCode)
        {
            // Offline mode - simulate connection
            JoinCode = joinCode;
            IsHost = false;
            IsConnected = true;
            LocalPlayerIndex = 1;
            _isInitialized = true;

            Debug.Log($"[RelayServiceWrapper] Joined offline relay: {joinCode}");
            OnRelayJoined?.Invoke();
            await Task.CompletedTask;
            return true;
        }
        #endregion

        #region Public Methods - Data Transmission
        /// <summary>
        /// Send data to all connected players
        /// </summary>
        public void SendToAll(byte[] data)
        {
            if (!IsConnected) return;

            // In offline mode, simulate local echo for testing
            // Queue the message to be processed next frame
            _messageQueue.Enqueue(new QueuedMessage
            {
                SenderIndex = LocalPlayerIndex,
                Data = data,
                TargetAll = true
            });
        }

        /// <summary>
        /// Send data to a specific player (host only)
        /// </summary>
        public void SendToPlayer(int playerIndex, byte[] data)
        {
            if (!IsHost || !IsConnected) return;

            _messageQueue.Enqueue(new QueuedMessage
            {
                SenderIndex = LocalPlayerIndex,
                Data = data,
                TargetPlayerIndex = playerIndex
            });
        }

        /// <summary>
        /// Send data to host (client only)
        /// </summary>
        public void SendToHost(byte[] data)
        {
            if (IsHost || !IsConnected) return;

            _messageQueue.Enqueue(new QueuedMessage
            {
                SenderIndex = LocalPlayerIndex,
                Data = data,
                TargetPlayerIndex = 0
            });
        }

        /// <summary>
        /// Disconnect from relay
        /// </summary>
        public void Disconnect()
        {
            _isInitialized = false;
            IsConnected = false;
            IsHost = false;
            _connectedClients.Clear();
            _messageQueue.Clear();
            Debug.Log("[RelayServiceWrapper] Disconnected");
        }

        /// <summary>
        /// Get list of connected client indices
        /// </summary>
        public List<int> GetConnectedClients()
        {
            return new List<int>(_connectedClients);
        }

        /// <summary>
        /// Simulate a client connecting (for offline testing)
        /// </summary>
        public void SimulateClientConnect(int clientIndex)
        {
            if (!IsHost || _connectedClients.Contains(clientIndex)) return;

            _connectedClients.Add(clientIndex);
            Debug.Log($"[RelayServiceWrapper] Simulated client connected: {clientIndex}");
            OnClientConnected?.Invoke(clientIndex);
        }

        /// <summary>
        /// Simulate a client disconnecting (for offline testing)
        /// </summary>
        public void SimulateClientDisconnect(int clientIndex)
        {
            if (!_connectedClients.Contains(clientIndex)) return;

            _connectedClients.Remove(clientIndex);
            Debug.Log($"[RelayServiceWrapper] Simulated client disconnected: {clientIndex}");
            OnClientDisconnected?.Invoke(clientIndex);
        }

        /// <summary>
        /// Simulate receiving data (for offline testing)
        /// </summary>
        public void SimulateDataReceived(int senderIndex, byte[] data)
        {
            OnDataReceived?.Invoke(senderIndex, data);
        }
        #endregion

        #region Private Methods
        private void ProcessMessageQueue()
        {
            while (_messageQueue.Count > 0)
            {
                var message = _messageQueue.Dequeue();

                // In offline mode, we just trigger the receive event locally
                // This allows testing without actual network connectivity
                if (message.TargetAll)
                {
                    // Simulate receiving data from sender
                    OnDataReceived?.Invoke(message.SenderIndex, message.Data);
                }
                else if (message.TargetPlayerIndex == LocalPlayerIndex)
                {
                    OnDataReceived?.Invoke(message.SenderIndex, message.Data);
                }
            }
        }

        private string GenerateOfflineCode()
        {
            return Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        }
        #endregion

        #region Data Classes
        private class QueuedMessage
        {
            public int SenderIndex;
            public byte[] Data;
            public bool TargetAll;
            public int TargetPlayerIndex;
        }
        #endregion
    }
}
