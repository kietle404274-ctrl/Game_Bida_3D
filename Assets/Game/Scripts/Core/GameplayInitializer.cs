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
            bool isLobby = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Lobby";
            ApplyGlobalPhysicsSettings(isLobby);
            RemoveRedundantCueSticks();
            FindReferencesIfMissing();

            if (isLobby)
            {
                isLobbyDisplayMode = true;
                DisableGameplayForLobby();
                TableThemeApplier.ApplyThemeToCurrentScene(GameSession.SelectedTableTheme);
            }
            else if (Photon.Pun.PhotonNetwork.InRoom)
            {
                var session = GameSession.GetOrCreateInstance();
                var netMgr = Network.NetworkManager.Instance;
                MatchSettings settings = netMgr != null ? netMgr.ExtractSettingsFromRoomProperties() : session.CurrentSettings;
                string p1 = netMgr != null ? netMgr.GetPlayer1Name() : session.PlayerAName;
                string p2 = netMgr != null ? netMgr.GetPlayer2Name() : session.PlayerBName;
                session.SyncOnlineMatchState(settings, p1, p2, Photon.Pun.PhotonNetwork.IsMasterClient);
                TableThemeApplier.ApplyThemeToCurrentScene(TableThemeApplier.ResolveActiveMatchTableTheme());
            }
            else
            {
                TableThemeApplier.ApplyThemeToCurrentScene(GameSession.SelectedTableTheme);
            }
        }

        /// <summary>
        /// Scene Lobby chỉ là bối cảnh trưng bày: tắt toàn bộ điều khiển gameplay (chuột điều khiển cơ/camera/AI/HUD/vật lý ngầm)
        /// để chuột dành riêng cho các nút sảnh và triệt tiêu 100% tải FixedUpdate ở sảnh chờ. Camera do LobbyCameraOrbit điều khiển.
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
            if (movementMonitor != null) movementMonitor.enabled = false;
            if (ruleEngine != null) ruleEngine.enabled = false;
            if (cueController != null) cueController.enabled = false;
        }

        private bool isLobbyDisplayMode;

        private System.Collections.IEnumerator LobbyDisplayRoutine()
        {
            if (ballManager != null)
            {
                ballManager.ResetRack(BallManager.GenerateWpaRackBallNumbersStatic(2026));
                if (ballManager.AllBalls != null)
                {
                    for (int i = 0; i < ballManager.AllBalls.Length; i++)
                    {
                        var b = ballManager.AllBalls[i];
                        if (b == null) continue;
                        b.SetKinematic(true);
                        if (b.SphereCollider != null) b.SphereCollider.enabled = false;
                        b.enabled = false;
                    }
                }
                ballManager.enabled = false;
            }

            PlaceLobbyDisplayCueStick();
            yield return null;
            PlaceLobbyDisplayCueStick();
        }

        private void PlaceLobbyDisplayCueStick()
        {
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
                cueController.enabled = false;
            }
        }

        private void ApplyGlobalPhysicsSettings(bool isLobby = false)
        {
            UnityEngine.Physics.bounceThreshold = 0.05f;
            UnityEngine.Physics.defaultSolverIterations = isLobby ? 6 : 12;
            UnityEngine.Physics.defaultSolverVelocityIterations = isLobby ? 1 : 4;
            UnityEngine.Physics.defaultMaxAngularSpeed = 1000f;
            Time.fixedDeltaTime = isLobby ? 0.02f : 0.01f;
        }

        private static bool IsInsideWallCueRackOrWrapper(Transform t)
        {
            Transform cur = t != null ? t.parent : null;
            while (cur != null)
            {
                string pName = cur.name;
                if (!string.IsNullOrEmpty(pName) &&
                    (pName.IndexOf("Cue Rack", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     pName.IndexOf("Cue Sticks", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     pName.IndexOf("CueStickWrapper", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
                cur = cur.parent;
            }
            return false;
        }

        private void RemoveRedundantCueSticks()
        {
            var allTransforms = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t == null || t.gameObject == null) continue;
                string n = t.gameObject.name;
                if (n != "Cue Stick (1)" && n != "Cue Stick (2)") continue;
                if (IsInsideWallCueRackOrWrapper(t)) continue;

                var ctrl = t.gameObject.GetComponent<CueController>();
                if (ctrl != null) DestroyImmediate(ctrl);
                t.gameObject.SetActive(false);
                Destroy(t.gameObject);
            }
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

            int[] initialRack = null;
            int initialBreaker = 0;
            int spinSeed = UnityEngine.Random.Range(1000, 999999);
            var session = GameSession.GetOrCreateInstance();
            bool isOnline = session != null && session.CurrentGameMode == GameMode.OnlinePvP;
            bool isPractice = session != null && session.CurrentGameMode == GameMode.LocalPractice;
            FirstBreakMethod breakMethod = session != null ? session.CurrentSettings.firstBreakMethod : FirstBreakMethod.RandomWheel;
            bool rackIntroEnabled = GameSession.RackIntroEnabled;

            if (Photon.Pun.PhotonNetwork.InRoom && Photon.Pun.PhotonNetwork.CurrentRoom != null && Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties != null)
            {
                var roomProps = Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties;
                if (roomProps.TryGetValue(Network.NetworkConstants.PROP_RACK_BALLS, out object rbObj) && rbObj is int[] rackArr && rackArr.Length == 15)
                {
                    initialRack = rackArr;
                }
                if (roomProps.TryGetValue(Network.NetworkConstants.PROP_FIRST_BREAKER, out object fbObj))
                {
                    initialBreaker = System.Convert.ToInt32(fbObj) == 1 ? 1 : 0;
                }
                if (roomProps.TryGetValue(Network.NetworkConstants.PROP_SPIN_SEED, out object ssObj))
                {
                    spinSeed = System.Convert.ToInt32(ssObj);
                }
                if (roomProps.TryGetValue(Network.NetworkConstants.PROP_BREAK_METHOD, out object bmObj))
                {
                    breakMethod = (FirstBreakMethod)System.Convert.ToInt32(bmObj);
                }
                if (roomProps.TryGetValue(Network.NetworkConstants.PROP_RACK_INTRO, out object riObj))
                {
                    rackIntroEnabled = System.Convert.ToInt32(riObj) != 0;
                }
            }
            else if (!isPractice)
            {
                initialBreaker = UnityEngine.Random.Range(0, 2);
            }

            // Chờ 1 frame để các script khác (CameraDirector, ...) hoàn tất Start()
            yield return null;

            // Online: chờ màn hình tải (LoadingScreen) đồng bộ xong cả 2 máy trước khi bắt đầu Đua Bi / Vòng Quay
            while (UI.LoadingScreen.IsActive)
            {
                yield return null;
            }

            // Đọc lại Room CustomProperties sau khi LoadingScreen kết thúc để đảm bảo gói SetCustomProperties
            // từ lúc bắt đầu trận đã cập nhật đầy đủ 100% trên cả Master và Client.
            if (Photon.Pun.PhotonNetwork.InRoom && Photon.Pun.PhotonNetwork.CurrentRoom != null && Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties != null)
            {
                var roomProps = Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties;
                if (roomProps.TryGetValue(Network.NetworkConstants.PROP_RACK_BALLS, out object rbObj) && rbObj is int[] rackArr && rackArr.Length == 15)
                {
                    initialRack = rackArr;
                }
                if (roomProps.TryGetValue(Network.NetworkConstants.PROP_FIRST_BREAKER, out object fbObj))
                {
                    initialBreaker = System.Convert.ToInt32(fbObj) == 1 ? 1 : 0;
                }
                if (roomProps.TryGetValue(Network.NetworkConstants.PROP_SPIN_SEED, out object ssObj))
                {
                    spinSeed = System.Convert.ToInt32(ssObj);
                }
                if (roomProps.TryGetValue(Network.NetworkConstants.PROP_BREAK_METHOD, out object bmObj))
                {
                    breakMethod = (FirstBreakMethod)System.Convert.ToInt32(bmObj);
                }
                if (roomProps.TryGetValue(Network.NetworkConstants.PROP_RACK_INTRO, out object riObj))
                {
                    rackIntroEnabled = System.Convert.ToInt32(riObj) != 0;
                }
            }

            // Trước khi xếp bi ở ván 1: chọn người phá bi đầu theo chế độ cấu hình (Đua Bi Băng Ngắn hoặc Vòng Quay Random)
            if (!isPractice)
            {
                int localPlayerIdx = isOnline
                    ? (Photon.Pun.PhotonNetwork.InRoom ? (Photon.Pun.PhotonNetwork.IsMasterClient ? 0 : 1) : (session.IsOnlineMaster ? 0 : 1))
                    : 0;
                string pAName = session != null ? session.PlayerAName : "Player 1";
                string pBName = session != null ? session.PlayerBName : "AI";

                if (isOnline && breakMethod == FirstBreakMethod.LagShot && ballManager != null && tableWrapper != null)
                {
                    // Khởi tạo trước dữ liệu người chơi và trượt HUD (TopHUDBar + PowerBarUI) vào màn hình ngay khi bắt đầu Đua Bi
                    if (matchStateManager != null)
                    {
                        matchStateManager.Initialize(
                            session != null ? session.CurrentSettings : MatchSettings.Default,
                            pAName,
                            pBName,
                            session != null && session.IsPlayerBAi,
                            0
                        );
                    }
                    if (gameplayHUD != null)
                    {
                        gameplayHUD.PrepareForPreMatchLag(matchStateManager, inputController, ballManager);
                    }
                    if (hudEntrance != null)
                    {
                        hudEntrance.Play();
                        hudEntrance = null;
                    }

                    bool lagDone = false;
                    Match.LagForBreakController.PlayLagShot(
                        this,
                        ballManager,
                        tableWrapper,
                        cameraDirector,
                        cueController,
                        movementMonitor,
                        gameplayHUD,
                        localPlayerIdx,
                        pAName,
                        pBName,
                        spinSeed,
                        winnerIdx =>
                        {
                            initialBreaker = winnerIdx;
                            lagDone = true;
                        });
                    while (!lagDone) yield return null;
                }
                else
                {
                    // Ẩn tạm các bi trên mặt bàn trong lúc quay vòng tròn trước khi xếp khuôn bi
                    if (ballManager != null)
                    {
                        ballManager.InitializeRegistry();
                        if (ballManager.AllBalls != null)
                        {
                            foreach (var b in ballManager.AllBalls)
                            {
                                if (b != null) b.gameObject.SetActive(false);
                            }
                        }
                    }
                    if (cueController != null) cueController.SetAimingState(false);

                    bool wheelDone = false;
                    UI.BreakTossWheelUI.PlayWheel(this, initialBreaker, localPlayerIdx, pAName, pBName, spinSeed, () => wheelDone = true);
                    while (!wheelDone) yield return null;
                }
            }

            if (autoResetRackOnStart && ballManager != null)
            {
                if (isOnline)
                {
                    if (Photon.Pun.PhotonNetwork.InRoom && Photon.Pun.PhotonNetwork.CurrentRoom != null && Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties != null)
                    {
                        var roomProps = Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties;
                        if (roomProps.TryGetValue(Network.NetworkConstants.PROP_SPIN_SEED, out object ssLatest))
                        {
                            spinSeed = System.Convert.ToInt32(ssLatest);
                        }
                        if (roomProps.TryGetValue(Network.NetworkConstants.PROP_RACK_BALLS, out object rbLatest) && rbLatest is int[] latestArr && latestArr.Length == 15)
                        {
                            initialRack = latestArr;
                        }
                    }

                    if (initialRack == null || initialRack.Length != 15)
                    {
                        initialRack = BallManager.GenerateWpaRackBallNumbersStatic(spinSeed + 9973);
                    }
                }
                ballManager.ResetRack(initialRack);
            }

            bool introDone = false;
            var intro = FindObjectOfType<RackIntroSequence>();
            if (intro == null && ballManager != null && rackIntroEnabled)
            {
                intro = gameObject.AddComponent<RackIntroSequence>();
            }

            if (intro != null && rackIntroEnabled)
            {
                intro.Play(() => introDone = true);
                while (!introDone) yield return null;
            }

            if (hudEntrance != null) hudEntrance.Play();

            SetupWiring(initialBreaker);
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

        public void SetupWiring(int initialBreakerIndex = 0)
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
                        cueController.UpdateAim(cameraDirector.AimDirection, 0.85f);
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
                        session.IsPlayerBAi,
                        initialBreakerIndex
                    );
                }
                else
                {
                    matchStateManager.Initialize(MatchSettings.Default, initialBreakerIndex: initialBreakerIndex);
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

                if (gameplayHUD != null)
                {
                    UI.OnlineChatUI.Ensure(gameplayHUD);
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
