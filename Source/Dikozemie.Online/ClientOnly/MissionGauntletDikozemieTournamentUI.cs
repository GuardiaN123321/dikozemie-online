using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;

namespace Dikozemie.Online
{
    public sealed class MissionGauntletDikozemieTournamentUI : MissionView
    {
        private DikozemieTournamentVM _viewModel;
        private GauntletLayer _layer;
        private Camera _arenaCamera;
        private bool _ownsArenaCamera;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            DikozemieLocalArenaTournamentController controller =
                Mission.GetMissionBehavior<
                    DikozemieLocalArenaTournamentController>();
            if (controller == null)
            {
                return;
            }

            ViewOrderPriority = 40;
            _viewModel = new DikozemieTournamentVM(controller);
            _viewModel.VisibilityChanged += HandleVisibilityChanged;
            _layer = new GauntletLayer(
                "DikozemieTournament",
                ViewOrderPriority);
            _layer.LoadMovie("DikozemieTournament", _viewModel);
            MissionScreen.AddLayer(_layer);
            ApplyPresentationVisibility(_viewModel.IsVisible);
        }

        public override void OnMissionScreenFinalize()
        {
            if (MissionScreen.CustomCamera == _arenaCamera)
            {
                MissionScreen.CustomCamera = null;
            }
            if (_layer != null)
            {
                _layer.InputRestrictions.ResetInputRestrictions();
                ScreenManager.TryLoseFocus(_layer);
                MissionScreen.RemoveLayer(_layer);
            }
            if (_viewModel != null)
            {
                _viewModel.VisibilityChanged -= HandleVisibilityChanged;
                _viewModel.OnFinalize();
            }
            if (_ownsArenaCamera)
            {
                _arenaCamera?.ReleaseCamera();
            }
            _arenaCamera = null;
            _ownsArenaCamera = false;
            _viewModel = null;
            _layer = null;
            base.OnMissionScreenFinalize();
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (_viewModel != null &&
                _viewModel.IsVisible &&
                _arenaCamera != null)
            {
                // The native multiplayer controller updates CombatCamera every
                // frame even while a custom Gauntlet screen is focused.  Keep
                // the tournament spectator shot authoritative until the player
                // enters the next fight.
                ApplyArenaCameraPose();
                if (MissionScreen.CustomCamera != _arenaCamera)
                {
                    MissionScreen.CustomCamera = _arenaCamera;
                }
            }
        }

        private void HandleVisibilityChanged(bool visible)
        {
            ApplyPresentationVisibility(visible);
        }

        private void ApplyPresentationVisibility(bool visible)
        {
            if (_layer == null)
            {
                return;
            }

            if (visible)
            {
                EnsureArenaCamera();
                MissionScreen.CustomCamera = _arenaCamera;
                _layer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_layer);
                _layer.InputRestrictions.SetInputRestrictions(
                    isMouseVisible: true,
                    mask: InputUsageMask.All);
            }
            else
            {
                if (MissionScreen.CustomCamera == _arenaCamera)
                {
                    MissionScreen.CustomCamera = null;
                }
                _layer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(_layer);
                _layer.InputRestrictions.ResetInputRestrictions();
            }
        }

        private void EnsureArenaCamera()
        {
            if (_arenaCamera != null)
            {
                return;
            }

            // arena_empire_a is long from west to east.  Place the spectator
            // camera inside the western end of the fighting floor so the arena
            // and both rows of stands remain visible on ultrawide displays.
            _arenaCamera = MissionScreen.CombatCamera;
            if (_arenaCamera == null)
            {
                _arenaCamera = Camera.CreateCamera();
                _ownsArenaCamera = true;
            }
            ApplyArenaCameraPose();
        }

        private void ApplyArenaCameraPose()
        {
            if (_arenaCamera == null)
            {
                return;
            }

            Vec3 position = new Vec3(350f, 438f, 10f);
            Vec3 target = new Vec3(384f, 438f, 1.5f);
            float aspectRatio =
                TaleWorlds.Engine.Screen.RealScreenResolutionWidth /
                TaleWorlds.Engine.Screen.RealScreenResolutionHeight;
            _arenaCamera.SetFovVertical(
                0.95f,
                aspectRatio,
                0.05f,
                2000f);
            _arenaCamera.LookAt(position, target, Vec3.Up);
        }
    }
}
