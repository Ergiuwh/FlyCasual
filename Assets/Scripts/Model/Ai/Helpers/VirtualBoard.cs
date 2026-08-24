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
        public static NewVirtualBoard<EmptyStruct> RealBoard;

        static VirtualBoardManager() {
            RealBoard = new();
            ActiveVirtualBoard = RealBoard;
        }

        public static void DeactivateCurrentBoard()
        {
            if (ActiveVirtualBoard == RealBoard)
            {
                UpdateRealBoardInternal();
            }
            ActiveVirtualBoard?.DeactivateInternal();
            LastActiveVirtualBoard = ActiveVirtualBoard;

            if (ActiveVirtualBoard != null) Logs.LogDeactivateBoard(ActiveVirtualBoard);

            ActiveVirtualBoard = null;
        }

        public static void ActivateVirtualBoard(IVirtualBoard virtualBoard)
        {
            if (ActiveVirtualBoard == null) {
                if (LastActiveVirtualBoard == virtualBoard)
                {
                    ActiveVirtualBoard = virtualBoard;
                    virtualBoard.RecoverActiveInternal();
                }
                else
                {
                    ActiveVirtualBoard = virtualBoard;
                    virtualBoard.ActivateInternal();
                }

                Logs.LogActivateBoard(virtualBoard);
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

            Logs.LogActivateBoard(RealBoard);
        }

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
                    RealBoard.Ships.Add(ship, new NewVirtualBoard<EmptyStruct>.ShipInfo(ship.GetPositionInfo(), new()));
                }
            }

            Logs.LogUpdateRealBoard();
        }

        public struct EmptyStruct : ICloneable
        {
            public readonly object Clone()
            {
                return new EmptyStruct();
            }
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

        public static class Logs
        {
            public struct LogedData
            {
                public enum OperationType
                {
                    ActivateBoard,
                    DeactivateBoard,
                    ReactivateBoard,
                    UpdateRealBoard,
                }

                public struct BoardRef
                {
                    public IVirtualBoard Value;

                    public BoardRef(IVirtualBoard virtualBoard)
                    {
                        Value = virtualBoard;
                    }

                    public override readonly string ToString()
                    {
                        if (Value == RealBoard)
                        {
                            return "RealBoard";
                        }

                        return Value.GetHashCode().ToString();
                    }
                }

                public OperationType Operation;
                public BoardRef Board;

                public LogedData(IVirtualBoard virtualBoard, OperationType operation)
                {
                    Operation = operation;
                    Board = new BoardRef(virtualBoard);
                }

                public override readonly string ToString()
                {
                    string operationString = Operation switch
                    {
                        OperationType.ActivateBoard => "Activate",
                        OperationType.DeactivateBoard => "Deactivate",
                        OperationType.ReactivateBoard => "Reactivate",
                        OperationType.UpdateRealBoard => "Update",
                        _ => "",
                    };
                    return $"{operationString} {Board}";
                }
            }

            public static List<LogedData> Values = new();
            
            private static bool? doLogging;
            public static bool DoLogging
            {
                get { return doLogging ?? false; }
                set { doLogging = value; }
            }

            public static void LogActivateBoard(IVirtualBoard virtualBoard)
            {
                if (DoLogging)
                {
                    Values.Add(new LogedData(virtualBoard, LogedData.OperationType.ActivateBoard));
                }
            }

            public static void LogDeactivateBoard(IVirtualBoard virtualBoard)
            {
                if (DoLogging)
                {
                    Values.Add(new LogedData(virtualBoard, LogedData.OperationType.DeactivateBoard));
                }
            }

            public static void LogReactivateBoard(IVirtualBoard virtualBoard)
            {
                if (DoLogging)
                {
                    Values.Add(new LogedData(virtualBoard, LogedData.OperationType.ReactivateBoard));
                }
            }

            public static void LogUpdateRealBoard()
            {
                if (DoLogging)
                {
                    Values.Add(new LogedData(RealBoard, LogedData.OperationType.UpdateRealBoard));
                }
            }
        }
    }

    public class NewVirtualBoard<T> : VirtualBoardManager.IVirtualBoard where T: ICloneable
    {
        public Dictionary<GenericShip, ShipInfo> Ships;
        public VirtualBoardState State { get; protected set; }

        public enum VirtualBoardState
        {
            Virtual,
            Other,
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
                    ShipPositionInfo savedModelPosition = new(thisShip.GetShipAllPartsTransform().position, thisShip.GetShipAllPartsTransform().eulerAngles);
                    thisShip.SetPositionInfo(VirtualPosition);
                    thisShip.GetShipAllPartsTransform().position = savedModelPosition.Position;
                    thisShip.GetShipAllPartsTransform().eulerAngles = savedModelPosition.Angles;
                    thisShip.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").position = VirtualPosition.Position;
                    thisShip.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").localPosition += new Vector3(0, 0.150289f, 1.156069f);
                    thisShip.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").eulerAngles = VirtualPosition.Angles;
                }
            }
        }

        public NewVirtualBoard()
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

            State = VirtualBoardState.Other;
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
            }

            State = VirtualBoardState.Inactive;
        }

        public NewVirtualBoard<T> Activate()
        {
            if (VirtualBoardManager.ActiveVirtualBoard == this)
            {
                return this;
            }

            VirtualBoardManager.ActivateVirtualBoard(this);
            return this;
        }

        public NewVirtualBoard<T>? TryActivate()
        {
            if (VirtualBoardManager.ActiveVirtualBoard == null)
            {
                VirtualBoardManager.ActivateVirtualBoard(this);
            }

            return VirtualBoardManager.ActiveVirtualBoard == this ? this : null;
        }

        /// <summary>
        /// If the real board is active, deactivate it. Then activate this.
        /// </summary>
        /// <returns></returns>
        public NewVirtualBoard<T> ClaimActive()
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

        public NewVirtualBoard<T> Deactivate()
        {
            if (VirtualBoardManager.ActiveVirtualBoard != this)
            {
                return this;
            }

            VirtualBoardManager.DeactivateCurrentBoard();
            return this;
        }

        public NewVirtualBoard<T> ApplyVirtualPositions()
        {
            if (State == VirtualBoardState.Other || State == VirtualBoardState.Virtual)
            {
                foreach (GenericShip ship in Ships.Keys)
                {
                    ship.SetPositionInfo(Ships[ship].VirtualPosition);
                }
                State = VirtualBoardState.Virtual;
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
        public NewVirtualBoard<T> UpdateToReal(Func<GenericShip, T> newShipInitialiser)
        {
            if (VirtualBoardManager.ActiveVirtualBoard == VirtualBoardManager.RealBoard)
            {
                VirtualBoardManager.UpdateRealBoard();
            }

            foreach (GenericShip ship in Ships.Keys)
            {
                if (!VirtualBoardManager.RealBoard.Ships.ContainsKey(ship))
                {
                    Ships.Remove(ship);
                }
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

        public struct ShipInterface
        {
            public readonly NewVirtualBoard<T> CreatedBy;
            public readonly GenericShip Ship;

            public readonly ShipInfo ShipInfo
            {
                get { return CreatedBy.Ships[Ship]; }
            }

            public readonly T ShipData
            {
                get { return ShipInfo.OtherData; }
            }

            public ShipInterface(GenericShip ship, NewVirtualBoard<T> createdBy)
            {
                CreatedBy = createdBy;
                Ship = ship;
            }

            public ShipInterface ApplyPosition()
            {
                if (CreatedBy.State == VirtualBoardState.Inactive)
                {
                    throw new Exception("Attempt to apply positions of an inactive virtual board.");
                }
                CreatedBy.Ships[Ship].ApplyPosition(Ship);
                return this;
            }

            public ShipInterface RemoveCollisions()
            {
                CreatedBy.Ships[Ship].RemoveCollisions(Ship);
                return this;
            }

            public ShipInterface ReturnCollisions()
            {
                CreatedBy.Ships[Ship].ReturnCollisions(Ship);
                return this;
            }

            /// <summary>
            /// If this board is active, also apply the new position.
            /// </summary>
            /// <param name="info"></param>
            /// <returns></returns>
            public ShipInterface SetPosition(ShipPositionInfo info)
            {
                CreatedBy.Ships[Ship].VirtualPosition = info;
                if (CreatedBy.State != VirtualBoardState.Inactive)
                {
                    ApplyPosition();
                }

                return this;
            }

            public ShipInterface SetPosition(ShipPositionInfo info, bool applyPosition)
            {
                CreatedBy.Ships[Ship].VirtualPosition = info;
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
                SetPosition(VirtualBoardManager.RealBoard.Ships[Ship].VirtualPosition);

                return this;
            }

            public ShipInterface UpdateToRealPosition(bool applyPosition)
            {
                AssertIsInRealBoard();
                SetPosition(VirtualBoardManager.RealBoard.Ships[Ship].VirtualPosition, applyPosition);

                return this;
            }

            private void AssertIsInRealBoard()
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

        public void AssertIsInVirtualPosition()
        {
            if (State != VirtualBoardState.Virtual)
            {
                throw new Exception("VirtualBoard assert failed: AssertIsInVirtualPosition");
            }
        }

        public void AssertIsActive()
        {
            if (State == VirtualBoardState.Inactive)
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
            AssertIsInVirtualPosition();
            return new ShotInfo(attacker, defender, weapon);
        }

        public T GetShipData(GenericShip ship)
        {
            return Ships[ship].OtherData;
        }

        public NewVirtualBoard<T> ReturnAllCollisions()
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