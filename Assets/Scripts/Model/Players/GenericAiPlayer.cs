#nullable enable
// parts of this file have nullable warnings disabled.

using ActionsList;
using AI.Helpers.Types;
using BoardTools;
using GameCommands;
using GameModes;
using MainPhases;
using Movement;
using Ship;
using SubPhases;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Players
{
    public partial class GenericAiPlayer : GenericPlayer
    {
        public static float WaitAfterAssigningDial = 0.2f;
        public static float WaitAfterDeployingShip = 0.5f;
        public static float WaitAfterDiceModification = 1f;
        public static float WaitAfterPlacingObstacle = 1f;
        public static float WaitWhenInformingAboutCrit = 3f;

        private List<GenericShip> ShipsWithoutManeuverAssignCommandSent;

        protected BarrelRollPlanInfo? CurrentBarrelRollPlan;

        private static readonly string[] BarrelRollPositions = new string[3] { "Forward", "Center", "Backwards" };
        
        private List<string> BarrelRollPositionsToTry;

        public GenericAiPlayer() : base()
        {
            PlayerType = PlayerType.Ai;
            Name = "AI";
            ShipsWithoutManeuverAssignCommandSent = new();
            BarrelRollPositionsToTry = new();
        }

        public override void SetupShip()
        {
            base.SetupShip();

            foreach (var shipHolder in Ships)
            {
                if (!shipHolder.Value.IsSetupPerformed && shipHolder.Value.State.Initiative == Phases.CurrentSubPhase.RequiredInitiative)
                {
                    Selection.ChangeActiveShip(shipHolder.Value);

                    int direction = (Phases.CurrentSubPhase.RequiredPlayer == PlayerNo.Player1) ? -1 : 1;
                    Vector3 position = shipHolder.Value.GetPosition() - direction * new Vector3(0, 0, Board.BoardIntoWorld(Board.DISTANCE_1 + Board.RANGE_1));

                    GameManagerScript.Wait(
                        WaitAfterDeployingShip,
                        delegate {
                            GameCommand command = SetupSubPhase.GeneratePlaceShipCommand(shipHolder.Value.ShipId, position, shipHolder.Value.GetAngles());
                            GameMode.CurrentGameMode.ExecuteCommand(command);
                        }
                    );
                    return;
                }
            }

            Phases.Next();
        }

        public override void SetupRemote()
        {
            base.SetupRemote();

            UI.CallClickNextPhase();
        }

        public override void PerformManeuver()
        {
            base.PerformManeuver();

            GenericShip ship;
            if (DebugManager.DebugStraightToCombat)
            {
                ship = GetNextShipWithoutFinishedManeuver();
            }
            else
            {
                ship = SelectShipToActivate();
            }

            if (ship is not null)
            {
                ActivateShip(ship);
            }
            else
            {
                Phases.Next();
            }
        }

        private static GenericShip GetNextShipWithoutFinishedManeuver()
        {
            return Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer).Ships.Values
                .Where(n => n.State.Initiative == Phases.CurrentSubPhase.RequiredInitiative)
                .FirstOrDefault(n => !n.IsManeuverPerformed);
        }

        protected virtual GenericShip SelectShipToActivate()
        {
            return GetNextShipWithoutFinishedManeuver();
        }

        /// <summary>
        /// This should be "private void", but HotacAiPlayer.cs requires "public virtual void".
        /// </summary>
        /// <param name="ship"></param>
        public virtual void ActivateShip(GenericShip ship)
        {
            Selection.ChangeActiveShip("ShipId:" + ship.ShipId);
            PerformManeuverOfShip(ship);
        }

        protected void PerformManeuverOfShip(GenericShip ship)
        {
            if (Selection.ThisShip is null)
            {
                throw new Exception("Selection.ThisShip null in unexpected place.");
            }

            ship.IsManeuverPerformed = true;
            GameCommand command = ShipMovementScript.GenerateActivateAndMoveCommand(Selection.ThisShip.ShipId);
            GameMode.CurrentGameMode.ExecuteCommand(command);
        }

        //TODO: Don't skip attack of all PS ships if one cannot attack (Biggs interaction)

        public override void PerformAttack()
        {
            base.PerformAttack();
            
            GenericShip? attacker = GetShipThatCanAttack();

            if (attacker != null)
            {
                GameMode.CurrentGameMode.ExecuteCommand(
                    CombatSubPhase.GenerateCombatActivationCommand(attacker.ShipId)
                );
            }
            else
            {
                Debug.Log("AI cannot find ship to activate");
            }
        }

        private void PerformAttackContinue()
        {
            if (Selection.ThisShip != null)
            {
                GenericShip? targetForAttack = SelectTargetForAttack();

                Selection.ThisShip.IsAttackPerformed = true;

                if (targetForAttack != null)
                {
                    Selection.ThisShip.IsAttackPerformed = true;

                    Messages.ShowInfo("Attacking with " + Combat.ChosenWeapon.Name);

                    GameCommand command = Combat.GenerateIntentToAttackCommand(Selection.ThisShip.ShipId, targetForAttack.ShipId, true, Combat.ChosenWeapon);
                    if (command != null) GameMode.CurrentGameMode.ExecuteServerCommand(command);
                }
                else
                {
                    OnTargetNotLegalForAttack();
                }
            }
        }

        protected virtual GenericShip? SelectTargetForAttack()
        {
            if (DebugManager.DebugNoCombat) return null;

            return AI.HotAC.TargetForAttackSelector.SelectTargetAndWeapon(Selection.ThisShip);
        }

        private static GenericShip? GetShipThatCanAttack()
        {
            foreach (var shipHolder in Roster.GetPlayer(Phases.CurrentPhasePlayer).Ships)
            {
                if (shipHolder.Value.State.CombatActivationAtInitiative == Phases.CurrentSubPhase.RequiredInitiative)
                {
                    if (!shipHolder.Value.IsAttackPerformed)
                    {
                        return shipHolder.Value;
                    }
                }
            }

            return null;
        }

        public GenericShip? FindNearestEnemyShip(GenericShip thisShip, bool ignoreCollided = false, bool inArcAndRange = false)
        {
            Dictionary<GenericShip, float> results = GetEnemyShipsAndDistance(thisShip, ignoreCollided, inArcAndRange);
            GenericShip? result = null;
            if (results.Count != 0)
            {
                result = results.OrderBy(n => n.Value).First().Key;
            }
            return result;
        }


        // TODO: Remove, used in AI/HotAC/TargetForAttackSelector
        public Dictionary<GenericShip, float> GetEnemyShipsAndDistance(GenericShip thisShip, bool ignoreCollided = false, bool inArcAndRange = false)
        {
            Dictionary<GenericShip, float> results = new();

            foreach (var shipHolder in Roster.GetPlayer(Roster.AnotherPlayer(thisShip.Owner.PlayerNo)).Ships)
            {
                if (!shipHolder.Value.IsDestroyed)
                {

                    if (ignoreCollided)
                    {
                        if (thisShip.LastShipCollision != null)
                        {
                            if (thisShip.LastShipCollision.ShipId == shipHolder.Value.ShipId)
                            {
                                continue;
                            }
                        }
                        if (shipHolder.Value.LastShipCollision != null)
                        {
                            if (shipHolder.Value.LastShipCollision.ShipId == thisShip.ShipId)
                            {
                                continue;
                            }
                        }
                    }

                    if (inArcAndRange)
                    {
                        BoardTools.DistanceInfo distanceInfo = new(thisShip, shipHolder.Value);
                        if (distanceInfo.Range > 3)
                        {
                            continue;
                        }
                    }

                    float distance = Vector3.Distance(thisShip.GetCenter(), shipHolder.Value.GetCenter());
                    results.Add(shipHolder.Value, distance);
                }
            }
            results = results.OrderBy(n => n.Value).ToDictionary(n => n.Key, n => n.Value);

            return results;
        }

        public override void UseDiceModifications(DiceModificationTimingType type)
        {
            base.UseDiceModifications(type);

            switch (type)
            {
                case DiceModificationTimingType.Normal:
                    Selection.ActiveShip = (Combat.AttackStep == CombatStep.Attack) ? Combat.Attacker : Combat.Defender;
                    break;
                case DiceModificationTimingType.AfterRolled:
                    Selection.ActiveShip = (Combat.AttackStep == CombatStep.Attack) ? Combat.Attacker : Combat.Defender;
                    break;
                case DiceModificationTimingType.Opposite:
                    Selection.ActiveShip = (Combat.AttackStep == CombatStep.Attack) ? Combat.Defender : Combat.Attacker;
                    break;
                case DiceModificationTimingType.CompareResults:
                    Selection.ActiveShip = Combat.Attacker;
                    break;
                default:
                    break;
            }

            Dictionary<GenericAction, int> actionsPriority = new();

            if (Selection.ActiveShip is null)
            {
                throw new Exception("Selection.ActiveShip null in unexpected place.");
            }

            foreach (var diceModification in Combat.DiceModifications.AvailableDiceModifications.Values)
            {
                int priority = diceModification.GetDiceModificationPriority();
                Selection.ActiveShip.CallOnAiGetDiceModificationPriority(diceModification, ref priority);
                actionsPriority.Add(diceModification, priority);
            }

            actionsPriority = actionsPriority.OrderByDescending(n => n.Value).ToDictionary(n => n.Key, n => n.Value);

            bool isActionEffectTaken = false;

            if (actionsPriority.Count > 0)
            {
                KeyValuePair<GenericAction, int> prioritizedActionEffect = actionsPriority.First();
                if (prioritizedActionEffect.Value > 0)
                {
                    isActionEffectTaken = true;

                    if (!DebugManager.BatchAiSquadTestingModeActive)
                    {
                        GameManagerScript.Wait(WaitAfterDiceModification, delegate
                        {
                            GameCommand command = DiceModificationsManager.GenerateDiceModificationCommand(prioritizedActionEffect.Key.Name);
                            GameMode.CurrentGameMode.ExecuteCommand(command);
                        });
                    }
                    else
                    {
                        GameCommand command = DiceModificationsManager.GenerateDiceModificationCommand(prioritizedActionEffect.Key.Name);
                        GameMode.CurrentGameMode.ExecuteCommand(command);
                    }
                }
            }

            if (!isActionEffectTaken)
            {
                if (!DebugManager.BatchAiSquadTestingModeActive)
                {
                    GameManagerScript.Wait(WaitAfterDiceModification, delegate {
                        GameCommand command = DiceModificationsManager.GenerateDiceModificationCommand("OK");
                        GameMode.CurrentGameMode.ExecuteCommand(command);
                    });
                }
                else
                {
                    GameCommand command = DiceModificationsManager.GenerateDiceModificationCommand("OK");
                    GameMode.CurrentGameMode.ExecuteCommand(command);
                }
            }
        }

        public override void ConfirmDiceCheck()
        {
            DiceCheckConfirm();
        }

        public override void OnTargetNotLegalForAttack()
        {
            /*Selection.ThisShip.CallAfterAttackWindow();
            Selection.ThisShip.IsAttackPerformed = true;
            Selection.ThisShip.CallCombatDeactivation(
                delegate { Phases.FinishSubPhase(typeof(CombatSubPhase)); }
            );*/

            Phases.CurrentSubPhase.IsReadyForCommands = true;
            GameMode.CurrentGameMode.ExecuteCommand(UI.GenerateSkipButtonCommand());
        }

        public override void AskAssignManeuver()
        {
            base.AskAssignManeuver();

            if (DebugManager.DebugStraightToCombat)
            {
                DirectionsMenu.Callback?.Invoke("2.F.S");
            }
            else
            {
                Func<string, bool> stringFilter = DirectionsMenu.Filter;

                Maneuver maneuver;

                if (stringFilter is null)
                {
                    maneuver = GetManeuverDecisionFnFromFilter()(_ => true);
                }
                else
                {
                    maneuver = GetManeuverDecisionFnFromFilter()(ManeuverFilterFromStringFilter(stringFilter));
                }

                DirectionsMenu.Callback?.Invoke(maneuver.ToString());
            }
        }

        public override void ChangeManeuver(Action<string> doWithManeuverString, Action callback, Func<string, bool>? filter = null)
        {
            base.ChangeManeuver(doWithManeuverString, callback, filter);

            DirectionsMenu.Show(doWithManeuverString, callback, filter);
        }

        public override void SelectManeuver(Action<string> doWithManeuverString, Action callback, Func<string, bool>? filter = null)
        {
            base.SelectManeuver(doWithManeuverString, callback, filter);

            DirectionsMenu.Show(doWithManeuverString, callback, filter);
        }

        protected virtual Func<Func<Maneuver, bool>, Maneuver> GetManeuverDecisionFnFromFilter()
        {
            if (Phases.CurrentPhase is PlanningPhase)
            {
                return ChooseManeuverFromFilter;
            }
            else
            {
                return ChooseManeuverToExecuteFromFilter;
            }
        }

        protected virtual Func<List<Maneuver>, Maneuver> GetManeuverDecisionFn()
        {
            if (Phases.CurrentPhase is PlanningPhase)
            {
                return ChooseManeuverFrom;
            }
            else
            {
                return ChooseManeuverToExecuteFrom;
            }
        }

        private Func<Maneuver, bool> ManeuverFilterFromStringFilter(Func<string, bool> filter)
        {
            return new ManeuverFilterFromStringFilterStruct(filter).Check;
        }

        private readonly struct ManeuverFilterFromStringFilterStruct
        {
            readonly Func<string, bool> filter;
            public ManeuverFilterFromStringFilterStruct(Func<string, bool> filter)
            {
                this.filter = filter;
            }
            public readonly bool Check(Maneuver maneuver)
            {
                return filter(maneuver.ToString());
            }
        }

        /// <summary>
        /// Use when the maneuver is about to be executed (e.g. SLAM, Countess Ryad)
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        protected virtual Maneuver ChooseManeuverToExecuteFrom(List<Maneuver> options)
        {
            return options[0];
        }

        /// <summary>
        /// Use when the maneuver is about to be executed (e.g. SLAM, Countess Ryad)
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        protected Maneuver ChooseManeuverToExecuteFromFilter(Func<Maneuver, bool>? filter = null)
        {
            if (Selection.ThisShip is null)
            {
                throw new Exception("Selection.ThisShip null in unexpected place.");
            }
            
            return ChooseManeuverToExecuteFrom(GetShipPossibleManeuvers(Selection.ThisShip).Where(filter).ToList());
        }

        private List<Maneuver> GetShipPossibleManeuvers(GenericShip ship)
        {
            Dictionary<string, MovementComplexity> maneuversStringFormat = ship.GetManeuvers();
            List<Maneuver> maneuvers = maneuversStringFormat.Select(a => new Maneuver(a.Key, a.Value)).ToList();
            return maneuvers;
        }

        protected virtual Maneuver ChooseManeuverFrom(List<Maneuver> options)
        {
            return options[0];
        }

        protected Maneuver ChooseManeuverFromFilter(Func<Maneuver, bool>? filter = null)
        {
            if (Selection.ThisShip is null)
            {
                throw new Exception("Selection.ThisShip null in unexpected place.");
            }

            return ChooseManeuverFrom(GetShipPossibleManeuvers(Selection.ThisShip).Where(filter).ToList());
        }

        public override void StartExtraAttack()
        {
            GenericShip? targetForAttack = SelectTargetForAttack();

            if (targetForAttack != null)
            {
                if (Selection.ThisShip is null)
                {
                    throw new Exception("Selection.ThisShip null in unexpected place.");
                }

                Selection.ThisShip.IsAttackPerformed = true;

                Selection.AnotherShip = targetForAttack;
                Combat.TryPerformAttack(isSilent: true);
            }
            else
            {
                UI.SkipButtonPressedEffect();
            }
        }

#nullable disable warnings
        public override void SelectShipForAbility()
        {
            base.SelectShipForAbility();

            if (Phases.CurrentSubPhase is SelectTargetForAttackSubPhase)
            {
                PerformAttackContinue();
            }
            else
            {
                (Phases.CurrentSubPhase as SelectShipSubPhase).AiSelectPrioritizedTarget();
            }
        }

        public override void SelectShipsForAbility()
        {
            base.SelectShipsForAbility();

            (Phases.CurrentSubPhase as MultiSelectionSubphase).AiSelectPrioritizedTarget();
        }

        public override void PlaceObstacle()
        {
            base.PlaceObstacle();

            ObstaclesPlacementSubPhase subphase = Phases.CurrentSubPhase as ObstaclesPlacementSubPhase;
            if (subphase.IsRandomSetupSelected[Roster.AnotherPlayer(this.PlayerNo)] || DebugManager.BatchAiSquadTestingModeActive)
            {
                (Phases.CurrentSubPhase as ObstaclesPlacementSubPhase).PlaceRandom();
            }
            else
            {
                GameManagerScript.Wait(WaitAfterPlacingObstacle, delegate
                {
                    (Phases.CurrentSubPhase as ObstaclesPlacementSubPhase).PlaceRandom();
                    Messages.ShowInfo("The AI has placed an obstacle");
                });
            }
        }
#nullable enable

        public override void RerollManagerIsPrepared()
        {
            base.RerollManagerIsPrepared();
            DiceRerollManager.CurrentDiceRerollManager.ConfirmRerollButtonIsPressed();
        }

        public override void PerformSystemsActivation()
        {
            base.PerformSystemsActivation();
            UI.SkipButtonPressedEffect();
        }

        public override void InformAboutCrit()
        {
            base.InformAboutCrit();

            if (!Roster.Players.Any(p => p is HumanPlayer))
            {
                if (!DebugManager.BatchAiSquadTestingModeActive)
                {
                    GameManagerScript.Wait(WaitWhenInformingAboutCrit, InformCrit.ButtonConfirm);
                }
                else
                {
                    InformCrit.ButtonConfirm();
                }
            }
            else
            {
                InformCrit.ShowConfirmButton();
            }
        }

        public override void SyncDiceResults()
        {
            base.SyncDiceResults();

            GameMode.CurrentGameMode.ExecuteCommand(DiceRoll.GenerateSyncDiceCommand());
        }

#nullable disable warnings
        public override void TakeDecision()
        {
            DecisionSubPhase subphase = (Phases.CurrentSubPhase as DecisionSubPhase);

            if (subphase.IsForced)
            {
                (Phases.CurrentSubPhase as DecisionSubPhase).DoDefault();
            }
            else if (Phases.CurrentSubPhase is ActionDecisonSubPhase)
            {
                if (Selection.ThisShip is null)
                {
                    throw new Exception("Selection.ThisShip null in unexpected place.");
                }

                GameManagerScript.Instance.StartCoroutine(CalculateAndPerformAction(Selection.ThisShip.GetAvailableActions()));
            }
            else if (Phases.CurrentSubPhase is FreeActionDecisonSubPhase)
            {
                if (Selection.ThisShip is null)
                {
                    throw new Exception("Selection.ThisShip null in unexpected place.");
                }

                GameManagerScript.Instance.StartCoroutine(CalculateAndPerformAction(Selection.ThisShip.GetAvailableFreeActions()));
            }
            else if (Phases.CurrentSubPhase is BarrelRollPlanningSubPhase.BarrelRollDirectionDecisionSubPhase)
            {
                PerformDecision(
                    CurrentBarrelRollPlan?.TemplateOptionName
                    ?? (Phases.CurrentSubPhase as DecisionSubPhase).GetDecisions().First().Name
                    );
            }
            else if (Phases.CurrentSubPhase is BarrelRollPlanningSubPhase.BarrelRollPositionDecisionSubPhase)
            {
                PerformDecision(SelectBarrelRollPosition());
                PerformDecisionNext();
            }
            else (Phases.CurrentSubPhase as DecisionSubPhase).DoDefault();
        }
#nullable enable

        protected IEnumerator CalculateAndPerformAction(List<GenericAction> actionsList)
        {
            if (actionsList.Any(a => a is BarrelRollAction) && DoCalculateBarrelRollResults())
            {
                if (Selection.ThisShip is null)
                {
                    throw new Exception();
                }

                int barrelRollActionIndex = -1;
                for (int i = 0; i < actionsList.Count; i++)
                {
                    if (actionsList[i] is BarrelRollAction)
                    {
                        barrelRollActionIndex = i;
                        break;
                    }
                }

                List<ManeuverTemplate> barrelRollTemplates = Selection.ThisShip.GetAvailableBarrelRollActionTemplates(actionsList[barrelRollActionIndex]);

                List<BarrelRollPlanInfo> barrelRollPlans = CreatePossibleBarrelRollPlans(barrelRollTemplates);

                foreach (BarrelRollPlanInfo barrelRollPlan in barrelRollPlans)
                {
                    yield return barrelRollPlan.DoCalculations();
                }

                BarrelRollPositionsToTry = new(BarrelRollPositions);

                BarrelRollActionWithPlans barrelRollActionWithPlans = new()
                {
                    barrelRollPlans = barrelRollPlans
                };
                actionsList[barrelRollActionIndex] = barrelRollActionWithPlans;
            }

            GenericAction? selectedAction = SelectActionToPerformFromList(actionsList);
            PerformDecision(selectedAction?.Name);
        }

        protected virtual bool DoCalculateBarrelRollResults()
        {
            return false;
        }

        protected void PerformDecision(string? name)
        {
            if (name == null)
            {
                GameMode.CurrentGameMode.ExecuteCommand(UI.GenerateSkipButtonCommand());
            }
            else
            {
                JSONObject parameters = new();
                parameters.AddField("name", name);
                GameController.SendCommand(
                    GameCommandTypes.Decision,
                    Phases.CurrentSubPhase.GetType(),
                    Phases.CurrentSubPhase.ID,
                    parameters.ToString()
                );
            }
        }

        protected void PerformDecisionNext()
        {
            GameMode.CurrentGameMode.ExecuteCommand(UI.GenerateNextButtonCommand());
        }

        protected virtual GenericAction? SelectActionToPerformFromList(List<GenericAction> actionsList)
        {
            return null;
        }

        private string SelectBarrelRollPosition()
        {
            if (CurrentBarrelRollPlan is not null)
            {
                string position = CurrentBarrelRollPlan?.PositionOptionName ?? throw new Exception();
                CurrentBarrelRollPlan = null;
                return position;
            }
            else
            {
                string position = BarrelRollPositionsToTry.Last();
                BarrelRollPositionsToTry.RemoveAt(BarrelRollPositionsToTry.Count - 1);
                return position;
            }
        }

        protected List<BarrelRollPlanInfo> CreatePossibleBarrelRollPlans(List<string> availableBarrelRollTemplates)
        {
            List<BarrelRollPlanInfo> result = new();
            foreach (string template in availableBarrelRollTemplates)
            {
                foreach (string position in BarrelRollPositions)
                {
                    result.Add(new BarrelRollPlanInfo(template, position, Selection.ThisShip ?? throw new Exception("Selection.ThisShip is null in an unexpected place.")));
                }
            }

            return result;
        }

        protected List<BarrelRollPlanInfo> CreatePossibleBarrelRollPlans(List<ManeuverTemplate> availableBarrelRollTemplates)
        {
            List<string> barrelRollOptionStrings = new();
            foreach (ManeuverTemplate maneuverTemplate in availableBarrelRollTemplates)
            {
                if (maneuverTemplate.Bearing == ManeuverBearing.Straight)
                {
                    barrelRollOptionStrings.Add(BarrelRollPlanningSubPhase.NameTemplateOption(maneuverTemplate, Direction.Left));
                    barrelRollOptionStrings.Add(BarrelRollPlanningSubPhase.NameTemplateOption(maneuverTemplate, Direction.Right));
                }
                else
                {
                    if (maneuverTemplate.Direction == ManeuverDirection.Left)
                    {
                        barrelRollOptionStrings.Add(BarrelRollPlanningSubPhase.NameTemplateOption(maneuverTemplate, Direction.Right, Direction.Top));
                        barrelRollOptionStrings.Add(BarrelRollPlanningSubPhase.NameTemplateOption(maneuverTemplate, Direction.Left, Direction.Bottom));
                    }
                    else
                    {
                        barrelRollOptionStrings.Add(BarrelRollPlanningSubPhase.NameTemplateOption(maneuverTemplate, Direction.Left, Direction.Top));
                        barrelRollOptionStrings.Add(BarrelRollPlanningSubPhase.NameTemplateOption(maneuverTemplate, Direction.Right, Direction.Bottom));
                    }
                }
            }

            return CreatePossibleBarrelRollPlans(barrelRollOptionStrings);
        }

        public override void SyncDiceRerollSelected()
        {
            GameMode.CurrentGameMode.ExecuteCommand(DiceRerollManager.GenerateConfirmRerollCommand());
        }

        public override void AssignManeuversStart()
        {
            base.AssignManeuversStart();

            if (DebugManager.DebugStraightToCombat)
            {
                BeginAssignManeuversRecursive();
            }
            else
            {
                DoPlanningInPlanningPhase(BeginAssignManeuversRecursive);
            }
        }

        protected virtual void DoPlanningInPlanningPhase(Action callback)
        {
            callback();
        }

        private void BeginAssignManeuversRecursive()
        {
            ShipsWithoutManeuverAssignCommandSent = new(Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer).Ships.Values);
            PlanningPhaseAssignManeuversRecursive();
        }

        private void PlanningPhaseAssignManeuversRecursive()
        {
            GenericShip? ship = ShipsWithoutManeuverAssignCommandSent.LastOrDefault();

            if (ship != null)
            {
                Selection.ChangeActiveShip(ship);
                ShipsWithoutManeuverAssignCommandSent.RemoveAt(ShipsWithoutManeuverAssignCommandSent.Count - 1);
                if (DebugManager.DebugStraightToCombat)
                {
                    DirectionsMenu.Show(ShipMovementScript.SendAssignManeuverCommand, PlanningPhaseAssignManeuversRecursive, null, true);
                }
                else
                {
                    GameManagerScript.Wait(WaitAfterAssigningDial, delegate { DirectionsMenu.Show(ShipMovementScript.SendAssignManeuverCommand, PlanningPhaseAssignManeuversRecursive, null, true); });
                }
            }
            else
            {
                EndAssignManeuversRecursive();
            }
        }

        private void EndAssignManeuversRecursive()
        {
            GameMode.CurrentGameMode.ExecuteCommand(UI.GenerateNextButtonCommand());
        }

        public class BarrelRollPlanInfo
        {
            public string TemplateOptionName;
            private string[] SplitTemplateOptionName
            {
                get
                {
                    return TemplateOptionName.Split(' ');
                }
            }
            public string PositionOptionName;

            public GenericShip Ship;

            public ShipPositionInfo? FinalPositionInfo;

            public bool IsAllowed;

            public BarrelRollPlanInfo(string template, string position, GenericShip ship)
            {
                TemplateOptionName = template;
                PositionOptionName = position;
                Ship = ship;
            }

            public Direction GetDirectionPrimary()
            {
                switch (SplitTemplateOptionName.First())
                {
                    case "Right": 
                        return Direction.Right;
                    case "Left":
                        return Direction.Left;
                    default:
                        Console.Write("GenericAiPlayer.BarrelRollPlanInfo TemplateSplit[0] not recognised.", false, "red");
                        return Direction.None;
                }
            }

            public Direction GetDirectionSecondary()
            {
                if (SplitTemplateOptionName.Length == 3)
                {
                    return Direction.None;
                }
                else
                {
                    switch (SplitTemplateOptionName[3])
                    {
                        case "Forward": 
                            return Direction.Top;
                        case "Backwards":
                            return Direction.Bottom;
                        default:
                            Console.Write("GenericAiPlayer.BarrelRollPlanInfo TemplateSplit[3] not recognised.", false, "red");
                            return Direction.None;
                    }
                }
            }

            public Direction GetDirectionShift()
            {
                switch (PositionOptionName)
                {
                    case "Forward":
                        return Direction.Top;
                    case "Center":
                        return Direction.None;
                    case "Backwards":
                        return Direction.Bottom;
                    default:
                        Console.Write("GenericAiPlayer.BarrelRollPlanInfo Position not recognised.", false, "red");
                        return Direction.None;
                }
            }

            public Maneuver GetTemplate()
            {
                ManeuverBearing bearing;
                switch (SplitTemplateOptionName[1])
                {
                    case "Straight":
                        bearing = ManeuverBearing.Straight;
                        break;
                    case "Bank":
                        bearing = ManeuverBearing.Bank;
                        break;
                    case "Turn":
                        bearing = ManeuverBearing.Turn;
                        break;
                    default:
                        Console.Write("GenericAiPlayer.BarrelRollPlanInfo TemplateSplit[1] not recognised.", false, "red");
                        bearing = ManeuverBearing.None;
                        break;
                }

                ManeuverSpeed speed;
                switch (SplitTemplateOptionName[2])
                {
                    case "1":
                        speed = ManeuverSpeed.Speed1;
                        break;
                    case "2":
                        speed = ManeuverSpeed.Speed2;
                        break;
                    case "3":
                        speed = ManeuverSpeed.Speed3;
                        break;
                    case "4":
                        speed = ManeuverSpeed.Speed4;
                        break;
                    case "5":
                        speed = ManeuverSpeed.Speed5;
                        break;
                    default:
                        speed = ManeuverSpeed.Speed0;
                        Console.Write("GenericAiPlayer.BarrelRollPlanInfo TemplateSplit[2] not recognised.", false, "red");
                        break;
                }

                bool isLeftTemplate = GetDirectionPrimary() == Direction.Left ^ GetDirectionSecondary() == Direction.Top;
                ManeuverDirection direction = isLeftTemplate ? ManeuverDirection.Left : ManeuverDirection.Right;
                
                return new Maneuver(speed, direction, bearing);
            }

            public IEnumerator DoCalculations()
            {
                Maneuver maneuver = GetTemplate();
                bool isSideTemplate = Ship.ShipBase.Size != BaseSize.Small;
                ManeuverTemplate template = maneuver.ToManeuverTemplate(isSideTemplate);
                Direction primaryDirection = GetDirectionPrimary();
                template.ApplyTemplate(
                    Ship,
                    (primaryDirection == Direction.Left) ? Ship.GetLeft() : Ship.GetRight(),
                    primaryDirection
                    );
                template.Collider.TheShip = Ship;
                template.Collider.ReCheckCollisionsStart();
                template.ToggleRenderer(false);
                yield return new WaitForFixedUpdate();
                IsAllowed = BarrelRollPlanningSubPhase.IsColliderDataAllowed(template.Collider, Ship, true, false);
                Direction shiftDirection = GetDirectionShift();
                FinalPositionInfo = BarrelRollPlanningSubPhase.FinalPositionInfo(Ship.ShipBase, template, primaryDirection, shiftDirection);
                template.DestroyTemplate();
            }
        }

        public class BarrelRollActionWithPlans : BarrelRollAction
        {
            public List<BarrelRollPlanInfo> barrelRollPlans = new();
        }

    }
}