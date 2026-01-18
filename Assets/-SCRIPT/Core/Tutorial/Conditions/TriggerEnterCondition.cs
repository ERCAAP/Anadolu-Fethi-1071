using UnityEngine;

namespace AnadoluFethi.Core.Tutorial
{
    public class TriggerEnterCondition : MonoBehaviour, ITutorialCondition
    {
        [SerializeField] private string _conditionId;
        [SerializeField] private string _targetTag = "Player";
        [SerializeField] private bool _useLayer;
        [SerializeField] private LayerMask _targetLayer;

        public bool IsMet { get; private set; }

        private bool _isListening;

        private void Awake()
        {
            if (!string.IsNullOrEmpty(_conditionId))
            {
                TutorialManager.Instance?.RegisterCondition(_conditionId, this);
            }
        }

        public void StartListening()
        {
            IsMet = false;
            _isListening = true;
        }

        public void StopListening()
        {
            _isListening = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isListening)
                return;

            if (IsValidTarget(other.gameObject))
            {
                IsMet = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isListening)
                return;

            if (IsValidTarget(other.gameObject))
            {
                IsMet = true;
            }
        }

        private bool IsValidTarget(GameObject obj)
        {
            if (_useLayer)
            {
                return (_targetLayer & (1 << obj.layer)) != 0;
            }

            return obj.CompareTag(_targetTag);
        }

        private void OnDestroy()
        {
            TutorialManager.Instance?.UnregisterCondition(_conditionId);
        }
    }
}
