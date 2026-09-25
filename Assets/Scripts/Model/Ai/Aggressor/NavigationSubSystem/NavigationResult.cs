#nullable enable

using Movement;
using Ship;
using System;
using UnityEngine;

namespace AI.Aggressor
{
    public class NavigationResult
    {
        public bool isOffTheBoard;
        public bool isLandedOnObstacle;

        public int enemiesInShotRange;

        public int obstaclesHit;
        public int minesHit;

        public float distanceToNearestEnemy;
        public float distanceToNearestEnemyInShotRange;
        public float angleToNearestEnemy;

        public bool isOffTheBoardNextTurn;
        public bool isHitAsteroidNextTurn;

        public int enemiesWithThisAsOnlyTarget;

        public bool isBumped;

        public GenericShip? TheShip;

        public GenericMovement? movement;

        public int Priority { get; private set; }

        public ShipPositionInfo FinalPositionInfo { get; set; }

        public void CalculatePriorityInRoundTwo()
        {
            if (isOffTheBoard)
            {
                Priority = int.MinValue;
                return;
            }

            Priority = 0;

            if (TheShip == null) throw new Exception("AI.Aggressor.NavigationResult CalculatePriority() requires this.TheShip != null");

            if (isLandedOnObstacle) Priority -= 20000;

            if (isOffTheBoardNextTurn) Priority -= 40000;

            Priority += (int)(Math.Sqrt(enemiesInShotRange) * 1000);

            Priority -= minesHit * 2000;

            int asteroidAvoidPriority = (BoardTools.Board.DistanceToRange(distanceToNearestEnemy) < 4) ? 1 : 10;

            Priority -= obstaclesHit * 2000 * asteroidAvoidPriority;
            if (isHitAsteroidNextTurn) Priority -= 1000 * asteroidAvoidPriority;

            if (isBumped) Priority -= 500;

            if (TheShip.Damage.HasCrit(typeof(DamageDeckCardSE.LooseStabilizer)) && movement?.Bearing != ManeuverBearing.Straight)
            {
                if (TheShip.State.HullCurrent + TheShip.State.ShieldsCurrent == 1)
                {
                    Priority -= 20000;
                }
                else
                {
                    Priority -= 1000;
                }
            }

            switch (movement?.ColorComplexity)
            {
                case MovementComplexity.Easy:
                    if (TheShip.IsStressed) Priority += 500;
                    break;
                case MovementComplexity.Complex:
                    if (TheShip.IsStressed)
                    {
                        Priority = int.MinValue;
                        return;
                    }
                    else
                    {
                        Priority -= 500;
                    }
                    break;
                case MovementComplexity.None: // Impossible maneuvers
                    Priority = int.MinValue;
                    return;
                default:
                    break;
            }

            //distance is 0..10, result 0..100
            Priority += 100 - (int)(distanceToNearestEnemy * 10);

            //angle is 0..180, result 0..180
            Priority += 180 - Mathf.RoundToInt(angleToNearestEnemy);
        }

        public override string ToString()
        {
            string result = "";

            result += Priority + " = ";

            result += "distance:" + distanceToNearestEnemy + " ";
            if (enemiesInShotRange > 0) result += "distanceShot:" + distanceToNearestEnemyInShotRange + " ";
            result += "angle:" + angleToNearestEnemy + " ";

            switch (movement?.ColorComplexity)
            {
                case MovementComplexity.None:
                    result += "color:none ";
                    break;
                case MovementComplexity.Easy:
                    // When TheShip is null, this could throw an error instead.
                    if (TheShip?.IsStressed ?? false) result += "color:blue ";
                    break;
                case MovementComplexity.Normal:
                    break;
                case MovementComplexity.Complex:
                    result += "color:red ";
                    break;
                case MovementComplexity.Purple:
                    // Purple maneuvers are currently have no effect for aggressor.
                    // result += "color:purple ";
                    break;
                default:
                    // This could throw an error instead.
                    result += "color:null ";
                    break;
            }

            if (isOffTheBoard) result += "OffBoard ";
            if (isLandedOnObstacle) result += "LandedOnObstacle ";
            if (isBumped) result += "Bumped ";

            if (enemiesInShotRange > 0) result += "enemiesToShoot:" + enemiesInShotRange + " ";
            if (enemiesWithThisAsOnlyTarget > 0) result += "timesShot:" + enemiesWithThisAsOnlyTarget + " ";

            if (obstaclesHit > 0) result += "obstaclesHit:" + obstaclesHit + " ";
            if (minesHit > 0) result += "minesHit:" + obstaclesHit + " ";

            return result;
        }

        public void CalculatePriority()
        {
            CalculatePriorityInRoundTwo();
            if (Priority == int.MinValue)
            {
                return;
            }

            Priority -= enemiesWithThisAsOnlyTarget * 800;
        }
    }
}
