using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Billiards.AI;

namespace Game.Billiards.Core
{
    public enum GameMode
    {
        OfflineVsAI = 0,
        OnlinePvP = 1,
        LocalPractice = 2
    }

    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [Header("Match Setup")]
        [SerializeField] private GameMode currentGameMode = GameMode.OfflineVsAI;
        [SerializeField] private AIDifficulty selectedAIDifficulty = AIDifficulty.Normal;
        [SerializeField] private MatchSettings currentSettings = MatchSettings.Default;

        [Header("Players")]
        [SerializeField] private string playerAName = "Cơ Thủ (Player 1)";
        [SerializeField] private string playerBName = "AI Đối Thủ (Normal)";
        [SerializeField] private bool isPlayerBAi = true;

        public GameMode CurrentGameMode => currentGameMode;
        public AIDifficulty SelectedAIDifficulty => selectedAIDifficulty;
        public MatchSettings CurrentSettings => currentSettings;
        public string PlayerAName => playerAName;
        public string PlayerBName => playerBName;
        public bool IsPlayerBAi => isPlayerBAi;

        private const string AimGuidePrefKey = "AimGuide_Enabled";

        /// <summary>Bật/tắt thước ngắm (lưu qua PlayerPrefs, mặc định bật).</summary>
        public static bool AimGuideEnabled
        {
            get => PlayerPrefs.GetInt(AimGuidePrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(AimGuidePrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private const string RackIntroPrefKey = "RackIntro_Enabled";

        /// <summary>Bật/tắt đoạn intro xếp bi đầu trận (lưu qua PlayerPrefs, mặc định bật).</summary>
        public static bool RackIntroEnabled
        {
            get => PlayerPrefs.GetInt(RackIntroPrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(RackIntroPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Cấu hình target frame rate theo yêu cầu đồ án (60 FPS)
            Application.targetFrameRate = 60;
        }

        public static GameSession GetOrCreateInstance()
        {
            if (Instance == null)
            {
                var go = new GameObject("GameSession");
                Instance = go.AddComponent<GameSession>();
                DontDestroyOnLoad(go);
            }
            return Instance;
        }

        public void StartOfflineMatch(AIDifficulty difficulty, RaceTarget raceTarget, ShotTimerOption timerOption, string playerName = "Cơ Thủ")
        {
            currentGameMode = GameMode.OfflineVsAI;
            selectedAIDifficulty = difficulty;
            isPlayerBAi = true;

            currentSettings = new MatchSettings
            {
                raceTarget = raceTarget,
                shotTimer = timerOption,
                useWpaRules = true
            };

            playerAName = string.IsNullOrEmpty(playerName) ? LocalizationManager.Get("default_player_name") : playerName;
            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    playerBName = LocalizationManager.Get("ai_easy_name");
                    break;
                case AIDifficulty.Hard:
                    playerBName = LocalizationManager.Get("ai_hard_name");
                    break;
                case AIDifficulty.Normal:
                default:
                    playerBName = LocalizationManager.Get("ai_normal_name");
                    break;
            }

            Debug.Log($"[GameSession] Khởi động trận Offline vs AI: Cấp độ {difficulty}, Race to {(int)raceTarget}, Shot Timer: {timerOption}");
            LoadGameplayScene();
        }

        public void StartOnlineMatch(MatchSettings settings, string playerA, string playerB, bool amIMaster)
        {
            currentGameMode = GameMode.OnlinePvP;
            selectedAIDifficulty = AIDifficulty.Normal;
            isPlayerBAi = false;
            currentSettings = settings;
            playerAName = playerA;
            playerBName = playerB;

            Debug.Log($"[GameSession] Khởi động trận Online 1v1: {playerA} vs {playerB}");
            LoadGameplayScene();
        }

        public void LoadLobbyScene()
        {
            if (Application.isPlaying)
            {
                // Online: rời phòng Photon để đối thủ nhận OnPlayerLeftRoom (thoát giữa trận = thua trận, Rule 124)
                if (currentGameMode == GameMode.OnlinePvP && Photon.Pun.PhotonNetwork.InRoom)
                {
                    Photon.Pun.PhotonNetwork.LeaveRoom();
                }

                // Màn hình tải bất đồng bộ khi thoát về sảnh
                Game.Billiards.UI.LoadingScreen.ShowForLobby();
            }
        }

        public void LoadGameplayScene()
        {
            if (!Application.isPlaying) return;

            if (currentGameMode == GameMode.OnlinePvP)
            {
                // Online: scene Gameplay do Photon tải đồng bộ (PhotonNetwork.LoadLevel). Màn hình tải theo dõi tiến độ và chờ cả hai máy.
                Game.Billiards.UI.LoadingScreen.ShowForOnlineMatch();
            }
            else
            {
                Game.Billiards.UI.LoadingScreen.ShowForOfflineMatch();
            }
        }

        public void QuitGame()
        {
            Debug.Log("[GameSession] Thoát trò chơi về Desktop.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
