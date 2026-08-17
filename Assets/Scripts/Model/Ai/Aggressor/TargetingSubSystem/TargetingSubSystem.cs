using BoardTools;
using Remote;
using Ship;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Upgrade;

namespace AI.Aggressor
{
    public class AttackDecision
    {
        public GenericShip CurrentShip { get; private set; }
        public GenericShip TargetShip { get; private set; }
        public IShipWeapon Weapon { get; private set; }
        public int Priority { get; private set; }

        public AttackDecision(GenericShip currentShip, GenericShip targetShip, IShipWeapon weapon)
        {
            CurrentShip = currentShip;
            TargetShip = targetShip;
            Weapon = weapon;

            CalculatePriority();
        }

        private void CalculatePriority()
        {
            ShotInfo shotInfo = new(CurrentShip, TargetShip, Weapon);

            float averageHits = Helpers.AttackCalculations.FastAttackCalculations.AverageHits(shotInfo, CurrentShip, TargetShip, Weapon);
            float averageCrits = Helpers.AttackCalculations.FastAttackCalculations.AverageCrits(shotInfo, CurrentShip, TargetShip, Weapon);
            float averageEvades = Helpers.AttackCalculations.FastAttackCalculations.AverageEvades(shotInfo, TargetShip, Weapon);

            float averageDamageIncorrect = averageHits - averageEvades;

            float targetHP = TargetShip.State.HullCurrent + TargetShip.State.ShieldsCurrent;
            float damageImpact = averageDamageIncorrect / targetHP;
            if (targetHP < averageDamageIncorrect) damageImpact *= 2;

            float shipCost = TargetShip.PilotInfo.Cost;

            IShipWeapon currentWeapon;
            GenericUpgrade currentUpgrade = null;

            // Find the upgrade that matches our current weapon.
            foreach (GenericUpgrade upgrade in Selection.ThisShip.UpgradeBar.GetSpecialWeaponsActive())
            {
                if (upgrade is GenericSpecialWeapon)
                {
                    currentWeapon = (upgrade as IShipWeapon);
                    if (currentWeapon.Name == Weapon.Name)
                    {
                        currentUpgrade = upgrade;
                        break;
                    }
                }
            }

            // If our current weapon uses charges and has inadequate charges available, don't use it.
            if (currentUpgrade != null && Weapon.WeaponInfo.UsesCharges && currentUpgrade.UpgradeInfo.Charges > currentUpgrade.State.Charges)
            {
                Priority = 0;
            }
            else if (TargetShip is GenericRemote)
            {
                Priority = 1;
            }
            else
            {
                int priority = (int)(damageImpact * 1000f + averageCrits * 100f + shipCost);
                CurrentShip.Ai.CallGetWeaponPriority(TargetShip, Weapon, ref priority);
                Priority = priority;
            }
        }
    }

    public static class TargetingSubSystem
    {
        public static GenericShip CurrentShip { get; private set; }

        public static List<AttackDecision> AttackDecisions;

        public static GenericShip SelectTargetAndWeapon(GenericShip ship)
        {
            CurrentShip = ship;
            AttackDecisions = new List<AttackDecision>();

            foreach (GenericShip enemyShip in GetEnemyShipsAndDistance(CurrentShip, inArcAndRange: true))
            {
                bool isAllowedByAbility = true;
                ship.CallTargetForAttackIsAllowed(enemyShip, ref isAllowedByAbility);

                if (!isAllowedByAbility) continue;

                Selection.AnotherShip = enemyShip;
                foreach (IShipWeapon weapon in CurrentShip.GetAllWeapons())
                {
                    if (Rules.TargetIsLegalForShot.IsLegal(CurrentShip, enemyShip, weapon, isSilent: true))
                    {
                        AttackDecisions.Add(new AttackDecision(CurrentShip, enemyShip, weapon));
                    }
                }
            }

            AttackDecision BestAttackDecision = AttackDecisions.OrderByDescending(n => n.Priority).FirstOrDefault();
            if (BestAttackDecision != null)
            {
                Combat.ChosenWeapon = BestAttackDecision.Weapon;
                Combat.ShotInfo = new ShotInfo(CurrentShip, BestAttackDecision.TargetShip, BestAttackDecision.Weapon);

                return BestAttackDecision.TargetShip;
            }
            else
            {
                return null;
            }
        }

        private static List<GenericShip> GetEnemyShipsAndDistance(GenericShip thisShip, bool ignoreCollided = false, bool inArcAndRange = false)
        {
            Dictionary<GenericShip, float> results = new();

            List<GenericShip> enemyTargets = Roster.GetPlayer(Roster.AnotherPlayer(thisShip.Owner.PlayerNo)).Ships.Values.ToList();
            enemyTargets.AddRange(Roster.GetPlayer(Roster.AnotherPlayer(thisShip.Owner.PlayerNo)).Units.Values.Where(n => n is GenericRemote).Cast<GenericShip>());

            foreach (GenericShip enemyShip in enemyTargets)
            {
                if (!enemyShip.IsDestroyed)
                {
                    if (ignoreCollided)
                    {
                        if (thisShip.LastShipCollision != null
                            && thisShip.LastShipCollision.ShipId == enemyShip.ShipId)
                        {
                            continue;
                        }


                        if (enemyShip.LastShipCollision != null
                            && enemyShip.LastShipCollision.ShipId == thisShip.ShipId)
                        {
                            continue;
                        }
                    }

                    if (inArcAndRange)
                    {
                        DistanceInfo distanceInfo = new(thisShip, enemyShip);

                        if ((distanceInfo.Range > 3))
                        {
                            continue;
                        }
                    }

                    float distance = Vector3.Distance(thisShip.GetCenter(), enemyShip.GetCenter());
                    results.Add(enemyShip, distance);
                }
            }

            results = results.OrderBy(n => n.Value).ToDictionary(n => n.Key, n => n.Value);

            return results.Select(n => n.Key).ToList();
        }
    }
}
