using System;
using UnityEngine;

namespace Game.Billiards.Audio
{
    [CreateAssetMenu(fileName = "DefaultAudioLibrary", menuName = "Billiards/Audio Library", order = 1)]
    public class AudioLibrary : ScriptableObject
    {
        [Header("SFX Clips")]
        [Tooltip("Âm thanh gậy cơ chọc bi cái")]
        [SerializeField] private AudioClip cueHitClip;

        [Tooltip("Âm thanh va chạm giữa 2 bi bida")]
        [SerializeField] private AudioClip ballHitClip;

        [Tooltip("Âm thanh cú phá bi (khai cuộc)")]
        [SerializeField] private AudioClip breakShotClip;

        [Tooltip("Âm thanh bi đập vào băng cao su bàn")]
        [SerializeField] private AudioClip cushionHitClip;

        [Tooltip("Âm thanh bi rơi vào lỗ")]
        [SerializeField] private AudioClip pocketDropClip;

        [Tooltip("Âm thanh thông báo phạm lỗi bi (Tieng_Loi_Bi)")]
        [SerializeField] private AudioClip foulClip;

        [Tooltip("Âm thanh chiến thắng ván đấu (dự phòng)")]
        [SerializeField] private AudioClip winFanfareClip;

        [Tooltip("Âm thanh thông báo thắng mỗi ván (Tieng_Ngon_Thi)")]
        [SerializeField] private AudioClip rackWinClip;

        [Tooltip("Âm thanh thông báo thua mỗi ván (Tieng_Meo_Khoc)")]
        [SerializeField] private AudioClip rackLoseClip;

        [Tooltip("Âm thanh đồng hồ đếm ngược khi gần hết giờ (Tieng_Dong_Ho)")]
        [SerializeField] private AudioClip timerTickClip;

        [Tooltip("Âm thanh chiến thắng cuối trận (Tieng_Win)")]
        [SerializeField] private AudioClip matchWinClip;

        [Tooltip("Âm thanh thua cuộc cuối trận (Tieng_Thua)")]
        [SerializeField] private AudioClip matchLoseClip;

        [Tooltip("Âm thanh tương tác nút bấm UI")]
        [SerializeField] private AudioClip buttonClickClip;

        [Header("Music")]
        [Tooltip("Nhạc nền 1 của trò chơi (Nhac_nen)")]
        [SerializeField] private AudioClip backgroundMusicClip;

        [Tooltip("Nhạc nền 2 của trò chơi (Nhac_nen_2)")]
        [SerializeField] private AudioClip backgroundMusicClip2;

        [Header("Pitch Variation Settings")]
        [Range(0.8f, 1.2f)]
        [SerializeField] private float minPitch = 0.94f;

        [Range(0.8f, 1.2f)]
        [SerializeField] private float maxPitch = 1.06f;

        public AudioClip CueHitClip => cueHitClip;
        public AudioClip BallHitClip => ballHitClip;
        public AudioClip BreakShotClip => breakShotClip;
        public AudioClip CushionHitClip => cushionHitClip;
        public AudioClip PocketDropClip => pocketDropClip;
        public AudioClip FoulClip
        {
            get
            {
#if UNITY_EDITOR
                if (foulClip == null || foulClip.name == "sfx_foul")
                {
                    var customFoul = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Tieng_Loi_Bi.mp3");
                    if (customFoul != null) foulClip = customFoul;
                }
#endif
                return foulClip;
            }
        }
        public AudioClip WinFanfareClip => winFanfareClip;
        public AudioClip RackWinClip => rackWinClip;
        public AudioClip RackLoseClip => rackLoseClip;
        public AudioClip TimerTickClip => timerTickClip;
        public AudioClip MatchWinClip => matchWinClip;
        public AudioClip MatchLoseClip => matchLoseClip;
        public AudioClip ButtonClickClip => buttonClickClip;
        public AudioClip BackgroundMusicClip => backgroundMusicClip;
        public AudioClip BackgroundMusicClip2 => backgroundMusicClip2;

        public AudioClip GetMusicClip(int index)
        {
            if (index == 1)
            {
#if UNITY_EDITOR
                if (backgroundMusicClip2 == null)
                {
                    backgroundMusicClip2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Nhac_nen_2.mp3");
                }
#endif
                if (backgroundMusicClip2 != null) return backgroundMusicClip2;
            }
#if UNITY_EDITOR
            if (backgroundMusicClip == null)
            {
                backgroundMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Nhac_nen.mp3");
            }
#endif
            return backgroundMusicClip != null ? backgroundMusicClip : backgroundMusicClip2;
        }

        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;

        public float GetRandomPitch()
        {
            return UnityEngine.Random.Range(minPitch, maxPitch);
        }
    }
}
