using UnityEngine;
using System;
using System.Collections.Generic;

namespace AnadoluFethi.Core
{
    public class UIManager : Singleton<UIManager>, IManager
    {
        [Header("Settings")]
        [SerializeField] private Canvas _mainCanvas;
        [SerializeField] private Transform _panelContainer;

        private readonly Dictionary<Type, UIPanel> _panels = new Dictionary<Type, UIPanel>();
        private readonly Stack<UIPanel> _panelHistory = new Stack<UIPanel>();
        private UIPanel _currentPanel;

        public Canvas MainCanvas => _mainCanvas;
        public UIPanel CurrentPanel => _currentPanel;

        public event Action<UIPanel> OnPanelOpened;
        public event Action<UIPanel> OnPanelClosed;

        public void Initialize()
        {
            RegisterExistingPanels();
        }

        public void Dispose()
        {
            _panels.Clear();
            _panelHistory.Clear();
        }

        private void RegisterExistingPanels()
        {
            if (_panelContainer == null)
                return;

            var panels = _panelContainer.GetComponentsInChildren<UIPanel>(true);
            foreach (var panel in panels)
            {
                RegisterPanel(panel);
            }
        }

        public void RegisterPanel(UIPanel panel)
        {
            var type = panel.GetType();
            if (!_panels.ContainsKey(type))
            {
                _panels.Add(type, panel);
                panel.Initialize();
            }
        }

        public T GetPanel<T>() where T : UIPanel
        {
            if (_panels.TryGetValue(typeof(T), out var panel))
            {
                return panel as T;
            }
            return null;
        }

        public void ShowPanel<T>(bool addToHistory = true) where T : UIPanel
        {
            var panel = GetPanel<T>();
            if (panel == null)
                return;

            ShowPanel(panel, addToHistory);
        }

        public void ShowPanel(UIPanel panel, bool addToHistory = true)
        {
            if (_currentPanel != null && addToHistory)
            {
                _panelHistory.Push(_currentPanel);
                _currentPanel.Hide();
            }

            _currentPanel = panel;
            _currentPanel.Show();
            OnPanelOpened?.Invoke(_currentPanel);
        }

        public void HidePanel<T>() where T : UIPanel
        {
            var panel = GetPanel<T>();
            if (panel != null)
            {
                HidePanel(panel);
            }
        }

        public void HidePanel(UIPanel panel)
        {
            panel.Hide();
            OnPanelClosed?.Invoke(panel);

            if (_currentPanel == panel)
            {
                _currentPanel = null;
            }
        }

        public void GoBack()
        {
            if (_panelHistory.Count == 0)
                return;

            if (_currentPanel != null)
            {
                _currentPanel.Hide();
                OnPanelClosed?.Invoke(_currentPanel);
            }

            _currentPanel = _panelHistory.Pop();
            _currentPanel.Show();
            OnPanelOpened?.Invoke(_currentPanel);
        }

        public void HideAll()
        {
            foreach (var panel in _panels.Values)
            {
                panel.Hide();
            }
            _currentPanel = null;
            _panelHistory.Clear();
        }
    }

    public abstract class UIPanel : MonoBehaviour
    {
        [SerializeField] protected CanvasGroup _canvasGroup;

        public bool IsVisible { get; private set; }

        public virtual void Initialize() { }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            IsVisible = true;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            OnShow();
        }

        public virtual void Hide()
        {
            IsVisible = false;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
            OnHide();
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
    }
}
