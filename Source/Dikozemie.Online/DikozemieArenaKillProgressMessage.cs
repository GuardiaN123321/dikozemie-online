using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace Dikozemie.Online
{
    /// <summary>
    /// Server-authoritative personal result for the current team battle.
    /// Sent after every credited kill so the eliminated player can see the
    /// exact running total while spectating the rest of the fight.
    /// </summary>
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class DikozemieArenaKillProgressMessage : GameNetworkMessage
    {
        public int PersonalKills { get; private set; }
        public int RewardEarned { get; private set; }
        public string VictimName { get; private set; }
        public int VictimRating { get; private set; }
        public int LastReward { get; private set; }
        public int RewardMultiplier { get; private set; }

        public DikozemieArenaKillProgressMessage()
        {
        }

        internal DikozemieArenaKillProgressMessage(
            int personalKills,
            int rewardEarned,
            string victimName,
            int victimRating,
            int lastReward,
            int rewardMultiplier)
        {
            PersonalKills = Math.Max(0, personalKills);
            RewardEarned = Math.Max(0, rewardEarned);
            VictimName = victimName ?? string.Empty;
            VictimRating = Math.Max(0, victimRating);
            LastReward = Math.Max(0, lastReward);
            RewardMultiplier = Math.Max(1, rewardMultiplier);
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(PersonalKills,
                CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(RewardEarned,
                CompressionMatchmaker.ScoreCompressionInfo);
            WriteStringToPacket(VictimName ?? string.Empty);
            WriteIntToPacket(VictimRating,
                CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(LastReward,
                CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(RewardMultiplier,
                CompressionMatchmaker.ScoreCompressionInfo);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            PersonalKills = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            RewardEarned = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            VictimName = ReadStringFromPacket(ref valid);
            VictimRating = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            LastReward = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            RewardMultiplier = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            return valid && PersonalKills >= 0 && RewardEarned >= 0 &&
                (VictimName?.Length ?? 0) <= 128 && VictimRating >= 0 &&
                LastReward >= 0 && RewardMultiplier >= 1 &&
                RewardMultiplier <= 2;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            "Dikozemie arena kill progress";
    }
}
