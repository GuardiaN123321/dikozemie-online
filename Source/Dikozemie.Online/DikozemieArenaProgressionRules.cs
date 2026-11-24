namespace Dikozemie.Online
{
    internal static class DikozemieArenaProgressionRules
    {
        // Classes are ordered in the same order as the native armory:
        // infantry, ranged and cavalry. A fresh fighter receives only the
        // first (topmost) class. The remaining classes open through ranked
        // arena victories.
        private static readonly int[] RequiredWins =
        {
            0,
            1,
            3,
            6,
            10,
            15,
            25
        };

        internal static int GetUnlockedClassCount(int arenaWins)
        {
            int wins = arenaWins < 0 ? 0 : arenaWins;
            int unlocked = 0;
            for (int index = 0; index < RequiredWins.Length; index++)
            {
                if (wins < RequiredWins[index])
                {
                    break;
                }
                unlocked++;
            }
            return unlocked < 1 ? 1 : unlocked;
        }

        internal static bool IsClassUnlocked(int classIndex, int arenaWins)
        {
            return classIndex >= 0 && classIndex < GetUnlockedClassCount(arenaWins);
        }
    }
}
