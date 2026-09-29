using System.Collections;
using Movement;
using Ship;
using UnityEngine;

namespace AI.Helpers.Navigation.Internal
{
    /// <summary>
    /// MovementPrediction does not set properties if this overlaps a ship.
    /// </summary>
    public class TeleportMovement : GenericMovement
    {
        public ShipPositionInfo FinalBasePosition;
        public TeleportMovement(ShipPositionInfo finalBasePosition) : base(0, ManeuverDirection.Stationary, ManeuverBearing.None, MovementComplexity.None)
        {
            FinalBasePosition = finalBasePosition;
        }

        public override void AdaptSuccessProgress()
        {
            throw new System.NotImplementedException();
        }

        public override IEnumerator Perform()
        {
            throw new System.NotImplementedException();
        }

        public override GameObject[] PlanFinalPosition()
        {
            return PlanMovement();
        }

        public override GameObject[] PlanMovement()
        {
            GameObject[] result = new GameObject[1];

            Vector3 position = FinalBasePosition.Position;
            Quaternion rotation = Quaternion.Euler(FinalBasePosition.Angles);

            GameObject prefab = (GameObject)Resources.Load(TheShip.ShipBase.TemporaryPrefabPath, typeof(GameObject));
            GameObject ShipStand = MonoBehaviour.Instantiate(prefab, position, rotation, BoardTools.Board.GetBoard());

            Renderer[] renderers = ShipStand.GetComponentsInChildren<Renderer>();
            foreach (Renderer render in renderers)
            {
                render.enabled = false;
            }

            result[0] = ShipStand;

            return result;
        }
    }
}