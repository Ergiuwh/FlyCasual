#nullable enable

using BoardTools;
using Editions;
using Ship;
using System;
using Tokens;

namespace AI.Helpers.AttackCalculations
{
    public static class FastAttackCalculations
    {
        /// <summary>
        /// Includes Focus, Lock
        /// </summary>
        /// <param name="shotInfo"></param>
        /// <param name="attacker"></param>
        /// <param name="defender"></param>
        /// <param name="weapon"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Includes Focus, Lock.
        /// </summary>
        /// <param name="shotInfo"></param>
        /// <param name="attacker"></param>
        /// <param name="defender"></param>
        /// <param name="weapon"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Includes Focus, Evade
        /// </summary>
        /// <param name="shotInfo"></param>
        /// <param name="defender"></param>
        /// <param name="weapon"></param>
        /// <returns></returns>
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
            if (defender.Tokens.HasToken<EvadeToken>())
            {
                float chanceOfAllNotBlank = (float)Math.Pow(0.75f,defenceDiceThrown);
                float averageEvadesWithToken = averageEvadesWithoutToken + 1 - chanceOfAllNotBlank;
                return averageEvadesWithToken;
            }
            else
            {
                return averageEvadesWithoutToken;
            }
        }
    }
}