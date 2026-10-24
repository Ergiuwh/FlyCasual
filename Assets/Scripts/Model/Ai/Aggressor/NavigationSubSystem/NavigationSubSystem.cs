#nullable enable

using ActionsList;
using AI.Helpers;
using AI.Helpers.Navigation;
using AI.Helpers.Navigation.PredictionHelpers;
using AI.Helpers.Types;
using AI.Helpers.Types.Actions;
using AI.Helpers.VirtualBoard;
using AI.Helpers.VirtualBoard.Debug;
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

        private static readonly Maneuver DefaultManeuver = Maneuver.Static.TwoFowardStraight;

        public static void CalculateNavigation(Action callback)
        {
            PrepareCalculateNavigation(callback, DoCalculations);
        }

        public static void CalculateNavigationQuick(Action callback)
        {
            PrepareCalculateNavigation(callback, DoCalculationsQuick);
        }

        private static void PrepareCalculateNavigation(Action callback, Func<Action, IEnumerator> calculationFunc)
        {
            CurrentPlayer = Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer);

            if (Phases.RoundCounter == 1)
            {
                virtualBoards = new Dictionary<PlayerNo, VirtualBoard<AggressorVirtualShipInfo>>()
                {
                    { PlayerNo.Player1, new VirtualBoard<AggressorVirtualShipInfo>("Aggressor main") },
                    { PlayerNo.Player2, new VirtualBoard<AggressorVirtualShipInfo>("Aggressor main") }
                };

                VirtualBoardLog.Instance.InitializeLogForPlayer(CurrentPlayer.PlayerNo);
            }
            else
            {
                VirtualBoardLog.Instance.ClearLogForPlayer(CurrentPlayer.PlayerNo);
            }

            VirtualBoardManager.UpdateVirtualRealBoard();

            GameManagerScript.Instance.StartCoroutine
            (
                calculationFunc.Invoke(callback)
            );
        }

        private static void DoCalculationsStart()
        {
            if (CurrentPlayer is null)
            {
                throw new Exception("AI.Aggressor.NavigationSubSystem : StartCalculations called when CurrentPlayer is null.");
            }

            AiPlanningLog.OpenNewGroup("Planning Phase");

            VirtualBoard.Activate();
            VirtualBoard.UpdateToReal(NewShipInitialiser);
            VirtualBoard.ApplyVirtualPositions();
            VirtualBoardLog.Instance.CurrentLog.AddUpdateToVirtualBoard("inital", VirtualBoard);

            ShowCalculationsStart();
        }

        private static void DoCalculationsEnd()
        {
            CopyOrderOfActivationToVirtualBoard();

            VirtualBoard.Deactivate();

            ShowCalculationsEnd();

            AiPlanningLog.CloseGroup();
        }

        private static IEnumerator DoCalculations(Action callback)
        {
            DoCalculationsStart();

            OrderOfActivation = GenerateOrderOfActivation();

            yield return FirstPredictionRound();

            AiPlanningLog.OpenNewGroup("Second prediction round");
            yield return SecondPredictionRound();
            AiPlanningLog.CloseGroup();

            AiPlanningLog.OpenNewGroup("Third prediction round");
            yield return ThirdPredictionRound();
            AiPlanningLog.CloseGroup();

            DoCalculationsEnd();

            callback();
        }

        private static void VirtualBoardLogAddPosition(string name, GenericShip ship)
        {
            VirtualBoardLog.Instance.CurrentLog.AddSingleMoveStep(name, ship, VirtualBoard.Ships[ship].VirtualPosition);
        }

        private static void VirtualBoardLogAddUpdateToVirtualBoard(string name)
        {
            VirtualBoardLog.Instance.CurrentLog.AddUpdateToVirtualBoard(name, VirtualBoard);
        }

        private static IEnumerator FirstPredictionRound()
        {
            foreach (GenericShip ship in OrderOfActivation)
            {
                Maneuver maneuver = DefaultManeuver;
                VirtualBoard.GetShipDataOrError(ship).SetPlannedManeuver(maneuver);
                yield return NavFunctions.ApplyManeuverOnVirtualBoard(VirtualBoard, ship, maneuver, isSimple: true);
                VirtualBoardLogAddPosition($"First prediction round: {ship}", ship);
            }
        }

        private static IEnumerator SecondPredictionRound()
        {
            foreach (GenericShip ship in OrderOfActivation)
            {
                yield return PredictBestManeuverForShipRoundTwo(ship);

                Maneuver? plannedManeuver = VirtualBoard.GetShipDataOrError(ship).PlannedManeuver;
                if (plannedManeuver == null)
                {
                    plannedManeuver = DefaultManeuver;
                    ConsoleHelpers.Error("AI.Aggressor.NavigationSubSystem.SecondPredictionRound", $"plannedManeuver is null. Recovered by using {DefaultManeuver}.");
                }

                yield return NavFunctions.ApplyManeuverOnVirtualBoard(
                    VirtualBoard,
                    ship,
                    (Maneuver)plannedManeuver,
                    isSimple: true
                    );

                VirtualBoardLogAddPosition($"Second prediction round: final position of {ship}", ship);

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

                    Maneuver? plannedManeuver = VirtualBoard.GetShipDataOrError(ship).PlannedManeuver;
                    if (plannedManeuver == null)
                    {
                        plannedManeuver = DefaultManeuver;
                        ConsoleHelpers.Error("AI.Aggressor.NavigationSubSystem.ThirdPredictionRound", $"plannedManeuver is null. Recovered by using {DefaultManeuver}.");
                    }

                    yield return NavFunctions.ApplyManeuverOnVirtualBoard(
                        VirtualBoard,
                        ship,
                        (Maneuver)plannedManeuver,
                        isSimple: true
                        );

                    VirtualBoardLogAddPosition($"Third prediction round: final position of {ship}", ship);

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

            yield return BatchedMovementPredicitionHelper.Calculate(
                ship,
                ship.GetManeuvers(),
                predictionType: MovementPrediction.PredictionType.EndpointInfo);
            Dictionary<string, MovementPrediction> finalPredictions = BatchedMovementPredicitionHelper.Predictions;

            VirtualBoard.ReturnAllCollisions();

            VirtualBoardLogAddUpdateToVirtualBoard("Start predict best maneuver (round 2)");

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

                fastPrediction.IsOffTheBoard = Board.IsOffTheBoard(ship);

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

            yield return BatchedMovementPredicitionHelper.Calculate(ship, maneuvers);

            foreach (KeyValuePair<string, MovementPrediction> item in BatchedMovementPredicitionHelper.Predictions)
            {
                finalPredictions.Add(item.Key, item.Value);
            }

            VirtualBoard.ReturnAllCollisions();

            VirtualBoardLogAddUpdateToVirtualBoard("Start predict best maneuver (round 3)");

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

            AiPlanningLog.OpenNewGroup($"Scoring maneuvers for {ship}");

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

                AiPlanningLog.Add($"{maneuver.Key}  {CurrentNavigationResult}");
            }

            VirtualBoard.GetShipDataOrError(ship).UpdateNavigationResults(navigationResults);

            // Without randomly selecting the maneuver, there is a bias towards left maneuvers.
            List<KeyValuePair<string, NavigationResult>> bestManeuvers = navigationResults.OrderByDescending(a => a.Value.Priority).ToList();
            double highestPriority = bestManeuvers.First().Value.Priority;
            int numberOfHighestPriority = bestManeuvers.Count(a => Math.Abs(a.Value.Priority - highestPriority) < 0.00000001);

            Maneuver chosenManeuver = new(bestManeuvers[UnityEngine.Random.Range(0,numberOfHighestPriority)].Key);

            AiPlanningLog.Add($"Selected {chosenManeuver}");

            VirtualBoard.GetShipDataOrError(ship).SetPlannedManeuver(chosenManeuver);
            VirtualBoard.GetShipInterface(ship).UpdateToRealPosition();
            VirtualBoard.ReturnAllCollisions();

            AiPlanningLog.CloseGroup();
        }

        /// <summary>
        /// May change TheShip's virtual position. May change CurrentNavigationResult.
        /// enemyShipsWithoutOtherTargets is not required to be correct.
        /// Calls result.CalculatePriority().
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

            CurrentNavigationResult.CalculatePriority();

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

            if (AiPlanningLog.DoLogging) {
                string orderOfActivationText = "Order of activation chosen: ";
                foreach (GenericShip ship in orderOfActivation)
                {
                    orderOfActivationText += ship + ", ";
                }

                AiPlanningLog.Add(orderOfActivationText);
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
                .OrderBy(n => VirtualBoard.GetShipData(n)?.OrderToActivate ?? 0)
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
                VirtualBoard.GetShipDataOrError(ship).SetPlannedManeuver(Maneuver.Static.TwoFowardStraight);
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
            Maneuver? plannedManeuver = VirtualBoard.GetShipData(ship)?.PlannedManeuver;
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
            Maneuver? plannedManeuver = VirtualBoard.GetShipData(ship)?.PlannedManeuver;
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

        public static int GetActionPriority(GenericAction action, ref BarrelRollPlanInfo? currentBarrelRollPlan, ref BoostPlanInfo? currentBoostPlan, int redActionPriorityModifier, List<GenericAction> linkedActions)
        {
            VirtualBoard<VirtualBoardManager.EmptyClass> virtualBoard = new("Aggressor action priority");
            virtualBoard.UpdateToReal(a => new());
            virtualBoard.Activate();

            GenericShip ship = action.HostShip;
            int priority;

            if (action is BarrelRollActionWithPlans castBarrelRollAction)
            {
                (double, BarrelRollPlanInfo?) barrelRollActionInfo = GetBarrelRollActionPriority(castBarrelRollAction.BarrelRollPlans, (ActiveVirtualBoard<VirtualBoardManager.EmptyClass>)virtualBoard);
                currentBarrelRollPlan = barrelRollActionInfo.Item2;
                priority = (int)barrelRollActionInfo.Item1;
                if (barrelRollActionInfo.Item2?.FinalPositionInfo is not null) {
                    virtualBoard.GetShipInterface(ship).SetPosition((ShipPositionInfo)barrelRollActionInfo.Item2.FinalPositionInfo);
                }
            }
            else if (action is BoostActionWithPlans castBoostAction)
            {
                (double, BoostPlanInfo?) boostActionInfo = GetBoostActionPriority(castBoostAction.BoostPlans, (ActiveVirtualBoard<VirtualBoardManager.EmptyClass>)virtualBoard);
                currentBoostPlan = boostActionInfo.Item2;
                priority = (int)boostActionInfo.Item1;
                if (boostActionInfo.Item2?.FinalPositionInfo is not null) {
                    virtualBoard.GetShipInterface(ship).SetPosition((ShipPositionInfo)boostActionInfo.Item2.FinalPositionInfo);
                }
            }
            else
            {
                priority = action.GetActionPriority();
            }

            ship.Ai.CallGetActionPriority(action, ref priority);

            if (action.IsRed)
            {
                priority += redActionPriorityModifier;
            }

            virtualBoard.Deactivate();

            BarrelRollPlanInfo? nullBarrelRollPlanInfo = null;
            BoostPlanInfo? nullBoostPlanInfo = null;

            int bestLinkedActionPriority = 0;

            foreach (GenericAction linkedAction in linkedActions)
            {
                int linkedActionPriority = GetActionPriority(linkedAction, ref nullBarrelRollPlanInfo, ref nullBoostPlanInfo, redActionPriorityModifier, new());
                
                if (linkedActionPriority > bestLinkedActionPriority)
                {
                    bestLinkedActionPriority = linkedActionPriority;
                }
            }

            priority += bestLinkedActionPriority;

            return priority;
        }

        private static (double, BarrelRollPlanInfo?) GetBarrelRollActionPriority<T>(List<BarrelRollPlanInfo> barrelRollPlans, ActiveVirtualBoard<T> activeVirtualBoard) where T : class
        {
            GenericShip theShip = Selection.ThisShip ?? throw new Exception("Selection.ThisShip null in unexpected place.");
            VirtualBoard<T> virtualBoard = (VirtualBoard<T>)activeVirtualBoard;

            AiPlanningLog.OpenNewGroup("Barrel Roll Priority");

            List<(double, BarrelRollPlanInfo)> scoredBarrelRollPlans = new();

            PositionResult currentPositionResult = PositionResult.NewNoCollision(theShip);
            currentPositionResult.CalculatePositionPriority();

            foreach (BarrelRollPlanInfo planInfo in barrelRollPlans)
            {
                if (planInfo.FinalPositionInfo is null)
                {
                    ConsoleHelpers.Error("AI.Aggressor.NavigationSubSystem.GetBarrelRollActionPriority", "planInfo.finalPositionInfo is null.");
                    AiPlanningLog.Add($"{planInfo.TemplateOptionName} {planInfo.PositionOptionName}  int.MinValue reason:error");
                    scoredBarrelRollPlans.Add((int.MinValue, planInfo));
                    continue;
                }

                if (planInfo.IsAllowed == false)
                {
                    scoredBarrelRollPlans.Add((0, planInfo));
                    AiPlanningLog.Add($"{planInfo.TemplateOptionName} {planInfo.PositionOptionName}  0  reason:Fails");
                    continue;
                }

                virtualBoard.GetShipInterface(theShip).SetPosition((ShipPositionInfo)planInfo.FinalPositionInfo);

                PositionResult movementResult = PositionResult.NewNoCollision(theShip);
                movementResult.CalculatePositionPriority();

                double delta = movementResult.Priority - currentPositionResult.Priority;
                scoredBarrelRollPlans.Add((delta / 20, planInfo));

                AiPlanningLog.Add($"{planInfo.TemplateOptionName} {planInfo.PositionOptionName}  {delta / 20}");
            }

            (double, BarrelRollPlanInfo) chosenPlan = scoredBarrelRollPlans.OrderByDescending(a => a.Item1).First();
            if (chosenPlan.Item1 == int.MinValue)
            {
                AiPlanningLog.CloseGroup();
                return (double.NegativeInfinity, null);
            }
            else
            {
                AiPlanningLog.Add($"Best plan: {chosenPlan.Item2.TemplateOptionName} {chosenPlan.Item2.PositionOptionName}");
                AiPlanningLog.CloseGroup();
                return chosenPlan;
            }
        }

        private static (double, BoostPlanInfo?) GetBoostActionPriority<T>(List<BoostPlanInfo> boostPlans, ActiveVirtualBoard<T> activeVirtualBoard) where T : class
        {
            GenericShip theShip = Selection.ThisShip ?? throw new Exception("Selection.ThisShip null in unexpected place.");
            VirtualBoard<T> virtualBoard = (VirtualBoard<T>)activeVirtualBoard;

            AiPlanningLog.OpenNewGroup("Boost Priority");

            List<(double, BoostPlanInfo)> scoredPlans = new();

            PositionResult currentPositionResult = PositionResult.NewNoCollision(theShip);
            currentPositionResult.CalculatePositionPriority();

            foreach (BoostPlanInfo planInfo in boostPlans)
            {
                if (planInfo.FinalPositionInfo is null)
                {
                    ConsoleHelpers.Error("AI.Aggressor.NavigationSubSystem.GetBoostActionPriority", "planInfo.finalPositionInfo is null.");
                    AiPlanningLog.Add($"{planInfo.BoostMove.Name}  int.MinValue reason:error");
                    scoredPlans.Add((int.MinValue, planInfo));
                    continue;
                }

                if (planInfo.IsAllowed == false)
                {
                    scoredPlans.Add((0, planInfo));
                    AiPlanningLog.Add($"{planInfo.BoostMove.Name}  0  reason:Fails");
                    continue;
                }

                virtualBoard.GetShipInterface(theShip).SetPosition((ShipPositionInfo)planInfo.FinalPositionInfo);

                PositionResult movementResult = PositionResult.NewNoCollision(theShip);
                movementResult.CalculatePositionPriority();

                double delta = movementResult.Priority - currentPositionResult.Priority;
                scoredPlans.Add((delta / 20, planInfo));

                AiPlanningLog.Add($"{planInfo.BoostMove.Name}  {delta / 20}{(planInfo.IsAllowed ? "" : " Fails")}");
            }

            (double, BoostPlanInfo) chosenPlan = scoredPlans.OrderByDescending(a => a.Item1).First();
            if (chosenPlan.Item1 == int.MinValue)
            {
                AiPlanningLog.CloseGroup();
                return (double.NegativeInfinity, null);
            }
            else
            {
                AiPlanningLog.Add($"Best plan: {chosenPlan.Item2.BoostMove.Name}");
                AiPlanningLog.CloseGroup();
                return chosenPlan;
            }
        }

        private static IEnumerator DoCalculationsQuick(Action callback)
        {
            DoCalculationsStart();

            OrderOfActivation = GenerateOrderOfActivation();

            AiPlanningLog.OpenNewGroup("Inital Movement Predictions");
            foreach (GenericShip ship in OrderOfActivation)
            {
                AiPlanningLog.OpenNewGroup($"Scoring maneuvers for {ship}");
                if (ship.Owner == CurrentPlayer) {
                    Dictionary<string, NavigationResult> shipNavigationResults = new();
                    ShipPositionInfo savedPositionInfo = ship.GetPositionInfo();
                    foreach (KeyValuePair<string, MovementComplexity> maneuver in ship.GetManeuvers())
                    {
                        MovementPrediction prediction = NavFunctions.PredictFinalPosition(ship, maneuver);

                        NavigationResult navigationResult = CreateNavigationResult(prediction, new());
                        navigationResult.CalculatePriorityInRoundTwo();

                        shipNavigationResults.Add(maneuver.Key, navigationResult);

                        AiPlanningLog.Add($"{maneuver.Key}  {CurrentNavigationResult}");

                        VirtualBoard.GetShipInterface(ship).SetPosition(savedPositionInfo);
                    }

                    VirtualBoard.GetShipDataOrError(ship).UpdateNavigationResults(shipNavigationResults);
                }
                AiPlanningLog.CloseGroup();

                MovementPrediction movementPrediction = NavFunctions.PredictFinalPosition(ship, Maneuver.Static.TwoFowardStraight);
                VirtualBoard.GetShipInterface(ship).SetPosition(movementPrediction.FinalPositionInfo);
            }
            AiPlanningLog.CloseGroup();

            VirtualBoard.UpdateToReal(NewShipInitialiser);

            AiPlanningLog.OpenNewGroup("Final Movement Predictions");
            foreach (GenericShip ship in OrderOfActivation)
            {
                if (ship.Owner == CurrentPlayer)
                {
                    AiPlanningLog.OpenNewGroup($"Scoring maneuvers for {ship}");

                    List<GenericShip> enemyShipsWithoutOtherTarget = FindEnemyShipsWithoutOtherTarget(ship);
                    Dictionary<string, NavigationResult> navigationResults = VirtualBoard.GetShipDataOrError(ship).NavigationResults
                        ?? throw new Exception("AI.Aggressor.NavigationSubSystem.StartCalculationQuick: navigationResults should not be null");
                    ShipPositionInfo savedPositionInfo = ship.GetPositionInfo();

                    bool isManeuverFound = false;

                    // prevent infinite loop if I made a mistake
                    for (int i = 0; i < 2*navigationResults.Count; i++) {
                        KeyValuePair<string, NavigationResult> navigationResultToTry = navigationResults.OrderByDescending(a => a.Value.Priority).First();

                        yield return SingleMovementPredictionHelper.Calculate(ship, navigationResultToTry.Key, true);
                        MovementPrediction prediction = SingleMovementPredictionHelper.Prediction;

                        NavigationResult result = CreateNavigationResult(prediction, enemyShipsWithoutOtherTarget);
                        result.CalculatePriorityInRoundTwo();

                        VirtualBoard.GetShipInterface(ship).SetPosition(savedPositionInfo, true);

                        AiPlanningLog.Add($"{navigationResultToTry.Key}  Expected: {navigationResultToTry.Value}, Result: {result}");

                        if (Math.Abs(navigationResultToTry.Value.Priority - result.Priority) < 0.00001)
                        {
                            AiPlanningLog.Add($"Selected {navigationResultToTry.Key}");

                            isManeuverFound = true;

                            VirtualBoard.GetShipDataOrError(ship).SetPlannedManeuver(new Maneuver(navigationResultToTry.Key));
                            yield return NavFunctions.ApplyManeuverOnVirtualBoard(VirtualBoard, ship, new Maneuver(navigationResultToTry.Key));
                            break;
                        }
                        else
                        {
                            navigationResults[navigationResultToTry.Key] = result;
                        }
                    }

                    if (!isManeuverFound)
                    {
                        ConsoleHelpers.Error("AI.Aggressor.NavigationSubSystem.StartCalculationsQuick", "Did not find maneuver.");
                    }

                    AiPlanningLog.CloseGroup();
                }
                else
                {
                    yield return NavFunctions.ApplyManeuverOnVirtualBoard(VirtualBoard, ship, Maneuver.Static.TwoFowardStraight);
                }
            }
            AiPlanningLog.CloseGroup();

            DoCalculationsEnd();

            callback();
        }

    }
}