#nullable enable

using ActionsList;
using AI.Aggressor;
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

            DebugManager.AiPlanningLog.OpenNewGroup($"Action decision for {Selection.ThisShip}");

            Dictionary<GenericAction, int> actionsPriority = new();

            GenericShip ship = Selection.ThisShip;

            int redActionPriorityModifier = ship.GetAIStressPriority();

            foreach (GenericAction action in actionsList)
            {
                ship.CallOnCheckActionComplexity(action, ref action.Color);
                ship.CallOnCheckActionColor(action, ref action.Color);

                int priority = NavigationSubSystem.GetActionPriority(action, ref CurrentBarrelRollPlan, ref CurrentBoostPlan);
                if (priority == int.MinValue)
                {
                    continue;
                }

                ship.Ai.CallGetActionPriority(action, ref priority);

                // De-prioritize red actions unless overriden
                if (action.IsRed)
                {
                    if (ship.IsStressed && !ship.CallCanPerformActionWhileStressed(action)) continue;

                    priority += redActionPriorityModifier;
                }

                DebugManager.AiPlanningLog.Add($"{action.Name} -> {priority}");
                actionsPriority.Add(action, priority);
            }

            actionsPriority = actionsPriority.OrderByDescending(n => n.Value)
                .ToDictionary(n => n.Key, n => n.Value);

            if (actionsPriority.Count > 0)
            {
                KeyValuePair<GenericAction, int> prioritizedAction = actionsPriority.First();

                if (prioritizedAction.Value > 0)
                {
                    DebugManager.AiPlanningLog.Add($"Selected action {prioritizedAction.Key}");
                    DebugManager.AiPlanningLog.CloseGroup();
                    return prioritizedAction.Key;
                }
            }
            
            DebugManager.AiPlanningLog.Add("Selected to skip action.");
            DebugManager.AiPlanningLog.CloseGroup();
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

    public class AggressorAiPlayerQuick : GenericAiPlayer
    {
        public bool DoRepositionActions;
        public AggressorAiPlayerQuick() : base()
        {
            Name = "Aggressor AI (quick)";
            DoRepositionActions = false;
        }

        protected override void DoPlanningInPlanningPhase(Action callback)
        {
            NavigationSubSystem.CalculateNavigationQuick(callback);
        }

        protected override bool DoCalculateBarrelRollResults()
        {
            return DoRepositionActions;
        }

        protected override bool DoCalculateBoostResults()
        {
            return DoRepositionActions;
        }
    }
}