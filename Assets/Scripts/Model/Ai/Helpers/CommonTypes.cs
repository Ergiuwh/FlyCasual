#nullable enable

using System.Collections.Generic;
using Ship;

namespace AI.Helpers.Types
{
    public class OrderOfActivation
    {
        public List<GenericShip> Ships;

        public OrderOfActivation(List<GenericShip> ships)
        {
            Ships = ships;
        }
    }
}