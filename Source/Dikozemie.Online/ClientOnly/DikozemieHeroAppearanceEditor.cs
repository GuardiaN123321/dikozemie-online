using System;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.GauntletUI.BodyGenerator;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;

namespace Dikozemie.Online
{
    /// <summary>
    /// Uses the same lifecycle as Bannerlord's stock Gauntlet body-generator
    /// screen. The earlier inline layers showed the controls but never
    /// activated the native render scene, leaving the character invisible.
    /// </summary>
    internal sealed class DikozemieHeroFacegenScreen : ScreenBase
    {
        private sealed class BackdropVM : ViewModel
        {
        }

        private readonly BodyGeneratorView _editor;
        private readonly BackdropVM _backdropViewModel = new BackdropVM();
        private GauntletLayer _backdropLayer;
        private GauntletMovieIdentifier _backdropMovie;
        private bool _editorFinalized;

        internal DikozemieHeroFacegenScreen(BodyGeneratorView editor)
        {
            _editor = editor;
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            Game.Current.GameStateManager.RegisterActiveStateDisableRequest(this);

            _backdropLayer = new GauntletLayer("DikozemieHeroFacegenBackdrop", 1);
            _backdropMovie = _backdropLayer.LoadMovie(
                "DikozemieHeroFacegenBackdrop",
                _backdropViewModel);
            SetLayerOrder(_editor.GauntletLayer, 2);
            NarrowNativePanel();
            AddLayer(_backdropLayer);
            AddLayer(_editor.GauntletLayer);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            if (!HasLayer(_editor.SceneLayer))
            {
                AddLayer(_editor.SceneLayer);
            }
            ScreenManager.TrySetFocus(_editor.GauntletLayer);
        }

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            _editor.OnTick(dt);
        }

        protected override void OnDeactivate()
        {
            FinalizeEditorOnce();
            base.OnDeactivate();
        }

        protected override void OnFinalize()
        {
            FinalizeEditorOnce();
            if (_backdropMovie != null)
            {
                _backdropLayer.ReleaseMovie(_backdropMovie);
                _backdropMovie = null;
            }
            _backdropViewModel.OnFinalize();
            Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(this);
            base.OnFinalize();
        }

        private void NarrowNativePanel()
        {
            Widget root = _editor.GauntletLayer
                .GetMovieIdentifier("FaceGen")
                ?.Movie
                ?.RootWidget;
            if (root == null)
            {
                return;
            }
            foreach (Widget widget in
                     root.GetAllChildrenOfTypeRecursive<Widget>())
            {
                if (Math.Abs(widget.SuggestedWidth - 694f) < 0.5f)
                {
                    widget.SuggestedWidth = 640f;
                    break;
                }
            }
        }

        private void FinalizeEditorOnce()
        {
            if (_editorFinalized)
            {
                return;
            }
            _editorFinalized = true;
            _editor.OnFinalize();
        }

        private static void SetLayerOrder(ScreenLayer layer, int order)
        {
            PropertyInfo orderProperty = typeof(InputRestrictions).GetProperty(
                "Order",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            MethodInfo setter = orderProperty?.GetSetMethod(true);
            if (setter == null)
            {
                throw new MissingMethodException(
                    typeof(InputRestrictions).FullName,
                    "set_Order");
            }
            setter.Invoke(layer.InputRestrictions, new object[] { order });
        }
    }

    /// <summary>
    /// Opens Bannerlord's native FaceGen with its stock screen lifecycle while
    /// keeping the multiplayer lobby alive beneath it.
    /// </summary>
    internal static class DikozemieHeroAppearanceEditor
    {
        private static BasicCharacterObject _editingCharacter;
        private static BodyGeneratorView _editor;
        private static DikozemieHeroFacegenScreen _facegenScreen;
        private static ScreenBase _hostScreen;
        private static bool _closePending;
        private static bool _savePending;
        private static int _navigationTab = -1;
        private static bool _exitLobbyPending;

        internal static bool IsOpen => _editor != null;

        internal static void Open()
        {
            if (_editor != null)
            {
                return;
            }

            BasicCharacterObject fallback = MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                "mp_heavy_infantry_empire_troop");
            BasicCharacterObject character = DikozemieHeroProfile.IsCreated
                ? DikozemieHeroProfile.ResolveCharacter(fallback)
                : DikozemieHeroProfile.ResolveCreationPreviewCharacter(fallback);
            ScreenBase hostScreen = ScreenManager.TopScreen;
            if (character == null || hostScreen == null)
            {
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "hero-facegen rejected: character or host missing");
                Console.WriteLine("DikozemieOnline: inline FaceGen could not find character or multiplayer host screen");
                return;
            }

            DikozemieHeroProfileState profile = DikozemieHeroProfile.Snapshot;
            if (BodyProperties.FromString(profile.BodyProperties, out BodyProperties body))
            {
                character.UpdatePlayerCharacterBodyProperties(
                    body,
                    profile.Race,
                    profile.IsFemale);
            }

            try
            {
                _editingCharacter = character;
                _hostScreen = hostScreen;
                _closePending = false;
                _savePending = false;
                _navigationTab = -1;
                _exitLobbyPending = false;

                BodyGeneratorView editor = new BodyGeneratorView(
                    new ControlCharacterCreationStage(RequestSaveAndClose),
                    GameTexts.FindText("str_done", null),
                    new ControlCharacterCreationStage(RequestCancelAndClose),
                    GameTexts.FindText("str_cancel", null),
                    character,
                    openedFromMultiplayer: true,
                    filter: null);
                _editor = editor;
                _facegenScreen = new DikozemieHeroFacegenScreen(editor);
                DikozemieHeroMenuController.Hide();
                ScreenManager.PushScreen(_facegenScreen);
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "hero-facegen opened native screen");
                Console.WriteLine(
                    "DikozemieOnline: native FaceGen opened over " +
                    hostScreen.GetType().Name);
            }
            catch (Exception error)
            {
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "hero-facegen failed: " + error.GetType().Name + " " +
                    error.Message);
                Console.WriteLine("DikozemieOnline: inline FaceGen failed to open: " + error);
                CloseNow(save: false, reopenHeroMenu: true);
            }
        }

        internal static void Tick(float dt)
        {
            BodyGeneratorView editor = _editor;
            if (editor == null)
            {
                return;
            }

            if (_hostScreen == null || _hostScreen.IsFinalized ||
                _facegenScreen == null || _facegenScreen.IsFinalized)
            {
                CloseNow(save: false, reopenHeroMenu: false);
                return;
            }

            if (_navigationTab >= 0)
            {
                int tabIndex = _navigationTab;
                CloseNow(save: false, reopenHeroMenu: false);
                DikozemieLobbyShellIntegration.SelectTopTab(tabIndex);
                return;
            }

            if (_exitLobbyPending)
            {
                CloseNow(save: false, reopenHeroMenu: false);
                DikozemieLobbyShellIntegration.TryHandleLobbyExitButtonClick();
                return;
            }

            if (_closePending)
            {
                CloseNow(_savePending, reopenHeroMenu: true);
            }
        }

        private static void RequestSaveAndClose()
        {
            _savePending = true;
            _closePending = true;
        }

        private static void RequestCancelAndClose()
        {
            _savePending = false;
            _closePending = true;
        }

        internal static void RequestNavigateToTab(int tabIndex)
        {
            if (_editor != null)
            {
                _navigationTab = tabIndex;
            }
        }

        internal static void RequestLobbyExit()
        {
            if (_editor != null)
            {
                _exitLobbyPending = true;
            }
        }

        private static void CloseNow(bool save, bool reopenHeroMenu)
        {
            BodyGeneratorView editor = _editor;
            BasicCharacterObject character = _editingCharacter;
            ScreenBase hostScreen = _hostScreen;
            DikozemieHeroFacegenScreen facegenScreen = _facegenScreen;

            _editor = null;
            _editingCharacter = null;
            _facegenScreen = null;
            _hostScreen = null;
            _closePending = false;
            _savePending = false;
            _navigationTab = -1;
            _exitLobbyPending = false;
            DikozemieHeroFacegenChromeController.Hide();

            if (save && character != null)
            {
                DikozemieHeroProfile.SetAppearance(
                    character.GetBodyProperties(character.Equipment).ToString(),
                    character.IsFemale,
                    character.Race);
                DikozemieHeroProfile.MarkAppearanceConfirmed();
                Console.WriteLine("DikozemieOnline: hero appearance saved from inline native FaceGen");
            }

            if (facegenScreen != null &&
                ReferenceEquals(ScreenManager.TopScreen, facegenScreen))
            {
                ScreenManager.PopScreen();
            }

            if (reopenHeroMenu && hostScreen != null && !hostScreen.IsFinalized)
            {
                DikozemieHeroMenuController.Show();
            }
        }

        internal static void AbandonForApplicationShutdown()
        {
            DikozemieHeroFacegenChromeController.AbandonForApplicationShutdown();
            CloseNow(save: false, reopenHeroMenu: false);
        }
    }
}
