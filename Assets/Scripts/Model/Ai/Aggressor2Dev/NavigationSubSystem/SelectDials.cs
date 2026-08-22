#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI.Helpers.Navigation;
using AI.Helpers.Types;
using Players;
using Ship;

namespace AI.Aggressor2Dev.Navigation
{
    public class DialPlan
    {
        public Dictionary<GenericShip, Maneuver> Values;

        public DialPlan(Dictionary<GenericShip, Maneuver> dials)
        {
            Values = dials;
        }
    }

    public static class PlanDialFns {
        public static IEnumerator CreateDialPlan(DialPlan? writeDialPlanTo)
        {
            List<DialPlan> locationsToSearch = CreateSeedPlans(new());

            throw new NotImplementedException();
        }

        private struct VirtualBoardResult
        {
            public DialPlan Plan;
            public float Score;

            public VirtualBoardResult(DialPlan plan, float score)
            {
                Plan = plan;
                Score = score;
            }
        }

        public static List<DialPlan> CreateSeedPlans(NewVirtualBoard<VirtualBoardData> virtualBoard)
        {
            List<DialPlan> result = new();
            List<GenericShip> myShips = virtualBoard.Ships.Keys.Where(IsShipOnMyTeam).ToList();
            Dictionary<GenericShip, Maneuver> allStraightTwo = new();
            foreach (GenericShip ship in myShips)
            {
                allStraightTwo[ship] = new("2.F.S");
            }
            result.Add(new(allStraightTwo));
            return result;
        }

        private static bool IsShipOnMyTeam(GenericShip ship)
        {
            GenericPlayer CurrentPlayer = Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer);
            return ship.Owner == CurrentPlayer;
        }
    }
}