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

            playerAName = string.IsNullOrEmpty(playerName) ? "Cơ Thủ" : playerName;
            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    playerBName = "AI (Dễ)";
                    break;
                case AIDifficulty.Hard:
                    playerBName = "AI (Khó)";
                    break;
                case AIDifficulty.Normal:
                default:
                    playerBName = "AI (Bình Thường)";
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
            SceneManager.LoadScene("Lobby");
        }

        public void LoadGameplayScene()
        {
            SceneManager.LoadScene("Gameplay");
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
