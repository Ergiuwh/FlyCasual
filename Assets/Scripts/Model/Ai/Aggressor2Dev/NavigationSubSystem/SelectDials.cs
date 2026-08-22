#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using AI.Helpers.Navigation;
using AI.Helpers.Types;
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
        public static IEnumerator CreateDialPlan(out DialPlan? writeDialPlanTo)
        {
            throw new NotImplementedException();

            writeDialPlanTo = new(new());
        }

        public static List<VirtualBoardWrapper<VirtualBoardData>> CreateSeedPoints(VirtualBoardWrapper<VirtualBoardData> virtualBoard)
        {
            throw new NotImplementedException();
        }
    }
}