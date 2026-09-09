#nullable enable annotations

using Movement;
using Ship;
using Tokens;

namespace ActionsList
{

    public class SlamAction : GenericAction
    {
        private GenericMovement? savedManeuver;
        private ShipPositionInfo? savedPositionInfo;

        public SlamAction()
        {
            Name = DiceModificationName = "SLAM";
            ImageUrl = "https://raw.githubusercontent.com/guidokessels/xwing-data/master/images/reference-cards/SlamAction.png";
        }

        public SlamAction(GenericShip hostShip)
        {
            Name = DiceModificationName = "SLAM";
            ImageUrl = "https://raw.githubusercontent.com/guidokessels/xwing-data/master/images/reference-cards/SlamAction.png";
            HostShip = hostShip;
        }

        public override bool IsActionAvailable()
        {
            return Phases.CurrentPhase is MainPhases.ActivationPhase
                && (Phases.CurrentPhase as MainPhases.ActivationPhase).ActivationShip == HostShip;
        }

        public override void ActionTake()
        {
            if (Selection.ThisShip.Owner.UsesHotacAiRules)
            {
                Phases.CurrentSubPhase.CallBack();
            }
            else
            {
                Phases.CurrentSubPhase.Pause();

                Selection.ThisShip.Owner.SelectManeuver(
                    DoWithManeuver,
                    delegate {  },
                    IsSameSpeed
                );
            }
        }

        /// <summary>
        /// Calls ship.CallUpdateChosenSlamTemplate()
        /// </summary>
        /// <param name="callback"></param>
        private void DoWithManeuver(string maneuverCode)
        {
            savedManeuver = Selection.ThisShip.AssignedManeuver;
            savedPositionInfo = Selection.ThisShip.GetPositionInfo();

            ShipMovementScript.SendAssignManeuverCommand(maneuverCode);
            Selection.ThisShip.AssignedManeuver.IsRevealDial = false;

            Selection.ThisShip.CallUpdateChosenSlamTemplate(Selection.ThisShip.AssignedManeuver);

            ShipMovementScript.LaunchMovement(AssignWeaponsDisabledToken);
        }

        private void AssignWeaponsDisabledToken()
        {
            Selection.ThisShip.Tokens.AssignToken(typeof(WeaponsDisabledToken), FinishSlam);
        }

        private void FinishSlam()
        {
            Selection.ThisShip.SetAssignedManeuver(savedManeuver);
            Selection.ThisShip.CallSlam(Phases.CurrentSubPhase.CallBack);
        }

        private bool IsSameSpeed(string maneuverString)
        {
            bool result = false;
            ManeuverHolder movementStruct = new(maneuverString);
            if (movementStruct.Speed == Selection.ThisShip.AssignedManeuver.ManeuverSpeed)
            {
                result = true;
            }
            return result;
        }

        public override int GetActionPriority()
        {
            int result = 0;
            return result;
        }
    }
}