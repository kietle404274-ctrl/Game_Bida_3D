using System;

namespace Game.Billiards.Core
{
    public enum FoulType
    {
        None,
        CueBallScratch,
        WrongBallFirst,
        NoRailAfterContact,
        BallDrivenOffTable,
        BallsStillMoving,
        BadCueBallPlacement,
        PlayingOutOfTurn,
        ShotClockExpired,
        IllegalBreak,
        DoubleHit,
        PushShot,
        NoBallHit
    }

    public enum RackOutcome
    {
        Continue,
        PlayerAWins,
        PlayerBWins,
        ReRack
    }

    public enum PlayerGroup
    {
        Open,
        Solids,      // Bi trơn 1-7
        Stripes,     // Bi sọc 9-15
        EightBall    // Bi 8 đen
    }

    public enum MatchPhase
    {
        Initializing,
        BreakSetup,
        Aiming,
        ShotInProgress,
        ResolveShot,
        BallInHand,
        RackResult,
        MatchResult
    }
}
