using UnityEngine;
using UnityEngine.UI;

namespace AnadoluFethi.Core.Tutorial
{
    public class ButtonClickCondition : MonoBehaviour, ITutorialCondition
    {
        [SerializeField] private string _conditionId;
        [SerializeField] private Button _targetButton;

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
            if (_targetButton != null)
            {
                _targetButton.onClick.AddListener(OnButtonClicked);
            }
        }

        public void StopListening()
        {
            if (_targetButton != null)
            {
                _targetButton.onClick.RemoveListener(OnButtonClicked);
            }
        }

        private void OnButtonClicked()
        {
            IsMet = true;
        }

        private void OnDestroy()
        {
            TutorialManager.Instance?.UnregisterCondition(_conditionId);
        }
    }
}
