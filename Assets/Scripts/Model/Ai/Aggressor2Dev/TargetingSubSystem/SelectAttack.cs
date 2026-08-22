#nullable enable

using System.Collections.Generic;
using AI.Aggressor2Dev.Navigation;
using AI.Helpers.AttackCalculations;
using AI.Helpers.Navigation;
using BoardTools;
using Ship;

namespace AI.Aggressor2Dev.Targeting
{
    public class AttackResult
    {
        public GenericShip Defender;
        public IShipWeapon Weapon;
        public DiscreteProbabilityDistribution Damage;

        public AttackResult(GenericShip defender, IShipWeapon weapon, DiscreteProbabilityDistribution damage)
        {
            Defender = defender;
            Weapon = weapon;
            Damage = damage;
        }

        public float Score()
        {
            int defenderHealthRemaining = Defender.State.HullCurrent + Defender.State.ShieldsCurrent;
            float chanceOfKill = Damage.ChanceEqualOrGreaterThan(defenderHealthRemaining);
            return Damage.Average() * (1 + chanceOfKill * Aggressor2DevConfig.KillBonusModifier);
        }
    }

    public static class TargetingFns
    {
        /// <summary>
        /// This currently does not consider that weapons may not be "normal" attacks. (e.g. Homing missiles, jamming beam.)
        /// </summary>
        /// <param name="ship"></param>
        /// <param name="virtualBoard"></param>
        /// <returns></returns>
        public static AttackResult? SelectBestAttack(GenericShip ship, NewVirtualBoard<VirtualBoardData> virtualBoard)
        {
            List<NewVirtualBoard<VirtualBoardData>.ShipInterface> enemyShips = virtualBoard.GetShipInterfaceOnAllShipsWhere(a=>Tools.IsAnotherTeam(ship,a));

            AttackResult? bestAttack = null;
            foreach (NewVirtualBoard<VirtualBoardData>.ShipInterface targetShipI in enemyShips)
            {
                IShipWeapon? bestWeapon = null;
                float bestAverageDamage = 0f;
                foreach (IShipWeapon weapon in targetShipI.Ship.GetAllWeapons())
                {
                    ShotInfo shotInfo = new(ship, targetShipI.Ship, weapon);
                    if (!shotInfo.IsShotAvailable) continue;
                    float averageDamage = FastAttackCalculations.AverageHits(shotInfo, ship, targetShipI.Ship, weapon);
                    if (averageDamage > bestAverageDamage)
                    {
                        bestWeapon = weapon;
                        bestAverageDamage = averageDamage;
                    }
                }

                if (bestWeapon == null) continue;

                DiscreteProbabilityDistribution probabilityDistribution = DiscreteProbabilityDistribution.CalculateAttackDamageDistribution(ship, targetShipI.Ship, bestWeapon).damageResult;

                AttackResult newAttack = new(targetShipI.Ship, bestWeapon, probabilityDistribution);

                if (bestAttack == null || bestAttack.Score() < newAttack.Score())
                {
                    bestAttack = newAttack;
                }
            }

            return bestAttack;
        }
    }
}