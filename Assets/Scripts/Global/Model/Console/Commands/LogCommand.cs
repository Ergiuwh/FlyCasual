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
                        + "log (src:<source>|source:<source>) [enable/disable/on/off] [stacktrace:<on/off>]\n"
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

            bool hasStackTraceString = parameters.TryGetValue("stacktrace", out string stackTraceString);
            bool? stacktraceValue = null;
            if (hasStackTraceString)
            {
                if (stackTraceString == "on")
                {
                    stacktraceValue = true;
                }
                else if (stackTraceString == "off")
                {
                    stacktraceValue = false;
                }
                else
                {
                    ShowHelp();
                    return;
                }
            }

            switch (sourceString)
            {
                case "virtualboardmanager":
                case "virtualbm":
                case "vbm":
                    SetVirtualBoardManagerLogger(setOnTo, stacktraceValue);
                    break;
                case "selection":
                    SetSelectionLogger(setOnTo, stacktraceValue);
                    break;
                default:
                    ShowHelp();
                    return;
            }
        }
        
        private void SetVirtualBoardManagerLogger(bool? setOnTo, bool? setStackTraceTo)
        {
            if (setOnTo != null)
            {
                VirtualBoardManager.Logger.DoLogging = (bool)setOnTo;
            }
            
            if (setStackTraceTo != null)
            {
                VirtualBoardManager.Logger.ProvideStackTrace = (bool)setStackTraceTo;
            }

            string setOnToString;
            switch (setOnTo)
            {
                case null:
                    setOnToString = "";
                    break;
                case true:
                    setOnToString = "on";
                    break;
                case false:
                    setOnToString = "off";
                    break;
            }

            string setstackTraceToString;
            switch (setStackTraceTo)
            {
                case null:
                    setstackTraceToString = "";
                    break;
                case true:
                    setstackTraceToString = "stacktrace:on";
                    break;
                case false:
                    setstackTraceToString = "stacktrace:off";
                    break;
            }

            Console.Write($"log src:virtualboardmanager {setOnToString}, {setstackTraceToString}");
        }

        private void SetSelectionLogger(bool? setOnTo, bool? setStackTraceTo)
        {
            if (setOnTo != null)
            {
                Selection.Logger.DoLogging = (bool)setOnTo;
            }

            if (setStackTraceTo != null)
            {
                Selection.Logger.ProvideStackTrace = (bool)setStackTraceTo;
            }

            string setOnToString;
            switch (setOnTo)
            {
                case null:
                    setOnToString = "";
                    break;
                case false:
                    setOnToString = "off";
                    break;
                case true:
                    setOnToString = "on";
                    break;
            }

            string setstackTraceToString;
            switch (setStackTraceTo)
            {
                case null:
                    setstackTraceToString = "";
                    break;
                case true:
                    setstackTraceToString = "stacktrace:on";
                    break;
                case false:
                    setstackTraceToString = "stacktrace:off";
                    break;
            }

            Console.Write($"log src:selection {setOnToString} {setstackTraceToString}");
        }
    }
}