#nullable enable

using System.Collections;
using System.Collections.Generic;
using Movement;
using Ship;
using UnityEngine;

namespace AI.Helpers.Navigation.PredictionHelpers
{
    public class MovementPredictionBatchManager
    {
        public static int BatchSize { get; private set; } = 10;

        public static IEnumerator CalculatePredictionsWithBatchSize(GenericShip ship, List<MovementPrediction> predictions, int batchSize)
        {
            GenericShip? savedThisShip = Selection.ThisShip;
            GenericMovement savedMovement = ship.AssignedManeuver;

            bool savedColliderState = ship.GetToggleColliders(false);
            Selection.ThisShip = ship;

            foreach (MovementPrediction[] chunk in Chunks(batchSize, predictions))
            {
                foreach (MovementPrediction prediction in chunk)
                {
                    ship.SetAssignedManeuver(prediction.CurrentMovement, isSilent: true);

                    switch (prediction.predictionType)
                    {
                        case MovementPrediction.PredictionType.FullInfo:
                            MovementPrediction.ExposedInternals.GenerateShipStands(prediction);
                            break;
                        case MovementPrediction.PredictionType.EndpointInfo:
                            MovementPrediction.ExposedInternals.GenerateFinalShipStand(prediction);
                            break;
                        default:
                            Console.Write("MovementPredictionBatchManager: Calculate included unsupported predicition type.", true, "yellow");
                            break;
                    }
                }

                yield return new WaitForFixedUpdate();

                foreach (MovementPrediction prediction in chunk)
                {
                    switch (prediction.predictionType)
                    {
                        case MovementPrediction.PredictionType.FullInfo:
                            MovementPrediction.ExposedInternals.GetResults(prediction);
                            break;
                        case MovementPrediction.PredictionType.EndpointInfo:
                            MovementPrediction.ExposedInternals.GetResultsFromFinalPosition(prediction);
                            break;
                        default:
                            // We have already provided a warning.
                            break;
                    }

                    MovementPrediction.ExposedInternals.DestroyGeneratedShipStands(prediction);
                }
            }

            ship.ToggleColliders(savedColliderState);

            ship.SetAssignedManeuver(savedMovement, isSilent: true);
            Selection.ThisShip = savedThisShip;
        }

        public static IEnumerator CalculatePredictions(GenericShip ship, List<MovementPrediction> predictions)
        {
            return CalculatePredictionsWithBatchSize(ship, predictions, BatchSize);
        }

        private static IEnumerable<T[]> Chunks<T>(int size, List<T> list)
        {
            T[] result = new T[size];
            int counter = 0;
            foreach (T item in list)
            {
                result[counter] = item;
                counter += 1;
                if (counter >= size)
                {
                    yield return result;
                    counter = 0;
                }
            }

            if (counter > 0)
            {
                T[] finalResult = new T[counter];
                for (int i = 0; i < counter; i++)
                {
                    finalResult[i] = result[i];
                }

                yield return finalResult;
            }
        }

        public static bool SetBatchSize(int newSize)
        {
            if (newSize <= 0)
            {
                return false;
            }

            BatchSize = newSize;
            return true;
        }
    }
}