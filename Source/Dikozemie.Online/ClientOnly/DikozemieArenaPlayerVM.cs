using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection;

namespace Dikozemie.Online
{
    public sealed class DikozemieArenaPlayerVM : MPPlayerVM
    {
        private int _roundWins;
        private int _deaths;
        private int _rating;
        private int _ratingWins;
        private int _teamIndex;

        public DikozemieArenaPlayerVM(MissionPeer peer, int teamIndex) : base(peer)
        {
            TeamIndex = teamIndex;
        }

        [DataSourceProperty]
        public int RoundWins
        {
            get => _roundWins;
            set
            {
                if (_roundWins == value) return;
                _roundWins = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public int Deaths
        {
            get => _deaths;
            set
            {
                if (_deaths == value) return;
                _deaths = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public int Rating
        {
            get => _rating;
            set
            {
                if (_rating == value) return;
                _rating = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public int RatingWins
        {
            get => _ratingWins;
            set
            {
                if (_ratingWins == value) return;
                _ratingWins = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public int TeamIndex
        {
            get => _teamIndex;
            set
            {
                if (_teamIndex == value) return;
                _teamIndex = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }
}
