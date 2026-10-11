using System;

namespace Game.Billiards.Core
{
    public enum RaceTarget
    {
        RaceTo3 = 3,
        RaceTo5 = 5,
        RaceTo7 = 7,
        RaceTo9 = 9
    }

    public enum ShotTimerOption
    {
        Unlimited = 0,
        Seconds30 = 30,
        Seconds45 = 45,
        Seconds60 = 60
    }

    [Serializable]
    public struct MatchSettings
    {
        public RaceTarget raceTarget;
        public ShotTimerOption shotTimer;
        public bool useWpaRules;

        public static MatchSettings Default => new MatchSettings
        {
            raceTarget = RaceTarget.RaceTo5,
            shotTimer = ShotTimerOption.Unlimited,
            useWpaRules = true
        };
    }

    [Serializable]
    public struct PlayerState
    {
        public string displayName;
        public bool isAI;
        public int actorId;
        public bool isConnected;
        public bool isReady;

        public PlayerState(string name, bool isAi, int actor = 0)
        {
            displayName = name;
            this.isAI = isAi;
            actorId = actor;
            isConnected = true;
            isReady = true;
        }
    }

    [Serializable]
    public class MatchState
    {
        public MatchPhase phase = MatchPhase.BreakSetup;
        public int currentPlayerIndex = 0; // 0: Player A, 1: Player B
        public PlayerState[] players = new PlayerState[2];
        public int rackNumber = 1;
        public int playerARackWins = 0;
        public int playerBRackWins = 0;
        public PlayerGroup playerAGroup = PlayerGroup.Open;
        public PlayerGroup playerBGroup = PlayerGroup.Open;
        public bool tableOpen = true;
        public MatchSettings settings = MatchSettings.Default;
        public int turnSequence = 0;

        public PlayerGroup CurrentShooterGroup => (currentPlayerIndex == 0) ? playerAGroup : playerBGroup;
        public PlayerGroup CurrentOpponentGroup => (currentPlayerIndex == 0) ? playerBGroup : playerAGroup;

        public void SwitchTurn()
        {
            currentPlayerIndex = (currentPlayerIndex == 0) ? 1 : 0;
            turnSequence++;
        }

        public void AssignGroups(PlayerGroup shooterGroup)
        {
            tableOpen = false;
            if (currentPlayerIndex == 0)
            {
                playerAGroup = shooterGroup;
                playerBGroup = (shooterGroup == PlayerGroup.Solids) ? PlayerGroup.Stripes : PlayerGroup.Solids;
            }
            else
            {
                playerBGroup = shooterGroup;
                playerAGroup = (shooterGroup == PlayerGroup.Solids) ? PlayerGroup.Stripes : PlayerGroup.Solids;
            }
        }

        public void ResetForNewRack()
        {
            phase = MatchPhase.BreakSetup;
            tableOpen = true;
            playerAGroup = PlayerGroup.Open;
            playerBGroup = PlayerGroup.Open;
            turnSequence = 0;
        }
    }
}
