using UnityEngine;

namespace AnadoluFethi.Core.Tutorial
{
    public enum InputConditionType
    {
        KeyDown,
        KeyUp,
        KeyHold,
        AnyKey,
        MouseButton
    }

    public class InputCondition : MonoBehaviour, ITutorialCondition
    {
        [SerializeField] private string _conditionId;
        [SerializeField] private InputConditionType _inputType = InputConditionType.KeyDown;
        [SerializeField] private KeyCode _targetKey = KeyCode.Space;
        [SerializeField] private int _mouseButton;

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

        private void Update()
        {
            if (!_isListening || IsMet)
                return;

            IsMet = CheckInput();
        }

        private bool CheckInput()
        {
            return _inputType switch
            {
                InputConditionType.KeyDown => Input.GetKeyDown(_targetKey),
                InputConditionType.KeyUp => Input.GetKeyUp(_targetKey),
                InputConditionType.KeyHold => Input.GetKey(_targetKey),
                InputConditionType.AnyKey => Input.anyKeyDown,
                InputConditionType.MouseButton => Input.GetMouseButtonDown(_mouseButton),
                _ => false
            };
        }

        private void OnDestroy()
        {
            TutorialManager.Instance?.UnregisterCondition(_conditionId);
        }
    }
}
