using Players;

namespace ExtraOptions
{
    namespace ExtraOptionsList
    {
        public class ImprovedAiExtraOption : ExtraOption
        {
            public ImprovedAiExtraOption()
            {
                Name = "Improved Ai";
                Description = "AI uses improved dial-planning system. AI can boost and barrel roll. AI planning and action decisions take longer.";
            }

            protected override void Activate()
            {
                SquadBuilderNS.SquadBuilder.ai_player_1 = typeof(AggressorAiPlayer);
                SquadBuilderNS.SquadBuilder.ai_player_2 = typeof(AggressorAiPlayer);
            }

            protected override void Deactivate()
            {
                SquadBuilderNS.SquadBuilder.ai_player_1 = typeof(AggressorAiPlayerQuick);
                SquadBuilderNS.SquadBuilder.ai_player_2 = typeof(AggressorAiPlayerQuick);
            }
        }
    }
}