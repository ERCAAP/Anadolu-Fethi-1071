using UnityEngine;
using UnityEngine.UI;

namespace AnadoluFethi.Core.Tutorial
{
    public class TutorialHighlight : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform _highlightRect;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Image _maskImage;

        [Header("Settings")]
        [SerializeField] private float _padding = 20f;
        [SerializeField] private Color _overlayColor = new Color(0, 0, 0, 0.7f);

        [Header("Animation")]
        [SerializeField] private float _pulseSpeed = 1f;
        [SerializeField] private float _pulseAmount = 5f;

        private Transform _target;
        private Camera _mainCamera;
        private bool _allowInteraction;

        private void Awake()
        {
            _mainCamera = Camera.main;
            Hide();
        }

        private void Update()
        {
            if (_canvasGroup.alpha <= 0 || _target == null)
                return;

            UpdatePosition();
            UpdateSize();
            Animate();
        }

        public void Show(Transform target, bool allowInteraction = true)
        {
            _target = target;
            _allowInteraction = allowInteraction;

            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = !allowInteraction;
            gameObject.SetActive(true);

            UpdatePosition();
            UpdateSize();
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0f;
            _target = null;
            gameObject.SetActive(false);
        }

        private void UpdatePosition()
        {
            if (_target == null)
                return;

            Vector2 screenPos = GetTargetScreenPosition();
            _highlightRect.position = screenPos;
        }

        private void UpdateSize()
        {
            if (_target == null)
                return;

            Vector2 size = GetTargetSize();
            size += Vector2.one * _padding * 2;
            _highlightRect.sizeDelta = size;
        }

        private Vector2 GetTargetScreenPosition()
        {
            if (_target is RectTransform rectTarget)
            {
                return rectTarget.position;
            }

            return _mainCamera.WorldToScreenPoint(_target.position);
        }

        private Vector2 GetTargetSize()
        {
            if (_target is RectTransform rectTarget)
            {
                return rectTarget.sizeDelta;
            }

            var renderer = _target.GetComponent<Renderer>();
            if (renderer != null)
            {
                Vector3 size = renderer.bounds.size;
                Vector3 screenSize = _mainCamera.WorldToScreenPoint(_target.position + size) -
                                    _mainCamera.WorldToScreenPoint(_target.position);
                return new Vector2(Mathf.Abs(screenSize.x), Mathf.Abs(screenSize.y));
            }

            return new Vector2(100, 100);
        }

        private void Animate()
        {
            float pulse = Mathf.Sin(Time.unscaledTime * _pulseSpeed * Mathf.PI * 2) * _pulseAmount;
            Vector2 currentSize = _highlightRect.sizeDelta;
            Vector2 targetSize = GetTargetSize() + Vector2.one * (_padding * 2 + pulse);
            _highlightRect.sizeDelta = Vector2.Lerp(currentSize, targetSize, Time.unscaledDeltaTime * 5f);
        }

        public void SetOverlayColor(Color color)
        {
            _overlayColor = color;
            if (_maskImage != null)
            {
                _maskImage.color = color;
            }
        }
    }
}
