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
        private AudioSource timerSource;

        [Header("SFX Polyphony Pool")]
        [SerializeField] private int sfxPoolSize = 6;
        private List<AudioSource> sfxPool = new List<AudioSource>();
        private int currentPoolIndex = 0;

        private float masterVolume = 1.0f;
        private float sfxVolume = 1.0f;
        private float musicVolume = 0.8f;
        private int selectedMusicTrack = 0;

        // Theo dõi năng lượng âm thanh game (BGM + SFX) để khử vọng vào Mic & tự động giảm nhạc nền (Auto-Ducking) khi đàm thoại
        private float voiceDuckFactor = 0f; // 0: bình thường, 1: đang đàm thoại (giảm nhạc nền để ưu tiên giọng nói)
        private float sfxImpulseEnvelope = 0f;
        private float lastSfxImpulseTime = -10f;
        private readonly float[] musicProbeBuf = new float[128];

        private float lastButtonClickTime = -1f;
        private static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        public float MasterVolume => masterVolume;
        public float SFXVolume => sfxVolume;
        public float MusicVolume => musicVolume;
        public int SelectedMusicTrack => selectedMusicTrack;
        public float SfxImpulseEnvelope => sfxImpulseEnvelope;
        public float LastSfxImpulseTime => lastSfxImpulseTime;
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
            sfxMainSource.spatialBlend = 0f;
            sfxMainSource.priority = 64;

            if (uiSource == null)
            {
                uiSource = gameObject.AddComponent<AudioSource>();
                uiSource.playOnAwake = false;
            }
            uiSource.spatialBlend = 0f;
            uiSource.priority = 32;

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
            }
            musicSource.spatialBlend = 0f;
            musicSource.priority = 0; // Ưu tiên cao nhất tuyệt đối trong FMOD để không bao giờ bị ngắt quãng
            musicSource.bypassEffects = true;
            musicSource.bypassListenerEffects = true;
            musicSource.bypassReverbZones = true;

            if (timerSource == null)
            {
                timerSource = gameObject.AddComponent<AudioSource>();
                timerSource.playOnAwake = false;
                timerSource.loop = false;
            }
            timerSource.spatialBlend = 0f;
            timerSource.priority = 48;

            // Tạo pool AudioSource cho các va chạm bi đồng thời
            sfxPool.Clear();
            for (int i = 0; i < sfxPoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.priority = 96;
                sfxPool.Add(src);
            }
        }

        private void Update()
        {
            if (sfxImpulseEnvelope > 0f)
            {
                sfxImpulseEnvelope = Mathf.MoveTowards(sfxImpulseEnvelope, 0f, Time.unscaledDeltaTime * 4.2f);
            }

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
            if (audioLibrary == null)
            {
                audioLibrary = Resources.Load<AudioLibrary>("DefaultAudioLibrary");
            }

            AudioClip targetClip = clip != null ? clip : (audioLibrary != null ? audioLibrary.GetMusicClip(selectedMusicTrack) : null);
            if (targetClip == null) return;

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.loop = true;
            }
            musicSource.spatialBlend = 0f;
            musicSource.priority = 0;
            musicSource.bypassEffects = true;
            musicSource.bypassListenerEffects = true;
            musicSource.bypassReverbZones = true;

            if (musicSource.clip == targetClip && musicSource.isPlaying)
            {
                return; // Đang phát bài này rồi
            }

            float musicDuck = Mathf.Lerp(1.0f, 0.12f, voiceDuckFactor);
            musicSource.Stop();
            musicSource.clip = targetClip;
            musicSource.time = 0f;
            musicSource.loop = true;
            musicSource.volume = musicVolume * masterVolume * musicDuck;
            musicSource.Play();
        }

        public void SetMusicTrack(int trackIndex)
        {
            selectedMusicTrack = Mathf.Clamp(trackIndex, 0, 1);
            PlayerPrefs.SetInt("Music_Track", selectedMusicTrack);
            PlayerPrefs.Save();
            PlayMusic();
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
            selectedMusicTrack = Mathf.Clamp(PlayerPrefs.GetInt("Music_Track", 0), 0, 1);

            ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = masterVolume;
            float musicDuck = Mathf.Lerp(1.0f, 0.12f, voiceDuckFactor);
            float sfxDuck = Mathf.Lerp(1.0f, 0.55f, voiceDuckFactor);

            if (musicSource != null) musicSource.volume = musicVolume * masterVolume * musicDuck;
            if (sfxMainSource != null) sfxMainSource.volume = masterVolume * sfxDuck;
            if (uiSource != null) uiSource.volume = masterVolume * sfxDuck;
            if (timerSource != null) timerSource.volume = masterVolume * sfxDuck;

            foreach (var src in sfxPool)
            {
                if (src != null) src.volume = masterVolume * sfxDuck;
            }
        }

        /// <summary>
        /// Điều chỉnh mức giảm âm lượng nhạc nền & SFX khi đang đàm thoại (0 = không giảm, 1 = giảm tối đa khi có người nói).
        /// Giúp giọng nói nghe to, rõ ràng và ngăn tiếng nhạc nền dội ngược vào Microphone.
        /// </summary>
        public void SetVoiceDuckingFactor(float duck01)
        {
            float clamped = Mathf.Clamp01(duck01);
            if (Mathf.Abs(voiceDuckFactor - clamped) < 0.01f) return;
            voiceDuckFactor = clamped;
            ApplyVolumes();
        }

        /// <summary>
        /// Đo mức năng lượng âm thanh (BGM + SFX) hiện tại của game đang phát ra loa để bộ lọc Mic khử tiếng game.
        /// </summary>
        public float GetCurrentGameAudioLevel()
        {
            float musicRms = 0f;
            if (musicSource != null && musicSource.isPlaying && musicSource.volume > 0.001f)
            {
                try
                {
                    musicSource.GetOutputData(musicProbeBuf, 0);
                    float sumSq = 0f;
                    for (int i = 0; i < musicProbeBuf.Length; i++)
                    {
                        float s = musicProbeBuf[i];
                        sumSq += s * s;
                    }
                    musicRms = Mathf.Sqrt(sumSq / musicProbeBuf.Length) * musicSource.volume;
                }
                catch
                {
                    musicRms = musicSource.volume * 0.12f;
                }
            }

            return Mathf.Clamp01(musicRms + sfxImpulseEnvelope);
        }

        private void RegisterSfxImpulse(float rawVol)
        {
            lastSfxImpulseTime = Time.unscaledTime;
            float effective = Mathf.Clamp01(rawVol * masterVolume * Mathf.Lerp(1.0f, 0.55f, voiceDuckFactor));
            if (effective > sfxImpulseEnvelope)
            {
                sfxImpulseEnvelope = effective;
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

        private float lastCueHitTime = -10f;

        /// <summary>
        /// Khi mở 2 cửa sổ (Host + Client) trên cùng 1 máy tính để test Online (runInBackground = true),
        /// chỉ cửa sổ đang active (isFocused) mới phát âm thanh hiệu ứng mặt bàn để tránh tiếng vang đúp lệch ping.
        /// Khi chơi trên 2 máy tính độc lập, mỗi người chơi đều focus vào cửa sổ của mình nên nghe đầy đủ 100%.
        /// </summary>
        private static bool ShouldSuppressBackgroundOnlineSfx()
        {
            return Application.isPlaying && !Application.isFocused && Photon.Pun.PhotonNetwork.InRoom;
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
            if (ShouldSuppressBackgroundOnlineSfx()) return;
            if (Time.unscaledTime - lastCueHitTime < 0.22f) return;
            lastCueHitTime = Time.unscaledTime;

            if (isBreak && audioLibrary.BreakShotClip != null)
            {
                var breakSrc = GetNextSfxSource();
                float breakVol = Mathf.Lerp(0.6f, 1.0f, Mathf.Clamp01(power01)) * sfxVolume;
                RegisterSfxImpulse(breakVol);
                breakSrc.pitch = UnityEngine.Random.Range(0.99f, 1.01f);
                breakSrc.PlayOneShot(audioLibrary.BreakShotClip, breakVol);
                return;
            }

            if (audioLibrary.CueHitClip == null) return;

            var src = GetNextSfxSource();
            float p = Mathf.Clamp01(power01);
            float vol = Mathf.Lerp(0.30f, 1.0f, Mathf.Pow(p, 0.8f)) * sfxVolume;
            RegisterSfxImpulse(vol);
            src.pitch = UnityEngine.Random.Range(0.97f, 1.03f) * Mathf.Lerp(0.96f, 1.04f, p);
            src.PlayOneShot(audioLibrary.CueHitClip, vol);
        }

        public void PlayBallHit(float relativeSpeed)
        {
            if (audioLibrary == null || audioLibrary.BallHitClip == null) return;
            if (ShouldSuppressBackgroundOnlineSfx()) return;
            if (relativeSpeed < 0.05f) return;

            float norm = Mathf.Clamp01(relativeSpeed / 6.0f);
            float vol = Mathf.Lerp(0.12f, 1.0f, Mathf.Pow(norm, 0.7f)) * sfxVolume;
            RegisterSfxImpulse(vol);

            var src = GetNextSfxSource();
            src.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
            src.PlayOneShot(audioLibrary.BallHitClip, vol);
        }

        public void PlayCushionHit(float speed)
        {
            if (audioLibrary == null || audioLibrary.CushionHitClip == null) return;
            if (ShouldSuppressBackgroundOnlineSfx()) return;
            if (speed < 0.15f) return;

            float norm = Mathf.Clamp01(speed / 5.0f);
            float vol = Mathf.Lerp(0.10f, 0.85f, Mathf.Pow(norm, 0.8f)) * sfxVolume;
            RegisterSfxImpulse(vol);

            var src = GetNextSfxSource();
            src.pitch = UnityEngine.Random.Range(0.96f, 1.04f) * Mathf.Lerp(1.04f, 0.96f, norm);
            src.PlayOneShot(audioLibrary.CushionHitClip, vol);
        }

        public void PlayPocketDrop()
        {
            if (audioLibrary == null || audioLibrary.PocketDropClip == null) return;
            if (ShouldSuppressBackgroundOnlineSfx()) return;
            RegisterSfxImpulse(sfxVolume);

            var src = GetNextSfxSource();
            src.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
            src.PlayOneShot(audioLibrary.PocketDropClip, sfxVolume);
        }

        public void PlayPocket() => PlayPocketDrop();

        /// <summary>
        /// Phát âm thanh thông báo phạm lỗi bi (Tieng_Loi_Bi.mp3).
        /// </summary>
        public void PlayFoul()
        {
            if (audioLibrary == null || audioLibrary.FoulClip == null) return;

            StopTimerTick();
            RegisterSfxImpulse(sfxVolume);
            if (sfxMainSource != null)
            {
                sfxMainSource.Stop();
                sfxMainSource.pitch = 1.0f;
                sfxMainSource.PlayOneShot(audioLibrary.FoulClip, sfxVolume);
            }
        }

        public void PlayWinFanfare()
        {
            if (audioLibrary == null || audioLibrary.WinFanfareClip == null) return;
            RegisterSfxImpulse(sfxVolume);

            if (sfxMainSource != null)
            {
                sfxMainSource.pitch = 1.0f;
                sfxMainSource.PlayOneShot(audioLibrary.WinFanfareClip, sfxVolume);
            }
        }

        private AudioSource rackCrowdSource;
        private AudioSource rackAmbienceSource;
        private Coroutine rackResultAudioRoutine;

        private void EnsureRackCrowdSources()
        {
            float duck = Mathf.Lerp(1.0f, 0.55f, voiceDuckFactor);
            if (rackCrowdSource == null)
            {
                rackCrowdSource = gameObject.AddComponent<AudioSource>();
                rackCrowdSource.playOnAwake = false;
                rackCrowdSource.loop = false;
                rackCrowdSource.spatialBlend = 0f;
                rackCrowdSource.priority = 52;
            }
            rackCrowdSource.volume = masterVolume * duck;

            if (rackAmbienceSource == null)
            {
                rackAmbienceSource = gameObject.AddComponent<AudioSource>();
                rackAmbienceSource.playOnAwake = false;
                rackAmbienceSource.loop = false;
                rackAmbienceSource.spatialBlend = 0f;
                rackAmbienceSource.priority = 60;
            }
            rackAmbienceSource.volume = masterVolume * duck;
        }

        private void StopRackCrowdSources()
        {
            if (rackResultAudioRoutine != null)
            {
                StopCoroutine(rackResultAudioRoutine);
                rackResultAudioRoutine = null;
            }
            if (rackCrowdSource != null && rackCrowdSource.isPlaying)
            {
                rackCrowdSource.Stop();
            }
            if (rackAmbienceSource != null && rackAmbienceSource.isPlaying)
            {
                rackAmbienceSource.Stop();
            }
        }

        private AudioClip cachedRackWinClip;
        private AudioClip cachedRackLoseClip;

        /// <summary>
        /// Phát âm thanh thông báo thắng mỗi ván (Tieng_Vo_Tay_1.mp3 đã cắt gọn ~2.1s kèm fade-out mượt ở cuối).
        /// </summary>
        public void PlayRackWin()
        {
            if (audioLibrary == null)
            {
                audioLibrary = Resources.Load<AudioLibrary>("DefaultAudioLibrary");
            }

            if (cachedRackWinClip == null)
            {
                cachedRackWinClip = Resources.Load<AudioClip>("Audio/Tieng_Vo_Tay_1");
            }

            AudioClip clip = cachedRackWinClip;
            if (clip == null && audioLibrary != null)
            {
                AudioClip libClip = audioLibrary.RackWinClip;
                if (libClip != null && !libClip.name.Contains("Ngon_Thi"))
                {
                    clip = libClip;
                }
                else
                {
                    clip = audioLibrary.WinFanfareClip;
                }
            }
            if (clip == null) return;

            StopTimerTick();
            StopRackCrowdSources();
            if (sfxMainSource != null) sfxMainSource.Stop();

            RegisterSfxImpulse(Mathf.Clamp01(sfxVolume));
            rackResultAudioRoutine = StartCoroutine(PlayRackClipWithEnvelopeRoutine(clip, 1.0f, 0.45f));
        }

        /// <summary>
        /// Phát âm thanh thông báo thua mỗi ván (Tieng_Thua_Game.mp3 đã cắt gọn ~1.7s kèm fade-out mượt ở cuối).
        /// </summary>
        public void PlayRackLose()
        {
            if (audioLibrary == null)
            {
                audioLibrary = Resources.Load<AudioLibrary>("DefaultAudioLibrary");
            }

            if (cachedRackLoseClip == null)
            {
                cachedRackLoseClip = Resources.Load<AudioClip>("Audio/Tieng_Thua_Game");
            }

            AudioClip clip = cachedRackLoseClip;
            if (clip == null && audioLibrary != null)
            {
                AudioClip libClip = audioLibrary.RackLoseClip;
                if (libClip != null && !libClip.name.Contains("Meo_Khoc"))
                {
                    clip = libClip;
                }
                else
                {
                    clip = audioLibrary.FoulClip;
                }
            }
            if (clip == null) return;

            StopTimerTick();
            StopRackCrowdSources();
            if (sfxMainSource != null) sfxMainSource.Stop();

            RegisterSfxImpulse(Mathf.Clamp01(sfxVolume));
            rackResultAudioRoutine = StartCoroutine(PlayRackClipWithEnvelopeRoutine(clip, 1.0f, 0.38f));
        }

        private System.Collections.IEnumerator PlayRackClipWithEnvelopeRoutine(AudioClip clip, float gainMul, float fadeOutDuration)
        {
            EnsureRackCrowdSources();
            if (rackCrowdSource == null || clip == null)
            {
                rackResultAudioRoutine = null;
                yield break;
            }

            float duck = Mathf.Lerp(1.0f, 0.55f, voiceDuckFactor);
            float baseVol = Mathf.Clamp01(masterVolume * sfxVolume * duck * gainMul);

            rackCrowdSource.Stop();
            rackCrowdSource.clip = clip;
            rackCrowdSource.pitch = 1.0f;
            rackCrowdSource.time = 0f;
            rackCrowdSource.volume = baseVol;
            rackCrowdSource.Play();

            float totalDur = Mathf.Min(clip.length, 2.25f);
            float fadeStart = Mathf.Max(0.1f, totalDur - Mathf.Max(0.1f, fadeOutDuration));
            float elapsed = 0f;

            while (elapsed < totalDur && rackCrowdSource != null && rackCrowdSource.isPlaying)
            {
                elapsed += Time.unscaledDeltaTime;
                duck = Mathf.Lerp(1.0f, 0.55f, voiceDuckFactor);
                baseVol = Mathf.Clamp01(masterVolume * sfxVolume * duck * gainMul);

                float fadeOut = elapsed > fadeStart
                    ? Mathf.SmoothStep(1f, 0f, (elapsed - fadeStart) / (totalDur - fadeStart))
                    : 1f;
                rackCrowdSource.volume = baseVol * fadeOut;
                yield return null;
            }

            if (rackCrowdSource != null) rackCrowdSource.Stop();
            rackResultAudioRoutine = null;
        }

        /// <summary>
        /// Phát âm thanh chiến thắng cuối trận (Tieng_Win.mp3) khi hiện bảng thống kê kết thúc trận đấu.
        /// </summary>
        public void PlayMatchWin()
        {
            if (audioLibrary == null) return;
            AudioClip clip = audioLibrary.MatchWinClip != null ? audioLibrary.MatchWinClip : audioLibrary.WinFanfareClip;
            if (clip == null) return;

            StopTimerTick();
            StopRackCrowdSources();
            RegisterSfxImpulse(sfxVolume);
            if (sfxMainSource != null)
            {
                sfxMainSource.Stop();
                sfxMainSource.pitch = 1.0f;
                sfxMainSource.PlayOneShot(clip, sfxVolume);
            }
        }

        /// <summary>
        /// Phát âm thanh thua cuộc cuối trận (Tieng_Thua.mp3) khi hiện bảng thống kê kết thúc trận đấu.
        /// </summary>
        public void PlayMatchLose()
        {
            if (audioLibrary == null) return;
            AudioClip clip = audioLibrary.MatchLoseClip != null ? audioLibrary.MatchLoseClip : audioLibrary.FoulClip;
            if (clip == null) return;

            StopTimerTick();
            StopRackCrowdSources();
            RegisterSfxImpulse(sfxVolume);
            if (sfxMainSource != null)
            {
                sfxMainSource.Stop();
                sfxMainSource.pitch = 1.0f;
                sfxMainSource.PlayOneShot(clip, sfxVolume);
            }
        }

        /// <summary>
        /// Dừng ngay âm thanh kết quả ván/trận đang phát khi chuyển ván, bấm Đánh tiếp hoặc Về sảnh.
        /// </summary>
        public void StopMatchResultAudio()
        {
            StopTimerTick();
            StopRackCrowdSources();
            if (sfxMainSource != null)
            {
                sfxMainSource.Stop();
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
        private static AudioClip s_edmRoomJoinClip;
        private static AudioClip s_edmRoomLeaveClip;
        private float lastRoomEventSfxTime = -10f;

        /// <summary>
        /// Phát âm thanh "Vào phòng chờ" phong cách EDM Future-Bass (3 nhịp Pluck 1/16 F4 -> A4 -> C5
        /// kết hợp Filter Sweep Uplifter bung ra hợp âm SuperSaw Fmaj7 + Sub-Bass 808 + LFO Wobble).
        /// Khác biệt hoàn toàn với cấu trúc 2 nốt đơn của Discord.
        /// </summary>
        public void PlayRoomJoinSound()
        {
            if (Time.unscaledTime - lastRoomEventSfxTime < 0.08f) return;
            lastRoomEventSfxTime = Time.unscaledTime;

            if (s_edmRoomJoinClip == null) s_edmRoomJoinClip = BuildEdmRoomJoinClip();
            var src = uiSource != null ? uiSource : GetNextSfxSource();
            if (src == null) return;

            float vol = Mathf.Clamp01(sfxVolume * 0.92f);
            RegisterSfxImpulse(vol * 0.5f);
            src.pitch = 1.0f;
            src.PlayOneShot(s_edmRoomJoinClip, vol);
        }

        /// <summary>
        /// Phát âm thanh "Đối thủ rời phòng chờ" phong cách EDM Tape-Stop / Pitch-Glide Downlifter
        /// (hợp âm Synth Pluck lướt trượt tần số xuống + đóng bộ lọc Low-Pass + 808 Sub-Drop).
        /// Chỉ phát ở sảnh phòng chờ Lobby, hoàn toàn khác Discord.
        /// </summary>
        public void PlayRoomLeaveSound()
        {
            if (Time.unscaledTime - lastRoomEventSfxTime < 0.08f) return;
            lastRoomEventSfxTime = Time.unscaledTime;

            if (s_edmRoomLeaveClip == null) s_edmRoomLeaveClip = BuildEdmRoomLeaveClip();
            var src = uiSource != null ? uiSource : GetNextSfxSource();
            if (src == null) return;

            float vol = Mathf.Clamp01(sfxVolume * 0.88f);
            RegisterSfxImpulse(vol * 0.5f);
            src.pitch = 1.0f;
            src.PlayOneShot(s_edmRoomLeaveClip, vol);
        }

        private static float SuperSawWave(float freq, float t)
        {
            // 5-voice detuned EDM SuperSaw (ấm, dày và sáng như synth Future Bass)
            float s0 = Mathf.Repeat(freq * t, 1f) * 2f - 1f;
            float s1 = Mathf.Repeat(freq * 1.0055f * t + 0.17f, 1f) * 2f - 1f;
            float s2 = Mathf.Repeat(freq * 0.9945f * t + 0.43f, 1f) * 2f - 1f;
            float s3 = Mathf.Repeat(freq * 1.0110f * t + 0.68f, 1f) * 2f - 1f;
            float s4 = Mathf.Repeat(freq * 0.9890f * t + 0.89f, 1f) * 2f - 1f;
            return (s0 * 0.32f + (s1 + s2) * 0.22f + (s3 + s4) * 0.12f);
        }

        private static float EdmPluckVoice(float freq, float dt)
        {
            // EDM Pluck: sóng hỗn hợp Sine + Saw qua bộ lọc bao hình (Filter Envelope đóng nhanh tạo tiếng "pluck" nảy)
            float brightness = Mathf.Exp(-dt * 38f);
            float fund = Mathf.Sin(2f * Mathf.PI * freq * dt);
            float saw = SuperSawWave(freq, dt);
            float h2 = Mathf.Sin(2f * Mathf.PI * (freq * 2f) * dt) * brightness;
            float h3 = Mathf.Sin(2f * Mathf.PI * (freq * 3f) * dt) * brightness * brightness;
            return fund * 0.52f + saw * (0.18f + 0.32f * brightness) + h2 * 0.24f + h3 * 0.12f;
        }

        private static AudioClip BuildEdmRoomJoinClip()
        {
            const int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.48f);
            float[] data = new float[samples];

            // Chuỗi 3 nốt EDM Pluck 1/16 nhanh (F4 -> A4 -> Hợp âm Future Bass Fmaj7: C5 + E5 + A5)
            const float fSub = 87.31f;  // F2 (808 Sub-bass)
            const float f1 = 349.23f;   // F4
            const float f2 = 440.00f;   // A4
            const float f3 = 523.25f;   // C5
            const float fChordE = 659.25f; // E5
            const float fChordA = 880.00f; // A5

            const float t1 = 0.000f;
            const float t2 = 0.062f;
            const float t3 = 0.124f;

            float lpState = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float sample = 0f;

                // Nốt Pluck 1 (F4)
                if (t >= t1)
                {
                    float dt = t - t1;
                    float env = Mathf.Exp(-dt * 18f) * Mathf.Clamp01(dt / 0.002f);
                    sample += EdmPluckVoice(f1, dt) * env * 0.50f;
                }

                // Nốt Pluck 2 (A4)
                if (t >= t2)
                {
                    float dt = t - t2;
                    float env = Mathf.Exp(-dt * 18f) * Mathf.Clamp01(dt / 0.002f);
                    sample += EdmPluckVoice(f2, dt) * env * 0.56f;
                }

                // Nốt 3: Bung hợp âm Future-Bass SuperSaw (Fmaj7) + 808 Sub-Bass + LFO Wobble (14Hz)
                if (t >= t3)
                {
                    float dt = t - t3;
                    float env = Mathf.Exp(-dt * 6.5f) * Mathf.Clamp01(dt / 0.004f);
                    float wobble = 0.82f + 0.18f * Mathf.Sin(2f * Mathf.PI * 14f * dt);

                    float chord = EdmPluckVoice(f3, dt) * 0.45f
                                + SuperSawWave(fChordE, dt) * 0.24f
                                + SuperSawWave(f2, dt) * 0.22f
                                + Mathf.Sin(2f * Mathf.PI * fChordA * dt) * 0.15f * Mathf.Exp(-dt * 11f);

                    // 808 Sub-bass punch
                    float subFreq = fSub + 45f * Mathf.Exp(-dt * 35f);
                    float sub808 = Mathf.Sin(2f * Mathf.PI * subFreq * dt) * Mathf.Exp(-dt * 7.5f) * 0.42f;

                    sample += chord * env * wobble * 0.78f + sub808;
                }

                // Bộ lọc Low-Pass Uplifter mở dần từ 0.00s -> 0.15s giúp âm thanh mượt và đậm chất EDM
                float cutoff = Mathf.Lerp(0.22f, 0.72f, Mathf.Clamp01(t / 0.15f));
                lpState += (sample - lpState) * cutoff;

                if (t > 0.44f)
                {
                    lpState *= Mathf.Clamp01((0.48f - t) / 0.04f);
                }

                data[i] = lpState / (1f + Mathf.Abs(lpState));
            }

            var clip = AudioClip.Create("EdmRoomJoinFutureBass", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildEdmRoomLeaveClip()
        {
            const int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.36f);
            float[] data = new float[samples];

            // Kỹ thuật EDM Tape-Stop / Pitch-Glide Downlifter: trượt mượt tần số từ hợp âm E5+A4 xuống kèm đóng bộ lọc Low-Pass
            float phaseLead = 0f;
            float phaseFifth = 0f;
            float phaseSub = 0f;
            float lpState = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float u = Mathf.Clamp01(t / 0.32f);

                // Đường cong trượt tần số Tape-Stop (giữ nhẹ 35ms đầu rồi lướt mượt xuống 0.52x)
                float glide = t < 0.035f ? 1.0f : Mathf.Lerp(1.0f, 0.52f, Mathf.Pow((t - 0.035f) / 0.285f, 1.35f));

                float fLead = 587.33f * glide;  // D5 trượt xuống
                float fFifth = 392.00f * glide; // G4 trượt xuống
                float fSub = Mathf.Lerp(130.81f, 48.0f, u); // 808 Sub-drop

                phaseLead += fLead / sampleRate;
                phaseLead -= Mathf.Floor(phaseLead);

                phaseFifth += fFifth / sampleRate;
                phaseFifth -= Mathf.Floor(phaseFifth);

                phaseSub += 2f * Mathf.PI * fSub / sampleRate;

                float env = Mathf.Exp(-t * 7.8f) * Mathf.Clamp01(t / 0.003f);
                float sawLead = (phaseLead * 2f - 1f) * 0.30f + Mathf.Sin(phaseLead * 2f * Mathf.PI) * 0.45f;
                float sawFifth = (phaseFifth * 2f - 1f) * 0.22f + Mathf.Sin(phaseFifth * 2f * Mathf.PI) * 0.35f;
                float subDrop = Mathf.Sin(phaseSub) * Mathf.Exp(-t * 9.0f) * 0.48f;

                float raw = (sawLead * 0.52f + sawFifth * 0.42f) * env + subDrop;

                // Đóng dần bộ lọc Low-Pass (Filter Down-Sweep) từ sáng -> trầm ấm
                float cutoff = Mathf.Lerp(0.55f, 0.08f, u);
                lpState += (raw - lpState) * cutoff;

                if (t > 0.32f)
                {
                    lpState *= Mathf.Clamp01((0.36f - t) / 0.04f);
                }

                data[i] = lpState / (1f + Mathf.Abs(lpState));
            }

            var clip = AudioClip.Create("EdmRoomLeaveTapeStop", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void PlayTimerTick()
        {
            AudioClip customTick = audioLibrary != null ? audioLibrary.TimerTickClip : null;
            if (customTick != null)
            {
                if (timerSource == null)
                {
                    timerSource = gameObject.AddComponent<AudioSource>();
                    timerSource.playOnAwake = false;
                }
                RegisterSfxImpulse(sfxVolume * 0.6f);
                timerSource.Stop();
                timerSource.pitch = 1.0f;
                timerSource.volume = masterVolume * Mathf.Lerp(1.0f, 0.55f, voiceDuckFactor);
                timerSource.PlayOneShot(customTick, sfxVolume);
                return;
            }

            if (tickClip == null) tickClip = BuildTickClip();

            tickToggle = !tickToggle;
            var src = timerSource != null ? timerSource : (uiSource != null ? uiSource : GetNextSfxSource());
            if (src == null) return;
            src.pitch = tickToggle ? 1.0f : 0.78f;
            src.PlayOneShot(tickClip, 0.8f * sfxVolume);
        }

        public void StopTimerTick()
        {
            if (timerSource != null && timerSource.isPlaying)
            {
                timerSource.Stop();
            }
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
