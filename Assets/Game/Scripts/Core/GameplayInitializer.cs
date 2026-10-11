using UnityEngine;
using Game.Billiards.Camera;
using Game.Billiards.Core;
using Game.Billiards.Input;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.Core
{
    public class GameplayInitializer : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private BallManager ballManager;
        [SerializeField] private BallMovementMonitor movementMonitor;
        [SerializeField] private TableWrapper tableWrapper;
        [SerializeField] private ShotExecutor shotExecutor;
        [SerializeField] private CameraDirector cameraDirector;
        [SerializeField] private CueController cueController;
        [SerializeField] private PlayerInputController inputController;
        [SerializeField] private BallInHandController ballInHandController;
        [SerializeField] private UI.CueBallContactSelector contactSelector;
        [SerializeField] private MatchStateManager matchStateManager;
        [SerializeField] private Rules.Wpa8BallRuleEngine ruleEngine;
        [SerializeField] private Rules.ShotEventCollector eventCollector;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private UI.GameplayHUD gameplayHUD;
        [SerializeField] private AI.AIController aiController;
        [SerializeField] private Network.NetworkMatchManager networkMatchManager;
        [SerializeField] private Audio.AudioManager audioManager;
        [SerializeField] private VFX.VFXManager vfxManager;
        [SerializeField] private PhysicsConfig physicsConfig;

        [Header("Auto Start")]
        [Tooltip("Tự động xếp dàn bi WPA 8-Ball khi bắt đầu scene")]
        [SerializeField] private bool autoResetRackOnStart = true;

        private void Awake()
        {
            FindReferencesIfMissing();
        }

        private void Start()
        {
            SetupWiring();

            if (autoResetRackOnStart && ballManager != null)
            {
                ballManager.ResetRack();
            }
        }

        private void FindReferencesIfMissing()
        {
            if (ballManager == null) ballManager = FindObjectOfType<BallManager>();
            if (movementMonitor == null) movementMonitor = FindObjectOfType<BallMovementMonitor>();
            if (tableWrapper == null) tableWrapper = FindObjectOfType<TableWrapper>();
            if (shotExecutor == null) shotExecutor = FindObjectOfType<ShotExecutor>();
            if (cameraDirector == null) cameraDirector = FindObjectOfType<CameraDirector>();
            if (cueController == null) cueController = FindObjectOfType<CueController>();
            if (inputController == null) inputController = FindObjectOfType<PlayerInputController>();
            if (ballInHandController == null) ballInHandController = FindObjectOfType<BallInHandController>();
            if (contactSelector == null) contactSelector = FindObjectOfType<UI.CueBallContactSelector>();
            if (matchStateManager == null) matchStateManager = FindObjectOfType<MatchStateManager>();
            if (ruleEngine == null) ruleEngine = FindObjectOfType<Rules.Wpa8BallRuleEngine>();
            if (eventCollector == null) eventCollector = FindObjectOfType<Rules.ShotEventCollector>();
            if (turnManager == null) turnManager = FindObjectOfType<TurnManager>();
            if (gameplayHUD == null) gameplayHUD = FindObjectOfType<UI.GameplayHUD>();
            if (aiController == null) aiController = FindObjectOfType<AI.AIController>();
            if (networkMatchManager == null) networkMatchManager = FindObjectOfType<Network.NetworkMatchManager>();
            if (audioManager == null) audioManager = FindObjectOfType<Audio.AudioManager>();
            if (vfxManager == null) vfxManager = FindObjectOfType<VFX.VFXManager>();
        }

        public void SetupWiring()
        {
            // 1. Khởi tạo BallManager
            if (ballManager != null)
            {
                ballManager.InitializeRegistry();
            }

            // 2. Liên kết bi cái với CameraDirector và CueController
            var cueBall = ballManager != null ? ballManager.CueBall : null;
            if (cueBall != null)
            {
                if (cameraDirector != null)
                {
                    cameraDirector.SetCueBallTarget(cueBall.transform);
                }

                if (cueController != null)
                {
                    cueController.SetCueBallTarget(cueBall.transform);
                    if (cameraDirector != null)
                    {
                        cueController.UpdateAim(cameraDirector.AimDirection, 0.35f);
                    }
                }
            }

            // 3. Khởi tạo BallInHandController
            if (ballInHandController != null && ballManager != null && tableWrapper != null && cameraDirector != null)
            {
                ballInHandController.Initialize(ballManager, tableWrapper, cameraDirector);
            }

            // 4. Khởi tạo ShotEventCollector
            if (eventCollector != null && ballManager != null && tableWrapper != null)
            {
                eventCollector.Initialize(ballManager, tableWrapper);
            }

            // 5. Khởi tạo MatchStateManager
            if (matchStateManager != null)
            {
                var session = GameSession.Instance;
                if (session != null)
                {
                    matchStateManager.Initialize(
                        session.CurrentSettings, 
                        session.PlayerAName, 
                        session.PlayerBName, 
                        session.IsPlayerBAi
                    );
                }
                else
                {
                    matchStateManager.Initialize(MatchSettings.Default);
                }
            }

            // 6. Khởi tạo ShotExecutor
            if (shotExecutor != null && physicsConfig != null && ballManager != null && movementMonitor != null)
            {
                shotExecutor.Initialize(physicsConfig, ballManager, movementMonitor);
            }

            // 7. Khởi tạo TurnManager
            if (turnManager != null && matchStateManager != null && ruleEngine != null && eventCollector != null && shotExecutor != null && movementMonitor != null && ballManager != null && ballInHandController != null)
            {
                turnManager.Initialize(matchStateManager, ruleEngine, eventCollector, shotExecutor, movementMonitor, ballManager, ballInHandController);
            }

            // 8. Khởi tạo PlayerInputController
            if (inputController != null && cameraDirector != null && cueController != null && shotExecutor != null && movementMonitor != null)
            {
                inputController.Initialize(cameraDirector, cueController, shotExecutor, movementMonitor, ballInHandController, contactSelector);
            }

            // 9. Khởi tạo GameplayHUD
            if (gameplayHUD != null && matchStateManager != null && turnManager != null && inputController != null && ballManager != null && movementMonitor != null)
            {
                gameplayHUD.Initialize(matchStateManager, turnManager, inputController, ballManager, movementMonitor, contactSelector);
            }

            // 10. Khởi tạo AIController
            if (aiController != null && matchStateManager != null && turnManager != null && ballManager != null && tableWrapper != null && shotExecutor != null && cueController != null && cameraDirector != null && ballInHandController != null)
            {
                aiController.Initialize(matchStateManager, turnManager, ballManager, tableWrapper, shotExecutor, cueController, cameraDirector, ballInHandController);

                var session = GameSession.Instance;
                if (session != null)
                {
                    aiController.SetAIEnabled(session.IsPlayerBAi, 1);
                    aiController.SetDifficulty(session.SelectedAIDifficulty);
                }
            }

            // 11. Khởi tạo NetworkMatchManager (Milestone M9)
            if (networkMatchManager != null)
            {
                networkMatchManager.Initialize(
                    matchStateManager,
                    turnManager,
                    ballManager,
                    shotExecutor,
                    inputController,
                    cueController,
                    cameraDirector,
                    movementMonitor,
                    ballInHandController
                );

                if (turnManager != null)
                {
                    turnManager.SetNetworkMatchManager(networkMatchManager);
                }

                if (networkMatchManager.IsOnlineMatch && aiController != null)
                {
                    aiController.SetAIEnabled(false, 1);
                }
            }
        }
    }
}
