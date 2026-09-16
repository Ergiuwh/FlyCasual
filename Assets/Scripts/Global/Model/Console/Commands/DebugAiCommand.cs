#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CommandsList
{
    public class DebugAICommand : GenericCommand
    {
        public DebugAICommand()
        {
            Keyword = "debugai";
            Description = "Utilities and settings for debuging AIs.\n" 
                        + "debugai showplanning:<(on/true/enable)/(off/false/disable)>\n"
                        + "debugai printplanninglog[:copy]";

            Console.AddAvailableCommand(this);
        }

        public override void Execute(Dictionary<string, string> parameters)
        {
            bool setDebugMovementShowPlanningTo = DebugManager.DebugMovementShowPlanning;
            bool displaySetDebugMovementShowPlanningTo = false;
            bool printPlanning = false;
            bool copyPlanningLog = false;

            if (parameters.TryGetValue("showplanning", out string showPlanningValue))
            {
                switch (showPlanningValue)
                {
                    case "on":
                    case "true":
                    case "enable":
                        setDebugMovementShowPlanningTo = true;
                        displaySetDebugMovementShowPlanningTo = true;
                        break;
                    case "off":
                    case "false":
                    case "disable":
                        setDebugMovementShowPlanningTo = false;
                        displaySetDebugMovementShowPlanningTo = true;
                        break;
                    default:
                        ShowHelp();
                        return;
                }
            }

            if (parameters.TryGetValue("printplanninglog", out string runLogPlanningValue))
            {
                printPlanning = true;
                string[] options = runLogPlanningValue.Split(',');
                foreach (string option in options)
                {
                    switch (option)
                    {
                        case "copy":
                            copyPlanningLog = true;
                            break;
                        case "_": // used to show no input in printout, so we allow it here.
                            break;
                        default:
                            ShowHelp();
                            return;
                    }
                }
            }

            Console.Write("debugai " +
                (displaySetDebugMovementShowPlanningTo ? $"showplanning:{setDebugMovementShowPlanningTo} " : "") +
                (printPlanning ? $"printplanning:{(copyPlanningLog ? "copy" : "_")} " : "")
                );

            if (printPlanning)
            {
                string? formattedPlanningLog = DebugManager.AiPlanningLog.FormatAllStoredRounds();
                if (formattedPlanningLog is not null) {
                    Console.Write(formattedPlanningLog);

                    if (copyPlanningLog)
                    {
                        GUIUtility.systemCopyBuffer = formattedPlanningLog;
                        Console.Write("Formatted planning log coppied to clipboard.");
                    }
                }
                else
                {
                    Console.Write("formattedPlanningLog is null.");
                }
            }

            DebugManager.DebugMovementShowPlanning = setDebugMovementShowPlanningTo;
        }
    }
}