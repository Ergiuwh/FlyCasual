#nullable enable

using ActionsList;
using AI.Aggressor;
using AI.Helpers;
using AI.Helpers.Types;
using Ship;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Players
{
    public partial class AggressorAiPlayer : GenericAiPlayer
    {
        public AggressorAiPlayer() : base()
        {
            Name = "Aggressor AI";

            NickName = "Aggressor";
            Title = "Assassin Droid";
            Avatar = "UpgradesList.SecondEdition.IG88D";
        }

        protected override void DoPlanningInPlanningPhase(Action callback)
        {
            NavigationSubSystem.CalculateNavigation(callback);
        }

        protected override GenericShip? SelectTargetForAttack()
        {
            if (DebugManager.DebugNoCombat) return null;

            return TargetingSubSystem.SelectTargetAndWeapon(Selection.ThisShip);
        }

        protected override GenericAction? SelectActionToPerformFromList(List<GenericAction> actionsList)
        {
            if (Selection.ThisShip is null ) { throw new Exception(); }

            AiPlanningLog.OpenNewGroup($"Action decision for {Selection.ThisShip}");

            Dictionary<GenericAction, int> actionsPriority = new();

            GenericShip ship = Selection.ThisShip;

            int redActionPriorityModifier = ship.GetAIStressPriority();

            foreach (GenericAction action in actionsList)
            {
                ship.CallOnCheckActionComplexity(action, ref action.Color);
                ship.CallOnCheckActionColor(action, ref action.Color);

                if (action.IsRed && ship.IsStressed && !ship.CallCanPerformActionWhileStressed(action))
                {
                    continue;
                }

                List<GenericAction> linkedActions;
                if (action.IsRed || ship.IsStressed)
                {
                    linkedActions = ship.ActionBar.LinkedActions
                    .Where(a => action.GetType().IsInstanceOfType(a.Key))
                    .Where(a => !ship.IsAlreadyExecutedAction(a.Value))
                    .Where(a => a.Value.CanBePerformedWhileStressed)
                    .Select(a => a.Value)
                    .ToList();
                }
                else
                {
                    linkedActions = ship.ActionBar.LinkedActions
                    .Where(a => action.GetType().IsInstanceOfType(a.Key))
                    .Where(a => !ship.IsAlreadyExecutedAction(a.Value))
                    .Select(a => a.Value)
                    .ToList();
                }

                int priority = NavigationSubSystem.GetActionPriority(action, ref CurrentBarrelRollPlan, ref CurrentBoostPlan, redActionPriorityModifier, linkedActions);
                if (priority == int.MinValue)
                {
                    continue;
                }

                AiPlanningLog.Add($"{(action.IsRed ? "Red " : "")}{action.Name} -> {priority}");
                actionsPriority.Add(action, priority);
            }

            actionsPriority = actionsPriority.OrderByDescending(n => n.Value)
                .ToDictionary(n => n.Key, n => n.Value);

            if (actionsPriority.Count > 0)
            {
                KeyValuePair<GenericAction, int> prioritizedAction = actionsPriority.First();

                if (prioritizedAction.Value > 0)
                {
                    AiPlanningLog.Add($"Selected action {prioritizedAction.Key}");
                    AiPlanningLog.CloseGroup();
                    return prioritizedAction.Key;
                }
            }
            
            AiPlanningLog.Add("Selected to skip action.");
            AiPlanningLog.CloseGroup();
            return null;
        }

        public override void SetupShip()
        {
            Roster.HighlightPlayer(PlayerNo);

            DeploymentSubSystem.SetupShip();
        }

        protected override GenericShip SelectShipToActivate()
        {
            if (DebugManager.DebugStraightToCombat)
            {
                return base.SelectShipToActivate();
            }
            else
            {
                return NavigationSubSystem.GetNextShipWithoutFinishedManeuver();
            }
        }

        protected override Maneuver ChooseManeuverFrom(List<Maneuver> options)
        {
            return NavigationSubSystem.SelectManeuverFrom(
                options,
                Selection.ThisShip ?? throw new Exception("Selection.ThisShip null in unexpected place.")
                );
        }

        protected override Maneuver ChooseManeuverToExecuteFrom(List<Maneuver> options)
        {
            return NavigationSubSystem.SelectManeuverToExecuteFrom(
                options,
                Selection.ThisShip ?? throw new Exception("Selection.ThisShip null in unexpected place.")
                );
        }

        protected override bool DoCalculateBarrelRollResults()
        {
            return true;
        }

        protected override bool DoCalculateBoostResults()
        {
            return true;
        }
    }

    public class AggressorAiPlayerQuick : AggressorAiPlayer
    {
        public AggressorAiPlayerQuick() : base()
        {
            Name = "Aggressor AI (fast)";

            NickName = "Aggressor (fast)";
        }

        protected override void DoPlanningInPlanningPhase(Action callback)
        {
            NavigationSubSystem.CalculateNavigationQuick(callback);
        }

        protected override bool DoCalculateBarrelRollResults()
        {
            return false;
        }

        protected override bool DoCalculateBoostResults()
        {
            return false;
        }
    }
}