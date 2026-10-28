#nullable enable

using System.Collections.Generic;
using Ship;

namespace AI.Helpers.VirtualBoard
{
    public static class VirtualBoardManager
    {
        private static Stack<AbstractVirtualBoard> VirtualBoardStack;
        /// <summary>
        /// This holds a copy of all ship positions in the current board.
        /// </summary>
        public static VirtualRealBoardClass VirtualRealBoard;
        public static AbstractVirtualBoard ActiveVirtualBoard { get { return VirtualBoardStack.Peek(); } }
        public static bool IsVirtualRealBoardActive { get { return ReferenceEquals(ActiveVirtualBoard, VirtualRealBoard); } }

        static VirtualBoardManager()
        {
            VirtualBoardStack = new();
            VirtualRealBoard = new VirtualRealBoardClass("VirtualRealBoard - unset");
        }

        public static void Initialize()
        {
            VirtualBoardStack.Clear();
            VirtualRealBoard = new VirtualRealBoardClass("VirtualRealBoard");
            VirtualRealBoard.ActivateInternal();
            VirtualBoardStack.Push(VirtualRealBoard);
        }

        public static void Activate(AbstractVirtualBoard virtualBoard)
        {
            AbstractVirtualBoard currentActiveVirtualBoard = VirtualBoardStack.Peek();
            currentActiveVirtualBoard.PauseActiveInternal();
            virtualBoard.ActivateInternal();
            VirtualBoardStack.Push(virtualBoard);
        }

        public static void Deactivate(AbstractVirtualBoard virtualBoardToDeactivate)
        {
            if (ReferenceEquals(virtualBoardToDeactivate, VirtualRealBoard))
            {
                ConsoleHelpers.Error("VirtualBoardManager.DeactivateVirtualBoard", "Cannot deactivate VirtualRealBoard.");
                return;
            }

            AbstractVirtualBoard currentActiveVirtualBoard = VirtualBoardStack.Pop();
            if (ReferenceEquals(currentActiveVirtualBoard, virtualBoardToDeactivate))
            {
                virtualBoardToDeactivate.DeactivateInternal();
                VirtualBoardStack.Peek().UnpauseActiveInternal();
            }
            else // identify, log, and recover from error.
            {
                Stack<AbstractVirtualBoard> virtualBoardsCheckedStack = new();
                virtualBoardsCheckedStack.Push(currentActiveVirtualBoard);

                while (VirtualBoardStack.TryPop(out AbstractVirtualBoard virtualBoardToCheck))
                {
                    if (ReferenceEquals(virtualBoardToDeactivate, virtualBoardToCheck))
                    {
                        string errorMessage = $"Attempted to deactivate {virtualBoardToDeactivate} while virtual boards above where still active. "
                            + $"Attempted to recover by deactivating all virtual boards above. Virtual boards deactivated: [ ";

                        Queue<string> errorsToLogAfterErrorMessage = new();

                        while (virtualBoardsCheckedStack.TryPop(out AbstractVirtualBoard virtualBoardAboveToDeactivate))
                        {
                            errorMessage += $"{virtualBoardAboveToDeactivate}, ";

                            try
                            {
                                virtualBoardAboveToDeactivate.DeactivateInternal();
                            }
                            catch
                            {
                                errorsToLogAfterErrorMessage.Enqueue($"{virtualBoardAboveToDeactivate} did not deactivate correctly");
                            }
                        }

                        errorMessage += " ]";

                        ConsoleHelpers.Error("VirtualBoardManager.DeactivateVirtualBoard", errorMessage);

                        while (errorsToLogAfterErrorMessage.TryDequeue(out string errorMessageToLog))
                        {
                            ConsoleHelpers.Error("VirtualBoardManager.DeactivateVirtualBoard", errorMessageToLog);
                        }

                        return;
                    }
                    else
                    {
                        virtualBoardsCheckedStack.Push(virtualBoardToCheck);
                    }
                }
                
                while (virtualBoardsCheckedStack.TryPop(out AbstractVirtualBoard virtualBoard))
                {
                    VirtualBoardStack.Push(virtualBoard);
                }

                ConsoleHelpers.Error("VirtualBoardManager.DeactivateVirtualBoard", $"Attempted to deactivate {virtualBoardToDeactivate}, no matching "
                    + $"virtual board found in stack. Attempted recovery by calling .DeactivateInernal() on {virtualBoardToDeactivate} without "
                    + $"changing VirtualBoardStack.");

                try
                {
                    virtualBoardToDeactivate.DeactivateInternal();
                }
                catch
                {
                    ConsoleHelpers.Error("VirtualBoardManager.DeactivateVirtualBoard", $"{virtualBoardToDeactivate} did not deactivate correctly");
                }
            }
        }

        public static void UpdateVirtualRealBoard()
        {
            if (ReferenceEquals(ActiveVirtualBoard, VirtualRealBoard))
            {
                UpdateVirtualRealBoardInternal();
            }
            else
            {
                ConsoleHelpers.Error("VirtualBoardManager.UpdateVirtualRealBoard", "VirtualRealBoard must be active.");
            }
        }

        private static void UpdateVirtualRealBoardInternal()
        {
            List<GenericShip> shipsToRemove = new();
            foreach (GenericShip ship in VirtualRealBoard.Ships.Keys)
            {
                if (ship.IsDestroyed)
                {
                    shipsToRemove.Add(ship);
                    continue;
                }
            }

            foreach (GenericShip ship in shipsToRemove)
            {
                VirtualRealBoard.Ships.Remove(ship);
            }

            foreach (KeyValuePair<GenericShip, VirtualBoard<EmptyClass>.ShipInfo> shipValuePair in VirtualRealBoard.Ships)
            {
                shipValuePair.Value.VirtualPosition = shipValuePair.Key.GetPositionInfo();
            }

            foreach (GenericShip ship in Roster.AllShips.Values)
            {
                if (!VirtualRealBoard.Ships.ContainsKey(ship))
                {
                    VirtualRealBoard.Ships.Add(ship, new VirtualBoard<EmptyClass>.ShipInfo(ship.GetPositionInfo(), new()));
                }
            }
        }

        public static void AfterSetDebugMovementShowPlanning()
        {
            if (IsVirtualRealBoardActive)
            {
                UpdateVirtualRealBoard();
            }

            ActiveVirtualBoard.UpdateDisplayInternal();
        }

        public class VirtualRealBoardClass : VirtualBoard<EmptyClass>
        {
            public VirtualRealBoardClass(string name) : base(name) {  }

            public override void PauseActiveInternal()
            {
                UpdateVirtualRealBoardInternal();
                base.PauseActiveInternal();
            }
        }

        public abstract class AbstractVirtualBoard
        {
            /// <summary>
            /// <para>You shouldn't call this outside of a implementing a virtual board or VirtualBoardManager.</para>
            /// </summary>
            public abstract void ActivateInternal();
            /// <summary>
            /// <para>You shouldn't call this outside of a implementing a virtual board or VirtualBoardManager.</para>
            /// </summary>
            public abstract void DeactivateInternal();
            /// <summary>
            /// <para>You shouldn't call this outside of a implementing a virtual board or VirtualBoardManager.</para>
            /// </summary>
            public abstract void PauseActiveInternal();
            /// <summary>
            /// <para>You shouldn't call this outside of a implementing a virtual board or VirtualBoardManager.</para>
            /// </summary>
            public abstract void UnpauseActiveInternal();
            /// <summary>
            /// <para>You shouldn't call this outside of a implementing a virtual board or VirtualBoardManager.</para>
            /// <para>You can assume this is active.</para>
            /// </summary>
            public abstract void UpdateDisplayInternal();
        }

        public class EmptyClass
        {
            
        }

        public static class DebugUtils
        {
            /// <summary>
            /// result[0] is the top (active) virtual board, result[1] is the second top virtual board, result.Last() is the virtual real board.
            /// </summary>
            public static List<AbstractVirtualBoard> GetVirtualBoardStack()
            {
                Stack<AbstractVirtualBoard> secondVirtualBoardStack = new();
                List<AbstractVirtualBoard> result = new();

                while (VirtualBoardStack.TryPop(out AbstractVirtualBoard virtualBoard))
                {
                    secondVirtualBoardStack.Push(virtualBoard);
                    result.Add(virtualBoard);
                }

                while (secondVirtualBoardStack.TryPop(out AbstractVirtualBoard virtualBoard))
                {
                    VirtualBoardStack.Push(virtualBoard);
                }

                return result;
            }
        }
    }
}a