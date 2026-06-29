using ActionsList;
using BoardTools;
using Movement;
using System.Collections.Generic;
using Upgrade;

namespace UpgradesList.SecondEdition
{
    public class OverdriveThruster : GenericUpgrade
    {
        public OverdriveThruster() : base()
        {
            UpgradeInfo = new UpgradeCardInfo(
                "Overdrive Thruster",
                UpgradeType.Modification,
                cost: 5,
                isLimited: true,
                restriction: new ShipRestriction(typeof(Ship.SecondEdition.T70XWing.T70XWing)),
                abilityType: typeof(Abilities.SecondEdition.OverdriveThrusterAbility)
            );

            
        }
    }
}

namespace Abilities.SecondEdition
{
    public class OverdriveThrusterAbility : GenericAbility
    {
        public override void ActivateAbility()
        {
            HostShip.OnGetAvailableBoostTemplates += UpdateBoostTemplate;
            HostShip.OnGetAvailableBarrelRollTemplates += UpdateBarrelRollTemplate;
            HostShip.OnUpdateChosenSlamTemplate += UpdateSlamTemplate;
        }

        public override void DeactivateAbility()
        {
            HostShip.OnGetAvailableBoostTemplates -= UpdateBoostTemplate;
            HostShip.OnGetAvailableBarrelRollTemplates -= UpdateBarrelRollTemplate;
            HostShip.OnUpdateChosenSlamTemplate -= UpdateSlamTemplate;
        }

        private void UpdateBoostTemplate(List<BoostMove> availableTemplates, GenericAction action)
        {
            if (action.IsRed)
            {
                List<BoostMove> newTemplates = new();

                foreach (BoostMove template in availableTemplates)
                {
                    BoostMove newMove = new(
                        BoostMove.GetBoostTemplateFromName(template.Name.Replace('1', '2')),
                        template.IsRed,
                        template.IsPurple,
                        template.IsForced);

                    newTemplates.Add(newMove);
                }

                availableTemplates.Clear();
                availableTemplates.AddRange(newTemplates);
            }
        }

        private void UpdateBarrelRollTemplate(List<ManeuverTemplate> availableTemplates, GenericAction action)
        {
            if (action.IsRed)
            {
                foreach (ManeuverTemplate template in availableTemplates)
                {
                    template.TryIncreaseSpeed();
                }
            }
        }

        private void UpdateSlamTemplate(GenericMovement movement)
        {
            if (ActionsHolder.CurrentAction.IsRed)
            {
                if (movement.TryIncreaseSpeed())
                {
                    Messages.ShowInfo("Overdrive Thursters: Template of 1 speed higher is used");
                }
            }
        }
    }
}
