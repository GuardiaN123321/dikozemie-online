using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    public sealed class DikozemieArenaSpawnFrameBehavior : SpawnFrameBehaviorBase
    {
        public override void Initialize()
        {
            base.Initialize();
            string scene = MultiplayerOptions.OptionType.Map.GetStrValue();
            if (DikozemieArenaSceneRules.IsNavalBoardingScene(scene))
            {
                // The open-sea scene owns deck bootstrap frames. Replacing
                // them with arena-only sp_arena points would put sailors at
                // unrelated dry-land tournament coordinates.
                MissionMultiplayerDikozemieArena.Trace(
                    "naval scene retaining authored multiplayer spawn points");
                return;
            }
            // Native arena scenes also contain generic "spawnpoint" entities
            // outside the fighting ring.  Prefer the dedicated arena points;
            // otherwise a player can appear beyond the mission boundary and
            // lose to the retreat countdown before the duel even begins.
            var arenaSpawnPoints = Mission.Current.Scene
                .FindEntitiesWithTag("sp_arena")
                .ToList();
            if (arenaSpawnPoints.Any())
            {
                SpawnPoints = arenaSpawnPoints;
            }
        }

        public override MatrixFrame GetSpawnFrame(Team team, bool hasMount, bool isInitialSpawn)
        {
            if (DikozemieArenaSceneRules.IsNavalBoardingScene(
                    MultiplayerOptions.OptionType.Map.GetStrValue()))
            {
                bool attackerTeam = team == Mission.Current?.AttackerTeam;
                DikozemieNavalBoardingBehavior boarding =
                    DikozemieNavalBoardingBehavior.Active;
                DikozemieNavalBoardingLayout navalLayout =
                    boarding?.CurrentLayout ??
                    DikozemieNavalBoardingLayout.Resolve(
                        (int)DikozemieNavalShipClass.Light);
                MatrixFrame deckFrame = MatrixFrame.Identity;
                // Use the measured support surface, not the layout's nominal
                // visual height. The native agent is built at this frame and
                // otherwise gets projected onto the seabed before we can move
                // it onto the runtime deck.
                deckFrame.origin = boarding?.GetDeckPosition(
                    attackerTeam,
                    0f,
                    attackerTeam ? 6f : -6f) ??
                    navalLayout.GetStartDeckCenter(attackerTeam);
                deckFrame.rotation.f = attackerTeam
                    ? new Vec3(1f, 0f, 0f)
                    : new Vec3(-1f, 0f, 0f);
                return deckFrame;
            }
            var spawnPoints = SpawnPoints?.ToList();
            if (spawnPoints != null && spawnPoints.Count > 0)
            {
                return GetSpawnFrameFromSpawnPoints(
                    spawnPoints,
                    team,
                    hasMount);
            }

            // Open-field scenes do not necessarily author the multiplayer
            // spawnpoint entities expected by SpawnFrameBehaviorBase.  The
            // arena immediately arranges every combatant with the configured
            // wall-safe layout after Mission.SpawnAgent, so use the matching
            // gate as a safe bootstrap frame instead of indexing an empty
            // native list forever.
            DikozemieArenaSpawnLayout layout =
                DikozemieArenaSpawnLayouts.Resolve(
                    MultiplayerOptions.OptionType.Map.GetStrValue());
            bool attacker = team == Mission.Current?.AttackerTeam;
            MatrixFrame fallback = MatrixFrame.Identity;
            fallback.origin = attacker
                ? layout.AttackerGate
                : layout.DefenderGate;
            return fallback;
        }
    }
}
