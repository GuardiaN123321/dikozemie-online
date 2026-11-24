using TaleWorlds.Library;

namespace Dikozemie.Online
{
    internal enum DikozemieTournamentPresentationState
    {
        Loading,
        Opening,
        BetweenRounds,
        RoundComplete,
        Fighting,
        Finished
    }

    internal sealed class DikozemieTournamentMatchData
    {
        internal string LeftName = string.Empty;
        internal string LeftRating = string.Empty;
        internal string RightName = string.Empty;
        internal string RightRating = string.Empty;
        internal string LeftResult = string.Empty;
        internal string RightResult = string.Empty;
        internal bool IsCurrent;
        internal bool IsPlayerMatch;
        internal bool IsPlaceholder;

        internal static DikozemieTournamentMatchData CreatePlaceholder()
        {
            return new DikozemieTournamentMatchData
            {
                LeftName = "ОЖИДАЕТСЯ",
                RightName = "ОЖИДАЕТСЯ",
                IsPlaceholder = true
            };
        }
    }

    internal sealed class DikozemieTournamentMatchVM : ViewModel
    {
        private string _leftName;
        private string _leftRating;
        private string _rightName;
        private string _rightRating;
        private string _leftResult;
        private string _rightResult;
        private bool _isCurrent;
        private bool _isPlayerMatch;
        private bool _isPlaceholder;

        internal DikozemieTournamentMatchVM(
            DikozemieTournamentMatchData data)
        {
            LeftName = data.LeftName;
            LeftRating = data.LeftRating;
            RightName = data.RightName;
            RightRating = data.RightRating;
            LeftResult = data.LeftResult;
            RightResult = data.RightResult;
            IsCurrent = data.IsCurrent;
            IsPlayerMatch = data.IsPlayerMatch;
            IsPlaceholder = data.IsPlaceholder;
        }

        [DataSourceProperty]
        public string LeftName
        {
            get => _leftName;
            set
            {
                if (_leftName == value) return;
                _leftName = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string LeftRating
        {
            get => _leftRating;
            set
            {
                if (_leftRating == value) return;
                _leftRating = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string RightName
        {
            get => _rightName;
            set
            {
                if (_rightName == value) return;
                _rightName = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string RightRating
        {
            get => _rightRating;
            set
            {
                if (_rightRating == value) return;
                _rightRating = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string LeftResult
        {
            get => _leftResult;
            set
            {
                if (_leftResult == value) return;
                _leftResult = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string RightResult
        {
            get => _rightResult;
            set
            {
                if (_rightResult == value) return;
                _rightResult = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsCurrent
        {
            get => _isCurrent;
            set
            {
                if (_isCurrent == value) return;
                _isCurrent = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsPlayerMatch
        {
            get => _isPlayerMatch;
            set
            {
                if (_isPlayerMatch == value) return;
                _isPlayerMatch = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsPlaceholder
        {
            get => _isPlaceholder;
            set
            {
                if (_isPlaceholder == value) return;
                _isPlaceholder = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }
}
