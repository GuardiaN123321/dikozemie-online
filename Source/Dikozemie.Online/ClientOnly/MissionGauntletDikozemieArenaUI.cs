using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    public sealed class MissionGauntletDikozemieArenaUI : MissionView
    {
        private DikozemieArenaVM _viewModel;
        private GauntletLayer _layer;
        private SpriteCategory _hudCategory;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            ViewOrderPriority = 15;
            var client = Mission.GetMissionBehavior<MissionMultiplayerDikozemieArenaClient>();
            _viewModel = new DikozemieArenaVM(client) { IsEnabled = true };
            _viewModel.DialogueVisibilityChanged += HandleDialogueVisibilityChanged;
            _layer = new GauntletLayer("DikozemieArena", ViewOrderPriority);
            _layer.LoadMovie("DikozemieArena", _viewModel);

            // Bannerlord's multiplayer HUD atlas is registered as
            // ui_mpmission. Indexing the non-existent ui_mphud key crashed
            // the client as soon as mp_duel_001 finished loading.
            _hudCategory = UIResourceManager.LoadSpriteCategory("ui_mpmission");
            MissionScreen.AddLayer(_layer);

        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            _viewModel?.Tick(dt);
            if (Mission != null && Mission.CurrentTime > 5f)
            {
                DikozemieSoundProbe.Run(Mission.SceneName);
            }
            // The layer has no focus while the camera follows other fighters,
            // so its own input never saw F10 (2026-11-04: the button and the
            // key both did nothing). Read the key globally.
            if (_viewModel?.IsSpectatorNoticeVisible == true &&
                (Input.IsKeyReleased(InputKey.F10) ||
                 _layer?.Input?.IsKeyReleased(InputKey.F10) == true))
            {
                _viewModel.ExecuteFinishBattle();
            }
            // «Начать с ботом» while waiting for people: F9 or the button.
            if (_viewModel?.IsWaitingPanelVisible == true &&
                (Input.IsKeyReleased(InputKey.F9) ||
                 _layer?.Input?.IsKeyReleased(InputKey.F9) == true))
            {
                _viewModel.ExecuteStartWithBots();
            }
            SyncCursor();
        }

        // The result screen has buttons and switches: it takes the focus and
        // shows the cursor while it is open, and gives both back after. The
        // waiting panel shows the cursor for its button but takes only the
        // mouse: the keyboard still walks the hero.
        private enum CursorMode
        {
            None,
            Waiting,
            Result
        }

        private CursorMode _cursorMode;

        private void SyncCursor()
        {
            if (_layer == null || _viewModel == null || _viewModel.IsDialogueVisible)
            {
                return;
            }
            CursorMode wanted = _viewModel.IsResultVisible
                ? CursorMode.Result
                : _viewModel.IsWaitingPanelVisible
                    ? CursorMode.Waiting
                    : CursorMode.None;
            if (wanted == _cursorMode)
            {
                return;
            }
            _cursorMode = wanted;
            if (wanted == CursorMode.None)
            {
                _layer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(_layer);
                _layer.InputRestrictions.ResetInputRestrictions();
                return;
            }
            _layer.IsFocusLayer = true;
            ScreenManager.TrySetFocus(_layer);
            _layer.InputRestrictions.SetInputRestrictions(
                isMouseVisible: true,
                mask: wanted == CursorMode.Result
                    ? InputUsageMask.All
                    : InputUsageMask.Mouse);
        }

        public override void OnMissionScreenFinalize()
        {
            if (_layer != null)
            {
                _layer.InputRestrictions.ResetInputRestrictions();
                ScreenManager.TryLoseFocus(_layer);
                MissionScreen.RemoveLayer(_layer);
            }
            _hudCategory?.Unload();
            if (_viewModel != null)
            {
                _viewModel.DialogueVisibilityChanged -= HandleDialogueVisibilityChanged;
                _viewModel.OnFinalize();
            }
            _viewModel = null;
            _layer = null;
            base.OnMissionScreenFinalize();
        }

        private void HandleDialogueVisibilityChanged(bool visible)
        {
            if (_layer == null)
            {
                return;
            }

            if (visible)
            {
                _layer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_layer);
                _layer.InputRestrictions.SetInputRestrictions(
                    isMouseVisible: true,
                    mask: InputUsageMask.All);
            }
            else
            {
                _layer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(_layer);
                _layer.InputRestrictions.ResetInputRestrictions();
            }
        }
    }
}
