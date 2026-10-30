using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        private float lastButtonClickTime = -1f;
        private static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        public float MasterVolume => masterVolume;
        public float SFXVolume => sfxVolume;
        public float MusicVolume => musicVolume;
        public AudioLibrary Library => audioLibrary;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GetOrCreateInstance();
        }

        public static AudioManager GetOrCreateInstance()
        {
            if (Instance == null)
            {
                var existing = FindObjectOfType<AudioManager>();
                if (existing != null)
                {
                    Instance = existing;
                    DontDestroyOnLoad(existing.gameObject);
                }
                else
                {
                    var go = new GameObject("AudioManager");
                    Instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (gameObject.name == "AudioManager")
                {
                    Destroy(gameObject);
                }
                else
                {
                    Destroy(this);
                }
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (audioLibrary == null)
            {
                audioLibrary = Resources.Load<AudioLibrary>("DefaultAudioLibrary");
            }

            InitializeAudioSources();
            LoadVolumeSettings();
            PlayMusic();
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

        private void Update()
        {
            // Tự động phát âm thanh khi click vào bất kỳ nút bấm UI nào (Button, Toggle)
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                var es = EventSystem.current;
                if (es != null)
                {
                    var pointerData = new PointerEventData(es) { position = UnityEngine.Input.mousePosition };
                    raycastResults.Clear();
                    es.RaycastAll(pointerData, raycastResults);
                    for (int i = 0; i < raycastResults.Count; i++)
                    {
                        var go = raycastResults[i].gameObject;
                        if (go == null) continue;

                        var sel = go.GetComponentInParent<Selectable>();
                        if (sel != null && (sel is Button || sel is Toggle) && sel.interactable && sel.isActiveAndEnabled)
                        {
                            PlayButtonClick();
                            break;
                        }
                    }
                }
            }
        }

        public void PlayMusic(AudioClip clip = null)
        {
            AudioClip targetClip = clip != null ? clip : (audioLibrary != null ? audioLibrary.BackgroundMusicClip : null);
            if (targetClip == null) return;

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.loop = true;
            }

            if (musicSource.clip == targetClip && musicSource.isPlaying)
            {
                return; // Đang phát bài này rồi
            }

            musicSource.clip = targetClip;
            musicSource.loop = true;
            musicSource.volume = musicVolume * masterVolume;
            musicSource.Play();
        }

        public void StopMusic()
        {
            if (musicSource != null) musicSource.Stop();
        }

        public void PauseMusic()
        {
            if (musicSource != null) musicSource.Pause();
        }

        public void ResumeMusic()
        {
            if (musicSource != null && !musicSource.isPlaying) musicSource.UnPause();
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
            float vol = Mathf.Lerp(0.30f, 1.0f, Mathf.Pow(p, 0.8f)) * sfxVolume;
            src.pitch = UnityEngine.Random.Range(0.97f, 1.03f) * Mathf.Lerp(0.96f, 1.04f, p);
            src.PlayOneShot(audioLibrary.CueHitClip, vol);
        }

        public void PlayBallHit(float relativeSpeed)
        {
            if (audioLibrary == null || audioLibrary.BallHitClip == null) return;
            if (relativeSpeed < 0.05f) return;

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

        public void PlayPocket() => PlayPocketDrop();

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
            if (Time.unscaledTime - lastButtonClickTime < 0.04f) return;
            lastButtonClickTime = Time.unscaledTime;

            if (audioLibrary == null || audioLibrary.ButtonClickClip == null) return;

            if (uiSource != null)
            {
                uiSource.pitch = UnityEngine.Random.Range(0.98f, 1.02f);
                uiSource.PlayOneShot(audioLibrary.ButtonClickClip, sfxVolume);
            }
            else
            {
                var src = GetNextSfxSource();
                src.pitch = UnityEngine.Random.Range(0.98f, 1.02f);
                src.PlayOneShot(audioLibrary.ButtonClickClip, sfxVolume);
            }
        }

        private AudioClip tickClip;
        private bool tickToggle;

        public void PlayTimerTick()
        {
            if (tickClip == null) tickClip = BuildTickClip();

            tickToggle = !tickToggle;
            var src = uiSource != null ? uiSource : GetNextSfxSource();
            if (src == null) return;
            src.pitch = tickToggle ? 1.0f : 0.78f;
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
