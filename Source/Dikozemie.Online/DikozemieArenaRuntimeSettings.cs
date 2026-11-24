using System;

namespace Dikozemie.Online
{
    internal enum DikozemieArenaBotDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    internal enum DikozemieArenaDiscipline
    {
        Foot = 0,
        Mounted = 1,
        Open = 2
    }

    internal enum DikozemieArenaComposition
    {
        Mixed = 0,
        Melee = 1,
        Ranged = 2
    }

    internal static class DikozemieArenaCompositionRules
    {
        internal static bool IsSupported(
            DikozemieArenaComposition composition,
            DikozemieArenaDiscipline discipline)
        {
            // Composition and movement discipline are independent axes.  In
            // Mounted/Ranged the normal discipline pass supplies horses, so
            // the result is horse archers; Mounted/Melee is melee cavalry.
            return composition == DikozemieArenaComposition.Mixed ||
                composition == DikozemieArenaComposition.Melee ||
                composition == DikozemieArenaComposition.Ranged;
        }
    }

    internal static class DikozemieArenaRuntimeSettings
    {
        private static readonly object Gate = new object();
        // An arena entry is one duel, not a best-of-three series.  The
        // dedicated server never runs the client-side Configure path, so its
        // default must express that rule as well.
        private static int _winsRequired = 1;
        private static int _opponentWaitSeconds = 5;
        private static DikozemieArenaBotDifficulty _botDifficulty =
            DikozemieArenaBotDifficulty.Normal;

        internal static int WinsRequired
        {
            get
            {
                lock (Gate)
                {
                    return _winsRequired;
                }
            }
        }

        internal static TimeSpan OpponentWait
        {
            get
            {
                lock (Gate)
                {
                    return TimeSpan.FromSeconds(_opponentWaitSeconds);
                }
            }
        }

        internal static DikozemieArenaBotDifficulty BotDifficulty
        {
            get
            {
                lock (Gate)
                {
                    return _botDifficulty;
                }
            }
        }

        internal static float BotHealthMultiplier
        {
            get
            {
                switch (BotDifficulty)
                {
                    case DikozemieArenaBotDifficulty.Easy:
                        return 0.75f;
                    case DikozemieArenaBotDifficulty.Hard:
                        return 1.35f;
                    default:
                        return 1.0f;
                }
            }
        }

        internal static void Configure(
            int winsRequired,
            int opponentWaitSeconds,
            DikozemieArenaBotDifficulty botDifficulty)
        {
            lock (Gate)
            {
                _winsRequired = winsRequired == 1 || winsRequired == 5
                    ? winsRequired
                    : 3;
                _opponentWaitSeconds = Math.Max(3, Math.Min(30, opponentWaitSeconds));
                _botDifficulty = botDifficulty;
            }
        }
    }
}
