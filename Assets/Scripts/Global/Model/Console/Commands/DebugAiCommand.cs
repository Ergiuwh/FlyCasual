#nullable enable

using System.Collections.Generic;

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
                        + "where bool_input: (on/true/enable), (off/false/disable)";

            Console.AddAvailableCommand(this);
        }

        public override void Execute(Dictionary<string, string> parameters)
        {
            bool hasSet = parameters.ContainsKey("set");
            bool hasCopyPlanningLog = parameters.ContainsKey("copyplanninglog");
            if (hasSet && hasCopyPlanningLog)
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

                if (numberToGet > DebugManager.AiPlanningLog.RoundsCurrentlyStored)
                {
                    numberToGet = DebugManager.AiPlanningLog.RoundsCurrentlyStored;
                }

                string formattedPlanningLog = DebugManager.AiPlanningLog.FormatLastNRounds(numberToGet);

                Console.Write($"debugai copyplanninglog count:{numberToGet}");

                UnityEngine.GUIUtility.systemCopyBuffer = formattedPlanningLog;

                Console.Write("Planning log coppied to clipboard.");
            }
            else
            {
                ShowHelp();
                return;
            }
        }
    }
}