using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace AnadoluFethi.Core
{
    public class SoundManager : Singleton<SoundManager>, IManager
    {
        [Header("Audio Mixer")]
        [SerializeField] private AudioMixerGroup _masterMixerGroup;
        [SerializeField] private AudioMixerGroup _musicMixerGroup;
        [SerializeField] private AudioMixerGroup _sfxMixerGroup;

        [Header("Settings")]
        [SerializeField] private int _sfxSourcePoolSize = 10;
        [SerializeField] private float _defaultMusicVolume = 1f;
        [SerializeField] private float _defaultSFXVolume = 1f;

        private AudioSource _musicSource;
        private readonly Queue<AudioSource> _sfxPool = new Queue<AudioSource>();
        private readonly List<AudioSource> _activeSfxSources = new List<AudioSource>();
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        private float _musicVolume;
        private float _sfxVolume;
        private bool _isMuted;

        public float MusicVolume
        {
            get => _musicVolume;
            set => SetMusicVolume(value);
        }

        public float SFXVolume
        {
            get => _sfxVolume;
            set => SetSFXVolume(value);
        }

        public bool IsMuted => _isMuted;
        public bool IsMusicPlaying => _musicSource != null && _musicSource.isPlaying;

        public void Initialize()
        {
            CreateMusicSource();
            CreateSFXPool();
            LoadVolumeSettings();
        }

        public void Dispose()
        {
            SaveVolumeSettings();
            _clips.Clear();
        }

        private void CreateMusicSource()
        {
            var go = new GameObject("MusicSource");
            go.transform.SetParent(transform);
            _musicSource = go.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.outputAudioMixerGroup = _musicMixerGroup;
        }

        private void CreateSFXPool()
        {
            for (int i = 0; i < _sfxSourcePoolSize; i++)
            {
                CreateSFXSource();
            }
        }

        private AudioSource CreateSFXSource()
        {
            var go = new GameObject("SFXSource");
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = _sfxMixerGroup;
            _sfxPool.Enqueue(source);
            return source;
        }

        public void RegisterClip(string key, AudioClip clip)
        {
            if (!_clips.ContainsKey(key))
            {
                _clips[key] = clip;
            }
        }

        public void PlayMusic(AudioClip clip, bool loop = true, float fadeTime = 0f)
        {
            if (_musicSource.clip == clip && _musicSource.isPlaying)
                return;

            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.volume = _musicVolume;
            _musicSource.Play();
        }

        public void PlayMusic(string key, bool loop = true)
        {
            if (_clips.TryGetValue(key, out var clip))
            {
                PlayMusic(clip, loop);
            }
        }

        public void StopMusic()
        {
            _musicSource.Stop();
        }

        public void PauseMusic()
        {
            _musicSource.Pause();
        }

        public void ResumeMusic()
        {
            _musicSource.UnPause();
        }

        public AudioSource PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            var source = GetSFXSource();
            source.clip = clip;
            source.volume = _sfxVolume * volumeScale;
            source.Play();
            _activeSfxSources.Add(source);
            return source;
        }

        public AudioSource PlaySFX(string key, float volumeScale = 1f)
        {
            if (_clips.TryGetValue(key, out var clip))
            {
                return PlaySFX(clip, volumeScale);
            }
            return null;
        }

        public AudioSource PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
        {
            var source = GetSFXSource();
            source.transform.position = position;
            source.spatialBlend = 1f;
            source.clip = clip;
            source.volume = _sfxVolume * volumeScale;
            source.Play();
            _activeSfxSources.Add(source);
            return source;
        }

        private AudioSource GetSFXSource()
        {
            CleanupFinishedSources();

            if (_sfxPool.Count > 0)
            {
                return _sfxPool.Dequeue();
            }

            return CreateSFXSource();
        }

        private void CleanupFinishedSources()
        {
            for (int i = _activeSfxSources.Count - 1; i >= 0; i--)
            {
                if (!_activeSfxSources[i].isPlaying)
                {
                    _sfxPool.Enqueue(_activeSfxSources[i]);
                    _activeSfxSources.RemoveAt(i);
                }
            }
        }

        public void StopAllSFX()
        {
            foreach (var source in _activeSfxSources)
            {
                source.Stop();
                _sfxPool.Enqueue(source);
            }
            _activeSfxSources.Clear();
        }

        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            _musicSource.volume = _musicVolume;
        }

        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
        }

        public void Mute()
        {
            _isMuted = true;
            AudioListener.volume = 0f;
        }

        public void Unmute()
        {
            _isMuted = false;
            AudioListener.volume = 1f;
        }

        public void ToggleMute()
        {
            if (_isMuted)
                Unmute();
            else
                Mute();
        }

        private void LoadVolumeSettings()
        {
            _musicVolume = PlayerPrefs.GetFloat("MusicVolume", _defaultMusicVolume);
            _sfxVolume = PlayerPrefs.GetFloat("SFXVolume", _defaultSFXVolume);
            _isMuted = PlayerPrefs.GetInt("IsMuted", 0) == 1;

            if (_isMuted)
                Mute();
        }

        private void SaveVolumeSettings()
        {
            PlayerPrefs.SetFloat("MusicVolume", _musicVolume);
            PlayerPrefs.SetFloat("SFXVolume", _sfxVolume);
            PlayerPrefs.SetInt("IsMuted", _isMuted ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
