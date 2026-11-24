using System;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    public sealed class DikozemieArenaRepresentative : MissionRepresentativeBase
    {
        public int RoundWins { get; private set; }
        public int Deaths { get; private set; }
        public int Rating { get; private set; }
        public int RatingWins { get; private set; }

        public event Action<NetworkCommunicator, int, int, int, int> ProgressUpdated;

        public void AddRemoveMessageHandlers(GameNetwork.NetworkMessageHandlerRegisterer.RegisterMode mode)
        {
            if (!GameNetwork.IsClient)
            {
                return;
            }

            var registerer = new GameNetwork.NetworkMessageHandlerRegisterer(mode);
            registerer.Register<DikozemieArenaProgressMessage>(HandleProgressUpdate);
        }

        public void UpdateProgress(int roundWins, int deaths, int rating, int ratingWins)
        {
            RoundWins = Math.Max(0, roundWins);
            Deaths = Math.Max(0, deaths);
            Rating = Math.Max(0, rating);
            RatingWins = Math.Max(0, ratingWins);
        }

        private void HandleProgressUpdate(DikozemieArenaProgressMessage message)
        {
            MissionRepresentativeBase baseRepresentative = message.NetworkCommunicator.GetComponent<MissionRepresentativeBase>();
            if (baseRepresentative is DikozemieArenaRepresentative representative)
            {
                representative.UpdateProgress(message.RoundWins, message.Deaths, message.Rating, message.RatingWins);
                ProgressUpdated?.Invoke(
                    message.NetworkCommunicator,
                    message.RoundWins,
                    message.Deaths,
                    message.Rating,
                    message.RatingWins);
            }
        }
    }
}
