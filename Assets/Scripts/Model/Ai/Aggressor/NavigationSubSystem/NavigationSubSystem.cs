#nullable enable

using AI.Helpers.Navigation;
using AI.Helpers.Navigation.Internal;
using AI.Helpers.Types;
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

        private static Dictionary<PlayerNo, VirtualBoard<AggressorVirtualShipInfo>> virtualBoards = new();

        private static VirtualBoard<AggressorVirtualShipInfo> VirtualBoard
        {
            get { return virtualBoards[CurrentPlayer?.PlayerNo ?? throw new Exception("AI.Aggressor.NavigationSubSystem : Attempt to get VirtualBoard without an active player.")]; }
            set { virtualBoards[CurrentPlayer?.PlayerNo ?? throw new Exception("AI.Aggressor.NavigationSubSystem : Attempt to set VirtualBoard without an active player.")] = value; }
        }

        private static Dictionary<PlayerNo, List<GenericShip>> orderOfActivations = new();
        private static List<GenericShip> OrderOfActivation
        {
            get { return orderOfActivations[CurrentPlayer?.PlayerNo ?? throw new Exception("AI.Aggressor.NavigationSubSystem : Attempt to get OrderOfActivation without an active player.")]; }
            set { orderOfActivations[CurrentPlayer?.PlayerNo ?? throw new Exception("AI.Aggressor.NavigationSubSystem : Attempt to set OrderOfActivation without an active player.")] = value; }
        }

        private static NavigationResult? CurrentNavigationResult;

        private static readonly Maneuver DefaultManeuver = new("2.F.S");

        public static void CalculateNavigation(Action callback)
        {
            CurrentPlayer = Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer);

            if (Phases.RoundCounter == 1) virtualBoards = new Dictionary<PlayerNo, VirtualBoard<AggressorVirtualShipInfo>>()
            {
                { PlayerNo.Player1, new VirtualBoard<AggressorVirtualShipInfo>() },
                { PlayerNo.Player2, new VirtualBoard<AggressorVirtualShipInfo>() }
            };

            VirtualBoardManager.UpdateRealBoard();

            GameManagerScript.Instance.StartCoroutine
            (
                StartCalculations(callback)
            );
        }

        private static IEnumerator StartCalculations(Action callback)
        {
            if (CurrentPlayer is null)
            {
                throw new Exception("AI.Aggressor.NavigationSubSystem : StartCalculations called when CurrentPlayer is null.");
            }

            DebugManager.AiPlanningLog.OpenNewGroup("Planning Phase");

            VirtualBoard.ClaimActive();
            VirtualBoard.UpdateToReal(NewShipInitialiser);
            VirtualBoard.ApplyVirtualPositions();
            ShowCalculationsStart();

            OrderOfActivation = GenerateOrderOfActivation();
            
            yield return FirstPredictionRound();

            DebugManager.AiPlanningLog.OpenNewGroup("Second prediction round");
            yield return SecondPredictionRound();
            DebugManager.AiPlanningLog.CloseGroup();

            DebugManager.AiPlanningLog.OpenNewGroup("Third prediction round");
            yield return ThirdPredictionRound();
            DebugManager.AiPlanningLog.CloseGroup();

            CopyOrderOfActivationToVirtualBoard();

            VirtualBoard.Deactivate();

            ShowCalculationsEnd();

            DebugManager.AiPlanningLog.CloseGroup();
            VirtualBoardManager.ActivateRealBoard();

            callback();
        }

        private static IEnumerator FirstPredictionRound()
        {
            foreach (GenericShip ship in OrderOfActivation)
            {
                Maneuver maneuver = DefaultManeuver;
                VirtualBoard.GetShipDataOrError(ship).SetPlannedManeuver(maneuver);
                yield return NavFunctions.ApplyManeuverOnVirtualBoard(VirtualBoard, ship, maneuver, isSimple: true);
            }
        }

        private static IEnumerator SecondPredictionRound()
        {
            foreach (GenericShip ship in OrderOfActivation)
            {
                FastPredictBestManeuverForShip(ship, CallNavigationResultCalculateInRoundTwo);
                yield return NavFunctions.ApplyManeuverOnVirtualBoard(
                    VirtualBoard,
                    ship,
                    VirtualBoard.GetShipDataOrError(ship).PlannedManeuver ?? DefaultManeuver,
                    isSimple: true
                    );
                yield return null;
                Selection.DeselectThisShip();
            }
        }

        private static IEnumerator ThirdPredictionRound()
        {
            foreach (GenericShip ship in OrderOfActivation)
            {
                if (ship.Owner == CurrentPlayer) {
                    yield return PredictBestManeuverForShip(ship, CallNavigationResultCalculate);
                    yield return NavFunctions.ApplyManeuverOnVirtualBoard(
                        VirtualBoard,
                        ship,
                        VirtualBoard.GetShipDataOrError(ship).PlannedManeuver ?? DefaultManeuver,
                        isSimple: true
                        );
                    yield return null;
                }
            }
        }

        private static void CallNavigationResultCalculateInRoundTwo(NavigationResult navigationResult)
        {
            navigationResult.CalculatePriorityInRoundTwo();
        }

        private static void CallNavigationResultCalculate(NavigationResult navigationResult)
        {
            navigationResult.CalculatePriority();
        }

        /// <summary>
        /// Calls VirtualBoard.GetShipDataOrError(ship).SetPlannedManeuver(maneuver) with the selected maneuver
        /// </summary>
        /// <param name="ship"></param>
        /// <returns></returns>
        private static IEnumerator PredictBestManeuverForShip(GenericShip ship, Action<NavigationResult> navigationResultPriorityCalculator)
        {
            if (ship.Owner == CurrentPlayer)
            {
                Selection.ChangeActiveShip(ship);
            }
            else
            {
                Selection.ThisShip = ship;
            }

            VirtualBoard.GetShipInterface(ship).UpdateToRealPosition();

            Dictionary<string, MovementPrediction> finalPredictions = new();
            Dictionary<string, MovementComplexity> maneuvers = new();
            foreach (KeyValuePair<string, MovementComplexity> maneuver in ship.GetManeuvers())
            {
                MovementPrediction fastPrediction = NavFunctions.FastMovementPrediction(ship, maneuver.Key);
                if (fastPrediction.IsOffTheBoard)
                {
                    finalPredictions.Add(maneuver.Key, fastPrediction);
                }
                else
                {
                    maneuvers.Add(maneuver.Key, maneuver.Value);
                }
            }

            BatchedMovementPrediction<string> batchedMovementPredictions = NavFunctions.CreateBatchedPredictions(ship, maneuvers);
            yield return batchedMovementPredictions.Calculate();

            foreach (KeyValuePair<string, MovementPrediction> item in batchedMovementPredictions.Predictions)
            {
                finalPredictions.Add(item.Key, item.Value);
            }

            PredictBestManeuverForShip(ship, a => finalPredictions[a], navigationResultPriorityCalculator);
        }

        /// <summary>
        /// Calls VirtualBoard.GetShipDataOrError(ship).SetPlannedManeuver(maneuver) with the selected maneuver
        /// </summary>
        /// <param name="ship"></param>
        /// <returns></returns>
        private static void FastPredictBestManeuverForShip(GenericShip ship, Action<NavigationResult> navigationResultPriorityCalculator)
        {
            PredictBestManeuverForShip(ship, a => NavFunctions.FastMovementPrediction(ship, a), navigationResultPriorityCalculator);
        }

        /// <summary>
        /// Calls VirtualBoard.GetShipDataOrError(ship).SetPlannedManeuver(maneuver) with the selected maneuver
        /// </summary>
        /// <param name="ship"></param>
        /// <returns></returns>
        private static void PredictBestManeuverForShip(GenericShip ship, Func<string, MovementPrediction> predictionProvider, Action<NavigationResult> navigationResultPriorityCalculator)
        {
            if (CurrentPlayer is null)
            {
                throw new Exception("AI.Aggressor.NavigationSubSystem : PredictBestManeuverForShip called when CurrentPlayer is null.");
            }

            DebugManager.AiPlanningLog.OpenNewGroup($"Scoring maneuvers for {ship.ShipId}");

            if (ship.Owner == CurrentPlayer)
            {
                Selection.ChangeActiveShip(ship);
            }
            else
            {
                Selection.ThisShip = ship;
            }

            Dictionary<string, NavigationResult> navigationResults = new();

            List<GenericShip> enemyShipsWithoutOtherTargets = new();
            foreach (GenericShip enemyShip in CurrentPlayer.EnemyShips.Values)
            {
                bool hasShot = false;
                foreach (GenericShip myShip in enemyShip.Owner.AnotherPlayer.Units.Values)
                {
                    if (myShip == ship)
                    {
                        continue;
                    }

                    // This will be enough for most cases, exceptions are tie/wi with a cannon and anything with a turret upgrade.
                    ShotInfo shotInfo = new(enemyShip, myShip, enemyShip.PrimaryWeapons);

                    if (shotInfo.IsShotAvailable)
                    {
                        hasShot = true;
                        break;
                    }
                }

                if (!hasShot)
                {
                    enemyShipsWithoutOtherTargets.Add(enemyShip);
                }
            }

            int indexOfShipInOrderOfActivation = OrderOfActivation.IndexOf(ship);

            foreach (KeyValuePair<string, MovementComplexity> maneuver in ship.GetManeuvers())
            {
                VirtualBoard.GetShipInterface(ship).UpdateToRealPosition();
                foreach (GenericShip item in OrderOfActivation.GetRange(indexOfShipInOrderOfActivation, OrderOfActivation.Count - indexOfShipInOrderOfActivation))
                {
                    VirtualBoard.GetShipInterface(item).RemoveCollisions();
                }

                MovementPrediction prediction = predictionProvider(maneuver.Key);

                CurrentNavigationResult = CreateNavigationResult(prediction, enemyShipsWithoutOtherTargets);

                navigationResultPriorityCalculator.Invoke(CurrentNavigationResult);

                navigationResults.Add(maneuver.Key, CurrentNavigationResult);

                DebugManager.AiPlanningLog.Add($"{maneuver.Key}  {CurrentNavigationResult}");
            }

            VirtualBoard.GetShipDataOrError(ship).UpdateNavigationResults(navigationResults);

            // Without randomly selecting the maneuver, there is a bias towards left maneuvers.
            List<KeyValuePair<string, NavigationResult>> bestManeuvers = navigationResults.OrderByDescending(a => a.Value.Priority).ToList();
            int highestPriority = bestManeuvers.First().Value.Priority;
            int numberOfHighestPriority = bestManeuvers.Count(a => a.Value.Priority == highestPriority);

            Maneuver chosenManeuver = new(bestManeuvers[UnityEngine.Random.Range(0,numberOfHighestPriority)].Key);

            DebugManager.AiPlanningLog.Add($"Selected {chosenManeuver}");

            VirtualBoard.GetShipDataOrError(ship).SetPlannedManeuver(chosenManeuver);
            VirtualBoard.GetShipInterface(ship).UpdateToRealPosition();
            VirtualBoard.ReturnAllCollisions();

            DebugManager.AiPlanningLog.CloseGroup();
        }

        /// <summary>
        /// May change TheShip's virtual position. May change this.CurrentNavigationResult.
        /// </summary>
        /// <param name="prediction"></param>
        /// <returns></returns>
        private static NavigationResult CreateNavigationResult(MovementPrediction prediction, List<GenericShip> enemyShipsWithoutOtherTargets)
        {
            CurrentNavigationResult = new NavigationResult()
            {
                TheShip = prediction.CurrentMovement.TheShip,
                movement = prediction.CurrentMovement,
                isBumpedEnemy = prediction.IsBumpedAnotherTeam,
                isBumpedFriendly = prediction.IsBumpedSameTeam,
                isLandedOnObstacle = prediction.IsLandedOnAsteroid,
                obstaclesHit = prediction.AsteroidsHit.Count,
                isOffTheBoard = prediction.IsOffTheBoard,
                minesHit = prediction.MinesHit.Count,
                isOffTheBoardNextTurn = false, // Will be set by CheckNextTurnRecursive.
                isHitAsteroidNextTurn = false, // Will be set by CheckNextTurnRecursive.
                FinalPositionInfo = prediction.FinalPositionInfo,
            };

            if (!prediction.IsOffTheBoard)
            {
                VirtualBoard.GetShipInterface(prediction.CurrentMovement.TheShip).SetPosition(prediction.FinalPositionInfo);
                CheckNextTurnRecursive(prediction.CurrentMovement.TheShip);
                ProcessHeavyGeometryCalculations(prediction.CurrentMovement.TheShip, out float minDistanceToEnemyShip, out float minDistanceToNearestEnemyInShotRange, out float minAngle, out int enemiesInShotRange);

                CurrentNavigationResult.distanceToNearestEnemy = minDistanceToEnemyShip;
                CurrentNavigationResult.distanceToNearestEnemyInShotRange = minDistanceToNearestEnemyInShotRange;
                CurrentNavigationResult.angleToNearestEnemy = minAngle;
                CurrentNavigationResult.enemiesInShotRange = enemiesInShotRange;

                int enemiesWithThisAsOnlyTarget = 0;
                foreach (GenericShip enemyShip in enemyShipsWithoutOtherTargets)
                {
                    ShotInfo shotInfo = new(enemyShip, prediction.CurrentMovement.TheShip, enemyShip.PrimaryWeapons);

                    if (shotInfo.IsShotAvailable)
                    {
                        enemiesWithThisAsOnlyTarget += 1;
                    }
                }

                CurrentNavigationResult.enemiesWithThisAsOnlyTarget = enemiesWithThisAsOnlyTarget;
            }

            return CurrentNavigationResult;
        }

        private static List<GenericShip> GenerateOrderOfActivation()
        {
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

            if (DebugManager.AiPlanningLog.DoLogging) {
                string orderOfActivationText = "Order of activation chosen: ";
                foreach (GenericShip ship in orderOfActivation)
                {
                    orderOfActivationText += (ship.ShipId + ", ");
                }

                DebugManager.AiPlanningLog.Add(orderOfActivationText);
            }

            return orderOfActivation;
        }

        private static void ProcessHeavyGeometryCalculations(GenericShip ship, out float minDistanceToEnemyShip, out float minDistanceToNearestEnemyInShotRange, out float minAngle, out int enemiesInShotRange)
        {
            minDistanceToEnemyShip = float.MaxValue;
            minDistanceToNearestEnemyInShotRange = 0;
            minAngle = float.MaxValue;
            enemiesInShotRange = 0;
            foreach (GenericShip enemyShip in ship.Owner.EnemyShips.Values)
            {
                DistanceInfo distInfo = new(ship, enemyShip);
                if (distInfo.MinDistance.DistanceReal < minDistanceToEnemyShip)
                {
                    minDistanceToEnemyShip = distInfo.MinDistance.DistanceReal;
                }

                ShotInfo shotInfo = new(ship, enemyShip, ship.PrimaryWeapons.First());
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

        private static void CheckNextTurnRecursive(GenericShip ship)
        {
            if (CurrentNavigationResult is null)
            {
                throw new Exception("AI.Aggressor.NavigationSubSystem : CheckNextTurnRecursive called when CurrentNavigationResult is null.");
            }

            foreach (var shipI in VirtualBoard.GetShipInterfaceOnAllShipsWhere(a => a != ship))
            {
                shipI.RemoveCollisions();
            }

            bool HasAnyManeuverWithoutOffBoardFinish = false;
            bool HasAnyManeuverWithoutAsteroidCollision = false;

            foreach (string turnManeuver in NavFunctions.GetShortestTurnManeuvers(ship))
            {
                MovementPrediction prediction = NavFunctions.FastMovementPrediction(ship, turnManeuver);

                if (!prediction.IsOffTheBoard) HasAnyManeuverWithoutOffBoardFinish = true;
                if (!prediction.IsLandedOnAsteroid) HasAnyManeuverWithoutAsteroidCollision = true;
            }

            CurrentNavigationResult.isOffTheBoardNextTurn = !HasAnyManeuverWithoutOffBoardFinish;
            CurrentNavigationResult.isHitAsteroidNextTurn = !HasAnyManeuverWithoutAsteroidCollision;

            VirtualBoard.ReturnAllCollisions();
        }

        public static GenericShip GetNextShipWithoutFinishedManeuver()
        {
            return Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer).Ships.Values
                .Where(n => !n.IsManeuverPerformed)
                .OrderBy(n => VirtualBoard.TryGetShipData(n)?.OrderToActivate ?? 0)
                .FirstOrDefault();
        }

        /// <summary>
        /// Used for VirtualBoard.UpdateToReal(NewShipInitialiser);
        /// </summary>
        /// <param name="ship"></param>
        /// <returns></returns>
        private static AggressorVirtualShipInfo NewShipInitialiser(GenericShip ship)
        {
            return new AggressorVirtualShipInfo(ship);
        }

        public static void EnsureShipHasPlannedManeuver(GenericShip ship)
        {
            VirtualBoard.UpdateToReal(NewShipInitialiser);
            if (VirtualBoard.GetShipDataOrError(ship).PlannedManeuver is null)
            {
                VirtualBoard.GetShipDataOrError(ship).SetPlannedManeuver(new AI.Helpers.Types.Maneuver("2.F.S"));
            }
        }

        private static void CopyOrderOfActivationToVirtualBoard()
        {
            int orderOfActivationCounter = 0;
            foreach (GenericShip ship in OrderOfActivation)
            {
                VirtualBoard.GetShipDataOrError(ship).SetOrderToActivate(orderOfActivationCounter);
                orderOfActivationCounter += 1;
            }
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

        public static Maneuver SelectManeuverFrom(List<Maneuver> options, GenericShip ship)
        {
            Maneuver? plannedManeuver = VirtualBoard.TryGetShipData(ship)?.PlannedManeuver;
            if (plannedManeuver == null)
            {
                return options[0];
            }
            else if (options.Contains((Maneuver)plannedManeuver))
            {
                return (Maneuver)plannedManeuver;
            }
            else
            {
                return options[0];
            }
        }

        public static Maneuver SelectManeuverToExecuteFrom(List<Maneuver> options, GenericShip ship)
        {
            Maneuver? plannedManeuver = VirtualBoard.TryGetShipData(ship)?.PlannedManeuver;
            if (plannedManeuver == null)
            {
                return options[0];
            }
            else if (options.Contains((Maneuver)plannedManeuver))
            {
                return (Maneuver)plannedManeuver;
            }
            else
            {
                return options[0];
            }
        }
    }
}