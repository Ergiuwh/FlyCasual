#nullable enable

using System;
using System.Linq;
using BoardTools;
using Editions;
using Ship;
using Tokens;

namespace AI.Helpers.AttackCalculations
{
    public struct DiscreteProbabilityDistribution
    {
        public float[] Values;

        public DiscreteProbabilityDistribution(float[] values)
        {
            Values = values;
        }

        /// <summary>
        /// AttackerDiceDistribution - DefenderDiceDistribution
        /// </summary>
        /// <param name="attacker"></param>
        /// <param name="defender"></param>
        /// <param name="weapon"></param>
        /// <returns></returns>
        public static (DiscreteProbabilityDistribution damageResult, float averageCrits) CalculateAttackDamageDistribution(GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            ShotInfo shotInfo = new(attacker, defender, weapon);

            (DiscreteProbabilityDistribution attackDice, float averageCrits) = CalculateAttackerDiceDistribution(shotInfo, attacker, defender, weapon);
            DiscreteProbabilityDistribution defenceDice = CalculateDefenderDiceDistribution(shotInfo, attacker, defender, weapon);

            DiscreteProbabilityDistribution damageResult = SubtractDefenceFromAttack(attackDice,defenceDice);

            return (damageResult, averageCrits);
        }

        public static (DiscreteProbabilityDistribution,float) CalculateAttackerDiceDistribution(ShotInfo shotInfo, GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            const float attackDiceChanceUnmodified = 0.5f;
            const float attackDiceChanceSingleModification = 0.75f;
            const float attackDiceChanceFullModification = 0.938f;

            const float potentialCritsNoReroll = 0.125f;
            const float potentialCritsWithRerollBlanksAndFocus = 0.1875f;
            const float potentialCritsWithRerollBlanks = 0.15625f;


            int attackDiceThrown = weapon.WeaponInfo.AttackValue;
            if (shotInfo.Range <= 1 && Edition.Current.IsWeaponHaveRangeBonus(weapon)) attackDiceThrown++;

            float attackDiceModifier;
            float criticalHitsModifier = potentialCritsNoReroll;
            if (attacker.Tokens.HasToken<FocusToken>() && ActionsHolder.HasTargetLockOn(attacker, defender))
            {
                criticalHitsModifier = potentialCritsWithRerollBlanks;
                attackDiceModifier = attackDiceChanceFullModification;
            }
            else if (attacker.Tokens.HasToken<FocusToken>())
            {
                attackDiceModifier = attackDiceChanceSingleModification;
            }
            else if (ActionsHolder.HasTargetLockOn(attacker, defender))
            {
                attackDiceModifier = attackDiceChanceSingleModification;
                criticalHitsModifier = potentialCritsWithRerollBlanksAndFocus;
            }
            else
            {
                attackDiceModifier = attackDiceChanceUnmodified;
            }
            
            float averageCrits = attackDiceThrown * criticalHitsModifier;
            return (CreateFromSuccessChance(attackDiceThrown,attackDiceModifier),averageCrits);
        }

        public static DiscreteProbabilityDistribution CalculateDefenderDiceDistribution(ShotInfo shotInfo, GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            const float defenceDiceChanceUnmodified = 0.375f;
            const float defenceDiceChanceFocusModification = 0.625f;

            int defenceDiceThrown = defender.State.Agility;
            if (shotInfo.Range == 3 && Edition.Current.IsWeaponHaveRangeBonus(weapon)) defenceDiceThrown++;
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

            DiscreteProbabilityDistribution potentialEvades = CreateFromSuccessChance(defenceDiceThrown, defenceDiceModifier);
            if (defender.Tokens.HasToken<EvadeToken>() && defenceDiceThrown > 0)
            {
                potentialEvades.ModificationSetFailToSuccess();
            }

            return potentialEvades;
        }

        private void ModificationSetFailToSuccess()
        {
            Values[^1] += Values[^2];
            for (int i = Values.Length - 2; i > 0; i--)
            {
                Values[i] = Values[i-1];
            }
            Values[0] = 0;
        }

        public float Average()
        {
            float r = 0;
            for (int i = 1; i < Values.Count(); i++)
            {
                r += i * Values[i];
            }
            return r;
        }

        public float ChanceEqualOrGreaterThan(int number)
        {
            return SumRange(number,Values.Length);
        }

        public float ChanceLessThan(int number)
        {
            return SumRange(0,Math.Min(number,Values.Length));
        }

        private float SumRange(int start_inclusive, int end_exclusive)
        {
            float s = 0;
            for (int i = start_inclusive; i < end_exclusive; i++)
            {
                s += Values[i];
            }
            return s;
        }

        private static float ChanceASubBEquals(int number, DiscreteProbabilityDistribution attack, DiscreteProbabilityDistribution defence)
        {
            float r = 0f;
            for (int i = number, j = 0; i < attack.Values.Count() && j < defence.Values.Count(); i++, j++)
            {
                r += attack.Values[i] * defence.Values[j];
            }
            return r;
        }

        private static float ChanceAPlusBEquals(int number, DiscreteProbabilityDistribution distA, DiscreteProbabilityDistribution distB)
        {
            float r = 0f;
            for (int i = 0; i <= number; i++)
            {
                int j = number - i;
                r += distA.Values[i] * distB.Values[j];
            }
            return r;
        }

        public static DiscreteProbabilityDistribution SubtractDefenceFromAttack(DiscreteProbabilityDistribution attack, DiscreteProbabilityDistribution defence)
        {
            int length = attack.Values.Length;
            float[] values = new float[length];
            for (int i = 1; i < length; i++)
            {
                values[i] = ChanceASubBEquals(i, attack, defence);
            }
            values[0] = 1 - values.Sum();
            return new(values);
        }

        public static DiscreteProbabilityDistribution Add(DiscreteProbabilityDistribution distA, DiscreteProbabilityDistribution distB)
        {
            int length = distA.Values.Length + distB.Values.Length;
            float[] values = new float[length];
            for (int i = 0; i < length; i++)
            {
                values[i] = ChanceAPlusBEquals(i, distA, distB);
            }
            return new(values);
        }

        private static float BinomialCoefficient(int subsetSize, int totalSize)
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

        private static DiscreteProbabilityDistribution CreateFromSuccessChance(int numberOfDice, float chanceOfSuccess)
        {
            float[] values = new float[numberOfDice];
            for (int i = 0; i <= numberOfDice; i++)
            {
                values[i] = (float)Math.Pow(chanceOfSuccess,i)
                    * (float)Math.Pow(1-chanceOfSuccess,numberOfDice-i)
                    * BinomialCoefficient(i,numberOfDice);
            }
            return new(values);
        }
    }

    public static class FastAttackCalculations
    {
        public static float AverageHits(ShotInfo shotInfo, GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            const float attackDiceChanceUnmodified = 0.5f;
            const float attackDiceChanceSingleModification = 0.75f;
            const float attackDiceChanceFullModification = 0.938f;

            int attackDiceThrown = weapon.WeaponInfo.AttackValue;
            if (shotInfo.Range <= 1 && Edition.Current.IsWeaponHaveRangeBonus(weapon)) attackDiceThrown++;

            float attackDiceModifier;
            if (attacker.Tokens.HasToken<FocusToken>() && ActionsHolder.HasTargetLockOn(attacker, defender))
            {
                attackDiceModifier = attackDiceChanceFullModification;
            }
            else if (attacker.Tokens.HasToken<FocusToken>() || ActionsHolder.HasTargetLockOn(attacker, defender))
            {
                attackDiceModifier = attackDiceChanceSingleModification;
            }
            else
            {
                attackDiceModifier = attackDiceChanceUnmodified;
            }
            return attackDiceModifier * attackDiceThrown;
        }

        public static float AverageCrits(ShotInfo shotInfo, GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            const float potentialCritsNoReroll = 0.125f;
            const float potentialCritsWithRerollBlanksAndFocus = 0.1875f;
            const float potentialCritsWithRerollBlanks = 0.15625f;

            int attackDiceThrown = weapon.WeaponInfo.AttackValue;
            if (shotInfo.Range <= 1 && Edition.Current.IsWeaponHaveRangeBonus(weapon)) attackDiceThrown++;

            float criticalHitsModifier = potentialCritsNoReroll;
            if (ActionsHolder.HasTargetLockOn(attacker, defender)) {
                if (attacker.Tokens.HasToken<FocusToken>())
                {
                    criticalHitsModifier = potentialCritsWithRerollBlanks;
                }
                else
                {
                    criticalHitsModifier = potentialCritsWithRerollBlanksAndFocus;
                }
            }
            return attackDiceThrown * criticalHitsModifier;
        }

        public static float AverageEvades(ShotInfo shotInfo, GenericShip defender, IShipWeapon weapon)
        {
            const float defenceDiceChanceUnmodified = 0.375f;
            const float defenceDiceChanceFocusModification = 0.625f;

            int defenceDiceThrown = defender.State.Agility;
            if (shotInfo.Range == 3 && Edition.Current.IsWeaponHaveRangeBonus(weapon)) defenceDiceThrown++;
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
            float averageEvadesWithoutToken = defenceDiceThrown * defenceDiceModifier;
            float chanceOfAllNotBlank = (float)Math.Pow(0.75f,defenceDiceThrown);
            float averageEvadesWithToken = averageEvadesWithoutToken + 1 - chanceOfAllNotBlank;
            return averageEvadesWithToken;
        }
    }
}