#nullable enable

using AI.Aggressor2Dev.Navigation;
using AI.Aggressor2Dev.Targeting;
using AI.Helpers.Navigation;
using Ship;

namespace AI.Aggressor2Dev.OpponentPrediction
{
    public static class PredictAttackFns
    {
        public static AttackResult? PredictAttack(GenericShip ship, NewVirtualBoard<VirtualBoardData> virtualBoard)
        {
            return TargetingFns.SelectBestAttack(ship, virtualBoard);
        }
    }
}