#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using AI.Helpers.Types;
using BoardTools;
using Movement;
using Ship;
using UnityEngine;

namespace AI.Helpers.Navigation
{
    public class VirtualShipInfo<Result>
    {
        public GenericShip Ship { get; private set; }
        public ShipPositionInfo RealPositionInfo { get; private set; }
        public ShipPositionInfo VirtualPositionInfo { get; private set; }
        public string? PlannedManeuverCode { get; set; }
        public Dictionary<string, Result>? NavigationResults { get; private set; }
        public int OrderToActivate { get; set; }

        private bool SimpleManeuverPredictionIsReady;
        private bool AllFinalPositionsAreKnown { get { return NavigationResults != null; } }

        public bool VirtualPositionWithCollisionsIsReady { get; private set; }

        public bool CollisionsRemoved { get; private set; }

        public VirtualShipInfo(GenericShip ship)
        {
            Ship = ship;
            RealPositionInfo = new ShipPositionInfo(ship.GetPosition(), ship.GetAngles());
        }

        public void UpdateSimpleManeuverPrediction(ShipPositionInfo virtualPositionInfo, string? maneuverCode)
        {
            VirtualPositionInfo = virtualPositionInfo;
            PlannedManeuverCode = maneuverCode;
            SimpleManeuverPredictionIsReady = true;
        }

        public void UpdateNavigationResults(Dictionary<string, Result> navigationResults)
        {
            NavigationResults = navigationResults;
        }

        public void Clear(ShipPositionInfo positionInfo)
        {
            RealPositionInfo = VirtualPositionInfo = positionInfo;

            SimpleManeuverPredictionIsReady = false;
            NavigationResults = null;
            VirtualPositionWithCollisionsIsReady = false;
        }

        public bool RequiresFinalPositionPrediction()
        {
            return !SimpleManeuverPredictionIsReady && !AllFinalPositionsAreKnown;
        }

        public bool RequiresManeuverAssignment()
        {
            return PlannedManeuverCode == null;
        }

        public void SetPlannedManeuverCode(string maneuverCode, int order)
        {
            PlannedManeuverCode = maneuverCode;
            OrderToActivate = order;
        }

        public bool RequiresCollisionPrediction()
        {
            return VirtualPositionWithCollisionsIsReady == false;
        }

        public void SwitchToRealPosition()
        {
            if (!DebugManager.DebugMovementShowPlanning)
            {
                ShipPositionInfo savedModelPosition = new(Ship.GetShipAllPartsTransform().position, Ship.GetShipAllPartsTransform().eulerAngles);
                Ship.SetPositionInfo(RealPositionInfo);
                Ship.GetShipAllPartsTransform().position = savedModelPosition.Position;
                Ship.GetShipAllPartsTransform().eulerAngles = savedModelPosition.Angles;
                Ship.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").position = savedModelPosition.Position;
                Ship.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").eulerAngles = savedModelPosition.Angles;
            }
            else
            {
                Ship.SetPositionInfo(RealPositionInfo);
            }
            VirtualPositionWithCollisionsIsReady = false;
        }

        public void SwitchToVirtualPosition()
        {
            if (!DebugManager.DebugMovementShowPlanning)
            {
                ShipPositionInfo savedModelPosition = new(Ship.GetShipAllPartsTransform().position, Ship.GetShipAllPartsTransform().eulerAngles);
                Ship.SetPositionInfo(VirtualPositionInfo);
                Ship.GetShipAllPartsTransform().position = savedModelPosition.Position;
                Ship.GetShipAllPartsTransform().eulerAngles = savedModelPosition.Angles;
                Ship.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").position = VirtualPositionInfo.Position;
                Ship.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").localPosition += new Vector3(0, 0.150289f, 1.156069f);
                Ship.GetShipAllPartsTransform().Find("ShipBase/ShipBaseCollider").eulerAngles = VirtualPositionInfo.Angles;
            }
            else
            {
                Ship.SetPositionInfo(VirtualPositionInfo);
            }
            VirtualPositionWithCollisionsIsReady = true; // todo check this
        }

        public void RemoveCollisions()
        {
            if (!CollisionsRemoved)
            {
                Vector3 savedModelPosition = Ship.GetShipAllPartsTransform().position;

                Ship.SetPosition(Ship.GetPosition() - new Vector3(0, -100, 0));
                Ship.GetShipAllPartsTransform().position = savedModelPosition;

                CollisionsRemoved = true;
            }
        }

        public void ReturnCollisions()
        {
            if (CollisionsRemoved)
            {
                Vector3 savedModelPosition = Ship.GetShipAllPartsTransform().position;

                Ship.SetPosition(Ship.GetPosition() - new Vector3(0, +100, 0));
                Ship.GetShipAllPartsTransform().position = savedModelPosition;

                CollisionsRemoved = false;
            }
        }
    }

    public class VirtualBoard<Result>
    {
        public Dictionary<GenericShip, VirtualShipInfo<Result>> Ships;
        public int Round;

        public VirtualBoard()
        {
            Update();
            Ships ??= new();
        }

        public void Update()
        {
            if (Round < Phases.RoundCounter)
            {
                Ships = new Dictionary<GenericShip, VirtualShipInfo<Result>>();
                foreach (GenericShip ship in Roster.AllShips.Values)
                {
                    Ships.Add(ship, new VirtualShipInfo<Result>(ship));
                }

                Round = Phases.RoundCounter;
            }
        }

        public void SetVirtualPositionInfo(GenericShip ship, ShipPositionInfo virtualPositionInfo, string? maneuverCode)
        {
            Ships[ship].UpdateSimpleManeuverPrediction(virtualPositionInfo, maneuverCode);
        }

        public void UpdatePositionInfo(GenericShip ship)
        {
            Ships[ship].Clear(new ShipPositionInfo(ship.GetPosition(), ship.GetAngles()));
        }

        public void SwitchToVirtualPosition(GenericShip ship)
        {
            Ships[ship].SwitchToVirtualPosition();
        }

        public void SwitchToRealPosition(GenericShip ship)
        {
            Ships[ship].SwitchToRealPosition();
        }

        public void RestoreBoard()
        {
            foreach (GenericShip ship in Ships.Keys)
            {
                SwitchToRealPosition(ship);
            }
        }

        public void RemoveCollisionsExcept(GenericShip exceptShip)
        {
            foreach (GenericShip ship in Ships.Keys)
            {
                if (ship == exceptShip) continue;

                Ships[ship].RemoveCollisions();
            }
        }

        public void ReturnAllCollisions()
        {
            foreach (GenericShip ship in Ships.Keys)
            {

                Ships[ship].ReturnCollisions();
            }
        }

        public void RemoveCollisions(GenericShip ship)
        {
            Ships[ship].RemoveCollisions();
        }

        public void ReturnCollisions(GenericShip ship)
        {
            Ships[ship].ReturnCollisions();
        }

        public bool RequiresCollisionPrediction(GenericShip ship)
        {
            return Ships[ship].RequiresCollisionPrediction();
        }

        public bool RequiresFinalPositionPrediction(GenericShip ship)
        {
            return Ships[ship].RequiresFinalPositionPrediction();
        }

        public bool RequiresManeuverAssignment(GenericShip ship)
        {
            return Ships[ship].RequiresManeuverAssignment();
        }

        public void UpdateNavigationResults(GenericShip ship, Dictionary<string, Result> navigationResults)
        {
            Ships[ship].UpdateNavigationResults(navigationResults);
        }
    }

    public class VirtualBoardWrapper<Result>
    {
        public VirtualBoard<Result> InternalVirtualBoard;
        public bool IsInRealPosition { get; private set; }
        
        public VirtualBoardWrapper()
        {
            InternalVirtualBoard = new();
            IsInRealPosition = true;
        }

        public void CleanupForDrop()
        {
            InternalVirtualBoard.RestoreBoard();
            IsInRealPosition = true;
        }

        public VirtualBoard<Result> GetVirtualBoard()
        {
            return InternalVirtualBoard;
        }

        public VirtualBoard<Result> GetVirtualBoardRequireColliders()
        {
            if (IsInRealPosition)
            {
                Console.Write("\nDebug Warning: Read requiring colliders on virtual board in real position.", false, "red");
                Messages.ShowError("Debug Warning: Read requiring colliders on virtual board in real position.");
            }
            if (!IsAllShipsVirtualPositionAccurate())
            {
                Console.Write("\nDebug Warning: Read requiring colliders on virtual board with colliders possibly incorrect.", false, "red");
                Messages.ShowError("Debug Warning: Read requiring colliders on virtual board with colliders possibly incorrect.");
            }
            return InternalVirtualBoard;
        }

        public void SwitchAllToRealPosition()
        {
            InternalVirtualBoard.RestoreBoard();
            IsInRealPosition = true;
        }

        public void SwitchAllToVirtualPositions()
        {
            foreach (GenericShip ship in InternalVirtualBoard.Ships.Keys)
            {
                InternalVirtualBoard.SwitchToVirtualPosition(ship);
            }
            IsInRealPosition = false;
        }

        public ShotInfo GenerateShotInfo(GenericShip attacker, GenericShip defender, IShipWeapon weapon)
        {
            return new ShotInfo(attacker, defender, weapon);
        }

        public bool IsAllShipsVirtualPositionAccurate()
        {
            foreach (VirtualShipInfo<Result> shipInfo in InternalVirtualBoard.Ships.Values)
            {
                if (!shipInfo.VirtualPositionWithCollisionsIsReady)
                {
                    return false;
                }
            }
            return true;
        }

        public void SetOrderOfActivation(OrderOfActivation order)
        {
            for (int i = 0; i < order.Ships.Count; i++)
            {
                InternalVirtualBoard.Ships[order.Ships[i]].OrderToActivate = i;
            }
        }

        public VirtualBoardWrapperShipInterface GetShipInterface(GenericShip ship)
        {
            return new VirtualBoardWrapperShipInterface(ship, this);
        }

        public List<VirtualBoardWrapperShipInterface> GetShipInterfaceOnAllShips()
        {
            List<VirtualBoardWrapperShipInterface> result = new();
            foreach (GenericShip ship in InternalVirtualBoard.Ships.Keys)
            {
                result.Add(new VirtualBoardWrapperShipInterface(ship, this));
            }
            return result;
        }

        public List<VirtualBoardWrapperShipInterface> GetShipInterfaceOnAllShipsWhere(Func<GenericShip, bool> predicate)
        {
            List<VirtualBoardWrapperShipInterface> result = new();
            foreach (GenericShip ship in InternalVirtualBoard.Ships.Keys)
            {
                if (predicate(ship)) {
                    result.Add(new VirtualBoardWrapperShipInterface(ship, this));
                }
            }
            return result;
        }

        public readonly struct VirtualBoardWrapperShipInterface
        {
            public readonly GenericShip Ship { get; }
            public readonly VirtualBoardWrapper<Result> CreatedBy { get; }

            public VirtualBoardWrapperShipInterface(GenericShip ship, VirtualBoardWrapper<Result> createdBy)
            {
                Ship = ship;
                CreatedBy = createdBy;
            }

            public readonly VirtualBoardWrapperShipInterface ResetVirtualToRealPosition()
            {
                CreatedBy.GetVirtualBoard().UpdatePositionInfo(Ship);
                return this;
            }

            public readonly IEnumerator AssignAndApplyManeuver(string maneuverCode)
            {
                GenericMovement movement = ShipMovementScript.MovementFromString(maneuverCode);
                MovementPrediction prediction = new(Ship, movement);
                yield return prediction.CalculateMovementPredicition();
                CreatedBy.GetVirtualBoardRequireColliders().SetVirtualPositionInfo(Ship, prediction.FinalPositionInfo, maneuverCode);
            }

            public readonly VirtualBoardWrapperShipInterface SetVirtualPositionInfo(ShipPositionInfo virtualPositionInfo, string maneuverCode)
            {
                CreatedBy.GetVirtualBoard().SetVirtualPositionInfo(Ship, virtualPositionInfo, maneuverCode);
                return this;
            }

            /// <summary>
            /// Clears stored maneuver.
            /// </summary>
            /// <param name="virtualPositionInfo"></param>
            /// <returns></returns>
            public readonly VirtualBoardWrapperShipInterface SetVirtualPositionInfo(ShipPositionInfo virtualPositionInfo)
            {
                CreatedBy.GetVirtualBoard().SetVirtualPositionInfo(Ship, virtualPositionInfo, null);
                return this;
            }
        }
    }
}