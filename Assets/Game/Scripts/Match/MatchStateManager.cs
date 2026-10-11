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

        public void RecordRackWin(int winnerIndex)
        {
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

        public void StartNextRack()
        {
            matchState.rackNumber++;
            matchState.ResetForNewRack();
            SetPhase(MatchPhase.BreakSetup);
        }
    }
}
