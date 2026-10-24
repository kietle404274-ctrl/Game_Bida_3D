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
            ApplyGlobalPhysicsSettings();
            RemoveRedundantCueSticks();
            FindReferencesIfMissing();

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Lobby")
            {
                isLobbyDisplayMode = true;
                DisableGameplayForLobby();
            }
        }

        /// <summary>
        /// Scene Lobby chỉ là bối cảnh trưng bày: tắt toàn bộ điều khiển gameplay (chuột điều khiển cơ/camera/AI/HUD)
        /// để chuột dành riêng cho các nút sảnh. Camera do LobbyCameraOrbit điều khiển.
        /// </summary>
        private void DisableGameplayForLobby()
        {
            if (inputController != null) inputController.enabled = false;
            if (ballInHandController != null) ballInHandController.enabled = false;
            if (contactSelector != null) contactSelector.enabled = false;
            if (aiController != null) aiController.enabled = false;
            if (gameplayHUD != null) gameplayHUD.enabled = false;
            if (turnManager != null) turnManager.enabled = false;
            if (shotExecutor != null) shotExecutor.enabled = false;
            if (eventCollector != null) eventCollector.enabled = false;
            if (matchStateManager != null) matchStateManager.enabled = false;
            if (cameraDirector != null) cameraDirector.enabled = false;
        }

        private bool isLobbyDisplayMode;

        private System.Collections.IEnumerator LobbyDisplayRoutine()
        {
            if (ballManager != null) ballManager.ResetRack();

            // Chờ 1 frame để CueController.Start() chạy xong rồi mới đặt cơ cố định
            yield return null;

            var cueBall = ballManager != null ? ballManager.CueBall : null;
            if (cueBall != null && cueController != null && tableWrapper != null)
            {
                Vector3 head = tableWrapper.HeadSpot;
                Vector3 foot = tableWrapper.FootSpot;
                Vector3 center = (head + foot) * 0.5f;

                // u: trục dọc bàn (hướng về phía bi cái), v: trục ngang bàn
                Vector3 u = head - foot; u.y = 0f;
                if (u.sqrMagnitude < 0.0001f) u = Vector3.right;
                u.Normalize();
                Vector3 v = Vector3.Cross(Vector3.up, u).normalized;

                // Cơ nằm trên mặt nỉ, chéo ~22° so với trục bàn, lệch về một bên cho cân đối với dàn bi
                float yaw = 22f * Mathf.Deg2Rad;
                Vector3 forward = (-u * Mathf.Cos(yaw) + v * Mathf.Sin(yaw)).normalized;
                Vector3 target = center + u * -0.08f + v * 0.16f;

                float radius = (ballManager != null && ballManager.PhysicsConfig != null) ? ballManager.PhysicsConfig.BallRadius : 0.0286f;
                float clothY = head.y - radius;

                cueController.SetCueBallTarget(cueBall.transform);
                cueController.PlaceAtRest(new Vector3(target.x, clothY + 0.02f, target.z), Quaternion.LookRotation(forward, Vector3.up));

                // Căn lại theo bounds thật của cây cơ: tâm cơ về điểm đặt, đáy cơ chạm mặt nỉ
                Transform stick = cueController.StickRoot;
                var rends = stick.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    Vector3 shift = new Vector3(target.x - b.center.x, clothY - b.min.y, target.z - b.center.z);
                    stick.position += shift;
                }
            }
        }

        private void ApplyGlobalPhysicsSettings()
        {
            UnityEngine.Physics.bounceThreshold = 0.05f;
            UnityEngine.Physics.defaultSolverIterations = 12;
            UnityEngine.Physics.defaultSolverVelocityIterations = 4;
            UnityEngine.Physics.defaultMaxAngularSpeed = 100f;
        }

        private void RemoveRedundantCueSticks()
        {
            var oldCue1 = GameObject.Find("Cue Stick (1)");
            if (oldCue1 != null) oldCue1.SetActive(false);
            var oldCue2 = GameObject.Find("Cue Stick (2)");
            if (oldCue2 != null) oldCue2.SetActive(false);
        }

        private System.Collections.IEnumerator Start()
        {
            if (isLobbyDisplayMode)
            {
                yield return LobbyDisplayRoutine();
                yield break;
            }

            // HUD trượt vào khi bắt đầu trận: đẩy ra ngoài màn hình trước, phát hiệu ứng sau khi intro (nếu có) kết thúc
            UI.HudEntranceAnimator hudEntrance = null;
            if (gameplayHUD != null && gameplayHUD.isActiveAndEnabled)
            {
                try { hudEntrance = UI.HudEntranceAnimator.Prepare(gameplayHUD); }
                catch (System.Exception e) { Debug.LogException(e); }
            }

            if (FindObjectOfType<UI.TvScoreboardDisplay>() == null)
                gameObject.AddComponent<UI.TvScoreboardDisplay>();

            if (autoResetRackOnStart && ballManager != null)
            {
                ballManager.ResetRack();
            }

            // Chờ 1 frame để các script khác (CameraDirector, ...) hoàn tất Start()
            yield return null;

            bool introDone = false;
            var intro = FindObjectOfType<RackIntroSequence>();
            if (intro == null && ballManager != null && GameSession.RackIntroEnabled)
            {
                intro = gameObject.AddComponent<RackIntroSequence>();
            }

            if (intro != null && GameSession.RackIntroEnabled)
            {
                intro.Play(() => introDone = true);
                while (!introDone) yield return null;
            }

            if (hudEntrance != null) hudEntrance.Play();

            SetupWiring();
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
                    cameraDirector.SwitchToAimCamera();
                }

                if (cueController != null)
                {
                    cueController.SetCueBallTarget(cueBall.transform);
                    cueController.SetAimingState(true);
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
                var session = GameSession.GetOrCreateInstance();
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
                inputController.Initialize(cameraDirector, cueController, shotExecutor, movementMonitor, ballInHandController, contactSelector, matchStateManager, turnManager);
            }

            // 8b. Thước ngắm (bật/tắt theo cài đặt trước trận)
            if (ballManager != null && cameraDirector != null && inputController != null)
            {
                var aimGuide = FindObjectOfType<UI.AimGuide>();
                if (aimGuide == null) aimGuide = gameObject.AddComponent<UI.AimGuide>();
                aimGuide.Initialize(ballManager, cameraDirector, inputController);
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

                var session = GameSession.GetOrCreateInstance();
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

            // 12. Hiệu ứng hover cho nút HUD và các popup (hướng dẫn, luật, cài đặt, kết quả...) sau khi mọi thứ đã khởi tạo
            UI.UIHoverEffect.AttachToSceneButtons();

            // 13. Thống kê trận đấu + màn hình thống kê (phím Tab). Tạo sau bước 12 để không bị ghi đè hiệu ứng hover của tab.
            SetupMatchStats();
        }

        private void SetupMatchStats()
        {
            if (gameplayHUD == null || matchStateManager == null)
            {
                Debug.LogWarning("[GameplayInitializer] Bỏ qua khởi tạo thống kê: thiếu gameplayHUD hoặc matchStateManager.");
                return;
            }

            var tracker = FindObjectOfType<MatchStatsTracker>();
            if (tracker == null) tracker = gameObject.AddComponent<MatchStatsTracker>();
            tracker.Initialize(matchStateManager, turnManager, shotExecutor, ruleEngine, ballManager, networkMatchManager);

            if (FindObjectOfType<UI.StatsPanelUI>() == null)
            {
                var panel = UI.StatsPanelUI.Create(gameplayHUD, tracker, matchStateManager);
                gameplayHUD.SetStatsPanel(panel);
            }
        }
    }
}
