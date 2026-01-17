using UnityEngine;
using System.Collections.Generic;

namespace AnadoluFethi.Core
{
    public class VFXManager : Singleton<VFXManager>, IManager
    {
        [Header("Settings")]
        [SerializeField] private int _defaultPoolSize = 10;
        [SerializeField] private Transform _poolContainer;

        private readonly Dictionary<string, Queue<ParticleSystem>> _pools = new Dictionary<string, Queue<ParticleSystem>>();
        private readonly Dictionary<string, ParticleSystem> _prefabs = new Dictionary<string, ParticleSystem>();

        public void Initialize()
        {
            if (_poolContainer == null)
            {
                _poolContainer = new GameObject("[VFX_Pool]").transform;
                _poolContainer.SetParent(transform);
            }
        }

        public void Dispose()
        {
            ClearAllPools();
        }

        public void RegisterVFX(string key, ParticleSystem prefab, int poolSize = -1)
        {
            if (_prefabs.ContainsKey(key))
                return;

            _prefabs[key] = prefab;
            _pools[key] = new Queue<ParticleSystem>();

            int size = poolSize > 0 ? poolSize : _defaultPoolSize;
            WarmPool(key, size);
        }

        private void WarmPool(string key, int count)
        {
            if (!_prefabs.TryGetValue(key, out var prefab))
                return;

            for (int i = 0; i < count; i++)
            {
                var instance = CreateInstance(prefab);
                _pools[key].Enqueue(instance);
            }
        }

        private ParticleSystem CreateInstance(ParticleSystem prefab)
        {
            var instance = Instantiate(prefab, _poolContainer);
            instance.gameObject.SetActive(false);
            return instance;
        }

        public ParticleSystem Play(string key, Vector3 position)
        {
            return Play(key, position, Quaternion.identity);
        }

        public ParticleSystem Play(string key, Vector3 position, Quaternion rotation)
        {
            var vfx = GetFromPool(key);
            if (vfx == null)
                return null;

            vfx.transform.SetPositionAndRotation(position, rotation);
            vfx.gameObject.SetActive(true);
            vfx.Play();

            return vfx;
        }

        public ParticleSystem Play(string key, Transform parent)
        {
            var vfx = GetFromPool(key);
            if (vfx == null)
                return null;

            vfx.transform.SetParent(parent);
            vfx.transform.localPosition = Vector3.zero;
            vfx.transform.localRotation = Quaternion.identity;
            vfx.gameObject.SetActive(true);
            vfx.Play();

            return vfx;
        }

        private ParticleSystem GetFromPool(string key)
        {
            if (!_pools.TryGetValue(key, out var pool))
                return null;

            if (pool.Count > 0)
            {
                return pool.Dequeue();
            }

            if (_prefabs.TryGetValue(key, out var prefab))
            {
                return CreateInstance(prefab);
            }

            return null;
        }

        public void ReturnToPool(string key, ParticleSystem vfx)
        {
            if (!_pools.ContainsKey(key))
                return;

            vfx.Stop();
            vfx.gameObject.SetActive(false);
            vfx.transform.SetParent(_poolContainer);
            _pools[key].Enqueue(vfx);
        }

        public void StopAll()
        {
            foreach (var pool in _pools.Values)
            {
                foreach (var vfx in pool)
                {
                    if (vfx.isPlaying)
                    {
                        vfx.Stop();
                    }
                }
            }
        }

        public void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                while (pool.Count > 0)
                {
                    var vfx = pool.Dequeue();
                    if (vfx != null)
                    {
                        Destroy(vfx.gameObject);
                    }
                }
            }
            _pools.Clear();
            _prefabs.Clear();
        }
    }
}
