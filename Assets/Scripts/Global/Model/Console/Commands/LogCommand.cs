#nullable enable

using System.Collections.Generic;
using AI.Helpers.Navigation;

namespace CommandsList
{
    public class LogCommand : GenericCommand
    {
        public LogCommand()
        {
            Keyword = "log";
            Description = "Manipulate various logging features\n"
                        + "log (src:<sources>|source:<sources>) [enable/disable/on/off] [clear] [print]"
                        + "where source: (virtualboardmanager, virtualbm, vbm)";

            Console.AddAvailableCommand(this);
        }

        public override void Execute(Dictionary<string, string> parameters)
        {
            int setOnParams = 0;
            bool? setOnTo = null;
            foreach (string key in parameters.Keys)
            {
                if (key == "enable" || key == "on")
                {
                    setOnTo = true;
                    setOnParams++;
                }
                else if (key == "disable" || key == "off")
                {
                    setOnTo = false;
                    setOnParams++;
                }
            }

            if (setOnParams > 1)
            {
                ShowHelp();
                return;
            }

            string sourceString;

            bool hasSrcTag = parameters.ContainsKey("src");
            bool hasSourceTag = parameters.ContainsKey("source");

            if (hasSrcTag && hasSourceTag)
            {
                ShowHelp();
                return;
            }
            else if (hasSrcTag) {
                sourceString = parameters["src"];
            }
            else if (hasSourceTag)
            {
                sourceString = parameters["source"];
            }
            else
            {
                ShowHelp();
                return;
            }

            switch (sourceString)
            {
                case "virtualboardmanager":
                case "virtualbm":
                case "vbm":
                    SetVirtualBoardManagerLogger(setOnTo, parameters.ContainsKey("clear"));
                    if (parameters.ContainsKey("print"))
                    {
                        PrintVirtualBoardManagerLogs();
                    }

                    break;
                default:
                    ShowHelp();
                    return;
            }
        }
        
        private void SetVirtualBoardManagerLogger(bool? setOnTo, bool clear)
        {
            if (setOnTo != null) {
                VirtualBoardManager.Logs.DoLogging = (bool)setOnTo;
            }

            if (clear)
            {
                VirtualBoardManager.Logs.Values.Clear();
                VirtualBoardManager.Logs.Values.TrimExcess();
            }

            string setOnToString;
            switch (setOnTo)
            {
                case null:
                    setOnToString = "";
                    break;
                case false:
                    setOnToString = "disable";
                    break;
                case true:
                    setOnToString = "enable";
                    break;
            }

            string clearString = clear ? "clear" : "";

            Console.Write($"log source:virtualboardmanager {setOnToString} {clearString}");
        }
        
        private void PrintVirtualBoardManagerLogs()
        {
            if (VirtualBoardManager.Logs.Values.Count > 0) {
                Console.Write("Virtual Board Manager Logs:",true);
                foreach (var log in VirtualBoardManager.Logs.Values)
                {
                    Console.Write(log.ToString());
                }
            }
        }
    }
}