#nullable enable

using AI.Helpers.Navigation;
using BoardTools;
using Movement;
using Players;
using Ship;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AI.Aggressor
{
    public static class NavigationSubSystem
    {
        private static GenericPlayer? CurrentPlayer;

        // private static Dictionary<PlayerNo, VirtualBoard<NavigationResult>> VirtualBoards;
        // private static VirtualBoard<NavigationResult> VirtualBoard
        // {
        //     get { return VirtualBoards[CurrentPlayer.PlayerNo]; }
        //     set { VirtualBoards[CurrentPlayer.PlayerNo] = value; }
        // }

        private static Dictionary<PlayerNo, NewVirtualBoard<AggressorVirtualShipInfo>> NewVirtualBoards = new();

        private static NewVirtualBoard<AggressorVirtualShipInfo> NewVirtualBoard
        {
            get { return NewVirtualBoards[CurrentPlayer?.PlayerNo ?? throw new Exception("AI.Aggressor.NavigationSubSystem : Attempt to get VirtualBoard without an active player.")]; }
            set { NewVirtualBoards[CurrentPlayer?.PlayerNo ?? throw new Exception("AI.Aggressor.NavigationSubSystem : Attempt to set VirtualBoard without an active player.")] = value; }
        }

        private static int OrderOfActivation;

        private static NavigationResult? CurrentNavigationResult;

        public static void CalculateNavigation(Action callback)
        {
            CurrentPlayer = Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer);

            ConfigureVirtualBoards();

            GameManagerScript.Instance.StartCoroutine
            (
                StartCalculations(callback)
            );
        }

        private static IEnumerator StartCalculations(Action callback)
        {
            if (CurrentPlayer == null)
            {
                throw new Exception("AI.Aggressor.NavigationSubSystem : StartCalculations called when CurrentPlayer is null.");
            }

            NewVirtualBoard.ClaimActive();
            NewVirtualBoard.UpdateToReal(a => new(a));
            ShowCalculationsStart();

            SwitchEnemyShipsToSimpleVirtualPositions();
            yield return PredictAllFinalPositionsOfOwnShips();

            NewVirtualBoard.UpdateToReal(a => new(a));

            List<GenericShip> orderOfActivation = GenerateOrderOfActivation();

            yield return FindBestManeuversForShips(orderOfActivation);

            NewVirtualBoard.Deactivate();
            VirtualBoardManager.ActivateRealBoard();
            ShowCalculationsEnd();


            callback();
        }

        private static void SwitchEnemyShipsToSimpleVirtualPositions()
        {
            if (CurrentPlayer == null)
            {
                throw new Exception("AI.Aggressor.NavigationSubSystem : CurrentPlayer null in unexpected place.");
            }

            foreach (GenericShip ship in CurrentPlayer.EnemyShips.Values)
            {
                PredictSimpleFinalPositionOfEnemyShip(ship);
            }
        }

        private static void PredictSimpleFinalPositionOfEnemyShip(GenericShip ship)
        {
            Selection.ThisShip = ship;

            GenericMovement savedMovement = ship.AssignedManeuver;

            // Decide what maneuvers to use as temporary
            string temporyManeuver = (ship.State.IsIonized) ? "1.F.S" : "2.F.S";
            bool isTemporaryManeuverAdded = false;
            if (!ship.HasManeuver(temporyManeuver))
            {
                isTemporaryManeuverAdded = true;
                ship.Maneuvers.Add(temporyManeuver, MovementComplexity.Easy);
            }
            GenericMovement movement = ShipMovementScript.MovementFromString(temporyManeuver);

            // Check maneuver
            ship.SetAssignedManeuver(movement, isSilent: true);
            movement.Initialize();
            movement.IsSimple = true;

            MovementPrediction prediction = new MovementPrediction(ship, movement);
            prediction.CalculateOnlyFinalPositionIgnoringCollisions();

            if (isTemporaryManeuverAdded)
            {
                ship.Maneuvers.Remove(temporyManeuver);
            }

            if (savedMovement != null)
            {
                ship.SetAssignedManeuver(savedMovement, isSilent: true);
            }
            else
            {
                ship.ClearAssignedManeuver();
            }

            // VirtualBoard.SetVirtualPositionInfo(ship, prediction.FinalPositionInfo, temporyManeuver);
            NewVirtualBoard.GetShipInterface(ship).ShipData.SetPredictedPosition(prediction.FinalPositionInfo).SetPlannedManeuver(new(temporyManeuver));
        }

        private static IEnumerator PredictAllFinalPositionsOfOwnShips()
        {
            // foreach (GenericShip ship in CurrentPlayer.EnemyShips.Values)
            // {
            //     VirtualBoard.SwitchToVirtualPosition(ship);
            // }

            // foreach (GenericShip ship in CurrentPlayer.Ships.Values)
            // {
            //     yield return PredictFinalPosionsOfOwnShip(ship);
            // }

            // foreach (GenericShip ship in CurrentPlayer.EnemyShips.Values)
            // {
            //     VirtualBoard.SwitchToRealPosition(ship);
            // }

            // NewVirtualBoard.Activate().ApplyVirtualPositions();

            if (CurrentPlayer == null)
            {
                throw new Exception("AI.Aggressor.NavigationSubSystem : CurrentPlayer null in unexpected place.");
            }

            NewVirtualBoard.ApplyVirtualPositions();
            foreach (GenericShip ship in CurrentPlayer.Ships.Values)
            {
                yield return PredictFinalPosionsOfOwnShip(ship);
            }
            // NewVirtualBoard.Deactivate();

            // VirtualBoardManager.RestoreRealBoard();
        }

        private static IEnumerator PredictFinalPosionsOfOwnShip(GenericShip ship)
        {
            Selection.ChangeActiveShip(ship);
            // VirtualBoard.SwitchToRealPosition(ship);
            NewVirtualBoard.GetShipInterface(ship).UpdateToRealPosition();

            Dictionary<string, NavigationResult> navigationResults = new();
            foreach (KeyValuePair<string, MovementComplexity> maneuver in ship.GetManeuvers())
            {
                GenericMovement movement = ShipMovementScript.MovementFromString(maneuver.Key);
                ship.SetAssignedManeuver(movement, isSilent: true);
                movement.Initialize();
                movement.IsSimple = true;

                MovementPrediction prediction = new MovementPrediction(ship, movement);
                prediction.CalculateOnlyFinalPositionIgnoringCollisions();

                // VirtualBoard.SetVirtualPositionInfo(ship, prediction.FinalPositionInfo, prediction.CurrentMovement.ToString());
                // VirtualBoard.SwitchToVirtualPosition(ship);
                NewVirtualBoard.GetShipInterface(ship)
                    .SetPosition(prediction.FinalPositionInfo).ShipData
                    .SetPredictedPosition(prediction.FinalPositionInfo).SetPlannedManeuver(new(prediction.CurrentMovement.ToString()));

                ProcessHeavyGeometryCalculations(ship, out float minDistanceToEnemyShip, out float minDistanceToNearestEnemyInShotRange, out float minAngle, out int enemiesInShotRange);

                NavigationResult result = new NavigationResult()
                {
                    movement = prediction.CurrentMovement,
                    distanceToNearestEnemy = minDistanceToEnemyShip,
                    distanceToNearestEnemyInShotRange = minDistanceToNearestEnemyInShotRange,
                    angleToNearestEnemy = minAngle,
                    enemiesInShotRange = enemiesInShotRange,
                    isBumped = prediction.IsBumped,
                    isLandedOnObstacle = prediction.IsLandedOnAsteroid,
                    isOffTheBoard = prediction.IsOffTheBoard,
                    FinalPositionInfo = prediction.FinalPositionInfo
                };
                result.CalculatePriority();

                navigationResults.Add(maneuver.Key, result);

                // VirtualBoard.SwitchToRealPosition(ship);
                NewVirtualBoard.GetShipInterface(ship).UpdateToRealPosition();

                yield return true;
            }

            ship.ClearAssignedManeuver();
            // VirtualBoard.UpdateNavigationResults(ship, navigationResults);
            NewVirtualBoard.GetShipData(ship).UpdateNavigationResults(navigationResults);
            Selection.DeselectThisShip();
        }

        private static List<GenericShip> GenerateOrderOfActivation()
        {
            OrderOfActivation = 0;

            List<GenericShip> orderOfActivation = new();

            List<GenericShip> AllShips = new(Roster.AllShips.Values.ToList());

            while (AllShips.Count > 0)
            {
                int lowestInitiative = AllShips.Min(n => n.State.Initiative);

                GenericShip shipToActivate = AllShips
                    .Where(n => n.State.Initiative == lowestInitiative)
                    .OrderBy(n => GetMinDistanceToEnemyShip(n))
                    .OrderByDescending(n => n.Owner.PlayerNo == Phases.PlayerWithInitiative)
                    .First();

                orderOfActivation.Add(shipToActivate);
                AllShips.Remove(shipToActivate);
            }

            if (DebugManager.DebugAiNavigation)
            {
                string orderOfActivationText = "";
                foreach (GenericShip ship in orderOfActivation)
                {
                    orderOfActivationText += (ship.ShipId + ", ");
                }
            }

            return orderOfActivation;
        }

        private static IEnumerator FindBestManeuversForShips(List<GenericShip> orderOfActivation)
        {
            if (CurrentPlayer == null)
            {
                throw new Exception("AI.Aggressor.NavigationSubSystem : CurrentPlayer null in unexpected place.");
            }

            while (orderOfActivation.Count > 0)
            {
                NewVirtualBoard.ClaimActive();

                SetVirtualPositionsForShipsWithPreviousActivations(orderOfActivation);

                GenericShip ship = orderOfActivation.First();
                orderOfActivation.Remove(ship);

                if (ship.Owner.PlayerNo == CurrentPlayer.PlayerNo)
                {
                    yield return FindBestManeuver(ship);
                }
                else
                {
                    yield return PredictCollisionDetectionOfEnemyShip(ship);
                }

                NewVirtualBoard.Deactivate();
                VirtualBoardManager.ActivateRealBoard();
            }
        }

        private static IEnumerator FindBestManeuver(GenericShip ship)
        {
            if (CurrentPlayer == null)
            {
                throw new Exception("AI.Aggressor.NavigationSubSystem : CurrentPlayer null in unexpected place.");
            }

            if (NewVirtualBoard.GetShipData(ship).NavigationResults == null)
            {
                throw new Exception("AI.Aggressor.NavigationSubSystem : FindBestManeuver : NavigationResults of the input ship is null.");
            }

            Selection.ChangeActiveShip(ship);

            int bestPriority = int.MinValue;
            KeyValuePair<string, NavigationResult> maneuverToCheck = new();

            do
            {
                // VirtualBoard.SwitchToRealPosition(ship);
                NewVirtualBoard.GetShipInterface(ship).UpdateToRealPosition();

                // bestPriority = VirtualBoard.Ships[ship].NavigationResults.Max(n => n.Value.Priority);
                // maneuverToCheck = VirtualBoard.Ships[ship].NavigationResults.Where(n => n.Value.Priority == bestPriority).First();
                bestPriority = NewVirtualBoard.GetShipData(ship).NavigationResults.Max(n => n.Value.Priority);
                maneuverToCheck = NewVirtualBoard.GetShipData(ship).NavigationResults.First(n => n.Value.Priority == bestPriority);

                GenericMovement movement = ShipMovementScript.MovementFromString(maneuverToCheck.Key);

                ship.SetAssignedManeuver(movement, isSilent: true);
                movement.Initialize();
                movement.IsSimple = true;

                MovementPrediction prediction = new(ship, movement);
                yield return prediction.CalculateMovementPredicition();

                // VirtualBoard.SetVirtualPositionInfo(ship, prediction.FinalPositionInfo, prediction.CurrentMovement.ToString());
                // VirtualBoard.SwitchToVirtualPosition(ship);
                NewVirtualBoard.GetShipInterface(ship)
                    .SetPosition(prediction.FinalPositionInfo).ShipData
                    .SetPredictedPosition(prediction.FinalPositionInfo)
                    .SetPlannedManeuver(new(prediction.CurrentMovement.ToString()));

                CurrentNavigationResult = new NavigationResult()
                {
                    movement = prediction.CurrentMovement,
                    isBumped = prediction.IsBumped,
                    isLandedOnObstacle = prediction.IsLandedOnAsteroid,
                    obstaclesHit = prediction.AsteroidsHit.Count,
                    isOffTheBoard = prediction.IsOffTheBoard,
                    minesHit = prediction.MinesHit.Count,
                    isOffTheBoardNextTurn = false, //!NextTurnNavigationResults.Any(n => !n.isOffTheBoard),
                    isHitAsteroidNextTurn = false, //!NextTurnNavigationResults.Any(n => n.obstaclesHit == 0),
                    FinalPositionInfo = prediction.FinalPositionInfo
                };

                foreach (GenericShip enemyShip in CurrentPlayer.EnemyShips.Values)
                {
                    // VirtualBoard.SwitchToVirtualPosition(enemyShip);
                    NewVirtualBoard.GetShipInterface(enemyShip).SetPosition(NewVirtualBoard.GetShipData(enemyShip).PredictedPosition);
                }

                if (!prediction.IsOffTheBoard)
                {
                    yield return CheckNextTurnRecursive(ship);

                    ProcessHeavyGeometryCalculations(ship, out float minDistanceToEnemyShip, out float minDistanceToNearestEnemyInShotRange, out float minAngle, out int enemiesInShotRange);

                    CurrentNavigationResult.distanceToNearestEnemy = minDistanceToEnemyShip;
                    CurrentNavigationResult.distanceToNearestEnemyInShotRange = minDistanceToNearestEnemyInShotRange;
                    CurrentNavigationResult.angleToNearestEnemy = minAngle;
                    CurrentNavigationResult.enemiesInShotRange = enemiesInShotRange;
                }

                CurrentNavigationResult.CalculatePriority();

                // VirtualBoard.Ships[ship].NavigationResults[maneuverToCheck.Key] = CurrentNavigationResult;
                NewVirtualBoard.GetShipData(ship).NavigationResults[maneuverToCheck.Key] = CurrentNavigationResult;

                // bestPriority = VirtualBoard.Ships[ship].NavigationResults.Max(n => n.Value.Priority);
                bestPriority = NewVirtualBoard.GetShipData(ship).NavigationResults.Max(n => n.Value.Priority);

                // VirtualBoard.SwitchToRealPosition(ship);
                NewVirtualBoard.GetShipInterface(ship).UpdateToRealPosition();
                
                // maneuverToCheck = VirtualBoard.Ships[ship].NavigationResults.First(n => n.Key == maneuverToCheck.Key);
                maneuverToCheck = NewVirtualBoard.GetShipData(ship).NavigationResults.First(n => n.Key == maneuverToCheck.Key);

                foreach (GenericShip enemyShip in CurrentPlayer.EnemyShips.Values)
                {
                    // VirtualBoard.SwitchToRealPosition(enemyShip);
                    NewVirtualBoard.GetShipInterface(enemyShip).UpdateToRealPosition();
                }

            } while (maneuverToCheck.Value.Priority != bestPriority);

            // VirtualBoard.Ships[ship].SetPlannedManeuverCode(maneuverToCheck.Key, ++OrderOfActivation);
            NewVirtualBoard.GetShipData(ship)
                .SetPlannedManeuver(new(maneuverToCheck.Key))
                .SetOrderToActivate(++OrderOfActivation);
            ship.ClearAssignedManeuver();
            Selection.DeselectThisShip();
        }

        private static void ProcessHeavyGeometryCalculations(GenericShip ship, out float minDistanceToEnemyShip, out float minDistanceToNearestEnemyInShotRange, out float minAngle, out int enemiesInShotRange)
        {
            minDistanceToEnemyShip = float.MaxValue;
            minDistanceToNearestEnemyInShotRange = 0;
            minAngle = float.MaxValue;
            enemiesInShotRange = 0;
            foreach (GenericShip enemyShip in ship.Owner.EnemyShips.Values)
            {
                DistanceInfo distInfo = new DistanceInfo(ship, enemyShip);
                if (distInfo.MinDistance.DistanceReal < minDistanceToEnemyShip)
                {
                    minDistanceToEnemyShip = distInfo.MinDistance.DistanceReal;
                }

                ShotInfo shotInfo = new ShotInfo(ship, enemyShip, ship.PrimaryWeapons.First());
                if (shotInfo.IsShotAvailable)
                {
                    enemiesInShotRange++;

                    if (minDistanceToNearestEnemyInShotRange < shotInfo.DistanceReal)
                    {
                        minDistanceToNearestEnemyInShotRange = shotInfo.DistanceReal;
                    }
                }

                Vector3 forward = ship.GetFrontFacing();
                Vector3 toEnemyShip = enemyShip.GetCenter() - ship.GetCenter();
                float angle = Mathf.Abs(Vector3.SignedAngle(forward, toEnemyShip, Vector3.down));
                if (angle < minAngle) minAngle = angle;
            }
        }

        private static void SetVirtualPositionsForShipsWithPreviousActivations(List<GenericShip> orderOfActivation)
        {
            foreach (GenericShip ship in Roster.AllShips.Values)
            {
                if (!orderOfActivation.Contains(ship))
                {
                    // VirtualBoard.SwitchToVirtualPosition(ship);
                    NewVirtualBoard.GetShipInterface(ship)
                        .SetPosition(NewVirtualBoard.GetShipData(ship).PredictedPosition);
                }
            }
        }

        private static IEnumerator PredictCollisionDetectionOfEnemyShip(GenericShip ship)
        {
            Selection.ThisShip = ship;

            GenericMovement savedMovement = ship.AssignedManeuver;

            // Decide what maneuvers to use as temporary
            string temporyManeuver = (ship.State.IsIonized) ? "1.F.S" : "2.F.S";
            bool isTemporaryManeuverAdded = false;
            if (!ship.HasManeuver(temporyManeuver))
            {
                isTemporaryManeuverAdded = true;
                ship.Maneuvers.Add(temporyManeuver, MovementComplexity.Easy);
            }
            GenericMovement movement = ShipMovementScript.MovementFromString(temporyManeuver);

            // Check maneuver
            ship.SetAssignedManeuver(movement, isSilent: true);
            movement.Initialize();
            movement.IsSimple = true;

            MovementPrediction prediction = new(ship, movement);
            yield return prediction.CalculateMovementPredicition();

            if (isTemporaryManeuverAdded)
            {
                ship.Maneuvers.Remove(temporyManeuver);
            }

            if (savedMovement != null)
            {
                ship.SetAssignedManeuver(savedMovement, isSilent: true);
            }
            else
            {
                ship.ClearAssignedManeuver();
            }

            // VirtualBoard.SetVirtualPositionInfo(ship, prediction.FinalPositionInfo, temporyManeuver);
            NewVirtualBoard.GetShipInterface(ship)
                .SetPosition(prediction.FinalPositionInfo).ShipData
                .SetPredictedPosition(prediction.FinalPositionInfo)
                .SetPlannedManeuver(new(temporyManeuver));
        }

        private static IEnumerator CheckNextTurnRecursive(GenericShip ship)
        {
            if (CurrentNavigationResult == null)
            {
                throw new Exception("AI.Aggressor.NavigationSubSystem : CheckNextTurnRecursive called when CurrentNavigationResult is null.");
            }

            // VirtualBoard.RemoveCollisionsExcept(ship);
            foreach (var shipI in NewVirtualBoard.GetShipInterfaceOnAllShipsWhere(a => a != ship))
            {
                shipI.RemoveCollisions();
            }

            bool HasAnyManeuverWithoutOffBoardFinish = false;
            bool HasAnyManeuverWithoutAsteroidCollision = false;

            foreach (string turnManeuver in NavFunctions.GetShortestTurnManeuvers(ship))
            {
                GenericMovement movement = ShipMovementScript.MovementFromString(turnManeuver);

                ship.SetAssignedManeuver(movement, isSilent: true);
                movement.Initialize();
                movement.IsSimple = true;

                MovementPrediction prediction = new(ship, movement);
                yield return prediction.CalculateMovementPredicition();

                if (!prediction.IsOffTheBoard) HasAnyManeuverWithoutOffBoardFinish = true;
                if (!prediction.IsLandedOnAsteroid) HasAnyManeuverWithoutAsteroidCollision = true;
            }

            CurrentNavigationResult.isOffTheBoardNextTurn = !HasAnyManeuverWithoutOffBoardFinish;
            CurrentNavigationResult.isHitAsteroidNextTurn = !HasAnyManeuverWithoutAsteroidCollision;

            // VirtualBoard.ReturnAllCollisions();
            NewVirtualBoard.ReturnAllCollisions();
        }

        public static GenericShip GetNextShipWithoutAssignedManeuver()
        {
            return Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer).Ships.Values
                .Where(n => n.AssignedManeuver == null)
                // .OrderBy(n => VirtualBoard.Ships[n].OrderToActivate)
                .OrderBy(n => NewVirtualBoard.GetShipData(n).OrderToActivate)
                .FirstOrDefault();
        }

        public static GenericShip GetNextShipWithoutFinishedManeuver()
        {
            return Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer).Ships.Values
                .Where(n => !n.IsManeuverPerformed)
                // .OrderBy(n => VirtualBoard.Ships[n].OrderToActivate)
                .OrderBy(n => NewVirtualBoard.GetShipData(n).OrderToActivate)
                .FirstOrDefault();
        }

        public static void AssignPlannedManeuver(Action callBack, float delay)
        {
            // ShipMovementScript.SendAssignManeuverCommand(VirtualBoard.Ships[Selection.ThisShip].PlannedManeuver.ToString());
            ShipMovementScript.SendAssignManeuverCommand(NewVirtualBoard.GetShipData(Selection.ThisShip).PlannedManeuver.ToString());
            GameManagerScript.Wait(delay, delegate { Selection.DeselectThisShip(); callBack(); });
        }

        // Low Priority

        private static void ConfigureVirtualBoards()
        {
            // if (Phases.RoundCounter == 1) VirtualBoards = new Dictionary<PlayerNo, VirtualBoard<NavigationResult>>()
            // {
            //     { PlayerNo.Player1, new VirtualBoard<NavigationResult>() },
            //     { PlayerNo.Player2, new VirtualBoard<NavigationResult>() }
            // };

            // VirtualBoard.Update();

            if (Phases.RoundCounter == 1) NewVirtualBoards = new Dictionary<PlayerNo, NewVirtualBoard<AggressorVirtualShipInfo>>()
            {
                { PlayerNo.Player1, new NewVirtualBoard<AggressorVirtualShipInfo>() },
                { PlayerNo.Player2, new NewVirtualBoard<AggressorVirtualShipInfo>() }
            };

            NewVirtualBoard.UpdateToReal(a => new(a));
        }

        private static void ShowCalculationsStart()
        {
            Roster.ToggleCalculatingStatus(Phases.CurrentSubPhase.RequiredPlayer, true);
        }

        private static void ShowCalculationsEnd()
        {
            Roster.ToggleCalculatingStatus(Phases.CurrentSubPhase.RequiredPlayer, false);
        }

        private static float GetMinDistanceToEnemyShip(GenericShip ship)
        {
            float minDistanceToEnemyShip = float.MaxValue;

            foreach (GenericShip enemyShip in ship.Owner.EnemyShips.Values)
            {
                DistanceInfo distInfo = new(ship, enemyShip);
                if (distInfo.MinDistance.DistanceReal < minDistanceToEnemyShip)
                {
                    minDistanceToEnemyShip = distInfo.MinDistance.DistanceReal;
                }
            }

            return minDistanceToEnemyShip;
        }
    }
}