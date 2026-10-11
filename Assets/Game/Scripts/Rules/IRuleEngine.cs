using Game.Billiards.Core;
using Game.Billiards.Match;

namespace Game.Billiards.Rules
{
    public interface IRuleEngine
    {
        ShotResult EvaluateShot(ShotCommand command, ShotResult rawResult, MatchState matchState, BallManager ballManager);
    }
}
