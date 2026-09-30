#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI.Helpers.Navigation.PredictionHelpers;
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


        public static IEnumerator ApplyManeuverOnVirtualBoard<T>(VirtualBoard<T> virtualBoard, GenericShip ship, Maneuver maneuver, bool isSimple) where T : class {
            GenericMovement movement = CreateMovement(ship, maneuver, isSimple);

            yield return ApplyManeuverOnVirtualBoard(virtualBoard, movement);
        }

        public static IEnumerator ApplyManeuverOnVirtualBoard<T>(VirtualBoard<T> virtualBoard, GenericShip ship, string maneuverCode, bool isSimple) where T : class {
            GenericMovement movement = CreateMovement(ship, maneuverCode, isSimple);

            yield return ApplyManeuverOnVirtualBoard(virtualBoard, movement);
        }

        public static IEnumerator ApplyManeuverOnVirtualBoard<T>(VirtualBoard<T> virtualBoard, GenericMovement movement) where T : class {
            virtualBoard.AssertIsActive();

            yield return SingleMovementPredictionHelper.Calculate(movement);
            MovementPrediction prediction = SingleMovementPredictionHelper.Prediction;

            virtualBoard.GetShipInterface(movement.TheShip).SetPosition(prediction.FinalPositionInfo);
        }


        public static GenericMovement CreateMovement(GenericShip ship, Maneuver maneuver, bool isSimple = true)
        {
            return CreateMovement(ship, maneuver.ToString(), isSimple, maneuver.GetComplexity());
        }

        public static GenericMovement CreateMovement(GenericShip ship, string maneuverCode, bool isSimple = true, MovementComplexity? complexity = null)
        {
            GenericMovement movement = ShipMovementScript.MovementFromString(maneuverCode);
            movement.TheShip = ship;
            movement.Initialize();
            movement.IsSimple = isSimple;
            if (complexity != null) {
                movement.ColorComplexity = (MovementComplexity)complexity;
            }
            return movement;
        }


        /// <summary>
        /// Only calculates FinalPositionInfo and FinalPositionBeforeRotationInfo.
        /// </summary>
        /// <param name="movement"></param>
        /// <returns></returns>
        public static MovementPrediction PredictFinalPosition(GenericMovement movement)
        {
            Selection.ThisShip = movement.TheShip;
            GenericMovement savedMovement = movement.TheShip.AssignedManeuver;

            movement.TheShip.SetAssignedManeuver(movement, isSilent: true);

            MovementPrediction prediction = new(movement.TheShip, movement, MovementPrediction.PredictionType.EndpointPositionOnly);
            prediction.CalculateOnlyFinalPositionIgnoringCollisions();

            movement.TheShip.SetAssignedManeuver(savedMovement, isSilent: true);

            return prediction;
        }

        /// <summary>
        /// Only calculates FinalPositionInfo and FinalPositionBeforeRotationInfo.
        /// </summary>
        /// <param name="ship"></param>
        /// <param name="maneuver"></param>
        /// <returns></returns>
        public static MovementPrediction PredictFinalPosition(GenericShip ship, Maneuver maneuver)
        {
            return PredictFinalPosition(ship, maneuver.ToString());
        }

        /// <summary>
        /// Only calculates FinalPositionInfo and FinalPositionBeforeRotationInfo.
        /// </summary>
        /// <param name="ship"></param>
        /// <param name="maneuverCode"></param>
        /// <returns></returns>
        public static MovementPrediction PredictFinalPosition(GenericShip ship, string maneuverCode)
        {
            // isSimple is not currently used by CalculateOnlyFinalPositionIgnoringCollisions()
            GenericMovement movement = CreateMovement(ship, maneuverCode, true);
            return PredictFinalPosition(movement);
        }
    }
}