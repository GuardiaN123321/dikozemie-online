using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace Dikozemie.Online
{
    /// <summary>
    /// Server-authoritative experience snapshot taken when the local hero falls.
    /// It is presentation-only: durable progression is still applied once, by
    /// DikozemieArenaSkillProgressMessage after the battle result is resolved.
    /// </summary>
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class DikozemieArenaDeathSummaryMessage : GameNetworkMessage
    {
        public int HeroExperience { get; private set; }

        public DikozemieArenaDeathSummaryMessage()
        {
        }

        internal DikozemieArenaDeathSummaryMessage(int heroExperience)
        {
            HeroExperience = Math.Max(0, heroExperience);
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(HeroExperience,
                CompressionMatchmaker.ScoreCompressionInfo);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            HeroExperience = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            return valid && HeroExperience >= 0;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            "Dikozemie arena death summary";
    }
}
