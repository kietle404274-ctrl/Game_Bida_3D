using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Game.Billiards.Core;
using Game.Billiards.Input;
using Game.Billiards.Match;
using Game.Billiards.Physics;
using Game.Billiards.Rules;
using Game.Billiards.UI;
using Game.Billiards.Camera;

namespace Game.Billiards.Network
{
    public class NetworkMatchManager : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        [Header("Tick Settings")]
        [Tooltip("Tần số đồng bộ tọa độ bi mỗi giây (tick/s)")]
        [SerializeField] private float networkTickRate = 20f; // 20 updates per second (50ms)

        [Header("Component References")]
        [SerializeField] private MatchStateManager matchStateManager;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private BallManager ballManager;
        [SerializeField] private ShotExecutor shotExecutor;
        [SerializeField] private PlayerInputController inputController;
        [SerializeField] private CueController cueController;
        [SerializeField] private CameraDirector cameraDirector;
        [SerializeField] private BallMovementMonitor movementMonitor;
        [SerializeField] private BallInHandController ballInHandController;

        private bool isOnlineMatch = false;
        private int localPlayerIndex = 0; // 0: Master (Host), 1: Client
        private float tickInterval;
        private float lastTickTime;
        private Coroutine ballInterpolationRoutine;

        private float lastAimSyncTime;
        private Vector3 lastSentAimDir = Vector3.zero;
        private float lastSentPower = -1f;
        private float lastSentElev = -1f;

        public bool IsOnlineMatch => isOnlineMatch;
        public bool IsMaster => PhotonNetwork.IsMasterClient;
        public int LocalPlayerIndex => localPlayerIndex;
        public bool IsMyTurn => !isOnlineMatch || (matchStateManager != null && matchStateManager.CurrentPlayerIndex == localPlayerIndex);

        /// <summary>Đối thủ đã rời phòng (thoát giữa trận hoặc sau khi trận kết thúc). Dùng để ẩn nút Chơi lại.</summary>
        public bool OpponentLeft { get; private set; }
        /// <summary>Máy cục bộ bị mất kết nối Photon giữa trận (bị xử thua).</summary>
        public bool LocalDisconnected { get; private set; }

        /// <summary>Client đã chạy dự đoán cú đánh cục bộ và đang chờ kết quả chính thức từ Master (bỏ qua sự kiện bi dừng cục bộ).</summary>
        public bool AwaitingMasterResult { get; private set; }

        // Client: snapshot vị trí bi trước khi dự đoán, dùng để khôi phục nếu Master từ chối lệnh
        private struct PredictedBallBackup { public BallController ball; public Vector3 pos; public bool pocketed; }
        private PredictedBallBackup[] predictedBackup;

        // Đồng bộ theo BƯỚC VẬT LÝ: cả Master và Client đếm số FixedUpdate kể từ lúc bắt đầu cú đánh.
        // Master gửi kèm số bước; Client so vị trí Master với lịch sử của CHÍNH NÓ ở đúng bước đó => không phụ thuộc độ trễ mạng.
        private int masterSimStep;
        private bool masterSimActive;
        private bool clientSimActive;
        private readonly List<Vector3[]> predPosHistory = new List<Vector3[]>();
        private readonly List<Vector3[]> predVelHistory = new List<Vector3[]>();
        private readonly HashSet<int> desyncedBalls = new HashSet<int>();



        public event Action<string> OnMatchDisconnected;

        /// <summary>Client nhận snapshot thống kê trận từ Master.</summary>
        public event Action<float[]> OnStatsReceived;

        public void BroadcastStats(float[] data)
        {
            if (!isOnlineMatch || !IsMaster || data == null) return;
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_STATS_SYNC, data, options, SendOptions.SendReliable);
        }

        public override void OnEnable()
        {
            base.OnEnable();
            PhotonNetwork.AddCallbackTarget(this);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        public void Initialize(
            MatchStateManager stateMgr,
            TurnManager turnMgr,
            BallManager bMgr,
            ShotExecutor executor,
            PlayerInputController inputCtrl,
            CueController cueCtrl,
            CameraDirector camDir,
            BallMovementMonitor monitor,
            BallInHandController bihCtrl)
        {
            matchStateManager = stateMgr;
            turnManager = turnMgr;
            ballManager = bMgr;
            shotExecutor = executor;
            inputController = inputCtrl;
            cueController = cueCtrl;
            cameraDirector = camDir;
            movementMonitor = monitor;
            ballInHandController = bihCtrl;

            tickInterval = 1f / Mathf.Max(networkTickRate, 30f);

            var session = GameSession.Instance;
            isOnlineMatch = (session != null && session.CurrentGameMode == GameMode.OnlinePvP && PhotonNetwork.InRoom);
            localPlayerIndex = PhotonNetwork.IsMasterClient ? 0 : 1;

            Debug.Log($"[NetworkMatchManager] Khởi tạo trận đấu mạng: IsOnline={isOnlineMatch}, LocalPlayerIndex={localPlayerIndex} ({(localPlayerIndex == 0 ? "Chủ phòng / Master" : "Khách / Client")})");

            if (isOnlineMatch)
            {
                // Đăng ký nhận sự kiện Shot từ PlayerInputController
                if (inputController != null)
                {
                    inputController.OnShotInitiated -= HandleLocalShotInitiated;
                    inputController.OnShotInitiated += HandleLocalShotInitiated;

                    inputController.OnCallShotChanged -= HandleLocalCallShotChanged;
                    inputController.OnCallShotChanged += HandleLocalCallShotChanged;
                }

                if (ballInHandController != null)
                {
                    ballInHandController.OnBallInHandConfirmed -= HandleLocalBallInHandConfirmed;
                    ballInHandController.OnBallInHandConfirmed += HandleLocalBallInHandConfirmed;

                    ballInHandController.OnBallInHandMoved -= HandleLocalBallInHandMoved;
                    ballInHandController.OnBallInHandMoved += HandleLocalBallInHandMoved;
                }

                if (matchStateManager != null)
                {
                    matchStateManager.OnPhaseChanged -= HandlePhaseChanged;
                    matchStateManager.OnPhaseChanged += HandlePhaseChanged;

                    matchStateManager.OnRackStarted -= HandleRackStarted;
                    matchStateManager.OnRackStarted += HandleRackStarted;
                }

                if (turnManager != null)
                {
                    turnManager.OnTurnChanged -= HandleTurnChanged;
                    turnManager.OnTurnChanged += HandleTurnChanged;
                }

                if (shotExecutor != null)
                {
                    shotExecutor.OnShotExecuted -= HandleShotExecutedForSync;
                    shotExecutor.OnShotExecuted += HandleShotExecutedForSync;
                }

                // Cập nhật quyền điều khiển theo lượt hiện tại
                UpdateInputPermissions();
            }
        }

        private void OnDestroy()
        {
            if (inputController != null)
            {
                inputController.OnShotInitiated -= HandleLocalShotInitiated;
                inputController.OnCallShotChanged -= HandleLocalCallShotChanged;
            }

            if (ballInHandController != null)
            {
                ballInHandController.OnBallInHandConfirmed -= HandleLocalBallInHandConfirmed;
                ballInHandController.OnBallInHandMoved -= HandleLocalBallInHandMoved;
            }

            if (matchStateManager != null)
            {
                matchStateManager.OnPhaseChanged -= HandlePhaseChanged;
                matchStateManager.OnRackStarted -= HandleRackStarted;
            }

            if (turnManager != null)
                turnManager.OnTurnChanged -= HandleTurnChanged;

            if (shotExecutor != null)
                shotExecutor.OnShotExecuted -= HandleShotExecutedForSync;
        }

        /// <summary>Cả hai máy: bắt đầu đếm bước vật lý ngay khi cú đánh được thực thi (Master: cú thật; Client: cú dự đoán).</summary>
        private void HandleShotExecutedForSync(ShotCommand cmd)
        {
            if (!isOnlineMatch) return;
            if (IsMaster)
            {
                masterSimStep = 0;
                masterSimActive = true;
            }
            else
            {
                ResetPredictionSim();
                clientSimActive = true;
            }
        }

        private void ResetPredictionSim()
        {
            clientSimActive = false;
            predPosHistory.Clear();
            predVelHistory.Clear();
            desyncedBalls.Clear();
        }

        private void FixedUpdate()
        {
            if (!isOnlineMatch) return;

            if (IsMaster)
            {
                if (!masterSimActive) return;
                if (matchStateManager != null && matchStateManager.CurrentPhase != MatchPhase.ShotInProgress)
                {
                    masterSimActive = false;
                    return;
                }
                masterSimStep++;
                return;
            }

            if (!clientSimActive || !AwaitingMasterResult || ballManager == null || ballManager.AllBalls == null) return;

            // FixedUpdate chạy TRƯỚC bước PhysX: trạng thái đang thấy = sau (history.Count) bước đã hoàn tất
            var balls = ballManager.AllBalls;
            var pos = new Vector3[16];
            var vel = new Vector3[16];
            for (int i = 0; i < balls.Length; i++)
            {
                var b = balls[i];
                if (b == null) continue;
                int n = b.BallNumber;
                if (n < 0 || n >= 16) continue;
                var rb = b.Rigidbody;
                pos[n] = rb != null ? rb.position : b.transform.position;
                vel[n] = rb != null ? rb.velocity : Vector3.zero;
            }
            predPosHistory.Add(pos);
            predVelHistory.Add(vel);
        }

        private void Update()
        {
            if (!isOnlineMatch) return;

            // Master Client gửi tọa độ bi định kỳ khi các bi đang chuyển động
            if (IsMaster && matchStateManager != null && matchStateManager.CurrentPhase == MatchPhase.ShotInProgress)
            {
                if (Time.time - lastTickTime >= tickInterval)
                {
                    lastTickTime = Time.time;
                    BroadcastBallTransforms();
                }
            }

            // Đồng bộ hướng ngắm và lực kéo của cơ theo thời gian thực tới đối thủ (15 FPS)
            if (IsMyTurn && matchStateManager != null)
            {
                var phase = matchStateManager.CurrentPhase;
                if (phase == MatchPhase.Aiming || phase == MatchPhase.BreakSetup)
                {
                    if (Time.time - lastAimSyncTime >= 0.066f && cameraDirector != null && inputController != null)
                    {
                        Vector3 aim = cameraDirector.AimDirection;
                        float pwr = inputController.CurrentPower01;
                        float elev = cueController != null ? cueController.UserElevationDeg : 0f;

                        if (Vector3.Dot(aim, lastSentAimDir) < 0.9999f || Mathf.Abs(pwr - lastSentPower) > 0.005f || Mathf.Abs(elev - lastSentElev) > 0.1f)
                        {
                            lastAimSyncTime = Time.time;
                            lastSentAimDir = aim;
                            lastSentPower = pwr;
                            lastSentElev = elev;
                            SendAimSync(aim, pwr, elev);
                        }
                    }
                }
            }
        }

        private void HandlePhaseChanged(MatchPhase phase)
        {
            UpdateInputPermissions();
        }

        private void HandleTurnChanged(int newPlayerIndex)
        {
            UpdateInputPermissions();
        }

        public void UpdateInputPermissions()
        {
            if (!isOnlineMatch || inputController == null || matchStateManager == null) return;

            bool isMyTurn = (matchStateManager.CurrentPlayerIndex == localPlayerIndex);
            bool isAimPhase = (matchStateManager.CurrentPhase == MatchPhase.Aiming || 
                               matchStateManager.CurrentPhase == MatchPhase.BreakSetup || 
                               matchStateManager.CurrentPhase == MatchPhase.BallInHand);

            bool canControl = isMyTurn && isAimPhase;

            inputController.SetTurnActive(canControl);

            if (canControl)
            {
                if (ballManager != null && ballManager.CueBall != null)
                {
                    if (cameraDirector != null) cameraDirector.SetCueBallTarget(ballManager.CueBall.transform);
                    if (cueController != null) cueController.SetCueBallTarget(ballManager.CueBall.transform);
                }
                if (cueController != null && matchStateManager.CurrentPhase != MatchPhase.BallInHand)
                {
                    cueController.SetAimingState(true);
                    if (cameraDirector != null)
                    {
                        cueController.UpdateAim(cameraDirector.AimDirection, inputController.CurrentPower01);
                    }
                }
                if (cameraDirector != null && cameraDirector.CurrentState != CameraState.Aim && 
                    matchStateManager.CurrentPhase != MatchPhase.BallInHand)
                {
                    cameraDirector.SwitchToAimCamera();
                }
            }
            else
            {
                if (!isMyTurn)
                {
                    if (cameraDirector != null && cameraDirector.CurrentState != CameraState.OpponentView && 
                        matchStateManager.CurrentPhase != MatchPhase.ShotInProgress)
                    {
                        cameraDirector.SwitchToOpponentCamera();
                    }
                    if (cueController != null)
                    {
                        cueController.SetAimingState(false);
                    }
                }
            }
        }

        #region Shot Execution & Forwarding

        private void HandleLocalShotInitiated(ShotCommand command)
        {
            if (!isOnlineMatch) return;

            if (IsMaster)
            {
                // Master Client thực thi tại local và broadcast cho Client biết để diễn hoạt gậy cơ
                BroadcastShotCommand(command);
            }
            else
            {
                // Client KHÔNG được tự ý chạy vật lý (Rule 114)
                // Gửi ShotCommand tới Master Client để xin cấp phép thực thi (Rule 115)
                SendShotCommandToMaster(command);

                // Hiệu ứng hình ảnh + DỰ ĐOÁN: cơ đánh NGAY trên máy Client và bi lăn ngay bằng vật lý cục bộ.
                // Master vẫn là bên quyết định (kiểm luật, kết quả lượt) và sửa lại vị trí bi bằng gói đồng bộ.
                if (cueController != null)
                {
                    clientLocalStrikeActive = true;
                    cueController.SetAimingState(true);
                    cueController.SetElevation(command.cueElevationDeg);
                    cueController.UpdateAim(command.direction, command.power01);
                    cueController.TriggerStrikeAnimation(() =>
                    {
                        if (!clientLocalStrikeActive) return; // đã bị Master từ chối / kết quả đã về
                        if (cameraDirector != null)
                        {
                            cameraDirector.SwitchToShotFollowCamera(command.direction);
                        }
                        BeginPredictedShot(command);
                    });
                }
            }
        }

        /// <summary>Client: lưu vị trí bi rồi chạy cú đánh dự đoán bằng vật lý cục bộ. Nếu không đánh được thì để Master xử lý như trước.</summary>
        private void BeginPredictedShot(ShotCommand command)
        {
            if (shotExecutor == null || ballManager == null || ballManager.AllBalls == null) return;
            if (!shotExecutor.CanExecuteShot()) return;

            var balls = ballManager.AllBalls;
            predictedBackup = new PredictedBallBackup[balls.Length];
            for (int i = 0; i < balls.Length; i++)
            {
                var b = balls[i];
                predictedBackup[i] = new PredictedBallBackup
                {
                    ball = b,
                    pos = b != null ? b.transform.position : Vector3.zero,
                    pocketed = b != null && b.IsPocketed
                };
            }

            if (shotExecutor.ExecuteShot(command))
            {
                AwaitingMasterResult = true;
            }
            else
            {
                predictedBackup = null;
            }
        }

        /// <summary>Client: Master từ chối lệnh -> đưa các bi về đúng vị trí trước cú đánh dự đoán.</summary>
        private void RestorePredictedBalls()
        {
            if (predictedBackup != null)
            {
                for (int i = 0; i < predictedBackup.Length; i++)
                {
                    var e = predictedBackup[i];
                    if (e.ball == null || e.pocketed) continue;
                    e.ball.ResetTo(e.pos, Quaternion.identity);
                }
                predictedBackup = null;
            }
            if (movementMonitor != null) movementMonitor.ResetToResting();
            AwaitingMasterResult = false;
            ResetPredictionSim();
        }

        private void SendShotCommandToMaster(ShotCommand command)
        {
            object[] payload = PackShotCommand(command);
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_SHOT_COMMAND, payload, options, SendOptions.SendReliable);
            Debug.Log("[NetworkMatchManager] [Client] Đã gửi ShotCommand tới Master Client thẩm định.");
        }

        private void BroadcastShotCommand(ShotCommand command)
        {
            object[] payload = PackShotCommand(command);
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_SHOT_COMMAND, payload, options, SendOptions.SendReliable);
            Debug.Log("[NetworkMatchManager] [Master] Đã broadcast ShotCommand tới Client.");
        }

        private object[] PackShotCommand(ShotCommand cmd)
        {
            return new object[]
            {
                cmd.shooterActorId,
                cmd.direction.x, cmd.direction.y, cmd.direction.z,
                cmd.power01,
                cmd.cueContact01.x, cmd.cueContact01.y,
                cmd.calledBallNumber,
                cmd.calledPocketId,
                cmd.calledSafety,
                matchStateManager != null ? matchStateManager.State.turnSequence : 0,
                cmd.cueElevationDeg
            };
        }

        private ShotCommand UnpackShotCommand(object[] data)
        {
            int actor = (int)data[0];
            Vector3 dir = new Vector3((float)data[1], (float)data[2], (float)data[3]);
            float power = (float)data[4];
            Vector2 contact = new Vector2((float)data[5], (float)data[6]);
            int ball = (int)data[7];
            int pocket = (int)data[8];
            bool safety = (bool)data[9];

            // Phần tử 11 = góc nâng cơ (gói cũ không có => 0)
            float elevation = (data.Length > 11 && data[11] is float e) ? e : 0f;

            return new ShotCommand(actor, dir, power, contact, ball, pocket, safety, 0, elevation);
        }

        #endregion

        #region Ball Synchronization (Master -> Client)

        private void BroadcastBallTransforms()
        {
            if (ballManager == null || ballManager.AllBalls == null) return;

            var balls = ballManager.AllBalls;
            // Đóng gói: [count, (ballNum, px, py, pz, vx, vy, vz, pocketed)...]
            List<float> stream = new List<float>(balls.Length * 8 + 1);
            stream.Add(balls.Length);

            for (int i = 0; i < balls.Length; i++)
            {
                var ball = balls[i];
                if (ball == null) continue;

                Vector3 pos = ball.Rigidbody != null ? ball.Rigidbody.position : ball.transform.position;
                Vector3 vel = ball.Rigidbody != null ? ball.Rigidbody.velocity : Vector3.zero;
                stream.Add(ball.BallNumber);
                stream.Add(pos.x);
                stream.Add(pos.y);
                stream.Add(pos.z);
                stream.Add(vel.x);
                stream.Add(vel.y);
                stream.Add(vel.z);
                stream.Add(ball.IsPocketed ? 1f : 0f);
            }

            // Phần tử cuối: số bước vật lý Master đã chạy từ lúc đánh (Client dùng để so sánh đúng bước)
            stream.Add(masterSimStep);

            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_BALLS_SYNC, stream.ToArray(), options, SendOptions.SendUnreliable);
        }

        private void ApplyBallsSyncPacket(float[] stream)
        {
            if (ballManager == null || stream == null || stream.Length < 1) return;

            int count = Mathf.RoundToInt(stream[0]);
            int stride = 8;
            int idx = 1;
            // Cú đánh dự đoán của chính Client bắt đầu sớm hơn Master ~1 ping (nửa ping lệnh đi + nửa ping gói về) -> bù đủ 1 ping
            float lagComp = AwaitingMasterResult
                ? Mathf.Clamp(PhotonNetwork.GetPing() * 0.001f, 0f, 0.4f)
                : Mathf.Clamp(PhotonNetwork.GetPing() * 0.0005f, 0f, 0.15f);

            int masterStep = stream.Length == 1 + count * stride + 1 ? Mathf.RoundToInt(stream[1 + count * stride]) : -1;
            bool reconcile = AwaitingMasterResult && clientSimActive && masterStep >= 0 && masterStep < predPosHistory.Count;

            for (int i = 0; i < count; i++)
            {
                if (idx + stride > stream.Length) break;

                int ballNum = Mathf.RoundToInt(stream[idx]);
                Vector3 targetPos = new Vector3(stream[idx + 1], stream[idx + 2], stream[idx + 3]);
                Vector3 targetVel = new Vector3(stream[idx + 4], stream[idx + 5], stream[idx + 6]);
                bool pocketed = stream[idx + 7] > 0.5f;

                var ball = ballManager.GetBall(ballNum);
                if (ball != null)
                {
                    if (pocketed && !ball.IsPocketed)
                    {
                        ball.MarkPocketed(-1);
                    }
                    else if (!ball.IsPocketed)
                    {
                        var rbd = ball.Rigidbody;
                        if (rbd != null)
                        {
                            if (rbd.isKinematic) rbd.isKinematic = false;

                            if (reconcile && ballNum >= 0 && ballNum < 16 && !desyncedBalls.Contains(ballNum))
                            {
                                Vector3 rerr = targetPos - predPosHistory[masterStep][ballNum];
                                rerr.y = 0f;
                                float e = rerr.magnitude;
                                if (e > 0.15f)
                                {
                                    // Mô phỏng thật sự lệch (va chạm khác): snap về Master, ngoại suy theo số bước đã trôi
                                    Vector3 sp = targetPos + new Vector3(targetVel.x, 0f, targetVel.z) * ((predPosHistory.Count - masterStep) * Time.fixedDeltaTime);
                                    rbd.position = sp;
                                    ball.transform.position = sp;
                                    rbd.velocity = targetVel;
                                    desyncedBalls.Add(ballNum);
                                }
                                else if (e > 0.004f)
                                {
                                    Vector3 d = rerr * 0.5f;
                                    Vector3 hv = predVelHistory[masterStep][ballNum];
                                    Vector3 dv = new Vector3(targetVel.x - hv.x, 0f, targetVel.z - hv.z) * 0.5f;
                                    rbd.position += d;
                                    ball.transform.position += d;
                                    rbd.velocity += dv;
                                    for (int h = masterStep; h < predPosHistory.Count; h++)
                                    {
                                        predPosHistory[h][ballNum] += d;
                                        predVelHistory[h][ballNum] += dv;
                                    }
                                }
                                idx += stride;
                                continue;
                            }

                            // Bù độ trễ gói (~nửa ping): vị trí Master gửi đã cũ, ngoại suy theo vận tốc
                            Vector3 predictedPos = targetPos + new Vector3(targetVel.x, 0f, targetVel.z) * lagComp;
                            Vector3 err = predictedPos - ball.transform.position;
                            err.y = 0f;

                            if (err.magnitude > 0.25f)
                            {
                                // Lệch quá xa: snap hẳn về vị trí Master
                                rbd.position = predictedPos;
                                ball.transform.position = predictedPos;
                                rbd.velocity = targetVel;
                            }
                            else if (targetVel.sqrMagnitude < 0.0001f && err.sqrMagnitude < 0.000009f)
                            {
                                // Bi nghỉ và đã gần đích: không động vào để tránh rung vi mô
                            }
                            else
                            {
                                // Giữ mô phỏng vật lý cục bộ (mượt, có âm thanh va chạm) và kéo nhẹ về đích bằng vận tốc hiệu chỉnh
                                rbd.velocity = targetVel + err * 6f;
                            }
                        }
                    }
                }

                idx += stride;
            }
        }

        #endregion

        #region Turn Result Broadcast (Master -> Client)

        public void BroadcastTurnOutcome(ShotResult result)
        {
            if (!isOnlineMatch || !IsMaster || matchStateManager == null) return;
            masterShotAcceptedAt = -100f; // lượt đã kết thúc: cho phép nhận lệnh đánh mới

            // Đóng gói MatchState delta và kết quả lượt đánh
            var state = matchStateManager.State;
            bool wasBreakShot = turnManager != null && turnManager.LastShotWasBreak;
            bool headStringOnly = result.foul == FoulType.IllegalBreak || (wasBreakShot && result.foul == FoulType.CueBallScratch);
            ExitGames.Client.Photon.Hashtable outcomeData = new ExitGames.Client.Photon.Hashtable
            {
                { "phase", (int)state.phase },
                { "currentPlayer", state.currentPlayerIndex },
                { "turnSeq", state.turnSequence },
                { "tableOpen", state.tableOpen },
                { "pAGroup", (int)state.playerAGroup },
                { "pBGroup", (int)state.playerBGroup },
                { "pAWins", state.playerARackWins },
                { "pBWins", state.playerBRackWins },
                { "foul", (int)result.foul },
                { "rackEnded", result.rackEnded },
                { "outcome", (int)result.outcome },
                { "breakPocketed", (result.pocketedBalls != null ? result.pocketedBalls.FindAll(b => b >= 1 && b <= 15).Count : 0) },
                // Cú khai cuộc / đặt bi sau lỗi khai cuộc phải tính TRƯỚC khi turnSequence tăng (SwitchTurn/IncrementTurnSequence đều ++)
                { "wasBreak", wasBreakShot },
                { "headString", headStringOnly },
                // Thời điểm Master gói gửi (giây mạng Photon) để Client bù trễ cho đồng hồ lượt
                { "tSent", PhotonNetwork.Time }
            };

            // Đóng gói snapshot tọa độ chính xác của 16 bi
            if (ballManager != null && ballManager.AllBalls != null)
            {
                var balls = ballManager.AllBalls;
                List<float> snap = new List<float>(balls.Length * 5);
                for (int i = 0; i < balls.Length; i++)
                {
                    var b = balls[i];
                    if (b == null) continue;
                    snap.Add(b.BallNumber);
                    snap.Add(b.transform.position.x);
                    snap.Add(b.transform.position.y);
                    snap.Add(b.transform.position.z);
                    snap.Add(b.IsPocketed ? 1f : 0f);
                }
                outcomeData.Add("ballSnap", snap.ToArray());
            }

            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_TURN_RESULT, outcomeData, options, SendOptions.SendReliable);
            Debug.Log($"[NetworkMatchManager] [Master] Đã broadcast EVENT_TURN_RESULT (NextPlayer: {state.currentPlayerIndex}, Phase: {state.phase})");
        }

        private void ApplyTurnResultPacket(ExitGames.Client.Photon.Hashtable data)
        {
            if (matchStateManager == null || data == null) return;

            var state = matchStateManager.State;
            // Hết lượt: xóa marker lỗ đã chỉ định của người vừa đánh trên máy người xem
            if (VFX.VFXManager.Instance != null) VFX.VFXManager.Instance.SetPocketCallMarker(Vector3.zero, false);
            clientLocalStrikeActive = false; // Kết quả lượt đã về: cú đánh cục bộ đã được Master xử lý xong
            AwaitingMasterResult = false;
            ResetPredictionSim();
            predictedBackup = null;
            MatchPhase targetPhase = (MatchPhase)Convert.ToInt32(data["phase"]);
            state.currentPlayerIndex = Convert.ToInt32(data["currentPlayer"]);
            state.turnSequence = Convert.ToInt32(data["turnSeq"]);
            state.tableOpen = (bool)data["tableOpen"];
            state.playerAGroup = (PlayerGroup)Convert.ToInt32(data["pAGroup"]);
            state.playerBGroup = (PlayerGroup)Convert.ToInt32(data["pBGroup"]);
            state.playerARackWins = Convert.ToInt32(data["pAWins"]);
            state.playerBRackWins = Convert.ToInt32(data["pBWins"]);

            // Snap chính xác vị trí 16 bi
            if (data.TryGetValue("ballSnap", out object snapObj) && ballManager != null)
            {
                float[] snap = (float[])snapObj;
                int stride = 5;
                for (int i = 0; i < snap.Length; i += stride)
                {
                    int num = Mathf.RoundToInt(snap[i]);
                    Vector3 pos = new Vector3(snap[i + 1], snap[i + 2], snap[i + 3]);
                    bool pocketed = snap[i + 4] > 0.5f;

                    var ball = ballManager.GetBall(num);
                    if (ball != null)
                    {
                        if (pocketed)
                        {
                            if (!ball.IsPocketed) ball.MarkPocketed(-1);
                        }
                        else
                        {
                            if (ball.IsPocketed || !ball.gameObject.activeSelf)
                            {
                                ball.ResetTo(pos, Quaternion.identity);
                            }
                            else
                            {
                                ball.transform.position = pos;
                                if (ball.Rigidbody != null)
                                {
                                    if (ball.Rigidbody.isKinematic)
                                    {
                                        ball.Rigidbody.isKinematic = false;
                                    }
                                    ball.Rigidbody.velocity = Vector3.zero;
                                    ball.Rigidbody.angularVelocity = Vector3.zero;
                                }
                            }
                        }
                    }
                }
            }

            FoulType foul = (FoulType)Convert.ToInt32(data["foul"]);
            bool rackEnded = (bool)data["rackEnded"];

            if (foul != FoulType.None)
            {
                FoulPopupUI.Show(foul);
            }
            else
            {
                int breakPocketed = data.ContainsKey("breakPocketed") ? Convert.ToInt32(data["breakPocketed"]) : 0;
                bool wasBreakShot = data.ContainsKey("wasBreak") && (bool)data["wasBreak"];
                if (wasBreakShot && breakPocketed > 0)
                {
                    bool isLocalShooter = (state.currentPlayerIndex == localPlayerIndex);
                    string nextTurnSubtitle = isLocalShooter
                        ? LocalizationManager.Get("turn_your_turn")
                        : LocalizationManager.Format("turn_banner_opponent_sub_simple", state.players[state.currentPlayerIndex].displayName);

                    FoulPopupUI.ShowBreakSuccess(
                        LocalizationManager.Get("break_ball_pocketed_title"),
                        nextTurnSubtitle,
                        2.4f
                    );
                }
            }

            if (rackEnded)
            {
                // Master đã gửi tỉ số GỒM ván vừa thắng: chỉ chuyển phase/sự kiện, không cộng điểm lần nữa
                RackOutcome outcome = (RackOutcome)Convert.ToInt32(data["outcome"]);
                matchStateManager.ApplyRackEndFromMaster(outcome == RackOutcome.PlayerAWins ? 0 : 1);
            }
            else
            {
                // Giải phóng trạng thái bắn và trạng thái chuyển động của Client khi nhận kết quả authoritative từ Master
                if (inputController != null)
                {
                    inputController.ResetShotState();
                }
                if (movementMonitor != null)
                {
                    movementMonitor.ResetToResting();
                }

                matchStateManager.SetPhase(targetPhase);
                state.phase = targetPhase;

                if (turnManager != null)
                {
                    turnManager.RaiseTurnChanged(state.currentPlayerIndex);

                    // Bù độ trễ mạng cho đồng hồ lượt: Client bắt đầu đếm muộn hơn Master ~1 lần trễ gói
                    if (targetPhase == MatchPhase.Aiming && data.ContainsKey("tSent") && data["tSent"] is double tSent)
                    {
                        float elapsed = (float)(PhotonNetwork.Time - tSent);
                        if (elapsed > 0f && elapsed < 3f) turnManager.CompensateShotTimer(elapsed);
                    }
                }

                if (targetPhase == MatchPhase.BallInHand && ballInHandController != null)
                {
                    bool headString = data.ContainsKey("headString") && (bool)data["headString"];
                    ballInHandController.EnterBallInHand(headString);
                }
                else if (targetPhase == MatchPhase.Aiming)
                {
                    if (ballInHandController != null && ballInHandController.IsActive)
                    {
                        ballInHandController.CancelBallInHand();
                    }
                    if (state.currentPlayerIndex == localPlayerIndex)
                    {
                        if (cameraDirector != null && cameraDirector.CurrentState != CameraState.Aim)
                        {
                            cameraDirector.SwitchToAimCamera();
                        }
                        if (cueController != null)
                        {
                            cueController.SetAimingState(true);
                            if (cameraDirector != null)
                            {
                                cueController.UpdateAim(cameraDirector.AimDirection, inputController != null ? inputController.CurrentPower01 : 0.35f);
                            }
                        }
                    }
                }
            }

            // Cập nhật HUD trên Client (nhóm bi, ô bi còn lại, điểm, đèn lượt) vì sự kiện OnShotEvaluated chỉ chạy trên Master
            var hud = FindObjectOfType<GameplayHUD>();
            if (hud != null) hud.RefreshAllUI();

            UpdateInputPermissions();
            Debug.Log($"[NetworkMatchManager] [Client] Đã áp dụng snapshot authoritative từ Master (Lượt: Player {state.currentPlayerIndex}, Phase: {state.phase})");
        }

        #endregion

        #region Photon Event Callback Implementation

        public void OnEvent(EventData photonEvent)
        {
            if (!isOnlineMatch) return;

            byte code = photonEvent.Code;

            switch (code)
            {
                case NetworkConstants.EVENT_SHOT_COMMAND:
                    HandleReceivedShotCommand(photonEvent.CustomData);
                    break;

                case NetworkConstants.EVENT_SHOT_REJECTED:
                    HandleReceivedShotRejected();
                    break;

                case NetworkConstants.EVENT_SURRENDER_REQUEST:
                    HandleReceivedSurrenderRequest(photonEvent.CustomData);
                    break;

                case NetworkConstants.EVENT_BALLS_SYNC:
                    // Chỉ nhận khi Client đang trong ShotInProgress; gói trễ sau TURN_RESULT sẽ bị bỏ (không kéo bi lùi)
                    if (!IsMaster && matchStateManager != null && matchStateManager.CurrentPhase == MatchPhase.ShotInProgress)
                    {
                        ApplyBallsSyncPacket((float[])photonEvent.CustomData);
                    }
                    break;

                case NetworkConstants.EVENT_TURN_RESULT:
                    if (!IsMaster)
                    {
                        ApplyTurnResultPacket((ExitGames.Client.Photon.Hashtable)photonEvent.CustomData);
                    }
                    break;

                case NetworkConstants.EVENT_CALL_SHOT:
                    HandleReceivedCallShot(photonEvent.CustomData);
                    break;

                case NetworkConstants.EVENT_SURRENDER:
                    HandleReceivedSurrender(photonEvent.CustomData);
                    break;

                case NetworkConstants.EVENT_STATS_SYNC:
                    if (!IsMaster)
                    {
                        OnStatsReceived?.Invoke(photonEvent.CustomData as float[]);
                    }
                    break;

                case NetworkConstants.EVENT_RACK_SYNC:
                    HandleReceivedRackSync(photonEvent.CustomData);
                    break;

                case NetworkConstants.EVENT_AIM_SYNC:
                    HandleReceivedAimSync(photonEvent.CustomData);
                    break;

                case NetworkConstants.EVENT_BALL_IN_HAND_CONFIRMED:
                    HandleReceivedBallInHandConfirmed(photonEvent.CustomData);
                    break;

                case NetworkConstants.EVENT_BALL_IN_HAND_PREVIEW:
                    HandleReceivedBallInHandPreview(photonEvent.CustomData);
                    break;

                case NetworkConstants.EVENT_NEXT_RACK:
                    HandleReceivedNextRack(photonEvent.CustomData);
                    break;

                case NetworkConstants.EVENT_REMATCH:
                    HandleReceivedRematch(photonEvent.CustomData);
                    break;
            }
        }

        // Master: thời điểm nhận lệnh đánh hợp lệ gần nhất (chống nhận 2 lệnh trong khi giật cơ chưa xong, phase vẫn Aiming)
        private float masterShotAcceptedAt = -100f;

        private void SendShotRejected()
        {
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_SHOT_REJECTED, null, options, SendOptions.SendReliable);
        }

        // Client: đã tự chạy hoạt ảnh đánh cục bộ cho cú đánh đang chờ Master xác nhận
        private bool clientLocalStrikeActive = false;

        private void HandleReceivedShotRejected()
        {
            if (IsMaster) return;
            Debug.LogWarning("[NetworkMatchManager] [Client] Master từ chối lệnh đánh -> mở khóa để đánh lại.");
            bool hadLocalStrike = clientLocalStrikeActive;
            bool hadPrediction = AwaitingMasterResult;
            clientLocalStrikeActive = false;
            if (hadPrediction)
            {
                RestorePredictedBalls(); // bi đã lăn thử cục bộ: đặt lại đúng vị trí cũ
                if (matchStateManager != null && matchStateManager.CurrentPhase == MatchPhase.ShotInProgress && IsMyTurn)
                {
                    matchStateManager.SetPhase(MatchPhase.Aiming);
                }
            }
            if (inputController != null) inputController.ResetShotState();

            // Hoạt ảnh đánh cục bộ đã ẩn cơ/chuyển camera: khôi phục lại trạng thái ngắm
            if (hadLocalStrike)
            {
                if (cameraDirector != null && cameraDirector.CurrentState != CameraState.Aim)
                {
                    cameraDirector.SwitchToAimCamera();
                }
                if (cueController != null)
                {
                    cueController.SetAimingState(true);
                    if (cameraDirector != null)
                    {
                        cueController.UpdateAim(cameraDirector.AimDirection, inputController != null ? inputController.CurrentPower01 : 0.35f);
                    }
                }
            }
            UpdateInputPermissions();
        }

        private void HandleReceivedShotCommand(object rawData)
        {
            if (rawData == null) return;
            ShotCommand cmd = UnpackShotCommand((object[])rawData);

            if (IsMaster)
            {
                // Master kiểm tra phase=Aiming/BreakSetup, đúng lượt của Client (gói này chỉ từ Client) và chưa có lệnh đang xử lý
                bool validPhase = matchStateManager != null && 
                    (matchStateManager.CurrentPhase == MatchPhase.Aiming || matchStateManager.CurrentPhase == MatchPhase.BreakSetup);
                bool clientsTurn = matchStateManager != null && matchStateManager.CurrentPlayerIndex != localPlayerIndex;
                bool notDuplicate = Time.time - masterShotAcceptedAt > 6f;

                if (validPhase && clientsTurn && notDuplicate)
                {
                    masterShotAcceptedAt = Time.time;
                    if (turnManager != null) turnManager.StopShotTimer();

                    Debug.Log($"[NetworkMatchManager] [Master] Phê duyệt ShotCommand từ Client: Power={cmd.power01:F2}, Dir={cmd.direction}. Thực thi authoritative simulation...");
                    
                    // Broadcast cho Client biết để trigger animation
                    BroadcastShotCommand(cmd);

                    // Master hiển thị animation gậy cơ của Client trên màn hình Master
                    if (cueController != null)
                    {
                        cueController.SetAimingState(true);
                        cueController.SetElevation(cmd.cueElevationDeg);
                        cueController.UpdateAim(cmd.direction, cmd.power01);
                        cueController.TriggerStrikeAnimation(() =>
                        {
                            if (cameraDirector != null)
                            {
                                cameraDirector.SwitchToOpponentCamera();
                            }
                            if (shotExecutor != null)
                            {
                                shotExecutor.ExecuteShot(cmd);
                            }
                        });
                    }
                    else if (shotExecutor != null)
                    {
                        shotExecutor.ExecuteShot(cmd);
                    }
                }
                else
                {
                    Debug.LogWarning("[NetworkMatchManager] [Master] Từ chối ShotCommand (sai phase/lượt hoặc trùng lặp)!");
                    SendShotRejected();
                }
            }
            else
            {
                // Client nhận xác nhận thực thi từ Master -> chuyển sang ShotInProgress và chạy hoạt họa đánh!
                Debug.Log("[NetworkMatchManager] [Client] Nhận lệnh ShotCommand từ Master -> chạy hoạt họa đánh!");
                // Client đã dự đoán cú đánh cục bộ (bi đang lăn, phase đã là ShotInProgress): không mở khóa/đổi phase lần nữa
                if (!AwaitingMasterResult)
                {
                    if (ballManager != null)
                    {
                        ballManager.UnlockAllBallsKinematic();
                    }
                    if (matchStateManager != null)
                    {
                        matchStateManager.SetPhase(MatchPhase.ShotInProgress);
                    }
                }
                // Nếu Client đã tự chạy hoạt ảnh đánh ngay khi bấm thì không chạy lại lần 2 (tránh cơ giật 2 lần)
                bool alreadyAnimated = clientLocalStrikeActive;
                if (cueController != null && !alreadyAnimated)
                {
                    cueController.SetAimingState(true);
                    cueController.SetElevation(cmd.cueElevationDeg);
                    cueController.UpdateAim(cmd.direction, cmd.power01);
                    cueController.TriggerStrikeAnimation(() =>
                    {
                        if (cameraDirector != null)
                        {
                            if (matchStateManager != null && matchStateManager.CurrentPlayerIndex == localPlayerIndex)
                            {
                                cameraDirector.SwitchToShotFollowCamera(cmd.direction);
                            }
                            else
                            {
                                cameraDirector.SwitchToOpponentCamera();
                            }
                        }
                    });
                }
            }
        }

        private void SendAimSync(Vector3 aim, float pwr, float elev)
        {
            float[] payload = new float[] { aim.x, aim.z, pwr, elev };
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_AIM_SYNC, payload, options, SendOptions.SendUnreliable);
        }

        private void HandleReceivedAimSync(object rawData)
        {
            if (IsMyTurn) return;
            if (!IsPhaseAllowingRemoteAim()) return; // gói unreliable đến muộn sau khi đã bắn/kết thúc ván
            if (rawData is float[] data && data.Length >= 4)
            {
                Vector3 aimDir = new Vector3(data[0], 0f, data[1]);
                float power = data[2];
                float elevation = data[3];

                if (cueController != null)
                {
                    cueController.SetAimingState(true);
                    cueController.SetElevation(elevation);
                    cueController.UpdateAim(aimDir, power);
                }
            }
        }

        private float lastPreviewSyncTime;

        private void HandleLocalBallInHandMoved(Vector3 pos)
        {
            if (!isOnlineMatch) return;
            if (Time.time - lastPreviewSyncTime < 0.066f) return; // ~15 FPS
            lastPreviewSyncTime = Time.time;

            float[] payload = new float[] { pos.x, pos.y, pos.z };
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_BALL_IN_HAND_PREVIEW, payload, options, SendOptions.SendUnreliable);
        }

        private bool IsPhaseAllowingRemoteAim()
        {
            if (matchStateManager == null) return true;
            var p = matchStateManager.CurrentPhase;
            return p != MatchPhase.ShotInProgress && p != MatchPhase.RackResult && p != MatchPhase.MatchResult;
        }

        private void HandleReceivedBallInHandPreview(object rawData)
        {
            if (IsMyTurn) return;
            if (!IsPhaseAllowingRemoteAim()) return;
            if (rawData is float[] coords && coords.Length >= 3)
            {
                Vector3 pos = new Vector3(coords[0], coords[1], coords[2]);
                if (ballManager != null && ballManager.CueBall != null)
                {
                    ballManager.CueBall.transform.position = pos;
                    if (ballManager.CueBall.Rigidbody != null)
                    {
                        ballManager.CueBall.Rigidbody.position = pos;
                    }
                }
            }
        }

        private void HandleLocalBallInHandConfirmed(Vector3 pos)
        {
            if (!isOnlineMatch) return;
            float[] payload = new float[] { pos.x, pos.y, pos.z };
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_BALL_IN_HAND_CONFIRMED, payload, options, SendOptions.SendReliable);
        }

        private void HandleReceivedBallInHandConfirmed(object rawData)
        {
            // Master chỉ chấp nhận đặt bi từ Client khi đang là lượt của Client
            if (IsMaster && IsMyTurn) return;
            if (rawData is float[] coords && coords.Length >= 3)
            {
                Vector3 pos = new Vector3(coords[0], coords[1], coords[2]);

                // Master thẩm định vị trí Client đặt bi: phải trong mặt bàn và (nếu lỗi khai cuộc) sau vạch đầu bàn.
                // Chỉ kiểm tra khi controller đặt bi của Master đang hoạt động (đúng trạng thái BallInHand) để không làm sai lệch trường hợp hợp lệ.
                if (IsMaster && ballInHandController != null && ballInHandController.IsActive
                    && ballManager != null && ballManager.TableWrapper != null)
                {
                    var tw = ballManager.TableWrapper;
                    Vector3 fixedPos = pos;
                    float r = (ballManager.PhysicsConfig != null) ? ballManager.PhysicsConfig.BallRadius : 0.02855f;
                    if (!tw.IsInsidePlayArea(fixedPos, r + 0.01f)) fixedPos = tw.ClampToPlayArea(fixedPos, 0.035f);
                    if (ballInHandController.BehindHeadStringOnly && !tw.IsBehindHeadString(fixedPos)) fixedPos = tw.HeadSpot;

                    if ((fixedPos - pos).sqrMagnitude > 0.000001f)
                    {
                        Debug.LogWarning($"[NetworkMatchManager] [Master] Vị trí đặt bi của Client không hợp lệ {pos} -> chỉnh về {fixedPos} và gửi lại.");
                        pos = fixedPos;
                        float[] corrected = new float[] { pos.x, pos.y, pos.z };
                        RaiseEventOptions echoOptions = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
                        PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_BALL_IN_HAND_CONFIRMED, corrected, echoOptions, SendOptions.SendReliable);
                    }
                }
                Debug.Log($"[NetworkMatchManager] Nhận vị trí đặt bi cái từ đối thủ: {pos}");
                if (ballManager != null && ballManager.CueBall != null)
                {
                    ballManager.CueBall.ResetTo(pos, Quaternion.identity);
                    if (cameraDirector != null)
                    {
                        cameraDirector.SetCueBallTarget(ballManager.CueBall.transform);
                    }
                }
                if (ballInHandController != null)
                {
                    ballInHandController.CancelBallInHand();
                }
                if (matchStateManager != null)
                {
                    matchStateManager.SetPhase(MatchPhase.Aiming);
                }
                UpdateInputPermissions();
            }
        }

        private void HandleRackStarted()
        {
            if (!isOnlineMatch) return;
            if (IsMaster)
            {
                int[] newRack = BallManager.GenerateWpaRackBallNumbersStatic();
                var props = new ExitGames.Client.Photon.Hashtable { { NetworkConstants.PROP_RACK_BALLS, newRack } };
                PhotonNetwork.CurrentRoom.SetCustomProperties(props);

                RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
                PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_RACK_SYNC, newRack, options, SendOptions.SendReliable);

                if (ballManager != null)
                {
                    ballManager.ResetRack(newRack);
                }
            }
            UpdateInputPermissions();
        }

        private void HandleReceivedRackSync(object rawData)
        {
            if (IsMaster) return;
            if (rawData is int[] rack && rack.Length == 15)
            {
                Debug.Log("[NetworkMatchManager] [Client] Nhận rack mới từ Master. Xếp lại dàn bi...");
                if (ballManager != null)
                {
                    ballManager.ResetRack(rack);
                }
            }
        }

        private void HandleLocalCallShotChanged(int ballNum, int pocketId, bool isSafety)
        {
            if (!isOnlineMatch) return;
            int[] payload = new int[] { ballNum, pocketId, isSafety ? 1 : 0 };
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_CALL_SHOT, payload, options, SendOptions.SendReliable);
        }

        private void HandleReceivedCallShot(object rawData)
        {
            if (rawData is int[] callData && callData.Length >= 3)
            {
                int ballNum = callData[0];
                int pocketId = callData[1];
                bool isSafety = callData[2] == 1;

                if (isSafety)
                {
                    Debug.Log("[NetworkMatchManager] Đối thủ chọn đánh An Toàn (Safety).");
                    if (VFX.VFXManager.Instance != null)
                    {
                        VFX.VFXManager.Instance.SetPocketCallMarker(Vector3.zero, false);
                    }
                    FoulPopupUI.ShowBreakSuccess(LocalizationManager.Get("call_safety_opponent"), "", 2.0f);
                }
                else if (ballNum > 0 && pocketId >= 0)
                {
                    Debug.Log($"[NetworkMatchManager] Đối thủ đã chỉ định bi {ballNum} vào lỗ {pocketId}");
                    if (VFX.VFXManager.Instance != null && ballManager != null && ballManager.TableWrapper != null)
                    {
                        var pocket = ballManager.TableWrapper.GetPocket(pocketId);
                        if (pocket != null)
                        {
                            VFX.VFXManager.Instance.SetPocketCallMarker(pocket.PocketPosition, true);
                        }
                    }
                }
                else
                {
                    if (VFX.VFXManager.Instance != null)
                    {
                        VFX.VFXManager.Instance.SetPocketCallMarker(Vector3.zero, false);
                    }
                }
            }
        }

        public void SendCallShot(int ballNumber, int pocketId)
        {
            if (!isOnlineMatch) return;
            int[] data = new int[] { ballNumber, pocketId, 0 };
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_CALL_SHOT, data, options, SendOptions.SendReliable);
        }

        public void BroadcastNextRack()
        {
            if (!isOnlineMatch || !IsMaster) return;
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_NEXT_RACK, null, options, SendOptions.SendReliable);
            Debug.Log("[NetworkMatchManager] [Master] Đã broadcast EVENT_NEXT_RACK.");
        }

        private void HandleReceivedNextRack(object rawData)
        {
            if (IsMaster) return;
            Debug.Log("[NetworkMatchManager] [Client] Nhận lệnh chuyển ván EVENT_NEXT_RACK từ Master.");
            var resultUI = FindObjectOfType<ResultUI>();
            if (resultUI != null)
            {
                resultUI.AdvanceToNextRackFromMaster();
            }
        }

        public void BroadcastRematch()
        {
            if (!isOnlineMatch || !IsMaster) return;
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_REMATCH, null, options, SendOptions.SendReliable);
            Debug.Log("[NetworkMatchManager] [Master] Đã broadcast EVENT_REMATCH.");
        }

        private void HandleReceivedRematch(object rawData)
        {
            if (IsMaster) return;
            Debug.Log("[NetworkMatchManager] [Client] Nhận lệnh đấu lại EVENT_REMATCH từ Master.");
            var resultUI = FindObjectOfType<ResultUI>();
            if (resultUI != null)
            {
                resultUI.RematchFromMaster();
            }
        }

        #endregion

        #region Surrender Sync

        /// <summary>Người chơi cục bộ đầu hàng ván hiện tại: thông báo cho đối thủ và ghi điểm ván cho đối thủ trên cả hai máy.</summary>
        public void Surrender()
        {
            if (!isOnlineMatch || matchStateManager == null) return;
            if (!CanApplySurrender()) return;

            if (IsMaster)
            {
                MasterResolveSurrender(localPlayerIndex);
            }
            else
            {
                // Client chỉ xin đầu hàng; Master quyết định và broadcast kết quả (tránh cộng điểm đôi khi trùng lúc kết thúc ván)
                RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
                PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_SURRENDER_REQUEST, new int[] { localPlayerIndex }, options, SendOptions.SendReliable);
                Debug.Log($"[NetworkMatchManager] [Client] Đã gửi EVENT_SURRENDER_REQUEST tới Master.");
            }
        }

        private void HandleReceivedSurrenderRequest(object rawData)
        {
            if (!IsMaster) return;
            if (!(rawData is int[] data) || data.Length < 1) return;
            // Chỉ Client (index 1) mới được gửi yêu cầu tới Master
            MasterResolveSurrender(1);
        }

        /// <summary>Master xác nhận đầu hàng (chỉ 1 lần mỗi ván), ghi điểm rồi gửi tỉ số chính xác cho Client.</summary>
        private void MasterResolveSurrender(int surrenderingIndex)
        {
            if (!IsMaster || matchStateManager == null) return;
            if (!CanApplySurrender()) return;

            ApplySurrender(surrenderingIndex);

            var st = matchStateManager.State;
            int winner = (surrenderingIndex == 0) ? 1 : 0;
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_SURRENDER,
                new int[] { surrenderingIndex, winner, st.playerARackWins, st.playerBRackWins }, options, SendOptions.SendReliable);
            Debug.Log($"[NetworkMatchManager] [Master] Player {surrenderingIndex} đầu hàng ván này -> broadcast EVENT_SURRENDER.");
        }

        private void HandleReceivedSurrender(object rawData)
        {
            if (IsMaster) return; // Chỉ Client áp dụng quyết định của Master
            if (!(rawData is int[] data) || data.Length < 4) return;
            int winnerIndex = data[1];
            if (winnerIndex != 0 && winnerIndex != 1) return;
            if (!CanApplySurrender()) return;

            var st = matchStateManager.State;
            st.playerARackWins = data[2];
            st.playerBRackWins = data[3];
            Debug.Log($"[NetworkMatchManager] [Client] Master xác nhận đầu hàng. Người thắng ván: Player {winnerIndex}");
            matchStateManager.ApplyRackEndFromMaster(winnerIndex);
        }

        private bool CanApplySurrender()
        {
            if (matchStateManager == null) return false;
            var phase = matchStateManager.CurrentPhase;
            return phase != MatchPhase.RackResult && phase != MatchPhase.MatchResult;
        }

        private void ApplySurrender(int surrenderingIndex)
        {
            int winnerIndex = (surrenderingIndex == 0) ? 1 : 0;
            matchStateManager.RecordRackWin(winnerIndex);
        }

        #endregion

        #region Disconnect Handling (Rule 124 & 125)

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (!isOnlineMatch) return;

            Debug.LogWarning($"[NetworkMatchManager] Người chơi {otherPlayer.NickName} đã thoát/mất kết nối trong trận đấu (Rule 124)!");
            OpponentLeft = true;

            if (matchStateManager == null) return;

            // Trận đã kết thúc (ví dụ người thắng bấm Về sảnh): giữ nguyên kết quả, chỉ ẩn nút Chơi lại
            if (matchStateManager.CurrentPhase == MatchPhase.MatchResult)
            {
                var statsPanel = FindObjectOfType<StatsPanelUI>();
                if (statsPanel != null) statsPanel.SetLobbyOnly(true);
                return;
            }

            // Rule 124: Bất kỳ player nào rời/mất kết nối trong match: player đó thua match ngay, player còn lại thắng
            // Rule 125: Không tiếp tục match bằng host migration vì yêu cầu sản phẩm là xử thua khi mất kết nối
            int remainingPlayerIndex = localPlayerIndex;
            matchStateManager.RecordMatchWon(remainingPlayerIndex);

            string winMsg = LocalizationManager.Format("net_win_disconnect", otherPlayer.NickName);
            OnMatchDisconnected?.Invoke(winMsg);
            Debug.Log($"[NetworkMatchManager] {winMsg}");
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (!isOnlineMatch) return;

            Debug.LogWarning($"[NetworkMatchManager] Bạn đã mất kết nối tới máy chủ Photon: {cause}");
            string loseMsg = LocalizationManager.Get("net_lose_disconnect");
            LocalDisconnected = true;

            // Rule 124: người mất kết nối thua cả trận (nếu trận chưa kết thúc)
            if (matchStateManager != null
                && matchStateManager.CurrentPhase != MatchPhase.MatchResult
                && !OpponentLeft)
            {
                matchStateManager.RecordMatchWon(localPlayerIndex == 0 ? 1 : 0);
            }
            OnMatchDisconnected?.Invoke(loseMsg);
        }

        #endregion
    }
}
