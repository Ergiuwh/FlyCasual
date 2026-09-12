using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Ship;
using ActionsList;
using BoardTools;
using SubPhases;
using GameModes;
using GameCommands;
using AI.Helpers.Types;
using MainPhases;

namespace Players
{

    public partial class GenericAiPlayer : GenericPlayer
    {
        public static float WaitAfterAssigningDial = 0.2f;
        public static float WaitAfterDeployingShip = 0.5f;
        public static float WaitAfterDiceModification = 1f;
        public static float WaitAfterPlacingObstacle = 1f;
        public static float WaitWhenInformingAboutCrit = 3f;

        public GenericAiPlayer() : base()
        {
            PlayerType = PlayerType.Ai;
            Name = "AI";
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

            bool foundToActivate = false;
            foreach (var shipHolder in Roster.GetPlayer(Phases.CurrentPhasePlayer).Ships)
            {
                if (shipHolder.Value.State.Initiative == Phases.CurrentSubPhase.RequiredInitiative)
                {
                    if (!shipHolder.Value.IsManeuverPerformed)
                    {
                        foundToActivate = true;
                        Selection.ChangeActiveShip("ShipId:" + shipHolder.Value.ShipId);
                        ActivateShip(shipHolder.Value);
                        break;
                    }
                }
            }

            if (!foundToActivate)
            {
                Phases.Next();
            }
        }

        public virtual void ActivateShip(GenericShip ship)
        {
            Selection.ChangeActiveShip("ShipId:" + ship.ShipId);
            PerformManeuverOfShip(ship);
        }

        protected void PerformManeuverOfShip(GenericShip ship)
        {
            ship.IsManeuverPerformed = true;
            GameCommand command = ShipMovementScript.GenerateActivateAndMoveCommand(Selection.ThisShip.ShipId);
            GameMode.CurrentGameMode.ExecuteCommand(command);
        }

        //TODO: Don't skip attack of all PS ships if one cannot attack (Biggs interaction)

        public override void PerformAttack()
        {
            base.PerformAttack();

            GenericShip attacker = GetShipThatCanAttack();

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
                GenericShip targetForAttack = SelectTargetForAttack();

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

        protected virtual GenericShip SelectTargetForAttack()
        {
            if (DebugManager.DebugNoCombat) return null;

            return AI.HotAC.TargetForAttackSelector.SelectTargetAndWeapon(Selection.ThisShip);
        }

        private static GenericShip GetShipThatCanAttack()
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

        public GenericShip FindNearestEnemyShip(GenericShip thisShip, bool ignoreCollided = false, bool inArcAndRange = false)
        {
            Dictionary<GenericShip, float> results = GetEnemyShipsAndDistance(thisShip, ignoreCollided, inArcAndRange);
            GenericShip result = null;
            if (results.Count != 0)
            {
                result = results.OrderBy(n => n.Value).First().Key;
            }
            return result;
        }


        // TODO: Remove, used in AI/HotAC/TargetForAttackSelector
        public Dictionary<GenericShip, float> GetEnemyShipsAndDistance(GenericShip thisShip, bool ignoreCollided = false, bool inArcAndRange = false)
        {
            Dictionary<GenericShip, float> results = new Dictionary<GenericShip, float>();

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
                        BoardTools.DistanceInfo distanceInfo = new BoardTools.DistanceInfo(thisShip, shipHolder.Value);
                        if ((distanceInfo.Range > 3))
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

            Dictionary<GenericAction, int> actionsPriority = new Dictionary<GenericAction, int>();

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
                ShipMovementScript.SendAssignManeuverCommand("2.F.S");
            }
            else
            {
                Func<string, bool> stringFilter = DirectionsMenu.Filter;
                
                Maneuver maneuver;

                if (stringFilter == null)
                {
                    maneuver = GetManeuverDecisionFnFromFilter()(_ => true);
                }
                else
                {
                    maneuver = GetManeuverDecisionFnFromFilter()(ManeuverFilterFromStringFilter(stringFilter));
                }

                ShipMovementScript.SendAssignManeuverCommand(maneuver.ToString());
            }
        }

        public override void ChangeManeuver(Action<string> doWithManeuverString, Action callback, Func<string, bool> filter = null)
        {
            base.ChangeManeuver(doWithManeuverString, callback, filter);

            DirectionsMenu.Show(doWithManeuverString, callback, filter);
        }

        public override void SelectManeuver(Action<string> doWithManeuverString, Action callback, Func<string, bool> filter = null)
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
        protected Maneuver ChooseManeuverToExecuteFromFilter(Func<Maneuver, bool> filter = null)
        {
            return ChooseManeuverToExecuteFrom(Selection.ThisShip.Maneuvers.Select(a => new Maneuver(a.Key, Selection.ThisShip)).Where(filter).ToList());
        }

        /// <summary>
        /// Selects highest priority maneuver. Assumes highest priority is > 0.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="priorityFn"></param>
        /// <returns></returns>
        protected Maneuver ChooseManeuverFrom(List<Maneuver> options, Func<Maneuver, int> priorityFn)
        {
            Maneuver bestManeuver = options[0];
            int bestPriority = 0;
            foreach (Maneuver maneuver in options)
            {
                int priority = priorityFn(maneuver);
                if (priority > bestPriority)
                {
                    bestManeuver = maneuver;
                    bestPriority = priority;
                }
            }

            return bestManeuver;
        }

        protected Maneuver ChooseManeuverFromFilter(Func<Maneuver, int> priorityFn, Func<Maneuver, bool> filter = null)
        {
            return ChooseManeuverFrom(Selection.ThisShip.Maneuvers.Select(a => new Maneuver(a.Key, Selection.ThisShip)).Where(filter).ToList(), priorityFn);
        }

        protected virtual Maneuver ChooseManeuverFrom(List<Maneuver> options)
        {
            return options[0];
        }

        protected Maneuver ChooseManeuverFromFilter(Func<Maneuver, bool> filter = null)
        {
            return ChooseManeuverFrom(Selection.ThisShip.Maneuvers.Select(a => new Maneuver(a.Key, Selection.ThisShip)).Where(filter).ToList());
        }

        public override void StartExtraAttack()
        {
            GenericShip targetForAttack = SelectTargetForAttack();

            if (targetForAttack != null)
            {
                Selection.ThisShip.IsAttackPerformed = true;

                Selection.AnotherShip = targetForAttack;
                Combat.TryPerformAttack(isSilent: true);
            }
            else
            {
                UI.SkipButtonPressedEffect();
            }
        }

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

        public override void RerollManagerIsPrepared()
        {
            base.RerollManagerIsPrepared();
            DiceRerollManager.CurrentDiceRerollManager.ConfirmRerollButtonIsPressed();
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

        public override void TakeDecision()
        {
            DecisionSubPhase subphase = (Phases.CurrentSubPhase as DecisionSubPhase);

            if (subphase.IsForced)
            {
                JSONObject parameters = new JSONObject();
                parameters.AddField("name", subphase.GetDecisions().First().Name);
                GameController.SendCommand(
                    GameCommandTypes.Decision,
                    Phases.CurrentSubPhase.GetType(),
                    Phases.CurrentSubPhase.ID,
                    parameters.ToString()
                );
            }
            else if (Phases.CurrentSubPhase is ActionDecisonSubPhase)
            {
                PerformActionFromList(Selection.ThisShip.GetAvailableActions());
            }
            else if (Phases.CurrentSubPhase is FreeActionDecisonSubPhase)
            {
                PerformActionFromList(Selection.ThisShip.GetAvailableFreeActions());
            }
            else (Phases.CurrentSubPhase as DecisionSubPhase).DoDefault();
        }

        protected virtual void PerformActionFromList(List<GenericAction> actionsList) { }

        public override void SyncDiceRerollSelected()
        {
            GameMode.CurrentGameMode.ExecuteCommand(DiceRerollManager.GenerateConfirmRerollCommand());
        }
    }
}