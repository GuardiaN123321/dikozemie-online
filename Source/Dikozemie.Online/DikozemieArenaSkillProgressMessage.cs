using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace Dikozemie.Online
{
    /// <summary>
    /// Server-authoritative raw skill experience earned during one arena duel.
    /// The client owns the durable hero file, but never invents hit experience.
    /// </summary>
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class DikozemieArenaSkillProgressMessage : GameNetworkMessage
    {
        public bool Victory { get; private set; }
        public int HeroExperience { get; private set; }
        public string SkillExperiencePayload { get; private set; }

        public DikozemieArenaSkillProgressMessage()
        {
        }

        internal DikozemieArenaSkillProgressMessage(
            bool victory,
            int heroExperience,
            string skillExperiencePayload)
        {
            Victory = victory;
            HeroExperience = Math.Max(0, heroExperience);
            SkillExperiencePayload = skillExperiencePayload ?? string.Empty;
        }

        protected override void OnWrite()
        {
            WriteBoolToPacket(Victory);
            WriteIntToPacket(HeroExperience, CompressionMatchmaker.ScoreCompressionInfo);
            WriteStringToPacket(SkillExperiencePayload ?? string.Empty);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            Victory = ReadBoolFromPacket(ref valid);
            HeroExperience = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            SkillExperiencePayload = ReadStringFromPacket(ref valid);
            return valid && HeroExperience >= 0 &&
                (SkillExperiencePayload?.Length ?? 0) <= 512;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            "Dikozemie arena skill progress";
    }
}
