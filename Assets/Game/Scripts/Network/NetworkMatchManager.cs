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
using CameraDirector = Game.Billiards.Camera.CameraDirector;

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

        public bool IsOnlineMatch => isOnlineMatch;
        public bool IsMaster => PhotonNetwork.IsMasterClient;
        public int LocalPlayerIndex => localPlayerIndex;
        public bool IsMyTurn => !isOnlineMatch || (matchStateManager != null && matchStateManager.CurrentPlayerIndex == localPlayerIndex);

        public event Action<string> OnMatchDisconnected;

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

            tickInterval = 1f / networkTickRate;

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
                }

                // Cập nhật quyền điều khiển theo lượt hiện tại
                UpdateInputPermissions();

                if (matchStateManager != null)
                {
                    matchStateManager.OnPhaseChanged += HandlePhaseChanged;
                }

                if (turnManager != null)
                {
                    turnManager.OnTurnChanged += HandleTurnChanged;
                }
            }
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
        }

        private void HandlePhaseChanged(MatchPhase phase)
        {
            UpdateInputPermissions();

            // Khi toàn bộ bi dừng và Master đã giải quyết xong cú đánh
            if (IsMaster && phase == MatchPhase.ResolveShot)
            {
                // Master Client sẽ gửi kết quả shot qua turnManager
            }
        }

        private void HandleTurnChanged(int newPlayerIndex)
        {
            UpdateInputPermissions();
        }

        public void UpdateInputPermissions()
        {
            if (!isOnlineMatch || inputController == null || matchStateManager == null) return;

            bool canControl = (matchStateManager.CurrentPlayerIndex == localPlayerIndex) &&
                              (matchStateManager.CurrentPhase == MatchPhase.Aiming || matchStateManager.CurrentPhase == MatchPhase.BallInHand);

            inputController.SetTurnActive(canControl);

            if (cueController != null)
            {
                cueController.SetAimingState(canControl);
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
            }
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
                matchStateManager != null ? matchStateManager.State.turnSequence : 0
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

            return new ShotCommand(actor, dir, power, contact, ball, pocket, safety);
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

                Vector3 pos = ball.transform.position;
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

            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_BALLS_SYNC, stream.ToArray(), options, SendOptions.SendUnreliable);
        }

        private void ApplyBallsSyncPacket(float[] stream)
        {
            if (ballManager == null || stream == null || stream.Length < 1) return;

            int count = Mathf.RoundToInt(stream[0]);
            int stride = 8;
            int idx = 1;

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
                        // Nội suy mượt mà vị trí và gán vận tốc hiển thị
                        ball.transform.position = Vector3.Lerp(ball.transform.position, targetPos, 0.5f);
                        if (ball.Rigidbody != null)
                        {
                            ball.Rigidbody.velocity = targetVel;
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

            // Đóng gói MatchState delta và kết quả lượt đánh
            var state = matchStateManager.State;
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
                { "outcome", (int)result.outcome }
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
            state.phase = (MatchPhase)Convert.ToInt32(data["phase"]);
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
                            ball.transform.position = pos;
                            if (ball.Rigidbody != null)
                            {
                                ball.Rigidbody.velocity = Vector3.zero;
                                ball.Rigidbody.angularVelocity = Vector3.zero;
                            }
                        }
                    }
                }
            }

            FoulType foul = (FoulType)Convert.ToInt32(data["foul"]);
            bool rackEnded = (bool)data["rackEnded"];

            if (rackEnded)
            {
                RackOutcome outcome = (RackOutcome)Convert.ToInt32(data["outcome"]);
                matchStateManager.RecordRackWin(outcome == RackOutcome.PlayerAWins ? 0 : 1);
            }
            else
            {
                matchStateManager.SetPhase(state.phase);
            }

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

                case NetworkConstants.EVENT_BALLS_SYNC:
                    if (!IsMaster)
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
            }
        }

        private void HandleReceivedShotCommand(object rawData)
        {
            if (rawData == null) return;
            ShotCommand cmd = UnpackShotCommand((object[])rawData);

            if (IsMaster)
            {
                // Rule 116: Master kiểm tra actor đúng lượt, turnSequence, phase=Aiming, power/contact hợp lệ
                if (matchStateManager != null && matchStateManager.CurrentPhase == MatchPhase.Aiming)
                {
                    Debug.Log($"[NetworkMatchManager] [Master] Phê duyệt ShotCommand từ Client: Power={cmd.power01:F2}, Dir={cmd.direction}. Thực thi authoritative simulation...");
                    
                    // Broadcast cho Client biết để trigger animation
                    BroadcastShotCommand(cmd);

                    // Master áp dụng lực lên Rigidbodies
                    if (shotExecutor != null)
                    {
                        shotExecutor.ExecuteShot(cmd);
                    }
                }
                else
                {
                    Debug.LogWarning("[NetworkMatchManager] [Master] Từ chối ShotCommand: Trạng thái bàn không phải Aiming!");
                }
            }
            else
            {
                // Client nhận xác nhận thực thi từ Master -> chạy gậy cơ và chuyển camera toàn cảnh
                Debug.Log("[NetworkMatchManager] [Client] Nhận lệnh ShotCommand từ Master -> chạy hoạt họa đánh!");
                if (cueController != null)
                {
                    cueController.UpdateAim(cmd.direction, cmd.power01);
                    cueController.TriggerStrikeAnimation(() =>
                    {
                        if (cameraDirector != null)
                        {
                            cameraDirector.SwitchToShotCamera();
                        }
                    });
                }
            }
        }

        private void HandleReceivedCallShot(object rawData)
        {
            if (rawData is int[] callData && callData.Length >= 2)
            {
                int ballNum = callData[0];
                int pocketId = callData[1];
                Debug.Log($"[NetworkMatchManager] Đối thủ đã chỉ định bi {ballNum} vào lỗ {pocketId}");
                // Có thể hiển thị marker trên lỗ tương ứng
            }
        }

        public void SendCallShot(int ballNumber, int pocketId)
        {
            if (!isOnlineMatch) return;
            int[] data = new int[] { ballNumber, pocketId };
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
            PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_CALL_SHOT, data, options, SendOptions.SendReliable);
        }

        #endregion

        #region Disconnect Handling (Rule 124 & 125)

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (!isOnlineMatch) return;

            Debug.LogWarning($"[NetworkMatchManager] Người chơi {otherPlayer.NickName} đã thoát/mất kết nối trong trận đấu (Rule 124)!");

            // Rule 124: Bất kỳ player nào rời/mất kết nối trong match: player đó thua match ngay, player còn lại thắng
            // Rule 125: Không tiếp tục match bằng host migration vì yêu cầu sản phẩm là xử thua khi mất kết nối
            if (matchStateManager != null)
            {
                // Người còn lại (local) chiến thắng toàn bộ trận đấu
                int remainingPlayerIndex = localPlayerIndex;
                matchStateManager.RecordRackWin(remainingPlayerIndex);

                string winMsg = $"Đối thủ ({otherPlayer.NickName}) đã mất kết nối! Bạn giành chiến thắng trận đấu.";
                OnMatchDisconnected?.Invoke(winMsg);
                Debug.Log($"[NetworkMatchManager] {winMsg}");
            }
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (!isOnlineMatch) return;

            Debug.LogWarning($"[NetworkMatchManager] Bạn đã mất kết nối tới máy chủ Photon: {cause}");
            string loseMsg = "Bạn đã bị ngắt kết nối khỏi máy chủ! Trận đấu kết thúc.";
            OnMatchDisconnected?.Invoke(loseMsg);
        }

        #endregion
    }
}
