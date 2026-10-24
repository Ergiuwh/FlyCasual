#nullable enable

using System;
using AI.Aggressor.Internal;
using Ship;
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

        public double Priority { get; protected set; }

        public PositionScoreInfoInternal()
        {

        }

        public void CalculatePositionPriority()
        {
            if (isOffTheBoard)
            {
                Priority = double.NegativeInfinity;
                return;
            }

            Priority = 0f;

            if (isLandedOnObstacle) Priority -= 20000;

            if (isOffTheBoardNextTurn) Priority -= 40000;

            Priority += Math.Sqrt(enemiesInShotRange) * 1000;

            //distance is 0..10, result 0..100
            Priority += 100 - distanceToNearestEnemy * 10;

            //angle is 0..180, result 0..180
            Priority += 180 - Mathf.Round(angleToNearestEnemy);

            Priority -= enemiesWithThisAsOnlyTarget * 800;
        }

        public override string ToString()
        {
            string result = "";

            result += $"{Priority} = ";

            result += $"distance:{distanceToNearestEnemy} ";
            if (enemiesInShotRange > 0) result += $"distanceShot:{distanceToNearestEnemyInShotRange} ";
            result += $"angle:{angleToNearestEnemy} ";

            if (isOffTheBoard) result += "OffBoard ";
            if (isLandedOnObstacle) result += "LandedOnObstacle ";

            if (enemiesInShotRange > 0) result += $"enemiesToShoot:{enemiesInShotRange} ";
            if (enemiesWithThisAsOnlyTarget > 0) result += $"enemiesWithThisAsOnlyTarget:{enemiesWithThisAsOnlyTarget} ";

            return result;
        }
    }
}

namespace AI.Aggressor
{
    public class PositionResult : PositionScoreInfoInternal
    {
        public static PositionResult NewNoCollision(GenericShip ship)
        {
            NavigationSubSystem.CheckNextTurn(ship, out bool isOffTheBoardNextTurn);
            NavigationSubSystem.ProcessHeavyGeometryCalculations(
                ship,
                out float minDistanceToEnemyShip,
                out float minDistanceToNearestEnemyInShotRange,
                out float minAngle,
                out int enemiesInShotRange);

            PositionResult result = new()
            {
                isOffTheBoard = false,
                isLandedOnObstacle = false,
                isOffTheBoardNextTurn = isOffTheBoardNextTurn,
                angleToNearestEnemy = minAngle,
                distanceToNearestEnemy = minDistanceToEnemyShip,
                distanceToNearestEnemyInShotRange = minDistanceToNearestEnemyInShotRange,
                enemiesInShotRange = enemiesInShotRange,
                enemiesWithThisAsOnlyTarget = ship.Owner.Ships.Count > 1 ? NavigationSubSystem.CountEnemiesWithThisAsOnlyTarget(ship) : 0
            };

            return result;
        }
    }
}