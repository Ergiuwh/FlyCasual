#nullable enable

using System;
using System.Collections.Generic;
using AI.Helpers.Navigation;

namespace AI.Aggressor2Dev.Navigation
{
    public class ImprovementSuggestions
    {
        public List<VirtualBoardWrapper<VirtualBoardData>> possibleSuggestedBoards;

        public ImprovementSuggestions(List<VirtualBoardWrapper<VirtualBoardData>> virtualBoards)
        {
            possibleSuggestedBoards = virtualBoards;
        }
    }
    public static class ScoreVirtualBoardFns
    {
        public static (double, ImprovementSuggestions) CalculateWithSuggestions(VirtualBoardWrapper<VirtualBoardData> virtualBoard)
        {
            throw new NotImplementedException();
        }
    }
}