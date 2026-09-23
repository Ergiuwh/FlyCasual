#nullable enable

using ActionsList;
using AI.Helpers.Types;
using GameModes;
using MainPhases;
using Ship;
using SubPhases;
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
            AI.Aggressor.NavigationSubSystem.CalculateNavigation(callback);
        }

        protected override GenericShip? SelectTargetForAttack()
        {
            if (DebugManager.DebugNoCombat) return null;

            return AI.Aggressor.TargetingSubSystem.SelectTargetAndWeapon(Selection.ThisShip);
        }

        protected override void PerformActionFromList(List<GenericAction> actionsList)
        {
            if (Selection.ThisShip is null ) { throw new Exception(); }

            bool isActionTaken = false;

            List<GenericAction> availableActionsList = actionsList;

            Dictionary<GenericAction, int> actionsPriority = new();

            GenericShip ship = Selection.ThisShip;

            foreach (GenericAction action in availableActionsList)
            {
                ship.CallOnCheckActionComplexity(action, ref action.Color);
                ship.CallOnCheckActionColor(action, ref action.Color);

                int priority = action.GetActionPriority();
                ship.Ai.CallGetActionPriority(action, ref priority);

                // De-prioritize red actions unless overriden
                if (action.IsRed)
                {
                    if (ship.IsStressed && !ship.CallCanPerformActionWhileStressed(action)) continue;

                    double redActionPriorityModifier = 0.2;
                    ship.Ai.CallGetRedActionPriorityModifier(action, ref redActionPriorityModifier);
                    priority = (int)(priority * redActionPriorityModifier);
                }

                actionsPriority.Add(action, priority);
            }

            actionsPriority = actionsPriority.OrderByDescending(n => n.Value)
                .ToDictionary(n => n.Key, n => n.Value);

            if (actionsPriority.Count > 0)
            {
                KeyValuePair<GenericAction, int> prioritizedActions = actionsPriority.First();

                if (prioritizedActions.Value > 0)
                {
                    isActionTaken = true;

                    JSONObject parameters = new();
                    parameters.AddField("name", prioritizedActions.Key.Name);
                    GameController.SendCommand(
                        GameCommandTypes.Decision,
                        Phases.CurrentSubPhase.GetType(),
                        Phases.CurrentSubPhase.ID,
                        parameters.ToString()
                    );
                }
            }

            if (!isActionTaken)
            {
                GameMode.CurrentGameMode.ExecuteCommand(UI.GenerateSkipButtonCommand());
            }
        }

        public override void SetupShip()
        {
            Roster.HighlightPlayer(PlayerNo);

            AI.Aggressor.DeploymentSubSystem.SetupShip();
        }

        protected override GenericShip SelectShipToActivate()
        {
            if (DebugManager.DebugStraightToCombat)
            {
                return base.SelectShipToActivate();
            }
            else
            {
                return AI.Aggressor.NavigationSubSystem.GetNextShipWithoutFinishedManeuver();
            }
        }

        protected override Maneuver ChooseManeuverFrom(List<Maneuver> options)
        {
            return AI.Aggressor.NavigationSubSystem.SelectManeuverFrom(
                options,
                Selection.ThisShip ?? throw new Exception("Selection.ThisShip null in unexpected place.")
                );
        }

        protected override Maneuver ChooseManeuverToExecuteFrom(List<Maneuver> options)
        {
            return AI.Aggressor.NavigationSubSystem.SelectManeuverToExecuteFrom(
                options,
                Selection.ThisShip ?? throw new Exception("Selection.ThisShip null in unexpected place.")
                );
        }
    }
}