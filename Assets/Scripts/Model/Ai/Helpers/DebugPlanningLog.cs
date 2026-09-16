#nullable enable

using System.Collections.Generic;

public static partial class DebugManager
{
    public static class AiPlanningLog {
        private class RecursiveStringList {
            public class RecursiveStringListNode
            {
                public string Name { get; private set; }
                public List<RecursiveStringListNode>? Children;

                public RecursiveStringListNode(string name, bool allowChildren)
                {
                    Name = name;
                    if (allowChildren) {
                        Children = new();
                    }
                    else
                    {
                        Children = null;
                    }
                }

                public void AddString(string value)
                {
                    if (Children == null)
                    {
                        return;
                    }

                    RecursiveStringListNode stringNode = new(value, false);
                    Children.Add(stringNode);
                }

                public void CreateChildNode(string name)
                {
                    if (Children == null)
                    {
                        return;
                    }

                    RecursiveStringListNode toAdd = new(name, true);
                    Children.Add(toAdd);
                }
            }

            public RecursiveStringListNode HeadNode { get; private set; }
            private List<RecursiveStringListNode> activePath;

            public RecursiveStringList(string HeadNodeName)
            {
                HeadNode = new RecursiveStringListNode(HeadNodeName, true);
                activePath = new();
            }

            private RecursiveStringListNode GetActiveNode()
            {
                if (activePath.Count == 0)
                {
                    return HeadNode;
                }
                else
                {
                    return activePath[^1];
                }
            }

            public void AddString(string value)
            {
                GetActiveNode().AddString(value);
            }

            public void OpenGroup(string name)
            {
                RecursiveStringListNode activeNode = GetActiveNode();
                if (activeNode.Children is null)
                {
                    // This should never happen.
                    Console.Write("DebugManager.AiPlanningLog.RecursiveStringList.OpenGroup: an error occurred.", true, "red");
                    return;
                }

                activeNode.CreateChildNode(name);
                activePath.Add(activeNode.Children[^1]);
            }

            public void CloseGroup()
            {
                if (activePath.Count == 0)
                {
                    Console.Write("DebugManager.AiPlanningLog.RecursiveStringList.CloseGroup: attempt to close group when none are open.", false, "red");
                    return;
                }

                activePath.RemoveAt(activePath.Count - 1);
            }
        }
        private static List<RecursiveStringList> Log { get; set; } = new();
        private static int roundsStoredCount = 2;
        public static int RoundsStoredCount
        {
            get
            {
                return roundsStoredCount;
            }
            set { roundsStoredCount = value; }
        }

        public static bool DoLogging
        {
            get
            {
                return roundsStoredCount != 0;
            }
        }

        private static void StartNewRound(string title)
        {
            if (RoundsStoredCount == 0)
            {
                return;
            }

            if (Log.Count == RoundsStoredCount)
            {
                Log.RemoveAt(0);
            }

            Log.Add(new RecursiveStringList(title));
        }

        public static void OpenNewGroup(string name)
        {
            if (RoundsStoredCount == 0)
            {
                return;
            }

            Log[^1].OpenGroup(name);
        }
        

        public static void Add(string value)
        {
            if (RoundsStoredCount == 0)
            {
                return;
            }

            Log[^1].AddString(value);
        }

        public static void CloseGroup()
        {
            if (RoundsStoredCount == 0)
            {
                return;
            }

            Log[^1].CloseGroup();
        }

        private static string FormatGroup(RecursiveStringList.RecursiveStringListNode group, int depth)
        {
            const int MAX_RECURSION = 10;

            if (depth > MAX_RECURSION)
            {
                return "";
            }

            if (group.Children == null)
            {
                return group.Name;
            }

            string result = "";
            for (int i = 0; i <= depth; i++)
            {
                result += "#";
            }

            result += $" {group.Name}\n";

            for (int i = 0; i < group.Children.Count; i++)
            {
                result += FormatGroup(group.Children[i], depth + 1);
                if (i != group.Children.Count - 1)
                {
                    result += "\\";
                }

                result += "\n";
            }

            return result;
        }


        public static string FormatAllStoredRounds()
        {
            string result = "";
            foreach (RecursiveStringList item in Log)
            {
                result += FormatGroup(item.HeadNode, 0);
            }

            return result;
        }

        public static void Initialize()
        {
            Log = new();
            StartNewRound("setup");
            Phases.Events.OnRoundStart += delegate { AiPlanningLog.StartNewRound($"Round {Phases.RoundCounter}"); };
        }
    }
}