using System;

namespace Dikozemie.Online
{
    internal static class DikozemieNavalRewardRules
    {
        internal const int PersonalKillReward = 2;
        internal const int LightLossCoinPenalty = 10;
        internal const int MediumLossCoinPenalty = 20;
        internal const int HeavyLossCoinPenalty = 30;

        internal static int CalculatePersonalKillReward(int personalKills)
        {
            return Math.Max(0, personalKills) * PersonalKillReward;
        }

        internal static int GetLossCoinPenalty(
            DikozemieNavalShipClass shipClass)
        {
            switch (shipClass)
            {
                case DikozemieNavalShipClass.Heavy:
                    return HeavyLossCoinPenalty;
                case DikozemieNavalShipClass.Medium:
                    return MediumLossCoinPenalty;
                default:
                    return LightLossCoinPenalty;
            }
        }

        // Customer 2026-11-01: the losers' coins go to the winners. Crews are
        // equal, so each winner gets what each loser pays.
        internal static int GetVictoryCoinReward(
            DikozemieNavalShipClass shipClass)
        {
            return GetLossCoinPenalty(shipClass);
        }

        internal static int CalculateLossCoinPenalty(
            DikozemieNavalShipClass shipClass,
            int currentCoins)
        {
            return Math.Min(
                GetLossCoinPenalty(shipClass),
                Math.Max(0, currentCoins));
        }
    }
}
