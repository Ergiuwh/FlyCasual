#nullable enable

using Ship;
using System;
using System.Collections.Generic;
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
        /// If this is on top of the vb stack, deactivate this, removing it from vb stack.
        /// Then active the top vb in the vb stack.
        /// </summary>
        public void Deactivate()
        {
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

            UpdateToVirtualBoard(newShipInitialiser, VirtualBoardManager.VirtualRealBoard);
        }

        /// <summary>
        /// This does not use extra ship info T1 in any way.
        /// </summary>
        public void UpdateToVirtualBoard<T1>(Func<GenericShip, T> newShipInitialiser, VirtualBoard<T1> virtualBoard) where T1 : class
        {
            List<GenericShip> shipsToRemove = new();
            foreach (GenericShip ship in Ships.Keys)
            {
                if (!virtualBoard.Ships.ContainsKey(ship))
                {
                    shipsToRemove.Add(ship);
                }
            }
            foreach (GenericShip ship in shipsToRemove)
            {
                Ships.Remove(ship);
            }

            foreach (GenericShip ship in virtualBoard.Ships.Keys)
            {
                if (Ships.ContainsKey(ship))
                {
                    Ships[ship].VirtualPosition = virtualBoard.Ships[ship].VirtualPosition;
                }
                else
                {
                    Ships.Add(ship, new ShipInfo(virtualBoard.Ships[ship].VirtualPosition, newShipInitialiser(ship)));
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

        public override void UpdateDisplayInternal()
        {
            ApplyVirtualPositionsInternal();
        }
    }
}