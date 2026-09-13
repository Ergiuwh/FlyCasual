#nullable enable

using Movement;
using Ship;

namespace AI.Helpers.Types
{
    public readonly struct Maneuver
    {
        private readonly Movement.ManeuverSpeed speed;
        private readonly Movement.ManeuverDirection direction;
        private readonly Movement.ManeuverBearing bearing;
        private readonly Movement.MovementComplexity complexity;

        public Maneuver(ManeuverSpeed speed, ManeuverDirection direction, ManeuverBearing bearing, MovementComplexity complexity = MovementComplexity.None)
        {
            this.speed = speed;
            this.direction = direction;
            this.bearing = bearing;
            this.complexity = complexity;
        }

        public Maneuver(string maneuverCode)
        {
            ManeuverHolder maneuverHolder = new(maneuverCode);

            speed = maneuverHolder.Speed;
            direction = maneuverHolder.Direction;
            bearing = maneuverHolder.Bearing;
            complexity = maneuverHolder.ColorComplexity;
        }

        public Maneuver(string maneuverCode, GenericShip ship)
        {
            ManeuverHolder maneuverHolder = new(maneuverCode, ship);

            speed = maneuverHolder.Speed;
            direction = maneuverHolder.Direction;
            bearing = maneuverHolder.Bearing;
            complexity = maneuverHolder.ColorComplexity;
        }

        public Maneuver(string maneuverCode, MovementComplexity complexity)
        {
            ManeuverHolder maneuverHolder = new(maneuverCode);

            speed = maneuverHolder.Speed;
            direction = maneuverHolder.Direction;
            bearing = maneuverHolder.Bearing;

            this.complexity = complexity;
        }

        public Maneuver(ManeuverHolder maneuverHolder)
        {
            speed = maneuverHolder.Speed;
            direction = maneuverHolder.Direction;
            bearing = maneuverHolder.Bearing;
            complexity = maneuverHolder.ColorComplexity;
        }

        public override string ToString()
        {
            return this.ToManeuverHolder().ToString();
        }

        public ManeuverHolder ToManeuverHolder()
        {
            return new ManeuverHolder(speed, direction, bearing, complexity);
        }

        public readonly int GetSpeedIntSigned()
        {
            return new ManeuverHolder(speed, direction, bearing, complexity).SpeedIntSigned;
        }

        public readonly int GetSpeedIntUnsigned()
        {
            return new ManeuverHolder(speed, direction, bearing, complexity).SpeedIntUnsigned;
        }

        public readonly Movement.ManeuverSpeed GetSpeed()
        {
            return speed;
        }

        public readonly Movement.ManeuverDirection GetDirection()
        {
            return direction;
        }

        public readonly Movement.ManeuverBearing GetBearing()
        {
            return bearing;
        }

        public readonly Movement.MovementComplexity GetComplexity()
        {
            return complexity;
        }
    }
}