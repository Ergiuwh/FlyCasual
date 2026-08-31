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
                        + "log (src:<source>|source:<source>) [enable/disable/on/off]\n"
                        + "where source: (virtualboardmanager, virtualbm, vbm), selection";

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
                    SetVirtualBoardManagerLogger(setOnTo);
                    break;
                case "selection":
                    SetSelectionLogger(setOnTo);
                    break;
                default:
                    ShowHelp();
                    return;
            }
        }
        
        private void SetVirtualBoardManagerLogger(bool? setOnTo)
        {
            if (setOnTo != null) {
                VirtualBoardManager.Logger.DoLogging = (bool)setOnTo;
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

            Console.Write($"log source:virtualboardmanager {setOnToString}");
        }

        private void SetSelectionLogger(bool? setOnTo)
        {
            if (setOnTo != null) {
                VirtualBoardManager.Logger.DoLogging = (bool)setOnTo;
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

            Console.Write($"log source:selection {setOnToString}");
        }
    }
}