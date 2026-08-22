#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using AI.Helpers.Types;
using BoardTools;
using Movement;
using Ship;
using UnityEngine;

// namespace AI.Helpers.Navigation
// {
//     public class VirtualShipInfo<R> where R : ICloneable
//     {
//         public GenericShip Ship { get; private set; }
//         public ShipPositionInfo RealPositionInfo { get; private set; }
//         public ShipPositionInfo VirtualPositionInfo { get; private set; }
//         public Maneuver? PlannedManeuver { get; set; }
//         public Dictionary<string, R>? NavigationResults { get; private set; }
//         public int OrderToActivate { get; set; }

//         private bool SimpleManeuverPredictionIsReady;
//         private bool AllFinalPositionsAreKnown { get { return NavigationResults != null; } }

//         public bool VirtualPositionWithCollisionsIsReady { get; private set; }

//         public bool CollisionsRemoved { get; private set; }

//         public VirtualShipInfo(GenericShip ship)
//         {
//             Ship = ship;
//             RealPositionInfo = new ShipPositionInfo(ship.GetPosition(), ship.GetAngles());
//         }

//         public VirtualShipInfo(VirtualShipInfo<R> copyFrom)
//         {
//             Ship = copyFrom.Ship;
//             RealPositionInfo = copyFrom.RealPositionInfo;
//             VirtualPositionInfo = copyFrom.VirtualPositionInfo;
//             PlannedManeuver = copyFrom.PlannedManeuver;
//             if (copyFrom.NavigationResults == null)
//             {
//                 NavigationResults = null;
//             }
//             else
//             {
//                 NavigationResults = new();
//                 foreach (string movementCode in copyFrom.NavigationResults.Keys)
//                 {
//                     NavigationResults[movementCode] = (R)copyFrom.NavigationResults[movementCode].Clone();
//                 }
//             }
//             OrderToActivate = copyFrom.OrderToActivate;
//             SimpleManeuverPredictionIsReady = copyFrom.SimpleManeuverPredictionIsReady;
//             VirtualPositionWithCollisionsIsReady = copyFrom.VirtualPositionWithCollisionsIsReady;
//             CollisionsRemoved = copyFrom.CollisionsRemoved;
//         }

//         public void UpdateSimpleManeuverPrediction(ShipPositionInfo virtualPositionInfo, string maneuverCode)
//         {
//             VirtualPositionInfo = virtualPositionInfo;
//             PlannedManeuver = new(maneuverCode);
//             SimpleManeuverPredictionIsReady = true;
//         }

//         public void UpdateSimpleManeuverPrediction(ShipPositionInfo virtualPositionInfo, Maneuver maneuver)
//         {
//             VirtualPositionInfo = virtualPositionInfo;
//             PlannedManeuver = maneuver;
//             SimpleManeuverPredictionIsReady = true;
//         }

//         public void UpdateSimpleManeuverPrediction(ShipPositionInfo virtualPositionInfo)
//         {
//             VirtualPositionInfo = virtualPositionInfo;
//             PlannedManeuver = null;
//             SimpleManeuverPredictionIsReady = true;
//         }

//         public void UpdateNavigationResults(Dictionary<string, R> navigationResults)
//         {
//             NavigationResults = navigationResults;
//         }

//         public void Clear(ShipPositionInfo positionInfo)
//         {
//             RealPositionInfo = VirtualPositionInfo = positionInfo;

//             SimpleManeuverPredictionIsReady = false;
//             NavigationResults = null;
//             VirtualPositionWithCollisionsIsReady = false;
//         }

//         public bool RequiresFinalPositionPrediction()
//         {
//             return !SimpleManeuverPredictionIsReady && !AllFinalPositionsAreKnown;
//         }

//         public bool RequiresManeuverAssignment()
//         {
//             return PlannedManeuver == null;
//         }

//         public void SetPlannedManeuverCode(string maneuverCode, int order)
//         {
//             PlannedManeuver = new(maneuverCode);
//             OrderToActivate = order;
//         }

//         public void SetPlannedManeuver(Maneuver maneuver, int order)
//         {
//             PlannedManeuver = maneuver;
//             OrderToActivate = order;
//         }

//         public bool RequiresCollisionPrediction()
//         {
//             return VirtualPositionWithCollisionsIsReady == false;
//         }

//         public void SwitchToRealPosition()
//         {
//             if (!DebugManager.DebugMovementShowPlanning)
//             {
//                 ShipPositionInfo savedModelPosition = new(Ship.GetShipAllPartsTransform().position, Ship.GetShipAllPartsTransform().eulerAngles);
//                 Ship.SetPositionInfo(RealPositionInfo);
//                 Ship.GetShipAllPartsTransform().position = savedModelPosition.Position;
//                 Ship.GetShipAllPartsTransform().eulerAngles = savedModelPosition.Angles;
//                 Ship.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").position = savedModelPosition.Position;
//                 Ship.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").eulerAngles = savedModelPosition.Angles;
//             }
//             else
//             {
//                 Ship.SetPositionInfo(RealPositionInfo);
//             }
//             VirtualPositionWithCollisionsIsReady = false;
//         }

//         public void SwitchToVirtualPosition()
//         {
//             if (!DebugManager.DebugMovementShowPlanning)
//             {
//                 ShipPositionInfo savedModelPosition = new(Ship.GetShipAllPartsTransform().position, Ship.GetShipAllPartsTransform().eulerAngles);
//                 Ship.SetPositionInfo(VirtualPositionInfo);
//                 Ship.GetShipAllPartsTransform().position = savedModelPosition.Position;
//                 Ship.GetShipAllPartsTransform().eulerAngles = savedModelPosition.Angles;
//                 Ship.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").position = VirtualPositionInfo.Position;
//                 Ship.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").localPosition += new Vector3(0, 0.150289f, 1.156069f);
//                 Ship.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").eulerAngles = VirtualPositionInfo.Angles;
//             }
//             else
//             {
//                 Ship.SetPositionInfo(VirtualPositionInfo);
//             }
//             VirtualPositionWithCollisionsIsReady = true; // todo check this
//         }

//         public void RemoveCollisions()
//         {
//             if (!CollisionsRemoved)
//             {
//                 Vector3 savedModelPosition = Ship.GetShipAllPartsTransform().position;

//                 Ship.SetPosition(Ship.GetPosition() - new Vector3(0, -100, 0));
//                 Ship.GetShipAllPartsTransform().position = savedModelPosition;

//                 CollisionsRemoved = true;
//             }
//         }

//         public void ReturnCollisions()
//         {
//             if (CollisionsRemoved)
//             {
//                 Vector3 savedModelPosition = Ship.GetShipAllPartsTransform().position;

//                 Ship.SetPosition(Ship.GetPosition() - new Vector3(0, +100, 0));
//                 Ship.GetShipAllPartsTransform().position = savedModelPosition;

//                 CollisionsRemoved = false;
//             }
//         }
//     }

//     public class VirtualBoard<R> where R : ICloneable
//     {
//         public Dictionary<GenericShip, VirtualShipInfo<R>> Ships;
//         public int Round;

//         public VirtualBoard()
//         {
//             Update();
//             Ships ??= new();
//         }

//         public VirtualBoard(VirtualBoard<R> copyFrom)
//         {
//             Round = copyFrom.Round;
//             Ships = new();
//             foreach (GenericShip ship in copyFrom.Ships.Keys)
//             {
//                 Ships[ship] = new(copyFrom.Ships[ship]);
//             }
//         }

//         public void Update()
//         {
//             if (Round < Phases.RoundCounter)
//             {
//                 Ships = new Dictionary<GenericShip, VirtualShipInfo<R>>();
//                 foreach (GenericShip ship in Roster.AllShips.Values)
//                 {
//                     Ships.Add(ship, new VirtualShipInfo<R>(ship));
//                 }

//                 Round = Phases.RoundCounter;
//             }
//         }

//         public void SetVirtualPositionInfo(GenericShip ship, ShipPositionInfo virtualPositionInfo, string maneuverCode)
//         {
//             Ships[ship].UpdateSimpleManeuverPrediction(virtualPositionInfo, maneuverCode);
//         }

//         public void SetVirtualPositionInfoWithoutManeuver(GenericShip ship, ShipPositionInfo virtualPositionInfo)
//         {
//             Ships[ship].UpdateSimpleManeuverPrediction(virtualPositionInfo);
//         }

//         public void SetVirtualPositionInfo(GenericShip ship, ShipPositionInfo virtualPositionInfo, Maneuver maneuver)
//         {
//             Ships[ship].UpdateSimpleManeuverPrediction(virtualPositionInfo, maneuver);
//         }

//         public void UpdatePositionInfo(GenericShip ship)
//         {
//             Ships[ship].Clear(new ShipPositionInfo(ship.GetPosition(), ship.GetAngles()));
//         }

//         public void SwitchToVirtualPosition(GenericShip ship)
//         {
//             Ships[ship].SwitchToVirtualPosition();
//         }

//         public void SwitchToRealPosition(GenericShip ship)
//         {
//             Ships[ship].SwitchToRealPosition();
//         }

//         public void RestoreBoard()
//         {
//             foreach (GenericShip ship in Ships.Keys)
//             {
//                 SwitchToRealPosition(ship);
//             }
//         }

//         public void RemoveCollisionsExcept(GenericShip exceptShip)
//         {
//             foreach (GenericShip ship in Ships.Keys)
//             {
//                 if (ship == exceptShip) continue;

//                 Ships[ship].RemoveCollisions();
//             }
//         }

//         public void ReturnAllCollisions()
//         {
//             foreach (GenericShip ship in Ships.Keys)
//             {

//                 Ships[ship].ReturnCollisions();
//             }
//         }

//         public void RemoveCollisions(GenericShip ship)
//         {
//             Ships[ship].RemoveCollisions();
//         }

//         public void ReturnCollisions(GenericShip ship)
//         {
//             Ships[ship].ReturnCollisions();
//         }

//         public bool RequiresCollisionPrediction(GenericShip ship)
//         {
//             return Ships[ship].RequiresCollisionPrediction();
//         }

//         public bool RequiresFinalPositionPrediction(GenericShip ship)
//         {
//             return Ships[ship].RequiresFinalPositionPrediction();
//         }

//         public bool RequiresManeuverAssignment(GenericShip ship)
//         {
//             return Ships[ship].RequiresManeuverAssignment();
//         }

//         public void UpdateNavigationResults(GenericShip ship, Dictionary<string, R> navigationResults)
//         {
//             Ships[ship].UpdateNavigationResults(navigationResults);
//         }
//     }

//     public class VirtualBoardWrapper<R> : VirtualBoardManager.IVirtualBoard where R : ICloneable
//     {
//         private readonly VirtualBoard<R> InternalVirtualBoard;
        
//         public VirtualBoardPosition Position { get; private set; }

//         public enum VirtualBoardPosition
//         {
//             Real,
//             Virtual,
//             Inactive,
//         }

//         public VirtualBoardWrapper()
//         {
//             InternalVirtualBoard = new();
//             Position = VirtualBoardPosition.Inactive;
//         }

//         public void CleanupForDrop()
//         {
//             Deactivate();
//         }

//         public void Activate()
//         {
//             if (VirtualBoardManager.ActiveVirtualBoard == null)
//             {
//                 VirtualBoardManager.ActivateVirtualBoard(this);
//             }
//             else if (VirtualBoardManager.ActiveVirtualBoard == this)
//             {
//                 // pass
//             }
//             else
//             {
//                 throw new Exception("Attempt to activate virtual board while another is active.");
//             }
//         }

//         /// <summary>
//         /// You should not call this outside of VirtualBoard.cs
//         /// </summary>
//         public void ActivateInternal()
//         {
//             Position = VirtualBoardPosition.Virtual;
//             SwitchAllToVirtualPositions();
//         }

//         /// <summary>
//         /// You should not call this outside of VirtualBoard.cs
//         /// </summary>
//         public void RecoverActiveInternal()
//         {
//             ActivateInternal();
//         }

//         public bool TryActivate()
//         {
//             if (VirtualBoardManager.ActiveVirtualBoard == null)
//             {
//                 VirtualBoardManager.ActivateVirtualBoard(this);
//                 return true;
//             }
//             else if (VirtualBoardManager.ActiveVirtualBoard == this)
//             {
//                 return true;
//             }
//             else
//             {
//                 return false;
//             }
//         }

//         public void ForceActivate()
//         {
//             if (VirtualBoardManager.ActiveVirtualBoard != null && VirtualBoardManager.ActiveVirtualBoard != this)
//             {
//                 VirtualBoardManager.DeactivateCurrentBoard();
//             }
//             VirtualBoardManager.ActivateVirtualBoard(this);
//         }

//         public void Deactivate()
//         {
//             if (Position != VirtualBoardPosition.Inactive)
//             {
//                 VirtualBoardManager.DeactivateCurrentBoard();
//             }
//         }

//         /// <summary>
//         /// You should not call this outside of VirtualBoard.cs
//         /// </summary>
//         public void DeactivateInternal()
//         {
//             Position = VirtualBoardPosition.Inactive;
//         }

//         public VirtualBoard<R> GetVirtualBoard()
//         {
//             return InternalVirtualBoard;
//         }

//         public VirtualBoard<R> GetVirtualBoardRequireVirtualColliders()
//         {
//             AssertCollidersAreAccurate();
//             return InternalVirtualBoard;
//         }

//         public void AssertPositionsAreAccurate()
//         {
//             if (Position != VirtualBoardPosition.Virtual)
//             {
//                 throw new Exception("Read requiring virtual colliders on virtual board in real position.");
//             }
//             if (!IsAllShipsVirtualPositionAccurate())
//             {
//                 throw new Exception("Read requiring colliders on virtual board with colliders possibly incorrect.");
//             }
//         }

//         public void AssertCollidersAreAccurate()
//         {
//             AssertPositionsAreAccurate();
//         }

//         public void SwitchAllToRealPosition()
//         {
//             switch (Position)
//             {
//                 case VirtualBoardPosition.Virtual:
//                     InternalVirtualBoard.RestoreBoard();
//                     Position = VirtualBoardPosition.Real;
//                     break;
//                 case VirtualBoardPosition.Real:
//                     break;
//                 case VirtualBoardPosition.Inactive:
//                     throw new Exception("Attempt to switch positions of inactive virtual board.");
                    
//             }
//         }

//         public void SwitchAllToVirtualPositions()
//         {
//             switch (Position)
//             {
//                 case VirtualBoardPosition.Virtual:
//                     // fall.
//                 case VirtualBoardPosition.Real:
//                     foreach (GenericShip ship in InternalVirtualBoard.Ships.Keys)
//                     {
//                         InternalVirtualBoard.SwitchToVirtualPosition(ship);
//                     }
//                     Position = VirtualBoardPosition.Virtual;
//                     break;
//                 case VirtualBoardPosition.Inactive:
//                     throw new Exception("Attempt to switch positions of inactive virtual board.");
//             }
//         }

//         public ShotInfo GenerateShotInfo(GenericShip attacker, GenericShip defender, IShipWeapon weapon)
//         {
//             AssertPositionsAreAccurate();
//             return new ShotInfo(attacker, defender, weapon);
//         }

//         public bool IsAllShipsVirtualPositionAccurate()
//         {
//             foreach (VirtualShipInfo<R> shipInfo in InternalVirtualBoard.Ships.Values)
//             {
//                 if (!shipInfo.VirtualPositionWithCollisionsIsReady)
//                 {
//                     return false;
//                 }
//             }
//             return true;
//         }

//         public void SetOrderOfActivation(OrderOfActivation order)
//         {
//             for (int i = 0; i < order.Ships.Count; i++)
//             {
//                 InternalVirtualBoard.Ships[order.Ships[i]].OrderToActivate = i;
//             }
//         }

//         /// <summary>
//         /// Clone copyFrom, with the new objects's Position as Inactive.
//         /// </summary>
//         /// <param name="copyFrom"></param>
//         public VirtualBoardWrapper(VirtualBoardWrapper<R> copyFrom)
//         {
//             InternalVirtualBoard = new(copyFrom.InternalVirtualBoard);
//             Position = VirtualBoardPosition.Inactive;
//         }

//         public VirtualBoardWrapperShipInterface GetShipInterface(GenericShip ship)
//         {
//             return new VirtualBoardWrapperShipInterface(ship, this);
//         }

//         public List<VirtualBoardWrapperShipInterface> GetShipInterfaceOnAllShips()
//         {
//             List<VirtualBoardWrapperShipInterface> result = new();
//             foreach (GenericShip ship in InternalVirtualBoard.Ships.Keys)
//             {
//                 result.Add(new VirtualBoardWrapperShipInterface(ship, this));
//             }
//             return result;
//         }

//         public List<VirtualBoardWrapperShipInterface> GetShipInterfaceOnAllShipsWhere(Func<GenericShip, bool> predicate)
//         {
//             List<VirtualBoardWrapperShipInterface> result = new();
//             foreach (GenericShip ship in InternalVirtualBoard.Ships.Keys)
//             {
//                 if (predicate(ship)) {
//                     result.Add(new VirtualBoardWrapperShipInterface(ship, this));
//                 }
//             }
//             return result;
//         }

//         public readonly struct VirtualBoardWrapperShipInterface
//         {
//             public readonly GenericShip Ship { get; }
//             public readonly VirtualBoardWrapper<R> CreatedBy { get; }

//             public VirtualBoardWrapperShipInterface(GenericShip ship, VirtualBoardWrapper<R> createdBy)
//             {
//                 Ship = ship;
//                 CreatedBy = createdBy;
//             }

//             public readonly VirtualBoardWrapperShipInterface ResetVirtualToRealPosition()
//             {
//                 CreatedBy.GetVirtualBoard().UpdatePositionInfo(Ship);
//                 return this;
//             }

//             public readonly IEnumerator AssignAndApplyManeuver(string maneuverCode)
//             {
//                 GenericMovement movement = ShipMovementScript.MovementFromString(maneuverCode);
//                 MovementPrediction prediction = new(Ship, movement);
//                 yield return prediction.CalculateMovementPredicition();
//                 CreatedBy.GetVirtualBoardRequireVirtualColliders().SetVirtualPositionInfo(Ship, prediction.FinalPositionInfo, maneuverCode);
//             }

//             public readonly IEnumerator AssignAndApplyManeuver(Maneuver maneuver)
//             {
//                 GenericMovement movement = ShipMovementScript.MovementFromString(maneuver.ToString());
//                 MovementPrediction prediction = new(Ship, movement);
//                 yield return prediction.CalculateMovementPredicition();
//                 CreatedBy.GetVirtualBoardRequireVirtualColliders().SetVirtualPositionInfo(Ship, prediction.FinalPositionInfo, maneuver);
//             }

//             public readonly VirtualBoardWrapperShipInterface SetVirtualPositionInfo(ShipPositionInfo virtualPositionInfo, string maneuverCode)
//             {
//                 CreatedBy.GetVirtualBoard().SetVirtualPositionInfo(Ship, virtualPositionInfo, maneuverCode);
//                 return this;
//             }

//             /// <summary>
//             /// Clears stored maneuver.
//             /// </summary>
//             /// <param name="virtualPositionInfo"></param>
//             /// <returns></returns>
//             public readonly VirtualBoardWrapperShipInterface SetVirtualPositionInfo(ShipPositionInfo virtualPositionInfo)
//             {
//                 CreatedBy.GetVirtualBoard().SetVirtualPositionInfoWithoutManeuver(Ship, virtualPositionInfo);
//                 return this;
//             }
//         }
//     }
// }

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
                UpdateRealBoard();
            }
            ActiveVirtualBoard?.DeactivateInternal();
            LastActiveVirtualBoard = ActiveVirtualBoard;
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
            }
            else
            {
                throw new Exception("Attempt to activate a virtual board while another is active.");
            }
        }
        
        /// <summary>
        /// This functions as a claim.
        /// </summary>
        public static void RestoreRealBoard()
        {
            if (ActiveVirtualBoard != null && ActiveVirtualBoard != RealBoard)
            {
                DeactivateCurrentBoard();
            }
            ActivateVirtualBoard(RealBoard);
            RealBoard.ApplyVirtualPositions();
        }

        private static void UpdateRealBoard()
        {
            foreach (GenericShip ship in RealBoard.Ships.Keys)
            {
                if (ship.IsDestroyed)
                {
                    RealBoard.Ships.Remove(ship);
                    continue;
                }

                RealBoard.Ships[ship].VirtualPosition = ship.GetPositionInfo();
            }

            foreach (GenericShip ship in Roster.AllShips.Values)
            {
                if (!RealBoard.Ships.ContainsKey(ship))
                {
                    RealBoard.Ships.Add(ship, new NewVirtualBoard<EmptyStruct>.ShipInfo(ship.GetPositionInfo(), new()));
                }
            }
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

            public ShipInfo(ShipPositionInfo shipPositionInfo, T otherData)
            {
                VirtualPosition = shipPositionInfo;
                OtherData = otherData;
                CollisionsRemoved = true;
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
                thisShip.SetPositionInfo(VirtualPosition);
            }
        }

        public NewVirtualBoard()
        {
            Ships = new();
            State = VirtualBoardState.Inactive;
        }

        public void ActivateInternal()
        {
            State = VirtualBoardState.Other;
        }

        public void RecoverActiveInternal()
        {
            ActivateInternal();
        }

        public void DeactivateInternal()
        {
            State = VirtualBoardState.Inactive;
        }

        public NewVirtualBoard<T> Activate()
        {
            if (VirtualBoardManager.ActiveVirtualBoard != this)
            {
                VirtualBoardManager.ActivateVirtualBoard(this);
            }
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

        public NewVirtualBoard<T> ClaimActive()
        {
            VirtualBoardManager.DeactivateCurrentBoard();
            VirtualBoardManager.ActivateVirtualBoard(this);
            return this;
        }

        public NewVirtualBoard<T> Deactivate()
        {
            if (VirtualBoardManager.ActiveVirtualBoard == this)
            {
                VirtualBoardManager.DeactivateCurrentBoard();
            }
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

        public NewVirtualBoard<T> UpdateToReal(Func<GenericShip, T> newShipInitialiser)
        {
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
                    CreatedBy.Ships[Ship].ApplyPosition(Ship);
                }
                return this;
            }

            /// <summary>
            /// If this board is active, also apply the new position.
            /// </summary>
            /// <param name="info"></param>
            /// <returns></returns>
            public ShipInterface ResetToRealPosition()
            {
                CreatedBy.Ships[Ship].VirtualPosition = VirtualBoardManager.RealBoard.Ships[Ship].VirtualPosition;
                if (CreatedBy.State != VirtualBoardState.Inactive)
                {
                    CreatedBy.Ships[Ship].ApplyPosition(Ship);
                }
                return this;
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
                throw new Exception();
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
            foreach (KeyValuePair<GenericShip, ShipInfo> pair in Ships)
            {
                pair.Value.ReturnCollisions(pair.Key);
            }
            return this;
        }
    }
}