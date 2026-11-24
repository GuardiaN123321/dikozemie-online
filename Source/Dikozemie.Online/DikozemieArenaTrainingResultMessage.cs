using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace Dikozemie.Online
{
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class DikozemieArenaTrainingResultMessage : GameNetworkMessage
    {
        public bool Victory { get; private set; }
        public int PlayerScore { get; private set; }
        public int BotScore { get; private set; }
        // Normal completed 1v1 has a nonzero winner's score; 0:0 is a draw.
        public bool IsDraw => !Victory && PlayerScore == 0 && BotScore == 0;
        public string OpponentCharacterId { get; private set; }
        public int OpponentRating { get; private set; }
        public int OpponentWins { get; private set; }
        public int OpponentLosses { get; private set; }
        public int PlayerRating { get; private set; }
        public int PlayerWins { get; private set; }
        public int PlayerLosses { get; private set; }
        public int SeriesBattles { get; private set; }
        public int SeriesWins { get; private set; }
        public int SeriesLosses { get; private set; }
        public int CoinsEarned { get; private set; }
        public int RatingEarned { get; private set; }

        public DikozemieArenaTrainingResultMessage()
        {
        }

        internal DikozemieArenaTrainingResultMessage(
            bool victory,
            int playerScore,
            int botScore,
            string opponentCharacterId,
            DikozemieArenaRatingEntry player,
            DikozemieArenaRatingEntry opponent,
            int seriesBattles,
            int seriesWins,
            int seriesLosses,
            int coinsEarned,
            int ratingEarned)
        {
            Victory = victory;
            PlayerScore = playerScore;
            BotScore = botScore;
            OpponentCharacterId = opponentCharacterId ?? string.Empty;
            OpponentRating = opponent?.Rating ?? 0;
            OpponentWins = opponent?.Wins ?? 0;
            OpponentLosses = opponent?.Losses ?? 0;
            PlayerRating = player?.Rating ?? 0;
            PlayerWins = player?.Wins ?? 0;
            PlayerLosses = player?.Losses ?? 0;
            SeriesBattles = seriesBattles;
            SeriesWins = seriesWins;
            SeriesLosses = seriesLosses;
            CoinsEarned = coinsEarned;
            RatingEarned = ratingEarned;
        }

        protected override void OnWrite()
        {
            WriteBoolToPacket(Victory);
            WriteIntToPacket(PlayerScore, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(BotScore, CompressionMatchmaker.ScoreCompressionInfo);
            WriteStringToPacket(OpponentCharacterId ?? string.Empty);
            WriteIntToPacket(OpponentRating, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(OpponentWins, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(OpponentLosses, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(PlayerRating, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(PlayerWins, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(PlayerLosses, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(SeriesBattles, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(SeriesWins, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(SeriesLosses, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(CoinsEarned, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(RatingEarned, CompressionMatchmaker.ScoreCompressionInfo);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            Victory = ReadBoolFromPacket(ref valid);
            PlayerScore = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            BotScore = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            OpponentCharacterId = ReadStringFromPacket(ref valid);
            OpponentRating = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            OpponentWins = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            OpponentLosses = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            PlayerRating = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            PlayerWins = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            PlayerLosses = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            SeriesBattles = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            SeriesWins = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            SeriesLosses = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            CoinsEarned = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            RatingEarned = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            return valid;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            "Dikozemie arena training result";
    }
}
