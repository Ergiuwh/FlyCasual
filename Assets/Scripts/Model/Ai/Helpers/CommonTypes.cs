#nullable enable

using System.Collections.Generic;
using Movement;
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

    public struct Maneuver
    {
        private ManeuverHolder Value;
        
        public Maneuver(string maneuverCode)
        {
            Value = new(maneuverCode);
        }

        public Maneuver(ManeuverHolder maneuverHolder)
        {
            Value = maneuverHolder;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public ManeuverHolder ToManeuverHolder()
        {
            return Value;
        }
    }
}