#nullable enable

using System;
using System.Collections.Generic;
using BoardTools;
using Ship;
using UnityEngine;

namespace AI.Helpers.Navigation
{
    public static class VirtualBoardManager
    {
        public static IVirtualBoard? ActiveVirtualBoard { get; private set; }
        public static IVirtualBoard? LastActiveVirtualBoard { get; private set; }
        public static VirtualBoard<EmptyClass> RealBoard = new();

        public static void InitializeForGame()
        {
            RealBoard = new();
            ActiveVirtualBoard = RealBoard;
        }

        /// <summary>
        /// 
        /// </summary>
        public static void DeactivateCurrentBoard()
        {
            if (ActiveVirtualBoard == RealBoard)
            {
                UpdateRealBoardInternal();
            }
            ActiveVirtualBoard?.DeactivateInternal();
            LastActiveVirtualBoard = ActiveVirtualBoard;

            if (ActiveVirtualBoard != null) Logger.LogDeactivateBoard(ActiveVirtualBoard);

            ActiveVirtualBoard = null;
        }

        /// <summary>
        /// If any virtual board is active, including <paramref name="virtualBoard"/>, throw Exception.
        /// If LastActiveVirtualBoard == <paramref name="virtualBoard"/>, call <paramref name="virtualBoard"/>.RecoverActiveInternal().
        /// Otherwise, call <paramref name="virtualBoard"/>.ActivateInternal().
        /// </summary>
        /// <param name="virtualBoard"></param>
        /// <exception cref="Exception"></exception>
        public static void ActivateVirtualBoard(IVirtualBoard virtualBoard)
        {
            if (ActiveVirtualBoard == null) {
                if (LastActiveVirtualBoard == virtualBoard)
                {
                    ActiveVirtualBoard = virtualBoard;
                    virtualBoard.RecoverActiveInternal();
                    Logger.LogReactivateBoard(virtualBoard);
                }
                else
                {
                    ActiveVirtualBoard = virtualBoard;
                    virtualBoard.ActivateInternal();
                    Logger.LogActivateBoard(virtualBoard);
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
        public static void ActivateRealBoard()
        {
            if (ActiveVirtualBoard != null && ActiveVirtualBoard != RealBoard)
            {
                DeactivateCurrentBoard();
            }
            RealBoard.Activate();
            RealBoard.ApplyVirtualPositions();

            Logger.LogActivateBoard(RealBoard);
        }

        /// <summary>
        /// Throws Exception if ActiveVirtualBoard != RealBoard.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public static void UpdateRealBoard()
        {
            if (ActiveVirtualBoard == RealBoard)
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

            Logger.LogUpdateRealBoard();
        }

        /// <summary>
        /// This is used as the data type of RealBoard.
        /// </summary>
        public class EmptyClass
        {
            
        }

        public interface IVirtualBoard
        {
            /// <summary>
            /// You shouldn't call this outside of a implementing a virtual board. (Which you shouldn't need to do.)
            /// </summary>
            void ActivateInternal();
            /// <summary>
            /// You shouldn't call this outside of a implementing a virtual board. (Which you shouldn't need to do.)
            /// </summary>
            void DeactivateInternal();
            /// <summary>
            /// <para>You shouldn't call this outside of a implementing a virtual board. (Which you shouldn't need to do.) </para>
            /// No other board has activated since we last deactivated.
            /// </summary>
            void RecoverActiveInternal()
            {
                ActivateInternal();
            }
        }

        public static class Logger
        {            
            public static bool DoLogging = false;
            public static bool ProvideStackTrace = false;

            public static void LogActivateBoard(IVirtualBoard virtualBoard)
            {
                if (DoLogging && ProvideStackTrace)
                {
                    Console.Write($"VirtualBoardLogger: Activate {virtualBoard}. Stack trace: {new System.Diagnostics.StackTrace()}");
                }
                else if (DoLogging)
                {
                    Console.Write($"VirtualBoardLogger: Activate {virtualBoard}.");
                }
            }

            public static void LogDeactivateBoard(IVirtualBoard virtualBoard)
            {
                if (DoLogging && ProvideStackTrace)
                {
                    Console.Write($"VirtualBoardLogger: Deactivate {virtualBoard}. Stack trace: {new System.Diagnostics.StackTrace()}");
                }
                else if (DoLogging)
                {
                    Console.Write($"VirtualBoardLogger: Deactivate {virtualBoard}.");
                }
            }

            public static void LogReactivateBoard(IVirtualBoard virtualBoard)
            {
                if (DoLogging && ProvideStackTrace)
                {
                    Console.Write($"VirtualBoardLogger: Reactivate {virtualBoard}. Stack trace: {new System.Diagnostics.StackTrace()}");
                }
                else if (DoLogging)
                {
                    Console.Write($"VirtualBoardLogger: Reactivate {virtualBoard}.");
                }
            }

            public static void LogUpdateRealBoard()
            {
                if (DoLogging && ProvideStackTrace)
                {
                    Console.Write($"VirtualBoardLogger: Update real board. Stack trace: {new System.Diagnostics.StackTrace()}");
                }
                else if (DoLogging)
                {
                    Console.Write($"VirtualBoardLogger: Update real board.");
                }
            }
        }
    }

    public class VirtualBoard<T> : VirtualBoardManager.IVirtualBoard where T: class
    {
        public Dictionary<GenericShip, ShipInfo> Ships;
        public VirtualBoardState State { get; protected set; }

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
                    Vector3 savedModelPosition = thisShip.GetShipAllPartsTransform().position;

                    thisShip.SetPosition(thisShip.GetPosition() - new Vector3(0, -100, 0));
                    thisShip.GetShipAllPartsTransform().position = savedModelPosition;

                    CollisionsRemoved = true;
                }
            }

            public void ReturnCollisions(GenericShip thisShip)
            {
                if (CollisionsRemoved)
                {
                    Vector3 savedModelPosition = thisShip.GetShipAllPartsTransform().position;

                    thisShip.SetPosition(thisShip.GetPosition() - new Vector3(0, +100, 0));
                    thisShip.GetShipAllPartsTransform().position = savedModelPosition;

                    CollisionsRemoved = false;
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
            State = VirtualBoardState.Inactive;
        }

        public void ActivateInternal()
        {
            foreach (KeyValuePair<GenericShip, ShipInfo> shipInfoPair in Ships)
            {
                if (shipInfoPair.Value.SavedCollisionsRemoved)
                {
                    shipInfoPair.Value.RemoveCollisions(shipInfoPair.Key);
                }
            }

            State = VirtualBoardState.Active;
        }

        public void RecoverActiveInternal()
        {
            ActivateInternal();
        }

        public void DeactivateInternal()
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

            State = VirtualBoardState.Inactive;
        }

        /// <summary>
        /// Short-circuit if ActiveVirtualBoard == this.
        /// Throws an exception if any other virtual board is active.
        /// </summary>
        /// <returns></returns>
        public VirtualBoard<T> Activate()
        {
            if (VirtualBoardManager.ActiveVirtualBoard == this)
            {
                return this;
            }

            VirtualBoardManager.ActivateVirtualBoard(this);
            return this;
        }

        /// <summary>
        /// Returns null if another virtual board is active.
        /// </summary>
        /// <returns></returns>
        public VirtualBoard<T>? TryActivate()
        {
            if (VirtualBoardManager.ActiveVirtualBoard == null)
            {
                VirtualBoardManager.ActivateVirtualBoard(this);
            }

            return VirtualBoardManager.ActiveVirtualBoard == this ? this : null;
        }

        /// <summary>
        /// Short-circuit if ActiveVirtualBoard == this.
        /// If the real board is active, deactivate it. Then activate this.
        /// Throws an exception if any other virtual board is active.
        /// </summary>
        /// <returns></returns>
        public VirtualBoard<T> ClaimActive()
        {
            if (VirtualBoardManager.ActiveVirtualBoard == this)
            {
                return this;
            }

            if (VirtualBoardManager.ActiveVirtualBoard == VirtualBoardManager.RealBoard)
            {
                VirtualBoardManager.DeactivateCurrentBoard();
            }

            VirtualBoardManager.ActivateVirtualBoard(this);
            return this;
        }

        public VirtualBoard<T> Deactivate()
        {
            if (VirtualBoardManager.ActiveVirtualBoard != this)
            {
                return this;
            }

            VirtualBoardManager.DeactivateCurrentBoard();
            return this;
        }

        public VirtualBoard<T> ApplyVirtualPositions()
        {
            if (State == VirtualBoardState.Active)
            {
                foreach (GenericShip ship in Ships.Keys)
                {
                    Ships[ship].ApplyPosition(ship);
                }
                State = VirtualBoardState.Active;
                return this;
            }
            else
            {
                throw new Exception("Attempt to apply positions of an inactive virtual board.");
            }
        }

        /// <summary>
        /// If the real board is active, this updates it first.
        /// </summary>
        /// <param name="newShipInitialiser"></param>
        /// <returns></returns>
        public VirtualBoard<T> UpdateToReal(Func<GenericShip, T> newShipInitialiser)
        {
            if (VirtualBoardManager.ActiveVirtualBoard == VirtualBoardManager.RealBoard)
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

            return this;
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

            public ShipInterface ApplyPosition()
            {
                if (CreatedBy.State == VirtualBoardState.Inactive)
                {
                    throw new Exception("Attempt to apply positions of an inactive virtual board.");
                }
                ShipInfo.ApplyPosition(Ship);
                return this;
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

            /// <summary>
            /// If this board is active, also apply the new position.
            /// </summary>
            /// <param name="info"></param>
            /// <returns></returns>
            public ShipInterface SetPosition(ShipPositionInfo info)
            {
                return SetPosition(info, CreatedBy.State != VirtualBoardState.Inactive);
            }

            public ShipInterface SetPosition(ShipPositionInfo info, bool applyPosition)
            {
                ShipInfo.VirtualPosition = info;
                if (applyPosition)
                {
                    ApplyPosition();
                }

                return this;
            }

            /// <summary>
            /// If this board is active, also apply the new position.
            /// </summary>
            /// <param name="info"></param>
            /// <returns></returns>
            public ShipInterface UpdateToRealPosition()
            {
                return UpdateToRealPosition(CreatedBy.State != VirtualBoardState.Inactive);
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

        public ShipInterface GetShipInterface(GenericShip ship)
        {
            return new ShipInterface(ship, this);
        }

        public List<ShipInterface> GetShipInterfaceOnAllShips()
        {
            List<ShipInterface> result = new();
            foreach (GenericShip ship in Ships.Keys)
            {
                result.Add(new ShipInterface(ship, this));
            }
            return result;
        }

        public List<ShipInterface> GetShipInterfaceOnAllShipsWhere(Func<GenericShip, bool> predicate)
        {
            List<ShipInterface> result = new();
            foreach (GenericShip ship in Ships.Keys)
            {
                if (predicate(ship)) {
                    result.Add(new ShipInterface(ship, this));
                }
            }
            return result;
        }

        public void AssertIsActive()
        {
            if (State != VirtualBoardState.Active)
            {
                throw new Exception("VirtualBoard assert failed: AssertIsActive");
            }
        }

        public void AssertIsNotActive()
        {
            if (State != VirtualBoardState.Inactive)
            {
                throw new Exception("VirtualBoard assert failed: AssertIsNotActive");
            }
        }

        public ShotInfo GenerateShotInfo(GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            AssertIsActive();
            return new ShotInfo(attacker, defender, weapon);
        }

        public T? TryGetShipData(GenericShip ship)
        {
            bool success = Ships.TryGetValue(ship, out ShipInfo info);
            return success ? info.OtherData : null;
        }

        public T GetShipDataOrError(GenericShip ship)
        {
            return Ships[ship].OtherData;
        }

        public VirtualBoard<T> ReturnAllCollisions()
        {
            AssertIsActive();
            foreach (KeyValuePair<GenericShip, ShipInfo> pair in Ships)
            {
                pair.Value.ReturnCollisions(pair.Key);
            }
            return this;
        }
    }
}