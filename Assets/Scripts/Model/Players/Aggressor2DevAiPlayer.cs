#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ActionsList;
using AI.Aggressor2Dev.Navigation;
using AI.Aggressor2Dev.Targeting;
using AI.Helpers.Navigation;
using BoardTools;
using GameModes;
using Ship;
using SubPhases;

namespace Players
{
    public class Aggressor2DevAiPlayer : GenericAiPlayer
    {
        private DialPlan? DialPlan;

        private Dictionary<string, VirtualBoardWrapper<VirtualBoardData>> VirtualBoardCache;

        public Aggressor2DevAiPlayer() : base()
        {
            Name = "Aggressor AI Improved Version";

            NickName = "Aggressor 2 Dev";
            Title = "Assassin Droid";
            Avatar = "UpgradesList.SecondEdition.IG88D";

            VirtualBoardCache = new();
        }

        public override void AssignManeuversStart()
        {
            base.AssignManeuversStart();

            GameManagerScript.Instance.StartCoroutine
            (
                CalculateNavigation()
            );
        }

        private IEnumerator CalculateNavigation()
        {
            yield return PlanDialFns.CreateDialPlan(out DialPlan);

            AssignManeuversRecursive();
        }

        private void AssignManeuversRecursive()
        {
            GenericShip shipWithoutManeuver = GetAnyShipWithoutAssignedManeuver();
            if (shipWithoutManeuver == null)
            {
                SendNextCommand();
            }
            else
            {
                Selection.ChangeActiveShip(shipWithoutManeuver);
                OpenDirectionsUiSilent();
            }
        }

        private GenericShip GetAnyShipWithoutAssignedManeuver()
        {
            return Roster.GetPlayer(Phases.CurrentSubPhase.RequiredPlayer).Ships.Values
                .First(n => n.AssignedManeuver == null);
        }

        private void OpenDirectionsUiSilent()
        {
            GameMode.CurrentGameMode.ExecuteCommand(
                PlanningSubPhase.GenerateSelectShipToAssignManeuver(Selection.ThisShip.ShipId)
            );
        }

        public override void AskAssignManeuver()
        {
            if (DialPlan == null)
            {
                throw new Exception();
            }
            ShipMovementScript.SendAssignManeuverCommand(DialPlan.Values[Selection.ThisShip].ToString());
            GameManagerScript.Wait(WaitAfterAssigningDial, delegate { Selection.DeselectThisShip(); AssignManeuversRecursive(); });
        }

        private void SendNextCommand()
        {
            GameMode.CurrentGameMode.ExecuteCommand(UI.GenerateNextButtonCommand());
        }

        protected override GenericShip? SelectTargetForAttack()
        {
            if (DebugManager.DebugNoCombat) return null;

            AttackResult? chosenAttackResult = TargetingFns.SelectBestAttack(Selection.ThisShip, new());
            if (chosenAttackResult == null)
            {
                return null;
            }

            Combat.ChosenWeapon = chosenAttackResult.Weapon;
            Combat.ShotInfo = new ShotInfo(Selection.ThisShip, chosenAttackResult.Defender, chosenAttackResult.Weapon);
            return chosenAttackResult.Defender;
        }

        protected override void PerformActionFromList(List<GenericAction> actionsList)
        {
            GenericAction? selectedAction = SelectActionFromList(actionsList);
            if (selectedAction == null)
            {
                GameMode.CurrentGameMode.ExecuteCommand(UI.GenerateSkipButtonCommand());
            }
            else
            {
                JSONObject parameters = new();
                parameters.AddField("name", selectedAction.Name);
                GameController.SendCommand(
                    GameCommandTypes.Decision,
                    Phases.CurrentSubPhase.GetType(),
                    Phases.CurrentSubPhase.ID,
                    parameters.ToString()
                );
            }
        }

        private GenericAction? SelectActionFromList(List<GenericAction> actionsList)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This currently uses Aggressor's deployment.
        /// </summary>
        public override void SetupShip()
        {
            Roster.HighlightPlayer(PlayerNo);

            AI.Aggressor.DeploymentSubSystem.SetupShip();
        }

        public override void PerformManeuver()
        {
            Roster.HighlightPlayer(PlayerNo);

            List<GenericShip> shipsCanActivate = Roster.GetPlayer(Phases.CurrentPhasePlayer).Ships.Values
                .Where(ship => ship.State.Initiative == Phases.CurrentSubPhase.RequiredInitiative)
                .ToList();

            if (shipsCanActivate.Count == 0)
            {
                Phases.Next();
            }
            else
            {
                GenericShip shipToActivate = AI.Aggressor2Dev.Coordination.ActivationPhaseOrder.SelectShipFromList(shipsCanActivate);
                Selection.ChangeActiveShip("ShipId:" + shipToActivate.ShipId);
                ActivateShip(shipToActivate);
            }
        }
    }
}