#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI.Helpers.Types;
using Movement;
using Ship;
using UnityEngine;

namespace AI.Helpers.Navigation.Internal
{
    public class BatchedMovementPredictions<TKey> {
        private Dictionary<TKey, GenericMovement> Movements;

        private List<GenericShip> Ships;

        private Dictionary<TKey, MovementPrediction>? predictions;
        public Dictionary<TKey, MovementPrediction> Predictions
        {
            get
            {
                return predictions ?? throw new System.Exception("Attempt to read BatchedMovementPrediction.Predictions before calling Calculate()");
            }
        }

        public BatchedMovementPredictions(Dictionary<TKey, GenericMovement> movements, List<GenericShip> ships)
        {
            Movements = movements;
            Ships = ships;
        }

        public BatchedMovementPredictions(Dictionary<TKey, GenericMovement> movements)
        {
            Movements = movements;

            HashSet<GenericShip> shipSet = new();
            foreach (GenericMovement movement in movements.Values)
            {
                shipSet.Add(movement.TheShip);
            }

            Ships = shipSet.ToList();
        }

        public IEnumerator Calculate()
        {
            GenericShip? savedThisShip = Selection.ThisShip;
            Dictionary<GenericShip, GenericMovement> savedMovements = GetShipMovements();
            CreatePredictions();
            ToggleMovingShipColliders(false);
            GenerateShipStands();
            yield return new WaitForFixedUpdate();
            GetResults();
            ToggleMovingShipColliders(true);
            Cleanup();
            RestoreMovements(savedMovements);
            Selection.ThisShip = savedThisShip;
        }

        private Dictionary<GenericShip, GenericMovement> GetShipMovements()
        {
            Dictionary<GenericShip, GenericMovement> savedMovements = new();

            foreach (GenericShip ship in Ships)
            {
                savedMovements.Add(ship, ship.AssignedManeuver);
            }

            return savedMovements;
        }

        private void CreatePredictions()
        {
            predictions = new();

            foreach (KeyValuePair<TKey, GenericMovement> movementPair in Movements)
            {
                predictions.Add(movementPair.Key, new MovementPrediction(movementPair.Value.TheShip, movementPair.Value));
            }
        }

        private void ToggleMovingShipColliders(bool newValue)
        {
            foreach (GenericShip ship in Ships)
            {
                ship.ToggleColliders(newValue);
            }
        }

        private void GenerateShipStands()
        {
            // null forgiving: This is private, and we only call this after CreatePredictions.
            foreach (MovementPrediction prediction in predictions!.Values)
            {
                Selection.ThisShip = prediction.Ship;
                prediction.Ship.SetAssignedManeuver(prediction.CurrentMovement, isSilent: true);
                MovementPrediction.ExposedInternals.GenerateShipStands(prediction);
            }
        }

        private void GetResults()
        {
            // null forgiving: This is private, and we only call this after CreatePredictions.
            foreach (MovementPrediction prediction in predictions!.Values)
            {
                MovementPrediction.ExposedInternals.GetResults(prediction);
            }
        }

        private void Cleanup()
        {
            // null forgiving: This is private, and we only call this after CreatePredictions.
            foreach (MovementPrediction prediction in predictions!.Values)
            {
                MovementPrediction.ExposedInternals.DestroyGeneratedShipStands(prediction);
            }
        }

        private void RestoreMovements(Dictionary<GenericShip, GenericMovement> savedMovements)
        {
            foreach (KeyValuePair<GenericShip, GenericMovement> pair in savedMovements)
            {
                pair.Key.SetAssignedManeuver(pair.Value, isSilent: true);
            }
        }
    }
}

namespace AI.Helpers.Navigation.PredictionHelpers
{
    public class MovementPredictionBatchManager
    {
        public static int BatchSize { get; private set; } = 2;

        public static Dictionary<string, MovementPrediction> Predictions = new();

        public static IEnumerator Calculate(GenericShip ship, List<(string, GenericMovement)> movements)
        {
            Predictions.Clear();

            GenericShip? savedThisShip = Selection.ThisShip;
            GenericMovement savedMovement = ship.AssignedManeuver;

            ship.ToggleColliders(false);
            Selection.ThisShip = ship;

            foreach ((string, GenericMovement)[] chunk in Chunks(BatchSize, movements))
            {
                foreach ((string, GenericMovement) item in chunk)
                {
                    MovementPrediction prediction = new(ship, item.Item2);
                    Predictions.Add(item.Item1, prediction);

                    ship.SetAssignedManeuver(prediction.CurrentMovement, isSilent: true);

                    MovementPrediction.ExposedInternals.GenerateShipStands(prediction);
                }

                yield return new WaitForFixedUpdate();

                foreach ((string, GenericMovement) item in chunk)
                {
                    MovementPrediction prediction = Predictions[item.Item1];

                    MovementPrediction.ExposedInternals.GetResults(prediction);

                    MovementPrediction.ExposedInternals.DestroyGeneratedShipStands(prediction);
                }
            }

            ship.ToggleColliders(true);
            ship.SetAssignedManeuver(savedMovement, isSilent: true);
            Selection.ThisShip = savedThisShip;
        }

        public static IEnumerator Calculate(GenericShip ship, List<string> maneuverCodes)
        {
            List<(string, GenericMovement)> movements = new();
            foreach (string maneuverCode in maneuverCodes)
            {
                GenericMovement movement = NavFunctions.CreateMovement(ship, maneuverCode);
                movements.Add((maneuverCode, movement));
            }

            return Calculate(ship, movements);
        }

        public static IEnumerator Calculate(GenericShip ship, List<Maneuver> maneuvers)
        {
            List<(string, GenericMovement)> movements = new();
            foreach (Maneuver maneuver in maneuvers)
            {
                GenericMovement movement = NavFunctions.CreateMovement(ship, maneuver);
                movements.Add((maneuver.ToString(), movement));
            }

            return Calculate(ship, movements);
        }

        public static IEnumerator Calculate(GenericShip ship, Dictionary<string, MovementComplexity> maneuvers)
        {
            List<(string, GenericMovement)> movements = new();
            foreach (KeyValuePair<string, MovementComplexity> maneuver in maneuvers)
            {
                GenericMovement movement = NavFunctions.CreateMovement(ship, maneuver.Key, complexity: maneuver.Value);
                movements.Add((maneuver.Key, movement));
            }

            return Calculate(ship, movements);
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