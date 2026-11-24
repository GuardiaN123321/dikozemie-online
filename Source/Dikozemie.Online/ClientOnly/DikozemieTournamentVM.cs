using System;
using TaleWorlds.Library;

namespace Dikozemie.Online
{
    internal sealed class DikozemieTournamentVM : ViewModel
    {
        private readonly DikozemieLocalArenaTournamentController _controller;
        private bool _isVisible;
        private bool _isBetWindowVisible;
        private bool _canOpenBet;
        private string _title;
        private string _subtitle;
        private string _statusText;
        private string _stageText;
        private string _primaryActionText;
        private string _balanceText;
        private string _betSummaryText;
        private string _oddsText;
        private string _wagerText;
        private string _expectedPayoutText;
        private MBBindingList<DikozemieTournamentMatchVM> _roundOneMatches;
        private MBBindingList<DikozemieTournamentMatchVM> _roundTwoMatches;
        private MBBindingList<DikozemieTournamentMatchVM> _roundThreeMatches;
        private MBBindingList<DikozemieTournamentMatchVM> _roundFourMatches;

        internal DikozemieTournamentVM(
            DikozemieLocalArenaTournamentController controller)
        {
            _controller = controller;
            RoundOneMatches = new MBBindingList<DikozemieTournamentMatchVM>();
            RoundTwoMatches = new MBBindingList<DikozemieTournamentMatchVM>();
            RoundThreeMatches = new MBBindingList<DikozemieTournamentMatchVM>();
            RoundFourMatches = new MBBindingList<DikozemieTournamentMatchVM>();
            if (_controller != null)
            {
                _controller.TournamentPresentationChanged += Refresh;
            }
            Refresh();
        }

        internal event Action<bool> VisibilityChanged;

        public override void OnFinalize()
        {
            if (_controller != null)
            {
                _controller.TournamentPresentationChanged -= Refresh;
            }
            base.OnFinalize();
        }

        internal void Refresh()
        {
            if (_controller == null)
            {
                IsVisible = false;
                return;
            }

            Title = _controller.TournamentTitle;
            Subtitle = _controller.TournamentSubtitle;
            StatusText = _controller.PresentationMessage;
            StageText = _controller.CurrentStageName;
            PrimaryActionText = _controller.PrimaryActionText;
            BalanceText = "БАЛАНС  " + _controller.ResourceBalance + " МОНЕТ";
            CanOpenBet = _controller.CanPlaceWager &&
                _controller.MaximumWager > 0;
            BetSummaryText = _controller.PendingWager > 0
                ? "СТАВКА НА ПОБЕДУ В ТУРНИРЕ: " +
                  _controller.PendingWager
                : "СТАВКА НЕ ВЫБРАНА";
            OddsText = "КОЭФФИЦИЕНТ  ×" +
                _controller.CurrentWagerMultiplier.ToString("0.00");
            WagerText = _controller.PendingWager.ToString();
            ExpectedPayoutText = "ВЫПЛАТА ПРИ ПОБЕДЕ  " +
                _controller.ExpectedWagerPayout;

            ReplaceMatches(RoundOneMatches, 0);
            ReplaceMatches(RoundTwoMatches, 1);
            ReplaceMatches(RoundThreeMatches, 2);
            ReplaceMatches(RoundFourMatches, 3);
            if (!_controller.CanPlaceWager)
            {
                IsBetWindowVisible = false;
            }
            IsVisible = _controller.IsPresentationVisible;
        }

        public void ExecutePrimary()
        {
            IsBetWindowVisible = false;
            _controller?.ExecutePrimaryPresentationAction();
        }

        public void ExecuteSecondary()
        {
            IsBetWindowVisible = false;
            _controller?.ExecuteExitPresentationAction();
        }

        public void ExecuteOpenBet()
        {
            if (CanOpenBet)
            {
                IsBetWindowVisible = true;
            }
        }

        public void ExecuteDecreaseWager()
        {
            if (_controller != null)
            {
                _controller.TrySetPendingWager(
                    Math.Max(0, _controller.PendingWager - 1));
            }
        }

        public void ExecuteIncreaseWager()
        {
            if (_controller != null)
            {
                _controller.TrySetPendingWager(
                    Math.Min(
                        _controller.MaximumWager,
                        _controller.PendingWager + 1));
            }
        }

        public void ExecuteConfirmBet()
        {
            IsBetWindowVisible = false;
        }

        public void ExecuteCancelBet()
        {
            _controller?.TrySetPendingWager(0);
            IsBetWindowVisible = false;
        }

        private void ReplaceMatches(
            MBBindingList<DikozemieTournamentMatchVM> destination,
            int roundIndex)
        {
            destination.Clear();
            foreach (DikozemieTournamentMatchData match in
                _controller.BuildRoundMatches(roundIndex))
            {
                destination.Add(new DikozemieTournamentMatchVM(match));
            }
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible == value) return;
                _isVisible = value;
                OnPropertyChangedWithValue(value);
                VisibilityChanged?.Invoke(value);
            }
        }

        [DataSourceProperty]
        public bool IsBetWindowVisible
        {
            get => _isBetWindowVisible;
            set
            {
                if (_isBetWindowVisible == value) return;
                _isBetWindowVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool CanOpenBet
        {
            get => _canOpenBet;
            set
            {
                if (_canOpenBet == value) return;
                _canOpenBet = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set
            {
                if (_title == value) return;
                _title = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string Subtitle
        {
            get => _subtitle;
            set
            {
                if (_subtitle == value) return;
                _subtitle = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText == value) return;
                _statusText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string StageText
        {
            get => _stageText;
            set
            {
                if (_stageText == value) return;
                _stageText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string PrimaryActionText
        {
            get => _primaryActionText;
            set
            {
                if (_primaryActionText == value) return;
                _primaryActionText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string BalanceText
        {
            get => _balanceText;
            set
            {
                if (_balanceText == value) return;
                _balanceText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string BetSummaryText
        {
            get => _betSummaryText;
            set
            {
                if (_betSummaryText == value) return;
                _betSummaryText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string OddsText
        {
            get => _oddsText;
            set
            {
                if (_oddsText == value) return;
                _oddsText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string WagerText
        {
            get => _wagerText;
            set
            {
                if (_wagerText == value) return;
                _wagerText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ExpectedPayoutText
        {
            get => _expectedPayoutText;
            set
            {
                if (_expectedPayoutText == value) return;
                _expectedPayoutText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public MBBindingList<DikozemieTournamentMatchVM> RoundOneMatches
        {
            get => _roundOneMatches;
            set
            {
                if (_roundOneMatches == value) return;
                _roundOneMatches = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public MBBindingList<DikozemieTournamentMatchVM> RoundTwoMatches
        {
            get => _roundTwoMatches;
            set
            {
                if (_roundTwoMatches == value) return;
                _roundTwoMatches = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public MBBindingList<DikozemieTournamentMatchVM> RoundThreeMatches
        {
            get => _roundThreeMatches;
            set
            {
                if (_roundThreeMatches == value) return;
                _roundThreeMatches = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public MBBindingList<DikozemieTournamentMatchVM> RoundFourMatches
        {
            get => _roundFourMatches;
            set
            {
                if (_roundFourMatches == value) return;
                _roundFourMatches = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }
}
