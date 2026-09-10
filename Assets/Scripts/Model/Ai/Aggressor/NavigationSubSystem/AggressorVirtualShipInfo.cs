#nullable enable

using System.Collections.Generic;
using AI.Helpers.Types;
using Ship;

namespace AI.Aggressor
{
    public class AggressorVirtualShipInfo
    {
        public Maneuver? PlannedManeuver { get; set; }
        public Dictionary<string, NavigationResult>? NavigationResults { get; private set; }
        public int OrderToActivate { get; set; }

        private bool AllFinalPositionsAreKnown { get { return NavigationResults != null; } }

        public bool VirtualPositionWithCollisionsIsReady { get; private set; }

        public ShipPositionInfo PredictedPosition { get; set; }

        public AggressorVirtualShipInfo()
        {
            
        }
        public AggressorVirtualShipInfo(GenericShip ship)
        {
            
        }

        public AggressorVirtualShipInfo SetPlannedManeuver(Maneuver maneuver)
        {
            PlannedManeuver = maneuver;
            return this;
        }

        public AggressorVirtualShipInfo ClearPlannedManeuver()
        {
            PlannedManeuver = null;
            return this;
        }

        public AggressorVirtualShipInfo UpdateNavigationResults(Dictionary<string, NavigationResult> navigationResults)
        {
            NavigationResults = navigationResults;
            return this;
        }

        public AggressorVirtualShipInfo SetPredictedPosition(ShipPositionInfo predictedPosition)
        {
            PredictedPosition = predictedPosition;
            return this;
        }

        public AggressorVirtualShipInfo SetOrderToActivate(int orderToActivate)
        {
            OrderToActivate = orderToActivate;
            return this;
        }
    }
}