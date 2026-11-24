using System;

namespace Dikozemie.Online
{
    // The singleplayer battlefields of each land (SandBoxCore battle_terrain
    // scenes, named in CustomBattle/ModuleData/custom_battle_scenes.xml after
    // places of Calradia). A road fight is fought on a field of the land the
    // hero walks through, in a wood when he walks through a forest.
    internal static class DikozemieRoadScenes
    {
        private static readonly string[] EmpireOpen =
        {
            "battle_terrain_O", "battle_terrain_m", "battle_terrain_i", "battle_terrain_s",
            "battle_terrain_L", "battle_terrain_029", "battle_terrain_027", "battle_terrain_032",
            "battle_terrain_026", "battle_terrain_biome_012", "battle_terrain_biome_130",
            "battle_terrain_biome_131", "battle_terrain_biome_098", "battle_terrain_biome_096"
        };

        private static readonly string[] EmpireForest =
        {
            "battle_terrain_h", "battle_terrain_k", "battle_terrain_v", "battle_terrain_003",
            "battle_terrain_023", "battle_terrain_t", "battle_terrain_biome_106", "battle_terrain_biome_097"
        };

        private static readonly string[] VlandiaOpen =
        {
            "battle_terrain_020", "battle_terrain_021", "battle_terrain_030", "battle_terrain_z",
            "battle_terrain_biome_005", "battle_terrain_biome_025", "battle_terrain_biome_028"
        };

        private static readonly string[] VlandiaForest =
        {
            "battle_terrain_p", "battle_terrain_u", "battle_terrain_biome_006"
        };

        private static readonly string[] SturgiaOpen =
        {
            "battle_terrain_001", "battle_terrain_004", "battle_terrain_011", "battle_terrain_031",
            "battle_terrain_biome_030", "battle_terrain_biome_044", "battle_terrain_biome_047",
            "battle_terrain_biome_050"
        };

        private static readonly string[] SturgiaForest =
        {
            "battle_terrain_J", "battle_terrain_a", "battle_terrain_biome_046"
        };

        private static readonly string[] BattaniaOpen =
        {
            "battle_terrain_biome_027", "battle_terrain_biome_149", "battle_terrain_033"
        };

        private static readonly string[] BattaniaForest =
        {
            "battle_terrain_025", "battle_terrain_024", "battle_terrain_r", "battle_terrain_028",
            "battle_terrain_n"
        };

        private static readonly string[] Steppe =
        {
            "battle_terrain_biome_088", "battle_terrain_biome_089", "battle_terrain_biome_148",
            "battle_terrain_y", "battle_terrain_006", "battle_terrain_012", "battle_terrain_013",
            "battle_terrain_014", "battle_terrain_018", "battle_terrain_007", "battle_terrain_015",
            "battle_terrain_017", "battle_terrain_019"
        };

        private static readonly string[] Desert =
        {
            "battle_terrain_biome_083", "battle_terrain_biome_076", "battle_terrain_biome_075",
            "battle_terrain_biome_065", "battle_terrain_biome_067", "battle_terrain_010",
            "battle_terrain_b", "battle_terrain_d", "battle_terrain_e", "battle_terrain_008",
            "battle_terrain_009", "battle_terrain_022"
        };

        internal static string Pick(string culture, bool forest, int seed)
        {
            string[] choices = Choices(DikozemieRoadFolk.FolkCulture(culture), forest);
            return choices[(seed & int.MaxValue) % choices.Length];
        }

        internal static string[] Choices(string culture, bool forest)
        {
            switch (culture)
            {
                case "vlandia": return forest ? VlandiaForest : VlandiaOpen;
                case "sturgia": return forest ? SturgiaForest : SturgiaOpen;
                case "battania": return forest ? BattaniaForest : BattaniaOpen;
                case "khuzait": return Steppe;
                case "aserai": return Desert;
                default: return forest ? EmpireForest : EmpireOpen;
            }
        }

        // Sturgia under snow, the rest in summer.
        internal static string Season(string culture) =>
            DikozemieRoadFolk.FolkCulture(culture) == "sturgia" ? "winter" : "summer";

        // The atmospheres the custom battles know: 6, 12, 15, 18 and 22 hours.
        internal static int AtmosphereHour(float hourOfDay)
        {
            int hour = (int)Math.Floor(hourOfDay) % 24;
            if (hour >= 21 || hour < 5) return 22;
            if (hour < 9) return 6;
            if (hour < 14) return 12;
            if (hour < 17) return 15;
            return 18;
        }
    }
}
