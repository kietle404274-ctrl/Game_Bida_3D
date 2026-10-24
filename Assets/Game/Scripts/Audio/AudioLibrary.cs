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

        [Tooltip("Âm thanh tiếng còi phạm quy")]
        [SerializeField] private AudioClip foulClip;

        [Tooltip("Âm thanh chiến thắng trận đấu")]
        [SerializeField] private AudioClip winFanfareClip;

        [Tooltip("Âm thanh tương tác nút bấm UI")]
        [SerializeField] private AudioClip buttonClickClip;

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
        public AudioClip FoulClip => foulClip;
        public AudioClip WinFanfareClip => winFanfareClip;
        public AudioClip ButtonClickClip => buttonClickClip;

        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;

        public float GetRandomPitch()
        {
            return UnityEngine.Random.Range(minPitch, maxPitch);
        }
    }
}
