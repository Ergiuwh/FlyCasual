#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Movement;
using Ship;
using UnityEngine;

namespace AI.Helpers.Navigation.Internal
{
    public class BatchedMovementPrediction<TKey> {
        private Dictionary<TKey, GenericMovement> movements;

        private List<GenericShip> ships;

        private Dictionary<TKey, MovementPrediction>? predictions;
        public Dictionary<TKey, MovementPrediction> Predictions
        {
            get
            {
                return predictions ?? throw new System.Exception("Attempt to read BatchedMovementPrediction.Predictions before calling Calculate()");
            }
        }

        public BatchedMovementPrediction(Dictionary<TKey, GenericMovement> movements, List<GenericShip> ships)
        {
            this.movements = movements;
            this.ships = ships;
        }

        public BatchedMovementPrediction(Dictionary<TKey, GenericMovement> movements)
        {
            this.movements = movements;

            HashSet<GenericShip> shipSet = new();
            foreach (GenericMovement movement in movements.Values)
            {
                shipSet.Add(movement.TheShip);
            }

            this.ships = shipSet.ToList();
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

            foreach (GenericShip ship in ships)
            {
                savedMovements.Add(ship, ship.AssignedManeuver);
            }

            return savedMovements;
        }

        private void CreatePredictions()
        {
            predictions = new();

            foreach (KeyValuePair<TKey, GenericMovement> movementPair in movements)
            {
                predictions.Add(movementPair.Key, new MovementPrediction(movementPair.Value.TheShip, movementPair.Value));
            }
        }

        private void ToggleMovingShipColliders(bool newValue)
        {
            foreach (GenericShip ship in ships)
            {
                ship.ToggleColliders(newValue);
            }
        }

        private void GenerateShipStands()
        {
            // This is private, and we only call this after CreatePredictions.
            foreach (MovementPrediction prediction in predictions!.Values)
            {
                Selection.ThisShip = prediction.Ship;
                prediction.Ship.SetAssignedManeuver(prediction.CurrentMovement, isSilent: true);
                MovementPrediction.ExposedInternals.GenerateShipStands(prediction);
            }
        }

        private void GetResults()
        {
            // This is private, and we only call this after CreatePredictions.
            foreach (MovementPrediction prediction in predictions!.Values)
            {
                MovementPrediction.ExposedInternals.GetResults(prediction);
            }
        }

        private void Cleanup()
        {
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