#nullable enable

using System.Collections.Generic;
using AI.Helpers;
using AI.Helpers.Types;
using AI.Helpers.VirtualBoard;
using AI.Helpers.VirtualBoard.Debug;
using Players;

namespace CommandsList
{
    public class DebugAICommand : GenericCommand
    {
        public DebugAICommand()
        {
            Keyword = "debugai";
            Description = "Utilities and settings for debuging AIs.\n" 
                        + "debugai set movementshowplanning:<bool_input>\n"
                        + "debugai copyplanninglog [count:<number>]\n"
                        + "debugai showvirtualboardlog [player:<1/2>]\n"
                        + "debugai showvirtualboardstack\n"
                        + "where bool_input: (on/true/enable), (off/false/disable)";

            Console.AddAvailableCommand(this);
        }

        public override void Execute(Dictionary<string, string> parameters)
        {
            bool hasSet = parameters.ContainsKey("set");
            bool hasCopyPlanningLog = parameters.ContainsKey("copyplanninglog");
            bool hasShowVirtualBoardLog = parameters.ContainsKey("showvirtualboardlog");
            bool hasShowVirtualBoardStack = parameters.ContainsKey("showvirtualboardstack");
            int numberOfParams = (hasSet?1:0) + (hasCopyPlanningLog?1:0) + (hasShowVirtualBoardLog?1:0) + (hasShowVirtualBoardStack?1:0);
            if (numberOfParams != 1)
            {
                ShowHelp();
                return;
            }
            else if (hasSet)
            {
                if (parameters.TryGetValue("movementshowplanning", out string movementShowPlanning))
                {
                    switch (movementShowPlanning)
                    {
                        case "on":
                        case "true":
                        case "enable":
                            DebugManager.DebugMovementShowPlanning = true;
                            Console.Write("debugai set movementshowplanning:true");
                            return;
                        case "off":
                        case "false":
                        case "disable":
                            DebugManager.DebugMovementShowPlanning = false;
                            Console.Write("debugai set movementshowplanning:false");
                            return;
                        default:
                            ShowHelp();
                            return;
                    }
                }

                ShowHelp();
                return;
            }
            else if (hasCopyPlanningLog)
            {
                int numberToGet = int.MaxValue;

                if (parameters.TryGetValue("count", out string count))
                {
                    if (int.TryParse(count, out int countInt))
                    {
                        numberToGet = countInt;
                    }
                }

                if (numberToGet > AiPlanningLog.RoundsCurrentlyStored)
                {
                    numberToGet = AiPlanningLog.RoundsCurrentlyStored;
                }

                string formattedPlanningLog = AiPlanningLog.FormatLastNRounds(numberToGet);

                Console.Write($"debugai copyplanninglog count:{numberToGet}");

                UnityEngine.GUIUtility.systemCopyBuffer = formattedPlanningLog;

                Console.Write("Planning log coppied to clipboard.");
            }
            else if (hasShowVirtualBoardLog)
            {
                PlayerNo playerNo;
                if (parameters.TryGetValue("player", out string player))
                {
                    switch (player)
                    {
                        case "1":
                            playerNo = PlayerNo.Player1;
                            break;
                        case "2":
                            playerNo = PlayerNo.Player2;
                            break;
                        default:
                            ShowHelp();
                            return;
                    }
                }
                else
                {
                    playerNo = PlayerNo.Player2;
                }

                Result<int, string> result = VirtualBoardLog.Instance.TryStartDisplay(playerNo);
                
                if (result.TryGetError(out string? error))
                {
                    Console.Write(error);
                    ShowHelp();
                }
            }
            else if (hasShowVirtualBoardStack)
            {
                Console.Write($"debugai showvirtualboardstack");

                foreach (VirtualBoardManager.AbstractVirtualBoard item in VirtualBoardManager.DebugUtils.GetVirtualBoardStack())
                {
                    Console.Write(item.ToString());
                }
            }
        }
    }
}