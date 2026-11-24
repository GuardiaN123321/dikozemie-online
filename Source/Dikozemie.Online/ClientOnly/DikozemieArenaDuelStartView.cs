using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace Dikozemie.Online
{
    // Server-side LookDirection is not the local player's camera. Native
    // HandleInputs initializes CameraBearing from the original spawn frame,
    // then MissionMainAgentController writes that bearing back to the player.
    // Align after HandleInputs, immediately before the native camera update.
    public sealed class DikozemieArenaDuelStartView : MissionView
    {
        // 0.5.134: 10 m missed the team formations (two rows, 2.75 m apart, a
        // mounted spacing wider still): on 2026-11-20 no 2x2 start aligned the
        // camera, which stayed on the wall or looked straight down (customer
        // 2026-11-19: «странности ещё замечаю в самом начале»).
        private const float StartPositionTolerance = 25f;
        private const float TeleportDistance = 6f;
        private Agent _orientedPlayer;
        private Vec3 _lastPosition;
        private bool _skipTraced;

        public override bool UpdateOverridenCamera(float dt)
        {
            Agent player = Mission?.MainAgent;
            if (player == null || !player.IsActive() || !player.IsHuman ||
                player.Controller != AgentControllerType.Player ||
                player.Team == null ||
                (player.Team.Side != BattleSideEnum.Attacker &&
                 player.Team.Side != BattleSideEnum.Defender) ||
                MissionScreen?.CombatCamera == null ||
                MissionScreen.IsPhotoModeEnabled ||
                // boarding moves fighters across the decks: never aim there
                DikozemieArenaSceneRules.IsNavalBoardingScene(
                    MultiplayerOptions.OptionType.Map.GetStrValue()))
            {
                return false;
            }

            // A team battle waits for players at one gate and may then move
            // the player to the other one: the camera kept its old direction
            // and looked at the wall (2026-10-31). Such a jump aims it again.
            Vec3 moved = player.Position - _lastPosition;
            moved.z = 0f;
            bool teleported =
                moved.LengthSquared > TeleportDistance * TeleportDistance;
            _lastPosition = player.Position;
            if (ReferenceEquals(player, _orientedPlayer) && !teleported)
            {
                return false;
            }

            DikozemieArenaSpawnLayout layout =
                DikozemieArenaSpawnLayouts.Resolve(
                    MultiplayerOptions.OptionType.Map.GetStrValue());
            Vec3 attackerGate = DikozemieArenaSpawnLayouts.BuildFormation(
                layout, true, 1, layout.FootSpacing)[0];
            Vec3 defenderGate = DikozemieArenaSpawnLayouts.BuildFormation(
                layout, false, 1, layout.FootSpacing)[0];

            // CreatePlayer can arrive before the authoritative gate placement.
            // Do not aim from a random native spawn or grab the camera later
            // when the player is already fighting in the middle of the ring.
            if (!IsNear(player.Position, attackerGate) &&
                !IsNear(player.Position, defenderGate))
            {
                if (teleported && !_skipTraced)
                {
                    _skipTraced = true;
                    DikozemieLobbyShellIntegration.TraceButtonAction(
                        "arena-duel-start camera skipped player at " +
                        player.Position.x.ToString("0.0") + "," + player.Position.y.ToString("0.0") +
                        " gates " + attackerGate.x.ToString("0.0") + "," + attackerGate.y.ToString("0.0") +
                        " / " + defenderGate.x.ToString("0.0") + "," + defenderGate.y.ToString("0.0"));
                }
                return false;
            }

            Vec3 direction = ((attackerGate + defenderGate) * 0.5f) -
                player.Position;
            direction.z = 0f;
            if (direction.Normalize() <= 0.001f)
            {
                return false;
            }

            // Camera looks along -u; keep a level horizon and preserve its
            // position. Native UpdateCamera still computes the normal first/
            // third-person position this frame. No custom camera, tracking,
            // interpolation, movement injection or permanent mouse lock.
            Mat3 rotation = new Mat3(
                Vec3.CrossProduct(direction, Vec3.Up),
                Vec3.Up,
                -direction);
            MatrixFrame frame = new MatrixFrame(
                rotation,
                MissionScreen.CombatCamera.Frame.origin);
            player.LookDirection = direction;
            if (player.MountAgent != null)
            {
                player.MountAgent.LookDirection = direction;
            }
            MissionScreen.UpdateFreeCamera(frame);
            _orientedPlayer = player;
            _skipTraced = false;
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "arena-duel-start camera aligned player=" + player.Index +
                " side=" + player.Team.Side);

            // Let Bannerlord render its normal combat camera. A new player
            // agent after Next Battle is aligned once; subsequent frames are
            // completely untouched, including mouse turns during combat.
            return false;
        }

        private static bool IsNear(Vec3 position, Vec3 gate)
        {
            Vec3 offset = position - gate;
            offset.z = 0f;
            return offset.LengthSquared <=
                StartPositionTolerance * StartPositionTolerance;
        }

        public override void OnMissionScreenFinalize()
        {
            _orientedPlayer = null;
            base.OnMissionScreenFinalize();
        }
    }
}
