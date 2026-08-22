#nullable enable

using System;
using System.Collections.Generic;
using AI.Aggressor2Dev.Navigation;
using AI.Helpers.Navigation;
using Ship;

namespace AI.Aggressor2Dev.Coordination
{
    public static class ActivationPhaseOrder
    {
        public static GenericShip SelectShipFromList(List<GenericShip> ships, NewVirtualBoard<VirtualBoardData> virtualBoard)
        {
            throw new NotImplementedException();
        }
        public static GenericShip SelectShipFromList(List<GenericShip> ships)
        {
            return SelectShipFromList(ships, new());
        }
    }
}