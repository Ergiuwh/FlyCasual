#nullable enable

using BoardTools;
using Ship;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AI.Helpers.VirtualBoard
{
    public static class VirtualBoardManager
    {
        public static AbstractVirtualBoard? ActiveVirtualBoard { get; private set; }
        public static AbstractVirtualBoard? LastActiveVirtualBoard { get; private set; }
        public static VirtualBoard<EmptyClass> RealBoard = new();
        public static bool IsRealBoardActive
        {
            get
            {
                return ReferenceEquals(ActiveVirtualBoard, RealBoard);
            }
        }

        private static Stack<AbstractVirtualBoard> VirtualBoardStack = new();

        public static void InitializeForGame()
        {
            RealBoard = new();
            ActiveVirtualBoard = RealBoard;
        }

        /// <summary>
        /// 
        /// </summary>
        private static void DeactivateCurrentBoard()
        {
            if (ReferenceEquals(ActiveVirtualBoard, RealBoard))
            {
                UpdateRealBoardInternal();
            }
            ActiveVirtualBoard?.DeactivateInternal();
            LastActiveVirtualBoard = ActiveVirtualBoard;

            ActiveVirtualBoard = null;
        }

        /// <summary>
        /// If any virtual board is active, including <paramref name="virtualBoard"/>, throw Exception.
        /// If LastActiveVirtualBoard == <paramref name="virtualBoard"/>, call <paramref name="virtualBoard"/>.RecoverActiveInternal().
        /// Otherwise, call <paramref name="virtualBoard"/>.ActivateInternal().
        /// </summary>
        private static void ActivateVirtualBoard(AbstractVirtualBoard virtualBoard)
        {
            if (ActiveVirtualBoard is null) {
                if (ReferenceEquals(LastActiveVirtualBoard, virtualBoard))
                {
                    ActiveVirtualBoard = virtualBoard;
                    virtualBoard.RecoverActiveInternal();
                }
                else
                {
                    ActiveVirtualBoard = virtualBoard;
                    virtualBoard.ActivateInternal();
                }
            }
            else
            {
                throw new Exception("Attempt to activate a virtual board while another is active.");
            }
        }

        /// <summary>
        /// This deactivates the active virtual board first.
        /// </summary>
        private static void ActivateRealBoard()
        {
            if (ActiveVirtualBoard != null && ActiveVirtualBoard != RealBoard)
            {
                DeactivateCurrentBoard();
            }
            RealBoard.Activate();
            RealBoard.ApplyVirtualPositions();
        }

        /// <summary>
        /// Throws Exception if ActiveVirtualBoard != RealBoard.
        /// </summary>
        public static void UpdateRealBoard()
        {
            if (ReferenceEquals(ActiveVirtualBoard, RealBoard))
            {
                UpdateRealBoardInternal();
            }
            else
            {
                throw new Exception("Attempt to update VirtualBoardManager.RealBoard while another virtual board is active.");
            }
        }

        private static void UpdateRealBoardInternal()
        {
            List<GenericShip> shipsToRemove = new();
            foreach (GenericShip ship in RealBoard.Ships.Keys)
            {
                if (ship.IsDestroyed)
                {
                    shipsToRemove.Add(ship);
                    continue;
                }

                RealBoard.Ships[ship].VirtualPosition = ship.GetPositionInfo();
            }

            foreach (GenericShip ship in shipsToRemove)
            {
                RealBoard.Ships.Remove(ship);
            }

            foreach (GenericShip ship in Roster.AllShips.Values)
            {
                if (!RealBoard.Ships.ContainsKey(ship))
                {
                    RealBoard.Ships.Add(ship, new VirtualBoard<EmptyClass>.ShipInfo(ship.GetPositionInfo(), new()));
                }
            }
        }

        public static void ActivateAndPushToStack(AbstractVirtualBoard virtualBoard)
        {
            DeactivateCurrentBoard();
            ActivateVirtualBoard(virtualBoard);
            VirtualBoardStack.Push(virtualBoard);
        }

        public static void PopVirtualBoardStack()
        {
            DeactivateCurrentBoard();
            VirtualBoardStack.TryPop(out _);
            if (VirtualBoardStack.TryPeek(out AbstractVirtualBoard result))
            {
                ActivateVirtualBoard(result);
            }
            else
            {
                ActivateRealBoard();
            }
        }

        /// <summary>
        /// Activates real board.
        /// </summary>
        public static void ClearStack()
        {
            if (VirtualBoardStack.Count != 0)
            {
                DeactivateCurrentBoard();
                VirtualBoardStack.Clear();
            }

            ActivateRealBoard();
        }

        /// <summary>
        /// This is used as the data type of RealBoard.
        /// </summary>
        public class EmptyClass
        {
            
        }

        public abstract class AbstractVirtualBoard
        {
            /// <summary>
            /// You shouldn't call this outside of a implementing a virtual board. (Which you shouldn't need to do.)
            /// </summary>
            public abstract void ActivateInternal();
            /// <summary>
            /// You shouldn't call this outside of a implementing a virtual board. (Which you shouldn't need to do.)
            /// </summary>
            public abstract void DeactivateInternal();
            /// <summary>
            /// <para>You shouldn't call this outside of a implementing a virtual board. (Which you shouldn't need to do.) </para>
            /// No other board has activated since we last deactivated.
            /// </summary>
            public virtual void RecoverActiveInternal()
            {
                ActivateInternal();
            }
        }
    }

    public class VirtualBoard<T> : VirtualBoardManager.AbstractVirtualBoard where T: class
    {
        public Dictionary<GenericShip, ShipInfo> Ships;
        public bool IsActive { get { return ReferenceEquals(VirtualBoardManager.ActiveVirtualBoard, this); } }
        public bool IsNotActive { get { return !IsActive; } }

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
                    thisShip.GetShipAllPartsTransform().GetPositionAndRotation(out Vector3 savedModelPosition, out Quaternion savedModelRotation);
                    thisShip.SetPositionInfo(VirtualPosition);
                    thisShip.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").GetPositionAndRotation(out Vector3 savedHitboxPosition, out Quaternion savedHitboxRotation);
                    thisShip.GetShipAllPartsTransform().SetPositionAndRotation(savedModelPosition,savedModelRotation);
                    thisShip.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").SetPositionAndRotation(savedHitboxPosition, savedHitboxRotation);
                }
            }
        }

        public VirtualBoard()
        {
            Ships = new();
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

            ApplyVirtualPositions();
        }

        public override void RecoverActiveInternal()
        {
            ActivateInternal();
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

            VirtualBoardManager.ActivateAndPushToStack(this);
        }

        /// <summary>
        /// If possible, activate this and push to vb stack. Return true if this is active.
        /// Applies virtual positions.
        /// </summary>
        public bool TryActivate()
        {
            if (VirtualBoardManager.ActiveVirtualBoard is null)
            {
                VirtualBoardManager.ActivateAndPushToStack(this);
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

            VirtualBoardManager.PopVirtualBoardStack();
        }

        public void ApplyVirtualPositions()
        {
            if (IsActive)
            {
                foreach (GenericShip ship in Ships.Keys)
                {
                    Ships[ship].ApplyPosition(ship);
                }

                return;
            }
            else
            {
                throw new Exception("Attempt to apply positions of an inactive virtual board.");
            }
        }

        /// <summary>
        /// If the real board is active, this updates it first.
        /// </summary>
        public void UpdateToReal(Func<GenericShip, T> newShipInitialiser)
        {
            if (VirtualBoardManager.IsRealBoardActive)
            {
                VirtualBoardManager.UpdateRealBoard();
            }

            List<GenericShip> shipsToRemove = new();
            foreach (GenericShip ship in Ships.Keys)
            {
                if (!VirtualBoardManager.RealBoard.Ships.ContainsKey(ship))
                {
                    shipsToRemove.Add(ship);
                }
            }
            foreach (GenericShip ship in shipsToRemove)
            {
                Ships.Remove(ship);
            }

            foreach (GenericShip ship in VirtualBoardManager.RealBoard.Ships.Keys)
            {
                if (Ships.ContainsKey(ship))
                {
                    Ships[ship].VirtualPosition = VirtualBoardManager.RealBoard.Ships[ship].VirtualPosition;
                }
                else
                {
                    Ships.Add(ship, new ShipInfo(VirtualBoardManager.RealBoard.Ships[ship].VirtualPosition, newShipInitialiser(ship)));
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
                SetPosition(VirtualBoardManager.RealBoard.Ships[Ship].VirtualPosition, applyPosition);

                return this;
            }

            private void AssertShipExistsInRealBoard()
            {
                if (!VirtualBoardManager.RealBoard.Ships.ContainsKey(Ship))
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
    }
}