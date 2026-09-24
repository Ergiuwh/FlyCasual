#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI.Helpers.Navigation.Internal;
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

            yield return MovementPredictionHelper.Calculate(movement);
            MovementPrediction prediction = MovementPredictionHelper.Prediction;

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


        public static MovementPrediction FastMovementPrediction(GenericMovement movement)
        {
            Selection.ThisShip = movement.TheShip;
            GenericMovement savedMovement = movement.TheShip.AssignedManeuver;

            movement.TheShip.SetAssignedManeuver(movement, isSilent: true);

            MovementPrediction prediction = new(movement.TheShip, movement);
            prediction.CalculateOnlyFinalPositionIgnoringCollisions();

            movement.TheShip.SetAssignedManeuver(savedMovement, isSilent: true);

            return prediction;
        }

        public static MovementPrediction FastMovementPrediction(GenericShip ship, Maneuver maneuver)
        {
            return FastMovementPrediction(ship, maneuver.ToString());
        }

        public static MovementPrediction FastMovementPrediction(GenericShip ship, string maneuverCode)
        {
            // isSimple is not currently referenced by CalculateOnlyFinalPositionIgnoringCollisions()
            GenericMovement movement = CreateMovement(ship, maneuverCode, true);
            return FastMovementPrediction(movement);
        }


        public static List<Maneuver> GetShipManeuvers(GenericShip ship)
        {
            return ship.GetManeuverHolders().Select(a => new Maneuver(a)).ToList();
        }


        /// <summary>
        /// Does not call Calulate().
        /// </summary>
        /// <param name="ship"></param>
        /// <param name="maneuvers"></param>
        /// <returns></returns>
        public static BatchedMovementPrediction<Maneuver> CreateBatchedPredictions(GenericShip ship, List<Maneuver> maneuvers)
        {
            Dictionary<Maneuver, GenericMovement> movements = new();
            foreach (Maneuver maneuver in maneuvers)
            {
                movements.Add(maneuver, CreateMovement(ship, maneuver, true));
            }

            return new BatchedMovementPrediction<Maneuver>(
                movements,
                new List<GenericShip>() { ship });
        }

        /// <summary>
        /// Does not call Calulate().
        /// </summary>
        /// <param name="ship"></param>
        /// <param name="maneuvers"></param>
        /// <returns></returns>
        public static BatchedMovementPrediction<string> CreateBatchedPredictions(GenericShip ship, List<string> maneuverCodes)
        {
            Dictionary<string, GenericMovement> movements = new();
            foreach (string maneuverCode in maneuverCodes)
            {
                movements.Add(maneuverCode, CreateMovement(ship, maneuverCode, true));
            }

            return new BatchedMovementPrediction<string>(
                movements,
                new List<GenericShip>() { ship });
        }

        /// <summary>
        /// Does not call Calulate(). Stores complexity in movements.
        /// </summary>
        /// <param name="ship"></param>
        /// <param name="maneuvers"></param>
        /// <returns></returns>
        public static BatchedMovementPrediction<string> CreateBatchedPredictions(GenericShip ship, Dictionary<string, MovementComplexity> maneuvers)
        {
            Dictionary<string, GenericMovement> movements = new();
            foreach (KeyValuePair<string, MovementComplexity> maneuver in maneuvers)
            {
                movements.Add(maneuver.Key, CreateMovement(ship, maneuver.Key, isSimple: true, complexity: maneuver.Value));
            }

            return new BatchedMovementPrediction<string>(
                movements,
                new List<GenericShip>() { ship });
        }
    }

    public static class MovementPredictionHelper
    {
        private static MovementPrediction? prediction;
        public static MovementPrediction Prediction
        {
            get
            {
                return prediction ?? throw new System.Exception("Attempt to access MovementPredictionHelper.Prediction before MovementPredictionHelper.Calculate().");
            }
        }

        /// <summary>
        /// Sets Selection.ThisShip to ship.
        /// </summary>
        /// <param name="ship"></param>
        /// <param name="maneuver"></param>
        /// <param name="isSimple"></param>
        /// <param name="movementPrediction"></param>
        /// <returns></returns>
        public static IEnumerator Calculate(GenericShip ship, Maneuver maneuver, bool isSimple)
        {
            return Calculate(ship, maneuver.ToString(), isSimple);
        }

        public static IEnumerator Calculate(GenericShip ship, string maneuverCode, bool isSimple)
        {
            GenericMovement movement = NavFunctions.CreateMovement(ship, maneuverCode, isSimple);
            return Calculate(movement);
        }

        public static IEnumerator Calculate(GenericMovement movement)
        {
            GenericShip? savedThisShip = Selection.ThisShip;

            Selection.ThisShip = movement.TheShip;
            GenericMovement savedMovement = movement.TheShip.AssignedManeuver;

            movement.TheShip.SetAssignedManeuver(movement, isSilent: true);

            prediction = new MovementPrediction(movement.TheShip, movement);
            yield return prediction.CalculateMovementPredicition();

            movement.TheShip.SetAssignedManeuver(savedMovement, isSilent: true);

            Selection.ThisShip = savedThisShip;
        }
    }
}