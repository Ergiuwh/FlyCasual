#nullable enable

using System;
using UnityEngine;

namespace AI.Aggressor.Internal
{
    public class PositionScoreInfoInternal
    {
        public bool isOffTheBoard;
        public bool isLandedOnObstacle;

        public int enemiesInShotRange;

        public float distanceToNearestEnemy;
        public float distanceToNearestEnemyInShotRange;
        public float angleToNearestEnemy;

        public bool isOffTheBoardNextTurn;

        public int enemiesWithThisAsOnlyTarget;

        public int Priority { get; protected set; }

        public PositionScoreInfoInternal()
        {
            
        }

        public void CalculatePositionPriority()
        {
            if (isOffTheBoard)
            {
                Priority = int.MinValue;
                return;
            }

            Priority = 0;

            if (isLandedOnObstacle) Priority -= 20000;

            if (isOffTheBoardNextTurn) Priority -= 40000;

            Priority += (int)(Math.Sqrt(enemiesInShotRange) * 1000);

            //distance is 0..10, result 0..100
            Priority += 100 - (int)(distanceToNearestEnemy * 10);

            //angle is 0..180, result 0..180
            Priority += 180 - Mathf.RoundToInt(angleToNearestEnemy);

            Priority -= enemiesWithThisAsOnlyTarget * 800;
        }

        public override string ToString()
        {
            string result = "";

            result += Priority + " = ";

            result += "distance:" + distanceToNearestEnemy + " ";
            if (enemiesInShotRange > 0) result += "distanceShot:" + distanceToNearestEnemyInShotRange + " ";
            result += "angle:" + angleToNearestEnemy + " ";

            if (isOffTheBoard) result += "OffBoard ";
            if (isLandedOnObstacle) result += "LandedOnObstacle ";

            if (enemiesInShotRange > 0) result += "enemiesToShoot:" + enemiesInShotRange + " ";
            if (enemiesWithThisAsOnlyTarget > 0) result += "timesShot:" + enemiesWithThisAsOnlyTarget + " ";

            return result;
        }
    }
}