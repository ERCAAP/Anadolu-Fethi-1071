using UnityEngine;
using System.Collections.Generic;

namespace AnadoluFethi.Core
{
    public class CoreManager : PersistentSingleton<CoreManager>
    {
        [Header("Manager References")]
        [SerializeField] private GameManager _gameManager;
        [SerializeField] private LevelManager _levelManager;
        [SerializeField] private UIManager _uiManager;
        [SerializeField] private VFXManager _vfxManager;
        [SerializeField] private SoundManager _soundManager;

        private readonly List<IManager> _managers = new List<IManager>();

        public GameManager Game => _gameManager;
        public LevelManager Level => _levelManager;
        public UIManager UI => _uiManager;
        public VFXManager VFX => _vfxManager;
        public SoundManager Sound => _soundManager;

        protected override void InitializeSingleton()
        {
            base.InitializeSingleton();
            RegisterManagers();
            InitializeAllManagers();
        }

        private void RegisterManagers()
        {
            TryRegisterManager(_gameManager);
            TryRegisterManager(_levelManager);
            TryRegisterManager(_uiManager);
            TryRegisterManager(_vfxManager);
            TryRegisterManager(_soundManager);
        }

        private void TryRegisterManager<T>(T manager) where T : MonoBehaviour, IManager
        {
            if (manager != null)
            {
                _managers.Add(manager);
            }
        }

        private void InitializeAllManagers()
        {
            foreach (var manager in _managers)
            {
                manager.Initialize();
            }
        }

        protected override void OnDestroy()
        {
            foreach (var manager in _managers)
            {
                manager.Dispose();
            }
            _managers.Clear();

            base.OnDestroy();
        }
    }
}
