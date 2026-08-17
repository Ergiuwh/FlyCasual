using BoardTools;
using Ship;

namespace AI.Helpers.AttackCalculations
{
    /// <summary>
    /// Re-exports common functions.
    /// </summary>
    public static class Prelude
    {
        public static DiscreteProbabilityDistribution CalculateAttackDamageDistributionIgnoreCrits(GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            return DiscreteProbabilityDistribution.CalculateAttackDamageDistribution(attacker, defender, weapon).damageResult;
        }
        public static (DiscreteProbabilityDistribution damageResult, float averageCrits) CalculateAttackDamageDistribution(GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            return DiscreteProbabilityDistribution.CalculateAttackDamageDistribution(attacker, defender, weapon);
        }
        public static float AverageHits(ShotInfo shotInfo, GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            return FastAttackCalculations.AverageHits(shotInfo, attacker, defender, weapon);
        }
        public static float AverageCrits(ShotInfo shotInfo, GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            return FastAttackCalculations.AverageCrits(shotInfo, attacker, defender, weapon);
        }
        public static float AverageEvades(ShotInfo shotInfo, GenericShip defender, IShipWeapon weapon)
        {
            return FastAttackCalculations.AverageEvades(shotInfo, defender, weapon);
        }
    }
}