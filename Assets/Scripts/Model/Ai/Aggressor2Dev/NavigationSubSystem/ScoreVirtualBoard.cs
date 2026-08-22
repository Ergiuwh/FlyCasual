#nullable enable

using System;
using System.Collections.Generic;
using AI.Helpers.Navigation;

namespace AI.Aggressor2Dev.Navigation
{
    public class ImprovementSuggestions
    {
        public List<NewVirtualBoard<VirtualBoardData>> possibleSuggestedBoards;

        public ImprovementSuggestions(List<NewVirtualBoard<VirtualBoardData>> virtualBoards)
        {
            possibleSuggestedBoards = virtualBoards;
        }
    }
    public static class ScoreVirtualBoardFns
    {
        public static (double, ImprovementSuggestions) CalculateWithSuggestions(NewVirtualBoard<VirtualBoardData> virtualBoard)
        {
            throw new NotImplementedException();
        }
    }
}