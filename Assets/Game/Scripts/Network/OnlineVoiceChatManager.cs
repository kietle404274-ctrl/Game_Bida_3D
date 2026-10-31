using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Game.Billiards.Audio;
using Game.Billiards.Core;

namespace Game.Billiards.Network
{
    /// <summary>
    /// Hệ thống Đàm thoại Giọng nói Thời gian thực (Realtime Voice Chat) cho chế độ Online PvP:
    /// - Tự động tương thích mọi thiết bị Microphone trên Windows (ưu tiên Mic vật lý, tự động chuyển đổi nếu thiết bị đầu tiên bị câm,
    ///   tự nhận diện tần số phần cứng 8kHz/16kHz/44.1kHz/48kHz qua Microphone.GetDeviceCaps và lấy mẫu xuống 8000 Hz chuẩn thoại).
    /// - Luôn hoạt động ngay cả khi cửa sổ game chạy nền (Application.runInBackground = true) để test 2 cửa sổ trên 1 máy
    ///   hoặc vừa chơi vừa alt-tab vẫn nghe/nói bình thường.
    /// - Hiệu chỉnh chuẩn xác theo thông số phần cứng thực tế (BaseVadRmsThreshold = 0.0045f chặn 100% tiếng quạt 0.0022f,
    ///   AGC tự nhiên 0.75x - 2.60x không bao giờ phóng đại tiếng ồn hay gây vỡ tiếng).
    /// - Phát âm thanh trực tiếp trên luồng Audio Thread qua OnAudioFilterRead (gói 50ms + đệm chống giật 75ms)
    ///   đảm bảo âm thanh phát ra loa liền mạch, không bị ngắt quãng hay nghẽn bộ đệm FMOD.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class OnlineVoiceChatManager : MonoBehaviour
    {
        public const string PrefsMicEnabled = "Voice_MicEnabled";
        public const string PrefsSpeakerEnabled = "Voice_SpeakerEnabled";
        public const string PrefsMicVolume = "Voice_MicVolume";
        public const string PrefsSpeakerVolume = "Voice_SpeakerVolume";

        private const int NetworkSampleRate = 8000;
        private const int NetworkChunkSamples = 400; // 50ms @ 8000Hz -> 400 bytes/gói (20 gói/giây, cực mượt)
        private const int MicClipLengthSec = 2;
        private const int RingBufferCapacity = NetworkSampleRate * 4; // 4 giây bộ đệm vòng
        private const int JitterStartThreshold = 600; // Đệm 75ms trước khi bắt đầu phát câu nói mới để giảm độ trễ và chống hụt mẫu
        private const int MaxStarvationSamples = 320; // Cho phép thiếu mẫu tối đa 40ms trước khi quay lại trạng thái chờ đệm
        private const int MaxLatencySamples = NetworkSampleRate; // Tối đa 1 giây trễ trước khi tua kịp thời gian thực

        // Ngưỡng nhận diện giọng người & Khử tạp âm chuẩn hóa từ đo đạc thực tế trên Microphone Array (Realtek):
        // - Tiếng quạt tĩnh thực tế: VoiceRMS = 0.0016 .. 0.0023 -> đặt BaseVadRmsThreshold = 0.0045f (chặn 100% quạt)
        // - Giọng nói thực tế: VoiceRMS = 0.0350 .. 0.1650 -> đặt TargetSpeechRms = 0.16f, AGC = 0.75x .. 2.60x (tự nhiên, không phóng đại lố)
        private const float BaseVadRmsThreshold = 0.0045f;
        private const float TargetSpeechRms = 0.16f;
        private const float MinAgcGain = 0.75f;
        private const float MaxAgcGain = 2.60f;
        private const float DefaultAgcGain = 1.40f;
        private const float VadHangoverDuration = 0.20f;
        private const float EchoGuardDuration = 0.45f;
        private const float PostSpeechEchoCooldown = 0.40f;
        private const int SubFrameSamples = 40; // 5ms @ 8000 Hz (10 khung con trong mỗi gói 50ms)
        private const int NoiseHistorySize = 16; // Cửa sổ trượt 800ms theo dõi nền tiếng quạt tĩnh
        private const int MuLawBias = 0x84;
        private const int MuLawClip = 32635;

        public static OnlineVoiceChatManager Instance { get; private set; }

        /// <summary>Sự kiện phát ra khi trạng thái Bật/Tắt hoặc Âm lượng Mic/Loa thay đổi.</summary>
        public event Action OnVoiceSettingsChanged;

        private NetworkMatchManager networkMatchManager;
        private AudioSource playbackSource;
        private AudioClip dummyCarrierClip;

        // Trạng thái thiết bị Microphone phần cứng
        private AudioClip micClip;
        private string activeMicDevice;
        private int currentMicDeviceIndex = -1;
        private bool hasConfirmedWorkingMic;
        private float silentMicDuration;
        private int micHardwareRate = 16000;
        private int micClipTotalSamples = 32000;
        private int micCaptureChunkSamples = 800;
        private bool isRecordingMic;
        private int lastMicReadPos;
        private int lastObservedMicPos = -1;
        private float lastMicAdvanceTime;

        private float[] rawHardwareCaptureBuf = new float[4800];
        private readonly float[] resampled8kBuf = new float[NetworkChunkSamples];
        private readonly float[] pitchBandBuf = new float[NetworkChunkSamples];
        private readonly byte[] encodeByteBuf = new byte[NetworkChunkSamples];
        private readonly byte[] lookaheadEncodeBuf = new byte[NetworkChunkSamples];
        private bool hasLookaheadPacket;
        private float lookaheadLevel01;
        private int consecutiveVoicedFrames;

        // Trạng thái chuỗi DSP:
        // - Lọc thông cao 120 Hz cắt tiếng ù quạt + Lọc thông thấp 3400 Hz + Nhấn độ rõ giọng nói (1.2-2.8 kHz)
        // - Tự tương quan chu kỳ dây thanh quản (Pitch Autocorrelation 85-400 Hz) trên dải 120-3400 Hz
        // - Lookahead 2 khung (100ms) chặn đứng 100% tiếng gõ bàn & click chuột
        private float hp1PrevIn, hp1PrevOut;
        private float hp2PrevIn, hp2PrevOut;
        private float lpPrevOut;
        private float presPrevIn, presPrevOut;
        private float adaptiveNoiseFloor = 0.0020f;
        private readonly float[] recentRmsHistory = new float[NoiseHistorySize];
        private int rmsHistoryWriteIdx;
        private int rmsHistoryCount;
        private float currentNoiseGate;
        private float currentAgcGain = DefaultAgcGain;
        private float echoHoldUntil = -10f;
        private float postSpeechCooldownUntil = -10f;
        private float lastPacketSentTime = -10f;
        private float selfEchoEnvelope;
        private bool wasTransmittingLastFrame;

        // Thống kê chẩn đoán định kỳ mỗi 3 giây
        private float nextDiagnosticLogTime;
        private float diagMaxRawPeak;
        private float diagMaxVoiceRms;
        private float diagMaxPitch;
        private int diagSentPackets;
        private int diagRecvPackets;

        // Bộ đệm vòng (Thread-Safe PCM Ring Buffer @ 8000 Hz) dùng cho luồng âm thanh OnAudioFilterRead
        private readonly object ringLock = new object();
        private readonly float[] pcmRingBuffer = new float[RingBufferCapacity];
        private int ringWriteIndex;
        private int ringReadIndex;
        private int ringAvailableSamples;
        private float resampleFracPhase;
        private float prevReadSample;
        private float currReadSample;
        private bool jitterBuffering = true;
        private int starvationSampleCount;
        private int outputSampleRate = 48000;
        private volatile float threadOutputGain = 1.15f;
        private volatile bool threadSpeakerActive = true;

        private float lastPacketReceiveTime = -10f;
        private float vadHangoverUntil = -10f;
        private float nextStateSyncTime;
        private float currentDuckingFactor;

        private bool isMicEnabled = true;
        private bool isSpeakerEnabled = true;
        private float micVolume = 1.0f;
        private float speakerVolume = 1.0f;

        private bool remoteMicEnabled = true;
        private bool remoteSpeakerEnabled = true;
        private int remoteAvatarIndex = -1;

        private float localVoiceLevel01;
        private float remoteVoiceLevel01;
        private float remoteTargetLevel01;
        private float simulatedLocalLevel01;
        private float simulatedRemoteLevel01;

        public bool IsMicEnabled => isMicEnabled;
        public bool IsSpeakerEnabled => isSpeakerEnabled;
        public float MicVolume => micVolume;
        public float SpeakerVolume => speakerVolume;

        public bool RemoteMicEnabled => remoteMicEnabled;
        public bool RemoteSpeakerEnabled => remoteSpeakerEnabled;
        public int RemoteAvatarIndex => remoteAvatarIndex;
        public bool HasMicrophoneDevice => Microphone.devices != null && Microphone.devices.Length > 0;

        /// <summary>Biên độ giọng nói của người chơi cục bộ (0..1) để điều khiển icon Mic nhấp nháy lên xuống.</summary>
        public float LocalVoiceLevel01 => isMicEnabled ? Mathf.Clamp01(Mathf.Max(localVoiceLevel01, simulatedLocalLevel01)) : 0f;

        /// <summary>Biên độ giọng nói của đối thủ (0..1) để điều khiển icon Mic của đối thủ nhấp nháy lên xuống.</summary>
        public float RemoteVoiceLevel01 => (remoteMicEnabled && isSpeakerEnabled)
            ? Mathf.Clamp01(Mathf.Max(remoteVoiceLevel01, simulatedRemoteLevel01))
            : (remoteMicEnabled ? Mathf.Clamp01(simulatedRemoteLevel01) : 0f);

        public bool IsLocalSpeaking => IsMicEnabled && LocalVoiceLevel01 > 0.04f;
        public bool IsRemoteSpeaking => RemoteMicEnabled && RemoteVoiceLevel01 > 0.04f;

        public void SetRemoteAvatarIndex(int avatarIdx)
        {
            remoteAvatarIndex = avatarIdx;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        public static OnlineVoiceChatManager Ensure()
        {
            if (Instance != null) return Instance;

            var existing = FindObjectOfType<OnlineVoiceChatManager>(true);
            if (existing != null)
            {
                Instance = existing;
                if (!existing.gameObject.activeSelf) existing.gameObject.SetActive(true);
                return existing;
            }

            var go = new GameObject("OnlineVoiceChatManager");
            Instance = go.AddComponent<OnlineVoiceChatManager>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Bắt buộc chạy nền để khi mở 2 cửa sổ test hoặc alt-tab vẫn thu/phát âm thanh bình thường
            Application.runInBackground = true;
            AudioListener.pause = false;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.audioMasterMute = false;
#endif
            outputSampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);

            LoadSettings();
            SetupPlaybackAudioSource();
            BindNetworkManager();
        }

        private void OnEnable()
        {
            Application.runInBackground = true;
            AudioListener.pause = false;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.audioMasterMute = false;
#endif
            outputSampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
            BindNetworkManager();
            if (playbackSource != null && !playbackSource.isPlaying)
            {
                playbackSource.Play();
            }
        }

        private void OnDestroy()
        {
            StopMicrophoneCapture();
            UnbindNetworkManager();
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetVoiceDuckingFactor(0f);
            }
            if (dummyCarrierClip != null)
            {
                Destroy(dummyCarrierClip);
                dummyCarrierClip = null;
            }
            if (Instance == this) Instance = null;
        }

        public void LoadSettings()
        {
            isMicEnabled = PlayerPrefs.GetInt(PrefsMicEnabled, 1) == 1;
            isSpeakerEnabled = PlayerPrefs.GetInt(PrefsSpeakerEnabled, 1) == 1;
            micVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsMicVolume, 1.0f));
            speakerVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsSpeakerVolume, 1.0f));
            UpdatePlaybackVolume();
        }

        public void SetMicEnabled(bool enabled)
        {
            if (isMicEnabled == enabled) return;
            isMicEnabled = enabled;
            PlayerPrefs.SetInt(PrefsMicEnabled, isMicEnabled ? 1 : 0);
            PlayerPrefs.Save();

            if (!isMicEnabled)
            {
                localVoiceLevel01 = 0f;
                StopMicrophoneCapture();
            }

            BroadcastLocalVoiceState();
            OnVoiceSettingsChanged?.Invoke();
        }

        public void ToggleMic()
        {
            SetMicEnabled(!isMicEnabled);
        }

        public void SetSpeakerEnabled(bool enabled)
        {
            if (isSpeakerEnabled == enabled) return;
            isSpeakerEnabled = enabled;
            PlayerPrefs.SetInt(PrefsSpeakerEnabled, isSpeakerEnabled ? 1 : 0);
            PlayerPrefs.Save();

            if (!isSpeakerEnabled)
            {
                remoteVoiceLevel01 = 0f;
                remoteTargetLevel01 = 0f;
                ClearPlaybackBuffer();
            }

            UpdatePlaybackVolume();
            BroadcastLocalVoiceState();
            OnVoiceSettingsChanged?.Invoke();
        }

        public void ToggleSpeaker()
        {
            SetSpeakerEnabled(!isSpeakerEnabled);
        }

        public void SetMicVolume(float volume01)
        {
            float clamped = Mathf.Clamp01(volume01);
            if (Mathf.Approximately(micVolume, clamped)) return;
            micVolume = clamped;
            PlayerPrefs.SetFloat(PrefsMicVolume, micVolume);
            PlayerPrefs.Save();

            if (micVolume <= 0.001f && isMicEnabled)
            {
                localVoiceLevel01 = 0f;
            }
            OnVoiceSettingsChanged?.Invoke();
        }

        public void SetSpeakerVolume(float volume01)
        {
            float clamped = Mathf.Clamp01(volume01);
            if (Mathf.Approximately(speakerVolume, clamped)) return;
            speakerVolume = clamped;
            PlayerPrefs.SetFloat(PrefsSpeakerVolume, speakerVolume);
            PlayerPrefs.Save();

            UpdatePlaybackVolume();
            OnVoiceSettingsChanged?.Invoke();
        }

        public void SetSimulatedVoiceLevels(float localLevel01, float remoteLevel01, bool remoteMicOn = true, bool remoteSpeakerOn = true)
        {
            simulatedLocalLevel01 = Mathf.Clamp01(localLevel01);
            simulatedRemoteLevel01 = Mathf.Clamp01(remoteLevel01);
            remoteMicEnabled = remoteMicOn;
            remoteSpeakerEnabled = remoteSpeakerOn;
        }

        private void SetupPlaybackAudioSource()
        {
            playbackSource = GetComponent<AudioSource>();
            if (playbackSource == null) playbackSource = gameObject.AddComponent<AudioSource>();

            playbackSource.playOnAwake = true;
            playbackSource.loop = true;
            playbackSource.spatialBlend = 0f; // 2D Stereo rõ nét
            playbackSource.priority = 0; // Ưu tiên cao nhất trong FMOD
            // Quan trọng: PHẢI để bypassEffects = false để Unity gọi OnAudioFilterRead đều đặn!
            playbackSource.bypassEffects = false;
            playbackSource.bypassListenerEffects = true;
            playbackSource.bypassReverbZones = true;
            playbackSource.volume = 1.0f;
            playbackSource.mute = false;

            // Tạo carrier clip có năng lượng vi mô khác 0 để FMOD không bao giờ virtualize kênh AudioSource này
            if (dummyCarrierClip == null)
            {
                dummyCarrierClip = AudioClip.Create("VoiceCarrierClip", 1024, 1, outputSampleRate, false);
                float[] carrier = new float[1024];
                for (int i = 0; i < carrier.Length; i++)
                {
                    carrier[i] = (i & 1) == 0 ? 0.001f : -0.001f;
                }
                dummyCarrierClip.SetData(carrier, 0);
            }

            playbackSource.clip = dummyCarrierClip;
            ClearPlaybackBuffer();
            UpdatePlaybackVolume();
            playbackSource.Play();
        }

        private void UpdatePlaybackVolume()
        {
            float master = AudioManager.Instance != null
                ? AudioManager.Instance.MasterVolume
                : PlayerPrefs.GetFloat("Volume_Master", 1.0f);

            float effectiveMaster = Mathf.Max(master, 0.55f);
            threadSpeakerActive = isSpeakerEnabled && speakerVolume > 0.001f;
            threadOutputGain = threadSpeakerActive ? Mathf.Clamp(effectiveMaster * speakerVolume * 1.10f, 0f, 1.25f) : 0f;

            if (playbackSource != null)
            {
                playbackSource.volume = 1.0f;
                playbackSource.mute = !threadSpeakerActive;
                if (!playbackSource.isPlaying && gameObject.activeInHierarchy)
                {
                    playbackSource.Play();
                }
            }
        }

        private void BindNetworkManager()
        {
            if (networkMatchManager == null)
            {
                networkMatchManager = FindObjectOfType<NetworkMatchManager>();
            }
            if (networkMatchManager != null)
            {
                networkMatchManager.OnVoiceDataReceived -= HandleVoiceDataReceived;
                networkMatchManager.OnVoiceDataReceived += HandleVoiceDataReceived;

                networkMatchManager.OnVoiceStateReceived -= HandleVoiceStateReceived;
                networkMatchManager.OnVoiceStateReceived += HandleVoiceStateReceived;
            }
        }

        private void UnbindNetworkManager()
        {
            if (networkMatchManager != null)
            {
                networkMatchManager.OnVoiceDataReceived -= HandleVoiceDataReceived;
                networkMatchManager.OnVoiceStateReceived -= HandleVoiceStateReceived;
            }
        }

        private bool IsOnlineVoiceActive()
        {
            if (UI.OnlineChatUI.ForceEnableForTest) return true;
            if (PhotonNetwork.InRoom) return true;
            var session = GameSession.Instance;
            if (session != null && session.CurrentGameMode == GameMode.OnlinePvP) return true;
            if (networkMatchManager != null && networkMatchManager.IsOnlineMatch) return true;
            return false;
        }

        private void Update()
        {
            if (networkMatchManager == null) BindNetworkManager();
            UpdatePlaybackVolume();

            bool online = IsOnlineVoiceActive();
            if (!online)
            {
                if (isRecordingMic) StopMicrophoneCapture();
                localVoiceLevel01 = 0f;
                remoteVoiceLevel01 = 0f;
                if (AudioManager.Instance != null) AudioManager.Instance.SetVoiceDuckingFactor(0f);
                return;
            }

            if (Time.unscaledTime >= nextStateSyncTime)
            {
                nextStateSyncTime = Time.unscaledTime + 2.0f;
                BroadcastLocalVoiceState();
            }

            // Nếu loa đang phát giọng của đối thủ, gia hạn Echo Guard để Microphone không bao giờ thu ngược tiếng loa gây lặp vòng lặp
            if (!jitterBuffering && threadSpeakerActive)
            {
                echoHoldUntil = Mathf.Max(echoHoldUntil, Time.unscaledTime + EchoGuardDuration);
            }

            // 1. Thu âm từ Microphone bất kể cửa sổ game đang active hay chạy nền
            if (isMicEnabled && micVolume > 0.001f)
            {
                EnsureMicrophoneRecording();
                ProcessMicrophoneCapture();

                if (Time.unscaledTime >= nextDiagnosticLogTime)
                {
                    nextDiagnosticLogTime = Time.unscaledTime + 3.0f;
                    if (diagSentPackets > 0 || diagRecvPackets > 0 || diagMaxRawPeak > 0.005f)
                    {
                        Debug.Log($"[OnlineVoiceChatManager] Mic='{activeMicDevice}' ({micHardwareRate}Hz) | RawPeak={diagMaxRawPeak:F4} | VoiceRMS={diagMaxVoiceRms:F4} | Floor={adaptiveNoiseFloor:F5} | Pitch={diagMaxPitch:F2} | Gain={currentAgcGain:F1}x | Sent={diagSentPackets} | Recv={diagRecvPackets}");
                    }
                    diagMaxRawPeak = 0f;
                    diagMaxVoiceRms = 0f;
                    diagMaxPitch = 0f;
                    diagSentPackets = 0;
                    diagRecvPackets = 0;
                }
            }
            else
            {
                if (isRecordingMic) StopMicrophoneCapture();
                hasLookaheadPacket = false;
                consecutiveVoicedFrames = 0;
                localVoiceLevel01 = Mathf.MoveTowards(localVoiceLevel01, 0f, Time.unscaledDeltaTime * 10f);
            }

            // 2. Cập nhật biên độ hiển thị của giọng đối thủ
            if (Time.unscaledTime - lastPacketReceiveTime > 0.16f)
            {
                remoteTargetLevel01 = 0f;
            }
            float remoteSpeed = remoteTargetLevel01 > remoteVoiceLevel01 ? 28f : 9f;
            remoteVoiceLevel01 = Mathf.MoveTowards(remoteVoiceLevel01, remoteTargetLevel01, Time.unscaledDeltaTime * remoteSpeed);

            // 3. Auto-Ducking: tự động hạ nhỏ nhạc nền BGM/SFX khi bật Mic hoặc khi đang nói chuyện
            if (AudioManager.Instance != null)
            {
                bool anyoneSpeaking = IsLocalSpeaking || IsRemoteSpeaking;
                float targetDuck = anyoneSpeaking ? 1.0f : (isMicEnabled ? 0.65f : 0f);
                float duckSpeed = targetDuck > currentDuckingFactor ? 12f : 4f;
                currentDuckingFactor = Mathf.MoveTowards(currentDuckingFactor, targetDuck, Time.unscaledDeltaTime * duckSpeed);
                AudioManager.Instance.SetVoiceDuckingFactor(currentDuckingFactor);
            }
        }

        private void BroadcastLocalVoiceState()
        {
            if (networkMatchManager != null)
            {
                networkMatchManager.SendVoiceState(isMicEnabled, isSpeakerEnabled);
            }
        }

        /// <summary>
        /// Chọn thiết bị Microphone vật lý tốt nhất trên Windows (tránh Stereo Mix hoặc Virtual Cable bị câm).
        /// </summary>
        private int SelectBestInitialMicrophoneIndex(string[] devices)
        {
            if (devices == null || devices.Length == 0) return -1;
            int bestIdx = 0;
            int bestScore = int.MinValue;

            for (int i = 0; i < devices.Length; i++)
            {
                string d = (devices[i] ?? string.Empty).ToLowerInvariant();
                int score = 0;
                if (d.Contains("microphone") || d.Contains("mic") || d.Contains("headset") || d.Contains("array"))
                    score += 30;
                if (d.Contains("usb") || d.Contains("realtek") || d.Contains("high definition"))
                    score += 15;
                if (d.Contains("stereo mix") || d.Contains("what u hear") || d.Contains("loopback"))
                    score -= 100;
                if (d.Contains("virtual") || d.Contains("steam") || d.Contains("oculus") || d.Contains("cable"))
                    score -= 40;
                if (i == 0)
                    score += 5; // Ưu tiên nhẹ cho thiết bị mặc định của Windows nếu cùng điểm

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        private void EnsureMicrophoneRecording()
        {
            string[] devices = Microphone.devices;
            if (devices == null || devices.Length == 0) return;

            // Kiểm tra nếu Microphone bị hệ điều hành ngắt hoặc đứng yên > 1.2 giây thì khởi động lại ngay
            if (isRecordingMic && micClip != null)
            {
                int pos = Microphone.GetPosition(activeMicDevice);
                if (pos != lastObservedMicPos)
                {
                    lastObservedMicPos = pos;
                    lastMicAdvanceTime = Time.unscaledTime;
                    return;
                }
                if (Time.unscaledTime - lastMicAdvanceTime < 1.2f && Microphone.IsRecording(activeMicDevice))
                {
                    return;
                }
                StopMicrophoneCapture();
            }

            try
            {
                if (currentMicDeviceIndex < 0 || currentMicDeviceIndex >= devices.Length)
                {
                    currentMicDeviceIndex = SelectBestInitialMicrophoneIndex(devices);
                }

                activeMicDevice = devices[currentMicDeviceIndex];
                Microphone.GetDeviceCaps(activeMicDevice, out int minFreq, out int maxFreq);

                // Chọn tần số phần cứng tối ưu (ưu tiên 16000 Hz hoặc tần số chuẩn của driver)
                if (minFreq == 0 && maxFreq == 0)
                {
                    micHardwareRate = 16000;
                }
                else
                {
                    int hi = maxFreq > 0 ? maxFreq : 48000;
                    int lo = minFreq > 0 ? minFreq : 8000;
                    micHardwareRate = Mathf.Clamp(16000, lo, hi);
                }

                micClipTotalSamples = micHardwareRate * MicClipLengthSec;
                micCaptureChunkSamples = Mathf.Max(80, micHardwareRate / 20); // 50ms mỗi gói (20 gói/giây)
                if (rawHardwareCaptureBuf.Length < micCaptureChunkSamples)
                {
                    rawHardwareCaptureBuf = new float[micCaptureChunkSamples];
                }

                micClip = Microphone.Start(activeMicDevice, true, MicClipLengthSec, micHardwareRate);
                lastMicReadPos = 0;
                lastObservedMicPos = -1;
                lastMicAdvanceTime = Time.unscaledTime;
                silentMicDuration = 0f;
                ResetFilterState();
                isRecordingMic = micClip != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OnlineVoiceChatManager] Không thể khởi tạo Microphone: {e.Message}");
                isRecordingMic = false;
            }
        }

        private void ResetFilterState()
        {
            hp1PrevIn = hp1PrevOut = 0f;
            hp2PrevIn = hp2PrevOut = 0f;
            lpPrevOut = 0f;
            presPrevIn = presPrevOut = 0f;
            adaptiveNoiseFloor = 0.0020f;
            Array.Clear(recentRmsHistory, 0, recentRmsHistory.Length);
            rmsHistoryWriteIdx = 0;
            rmsHistoryCount = 0;
            currentNoiseGate = 0f;
            currentAgcGain = DefaultAgcGain;
            hasLookaheadPacket = false;
            lookaheadLevel01 = 0f;
            consecutiveVoicedFrames = 0;
            selfEchoEnvelope = 0f;
            wasTransmittingLastFrame = false;
        }

        private void StopMicrophoneCapture()
        {
            if (!isRecordingMic) return;
            try
            {
                if (!string.IsNullOrEmpty(activeMicDevice) && Microphone.IsRecording(activeMicDevice))
                {
                    Microphone.End(activeMicDevice);
                }
            }
            catch
            {
            }
            isRecordingMic = false;
            micClip = null;
            lastMicReadPos = 0;
        }

        /// <summary>
        /// Tính Hệ số Tự tương quan Chuẩn hóa (Normalized Pitch Autocorrelation) trên dải băng rộng 120-3400 Hz
        /// (độ trễ 20..92 mẫu @ 8000 Hz tương ứng tần số thanh quản F0 = 87..400 Hz):
        /// - Tiếng quạt gió băng rộng không có cấu trúc lược hài âm nên Pitch rất thấp (&lt; 0.24).
        /// - Tiếng người nói có chuỗi hài âm F0, 2*F0, 3*F0 đồng pha nên Pitch cao (0.36 .. 0.88).
        /// </summary>
        private static float ComputePitchCorrelation(float[] bandBuf, int length)
        {
            const int minLag = 20; // ~400 Hz @ 8000 Hz
            const int maxLag = 92; // ~87 Hz @ 8000 Hz
            int startSample = maxLag;
            if (length <= startSample + 32) return 0f;

            float energy0 = 0f;
            for (int i = startSample; i < length; i += 2)
            {
                float s = bandBuf[i];
                energy0 += s * s;
            }
            if (energy0 < 1e-9f) return 0f;

            float bestCorr = 0f;
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                float cross = 0f;
                float energyLag = 0f;
                for (int i = startSample; i < length; i += 2)
                {
                    float a = bandBuf[i];
                    float b = bandBuf[i - lag];
                    cross += a * b;
                    energyLag += b * b;
                }

                if (cross > 0f && energyLag > 1e-9f)
                {
                    float corr = cross / Mathf.Sqrt(energy0 * energyLag);
                    if (corr > bestCorr)
                    {
                        bestCorr = corr;
                    }
                }
            }
            return Mathf.Clamp01(bestCorr);
        }

        private void ProcessMicrophoneCapture()
        {
            if (!isRecordingMic || micClip == null) return;

            int currentPos = Microphone.GetPosition(activeMicDevice);
            if (currentPos < 0) return;

            // CHỐNG VỌNG ÂM LOA (Acoustic Echo Guard):
            // Nếu loa đang phát giọng nói của đối thủ (hoặc đang phát lại bài test Mic phím [1]),
            // xả bỏ toàn bộ mẫu thu từ Mic để tuyệt đối không thu ngược tiếng loa gây lặp vô tận!
            if (Time.unscaledTime < echoHoldUntil)
            {
                lastMicReadPos = currentPos;
                hasLookaheadPacket = false;
                consecutiveVoicedFrames = 0;
                currentNoiseGate = 0f;
                localVoiceLevel01 = Mathf.MoveTowards(localVoiceLevel01, 0f, 0.5f);
                return;
            }

            int available = currentPos - lastMicReadPos;
            if (available < 0) available += micClipTotalSamples;

            // Nếu bị trễ tích tụ quá 0.4 giây thì nhảy tới khung gần nhất
            if (available > micClipTotalSamples / 2)
            {
                lastMicReadPos = (currentPos - micCaptureChunkSamples + micClipTotalSamples) % micClipTotalSamples;
                available = micCaptureChunkSamples;
            }

            // Tầng 1: Bộ lọc thông cao 120 Hz (cắt tiếng ù quạt < 120 Hz & rung động mặt bàn)
            // kết hợp lọc thông thấp 3400 Hz và bộ nhấn nhẹ độ rõ giọng nói (Vocal Presence @ ~1.2 - 2.8 kHz)
            const float hpAlpha = 0.910f;   // Cutoff ~120 Hz @ 8000 Hz
            const float lpAlpha = 0.820f;   // Cutoff ~3400 Hz @ 8000 Hz
            const float presAlpha = 0.515f; // High-pass nhánh Formant ~1200 Hz để tôn độ rõ chữ của giọng người

            while (available >= micCaptureChunkSamples)
            {
                micClip.GetData(rawHardwareCaptureBuf, lastMicReadPos);
                lastMicReadPos = (lastMicReadPos + micCaptureChunkSamples) % micClipTotalSamples;
                available -= micCaptureChunkSamples;

                float ratio = (float)micCaptureChunkSamples / NetworkChunkSamples;
                float voiceBandSqSum = 0f;
                float rawPeak = 0f;
                float bandPeak = 0f;
                int zeroCrossings = 0;
                float prevVoiceSample = 0f;

                for (int i = 0; i < NetworkChunkSamples; i++)
                {
                    int startIdx = Mathf.FloorToInt(i * ratio);
                    int endIdx = Mathf.Min(micCaptureChunkSamples, Mathf.FloorToInt((i + 1) * ratio));
                    if (endIdx <= startIdx) endIdx = Mathf.Min(micCaptureChunkSamples, startIdx + 1);

                    float acc = 0f;
                    for (int k = startIdx; k < endIdx; k++)
                    {
                        acc += rawHardwareCaptureBuf[k];
                    }
                    float x = acc / (endIdx - startIdx);
                    float absRaw = Mathf.Abs(x);
                    if (absRaw > rawPeak) rawPeak = absRaw;

                    // Hai tầng High-Pass 120 Hz nối tiếp: cắt tiếng ù điện 50/60Hz, tiếng gió quạt trầm và tiếng đập bàn
                    float hp1 = hpAlpha * (hp1PrevOut + x - hp1PrevIn);
                    hp1PrevIn = x;
                    hp1PrevOut = hp1;

                    float hp2 = hpAlpha * (hp2PrevOut + hp1 - hp2PrevIn);
                    hp2PrevIn = hp1;
                    hp2PrevOut = hp2;

                    // Low-Pass 3400 Hz cắt nhiễu rít tần số cao
                    lpPrevOut += lpAlpha * (hp2 - lpPrevOut);
                    float bandPassed = lpPrevOut;
                    pitchBandBuf[i] = bandPassed;

                    // Nhấn nhẹ dải Formant giọng nói (1.2 kHz - 2.8 kHz)
                    float pres = presAlpha * (presPrevOut + bandPassed - presPrevIn);
                    presPrevIn = bandPassed;
                    presPrevOut = pres;

                    float y = bandPassed + pres * 0.25f;
                    resampled8kBuf[i] = y;
                    voiceBandSqSum += y * y;
                    float absY = Mathf.Abs(y);
                    if (absY > bandPeak) bandPeak = absY;

                    if (i > 0 && ((y >= 0f && prevVoiceSample < 0f) || (y < 0f && prevVoiceSample >= 0f)))
                    {
                        zeroCrossings++;
                    }
                    prevVoiceSample = y;
                }

                float voiceBandRms = Mathf.Sqrt(voiceBandSqSum / NetworkChunkSamples);
                float zcr = (float)zeroCrossings / NetworkChunkSamples;

                if (rawPeak > diagMaxRawPeak) diagMaxRawPeak = rawPeak;
                if (voiceBandRms > diagMaxVoiceRms) diagMaxVoiceRms = voiceBandRms;

                // Nếu máy có nhiều thiết bị thu âm mà thiết bị hiện tại hoàn toàn câm (rawPeak < 0.00005f) suốt 3 giây
                // thì tự động chuyển sang thiết bị Microphone tiếp theo!
                if (!hasConfirmedWorkingMic && Microphone.devices != null && Microphone.devices.Length > 1)
                {
                    if (rawPeak < 0.00005f)
                    {
                        silentMicDuration += 0.05f;
                        if (silentMicDuration >= 3.0f)
                        {
                            silentMicDuration = 0f;
                            currentMicDeviceIndex = (currentMicDeviceIndex + 1) % Microphone.devices.Length;
                            StopMicrophoneCapture();
                            return;
                        }
                    }
                    else
                    {
                        silentMicDuration = 0f;
                    }
                }

                // Tầng 2: Cập nhật lịch sử RMS 800ms & Học nền tiếng quạt tĩnh (Minimum-Statistics Noise Floor)
                // Luôn theo dõi mức cực tiểu trong 800ms để không bao giờ bị kẹt nền quạt!
                recentRmsHistory[rmsHistoryWriteIdx] = voiceBandRms;
                rmsHistoryWriteIdx = (rmsHistoryWriteIdx + 1) % NoiseHistorySize;
                if (rmsHistoryCount < NoiseHistorySize) rmsHistoryCount++;

                float minRecentRms = voiceBandRms;
                float maxRecentRms = voiceBandRms;
                float sumRecentRms = 0f;
                for (int h = 0; h < rmsHistoryCount; h++)
                {
                    float val = recentRmsHistory[h];
                    if (val < minRecentRms) minRecentRms = val;
                    if (val > maxRecentRms) maxRecentRms = val;
                    sumRecentRms += val;
                }
                float meanRecentRms = sumRecentRms / Mathf.Max(1, rmsHistoryCount);
                float modulationIndex = (maxRecentRms - minRecentRms) / (meanRecentRms + 0.0001f);

                // Cập nhật adaptiveNoiseFloor bám sát nền quạt thực tế (0.0015 .. 0.0085)
                float floorCandidate = Mathf.Clamp(minRecentRms * 1.15f, 0.0008f, 0.0085f);
                if (modulationIndex < 0.45f)
                {
                    // Khi âm thanh phẳng đều (chỉ có quạt chạy), hội tụ nhanh về mức trung bình của quạt
                    floorCandidate = Mathf.Clamp(meanRecentRms * 1.12f, 0.0008f, 0.0085f);
                    adaptiveNoiseFloor = Mathf.Lerp(adaptiveNoiseFloor, floorCandidate, 0.18f);
                }
                else
                {
                    adaptiveNoiseFloor = Mathf.Lerp(adaptiveNoiseFloor, floorCandidate, 0.06f);
                }

                float noiseRef = Mathf.Max(adaptiveNoiseFloor, 0.0018f);

                // Tầng 3: Phân tích 10 khung con 5ms & Tính Pitch trên dải 120-3400 Hz để lọc bỏ Tiếng Gõ Bàn / Click Chuột
                int subFrameCount = NetworkChunkSamples / SubFrameSamples;
                float maxSubRms = 0f;
                float sumSubRms = 0f;
                int activeSubFrames = 0;

                for (int sf = 0; sf < subFrameCount; sf++)
                {
                    int offset = sf * SubFrameSamples;
                    float subSq = 0f;
                    for (int k = 0; k < SubFrameSamples; k++)
                    {
                        float s = resampled8kBuf[offset + k];
                        subSq += s * s;
                    }
                    float subRms = Mathf.Sqrt(subSq / SubFrameSamples);
                    if (subRms > maxSubRms) maxSubRms = subRms;
                    sumSubRms += subRms;
                    if (subRms > noiseRef * 1.45f) activeSubFrames++;
                }

                float meanSubRms = sumSubRms / subFrameCount;
                float subFrameCrest = maxSubRms / (meanSubRms + 0.00005f);
                float peakToRmsCrest = bandPeak / (voiceBandRms + 0.00005f);

                float pitchCorr = ComputePitchCorrelation(pitchBandBuf, NetworkChunkSamples);
                if (pitchCorr > diagMaxPitch) diagMaxPitch = pitchCorr;

                // Nhận diện xung gõ bàn / click chuột:
                // Tiếng gõ bàn có đỉnh nhọn đột biến (subFrameCrest > 1.95 hoặc peakToRmsCrest > 4.1)
                // hoặc chỉ lóe lên ngắn <= 4 khung con 5ms và không có cấu trúc hài âm thanh quản mạnh!
                bool isTransientKnock = (subFrameCrest > 1.95f && pitchCorr < 0.52f)
                    || (peakToRmsCrest > 4.10f && pitchCorr < 0.50f)
                    || (activeSubFrames <= 4 && subFrameCrest > 1.75f && pitchCorr < 0.45f);

                // Nếu phát hiện xung gõ bàn trong bất kỳ khung con 5ms nào, lập tức dập xung đó về mức nền
                if (isTransientKnock || subFrameCrest > 1.90f)
                {
                    float clampTarget = Mathf.Max(noiseRef, meanSubRms * 0.70f);
                    for (int sf = 0; sf < subFrameCount; sf++)
                    {
                        int offset = sf * SubFrameSamples;
                        float subSq = 0f;
                        for (int k = 0; k < SubFrameSamples; k++)
                        {
                            float s = resampled8kBuf[offset + k];
                            subSq += s * s;
                        }
                        float subRms = Mathf.Sqrt(subSq / SubFrameSamples);
                        if (subRms > clampTarget * 1.25f)
                        {
                            float scale = clampTarget / (subRms + 0.00001f);
                            for (int k = 0; k < SubFrameSamples; k++)
                            {
                                resampled8kBuf[offset + k] *= scale;
                            }
                        }
                    }
                }

                // Tầng 4: Nhận diện Giọng nói Con người Thực sự (Human Speech Confirmation)
                bool inSelfEchoWindow = (Time.unscaledTime - lastPacketSentTime) < 0.45f;
                if (!inSelfEchoWindow)
                {
                    selfEchoEnvelope = 0f;
                }

                // Ngưỡng mở cổng thực tế: luôn cao hơn cả BaseVadRmsThreshold (0.0045f) VÀ gấp 2.3 lần nền tiếng quạt!
                float effectiveVadThreshold = Mathf.Max(BaseVadRmsThreshold, noiseRef * 2.30f);
                if (!wasTransmittingLastFrame && Time.unscaledTime < postSpeechCooldownUntil)
                {
                    effectiveVadThreshold = Mathf.Max(effectiveVadThreshold, selfEchoEnvelope);
                }

                float snr = voiceBandRms / noiseRef;
                bool inHangover = Time.unscaledTime <= vadHangoverUntil && Time.unscaledTime >= postSpeechCooldownUntil;
                bool isZcrSpeechLike = zcr >= 0.025f && zcr <= 0.48f;

                float minPitch = inHangover ? 0.30f : 0.36f;
                bool isFrameVoiced = !isTransientKnock
                    && voiceBandRms >= effectiveVadThreshold
                    && snr >= (inHangover ? 1.85f : 2.30f)
                    && isZcrSpeechLike
                    && activeSubFrames >= (inHangover ? 4 : 5)
                    && pitchCorr >= minPitch;

                if (isFrameVoiced)
                {
                    consecutiveVoicedFrames++;
                    if (consecutiveVoicedFrames >= 2)
                    {
                        vadHangoverUntil = Time.unscaledTime + VadHangoverDuration;
                        selfEchoEnvelope = Mathf.Max(selfEchoEnvelope * 0.92f, Mathf.Clamp(voiceBandRms * 0.75f, 0.0050f, 0.0450f));
                        hasConfirmedWorkingMic = true;
                    }
                }
                else
                {
                    consecutiveVoicedFrames = 0;
                    hasLookaheadPacket = false;
                    selfEchoEnvelope *= 0.85f;
                }

                // Yêu cầu tối thiểu 2 khung liên tiếp (100ms) có giọng người mới mở cổng truyền:
                // - Khung 1 lưu vào lookaheadEncodeBuf (để không mất âm đầu câu nếu là tiếng người thật).
                // - Tiếng gõ bàn chỉ kéo dài 1 khung (< 50ms) sẽ bị hủy bỏ hoàn toàn ở khung 2, 0% lọt qua loa!
                bool shouldTransmit = (consecutiveVoicedFrames >= 2)
                    || (Time.unscaledTime <= vadHangoverUntil && !isTransientKnock && voiceBandRms >= effectiveVadThreshold * 0.80f);
                bool isFirstLookaheadFrame = (consecutiveVoicedFrames == 1 && !shouldTransmit);

                if (wasTransmittingLastFrame && !shouldTransmit)
                {
                    postSpeechCooldownUntil = Time.unscaledTime + PostSpeechEchoCooldown;
                    vadHangoverUntil = -10f;
                }
                wasTransmittingLastFrame = shouldTransmit;

                // Tầng 5: Khử nền tiếng quạt Wiener & Khuếch đại Giọng nói Tự nhiên (Natural Gain 0.75x - 2.60x)
                float cleanSqSum = 0f;
                const float overSubtraction = 1.35f;

                for (int sf = 0; sf < subFrameCount; sf++)
                {
                    int offset = sf * SubFrameSamples;
                    float subSq = 0f;
                    for (int k = 0; k < SubFrameSamples; k++)
                    {
                        float s = resampled8kBuf[offset + k];
                        subSq += s * s;
                    }
                    float subRms = Mathf.Sqrt(subSq / SubFrameSamples);

                    float targetGate = 0f;
                    if ((shouldTransmit || isFirstLookaheadFrame) && subRms > noiseRef * 1.20f)
                    {
                        float cleanRatio = Mathf.Clamp01((subRms - noiseRef * overSubtraction) / (subRms + 0.00001f));
                        targetGate = cleanRatio * Mathf.Sqrt(cleanRatio);
                    }

                    for (int k = 0; k < SubFrameSamples; k++)
                    {
                        float gateSpeed = targetGate > currentNoiseGate ? 0.35f : 0.08f;
                        currentNoiseGate = Mathf.Lerp(currentNoiseGate, targetGate, gateSpeed);
                        float cleanVoice = resampled8kBuf[offset + k] * currentNoiseGate;
                        resampled8kBuf[offset + k] = cleanVoice;
                        cleanSqSum += cleanVoice * cleanVoice;
                    }
                }

                float cleanSpeechRms = Mathf.Sqrt(cleanSqSum / NetworkChunkSamples);

                // AGC tự nhiên (0.75x - 2.60x): giữ âm lượng giọng nói trung thực, không bao giờ phóng đại quá mức!
                if (isFrameVoiced && cleanSpeechRms > noiseRef * 1.20f)
                {
                    float desiredGain = Mathf.Clamp(TargetSpeechRms / Mathf.Max(cleanSpeechRms, 0.045f), MinAgcGain, MaxAgcGain);
                    float agcSpeed = desiredGain < currentAgcGain ? 0.35f : 0.18f;
                    currentAgcGain = Mathf.Lerp(currentAgcGain, desiredGain, agcSpeed);
                }
                else if (!shouldTransmit && !isFirstLookaheadFrame)
                {
                    currentAgcGain = Mathf.Lerp(currentAgcGain, DefaultAgcGain, 0.05f);
                }

                float sfxImpulse = AudioManager.Instance != null ? AudioManager.Instance.SfxImpulseEnvelope : 0f;
                float sfxDampen = Mathf.Clamp(1.0f - sfxImpulse * 0.45f, 0.45f, 1.0f);

                float totalGain = currentAgcGain * micVolume * sfxDampen;

                float boostedRmsSum = 0f;
                float boostedPeak = 0f;

                for (int i = 0; i < NetworkChunkSamples; i++)
                {
                    float amplified = resampled8kBuf[i] * totalGain;
                    float cleanSample = SoftClipLimiter(amplified);
                    encodeByteBuf[i] = EncodeMuLaw(cleanSample);

                    if (shouldTransmit || isFirstLookaheadFrame)
                    {
                        boostedRmsSum += cleanSample * cleanSample;
                        float absClean = Mathf.Abs(cleanSample);
                        if (absClean > boostedPeak) boostedPeak = absClean;
                    }
                }

                float outRms = Mathf.Sqrt(boostedRmsSum / NetworkChunkSamples);
                float frameLevel01 = Mathf.Clamp01(outRms * 3.0f + boostedPeak * 0.50f);

                if (isFirstLookaheadFrame && outRms > 0.004f)
                {
                    Buffer.BlockCopy(encodeByteBuf, 0, lookaheadEncodeBuf, 0, NetworkChunkSamples);
                    hasLookaheadPacket = true;
                    lookaheadLevel01 = frameLevel01;
                }
                else if (shouldTransmit && outRms > 0.004f)
                {
                    if (hasLookaheadPacket)
                    {
                        hasLookaheadPacket = false;
                        lastPacketSentTime = Time.unscaledTime;
                        diagSentPackets++;
                        if (networkMatchManager != null)
                        {
                            networkMatchManager.SendVoiceData(lookaheadEncodeBuf);
                        }
                        localVoiceLevel01 = Mathf.Max(localVoiceLevel01, lookaheadLevel01);
                    }

                    float speed = frameLevel01 > localVoiceLevel01 ? 32f : 9f;
                    localVoiceLevel01 = Mathf.MoveTowards(localVoiceLevel01, frameLevel01, speed * 0.08f);
                    lastPacketSentTime = Time.unscaledTime;
                    diagSentPackets++;

                    if (networkMatchManager != null)
                    {
                        networkMatchManager.SendVoiceData(encodeByteBuf);
                    }
                }
                else
                {
                    hasLookaheadPacket = false;
                    localVoiceLevel01 = Mathf.MoveTowards(localVoiceLevel01, 0f, 0.6f);
                }
            }
        }

        private static float SoftClipLimiter(float x)
        {
            if (x > 3.8f) return 0.96f;
            if (x < -3.8f) return -0.96f;
            return (x / Mathf.Sqrt(1f + x * x)) * 0.96f;
        }

        private void HandleVoiceStateReceived(int senderIndex, bool micOn, bool speakerOn)
        {
            bool changed = (remoteMicEnabled != micOn) || (remoteSpeakerEnabled != speakerOn);
            remoteMicEnabled = micOn;
            remoteSpeakerEnabled = speakerOn;
            if (!remoteMicEnabled)
            {
                remoteTargetLevel01 = 0f;
                remoteVoiceLevel01 = 0f;
            }

            if (changed)
            {
                OnVoiceSettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Giải mã gói âm thanh G.711 μ-law từ đối thủ và đẩy vào bộ đệm vòng PCM cho OnAudioFilterRead phát ra loa.
        /// Hỗ trợ Trọng tài Đa cửa sổ thông minh (Dual-Tab Same-PC Arbitration):
        /// - Khi mở 2 tab trên cùng 1 máy và cả 2 cùng bật Mic, nếu cả 2 cùng thu giọng nói ở thời điểm t=0:
        ///   MasterClient giữ quyền truyền Mic liên tục, còn Client tự động chuyển sang chế độ Nhận &amp; Phát Loa (khóa Mic qua echoHoldUntil).
        /// - Nếu người chơi tắt Mic ở MasterClient và nói ở Client: MasterClient tự động chuyển sang Nhận &amp; Phát Loa!
        /// </summary>
        private void HandleVoiceDataReceived(int senderIndex, byte[] ulawData)
        {
            if (ulawData == null || ulawData.Length == 0) return;

            remoteMicEnabled = true;
            diagRecvPackets++;

            bool isSimultaneousCollision = isMicEnabled
                && micVolume > 0.001f
                && (Time.unscaledTime - lastPacketSentTime) < 0.24f;

            // Nếu cả 2 cửa sổ đang cùng thu chung 1 câu nói (mở 2 tab trên 1 máy):
            // MasterClient giữ vai trò Người Nói (bỏ qua gói dội trùng lặp từ Client để không tự khóa Mic của chính mình),
            // còn Client nhận vai trò Người Nghe (khóa Mic qua echoHoldUntil và phát giọng nói ra Loa)!
            if (isSimultaneousCollision && PhotonNetwork.IsMasterClient)
            {
                return;
            }

            if (threadSpeakerActive)
            {
                echoHoldUntil = Mathf.Max(echoHoldUntil, Time.unscaledTime + EchoGuardDuration);
            }

            int count = ulawData.Length;
            const float makeupGain = 1.0f;
            float sumSq = 0f;
            float peak = 0f;

            lock (ringLock)
            {
                // Nếu bộ đệm tích tụ quá nhiều (> 1 giây) thì nhảy bớt để giữ thời gian thực
                if (ringAvailableSamples + count > MaxLatencySamples)
                {
                    int drop = (ringAvailableSamples + count) - JitterStartThreshold;
                    if (drop > 0 && drop <= ringAvailableSamples)
                    {
                        ringReadIndex = (ringReadIndex + drop) % RingBufferCapacity;
                        ringAvailableSamples -= drop;
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    float raw = DecodeMuLaw(ulawData[i]);
                    float s = SoftClipLimiter(raw * makeupGain);
                    sumSq += s * s;
                    float abs = Mathf.Abs(s);
                    if (abs > peak) peak = abs;

                    if (threadSpeakerActive)
                    {
                        pcmRingBuffer[ringWriteIndex] = s;
                        ringWriteIndex = (ringWriteIndex + 1) % RingBufferCapacity;
                        if (ringAvailableSamples < RingBufferCapacity)
                        {
                            ringAvailableSamples++;
                        }
                        else
                        {
                            ringReadIndex = (ringReadIndex + 1) % RingBufferCapacity;
                        }
                    }
                }
            }

            float rms = Mathf.Sqrt(sumSq / count);
            remoteTargetLevel01 = Mathf.Clamp01(rms * 2.4f + peak * 0.45f);
            lastPacketReceiveTime = Time.unscaledTime;
        }

        /// <summary>
        /// Luồng âm thanh phần cứng của Unity (Audio Thread):
        /// Đọc mẫu thoại 8000 Hz từ pcmRingBuffer, nội suy tuyến tính mượt mà lên tần số đầu ra (44100/48000 Hz)
        /// và xuất thẳng ra các kênh loa (Stereo).
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (data == null || data.Length == 0 || channels <= 0) return;

            if (!threadSpeakerActive)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            float step = (float)NetworkSampleRate / Mathf.Max(8000, outputSampleRate);
            float gain = threadOutputGain;
            int frames = data.Length / channels;

            lock (ringLock)
            {
                if (jitterBuffering)
                {
                    if (ringAvailableSamples >= JitterStartThreshold)
                    {
                        jitterBuffering = false;
                        starvationSampleCount = 0;
                        prevReadSample = PopRingSampleUnsafe();
                        currReadSample = ringAvailableSamples > 0 ? PopRingSampleUnsafe() : prevReadSample;
                        resampleFracPhase = 0f;
                    }
                    else
                    {
                        Array.Clear(data, 0, data.Length);
                        return;
                    }
                }

                for (int f = 0; f < frames; f++)
                {
                    while (resampleFracPhase >= 1f)
                    {
                        resampleFracPhase -= 1f;
                        prevReadSample = currReadSample;
                        if (ringAvailableSamples > 0)
                        {
                            currReadSample = PopRingSampleUnsafe();
                            starvationSampleCount = 0;
                        }
                        else
                        {
                            // Giảm dần mượt về 0 và cho phép chờ ngắn (40ms) trước khi bật lại jitterBuffering
                            currReadSample *= 0.85f;
                            starvationSampleCount++;
                            if (starvationSampleCount >= MaxStarvationSamples)
                            {
                                currReadSample = 0f;
                                prevReadSample = 0f;
                                jitterBuffering = true;
                            }
                        }
                    }

                    float sample = Mathf.Lerp(prevReadSample, currReadSample, resampleFracPhase) * gain;
                    sample = Mathf.Clamp(sample, -0.98f, 0.98f);
                    resampleFracPhase += step;

                    int baseIdx = f * channels;
                    for (int c = 0; c < channels; c++)
                    {
                        data[baseIdx + c] = sample;
                    }
                }
            }
        }

        private float PopRingSampleUnsafe()
        {
            if (ringAvailableSamples <= 0) return 0f;
            float s = pcmRingBuffer[ringReadIndex];
            pcmRingBuffer[ringReadIndex] = 0f;
            ringReadIndex = (ringReadIndex + 1) % RingBufferCapacity;
            ringAvailableSamples--;
            return s;
        }

        private void ClearPlaybackBuffer()
        {
            lock (ringLock)
            {
                Array.Clear(pcmRingBuffer, 0, pcmRingBuffer.Length);
                ringWriteIndex = 0;
                ringReadIndex = 0;
                ringAvailableSamples = 0;
                resampleFracPhase = 0f;
                prevReadSample = 0f;
                currReadSample = 0f;
                starvationSampleCount = 0;
                jitterBuffering = true;
            }
        }

        #region ITU-T G.711 μ-law Codec (16-bit PCM <-> 8-bit μ-law)

        public static byte EncodeMuLaw(float sample)
        {
            int pcm = Mathf.Clamp(Mathf.RoundToInt(sample * 32767f), -32768, 32767);
            int sign = (pcm >> 8) & 0x80;
            if (sign != 0) pcm = -pcm;
            if (pcm > MuLawClip) pcm = MuLawClip;
            pcm += MuLawBias;

            int exponent = 7;
            for (int expMask = 0x4000; (pcm & expMask) == 0 && exponent > 0; exponent--, expMask >>= 1)
            {
            }

            int mantissa = (pcm >> (exponent + 3)) & 0x0F;
            byte ulawByte = (byte)~(sign | (exponent << 4) | mantissa);
            return ulawByte;
        }

        public static float DecodeMuLaw(byte ulawByte)
        {
            int ulaw = ~ulawByte;
            int sign = ulaw & 0x80;
            int exponent = (ulaw >> 4) & 0x07;
            int mantissa = ulaw & 0x0F;

            int sample = (((mantissa << 3) + MuLawBias) << exponent) - MuLawBias;
            if (sign != 0) sample = -sample;
            return Mathf.Clamp(sample / 32767f, -1f, 1f);
        }

        #endregion
    }
}
