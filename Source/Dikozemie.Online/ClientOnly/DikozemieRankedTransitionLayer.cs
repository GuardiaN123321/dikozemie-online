using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Dikozemie.Online
{
    internal sealed class DikozemieRankedTransitionVM : ViewModel
    {
        private string _statusText;

        public DikozemieRankedTransitionVM(string statusText)
        {
            _statusText = statusText ?? string.Empty;
        }

        [DataSourceProperty]
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (string.Equals(_statusText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _statusText = value ?? string.Empty;
                OnPropertyChangedWithValue(_statusText);
            }
        }
    }

    internal sealed class DikozemieRankedTransitionLayer : GlobalLayer, IDisposable
    {
        private readonly GauntletLayer _gauntletLayer;
        private readonly DikozemieRankedTransitionVM _viewModel;
        private GauntletMovieIdentifier _movie;

        public DikozemieRankedTransitionLayer(string statusText)
        {
            _viewModel = new DikozemieRankedTransitionVM(statusText);
            _gauntletLayer = new GauntletLayer("DikozemieRankedTransition", 10000);
            _movie = _gauntletLayer.LoadMovie("DikozemieRankedTransition", _viewModel);
            _gauntletLayer.InputRestrictions.SetInputRestrictions(
                true,
                InputUsageMask.All | InputUsageMask.BlockEverythingWithoutHitTest);
            Layer = _gauntletLayer;
        }

        public void SetStatus(string statusText)
        {
            _viewModel.StatusText = statusText;
        }

        public void Dispose()
        {
            _gauntletLayer.InputRestrictions.ResetInputRestrictions();
            if (_movie != null)
            {
                _gauntletLayer.ReleaseMovie(_movie);
                _movie = null;
            }
            _viewModel.OnFinalize();
        }
    }

    internal static class DikozemieRankedTransitionController
    {
        private static DikozemieRankedTransitionLayer _layer;

        public static bool IsActive => _layer != null;

        public static void Show(string statusText)
        {
            if (_layer == null)
            {
                _layer = new DikozemieRankedTransitionLayer(statusText);
                ScreenManager.AddGlobalLayer(_layer, false);
                return;
            }

            _layer.SetStatus(statusText);
        }

        public static void Hide()
        {
            DikozemieRankedTransitionLayer layer = _layer;
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

        public static void AbandonForApplicationShutdown()
        {
            _layer = null;
        }
    }
}
