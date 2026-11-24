using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    internal sealed class DikozemieHeroFacegenChromeVM : ViewModel
    {
        public void ExecuteLobbyExit()
        {
            DikozemieHeroAppearanceEditor.RequestLobbyExit();
        }
    }

    internal sealed class DikozemieHeroFacegenChromeLayer : GlobalLayer, IDisposable
    {
        private readonly GauntletLayer _gauntletLayer;
        private readonly DikozemieHeroFacegenChromeVM _viewModel;
        private GauntletMovieIdentifier _movie;

        internal DikozemieHeroFacegenChromeLayer()
        {
            _viewModel = new DikozemieHeroFacegenChromeVM();
            _gauntletLayer = new GauntletLayer(
                "DikozemieHeroFacegenChrome",
                10012);
            _movie = _gauntletLayer.LoadMovie(
                "DikozemieHeroFacegenChrome",
                _viewModel);
            _gauntletLayer.InputRestrictions.SetInputRestrictions(
                true,
                InputUsageMask.Mouse | InputUsageMask.Keyboardkeys);
            Layer = _gauntletLayer;
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

    internal static class DikozemieHeroFacegenChromeController
    {
        private static DikozemieHeroFacegenChromeLayer _layer;

        internal static void Show()
        {
            if (_layer != null)
            {
                return;
            }

            try
            {
                _layer = new DikozemieHeroFacegenChromeLayer();
                ScreenManager.AddGlobalLayer(_layer, false);
            }
            catch (Exception error)
            {
                _layer = null;
                Console.WriteLine(
                    "DikozemieOnline: FaceGen header failed: " + error);
            }
        }

        internal static void Hide()
        {
            DikozemieHeroFacegenChromeLayer layer = _layer;
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
