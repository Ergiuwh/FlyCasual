using System.Collections.Generic;
using System.Linq;
using Movement;
using Ship;

namespace AI.Helpers.Navigation
{
    public static class Functions
    {
        public static List<string> GetShortestTurnManeuvers(GenericShip ship)
        {
            List<string> bestTurnManeuvers = new();

            ManeuverHolder bestLeftTurnManeuver = ship.GetManeuverHolders()
                .Where(n =>
                    n.Bearing == ManeuverBearing.Turn
                    && n.Direction == ManeuverDirection.Left
                )
                .OrderBy(n => n.SpeedIntUnsigned)
                .FirstOrDefault();
            if(bestLeftTurnManeuver.Bearing == ManeuverBearing.Turn)
                bestTurnManeuvers.Add(bestLeftTurnManeuver.ToString());

            ManeuverHolder bestRightTurnManeuver = ship.GetManeuverHolders()
                .Where(n =>
                    n.Bearing == ManeuverBearing.Turn
                    && n.Direction == ManeuverDirection.Right
                )
                .OrderBy(n => n.SpeedIntUnsigned)
                .FirstOrDefault();
            if (bestRightTurnManeuver.Bearing == ManeuverBearing.Turn)
                bestTurnManeuvers.Add(bestRightTurnManeuver.ToString());

            return bestTurnManeuvers;
        }
    }
}