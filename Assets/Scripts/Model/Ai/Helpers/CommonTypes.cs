#nullable enable

using Movement;

namespace AI.Helpers.Types
{
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