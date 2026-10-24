using System;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Physics;
using Game.Billiards.Rules;

namespace Game.Billiards.Match
{
    /// <summary>
    /// Thu thập thống kê trận đấu (số cú, bi vào lỗ, lỗi, khai cuộc, chuỗi...).
    /// Chỉ Offline / Master Online tính toán; Client Online nhận snapshot từ Master (EVENT_STATS_SYNC).
    /// </summary>
    public class MatchStatsTracker : MonoBehaviour
    {
        private MatchStateManager stateManager;
        private TurnManager turnManager;
        private ShotExecutor shotExecutor;
        private Wpa8BallRuleEngine ruleEngine;
        private BallManager ballManager;
        private Network.NetworkMatchManager networkManager;

        public MatchStats Stats { get; } = new MatchStats();

        /// <summary>Phát ra khi số liệu thay đổi (UI cập nhật).</summary>
        public event Action OnStatsChanged;

        private struct PendingShot
        {
            public bool valid;
            public int shooter;
            public bool isBreak;
            public bool safety;
            public int calledBall;
        }

        private PendingShot pending;

        // Theo dõi lượt tới bàn (visit)
        private bool visitActive;
        private int visitOwner = -1;
        private int visitPocketed;
        private int visitRemainingAtStart;
        private bool visitBih;
        private bool visitBihCounted;
        private readonly bool[] bihPending = new bool[2];

        // Theo dõi ván
        private int rackBreaker = -1;
        private readonly int[] rackShots = new int[2];
        private float aimStartTime;
        private bool matchFinished;

        private bool IsAuthority =>
            networkManager == null || !networkManager.IsOnlineMatch || networkManager.IsMaster;

        public void Initialize(MatchStateManager sMgr, TurnManager tMgr, ShotExecutor executor, Wpa8BallRuleEngine rules, BallManager bMgr, Network.NetworkMatchManager netMgr)
        {
            Unsubscribe();

            stateManager = sMgr;
            turnManager = tMgr;
            shotExecutor = executor;
            ruleEngine = rules;
            ballManager = bMgr;
            networkManager = netMgr;

            Stats.ResetAll();
            ResetRackTracking();
            aimStartTime = Time.time;

            if (stateManager != null)
            {
                stateManager.OnPhaseChanged += HandlePhaseChanged;
                stateManager.OnRackStarted += HandleRackStarted;
                stateManager.OnMatchWon += HandleMatchWon;
            }
            if (shotExecutor != null) shotExecutor.OnShotExecuted += HandleShotExecuted;
            if (turnManager != null) turnManager.OnShotEvaluated += HandleShotEvaluated;
            if (networkManager != null) networkManager.OnStatsReceived += HandleStatsReceived;
        }

        private void Unsubscribe()
        {
            if (stateManager != null)
            {
                stateManager.OnPhaseChanged -= HandlePhaseChanged;
                stateManager.OnRackStarted -= HandleRackStarted;
                stateManager.OnMatchWon -= HandleMatchWon;
            }
            if (shotExecutor != null) shotExecutor.OnShotExecuted -= HandleShotExecuted;
            if (turnManager != null) turnManager.OnShotEvaluated -= HandleShotEvaluated;
            if (networkManager != null) networkManager.OnStatsReceived -= HandleStatsReceived;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (stateManager == null) return;
            var phase = stateManager.CurrentPhase;
            if (phase == MatchPhase.Initializing || phase == MatchPhase.RackResult || phase == MatchPhase.MatchResult) return;
            Stats.matchTime += Time.deltaTime;
            Stats.rackTime += Time.deltaTime;
        }

        private void ResetRackTracking()
        {
            pending = default;
            visitActive = false;
            visitOwner = -1;
            visitPocketed = 0;
            visitBih = false;
            visitBihCounted = false;
            bihPending[0] = bihPending[1] = false;
            rackBreaker = -1;
            rackShots[0] = rackShots[1] = 0;
        }

        private void HandlePhaseChanged(MatchPhase phase)
        {
            if (phase == MatchPhase.Aiming) aimStartTime = Time.time;
        }

        private void HandleMatchWon(int winner)
        {
            matchFinished = true;
        }

        private void HandleRackStarted()
        {
            // Chơi lại sau khi kết thúc trận: xoá luôn thống kê cả trận
            if (matchFinished)
            {
                matchFinished = false;
                Stats.ResetAll();
            }
            Stats.ResetRack();
            ResetRackTracking();
            PublishAndSync();
        }

        private void HandleStatsReceived(float[] data)
        {
            if (IsAuthority) return;
            if (Stats.Unpack(data)) OnStatsChanged?.Invoke();
        }

        private void HandleShotExecuted(ShotCommand cmd)
        {
            if (!IsAuthority || stateManager == null) return;

            var state = stateManager.State;
            int shooter = state.currentPlayerIndex;

            pending = new PendingShot
            {
                valid = true,
                shooter = shooter,
                isBreak = state.turnSequence == 0,
                safety = cmd.calledSafety,
                calledBall = cmd.calledBallNumber
            };

            if (rackBreaker < 0 && pending.isBreak) rackBreaker = shooter;
            if (shooter >= 0 && shooter <= 1) rackShots[shooter]++;

            // Bắt đầu lượt tới bàn mới
            if (!visitActive || visitOwner != shooter)
            {
                visitActive = true;
                visitOwner = shooter;
                visitPocketed = 0;
                visitRemainingAtStart = RemainingBallsFor(state);
                Stats.Add(shooter, StatId.Visits);

                visitBih = bihPending[shooter];
                visitBihCounted = false;
                bihPending[shooter] = false;
                if (visitBih) Stats.Add(shooter, StatId.BihChances);
            }

            Stats.Add(shooter, StatId.Shots);
            Stats.Add(shooter, StatId.PowerSum, cmd.power01);
            if (cmd.cueContact01.sqrMagnitude > 0.01f) Stats.Add(shooter, StatId.SpinShots);
            Stats.Add(shooter, StatId.ThinkSum, Mathf.Max(0f, Time.time - aimStartTime));
        }

        private int RemainingBallsFor(MatchState state)
        {
            var group = state.CurrentShooterGroup;
            if (group == PlayerGroup.Solids || group == PlayerGroup.Stripes)
            {
                return ruleEngine != null ? ruleEngine.CountRemainingBalls(group, ballManager) : 7;
            }
            return group == PlayerGroup.EightBall ? 0 : 7;
        }

        private void HandleShotEvaluated(ShotResult res)
        {
            if (!IsAuthority || stateManager == null || res == null) return;

            // Hết giờ không có cú đánh nào được thực thi
            if (!pending.valid)
            {
                int timedOut = stateManager.State.currentPlayerIndex;
                if (res.HasFoul) RecordFoul(timedOut, res.foul);
                EndVisit(timedOut, res, false);
                PublishAndSync();
                return;
            }

            PendingShot p = pending;
            pending = default;
            int s = p.shooter;

            int pocketed = 0;
            if (res.pocketedBalls != null)
            {
                for (int i = 0; i < res.pocketedBalls.Count; i++)
                    if (res.pocketedBalls[i] != 0) pocketed++;
            }

            bool foul = res.HasFoul;
            bool scored = !foul && pocketed > 0;

            if (p.safety)
            {
                Stats.Add(s, StatId.Safeties);
                if (!foul) Stats.Add(s, StatId.SafetiesOk);
            }
            else if (p.isBreak)
            {
                Stats.Add(s, StatId.Breaks);
                if (!foul) Stats.Add(s, StatId.BreakLegal);
                Stats.Add(s, StatId.BreakPocketed, pocketed);
            }
            else
            {
                Stats.Add(s, StatId.PotAttempts);
                if (scored) Stats.Add(s, StatId.PotSuccess);
                if (p.calledBall >= 0)
                {
                    Stats.Add(s, StatId.CalledAttempts);
                    if (!foul && res.calledShotMade) Stats.Add(s, StatId.CalledSuccess);
                }
            }

            Stats.Add(s, StatId.BallsPocketed, pocketed);

            if (scored)
            {
                visitPocketed += pocketed;
                Stats.Max(s, StatId.LongestRun, visitPocketed);

                if (visitBih && !visitBihCounted)
                {
                    visitBihCounted = true;
                    Stats.Add(s, StatId.BihConverted);
                }
            }

            if (foul) RecordFoul(s, res.foul);

            if (res.rackEnded)
            {
                HandleRackEndStats(s, res, foul, p);
            }

            bool continues = !foul && !res.rackEnded && res.calledShotMade && !p.safety;
            EndVisit(s, res, continues);

            PublishAndSync();
        }

        private void HandleRackEndStats(int shooter, ShotResult res, bool foul, PendingShot p)
        {
            int winner = res.outcome == RackOutcome.PlayerAWins ? 0 : res.outcome == RackOutcome.PlayerBWins ? 1 : -1;
            if (winner < 0) return;

            Stats.Add(winner, StatId.RacksWon);

            if (winner == shooter && !foul)
            {
                // Dọn bàn: ăn hết bi nhóm mình + bi 8 trong cùng một lượt
                if (visitPocketed >= visitRemainingAtStart + 1)
                {
                    Stats.Add(winner, StatId.RunOuts);

                    // Khai cuộc rồi dọn bàn: người khai cuộc thắng ván khi đối thủ chưa đánh cú nào
                    if (winner == rackBreaker && rackShots[1 - winner] == 0)
                        Stats.Add(winner, StatId.BreakAndRuns);
                }
            }
        }

        private void EndVisit(int shooter, ShotResult res, bool continues)
        {
            if (!continues)
            {
                visitActive = false;
                if (res.HasFoul && !res.rackEnded && shooter >= 0 && shooter <= 1)
                {
                    bihPending[1 - shooter] = true;
                }
            }
        }

        private void RecordFoul(int player, FoulType foul)
        {
            Stats.Add(player, StatId.Fouls);
            switch (foul)
            {
                case FoulType.CueBallScratch: Stats.Add(player, StatId.FoulScratch); break;
                case FoulType.WrongBallFirst: Stats.Add(player, StatId.FoulWrongBall); break;
                case FoulType.NoRailAfterContact: Stats.Add(player, StatId.FoulNoRail); break;
                case FoulType.BallDrivenOffTable: Stats.Add(player, StatId.FoulOffTable); break;
                case FoulType.ShotClockExpired: Stats.Add(player, StatId.FoulTimeout); break;
                case FoulType.IllegalBreak: Stats.Add(player, StatId.FoulIllegalBreak); break;
                default: Stats.Add(player, StatId.FoulOther); break;
            }
        }

        private void PublishAndSync()
        {
            OnStatsChanged?.Invoke();

            if (networkManager != null && networkManager.IsOnlineMatch && networkManager.IsMaster)
            {
                networkManager.BroadcastStats(Stats.Pack());
            }
        }
    }
}
