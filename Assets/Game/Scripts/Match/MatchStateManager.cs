using System;
using UnityEngine;
using Game.Billiards.Core;

namespace Game.Billiards.Match
{
    public class MatchStateManager : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private MatchState matchState = new MatchState();

        public MatchState State => matchState;
        public MatchPhase CurrentPhase => matchState.phase;
        public int CurrentPlayerIndex => matchState.currentPlayerIndex;
        public bool IsTableOpen => matchState.tableOpen;

        public event Action<MatchPhase> OnPhaseChanged;
        public event Action<int, int> OnRackScoreChanged;
        public event Action<RackOutcome> OnRackEnded;
        public event Action<int> OnMatchWon;

        /// <summary>Phát sau khi một ván mới đã sẵn sàng (bảng điểm, nhóm bi, lượt đã reset) để UI / điều khiển đồng bộ lại.</summary>
        public event Action OnRackStarted;

        public void RaiseRackStarted()
        {
            OnRackStarted?.Invoke();
        }

        public void Initialize(MatchSettings settings, string playerAName = "Player 1", string playerBName = "AI / Player 2", bool playerBIsAi = false)
        {
            matchState = new MatchState
            {
                settings = settings,
                players = new[]
                {
                    new PlayerState(playerAName, false, 0),
                    new PlayerState(playerBName, playerBIsAi, 1)
                }
            };
            matchState.ResetForNewRack();
        }

        public void SetPhase(MatchPhase newPhase)
        {
            if (matchState.phase != newPhase)
            {
                matchState.phase = newPhase;
                OnPhaseChanged?.Invoke(newPhase);
            }
        }

        private int lastRackWinnerIndex = -1;

        public void RecordRackWin(int winnerIndex)
        {
            lastRackWinnerIndex = winnerIndex;
            if (winnerIndex == 0)
            {
                matchState.playerARackWins++;
            }
            else
            {
                matchState.playerBRackWins++;
            }

            OnRackScoreChanged?.Invoke(matchState.playerARackWins, matchState.playerBRackWins);

            int targetWins = (int)matchState.settings.raceTarget;
            if (matchState.playerARackWins >= targetWins || matchState.playerBRackWins >= targetWins)
            {
                SetPhase(MatchPhase.MatchResult);
                OnMatchWon?.Invoke(winnerIndex);
            }
            else
            {
                SetPhase(MatchPhase.RackResult);
                OnRackEnded?.Invoke(winnerIndex == 0 ? RackOutcome.PlayerAWins : RackOutcome.PlayerBWins);
            }
        }

        /// <summary>
        /// Client online: áp kết thúc ván do Master thông báo. Tỉ số đã được Master gửi (gồm ván vừa thắng) nên KHÔNG cộng điểm lại,
        /// chỉ ghi người thắng, cập nhật bảng điểm và phát sự kiện kết thúc ván/trận như RecordRackWin.
        /// </summary>
        public void ApplyRackEndFromMaster(int winnerIndex)
        {
            lastRackWinnerIndex = winnerIndex;
            OnRackScoreChanged?.Invoke(matchState.playerARackWins, matchState.playerBRackWins);

            int targetWins = (int)matchState.settings.raceTarget;
            if (matchState.playerARackWins >= targetWins || matchState.playerBRackWins >= targetWins)
            {
                SetPhase(MatchPhase.MatchResult);
                OnMatchWon?.Invoke(winnerIndex);
            }
            else
            {
                SetPhase(MatchPhase.RackResult);
                OnRackEnded?.Invoke(winnerIndex == 0 ? RackOutcome.PlayerAWins : RackOutcome.PlayerBWins);
            }
        }

        /// <summary>Xử thắng toàn bộ trận đấu ngay lập tức (dành cho trường hợp đối thủ rời trận hoặc đầu hàng trận đấu).</summary>
        public void RecordMatchWon(int winnerIndex)
        {
            lastRackWinnerIndex = winnerIndex;
            int targetWins = (int)matchState.settings.raceTarget;
            if (winnerIndex == 0) matchState.playerARackWins = targetWins;
            else matchState.playerBRackWins = targetWins;

            OnRackScoreChanged?.Invoke(matchState.playerARackWins, matchState.playerBRackWins);
            SetPhase(MatchPhase.MatchResult);
            OnMatchWon?.Invoke(winnerIndex);
        }

        public void StartNextRack()
        {
            matchState.rackNumber++;
            int nextBreaker = (lastRackWinnerIndex >= 0) ? lastRackWinnerIndex : 0;
            matchState.ResetForNewRack(nextBreaker);
            // ResetForNewRack đã gán phase = BreakSetup trực tiếp nên SetPhase sẽ không phát sự kiện (phase không đổi).
            // Phát thủ công để AI / TurnManager / HUD nhận biết ván mới (nếu không AI sẽ đứng yên, không phá bi).
            OnPhaseChanged?.Invoke(MatchPhase.BreakSetup);
            OnRackStarted?.Invoke();
        }
    }
}
