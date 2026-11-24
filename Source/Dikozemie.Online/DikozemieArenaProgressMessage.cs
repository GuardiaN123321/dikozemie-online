using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace Dikozemie.Online
{
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class DikozemieArenaProgressMessage : GameNetworkMessage
    {
        public int RoundWins { get; private set; }
        public int Deaths { get; private set; }
        public int Rating { get; private set; }
        public int RatingWins { get; private set; }
        public NetworkCommunicator NetworkCommunicator { get; private set; }

        public DikozemieArenaProgressMessage()
        {
        }

        public DikozemieArenaProgressMessage(
            DikozemieArenaRepresentative representative,
            int roundWins,
            int deaths,
            int rating,
            int ratingWins)
        {
            NetworkCommunicator = representative.GetNetworkPeer();
            RoundWins = roundWins;
            Deaths = deaths;
            Rating = rating;
            RatingWins = ratingWins;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(RoundWins, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(Deaths, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(Rating, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(RatingWins, CompressionMatchmaker.ScoreCompressionInfo);
            WriteNetworkPeerReferenceToPacket(NetworkCommunicator);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            RoundWins = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            Deaths = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            Rating = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            RatingWins = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            NetworkCommunicator = ReadNetworkPeerReferenceFromPacket(ref valid);
            return valid;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.GameMode;
        }

        protected override string OnGetLogFormat()
        {
            return "Dikozemie Arena progress update";
        }
    }
}
