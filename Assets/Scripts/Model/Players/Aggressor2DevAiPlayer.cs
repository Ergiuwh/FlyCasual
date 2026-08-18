#nullable enable

using GameModes;
using SubPhases;

namespace Players
{
    public class Aggressor2DevAiPlayer : GenericAiPlayer
    {
        public Aggressor2DevAiPlayer() : base()
        {
            Name = "Aggressor AI Improved Version";

            NickName = "Aggressor 2 Dev";
            Title = "Assassin Droid";
            Avatar = "UpgradesList.SecondEdition.IG88D";
        }

        public override void AssignManeuversStart()
        {
            base.AssignManeuversStart();

            CalculateNavigation();
        }

        private void CalculateNavigation()
        {
            
        }

        private void OpenDirectionsUiSilent()
        {
            GameMode.CurrentGameMode.ExecuteCommand(
                PlanningSubPhase.GenerateSelectShipToAssignManeuver(Selection.ThisShip.ShipId)
            );
        }

        private void SendNextCommand()
        {
            GameMode.CurrentGameMode.ExecuteCommand(UI.GenerateNextButtonCommand());
        }
    }
}