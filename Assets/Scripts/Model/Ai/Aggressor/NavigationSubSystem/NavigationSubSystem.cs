#nullable enable

using ActionsList;
using AI.Helpers.Navigation;
using AI.Helpers.Navigation.PredictionHelpers;
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
                yield return PredictBestManeuverForShipRoundTwo(ship);
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
                    yield return PredictBestManeuverForShipRoundThree(ship);
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

        private static IEnumerator PredictBestManeuverForShipRoundTwo(GenericShip ship)
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

            int indexOfShipInOrderOfActivation = OrderOfActivation.IndexOf(ship);
            foreach (GenericShip item in OrderOfActivation.GetRange(indexOfShipInOrderOfActivation, OrderOfActivation.Count - indexOfShipInOrderOfActivation))
            {
                VirtualBoard.GetShipInterface(item).RemoveCollisions();
            }

            yield return BatchedPredicitionHelper.Calculate(
                ship,
                ship.GetManeuvers(),
                predictionType: MovementPrediction.PredictionType.EndpointInfo);
            Dictionary<string, MovementPrediction> finalPredictions = BatchedPredicitionHelper.Predictions;

            VirtualBoard.ReturnAllCollisions();

            PredictBestManeuverForShip(ship, a => finalPredictions[a], CallNavigationResultCalculateInRoundTwo);
        }

        private static IEnumerator PredictBestManeuverForShipRoundThree(GenericShip ship)
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

            int indexOfShipInOrderOfActivation = OrderOfActivation.IndexOf(ship);
            foreach (GenericShip item in OrderOfActivation.GetRange(indexOfShipInOrderOfActivation, OrderOfActivation.Count - indexOfShipInOrderOfActivation))
            {
                VirtualBoard.GetShipInterface(item).RemoveCollisions();
            }

            Dictionary<string, MovementPrediction> finalPredictions = new();
            Dictionary<string, MovementComplexity> maneuvers = new();
            foreach (KeyValuePair<string, MovementComplexity> maneuver in ship.GetManeuvers())
            {
                MovementPrediction fastPrediction = NavFunctions.PredictFinalPosition(ship, maneuver.Key);

                VirtualBoard.GetShipInterface(ship).SetPosition(fastPrediction.FinalPositionInfo);

                fastPrediction.IsOffTheBoard = BoardTools.Board.IsOffTheBoard(ship);

                VirtualBoard.GetShipInterface(ship).UpdateToRealPosition();

                if (fastPrediction.IsOffTheBoard)
                {
                    finalPredictions.Add(maneuver.Key, fastPrediction);
                }
                else
                {
                    maneuvers.Add(maneuver.Key, maneuver.Value);
                }
            }

            yield return BatchedPredicitionHelper.Calculate(ship, maneuvers);

            foreach (KeyValuePair<string, MovementPrediction> item in BatchedPredicitionHelper.Predictions)
            {
                finalPredictions.Add(item.Key, item.Value);
            }

            VirtualBoard.ReturnAllCollisions();

            PredictBestManeuverForShip(ship, a => finalPredictions[a], CallNavigationResultCalculate);
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

            DebugManager.AiPlanningLog.OpenNewGroup($"Scoring maneuvers for {ship}");

            if (ship.Owner == CurrentPlayer)
            {
                Selection.ChangeActiveShip(ship);
            }
            else
            {
                Selection.ThisShip = ship;
            }

            Dictionary<string, NavigationResult> navigationResults = new();

            List<GenericShip> enemyShipsWithoutOtherTargets = FindEnemyShipsWithoutOtherTarget(ship);

            int indexOfShipInOrderOfActivation = OrderOfActivation.IndexOf(ship);

            foreach (KeyValuePair<string, MovementComplexity> maneuver in ship.GetManeuvers())
            {
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
        /// May change TheShip's virtual position. May change CurrentNavigationResult.
        /// </summary>
        /// <param name="prediction"></param>
        /// <returns></returns>
        public static NavigationResult CreateNavigationResult(MovementPrediction prediction, List<GenericShip> enemyShipsWithoutOtherTargets)
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
                VirtualBoard.GetShipInterface(prediction.Ship).SetPosition(prediction.FinalPositionInfo);
                CheckNextTurn(prediction.Ship);
                ProcessHeavyGeometryCalculations(prediction.Ship, out float minDistanceToEnemyShip, out float minDistanceToNearestEnemyInShotRange, out float minAngle, out int enemiesInShotRange);

                CurrentNavigationResult.distanceToNearestEnemy = minDistanceToEnemyShip;
                CurrentNavigationResult.distanceToNearestEnemyInShotRange = minDistanceToNearestEnemyInShotRange;
                CurrentNavigationResult.angleToNearestEnemy = minAngle;
                CurrentNavigationResult.enemiesInShotRange = enemiesInShotRange;

                if (prediction.Ship.Owner.Ships.Count > 0) {
                    CurrentNavigationResult.enemiesWithThisAsOnlyTarget = CountEnemiesWithThisAsOnlyTarget(prediction.Ship, enemyShipsWithoutOtherTargets);
                }
                else
                {
                    CurrentNavigationResult.enemiesWithThisAsOnlyTarget = 0;
                }
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
                    orderOfActivationText += ship + ", ";
                }

                DebugManager.AiPlanningLog.Add(orderOfActivationText);
            }

            return orderOfActivation;
        }

        public static void ProcessHeavyGeometryCalculations(GenericShip ship, out float minDistanceToEnemyShip, out float minDistanceToNearestEnemyInShotRange, out float minAngle, out int enemiesInShotRange)
        {
            List<GenericShip> shipsToRemoveCollisions = new();
            foreach (var shipI in VirtualBoard.GetShipInterfaceOnAllShipsWhere(a => a != ship))
            {
                if (!shipI.HasCollisions()) {
                    shipsToRemoveCollisions.Add(shipI.Ship);
                    shipI.ReturnCollisions();
                }
            }

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

            foreach (GenericShip ship1 in shipsToRemoveCollisions)
            {
                VirtualBoard.GetShipInterface(ship1).RemoveCollisions();
            }
        }

        public static void CheckNextTurn(GenericShip ship, out bool isOffTheBoardNextTurn)
        {
            List<GenericShip> shipsToReturnCollisions = new();
            foreach (var shipI in VirtualBoard.GetShipInterfaceOnAllShipsWhere(a => a != ship))
            {
                if (shipI.HasCollisions()) {
                    shipsToReturnCollisions.Add(shipI.Ship);
                    shipI.RemoveCollisions();
                }
            }

            bool HasAnyManeuverWithoutOffBoardFinish = false;

            foreach (string turnManeuver in NavFunctions.GetShortestTurnManeuvers(ship))
            {
                MovementPrediction prediction = NavFunctions.PredictFinalPosition(ship, turnManeuver);

                if (!prediction.IsOffTheBoard) HasAnyManeuverWithoutOffBoardFinish = true;
            }

            isOffTheBoardNextTurn = !HasAnyManeuverWithoutOffBoardFinish;

            foreach (GenericShip ship1 in shipsToReturnCollisions)
            {
                VirtualBoard.GetShipInterface(ship1).ReturnCollisions();
            }
        }

        private static void CheckNextTurn(GenericShip ship)
        {
            if (CurrentNavigationResult is null)
            {
                throw new Exception("AI.Aggressor.NavigationSubSystem : CheckNextTurnRecursive called when CurrentNavigationResult is null.");
            }

            CheckNextTurn(ship, out bool isOffTheBoardNextTurn);

            CurrentNavigationResult.isOffTheBoardNextTurn = isOffTheBoardNextTurn;
            CurrentNavigationResult.isHitAsteroidNextTurn = false;
        }

        public static int CountEnemiesWithThisAsOnlyTarget(GenericShip ship, List<GenericShip> enemiesWithoutTarget)
        {
            int enemiesWithThisAsOnlyTarget = 0;
            foreach (GenericShip enemyShip in enemiesWithoutTarget)
            {
                ShotInfo shotInfo = new(enemyShip, ship, enemyShip.PrimaryWeapons);

                if (shotInfo.IsShotAvailable)
                {
                    enemiesWithThisAsOnlyTarget += 1;
                }
            }

            return enemiesWithThisAsOnlyTarget;
        }

        public static int CountEnemiesWithThisAsOnlyTarget(GenericShip ship)
        {
            return CountEnemiesWithThisAsOnlyTarget(ship, FindEnemyShipsWithoutOtherTarget(ship));
        }

        public static List<GenericShip> FindEnemyShipsWithoutOtherTarget(GenericShip ship)
        {
            List<GenericShip> enemyShipsWithoutOtherTargets = new();
            foreach (GenericShip enemyShip in ship.Owner.EnemyShips.Values)
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

            return enemyShipsWithoutOtherTargets;
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

        public static int GetActionPriority(GenericAction action, ref GenericAiPlayer.BarrelRollPlanInfo? currentBarrelRollPlan)
        {
            if (action is GenericAiPlayer.BarrelRollActionWithPlans castAction)
            {
                (int, GenericAiPlayer.BarrelRollPlanInfo?) barrelRollActionInfo = GetBarrelRollActionPriority(castAction.barrelRollPlans);
                currentBarrelRollPlan = barrelRollActionInfo.Item2;
                return barrelRollActionInfo.Item1;
            }
            else
            {
                return action.GetActionPriority();
            }
        }

        public static (int, GenericAiPlayer.BarrelRollPlanInfo?) GetBarrelRollActionPriority(List<GenericAiPlayer.BarrelRollPlanInfo> barrelRollPlans)
        {
            GenericShip theShip = Selection.ThisShip ?? throw new Exception("Selection.ThisShip null in unexpected place.");

            VirtualBoard.ClaimActive();
            VirtualBoard.UpdateToReal(NewShipInitialiser);

            List<(int, GenericAiPlayer.BarrelRollPlanInfo)> scoredBarrelRollPlans = new();

            PositionResult currentPositionResult = PositionResult.NewNoCollision(theShip);
            currentPositionResult.CalculatePositionPriority();

            foreach (GenericAiPlayer.BarrelRollPlanInfo planInfo in barrelRollPlans)
            {
                if (planInfo.FinalPositionInfo is null)
                {
                    Console.Write("AI.Aggressor.NavigationSubSystem.GetBarrelRollActionPriority() planInfo.finalPositionInfo is null.", false, "yellow");
                    scoredBarrelRollPlans.Add((int.MinValue, planInfo));
                    break;
                }

                if (planInfo.IsAllowed == false)
                { 
                    scoredBarrelRollPlans.Add((0, planInfo));
                }

                VirtualBoard.GetShipInterface(theShip).SetPosition((ShipPositionInfo)planInfo.FinalPositionInfo);

                PositionResult movementResult = PositionResult.NewNoCollision(theShip);
                movementResult.CalculatePositionPriority();

                int delta = movementResult.Priority - currentPositionResult.Priority;
                scoredBarrelRollPlans.Add((delta / 20, planInfo));
            }

            VirtualBoard.Deactivate();
            VirtualBoardManager.ActivateRealBoard();

            (int, GenericAiPlayer.BarrelRollPlanInfo) chosenPlan = scoredBarrelRollPlans.OrderByDescending(a => a.Item1).First();
            if (chosenPlan.Item1 == int.MinValue)
            {
                return (int.MinValue, null);
            }
            else
            {
                return chosenPlan;
            }
        }
    }
}