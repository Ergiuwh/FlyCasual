using BoardTools;
using Editions;
using Remote;
using Ship;
using System;
using System.Collections.Generic;
using System.Linq;
using Tokens;
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
                (LimitedDiscreteProbabilityDistribution damageResult, float averageCrits) = CalculateAttackDiceDistribution(CurrentShip, TargetShip, Weapon);

                float averageDamage = damageResult.Average();
                float targetHP = TargetShip.State.HullCurrent + TargetShip.State.ShieldsCurrent;
                float damageImpact = averageDamage / targetHP;
                if (targetHP < averageDamage) damageImpact *= 2;

                float shipCost = TargetShip.PilotInfo.Cost;

                int priority = (int)(damageImpact * 1000f + averageCrits * 100f + shipCost);
                CurrentShip.Ai.CallGetWeaponPriority(TargetShip, Weapon, ref priority);
                Priority = priority;
            }
        }

        private static (LimitedDiscreteProbabilityDistribution damageResult, float averageCrits) CalculateAttackDiceDistribution(GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            ShotInfo shotInfo = new(attacker, defender, weapon);

            (LimitedDiscreteProbabilityDistribution attackDice, float averageCrits) = CalculateAttackerDiceDistribution(shotInfo, attacker, defender, weapon);
            LimitedDiscreteProbabilityDistribution defenceDice = CalculateDefenderDiceDistribution(shotInfo, attacker, defender, weapon);

            LimitedDiscreteProbabilityDistribution damageResult = LimitedDiscreteProbabilityDistribution.CreateAttackResult(attackDice,defenceDice);

            return (damageResult, averageCrits);
        }

        private static (LimitedDiscreteProbabilityDistribution distribution, float averageCrits) CalculateAttackerDiceDistribution(ShotInfo shotInfo, GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            const float attackDiceChanceUnmodified = 0.5f;
            const float attackDiceChanceSingleModification = 0.75f;
            const float attackDiceChanceFullModification = 0.938f;

            const float potentialCritsNoReroll = 0.125f;
            const float potentialCritsWithReroll = 0.1875f;


            int attackDiceThrown = weapon.WeaponInfo.AttackValue;
            if (shotInfo.Range <= 1 && Edition.Current.IsWeaponHaveRangeBonus(weapon)) attackDiceThrown++;

            float attackDiceModifier;
            float criticalHitsModifier = potentialCritsNoReroll;
            if (attacker.Tokens.HasToken<FocusToken>() && ActionsHolder.HasTargetLockOn(attacker, defender))
            {
                attackDiceModifier = attackDiceChanceFullModification;
            }
            else if (attacker.Tokens.HasToken<FocusToken>() || ActionsHolder.HasTargetLockOn(attacker, defender))
            {
                attackDiceModifier = attackDiceChanceSingleModification;
                if (ActionsHolder.HasTargetLockOn(attacker, defender)) criticalHitsModifier = potentialCritsWithReroll;
            }
            else
            {
                attackDiceModifier = attackDiceChanceUnmodified;
            }
            
            float averageCrits = attackDiceThrown * criticalHitsModifier;
            return (LimitedDiscreteProbabilityDistribution.CreateFromSuccessChance(attackDiceThrown,attackDiceModifier),averageCrits);
        }

        private static LimitedDiscreteProbabilityDistribution CalculateDefenderDiceDistribution(ShotInfo shotInfo, GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            const float defenceDiceChanceUnmodified = 0.375f;
            const float defenceDiceChanceFocusModification = 0.625f;

            int defenceDiceThrown = defender.State.Agility;
            if (shotInfo.Range == 3 && !Edition.Current.IsWeaponHaveRangeBonus(weapon)) defenceDiceThrown++;
            if (shotInfo.IsObstructedByObstacle) defenceDiceThrown++;

            float defenceDiceModifier;
            if (defender.Tokens.HasToken<FocusToken>())
            {
                defenceDiceModifier = defenceDiceChanceFocusModification;
            }
            else
            {
                defenceDiceModifier = defenceDiceChanceUnmodified;
            }

            LimitedDiscreteProbabilityDistribution potentialEvades = LimitedDiscreteProbabilityDistribution.CreateFromSuccessChance(defenceDiceThrown, defenceDiceModifier);
            if (defender.Tokens.HasToken<EvadeToken>() && defenceDiceThrown > 0)
            {
                potentialEvades.ModificationSetFailToSuccess();
            }

            return potentialEvades;
        }
    }

    public class LimitedDiscreteProbabilityDistribution
    {
        public List<float> values;

        public LimitedDiscreteProbabilityDistribution(List<float> values)
        {
            this.values = values;
        }

        public void ModificationSetFailToSuccess()
        {
            values[^1] += values[^2];
            for (int i = values.Count - 2; i > 0; i--)
            {
                values[i] = values[i-1];
            }
            values[0] = 0;
        }

        public float Average()
        {
            float r = 0;
            for (int i = 1; i < values.Count; i++)
            {
                r += i * values[i];
            }
            return r;
        }

        public float ChanceEqualOrGreaterThan(int number)
        {
            return values.GetRange(number,values.Count-number).Sum();
        }

        public float ChanceLessThan(int number)
        {
            return values.GetRange(0,Math.Min(values.Count,number)).Sum();
        }

        private static float ChanceASubBEquals(int number, LimitedDiscreteProbabilityDistribution attack, LimitedDiscreteProbabilityDistribution defence)
        {
            float r = 0f;
            for (int i = number, j = 0; i < attack.values.Count && j < defence.values.Count; i++, j++)
            {
                r += attack.values[i] * defence.values[j];
            }
            return r;
        }

        public static LimitedDiscreteProbabilityDistribution CreateAttackResult(LimitedDiscreteProbabilityDistribution attack, LimitedDiscreteProbabilityDistribution defence)
        {
            List<float> values = new();
            values.Add(0f);
            for (int i = 1; i < attack.values.Count; i++)
            {
                values.Add(ChanceASubBEquals(i, attack, defence));
            }
            values[0] = 1 - values.Sum();
            return new(values);
        }

        public static float BinomialCoefficient(int subsetSize, int totalSize)
        {
            if (subsetSize > totalSize)
            {
                return 0;
            }

            if (subsetSize > totalSize - subsetSize)
            {
                subsetSize = totalSize - subsetSize;
            }

            float c = 1;
            for (float i = 1; i <= subsetSize; i++)
            {
                c *= totalSize--;
                c /= i;
            }
            return c;
        }

        public static LimitedDiscreteProbabilityDistribution CreateFromSuccessChance(int numberOfDice, float chanceOfSuccess)
        {
            List<float> values = new();
            for (int i = 0; i <= numberOfDice; i++)
            {
                values.Add((float)Math.Pow(chanceOfSuccess,i)
                    * (float)Math.Pow(1-chanceOfSuccess,numberOfDice-i)
                    * BinomialCoefficient(i,numberOfDice)
                    );
            }
            return new(values);
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