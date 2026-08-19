#nullable enable

using System;
using AI.Helpers.AttackCalculations;

namespace AI.Aggressor2Dev.Navigation
{
    public class VirtualBoardData : ICloneable
    {
        public DiscreteProbabilityDistribution? PendingIncomingDamage;

        public VirtualBoardData()
        {
            PendingIncomingDamage = null;
        }

        public object Clone()
        {
            VirtualBoardData newData = new()
            {
                PendingIncomingDamage = PendingIncomingDamage
            };
            return newData;
        }
    }
}