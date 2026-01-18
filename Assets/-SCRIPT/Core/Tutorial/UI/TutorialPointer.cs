using UnityEngine;

namespace AnadoluFethi.Core.Tutorial
{
    public class TutorialPointer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform _pointerRect;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Direction Sprites")]
        [SerializeField] private GameObject _upPointer;
        [SerializeField] private GameObject _downPointer;
        [SerializeField] private GameObject _leftPointer;
        [SerializeField] private GameObject _rightPointer;

        [Header("Animation")]
        [SerializeField] private float _bounceAmount = 10f;
        [SerializeField] private float _bounceSpeed = 2f;

        private Transform _target;
        private Vector2 _offset;
        private TutorialPointerDirection _direction;
        private Camera _mainCamera;
        private Canvas _parentCanvas;
        private Vector2 _fixedPosition;
        private bool _useFixedPosition;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _parentCanvas = GetComponentInParent<Canvas>();
            Hide();
        }

        private void Update()
        {
            if (_canvasGroup.alpha <= 0)
                return;

            UpdatePosition();
            Animate();
        }

        public void Show(Transform target, TutorialPointerDirection direction, Vector2 offset = default)
        {
            _target = target;
            _direction = direction;
            _offset = offset;
            _useFixedPosition = false;

            SetDirectionSprite(direction);
            _canvasGroup.alpha = 1f;
            gameObject.SetActive(true);
        }

        public void ShowAtPosition(Vector2 position, TutorialPointerDirection direction)
        {
            _target = null;
            _direction = direction;
            _fixedPosition = position;
            _useFixedPosition = true;

            SetDirectionSprite(direction);
            _pointerRect.anchoredPosition = position;
            _canvasGroup.alpha = 1f;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0f;
            _target = null;
            gameObject.SetActive(false);
        }

        private void SetDirectionSprite(TutorialPointerDirection direction)
        {
            _upPointer?.SetActive(direction == TutorialPointerDirection.Up);
            _downPointer?.SetActive(direction == TutorialPointerDirection.Down);
            _leftPointer?.SetActive(direction == TutorialPointerDirection.Left);
            _rightPointer?.SetActive(direction == TutorialPointerDirection.Right);
        }

        private void UpdatePosition()
        {
            if (_useFixedPosition)
            {
                _pointerRect.anchoredPosition = _fixedPosition + GetAnimationOffset();
                return;
            }

            if (_target == null)
                return;

            Vector2 screenPos = GetTargetScreenPosition();
            screenPos += _offset;
            screenPos += GetDirectionOffset();

            _pointerRect.position = screenPos;
        }

        private Vector2 GetTargetScreenPosition()
        {
            if (_target is RectTransform rectTarget)
            {
                return rectTarget.position;
            }

            return _mainCamera.WorldToScreenPoint(_target.position);
        }

        private Vector2 GetDirectionOffset()
        {
            float baseOffset = 50f;

            return _direction switch
            {
                TutorialPointerDirection.Up => new Vector2(0, -baseOffset),
                TutorialPointerDirection.Down => new Vector2(0, baseOffset),
                TutorialPointerDirection.Left => new Vector2(baseOffset, 0),
                TutorialPointerDirection.Right => new Vector2(-baseOffset, 0),
                _ => Vector2.zero
            };
        }

        private void Animate()
        {
            if (_useFixedPosition)
                return;

            Vector2 animOffset = GetAnimationOffset();
            _pointerRect.anchoredPosition += animOffset;
        }

        private Vector2 GetAnimationOffset()
        {
            float bounce = Mathf.Sin(Time.unscaledTime * _bounceSpeed) * _bounceAmount;

            return _direction switch
            {
                TutorialPointerDirection.Up => new Vector2(0, -bounce),
                TutorialPointerDirection.Down => new Vector2(0, bounce),
                TutorialPointerDirection.Left => new Vector2(bounce, 0),
                TutorialPointerDirection.Right => new Vector2(-bounce, 0),
                _ => Vector2.zero
            };
        }
    }
}
