#nullable enable

using Movement;
using Ship;

namespace AI.Helpers.Types
{
    public struct Maneuver
    {
        private ManeuverHolder Value;
        
        public Maneuver(string maneuverCode)
        {
            Value = new ManeuverHolder(maneuverCode);
        }

        public Maneuver(string maneuverCode, GenericShip ship)
        {
            Value = new ManeuverHolder(maneuverCode, ship);
        }

        public Maneuver(string maneuverCode, MovementComplexity complexity)
        {
            Value = new ManeuverHolder(maneuverCode)
            {
                ColorComplexity = complexity
            };
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