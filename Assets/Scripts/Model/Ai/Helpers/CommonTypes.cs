#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ActionsList;
using AI.Helpers.Navigation.PredictionHelpers;
using BoardTools;
using Movement;
using Ship;
using SubPhases;
using UnityEngine;

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
            ManeuverHolder.ParseManeuverCode(maneuverCode, out speed, out direction, out bearing);

            this.complexity = complexity;
        }

        public Maneuver(ManeuverHolder maneuverHolder)
        {
            speed = maneuverHolder.Speed;
            direction = maneuverHolder.Direction;
            bearing = maneuverHolder.Bearing;
            complexity = maneuverHolder.ColorComplexity;
        }

        public Maneuver(ManeuverTemplate maneuverTemplate, MovementComplexity complexity = MovementComplexity.None)
        {
            speed = maneuverTemplate.Speed;
            direction = maneuverTemplate.Direction;
            bearing = maneuverTemplate.Bearing;
            this.complexity = complexity;
        }

        public Maneuver(BoostMove boostMove)
        {
            switch (boostMove.Template)
            {
                case ActionsHolder.BoostTemplates.LeftBank1:
                case ActionsHolder.BoostTemplates.LeftBank2:
                case ActionsHolder.BoostTemplates.RightBank1:
                case ActionsHolder.BoostTemplates.RightBank2:
                    bearing = ManeuverBearing.Bank;
                    break;
                case ActionsHolder.BoostTemplates.LeftTurn1:
                case ActionsHolder.BoostTemplates.LeftTurn2:
                case ActionsHolder.BoostTemplates.RightTurn1:
                case ActionsHolder.BoostTemplates.RightTurn2:
                    bearing = ManeuverBearing.Turn;
                    break;
                case ActionsHolder.BoostTemplates.Straight1:
                case ActionsHolder.BoostTemplates.Straight2:
                    bearing = ManeuverBearing.Straight;
                    break;
                default:
                    Console.Write("AI.Helpers.Types.Maneuver new(BoostMove) BoostTemplate not recognised");
                    bearing = ManeuverBearing.Straight;
                    break;
            }

            switch (boostMove.Template)
            {
                case ActionsHolder.BoostTemplates.LeftBank1:
                case ActionsHolder.BoostTemplates.LeftBank2:
                case ActionsHolder.BoostTemplates.LeftTurn1:
                case ActionsHolder.BoostTemplates.LeftTurn2:
                    direction = ManeuverDirection.Left;
                    break;
                case ActionsHolder.BoostTemplates.RightBank1:
                case ActionsHolder.BoostTemplates.RightBank2:
                case ActionsHolder.BoostTemplates.RightTurn1:
                case ActionsHolder.BoostTemplates.RightTurn2:
                    direction = ManeuverDirection.Right;
                    break;
                case ActionsHolder.BoostTemplates.Straight1:
                case ActionsHolder.BoostTemplates.Straight2:
                    direction = ManeuverDirection.Forward;
                    break;
                default:
                    direction = ManeuverDirection.Forward;
                    break;
            }

            switch (boostMove.Template)
            {
                case ActionsHolder.BoostTemplates.LeftBank1:
                case ActionsHolder.BoostTemplates.RightBank1:
                case ActionsHolder.BoostTemplates.LeftTurn1:
                case ActionsHolder.BoostTemplates.RightTurn1:
                case ActionsHolder.BoostTemplates.Straight1:
                    speed = ManeuverSpeed.Speed1;
                    break;
                case ActionsHolder.BoostTemplates.LeftBank2:
                case ActionsHolder.BoostTemplates.RightBank2:
                case ActionsHolder.BoostTemplates.LeftTurn2:
                case ActionsHolder.BoostTemplates.RightTurn2:
                case ActionsHolder.BoostTemplates.Straight2:
                    speed = ManeuverSpeed.Speed2;
                    break;
                default:
                    speed = ManeuverSpeed.Speed1;
                    break;
            }

            if (boostMove.IsRed)
            {
                complexity = MovementComplexity.Complex;
            }
            else if (boostMove.IsPurple)
            {
                complexity = MovementComplexity.Purple;
            }
            else
            {
                complexity = MovementComplexity.Normal;
            }
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

        public ManeuverTemplate ToManeuverTemplate(bool isSideTemplate = false, bool isBombTemplate = false)
        {
            return new ManeuverTemplate(bearing, direction, speed, isBombTemplate, isSideTemplate);
        }

        public static class Static {
            public static Maneuver TwoFowardStraight { get; } = new("2.F.S", MovementComplexity.None);
        }
    }
}

namespace AI.Helpers.Types.Actions
{
    public class BarrelRollPlanInfo
    {
        public string TemplateOptionName;
        private string[] SplitTemplateOptionName
        {
            get
            {
                return TemplateOptionName.Split(' ');
            }
        }
        public string PositionOptionName;

        public GenericShip Ship;

        public ShipPositionInfo? FinalPositionInfo;

        public bool IsAllowed;

        public BarrelRollPlanInfo(string template, string position, GenericShip ship)
        {
            TemplateOptionName = template;
            PositionOptionName = position;
            Ship = ship;
        }

        public Direction GetDirectionPrimary()
        {
            switch (SplitTemplateOptionName.First())
            {
                case "Right": 
                    return Direction.Right;
                case "Left":
                    return Direction.Left;
                default:
                    ConsoleHelpers.Error("GenericAiPlayer.BarrelRollPlanInfo.GetDirectionPrimary", "TemplateSplit[0] not recognised.");
                    return Direction.None;
            }
        }

        public Direction GetDirectionSecondary()
        {
            if (SplitTemplateOptionName.Length == 3)
            {
                return Direction.None;
            }
            else
            {
                switch (SplitTemplateOptionName[3])
                {
                    case "Top": 
                        return Direction.Top;
                    case "Bottom":
                        return Direction.Bottom;
                    default:
                        ConsoleHelpers.Error("GenericAiPlayer.BarrelRollPlanInfo.GetDirectionSecondary", "TemplateSplit[3] not recognised.");
                        return Direction.None;
                }
            }
        }

        public Direction GetDirectionShift()
        {
            switch (PositionOptionName)
            {
                case "Forward":
                    return Direction.Top;
                case "Center":
                    return Direction.None;
                case "Backwards":
                    return Direction.Bottom;
                default:
                    ConsoleHelpers.Error("GenericAiPlayer.BarrelRollPlanInfo.GetDirectionShift", "Position not recognised.");
                    return Direction.None;
            }
        }

        public Maneuver GetTemplate()
        {
            ManeuverBearing bearing;
            switch (SplitTemplateOptionName[1])
            {
                case "Straight":
                    bearing = ManeuverBearing.Straight;
                    break;
                case "Bank":
                    bearing = ManeuverBearing.Bank;
                    break;
                case "Turn":
                    bearing = ManeuverBearing.Turn;
                    break;
                default:
                    ConsoleHelpers.Error("GenericAiPlayer.BarrelRollPlanInfo.GetTemplate", "TemplateSplit[1] not recognised.");
                    bearing = ManeuverBearing.None;
                    break;
            }

            ManeuverSpeed speed;
            switch (SplitTemplateOptionName[2])
            {
                case "1":
                    speed = ManeuverSpeed.Speed1;
                    break;
                case "2":
                    speed = ManeuverSpeed.Speed2;
                    break;
                case "3":
                    speed = ManeuverSpeed.Speed3;
                    break;
                case "4":
                    speed = ManeuverSpeed.Speed4;
                    break;
                case "5":
                    speed = ManeuverSpeed.Speed5;
                    break;
                default:
                    speed = ManeuverSpeed.Speed0;
                    ConsoleHelpers.Error("GenericAiPlayer.BarrelRollPlanInfo.GetTemplate", "TemplateSplit[2] not recognised.");
                    break;
            }

            bool isLeftTemplate = GetDirectionPrimary() == Direction.Left ^ GetDirectionSecondary() == Direction.Top;
            ManeuverDirection direction = isLeftTemplate ? ManeuverDirection.Left : ManeuverDirection.Right;
            
            return new Maneuver(speed, direction, bearing);
        }

        public IEnumerator DoCalculations()
        {
            Maneuver maneuver = GetTemplate();
            bool isSideTemplate = Ship.ShipBase.Size != BaseSize.Small;
            ManeuverTemplate template = maneuver.ToManeuverTemplate(isSideTemplate);
            Direction primaryDirection = GetDirectionPrimary();
            template.ApplyTemplate(
                Ship,
                (primaryDirection == Direction.Left) ? Ship.GetLeft() : Ship.GetRight(),
                primaryDirection
                );
            template.Collider.TheShip = Ship;
            template.Collider.ReCheckCollisionsStart();
            template.ToggleRenderer(false);
            yield return new WaitForFixedUpdate();
            IsAllowed = BarrelRollPlanningSubPhase.IsColliderDataAllowed(template.Collider, Ship, true, false);
            Direction shiftDirection = GetDirectionShift();
            FinalPositionInfo = BarrelRollPlanningSubPhase.FinalPositionInfo(Ship.ShipBase, template, primaryDirection, shiftDirection);
            template.DestroyTemplate();
        }
    }

    public class BarrelRollActionWithPlans : BarrelRollAction
    {
        public List<BarrelRollPlanInfo> BarrelRollPlans = new();

        public BarrelRollActionWithPlans(BarrelRollAction copyfrom, List<BarrelRollPlanInfo> barrelRollPlans) : base()
        {
            BarrelRollPlans = barrelRollPlans;

            HostShip = copyfrom.HostShip;
        }
    }

    public class BoostPlanInfo
    {
        public BoostMove BoostMove;

        public ShipPositionInfo? FinalPositionInfo;

        public bool IsAllowed;

        public BoostPlanInfo(BoostMove boostMove)
        {
            BoostMove = boostMove;
        }

        public IEnumerator DoCalculations(GenericShip ship)
        {
            ShipPositionInfo savedShipPositionInfo = ship.GetPositionInfo();

            Maneuver maneuver = new(BoostMove);

            yield return SingleMovementPredictionHelper.Calculate(ship, maneuver, true);
            MovementPrediction movementPrediction = SingleMovementPredictionHelper.Prediction;

            IsAllowed = movementPrediction.IsOffTheBoard || movementPrediction.IsBumped || movementPrediction.IsHitOrLandedOnObstacle;
            FinalPositionInfo = ship.GetPositionInfo();

            ship.SetPositionInfo(savedShipPositionInfo);
        }
    }

    public class BoostActionWithPlans : BoostAction
    {
        public List<BoostPlanInfo> BoostPlans = new();

        public BoostActionWithPlans(BoostAction copyfrom, List<BoostPlanInfo> boostPlans) : base()
        {
            BoostPlans = boostPlans;

            HostShip = copyfrom.HostShip;
        }
    }
}

namespace AI.Helpers.Types
{
    public class Result<T,E>
    {
        private T? success;
        private E? error;
        private bool isSuccess;

        public Result(T successValue)
        {
            success = successValue;
            error = default;
            isSuccess = true;
        }

        public Result(E errorValue)
        {
            success = default;
            error = errorValue;
            isSuccess = false;
        }

        public bool IsSuccess { get { return isSuccess; } }
        public bool IsError { get { return !IsSuccess; } }
        
        public bool TryGetSuccess([System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out T? success)
        {
            success = this.success;
            return IsSuccess;
        }

        public bool TryGetError([System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out E? error)
        {
            error = this.error;
            return IsError;
        }

        public T SuccessOrException()
        {
            if (IsSuccess)
            {
                // null forgiving: this.success should not be null when IsSuccess is true.
                return success!;
            }
            else
            {
                throw new Exception();
            }
        }

        public E ErrorOrException()
        {
            if (IsError)
            {
                // null forgiving: this.error should not be null when IsError is true.
                return error!;
            }
            else
            {
                throw new Exception();
            }
        }

        public Result<T,E> ExpectSuccess()
        {
            if (IsError)
            {
                throw new Exception();
            }
            else
            {
                return this;
            }
        }

        public Result<T,E> ExpectError()
        {
            if (IsSuccess)
            {
                throw new Exception();
            }
            else
            {
                return this;
            }
        }
    }
}

namespace AI.Helpers
{
    public static class ConsoleHelpers
    {
        public static void Warn(string source, string value)
        {
            string message = $"Warn: {source}: {value}";
            Console.Write(message, false, "yellow");
            Messages.ShowError(message, false);
        }

        public static void Error(string source, string value)
        {
            string message = $"Error: {source}: {value}";
            Console.Write(message, true, "red");
            Messages.ShowError(message, true);
        }
    }
}