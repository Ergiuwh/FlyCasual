#nullable enable

using System.Collections.Generic;
using AI.Helpers.Types;
using Players;
using Ship;
using SubPhases;

namespace AI.Helpers.VirtualBoard.Debug
{
    public class VirtualBoardLog
    {
        public List<Step> Steps;

        public VirtualBoardLog()
        {
            Steps = new();
        }

        public void AddSingleMoveStep(string name, GenericShip ship, ShipPositionInfo positionInfo)
        {
            Step step = new(name);
            step.Instructions.Add(new Instruction(ship, positionInfo));
            Steps.Add(step);
        }

        public void AddUpdateToVirtualBoard<T>(string name, VirtualBoard<T> virtualBoard) where T : class
        {
            Step step = new(name);
            foreach (KeyValuePair<GenericShip, VirtualBoard<T>.ShipInfo> shipInfo in virtualBoard.Ships)
            {
                step.Instructions.Add(new(shipInfo.Key, shipInfo.Value.VirtualPosition));
            }

            Steps.Add(step);
        }

        public class Step
        {
            public string Name;
            public List<Instruction> Instructions;

            public Step(string name)
            {
                Name = name;
                Instructions = new();
            }
        }

        public struct Instruction
        {
            public GenericShip Ship;
            public (bool, ShipPositionInfo) PositionInfo;
            public (bool, bool) EnabledCollisions;

            public Instruction(GenericShip ship, ShipPositionInfo positionInfo)
            {
                Ship = ship;
                PositionInfo = (true, positionInfo);
                EnabledCollisions = (false, default);
            }

            public Instruction(GenericShip ship, bool enabledCollisions)
            {
                Ship = ship;
                PositionInfo = (false, default);
                EnabledCollisions = (true, enabledCollisions);
            }
        }

        private static class DisplayManager
        {
            private static IEnumerator<Step>? Iter;
            private static VirtualBoardNoData DisplayVirtualBoard = new();
            private static bool SavedDebugMovementShowPlanning;

            public static bool Start(VirtualBoardLog virtualBoardLog)
            {
                if (virtualBoardLog.Steps.Count <= 2)
                {
                    return false;
                }
                else
                {
                    Iter = virtualBoardLog.Steps.GetEnumerator();
                    DisplayVirtualBoard = new();
                    DisplayVirtualBoard.Activate();

                    SavedDebugMovementShowPlanning = DebugManager.DebugMovementShowPlanning;
                    DebugManager.DebugMovementShowPlanning = true;

                    Iter.MoveNext();
                    return true;
                }
            }

            public static bool Next()
            {
                if (Iter is null)
                {
                    Console.Write("VirtualBoardLog.DisplayManager: Next while Iter is null.", true, "red");
                    return false;
                }

                Step value = Iter.Current;

                foreach (Instruction instruction in value.Instructions)
                {
                    if (instruction.PositionInfo.Item1) {
                        DisplayVirtualBoard.GetShipInterface(instruction.Ship).SetPosition(instruction.PositionInfo.Item2);
                    }

                    if (instruction.EnabledCollisions.Item1) {
                        DisplayVirtualBoard.GetShipInterface(instruction.Ship).ToggleCollisions(instruction.EnabledCollisions.Item2);
                    }
                }

                bool result = Iter.MoveNext();

                if (result == false)
                {
                    End();
                }

                return result;
            }

            public static void End()
            {
                DebugManager.DebugMovementShowPlanning = SavedDebugMovementShowPlanning;
                Iter = null;
                DisplayVirtualBoard.Deactivate();
            }

            public static bool TryStartSubPhase(VirtualBoardLog virtualBoardLog)
            {
                bool startSuccess = Start(virtualBoardLog);

                if (startSuccess == false)
                {
                    return false;
                }

                VirtualBoardLogDisplaySubPhase subPhase = Phases.StartTemporarySubPhaseNew<VirtualBoardLogDisplaySubPhase>("VirtualBoardLogDisplaySubPhase", DisplayManager.End);
                subPhase.Start();

                return true;
            }
        }

        private class VirtualBoardLogDisplaySubPhase : GenericSubPhase
        {
            public override void Start()
            {
                SingleVirtualBoardLogDisplaySubPhase subPhase = Phases.StartTemporarySubPhaseNew<SingleVirtualBoardLogDisplaySubPhase>("SingleVirtualBoardLogDisplaySubPhase", delegate {  });
                subPhase.Start();
            }

            public override List<GameCommandTypes> AllowedGameCommandTypes { get { return new List<GameCommandTypes>() { GameCommandTypes.PressSkip }; } }
        }

        private class SingleVirtualBoardLogDisplaySubPhase : GenericSubPhase
        {
            public SingleVirtualBoardLogDisplaySubPhase()
            {
                
            }

            public override void Start()
            {

            }

            public override void Next()
            {
                if (DisplayManager.Next()) {
                    SingleVirtualBoardLogDisplaySubPhase nextSubPhase = Phases.StartTemporarySubPhaseNew<SingleVirtualBoardLogDisplaySubPhase>("SingleVirtualBoardLogDisplaySubPhase", delegate {  });
                    nextSubPhase.Start();
                }
            }

            public override List<GameCommandTypes> AllowedGameCommandTypes { get { return new List<GameCommandTypes>() { GameCommandTypes.PressNext }; } }
        }

        
        public static class Instance
        {
            public static Dictionary<PlayerNo, VirtualBoardLog> Logs = new();

            public static VirtualBoardLog CurrentLog
            {
                get
                {
                    return Logs[Phases.CurrentPhasePlayer];
                }
            }

            public static void InitializeLogForPlayer(PlayerNo playerNo)
            {
                Logs.Add(playerNo, new VirtualBoardLog());
            }

            public static Result<int,string> StartDisplay(PlayerNo playerNo)
            {
                if (Logs.TryGetValue(playerNo, out VirtualBoardLog log))
                {
                    if (DisplayManager.TryStartSubPhase(log))
                    {
                        return new Result<int, string>(successValue: 0);
                    }
                    else
                    {
                        return new Result<int, string>(errorValue: $"PlayerNo {playerNo} does not have any recorded logs.");
                    }
                }
                else
                {
                    return new Result<int, string>(errorValue: $"PlayerNo {playerNo} does not exist in VirtualBoardLog.Instance.Logs");
                }
            }
        }
    }
}