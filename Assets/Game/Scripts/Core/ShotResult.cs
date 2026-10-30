using System;
using System.Collections.Generic;

namespace Game.Billiards.Core
{
    [Serializable]
    public class ShotResult
    {
        public int firstContactBall = -1;
        public List<int> initialContactCandidates = new List<int>();
        public List<int> pocketedBalls = new List<int>();
        public Dictionary<int, int> pocketedBallPockets = new Dictionary<int, int>();
        public List<int> ballsThatHitRail = new List<int>();
        public List<int> ballsThatHitRailAfterContact = new List<int>();
        public bool cueBallScratch = false;
        public bool anyBallOffTable = false;
        public List<int> ballsDrivenOffTable = new List<int>();
        public FoulType foul = FoulType.None;
        public bool calledShotMade = false;
        public bool rackEnded = false;
        public RackOutcome outcome = RackOutcome.Continue;

        public bool HasFoul => foul != FoulType.None;

        public ShotResult()
        {
            initialContactCandidates = new List<int>();
            pocketedBalls = new List<int>();
            pocketedBallPockets = new Dictionary<int, int>();
            ballsThatHitRail = new List<int>();
            ballsThatHitRailAfterContact = new List<int>();
            ballsDrivenOffTable = new List<int>();
        }
    }
}
