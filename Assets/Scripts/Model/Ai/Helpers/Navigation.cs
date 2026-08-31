#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI.Helpers.Types;
using Movement;
using Ship;

namespace AI.Helpers.Navigation
{
    public static class NavFunctions
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

        public static IEnumerator ApplyManeuverOnVirtualBoard<T>(NewVirtualBoard<T> virtualBoard, GenericShip ship, Maneuver maneuver) where T : class, ICloneable {
            yield return ApplyManeuverOnVirtualBoard<T>(virtualBoard, ship, ShipMovementScript.MovementFromString(maneuver.ToString()));
        }

        public static IEnumerator ApplyManeuverOnVirtualBoard<T>(NewVirtualBoard<T> virtualBoard, GenericShip ship, string maneuver) where T : class, ICloneable {
            yield return ApplyManeuverOnVirtualBoard<T>(virtualBoard, ship, ShipMovementScript.MovementFromString(maneuver));
        }

        public static IEnumerator ApplyManeuverOnVirtualBoard<T>(NewVirtualBoard<T> virtualBoard, GenericShip ship, GenericMovement movement) where T : class, ICloneable {
            virtualBoard.AssertIsInVirtualPosition();

            MovementPrediction prediction = new(ship, movement);

            yield return prediction.CalculateMovementPredicition();

            virtualBoard.GetShipInterface(ship).SetPosition(prediction.FinalPositionInfo);
        }
    }
}