#nullable enable

using System;
using System.Collections.Generic;
using AI.Helpers.Navigation;
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

    public class ActiveVirtualBoard<T> where T : ICloneable
    {
        private NewVirtualBoard<T> internalVirtualBoard;

        public NewVirtualBoard<T> VirtualBoard
        {
            get { internalVirtualBoard.AssertIsActive(); return internalVirtualBoard; }
        }

        public ActiveVirtualBoard(NewVirtualBoard<T> virtualBoard)
        {
            virtualBoard.AssertIsActive();
            internalVirtualBoard = virtualBoard;
        }

        public InactiveVirtualBoard<T> Deactivate()
        {
            internalVirtualBoard.Deactivate();
            return new InactiveVirtualBoard<T>(internalVirtualBoard);
        }
    }

    public class InactiveVirtualBoard<T> where T : ICloneable
    {
        private NewVirtualBoard<T> internalVirtualBoard;

        public NewVirtualBoard<T> VirtualBoard
        {
            get { internalVirtualBoard.AssertIsNotActive(); return internalVirtualBoard; }
        }

        public InactiveVirtualBoard(NewVirtualBoard<T> virtualBoard)
        {
            virtualBoard.AssertIsNotActive();
            internalVirtualBoard = virtualBoard;
        }

        public ActiveVirtualBoard<T> Activate()
        {
            internalVirtualBoard.Activate();
            return new ActiveVirtualBoard<T>(internalVirtualBoard);
        }
    }
}