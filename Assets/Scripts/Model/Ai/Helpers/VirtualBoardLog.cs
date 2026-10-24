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
        private List<Step> Steps;

        public VirtualBoardLog()
        {
            Steps = new();
        }

        public void AddSingleMoveStep(string name, GenericShip ship, ShipPositionInfo positionInfo)
        {
            AddStep(new StepBuilder(name).SingleMove(ship, positionInfo));
        }

        public void AddUpdateToVirtualBoard<T>(string name, VirtualBoard<T> virtualBoard) where T : class
        {
            AddStep(new StepBuilder(name).UpdateToVirtualBoard(virtualBoard));
        }

        public void AddToggleCollisions(string name, GenericShip ship, bool value)
        {
            AddStep(new StepBuilder(name).ToggleCollisions(ship, value));
        }

        public void AddStep(Step step)
        {
            Steps.Add(step);
        }

        public void AddStep(StepBuilder stepBuilder)
        {
            Steps.Add(stepBuilder.Build());
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
            private static VirtualBoardNoData DisplayVirtualBoard = new("VirtualBoardLog display unset");
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
                    DisplayVirtualBoard = new("VirtualBoardLog.DisplayVirtualBoard");
                    DisplayVirtualBoard.UpdateToReal();
                    DisplayVirtualBoard.Activate();

                    SavedDebugMovementShowPlanning = DebugManager.DebugMovementShowPlanning;
                    DebugManager.DebugMovementShowPlanning = true;

                    Next();
                    return true;
                }
            }

            public static bool Next()
            {
                if (Iter is null)
                {
                    ConsoleHelpers.Error("VirtualBoardLog.DisplayManager", "Next while Iter is null.");
                    return false;
                }

                if (Iter.MoveNext())
                {
                    foreach (Instruction instruction in Iter.Current.Instructions)
                    {
                        if (instruction.PositionInfo.Item1) {
                            DisplayVirtualBoard.GetShipInterface(instruction.Ship).SetPosition(instruction.PositionInfo.Item2);
                        }

                        if (instruction.EnabledCollisions.Item1) {
                            DisplayVirtualBoard.GetShipInterface(instruction.Ship).ToggleCollisions(instruction.EnabledCollisions.Item2);
                        }
                    }

                    Messages.ShowInfoToHuman(Iter.Current.Name, true);

                    return true;
                }
                else
                {
                    End();
                    return false;
                }
            }

            public static void End()
            {
                DebugManager.DebugMovementShowPlanning = SavedDebugMovementShowPlanning;
                Iter?.Dispose();
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

                VirtualBoardLogDisplaySubPhase subPhase = Phases.StartTemporarySubPhaseNew<VirtualBoardLogDisplaySubPhase>("SingleVirtualBoardLogDisplaySubPhase", delegate {  });
                subPhase.Start();

                return true;
            }
        }

        private class VirtualBoardLogDisplaySubPhase : GenericSubPhase
        {
            public VirtualBoardLogDisplaySubPhase()
            {
                
            }

            public override void Start()
            {
                UI.ShowNextButton();
                UI.HighlightNextButton();

                
            }

            public override void NextButton()
            {
                if (DisplayManager.Next()) {
                    VirtualBoardLogDisplaySubPhase nextSubPhase = Phases.StartTemporarySubPhaseNew<VirtualBoardLogDisplaySubPhase>("SingleVirtualBoardLogDisplaySubPhase", delegate {  });
                    nextSubPhase.Start();
                }
                else
                {
                    FinishSubPhase();
                }
            }

            private void FinishSubPhase()
            {
                UI.HideNextButton();
                Phases.FinishSubPhase(typeof(VirtualBoardLogDisplaySubPhase));
            }

            public override List<GameCommandTypes> AllowedGameCommandTypes { get { return new List<GameCommandTypes>() { GameCommandTypes.PressNext }; } }
        }

        
        public static class Instance
        {
            public static Dictionary<PlayerNo, VirtualBoardLog> Logs = new();

            public static VirtualBoardLog CurrentLog { get { return Logs[Phases.CurrentPhasePlayer]; } }

            public static void InitializeLogForPlayer(PlayerNo playerNo)
            {
                Logs.Add(playerNo, new VirtualBoardLog());
            }

            public static Result<int,string> TryStartDisplay(PlayerNo playerNo)
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

            /// <summary>
            /// Reference to the old log still point to the old log.
            /// </summary>
            public static void ClearLogForPlayer(PlayerNo playerNo)
            {
                Logs[playerNo] = new();
            }
        }

        public class StepBuilder
        {
            private List<Instruction> Instructions;
            private string Name;

            public StepBuilder(string name)
            {
                Name = name;
                Instructions = new();
            }

            public StepBuilder SingleMove(GenericShip ship, ShipPositionInfo positionInfo)
            {
                Instructions.Add(new Instruction(ship, positionInfo: positionInfo));

                return this;
            }

            public StepBuilder ToggleCollisions(GenericShip ship, bool value)
            {
                Instructions.Add(new Instruction(ship, enabledCollisions: value));

                return this;
            }

            public StepBuilder UpdateToVirtualBoard<T>(VirtualBoard<T> virtualBoard) where T : class
            {
                foreach (KeyValuePair<GenericShip, VirtualBoard<T>.ShipInfo> shipInfo in virtualBoard.Ships)
                {
                    Instructions.Add(new(shipInfo.Key, positionInfo: shipInfo.Value.VirtualPosition));
                    Instructions.Add(new(shipInfo.Key, enabledCollisions: shipInfo.Value.CollisionsEnabled));
                }

                return this;
            }

            public Step Build()
            {
                Step step = new(Name)
                {
                    Instructions = Instructions
                };

                return step;
            }
        }
    }
}