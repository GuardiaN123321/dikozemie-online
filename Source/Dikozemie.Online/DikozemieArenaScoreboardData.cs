using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    public sealed class DikozemieArenaScoreboardData : IScoreboardData
    {
        public MissionScoreboardComponent.ScoreboardHeader[] GetScoreboardHeaders()
        {
            return new[]
            {
                new MissionScoreboardComponent.ScoreboardHeader("avatar", peer => string.Empty, bot => string.Empty),
                new MissionScoreboardComponent.ScoreboardHeader(
                    "name",
                    peer => peer.DisplayedName,
                    bot => new TextObject("{=hvQSOi79}Bot").ToString()),
                new MissionScoreboardComponent.ScoreboardHeader(
                    "round_wins",
                    peer => GetRepresentative(peer)?.RoundWins.ToString() ?? "0",
                    bot => "0"),
                new MissionScoreboardComponent.ScoreboardHeader(
                    "rating",
                    peer => GetRepresentative(peer)?.Rating.ToString() ?? "1000",
                    bot => "0"),
                new MissionScoreboardComponent.ScoreboardHeader(
                    "rating_wins",
                    peer => GetRepresentative(peer)?.RatingWins.ToString() ?? "0",
                    bot => "0")
            };
        }

        private static DikozemieArenaRepresentative GetRepresentative(MissionPeer peer)
        {
            return peer?.GetNetworkPeer()?.GetComponent<DikozemieArenaRepresentative>();
        }
    }
}
