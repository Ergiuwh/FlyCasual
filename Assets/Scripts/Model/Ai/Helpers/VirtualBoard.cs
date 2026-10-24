#nullable enable

using Ship;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AI.Helpers.VirtualBoard
{
    public class VirtualBoard<T> : VirtualBoardManager.AbstractVirtualBoard where T: class
    {
        public Dictionary<GenericShip, ShipInfo> Ships;
        public bool IsActive { get { return ReferenceEquals(VirtualBoardManager.ActiveVirtualBoard, this); } }
        public bool IsNotActive { get { return !IsActive; } }

        public string Name { get; protected set; }

        public enum VirtualBoardState
        {
            Active,
            Inactive,
        }

        public class ShipInfo
        {
            public ShipPositionInfo VirtualPosition;
            public T OtherData;
            public bool CollisionsRemoved { get; private set; }
            public bool CollisionsEnabled { get { return !CollisionsRemoved; } }
            public bool SavedCollisionsRemoved;

            public ShipInfo(ShipPositionInfo shipPositionInfo, T otherData)
            {
                VirtualPosition = shipPositionInfo;
                OtherData = otherData;
                CollisionsRemoved = false;
                SavedCollisionsRemoved = false;
            }

            public void RemoveCollisions(GenericShip thisShip)
            {
                if (!CollisionsRemoved)
                {
                    thisShip.ToggleColliders(false);

                    CollisionsRemoved = true;
                }
            }

            public void ReturnCollisions(GenericShip thisShip)
            {
                if (CollisionsRemoved)
                {
                    thisShip.ToggleColliders(true);

                    CollisionsRemoved = false;
                }
            }

            public void ToggleCollisions(GenericShip thisShip, bool value)
            {
                if (value)
                {
                    ReturnCollisions(thisShip);
                }
                else
                {
                    RemoveCollisions(thisShip);
                }
            }

            public void ApplyPosition(GenericShip thisShip)
            {
                if (DebugManager.DebugMovementShowPlanning)
                {
                    thisShip.SetPositionInfo(VirtualPosition);
                }
                else
                {
                    thisShip.GetShipAllPartsTransform().GetPositionAndRotation(
                        out Vector3 savedModelPosition,
                        out Quaternion savedModelRotation);
                    thisShip.SetPositionInfo(VirtualPosition);
                    thisShip.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").GetPositionAndRotation(
                        out Vector3 savedHitboxPosition,
                        out Quaternion savedHitboxRotation);
                    thisShip.GetShipAllPartsTransform().SetPositionAndRotation(savedModelPosition,savedModelRotation);
                    thisShip.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").SetPositionAndRotation(savedHitboxPosition, savedHitboxRotation);
                }
            }
        }

        public VirtualBoard()
        {
            Ships = new();
            Name = "";
        }

        public VirtualBoard(string name)
        {
            Ships = new();
            Name = name;
        }

        public override void ActivateInternal()
        {
            foreach (KeyValuePair<GenericShip, ShipInfo> shipInfoPair in Ships)
            {
                if (shipInfoPair.Value.SavedCollisionsRemoved)
                {
                    shipInfoPair.Value.RemoveCollisions(shipInfoPair.Key);
                }
            }

            ApplyVirtualPositionsInternal();
        }

        public override void DeactivateInternal()
        {
            foreach (KeyValuePair<GenericShip, ShipInfo> shipInfoPair in Ships)
            {
                if (shipInfoPair.Value.CollisionsRemoved)
                {
                    shipInfoPair.Value.SavedCollisionsRemoved = true;
                    shipInfoPair.Value.ReturnCollisions(shipInfoPair.Key);
                }
                else
                {
                    shipInfoPair.Value.SavedCollisionsRemoved = false;
                }
            }
        }

        public override void PauseActiveInternal()
        {
            DeactivateInternal();
        }

        public override void UnpauseActiveInternal()
        {
            ActivateInternal();
        }

        /// <summary>
        /// Activate and push to vb stack.
        /// Short-circuit if ActiveVirtualBoard == this.
        /// Applies virtual positions.
        /// </summary>
        public void Activate()
        {
            if (IsActive)
            {
                return;
            }

            VirtualBoardManager.Activate(this);
        }

        /// <summary>
        /// If possible, activate this and push to vb stack. Return true if this is active.
        /// Applies virtual positions.
        /// </summary>
        public bool TryActivate()
        {
            if (VirtualBoardManager.ActiveVirtualBoard is null)
            {
                VirtualBoardManager.Activate(this);
            }

            return IsActive;
        }

        /// <summary>
        /// If this is on top of the vb stack, deactivate this, removing it from vb stack.
        /// Then active the top vb in the vb stack.
        /// </summary>
        public void Deactivate()
        {
            if (VirtualBoardManager.ActiveVirtualBoard != this)
            {
                return;
            }

            VirtualBoardManager.Deactivate(this);
        }

        public void ApplyVirtualPositions()
        {
            if (IsActive)
            {
                ApplyVirtualPositionsInternal();
            }
            else
            {
                throw new Exception("Attempt to apply positions of an inactive virtual board.");
            }
        }

        private void ApplyVirtualPositionsInternal()
        {
            foreach (GenericShip ship in Ships.Keys)
            {
                Ships[ship].ApplyPosition(ship);
            }
        }

        /// <summary>
        /// If the real board is active, this updates it first.
        /// </summary>
        public void UpdateToReal(Func<GenericShip, T> newShipInitialiser)
        {
            if (VirtualBoardManager.IsVirtualRealBoardActive)
            {
                VirtualBoardManager.UpdateVirtualRealBoard();
            }

            List<GenericShip> shipsToRemove = new();
            foreach (GenericShip ship in Ships.Keys)
            {
                if (!VirtualBoardManager.VirtualRealBoard.Ships.ContainsKey(ship))
                {
                    shipsToRemove.Add(ship);
                }
            }
            foreach (GenericShip ship in shipsToRemove)
            {
                Ships.Remove(ship);
            }

            foreach (GenericShip ship in VirtualBoardManager.VirtualRealBoard.Ships.Keys)
            {
                if (Ships.ContainsKey(ship))
                {
                    Ships[ship].VirtualPosition = VirtualBoardManager.VirtualRealBoard.Ships[ship].VirtualPosition;
                }
                else
                {
                    Ships.Add(ship, new ShipInfo(VirtualBoardManager.VirtualRealBoard.Ships[ship].VirtualPosition, newShipInitialiser(ship)));
                }
            }
        }

        public class ShipInterface
        {
            public VirtualBoard<T> CreatedBy;
            public GenericShip Ship;
            public VirtualBoard<T>.ShipInfo ShipInfo;

            public T ShipData
            {
                get { return ShipInfo.OtherData; }
            }

            public ShipInterface(GenericShip ship, VirtualBoard<T> createdBy)
            {
                CreatedBy = createdBy;
                Ship = ship;
                ShipInfo = CreatedBy.Ships[Ship];
            }

            public ShipInterface RemoveCollisions()
            {
                ShipInfo.RemoveCollisions(Ship);
                return this;
            }

            public ShipInterface ReturnCollisions()
            {
                ShipInfo.ReturnCollisions(Ship);
                return this;
            }

            public ShipInterface ToggleCollisions(bool value)
            {
                ShipInfo.ToggleCollisions(Ship, value);
                return this;
            }

            public bool HasCollisions()
            {
                return !ShipInfo.CollisionsRemoved;
            }

            /// <summary>
            /// If this board is active, automatically apply the new position.
            /// </summary>
            public ShipInterface SetPosition(ShipPositionInfo info)
            {
                return SetPosition(info, CreatedBy.IsActive);
            }

            public ShipInterface SetPosition(ShipPositionInfo info, bool applyPosition)
            {
                ShipInfo.VirtualPosition = info;
                if (applyPosition)
                {
                    if (CreatedBy.IsNotActive)
                    {
                        throw new Exception("Attempt to apply positions of an inactive virtual board.");
                    }

                    ShipInfo.ApplyPosition(Ship);
                }

                return this;
            }

            public ShipInterface ApplyPosition()
            {
                ShipInfo.ApplyPosition(Ship);
                
                return this;
            }

            /// <summary>
            /// If this board is active, also apply the new position.
            /// </summary>
            public ShipInterface UpdateToRealPosition()
            {
                return UpdateToRealPosition(CreatedBy.IsActive);
            }

            public ShipInterface UpdateToRealPosition(bool applyPosition)
            {
                AssertShipExistsInRealBoard();
                SetPosition(VirtualBoardManager.VirtualRealBoard.Ships[Ship].VirtualPosition, applyPosition);

                return this;
            }

            private void AssertShipExistsInRealBoard()
            {
                if (!VirtualBoardManager.VirtualRealBoard.Ships.ContainsKey(Ship))
                {
                    throw new Exception("VirtualBoard.ShipInterface assert failed: AssertIsInRealBoard");
                }
            }
        }

        /// <summary>
        /// Does not check if the returned ShipInterface is valid. If ship is not in this virtual board, using it may throw an exception.
        /// </summary>
        public ShipInterface GetShipInterface(GenericShip ship)
        {
            return new ShipInterface(ship, this);
        }

        public IEnumerable<ShipInterface> GetShipInterfaceOnAllShips()
        {
            foreach (GenericShip ship in Ships.Keys)
            {
                yield return GetShipInterface(ship);
            }
        }

        public IEnumerable<ShipInterface> GetShipInterfaceOnAllShipsWhere(Func<GenericShip, bool> predicate)
        {
            foreach (GenericShip ship in Ships.Keys)
            {
                if (predicate(ship)) {
                    yield return GetShipInterface(ship);
                }
            }
        }

        public void AssertIsActive()
        {
            if (IsNotActive)
            {
                throw new Exception("VirtualBoard assert failed: AssertIsActive");
            }
        }

        public void AssertIsNotActive()
        {
            if (IsActive)
            {
                throw new Exception("VirtualBoard assert failed: AssertIsNotActive");
            }
        }

        public T? GetShipData(GenericShip ship)
        {
            bool success = Ships.TryGetValue(ship, out ShipInfo info);
            return success ? info.OtherData : null;
        }

        public bool TryGetShipData(GenericShip ship, [System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out T? shipData)
        {
            bool success = Ships.TryGetValue(ship, out ShipInfo info);
            shipData = success ? info.OtherData : null;
            return success;
        }

        public T GetShipDataOrError(GenericShip ship)
        {
            return Ships[ship].OtherData;
        }

        public void ReturnAllCollisions()
        {
            AssertIsActive();
            foreach (KeyValuePair<GenericShip, ShipInfo> pair in Ships)
            {
                pair.Value.ReturnCollisions(pair.Key);
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}

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
            VirtualRealBoard = new VirtualRealBoardClass("VirtualRealBoard");
        }

        public static void InitializeForGame()
        {
            VirtualBoardStack.Clear();
            VirtualBoardStack.Push(VirtualRealBoard);
            VirtualRealBoard.ActivateInternal();
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
            }
            else
            {
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
        }

        public static void UpdateVirtualRealBoard()
        {
            if (ReferenceEquals(ActiveVirtualBoard, VirtualRealBoard))
            {
                UpdateVirtualRealBoardInternal();
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
            /// You shouldn't call this outside of a implementing a virtual board or VirtualBoardManager.
            /// </summary>
            public abstract void ActivateInternal();
            /// <summary>
            /// You shouldn't call this outside of a implementing a virtual board or VirtualBoardManager.
            /// </summary>
            public abstract void DeactivateInternal();
            /// <summary>
            /// You shouldn't call this outside of a implementing a virtual board or VirtualBoardManager.
            /// </summary>
            public abstract void PauseActiveInternal();
            /// <summary>
            /// You shouldn't call this outside of a implementing a virtual board or VirtualBoardManager.
            /// </summary>
            public abstract void UnpauseActiveInternal();
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
}