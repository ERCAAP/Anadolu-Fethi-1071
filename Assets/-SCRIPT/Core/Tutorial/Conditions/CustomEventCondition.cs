using UnityEngine;

namespace AnadoluFethi.Core.Tutorial
{
    public class CustomEventCondition : MonoBehaviour, ITutorialCondition
    {
        [SerializeField] private string _conditionId;

        public bool IsMet { get; private set; }

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
        }

        public void StopListening() { }

        public void TriggerCondition()
        {
            IsMet = true;
        }

        public void ResetCondition()
        {
            IsMet = false;
        }

        private void OnDestroy()
        {
            TutorialManager.Instance?.UnregisterCondition(_conditionId);
        }
    }
}
