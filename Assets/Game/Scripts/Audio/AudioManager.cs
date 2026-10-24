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

        public void PlayCueHit(float power01, bool isBreak = false)
        {
            if (audioLibrary == null) return;

            // Cú phá bi dùng tiếng riêng (nếu có), không biến thiên pitch để giữ âm "chát" đặc trưng
            if (isBreak && audioLibrary.BreakShotClip != null)
            {
                var breakSrc = GetNextSfxSource();
                breakSrc.pitch = UnityEngine.Random.Range(0.99f, 1.01f);
                breakSrc.PlayOneShot(audioLibrary.BreakShotClip, Mathf.Lerp(0.6f, 1.0f, Mathf.Clamp01(power01)) * sfxVolume);
                return;
            }

            if (audioLibrary.CueHitClip == null) return;

            var src = GetNextSfxSource();
            float p = Mathf.Clamp01(power01);
            // Đánh càng mạnh tiếng càng to; pitch hơi cao hơn khi đánh mạnh (đầu cơ nén cứng hơn)
            float vol = Mathf.Lerp(0.30f, 1.0f, Mathf.Pow(p, 0.8f)) * sfxVolume;
            src.pitch = UnityEngine.Random.Range(0.97f, 1.03f) * Mathf.Lerp(0.96f, 1.04f, p);
            src.PlayOneShot(audioLibrary.CueHitClip, vol);
        }

        public void PlayBallHit(float relativeSpeed)
        {
            if (audioLibrary == null || audioLibrary.BallHitClip == null) return;
            if (relativeSpeed < 0.05f) return;

            // Tốc độ va chạm tương đối (m/s) -> âm lượng. Chạm nhẹ vẫn nghe rõ tiếng "tách" nhỏ,
            // va chạm mạnh (phá bi) lên tới 1.0
            float norm = Mathf.Clamp01(relativeSpeed / 6.0f);
            float vol = Mathf.Lerp(0.12f, 1.0f, Mathf.Pow(norm, 0.7f)) * sfxVolume;

            var src = GetNextSfxSource();
            src.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
            src.PlayOneShot(audioLibrary.BallHitClip, vol);
        }

        public void PlayCushionHit(float speed)
        {
            if (audioLibrary == null || audioLibrary.CushionHitClip == null) return;
            if (speed < 0.15f) return;

            float norm = Mathf.Clamp01(speed / 5.0f);
            float vol = Mathf.Lerp(0.10f, 0.85f, Mathf.Pow(norm, 0.8f)) * sfxVolume;

            var src = GetNextSfxSource();
            // Băng bị nén mạnh thì tiếng trầm và nặng hơn
            src.pitch = UnityEngine.Random.Range(0.96f, 1.04f) * Mathf.Lerp(1.04f, 0.96f, norm);
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

        private AudioClip tickClip;
        private bool tickToggle;

        /// <summary>Tiếng tích-tắc đếm giờ (5 giây cuối). Tạo clip bằng code nên không cần thêm file âm thanh.</summary>
        public void PlayTimerTick()
        {
            if (tickClip == null) tickClip = BuildTickClip();

            tickToggle = !tickToggle;
            var src = uiSource != null ? uiSource : GetNextSfxSource();
            if (src == null) return;
            src.pitch = tickToggle ? 1.0f : 0.78f; // tích cao, tắc trầm
            src.PlayOneShot(tickClip, 0.8f * sfxVolume);
        }

        private static AudioClip BuildTickClip()
        {
            const int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.06f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-t * 90f);
                data[i] = (Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 3600f * t) * 0.3f) * env;
            }
            var clip = AudioClip.Create("TimerTick", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
