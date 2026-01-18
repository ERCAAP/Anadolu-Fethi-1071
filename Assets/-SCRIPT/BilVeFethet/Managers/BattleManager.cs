using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BilVeFethet.Data;
using BilVeFethet.Enums;
using BilVeFethet.Events;
using BilVeFethet.Network;
using BilVeFethet.Utils;
using UnityEngine;

namespace BilVeFethet.Managers
{
    /// <summary>
    /// Savaş mekanik yöneticisi - saldırı/savunma işlemlerini yönetir
    /// </summary>
    public class BattleManager : Singleton<BattleManager>
    {
        [Header("Battle Configuration")]
        [SerializeField] private float attackSelectionTime = 15f;
        [SerializeField] private float battleAnimationTime = 2f;

        // Current battle state
        private bool _isInBattle;
        private AttackData _currentAttack;
        private string _attackerId;
        private string _defenderId;
        private int _targetTerritoryId;
        private int _sourceTerritoryId;
        private bool _isSelectingTarget;
        private bool _usedMagicWings;
        private Coroutine _selectionTimerCoroutine;

        // Properties
        public bool IsInBattle => _isInBattle;
        public bool IsSelectingTarget => _isSelectingTarget;
        public string CurrentAttackerId => _attackerId;
        public string CurrentDefenderId => _defenderId;
        public int TargetTerritoryId => _targetTerritoryId;

        private void OnEnable()
        {
            GameEvents.OnAttackStarted += HandleAttackStarted;
            GameEvents.OnAttackResolved += HandleAttackResolved;
            GameEvents.OnAttackSelectionStarted += HandleAttackSelectionStarted;
            GameEvents.OnQuestionResultReceived += HandleQuestionResult;
        }

        private void OnDisable()
        {
            GameEvents.OnAttackStarted -= HandleAttackStarted;
            GameEvents.OnAttackResolved -= HandleAttackResolved;
            GameEvents.OnAttackSelectionStarted -= HandleAttackSelectionStarted;
            GameEvents.OnQuestionResultReceived -= HandleQuestionResult;
        }

        #region Attack Selection

        /// <summary>
        /// Saldırı seçim aşaması başladı
        /// </summary>
        private void HandleAttackSelectionStarted(string attackerId)
        {
            _attackerId = attackerId;
            _isSelectingTarget = true;
            _usedMagicWings = false;

            // Yerel oyuncu saldırıyorsa seçim modunu başlat
            if (attackerId == PlayerManager.Instance?.LocalPlayerId)
            {
                StartAttackSelection();
            }
        }

        /// <summary>
        /// Saldırı hedefi seçimini başlat
        /// </summary>
        public void StartAttackSelection()
        {
            _isSelectingTarget = true;

            // Sihirli kanatlar kontrol
            bool hasMagicWings = JokerManager.Instance?.HasJoker(JokerType.SihirliKanatlar) ?? false;

            // Harita seçim modunu başlat
            MapManager.Instance?.StartAttackSelectionMode(
                PlayerManager.Instance?.LocalPlayerId,
                _usedMagicWings || hasMagicWings,
                OnTargetSelected
            );

            // Süre sayacı başlat
            _selectionTimerCoroutine = StartCoroutine(SelectionTimerCoroutine());
        }

        /// <summary>
        /// Sihirli kanatları etkinleştir
        /// </summary>
        public bool ActivateMagicWings()
        {
            if (!_isSelectingTarget) return false;
            if (_usedMagicWings) return false;

            if (JokerManager.Instance?.UseJoker(JokerType.SihirliKanatlar) ?? false)
            {
                _usedMagicWings = true;
                
                // Harita seçimini yenile
                MapManager.Instance?.StartAttackSelectionMode(
                    PlayerManager.Instance?.LocalPlayerId,
                    true,
                    OnTargetSelected
                );
                
                return true;
            }

            return false;
        }

        /// <summary>
        /// Hedef seçildiğinde
        /// </summary>
        private void OnTargetSelected(int territoryId)
        {
            if (!_isSelectingTarget) return;

            _targetTerritoryId = territoryId;
            _isSelectingTarget = false;

            if (_selectionTimerCoroutine != null)
            {
                StopCoroutine(_selectionTimerCoroutine);
            }

            // Saldırı için kaynak toprak bul
            _sourceTerritoryId = FindSourceTerritory(territoryId);

            // Saldırıyı başlat
            InitiateAttack();
        }

        /// <summary>
        /// Saldırı için kaynak toprak bul
        /// </summary>
        private int FindSourceTerritory(int targetTerritoryId)
        {
            var localPlayerId = PlayerManager.Instance?.LocalPlayerId;
            var playerTerritories = MapManager.Instance?.GetPlayerTerritories(localPlayerId);

            if (playerTerritories == null) return -1;

            // Sihirli kanatlar kullanıldıysa herhangi bir toprak yeterli
            if (_usedMagicWings && playerTerritories.Count > 0)
            {
                return playerTerritories[0].territoryId;
            }

            // Hedefin komşusu olan bir toprak bul
            foreach (var territory in playerTerritories)
            {
                if (MapManager.Instance.AreNeighbors(territory.territoryId, targetTerritoryId))
                {
                    return territory.territoryId;
                }
            }

            return -1;
        }

        /// <summary>
        /// Seçim süresi sayacı
        /// </summary>
        private IEnumerator SelectionTimerCoroutine()
        {
            yield return new WaitForSeconds(attackSelectionTime);

            if (_isSelectingTarget)
            {
                // Süre doldu, rastgele hedef seç veya saldırıdan vazgeç
                AutoSelectTarget();
            }
        }

        /// <summary>
        /// Otomatik hedef seç
        /// </summary>
        private void AutoSelectTarget()
        {
            var attackableTargets = MapManager.Instance?.GetAttackableTargets(
                PlayerManager.Instance?.LocalPlayerId,
                _usedMagicWings
            );

            if (attackableTargets != null && attackableTargets.Count > 0)
            {
                // İlk hedefi seç
                OnTargetSelected(attackableTargets[0].territoryId);
            }
            else
            {
                // Saldırılacak hedef yok, turu geç
                _isSelectingTarget = false;
                MapManager.Instance?.EndSelectionMode();
            }
        }

        #endregion

        #region Attack Execution

        /// <summary>
        /// Saldırıyı başlat
        /// </summary>
        private async void InitiateAttack()
        {
            var targetTerritory = MapManager.Instance?.GetTerritory(_targetTerritoryId);
            if (targetTerritory == null) return;

            _defenderId = targetTerritory.ownerId;
            _isInBattle = true;

            _currentAttack = new AttackData
            {
                attackerId = _attackerId,
                defenderId = _defenderId,
                targetTerritoryId = _targetTerritoryId,
                sourceTerritoryId = _sourceTerritoryId,
                usedMagicWings = _usedMagicWings
            };

            GameEvents.TriggerAttackStarted(_currentAttack);

            // Sunucuya saldırı gönder
            await NetworkManager.Instance.SubmitAttackAsync(_currentAttack);
        }

        /// <summary>
        /// Kategori seçimi ile saldırı
        /// </summary>
        public async void InitiateAttackWithCategory(QuestionCategory category)
        {
            if (!_isInBattle || _currentAttack == null) return;

            _currentAttack.forcedCategory = category;
            await NetworkManager.Instance.SubmitAttackAsync(_currentAttack);
        }

        /// <summary>
        /// Saldırı başladı (event handler)
        /// </summary>
        private void HandleAttackStarted(AttackData attack)
        {
            _currentAttack = attack;
            _attackerId = attack.attackerId;
            _defenderId = attack.defenderId;
            _targetTerritoryId = attack.targetTerritoryId;
            _sourceTerritoryId = attack.sourceTerritoryId;
            _usedMagicWings = attack.usedMagicWings;
            _isInBattle = true;
        }

        /// <summary>
        /// Saldırı sonuçlandı
        /// </summary>
        private void HandleAttackResolved(AttackResultData result)
        {
            StartCoroutine(ProcessBattleResult(result));
        }

        /// <summary>
        /// Savaş sonucunu işle
        /// </summary>
        private IEnumerator ProcessBattleResult(AttackResultData result)
        {
            // Animasyon süresi
            yield return new WaitForSeconds(battleAnimationTime);

            _isInBattle = false;
            _currentAttack = null;
        }

        /// <summary>
        /// Soru sonucu (savaş için)
        /// </summary>
        private void HandleQuestionResult(QuestionResultData result)
        {
            if (!_isInBattle) return;

            // Savaş sorusu sonucu - saldıran mı savunan mı kazandı?
            // Bu bilgi AttackResultData içinde sunucudan gelecek
        }

        #endregion

        #region Battle Helpers

        /// <summary>
        /// Saldırı yapılabilir mi?
        /// </summary>
        public bool CanAttack(string playerId)
        {
            if (_isInBattle) return false;
            
            var attackableTargets = MapManager.Instance?.GetAttackableTargets(playerId, false);
            return attackableTargets != null && attackableTargets.Count > 0;
        }

        /// <summary>
        /// Kaleye saldırılıyor mu?
        /// </summary>
        public bool IsAttackingCastle()
        {
            if (!_isInBattle) return false;
            
            var territory = MapManager.Instance?.GetTerritory(_targetTerritoryId);
            return territory?.state == TerritoryState.Kale;
        }

        /// <summary>
        /// Yerel oyuncu saldırıyor mu?
        /// </summary>
        public bool IsLocalPlayerAttacking()
        {
            return _attackerId == PlayerManager.Instance?.LocalPlayerId;
        }

        /// <summary>
        /// Yerel oyuncu savunuyor mu?
        /// </summary>
        public bool IsLocalPlayerDefending()
        {
            return _defenderId == PlayerManager.Instance?.LocalPlayerId;
        }

        /// <summary>
        /// Savaşı iptal et
        /// </summary>
        public void CancelBattle()
        {
            _isInBattle = false;
            _isSelectingTarget = false;
            _currentAttack = null;
            MapManager.Instance?.EndSelectionMode();
            
            if (_selectionTimerCoroutine != null)
            {
                StopCoroutine(_selectionTimerCoroutine);
            }
        }

        /// <summary>
        /// Mevcut savaş verisini al
        /// </summary>
        public AttackData GetCurrentAttack()
        {
            return _currentAttack;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Bu maçta yapılan saldırı sayısı
        /// </summary>
        public int GetAttackCount(string playerId)
        {
            var player = GameManager.Instance?.GameState?.GetPlayer(playerId);
            return player?.territoriesCaptured ?? 0;
        }

        /// <summary>
        /// Bu maçta yapılan başarılı savunma sayısı
        /// </summary>
        public int GetSuccessfulDefenseCount(string playerId)
        {
            // Bu değer sunucudan takip edilmeli
            return 0;
        }

        #endregion
    }
}
