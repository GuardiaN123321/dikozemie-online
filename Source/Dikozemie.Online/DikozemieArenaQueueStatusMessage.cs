using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace Dikozemie.Online
{
    // Tells a waiting player how the gathering goes: on 2026-10-31 the customer
    // saw an empty arena for ten seconds and left, never seeing the bots come.
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class DikozemieArenaQueueStatusMessage : GameNetworkMessage
    {
        public int SecondsLeft { get; private set; }
        public int Players { get; private set; }
        public int Capacity { get; private set; }

        public DikozemieArenaQueueStatusMessage()
        {
        }

        internal DikozemieArenaQueueStatusMessage(int secondsLeft, int players, int capacity)
        {
            SecondsLeft = secondsLeft;
            Players = players;
            Capacity = capacity;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(SecondsLeft, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(Players, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(Capacity, CompressionMatchmaker.ScoreCompressionInfo);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            SecondsLeft = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            Players = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            Capacity = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            return valid;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            "Arena queue: " + Players + "/" + Capacity + " in " + SecondsLeft + " s";
    }
}
