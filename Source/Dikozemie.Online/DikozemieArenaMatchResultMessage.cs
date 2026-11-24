using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace Dikozemie.Online
{
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class DikozemieArenaMatchResultMessage : GameNetworkMessage
    {
        public NetworkCommunicator Winner { get; private set; }
        public NetworkCommunicator Loser { get; private set; }
        public int Delta { get; private set; }
        public bool IsDraw => Delta == 0;
        public int WinnerCampaignReward { get; private set; }
        public int LoserCampaignReward { get; private set; }
        public int WinnerRating { get; private set; }
        public int WinnerWins { get; private set; }
        public int WinnerLosses { get; private set; }
        public int LoserRating { get; private set; }
        public int LoserWins { get; private set; }
        public int LoserLosses { get; private set; }
        public int WinnerSeriesBattles { get; private set; }
        public int WinnerSeriesWins { get; private set; }
        public int WinnerSeriesLosses { get; private set; }
        public int WinnerSeriesCoins { get; private set; }
        public bool WinnerSeriesComplete { get; private set; }
        public int LoserSeriesBattles { get; private set; }
        public int LoserSeriesWins { get; private set; }
        public int LoserSeriesLosses { get; private set; }
        public int LoserSeriesCoins { get; private set; }
        public bool LoserSeriesComplete { get; private set; }

        public DikozemieArenaMatchResultMessage()
        {
        }

        internal DikozemieArenaMatchResultMessage(
            NetworkCommunicator winner,
            NetworkCommunicator loser,
            int delta,
            int winnerCampaignReward,
            int loserCampaignReward,
            DikozemieArenaRatingEntry winnerRating,
            DikozemieArenaRatingEntry loserRating,
            int winnerSeriesBattles,
            int winnerSeriesWins,
            int winnerSeriesLosses,
            int winnerSeriesCoins,
            bool winnerSeriesComplete,
            int loserSeriesBattles,
            int loserSeriesWins,
            int loserSeriesLosses,
            int loserSeriesCoins,
            bool loserSeriesComplete)
        {
            Winner = winner;
            Loser = loser;
            Delta = delta;
            WinnerCampaignReward = winnerCampaignReward;
            LoserCampaignReward = loserCampaignReward;
            WinnerRating = winnerRating.Rating;
            WinnerWins = winnerRating.Wins;
            WinnerLosses = winnerRating.Losses;
            LoserRating = loserRating.Rating;
            LoserWins = loserRating.Wins;
            LoserLosses = loserRating.Losses;
            WinnerSeriesBattles = winnerSeriesBattles;
            WinnerSeriesWins = winnerSeriesWins;
            WinnerSeriesLosses = winnerSeriesLosses;
            WinnerSeriesCoins = winnerSeriesCoins;
            WinnerSeriesComplete = winnerSeriesComplete;
            LoserSeriesBattles = loserSeriesBattles;
            LoserSeriesWins = loserSeriesWins;
            LoserSeriesLosses = loserSeriesLosses;
            LoserSeriesCoins = loserSeriesCoins;
            LoserSeriesComplete = loserSeriesComplete;
        }

        protected override void OnWrite()
        {
            WriteNetworkPeerReferenceToPacket(Winner);
            WriteNetworkPeerReferenceToPacket(Loser);
            WriteIntToPacket(Delta, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(WinnerCampaignReward, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(LoserCampaignReward, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(WinnerRating, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(WinnerWins, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(WinnerLosses, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(LoserRating, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(LoserWins, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(LoserLosses, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(WinnerSeriesBattles, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(WinnerSeriesWins, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(WinnerSeriesLosses, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(WinnerSeriesCoins, CompressionMatchmaker.ScoreCompressionInfo);
            WriteBoolToPacket(WinnerSeriesComplete);
            WriteIntToPacket(LoserSeriesBattles, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(LoserSeriesWins, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(LoserSeriesLosses, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(LoserSeriesCoins, CompressionMatchmaker.ScoreCompressionInfo);
            WriteBoolToPacket(LoserSeriesComplete);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            Winner = ReadNetworkPeerReferenceFromPacket(ref valid);
            Loser = ReadNetworkPeerReferenceFromPacket(ref valid);
            Delta = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            WinnerCampaignReward = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            LoserCampaignReward = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            WinnerRating = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            WinnerWins = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            WinnerLosses = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            LoserRating = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            LoserWins = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            LoserLosses = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            WinnerSeriesBattles = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            WinnerSeriesWins = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            WinnerSeriesLosses = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            WinnerSeriesCoins = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            WinnerSeriesComplete = ReadBoolFromPacket(ref valid);
            LoserSeriesBattles = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            LoserSeriesWins = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            LoserSeriesLosses = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            LoserSeriesCoins = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            LoserSeriesComplete = ReadBoolFromPacket(ref valid);
            return valid;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            "Dikozemie arena authoritative match result";
    }
}
