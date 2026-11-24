using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace Dikozemie.Online
{
    // Also carries «Начать с ботом» (customer 2026-11-05: a player waiting for
    // a live opponent must be able to stop waiting). That request is this
    // message with TeamSize 0. A new message type would renumber every
    // network message (the engine sorts them by name, ours before Native)
    // and lock all older installs out of the servers.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class DikozemieArenaDuelRequestMessage : GameNetworkMessage
    {
        internal const int StartNowTeamSize = 0;

        public int TeamSize { get; private set; } = 1;

        internal bool IsStartNowRequest => TeamSize == StartNowTeamSize;

        internal static DikozemieArenaDuelRequestMessage StartNow() =>
            new DikozemieArenaDuelRequestMessage(
                StartNowTeamSize,
                DikozemieArenaDiscipline.Open,
                DikozemieArenaComposition.Mixed);

        public int Discipline { get; private set; } =
            (int)DikozemieArenaDiscipline.Open;

        public int Composition { get; private set; } =
            (int)DikozemieArenaComposition.Mixed;

        public DikozemieArenaDuelRequestMessage()
        {
        }

        internal DikozemieArenaDuelRequestMessage(
            int teamSize,
            DikozemieArenaDiscipline discipline,
            DikozemieArenaComposition composition)
        {
            TeamSize = teamSize;
            Discipline = (int)discipline;
            Composition = (int)composition;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(TeamSize,
                CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(Discipline,
                CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(Composition,
                CompressionMatchmaker.ScoreCompressionInfo);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            TeamSize = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            Discipline = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            Composition = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            return valid && TeamSize >= StartNowTeamSize && TeamSize <= 100 &&
                Discipline >= 0 && Discipline <= 2 &&
                Composition >= 0 && Composition <= 2;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            IsStartNowRequest
                ? "Dikozemie arena start now with bots"
                : "Dikozemie arena duel request " + TeamSize + "x" + TeamSize +
                  " discipline=" + Discipline + " composition=" + Composition;
    }
}
