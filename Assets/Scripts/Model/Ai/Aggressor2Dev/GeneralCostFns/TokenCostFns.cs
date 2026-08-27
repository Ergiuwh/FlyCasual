#nullable enable

using System;
using AI.Aggressor2Dev.Navigation;
using AI.Helpers.Navigation;
using Ship;

namespace AI.Aggressor2Dev.CostFns
{
    public static class HullCost
    {
        public static float Hull(GenericShip ship, int quantity, NewVirtualBoard<VirtualBoardData> virtualBoard)
        {
            return 100 * ship.PilotInfo.Cost * quantity / ship.State.HullMax;
        }

        public static float Shields(GenericShip ship, int quantity, NewVirtualBoard<VirtualBoardData> virtualBoard)
        {
            return 100 * ship.PilotInfo.Cost * quantity / ship.State.ShieldsMax;
        }
    }

    public static class TokenCostFns
    {
        public static float Stress(GenericShip ship, int quantity, NewVirtualBoard<VirtualBoardData> virtualBoard)
        {
            throw new NotImplementedException();
        }
    }

    public static class ActionValueCost
    {
        
    }
}