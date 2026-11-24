using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace Dikozemie.Online
{
    internal sealed class DikozemiePvpCancelOverlayVM : ViewModel
    {
        private string _buttonText;

        internal DikozemiePvpCancelOverlayVM()
        {
            _buttonText = "ОТМЕНИТЬ • 00:00";
        }

        [DataSourceProperty]
        public string ButtonText
        {
            get => _buttonText;
            set
            {
                if (string.Equals(_buttonText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _buttonText = value ?? string.Empty;
                OnPropertyChangedWithValue(_buttonText);
            }
        }

        public void ExecuteCancel()
        {
            if (DikozemiePvpSearchController.IsSearching)
            {
                DikozemiePvpSearchController.Cancel();
            }
        }
    }

    internal sealed class DikozemiePvpCancelOverlayLayer : GlobalLayer, IDisposable
    {
        private readonly GauntletLayer _gauntletLayer;
        private readonly DikozemiePvpCancelOverlayVM _viewModel;
        private GauntletMovieIdentifier _movie;
        private ButtonWidget _cancelButton;
        private bool _cancelInputArmed;

        internal DikozemiePvpCancelOverlayLayer()
        {
            _viewModel = new DikozemiePvpCancelOverlayVM();
            _gauntletLayer = new GauntletLayer("DikozemiePvpCancelOverlay", 20000);
            _movie = _gauntletLayer.LoadMovie(
                "DikozemiePvpCancelOverlay",
                _viewModel);
            Widget root = _movie?.Movie?.RootWidget;
            if (root != null)
            {
                foreach (ButtonWidget button in
                         root.GetAllChildrenOfTypeRecursive<ButtonWidget>())
                {
                    if (string.Equals(
                            button.Id,
                            "ArenaSearchCancelOverlay",
                            StringComparison.Ordinal))
                    {
                        _cancelButton = button;
                        break;
                    }
                }
            }
            _gauntletLayer.InputRestrictions.SetInputRestrictions(
                true,
                InputUsageMask.Mouse);
            Layer = _gauntletLayer;
        }

        internal void Refresh()
        {
            _viewModel.ButtonText = "ОТМЕНИТЬ • " +
                DikozemiePvpSearchController.SearchElapsedText;
        }

        internal bool PollCancelClick()
        {
            bool isMouseDown =
                _gauntletLayer.Input.IsKeyDown(InputKey.LeftMouseButton);
            bool isMousePressed =
                _gauntletLayer.Input.IsKeyPressed(InputKey.LeftMouseButton);
            bool isMouseReleased =
                _gauntletLayer.Input.IsKeyReleased(InputKey.LeftMouseButton);

            // Ignore the release that originally activated Start.  Once the
            // pointer has been idle, react on the next press as well as its
            // release; the native busy curtain may be raised between them.
            if (!_cancelInputArmed)
            {
                if (!isMouseDown && !isMousePressed)
                {
                    _cancelInputArmed = true;
                }
                return false;
            }

            if ((!isMousePressed && !isMouseReleased) || _cancelButton == null)
            {
                return false;
            }

            var rawMousePosition = _gauntletLayer.Input.GetMousePositionPixel();
            bool isInside = _cancelButton.IsPointInsideMeasuredArea(
                new System.Numerics.Vector2(
                    rawMousePosition.X,
                    rawMousePosition.Y));
            if (isInside)
            {
                _cancelInputArmed = false;
                Console.WriteLine(
                    "DikozemieOnline: cancel overlay captured pointer input");
            }
            return isInside;
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

    internal static class DikozemiePvpCancelOverlayController
    {
        private static DikozemiePvpCancelOverlayLayer _layer;

        internal static void Show()
        {
            if (_layer != null)
            {
                _layer.Refresh();
                return;
            }

            _layer = new DikozemiePvpCancelOverlayLayer();
            ScreenManager.AddGlobalLayer(_layer, false);
        }

        internal static void Tick()
        {
            if (DikozemiePvpSearchController.IsSearching)
            {
                Show();
                if (_layer?.PollCancelClick() == true)
                {
                    // Loading/busy screens can consume Gauntlet's normal
                    // command event. Reading our top layer directly keeps the
                    // same visible button authoritative in those states.
                    DikozemiePvpSearchController.Cancel();
                }
            }
            else
            {
                Hide();
            }
        }

        private static void Hide()
        {
            DikozemiePvpCancelOverlayLayer layer = _layer;
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

        internal static void AbandonForApplicationShutdown()
        {
            _layer = null;
        }
    }
}
