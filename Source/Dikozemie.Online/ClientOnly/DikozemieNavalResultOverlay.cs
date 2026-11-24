using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Dikozemie.Online
{
    internal sealed class DikozemieNavalResultSummary
    {
        internal DikozemieNavalResultSummary(
            bool victory,
            string battleName,
            int personalKills,
            int experienceEarned,
            int ratingEarned,
            int goldEarned,
            int totalRating,
            int totalGold,
            string experienceSummary)
        {
            Victory = victory;
            BattleName = battleName ?? string.Empty;
            PersonalKills = Math.Max(0, personalKills);
            ExperienceEarned = Math.Max(0, experienceEarned);
            RatingEarned = ratingEarned;
            GoldEarned = goldEarned;
            TotalRating = Math.Max(0, totalRating);
            TotalGold = Math.Max(0, totalGold);
            ExperienceSummary = experienceSummary ?? string.Empty;
        }

        internal bool Victory { get; }
        internal string BattleName { get; }
        internal int PersonalKills { get; }
        internal int ExperienceEarned { get; }
        internal int RatingEarned { get; }
        internal int GoldEarned { get; }
        internal int TotalRating { get; }
        internal int TotalGold { get; }
        internal string ExperienceSummary { get; }
    }

    internal sealed class DikozemieNavalResultVM : ViewModel
    {
        private readonly DateTimeOffset _shownAt;
        private bool _isExperienceVisible;
        private bool _isRatingVisible;
        private bool _isGoldVisible;
        private bool _isContinueVisible;
        private int _pulseToken;

        internal DikozemieNavalResultVM(
            DikozemieNavalResultSummary summary,
            DateTimeOffset shownAt)
        {
            if (summary == null)
            {
                throw new ArgumentNullException(nameof(summary));
            }

            _shownAt = shownAt;
            Title = summary.Victory
                ? "ПОБЕДА В МОРСКОМ БОЮ"
                : "ПОРАЖЕНИЕ В МОРСКОМ БОЮ";
            BattleName = string.IsNullOrWhiteSpace(summary.BattleName)
                ? "АБОРДАЖНОЕ СРАЖЕНИЕ"
                : summary.BattleName.ToUpperInvariant();
            ExperienceReveal = "+" + summary.ExperienceEarned + " ОПЫТА";
            RatingReveal = FormatSigned(summary.RatingEarned, " РЕЙТИНГА");
            GoldReveal = FormatSigned(summary.GoldEarned, " МОНЕТ");
            KillSummary = "Личных убийств: " + summary.PersonalKills +
                " · за каждое начислено ×2 рейтинга и монет";
            Totals = "Теперь у героя: рейтинг " + summary.TotalRating +
                " · монеты " + summary.TotalGold;
            Progress = summary.ExperienceSummary;
            PulseToken = 1;
        }

        internal bool ContinueRequested { get; private set; }

        internal void Tick(DateTimeOffset now)
        {
            TimeSpan elapsed = now - _shownAt;
            if (!IsExperienceVisible && elapsed >= TimeSpan.FromSeconds(0.9d))
            {
                IsExperienceVisible = true;
                PulseToken++;
            }
            if (!IsRatingVisible && elapsed >= TimeSpan.FromSeconds(2.1d))
            {
                IsRatingVisible = true;
                PulseToken++;
            }
            if (!IsGoldVisible && elapsed >= TimeSpan.FromSeconds(3.3d))
            {
                IsGoldVisible = true;
                PulseToken++;
            }
            if (!IsContinueVisible && elapsed >= TimeSpan.FromSeconds(4.2d))
            {
                IsContinueVisible = true;
            }
        }

        public void ExecuteContinue()
        {
            if (IsContinueVisible)
            {
                ContinueRequested = true;
            }
        }

        internal void ExecuteSmokeContinue()
        {
            if (IsContinueVisible)
            {
                ContinueRequested = true;
            }
        }

        private static string FormatSigned(int value, string suffix)
        {
            return (value >= 0 ? "+" : "-") + Math.Abs(value) + suffix;
        }

        [DataSourceProperty]
        public string Title { get; }

        [DataSourceProperty]
        public string BattleName { get; }

        [DataSourceProperty]
        public string ExperienceReveal { get; }

        [DataSourceProperty]
        public string RatingReveal { get; }

        [DataSourceProperty]
        public string GoldReveal { get; }

        [DataSourceProperty]
        public string KillSummary { get; }

        [DataSourceProperty]
        public string Totals { get; }

        [DataSourceProperty]
        public string Progress { get; }

        [DataSourceProperty]
        public bool IsExperienceVisible
        {
            get => _isExperienceVisible;
            private set
            {
                if (_isExperienceVisible == value) return;
                _isExperienceVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsRatingVisible
        {
            get => _isRatingVisible;
            private set
            {
                if (_isRatingVisible == value) return;
                _isRatingVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsGoldVisible
        {
            get => _isGoldVisible;
            private set
            {
                if (_isGoldVisible == value) return;
                _isGoldVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsContinueVisible
        {
            get => _isContinueVisible;
            private set
            {
                if (_isContinueVisible == value) return;
                _isContinueVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public int PulseToken
        {
            get => _pulseToken;
            private set
            {
                if (_pulseToken == value) return;
                _pulseToken = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    internal sealed class DikozemieNavalResultOverlayLayer : GlobalLayer,
        IDisposable
    {
        private readonly GauntletLayer _gauntletLayer;
        private readonly DikozemieNavalResultVM _viewModel;
        private GauntletMovieIdentifier _movie;

        internal DikozemieNavalResultOverlayLayer(
            DikozemieNavalResultSummary summary,
            DateTimeOffset shownAt)
        {
            _viewModel = new DikozemieNavalResultVM(summary, shownAt);
            _gauntletLayer = new GauntletLayer(
                "DikozemieNavalResult",
                25000);
            _movie = _gauntletLayer.LoadMovie("DikozemieNavalResult", _viewModel);
            _gauntletLayer.IsFocusLayer = true;
            _gauntletLayer.InputRestrictions.SetInputRestrictions(
                isMouseVisible: true,
                mask: InputUsageMask.All);
            Layer = _gauntletLayer;
        }

        internal bool Tick(DateTimeOffset now)
        {
            _viewModel.Tick(now);
            return _viewModel.ContinueRequested;
        }

        internal void Focus()
        {
            ScreenManager.TrySetFocus(_gauntletLayer);
        }

        internal void ContinueForSmoke()
        {
            _viewModel.ExecuteSmokeContinue();
        }

        public void Dispose()
        {
            _gauntletLayer.InputRestrictions.ResetInputRestrictions();
            _gauntletLayer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus(_gauntletLayer);
            if (_movie != null)
            {
                _gauntletLayer.ReleaseMovie(_movie);
                _movie = null;
            }
            _viewModel.OnFinalize();
        }
    }

    internal static class DikozemieNavalResultOverlayController
    {
        private static DikozemieNavalResultOverlayLayer _layer;

        internal static void Show(
            DikozemieNavalResultSummary summary,
            DateTimeOffset shownAt)
        {
            if (_layer != null || summary == null)
            {
                return;
            }

            _layer = new DikozemieNavalResultOverlayLayer(summary, shownAt);
            ScreenManager.AddGlobalLayer(_layer, true);
            _layer.Focus();
        }

        internal static bool Tick(DateTimeOffset now)
        {
            return _layer?.Tick(now) == true;
        }

        internal static void ContinueForSmoke()
        {
            _layer?.ContinueForSmoke();
        }

        internal static void Hide()
        {
            DikozemieNavalResultOverlayLayer layer = _layer;
            if (layer == null)
            {
                return;
            }

            _layer = null;
            // Bannerlord 1.5.7 finalizes a removed global layer, so the movie
            // is released first (the native views do the same).
            try
            {
                layer.Dispose();
            }
            finally
            {
                ScreenManager.RemoveGlobalLayer(layer);
            }
        }
    }
}
