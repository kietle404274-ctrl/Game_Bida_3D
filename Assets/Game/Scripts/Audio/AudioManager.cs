using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Billiards.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Library")]
        [SerializeField] private AudioLibrary audioLibrary;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxMainSource;
        [SerializeField] private AudioSource uiSource;
        [SerializeField] private AudioSource musicSource;

        [Header("SFX Polyphony Pool")]
        [SerializeField] private int sfxPoolSize = 6;
        private List<AudioSource> sfxPool = new List<AudioSource>();
        private int currentPoolIndex = 0;

        private float masterVolume = 1.0f;
        private float sfxVolume = 1.0f;
        private float musicVolume = 0.8f;

        public float MasterVolume => masterVolume;
        public float SFXVolume => sfxVolume;
        public float MusicVolume => musicVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            LoadVolumeSettings();
        }

        private void InitializeAudioSources()
        {
            if (sfxMainSource == null)
            {
                sfxMainSource = gameObject.AddComponent<AudioSource>();
                sfxMainSource.playOnAwake = false;
            }

            if (uiSource == null)
            {
                uiSource = gameObject.AddComponent<AudioSource>();
                uiSource.playOnAwake = false;
            }

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
            }

            // Tạo pool AudioSource cho các va chạm bi đồng thời
            sfxPool.Clear();
            for (int i = 0; i < sfxPoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                sfxPool.Add(src);
            }
        }

        private void LoadVolumeSettings()
        {
            masterVolume = PlayerPrefs.GetFloat("Volume_Master", 1.0f);
            musicVolume = PlayerPrefs.GetFloat("Volume_Music", 0.8f);
            sfxVolume = PlayerPrefs.GetFloat("Volume_SFX", 1.0f);

            ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = masterVolume;
            if (musicSource != null) musicSource.volume = musicVolume * masterVolume;
            if (sfxMainSource != null) sfxMainSource.volume = sfxVolume * masterVolume;
            if (uiSource != null) uiSource.volume = sfxVolume * masterVolume;

            foreach (var src in sfxPool)
            {
                if (src != null) src.volume = sfxVolume * masterVolume;
            }
        }

        public void SetMasterVolume(float vol)
        {
            masterVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("Volume_Master", masterVolume);
            ApplyVolumes();
        }

        public void SetMusicVolume(float vol)
        {
            musicVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("Volume_Music", musicVolume);
            ApplyVolumes();
        }

        public void SetSFXVolume(float vol)
        {
            sfxVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("Volume_SFX", sfxVolume);
            ApplyVolumes();
        }

        private AudioSource GetNextSfxSource()
        {
            if (sfxPool == null || sfxPool.Count == 0) return sfxMainSource;
            var src = sfxPool[currentPoolIndex];
            currentPoolIndex = (currentPoolIndex + 1) % sfxPool.Count;
            return src;
        }

        public void PlayCueHit(float power01)
        {
            if (audioLibrary == null || audioLibrary.CueHitClip == null) return;

            var src = GetNextSfxSource();
            float vol = Mathf.Lerp(0.35f, 1.0f, Mathf.Clamp01(power01)) * sfxVolume;
            src.pitch = audioLibrary.GetRandomPitch();
            src.PlayOneShot(audioLibrary.CueHitClip, vol);
        }

        public void PlayBallHit(float impulse)
        {
            if (audioLibrary == null || audioLibrary.BallHitClip == null) return;

            // Xung lực va chạm càng mạnh, âm lượng càng lớn (chuẩn hóa trong khoảng 0.2f - 1.0f)
            float normImpulse = Mathf.Clamp01(impulse / 5.0f);
            float vol = Mathf.Lerp(0.25f, 1.0f, normImpulse) * sfxVolume;

            var src = GetNextSfxSource();
            src.pitch = audioLibrary.GetRandomPitch();
            src.PlayOneShot(audioLibrary.BallHitClip, vol);
        }

        public void PlayCushionHit(float speed)
        {
            if (audioLibrary == null || audioLibrary.CushionHitClip == null) return;

            float normSpeed = Mathf.Clamp01(speed / 4.0f);
            float vol = Mathf.Lerp(0.2f, 0.9f, normSpeed) * sfxVolume;

            var src = GetNextSfxSource();
            src.pitch = audioLibrary.GetRandomPitch();
            src.PlayOneShot(audioLibrary.CushionHitClip, vol);
        }

        public void PlayPocketDrop()
        {
            if (audioLibrary == null || audioLibrary.PocketDropClip == null) return;

            var src = GetNextSfxSource();
            src.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
            src.PlayOneShot(audioLibrary.PocketDropClip, sfxVolume);
        }

        public void PlayFoul()
        {
            if (audioLibrary == null || audioLibrary.FoulClip == null) return;

            if (sfxMainSource != null)
            {
                sfxMainSource.pitch = 1.0f;
                sfxMainSource.PlayOneShot(audioLibrary.FoulClip, sfxVolume);
            }
        }

        public void PlayWinFanfare()
        {
            if (audioLibrary == null || audioLibrary.WinFanfareClip == null) return;

            if (sfxMainSource != null)
            {
                sfxMainSource.pitch = 1.0f;
                sfxMainSource.PlayOneShot(audioLibrary.WinFanfareClip, sfxVolume);
            }
        }

        public void PlayButtonClick()
        {
            if (audioLibrary == null || audioLibrary.ButtonClickClip == null) return;

            if (uiSource != null)
            {
                uiSource.pitch = UnityEngine.Random.Range(0.98f, 1.02f);
                uiSource.PlayOneShot(audioLibrary.ButtonClickClip, sfxVolume);
            }
        }
    }
}
