#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI.Helpers.Types;
using Movement;
using Ship;


namespace AI.Helpers.Navigation.PredictionHelpers {
    public static class SingleMovementPredictionHelper
    {
        private static MovementPrediction? prediction;
        public static MovementPrediction Prediction
        {
            get
            {
                return prediction ?? throw new System.Exception("Attempt to access SingleMovementPredictionHelper.Prediction before SingleMovementPredictionHelper.Calculate().");
            }
            private set { prediction = value; }
        }

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

            Prediction = new MovementPrediction(movement.TheShip, movement, MovementPrediction.PredictionType.FullInfo);
            yield return Prediction.CalculateMovementPredicition();

            movement.TheShip.SetAssignedManeuver(savedMovement, isSilent: true);

            Selection.ThisShip = savedThisShip;
        }


        public static IEnumerator CalculateAtFinalPosition(GenericShip ship, Maneuver maneuver)
        {
            return CalculateAtFinalPosition(ship, maneuver.ToString());
        }

        public static IEnumerator CalculateAtFinalPosition(GenericShip ship, string maneuverCode)
        {
            GenericMovement movement = NavFunctions.CreateMovement(ship, maneuverCode, true);
            return CalculateAtFinalPosition(movement);
        }

        public static IEnumerator CalculateAtFinalPosition(GenericMovement movement)
        {
            GenericShip? savedThisShip = Selection.ThisShip;

            Selection.ThisShip = movement.TheShip;
            GenericMovement savedMovement = movement.TheShip.AssignedManeuver;

            movement.TheShip.SetAssignedManeuver(movement, isSilent: true);

            Prediction = new MovementPrediction(movement.TheShip, movement, MovementPrediction.PredictionType.EndpointInfo);
            yield return Prediction.CalculateResultsAtFinalPosition();

            movement.TheShip.SetAssignedManeuver(savedMovement, isSilent: true);

            Selection.ThisShip = savedThisShip;
        }
    }

    public static class BatchedPredicitionHelper
    {
        public static Dictionary<string, MovementPrediction> Predictions = new();

        public static IEnumerator Calculate(GenericShip ship, Dictionary<string, MovementComplexity> maneuvers)
        {
            Predictions.Clear();

            foreach (KeyValuePair<string, MovementComplexity> maneuver in maneuvers)
            {
                GenericMovement movement = NavFunctions.CreateMovement(ship, maneuver.Key, complexity: maneuver.Value);
                MovementPrediction prediction = new(ship, movement, MovementPrediction.PredictionType.FullInfo);

                Predictions.Add(maneuver.Key, prediction);
            }

            return MovementPredictionBatchManager.CalculatePredictions(ship, Predictions.Values.ToList());
        }

        public static IEnumerator Calculate(GenericShip ship, List<Maneuver> maneuvers)
        {
            Predictions.Clear();

            foreach (Maneuver maneuver in maneuvers)
            {
                GenericMovement movement = NavFunctions.CreateMovement(ship, maneuver);
                MovementPrediction prediction = new(ship, movement, MovementPrediction.PredictionType.FullInfo);

                Predictions.Add(maneuver.ToString(), prediction);
            }

            return MovementPredictionBatchManager.CalculatePredictions(ship, Predictions.Values.ToList());
        }

        public static IEnumerator Calculate(GenericShip ship, Dictionary<string, GenericMovement> movements)
        {
            Predictions.Clear();

            foreach (KeyValuePair<string, GenericMovement> movement in movements)
            {
                MovementPrediction prediction = new(ship, movement.Value, MovementPrediction.PredictionType.FullInfo);

                Predictions.Add(movement.Key, prediction);
            }

            return MovementPredictionBatchManager.CalculatePredictions(ship, Predictions.Values.ToList());
        }
    }
}