using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Game.Billiards.Camera;
using Game.Billiards.Core;
using Game.Billiards.Network;
using Game.Billiards.Physics;
using Game.Billiards.UI;

namespace Game.Billiards.Match
{
    /// <summary>
    /// Bộ điều khiển pha Đua Bi Băng Ngắn (Tournament Lag for Break) cho chế độ Online PvP.
    /// - Hiển thị Thanh Lực (PowerBarUI) chạy lên xuống liên tục với tốc độ nhanh vừa phải.
    /// - Người chơi Click Chuột Trái hoặc nhấn Space để khóa lực ngay tại thời điểm bấm.
    /// - Sau khi cả 2 đã khóa lực (hoặc hết thời gian đếm ngược), cả 2 cây cơ tự động lùi về phía sau
    ///   theo đúng mức lực đã khóa rồi đồng loạt đâm vào 2 viên bi cái.
    /// - Góc máy quay đặt xuyên chéo từ trên cao phía sau đầu bàn (không chúc thẳng đứng).
    /// - Hỗ trợ xử lý Hòa (cùng khoảng cách hoặc cùng phạm lỗi) -> Tự động đua lại lượt mới.
    /// </summary>
    public class LagForBreakController : MonoBehaviour, IOnEventCallback
    {
        private const float PrepDurationSeconds = 20.0f;
        private const float MaxAimAngleDeg = 6.5f;
        private const float LaneOffsetZ = 0.22f;
        private const float BaseCueDistance = 0.045f;
        private const float MaxCuePullback = 0.30f;
        private const int MaxLagRounds = 3;

        // Góc máy quay xuyên chéo từ trên cao phía sau đầu bàn (+X nhìn về -X, độ dốc ~13.5° - 17°)
        private static readonly Vector3 AimCamPos = new Vector3(2.06f, 1.32f, 0f);
        private static readonly Vector3 AimCamLookAt = new Vector3(-0.25f, 0.82f, 0f);
        private static readonly Vector3 FollowCamPos = new Vector3(1.92f, 1.36f, 0f);
        private static readonly Vector3 FollowCamLookAt = new Vector3(0.28f, 0.83f, 0f);

        private BallManager ballManager;
        private TableWrapper tableWrapper;
        private CameraDirector cameraDirector;
        private CueController cueController;
        private BallMovementMonitor movementMonitor;
        private GameplayHUD gameplayHUD;

        private BallController ballA; // Chủ phòng (Player 0) - Làn trái (Z < 0)
        private BallController ballB; // Khách (Player 1) - Làn phải (Z > 0)
        private Material savedBallBMaterial;
        private MeshRenderer ballBRenderer;

        private Transform cueStickA;
        private Transform cueStickB;
        private GameObject clonedCueStickGo;

        private GameObject visualsRoot;
        private LineRenderer aimLineLocal;
        private LineRenderer ringLineA;
        private LineRenderer ringLineB;
        private LineRenderer rulerLineA;
        private LineRenderer rulerLineB;

        // UI Overlay tối giản chỉ dùng để gắn 2 nhãn đo khoảng cách cm bám theo 2 viên bi khi dừng
        private GameObject uiRoot;
        private RectTransform uiRootRt;
        private Canvas parentCanvas;
        private RectTransform badgeARt;
        private RectTransform badgeBRt;
        private Text resultTextA;
        private Text resultTextB;

        private int localPlayerIndex;
        private string playerAName;
        private string playerBName;
        private int deterministicSeed;

        // Trạng thái ngắm & khóa thanh lực của 2 bên
        private float localAimAngleDeg = 0f;
        private float oscillatingPower01 = 0.50f;
        private float remoteAimAngleDeg = 0f;
        private float lastAimSyncSentTime = 0f;

        private bool lockedA = false;
        private float lockedAngleA = 0f;
        private float lockedPowerA = 0.56f;

        private bool lockedB = false;
        private float lockedAngleB = 0f;
        private float lockedPowerB = 0.56f;

        private int currentRound = 1;
        private bool requireMouseReleaseBeforeClick = false;
        private float inputGraceTimer = 0f;
        private float lastLockBroadcastTime = 0f;

        // Bộ đệm gói chốt lực từ đối thủ theo từng round (tránh mất gói khi đối thủ chốt sớm hơn vài frame)
        private readonly bool[] bufferedRemoteLockForRound = new bool[MaxLagRounds + 2];
        private readonly float[] bufferedRemoteAngleForRound = new float[MaxLagRounds + 2];
        private readonly float[] bufferedRemotePowerForRound = new float[MaxLagRounds + 2];

        // Đồng bộ kết quả chấm điểm Đua Băng authoritative từ Master sang Client (EVENT_LAG_RESULT)
        private int syncedResultRound = -1;
        private float syncedDistCmA = 0f;
        private float syncedDistCmB = 0f;
        private bool syncedTouchedFarA = false;
        private bool syncedCrossedLaneA = false;
        private bool syncedTouchedFarB = false;
        private bool syncedCrossedLaneB = false;
        private int syncedWinnerIndex = 0;
        private bool syncedIsRetryRound = false;

        // Cờ theo dõi chạm băng xa & lấn làn trong cả FixedUpdate lẫn Update để không bao giờ bỏ sót va chạm vật lý
        private bool simTrackingActive = false;
        private float simFarRailThresholdX = 0f;
        private bool simTouchedFarRailA = false;
        private bool simTouchedFarRailB = false;
        private bool simCrossedLaneA = false;
        private bool simCrossedLaneB = false;

        private bool isRunning = false;

        /// <summary>True khi pha Đua Bi Băng Ngắn đang diễn ra (chặn các bộ điều khiển khác tự ý tắt cây cơ hoặc đổi camera).</summary>
        public static bool IsRunning { get; private set; }

        /// <summary>Hủy ngay pha Đua Bi nếu đang diễn ra (ví dụ đối thủ rời phòng hoặc mất kết nối giữa chừng).</summary>
        public static void AbortIfRunning()
        {
            IsRunning = false;
            var existing = FindObjectOfType<LagForBreakController>(true);
            if (existing != null)
            {
                existing.StopAllCoroutines();
                existing.CleanupLagState();
                HudModalState.Release("lag_shot");
                existing.isRunning = false;
                Destroy(existing.gameObject);
            }
        }

        /// <summary>
        /// Khởi chạy pha Đua Bi Băng Ngắn (Lag Shot) và trả về chỉ số người thắng quyền phá bi đầu (0 hoặc 1) qua onComplete.
        /// </summary>
        public static Coroutine PlayLagShot(
            MonoBehaviour host,
            BallManager ballMgr,
            TableWrapper tableWrap,
            CameraDirector camDir,
            CueController cueCtrl,
            BallMovementMonitor monitor,
            GameplayHUD hud,
            int localPlayerIdx,
            string pAName,
            string pBName,
            int seed,
            Action<int> onComplete)
        {
            if (ballMgr == null || tableWrap == null)
            {
                onComplete?.Invoke(0);
                return null;
            }

            var existing = FindObjectOfType<LagForBreakController>(true);
            if (existing != null)
            {
                existing.StopAllCoroutines();
                existing.CleanupLagState();
                Destroy(existing.gameObject);
            }

            if (cueCtrl == null)
            {
                var inputCtrl = FindObjectOfType<Input.PlayerInputController>(true);
                if (inputCtrl != null && inputCtrl.CueController != null)
                {
                    cueCtrl = inputCtrl.CueController;
                }
                else
                {
                    cueCtrl = FindObjectOfType<CueController>(true);
                }
            }
            if (camDir == null) camDir = FindObjectOfType<CameraDirector>(true);
            if (monitor == null) monitor = FindObjectOfType<BallMovementMonitor>(true);
            if (hud == null) hud = FindObjectOfType<GameplayHUD>(true);

            IsRunning = true;

            var go = new GameObject("LagForBreakController");
            var ctrl = go.AddComponent<LagForBreakController>();
            ctrl.ballManager = ballMgr;
            ctrl.tableWrapper = tableWrap;
            ctrl.cameraDirector = camDir;
            ctrl.cueController = cueCtrl;
            ctrl.movementMonitor = monitor;
            ctrl.gameplayHUD = hud;
            ctrl.localPlayerIndex = localPlayerIdx;
            ctrl.playerAName = string.IsNullOrWhiteSpace(pAName) ? "Player 1" : pAName;
            ctrl.playerBName = string.IsNullOrWhiteSpace(pBName) ? "Player 2" : pBName;
            ctrl.deterministicSeed = seed;

            return ctrl.StartCoroutine(ctrl.LagRoutine(onComplete));
        }

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (!isRunning) return;

            byte code = photonEvent.Code;
            if (code == NetworkConstants.EVENT_LAG_AIM_SYNC)
            {
                if (photonEvent.CustomData is float[] data && data.Length >= 2)
                {
                    int senderIdx = Mathf.RoundToInt(data[0]);
                    if (senderIdx != localPlayerIndex)
                    {
                        remoteAimAngleDeg = Mathf.Clamp(data[1], -MaxAimAngleDeg, MaxAimAngleDeg);
                    }
                }
            }
            else if (code == NetworkConstants.EVENT_LAG_SHOT_LOCK)
            {
                if (photonEvent.CustomData is float[] data && data.Length >= 3)
                {
                    int senderIdx = Mathf.RoundToInt(data[0]);
                    float angle = Mathf.Clamp(data[1], -MaxAimAngleDeg, MaxAimAngleDeg);
                    float power = Mathf.Clamp(data[2], 0.10f, 1.0f);
                    int pktRound = data.Length >= 4 ? Mathf.Clamp(Mathf.RoundToInt(data[3]), 1, MaxLagRounds) : currentRound;

                    if (senderIdx != localPlayerIndex && pktRound >= 1 && pktRound < bufferedRemoteLockForRound.Length)
                    {
                        bufferedRemoteLockForRound[pktRound] = true;
                        bufferedRemoteAngleForRound[pktRound] = angle;
                        bufferedRemotePowerForRound[pktRound] = power;
                    }

                    if (pktRound == currentRound)
                    {
                        ApplyShotLock(senderIdx, angle, power);
                    }
                }
            }
            else if (code == NetworkConstants.EVENT_LAG_RESULT)
            {
                if (photonEvent.CustomData is object[] res && res.Length >= 9)
                {
                    syncedResultRound = Convert.ToInt32(res[0]);
                    syncedDistCmA = Convert.ToSingle(res[1]);
                    syncedDistCmB = Convert.ToSingle(res[2]);
                    syncedTouchedFarA = Convert.ToBoolean(res[3]);
                    syncedCrossedLaneA = Convert.ToBoolean(res[4]);
                    syncedTouchedFarB = Convert.ToBoolean(res[5]);
                    syncedCrossedLaneB = Convert.ToBoolean(res[6]);
                    syncedWinnerIndex = Convert.ToInt32(res[7]) == 1 ? 1 : 0;
                    syncedIsRetryRound = Convert.ToBoolean(res[8]);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!isRunning || !simTrackingActive) return;
            SampleBallSimulationFlags();
        }

        private void SampleBallSimulationFlags()
        {
            if (ballA != null && !ballA.IsPocketed)
            {
                Vector3 pA = ballA.Rigidbody != null ? ballA.Rigidbody.position : ballA.transform.position;
                if (pA.x <= simFarRailThresholdX) simTouchedFarRailA = true;
                if (pA.z > 0.045f) simCrossedLaneA = true;
            }

            if (ballB != null && !ballB.IsPocketed)
            {
                Vector3 pB = ballB.Rigidbody != null ? ballB.Rigidbody.position : ballB.transform.position;
                if (pB.x <= simFarRailThresholdX) simTouchedFarRailB = true;
                if (pB.z < -0.045f) simCrossedLaneB = true;
            }
        }

        private void BroadcastLocalLockPacket()
        {
            if (!PhotonNetwork.InRoom) return;
            float myAngle = (localPlayerIndex == 0) ? lockedAngleA : lockedAngleB;
            float myPower = (localPlayerIndex == 0) ? lockedPowerA : lockedPowerB;
            float[] payload = new float[] { localPlayerIndex, myAngle, myPower, currentRound };
            RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_LAG_SHOT_LOCK, payload, opts, SendOptions.SendReliable);
            lastLockBroadcastTime = Time.unscaledTime;
        }

        private void BroadcastAuthoritativeLagResult(
            int roundIdx,
            float distCmA,
            float distCmB,
            bool touchedFarA,
            bool crossedA,
            bool touchedFarB,
            bool crossedB,
            int winIdx,
            bool isRetry)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
            object[] payload = new object[]
            {
                roundIdx,
                distCmA,
                distCmB,
                touchedFarA,
                crossedA,
                touchedFarB,
                crossedB,
                winIdx,
                isRetry
            };
            RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_LAG_RESULT, payload, opts, SendOptions.SendReliable);
        }

        private void ApplyShotLock(int playerIdx, float angleDeg, float power01)
        {
            if (playerIdx == 0)
            {
                lockedA = true;
                lockedAngleA = angleDeg;
                lockedPowerA = power01;
            }
            else
            {
                lockedB = true;
                lockedAngleB = angleDeg;
                lockedPowerB = power01;
            }
        }

        private IEnumerator LagRoutine(Action<int> onComplete)
        {
            isRunning = true;
            IsRunning = true;
            HudModalState.ForceOpen("lag_shot");

            SetupTableAndBalls();
            Setup3DVisuals();
            BuildMeasurementBadgesUI();

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null && cameraDirector != null)
            {
                cam = cameraDirector.GetComponent<UnityEngine.Camera>() ?? cameraDirector.MainCamera;
            }
            if (cameraDirector != null)
            {
                cameraDirector.SwitchToCinematicCamera();
            }

            TurnSlideBannerUI slideBanner = gameplayHUD != null ? TurnSlideBannerUI.Ensure(gameplayHUD) : null;
            int winnerIndex = 0;

            for (int round = 1; round <= MaxLagRounds; round++)
            {
                currentRound = round;
                // 1. Reset trạng thái bàn, bi cái, cây cơ và cờ khóa lực cho lượt đua hiện tại
                lockedA = false;
                lockedB = false;
                localAimAngleDeg = 0f;
                remoteAimAngleDeg = 0f;
                oscillatingPower01 = 0.25f;
                requireMouseReleaseBeforeClick = UnityEngine.Input.GetMouseButton(0);
                inputGraceTimer = 0.28f;

                // Nếu gói tin chốt lực của đối thủ cho round này đã tới sớm vài frame, áp dụng ngay
                if (round < bufferedRemoteLockForRound.Length && bufferedRemoteLockForRound[round])
                {
                    int oppIdx = (localPlayerIndex == 0) ? 1 : 0;
                    ApplyShotLock(oppIdx, bufferedRemoteAngleForRound[round], bufferedRemotePowerForRound[round]);
                }

                ResetBallsToHeadString();
                if (cueController != null) cueController.SetAimingState(true);
                if (clonedCueStickGo != null) clonedCueStickGo.SetActive(true);
                if (ringLineA != null) ringLineA.gameObject.SetActive(true);
                if (ringLineB != null) ringLineB.gameObject.SetActive(true);
                if (rulerLineA != null) rulerLineA.gameObject.SetActive(false);
                if (rulerLineB != null) rulerLineB.gameObject.SetActive(false);
                if (badgeARt != null) badgeARt.gameObject.SetActive(true);
                if (badgeBRt != null) badgeBRt.gameObject.SetActive(true);

                // Đặt Camera góc xuyên chéo từ trên cao phía sau đầu bàn (không chúc thẳng đứng)
                Quaternion aimRot = Quaternion.LookRotation(AimCamLookAt - AimCamPos, Vector3.up);
                if (cam != null)
                {
                    cam.transform.position = AimCamPos;
                    cam.transform.rotation = aimRot;
                }

                // Hiển thị ngay nhãn định danh trên 2 cây cơ ("▼ CƠ CỦA BẠN" và "▼ CƠ ĐỐI THỦ")
                UpdatePreStrikeCueSticksAndAimLine();
                UpdateCueOwnerBadgesForAim(cam);

                // 2. Hiển thị Thanh Lực (PowerBarUI) và Banner hướng dẫn góc trên bên trái
                AimOnlyHudVisibility.ForceShowPowerBar = true;
                if (gameplayHUD != null && gameplayHUD.PowerBar != null)
                {
                    gameplayHUD.PowerBar.gameObject.SetActive(true);
                    gameplayHUD.PowerBar.UpdatePower(oscillatingPower01);
                }

                if (slideBanner != null)
                {
                    slideBanner.ShowCustomPrompt(
                        LocalizationManager.Get("lag_title"),
                        LocalizationManager.Get("lag_instruction"),
                        amberBlink: true);
                }

                // Chờ 2 frame để tránh cú click nút "Chơi lại" ở màn hình trước bị ăn xuyên sang frame đầu của Đua Bi
                yield return null;
                yield return null;
                if (UnityEngine.Input.GetMouseButton(0)) requireMouseReleaseBeforeClick = true;

                // 3. Vòng lặp chạy Thanh Lực lên xuống (Ping-Pong Timing Meter) & chờ 2 bên Click khóa lực
                float remainingTime = PrepDurationSeconds;
                float oscClock = 0.18f; // Bắt đầu từ ~25% lực đi lên

                while (!(lockedA && lockedB) && remainingTime > 0f)
                {
                    if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount < 2)
                    {
                        CleanupLagState();
                        HudModalState.Release("lag_shot");
                        isRunning = false;
                        onComplete?.Invoke(localPlayerIndex);
                        Destroy(gameObject);
                        yield break;
                    }

                    float dt = Time.unscaledDeltaTime;
                    remainingTime -= dt;
                    oscClock += dt;
                    if (inputGraceTimer > 0f) inputGraceTimer -= dt;
                    if (requireMouseReleaseBeforeClick && !UnityEngine.Input.GetMouseButton(0))
                    {
                        requireMouseReleaseBeforeClick = false;
                    }

                    // Cập nhật viền đồng hồ đếm ngược trên 2 thẻ tên người chơi (PlayerLeftPanel & PlayerRightPanel)
                    UpdateHudPlayerTimers(Mathf.Max(0f, remainingTime), PrepDurationSeconds);

                    bool amILocked = (localPlayerIndex == 0) ? lockedA : lockedB;
                    if (!amILocked)
                    {
                        // Thanh lực chạy lên xuống nhanh vừa phải giữa 10% và 100% (~1.2s một chu kỳ)
                        float pingPong = Mathf.PingPong(oscClock * 1.65f, 1.0f);
                        oscillatingPower01 = Mathf.Lerp(0.10f, 1.00f, pingPong);

                        if (gameplayHUD != null && gameplayHUD.PowerBar != null)
                        {
                            gameplayHUD.PowerBar.UpdatePower(oscillatingPower01);
                        }

                        HandleLocalLagInput(dt, slideBanner);
                    }
                    else if (PhotonNetwork.InRoom && Time.unscaledTime - lastLockBroadcastTime >= 0.25f)
                    {
                        // Định kỳ gửi nhắc gói chốt lực cho tới khi đối thủ cũng đã nhận và chốt xong
                        BroadcastLocalLockPacket();
                    }

                    // Giữ 2 cây cơ đặt ngay sau bi cái & cập nhật nhãn phân biệt cơ trong lúc chờ khóa lực / hết thời gian
                    UpdatePreStrikeCueSticksAndAimLine();
                    UpdateCueOwnerBadgesForAim(cam);
                    yield return null;
                }

                // Nếu hết thời gian mà người chơi cục bộ chưa click chuột, tự động khóa tại mức lực hiện tại của thanh lực
                bool localDone = (localPlayerIndex == 0) ? lockedA : lockedB;
                if (!localDone)
                {
                    LockLocalShot(localAimAngleDeg, oscillatingPower01, slideBanner);
                }

                // Chờ tối đa 4.0s cho gói tin chốt của đối thủ nếu mạng trễ hoặc 2 máy lệch đồng hồ vài giây
                float netWait = 0f;
                while (!(lockedA && lockedB) && netWait < 4.0f)
                {
                    netWait += Time.unscaledDeltaTime;
                    if (PhotonNetwork.InRoom && Time.unscaledTime - lastLockBroadcastTime >= 0.25f)
                    {
                        BroadcastLocalLockPacket();
                    }
                    UpdatePreStrikeCueSticksAndAimLine();
                    UpdateCueOwnerBadgesForAim(cam);
                    yield return null;
                }
                if (!lockedA) ApplyShotLock(0, localPlayerIndex == 0 ? localAimAngleDeg : remoteAimAngleDeg, 0.58f);
                if (!lockedB) ApplyShotLock(1, localPlayerIndex == 1 ? localAimAngleDeg : remoteAimAngleDeg, 0.58f);

                // Xóa đồng hồ trên thẻ HUD và ẩn tia ngắm + vòng định vị + banner hướng dẫn
                ClearHudPlayerTimers();
                if (aimLineLocal != null) aimLineLocal.gameObject.SetActive(false);
                if (ringLineA != null) ringLineA.gameObject.SetActive(false);
                if (ringLineB != null) ringLineB.gameObject.SetActive(false);
                if (slideBanner != null) slideBanner.HideBreakPlacementPrompt();

                // 4. Sau khi khóa lực / hết thời gian: 2 cây cơ tự động lùi về phía sau theo mức lực đã khóa rồi đồng loạt đánh vào bi!
                yield return StartCoroutine(SimultaneousPullbackAndStrikeRoutine(round, cam));

                // Ẩn thanh lực sau khi 2 cây cơ đã đánh vào bi
                AimOnlyHudVisibility.ForceShowPowerBar = false;

                // 5. Theo dõi 2 bi chạy tới băng ngắn xa (-X) và dội ngược về băng ngắn gần (+X)
                float minX = TableWrapper.FootCushionX;
                float maxX = TableWrapper.HeadCushionX;
                float ballRadius = (ballManager.PhysicsConfig != null) ? ballManager.PhysicsConfig.BallRadius : 0.02855f;
                simFarRailThresholdX = minX + ballRadius + 0.055f;
                simTouchedFarRailA = false;
                simTouchedFarRailB = false;
                simCrossedLaneA = false;
                simCrossedLaneB = false;
                simTrackingActive = true;

                Quaternion followRot = Quaternion.LookRotation(FollowCamLookAt - FollowCamPos, Vector3.up);
                float simElapsed = 0f;
                float settledTimer = 0f;
                const float maxSimDuration = 9.0f;

                while (simElapsed < maxSimDuration)
                {
                    float dt = Time.deltaTime;
                    simElapsed += dt;

                    // Khi 2 bi đã dội băng xa quay về nửa đầu bàn, lướt nhẹ góc máy xuyên chéo gần hơn để quan sát vạch đích
                    if (cam != null && simElapsed > 1.1f)
                    {
                        cam.transform.position = Vector3.Lerp(cam.transform.position, FollowCamPos, dt * 2.6f);
                        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, followRot, dt * 2.6f);
                    }

                    SampleBallSimulationFlags();
                    UpdateBadgeScreenPositions(cam);

                    bool aStopped = ballA == null || ballA.IsPocketed || (ballA.Rigidbody != null && ballA.Rigidbody.velocity.sqrMagnitude < 0.0001f);
                    bool bStopped = ballB == null || ballB.IsPocketed || (ballB.Rigidbody != null && ballB.Rigidbody.velocity.sqrMagnitude < 0.0001f);

                    if (simElapsed > 0.35f && aStopped && bStopped)
                    {
                        settledTimer += dt;
                        if (settledTimer >= 0.25f) break;
                    }
                    else
                    {
                        settledTimer = 0f;
                    }

                    yield return null;
                }

                simTrackingActive = false;
                StopBallsCompletely();

                // 6. Chấm điểm khoảng cách từ mỗi bi tới mép băng ngắn gần (Head Rail: X = maxX - ballRadius)
                float headCushionContactX = maxX - ballRadius;
                bool touchedFarRailA = simTouchedFarRailA;
                bool touchedFarRailB = simTouchedFarRailB;
                bool crossedLaneA = simCrossedLaneA;
                bool crossedLaneB = simCrossedLaneB;

                float distCmA = EvaluateBallLagDistanceCm(ballA, touchedFarRailA, crossedLaneA, headCushionContactX, out string statusA);
                float distCmB = EvaluateBallLagDistanceCm(ballB, touchedFarRailB, crossedLaneB, headCushionContactX, out string statusB);

                bool bothFouled = (distCmA >= 9000f && distCmB >= 9000f);
                bool isTiedDistance = Mathf.Abs(distCmA - distCmB) < 0.1f;
                bool isRetryRound = (bothFouled || isTiedDistance) && round < MaxLagRounds;

                if (Mathf.Abs(distCmA - distCmB) < 0.05f)
                {
                    winnerIndex = (deterministicSeed % 2 == 0) ? 0 : 1;
                }
                else
                {
                    winnerIndex = (distCmA < distCmB) ? 0 : 1;
                }

                // Đồng bộ kết quả chấm điểm authoritative từ Master sang Client để 2 máy luôn thống nhất 100% người thắng!
                if (PhotonNetwork.InRoom)
                {
                    if (PhotonNetwork.IsMasterClient)
                    {
                        BroadcastAuthoritativeLagResult(
                            round, distCmA, distCmB,
                            touchedFarRailA, crossedLaneA,
                            touchedFarRailB, crossedLaneB,
                            winnerIndex, isRetryRound);
                    }
                    else
                    {
                        float resultWait = 0f;
                        while (syncedResultRound != round && resultWait < 3.5f)
                        {
                            resultWait += Time.unscaledDeltaTime;
                            UpdateBadgeScreenPositions(cam);
                            yield return null;
                        }
                        if (syncedResultRound == round)
                        {
                            distCmA = syncedDistCmA;
                            distCmB = syncedDistCmB;
                            touchedFarRailA = syncedTouchedFarA;
                            crossedLaneA = syncedCrossedLaneA;
                            touchedFarRailB = syncedTouchedFarB;
                            crossedLaneB = syncedCrossedLaneB;
                            winnerIndex = syncedWinnerIndex;
                            isRetryRound = syncedIsRetryRound;
                            statusA = FormatLagStatusFromDistance(distCmA, touchedFarRailA, crossedLaneA);
                            statusB = FormatLagStatusFromDistance(distCmB, touchedFarRailB, crossedLaneB);
                        }
                    }
                }

                // Xử lý khi cả 2 bằng khoảng cách hoặc cùng phạm lỗi: thông báo Hòa và tự động đua lại lượt mới!
                if (isRetryRound)
                {
                    ShowRulersAndDistanceBadges(headCushionContactX, distCmA, statusA, distCmB, statusB);

                    string myStatus = (localPlayerIndex == 0) ? statusA : statusB;
                    string oppStatus = (localPlayerIndex == 0) ? statusB : statusA;
                    FoulPopupUI.ShowLagResult(
                        LocalizationManager.Get("lag_tie_badge", "HÒA ĐUA BI"),
                        LocalizationManager.Get("lag_tie_title", "HÒA KHOẢNG CÁCH — ĐUA LẠI LƯỢT MỚI!"),
                        LocalizationManager.Format("lag_tie_sub", myStatus, oppStatus),
                        styleMode: 2,
                        holdSeconds: 2.2f);

                    float tieHold = 0f;
                    float lastResultResend = Time.unscaledTime;
                    while (tieHold < 2.35f)
                    {
                        tieHold += Time.unscaledDeltaTime;
                        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && Time.unscaledTime - lastResultResend >= 0.35f)
                        {
                            lastResultResend = Time.unscaledTime;
                            BroadcastAuthoritativeLagResult(
                                round, distCmA, distCmB,
                                touchedFarRailA, crossedLaneA,
                                touchedFarRailB, crossedLaneB,
                                winnerIndex, true);
                        }
                        UpdateBadgeScreenPositions(cam);
                        yield return null;
                    }

                    deterministicSeed += 313;
                    continue;
                }

                // Đồng bộ PROP_FIRST_BREAKER trên phòng nếu là Master
                if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
                {
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
                    {
                        { NetworkConstants.PROP_FIRST_BREAKER, winnerIndex }
                    });
                }

                // 7. Hiển thị thước đo Laser 3D + Nhãn đo cm cạnh bi + Banner kết quả chuẩn HUD
                ShowRulersAndDistanceBadges(headCushionContactX, distCmA, statusA, distCmB, statusB);
                AnnounceLagWinnerBanner(statusA, statusB, winnerIndex);

                float resultHold = 0f;
                float lastWinResend = Time.unscaledTime;
                while (resultHold < 2.5f)
                {
                    resultHold += Time.unscaledDeltaTime;
                    if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && Time.unscaledTime - lastWinResend >= 0.35f)
                    {
                        lastWinResend = Time.unscaledTime;
                        BroadcastAuthoritativeLagResult(
                            round, distCmA, distCmB,
                            touchedFarRailA, crossedLaneA,
                            touchedFarRailB, crossedLaneB,
                            winnerIndex, false);
                    }
                    UpdateBadgeScreenPositions(cam);
                    yield return null;
                }

                break;
            }

            // 8. Dọn dẹp toàn bộ trạng thái tạm thời của pha Đua Băng trước khi trả về cho GameplayInitializer xếp khuôn 15 bi
            CleanupLagState();

            HudModalState.Release("lag_shot");
            isRunning = false;
            IsRunning = false;
            onComplete?.Invoke(winnerIndex);
            Destroy(gameObject);
        }

        private void HandleLocalLagInput(float dt, TurnSlideBannerUI slideBanner)
        {
            if (OnlineChatUI.IsChatOpen || OnlineChatUI.IsTyping) return;

            // 1. Chỉnh nhẹ hướng ngắm trái/phải bằng phím A/D hoặc mũi tên
            float keyH = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) keyH -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) keyH += 1f;

            if (Mathf.Abs(keyH) > 0.01f)
            {
                localAimAngleDeg = Mathf.Clamp(localAimAngleDeg + keyH * 7.5f * dt, -MaxAimAngleDeg, MaxAimAngleDeg);
            }

            // 2. Click Chuột Trái (Left Click) hoặc nhấn Space / Enter để KHÓA THANH LỰC tại vị trí hiện tại
            bool clickedShoot = false;
            if (inputGraceTimer <= 0f && !requireMouseReleaseBeforeClick)
            {
                if (UnityEngine.Input.GetMouseButtonDown(0) && IsMouseInsideGameWindow() && !IsPointerOverInteractableUI())
                {
                    clickedShoot = true;
                }
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    clickedShoot = true;
                }
            }

            if (clickedShoot)
            {
                if (Audio.AudioManager.Instance != null)
                {
                    Audio.AudioManager.Instance.PlayButtonClick();
                }
                LockLocalShot(localAimAngleDeg, oscillatingPower01, slideBanner);
                return;
            }

            // 3. Đồng bộ góc ngắm sang đối thủ
            if (PhotonNetwork.InRoom && Time.unscaledTime - lastAimSyncSentTime >= 0.065f)
            {
                lastAimSyncSentTime = Time.unscaledTime;
                float[] payload = new float[] { localPlayerIndex, localAimAngleDeg };
                RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
                PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_LAG_AIM_SYNC, payload, opts, SendOptions.SendUnreliable);
            }
        }

        private static bool IsMouseInsideGameWindow()
        {
            Vector3 mp = UnityEngine.Input.mousePosition;
            return mp.x >= 0f && mp.x <= Screen.width && mp.y >= 0f && mp.y <= Screen.height;
        }

        private static bool IsPointerOverInteractableUI()
        {
            var es = EventSystem.current;
            if (es == null) return false;

            var data = new PointerEventData(es) { position = UnityEngine.Input.mousePosition };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            es.RaycastAll(data, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                var sel = hits[i].gameObject != null ? hits[i].gameObject.GetComponentInParent<Selectable>() : null;
                if (sel != null && sel.IsInteractable()) return true;
            }
            return false;
        }

        private void LockLocalShot(float angleDeg, float power01, TurnSlideBannerUI slideBanner)
        {
            power01 = Mathf.Clamp(power01, 0.10f, 1.0f);
            ApplyShotLock(localPlayerIndex, angleDeg, power01);

            if (gameplayHUD != null && gameplayHUD.PowerBar != null)
            {
                gameplayHUD.PowerBar.UpdatePower(power01);
            }

            if (slideBanner != null)
            {
                int pct = Mathf.RoundToInt(power01 * 100f);
                slideBanner.ShowCustomPrompt(
                    LocalizationManager.Get("lag_title"),
                    LocalizationManager.Format("lag_locked_waiting", pct),
                    amberBlink: false);
            }

            BroadcastLocalLockPacket();
        }

        private void UpdateHudPlayerTimers(float remaining, float total)
        {
            if (gameplayHUD == null) return;

            // PlayerLeftPanel luôn là người chơi tại máy này, PlayerRightPanel là đối thủ
            bool localLocked = (localPlayerIndex == 0) ? lockedA : lockedB;
            bool remoteLocked = (localPlayerIndex == 0) ? lockedB : lockedA;

            if (gameplayHUD.PlayerLeftPanel != null)
            {
                gameplayHUD.PlayerLeftPanel.SetTurnActive(!localLocked);
                if (!localLocked) gameplayHUD.PlayerLeftPanel.SetTimer(remaining, total);
                else gameplayHUD.PlayerLeftPanel.ClearTimer();
            }

            if (gameplayHUD.PlayerRightPanel != null)
            {
                gameplayHUD.PlayerRightPanel.SetTurnActive(!remoteLocked);
                if (!remoteLocked) gameplayHUD.PlayerRightPanel.SetTimer(remaining, total);
                else gameplayHUD.PlayerRightPanel.ClearTimer();
            }
        }

        private void ClearHudPlayerTimers()
        {
            if (gameplayHUD == null) return;
            if (gameplayHUD.PlayerLeftPanel != null) gameplayHUD.PlayerLeftPanel.ClearTimer();
            if (gameplayHUD.PlayerRightPanel != null) gameplayHUD.PlayerRightPanel.ClearTimer();
        }

        private void SetupTableAndBalls()
        {
            ballManager.InitializeRegistry();
            var allBalls = ballManager.AllBalls;
            if (allBalls == null || allBalls.Length < 2) return;

            ballA = allBalls[0];
            ballB = allBalls[1];

            // Ẩn các bi từ số 2 đến 15
            for (int i = 2; i < allBalls.Length; i++)
            {
                if (allBalls[i] != null) allBalls[i].gameObject.SetActive(false);
            }

            // Phủ chất liệu trắng ngà của bi cái (ballA) lên ballB để cả 2 bên đều đánh bi cái trắng chuẩn giải đấu
            if (ballA != null && ballB != null)
            {
                var rendA = ballA.GetComponentInChildren<MeshRenderer>();
                ballBRenderer = ballB.GetComponentInChildren<MeshRenderer>();
                if (rendA != null && ballBRenderer != null)
                {
                    savedBallBMaterial = ballBRenderer.sharedMaterial;
                    ballBRenderer.sharedMaterial = rendA.sharedMaterial;
                }
            }

            ResetBallsToHeadString();
        }

        private void ResetBallsToHeadString()
        {
            float startX = Mathf.Max(tableWrapper.HeadStringX, 0.582f);
            float surfaceY = tableWrapper.HeadSpot.y;

            if (ballA != null)
            {
                ballA.gameObject.SetActive(true);
                ballA.ResetTo(new Vector3(startX, surfaceY, -LaneOffsetZ), Quaternion.identity);
            }
            if (ballB != null)
            {
                ballB.gameObject.SetActive(true);
                ballB.ResetTo(new Vector3(startX, surfaceY, +LaneOffsetZ), Quaternion.identity);
            }
        }

        private void StopBallsCompletely()
        {
            if (ballA != null && ballA.Rigidbody != null)
            {
                ballA.Rigidbody.velocity = Vector3.zero;
                ballA.Rigidbody.angularVelocity = Vector3.zero;
            }
            if (ballB != null && ballB.Rigidbody != null)
            {
                ballB.Rigidbody.velocity = Vector3.zero;
                ballB.Rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void Setup3DVisuals()
        {
            visualsRoot = new GameObject("LagVisualsRoot");
            visualsRoot.transform.SetParent(transform, false);

            // 1. Chuẩn bị 2 cây cơ 3D cho 2 làn (Trái = Player A, Phải = Player B)
            if (cueController != null && cueController.StickRoot != null)
            {
                cueController.SetAimingState(true);
                cueStickA = cueController.StickRoot;

                clonedCueStickGo = Instantiate(cueStickA.gameObject, visualsRoot.transform);
                clonedCueStickGo.name = "LagCueStick_PlayerB";
                var clonedCueCtrl = clonedCueStickGo.GetComponent<CueController>();
                if (clonedCueCtrl != null) Destroy(clonedCueCtrl);
                cueStickB = clonedCueStickGo.transform;
                cueStickB.gameObject.SetActive(true);
            }

            Shader lineShader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");

            // Màu đặc trưng: Xanh Cyan (#00F0FF) luôn dành cho BẠN, Vàng Cam (#FFC828) luôn dành cho ĐỐI THỦ
            Color colYou = new Color(0f, 0.94f, 1f, 0.92f);
            Color colOpp = new Color(1f, 0.78f, 0.14f, 0.92f);
            Color colA = (localPlayerIndex == 0) ? colYou : colOpp;
            Color colB = (localPlayerIndex == 1) ? colYou : colOpp;

            // 2. Tia ngắm ngắn định hướng của người chơi cục bộ
            aimLineLocal = CreateLine("LocalLagAimLine", lineShader, 0.005f,
                colYou, new Color(colYou.r, colYou.g, colYou.b, 0f));
            aimLineLocal.positionCount = 2;

            // 3. Vòng tròn phát sáng dưới chân 2 bi cái để phân biệt trực quan cơ bên nào là của Bạn / Đối thủ
            ringLineA = CreateCircleRing("CueRingA", lineShader, 0.0045f, 0.046f, colA);
            ringLineB = CreateCircleRing("CueRingB", lineShader, 0.0045f, 0.046f, colB);

            // 4. Hai thước đo Laser hiển thị khoảng cách tới băng ngắn gần khi bi dừng
            rulerLineA = CreateLine("RulerLineA", lineShader, 0.006f, colA, colA);
            rulerLineA.gameObject.SetActive(false);

            rulerLineB = CreateLine("RulerLineB", lineShader, 0.006f, colB, colB);
            rulerLineB.gameObject.SetActive(false);

            UpdatePreStrikeCueSticksAndAimLine();
        }

        private LineRenderer CreateCircleRing(string objName, Shader shader, float width, float radius, Color col)
        {
            const int segments = 32;
            var lr = CreateLine(objName, shader, width, col, col);
            lr.loop = true;
            lr.positionCount = segments;
            return lr;
        }

        private void UpdateCircleRingPositions(LineRenderer ring, Vector3 centerPos, float radius)
        {
            if (ring == null || !ring.gameObject.activeSelf) return;
            int count = ring.positionCount;
            float y = tableWrapper.ClothSurfaceY + 0.004f;
            for (int i = 0; i < count; i++)
            {
                float rad = (i / (float)count) * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(centerPos.x + Mathf.Cos(rad) * radius, y, centerPos.z + Mathf.Sin(rad) * radius));
            }
        }

        private LineRenderer CreateLine(string objName, Shader shader, float width, Color startCol, Color endCol)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(visualsRoot.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.material = new Material(shader);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = startCol;
            lr.endColor = endCol;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private static Vector3 GetAimDirectionFromAngle(float angleDeg)
        {
            Quaternion rot = Quaternion.Euler(0f, angleDeg, 0f);
            return (rot * Vector3.left).normalized;
        }

        private void UpdatePreStrikeCueSticksAndAimLine()
        {
            float angleA = (localPlayerIndex == 0) ? (lockedA ? lockedAngleA : localAimAngleDeg) : (lockedA ? lockedAngleA : remoteAimAngleDeg);
            float angleB = (localPlayerIndex == 1) ? (lockedB ? lockedAngleB : localAimAngleDeg) : (lockedB ? lockedAngleB : remoteAimAngleDeg);

            Vector3 dirA = GetAimDirectionFromAngle(angleA);
            Vector3 dirB = GetAimDirectionFromAngle(angleB);

            // Trong lúc chờ khóa lực / hết thời gian, 2 cây cơ đặt sẵn ở vị trí chuẩn bị sát sau bi cái
            if (cueStickA != null && ballA != null)
            {
                PositionCueStick(cueStickA, ballA.transform.position, dirA, BaseCueDistance);
                UpdateCircleRingPositions(ringLineA, ballA.transform.position, 0.046f);
            }
            if (cueStickB != null && ballB != null)
            {
                PositionCueStick(cueStickB, ballB.transform.position, dirB, BaseCueDistance);
                UpdateCircleRingPositions(ringLineB, ballB.transform.position, 0.046f);
            }

            BallController myBall = (localPlayerIndex == 0) ? ballA : ballB;
            Vector3 myDir = (localPlayerIndex == 0) ? dirA : dirB;
            bool amILocked = (localPlayerIndex == 0) ? lockedA : lockedB;

            if (aimLineLocal != null && myBall != null)
            {
                aimLineLocal.gameObject.SetActive(!amILocked);
                if (!amILocked)
                {
                    Vector3 start = myBall.transform.position;
                    start.y = tableWrapper.ClothSurfaceY + 0.005f;
                    aimLineLocal.SetPosition(0, start);
                    aimLineLocal.SetPosition(1, start + myDir * 0.65f);
                }
            }
        }

        private static void PositionCueStick(Transform stick, Vector3 ballPos, Vector3 aimDir, float distFromBall)
        {
            Vector3 backward = -aimDir.normalized;
            Quaternion elevationRot = Quaternion.AngleAxis(3.8f, Vector3.Cross(Vector3.up, aimDir));
            Vector3 offset = elevationRot * backward * distFromBall;
            stick.position = ballPos + offset;
            stick.rotation = Quaternion.LookRotation((ballPos - stick.position).normalized);
        }

        /// <summary>
        /// Sau khi cả 2 khóa lực (hoặc hết thời gian):
        /// 1. Hai cây cơ đồng loạt tự động lùi về phía sau theo đúng mức lực đã khóa (0.48s).
        /// 2. Dừng nhịp lấy đà ngắn ở điểm lùi cực đại (0.16s).
        /// 3. Đồng loạt đâm mạnh vào 2 viên bi cái (0.085s) và phóng bi chạy đua!
        /// </summary>
        private IEnumerator SimultaneousPullbackAndStrikeRoutine(int roundIndex, UnityEngine.Camera cam)
        {
            Vector3 dirA = GetAimDirectionFromAngle(lockedAngleA);
            Vector3 dirB = GetAimDirectionFromAngle(lockedAngleB);

            float pullDistA = BaseCueDistance + lockedPowerA * MaxCuePullback;
            float pullDistB = BaseCueDistance + lockedPowerB * MaxCuePullback;

            // Pha 1: 2 cây cơ tự động lùi về phía sau (Pullback Phase)
            const float pullbackDuration = 0.48f;
            float elapsed = 0f;
            while (elapsed < pullbackDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pullbackDuration);
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);

                if (cueStickA != null && ballA != null)
                {
                    PositionCueStick(cueStickA, ballA.transform.position, dirA, Mathf.Lerp(BaseCueDistance, pullDistA, easeOut));
                }
                if (cueStickB != null && ballB != null)
                {
                    PositionCueStick(cueStickB, ballB.transform.position, dirB, Mathf.Lerp(BaseCueDistance, pullDistB, easeOut));
                }
                UpdateBadgeScreenPositions(cam);
                yield return null;
            }

            // Pha 2: Giữ nhịp ngắm lấy đà ở điểm lùi tối đa
            yield return new WaitForSeconds(0.16f);

            // Pha 3: 2 cây cơ đồng loạt lao tới đánh vào bi
            const float strokeTime = 0.085f;
            elapsed = 0f;
            while (elapsed < strokeTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / strokeTime);
                float easeIn = t * t;

                if (cueStickA != null && ballA != null)
                {
                    PositionCueStick(cueStickA, ballA.transform.position, dirA, Mathf.Lerp(pullDistA, 0.0285f, easeIn));
                }
                if (cueStickB != null && ballB != null)
                {
                    PositionCueStick(cueStickB, ballB.transform.position, dirB, Mathf.Lerp(pullDistB, 0.0285f, easeIn));
                }
                UpdateBadgeScreenPositions(cam);
                yield return null;
            }

            // Ẩn 2 cây cơ ngay khi chạm bi
            if (cueController != null) cueController.SetAimingState(false);
            if (clonedCueStickGo != null) clonedCueStickGo.SetActive(false);

            // Cấp vận tốc đồng thời cho cả 2 bi
            int roundOffset = roundIndex * 1000;
            LaunchLagBall(ballA, dirA, lockedPowerA, deterministicSeed + roundOffset + 101);
            LaunchLagBall(ballB, dirB, lockedPowerB, deterministicSeed + roundOffset + 202);

            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayCueHit(Mathf.Max(lockedPowerA, lockedPowerB) * 0.65f, false);
            }
        }

        private void LaunchLagBall(BallController ball, Vector3 aimDir, float power01, int subSeed)
        {
            if (ball == null || ball.Rigidbody == null) return;

            System.Random rng = new System.Random(subSeed);
            float feelVariance = 1.0f + (float)(rng.NextDouble() * 0.018 - 0.009);

            // Đường cong vận tốc đua băng riêng cho pha Lag Shot (không ảnh hưởng tới lực đánh trong trận):
            // - 10%..30% (1.35 - 2.00 m/s): Quá nhẹ -> không chạm tới băng ngắn xa hoặc dội lại rất yếu ở đầu kia bàn.
            // - ~71%..74% (~3.42 m/s): Điểm ngọt hoàn hảo -> bi dội băng ngắn xa (-X) và lăn về dừng sát mép băng ngắn gần (+X).
            // - 85%..100% (3.85 - 4.35 m/s): Dư lực! Bi lao về đập mạnh vào băng ngắn gần (+X) rồi bật ngược ra xa (45 - 95 cm).
            float normPower = Mathf.InverseLerp(0.10f, 1.00f, Mathf.Clamp(power01, 0.10f, 1.00f));
            float speed = Mathf.Lerp(1.35f, 4.35f, normPower) * feelVariance;
            Vector3 velocity = aimDir.normalized * speed;

            Rigidbody rb = ball.Rigidbody;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY;
            rb.velocity = velocity;

            const float radius = 0.02855f;
            Vector3 rollAxis = Vector3.Cross(Vector3.up, aimDir.normalized);
            rb.angularVelocity = rollAxis * (speed / radius);

            ball.SyncPreStepState();
        }

        private float EvaluateBallLagDistanceCm(BallController ball, bool touchedFarRail, bool crossedLane, float headCushionContactX, out string statusLabel)
        {
            if (ball == null || ball.IsPocketed)
            {
                statusLabel = LocalizationManager.Get("lag_foul_scratch", "LỖI RƠI LỖ");
                return 9999f;
            }
            if (crossedLane)
            {
                statusLabel = LocalizationManager.Get("lag_foul_cross", "LẤN LÀN ĐỐI THỦ");
                return 9998f;
            }
            if (!touchedFarRail)
            {
                statusLabel = LocalizationManager.Get("lag_foul_short", "CHƯA CHẠM BĂNG XA");
                return 9997f;
            }

            float distMeters = Mathf.Max(0f, headCushionContactX - ball.transform.position.x);
            float distCm = distMeters * 100f;
            statusLabel = $"{distCm:0.0} cm";
            return distCm;
        }

        private static string FormatLagStatusFromDistance(float distCm, bool touchedFarRail, bool crossedLane)
        {
            if (distCm >= 9998.5f) return LocalizationManager.Get("lag_foul_scratch", "LỖI RƠI LỖ");
            if (crossedLane || distCm >= 9997.5f) return LocalizationManager.Get("lag_foul_cross", "LẤN LÀN ĐỐI THỦ");
            if (!touchedFarRail || distCm >= 9000f) return LocalizationManager.Get("lag_foul_short", "CHƯA CHẠM BĂNG XA");
            return $"{distCm:0.0} cm";
        }

        private void ShowRulersAndDistanceBadges(float headCushionContactX, float distCmA, string statusA, float distCmB, string statusB)
        {
            float lineY = tableWrapper.ClothSurfaceY + 0.006f;

            if (rulerLineA != null && ballA != null && !ballA.IsPocketed && distCmA < 9000f)
            {
                rulerLineA.gameObject.SetActive(true);
                Vector3 p = ballA.transform.position;
                rulerLineA.SetPosition(0, new Vector3(p.x, lineY, p.z));
                rulerLineA.SetPosition(1, new Vector3(headCushionContactX + 0.0285f, lineY, p.z));
            }

            if (rulerLineB != null && ballB != null && !ballB.IsPocketed && distCmB < 9000f)
            {
                rulerLineB.gameObject.SetActive(true);
                Vector3 p = ballB.transform.position;
                rulerLineB.SetPosition(0, new Vector3(p.x, lineY, p.z));
                rulerLineB.SetPosition(1, new Vector3(headCushionContactX + 0.0285f, lineY, p.z));
            }

            if (badgeARt != null && resultTextA != null)
            {
                badgeARt.gameObject.SetActive(true);
                bool aIsYou = (localPlayerIndex == 0);
                string tagA = aIsYou ? LocalizationManager.Get("wheel_you_short", "BẠN") : playerAName;
                string hexA = aIsYou ? "#00F0FF" : "#FFC828";
                resultTextA.text = $"{tagA}: <color={hexA}>{statusA}</color>";
            }

            if (badgeBRt != null && resultTextB != null)
            {
                badgeBRt.gameObject.SetActive(true);
                bool bIsYou = (localPlayerIndex == 1);
                string tagB = bIsYou ? LocalizationManager.Get("wheel_you_short", "BẠN") : playerBName;
                string hexB = bIsYou ? "#00F0FF" : "#FFC828";
                resultTextB.text = $"{tagB}: <color={hexB}>{statusB}</color>";
            }
        }

        private void UpdateCueOwnerBadgesForAim(UnityEngine.Camera cam)
        {
            if (badgeARt != null && resultTextA != null)
            {
                badgeARt.gameObject.SetActive(true);
                bool aIsYou = (localPlayerIndex == 0);
                if (aIsYou)
                {
                    if (lockedA)
                    {
                        int pct = Mathf.RoundToInt(lockedPowerA * 100f);
                        resultTextA.text = $"<color=#4CFF88>{LocalizationManager.Format("lag_you_locked", pct)}</color>";
                    }
                    else
                    {
                        resultTextA.text = $"<color=#00F0FF>{LocalizationManager.Format("lag_your_cue", playerAName)}</color>";
                    }
                }
                else
                {
                    resultTextA.text = lockedA
                        ? $"<color=#FFD95A>{LocalizationManager.Format("lag_opp_locked", playerAName)}</color>"
                        : $"<color=#FFC828>{LocalizationManager.Format("lag_opp_cue", playerAName)}</color>";
                }
            }

            if (badgeBRt != null && resultTextB != null)
            {
                badgeBRt.gameObject.SetActive(true);
                bool bIsYou = (localPlayerIndex == 1);
                if (bIsYou)
                {
                    if (lockedB)
                    {
                        int pct = Mathf.RoundToInt(lockedPowerB * 100f);
                        resultTextB.text = $"<color=#4CFF88>{LocalizationManager.Format("lag_you_locked", pct)}</color>";
                    }
                    else
                    {
                        resultTextB.text = $"<color=#00F0FF>{LocalizationManager.Format("lag_your_cue", playerBName)}</color>";
                    }
                }
                else
                {
                    resultTextB.text = lockedB
                        ? $"<color=#FFD95A>{LocalizationManager.Format("lag_opp_locked", playerBName)}</color>"
                        : $"<color=#FFC828>{LocalizationManager.Format("lag_opp_cue", playerBName)}</color>";
                }
            }

            UpdateBadgeScreenPositions(cam);
        }

        private void AnnounceLagWinnerBanner(string statusA, string statusB, int winnerIndex)
        {
            bool didLocalWin = (winnerIndex == localPlayerIndex);
            string winnerName = (winnerIndex == 0) ? playerAName : playerBName;
            string myStatus = (localPlayerIndex == 0) ? statusA : statusB;
            string oppStatus = (localPlayerIndex == 0) ? statusB : statusA;
            string oppName = (localPlayerIndex == 0) ? playerBName : playerAName;

            string title = didLocalWin
                ? LocalizationManager.Get("wheel_win_you", "BẠN ĐƯỢC QUYỀN PHÁ BI TRƯỚC!")
                : LocalizationManager.Format("wheel_win_opponent", winnerName);

            string subtitle = LocalizationManager.Format("lag_win_sub", myStatus, oppName, oppStatus);

            FoulPopupUI.ShowLagResult(
                LocalizationManager.Get("lag_badge", "ĐUA BĂNG"),
                title,
                subtitle,
                styleMode: didLocalWin ? 0 : 1,
                holdSeconds: 2.5f);
        }

        private void BuildMeasurementBadgesUI()
        {
            if (gameplayHUD != null && gameplayHUD.PlayerLeftPanel != null)
            {
                parentCanvas = gameplayHUD.PlayerLeftPanel.GetComponentInParent<Canvas>();
                if (parentCanvas != null && parentCanvas.rootCanvas != null) parentCanvas = parentCanvas.rootCanvas;
            }
            if (parentCanvas == null)
            {
                foreach (var c in FindObjectsOfType<Canvas>(true))
                {
                    if (c != null && c.name == "GameplayCanvas" && c.renderMode != RenderMode.WorldSpace)
                    {
                        parentCanvas = c;
                        break;
                    }
                }
            }
            if (parentCanvas == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            uiRoot = new GameObject("LagMeasurementOverlay", typeof(RectTransform));
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) uiRoot.layer = uiLayer;
            uiRoot.transform.SetParent(parentCanvas.transform, false);
            uiRoot.transform.SetAsLastSibling();

            uiRootRt = uiRoot.GetComponent<RectTransform>();
            uiRootRt.anchorMin = Vector2.zero;
            uiRootRt.anchorMax = Vector2.one;
            uiRootRt.offsetMin = Vector2.zero;
            uiRootRt.offsetMax = Vector2.zero;

            Color colYou = new Color(0f, 0.90f, 1f, 1f);
            Color colOpp = new Color(1f, 0.76f, 0.14f, 1f);
            Color colA = (localPlayerIndex == 0) ? colYou : colOpp;
            Color colB = (localPlayerIndex == 1) ? colYou : colOpp;

            badgeARt = BuildFloatingDistancePill(uiRoot.transform, "DistancePillA", colA, font, out resultTextA);
            badgeBRt = BuildFloatingDistancePill(uiRoot.transform, "DistancePillB", colB, font, out resultTextB);
            badgeARt.gameObject.SetActive(false);
            badgeBRt.gameObject.SetActive(false);
        }

        private void UpdateBadgeScreenPositions(UnityEngine.Camera cam)
        {
            if (cam == null || uiRootRt == null) return;

            if (badgeARt != null && badgeARt.gameObject.activeSelf && ballA != null)
            {
                Vector3 worldPosA = ballA.transform.position + new Vector3(0f, 0.065f, -0.08f);
                PositionBadgeAtWorld(badgeARt, worldPosA, cam, new Vector2(-115f, 34f));
            }

            if (badgeBRt != null && badgeBRt.gameObject.activeSelf && ballB != null)
            {
                Vector3 worldPosB = ballB.transform.position + new Vector3(0f, 0.065f, +0.08f);
                PositionBadgeAtWorld(badgeBRt, worldPosB, cam, new Vector2(115f, 34f));
            }
        }

        private void PositionBadgeAtWorld(RectTransform badgeRt, Vector3 worldPos, UnityEngine.Camera cam, Vector2 screenOffset)
        {
            Vector3 sp = cam.WorldToScreenPoint(worldPos);
            if (sp.z <= 0f) return;

            UnityEngine.Camera uiCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? parentCanvas.worldCamera
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(uiRootRt, sp, uiCam, out Vector2 localPt))
            {
                badgeRt.anchoredPosition = localPt + screenOffset;
            }
        }

        private static RectTransform BuildFloatingDistancePill(Transform parent, string name, Color accent, Font font, out Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300f, 42f);

            var bg = go.GetComponent<SkewedPanelGraphic>();
            bg.color = new Color(0.04f, 0.10f, 0.20f, 0.94f);
            bg.SetInsets(0f, 12f, 12f, 0f);
            bg.SetBottomDarken(0.35f);
            bg.raycastTarget = false;

            var fGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            fGo.transform.SetParent(rt, false);
            var fRt = fGo.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one; fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
            var fg = fGo.GetComponent<BorderProgressGraphic>();
            fg.SetInsets(0f, 12f, 12f, 0f);
            fg.SetBaseColor(accent);
            fg.raycastTarget = false;

            var tGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            tGo.transform.SetParent(rt, false);
            labelText = tGo.GetComponent<Text>();
            labelText.font = font;
            labelText.fontSize = 15;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.supportRichText = true;
            labelText.raycastTarget = false;
            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 11;
            labelText.resizeTextMaxSize = 16;
            labelText.rectTransform.anchorMin = Vector2.zero;
            labelText.rectTransform.anchorMax = Vector2.one;
            labelText.rectTransform.offsetMin = new Vector2(14f, 2f);
            labelText.rectTransform.offsetMax = new Vector2(-14f, -2f);

            var sh = tGo.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(1.2f, -1.2f);

            return rt;
        }

        private void CleanupLagState()
        {
            AimOnlyHudVisibility.ForceShowPowerBar = false;
            ClearHudPlayerTimers();

            if (gameplayHUD != null)
            {
                var banner = gameplayHUD.TurnSlideBanner;
                if (banner != null) banner.HideBreakPlacementPrompt();
            }

            if (ballBRenderer != null && savedBallBMaterial != null)
            {
                ballBRenderer.sharedMaterial = savedBallBMaterial;
            }

            if (clonedCueStickGo != null)
            {
                Destroy(clonedCueStickGo);
                clonedCueStickGo = null;
            }

            if (visualsRoot != null)
            {
                Destroy(visualsRoot);
                visualsRoot = null;
            }

            if (uiRoot != null)
            {
                Destroy(uiRoot);
                uiRoot = null;
            }
        }

        private void OnDestroy()
        {
            if (isRunning)
            {
                CleanupLagState();
                HudModalState.Release("lag_shot");
                isRunning = false;
            }
            IsRunning = false;
        }
    }
}
