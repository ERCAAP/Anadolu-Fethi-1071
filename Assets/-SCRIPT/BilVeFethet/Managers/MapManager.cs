using System;
using System.Collections.Generic;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Network;
using BilVeFethet.Utils;
using UnityEngine;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Harita ve toprak yöneticisi - 15 bölgeli Türkiye haritası
    /// </summary>
    public class MapManager : Singleton<MapManager>
    {
        [Header("Map Configuration")]
        [SerializeField] private Transform mapContainer;
        [SerializeField] private GameObject territoryPrefab;
        
        [Header("Colors")]
        [SerializeField] private Color emptyColor = Color.gray;
        [SerializeField] private Color greenColor = Color.green;
        [SerializeField] private Color blueColor = Color.blue;
        [SerializeField] private Color redColor = Color.red;
        [SerializeField] private Color protectedOverlay = new Color(1, 1, 0, 0.3f);

        // Map data
        private MapData _mapData;
        private Dictionary<int, TerritoryVisual> _territoryVisuals;

        // Selection state
        private bool _isSelectionMode;
        private string _selectingPlayerId;
        private int _selectionsRemaining;
        private List<int> _selectableTerritories;
        private Action<int> _onTerritorySelected;

        // Properties
        public MapData MapData => _mapData;
        public bool IsSelectionMode => _isSelectionMode;
        public int SelectionsRemaining => _selectionsRemaining;

        protected override void OnSingletonAwake()
        {
            _territoryVisuals = new Dictionary<int, TerritoryVisual>();
            _selectableTerritories = new List<int>();
        }

        private void OnEnable()
        {
            GameEvents.OnGameStarting += HandleGameStarting;
            GameEvents.OnTerritoryUpdated += HandleTerritoryUpdated;
            GameEvents.OnTerritoryCaptured += HandleTerritoryCaptured;
            GameEvents.OnTerritorySelectionStarted += HandleSelectionStarted;
            GameEvents.OnCastleDamaged += HandleCastleDamaged;
            GameEvents.OnCastleDestroyed += HandleCastleDestroyed;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStarting -= HandleGameStarting;
            GameEvents.OnTerritoryUpdated -= HandleTerritoryUpdated;
            GameEvents.OnTerritoryCaptured -= HandleTerritoryCaptured;
            GameEvents.OnTerritorySelectionStarted -= HandleSelectionStarted;
            GameEvents.OnCastleDamaged -= HandleCastleDamaged;
            GameEvents.OnCastleDestroyed -= HandleCastleDestroyed;
        }

        #region Initialization

        private void HandleGameStarting(GameStartData data)
        {
            _mapData = data.initialMap;
            InitializeMapVisuals();
        }

        private void InitializeMapVisuals()
        {
            foreach (var visual in _territoryVisuals.Values)
            {
                if (visual != null && visual.gameObject != null)
                {
                    Destroy(visual.gameObject);
                }
            }
            _territoryVisuals.Clear();

            foreach (var territory in _mapData.territories)
            {
                CreateTerritoryVisual(territory);
            }

            RefreshAllTerritories();
        }

        private void CreateTerritoryVisual(TerritoryData territory)
        {
            if (territoryPrefab == null || mapContainer == null)
            {
                Debug.LogWarning("[MapManager] Territory prefab or map container not set!");
                return;
            }

            Vector3 position = new Vector3(
                territory.mapPosition.x * 10,
                0,
                territory.mapPosition.y * 10
            );

            var go = Instantiate(territoryPrefab, position, Quaternion.identity, mapContainer);
            go.name = $"Territory_{territory.territoryId}_{territory.territoryName}";

            var visual = go.GetComponent<TerritoryVisual>();
            if (visual == null)
            {
                visual = go.AddComponent<TerritoryVisual>();
            }

            visual.Initialize(territory.territoryId, territory.territoryName, OnTerritoryClicked);
            _territoryVisuals[territory.territoryId] = visual;
        }

        #endregion

        #region Territory Updates

        private void HandleTerritoryUpdated(TerritoryUpdateData update)
        {
            var territory = _mapData.GetTerritory(update.territoryId);
            if (territory == null) return;

            if ((update.changeFlags & TerritoryUpdateData.FLAG_OWNER_CHANGED) != 0)
            {
                territory.ownerId = update.newOwnerId;
                territory.ownerColor = update.newOwnerColor;
            }

            if ((update.changeFlags & TerritoryUpdateData.FLAG_STATE_CHANGED) != 0)
            {
                territory.state = update.newState;
            }

            if ((update.changeFlags & TerritoryUpdateData.FLAG_CASTLE_HEALTH_CHANGED) != 0)
            {
                territory.castleHealth = update.newCastleHealth;
            }

            if ((update.changeFlags & TerritoryUpdateData.FLAG_PROTECTION_CHANGED) != 0)
            {
                territory.isProtected = update.isProtected;
                territory.protectionTurnsLeft = update.protectionTurnsLeft;
            }

            RefreshTerritoryVisual(update.territoryId);
        }

        private void HandleTerritoryCaptured(int territoryId, string oldOwnerId, string newOwnerId)
        {
            RefreshTerritoryVisual(territoryId);
        }

        private void HandleCastleDamaged(string playerId, int remainingHealth)
        {
            var player = GameManager.Instance?.GameState?.GetPlayer(playerId);
            if (player != null)
            {
                var territory = _mapData.GetTerritory(player.castleTerritoryId);
                if (territory != null)
                {
                    territory.castleHealth = remainingHealth;
                    RefreshTerritoryVisual(territory.territoryId);
                }
            }
        }

        private void HandleCastleDestroyed(string playerId)
        {
            var player = GameManager.Instance?.GameState?.GetPlayer(playerId);
            if (player != null)
            {
                foreach (var territoryId in player.ownedTerritories)
                {
                    RefreshTerritoryVisual(territoryId);
                }
            }
        }

        public void RefreshTerritoryVisual(int territoryId)
        {
            if (!_territoryVisuals.TryGetValue(territoryId, out var visual)) return;
            
            var territory = _mapData.GetTerritory(territoryId);
            if (territory == null) return;

            Color color = GetTerritoryColor(territory);
            visual.SetColor(color);
            visual.SetCastleState(territory.state == TerritoryState.Kale, territory.castleHealth);
            visual.SetProtected(territory.isProtected);

            bool selectable = _isSelectionMode && _selectableTerritories.Contains(territoryId);
            visual.SetSelectable(selectable);
        }

        public void RefreshAllTerritories()
        {
            foreach (var territory in _mapData.territories)
            {
                RefreshTerritoryVisual(territory.territoryId);
            }
        }

        private Color GetTerritoryColor(TerritoryData territory)
        {
            if (territory.IsEmpty) return emptyColor;

            return territory.ownerColor switch
            {
                PlayerColor.Yesil => greenColor,
                PlayerColor.Mavi => blueColor,
                PlayerColor.Kirmizi => redColor,
                _ => emptyColor
            };
        }

        #endregion

        #region Territory Selection

        private void HandleSelectionStarted(string playerId, int selectionCount)
        {
            _selectingPlayerId = playerId;
            _selectionsRemaining = selectionCount;
            
            if (playerId == PlayerManager.Instance?.LocalPlayerId)
            {
                StartSelectionMode(selectionCount);
            }
        }

        public void StartSelectionMode(int selectionsAllowed, List<int> specificTerritories = null)
        {
            _isSelectionMode = true;
            _selectionsRemaining = selectionsAllowed;
            _selectableTerritories.Clear();
            
            if (specificTerritories != null)
            {
                _selectableTerritories.AddRange(specificTerritories);
            }
            else
            {
                foreach (var territory in _mapData.GetEmptyTerritories())
                {
                    _selectableTerritories.Add(territory.territoryId);
                }
            }

            RefreshAllTerritories();
        }

        public void StartAttackSelectionMode(string attackerId, bool hasMagicWings, Action<int> onSelected)
        {
            _isSelectionMode = true;
            _selectionsRemaining = 1;
            _onTerritorySelected = onSelected;
            _selectableTerritories.Clear();
            
            var attackableTargets = _mapData.GetAttackableTargets(attackerId, hasMagicWings);
            foreach (var target in attackableTargets)
            {
                _selectableTerritories.Add(target.territoryId);
            }

            RefreshAllTerritories();
        }

        public void EndSelectionMode()
        {
            _isSelectionMode = false;
            _selectionsRemaining = 0;
            _selectableTerritories.Clear();
            _onTerritorySelected = null;
            RefreshAllTerritories();
        }

        private void OnTerritoryClicked(int territoryId)
        {
            if (!_isSelectionMode) return;
            if (!_selectableTerritories.Contains(territoryId)) return;

            if (_onTerritorySelected != null)
            {
                _onTerritorySelected.Invoke(territoryId);
                EndSelectionMode();
                return;
            }

            SelectTerritory(territoryId);
        }

        private async void SelectTerritory(int territoryId)
        {
            var success = await NetworkManager.Instance.SelectTerritoryAsync(territoryId);
            
            if (success)
            {
                _selectionsRemaining--;
                _selectableTerritories.Remove(territoryId);
                GameEvents.TriggerTerritorySelected(PlayerManager.Instance?.LocalPlayerId, territoryId);

                if (_selectionsRemaining <= 0)
                {
                    EndSelectionMode();
                }
                else
                {
                    RefreshAllTerritories();
                }
            }
        }

        #endregion

        #region Query Methods

        public TerritoryData GetTerritory(int territoryId) => _mapData?.GetTerritory(territoryId);
        public List<TerritoryData> GetPlayerTerritories(string playerId) => _mapData?.GetPlayerTerritories(playerId) ?? new List<TerritoryData>();
        public List<TerritoryData> GetEmptyTerritories() => _mapData?.GetEmptyTerritories() ?? new List<TerritoryData>();
        public List<TerritoryData> GetAttackableTargets(string playerId, bool hasMagicWings = false) => _mapData?.GetAttackableTargets(playerId, hasMagicWings) ?? new List<TerritoryData>();

        public bool AreNeighbors(int territoryId1, int territoryId2)
        {
            var territory1 = _mapData?.GetTerritory(territoryId1);
            return territory1?.adjacentTerritoryIds.Contains(territoryId2) ?? false;
        }

        public TerritoryData GetPlayerCastle(string playerId)
        {
            var player = GameManager.Instance?.GameState?.GetPlayer(playerId);
            if (player == null) return null;
            return _mapData?.GetTerritory(player.castleTerritoryId);
        }

        public int GetTerritoryCount(string playerId = null)
        {
            if (playerId == null) return _mapData?.territories.Count ?? 0;
            return _mapData?.GetPlayerTerritories(playerId).Count ?? 0;
        }

        #endregion

        #region Protection

        public void ProtectTerritory(int territoryId, int turns)
        {
            var territory = _mapData?.GetTerritory(territoryId);
            if (territory == null) return;
            territory.isProtected = true;
            territory.protectionTurnsLeft = turns;
            RefreshTerritoryVisual(territoryId);
        }

        public void ReduceProtectionTurns()
        {
            foreach (var territory in _mapData.territories)
            {
                if (territory.isProtected && territory.protectionTurnsLeft > 0)
                {
                    territory.protectionTurnsLeft--;
                    if (territory.protectionTurnsLeft <= 0)
                    {
                        territory.isProtected = false;
                    }
                    RefreshTerritoryVisual(territory.territoryId);
                }
            }
        }

        #endregion
    }

    public class TerritoryVisual : MonoBehaviour
    {
        private int _territoryId;
        private string _territoryName;
        private Action<int> _onClick;
        private Renderer _renderer;

        public void Initialize(int id, string name, Action<int> onClick)
        {
            _territoryId = id;
            _territoryName = name;
            _onClick = onClick;
            _renderer = GetComponent<Renderer>();
        }

        public void SetColor(Color color)
        {
            if (_renderer != null) _renderer.material.color = color;
        }

        public void SetCastleState(bool isCastle, int health) { }
        public void SetProtected(bool isProtected) { }
        public void SetSelectable(bool selectable) { }

        private void OnMouseDown() => _onClick?.Invoke(_territoryId);
    }
}
